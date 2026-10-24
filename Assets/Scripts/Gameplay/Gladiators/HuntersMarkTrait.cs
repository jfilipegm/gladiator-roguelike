using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Beastmaster's Hunter's Mark (note 37, docs/GLADIATORS.md): he starts with `weapon` (the Hunting Spear) beside
    /// his pack, and every enemy its hits land on is marked for `seconds`: a cyan paw sigil over its head, and the pack
    /// runs to marked enemies before any other (CompanionPack reads HuntersMark.Instance).
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Hunter's Mark", fileName = "TR_HuntersMark")]
    public sealed class HuntersMarkTrait : GladiatorTrait
    {
        [Tooltip("The weapon whose hits mark (the Hunting Spear). It's added to his starting weapons.")]
        public WeaponDef weapon;
        [Tooltip("Seconds a mark lasts; another hit renews it.")]
        public float seconds = 4f;
        [Header("The sigil (player colours: cyan on ink, never orange-red)")]
        [Tooltip("FX_HuntMark: a paw print facing +Z, origin at its bottom.")]
        public Mesh sigil;
        [Tooltip("The shared palette material (instanced).")]
        public Material material;
        [Tooltip("Gap between the enemy's head and the sigil's bottom (m).")]
        public float height = 0.45f;
        public float scale = 1f;
        [Tooltip("Up-and-down bob (m) and its speed (cycles/s).")]
        public float bob = 0.06f;
        public float bobSpeed = 1.6f;
        [Tooltip("Seconds the sigil takes to pop in, and to shrink away at the end.")]
        public float popIn = 0.12f;
        public float fadeOut = 0.35f;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<HuntersMark>();
            if (c == null) c = player.AddComponent<HuntersMark>();
            c.trait = this;
            // GladiatorLoadout set the gladiator's starting weapon just before the traits; WeaponSystem equips the list in its Awake
            var ws = player.GetComponent<WeaponSystem>();
            if (weapon == null || ws == null) return;
            var start = ws.startingWeapons;
            int n = start != null ? start.Length : 0;
            for (int i = 0; i < n; i++) if (start[i] == weapon) return;
            var grown = new WeaponDef[n + 1];
            for (int i = 0; i < n; i++) grown[i] = start[i];
            grown[n] = weapon;
            ws.startingWeapons = grown;
        }
    }

    /// <summary>
    /// Runtime half of HuntersMarkTrait: marks per enemy slot (with its uid, so a recycled slot isn't marked), a compact
    /// list of the marked for the pack's picks and the draw, and the sigils (one instanced call, the shared palette
    /// material, turned to face the camera). Allocates its arrays once; nothing per frame.
    /// </summary>
    public sealed class HuntersMark : MonoBehaviour, ITickable
    {
        public const int MaxMarks = 128;
        public static HuntersMark Instance { get; private set; }

        public HuntersMarkTrait trait;

        EventHandler<HitEvent> _onHit;
        int _weaponId;
        WeaponDef _idOf;
        float _clock;
        float[] _until, _born, _head;
        int[] _uid;
        bool[] _listed;
        readonly int[] _list = new int[MaxMarks];
        int _count;
        readonly Matrix4x4[] _matrices = new Matrix4x4[MaxMarks];
        int _drawn;
        RenderParams _rp;
        bool _rpReady;
        Transform _cam;

        /// <summary>Marks applied this run, renewals included (tests, bot log).</summary>
        public int Marks { get; private set; }
        /// <summary>Entries in the marked list (some may have run out since the last tick).</summary>
        public int Count => _count;
        /// <summary>The enemy slot of list entry k (check IsMarked: it may have died or run out).</summary>
        public int SlotAt(int k) => _list[k];
        /// <summary>Sigils drawn last frame (tests).</summary>
        public int Drawn => _drawn;

        void Awake() => _onHit = OnHit;

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Late, 82);
            GameEvents.OnHit.Subscribe(_onHit);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnHit.Unsubscribe(_onHit);
        }

        /// <summary>Is enemy slot `i` (whose uid is `uid`) marked right now? The caller checks it's alive.</summary>
        public bool IsMarked(int i, int uid) => _uid != null && i >= 0 && i < _uid.Length && _uid[i] == uid && _until[i] > _clock;

        /// <summary>Marks enemy slot `i` now, as a hit of the weapon would (tests, admin).</summary>
        public void Mark(int i)
        {
            var em = EnemyManager.Instance;
            if (em == null || !em.IsAlive(i)) return;
            Mark(em, i, em.GetUid(i), 1.2f);
        }

        void OnHit(in HitEvent e)
        {
            if (trait == null || trait.weapon == null || (e.Flags & DamageFlags.Status) != 0) return;
            if (_idOf != trait.weapon) { _idOf = trait.weapon; _weaponId = trait.weapon.RuntimeId; }
            if (e.SourceId != _weaponId) return;
            var em = EnemyManager.Instance;
            if (em == null || !em.IsAlive(e.Index) || em.GetUid(e.Index) != e.TargetId) return; // the killing blow: nothing to mark
            Mark(em, e.Index, e.TargetId, e.Position.y - em.GetPosition(e.Index).y);
        }

        void Mark(EnemyManager em, int i, int uid, float head)
        {
            if (trait == null) return;
            if (_uid == null || _uid.Length < em.capacity)
            {
                _until = new float[em.capacity];
                _born = new float[em.capacity];
                _head = new float[em.capacity];
                _uid = new int[em.capacity];
                _listed = new bool[em.capacity];
                _count = 0;
            }
            if (i < 0 || i >= _uid.Length) return;
            bool fresh = _uid[i] != uid || _until[i] <= _clock;
            if (!_listed[i])
            {
                if (_count >= MaxMarks) return; // a full list: the oldest still run their course
                _list[_count++] = i;
                _listed[i] = true;
            }
            _uid[i] = uid;
            _until[i] = _clock + Mathf.Max(0.1f, trait.seconds);
            _head[i] = Mathf.Max(0.6f, head);
            if (fresh) _born[i] = _clock;
            Marks++;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt > 0f)
            {
                _clock += dt;
                Prune();
                Build();
            }
            Draw(); // paused (level-up cards): last frame's sigils stay on screen
        }

        /// <summary>Drops the dead, the recycled and the run out from the list (swap-remove).</summary>
        void Prune()
        {
            var em = EnemyManager.Instance;
            for (int k = _count - 1; k >= 0; k--)
            {
                int i = _list[k];
                if (em != null && em.IsAlive(i) && em.GetUid(i) == _uid[i] && _until[i] > _clock) continue;
                _listed[i] = false;
                _list[k] = _list[--_count];
            }
        }

        void Build()
        {
            _drawn = 0;
            var em = EnemyManager.Instance;
            if (trait == null || trait.sigil == null || em == null || _count == 0) return;
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
            Vector3 f = _cam != null ? _cam.forward : Vector3.forward;
            f.y = 0f;
            var face = Quaternion.LookRotation(f.sqrMagnitude > 1e-4f ? -f.normalized : Vector3.back); // its front (+Z) to the camera
            float pop = Mathf.Max(0.01f, trait.popIn), fade = Mathf.Max(0.01f, trait.fadeOut);
            for (int k = 0; k < _count; k++)
            {
                int i = _list[k];
                float age = _clock - _born[i], left = _until[i] - _clock;
                float s = trait.scale * Mathf.Min(1f, Mathf.Min(age / pop, left / fade));
                if (age < pop * 2f) s *= 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(age / (pop * 2f)) * Mathf.PI); // a little overshoot
                if (s <= 0.01f) continue;
                float y = _head[i] + trait.height + trait.bob * Mathf.Sin((_clock * trait.bobSpeed + i * 0.13f) * Mathf.PI * 2f);
                _matrices[_drawn++] = Matrix4x4.TRS(em.GetPosition(i) + Vector3.up * y, face, new Vector3(s, s, s));
            }
        }

        void Draw()
        {
            if (_drawn == 0 || trait == null || trait.sigil == null || trait.material == null) return;
            if (!_rpReady)
            {
                _rp = new RenderParams(trait.material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                };
                _rpReady = true;
            }
            Graphics.RenderMeshInstanced(_rp, trait.sigil, 0, _matrices, _drawn);
        }
    }
}
