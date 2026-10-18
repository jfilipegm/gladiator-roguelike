using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// XP gems and gold coins as data + one instanced draw per kind (no GameObjects). Drop on kill with a small pop,
    /// bounce on the height grid, bob and spin; inside the magnet radius they're vacuumed to the player (CLAUDE.md §5).
    /// At capacity, new value merges into the nearest existing gem so nothing is lost.
    /// </summary>
    public sealed class PickupSystem : MonoBehaviour, ITickable
    {
        public enum Kind : byte { XpSmall, XpMedium, XpLarge, Gold }
        const int KindCount = 4;

        public static PickupSystem Instance { get; private set; }

        public ProgressionConfig config;
        public Mesh gemSmall, gemMedium, gemLarge, coin;
        public Material material;
        [Tooltip("Magnet radius multiplier (upgrades).")]
        public float magnetMultiplier = 1f;
        public float hoverHeight = 0.45f;

        Vector3[] _pos, _vel;
        float[] _value, _age, _phase;
        Kind[] _kind;
        bool[] _active, _vacuum;
        int[] _free;
        int _freeCount, _high, _activeCount;
        Matrix4x4[][] _matrices;
        int[] _batchCount;
        Mesh[] _meshes;
        RenderParams _rp;
        Transform _player;

        public int ActiveCount => _activeCount;

        void Awake()
        {
            Instance = this;
            int cap = config.maxPickups;
            _pos = new Vector3[cap]; _vel = new Vector3[cap];
            _value = new float[cap]; _age = new float[cap]; _phase = new float[cap];
            _kind = new Kind[cap]; _active = new bool[cap]; _vacuum = new bool[cap];
            _free = new int[cap];
            for (int i = cap - 1; i >= 0; i--) _free[_freeCount++] = i;
            _matrices = new Matrix4x4[KindCount][];
            for (int k = 0; k < KindCount; k++) _matrices[k] = new Matrix4x4[cap];
            _batchCount = new int[KindCount];
            _meshes = new[] { gemSmall, gemMedium, gemLarge, coin };
            _rp = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 200f),
            };
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Pickups);
            GameEvents.OnKill.Subscribe(OnKill);
            RunBotSensor.NearestPickup = NearestForBot;
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            if (RunBotSensor.NearestPickup == (System.Func<Vector3, float, (bool, Vector3)>)NearestForBot) RunBotSensor.NearestPickup = null;
            if (Instance == this) Instance = null;
        }

        void OnKill(in KillEvent e)
        {
            if (e.Xp > 0f) SpawnXp(e.Position, e.Xp);
            if (e.GoldChance > 0f && GameRandom.Chance(e.GoldChance)) Spawn(Kind.Gold, e.Position, config.goldPerCoin);
        }

        public void SpawnXp(Vector3 pos, float amount)
        {
            var t = config.gemTierValues;
            while (amount >= t.z) { Spawn(Kind.XpLarge, pos, t.z); amount -= t.z; }
            while (amount >= t.y) { Spawn(Kind.XpMedium, pos, t.y); amount -= t.y; }
            if (amount > 0.001f) Spawn(Kind.XpSmall, pos, amount);
        }

        public void Spawn(Kind kind, Vector3 pos, float value)
        {
            if (_freeCount == 0)
            {
                // Full: merge into the nearest active pickup of the same family.
                int best = -1; float bestD = float.MaxValue;
                bool gold = kind == Kind.Gold;
                for (int j = 0; j < _high; j++)
                {
                    if (!_active[j] || (_kind[j] == Kind.Gold) != gold) continue;
                    float d = (_pos[j] - pos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = j; }
                }
                if (best >= 0) _value[best] += value;
                return;
            }
            int i = _free[--_freeCount];
            _active[i] = true;
            _vacuum[i] = false;
            _kind[i] = kind;
            _value[i] = value;
            _age[i] = 0f;
            _phase[i] = GameRandom.Range(0f, 3f); // desyncs bob/spin only; age must start at 0 so the pop isn't skipped
            _pos[i] = pos + Vector3.up * 0.6f;
            _vel[i] = new Vector3(GameRandom.Range(-2.2f, 2.2f), GameRandom.Range(3.5f, 5.5f), GameRandom.Range(-2.2f, 2.2f));
            if (i >= _high) _high = i + 1;
            _activeCount++;
            DevCounters.Pickups = _activeCount;
        }

        /// <summary>Remove every pickup without granting anything (run reset, tests).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _high; i++)
                if (_active[i]) { _active[i] = false; _free[_freeCount++] = i; }
            _high = 0;
            _activeCount = 0;
            DevCounters.Pickups = 0;
        }

        (bool, Vector3) NearestForBot(Vector3 from, float maxDistance)
        {
            int best = -1; float bestD = maxDistance * maxDistance;
            for (int j = 0; j < _high; j++)
            {
                if (!_active[j]) continue;
                float dx = _pos[j].x - from.x, dz = _pos[j].z - from.z;
                float d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = j; }
            }
            return best >= 0 ? (true, _pos[best]) : (false, default);
        }

        public void Tick(float dt, float unscaledDt)
        {
            for (int k = 0; k < KindCount; k++) _batchCount[k] = 0;
            if (_player == null && Player.Main != null) _player = Player.Main.transform;
            var em = EnemyManager.Instance;
            var height = em != null ? em.Height : null;
            Vector3 arenaC = em != null ? em.arenaCenter : Vector3.zero;
            float arenaR = em != null ? em.arenaRadius - 0.3f : float.MaxValue;
            var run = RunState.Current;
            Vector3 target = _player != null ? _player.position + Vector3.up * 0.8f : Vector3.zero;
            float magnet = config.magnetRadius * magnetMultiplier * DevSettings.PickupRangeMul;
            float magnet2 = magnet * magnet;
            float collect2 = config.collectRadius * config.collectRadius;
            float magnetDelay = config.magnetDelay;

            for (int i = 0; i < _high; i++)
            {
                if (!_active[i]) continue;
                _age[i] += dt;
                Vector3 p = _pos[i];
                Vector3 to = target - p;
                float d2 = to.sqrMagnitude;

                if (_player != null && !_vacuum[i] && d2 < magnet2 && _age[i] > magnetDelay) { _vacuum[i] = true; _vel[i] = Vector3.zero; }

                if (_vacuum[i])
                {
                    float speed = config.vacuumStartSpeed + config.vacuumAcceleration * (_vel[i].x += dt); // _vel.x reused as vacuum timer
                    float dist = Mathf.Sqrt(d2);
                    if (dist <= speed * dt || d2 < collect2)
                    {
                        Collect(i, run);
                        continue;
                    }
                    p += to / dist * (speed * dt);
                }
                else
                {
                    // pop out, fall, settle at hover height above the ground
                    Vector3 v = _vel[i];
                    v.y -= 20f * dt;
                    p += v * dt;
                    // never pop out of the arena (the fence line is solid for everything)
                    float ax = p.x - arenaC.x, az = p.z - arenaC.z, a2 = ax * ax + az * az;
                    if (a2 > arenaR * arenaR) { float k = arenaR / Mathf.Sqrt(a2); p.x = arenaC.x + ax * k; p.z = arenaC.z + az * k; v.x = 0f; v.z = 0f; }
                    float ground = (height != null ? height.Sample(p.x, p.z) : 0f) + hoverHeight;
                    if (p.y < ground)
                    {
                        p.y = ground;
                        v.y = v.y < -2f ? -v.y * 0.3f : 0f;
                        v.x *= 0.6f; v.z *= 0.6f;
                    }
                    _vel[i] = v;
                }
                _pos[i] = p;

                int kind = (int)_kind[i];
                float t = _age[i] + _phase[i];
                float bob = _vacuum[i] ? 0f : Mathf.Sin(t * 3f) * 0.08f;
                float s = _vacuum[i] ? 0.85f : 1f;
                _matrices[kind][_batchCount[kind]++] = Matrix4x4.TRS(new Vector3(p.x, p.y + bob, p.z), Quaternion.Euler(0f, t * 120f, 0f), new Vector3(s, s, s));
            }

            for (int k = 0; k < KindCount; k++)
                if (_batchCount[k] > 0 && _meshes[k] != null)
                    Graphics.RenderMeshInstanced(_rp, _meshes[k], 0, _matrices[k], _batchCount[k]);
        }

        void Collect(int i, RunState run)
        {
            var kind = _kind[i];
            float v = _value[i];
            _active[i] = false;
            _free[_freeCount++] = i;
            _activeCount--;
            DevCounters.Pickups = _activeCount;
            while (_high > 0 && !_active[_high - 1]) _high--;
            if (run != null)
            {
                if (kind == Kind.Gold) run.AddGold(Mathf.RoundToInt(v));
                else run.AddXp(v);
            }
            GameEvents.OnPickup.Raise(new PickupEvent { Kind = (int)kind, Position = _pos[i], Amount = v });
        }
    }
}
