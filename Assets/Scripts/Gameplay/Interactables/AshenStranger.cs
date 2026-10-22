using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Stone Amphitheater's secret (Phase 5.12, docs/GLADIATORS.md "The Ashen Oracle"). On some runs, once, a masked
    /// stranger appears in a ring of smoke at one of the layout's secret spots and waits. A player who stands inside the
    /// ring for `readSeconds` (the ring fills; stepping out resets it) is read: he vanishes in a burst of ash, "WHO WAS
    /// THAT?", and the flag `ashen_stranger` (the Ashen Oracle's unlock) is saved at once. Unread, he fades after
    /// `stay` seconds. Weapons and enemies ignore him. Once the flag is saved he stops appearing.
    /// </summary>
    public sealed class AshenStranger : MonoBehaviour, ITickable
    {
        public enum Phase : byte { NotThisRun, Waiting, Here, Read, Gone }

        public GameObject model;
        public Material material;
        public Vector3[] spots = System.Array.Empty<Vector3>();
        public Vector3 arenaCenter;
        public string flag = "ashen_stranger";

        [Header("When")]
        [Range(0f, 1f)] public float chance = 0.5f;
        [Tooltip("He appears at a random moment in this window of the run (s).")]
        public Vector2 window = new Vector2(120f, 300f);
        public float stay = 25f;
        [Tooltip("Tests: always appear (still at the window's time unless Appear is called).")]
        public bool alwaysAppear;

        [Header("The reading")]
        public float ringRadius = 2f;
        public float readSeconds = 3f;
        public Color ringColor = new Color(0.72f, 0.55f, 0.95f, 0.85f);
        public Color readColor = new Color(0.9f, 0.8f, 1f, 0.95f);
        public string shout = "WHO WAS THAT?";
        public Color smokeA = new Color(0.45f, 0.42f, 0.5f), smokeB = new Color(0.72f, 0.45f, 0.95f);

        Transform _figure;
        float _at, _t, _read, _puff;
        int _spot;
        Vector3 _pos;

        public static AshenStranger Instance { get; private set; }
        public Phase State { get; private set; } = Phase.NotThisRun;
        public Vector3 Position => _pos;
        public float Read01 => readSeconds > 0f ? Mathf.Clamp01(_read / readSeconds) : 0f;

        void OnEnable() { Instance = this; GameLoop.Register(this, TickGroup.Pickups, 22); }
        void OnDisable() { GameLoop.Unregister(this); if (Instance == this) Instance = null; }

        void Start()
        {
            // R8: whether, when and where, forked from the run seed (the same seed brings him at the same time and spot)
            var rng = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("ashen_stranger"));
            if (spots == null || spots.Length == 0 || (!alwaysAppear && MetaProgress.Data.HasFlag(flag))) return;
            bool comes = rng.Next01() <= chance; // rolled even when alwaysAppear, so a test sees the seed's own time and spot
            if (!alwaysAppear && !comes) return;
            State = Phase.Waiting;
            _at = rng.Range(window.x, window.y);
            _spot = rng.Range(0, spots.Length);
        }

        /// <summary>He appears now at spot `index` (tests; the run's clock does it otherwise).</summary>
        public void Appear(int index)
        {
            if (spots == null || spots.Length == 0) return;
            _pos = spots[Mathf.Clamp(index, 0, spots.Length - 1)];
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) _pos.y = em.Height.Sample(_pos.x, _pos.z);
            if (_figure == null)
            {
                var go = model != null ? Instantiate(model, transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                if (model == null) { go.transform.SetParent(transform, false); Destroy(go.GetComponent<Collider>()); }
                go.name = "MaskedStranger";
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) if (material != null) r.sharedMaterial = material;
                _figure = go.transform;
            }
            Vector3 face = arenaCenter - _pos;
            face.y = 0f;
            _figure.SetPositionAndRotation(_pos, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward, Vector3.up));
            _figure.gameObject.SetActive(true);
            _t = 0f;
            _read = 0f;
            State = Phase.Here;
            Puff(16);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            if (State == Phase.Waiting)
            {
                var run = RunState.Current;
                if (run != null && run.Elapsed >= _at) Appear(_spot);
                return;
            }
            if (State != Phase.Here) return;
            _t += dt;
            var player = Player.Main;
            bool inside = false;
            if (player != null)
            {
                Vector3 d = player.transform.position - _pos;
                inside = new Vector2(d.x, d.z).magnitude < ringRadius && Mathf.Abs(d.y) < 1.5f;
            }
            _read = inside ? _read + dt : 0f;
            var tele = TelegraphRenderer.Instance;
            if (tele != null)
            {
                tele.Circle(_pos, ringRadius, 1f, ringColor, TelegraphRenderer.Style.Ring, 0.08f, 0.7f);
                if (_read > 0f) tele.Circle(_pos, ringRadius * Read01, 1f, readColor, TelegraphRenderer.Style.Ring, 0.12f, 1.3f);
            }
            _puff -= dt;
            if (_puff <= 0f) { _puff = 0.35f; Puff(3); }
            if (_read >= readSeconds) { Vanish(); return; }
            if (_t >= stay) { _figure.gameObject.SetActive(false); State = Phase.Gone; Puff(10); }
        }

        void Puff(int n)
        {
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(_pos + Vector3.up * 1.1f, n, 1.4f, 0.16f, smokeA, smokeB);
        }

        /// <summary>He's read you: gone in ash, and the flag is saved now (tests call it too).</summary>
        public void Vanish()
        {
            if (State != Phase.Here) return;
            State = Phase.Read;
            if (_figure != null) _figure.gameObject.SetActive(false);
            Puff(40);
            SfxPlayer.Play(SfxId.Explosion, _pos);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = shout, Color = readColor, Seconds = 2.2f, Kind = AnnounceKind.Line });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.5f);
            MetaProgress.SetFlag(flag);
        }
    }
}
