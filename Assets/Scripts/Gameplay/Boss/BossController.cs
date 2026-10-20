using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Every boss attack the controller knows. Appended only: BossDef assets serialize these.</summary>
    public enum BossAttack : byte { None, Sweep, Whip, Slam, Charge, Bash, Flurry }

    /// <summary>
    /// A boss's brain (docs/BOSSES.md), driven by its BossDef. The body is a Scripted enemy in EnemyManager — so every
    /// weapon, status, damage number and death pop already works on him — and this drives it: the gate entrance,
    /// chasing, the telegraphed attacks his def lists for each phase (Gorm: Flail Sweep, Whip Lash, Chain Slam, Charge
    /// in phase 2; the Champion: Shield Bash, Gladius Flurry, Shield Charge that smashes barricades and stuns him on
    /// stone), a frontal shield if his body has one, calling the guards through the gates, the phase-2 roar (Gorm sets
    /// the fence on fire, the Champion fires every spike trap), and his rigid-part animation. Telegraphs are orange-red
    /// (EnemyManager.telegraphColor). Central tick, no allocation after Begin.
    /// </summary>
    public sealed class BossController : MonoBehaviour, ITickable
    {
        public enum State : byte { Idle, Entering, Roaring, Chase, Windup, Strike, Recover, Enraging, Stunned, Dead }

        public static BossController Active { get; private set; }

        public BossDef def;
        [Tooltip("Seconds of walking in through the gate before the roar.")]
        public float entranceWalk = 2.2f;
        public float entranceRoar = 1.2f;
        public float playerRadius = 0.35f;

        EnemyManager _em;
        int _slot = -1, _uid;
        State _state;
        BossAttack _attack, _last;
        int _phase = 1;
        float _t, _dur, _clock, _walk, _spin, _contactCd, _guardsT, _chaseMin, _chargeTravel;
        bool _hitDone;
        int _flurryLeft, _chargesLeft;
        readonly float[] _weights = new float[8];
        Vector3 _pos, _aim, _target, _entryEnd;
        float _yaw;
        PartRig _rig;
        PartPose _pose = PartPose.Identity, _goal = PartPose.Identity;
        Transform _flail;
        Quaternion _flailBind;
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

        float Speed => _phase == 2 ? def.enrageSpeed : 1f;
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
            _em = EnemyManager.Instance;
            if (_em == null || def == null || def.enemy == null) return false;
            inward.y = 0f;
            inward.Normalize();
            _pos = gate + inward * 0.4f;
            _slot = _em.Spawn(def.enemy, _pos);
            if (_slot < 0) return false;
            _uid = _em.GetUid(_slot);
            _em.SetInvulnerable(_slot, true);
            _yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
            _entryEnd = gate + inward * 7.5f;
            _rig = _em.GetRig(_slot);
            var root = _em.GetViewRoot(_slot);
            _flail = Find(root, "Flail");
            if (_flail != null) _flailBind = _flail.localRotation;
            _phase = 1;
            Defeated = false;
            Active = this;
            Enter(State.Entering, entranceWalk);
            return true;
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

        /// <summary>Starts `attack` now (tests, admin). Ignored while entering, enraging or dead.</summary>
        public void ForceAttack(BossAttack attack)
        {
            if (_state == State.Entering || _state == State.Roaring || _state == State.Enraging || _state == State.Dead) return;
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

            if (_phase == 1 && _state != State.Entering && _state != State.Roaring && _state != State.Enraging && Hp01 <= def.phase2At)
            {
                _em.SetInvulnerable(_slot, true);
                EndStun();
                Enter(State.Enraging, def.enrageRoar);
                SfxPlayer.Play2D(SfxId.BossRoar);
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossPhase, Text = def.phase2Text, Color = Danger, Seconds = 2.2f });
                if (def.phase2SpikeVolley && SpikeTraps.Instance != null) SpikeTraps.Instance.FireAll(); // on his roar
                if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(1f);
                ScreenShake.Add(0.45f);
            }

            switch (_state)
            {
                case State.Entering:
                    Walk(_entryEnd, def.moveSpeed * 0.8f, dt);
                    if (u >= 1f || Flat(_entryEnd - _pos).sqrMagnitude < 0.3f)
                    {
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
                        _phase = 2;
                        if (def.phase2FireRing) LightTheFence();
                        _em.SetInvulnerable(_slot, false);
                        Enter(State.Chase, 0.5f);
                        _chaseMin = 0.5f;
                    }
                    break;
                case State.Chase:
                {
                    float d = Flat(pp - _pos).magnitude;
                    if (d > def.enemy.radius + 1.2f) Walk(pp, _phase == 2 ? def.enragedMoveSpeed : def.moveSpeed, dt);
                    else Face(pp, dt);
                    if (_t >= _chaseMin) StartAttack(Choose(d));
                    break;
                }
                case State.Windup:
                    Face(_attack == BossAttack.Slam ? pp : _pos + _aim, dt * (_attack == BossAttack.Slam ? 1f : 0.2f));
                    WindupTick(pp, u, tele);
                    if (u >= 1f) { Enter(State.Strike, StrikeTime()); _hitDone = false; _chargeTravel = 0f; StrikeBegin(pp); }
                    break;
                case State.Strike:
                    StrikeTick(pp, u, dt);
                    if (u >= 1f && _attack != BossAttack.Charge)
                    {
                        if (_attack == BossAttack.Flurry && _flurryLeft > 0) { _flurryLeft--; StartAttack(BossAttack.Flurry, true); } // re-aims
                        else { AttacksDone++; _last = _attack; Enter(State.Recover, def.recover / Speed); }
                    }
                    break;
                case State.Recover:
                    if (u >= 1f) { _attack = BossAttack.None; Enter(State.Chase, 0f); _chaseMin = 0.45f / Speed; }
                    break;
                case State.Stunned:
                    _goal.Body = new Vector3(28f, 0f, 0f);
                    _goal.Head = new Vector3(25f, 10f * Mathf.Sin(_clock * 9f), 0f);
                    _goal.ArmL = new Vector3(25f, 0f, 30f); // guard down
                    if (u >= 1f) { EndStun(); AttacksDone++; _last = _attack; _attack = BossAttack.None; Enter(State.Recover, def.recover / Speed); }
                    break;
            }

            // the guard clock runs through the whole fight (not just between attacks: a boss that attacks back to back
            // would never call them), but not during his entrance or phase-2 roar
            if (_state != State.Entering && _state != State.Roaring && _state != State.Enraging) GuardsClock(dt);
            ShieldUp = def.enemy.blockArc > 0f && _state != State.Stunned && _state != State.Entering;
            _em.SetScriptedShield(_slot, ShieldUp);
            Contact(pp, dt);
            _em.SetScripted(_slot, _pos, _yaw, _state == State.Windup);
            Animate(dt);
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
            _ => 0f,
        };

        /// <summary>A weighted pick from this phase's attacks (the last one is less likely). If none is in range: the
        /// charge (any range), else the slam, else the first listed.</summary>
        BossAttack Choose(float d)
        {
            var list = _phase == 2 ? def.phase2Attacks : def.phase1Attacks;
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
            bool charge = false, slam = false;
            foreach (var a in list) { charge |= a == BossAttack.Charge; slam |= a == BossAttack.Slam; }
            return charge ? BossAttack.Charge : slam ? BossAttack.Slam : list[0];
        }

        void StartAttack(BossAttack a, bool chained = false)
        {
            if (!chained) // a fresh flurry / charge sets how many follow; chained ones re-aim and count down
            {
                _flurryLeft = a == BossAttack.Flurry ? Mathf.Max(1, _phase == 2 ? def.flurryThrusts.y : def.flurryThrusts.x) - 1 : 0;
                _chargesLeft = a == BossAttack.Charge ? Mathf.Max(1, _phase == 2 ? def.chargeRepeats.y : def.chargeRepeats.x) - 1 : 0;
            }
            _attack = a;
            Vector3 pp = _player != null ? _player.position : _pos + Vector3.forward;
            _aim = Flat(pp - _pos);
            if (_aim.sqrMagnitude < 1e-4f) _aim = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            _aim.Normalize();
            _target = pp;
            float w = a switch
            {
                BossAttack.Sweep => def.sweepWindup,
                BossAttack.Whip => def.whipWindup,
                BossAttack.Slam => def.slamWindup,
                BossAttack.Bash => def.bashWindup,
                BossAttack.Flurry => def.flurryWindup,
                _ => def.chargeWindup,
            };
            Enter(State.Windup, w / Speed);
        }

        float StrikeTime() => _attack switch
        {
            BossAttack.Sweep => 0.3f,
            BossAttack.Whip => 0.18f,
            BossAttack.Slam => 0.25f,
            BossAttack.Bash => 0.25f,
            BossAttack.Flurry => 0.12f,
            _ => def.chargeLength / Mathf.Max(1f, def.chargeSpeed) + 0.2f,
        };

        float ChargeWidth => def.chargeWidth > 0f ? def.chargeWidth : def.enemy.radius * 2f;

        void WindupTick(Vector3 pp, float u, TelegraphRenderer tele)
        {
            var col = Danger;
            switch (_attack)
            {
                case BossAttack.Sweep:
                    if (tele != null) tele.Circle(_pos, def.sweepRadius, u, col, TelegraphRenderer.Style.Telegraph, 0.06f);
                    _spin += Time.deltaTime * (300f + 900f * u);
                    _goal.ArmR = new Vector3(-70f, 0f, -55f);
                    _goal.Body = new Vector3(0f, -25f * u, 0f);
                    break;
                case BossAttack.Whip:
                    if (tele != null) tele.Lane(_pos, _aim, def.whipLength, def.whipWidth, u, col);
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
                    _goal.ArmR = new Vector3(35f * u, 0f, 0f); // sword drawn back
                    _goal.Body = new Vector3(0f, -18f * u, 0f);
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
                        HitPlayer(def.whipDamage, _aim, 3f);
                        if (_motor != null) _motor.Slow(def.whipSlow, def.whipSlowSeconds);
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
                    if (_phase == 2 && HazardZones.Instance != null)
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
                    _goal.ArmR = new Vector3(-80f, 0f, -80f);
                    _goal.Body = new Vector3(0f, 40f, 0f);
                    break;
                case BossAttack.Whip:
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
                    _goal.ArmR = new Vector3(-90f, 0f, 0f); // thrust
                    _goal.Body = new Vector3(8f, 15f, 0f);
                    break;
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

        void HitPlayer(float damage, Vector3 dir, float knockback)
        {
            if (_health == null) return;
            dir.y = 0f;
            if (_health.TakeDamage(new DamageInfo { Amount = damage, Flags = DamageFlags.Melee, Point = _player.position, Direction = dir })
                && _motor != null && knockback > 0f)
                _motor.AddImpulse(dir * knockback + Vector3.up * knockback * 0.45f);
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
            if (def.guards == null || def.guards.Length == 0) return;
            SfxPlayer.Play2D(SfxId.Horn);
            int n = GameRandom.Range(def.guardCount.x, def.guardCount.y + 1);
            for (int k = 0; k < n; k++)
            {
                var g = def.guards[k % def.guards.Length];
                Vector3 at;
                if (gates != null && gates.Count > 0)
                {
                    int gi = k % gates.Count;
                    at = gates.SpawnPoint(gi);
                    gates.Open(gi, 3f);
                }
                else at = _pos + Quaternion.Euler(0f, k * 360f / n, 0f) * Vector3.forward * 6f;
                _em.Spawn(g, at);
            }
            GuardWaves++;
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.BossGuards, Text = def.guardsText, Color = Danger, Seconds = 2f });
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
        }

        // ---------- animation ----------

        void RoarPose()
        {
            _goal.ArmL = new Vector3(-60f, 0f, 55f);
            _goal.ArmR = new Vector3(-60f, 0f, -55f);
            _goal.Head = new Vector3(-25f, 0f, 0f);
            _goal.Body = new Vector3(-12f, 0f, 0f);
        }

        void Animate(float dt)
        {
            if (_rig == null || !_rig.IsValid) return;
            bool walking = _state == State.Chase || _state == State.Entering || (_state == State.Strike && _attack == BossAttack.Charge);
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
            if (_flail != null)
            {
                bool spinning = _attack == BossAttack.Sweep && (_state == State.Windup || _state == State.Strike);
                if (!spinning) _spin = Mathf.MoveTowardsAngle(_spin, 0f, 360f * dt);
                float tilt = spinning ? 75f : 8f * Mathf.Sin(_clock * 2f);
                _flail.localRotation = _flailBind * Quaternion.Euler(0f, _spin, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            }
        }
    }
}
