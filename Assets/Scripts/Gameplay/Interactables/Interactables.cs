using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Armory Chests and the Blood Altar (M2.7), one central tick (CLAUDE.md §4). A fixed pool of model instances is
    /// created at load; chests appear on clear, flat ground away from the player (later: the arena layout's anchors),
    /// cost gold (InteractableConfig.PriceFor), and open a pick of Uncommon+ cards through LevelUpController. The altar
    /// takes max HP for good and opens a pick of Rare+ cards, once. Interact = E / gamepad North within reach; the HUD
    /// shows the prompt and the floating prices. Allocation-free after load.
    /// </summary>
    public sealed class Interactables : MonoBehaviour, ITickable
    {
        public enum Kind : byte { Chest, Altar }
        public enum State : byte { Free, Ready, Opening, Spent }

        public static Interactables Instance { get; private set; }

        public InteractableConfig config;
        [Tooltip("Model with a child named \"Lid\" (pivot on the hinge).")]
        public GameObject chestModel;
        public GameObject altarModel;
        [Tooltip("Palette material for the models (they import without materials).")]
        public Material material;
        public DeathPopFx burst;
        public Color chestRing = new Color(1f, 0.84f, 0.3f, 0.85f);
        public Color altarRing = new Color(0.9f, 0.16f, 0.36f, 0.85f);
        [Tooltip("Spots from the arena layout (empty = random clear ground).")]
        public Vector3[] chestAnchors = System.Array.Empty<Vector3>();
        public Vector3[] altarAnchors = System.Array.Empty<Vector3>();
        [Tooltip("Seconds the opened chest stays before it sinks away.")]
        public float openLinger = 1.6f;

        struct Slot
        {
            public Kind Kind;
            public State State;
            public Vector3 Pos;
            public float T;
            public GameObject Go;
            public Transform Lid;
        }

        Slot[] _slots;
        int _chestSlots;
        bool _placed;
        float _respawnT;
        uint _rng = 0x51ED27u;

        public int SlotCount => _slots != null ? _slots.Length : 0;
        public Kind KindOf(int i) => _slots[i].Kind;
        public State StateOf(int i) => _slots[i].State;
        public Vector3 PositionOf(int i) => _slots[i].Pos;
        public bool IsReady(int i) => _slots[i].State == State.Ready;
        public int ChestsOpened { get; private set; }
        public int AltarsUsed { get; private set; }
        /// <summary>What the next chest costs (every chest on the map shows it).</summary>
        public int ChestPrice => config != null ? config.PriceFor(ChestsOpened) : 0;
        /// <summary>The ready interactable in the player's reach (-1 = none): the HUD prompt and Interact use it.</summary>
        public int InReach { get; private set; } = -1;

        void Awake()
        {
            Instance = this;
            int chests = config != null ? config.maxChests : 0, altars = config != null ? config.altars : 0;
            _chestSlots = chests;
            _slots = new Slot[chests + altars];
            for (int i = 0; i < _slots.Length; i++)
            {
                var kind = i < chests ? Kind.Chest : Kind.Altar;
                var model = kind == Kind.Chest ? chestModel : altarModel;
                var go = model != null ? Instantiate(model, transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (model == null) { go.transform.SetParent(transform, false); Destroy(go.GetComponent<Collider>()); }
                go.name = kind == Kind.Chest ? "ArmoryChest" + i : "BloodAltar";
                if (material != null)
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = material;
                go.SetActive(false);
                _slots[i] = new Slot { Kind = kind, State = State.Free, Go = go, Lid = FindDeep(go.transform, "Lid") };
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Pickups, 50);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        float Rand01()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x1000000;
        }

        bool FarEnough(Vector3 p, Vector3 player)
        {
            var c = config;
            if ((new Vector2(p.x - player.x, p.z - player.z)).sqrMagnitude < c.minPlayerDistance * c.minPlayerDistance) return false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State == State.Free) continue;
                var o = _slots[i].Pos;
                if ((new Vector2(p.x - o.x, p.z - o.z)).sqrMagnitude < c.minSpacing * c.minSpacing) return false;
            }
            return true;
        }

        /// <summary>A layout anchor for this kind that is far enough from the player and the others (random order).</summary>
        bool TryAnchor(Vector3[] anchors, EnemyManager em, Vector3 player, out Vector3 spot)
        {
            spot = default;
            int n = anchors != null ? anchors.Length : 0;
            if (n == 0) return false;
            int start = (int)(Rand01() * n);
            for (int k = 0; k < n; k++)
            {
                var p = anchors[(start + k) % n];
                if (!FarEnough(p, player)) continue;
                if (em.Height != null) p.y = em.Height.Sample(p.x, p.z);
                spot = p;
                return true;
            }
            return false;
        }

        /// <summary>A clear, flat spot inside the arena, away from the player and the other interactables.</summary>
        bool TryFindSpot(EnemyManager em, Vector3 player, out Vector3 spot)
        {
            spot = default;
            var c = config;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float a = Rand01() * Mathf.PI * 2f, r = Mathf.Sqrt(Rand01()) * (em.arenaRadius - 3f);
                var p = em.arenaCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if ((new Vector2(p.x - player.x, p.z - player.z)).sqrMagnitude < c.minPlayerDistance * c.minPlayerDistance) continue;
                bool crowded = false;
                for (int i = 0; i < _slots.Length && !crowded; i++)
                {
                    if (_slots[i].State == State.Free) continue;
                    var o = _slots[i].Pos;
                    crowded = (new Vector2(p.x - o.x, p.z - o.z)).sqrMagnitude < c.minSpacing * c.minSpacing;
                }
                if (crowded) continue;
                if (em.Obstacles != null && em.Obstacles.Overlaps(new Vector2(p.x, p.z), c.clearance)) continue;
                if (em.Height != null)
                {
                    float h = em.Height.Sample(p.x, p.z), k = c.clearance;
                    if (Mathf.Abs(em.Height.Sample(p.x + k, p.z) - h) > 0.2f || Mathf.Abs(em.Height.Sample(p.x - k, p.z) - h) > 0.2f ||
                        Mathf.Abs(em.Height.Sample(p.x, p.z + k) - h) > 0.2f || Mathf.Abs(em.Height.Sample(p.x, p.z - k) - h) > 0.2f) continue;
                    p.y = h;
                }
                spot = p;
                return true;
            }
            return false;
        }

        bool Place(int i, EnemyManager em, Vector3 player)
        {
            var anchors = _slots[i].Kind == Kind.Chest ? chestAnchors : altarAnchors;
            Vector3 p;
            if (anchors != null && anchors.Length > 0) { if (!TryAnchor(anchors, em, player, out p)) return false; }
            else if (!TryFindSpot(em, player, out p)) return false;
            ref var s = ref _slots[i];
            s.State = State.Ready;
            s.Pos = p;
            s.T = 0f;
            s.Go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Rand01() * 360f, 0f));
            s.Go.transform.localScale = Vector3.one;
            if (s.Lid != null) s.Lid.localRotation = Quaternion.identity;
            s.Go.SetActive(true);
            return true;
        }

        /// <summary>Puts a chest at `pos` right now (tests, dev). Returns its slot or -1 if every chest slot is busy.</summary>
        public int PlaceChestAt(Vector3 pos)
        {
            for (int i = 0; i < _chestSlots; i++)
            {
                if (_slots[i].State != State.Free) continue;
                ref var s = ref _slots[i];
                s.State = State.Ready; s.Pos = pos; s.T = 0f;
                s.Go.transform.SetPositionAndRotation(pos, Quaternion.identity);
                s.Go.transform.localScale = Vector3.one;
                if (s.Lid != null) s.Lid.localRotation = Quaternion.identity;
                s.Go.SetActive(true);
                return i;
            }
            return -1;
        }

        /// <summary>Moves the altar to `pos` (tests, dev). Returns its slot or -1.</summary>
        public int PlaceAltarAt(Vector3 pos)
        {
            int i = _chestSlots;
            if (i >= _slots.Length) return -1;
            ref var s = ref _slots[i];
            s.State = State.Ready; s.Pos = pos; s.T = 0f;
            s.Go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            s.Go.SetActive(true);
            return i;
        }

        /// <summary>Uses slot i: pays (gold or max HP) and queues the reward pick. False if not ready or unaffordable.</summary>
        public bool TryUse(int i)
        {
            if (i < 0 || i >= _slots.Length || _slots[i].State != State.Ready) return false;
            var run = RunState.Current;
            var lvl = LevelUpController.Instance;
            if (run == null || run.Ended || lvl == null) return false;
            ref var s = ref _slots[i];
            if (s.Kind == Kind.Chest)
            {
                if (!run.SpendGold(ChestPrice)) return false;
                ChestsOpened++;
                s.State = State.Opening;
                s.T = 0f;
                lvl.QueueReward(PickSource.Chest, config.chestFloor);
                if (burst != null) burst.Burst(s.Pos + Vector3.up * 0.8f, 24, 6f, 0.2f, new Color(1f, 0.86f, 0.4f), Color.white);
                if (_respawnT <= 0f) _respawnT = config.respawnDelay;
            }
            else
            {
                var player = Player.Main;
                var stats = player != null ? player.GetComponent<PlayerStats>() : null;
                var health = player != null ? player.GetComponent<PlayerHealth>() : null;
                if (stats == null || health == null) return false;
                float old = health.MaxHp;
                stats.Block.Add(new StatMod(StatId.MaxHp, ModOp.Multiply, 1f - config.altarMaxHpCost));
                health.OnMaxHpChanged(old);
                AltarsUsed++;
                s.State = State.Spent;
                lvl.QueueReward(PickSource.Altar, config.altarFloor);
                if (burst != null) burst.Burst(s.Pos + Vector3.up * 1.1f, 30, 5f, 0.22f, altarRing, new Color(0.35f, 0.05f, 0.1f));
            }
            return true;
        }

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            var player = Player.Main;
            if (config == null || em == null || player == null) return;
            Vector3 pp = player.transform.position;
            if (!_placed && em.Height != null)
            {
                _placed = true;
                for (int i = 0; i < Mathf.Min(config.startingChests, _chestSlots); i++) Place(i, em, pp);
                for (int i = _chestSlots; i < _slots.Length; i++) Place(i, em, pp);
                _respawnT = config.respawnDelay;
            }
            if (dt <= 0f) return; // paused (cards open): nothing moves, no use

            // top the map back up to maxChests, one chest per respawn delay
            int ready = 0, free = -1;
            for (int i = 0; i < _chestSlots; i++)
            {
                if (_slots[i].State == State.Ready) ready++;
                else if (_slots[i].State == State.Free && free < 0) free = i;
            }
            if (free >= 0 && ready < config.maxChests)
            {
                _respawnT -= dt;
                if (_respawnT <= 0f && Place(free, em, pp)) _respawnT = config.respawnDelay;
            }

            var tele = TelegraphRenderer.Instance;
            int reach = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                switch (s.State)
                {
                    case State.Ready:
                    {
                        float dx = s.Pos.x - pp.x, dz = s.Pos.z - pp.z, d2 = dx * dx + dz * dz;
                        float r = config.interactRadius + (s.Kind == Kind.Altar ? 0.5f : 0f); // the altar is bigger
                        if (d2 <= r * r && d2 < best && Mathf.Abs(pp.y - s.Pos.y) < 2f) { best = d2; reach = i; }
                        if (tele != null)
                            tele.Circle(s.Pos, s.Kind == Kind.Chest ? 1.15f : 1.5f, 0f, s.Kind == Kind.Chest ? chestRing : altarRing,
                                TelegraphRenderer.Style.Ring, 0.12f, i * 0.73f);
                        break;
                    }
                    case State.Opening:
                    {
                        s.T += dt;
                        if (s.Lid != null) s.Lid.localRotation = Quaternion.Euler(-105f * Mathf.Clamp01(s.T / 0.35f), 0f, 0f);
                        if (s.T > openLinger)
                        {
                            float k = Mathf.Clamp01(1f - (s.T - openLinger) / 0.4f);
                            s.Go.transform.localScale = new Vector3(k, k, k);
                            if (k <= 0f) { s.State = State.Free; s.Go.SetActive(false); }
                        }
                        break;
                    }
                }
            }
            InReach = reach;
            if (reach >= 0 && player.Input.InteractPressed) TryUse(reach);
        }
    }
}
