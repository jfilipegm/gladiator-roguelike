using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Slave Pit's second secret (Phase 5.6, docs/UNLOCKS.md). On some runs, once, a golden rat scurries out of a
    /// hole at one of the layout's secret spots and flees the player: faster than a run, slower than a slide, so it's
    /// caught with movement. It stops to sniff now and then, sparkles and squeaks so it's seen, and dives back into a
    /// hole if it isn't caught in time. Caught: a burst of coins, Favor, a cheer, and the first time the flag
    /// `golden_rat` (the Sling's unlock) is saved at once. Weapons and enemies ignore it.
    /// </summary>
    public sealed class GoldenRat : MonoBehaviour, ITickable
    {
        public enum Phase : byte { NotThisRun, Waiting, Running, Caught, Escaped }

        public GameObject model;
        public Material material;
        public Vector3[] holes = System.Array.Empty<Vector3>();
        public string flag = "golden_rat";

        [Header("When")]
        [Range(0f, 1f)] public float chance = 0.5f;
        [Tooltip("It comes out at a random moment in this window of the run (s).")]
        public Vector2 window = new Vector2(90f, 360f);
        [Tooltip("Tests: always appear (still at the window's time unless Appear is called).")]
        public bool alwaysAppear;

        [Header("How it runs")]
        [Tooltip("m/s. The player runs at 7.5: a slide or a jump closes the gap.")]
        public float speed = 8.5f;
        [Tooltip("It stops to sniff every `sniffEvery` s for `sniffTime` s: the window to catch it on foot.")]
        public float sniffEvery = 4f, sniffTime = 0.6f;
        [Tooltip("Seconds before it dives back into a hole.")]
        public float life = 18f;
        [Tooltip("Within this (m, XZ) of the player it's caught.")]
        public float catchRange = 1.2f;
        [Tooltip("The model's size: a real rat's size was lost in the horde at 1080p (review: ×1.6 reads).")]
        public float scale = 1.6f;
        [Tooltip("It notices the player within this range; farther away it just wanders.")]
        public float fleeRange = 14f;
        public float bodyRadius = 0.35f;

        [Header("Reward and show")]
        public int gold = 30;
        public float favor = 25f;
        public string firstShout = "WHAT WAS THAT?!";
        public string shout = "THE GOLDEN RAT!";
        public string escapeShout = "IT GOT AWAY!";
        public Color shoutColor = new Color(1f, 0.84f, 0.3f);
        public Color sparkA = new Color(1f, 0.9f, 0.45f), sparkB = new Color(1f, 1f, 0.85f);
        public float sparkleEvery = 0.5f, squeakEvery = 1.4f;

        Transform _view;
        Phase _phase = Phase.NotThisRun;
        float _appearAt, _t, _nextSniff, _sniffLeft, _nextSparkle, _nextSqueak, _wanderSeed;
        Vector3 _pos, _dir;
        Rng _rng; // R8: whether, when, which hole and the wander, forked from the run seed in Start

        public Phase State => _phase;
        public Vector3 Position => _pos;
        /// <summary>When it comes out this run (run seconds; 0 = not this run).</summary>
        public float AppearAt => _appearAt;
        /// <summary>The hole it comes out of (rolled with the time, at Start; -1 = not this run).</summary>
        public int Hole { get; private set; } = -1;
        public int Caught { get; private set; }
        public static GoldenRat Instance { get; private set; }

        void OnEnable() { Instance = this; GameLoop.Register(this, TickGroup.Pickups, 21); }
        void OnDisable() { GameLoop.Unregister(this); if (Instance == this) Instance = null; }

        void Start()
        {
            _rng = GameRandom.Fork(RngStream.Map, GameRandom.KeyOf("golden_rat"));
            if (holes == null || holes.Length == 0) return;
            bool comes = _rng.Next01() <= chance; // rolled even when alwaysAppear, so a test sees the seed's own time and hole
            if (!alwaysAppear && !comes) return;
            _appearAt = _rng.Range(window.x, window.y);
            Hole = _rng.Range(0, holes.Length);
            _phase = Phase.Waiting;
        }

        /// <summary>Out of hole `index` now (tests, admin).</summary>
        public void Appear(int index)
        {
            if (holes == null || holes.Length == 0) return;
            Hole = Mathf.Clamp(index, 0, holes.Length - 1);
            _pos = holes[Hole];
            var em = EnemyManager.Instance;
            if (em != null)
            {
                em.ClampInside(ref _pos, 1.5f);
                if (em.Height != null) _pos.y = em.Height.Sample(_pos.x, _pos.z);
            }
            if (_view == null) _view = Spawn();
            _view.gameObject.SetActive(true);
            _view.localScale = Vector3.one * scale;
            _phase = Phase.Running;
            _t = 0f;
            _nextSniff = sniffEvery;
            _sniffLeft = 0f;
            _wanderSeed = _rng.Next01() * 100f;
            Vector3 toCenter = em != null ? em.arenaCenter - _pos : -_pos;
            toCenter.y = 0f;
            _dir = toCenter.sqrMagnitude > 0.01f ? toCenter.normalized : Vector3.forward;
            SfxPlayer.Play(SfxId.Squeak, _pos);
            Burst(10);
        }

        Transform Spawn()
        {
            var go = model != null ? Instantiate(model, transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (model == null) { go.transform.SetParent(transform, false); Destroy(go.GetComponent<Collider>()); go.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f); }
            go.name = "GoldenRat";
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go.transform;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (dt <= 0f) return;
            if (_phase == Phase.Waiting)
            {
                var run = RunState.Current;
                if (run != null && run.Elapsed >= _appearAt && !run.Ended) Appear(Hole);
                return;
            }
            if (_phase != Phase.Running) return;
            var player = Player.Main;
            var em = EnemyManager.Instance;
            if (player == null || em == null) return;
            _t += dt;

            Vector3 pp = player.transform.position;
            Vector3 away = _pos - pp;
            away.y = 0f;
            float dist = away.magnitude;
            if (dist < catchRange && Mathf.Abs(pp.y - _pos.y) < 1.4f) { Catch(); return; }
            if (_t >= life) { Escape(); return; }

            // sniff stops: the catch window on foot
            _nextSniff -= dt;
            if (_nextSniff <= 0f) { _sniffLeft = sniffTime; _nextSniff = sniffEvery; }
            float move = speed;
            if (_sniffLeft > 0f) { _sniffLeft -= dt; move = 0f; }

            // away from the player, wobbling; near the wall, run along it (away from the player's side) instead of into it
            Vector3 want = dist > 0.01f && dist < fleeRange ? away / dist : _dir;
            float wob = Mathf.Sin((_t + _wanderSeed) * 2.3f) * 0.6f;
            want = Quaternion.Euler(0f, wob * 40f, 0f) * want;
            float edge = em.EdgeDistance01(_pos, 2f);
            if (edge > 0.8f)
            {
                Vector3 inward = em.arenaCenter - _pos;
                inward.y = 0f;
                inward.Normalize();
                Vector3 tangent = new Vector3(-inward.z, 0f, inward.x);
                if (Vector3.Dot(tangent, away) < 0f) tangent = -tangent;
                float k = Mathf.Clamp01((edge - 0.8f) / 0.2f);
                want = Vector3.Lerp(want, (tangent * 0.8f + inward * 0.4f).normalized, k);
            }
            _dir = Vector3.Slerp(_dir, want.normalized, Mathf.Clamp01(dt * 8f));
            Vector3 np = _pos + _dir * (move * dt);
            em.Obstacles?.PushOut(ref np, bodyRadius);
            em.ClampInside(ref np, 1f);
            if (em.Height != null) np.y = em.Height.Sample(np.x, np.z);
            _pos = np;

            // the show: a scurrying hop, a glint, a squeak
            float hop = move > 0f ? Mathf.Abs(Mathf.Sin(_t * 26f)) * 0.07f : 0f;
            _view.position = _pos + Vector3.up * hop;
            if (_dir.sqrMagnitude > 1e-4f) _view.rotation = Quaternion.LookRotation(_dir, Vector3.up);
            _nextSparkle -= dt;
            if (_nextSparkle <= 0f) { _nextSparkle = sparkleEvery; Burst(3); }
            _nextSqueak -= dt;
            if (_nextSqueak <= 0f) { _nextSqueak = squeakEvery; SfxPlayer.Play(SfxId.Squeak, _pos); }
        }

        void Burst(int n)
        {
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(_pos + Vector3.up * 0.35f * scale, n, 2.5f, 0.1f, sparkA, sparkB);
        }

        /// <summary>The player touched it (or tests): coins, Favor, a cheer, the flag.</summary>
        public void Catch()
        {
            if (_phase != Phase.Running) return;
            _phase = Phase.Caught;
            Caught++;
            _view.gameObject.SetActive(false);
            var pickups = PickupSystem.Instance;
            if (pickups != null)
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    pickups.Spawn(PickupSystem.Kind.Gold, _pos + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 0.8f, gold / 6f);
                }
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(_pos + Vector3.up * 0.4f, 24, 5f, 0.16f, sparkA, sparkB);
            SfxPlayer.Play(SfxId.Coin, _pos);
            SfxPlayer.Play2D(SfxId.Cheer);
            if (Favor.Instance != null) Favor.Instance.Add(favor);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.6f);
            bool first = MetaProgress.SetFlag(flag);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = first ? firstShout : shout, Color = shoutColor, Seconds = 2.4f, Kind = AnnounceKind.Line });
        }

        void Escape()
        {
            _phase = Phase.Escaped;
            _view.gameObject.SetActive(false);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(_pos + Vector3.up * 0.2f, 10, 3f, 0.12f, new Color(0.45f, 0.33f, 0.2f), new Color(0.62f, 0.5f, 0.33f));
            SfxPlayer.Play(SfxId.Squeak, _pos);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Text = escapeShout, Color = shoutColor, Seconds = 1.6f, Kind = AnnounceKind.Line });
        }
    }
}
