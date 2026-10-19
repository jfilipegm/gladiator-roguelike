using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// What the crowd throws when Favor fills (docs/FAVOR.md): a gift arcs out of the stands, a gold ring marks where it
    /// will land 3–6 m from the player, then it waits on the ground until walked over or its time runs out. Amphora
    /// heals, Gold Shower bursts coins, Laurel bursts XP gems, the Weapon in the Sand lends a pool weapon for a while.
    /// A few pooled model instances made at load; central tick; allocation-free after load.
    /// </summary>
    public sealed class CrowdGifts : MonoBehaviour, ITickable
    {
        public static CrowdGifts Instance { get; private set; }

        public FavorConfig config;
        [Tooltip("Models by GiftKind: Amphora, GoldShower, WeaponInSand, Laurel.")]
        public GameObject[] models = new GameObject[4];
        public Material material;
        public DeathPopFx burst;
        public Color ringColor = new Color(1f, 0.86f, 0.4f, 0.9f);

        public const string ThrowText = "A GIFT FROM THE CROWD!";

        enum State : byte { Free, Flying, Waiting }

        struct Slot
        {
            public State State;
            public GiftKind Kind;
            public WeaponDef Weapon;
            public Vector3 From, To;
            public float T;
            public Transform[] Views; // one per kind, only Kind's is active
            public Transform Root;
        }

        Slot[] _slots;
        uint _rng = 0x6A11F7u;
        PlayerHealth _health;
        WeaponSystem _weapons;
        PlayerBuild _build;
        readonly WeaponDef[] _candidates = new WeaponDef[16];

        public int Thrown { get; private set; }
        public int Collected { get; private set; }
        public int WaitingCount
        {
            get { int n = 0; if (_slots != null) foreach (var s in _slots) if (s.State == State.Waiting) n++; return n; }
        }

        void Awake()
        {
            Instance = this;
            int n = config != null ? Mathf.Max(1, config.maxGifts) : 3;
            _slots = new Slot[n];
            for (int i = 0; i < n; i++)
            {
                var root = new GameObject("Gift" + i).transform;
                root.SetParent(transform, false);
                var views = new Transform[4];
                for (int k = 0; k < views.Length; k++)
                {
                    var model = models != null && k < models.Length ? models[k] : null;
                    var go = model != null ? Instantiate(model, root) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                    if (model == null) { go.transform.SetParent(root, false); go.transform.localScale = Vector3.one * 0.6f; Destroy(go.GetComponent<Collider>()); }
                    if (material != null)
                        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) { r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
                    go.SetActive(false);
                    views[k] = go.transform;
                }
                root.gameObject.SetActive(false);
                _slots[i] = new Slot { State = State.Free, Root = root, Views = views };
            }
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Pickups, 60);
            RunBotSensor.NearestGift = NearestForBot;
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (RunBotSensor.NearestGift == (System.Func<Vector3, float, (bool, Vector3)>)NearestForBot) RunBotSensor.NearestGift = null;
            if (Instance == this) Instance = null;
        }

        float Rand01()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x1000000;
        }

        void FindPlayer()
        {
            if (_health != null || Player.Main == null) return;
            _health = Player.Main.GetComponent<PlayerHealth>();
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            _build = Player.Main.GetComponent<PlayerBuild>();
        }

        /// <summary>A pool weapon the player doesn't hold and has unlocked (null = none).</summary>
        WeaponDef PickLoanWeapon()
        {
            if (_build == null || _build.catalog == null || _weapons == null) return null;
            int n = 0;
            var ups = _build.catalog.upgrades;
            for (int i = 0; i < ups.Length && n < _candidates.Length; i++)
            {
                var u = ups[i];
                if (u == null || u.kind != UpgradeKind.Weapon || u.weapon == null) continue;
                if (_build.State.IsLocked(u) || _weapons.Find(u.weapon) != null) continue;
                _candidates[n++] = u.weapon;
            }
            return n > 0 ? _candidates[Mathf.Min(n - 1, (int)(Rand01() * n))] : null;
        }

        /// <summary>Throws a gift now (a full Favor meter; admin and tests). Returns the slot, or -1 without a player or arena.</summary>
        public int Throw() => Throw(null);

        /// <summary>Throws a specific gift (tests, admin). null = weighted pick.</summary>
        public int Throw(GiftKind? kind)
        {
            FindPlayer();
            var em = EnemyManager.Instance;
            if (config == null || em == null || Player.Main == null) return -1;
            var weapon = PickLoanWeapon();
            float hp = _health != null ? _health.Hp01 : 1f;
            var k = kind ?? config.PickGift(Rand01(), hp, weapon != null);
            if (k == GiftKind.WeaponInSand && weapon == null) k = GiftKind.Amphora;

            int slot = -1;
            float oldest = -1f;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State == State.Free) { slot = i; break; }
                if (_slots[i].State == State.Waiting && _slots[i].T > oldest) { oldest = _slots[i].T; slot = i; } // full: the oldest waiting gift makes room
            }
            if (slot < 0) return -1;

            Vector3 player = Player.Main.transform.position;
            Vector3 to = LandingSpot(em, player);
            Vector3 outward = new Vector3(to.x - em.arenaCenter.x, 0f, to.z - em.arenaCenter.z);
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
            outward.Normalize();
            Vector3 from = em.arenaCenter + outward * (em.arenaRadius + 4f);
            from.y = to.y + 5f;

            ref var s = ref _slots[slot];
            s.State = State.Flying;
            s.Kind = k;
            s.Weapon = k == GiftKind.WeaponInSand ? weapon : null;
            s.From = from;
            s.To = to;
            s.T = 0f;
            for (int v = 0; v < s.Views.Length; v++) s.Views[v].gameObject.SetActive(v == (int)k);
            s.Root.SetPositionAndRotation(from, Quaternion.identity);
            s.Root.localScale = Vector3.one;
            s.Root.gameObject.SetActive(true);
            Thrown++;
            SfxPlayer.Play(SfxId.GiftThrow, to);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = ThrowText, Color = ringColor, Seconds = 1.8f, Kind = AnnounceKind.Gift });
            GameEvents.OnFavor.Raise(new FavorEvent { Kind = FavorEventKind.GiftThrown, Gift = k, Position = to });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.75f);
            return slot;
        }

        /// <summary>Open ground 3–6 m from the player, inside the arena and off the obstacles.</summary>
        Vector3 LandingSpot(EnemyManager em, Vector3 player)
        {
            var c = config;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float a = Rand01() * Mathf.PI * 2f, r = Mathf.Lerp(c.landDistance.x, c.landDistance.y, Rand01());
                var p = player + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float dx = p.x - em.arenaCenter.x, dz = p.z - em.arenaCenter.z;
                if (dx * dx + dz * dz > (em.arenaRadius - 2f) * (em.arenaRadius - 2f)) continue;
                if (em.Obstacles != null && em.Obstacles.Overlaps(new Vector2(p.x, p.z), 0.8f)) continue;
                p.y = em.Height != null ? em.Height.Sample(p.x, p.z) : player.y;
                if (Mathf.Abs(p.y - player.y) > 2.5f) continue; // not on top of a mound you'd have to climb
                return p;
            }
            var fallback = player + Vector3.forward * 2f;
            fallback.y = em.Height != null ? em.Height.Sample(fallback.x, fallback.z) : player.y;
            return fallback;
        }

        (bool, Vector3) NearestForBot(Vector3 from, float maxDistance)
        {
            int best = -1;
            float bestD = maxDistance * maxDistance;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State != State.Waiting) continue;
                var p = _slots[i].To;
                float d = (p.x - from.x) * (p.x - from.x) + (p.z - from.z) * (p.z - from.z);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best >= 0 ? (true, _slots[best].To) : (false, default);
        }

        /// <summary>Where gift `i` is (tests).</summary>
        public Vector3 PositionOf(int i) => _slots[i].State == State.Flying ? _slots[i].Root.position : _slots[i].To;
        public bool IsWaiting(int i) => _slots[i].State == State.Waiting;
        public bool IsFree(int i) => _slots[i].State == State.Free;

        public void Tick(float dt, float unscaledDt)
        {
            if (config == null || dt <= 0f || _slots == null) return;
            FindPlayer();
            var run = RunState.Current;
            bool over = run != null && run.Ended;
            Vector3 player = Player.Main != null ? Player.Main.transform.position : Vector3.zero;
            var tele = TelegraphRenderer.Instance;
            float pick2 = config.pickupRadius * config.pickupRadius;

            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                switch (s.State)
                {
                    case State.Flying:
                    {
                        s.T += dt;
                        float k = Mathf.Clamp01(s.T / Mathf.Max(0.05f, config.throwTime));
                        Vector3 p = Vector3.Lerp(s.From, s.To, k);
                        p.y += config.throwArc * 4f * k * (1f - k); // parabola on top of the straight line
                        s.Root.SetPositionAndRotation(p, Quaternion.Euler(k * 540f, k * 360f, 0f));
                        if (tele != null) tele.Circle(s.To, 0.9f, k, ringColor, TelegraphRenderer.Style.Ring, 0.1f, i * 0.5f);
                        if (k >= 1f)
                        {
                            s.State = State.Waiting;
                            s.T = 0f;
                            s.Root.SetPositionAndRotation(s.To, Quaternion.identity);
                            SfxPlayer.Play(SfxId.GiftLand, s.To);
                            if (burst != null) burst.Burst(s.To + Vector3.up * 0.3f, 16, 4f, 0.16f, ringColor, Color.white);
                        }
                        break;
                    }
                    case State.Waiting:
                    {
                        s.T += dt;
                        float left = config.giftLifetime - s.T;
                        if (over || left <= 0f) { Release(ref s); break; }
                        float scale = left < 1f ? left : 1f;
                        float bob = Mathf.Sin(s.T * 2.4f + i) * 0.06f;
                        s.Root.SetPositionAndRotation(s.To + Vector3.up * (0.08f + bob), Quaternion.Euler(0f, s.T * 70f, 0f));
                        s.Root.localScale = new Vector3(scale, scale, scale);
                        if (tele != null && (left > 4f || Mathf.Repeat(s.T * 4f, 1f) < 0.6f)) // blinks in its last seconds
                            tele.Circle(s.To, 0.9f, 0f, ringColor, TelegraphRenderer.Style.Ring, 0.08f, i * 0.5f);
                        float dx = player.x - s.To.x, dz = player.z - s.To.z;
                        if (Player.Main != null && dx * dx + dz * dz <= pick2 && Mathf.Abs(player.y - s.To.y) < 2f) Collect(ref s);
                        break;
                    }
                }
            }
        }

        void Collect(ref Slot s)
        {
            var run = RunState.Current;
            var pickups = PickupSystem.Instance;
            Vector3 at = s.To + Vector3.up * 0.5f;
            switch (s.Kind)
            {
                case GiftKind.Amphora:
                    if (_health != null) _health.Heal(_health.MaxHp * config.amphoraHeal);
                    if (burst != null) burst.Burst(at, 22, 3.5f, 0.18f, new Color(1f, 0.3f, 0.42f), Color.white);
                    break;
                case GiftKind.GoldShower:
                    if (pickups != null)
                        for (int c = 0; c < config.goldShowerCoins; c++) pickups.Spawn(PickupSystem.Kind.Gold, at, pickups.config.goldPerCoin);
                    break;
                case GiftKind.Laurel:
                    if (pickups != null && run != null) pickups.SpawnXp(at, run.XpToNext * config.laurelXp);
                    break;
                case GiftKind.WeaponInSand:
                    if (_weapons != null && s.Weapon != null) _weapons.GrantTemporary(s.Weapon, config.sandWeaponLevel, config.sandWeaponSeconds);
                    break;
            }
            Collected++;
            SfxPlayer.Play(SfxId.Pick, s.To);
            GameEvents.OnFavor.Raise(new FavorEvent { Kind = FavorEventKind.GiftCollected, Gift = s.Kind, Position = s.To });
            Release(ref s);
        }

        static void Release(ref Slot s)
        {
            s.State = State.Free;
            s.Weapon = null;
            s.Root.gameObject.SetActive(false);
        }
    }
}
