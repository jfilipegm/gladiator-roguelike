using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    public enum BossAttack : byte { None, Sweep, Whip, Slam, Charge }

    /// <summary>
    /// Gorm's brain (docs/BOSSES.md). The body is a Scripted enemy in EnemyManager — so every weapon, status, damage
    /// number and death pop already works on him — and this drives it: the gate entrance, chasing, four telegraphed
    /// attacks (Flail Sweep, Whip Lash, Chain Slam, Charge in phase 2), calling the guards through the gates, the
    /// phase-2 roar that sets the fence on fire, and his rigid-part animation. Telegraphs are orange-red
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

        const string GuardsText = "GORM CALLS HIS GUARDS!";
        const string BurnText = "\"BURN IT!\"";

        EnemyManager _em;
        int _slot = -1, _uid;
        State _state;
        BossAttack _attack, _last;
        int _phase = 1;
        float _t, _dur, _clock, _walk, _spin, _contactCd, _guardsT, _chaseMin, _chargeTravel;
        bool _hitDone;
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
                Enter(State.Enraging, def.enrageRoar);
                SfxPlayer.Play2D(SfxId.BossRoar);
                GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = BurnText, Color = Danger, Seconds = 2.2f });
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
                        LightTheFence();
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
                    GuardsClock(dt);
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
                    if (u >= 1f && _attack != BossAttack.Charge) { AttacksDone++; _last = _attack; Enter(State.Recover, def.recover / Speed); }
                    break;
                case State.Recover:
                    GuardsClock(dt);
                    if (u >= 1f) { _attack = BossAttack.None; Enter(State.Chase, 0f); _chaseMin = 0.45f / Speed; }
                    break;
                case State.Stunned:
                    _goal.Body = new Vector3(28f, 0f, 0f);
                    _goal.Head = new Vector3(25f, 10f * Mathf.Sin(_clock * 9f), 0f);
                    if (u >= 1f) { AttacksDone++; _last = _attack; _attack = BossAttack.None; Enter(State.Recover, def.recover / Speed); }
                    break;
            }

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
            Vector3 c = _em.arenaCenter;
            Vector3 fromC = Flat(np - c);
            float max = _em.arenaRadius - r;
            if (fromC.magnitude > max) { np = c + fromC.normalized * max; blocked = true; }
            np.y = _em.Height != null ? _em.Height.Sample(np.x, np.z) : _pos.y;
            _pos = np;
            return blocked && Flat(np - want).sqrMagnitude > 0.0004f;
        }

        // ---------- attacks ----------

        BossAttack Choose(float d)
        {
            float wSweep = d < def.sweepRadius * 0.85f ? 3f : 0f;
            float wWhip = d > 3f && d < def.whipLength * 0.9f ? 3f : 0f;
            float wSlam = 2f;
            float wCharge = _phase == 2 && d > 5f && d < def.chargeLength ? 2.5f : 0f;
            if (_last == BossAttack.Sweep) wSweep *= 0.3f;
            if (_last == BossAttack.Whip) wWhip *= 0.3f;
            if (_last == BossAttack.Slam) wSlam *= 0.3f;
            if (_last == BossAttack.Charge) wCharge *= 0.3f;
            float r = GameRandom.Value * (wSweep + wWhip + wSlam + wCharge);
            if ((r -= wSweep) < 0f) return BossAttack.Sweep;
            if ((r -= wWhip) < 0f) return BossAttack.Whip;
            if ((r -= wSlam) < 0f) return BossAttack.Slam;
            return wCharge > 0f ? BossAttack.Charge : BossAttack.Slam;
        }

        void StartAttack(BossAttack a)
        {
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
                _ => def.chargeWindup,
            };
            Enter(State.Windup, w / Speed);
        }

        float StrikeTime() => _attack switch
        {
            BossAttack.Sweep => 0.3f,
            BossAttack.Whip => 0.18f,
            BossAttack.Slam => 0.25f,
            _ => def.chargeLength / Mathf.Max(1f, def.chargeSpeed) + 0.2f,
        };

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
                    Lane(_pos, _aim, def.whipLength, def.whipWidth, u, tele, col);
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
                    Lane(_pos, _aim, def.chargeLength, def.enemy.radius * 2f, u, tele, col);
                    _goal.Body = new Vector3(28f, 0f, 0f);
                    _goal.LegL = new Vector3(-30f, 0f, 0f);
                    _goal.LegR = new Vector3(20f, 0f, 0f);
                    break;
            }
        }

        static void Lane(Vector3 from, Vector3 dir, float length, float width, float fill, TelegraphRenderer tele, Color col)
        {
            if (tele == null) return;
            float r = width * 0.5f;
            int n = Mathf.Max(2, Mathf.CeilToInt(length / (r * 1.6f)));
            for (int k = 0; k < n; k++)
            {
                float along = (k + 0.5f) / n * length;
                float f = Mathf.Clamp01(fill * n - k); // fills from the boss outward
                tele.Circle(from + dir * along, r, f, col, TelegraphRenderer.Style.Telegraph, 0.08f, k * 0.17f);
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
                case BossAttack.Charge:
                {
                    float stepLen = def.chargeSpeed * dt;
                    bool wall = Step(_aim * stepLen);
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
                        Enter(State.Stunned, def.chargeStun);
                    }
                    else if (_chargeTravel >= def.chargeLength || u >= 1f)
                    {
                        AttacksDone++;
                        _last = _attack;
                        Enter(State.Recover, def.recover / Speed);
                    }
                    break;
                }
            }
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
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = GuardsText, Color = Danger, Seconds = 2f });
        }

        /// <summary>Phase 2: a ring of burning ground along the wall foot ("he set the fence on fire").</summary>
        void LightTheFence()
        {
            var zones = HazardZones.Instance;
            if (zones == null) return;
            Vector3 c = _em.arenaCenter;
            int n = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * def.fireRingRadius / def.fireRingSpacing));
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                var p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * def.fireRingRadius;
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
