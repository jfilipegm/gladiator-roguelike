using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Grand Arena's fire jets (docs/ARENAS.md §4): a row of nozzles across each arm. The rows fire in turn (staggered
    /// over the period): each telegraphs its strip in orange-red, then roars a wall of fire too tall to jump. It hurts
    /// everyone: the player once per burst, every enemy in the strip once per burst (ordinary ones double, a boss a
    /// quarter) and sets them Burning, so the horde walking down an arm burns. The Gilded Patron's phase 2 fires them
    /// all at once. Central tick, flames drawn instanced, no allocation.
    /// </summary>
    public sealed class FireJets : MonoBehaviour, ITickable
    {
        public static FireJets Instance { get; private set; }

        [Tooltip("Rows (x, z, yaw, width): the strip runs `width` along the row's local X and `depth` along its local Z.")]
        public Vector4[] rows = System.Array.Empty<Vector4>();
        [Tooltip("The strip's depth across the row (m).")]
        public float depth = 2.6f;
        [Tooltip("Metres between two nozzles (one flame each).")]
        public float nozzleSpacing = 2f;

        [Header("Cycle (s)")]
        public float period = 10f;
        public float telegraph = 1.2f;
        public float burn = 1.6f;
        [Tooltip("Seconds into the run before the first row's cycle starts.")]
        public float firstAfter = 20f;

        [Header("Damage")]
        public float playerDamage = 20f;
        public float enemyDamage = 45f;
        [Tooltip("Ordinary (non-elite) enemies take this multiple.")]
        public float ordinaryEnemyMul = 2f;
        [Tooltip("Bosses take this fraction.")]
        public float bossMul = 0.25f;
        [Tooltip("The flames' height (m): nothing jumps over them.")]
        public float height = 3.5f;
        [Tooltip("Enemies caught are set Burning (dps per stack, before nothing: it's the arena's fire).")]
        public StatusDef burnStatus;
        public float burnDps = 8f;

        [Header("Look")]
        public Mesh flameMesh;
        public Material material;
        public Color telegraphColor = new Color(1f, 0.32f, 0.12f, 0.95f);

        float[] _t = System.Array.Empty<float>();   // seconds into each row's cycle; < 0 = waiting
        bool[] _playerHit = System.Array.Empty<bool>();
        int[] _serial = System.Array.Empty<int>();  // each row's burst number
        Vector3[] _center = System.Array.Empty<Vector3>();
        Vector3[] _along = System.Array.Empty<Vector3>(), _across = System.Array.Empty<Vector3>();
        int[] _hitSerial = System.Array.Empty<int>(), _hitUid = System.Array.Empty<int>();  // per enemy slot: last burst that hit it
        Matrix4x4[] _matrices = System.Array.Empty<Matrix4x4>();
        readonly int[] _query = new int[512];
        int _serialSeq;
        RenderParams _rp;
        bool _rpReady, _placed;
        Transform _player;
        PlayerHealth _health;
        PlayerMotor _motor;

        public int Count => rows.Length;
        /// <summary>Bursts fired, enemies burnt, hits on the player (tests, bot log).</summary>
        public int Fired { get; private set; }
        public int EnemiesHit { get; private set; }
        public int PlayerHits { get; private set; }
        public bool IsBurning(int i) => i >= 0 && i < _t.Length && _t[i] >= telegraph && _t[i] < telegraph + burn;
        public bool IsTelegraphing(int i) => i >= 0 && i < _t.Length && _t[i] >= 0f && _t[i] < telegraph;
        public Vector3 Center(int i) => _center[i];
        /// <summary>The row's direction (along its nozzles) and across it (the way you cross it).</summary>
        public Vector3 Along(int i) => _along[i];
        public Vector3 Across(int i) => _across[i];

        void Awake()
        {
            Instance = this;
            int n = rows.Length;
            _t = new float[n];
            _playerHit = new bool[n];
            _serial = new int[n];
            _center = new Vector3[n];
            _along = new Vector3[n];
            _across = new Vector3[n];
            int flames = 0;
            for (int i = 0; i < n; i++)
            {
                _t[i] = -(firstAfter + period * i / Mathf.Max(1, n)); // in turn, going round
                float yaw = rows[i].z * Mathf.Deg2Rad;
                _along[i] = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                _across[i] = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                _center[i] = new Vector3(rows[i].x, 0f, rows[i].y);
                flames += Nozzles(i);
            }
            _matrices = new Matrix4x4[Mathf.Max(1, flames)];
        }

        int Nozzles(int i) => Mathf.Max(1, Mathf.RoundToInt(rows[i].w / Mathf.Max(0.5f, nozzleSpacing)));

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies, 31);
        void OnDisable() => GameLoop.Unregister(this);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Every row telegraphs now (the Gilded Patron's phase-2 roar).</summary>
        public void FireAll()
        {
            for (int i = 0; i < _t.Length; i++) if (!IsBurning(i)) Fire(i);
        }

        /// <summary>Row `i` telegraphs now (tests, the Patron).</summary>
        public void Fire(int i)
        {
            if (i < 0 || i >= _t.Length) return;
            _t[i] = 0f;
            _playerHit[i] = false;
        }

        /// <summary>The row nearest `p` (-1 if none).</summary>
        public int Nearest(Vector3 p)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < _center.Length; i++)
            {
                float d = (new Vector3(p.x, 0f, p.z) - _center[i]).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>True if `p` (XZ) is inside row `i`'s strip, grown by `pad` metres.</summary>
        public bool InStrip(int i, Vector3 p, float pad)
        {
            Vector3 d = p - _center[i];
            float a = d.x * _along[i].x + d.z * _along[i].z, c = d.x * _across[i].x + d.z * _across[i].z;
            return Mathf.Abs(a) <= rows[i].w * 0.5f + pad && Mathf.Abs(c) <= depth * 0.5f + pad;
        }

        void Place()
        {
            _placed = true;
            var em = EnemyManager.Instance;
            if (em == null || em.Height == null) return;
            for (int i = 0; i < _center.Length; i++) _center[i].y = em.Height.Sample(_center[i].x, _center[i].z);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f || rows.Length == 0) return;
            if (!_placed) Place();
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            var tele = TelegraphRenderer.Instance;
            var em = EnemyManager.Instance;
            int drawn = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                float before = _t[i];
                _t[i] += dt;
                if (_t[i] >= period) { _t[i] -= period; _playerHit[i] = false; }
                float t = _t[i];
                Vector3 c = _center[i];
                float w = rows[i].w;
                if (t >= 0f && t < telegraph)
                {
                    // the strip lights from one wall to the other: the fire comes exactly where it's lit
                    if (tele != null) tele.Lane(c - _along[i] * (w * 0.5f), _along[i], w, depth, t / telegraph, telegraphColor, 0.18f);
                }
                else if (t >= telegraph && t < telegraph + burn)
                {
                    if (before < telegraph) { Fired++; _serial[i] = ++_serialSeq; SfxPlayer.Play(SfxId.FirePot, c); ScreenShake.Add(0.06f); }
                    if (em != null) BurnEnemies(i, em);
                    HitPlayer(i);
                    // flames: shoot up in 0.1 s, flicker, sink over the last 0.25 s
                    float u = t - telegraph;
                    float h = u < 0.1f ? u / 0.1f : u > burn - 0.25f ? Mathf.Clamp01((burn - u) / 0.25f) : 1f;
                    int n = Nozzles(i);
                    float step = w / n;
                    for (int k = 0; k < n && drawn < _matrices.Length; k++)
                    {
                        Vector3 at = c + _along[i] * (-w * 0.5f + step * (k + 0.5f));
                        float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 31f + k * 1.7f + i);
                        _matrices[drawn++] = Matrix4x4.TRS(at, Quaternion.Euler(0f, rows[i].z + k * 37f, 0f), new Vector3(1f, h * flicker, 1f));
                    }
                }
            }
            Draw(drawn);
        }

        void BurnEnemies(int i, EnemyManager em)
        {
            if (_hitSerial.Length != em.capacity) { _hitSerial = new int[em.capacity]; _hitUid = new int[em.capacity]; }
            float reach = Mathf.Sqrt(rows[i].w * rows[i].w * 0.25f + depth * depth * 0.25f) + em.MaxRadius;
            int n = em.QueryAlive(_center[i], reach, _query);
            for (int k = 0; k < n; k++)
            {
                int e = _query[k];
                int uid = em.GetUid(e);
                if (_hitSerial[e] == _serial[i] && _hitUid[e] == uid) continue; // once per burst
                Vector3 p = em.GetPosition(e);
                if (!InStrip(i, p, em.GetRadius(e) * 0.5f) || p.y - _center[i].y > height) continue;
                _hitSerial[e] = _serial[i];
                _hitUid[e] = uid;
                var def = em.GetDef(e);
                bool boss = def != null && def.behaviour == EnemyBehaviour.Scripted;
                float dmg = enemyDamage * (boss ? bossMul : em.IsElite(e) ? 1f : ordinaryEnemyMul);
                if (burnStatus != null && !boss) em.ApplyStatus(e, burnStatus, burnDps);
                em.ApplyDamage(e, new DamageInfo { Amount = dmg, Flags = DamageFlags.Area, Point = p, Direction = Vector3.up, Knockback = 2f });
                EnemiesHit++;
            }
        }

        void HitPlayer(int i)
        {
            if (_playerHit[i] || _player == null || _health == null) return;
            Vector3 pp = _player.position;
            if (!InStrip(i, pp, 0.3f) || pp.y - _center[i].y > height || pp.y - _center[i].y < -1f) return;
            _playerHit[i] = true;
            Vector3 d = pp - _center[i];
            Vector3 away = (d.x * _across[i].x + d.z * _across[i].z) >= 0f ? _across[i] : -_across[i];
            if (_health.TakeDamage(new DamageInfo { Amount = playerDamage, Flags = DamageFlags.Area, Point = pp, Direction = away }))
            {
                PlayerHits++;
                if (_motor != null) _motor.AddImpulse(away * 6f + Vector3.up * 3f); // thrown out of the fire
            }
        }

        void Draw(int drawn)
        {
            if (flameMesh == null || material == null || drawn == 0) return;
            if (!_rpReady)
            {
                _rp = new RenderParams(material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };
                _rpReady = true;
            }
            Graphics.RenderMeshInstanced(_rp, flameMesh, 0, _matrices, drawn);
        }
    }
}
