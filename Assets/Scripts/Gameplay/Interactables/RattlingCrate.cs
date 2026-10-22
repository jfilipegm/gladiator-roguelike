using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Pit Goblin's discovery (docs/GLADIATORS.md): one ordinary-looking crate at one of the layout's secret spots,
    /// chosen per run. It rattles (a shake and a clatter) while the player is within `rattleRange`. Walk into it or
    /// press Interact beside it: the goblin bursts out cackling, grabs a little gold and dives out of the pit, and the
    /// save gets the `flag` at once (kept even if the run is lost). UL_PitGoblin waits on that flag. Once found, the
    /// crate stops appearing.
    /// </summary>
    public sealed class RattlingCrate : MonoBehaviour, ITickable
    {
        public enum Phase : byte { Hidden, Waiting, Escaping, Gone }

        public GameObject crateModel;
        public GameObject goblinModel;
        public Material envMaterial, charMaterial;
        public Vector3[] spots = System.Array.Empty<Vector3>();
        public Vector3 arenaCenter;
        public string flag = "rattling_crate";
        [Tooltip("The crate rattles while the player is this close (m).")]
        public float rattleRange = 10f;
        [Tooltip("Seconds between rattles: close (×0.5 at point blank) to far.")]
        public Vector2 rattleEvery = new Vector2(1.2f, 2.6f);
        public float rattleTime = 0.45f;
        [Tooltip("Rattle shake: tilt (deg) and hop (m). Big enough to catch the eye at 10 m.")]
        public float rattleAngle = 11f, rattleHop = 0.1f;
        [Tooltip("Walking this close breaks it (m).")]
        public float bumpRange = 1.1f;
        [Tooltip("Interact (E) works from this far (m).")]
        public float interactRange = 2.2f;
        public int goldGrabbed = 5;
        public string shout = "WHAT WAS THAT?!";
        public Color shoutColor = new Color(0.55f, 0.95f, 0.45f);
        public Color splinterA = new Color(0.62f, 0.45f, 0.28f), splinterB = new Color(0.85f, 0.72f, 0.5f);
        [Tooltip("Goblin escape: speed (m/s) and seconds before it dives out.")]
        public float escapeSpeed = 6.5f, escapeTime = 1.4f;
        [Tooltip("Tests: appear even if the flag is already in the save.")]
        public bool alwaysAppear;

        Transform _crate, _goblin;
        Quaternion _crateRot;
        Phase _phase = Phase.Hidden;
        float _nextRattle, _shake, _t;
        Vector3 _pos, _runDir;
        Rng _rng; // R8: the spot and the rattles' timing, forked from the run seed in Start

        public Phase State => _phase;
        public Vector3 Position => _pos;
        public int SpotIndex { get; private set; } = -1;
        public int Rattles { get; private set; }
        public static RattlingCrate Instance { get; private set; }

        void OnEnable() { Instance = this; GameLoop.Register(this, TickGroup.Pickups, 20); }
        void OnDisable() { GameLoop.Unregister(this); if (Instance == this) Instance = null; }

        void Start()
        {
            _rng = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("rattling_crate"));
            if (spots == null || spots.Length == 0 || (!alwaysAppear && MetaProgress.Data.HasFlag(flag))) return;
            Place(_rng.Range(0, spots.Length));
        }

        /// <summary>Puts the crate at spot `index` (tests pick one).</summary>
        public void Place(int index)
        {
            SpotIndex = Mathf.Clamp(index, 0, spots.Length - 1);
            _pos = spots[SpotIndex];
            var em = EnemyManager.Instance;
            if (em != null && em.Height != null) _pos.y = em.Height.Sample(_pos.x, _pos.z);
            if (_crate == null)
            {
                _crate = Spawn(crateModel, envMaterial, "SecretCrate");
                _crateRot = Quaternion.Euler(0f, 23f + SpotIndex * 41f, 0f);
            }
            _crate.position = _pos;
            _crate.rotation = _crateRot;
            _crate.gameObject.SetActive(true);
            _phase = Phase.Waiting;
            _nextRattle = rattleEvery.x;
        }

        Transform Spawn(GameObject model, Material mat, string name)
        {
            var go = model != null ? Instantiate(model, transform) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (model == null) { go.transform.SetParent(transform, false); Destroy(go.GetComponent<Collider>()); }
            go.name = name;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go.transform;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            if (_phase == Phase.Waiting) TickWaiting(dt);
            else if (_phase == Phase.Escaping) TickEscape(dt);
        }

        void TickWaiting(float dt)
        {
            var player = Player.Main;
            if (player == null) return;
            Vector3 d = player.transform.position - _pos;
            float dist = new Vector2(d.x, d.z).magnitude;
            bool levelOk = Mathf.Abs(d.y) < 1.5f;
            if (levelOk && (dist < bumpRange || (dist < interactRange && player.Input.InteractPressed))) { Break(); return; }
            if (dist < rattleRange)
            {
                _nextRattle -= dt;
                if (_nextRattle <= 0f)
                {
                    _shake = rattleTime;
                    Rattles++;
                    SfxPlayer.Play(SfxId.Rattle, _pos);
                    float near = Mathf.Lerp(0.5f, 1f, dist / rattleRange);
                    _nextRattle = _rng.Range(rattleEvery.x, rattleEvery.y) * near;
                }
            }
            if (_shake > 0f)
            {
                _shake -= dt;
                float k = _shake / rattleTime;
                float a = Mathf.Sin(_shake * 70f) * rattleAngle * k;
                _crate.rotation = _crateRot * Quaternion.Euler(a * 0.6f, a * 0.4f, a);
                _crate.position = _pos + Vector3.up * (Mathf.Abs(Mathf.Sin(_shake * 45f)) * rattleHop * k);
            }
            else { _crate.rotation = _crateRot; _crate.position = _pos; }
        }

        /// <summary>Out comes the goblin (Interact, a bump, or tests).</summary>
        public void Break()
        {
            if (_phase != Phase.Waiting) return;
            _phase = Phase.Escaping;
            _t = 0f;
            _crate.gameObject.SetActive(false);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(_pos + Vector3.up * 0.5f, 18, 5f, 0.18f, splinterA, splinterB);
            SfxPlayer.Play(SfxId.Rattle, _pos);
            SfxPlayer.Play(SfxId.Cackle, _pos);
            var run = RunState.Current;
            if (run != null && goldGrabbed > 0) run.SpendGold(Mathf.Min(goldGrabbed, run.Gold));
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = shout, Color = shoutColor, Seconds = 2.2f, Kind = AnnounceKind.Line });
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.5f);
            MetaProgress.SetFlag(flag);
            Vector3 outward = _pos - arenaCenter;
            outward.y = 0f;
            _runDir = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;
            if (_goblin == null) _goblin = Spawn(goblinModel, charMaterial, "EscapingGoblin");
            _goblin.gameObject.SetActive(true);
            _goblin.position = _pos;
            _goblin.rotation = Quaternion.LookRotation(_runDir, Vector3.up);
            _goblin.localScale = Vector3.one;
        }

        void TickEscape(float dt)
        {
            _t += dt;
            // a hop out of the crate, a scurry to the fence, a dive
            Vector3 p = _pos + _runDir * (escapeSpeed * Mathf.Max(0f, _t - 0.25f));
            float hop = _t < 0.25f ? Mathf.Sin(_t / 0.25f * Mathf.PI) * 0.9f : Mathf.Abs(Mathf.Sin(_t * 18f)) * 0.18f;
            float dive = Mathf.Clamp01((_t - escapeTime) / 0.3f);
            _goblin.position = p + Vector3.up * (hop + dive * 0.6f);
            _goblin.localScale = Vector3.one * (1f - dive);
            if (dive >= 1f)
            {
                _goblin.gameObject.SetActive(false);
                _phase = Phase.Gone;
            }
        }
    }
}
