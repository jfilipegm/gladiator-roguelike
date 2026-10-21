using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Every boss attack the controller knows. Appended only: BossDef assets serialize these.</summary>
    public enum BossAttack : byte { None, Sweep, Whip, Slam, Charge, Bash, Flurry, Pounce, CoinBombs, Volley, Disc }

    /// <summary>
    /// A boss's brain (docs/BOSSES.md), driven by its BossDef. The body is a Scripted enemy in EnemyManager — so every
    /// weapon, status, damage number and death pop already works on him — and this drives it: the gate entrance,
    /// chasing, the telegraphed attacks his def lists for each phase (Gorm: Flail Sweep, Whip Lash, Chain Slam, Charge
    /// in phase 2; the Champion: Shield Bash, Gladius Flurry, Shield Charge that smashes barricades and stuns him on
    /// stone; the Beast Lord: Goad Sweep (the cone), Hook Thrust (the lane), Pounce; the Gilded Patron: Coin Bombs, Golden
    /// Volley, Shove (the cone)), a frontal shield if his body has one, calling the guards through the gates (or at the
    /// arena's guard spots: the patron boxes) and the beasts through the cages, the Patron's payroll (guarded while his
    /// hired swords live), the phase-2 roar (Gorm sets the fence on fire, the Champion fires every spike trap, the Beast
    /// Lord opens every cage, the Patron fires every jet and spins every blade), an optional third phase (the Emperor's
    /// Champion: "FOR THE EMPEROR!", faster still, the charge twice; ChariotRace reads Phase and runs its races back to
    /// back), and his rigid-part animation. The Eternal Victor (R2) rises from below at the arena's centre instead of
    /// walking in (BeginRise), throws his falx down a lane and catches it on the way back (Disc), holds you with bone hands
    /// (the grasp: Whip with whipGrasp), leaps and throws twice in phase 3, and the first time he reaches 0 HP lies as
    /// remains for a few seconds (State.Remains): destroy them or he rises again. The Colosseum's Hypogeum reads his Phase,
    /// CurrentState, Position, GuardWaves and Defeated; he never calls it. Telegraphs are orange-red
    /// (EnemyManager.telegraphColor). Central tick, no allocation after Begin.
    /// </summary>
    public sealed class BossController : MonoBehaviour, ITickable
    {
        /// <summary>Appended only (Remains after Dead): tests and the hypogeum compare these.</summary>
        public enum State : byte { Idle, Entering, Roaring, Chase, Windup, Strike, Recover, Enraging, Stunned, Dead, Remains }

        /// <summary>The HP floor that keeps a false-death boss alive until his remains (EnemyManager.SetHpFloor).</summary>
        const float HpFloor = 1f;
        /// <summary>Seconds his remains can't be hurt as he collapses (a hit that killed him mustn't also end them).</summary>
        const float RemainsGrace = 0.4f;
        /// <summary>The bone-white ring round his remains (m).</summary>
        const float RemainsRing = 2.2f;
        /// <summary>The falx is caught within this of his hand (m).</summary>
        const float CatchDistance = 0.6f;

        public static BossController Active { get; private set; }

        public BossDef def;
        [Tooltip("Seconds of walking in through the gate before the roar.")]
        public float entranceWalk = 2.2f;
        public float entranceRoar = 1.2f;
        public float playerRadius = 0.35f;
        [Tooltip("The arena's guard spots (ArenaLayoutDef.guardSpots): guards appear there instead of at the gates.")]
        public Vector3[] guardSpots = System.Array.Empty<Vector3>();

        EnemyManager _em;
        int _slot = -1, _uid;
        State _state;
        BossAttack _attack, _last;
        int _phase = 1, _enrageTo = 2;
        float _t, _dur, _clock, _walk, _spin, _contactCd, _guardsT, _chaseMin, _chargeTravel;
        bool _hitDone;
        int _flurryLeft, _chargesLeft, _pouncesLeft, _bombsLeft;
        float _bombNext;
        bool _guarded;
        readonly int[] _guardSlots = new int[12];
        readonly float[] _spotD = new float[16];
        Transform[] _bearerLegs = System.Array.Empty<Transform>();
        Quaternion[] _bearerBind = System.Array.Empty<Quaternion>();
        bool[] _bearerLeft = System.Array.Empty<bool>(); // cached: Object.name allocates
        Vector3 _leapFrom;
        readonly float[] _weights = new float[8];
        Vector3 _pos, _aim, _target, _entryEnd;
        float _yaw;
        PartRig _rig;
        PartPose _pose = PartPose.Identity, _goal = PartPose.Identity;
        Transform _flail;
        Quaternion _flailBind;
        // the Emperor's Champion's drivable parts (R1-char-art): the spear levels for the thrust, the eagle's wings flap at his roars
        Transform _spear, _wingL, _wingR;
        Quaternion _spearBind, _wingLBind, _wingRBind;
        float _spearTilt, _spearGoal, _flap;
        readonly int[] _nearGates = new int[8];
        // the Eternal Victor's falx (R2-char-art: a child of ArmR, pivot at the grip): it flies alone for the throw, in
        // world space, spinning about its own middle, then back to his hand; the rest of the time it's held at its bind
        Transform _falx;
        Quaternion _falxBind, _falxRelease;
        Vector3 _falxBindPos, _falxCenter, _falxOffset;
        Vector3 _discPos, _discFrom, _discDir;
        float _discOut, _discSpinT, _discAge;
        bool _discFlying, _discBack, _discHitOut, _discHitBack;
        int _discsLeft;
        // the rise (BeginRise) and the false death
        bool _rising, _remainsGrace, _quietRoar;
        float _riseFrom, _riseGround, _shoveCd;
        PlayerHealth _health;
        PlayerMotor _motor;
        Transform _player;
        readonly int[] _query = new int[64];

        public State CurrentState => _state;
        public BossAttack CurrentAttack => _attack;
        public int Phase => _phase;
        public int Slot => _slot;
        public bool Alive => _slot >= 0 && _em != null && _em.IsAlive(_slot) && _em.GetUid(_slot) == _uid;
        public bool Defeated { get; private set; }
        public float Hp01 => Alive ? _em.GetHp(_slot) / Mathf.Max(1f, _em.GetMaxHp(_slot)) : 0f;
        public Vector3 Position => _pos;
        public int AttacksDone { get; private set; }
        public int GuardWaves { get; private set; }
        /// <summary>His shield blocks frontal hits right now (a body with a block arc, not stunned).</summary>
        public bool ShieldUp { get; private set; }
        public int Stuns { get; private set; }
        public int Pounces { get; private set; }
        /// <summary>Where the pounce he's winding up will land.</summary>
        public Vector3 PounceTarget => _target;
        /// <summary>His hired guard lives: he takes guardedDamageMul of the damage (the Gilded Patron's payroll).</summary>
        public bool Guarded => _guarded;
        /// <summary>Seconds of the fight (after his entrance) he spent guarded (bot runs log it; C24).</summary>
        public float GuardedSeconds { get; private set; }
        public int BombsThrown { get; private set; }
        public int CoinsFired { get; private set; }
        /// <summary>Javelins thrown (volleyJavelins) and charges started, chained ones included (tests, bot log).</summary>
        public int JavelinsFired { get; private set; }
        public int Charges { get; private set; }
        /// <summary>How long the current state lasts (s): a wind-up's length after the phase's speed (tests).</summary>
        public float StateDuration => _dur;
        /// <summary>He's rising from below at the arena's centre (entranceRise; CurrentState is Entering meanwhile).</summary>
        public bool Rising => _rising && _state == State.Entering;
        /// <summary>The falx is out of his hands (thrown, on its way out or back) and where it is (tests).</summary>
        public bool DiscFlying => _discFlying;
        public bool DiscReturning => _discFlying && _discBack;
        public Vector3 DiscPosition => _discFlying ? _discPos : Hand;
        /// <summary>Falx throws (chained ones included), hits on the player (out and back count apart), and throws that
        /// turned back early on stone (bot log, tests).</summary>
        public int DiscsThrown { get; private set; }
        public int DiscHits { get; private set; }
        public int DiscsStopped { get; private set; }
        /// <summary>Grasps of the Dead that caught the player (whipGrasp).</summary>
        public int Grasps { get; private set; }
        /// <summary>He lies as remains (falseDeath): Defeated stays false until they're destroyed.</summary>
        public bool InRemains => _state == State.Remains;
        /// <summary>Times he collapsed into remains, rose from them, and hit the player with the rising burst (bot log).</summary>
        public int FalseDeaths { get; private set; }
        public int RemainsRisen { get; private set; }
        public int RiseBurstHits { get; private set; }

        float Speed => _phase >= 3 ? def.phase3Speed : _phase == 2 ? def.enrageSpeed : 1f;
        /// <summary>His last phase: the false death waits for it.</summary>
        int LastPhase => def.phase3At > 0f ? 3 : 2;
        float MoveSpeed => _phase >= 3 && def.phase3MoveSpeed > 0f ? def.phase3MoveSpeed : _phase >= 2 ? def.enragedMoveSpeed : def.moveSpeed;
        Color Danger => _em != null ? _em.telegraphColor : new Color(1f, 0.22f, 0.06f, 0.95f);

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies, 20);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Active == this) Active = null;
        }

        /// <summary>Gorm walks in through the gate at `gate` heading `inward`. Returns false if he can't spawn.</summary>
        public bool Begin(Vector3 gate, Vector3 inward)
        {
            inward.y = 0f;
            inward.Normalize();
            if (!SpawnBody(gate + inward * 0.4f, Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg)) return false;
            _entryEnd = gate + inward * 7.5f;
            if (def.beastPack.y > 0 && BeastCages.Instance != null) BeastCages.Instance.RattleAll(entranceWalk + entranceRoar); // every pen shakes
            Enter(State.Entering, entranceWalk);
            return true;
        }

        /// <summary>
        /// The Eternal Victor rises at `at` (the Colosseum's stage lift, BossFight passes the arena's centre): he starts
        /// riseDepth below the ground there and comes up over riseTime, kneeling, then straightens; then the usual roar
        /// (the falx raised: the salute). Invulnerable and harmless meanwhile, as a gate entrance; a player standing over
        /// the lift is shoved off. The Hypogeum moves the lift's platform with his Position. False if he can't spawn.
        /// </summary>
        public bool BeginRise(Vector3 at)
        {
            var em = EnemyManager.Instance;
            if (em == null || def == null) return false;
            float ground = em.Height != null ? em.Height.Sample(at.x, at.z) : at.y;
            Vector3 to = Player.Main != null ? Flat(Player.Main.transform.position - at) : Vector3.zero;
            float yaw = to.sqrMagnitude > 1e-4f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : 0f;
            if (!SpawnBody(new Vector3(at.x, ground - def.riseDepth, at.z), yaw)) return false;
            _rising = true;
            _riseGround = ground;
            _riseFrom = _pos.y;
            _entryEnd = _pos;
            _shoveCd = 0f;
            _em.SetScripted(_slot, _pos, _yaw, false); // below the deck from the first frame (Spawn put him on the ground)
            var root = _em.GetViewRoot(_slot);
            if (root != null) root.SetPositionAndRotation(_pos, Quaternion.Euler(0f, _yaw, 0f));
            SfxPlayer.Play(SfxId.Gate, new Vector3(at.x, ground, at.z)); // the lift's machinery below
            Enter(State.Entering, def.riseTime);
            return true;
        }

        /// <summary>His body (a Scripted enemy) at `at`, invulnerable, facing `yaw`, with his drivable parts found.</summary>
        bool SpawnBody(Vector3 at, float yaw)
        {
            _em = EnemyManager.Instance;
            if (_em == null || def == null || def.enemy == null) return false;
            _pos = at;
            _slot = _em.Spawn(def.enemy, _pos);
            if (_slot < 0) return false;
            _uid = _em.GetUid(_slot);
            _em.SetInvulnerable(_slot, true);
            if (def.falseDeath) _em.SetHpFloor(_slot, HpFloor); // R2-STUB(enemies): a lethal hit in any phase leaves him at 1 HP for his remains
            _yaw = yaw;
            _rig = _em.GetRig(_slot);
            var root = _em.GetViewRoot(_slot);
            _flail = Find(root, "Flail");
            if (_flail != null) _flailBind = _flail.localRotation;
            _spear = Find(root, "Spear");
            if (_spear != null) _spearBind = _spear.localRotation;
            _wingL = Find(root, "WingL");
            if (_wingL != null) _wingLBind = _wingL.localRotation;
            _wingR = Find(root, "WingR");
            if (_wingR != null) _wingRBind = _wingR.localRotation;
            _spearTilt = _spearGoal = _flap = 0f;
            FindFalx(root);
            FindBearerLegs(root);
            _guarded = false;
            _phase = 1;
            _enrageTo = 2;
            _rising = _remainsGrace = false;
            _discsLeft = 0;
            Defeated = false;
            Active = this;
            return true;
        }

        /// <summary>The part that flies for the Falx Throw (def.discPart), its bind pose, and its middle in its own space
        /// (it spins about that, not about the grip).</summary>
        void FindFalx(Transform root)
        {
            _discFlying = false;
            _falx = string.IsNullOrEmpty(def.discPart) ? null : Find(root, def.discPart);
            if (_falx == null) return;
            _falxBind = _falx.localRotation;
            _falxBindPos = _falx.localPosition;
            var mf = _falx.GetComponentInChildren<MeshFilter>(true);
            _falxCenter = mf != null && mf.sharedMesh != null
                ? _falx.InverseTransformPoint(mf.transform.TransformPoint(mf.sharedMesh.bounds.center)) : Vector3.zero;
        }

        static Transform Find(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = Find(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        void Enter(State s, float duration)
        {
            _state = s;
            _t = 0f;
            _dur = Mathf.Max(0.01f, duration);
        }

        /// <summary>Starts `attack` now (tests, admin). Ignored while entering, enraging, lying as remains or dead.</summary>
        public void ForceAttack(BossAttack attack)
        {
            if (_state == State.Entering || _state == State.Roaring || _state == State.Enraging || _state == State.Dead || _state == State.Remains) return;
            if (_discFlying) CatchFalx(); // a forced attack mid-throw: the falx is back in his hands
            StartAttack(attack);
        }

        /// <summary>Calls the guards now (tests, admin).</summary>
        public void ForceGuards() => CallGuards();

        /// <summary>Puts him at `position` (on the ground) facing `yaw` degrees (tests, admin).</summary>
        public void Warp(Vector3 position, float yaw)
        {
            if (!Alive) return;
            _pos = position;
            if (_em.Height != null) _pos.y = _em.Height.Sample(position.x, position.z);
            _yaw = yaw;
            _em.SetScripted(_slot, _pos, _yaw, false);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (_slot < 0 || _state == State.Dead) return;
            if (!Alive) { Die(); return; }
            if (dt <= 0f) return;
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            Vector3 pp = _player != null ? _player.position : _pos;
            _clock += dt;
            _t += dt;
            float u = Mathf.Clamp01(_t / _dur);
            var tele = TelegraphRenderer.Instance;
            _goal = PartPose.Identity;
            _spearGoal = 0f;

            bool busy = _state == State.Entering || _state == State.Roaring || _state == State.Enraging || _state == State.Remains;
            int enrage = busy ? 0 : _phase == 1 && Hp01 <= def.phase2At ? 2
                       : _phase == 2 && def.phase3At > 0f && Hp01 <= def.phase3At ? 3 : 0; // a hit past both: P2's roar, then P3's
            if (enrage > 0)
            {
                BeginEnrage(enrage);
                u = 0f; // the roar starts now (u was the previous state's: a long chase would end the roar this frame)
            }
            else if (!busy && def.falseDeath && FalseDeaths == 0 && _phase >= LastPhase && _em.GetHp(_slot) <= HpFloor + 0.01f)
            {
                EnterRemains(); // "HE WILL NOT STAY DOWN!"
                u = 0f;
            }

            switch (_state)
            {
                case State.Entering:
                    if (_rising) RiseTick(u, pp, dt);
                    else Walk(_entryEnd, def.moveSpeed * 0.8f, dt);
                    if (u >= 1f || (!_rising && Flat(_entryEnd - _pos).sqrMagnitude < 0.3f))
                    {
                        if (_rising) Risen();
                        Enter(State.Roaring, entranceRoar);
                        SfxPlayer.Play2D(SfxId.BossRoar);
                        ScreenShake.Add(0.4f);
                        if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.9f);
                    }
                    break;
                case State.Roaring:
                    Face(pp, dt);
                    RoarPose();
                    if (u >= 1f) { _em.SetInvulnerable(_slot, false); Enter(State.Chase, 0.6f); _chaseMin = 0.6f; }
                    break;
                case State.Enraging:
                    RoarPose();
                    _goal.RootScale = Vector3.one * (1f + 0.06f * Mathf.Sin(_clock * 30f) * (1f - u));
                    if (u >= 1f)
                    {
                        _phase = _enrageTo;
                        bool extras = !_quietRoar; // the roar as he rises from his remains: none of the phase's extras again
                        _quietRoar = false;
                        if (extras && _phase == 2 && def.phase2FireRing) LightTheFence();
                        _em.SetInvulnerable(_slot, false);
                        Enter(State.Chase, 0.5f);
                        _chaseMin = 0.5f;
                        if (extras && _phase > 1 && _phase == def.guardsFromPhase) CallGuards(); // "Praetorians, to me!": the first wave as the roar ends
                    }
                    break;
                case State.Chase:
                {
                    float d = Flat(pp - _pos).magnitude;
                    bool far = def.closeInRange > 0f && d > def.closeInRange; // he strides in instead of attacking at nothing
                    if (_falx != null) { _goal.ArmR = new Vector3(-25f, 0f, -15f); _goal.ArmL = new Vector3(-45f, 0f, 30f); } // the falx held low across his body
                    if (d > def.enemy.radius + 1.2f) Walk(pp, far ? Mathf.Max(def.closeInSpeed, MoveSpeed) : MoveSpeed, dt);
                    else Face(pp, dt);
                    if (_t >= _chaseMin && !far)
                    {
                        var a = Choose(d);
                        if (a != BossAttack.None) StartAttack(a);
                    }
                    break;
                }
                case State.Windup:
                {
                    bool marks = _attack == BossAttack.Slam || _attack == BossAttack.Pounce; // he watches the spot he marks
                    Face(_attack == BossAttack.Slam ? pp : _attack == BossAttack.Pounce ? _target : _pos + _aim, dt * (marks ? 1f : 0.2f));
                    WindupTick(pp, u, tele);
                    if (u >= 1f) { Enter(State.Strike, StrikeTime()); _hitDone = false; _chargeTravel = 0f; StrikeBegin(pp); }
                    break;
                }
                case State.Strike:
                    StrikeTick(pp, u, dt);
                    if (u >= 1f && _attack != BossAttack.Charge && _attack != BossAttack.Pounce && _attack != BossAttack.Disc)
                    {
                        if (_attack == BossAttack.Flurry && _flurryLeft > 0) { _flurryLeft--; StartAttack(BossAttack.Flurry, true); } // re-aims
                        else { AttacksDone++; _last = _attack; Enter(State.Recover, def.recover / Speed); }
                    }
                    break;
                case State.Recover:
                    if (_attack == BossAttack.Pounce) { _goal.Body = new Vector3(32f, 0f, 0f); _goal.RootY = -0.25f * (1f - u); } // getting up
                    if (u >= 1f) { _attack = BossAttack.None; Enter(State.Chase, 0f); _chaseMin = 0.45f / Speed; }
                    break;
                case State.Stunned:
                    _goal.Body = new Vector3(28f, 0f, 0f);
                    _goal.Head = new Vector3(25f, 10f * Mathf.Sin(_clock * 9f), 0f);
                    _goal.ArmL = new Vector3(25f, 0f, 30f); // guard down
                    if (u >= 1f) { EndStun(); AttacksDone++; _last = _attack; _attack = BossAttack.None; Enter(State.Recover, def.recover / Speed); }
                    break;
                case State.Remains:
                    RemainsTick(pp, u, tele);
                    break;
            }

            if (_discFlying) FalxTick(pp, dt, tele); // it comes back even if he roars meanwhile
            // the guard clock runs through the whole fight from guardsFromPhase (not just between attacks: a boss that
            // attacks back to back would never call them), but not during his entrance, a phase roar or his remains
            if (_state != State.Entering && _state != State.Roaring && _state != State.Enraging && _state != State.Remains
                && _phase >= def.guardsFromPhase) GuardsClock(dt);
            if (def.guardedBy != null) Payroll(tele, dt);
            ShieldUp = def.enemy.blockArc > 0f && _state != State.Stunned && _state != State.Entering;
            _em.SetScriptedShield(_slot, ShieldUp);
            bool leaping = _state == State.Strike && _attack == BossAttack.Pounce; // in the air: the landing is the hit
            if (!leaping && _state != State.Remains) Contact(pp, dt);
            _em.SetScripted(_slot, _pos, _yaw, _state == State.Windup);
            Animate(dt);
        }

        /// <summary>He roars into phase `to` (invulnerable for enrageRoar). Phase 2's arena extras fire only on the way to
        /// phase 2: Gorm's fence is lit as that roar ends, the spikes, cages, jets and blades on it.</summary>
        void BeginEnrage(int to)
        {
            _em.SetInvulnerable(_slot, true);
            EndStun();
            Enter(State.Enraging, def.enrageRoar);
            _enrageTo = to;
            SfxPlayer.Play2D(SfxId.BossRoar);
            string text = to == 2 ? def.phase2Text : def.phase3Text;
            if (to == 2 || !string.IsNullOrEmpty(text))
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossPhase, Text = text, Color = Danger, Seconds = 2.2f });
            if (to == 2)
            {
                if (def.phase2SpikeVolley && SpikeTraps.Instance != null) SpikeTraps.Instance.FireAll(); // on his roar
                if (def.phase2CagePack > 0 && BeastCages.Instance != null) BeastCages.Instance.OpenAll(def.phase2CagePack); // "Feed them!"
                if (def.phase2FireJets && FireJets.Instance != null) FireJets.Instance.FireAll(); // "Everything is for sale!"
                if (def.phase2Blades && BladeSweepers.Instance != null) BladeSweepers.Instance.SpinForever();
            }
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(1f);
            ScreenShake.Add(0.45f);
        }

        // ---------- movement ----------

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void Face(Vector3 at, float dt)
        {
            Vector3 d = Flat(at - _pos);
            if (d.sqrMagnitude < 1e-4f) return;
            _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, def.turnRate * dt);
        }

        void Walk(Vector3 goal, float speed, float dt)
        {
            Vector3 d = Flat(goal - _pos);
            float len = d.magnitude;
            if (len < 0.01f) return;
            var mud = MudZones.Instance;
            float v = speed * (mud != null && mud.Inside(_pos) ? mud.enemySpeed : 1f);
            Step(d / len * Mathf.Min(len, v * dt));
            Face(goal, dt);
            _walk += dt * v * 1.6f;
        }

        /// <summary>Moves by `delta` respecting obstacles, the arena edge and the ground. True if something stopped it.</summary>
        bool Step(Vector3 delta)
        {
            Vector3 want = _pos + delta;
            Vector3 np = want;
            float r = def.enemy.radius;
            bool blocked = _em.Obstacles != null && _em.Obstacles.PushOut(ref np, r);
            if (_em.ClampInside(ref np, r)) blocked = true;
            np.y = _em.Height != null ? _em.Height.Sample(np.x, np.z) : _pos.y;
            _pos = np;
            return blocked && Flat(np - want).sqrMagnitude > 0.0004f;
        }

        // ---------- attacks ----------

        /// <summary>How much he wants `a` with the player `d` metres away (0 = out of its range).</summary>
        float Weight(BossAttack a, float d) => a switch
        {
            BossAttack.Sweep => d < def.sweepRadius * 0.85f ? 3f : 0f,
            BossAttack.Whip => d > 3f && d < def.whipLength * 0.9f ? 3f : 0f,
            BossAttack.Slam => 2f,
            BossAttack.Charge => d > 5f && d < def.chargeLength ? 2.5f : 0f,
            BossAttack.Bash => d < def.bashRange * 0.9f ? 3f : 0f,
            BossAttack.Flurry => d > 1.5f && d < def.flurryLength * 0.95f ? 2.5f : 0f,
            BossAttack.Pounce => d > 3f ? 2.2f : 0.4f,
            BossAttack.CoinBombs => d > 2.5f ? 2.4f : 0.6f,
            BossAttack.Volley => d > 4f && d < def.volleyRange * 0.8f ? 2.6f : 0f,
            BossAttack.Disc => d > 4f && d < def.discLength * 0.9f ? 2.4f : 0f,
            _ => 0f,
        };

        /// <summary>A weighted pick from this phase's attacks (the last one is less likely). If none is in range: the
        /// charge (any range), else the pounce, the bombs or the slam; with none of those, None (he keeps closing in).</summary>
        BossAttack Choose(float d)
        {
            var list = _phase >= 3 && def.phase3Attacks != null && def.phase3Attacks.Length > 0 ? def.phase3Attacks
                     : _phase >= 2 ? def.phase2Attacks : def.phase1Attacks;
            if (list == null || list.Length == 0) return BossAttack.Slam;
            float total = 0f;
            for (int k = 0; k < list.Length; k++)
            {
                float w = Weight(list[k], d) * (list[k] == _last ? 0.3f : 1f);
                _weights[k % _weights.Length] = w;
                total += w;
            }
            if (total > 0f)
            {
                float r = GameRandom.Value * total;
                for (int k = 0; k < list.Length; k++)
                    if ((r -= _weights[k % _weights.Length]) < 0f) return list[k];
            }
            bool charge = false, slam = false, pounce = false, bombs = false;
            foreach (var a in list) { charge |= a == BossAttack.Charge; slam |= a == BossAttack.Slam; pounce |= a == BossAttack.Pounce; bombs |= a == BossAttack.CoinBombs; }
            return charge ? BossAttack.Charge : pounce ? BossAttack.Pounce : bombs ? BossAttack.CoinBombs : slam ? BossAttack.Slam : BossAttack.None;
        }

        void StartAttack(BossAttack a, bool chained = false)
        {
            if (!chained) // a fresh flurry / charge sets how many follow; chained ones re-aim and count down
            {
                _flurryLeft = a == BossAttack.Flurry ? Mathf.Max(1, _phase >= 2 ? def.flurryThrusts.y : def.flurryThrusts.x) - 1 : 0;
                _chargesLeft = a == BossAttack.Charge ? Mathf.Max(1, ChargeRepeats) - 1 : 0;
                _pouncesLeft = a == BossAttack.Pounce ? Mathf.Max(1, PounceRepeats) - 1 : 0;
                _discsLeft = a == BossAttack.Disc ? Mathf.Max(1, DiscRepeats) - 1 : 0;
            }
            _attack = a;
            Vector3 pp = _player != null ? _player.position : _pos + Vector3.forward;
            _aim = Flat(pp - _pos);
            if (_aim.sqrMagnitude < 1e-4f) _aim = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            _aim.Normalize();
            _target = a == BossAttack.Pounce ? LeapTarget(pp) : pp;
            float w = a switch
            {
                BossAttack.Sweep => def.sweepWindup,
                BossAttack.Whip => def.whipWindup,
                BossAttack.Slam => def.slamWindup,
                BossAttack.Bash => def.bashWindup,
                BossAttack.Flurry => def.flurryWindup,
                BossAttack.Pounce => def.pounceWindup,
                BossAttack.CoinBombs => def.bombWindup,
                BossAttack.Volley => def.volleyWindup,
                BossAttack.Disc => def.discWindup,
                _ => def.chargeWindup,
            };
            Enter(State.Windup, w / Speed);
        }

        /// <summary>Where a pounce at `pp` lands: no farther than pounceRange, inside the arena, on the ground (the stage
        /// platform too: he follows you up).</summary>
        Vector3 LeapTarget(Vector3 pp)
        {
            Vector3 d = Flat(pp - _pos);
            float m = d.magnitude;
            Vector3 t = m > def.pounceRange ? _pos + d / m * def.pounceRange : pp;
            _em.ClampInside(ref t, def.enemy.radius);
            t.y = _em.Height != null ? _em.Height.Sample(t.x, t.z) : pp.y;
            return t;
        }

        float PounceFlight() => def.pounceFlight * Mathf.Clamp(Flat(_target - _pos).magnitude / 10f, 0.8f, 1.6f);

        float StrikeTime() => _attack switch
        {
            BossAttack.Sweep => 0.3f,
            BossAttack.Whip => 0.18f,
            BossAttack.Slam => 0.25f,
            BossAttack.Bash => 0.25f,
            BossAttack.Flurry => 0.12f,
            BossAttack.Pounce => PounceFlight(),
            BossAttack.CoinBombs => (BombCount - 1) * def.bombInterval / Speed + 0.2f,
            BossAttack.Volley => 0.25f,
            BossAttack.Disc => 2f * def.discLength / Mathf.Max(1f, def.discSpeed) + 1.5f, // a cap: the catch ends it
            _ => def.chargeLength / Mathf.Max(1f, def.chargeSpeed) + 0.2f,
        };

        float ChargeWidth => def.chargeWidth > 0f ? def.chargeWidth : def.enemy.radius * 2f;
        int ChargeRepeats => _phase >= 3 && def.phase3ChargeRepeats > 0 ? def.phase3ChargeRepeats : _phase >= 2 ? def.chargeRepeats.y : def.chargeRepeats.x;
        int PounceRepeats => _phase >= 3 && def.phase3PounceRepeats > 0 ? def.phase3PounceRepeats : _phase >= 2 ? def.pounceRepeats.y : def.pounceRepeats.x;
        int DiscRepeats => _phase >= 3 && def.phase3DiscRepeats > 0 ? def.phase3DiscRepeats : _phase >= 2 ? def.discRepeats.y : def.discRepeats.x;
        int BombCount => Mathf.Max(1, _phase >= 2 ? def.bombCount.y : def.bombCount.x);
        int VolleyCount => Mathf.Max(1, _phase >= 2 ? def.volleyCount.y : def.volleyCount.x);
        float VolleyArc => _phase >= 2 ? def.volleyArc.y : def.volleyArc.x;

        /// <summary>Coin `k` of the volley's direction (the fan is centred on his aim).</summary>
        Vector3 VolleyDir(int k)
        {
            int n = VolleyCount;
            float a = n > 1 ? -VolleyArc * 0.5f + VolleyArc * k / (n - 1) : 0f;
            return Quaternion.Euler(0f, a, 0f) * _aim;
        }

        /// <summary>The Spear's tilt (deg about its local X, on top of its bind) that levels the blade forward with the spear
        /// arm at pose `armX` (its Euler X: negative swings it forward). Spear upright at bind (R1-char-art); sign
        /// unverified in Unity.</summary>
        static float SpearLevel(float armX) => 90f - armX;

        /// <summary>Where his hand is (a purse or a coin leaves from here): up on the litter, a little forward.</summary>
        Vector3 Hand => _pos + Vector3.up * (def.enemy.radius > 1.2f ? 2.2f : 1.6f) + _aim * 0.6f;

        void WindupTick(Vector3 pp, float u, TelegraphRenderer tele)
        {
            var col = Danger;
            switch (_attack)
            {
                case BossAttack.Sweep:
                    if (tele != null) tele.Circle(_pos, def.sweepRadius, u, col, TelegraphRenderer.Style.Telegraph, 0.06f);
                    _spin += Time.deltaTime * (300f + 900f * u);
                    if (_falx != null) // the Reaper's Circle: twisted back, the falx out low behind him
                    {
                        _goal.ArmR = new Vector3(-80f, 0f, -70f);
                        _goal.ArmL = new Vector3(-65f, 0f, 45f);
                        _goal.Body = new Vector3(8f * u, -45f * u, 0f);
                        break;
                    }
                    _goal.ArmR = new Vector3(-70f, 0f, -55f);
                    _goal.Body = new Vector3(0f, -25f * u, 0f);
                    break;
                case BossAttack.Whip:
                    if (tele != null) tele.Lane(_pos, _aim, def.whipLength, def.whipWidth, u, col);
                    if (def.whipGrasp) // Grasp of the Dead: the falx raised in both hands, to be driven into the deck
                    {
                        _goal.ArmL = new Vector3(-160f * u, 0f, 15f);
                        _goal.ArmR = new Vector3(-160f * u, 0f, -15f);
                        _goal.Body = new Vector3(-10f * u, 0f, 0f);
                        break;
                    }
                    if (_spear != null) // the Spear Thrust: the spear arm drawn back, the blade levelled down the lane
                    {
                        _goal.ArmR = new Vector3(25f * u, 0f, 0f);
                        _goal.Body = new Vector3(-6f, -15f * u, 0f);
                        _spearGoal = SpearLevel(_goal.ArmR.x);
                        break;
                    }
                    _goal.ArmL = new Vector3(45f, 0f, 30f);
                    _goal.Body = new Vector3(-8f, 15f, 0f);
                    break;
                case BossAttack.Slam:
                    if (u < def.slamTrack) _target = Vector3.Lerp(_target, pp, 1f - Mathf.Exp(-6f * Time.deltaTime));
                    if (tele != null) tele.Circle(_target, def.slamRadius, u, col, TelegraphRenderer.Style.Telegraph, 0.07f);
                    _goal.ArmR = new Vector3(-165f, 0f, 0f);
                    _goal.Body = new Vector3(-12f, 0f, 0f);
                    break;
                case BossAttack.Charge:
                    if (tele != null) tele.Lane(_pos, _aim, def.chargeLength, ChargeWidth, u, col);
                    _goal.Body = new Vector3(28f, 0f, 0f);
                    _goal.ArmL = new Vector3(-60f, 0f, 0f); // shield up in front
                    _goal.LegL = new Vector3(-30f, 0f, 0f);
                    _goal.LegR = new Vector3(20f, 0f, 0f);
                    break;
                case BossAttack.Bash:
                    if (tele != null) tele.Sector(_pos, _aim, def.bashRange, def.bashAngle, u, col);
                    _goal.ArmL = new Vector3(-15f, 0f, 25f * u); // shield drawn in
                    _goal.Body = new Vector3(-10f * u, -22f * u, 0f);
                    break;
                case BossAttack.Flurry:
                    if (tele != null) tele.Lane(_pos, _aim, def.flurryLength, def.flurryWidth, u, col);
                    if (_falx != null) _goal.ArmL = new Vector3(35f * u, 0f, 15f); // the Victor's Lunges: both hands on the haft
                    _goal.ArmR = new Vector3(35f * u, 0f, _falx != null ? -15f : 0f); // sword drawn back
                    _goal.Body = new Vector3(0f, -18f * u, 0f);
                    break;
                case BossAttack.Disc:
                    if (tele != null) tele.Lane(_pos, _aim, def.discLength, def.discWidth, u, col);
                    _goal.ArmR = new Vector3(-150f * u, 0f, -35f); // the falx drawn back high
                    _goal.ArmL = new Vector3(-40f * u, 0f, 30f);
                    _goal.Body = new Vector3(-8f * u, -35f * u, 0f); // twisted
                    break;
                case BossAttack.CoinBombs:
                    _goal.ArmR = new Vector3(-150f * u, 0f, -20f); // the purse drawn back over his shoulder
                    _goal.Body = new Vector3(-10f * u, 15f * u, 0f);
                    break;
                case BossAttack.Volley:
                    if (tele != null)
                        for (int k = 0; k < VolleyCount; k++) tele.Lane(_pos, VolleyDir(k), def.volleyRange, 0.45f, u, col, 0.08f);
                    if (def.volleyJavelins) // javelins drawn from the quiver: the arm back over the shoulder
                    {
                        _goal.ArmR = new Vector3(-150f * u, 0f, -15f);
                        _goal.Body = new Vector3(-10f * u, -20f * u, 0f);
                        _spearGoal = -_goal.ArmR.x; // the spear stays upright in the fist
                        break;
                    }
                    _goal.ArmR = new Vector3(-60f, 0f, -70f * u); // a fistful of coins swept back
                    _goal.Body = new Vector3(0f, -25f * u, 0f);
                    break;
                case BossAttack.Pounce:
                    // the circle trails you (lagging) until pounceTrack, then locks: keep moving
                    if (u < def.pounceTrack) _target = Vector3.Lerp(_target, LeapTarget(pp), 1f - Mathf.Exp(-3.5f * Time.deltaTime));
                    if (tele != null) tele.Circle(_target, def.pounceRadius, u, col, TelegraphRenderer.Style.Telegraph, 0.07f);
                    _goal.Body = new Vector3(30f * u, 0f, 0f); // crouched, coiled
                    _goal.RootY = -0.3f * u;
                    _goal.ArmL = new Vector3(35f, 0f, 25f);
                    _goal.ArmR = new Vector3(35f, 0f, -25f);
                    break;
            }
        }

        void StrikeBegin(Vector3 pp)
        {
            var burst = _em.deathPop;
            switch (_attack)
            {
                case BossAttack.Sweep:
                {
                    Vector3 d = Flat(pp - _pos);
                    float reach = def.sweepRadius + playerRadius;
                    float feet = _player != null ? _player.position.y - (_em.Height != null ? _em.Height.Sample(pp.x, pp.z) : 0f) : 0f;
                    if (d.sqrMagnitude < reach * reach && feet < def.sweepJumpClear)
                        HitPlayer(def.sweepDamage, d.sqrMagnitude > 1e-4f ? d.normalized : _aim, def.sweepKnockback);
                    if (burst != null) burst.Burst(_pos + Vector3.up * 0.4f, 24, 9f, 0.25f, new Color(0.55f, 0.42f, 0.3f), Danger);
                    SfxPlayer.Play(SfxId.Slam, _pos);
                    ScreenShake.Add(0.2f);
                    break;
                }
                case BossAttack.Whip:
                {
                    Vector3 rel = Flat(pp - _pos);
                    float along = Vector3.Dot(rel, _aim);
                    float side = (rel - _aim * along).magnitude;
                    if (along > -0.5f && along < def.whipLength + playerRadius && side < def.whipWidth * 0.5f + playerRadius)
                    {
                        bool landed = HitPlayer(def.whipDamage, _aim, 3f);
                        if (_motor != null && def.whipSlow < 1f && def.whipSlowSeconds > 0f) _motor.Slow(def.whipSlow, def.whipSlowSeconds); // the spear doesn't slow
                        if (landed && def.whipGrasp) Grasps++; // held (whipSlow 0.1)
                    }
                    if (def.whipGrasp) // Grasp of the Dead: bone hands burst up all along the lane (not the crack)
                    {
                        if (burst != null)
                            for (int k = 0; k < 6; k++)
                                burst.Burst(_pos + _aim * (def.whipLength * (k + 0.5f) / 6f) + Vector3.up * 0.2f, 8, 4.5f, 0.18f, def.remainsColor, Color.white);
                        SfxPlayer.Play(SfxId.Slam, _pos + _aim * (def.whipLength * 0.5f));
                        ScreenShake.Add(0.15f);
                        break;
                    }
                    if (burst != null) burst.Burst(_pos + _aim * def.whipLength + Vector3.up * 0.3f, 8, 4f, 0.15f, Danger, Color.white);
                    SfxPlayer.Play(SfxId.Crit, _pos + _aim * (def.whipLength * 0.5f));
                    break;
                }
                case BossAttack.Slam:
                {
                    Vector3 d = Flat(pp - _target);
                    float reach = def.slamRadius + playerRadius;
                    float feet = _player != null ? _player.position.y - (_em.Height != null ? _em.Height.Sample(pp.x, pp.z) : 0f) : 0f;
                    if (d.sqrMagnitude < reach * reach && feet < 0.6f)
                        HitPlayer(def.slamDamage, d.sqrMagnitude > 1e-4f ? d.normalized : _aim, 7f);
                    if (burst != null) burst.Burst(_target + Vector3.up * 0.2f, 28, 8f, 0.28f, new Color(0.5f, 0.38f, 0.28f), Danger);
                    SfxPlayer.Play(SfxId.Slam, _target);
                    ScreenShake.Add(0.35f);
                    if (_phase >= 2 && HazardZones.Instance != null)
                        HazardZones.Instance.Add(_target, def.slamFire.x, def.slamFire.y, def.slamFire.z);
                    break;
                }
                case BossAttack.Bash:
                {
                    Vector3 rel = Flat(pp - _pos);
                    float dist = rel.magnitude;
                    if (dist < def.bashRange + playerRadius && (dist < def.enemy.radius + 0.3f || Vector3.Angle(rel, _aim) < def.bashAngle * 0.5f))
                        HitPlayer(def.bashDamage, dist > 0.01f ? rel / dist : _aim, def.bashKnockback);
                    if (burst != null) burst.Burst(_pos + _aim * (def.enemy.radius + 0.8f) + Vector3.up * 0.3f, 18, 7f, 0.2f, new Color(0.55f, 0.42f, 0.3f), Danger);
                    SfxPlayer.Play(SfxId.Slam, _pos);
                    ScreenShake.Add(0.25f);
                    break;
                }
                case BossAttack.Pounce:
                    _leapFrom = _pos;
                    SfxPlayer.Play(SfxId.Swing, _pos);
                    break;
                case BossAttack.CoinBombs:
                    _bombsLeft = BombCount;
                    _bombNext = 0f;
                    break;
                case BossAttack.Volley:
                {
                    var proj = EnemyProjectiles.Instance;
                    Vector3 from = Hand;
                    for (int k = 0; k < VolleyCount && proj != null; k++)
                    {
                        Vector3 d = VolleyDir(k);
                        float reach = Mathf.Max(4f, Flat(pp - _pos).magnitude);
                        Vector3 at = _pos + d * reach + Vector3.up * 0.9f; // level with your chest where you stand, then on past
                        if (def.volleyJavelins)
                        {
                            at.y = pp.y + 0.9f; // at your height, not his: a straight shot dies when it dips below the ground (the spina)
                            if (proj.FireStraight(from, at, def.volleySpeed, def.volleyDamage, 0.3f, def.volleyRange, arrow: true) >= 0) JavelinsFired++;
                        }
                        else if (proj.FireCoin(from, at, def.volleySpeed, def.volleyDamage, 0.3f, def.volleyRange) >= 0) CoinsFired++;
                    }
                    SfxPlayer.Play(SfxId.Throw, _pos);
                    if (!def.volleyJavelins) SfxPlayer.Play(SfxId.Coin, _pos);
                    break;
                }
                case BossAttack.Charge:
                    Charges++;
                    break;
                case BossAttack.Disc:
                    ThrowFalx();
                    break;
                case BossAttack.Flurry:
                {
                    Vector3 rel = Flat(pp - _pos);
                    float along = Vector3.Dot(rel, _aim);
                    float side = (rel - _aim * along).magnitude;
                    if (along > -0.5f && along < def.flurryLength + playerRadius && side < def.flurryWidth * 0.5f + playerRadius)
                        HitPlayer(def.flurryDamage, _aim, 2.5f);
                    if (burst != null) burst.Burst(_pos + _aim * def.flurryLength * 0.6f + Vector3.up * 1f, 6, 4f, 0.12f, Color.white, Danger);
                    SfxPlayer.Play(SfxId.Thrust, _pos + _aim);
                    break;
                }
            }
        }

        void StrikeTick(Vector3 pp, float u, float dt)
        {
            switch (_attack)
            {
                case BossAttack.Sweep:
                    _spin += dt * 1500f;
                    if (_falx != null) // a full turn of his whole body with the falx out at knee height (StrikeTime 0.3 s)
                    {
                        _yaw += dt * 360f / 0.3f;
                        _goal.ArmR = new Vector3(-85f, 0f, -85f);
                        _goal.ArmL = new Vector3(-80f, 0f, 60f);
                        _goal.Body = new Vector3(15f, 0f, 0f);
                        break;
                    }
                    _goal.ArmR = new Vector3(-80f, 0f, -80f);
                    _goal.Body = new Vector3(0f, 40f, 0f);
                    break;
                case BossAttack.Whip:
                    if (def.whipGrasp) // the falx driven into the deck, leaning into it
                    {
                        _goal.ArmL = new Vector3(-50f, 0f, 10f);
                        _goal.ArmR = new Vector3(-50f, 0f, -10f);
                        _goal.Body = new Vector3(35f, 0f, 0f);
                        break;
                    }
                    if (_spear != null) // lunged down the lane, the blade level
                    {
                        _goal.ArmR = new Vector3(-55f, 0f, 0f);
                        _goal.Body = new Vector3(12f, 12f, 0f);
                        _spearGoal = SpearLevel(_goal.ArmR.x);
                        break;
                    }
                    _goal.ArmL = new Vector3(-110f, 0f, 0f);
                    _goal.Body = new Vector3(10f, -10f, 0f);
                    break;
                case BossAttack.Slam:
                    _goal.ArmR = new Vector3(-15f, 0f, 0f);
                    _goal.Body = new Vector3(18f, 0f, 0f);
                    break;
                case BossAttack.Bash:
                    _goal.ArmL = new Vector3(-85f, 0f, 0f); // shield punched forward
                    _goal.Body = new Vector3(15f, 12f, 0f);
                    break;
                case BossAttack.Flurry:
                    if (_falx != null) _goal.ArmL = new Vector3(-90f, 0f, 12f); // both arms thrust
                    _goal.ArmR = new Vector3(-90f, 0f, _falx != null ? -12f : 0f); // thrust
                    _goal.Body = new Vector3(8f, 15f, 0f);
                    break;
                case BossAttack.Disc:
                    // empty-handed, facing the falx until it's back: the window to hit him; then again (re-aimed) or recover
                    if (_discFlying && u >= 1f) CatchFalx(); // a cap, should it never find his hand
                    if (_discFlying)
                    {
                        Face(_discPos, dt);
                        _goal.ArmR = new Vector3(_discBack ? -70f : -85f, 0f, -20f); // flung, then reaching for it
                        _goal.ArmL = new Vector3(-20f, 0f, 25f);
                        _goal.Body = new Vector3(10f, 20f, 0f);
                        break;
                    }
                    if (_discsLeft > 0) { _discsLeft--; StartAttack(BossAttack.Disc, true); } // phase 3: again
                    else { AttacksDone++; _last = BossAttack.Disc; Enter(State.Recover, def.recover / Speed); }
                    break;
                case BossAttack.CoinBombs:
                {
                    _goal.ArmR = new Vector3(-40f + 30f * Mathf.Sin(_t * 18f), 0f, -20f); // throwing, purse after purse
                    var proj = EnemyProjectiles.Instance;
                    while (_bombsLeft > 0 && _t >= _bombNext && proj != null)
                    {
                        int k = BombCount - _bombsLeft;
                        Vector3 at = pp;
                        if (k > 0) // the rest around you: where you'd go, and beside it
                        {
                            Vector3 v = _motor != null ? Flat(_motor.Velocity) : Vector3.zero;
                            float a = GameRandom.Range(0f, Mathf.PI * 2f);
                            at = pp + v * (def.bombFlight * 0.5f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * GameRandom.Range(def.bombSpread * 0.4f, def.bombSpread);
                            _em.ClampInside(ref at, 0.5f);
                        }
                        proj.Bomb(Hand, at, def.bombFlight, def.bombRadius, def.bombDamage, def.bombCoins);
                        BombsThrown++;
                        SfxPlayer.Play(SfxId.Throw, _pos);
                        _bombsLeft--;
                        _bombNext += def.bombInterval / Speed;
                    }
                    break;
                }
                case BossAttack.Volley:
                    if (def.volleyJavelins)
                    {
                        _goal.ArmR = new Vector3(-80f, 0f, 0f); // flung forward
                        _goal.Body = new Vector3(12f, 18f, 0f);
                        _spearGoal = -_goal.ArmR.x;
                        break;
                    }
                    _goal.ArmR = new Vector3(-90f, 0f, 40f); // flung
                    _goal.Body = new Vector3(0f, 20f, 0f);
                    break;
                case BossAttack.Pounce:
                {
                    // an arc over everything (stone too) onto the marked spot
                    Vector3 p = Vector3.Lerp(_leapFrom, _target, u);
                    p.y = Mathf.Lerp(_leapFrom.y, _target.y, u) + 4f * def.pounceHeight * u * (1f - u);
                    _pos = p;
                    _goal.Body = new Vector3(-15f, 0f, 0f);
                    _goal.ArmL = new Vector3(-130f, 0f, 20f);
                    _goal.ArmR = new Vector3(-130f, 0f, -20f);
                    _goal.LegL = new Vector3(-45f, 0f, 0f);
                    _goal.LegR = new Vector3(25f, 0f, 0f);
                    if (u >= 1f) PounceLands(pp);
                    break;
                }
                case BossAttack.Charge:
                {
                    float stepLen = def.chargeSpeed * dt;
                    // barricades and braziers in his way go first (gone for the run), so only stone stops him
                    var breakables = ArenaBreakables.Instance;
                    if (breakables != null) breakables.SmashAlong(_pos, _pos + _aim * (stepLen + def.enemy.radius), ChargeWidth * 0.5f);
                    bool wall = Step(_aim * stepLen);
                    _goal.ArmL = new Vector3(-60f, 0f, 0f);
                    _chargeTravel += stepLen;
                    _walk += dt * 10f;
                    _goal.Body = new Vector3(30f, 0f, 0f);
                    if (!_hitDone && Flat(pp - _pos).magnitude < def.enemy.radius + playerRadius + 0.4f)
                    {
                        _hitDone = true;
                        Vector3 side = Vector3.Cross(Vector3.up, _aim);
                        if (Vector3.Dot(Flat(pp - _pos), side) < 0f) side = -side;
                        HitPlayer(def.chargeDamage, (side + _aim * 0.5f).normalized, def.chargeKnockback);
                    }
                    if (wall)
                    {
                        SfxPlayer.Play(SfxId.Slam, _pos);
                        ScreenShake.Add(0.3f);
                        if (_em.deathPop != null) _em.deathPop.Burst(_pos + _aim * def.enemy.radius + Vector3.up * 0.8f, 18, 6f, 0.22f, new Color(0.5f, 0.38f, 0.28f), Color.white);
                        BeginStun();
                    }
                    else if (_chargeTravel >= def.chargeLength || u >= 1f)
                    {
                        if (_chargesLeft > 0) { _chargesLeft--; StartAttack(BossAttack.Charge, true); } // phase 2: again, re-aimed
                        else
                        {
                            AttacksDone++;
                            _last = _attack;
                            Enter(State.Recover, def.recover / Speed);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>He lands: everything in the circle on the ground is hit and thrown out; then again (phase 2) or he's
        /// slow to get up.</summary>
        void PounceLands(Vector3 pp)
        {
            _pos = _target;
            Step(Vector3.zero); // out of stone, inside the arena, on the ground
            Pounces++;
            Vector3 d = Flat(pp - _pos);
            float reach = def.pounceRadius + playerRadius;
            float feet = _player != null ? _player.position.y - (_em.Height != null ? _em.Height.Sample(pp.x, pp.z) : 0f) : 0f;
            if (d.sqrMagnitude < reach * reach && feet < 0.6f)
            {
                HitPlayer(def.pounceDamage, d.sqrMagnitude > 1e-4f ? d.normalized : _aim, def.pounceKnockback);
                _contactCd = def.contactInterval; // landing on you is one hit, not two
            }
            if (_em.deathPop != null) _em.deathPop.Burst(_pos + Vector3.up * 0.2f, 30, 9f, 0.28f, new Color(0.62f, 0.52f, 0.38f), Danger);
            SfxPlayer.Play(SfxId.Slam, _pos);
            ScreenShake.Add(0.4f);
            if (_pouncesLeft > 0) { _pouncesLeft--; StartAttack(BossAttack.Pounce, true); } // phase 2: again, re-aimed
            else { AttacksDone++; _last = BossAttack.Pounce; Enter(State.Recover, def.pounceRecover / Speed); }
        }

        /// <summary>The charge hit stone: stunned, guard down (takes stunDamageTakenMul), the rest of a double charge lost.</summary>
        void BeginStun()
        {
            Enter(State.Stunned, def.chargeStun);
            _chargesLeft = 0;
            Stuns++;
            if (def.stunDamageTakenMul != 1f) _em.SetDamageTakenMul(_slot, def.stunDamageTakenMul);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.4f);
        }

        void EndStun()
        {
            if (_state == State.Stunned && _slot >= 0) _em.SetDamageTakenMul(_slot, 1f);
        }

        /// <summary>True if the hit landed (not dodged by the player's invulnerability).</summary>
        bool HitPlayer(float damage, Vector3 dir, float knockback)
        {
            if (_health == null) return false;
            dir.y = 0f;
            if (!_health.TakeDamage(new DamageInfo { Amount = damage, Flags = DamageFlags.Melee, Point = _player.position, Direction = dir })) return false;
            if (_motor != null && knockback > 0f) _motor.AddImpulse(dir * knockback + Vector3.up * knockback * 0.45f);
            return true;
        }

        /// <summary>Feet height above the ground under `pp` (a jump clears the circle attacks).</summary>
        float FeetAbove(Vector3 pp) => _player != null ? _player.position.y - (_em.Height != null ? _em.Height.Sample(pp.x, pp.z) : 0f) : 0f;

        // ---------- the falx (the Eternal Victor) ----------

        /// <summary>He hurls the falx down the lane from his hand: it flies out in world space (FalxTick).</summary>
        void ThrowFalx()
        {
            _discFrom = _pos;
            _discPos = Hand;
            _discDir = _aim;
            _discOut = _discSpinT = _discAge = 0f;
            _discBack = _discHitOut = _discHitBack = false;
            _discFlying = true;
            _falxOffset = Vector3.zero;
            if (_falx != null)
            {
                _falxRelease = _falx.rotation;
                _falxOffset = _falx.TransformPoint(_falxCenter) - _discPos; // drawn from where it was, easing onto its path
            }
            DiscsThrown++;
            SfxPlayer.Play(SfxId.Throw, _pos);
        }

        /// <summary>
        /// The thrown falx, every frame while it's out (whatever his state): out along the lane at discSpeed until it has
        /// gone discLength or meets stone (an obstacle or the arena's edge; pits don't count), then back to his hand,
        /// caught within CatchDistance. The player within discWidth / 2 of its path and not more than 2 m above it is hit
        /// once on the way out and once on the way back (it can't be jumped). The lane stays lit while it's out.
        /// </summary>
        void FalxTick(Vector3 pp, float dt, TelegraphRenderer tele)
        {
            Vector3 prev = _discPos;
            bool back = _discBack; // this frame's leg (a turn on stone this frame still counts as the way out)
            float step = def.discSpeed * dt;
            Vector3 dir = _discDir;
            _discAge += dt;
            _discSpinT += def.discSpin * dt;
            if (!back)
            {
                _discPos += _discDir * step;
                _discOut += step;
                bool stone = (_em.Obstacles != null && _em.Obstacles.Overlaps(new Vector2(_discPos.x, _discPos.z), 0.4f))
                             || _em.EdgeDistance01(_discPos) > 1f;
                if (stone)
                {
                    _discBack = true;
                    DiscsStopped++;
                    if (_em.deathPop != null) _em.deathPop.Burst(_discPos, 14, 6f, 0.14f, Color.white, new Color(1f, 0.86f, 0.4f));
                    SfxPlayer.Play(SfxId.Blocked, _discPos);
                }
                else if (_discOut >= def.discLength) _discBack = true;
            }
            else
            {
                Vector3 to = Hand - _discPos;
                float len = to.magnitude;
                if (len <= CatchDistance + step) { CatchFalx(); SfxPlayer.Play(SfxId.Swing, _pos); return; } // caught
                dir = to / len;
                _discPos += dir * step;
            }
            if (tele != null) // its lane stays lit until it's back: stay out of it
                tele.Lane(_discFrom, _discDir, _discBack ? Mathf.Max(0.5f, _discOut) : def.discLength, def.discWidth, 1f, Danger, 0.08f);
            if (back ? _discHitBack : _discHitOut) return;
            float dy = pp.y - _discPos.y;
            if (dy < 2f && dy > -1.8f && SegmentDistanceXZ(prev, _discPos, pp) < def.discWidth * 0.5f + playerRadius)
            {
                if (back) _discHitBack = true; else _discHitOut = true;
                if (HitPlayer(def.discDamage, Flat(dir), def.discKnockback)) DiscHits++;
            }
        }

        /// <summary>The falx is in his hands again (caught, or snapped back by his remains, a forced attack or his death).</summary>
        void CatchFalx()
        {
            _discFlying = _discBack = false;
            if (_falx == null) return;
            _falx.localPosition = _falxBindPos;
            _falx.localRotation = _falxBind;
        }

        /// <summary>Distance in XZ from `p` to the segment a → b.</summary>
        static float SegmentDistanceXZ(Vector3 a, Vector3 b, Vector3 p)
        {
            float abx = b.x - a.x, abz = b.z - a.z, apx = p.x - a.x, apz = p.z - a.z;
            float len2 = abx * abx + abz * abz;
            float t = len2 > 1e-8f ? Mathf.Clamp01((apx * abx + apz * abz) / len2) : 0f;
            float dx = apx - abx * t, dz = apz - abz * t;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---------- the rise and the false death (the Eternal Victor) ----------

        /// <summary>Rising from below (BeginRise): eased up to the ground, kneeling, straightening over the last 40 %; a
        /// player standing over the lift is shoved off (no damage).</summary>
        void RiseTick(float u, Vector3 pp, float dt)
        {
            _pos.y = Mathf.Lerp(_riseFrom, _riseGround, u * u * (3f - 2f * u));
            Face(pp, dt * 0.35f);
            float k = Mathf.Clamp01((u - 0.6f) / 0.4f);
            float kneel = 1f - k * k * (3f - 2f * k);
            _goal.Body = new Vector3(30f * kneel, 0f, 0f);
            _goal.Head = new Vector3(25f * kneel, 0f, 0f);
            _goal.LegL = new Vector3(-80f * kneel, 0f, 0f); // one knee up...
            _goal.LegR = new Vector3(35f * kneel, 0f, 0f); // ...the other back on the deck
            _goal.RootY = -0.7f * kneel;
            _goal.ArmL = new Vector3(-35f * kneel, 0f, 20f * kneel); // the falx across his knees
            _goal.ArmR = new Vector3(-35f * kneel, 0f, -20f * kneel);
            _shoveCd -= dt;
            Vector3 d = Flat(pp - _pos);
            if (_motor != null && _shoveCd <= 0f && d.sqrMagnitude < 2.9f * 2.9f && Mathf.Abs(pp.y - _riseGround) < 0.8f)
            {
                float m = d.magnitude;
                _motor.AddImpulse((m > 0.05f ? d / m : Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward) * 9f + Vector3.up * 4f);
                _shoveCd = 0.35f;
            }
        }

        /// <summary>He's up: on the ground, dust round the lift.</summary>
        void Risen()
        {
            _rising = false;
            _pos.y = _riseGround;
            if (_em.deathPop != null) _em.deathPop.Burst(_pos + Vector3.up * 0.3f, 26, 7f, 0.25f, new Color(0.62f, 0.52f, 0.38f), new Color(0.89f, 0.84f, 0.74f));
            SfxPlayer.Play(SfxId.Slam, _pos);
        }

        /// <summary>
        /// The first 0 HP of his last phase (falseDeath): he collapses into a heap of bronze and bone with remainsHp for
        /// remainsTime. Killed in time → Die as usual; else RiseFromRemains. The HP floor goes, so the next 0 HP is final.
        /// </summary>
        void EnterRemains()
        {
            if (_discFlying) CatchFalx();
            EndStun();
            _em.SetHpFloor(_slot, 0f); // R2-STUB(enemies)
            _em.SetHp(_slot, def.remainsHp); // R2-STUB(enemies)
            _em.SetInvulnerable(_slot, true); // a moment's grace: the blow that felled him doesn't also end his remains
            _remainsGrace = true;
            Step(Vector3.zero); // on the ground (mid-leap he drops where he is)
            _attack = BossAttack.None;
            _flurryLeft = _chargesLeft = _pouncesLeft = _discsLeft = _bombsLeft = 0;
            Enter(State.Remains, def.remainsTime);
            FalseDeaths++;
            if (!string.IsNullOrEmpty(def.remainsText))
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossPhase, Text = def.remainsText, Color = def.remainsColor, Seconds = 2.5f });
            if (_em.deathPop != null) _em.deathPop.Burst(_pos + Vector3.up * 1f, 36, 7f, 0.26f, def.remainsColor, new Color(0.62f, 0.5f, 0.3f));
            SfxPlayer.Play(SfxId.Splinter, _pos);
            ScreenShake.Add(0.4f);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(1f);
        }

        /// <summary>His remains: a heap, still and harmless; a bone ring round them fills over remainsTime and, in its last
        /// riseBurstWarn, an orange-red circle under him warns that he's rising.</summary>
        void RemainsTick(Vector3 pp, float u, TelegraphRenderer tele)
        {
            if (_remainsGrace && _t >= RemainsGrace) { _remainsGrace = false; _em.SetInvulnerable(_slot, false); }
            float left = _dur - _t;
            float warn = Mathf.Min(def.riseBurstWarn, _dur);
            if (tele != null)
            {
                tele.Circle(_pos, RemainsRing, u, def.remainsColor, TelegraphRenderer.Style.Ring, 0.08f);
                if (warn > 0f && left <= warn) tele.Circle(_pos, def.riseBurstRadius, 1f - left / warn, Danger, TelegraphRenderer.Style.Telegraph, 0.07f);
            }
            _goal.Body = new Vector3(80f, 0f, 0f); // folded forward
            _goal.Head = new Vector3(40f, 0f, 0f); // head down
            _goal.ArmL = new Vector3(-20f, 0f, 75f); // arms out
            _goal.ArmR = new Vector3(-20f, 0f, -75f);
            _goal.LegL = new Vector3(-70f, 0f, 20f);
            _goal.LegR = new Vector3(-70f, 0f, -20f);
            _goal.RootY = -1.1f;
            if (warn > 0f && left <= warn) _goal.RootScale = Vector3.one * (1f + 0.04f * Mathf.Sin(_clock * 40f)); // stirring
            if (u >= 1f) RiseFromRemains(pp);
        }

        /// <summary>"NOT YET.": up with riseHp01 of his HP, a burst that throws back anyone close, and a roar (no phase text,
        /// no arena extras), then the fight goes on in the same phase.</summary>
        void RiseFromRemains(Vector3 pp)
        {
            _remainsGrace = false;
            _em.SetHp(_slot, _em.GetMaxHp(_slot) * def.riseHp01); // R2-STUB(enemies)
            RemainsRisen++;
            Vector3 d = Flat(pp - _pos);
            float reach = def.riseBurstRadius + playerRadius;
            if (d.sqrMagnitude < reach * reach && FeetAbove(pp) < 0.6f
                && HitPlayer(def.riseBurstDamage, d.sqrMagnitude > 1e-4f ? d.normalized : _aim, def.riseBurstKnockback))
                RiseBurstHits++;
            if (_em.deathPop != null) _em.deathPop.Burst(_pos + Vector3.up * 0.6f, 32, 9f, 0.26f, def.remainsColor, Danger);
            SfxPlayer.Play(SfxId.Slam, _pos);
            if (!string.IsNullOrEmpty(def.riseText))
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossPhase, Text = def.riseText, Color = Danger, Seconds = 2.2f });
            _em.SetInvulnerable(_slot, true);
            Enter(State.Enraging, def.enrageRoar);
            _enrageTo = _phase; // the roar ends back in this phase
            _quietRoar = true; // no guards, fence or other phase extras
            SfxPlayer.Play2D(SfxId.BossRoar);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(1f);
            ScreenShake.Add(0.45f);
        }

        void Contact(Vector3 pp, float dt)
        {
            _contactCd -= dt;
            if (_contactCd > 0f || _health == null || _state == State.Entering) return;
            if (Flat(pp - _pos).magnitude < def.enemy.radius + playerRadius + 0.1f && Mathf.Abs(pp.y - _pos.y) < 2f)
            {
                Vector3 d = Flat(pp - _pos);
                HitPlayer(def.contactDamage, d.sqrMagnitude > 1e-4f ? d.normalized : _aim, 4f);
                _contactCd = def.contactInterval;
            }
        }

        // ---------- guards, fire, death ----------

        void GuardsClock(float dt)
        {
            _guardsT += dt;
            if (_guardsT >= def.guardsEvery) CallGuards();
        }

        void CallGuards()
        {
            _guardsT = 0f;
            var gates = ArenaGates.Instance;
            var pens = BeastCages.Instance;
            bool beasts = def.beastPack.y > 0 && pens != null;
            bool guards = def.guards != null && def.guards.Length > 0;
            if (!beasts && !guards) return;
            SfxPlayer.Play2D(SfxId.Horn);
            if (beasts) // the pen nearest him opens: his boars (they trample his own horde too)
            {
                int c = pens.NearestIdle(_pos);
                if (c >= 0) pens.Open(c, GameRandom.Range(def.beastPack.x, def.beastPack.y + 1));
            }
            int n = guards ? GameRandom.Range(def.guardCount.x, def.guardCount.y + 1) : 0;
            int spots = guardSpots != null ? Mathf.Min(guardSpots.Length, _spotD.Length) : 0;
            int use = Mathf.Clamp(def.guardSpotsUsed, 1, Mathf.Max(1, spots));
            if (spots > 0) // the spots nearest the player, nearest first (a little selection sort: there are few)
            {
                Vector3 pp = _player != null ? _player.position : _pos;
                for (int s = 0; s < spots; s++) _spotD[s] = Flat(guardSpots[s] - pp).sqrMagnitude;
            }
            int nearGates = spots == 0 && def.guardsNearestGates && gates != null ? NearestGates(gates) : 0;
            for (int k = 0; k < n; k++)
            {
                var g = def.guards[k % def.guards.Length];
                Vector3 at;
                if (spots > 0)
                {
                    int rank = k % use, pick = -1;
                    for (int r = 0; r <= rank; r++) // the rank-th nearest spot
                    {
                        float best = float.MaxValue;
                        pick = -1;
                        for (int s = 0; s < spots; s++)
                        {
                            float d = _spotD[s];
                            bool taken = false;
                            for (int q = 0; q < r; q++) taken |= _guardSlots[q] == s;
                            if (!taken && d < best) { best = d; pick = s; }
                        }
                        _guardSlots[r] = pick;
                    }
                    float a = k * 2.4f;
                    at = guardSpots[Mathf.Max(0, pick)] + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.6f + 0.5f * (k / use));
                    if (_em.deathPop != null) _em.deathPop.Burst(at + Vector3.up * 1.5f, 8, 3f, 0.12f, def.guardThreadColor, Color.white);
                }
                else if (gates != null && gates.Count > 0)
                {
                    int gi = nearGates > 0 ? _nearGates[k % nearGates] : k % gates.Count;
                    at = gates.SpawnPoint(gi);
                    gates.Open(gi, 3f);
                }
                else at = _pos + Quaternion.Euler(0f, k * 360f / n, 0f) * Vector3.forward * 6f;
                _em.Spawn(g, at);
            }
            GuardWaves++;
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossGuards, Text = def.guardsText, Color = Danger, Seconds = 2f });
        }

        /// <summary>Fills _nearGates with the clamp(guardSpotsUsed, 1, gates) gates nearest the player, nearest first (a
        /// selection over at most 16 gates: no allocation); returns how many.</summary>
        int NearestGates(ArenaGates gates)
        {
            int count = Mathf.Min(gates.Count, _spotD.Length);
            Vector3 pp = _player != null ? _player.position : _pos;
            int valid = 0;
            for (int s = 0; s < count; s++)
            {
                var g = gates.gates[s];
                _spotD[s] = g != null ? Flat(g.position - pp).sqrMagnitude : float.MaxValue;
                if (g != null) valid++;
            }
            int use = Mathf.Min(Mathf.Clamp(def.guardSpotsUsed, 1, Mathf.Max(1, valid)), _nearGates.Length);
            for (int r = 0; r < use; r++)
            {
                int pick = -1;
                float best = float.MaxValue;
                for (int s = 0; s < count; s++)
                {
                    bool taken = false;
                    for (int q = 0; q < r; q++) taken |= _nearGates[q] == s;
                    if (!taken && _spotD[s] < best) { best = _spotD[s]; pick = s; }
                }
                if (pick < 0) return r;
                _nearGates[r] = pick;
            }
            return use;
        }

        /// <summary>
        /// The Gilded Patron's payroll: while any of his hired guard lives he takes guardedDamageMul of the damage, and a
        /// gold thread runs along the ground from each of them to him (up to 12). The bar says GUARDED (Hud).
        /// </summary>
        void Payroll(TelegraphRenderer tele, float dt)
        {
            bool guarded = _em.AliveOf(def.guardedBy) > 0;
            if (guarded != _guarded)
            {
                _guarded = guarded;
                if (_state != State.Stunned) _em.SetDamageTakenMul(_slot, guarded ? def.guardedDamageMul : 1f);
                if (!guarded && _em.deathPop != null) _em.deathPop.Burst(_pos + Vector3.up * 2f, 20, 6f, 0.2f, def.guardThreadColor, Color.white); // the guard is down
            }
            if (guarded && _state != State.Entering) GuardedSeconds += dt;
            if (!guarded || tele == null) return;
            int n = _em.FindAlive(def.guardedBy, _guardSlots);
            for (int k = 0; k < n; k++)
            {
                Vector3 from = _em.GetPosition(_guardSlots[k]);
                Vector3 d = Flat(_pos - from);
                float len = d.magnitude;
                if (len > 0.5f) tele.Lane(from, d / len, len, 0.14f, 1f, def.guardThreadColor, 0.05f);
            }
        }

        /// <summary>The bearers' legs (parts named BLeg*), swung while he's carried about.</summary>
        void FindBearerLegs(Transform root)
        {
            int n = 0;
            if (root != null) foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("BLeg")) n++;
            _bearerLegs = new Transform[n];
            _bearerBind = new Quaternion[n];
            _bearerLeft = new bool[n];
            if (n == 0) return;
            int k = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("BLeg")) { _bearerLegs[k] = t; _bearerBind[k] = t.localRotation; _bearerLeft[k] = t.name.EndsWith("L"); k++; }
        }

        /// <summary>Phase 2: a ring of burning ground along the wall foot ("he set the fence on fire").</summary>
        void LightTheFence()
        {
            var zones = HazardZones.Instance;
            if (zones == null) return;
            // around the arena's edge (an oval in the Slave Pit): its length sets the patch count
            int n = Mathf.Max(8, Mathf.RoundToInt(_em.EdgePerimeter(def.fireRingInset) / def.fireRingSpacing));
            for (int k = 0; k < n; k++)
            {
                var p = _em.EdgePoint(k * Mathf.PI * 2f / n, def.fireRingInset);
                p.y = _em.Height != null ? _em.Height.Sample(p.x, p.z) : 0f;
                zones.AddPermanent(p, def.fireRingPatch, def.fireRingDps);
            }
            ScreenShake.Add(0.3f);
        }

        void Die()
        {
            _state = State.Dead;
            Defeated = true;
            _slot = -1;
            _rising = false;
            if (_discFlying) CatchFalx(); // his view goes back to the pool with the falx in its hand
            if (def.defeatGold > 0) // his fortune bursts out of the litter
            {
                if (RunState.Current != null) RunState.Current.AddGold(def.defeatGold);
                if (_em != null && _em.deathPop != null)
                    for (int k = 0; k < 4; k++) _em.deathPop.Burst(_pos + Vector3.up * (1f + k * 0.7f), 30, 7f + k, 0.22f, def.guardThreadColor, new Color(1f, 0.95f, 0.6f));
                SfxPlayer.Play(SfxId.Coin, _pos);
                SfxPlayer.Play(SfxId.Chest, _pos);
            }
        }

        // ---------- animation ----------

        void RoarPose()
        {
            _goal.ArmL = new Vector3(-60f, 0f, 55f);
            _goal.ArmR = new Vector3(-60f, 0f, -55f);
            _goal.Head = new Vector3(-25f, 0f, 0f);
            _goal.Body = new Vector3(-12f, 0f, 0f);
            if (_spear != null) // the spear raised to the imperial box, upright
            {
                _goal.ArmR = new Vector3(-150f, 0f, -10f);
                _spearGoal = -_goal.ArmR.x;
            }
            if (_falx != null) // the falx raised high: the salute
            {
                _goal.ArmR = new Vector3(-170f, 0f, -10f);
                _goal.ArmL = new Vector3(-30f, 0f, 35f);
            }
        }

        void Animate(float dt)
        {
            if (_rig == null || !_rig.IsValid) return;
            bool walking = _state == State.Chase || (_state == State.Entering && !_rising) || (_state == State.Strike && _attack == BossAttack.Charge);
            if (walking)
            {
                float s = Mathf.Sin(_walk);
                _goal.LegL += new Vector3(-28f * s, 0f, 0f);
                _goal.LegR += new Vector3(28f * s, 0f, 0f);
                if (_state != State.Strike)
                {
                    _goal.ArmL += new Vector3(18f * s, 0f, 0f);
                    _goal.ArmR += new Vector3(-18f * s, 0f, 0f);
                }
                _goal.RootY = Mathf.Abs(Mathf.Cos(_walk)) * 0.08f;
            }
            _pose.LerpTo(_goal, 1f - Mathf.Exp(-14f * dt));
            _rig.Apply(_pose);
            for (int k = 0; k < _bearerLegs.Length; k++) // the bearers march in step: front and back pairs, left and right legs opposite
            {
                if (_bearerLegs[k] == null) continue;
                float swing = walking ? 26f * Mathf.Sin(_walk + (_bearerLeft[k] ? 0f : Mathf.PI)) : 0f;
                _bearerLegs[k].localRotation = _bearerBind[k] * Quaternion.Euler(-swing, 0f, 0f);
            }
            if (_flail != null)
            {
                bool spinning = _attack == BossAttack.Sweep && (_state == State.Windup || _state == State.Strike);
                if (!spinning) _spin = Mathf.MoveTowardsAngle(_spin, 0f, 360f * dt);
                float tilt = spinning ? 75f : 8f * Mathf.Sin(_clock * 2f);
                _flail.localRotation = _flailBind * Quaternion.Euler(0f, _spin, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            }
            if (_spear != null)
            {
                _spearTilt = Mathf.LerpUnclamped(_spearTilt, _spearGoal, 1f - Mathf.Exp(-14f * dt));
                _spear.localRotation = _spearBind * Quaternion.Euler(_spearTilt, 0f, 0f);
            }
            if (_wingL != null || _wingR != null) // the eagle beats its wings while he roars (local Z: sign unverified in Unity)
            {
                bool roaring = _state == State.Roaring || _state == State.Enraging;
                float goal = roaring ? 22f * (0.5f + 0.5f * Mathf.Sin(_clock * 14f)) : 0f;
                _flap = Mathf.Lerp(_flap, goal, 1f - Mathf.Exp(-18f * dt));
                if (_wingL != null) _wingL.localRotation = _wingLBind * Quaternion.Euler(0f, 0f, _flap);
                if (_wingR != null) _wingR.localRotation = _wingRBind * Quaternion.Euler(0f, 0f, -_flap);
            }
            if (_falx != null && _discFlying) // in flight: world space, spinning about the vertical through its middle
            {
                var rot = Quaternion.AngleAxis(_discSpinT, Vector3.up) * _falxRelease;
                Vector3 mid = _discPos + _falxOffset * Mathf.Clamp01(1f - _discAge / 0.15f);
                _falx.SetPositionAndRotation(mid - rot * Vector3.Scale(_falx.lossyScale, _falxCenter), rot);
            }
        }
    }
}
