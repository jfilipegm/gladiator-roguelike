using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Ground hazards that burn the player while they stand in them (Fire-Pot patches now; braziers, curse zones and
    /// boss fire later). Only a GROUNDED player inside takes damage, in fixed ticks — jumping over a patch is safe, so
    /// movement skill pays off (CLAUDE.md §5). Drawn as fire decals through TelegraphRenderer. Fixed capacity, no GC.
    /// </summary>
    public sealed class HazardZones : MonoBehaviour, ITickable
    {
        public static HazardZones Instance { get; private set; }

        public int capacity = 64;
        [Tooltip("Seconds between damage ticks while the player stands inside.")]
        public float tickInterval = 0.5f;
        [Tooltip("Extra reach beyond the drawn radius (m) — the player's capsule radius.")]
        public float playerRadius = 0.3f;
        [Tooltip("Max height of the player's feet above the patch that still counts as standing in it (m).")]
        public float maxFeetHeight = 0.6f;
        public Color fireColor = new Color(1f, 0.42f, 0.12f, 0.95f);

        Vector3[] _pos;
        float[] _radius, _life, _maxLife, _dps, _tick, _seed;
        bool[] _used, _permanent;
        int _high, _count;
        Transform _player;
        PlayerHealth _health;
        PlayerMotor _motor;

        public int Count => _count;
        /// <summary>True while the player is grounded inside any zone (bot / tests).</summary>
        public bool PlayerInside { get; private set; }

        void Awake()
        {
            Instance = this;
            _pos = new Vector3[capacity];
            _radius = new float[capacity]; _life = new float[capacity]; _maxLife = new float[capacity];
            _dps = new float[capacity]; _tick = new float[capacity]; _seed = new float[capacity];
            _used = new bool[capacity];
            _permanent = new bool[capacity];
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Projectiles, 50);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>Adds a burning patch on the ground at `center` (y = ground height). Returns the slot or -1.</summary>
        /// <summary>A zone that never burns out (arena fire pits). Cleared only by Clear().</summary>
        public int AddPermanent(Vector3 center, float radius, float dps)
        {
            int i = Add(center, radius, 1f, dps);
            if (i >= 0) _permanent[i] = true;
            return i;
        }

        public int Add(Vector3 center, float radius, float duration, float dps)
        {
            for (int i = 0; i < capacity; i++)
            {
                if (_used[i]) continue;
                _used[i] = true;
                _pos[i] = center;
                _radius[i] = radius;
                _life[i] = _maxLife[i] = duration;
                _dps[i] = dps;
                _tick[i] = 0f;
                _permanent[i] = false;
                _seed[i] = GameRandom.Value;
                if (i >= _high) _high = i + 1;
                _count++;
                return i;
            }
            return -1;
        }

        public void Clear()
        {
            for (int i = 0; i < _high; i++) _used[i] = false;
            _high = _count = 0;
        }

        public void Tick(float dt, float unscaledDt)
        {
            PlayerInside = false;
            if (_high == 0 || dt <= 0f) return;
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            var tele = TelegraphRenderer.Instance;
            Vector3 pp = _player != null ? _player.position : new Vector3(0f, -1000f, 0f);
            bool grounded = _motor == null || _motor.IsGrounded;

            for (int i = 0; i < _high; i++)
            {
                if (!_used[i]) continue;
                if (!_permanent[i]) _life[i] -= dt;
                if (_life[i] <= 0f)
                {
                    _used[i] = false;
                    _count--;
                    continue;
                }
                float life01 = _life[i] / _maxLife[i];
                // grow in over the first 0.15 s, shrink over the last 20 %
                float age = _permanent[i] ? 1f : _maxLife[i] - _life[i]; // permanent zones are fully grown
                float r = _radius[i] * Mathf.Min(1f, age / 0.15f) * Mathf.Min(1f, life01 / 0.2f + 0.3f);
                if (tele != null) tele.Circle(_pos[i], r, life01, fireColor, TelegraphRenderer.Style.Fire, 0.07f, _seed[i]);

                _tick[i] -= dt;
                float dx = pp.x - _pos[i].x, dz = pp.z - _pos[i].z;
                float reach = r + playerRadius;
                bool inside = grounded && dx * dx + dz * dz < reach * reach && pp.y - _pos[i].y < maxFeetHeight && pp.y - _pos[i].y > -1f;
                if (!inside) continue;
                PlayerInside = true;
                if (_tick[i] <= 0f && _health != null)
                {
                    _tick[i] = tickInterval;
                    _health.TakeDamage(new DamageInfo { Amount = _dps[i] * tickInterval, Flags = DamageFlags.Area | DamageFlags.Status, Point = pp });
                }
            }
            while (_high > 0 && !_used[_high - 1]) _high--;
        }
    }
}
