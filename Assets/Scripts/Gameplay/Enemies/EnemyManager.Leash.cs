using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Escorts on leashes (Phase 5.15, the Beast Handler): an EnemyDef with an `escort` spawns `escortCount` of them
    /// around itself, and while it lives they can't stray more than `leashLength` from it (a rope drawn on the sand
    /// shows the leash). When it dies its escorts are freed: they vanish in a white burst instead of fighting on (no
    /// kill, no XP). Struct-of-arrays like the rest of the manager; nothing allocates.
    /// </summary>
    public sealed partial class EnemyManager
    {
        [Header("Leashes (Phase 5.15)")]
        [Tooltip("The rope drawn from a handler to each beast on his leash (a neutral colour: it isn't a hazard).")]
        public Color leashColor = new Color(0.55f, 0.42f, 0.28f, 0.8f);

        int[] _leaderSlot, _leaderUid;
        bool _spawningEscorts;

        /// <summary>Beasts freed by killing their handler (bot log, tests).</summary>
        public int EscortsFreed { get; private set; }

        void AllocLeash()
        {
            _leaderSlot = new int[capacity];
            _leaderUid = new int[capacity];
            for (int i = 0; i < capacity; i++) _leaderSlot[i] = -1;
        }

        void ResetLeash(int i) => _leaderSlot[i] = -1;

        /// <summary>The slot of i's living leader, or -1.</summary>
        public int LeaderOf(int i)
        {
            int l = _leaderSlot[i];
            return l >= 0 && _active[l] && _uid[l] == _leaderUid[i] ? l : -1;
        }

        /// <summary>Spawns def.escortCount escorts around leader `i` on its leash (once: an escort's own escort is ignored).</summary>
        void SpawnEscorts(int i, EnemyDef def)
        {
            if (_spawningEscorts || def.escort == null || def.escortCount <= 0) return;
            _spawningEscorts = true;
            for (int k = 0; k < def.escortCount; k++)
            {
                float a = _yaw[i] + 180f + (k - (def.escortCount - 1) * 0.5f) * 50f; // behind him, fanned
                Vector3 at = _pos[i] + Quaternion.Euler(0f, a, 0f) * Vector3.forward * Mathf.Min(1.4f, def.leashLength * 0.6f);
                int e = Spawn(def.escort, at);
                if (e < 0) break;
                _leaderSlot[e] = i;
                _leaderUid[e] = _uid[i];
            }
            _spawningEscorts = false;
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

        /// <summary>The leader `i` died: every escort still on its leash is freed (a quiet despawn, no kill).</summary>
        void FreeEscorts(int i)
        {
            int uid = _uid[i];
            for (int j = 0; j < _high; j++)
            {
                if (!_active[j] || _leaderSlot[j] != i || _leaderUid[j] != uid) continue;
                _leaderSlot[j] = -1;
                _active[j] = false;
                _dying[j] = feedback != null ? feedback.deathPopTime : 0.1f;
                AliveCount--;
                var def = _view[j].Def;
                _aliveByDef[def] = AliveOf(def) - 1;
                if (deathPop != null) deathPop.Burst(_pos[j] + Vector3.up * 0.5f, 10, 4f, 0.12f, Color.white, new Color(0.72f, 0.95f, 1f));
                EscortsFreed++;
            }
            DevCounters.Enemies = AliveCount;
        }
    }
}
