using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Chimera's breath (R2, docs/ARENAS.md §6), <see cref="EnemyBehaviour.Breather"/>: it walks at you; with you
    /// within attackRange and in front of it (it turns slowly: flank it) it plants and an orange-red cone (attackRadius
    /// long, breathAngle wide) fills over its wind-up, aimed where you stood; then it breathes for breathTime: you once
    /// if you're in the cone and not jumping over it, every other enemy in it once × trampleMul (beasts don't care whom
    /// they burn; the War Boar's trample serials), and at its end breathPatches fire patches along the cone. Then it
    /// stands smoking for `recover`: the window.
    /// </summary>
    public sealed partial class EnemyManager
    {
        [Header("Breather (R2)")]
        [Tooltip("It starts a breath only with the player within the cone's half-width + this of its facing (deg): it turns slowly, so beside or behind it is safe.")]
        public float breathFacingSlack = 10f;
        [Tooltip("The player's feet higher than this above the beast's feet clear the breath (m): a jump over it.")]
        public float breathJumpClear = 1.2f;
        [Tooltip("The breath's push on the player: upward part (m/s); along the cone it's EnemyDef.breathKnockback.")]
        public float breathKnockUp = 2.5f;
        [Tooltip("Fire bursts along the cone this often while it breathes (s).")]
        public float breathFxEvery = 0.15f;
        public Color breathColorA = new Color(1f, 0.42f, 0.1f), breathColorB = new Color(1f, 0.82f, 0.35f);

        readonly int[] _breathQuery = new int[512];

        /// <summary>Breaths started this run (tests, bot log).</summary>
        public int Breaths { get; private set; }
        /// <summary>Breaths that hit the player.</summary>
        public int BreathHits { get; private set; }
        /// <summary>Enemies burned by a breath (each at most once per breath).</summary>
        public int BreathEnemyHits { get; private set; }

        /// <summary>
        /// True if a circle of radius `pad` at `p` touches the cone (XZ) from `origin` along `aim`, `length` long and
        /// `angleDeg` wide in all. Heights are the caller's.
        /// </summary>
        public static bool InCone(Vector3 origin, Vector3 aim, float length, float angleDeg, Vector3 p, float pad)
        {
            float dx = p.x - origin.x, dz = p.z - origin.z;
            float d2 = dx * dx + dz * dz, reach = length + pad;
            if (d2 > reach * reach) return false;
            if (d2 <= pad * pad) return true; // touching the mouth
            float am = Mathf.Sqrt(aim.x * aim.x + aim.z * aim.z);
            if (am < 1e-5f) return false;
            float d = Mathf.Sqrt(d2);
            float cos = (dx * aim.x + dz * aim.z) / (d * am);
            // the circle reaches into the cone by the angle it subtends at that distance
            float half = angleDeg * 0.5f * Mathf.Deg2Rad + (pad > 0f ? Mathf.Asin(Mathf.Min(1f, pad / d)) : 0f);
            return half >= Mathf.PI || cos >= Mathf.Cos(half);
        }

        Vector3 ThinkBreather(int i, EnemyDef def, Vector3 p, Vector3 player, Vector3 dirTo, float dist, float dt, TelegraphRenderer tele, out bool facePlayer)
        {
            facePlayer = false;
            float atkMul = _atkTimeMul[i];
            float len = def.attackRadius * _scale[i];
            switch (_state[i])
            {
                case AiState.Windup:
                    if (tele != null) tele.Sector(p, _aim[i], len, def.breathAngle, 1f - _stateT[i] / _stateDur[i], telegraphColor);
                    AddWindupThreat(p);
                    if (_stateT[i] <= 0f)
                    {
                        _attackHit[i] = false;
                        _charge[i] = ++_chargeSerial; // the trample serials: each enemy burns once per breath
                        Breaths++;
                        SetState(i, AiState.Attack, def.breathTime);
                        SfxPlayer.Play(SfxId.FirePot, p);
                        BreathFx(i, p, len);
                    }
                    return Vector3.zero;

                case AiState.Attack:
                {
                    if (tele != null) tele.Sector(p, _aim[i], len, def.breathAngle, 1f, telegraphColor);
                    AddWindupThreat(p);
                    float el = _stateDur[i] - _stateT[i];
                    if (breathFxEvery > 0f && (int)(el / breathFxEvery) != (int)((el - dt) / breathFxEvery)) BreathFx(i, p, len);
                    Breathe(i, def, p, player, len);
                    if (_stateT[i] <= 0f)
                    {
                        LeavePatches(def, p, _aim[i], len);
                        SetState(i, AiState.Recover, def.recover);
                        _attackCd[i] = def.attackCooldown * atkMul;
                    }
                    return Vector3.zero;
                }

                case AiState.Recover:
                    if (_stateT[i] <= 0f) _state[i] = AiState.Move;
                    return Vector3.zero; // smoking: the window

                default:
                {
                    float dy = player.y - p.y;
                    if (_attackCd[i] <= 0f && dist < def.attackRange && Mathf.Abs(dy) < 1f && Facing(i, dirTo, def.breathAngle * 0.5f + breathFacingSlack))
                    {
                        _aim[i] = dirTo; // locked: step out of the cone, or past its flank
                        _vel[i] = Vector3.zero; // it plants
                        SetState(i, AiState.Windup, def.windup * atkMul);
                        return Vector3.zero;
                    }
                    return dirTo * _speed[i];
                }
            }
        }

        /// <summary>True if `dir` (XZ, unit) is within `halfDeg` of enemy i's facing.</summary>
        bool Facing(int i, Vector3 dir, float halfDeg)
        {
            float yaw = _yaw[i] * Mathf.Deg2Rad;
            return Mathf.Sin(yaw) * dir.x + Mathf.Cos(yaw) * dir.z >= Mathf.Cos(Mathf.Min(180f, halfDeg) * Mathf.Deg2Rad);
        }

        /// <summary>One frame of fire: the player once per breath, every other enemy in the cone once per breath.</summary>
        void Breathe(int i, EnemyDef def, Vector3 p, Vector3 player, float len)
        {
            Vector3 aim = _aim[i];
            float dy = player.y - p.y;
            if (!_attackHit[i] && _playerHealth != null && dy < breathJumpClear && dy > -breathJumpClear
                && InCone(p, aim, len, def.breathAngle, player, playerRadius))
            {
                if (_playerHealth.TakeDamage(new DamageInfo { Amount = def.attackDamage, Flags = DamageFlags.Area, Point = player, Direction = aim }))
                {
                    _attackHit[i] = true;
                    BreathHits++;
                    if (_playerMotor != null) _playerMotor.AddImpulse(aim * def.breathKnockback + Vector3.up * breathKnockUp);
                }
            }
            if (def.trampleMul <= 0f) return;
            int n = QueryAlive(p, len, _breathQuery);
            for (int k = 0; k < n; k++)
            {
                int j = _breathQuery[k];
                if (j == i || !_active[j] || _trampled[j] == _charge[i]) continue;
                var dj = _view[j].Def;
                if (dj == def || dj.behaviour == EnemyBehaviour.Scripted) continue; // not its own kind, not a boss
                Vector3 q = _pos[j];
                if (Mathf.Abs(q.y - p.y) > breathJumpClear + 0.3f || !InCone(p, aim, len, def.breathAngle, q, _radius[j])) continue;
                _trampled[j] = _charge[i];
                BreathEnemyHits++;
                ApplyDamage(j, new DamageInfo
                {
                    Amount = def.attackDamage * def.trampleMul, Flags = DamageFlags.Area | DamageFlags.Proc, Point = q,
                    Direction = aim, Knockback = def.breathKnockback, SourceId = BreathSourceId,
                });
            }
        }

        /// <summary>Fire bursts at a quarter, half and three quarters of the cone.</summary>
        void BreathFx(int i, Vector3 p, float len)
        {
            if (deathPop == null) return;
            Vector3 aim = _aim[i];
            for (int k = 1; k <= 3; k++)
                deathPop.Burst(p + aim * (len * k * 0.25f) + Vector3.up * (0.5f + 0.15f * k), 4, 2.5f + k, 0.22f + 0.04f * k, breathColorA, breathColorB);
        }

        /// <summary>The breath's end: `breathPatches` fire patches at (k + 1) / (n + 1) of the cone's length.</summary>
        void LeavePatches(EnemyDef def, Vector3 p, Vector3 aim, float len)
        {
            var zones = HazardZones.Instance;
            int n = def.breathPatches;
            if (zones == null || n <= 0 || def.zoneDuration <= 0f) return;
            for (int k = 0; k < n; k++)
            {
                Vector3 c = p + aim * (len * (k + 1) / (n + 1));
                ClampInside(ref c, def.breathPatchRadius * 0.5f);
                c.y = _height != null ? _height.Sample(c.x, c.z) : p.y;
                zones.Add(c, def.breathPatchRadius, def.zoneDuration, def.zoneDps);
            }
        }

        /// <summary>The breather's body language (the Chimera; front legs = ArmL/ArmR): the wind-up draws the lion's head
        /// back and braces low, the breath thrusts it forward, the recovery hangs it.</summary>
        static void BreatherPose(ref PartPose pose, AiState st, float k, float shake)
        {
            if (st == AiState.Windup)
            {
                pose.Head = PartPose.Lean(-25f * k + shake * 3f);
                pose.Body = PartPose.Lean(-6f * k);
                pose.ArmL = pose.ArmR = PartPose.Swing(-14f * k); // front legs braced
                pose.LegL = pose.LegR = PartPose.Swing(10f * k);
                pose.RootY = -0.1f * k;
                pose.RootScale = new Vector3(1f + 0.06f * k, 1f - 0.08f * k, 1f);
            }
            else if (st == AiState.Attack)
            {
                pose.Head = PartPose.Lean(20f);
                pose.Body = PartPose.Lean(6f);
                pose.ArmL = pose.ArmR = PartPose.Swing(-10f);
                pose.RootY = -0.06f;
                pose.RootScale = new Vector3(1f, 0.97f, 1.05f);
            }
            else // recover: k runs 1 → 0
            {
                pose.Head = PartPose.Lean(12f * k);
                pose.Body = PartPose.Lean(4f * k);
            }
        }
    }
}
