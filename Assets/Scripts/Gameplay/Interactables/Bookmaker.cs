using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Provincial Pit's secret (Phase 5.6, docs/UNLOCKS.md): a hunched bookmaker at one of the layout's secret spots.
    /// W1 (the producer's note 23, docs/tracks/W1-wager.md): step into his gold chalk ring and press Interact (E / gamepad
    /// North, like the chests; a chest in reach still takes the press) and he **opens his book**: the run pauses and
    /// WagerPanel shows 1–4 wagers, the stake doubling (5, 10, 20, 40 gold), the challenge harder with the stake. Tier I
    /// is always offered; your kills this run open the others (`unlockKills`), and Luck may open one more (the level-up's
    /// fourth-card rule, rolled once per opening on a seeded fork). Pick one and he takes the stake and the wager runs
    /// (RunObjective counts it down under the timer): kill N in 30 s (N from your last 30 s of kills), the same without
    /// being hit, or kill the champion he sends in at a gate. Win: he pays the tier's payout and, the first time at tier II
    /// or higher, the flag `bookmaker_paid` (Loaded Dice's unlock) is saved at once. Lose: the house keeps the stake.
    /// After each wager he waits `cooldownSeconds`, then calls again. Walking away from the book costs nothing.
    /// Gameplay only: the book's state (OfferCount, Offer, BookOpen) is what the UI reads. Zero allocation per frame.
    /// </summary>
    public sealed class Bookmaker : MonoBehaviour, ITickable
    {
        /// <summary>Absent (not placed), Waiting (calls you over; E opens the book), Book (open: the run is paused),
        /// Wager (one running), Cooldown (after a wager, `cooldownSeconds`), Closed (`maxWagers` taken).</summary>
        public enum Phase : byte { Absent, Waiting, Book, Wager, Cooldown, Closed }

        /// <summary>How the last wager ended (Void: the champion left without a kill, the stake went back).</summary>
        public enum Outcome : byte { None, Won, Lost, Void }

        public const int MaxTiers = 4;

        public GameObject model;
        public Material material;
        public Vector3[] spots = System.Array.Empty<Vector3>();
        public Vector3 arenaCenter;
        public string flag = "bookmaker_paid";

        [Header("The book (W1)")]
        [Tooltip("The wagers, cheapest first (at most 4; the code defaults are the spec's table, W1-wager.md). Tier I is always offered.")]
        public WagerTier[] tiers = DefaultTiers();
        [Tooltip("No pace wager asks for more kills than this.")]
        public int maxKills = 600;
        [Tooltip("Game seconds he waits after a wager ends before he calls again.")]
        public float cooldownSeconds = 60f;
        [Tooltip("Wagers per run (0 = no limit).")]
        public int maxWagers;
        [Tooltip("The lowest tier (0 = I) whose win sets the flag: tier I is too easy to be the secret.")]
        public int flagMinTier = 1;
        [Tooltip("The champion comes in at a gate at least this far from you (the nearest one if none is).")]
        public float championGateDistance = 12f;

        [Header("The booth")]
        public float callRange = 9f;
        [Tooltip("His spot: Interact opens the book from inside this ring (it pulses while you're in it).")]
        public float ringRadius = 1.3f;
        [Tooltip("His chalk ring (review: a pale gold vanished on the sand; deeper and thicker reads).")]
        public Color ringColor = new Color(1f, 0.7f, 0.1f, 0.95f);
        public float ringThickness = 0.16f;
        public float favor = 30f;

        public const string CallText = "PSST! A WAGER, CHAMPION?";
        public const string BetText = "THE BET IS ON: BEAT YOUR OWN PACE!";
        public const string NoHitBetText = "THE BET IS ON: NOT A SCRATCH!";
        public const string ChampionBetText = "HE SENDS HIS CHAMPION!";
        public const string BrokeText = "NO GOLD, NO WAGER.";
        public const string LaterText = "COME BACK LATER, CHAMPION.";
        public const string WinText = "THE BOOKMAKER PAYS UP!";
        public const string FirstWinText = "WHAT A SHARP! HE PAYS UP!";
        public const string LoseText = "THE HOUSE WINS.";
        public const string VoidText = "NO CONTEST: YOUR STAKE BACK.";
        /// <summary>The countdown's labels under the timer (constant strings: the HUD formats the numbers without allocating).</summary>
        public const string ChampionLabel = "THE CHAMPION";
        static readonly string[] PaceLabels = { "WAGER I", "WAGER II", "WAGER III", "WAGER IV" };
        static readonly string[] NoHitLabels = { "WAGER I · NO HITS", "WAGER II · NO HITS", "WAGER III · NO HITS", "WAGER IV · NO HITS" };
        /// <summary>I, II, III, IV (the cards' numerals).</summary>
        public static readonly string[] Numerals = { "I", "II", "III", "IV" };
        public Color textColor = new Color(1f, 0.84f, 0.3f);

        Transform _view;
        Phase _phase = Phase.Absent;
        Vector3 _pos;
        float _left, _cooldown;
        bool _called, _hit, _blocked;
        int _startKills, _stake, _payout;
        uint _bookKey, _championKey;
        int _championSlot = -1, _championUid = -1;
        PlayerBuild _build;
        readonly WagerOffer[] _offers = new WagerOffer[MaxTiers];

        // kills per second over the last 30 s (one bucket per second of run time)
        readonly int[] _buckets = new int[30];
        int _bucketSecond = -1;

        public Phase State => _phase;
        public Vector3 Position => _pos;
        public int Target { get; private set; }
        public int Progress { get; private set; }
        /// <summary>He's waiting (or cooling down) and the player is within his call: the HUD shows his tag over his head.</summary>
        public bool Calling { get; private set; }
        /// <summary>The player stands in his ring: Interact opens the book (the HUD's bottom prompt).</summary>
        public bool InReach { get; private set; }
        /// <summary>After a wager: "COME BACK LATER" over him; E in his ring only gets his line.</summary>
        public bool CoolingDown => _phase == Phase.Cooldown;
        public float CooldownLeft => _phase == Phase.Cooldown ? Mathf.Max(0f, _cooldown) : 0f;
        public bool BookOpen => _phase == Phase.Book;
        /// <summary>Wagers offered in the open book (1–4), cheapest first: card i is tier i.</summary>
        public int OfferCount { get; private set; }
        public WagerOffer Offer(int i) => _offers[Mathf.Clamp(i, 0, MaxTiers - 1)];
        /// <summary>Bumps each time the book opens (the panel redraws its cards).</summary>
        public int BookVersion { get; private set; }
        public int BookOpens { get; private set; }
        /// <summary>This opening's Luck roll opened one tier beyond what the kills opened.</summary>
        public bool LuckOpenedTier { get; private set; }
        public int WagersPlaced { get; private set; }
        public int WagersWon { get; private set; }
        public Outcome LastOutcome { get; private set; }
        /// <summary>The running wager's tier (0 = I) and kind; -1 when none runs.</summary>
        public int ActiveTier { get; private set; } = -1;
        public WagerKind ActiveKind { get; private set; }
        /// <summary>The champion of a tier IV wager (EnemyManager slot, -1 = none).</summary>
        public int ChampionSlot => _championUid >= 0 ? _championSlot : -1;
        /// <summary>The player has offer `i`'s stake.</summary>
        public bool CanAfford(int i) => RunState.Current != null && i >= 0 && i < OfferCount && RunState.Current.Gold >= _offers[i].Stake;
        public static Bookmaker Instance { get; private set; }

        void OnEnable()
        {
            Instance = this;
            if (_bookKey == 0u) { _bookKey = GameRandom.KeyOf("bookmaker_book"); _championKey = GameRandom.KeyOf("bookmaker_champion"); }
            GameLoop.Register(this, TickGroup.Pickups, 22);
            GameEvents.OnKill.Subscribe(OnKill);
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnRunEnd.Subscribe(OnRunEnd);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnRunEnd.Unsubscribe(OnRunEnd);
            if (_phase == Phase.Book) CloseBook(); // never leave the run paused
            if (Instance == this) Instance = null;
            if (_phase == Phase.Wager) RunObjective.Clear();
        }

        void Start()
        {
            var rng = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("bookmaker")); // R8: the same seed, the same spot
            if (spots == null || spots.Length == 0) return;
            Place(rng.Range(0, spots.Length));
        }

        /// <summary>Puts him at spot `index`, waiting for a customer (tests pick one).</summary>
        public void Place(int index)
        {
            if (_phase == Phase.Book) CloseBook();
            if (_phase == Phase.Wager) RunObjective.Clear();
            _pos = spots[Mathf.Clamp(index, 0, spots.Length - 1)];
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) _pos.y = em.Height.Sample(_pos.x, _pos.z);
            if (_view == null) _view = Spawn();
            _view.position = _pos;
            Vector3 toCenter = arenaCenter - _pos;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f) _view.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
            _view.gameObject.SetActive(true);
            _phase = Phase.Waiting;
            _called = false;
            Calling = InReach = false;
            ActiveTier = -1;
            _championUid = -1;
        }

        Transform Spawn()
        {
            var go = model != null ? Instantiate(model, transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (model == null) { go.transform.SetParent(transform, false); Destroy(go.GetComponent<Collider>()); }
            go.name = "Bookmaker";
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go.transform;
        }

        // ------------------------------------------------------------------ the maths (pure: EditMode tests)

        /// <summary>The spec's table (W1-wager.md): the stake doubling 5, 10, 20, 40; kills this run open II, III, IV at
        /// 100, 300, 600.</summary>
        public static WagerTier[] DefaultTiers() => new[]
        {
            new WagerTier { kind = WagerKind.Pace, stake = 5, payout = 15, seconds = 30f, paceFactor = 0.8f, minKills = 25, unlockKills = 0 },
            new WagerTier { kind = WagerKind.Pace, stake = 10, payout = 35, seconds = 30f, paceFactor = 1.2f, minKills = 50, unlockKills = 100 },
            new WagerTier { kind = WagerKind.NoHit, stake = 20, payout = 80, seconds = 30f, paceFactor = 1f, minKills = 40, unlockKills = 300 },
            new WagerTier { kind = WagerKind.Champion, stake = 40, payout = 200, seconds = 45f, paceFactor = 0f, minKills = 1, unlockKills = 600 },
        };

        /// <summary>Tiers your kills open: tier I always, then each next tier while kills ≥ its unlockKills (in order).</summary>
        public static int OpenedByKills(WagerTier[] table, int kills)
        {
            int n = table != null ? Mathf.Min(table.Length, MaxTiers) : 0;
            if (n == 0) return 0;
            int open = 1;
            while (open < n && kills >= table[open].unlockKills) open++;
            return open;
        }

        /// <summary>Wagers offered: OpenedByKills, plus one more when `roll01` &lt; luck / luckPerTier (the level-up's
        /// fourth-card rule, UpgradeConfig.fourthCardLuck), never more than the table holds (at most 4).</summary>
        public static int OfferCountFor(WagerTier[] table, int kills, float luck, float luckPerTier, float roll01)
        {
            int n = table != null ? Mathf.Min(table.Length, MaxTiers) : 0;
            int open = OpenedByKills(table, kills);
            if (open < n && luckPerTier > 0f && roll01 < luck / luckPerTier) open++;
            return open;
        }

        /// <summary>The kills a tier asks for: the champion is one kill; a pace wager is paceFactor × your last 30 s of
        /// kills, rounded up, between its minKills and `cap`. (Less a hair before rounding up: 50 × 1.2f is 60.0000024 in
        /// floats, and must ask for 60.)</summary>
        public static int TargetFor(in WagerTier t, int recentKills, int cap)
        {
            if (t.kind == WagerKind.Champion) return 1;
            int want = Mathf.CeilToInt(Mathf.Max(0, recentKills) * t.paceFactor - 0.001f);
            return Mathf.Clamp(want, Mathf.Max(1, t.minKills), Mathf.Max(Mathf.Max(1, t.minKills), cap));
        }

        /// <summary>The countdown's label (a constant string) for tier `tier` (0 = I) of `kind`.</summary>
        public static string LabelFor(int tier, WagerKind kind)
        {
            int i = Mathf.Clamp(tier, 0, MaxTiers - 1);
            return kind == WagerKind.Champion ? ChampionLabel : kind == WagerKind.NoHit ? NoHitLabels[i] : PaceLabels[i];
        }

        // ------------------------------------------------------------------ the pace

        void OnKill(in KillEvent e)
        {
            var run = RunState.Current;
            if (run == null) return;
            Roll(run.Elapsed);
            _buckets[_bucketSecond % _buckets.Length]++;
            if (_phase == Phase.Wager && ActiveKind == WagerKind.Champion && _championUid >= 0 && e.TargetId == _championUid)
            {
                _championUid = -1;
                Progress = 1;
            }
        }

        void OnDamaged(in DamagedEvent e)
        {
            if (_phase == Phase.Wager && ActiveKind == WagerKind.NoHit && e.Damage > 0f) _hit = true;
        }

        // The run is over: an open book closes (nothing taken); a running wager is lost, quietly (the end screen speaks).
        void OnRunEnd(in RunEndEvent e)
        {
            if (_phase == Phase.Book) CloseBook();
            else if (_phase == Phase.Wager) EndWager(Outcome.Lost);
        }

        /// <summary>Advances the 30 one-second buckets to `elapsed`, emptying the seconds that went by.</summary>
        void Roll(float elapsed)
        {
            int sec = Mathf.FloorToInt(elapsed);
            if (_bucketSecond < 0) _bucketSecond = sec;
            for (int s = _bucketSecond + 1; s <= sec && s <= _bucketSecond + _buckets.Length; s++) _buckets[s % _buckets.Length] = 0;
            if (sec > _bucketSecond) _bucketSecond = sec;
        }

        /// <summary>Kills in the last 30 s of run time.</summary>
        public int RecentKills()
        {
            var run = RunState.Current;
            if (run != null) Roll(run.Elapsed);
            int n = 0;
            for (int k = 0; k < _buckets.Length; k++) n += _buckets[k];
            return n;
        }

        // ------------------------------------------------------------------ the booth

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return; // the book's pause, the level-up, the pause menu
            switch (_phase)
            {
                case Phase.Waiting:
                    TickBooth(dt, true);
                    break;
                case Phase.Cooldown:
                    _cooldown -= dt;
                    if (_cooldown <= 0f)
                    {
                        _phase = Phase.Waiting;
                        _called = false; // he calls again
                        TickBooth(dt, true);
                    }
                    else TickBooth(dt, false);
                    break;
                case Phase.Wager:
                    Calling = InReach = false;
                    TickWager(dt);
                    break;
                default:
                    Calling = InReach = false;
                    break;
            }
        }

        /// <summary>His call, his ring and E in it: `open` opens the book; during the cooldown E only gets his line.</summary>
        void TickBooth(float dt, bool open)
        {
            var player = Player.Main;
            var run = RunState.Current;
            if (player == null) return;
            Vector3 d = player.transform.position - _pos;
            float dist = new Vector2(d.x, d.z).magnitude;
            bool live = run != null && !run.Ended && !RunState.CombatOver;
            Calling = live && dist < callRange;
            InReach = live && dist < ringRadius && Mathf.Abs(d.y) < 1.5f;
            var tele = TelegraphRenderer.Instance;
            if (tele != null) tele.Circle(_pos, ringRadius, InReach ? 1f : 0f, ringColor, TelegraphRenderer.Style.Ring, ringThickness, 0.31f);
            if (dist < callRange)
            {
                Vector3 look = new Vector3(d.x, 0f, d.z);
                if (look.sqrMagnitude > 0.01f) _view.rotation = Quaternion.Slerp(_view.rotation, Quaternion.LookRotation(look, Vector3.up), dt * 4f);
                if (open && !_called)
                {
                    _called = true;
                    SfxPlayer.Play(SfxId.Quill, _pos);
                    Say(CallText, 2.4f);
                }
            }
            // E in his ring, like a chest. If a chest is in reach too, the chest takes the press (Interactables ticks after
            // us and reads the same press; its InReach is from last frame), so one press never does two things. The bot
            // never opens the book (nothing would close it).
            var props = Interactables.Instance;
            if (!InReach || !player.Input.InteractPressed || (props != null && props.InReach >= 0)) return;
            if (PlaytestBot.Active != null && player.OverrideSource == null) return;
            if (open) OpenBook();
            else Say(LaterText, 1.8f);
        }

        /// <summary>A level-up (or a chest's, the altar's, the boss's cards) is open or about to open: it goes first.</summary>
        static bool CardsBusy()
        {
            var lvl = LevelUpController.Instance;
            if (lvl == null || !lvl.isActiveAndEnabled) return false;
            return lvl.IsOpen || lvl.PendingRewards > 0 || (lvl.run != null && lvl.run.PendingLevelUps > 0);
        }

        PlayerBuild Build()
        {
            if (_build == null && Player.Main != null) _build = Player.Main.GetComponent<PlayerBuild>();
            return _build;
        }

        /// <summary>
        /// Opens his book (E in his ring, or tests): rolls this opening's Luck, works out each offered wager's numbers from
        /// your pace now, pauses the run and blocks gameplay input (WagerPanel shows the cards). False unless he's waiting
        /// in a live run with no cards about to open. Costs nothing.
        /// </summary>
        public bool OpenBook()
        {
            if (_phase != Phase.Waiting) return false;
            var run = RunState.Current;
            if (run == null || run.Ended || RunState.CombatOver || CardsBusy()) return false;
            var build = Build();
            float luck = build != null ? build.Stats.Luck : 0f;
            float perTier = build != null && build.config != null ? build.config.fourthCardLuck : 0.5f;
            var rng = GameRandom.Fork(RngStream.Loot, _bookKey + (uint)BookOpens); // R8: the nth opening rolls the same
            float roll = rng.Next01();
            int recent = RecentKills();
            int byKills = OpenedByKills(tiers, run.Kills);
            OfferCount = OfferCountFor(tiers, run.Kills, luck, perTier, roll);
            LuckOpenedTier = OfferCount > byKills;
            for (int i = 0; i < OfferCount; i++)
            {
                var t = tiers[i];
                _offers[i] = new WagerOffer
                {
                    Tier = i, Kind = t.kind, Stake = t.stake, Payout = t.payout, Seconds = t.seconds,
                    Target = TargetFor(t, recent, maxKills),
                };
            }
            BookOpens++;
            BookVersion++;
            _phase = Phase.Book;
            Calling = InReach = false;
            TimeController.PushPause();
            var player = Player.Main;
            _blocked = player != null;
            if (_blocked) player.BlockInput();
            SfxPlayer.Play2D(SfxId.Quill);
            return true;
        }

        /// <summary>Walks away from the open book (WALK AWAY, Esc, gamepad East): nothing taken, he waits as before.</summary>
        public void CloseBook()
        {
            if (_phase != Phase.Book) return;
            _phase = Phase.Waiting;
            ReleaseBook();
        }

        void ReleaseBook()
        {
            TimeController.PopPause();
            if (_blocked && Player.Main != null) Player.Main.UnblockInput();
            _blocked = false;
        }

        /// <summary>
        /// Takes wager `index` of the open book: the stake, then the wager runs (the book closes and the run goes on). False
        /// (the book stays open) when you can't afford it, or the champion can't be sent.
        /// </summary>
        public bool Pick(int index)
        {
            if (_phase != Phase.Book || index < 0 || index >= OfferCount) return false;
            var run = RunState.Current;
            var o = _offers[index];
            if (run == null || run.Gold < o.Stake)
            {
                SfxPlayer.Play2D(SfxId.Blocked);
                Say(BrokeText, 1.8f);
                return false;
            }
            ActiveKind = o.Kind;
            if (o.Kind == WagerKind.Champion && !SendChampion()) return false; // no champion, no bet (and no stake)
            run.SpendGold(o.Stake);
            _phase = Phase.Wager;
            ReleaseBook();
            ActiveTier = o.Tier;
            Target = o.Target;
            Progress = 0;
            _left = o.Seconds;
            _stake = o.Stake;
            _payout = o.Payout;
            _startKills = run.Kills;
            _hit = false;
            WagersPlaced++;
            RunObjective.Begin(LabelFor(o.Tier, o.Kind), Target, o.Seconds);
            SfxPlayer.Play(SfxId.Coin, _pos);
            SfxPlayer.Play(SfxId.Quill, _pos);
            Say(o.Kind == WagerKind.Champion ? ChampionBetText : o.Kind == WagerKind.NoHit ? NoHitBetText : BetText, 2.2f);
            return true;
        }

        /// <summary>Tier IV: an elite from the arena's own elite pick (the Director's, with an affix) in at a gate away from
        /// you (the gate swings open). False without a Director, an elite type or an affix. A switched-off Director (the
        /// admin's toggle, tests) still picks his champion: it's his, not the horde's.</summary>
        bool SendChampion()
        {
            var director = Director.Instance != null ? Director.Instance : FindAnyObjectByType<Director>();
            var em = EnemyManager.Instance;
            var player = Player.Main;
            if (director == null || em == null || player == null) return false;
            Vector3 p = player.transform.position;
            var gates = ArenaGates.Instance;
            int gate = -1;
            if (gates != null && gates.Count > 0)
            {
                var rng = GameRandom.Fork(RngStream.Elite, _championKey + (uint)WagersPlaced);
                gate = gates.Pick(p, championGateDistance, ref rng);
                if (gate < 0) gate = gates.Pick(p, 0f, ref rng);
            }
            Vector3 at = gate >= 0 ? gates.SpawnPoint(gate) : em.arenaCenter;
            int slot = director.SpawnEliteAt(at);
            if (slot < 0) return false;
            if (gate >= 0) gates.Open(gate, 2.5f);
            _championSlot = slot;
            _championUid = em.GetUid(slot);
            return true;
        }

        void TickWager(float dt)
        {
            var run = RunState.Current;
            if (run == null) return;
            _left -= dt;
            if (ActiveKind == WagerKind.Champion)
            {
                var em = EnemyManager.Instance;
                if (_championUid >= 0)
                {
                    if (em == null || !em.IsAlive(_championSlot) || em.GetUid(_championSlot) != _championUid)
                    {
                        Void(run); // gone without a kill event (a pit, the arena's end): no contest
                        return;
                    }
                    var tele = TelegraphRenderer.Instance;
                    float total = Mathf.Max(0.1f, WagerSeconds());
                    if (tele != null) tele.Circle(em.GetPosition(_championSlot), 1.6f, Mathf.Clamp01(1f - _left / total), ringColor, TelegraphRenderer.Style.Ring, 0.12f, 3f);
                }
            }
            else Progress = run.Kills - _startKills;
            RunObjective.Set(Progress, _left);
            if (Progress >= Target) Win(run);
            else if (_hit || _left <= 0f || run.Ended) Lose();
        }

        float WagerSeconds() => ActiveTier >= 0 && tiers != null && ActiveTier < tiers.Length ? tiers[ActiveTier].seconds : 45f;

        /// <summary>The wager is over: he waits `cooldownSeconds` (or packs up after `maxWagers`).</summary>
        void EndWager(Outcome outcome)
        {
            LastOutcome = outcome;
            RunObjective.Clear();
            _championUid = -1;
            _hit = false;
            _cooldown = cooldownSeconds;
            _phase = maxWagers > 0 && WagersPlaced >= maxWagers ? Phase.Closed : Phase.Cooldown;
        }

        void Win(RunState run)
        {
            int tier = ActiveTier;
            WagersWon++;
            EndWager(Outcome.Won);
            run.AddGold(_payout);
            var player = Player.Main;
            Vector3 at = player != null ? player.transform.position : _pos;
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at + Vector3.up * 1f, 24, 5f, 0.16f, new Color(1f, 0.9f, 0.45f), new Color(1f, 1f, 0.85f));
            SfxPlayer.Play2D(SfxId.Coin);
            SfxPlayer.Play2D(SfxId.Cheer);
            if (Favor.Instance != null) Favor.Instance.Add(favor);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.5f);
            bool first = tier >= flagMinTier && MetaProgress.SetFlag(flag); // Loaded Dice: tier II or higher
            Say(first ? FirstWinText : WinText, 2.4f);
        }

        void Lose()
        {
            EndWager(Outcome.Lost);
            SfxPlayer.Play2D(SfxId.Boo);
            Say(LoseText, 2f);
        }

        void Void(RunState run)
        {
            EndWager(Outcome.Void);
            run.AddGold(_stake);
            SfxPlayer.Play2D(SfxId.Coin);
            Say(VoidText, 2f);
        }

        void Say(string text, float secs) =>
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = text, Color = textColor, Seconds = secs, Kind = AnnounceKind.Line });
    }

    /// <summary>What a wager asks (W1): kill N in time (your pace), the same without being hit, or kill his champion.</summary>
    public enum WagerKind : byte { Pace, NoHit, Champion }

    /// <summary>One row of the Bookmaker's book: its stake and payout, its challenge, and the kills this run that open it.</summary>
    [System.Serializable]
    public struct WagerTier
    {
        public WagerKind kind;
        public int stake, payout;
        [Tooltip("Seconds to win it in.")]
        public float seconds;
        [Tooltip("Pace wagers: this × your kills of the last 30 s…")]
        public float paceFactor;
        [Tooltip("…but never fewer than this (the champion: ignored, one kill).")]
        public int minKills;
        [Tooltip("Kills this run that open this tier (tier I: always offered).")]
        public int unlockKills;
    }

    /// <summary>A card in the open book: the tier (0 = I), what it asks, and its numbers for this opening.</summary>
    public struct WagerOffer
    {
        public int Tier;
        public WagerKind Kind;
        public int Stake, Payout, Target;
        public float Seconds;
    }
}
