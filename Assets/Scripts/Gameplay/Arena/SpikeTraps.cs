using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Provincial Pit's spike grates (docs/ARENAS.md): each one cycles on its own clock (staggered so one is
    /// always close), telegraphs in orange-red, then shoots its spikes up for a while. They hurt everyone: the player
    /// (feet above `jumpClear` clear them, like Gorm's sweep) and every enemy on the grate (a boss takes a fraction).
    /// Luring the horde over a grate is a tactic. Central tick, spikes drawn instanced, no allocation.
    /// </summary>
    public sealed class SpikeTraps : MonoBehaviour, ITickable
    {
        public static SpikeTraps Instance { get; private set; }

        [Tooltip("Grate centres (y = the floor).")]
        public Vector3[] traps = System.Array.Empty<Vector3>();
        [Tooltip("Half the grate's side (m): who stands on it is hit.")]
        public float halfSize = 0.9f;
        [Tooltip("Half the side of the grate's painted border (m, PR_SpikeGrate): the telegraph covers exactly this square.")]
        public float borderHalf = 0.98f;

        [Header("Cycle (s)")]
        public float period = 7f;
        public float telegraph = 1f;
        public float upTime = 1.2f;
        [Tooltip("Seconds into the run before the first trap's cycle starts.")]
        public float firstAfter = 6f;

        [Header("Damage")]
        public float playerDamage = 15f;
        public float enemyDamage = 40f;
        [Tooltip("Ordinary (non-elite) enemies take this multiple: the horde dies on them.")]
        public float ordinaryEnemyMul = 2f;
        [Tooltip("Bosses take this fraction.")]
        public float bossMul = 0.25f;
        [Tooltip("Feet this high above the grate clear the spikes (m).")]
        public float jumpClear = 0.5f;

        [Header("Look")]
        public Mesh spikesMesh;
        public Material material;
        public Color telegraphColor = new Color(1f, 0.32f, 0.12f, 0.95f);
        [Tooltip("How far below the grate the spikes hide (m).")]
        public float hiddenDepth = 0.85f;

        float[] _t = System.Array.Empty<float>();    // seconds into each trap's cycle; < 0 = waiting to start
        bool[] _playerHit = System.Array.Empty<bool>();
        Matrix4x4[] _matrices = System.Array.Empty<Matrix4x4>();
        readonly int[] _query = new int[128];
        RenderParams _rp;
        bool _rpReady;
        Transform _player;
        PlayerHealth _health;
        PlayerMotor _motor;

        /// <summary>Times any trap's spikes came up (tests, bot log).</summary>
        public int Fired { get; private set; }
        public int EnemiesHit { get; private set; }
        public int PlayerHits { get; private set; }
        public bool IsUp(int i) => i >= 0 && i < _t.Length && _t[i] >= telegraph && _t[i] < telegraph + upTime;
        public bool IsTelegraphing(int i) => i >= 0 && i < _t.Length && _t[i] >= 0f && _t[i] < telegraph;

        void Awake()
        {
            Instance = this;
            int n = traps.Length;
            _t = new float[n];
            _playerHit = new bool[n];
            _matrices = new Matrix4x4[n];
            for (int i = 0; i < n; i++) _t[i] = -(firstAfter + period * i / Mathf.Max(1, n)); // staggered
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Enemies, 30);
        void OnDisable() => GameLoop.Unregister(this);
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Every trap telegraphs now (the Champion's phase-2 roar).</summary>
        public void FireAll()
        {
            for (int i = 0; i < _t.Length; i++) if (!IsUp(i)) { _t[i] = 0f; _playerHit[i] = false; }
        }

        /// <summary>Trap `i` telegraphs now (tests).</summary>
        public void Fire(int i)
        {
            if (i < 0 || i >= _t.Length) return;
            _t[i] = 0f;
            _playerHit[i] = false;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f || traps.Length == 0) return;
            if (_player == null && Player.Main != null)
            {
                _player = Player.Main.transform;
                _health = Player.Main.GetComponent<PlayerHealth>();
                _motor = Player.Main.GetComponent<PlayerMotor>();
            }
            var tele = TelegraphRenderer.Instance;
            var em = EnemyManager.Instance;
            int drawn = 0;
            for (int i = 0; i < traps.Length; i++)
            {
                float before = _t[i];
                _t[i] += dt;
                if (_t[i] >= period) { _t[i] -= period; _playerHit[i] = false; }
                float t = _t[i];
                Vector3 c = traps[i];
                if (t >= 0f && t < telegraph)
                {
                    // the grate's own square, filling across it: the spikes come up exactly where it's lit
                    if (tele != null) tele.Lane(c - Vector3.forward * borderHalf, Vector3.forward, borderHalf * 2f, borderHalf * 2f, t / telegraph, telegraphColor);
                }
                else if (t >= telegraph && t < telegraph + upTime)
                {
                    if (before < telegraph) Rise(i, c, em); // the frame they shoot up
                    HitPlayer(i, c);
                }
                // spikes: shoot up in 0.06 s, hold, sink over the last 0.25 s
                float h = -hiddenDepth;
                if (t >= telegraph && t < telegraph + upTime)
                {
                    float u = t - telegraph;
                    h = u < 0.06f ? Mathf.Lerp(-hiddenDepth, 0f, u / 0.06f)
                        : u > upTime - 0.25f ? Mathf.Lerp(0f, -hiddenDepth, (u - (upTime - 0.25f)) / 0.25f) : 0f;
                }
                if (h > -hiddenDepth + 0.01f) _matrices[drawn++] = Matrix4x4.Translate(new Vector3(c.x, c.y + h, c.z));
            }
            Draw(drawn);
        }

        void Rise(int i, Vector3 c, EnemyManager em)
        {
            Fired++;
            SfxPlayer.Play(SfxId.Spikes, c);
            if (em == null) return;
            int n = em.QueryAlive(c, halfSize + 1.2f, _query);
            for (int k = 0; k < n; k++)
            {
                int e = _query[k];
                Vector3 p = em.GetPosition(e);
                float r = em.GetRadius(e);
                if (Mathf.Abs(p.x - c.x) > halfSize + r * 0.5f || Mathf.Abs(p.z - c.z) > halfSize + r * 0.5f) continue;
                var def = em.GetDef(e);
                bool boss = def != null && def.behaviour == EnemyBehaviour.Scripted;
                float dmg = enemyDamage * (boss ? bossMul : em.IsElite(e) ? 1f : ordinaryEnemyMul);
                em.ApplyDamage(e, new DamageInfo { Amount = dmg, Flags = DamageFlags.Area, Point = p, Direction = Vector3.up, Knockback = 3f });
                EnemiesHit++;
            }
        }

        void HitPlayer(int i, Vector3 c)
        {
            if (_playerHit[i] || _player == null || _health == null) return;
            Vector3 pp = _player.position;
            if (Mathf.Abs(pp.x - c.x) > halfSize + 0.3f || Mathf.Abs(pp.z - c.z) > halfSize + 0.3f) return;
            if (pp.y - c.y > jumpClear || pp.y - c.y < -1f) return; // jumped over it
            _playerHit[i] = true;
            if (_health.TakeDamage(new DamageInfo { Amount = playerDamage, Flags = DamageFlags.Area, Point = pp, Direction = Vector3.up }))
            {
                PlayerHits++;
                if (_motor != null) _motor.AddImpulse(Vector3.up * 5f);
            }
        }

        void Draw(int drawn)
        {
            if (spikesMesh == null || material == null || drawn == 0) return;
            if (!_rpReady)
            {
                _rp = new RenderParams(material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows = true,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };
                _rpReady = true;
            }
            Graphics.RenderMeshInstanced(_rp, spikesMesh, 0, _matrices, drawn);
        }
    }
}
