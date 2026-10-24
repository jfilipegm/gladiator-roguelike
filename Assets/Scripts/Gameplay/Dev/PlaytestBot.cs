using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Automated player for perf runs and smoke tests: steers to pickups, flees enemies, stays inside the
    /// arena, jumps/slides/orbits the camera, picks random level-up cards. Deterministic (seeded).
    /// Command line (standalone or batch): -bot  -stress [-stressCount 1000]  -botSeconds 600  -profile
    /// (the arena: -arena &lt;run id&gt;, read by MainMenu, e.g. -arena colosseum once the main menu is rebuilt with it)
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class PlaytestBot : MonoBehaviour, IPlayerInputSource
    {
        public enum Mode { Normal, Stress }

        public static PlaytestBot Active { get; private set; }
        /// <summary>Set by gameplay systems; null until they exist.</summary>
        public static IBotSensor Sensor;

        [Header("Run")]
        public Mode mode = Mode.Normal;
        [Tooltip("0 = run until stopped. When elapsed: stop the FrameStatsRecorder and quit (standalone) / log (editor).")]
        public float runSeconds = 0f;
        public int stressCount = 1000;
        public int seed = 1234;
        [Tooltip("Perf/smoke runs shouldn't end early: God mode + Timeless while the bot plays (-mortal / -timed turn them off).")]
        public bool godMode = true;
        public bool timeless = true;

        [Header("Arena")]
        [Tooltip("Fallback circle when there is no EnemyManager; otherwise the bot uses the arena's oval.")]
        public Vector3 arenaCenter = Vector3.zero;
        public float arenaRadius = 15f;
        [Tooltip("Stays this far inside the arena's oval (m).")]
        public float edgeMargin = 4f;

        [Header("Behaviour")]
        public float pickupSeekRange = 25f;
        [Tooltip("Goes for a chest it can afford within this range (m).")]
        public float chestSeekRange = 30f;
        [Tooltip("A chest not reached in this many seconds is skipped for as long (the bot must never park on one).")]
        public float chestGiveUp = 15f;
        public float threatRadius = 5f;
        public float threatWeight = 2.5f;
        [Tooltip("Caps the flee push so a goal can still steer: uncapped, ten enemies outweighed any goal and the bot kited the horde around the big pit, rarely hitting anything.")]
        public float threatMax = 1.5f;
        [Tooltip("Within this distance of a chest, gift or gem it mostly ignores threats (a player commits to the grab).")]
        public float commitRange = 4f;
        [Tooltip("With nothing to collect it heads for the enemies within this range instead of wandering (0 = wander).")]
        public float engageRange = 18f;
        public Vector2 jumpEvery = new Vector2(1.5f, 4f);
        public Vector2 slideEvery = new Vector2(2.5f, 6f);
        public Vector2 slideHold = new Vector2(0.4f, 1.1f);
        public float cameraOrbitAmount = 0.35f;
        [Tooltip("The Imperial Arena: leaves the chariot lane when a chariot will reach it within this many seconds, and doesn't step onto it then (0 = never dodges).")]
        public float chariotDodge = 1.5f;
        [Tooltip("How far ahead (m) it looks before stepping onto the lane.")]
        public float chariotLookAhead = 2f;
        [Tooltip("The Colosseum: steps off a trapdoor or lift that opens or moves within this many seconds, won't step onto one then, and walks round doors held open (0 = never dodges).")]
        public float hypogeumDodge = 2.5f;
        [Tooltip("How far ahead (m) it looks before stepping onto a door or lift.")]
        public float hypogeumLookAhead = 2f;
        [Tooltip("-evolved: start from a late run's build (4 evolutions, 3 synergies), applied on the first frame (after PlayerBuild.Start).")]
        public bool evolvedBuild;
        bool _evolvedApplied;
        [Tooltip("Seconds between [BotTrace] lines (where it is, what it goes for, gold); 0 = off. Command line: -botTrace 5.")]
        public float traceEvery = 0f;

        Transform _body;
        uint _rng;
        float _elapsed, _nextJump, _nextSlide, _slideUntil, _nextWander, _cardDelay = -1f;
        float _chestSince = -1f, _chestIgnoreUntil, _nextTrace;
        Vector3 _wanderTarget, _chestTarget, _goalPos;
        char _goal = '-';
        float _bossSeconds, _bossNear, _bossChasing; // the boss fight: how long, how long within 10 m of him, how long chasing pickups (C24)
        bool _stressRequested, _finished;
        bool _dodging;          // stepping off the chariot lane
        int _chariotDodges;
        bool _hypDodging;       // stepping off a trapdoor or lift
        int _hypogeumDodges;

        public float Elapsed => _elapsed;
        public bool Finished => _finished;

        /// <summary>The body the bot steers (the player calls this when it adopts the bot as input).</summary>
        public void Bind(Transform body) => _body = body;

        bool _prevGod, _prevTimeless;

        void OnEnable()
        {
            Active = this;
            _prevGod = DevSettings.GodMode; _prevTimeless = DevSettings.Timeless;
            if (godMode) DevSettings.GodMode = true;
            if (timeless) DevSettings.Timeless = true;
            _rng = (uint)Mathf.Max(1, seed);
            _nextJump = Rand(jumpEvery);
            _nextSlide = Rand(slideEvery);
        }

        // Fields can be set right after AddComponent (bootstrap), i.e. after OnEnable: apply them again.
        void Start()
        {
            DevSettings.GodMode = godMode || _prevGod;
            DevSettings.Timeless = timeless || _prevTimeless;
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            DevSettings.GodMode = _prevGod; DevSettings.Timeless = _prevTimeless;
        }

        void Update()
        {
            if (_finished) return;
            _elapsed += UnityEngine.Time.unscaledDeltaTime;
            if (evolvedBuild && !_evolvedApplied && Player.Main != null) { _evolvedApplied = true; PlaytestBotBootstrap.EvolvedBuild(); }

            if (mode == Mode.Stress && !_stressRequested && Sensor != null && _elapsed > 1f)
            {
                int n = Sensor.RequestStressSpawn(stressCount);
                _stressRequested = true;
                Debug.Log($"[PlaytestBot] stress spawn requested: {n}");
            }

            // Level-up cards: pick after a short (unscaled) beat, like a human would.
            if (Sensor != null && Sensor.LevelUpPending(out int cards) && cards > 0)
            {
                if (_cardDelay < 0f) _cardDelay = 0.25f;
                _cardDelay -= UnityEngine.Time.unscaledDeltaTime;
                if (_cardDelay <= 0f)
                {
                    Sensor.ChooseCard((int)(Next() % (uint)cards));
                    _cardDelay = -1f;
                }
            }

            var boss = BossController.Active;
            float dt = UnityEngine.Time.deltaTime;
            if (boss != null && boss.Alive && _body != null && dt > 0f && boss.CurrentState != BossController.State.Entering)
            {
                _bossSeconds += dt;
                Vector3 d = boss.Position - _body.position;
                d.y = 0f;
                if (d.sqrMagnitude < 100f) _bossNear += dt;
                if (_goal == 'P') _bossChasing += dt;
            }

            if (traceEvery > 0f && _elapsed >= _nextTrace && _body != null) Trace();
            if (runSeconds > 0f && _elapsed >= runSeconds) Finish();
        }

        /// <summary>What the bot ended up with (perf runs record which upgrades were exercised).</summary>
        static readonly int HandlerTypeId = GameInfo.StableId("beasthandler");

        void LogBuild()
        {
            var player = Player.Main;
            var build = player != null ? player.GetComponent<PlayerBuild>() : null;
            if (build == null) return;
            var sb = new System.Text.StringBuilder("[PlaytestBot] build:");
            for (int i = 0; i < build.State.OwnedCount; i++)
            {
                var o = build.State.GetOwned(i);
                sb.Append(' ').Append(o.Def.id).Append(" L").Append(o.Level);
            }
            var run = RunState.Current;
            if (run != null) sb.Append(" | level ").Append(run.Level).Append(" gold ").Append(run.Gold).Append(" kills ").Append(run.Kills);
            var props = Interactables.Instance;
            if (props != null) sb.Append(" chests ").Append(props.ChestsOpened).Append(" altars ").Append(props.AltarsUsed);
            var gifts = CrowdGifts.Instance;
            if (gifts != null) sb.Append(" | favor gifts ").Append(gifts.Collected).Append('/').Append(gifts.Thrown);
            var master = ArenaMaster.Instance;
            if (master != null) sb.Append(" whims ").Append(master.WhimsFired).Append(" challenges won ").Append(master.ChallengesWon);
            var boss = BossController.Active;
            if (boss != null)
                sb.Append(" | boss ").Append(boss.Defeated ? "DEFEATED" : "alive").Append(" hp ").Append((boss.Hp01 * 100f).ToString("0")).Append("% phase ")
                  .Append(boss.Phase).Append(" attacks ").Append(boss.AttacksDone).Append(" guardWaves ").Append(boss.GuardWaves).Append(" stuns ").Append(boss.Stuns)
                  .Append(" | fight ").Append(_bossSeconds.ToString("0")).Append("s near ").Append(_bossNear.ToString("0")).Append("s chasingPickups ")
                  .Append(_bossChasing.ToString("0")).Append("s guarded ").Append(boss.GuardedSeconds.ToString("0")).Append('s');
            var ep = EnemyProjectiles.Instance;
            if (boss != null && boss.BombsThrown + boss.CoinsFired > 0)
                sb.Append(" purses ").Append(boss.BombsThrown).Append(" (hit you ").Append(ep != null ? ep.BombHits : 0).Append(") coins ").Append(boss.CoinsFired);
            if (boss != null && boss.JavelinsFired + boss.Charges > 0) // the Emperor's Champion (Phase 5.16)
                sb.Append(" javelins ").Append(boss.JavelinsFired).Append(" charges ").Append(boss.Charges);
            // the Grand Arena (Phase 5.13): its hazards, the mercenaries' thefts, the sorcerers
            var jets = FireJets.Instance;
            if (jets != null) sb.Append(" | jets ").Append(jets.Fired).Append(" (burnt ").Append(jets.EnemiesHit).Append(", hit you ").Append(jets.PlayerHits).Append(')');
            var blades = BladeSweepers.Instance;
            if (blades != null) sb.Append(" blades ").Append(blades.Spins).Append(" (mowed ").Append(blades.EnemiesHit).Append(", hit you ").Append(blades.PlayerHits).Append(')');
            var enemies = EnemyManager.Instance;
            if (enemies != null && enemies.GoldStolen + enemies.Blinks > 0)
                sb.Append(" goldStolen ").Append(enemies.GoldStolen).Append(" (back ").Append(enemies.GoldReturned).Append(") blinks ").Append(enemies.Blinks);
            if (ep != null && ep.RunesErupted > 0) sb.Append(" runes ").Append(ep.RunesErupted).Append(" (hit you ").Append(ep.RuneHits).Append(')');
            if (enemies != null && enemies.EscortsFreed > 0) sb.Append(" cursFreed ").Append(enemies.EscortsFreed);
            // the Imperial Arena (Phase 5.16): the chariot races and the Centurions' lines
            var race = ChariotRace.Instance;
            if (race != null)
                sb.Append(" | races ").Append(race.Races).Append(" (trampled ").Append(race.Trampled).Append(", hit you ").Append(race.PlayerHits)
                  .Append(", jumped ").Append(race.Jumped).Append(", boss ").Append(race.BossHits).Append(") dodged ").Append(_chariotDodges);
            if (enemies != null && enemies.FormationsSpawned > 0)
                sb.Append(" formations ").Append(enemies.FormationsSpawned).Append(" linesBroken ").Append(enemies.LinesBroken);
            // the Colosseum (R2): the hypogeum's drops and lift waves, the dead's bone piles, the Chimeras' breath, the falx
            var hyp = Hypogeum.Instance;
            if (hyp != null)
                sb.Append(" | hypogeum drops ").Append(hyp.Drops).Append(" (fallen ").Append(hyp.Fallen).Append(", hit you ").Append(hyp.PlayerHits)
                  .Append(", jumped ").Append(hyp.Jumped).Append(", boss ").Append(hyp.BossHits).Append(") lifts ").Append(hyp.LiftWaves)
                  .Append(" (raised ").Append(hyp.LiftsRaised).Append(", released ").Append(hyp.Released).Append(") dodged ").Append(_hypogeumDodges);
            if (enemies != null && enemies.PilesMade > 0)
                sb.Append(" piles ").Append(enemies.PilesMade).Append(" (risen ").Append(enemies.PilesRisen).Append(", stomped ").Append(enemies.PilesStomped)
                  .Append(", burned ").Append(enemies.PilesBurned).Append(", denied ").Append(enemies.PilesDenied).Append(')');
            if (enemies != null && enemies.Breaths > 0) // the Chimeras
                sb.Append(" breaths ").Append(enemies.Breaths).Append(" (hit you ").Append(enemies.BreathHits).Append(')');
            if (boss != null && boss.DiscsThrown + boss.Grasps + boss.FalseDeaths > 0) // the Eternal Victor
                sb.Append(" falx ").Append(boss.DiscsThrown).Append(" (hit you ").Append(boss.DiscHits).Append(", stopped ").Append(boss.DiscsStopped)
                  .Append(") grasps ").Append(boss.Grasps).Append(" falseDeaths ").Append(boss.FalseDeaths).Append(" (rose ").Append(boss.RemainsRisen)
                  .Append(", burst hit ").Append(boss.RiseBurstHits).Append(')');
            if (run != null) // Beast Handlers killed (the Beastmaster's unlock counts them; C26)
                for (int k = 0; k < run.KilledTypeCount; k++)
                {
                    run.GetTypeKills(k, out int type, out int n);
                    if (type == HandlerTypeId) sb.Append(" handlersKilled ").Append(n);
                }
            var pb = player.GetComponent<PlayerBuild>();
            if (pb != null)
            {
                sb.Append(" | evolutions ").Append(pb.Evolutions).Append(" synergies");
                if (pb.State.SynergyCount == 0) sb.Append(" none");
                for (int i = 0; i < pb.State.SynergyCount; i++) sb.Append(' ').Append(pb.State.GetSynergy(i).id);
            }
            var fx = player.GetComponent<BoonEffects>();
            if (fx != null) sb.Append(" | sparks ").Append(fx.SparksFired).Append(" spreads ").Append(fx.Spreads).Append(" executions ").Append(fx.Executions);
            var loadout = player.GetComponent<GladiatorLoadout>();
            if (loadout != null && loadout.Current != null) sb.Append(" | as ").Append(loadout.Current.id);
            var scav = player.GetComponent<Scavenger>();
            if (scav != null) sb.Append(" scavenged ").Append(scav.Fired);
            var snares = player.GetComponent<SnareLayer>();
            if (snares != null) sb.Append(" snares ").Append(snares.Snapped).Append('/').Append(snares.Placed);
            if (PlayerProjectiles.Instance != null && PlayerProjectiles.Instance.Blasts > 0) sb.Append(" bombs ").Append(PlayerProjectiles.Instance.Blasts);
            var rite = player.GetComponent<BloodRite>();
            if (rite != null) sb.Append(" rites ").Append(rite.Rites).Append(" (paid ").Append(rite.HpPaid.ToString("0")).Append(" HP)");
            var verdict = player.GetComponent<Verdict>();
            if (verdict != null) sb.Append(" verdicts ").Append(verdict.Verdicts).Append(" (hit ").Append(verdict.VerdictHits).Append(')');
            var health = player.GetComponent<PlayerHealth>();
            if (health != null && health.FlatReduction > 0f) sb.Append(" damageTaken ").Append(health.DamageTaken.ToString("0"));
            var weapons = player.GetComponent<WeaponSystem>();
            if (weapons != null && weapons.Slams > 0) sb.Append(" slams ").Append(weapons.Slams);
            if (weapons != null && weapons.ChainStrikes > 0) sb.Append(" chainStrikes ").Append(weapons.ChainStrikes);
            var storm = player.GetComponent<Stormcaller>();
            if (storm != null) sb.Append(" peakCharge ").Append(storm.PeakCharge);
            var dash = player.GetComponent<LightningDash>();
            if (dash != null) sb.Append(" dashes ").Append(dash.Dashes).Append(" (hit ").Append(dash.DashHits).Append(')');
            var rain = player.GetComponent<MakeItRain>();
            if (rain != null) sb.Append(" showers ").Append(rain.Showers).Append(" (gold ").Append(rain.GoldSpent).Append(')');
            var streak = player.GetComponent<GoldenStreak>();
            if (streak != null) sb.Append(" goldStreaks ").Append(streak.Streaks);
            var veil = player.GetComponent<SmokeVeil>();
            if (veil != null) sb.Append(" veils ").Append(veil.Veils).Append(" (hit ").Append(veil.VeilHits).Append(')');
            if (fx != null && player.GetComponent<TraitEffects>() != null && fx.BurstsFired > 0) sb.Append(" cursedBursts ").Append(fx.BurstsFired);
            var net = player.GetComponent<CastNet>();
            if (net != null) sb.Append(" nets ").Append(net.Casts).Append(" (hauled ").Append(net.Hauled).Append(", snared ").Append(net.Snared).Append(')');
            if (weapons != null && weapons.Prongs > 0) sb.Append(" prongs ").Append(weapons.Prongs);
            if (weapons != null && weapons.FireballsCast > 0 && PlayerProjectiles.Instance != null)
                sb.Append(" fireballs ").Append(weapons.FireballsCast).Append(" (burst ").Append(PlayerProjectiles.Instance.Fireballs).Append(')');
            var pack = CompanionPack.Instance;
            if (pack != null && pack.Bites > 0)
            {
                sb.Append(" wolves ").Append(pack.Count).Append(" bites ").Append(pack.Bites).Append(" pounces ").Append(pack.Pounces).Append(" frenzies ").Append(pack.Frenzies);
                // note 37: how they hunt (picks, of them marked; moves on after a bite window; runs back from the leash)
                sb.Append(" picks ").Append(pack.Picks).Append(" (marked ").Append(pack.MarkedPicks).Append(") moves ").Append(pack.Switches)
                  .Append(" recalls ").Append(pack.Recalls);
                var marks = HuntersMark.Instance;
                if (marks != null) sb.Append(" marks ").Append(marks.Marks);
            }
            if (weapons != null && weapons.Sweeps > 0)
                sb.Append(" sweeps ").Append(weapons.Sweeps).Append(" (whirls ").Append(weapons.Whirls).Append(", hits ").Append(weapons.SweepHits)
                  .Append(", stone ").Append(weapons.WeightHits).Append(')');
            var rage = player.GetComponent<Rage>();
            if (rage != null)
            {
                sb.Append(" unchained ").Append(rage.Unchains).Append(" (").Append(rage.UnchainedTime.ToString("0")).Append("s, bellow ")
                  .Append(rage.BellowHits).Append(") peakPress ").Append(rage.PeakPress);
                if (health != null && health.FlatReduction <= 0f) sb.Append(" damageTaken ").Append(health.DamageTaken.ToString("0")); // Q10: tunes his HP and the Rage rates
            }
            var gore = player.GetComponent<Gore>();
            if (gore != null) sb.Append(" gored ").Append(gore.Gored).Append(" (kills ").Append(gore.GoreKills).Append(')');
            var thirst = player.GetComponent<Bloodthirst>();
            if (thirst != null) sb.Append(" kill-healed ").Append(thirst.Healed.ToString("0")).Append(" HP");
            sb.Append(" | hitstops ").Append(TimeController.HitStopCount).Append(" (").Append(TimeController.HitStopSeconds.ToString("0.0")).Append(" s)");
            if (run != null) sb.Append(" | outcome ").Append(run.Outcome).Append(" at ").Append(run.Elapsed.ToString("0"));
            if (RunModifiers.Active.Length > 0) // R7: the edicts this run was played under
                sb.Append(" edicts ").Append(RunModifiers.Active.Length).Append(" (+").Append((RunModifiers.RenownBonus * 100f).ToString("0")).Append(" %)");
            sb.Append(" seed ").Append(GameRandom.SeedText(GameRandom.RunSeed)); // R8: replay it with -runSeed
            if (GameSession.HasDaily) sb.Append(" daily ").Append(GameSession.Daily.Date).Append(GameSession.DailyScored ? " scored" : " practice");
            Debug.Log(sb.ToString());
        }

        /// <summary>Opt-in (it allocates): where the bot is and what it goes for, to read a headless run afterwards.</summary>
        void Trace()
        {
            _nextTrace = _elapsed + traceEvery;
            var run = RunState.Current;
            var props = Interactables.Instance;
            Debug.Log($"[BotTrace] t={_elapsed:0} run={(run != null ? run.Elapsed : 0f):0} pos={_body.position:F1} goal={_goal} {_goalPos:F0} " +
                      $"gold={(run != null ? run.Gold : 0)} price={(props != null ? props.ChestPrice : 0)} chests={(props != null ? props.ChestsOpened : 0)} " +
                      $"level={(run != null ? run.Level : 0)} enemies={(EnemyManager.Instance != null ? EnemyManager.Instance.AliveCount : 0)} ts={Time.timeScale:0.00}");
        }

        void Finish()
        {
            _finished = true;
            var rec = FindAnyObjectByType<FrameStatsRecorder>();
            if (rec != null && rec.IsRecording) rec.End();
            Debug.Log($"[PlaytestBot] finished after {_elapsed:0.0}s");
            LogBuild(); // after the recorder stopped: this string building is not part of the measurement
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }

        public PlayerInputFrame Read()
        {
            var f = new PlayerInputFrame();
            if (_body == null || _finished) return f;
            float t = _elapsed;
            Vector3 pos = _body.position;

            // 1) Where to go: a chest it can pay for (and open it), else the nearest pickup, else a wander point.
            var em = EnemyManager.Instance;
            Vector3 desired, toCrowd;
            if (t >= _chestIgnoreUntil && Sensor != null && Sensor.TryGetAffordableChest(pos, chestSeekRange, out var chest, out bool atChest))
            {
                if (_chestSince < 0f || Flat(chest - _chestTarget).sqrMagnitude > 0.25f) { _chestSince = t; _chestTarget = chest; }
                else if (t - _chestSince > chestGiveUp) { _chestIgnoreUntil = t + chestGiveUp; _chestSince = -1f; }
                desired = Flat(chest - pos);
                f.InteractPressed = atChest;
                _goal = 'C'; _goalPos = chest;
            }
            else if (Sensor != null && Sensor.TryGetNearestPickup(pos, pickupSeekRange, out var pickup))
            {
                desired = Flat(pickup - pos);
                _goal = 'P'; _goalPos = pickup;
            }
            else if (engageRange > 0f && Sensor != null && (toCrowd = -Flat(Sensor.ThreatVector(pos, engageRange))).sqrMagnitude > 1e-4f)
            {
                desired = toCrowd; // toward the nearest of the crowd
                _goal = 'E'; _goalPos = pos + toCrowd.normalized * 5f;
            }
            else
            {
                if (t >= _nextWander || Flat(_wanderTarget - pos).sqrMagnitude < 1f)
                {
                    float ang = RandF() * Mathf.PI * 2f, k = Mathf.Sqrt(RandF()) * 0.8f;
                    _wanderTarget = em != null
                        ? em.arenaCenter + (em.EdgePoint(ang, edgeMargin) - em.arenaCenter) * k
                        : arenaCenter + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (arenaRadius * k);
                    _nextWander = t + 2f + RandF() * 3f;
                }
                desired = Flat(_wanderTarget - pos);
                _goal = 'W'; _goalPos = _wanderTarget;
            }
            if (desired.sqrMagnitude > 1e-4f) desired.Normalize();

            // 2) Flee threats, capped, and mostly not while grabbing something close.
            if (Sensor != null)
            {
                Vector3 threat = Vector3.ClampMagnitude(Sensor.ThreatVector(pos, threatRadius) * threatWeight, threatMax);
                if (_goal != 'W' && _goal != 'E' && Flat(_goalPos - pos).sqrMagnitude < commitRange * commitRange) threat *= 0.2f;
                desired += threat;
            }

            // 2b) The chariot races (the Imperial Arena): off the track when a chariot is about to pass, and not onto it.
            var race = ChariotRace.Instance;
            if (race != null && (race.Running || race.Telegraphing))
            {
                if (race.Danger(pos) < chariotDodge)
                {
                    desired = LaneOut(race, pos) * 2f; // the way out beats any goal
                    if (!_dodging) { _dodging = true; _chariotDodges++; }
                }
                else
                {
                    _dodging = false;
                    Vector3 ahead = pos + (desired.sqrMagnitude > 1e-4f ? desired.normalized : Vector3.zero) * chariotLookAhead;
                    if (desired.sqrMagnitude > 1e-4f && race.Danger(ahead) < chariotDodge)
                    {
                        Vector3 outward = LaneOut(race, ahead);
                        float into = -Vector3.Dot(desired, outward);
                        if (into > 0f) desired += outward * into; // wait at the lane's edge
                    }
                }
            }
            else _dodging = false;

            // 2c) The hypogeum (the Colosseum): off a door or lift about to open or move, not onto one then, round held doors.
            var hyp = Hypogeum.Instance;
            if (hyp != null && hypogeumDodge > 0f)
            {
                if (hyp.Danger(pos) < hypogeumDodge)
                {
                    desired = SafeStep(hyp, pos, desired) * 2f; // the way off beats any goal
                    if (!_hypDodging) { _hypDodging = true; _hypogeumDodges++; }
                }
                else
                {
                    _hypDodging = false;
                    if (desired.sqrMagnitude > 1e-4f) desired = AroundHoles(hyp, pos, desired);
                }
            }

            // 3) Stay inside the arena: its oval shrunk by edgeMargin (chest anchors sit near the edge of the big pit).
            if (em != null)
            {
                Vector3 inside = pos;
                if (em.ClampInside(ref inside, edgeMargin)) desired += Flat(inside - pos) * 2f;
            }
            else
            {
                Vector3 fromCenter = Flat(pos - arenaCenter);
                float d = fromCenter.magnitude;
                if (d > arenaRadius * 0.85f) desired -= fromCenter / Mathf.Max(d, 0.001f) * ((d - arenaRadius * 0.85f) * 2f);
            }

            if (desired.sqrMagnitude > 1f) desired.Normalize();

            // World direction -> camera-relative stick.
            var cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * desired;
            f.Move = new Vector2(local.x, local.z);

            // 4) Jump / slide rhythm.
            if (t >= _nextJump) { f.JumpPressed = true; _nextJump = t + Rand(jumpEvery); }
            f.JumpHeld = f.JumpPressed;
            if (t >= _nextSlide)
            {
                f.SlidePressed = true;
                _slideUntil = t + Rand(slideHold);
                _nextSlide = t + Rand(slideEvery);
            }
            f.SlideHeld = t < _slideUntil;

            // 5) Slow camera orbit to exercise the camera collision.
            f.LookStick = new Vector2(Mathf.Sin(t * 0.37f) * cameraOrbitAmount, Mathf.Sin(t * 0.21f) * cameraOrbitAmount * 0.3f);
            return f;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>The way off the chariot lane at `p` (XZ, unit): up the gradient of the distance to its centre line.</summary>
        static Vector3 LaneOut(ChariotRace race, Vector3 p)
        {
            const float h = 0.25f;
            float d = race.LaneOffset(p);
            var g = new Vector3(race.LaneOffset(p + new Vector3(h, 0f, 0f)) - d, 0f, race.LaneOffset(p + new Vector3(0f, 0f, h)) - d);
            return g.sqrMagnitude > 1e-6f ? g.normalized : Vector3.right;
        }

        static Vector3 Turn(Vector3 v, float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new Vector3(v.x * c + v.z * s, 0f, -v.x * s + v.z * c);
        }

        /// <summary>The shortest way off the door or lift under `p` (XZ, unit): the nearest of 16 bearings at 1.5, 3 and
        /// 4.5 m whose point is safe for hypogeumDodge seconds (across a door is 2 m to its edge, along it 4), the one
        /// closest to where it wanted to go on a tie. Probing Danger needs no door geometry and steps over a neighbour
        /// of the same group (the axis doors are 4 m apart). No allocation.</summary>
        Vector3 SafeStep(Hypogeum hyp, Vector3 p, Vector3 desired)
        {
            Vector3 want = desired.sqrMagnitude > 1e-4f ? desired.normalized : Vector3.forward;
            for (int ring = 1; ring <= 3; ring++)
            {
                Vector3 best = Vector3.zero;
                float bestDot = -2f;
                for (int k = 0; k < 16; k++)
                {
                    Vector3 dir = Turn(Vector3.forward, k * 22.5f);
                    if (hyp.Danger(p + dir * (1.5f * ring)) < hypogeumDodge) continue;
                    float dot = Vector3.Dot(dir, want);
                    if (dot > bestDot) { bestDot = dot; best = dir; }
                }
                if (bestDot > -2f) return best;
            }
            return want;
        }

        /// <summary>`desired` turned (by up to 150° either way, nearest first) so the point hypogeumLookAhead ahead isn't on a
        /// door or lift about to open, or open, or held open for good: held doors are walls. Zero if every way is (it waits).</summary>
        Vector3 AroundHoles(Hypogeum hyp, Vector3 p, Vector3 desired)
        {
            float m = desired.magnitude;
            Vector3 dir = desired / m;
            if (hyp.Danger(p + dir * hypogeumLookAhead) >= hypogeumDodge) return desired;
            for (int k = 1; k <= 6; k++)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 d = Turn(dir, side * 25f * k);
                    if (hyp.Danger(p + d * hypogeumLookAhead) >= hypogeumDodge) return d * m;
                }
            return Vector3.zero;
        }

        uint Next()
        {
            // xorshift32
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return _rng;
        }

        float RandF() => (Next() & 0xFFFFFF) / (float)0x1000000;
        float Rand(Vector2 range) => Mathf.Lerp(range.x, range.y, RandF());
    }
}
