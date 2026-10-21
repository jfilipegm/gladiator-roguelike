using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Escorts on leashes (Phase 5.15, the Beast Handler): an EnemyDef with an `escort` spawns `escortCount` of them
    /// around itself, and while it lives they can't stray more than `leashLength` from it (a rope drawn on the sand
    /// shows the leash). When it dies its escorts are freed: they vanish in a white burst instead of fighting on (no
    /// kill, no XP).
    ///
    /// Formations (Phase 5.16, the Centurion, docs/ARENAS.md §5): with `formationSlots` the escorts hold slots in the
    /// leader's frame instead (a line of shields ahead of him) and face his front, not the player. While his line stands
    /// he stops `formationHold` short of the player, and a Lunger leader's lunge becomes the line's push: he braces with
    /// his facing locked (a lane fills in front of the shields), then the whole line shoves forward, and any shield that
    /// touches the player hits once per push. With `freeEscortsOnDeath` off, his death breaks the line: every escort
    /// still with him gets `breakStatus` and its shield down for `breakShieldsDown` s, then fights on alone. The line
    /// is a bit mask per leader, kept at spawn, an escort's death and the break (no per-frame scans).
    /// Struct-of-arrays like the rest of the manager; nothing allocates.
    /// </summary>
    public sealed partial class EnemyManager
    {
        [Header("Leashes (Phase 5.15)")]
        [Tooltip("The rope drawn from a handler to each beast on his leash (a neutral colour: it isn't a hazard).")]
        public Color leashColor = new Color(0.55f, 0.42f, 0.28f, 0.8f);

        [Header("Formations (Phase 5.16)")]
        [Tooltip("A formed escort closes on its slot with this time constant (s), on top of following the slot's own motion.")]
        public float slotTime = 0.35f;
        [Tooltip("…and catches up at most this × its move speed (relative to the slot).")]
        public float slotCatchUp = 1.6f;
        [Tooltip("The slot's own motion is followed up to this × its move speed (the end of a wheeling line runs; a teleport doesn't fling it).")]
        public float slotFollowMax = 3f;

        int[] _leaderSlot, _leaderUid;
        bool _spawningEscorts;
        int[] _slotIndex;     // an escort's slot in its leader's line (-1: on a leash or free)
        int[] _formedMask;    // a leader's line: bit k = the escort in slot k still stands
        float[] _shieldsDownT;
        Vector3[] _slotPrev;  // where an escort's slot was last tick (its motion is followed exactly)

        /// <summary>Beasts freed by killing their handler (bot log, tests).</summary>
        public int EscortsFreed { get; private set; }
        /// <summary>Leaders spawned with a line, and lines broken by killing the leader (not by KillAll at the run's end)
        /// (bot log, tests).</summary>
        public int FormationsSpawned { get; private set; }
        public int LinesBroken { get; private set; }

        void AllocLeash()
        {
            _leaderSlot = new int[capacity];
            _leaderUid = new int[capacity];
            _slotIndex = new int[capacity];
            _formedMask = new int[capacity];
            _shieldsDownT = new float[capacity];
            _slotPrev = new Vector3[capacity];
            for (int i = 0; i < capacity; i++) _leaderSlot[i] = _slotIndex[i] = -1;
        }

        void ResetLeash(int i)
        {
            _leaderSlot[i] = _slotIndex[i] = -1;
            _formedMask[i] = 0;
            _shieldsDownT[i] = 0f;
        }

        /// <summary>The slot of i's living leader, or -1.</summary>
        public int LeaderOf(int i)
        {
            int l = _leaderSlot[i];
            return l >= 0 && _active[l] && _uid[l] == _leaderUid[i] ? l : -1;
        }

        /// <summary>A slot (x right, y forward, m) in the frame of a leader at `leader` facing `leaderYaw` (deg), in world
        /// space at the leader's height.</summary>
        public static Vector3 SlotWorld(Vector3 leader, float leaderYaw, Vector2 slot)
        {
            float r = leaderYaw * Mathf.Deg2Rad, s = Mathf.Sin(r), c = Mathf.Cos(r);
            // forward = (sin, 0, cos), right = (cos, 0, -sin)
            return new Vector3(leader.x + slot.x * c + slot.y * s, leader.y, leader.z - slot.x * s + slot.y * c);
        }

        /// <summary>True while i holds a slot in a living leader's line.</summary>
        public bool InFormation(int i) => i >= 0 && i < capacity && _slotIndex[i] >= 0 && LeaderOf(i) >= 0;

        /// <summary>Escorts still standing in leader `i`'s line (0: none, or no line).</summary>
        public int FormedCount(int i)
        {
            int n = 0;
            for (int m = i >= 0 && i < capacity ? _formedMask[i] : 0; m != 0; m &= m - 1) n++;
            return n;
        }

        /// <summary>Escort i's slot index in its leader's line, or -1 (tests).</summary>
        public int FormationSlotOf(int i) => i >= 0 && i < capacity ? _slotIndex[i] : -1;

        /// <summary>Where escort i's slot is now (its own position if it's not in a line) (tests, debug).</summary>
        public Vector3 SlotPosition(int i)
        {
            if (!InFormation(i)) return _pos[i];
            int l = LeaderOf(i);
            return SlotWorld(_pos[l], _yaw[l], _view[l].Def.formationSlots[_slotIndex[i]]);
        }

        /// <summary>Its shield is down (a broken line's escort): no block.</summary>
        public bool ShieldsDown(int i) => _shieldsDownT[i] > 0f;

        /// <summary>Spawns def.escortCount escorts around leader `i`, on its leash or in its line (once: an escort's own
        /// escort is ignored).</summary>
        void SpawnEscorts(int i, EnemyDef def)
        {
            if (_spawningEscorts || def.escort == null || def.escortCount <= 0) return;
            _spawningEscorts = true;
            var slots = def.formationSlots;
            bool line = slots != null && slots.Length > 0;
            int count = line ? Mathf.Min(def.escortCount, Mathf.Min(slots.Length, 32)) : def.escortCount;
            for (int k = 0; k < count; k++)
            {
                Vector3 at;
                if (line)
                {
                    at = SlotWorld(_pos[i], _yaw[i], slots[k]); // he already faces the player
                    ClampInside(ref at, def.escort.radius + 0.1f);
                }
                else
                {
                    float a = _yaw[i] + 180f + (k - (def.escortCount - 1) * 0.5f) * 50f; // behind him, fanned
                    at = _pos[i] + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Mathf.Min(1.4f, def.leashLength * 0.6f);
                }
                int e = Spawn(def.escort, at);
                if (e < 0) break;
                _leaderSlot[e] = i;
                _leaderUid[e] = _uid[i];
                if (!line) continue;
                _slotIndex[e] = k;
                _slotPrev[e] = SlotWorld(_pos[i], _yaw[i], slots[k]);
                _yaw[e] = _yaw[i];
                _formedMask[i] |= 1 << k;
            }
            if (_formedMask[i] != 0) FormationsSpawned++;
            _spawningEscorts = false;
        }

        /// <summary>
        /// A formed escort's desired velocity: its slot's own motion (the march, the wheel) plus closing on the slot
        /// (`slotTime`, at most `slotCatchUp` × its speed); during the leader's push, the push itself, rigidly. `faceYaw`
        /// is the line's facing.
        /// </summary>
        Vector3 FormationSteer(int i, EnemyDef def, Vector3 p, float dt, out float faceYaw)
        {
            int l = LeaderOf(i);
            var ldef = _view[l].Def;
            Vector3 slot = SlotWorld(_pos[l], _yaw[l], ldef.formationSlots[_slotIndex[i]]);
            Vector3 follow = (slot - _slotPrev[i]) / dt;
            _slotPrev[i] = slot;
            faceYaw = _yaw[l];
            if (_state[l] == AiState.Attack && ldef.behaviour == EnemyBehaviour.Lunger) return _aim[l] * ldef.lungeSpeed;
            follow.y = 0f;
            float max = slotFollowMax * def.moveSpeed;
            if (follow.sqrMagnitude > max * max) follow *= max / follow.magnitude;
            Vector3 close = new Vector3(slot.x - p.x, 0f, slot.z - p.z) / Mathf.Max(0.05f, slotTime);
            float cap = slotCatchUp * def.moveSpeed;
            if (close.sqrMagnitude > cap * cap) close *= cap / close.magnitude;
            return follow + close;
        }

        /// <summary>A leader with a line: slows over the last metre and stops `formationHold` short of the player while it
        /// walks, and turns the line to face the player (at its own turn rate: the line wheels slowly) except while it
        /// braces and pushes.</summary>
        Vector3 LeaderHold(int i, EnemyDef def, Vector3 desired, float dist, ref bool facePlayer)
        {
            if (_state[i] == AiState.Move || _state[i] == AiState.Recover) facePlayer = true;
            return _state[i] == AiState.Move && def.formationHold > 0f ? desired * Mathf.Clamp01(dist - def.formationHold) : desired;
        }

        /// <summary>The lane a bracing leader's line is about to push through (from the middle of the shields' front,
        /// along the locked facing): false unless leader `i` is winding up with a line. The telegraph, the bot, tests.</summary>
        public bool PushLane(int i, out Vector3 from, out Vector3 dir, out float width, out float depth)
        {
            from = dir = Vector3.zero;
            width = depth = 0f;
            if (!IsAlive(i) || _formedMask[i] == 0 || _state[i] != AiState.Windup) return false;
            var def = _view[i].Def;
            var slots = def.formationSlots;
            int mask = _formedMask[i];
            float minX = float.MaxValue, maxX = float.MinValue, front = float.MinValue;
            for (int k = 0; k < slots.Length && k < 32; k++)
            {
                if ((mask & (1 << k)) == 0) continue;
                if (slots[k].x < minX) minX = slots[k].x;
                if (slots[k].x > maxX) maxX = slots[k].x;
                if (slots[k].y > front) front = slots[k].y;
            }
            if (front == float.MinValue) return false;
            float er = def.escort != null ? def.escort.radius : 0.5f;
            dir = _aim[i];
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            from = _pos[i] + right * ((minX + maxX) * 0.5f) + dir * (front + er);
            width = maxX - minX + 2f * er + def.pushPad;
            depth = def.pushDepth;
            return true;
        }

        /// <summary>The push's telegraph: its lane fills in front of the shields over the wind-up.</summary>
        void DrawPush(int i, float fill, TelegraphRenderer tele)
        {
            if (!PushLane(i, out var from, out var dir, out float width, out float depth)) return;
            if (tele != null) tele.Lane(from, dir, depth, width, fill, telegraphColor);
            AddWindupThreat(from + dir * (depth * 0.5f));
        }

        /// <summary>
        /// The push, from escort i's side: while its leader shoves, a shield touching the player (feet below 1.1 m) hits
        /// once per push (the leader's attack flag) and throws the player along the line's facing. True if this shield
        /// reached the player (the caller skips its contact damage then).
        /// </summary>
        bool PushHit(int i, EnemyDef def, float dist, Vector3 player)
        {
            int l = LeaderOf(i);
            if (l < 0 || _state[l] != AiState.Attack || _view[l].Def.behaviour != EnemyBehaviour.Lunger) return false;
            float dy = player.y - _pos[i].y;
            if (dist > _radius[i] + playerRadius + 0.25f || dy >= 1.1f || dy <= -1.1f) return false;
            if (_attackHit[l]) return true;
            var ldef = _view[l].Def;
            if (_playerHealth.TakeDamage(new DamageInfo { Amount = ldef.attackDamage, Flags = DamageFlags.Melee, Point = player, Direction = _aim[l] }))
            {
                _attackHit[l] = true;
                _contactCd[i] = def.contactInterval;
                if (_playerMotor != null) _playerMotor.AddImpulse(_aim[l] * ldef.pushKnockback + Vector3.up * 3f);
                SfxPlayer.Play(SfxId.Slam, player, 0.5f);
            }
            return true;
        }

        /// <summary>Escort i died in its line: it leaves a gap (no closing ranks).</summary>
        void LeaveLine(int i)
        {
            int l = LeaderOf(i);
            if (l >= 0) _formedMask[l] &= ~(1 << _slotIndex[i]);
            _slotIndex[i] = -1;
        }

        /// <summary>Keeps a leashed escort within its leader's leash, and draws the rope. Drops the leash if the leader is gone.</summary>
        void LeashClamp(int i, ref Vector3 np)
        {
            int l = LeaderOf(i);
            if (l < 0) { _leaderSlot[i] = -1; return; }
            float len = _view[l].Def.leashLength;
            Vector3 lp = _pos[l];
            float dx = np.x - lp.x, dz = np.z - lp.z, d2 = dx * dx + dz * dz;
            if (d2 > len * len)
            {
                float k = len / Mathf.Sqrt(d2);
                np.x = lp.x + dx * k;
                np.z = lp.z + dz * k;
            }
            var tele = TelegraphRenderer.Instance;
            if (tele == null) return;
            Vector3 from = lp + Vector3.up * 0.02f;
            Vector3 d = new Vector3(np.x - lp.x, 0f, np.z - lp.z);
            float m = d.magnitude;
            if (m > 0.3f) tele.Lane(from, d / m, m, 0.06f, 1f, leashColor, 0.04f);
        }

        /// <summary>The leader `i` died: every escort still on its leash is freed (a quiet despawn, no kill), or, with
        /// freeEscortsOnDeath off, let go to fight on: its line breaks.</summary>
        void FreeEscorts(int i)
        {
            var leader = _view[i].Def;
            if (!leader.freeEscortsOnDeath) { BreakLine(i, leader); return; }
            int uid = _uid[i];
            for (int j = 0; j < _high; j++)
            {
                if (!_active[j] || _leaderSlot[j] != i || _leaderUid[j] != uid) continue;
                _leaderSlot[j] = -1;
                _slotIndex[j] = -1;
                _active[j] = false;
                _dying[j] = feedback != null ? feedback.deathPopTime : 0.1f;
                AliveCount--;
                var def = _view[j].Def;
                _aliveByDef[def] = AliveOf(def) - 1;
                if (deathPop != null) deathPop.Burst(_pos[j] + Vector3.up * 0.5f, 10, 4f, 0.12f, Color.white, new Color(0.72f, 0.95f, 1f));
                EscortsFreed++;
            }
            _formedMask[i] = 0;
            DevCounters.Enemies = AliveCount;
        }

        /// <summary>The leader `i` died and its escorts fight on: each one still with it gets `breakStatus` (the
        /// Stagger) and its shield down for `breakShieldsDown` s, then it's an ordinary enemy of its kind.</summary>
        void BreakLine(int i, EnemyDef leader)
        {
            int uid = _uid[i];
            bool line = false;
            for (int j = 0; j < _high; j++)
            {
                if (!_active[j] || _leaderSlot[j] != i || _leaderUid[j] != uid) continue;
                line |= _slotIndex[j] >= 0;
                _leaderSlot[j] = -1;
                _slotIndex[j] = -1;
                if (leader.breakStatus != null) ApplyStatus(j, leader.breakStatus, 0f);
                if (leader.breakShieldsDown > 0f) _shieldsDownT[j] = leader.breakShieldsDown;
            }
            _formedMask[i] = 0;
            if (line && !_killingAll) LinesBroken++;
        }
    }
}
