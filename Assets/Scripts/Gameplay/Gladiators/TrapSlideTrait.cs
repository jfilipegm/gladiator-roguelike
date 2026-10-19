using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Pit Goblin's ability (docs/GLADIATORS.md): every slide drops a snare behind it (at most `maxSnares` on the
    /// ground, each for `lifetime` s). The first enemy to step in is rooted (the Root status) and bleeds. Scripted
    /// enemies (bosses) ignore the root but still bleed.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Trap Slide", fileName = "TR_TrapSlide")]
    public sealed class TrapSlideTrait : GladiatorTrait
    {
        [Min(1)] public int maxSnares = 3;
        [Tooltip("Seconds a snare waits on the ground.")]
        public float lifetime = 10f;
        [Tooltip("Seconds before a fresh snare can close (so it lands behind you, not on what you slid through).")]
        public float armDelay = 0.25f;
        [Tooltip("Snare reach (m), plus the enemy's radius.")]
        public float triggerRadius = 0.6f;
        [Tooltip("Min seconds between two snares.")]
        public float cooldown = 0.5f;
        [Tooltip("Root status (duration = how long the enemy is held).")]
        public StatusDef root;
        public StatusDef bleed;
        [Tooltip("Bleed damage per second per stack (× the player's Damage).")]
        public float bleedPower = 3f;
        [Min(1)] public int bleedStacks = 3;
        [Tooltip("Damage when the jaws close (× the player's Damage).")]
        public float snapDamage = 8f;

        [Header("Look")]
        public Mesh mesh;
        public Material material;
        public float meshScale = 1f;
        [Tooltip("Ring under each snare: player colours, never orange-red.")]
        public Color ring = new Color(0.72f, 0.95f, 1f, 0.7f);

        public override void Install(GameObject player)
        {
            var s = player.GetComponent<SnareLayer>();
            if (s == null) s = player.AddComponent<SnareLayer>();
            s.trait = this;
        }
    }

    /// <summary>Runtime half of TrapSlideTrait: a tiny pool of snares, one central tick, instanced draw.</summary>
    public sealed class SnareLayer : MonoBehaviour, ITickable
    {
        public TrapSlideTrait trait;

        const int Capacity = 8;
        readonly Vector3[] _pos = new Vector3[Capacity];
        readonly float[] _t = new float[Capacity];
        readonly float[] _yaw = new float[Capacity];
        readonly Matrix4x4[] _matrices = new Matrix4x4[Capacity];
        readonly int[] _query = new int[64];
        int _count, _drawn;
        float _cooldown;
        PlayerStats _stats;
        RenderParams _rp;
        bool _rpReady;
        EventHandler<SlideEvent> _onSlide;

        public int Count => _count;
        public int Placed { get; private set; }
        public int Snapped { get; private set; }
        public Vector3 PositionOf(int k) => _pos[k];

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _onSlide = OnSlide;
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Weapons, 5);
            GameEvents.OnSlide.Subscribe(_onSlide);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnSlide.Unsubscribe(_onSlide);
        }

        void OnSlide(in SlideEvent e)
        {
            if (trait == null || _cooldown > 0f) return;
            Place(e.Position);
        }

        /// <summary>Drops a snare at `at` (ground height under it). The oldest goes when the ground is full.</summary>
        public void Place(Vector3 at)
        {
            if (trait == null) return;
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) at.y = em.Height.Sample(at.x, at.z);
            if (_count >= Mathf.Min(trait.maxSnares, Capacity)) RemoveAt(0);
            _pos[_count] = at;
            _t[_count] = 0f;
            _yaw[_count] = GameRandom.Range(0f, 360f);
            _count++;
            Placed++;
            _cooldown = trait.cooldown;
        }

        void RemoveAt(int k)
        {
            for (int j = k; j < _count - 1; j++) { _pos[j] = _pos[j + 1]; _t[j] = _t[j + 1]; _yaw[j] = _yaw[j + 1]; }
            _count--;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null) return;
            if (dt <= 0f) { Draw(); return; } // paused: keep them on screen
            _cooldown -= dt;
            var em = EnemyManager.Instance;
            var tele = TelegraphRenderer.Instance;
            _drawn = 0;
            for (int k = 0; k < _count; k++)
            {
                _t[k] += dt;
                if (_t[k] >= trait.lifetime) { RemoveAt(k); k--; continue; }
                if (_t[k] >= trait.armDelay && em != null && TrySnap(k, em)) { RemoveAt(k); k--; continue; }
                float left = trait.lifetime - _t[k];
                bool blinkOff = left < 2f && Mathf.Repeat(left * 4f, 1f) < 0.35f; // about to go: blink
                if (!blinkOff)
                {
                    _matrices[_drawn++] = Matrix4x4.TRS(_pos[k], Quaternion.Euler(0f, _yaw[k], 0f), Vector3.one * trait.meshScale);
                    if (tele != null) tele.Circle(_pos[k], trait.triggerRadius + 0.2f, 1f, trait.ring, TelegraphRenderer.Style.Ring, 0.05f, k * 0.31f);
                }
            }
            Draw();
        }

        bool TrySnap(int k, EnemyManager em)
        {
            Vector3 p = _pos[k];
            int n = em.QueryAlive(p, trait.triggerRadius + em.MaxRadius, _query);
            for (int q = 0; q < n; q++)
            {
                int e = _query[q];
                Vector3 ep = em.GetPosition(e);
                float dx = ep.x - p.x, dz = ep.z - p.z, rr = trait.triggerRadius + em.GetRadius(e);
                if (dx * dx + dz * dz > rr * rr) continue;
                Snap(em, e, p);
                return true;
            }
            return false;
        }

        void Snap(EnemyManager em, int e, Vector3 at)
        {
            float dmg = _stats != null ? _stats.Damage : 1f;
            var def = em.GetDef(e);
            bool scripted = def != null && def.behaviour == EnemyBehaviour.Scripted;
            if (!scripted && trait.root != null) em.ApplyStatus(e, trait.root, 0f);
            if (trait.bleed != null) em.ApplyStatus(e, trait.bleed, trait.bleedPower * dmg, trait.bleedStacks);
            em.ApplyDamage(e, new DamageInfo { Amount = trait.snapDamage * dmg, Flags = DamageFlags.Area, Point = em.GetPosition(e), Direction = Vector3.up });
            Snapped++;
            SfxPlayer.Play(SfxId.Snare, at);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at + Vector3.up * 0.3f, 8, 3f, 0.12f, trait.ring, Color.white);
        }

        void Draw()
        {
            if (trait.mesh == null || trait.material == null || _drawn == 0) return;
            if (!_rpReady)
            {
                _rp = new RenderParams(trait.material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };
                _rpReady = true;
            }
            Graphics.RenderMeshInstanced(_rp, trait.mesh, 0, _matrices, _drawn);
        }
    }
}
