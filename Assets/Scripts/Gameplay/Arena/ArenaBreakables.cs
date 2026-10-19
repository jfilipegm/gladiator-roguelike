using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Provincial Pit's barricades and braziers (docs/ARENAS.md): placed props that a boss charge can wreck.
    /// A smashed barricade is gone for the run (its baked obstacle switches off, its collider too, splinters fly), so
    /// the arena opens up as the fight goes on. A toppled brazier leaves burning ground for a while. Weapons don't
    /// break them: they shape the horde's paths for the whole run.
    /// </summary>
    public sealed class ArenaBreakables : MonoBehaviour
    {
        public static ArenaBreakables Instance { get; private set; }

        public Transform[] barricades = System.Array.Empty<Transform>();
        public Transform[] braziers = System.Array.Empty<Transform>();
        [Tooltip("Half the barricade's length and depth (m): what a charge lane must touch.")]
        public Vector2 barricadeHalf = new Vector2(1.3f, 0.65f);
        public float brazierRadius = 0.45f;
        [Header("Toppled brazier")]
        public float fireRadius = 1.7f;
        public float fireSeconds = 8f;
        public float fireDps = 10f;
        [Header("Look")]
        public Color splinterA = new Color(0.62f, 0.43f, 0.26f), splinterB = new Color(0.9f, 0.78f, 0.55f);
        public Color emberA = new Color(1f, 0.55f, 0.15f), emberB = new Color(1f, 0.9f, 0.6f);

        bool[] _barricadeBroken = System.Array.Empty<bool>();
        bool[] _brazierDown = System.Array.Empty<bool>();
        int[] _boxIndex = System.Array.Empty<int>();
        int[] _circleIndex = System.Array.Empty<int>();
        bool _mapped;

        public int BarricadesBroken { get; private set; }
        public int BraziersToppled { get; private set; }
        public bool IsBroken(int i) => i >= 0 && i < _barricadeBroken.Length && _barricadeBroken[i];
        public bool IsToppled(int i) => i >= 0 && i < _brazierDown.Length && _brazierDown[i];

        void Awake()
        {
            Instance = this;
            _barricadeBroken = new bool[barricades.Length];
            _brazierDown = new bool[braziers.Length];
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Finds each piece's baked obstacle (the EnemyManager loads them on Awake, so do it on first use).</summary>
        void Map()
        {
            if (_mapped) return;
            var obs = EnemyManager.Instance != null ? EnemyManager.Instance.Obstacles : null;
            if (obs == null) return;
            _mapped = true;
            _boxIndex = new int[barricades.Length];
            for (int i = 0; i < barricades.Length; i++)
                _boxIndex[i] = barricades[i] != null ? obs.FindBox(XZ(barricades[i].position), 0.8f) : -1;
            _circleIndex = new int[braziers.Length];
            for (int i = 0; i < braziers.Length; i++)
                _circleIndex[i] = braziers[i] != null ? obs.FindCircle(XZ(braziers[i].position), 0.6f) : -1;
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>
        /// Wrecks every barricade and topples every brazier a lane from `from` to `to`, `halfWidth` wide, touches.
        /// Returns how many pieces it broke.
        /// </summary>
        public int SmashAlong(Vector3 from, Vector3 to, float halfWidth)
        {
            Map();
            int n = 0;
            for (int i = 0; i < barricades.Length; i++)
                if (!_barricadeBroken[i] && barricades[i] != null && LaneTouches(from, to, halfWidth, barricades[i].position, barricadeHalf.x))
                {
                    BreakBarricade(i);
                    n++;
                }
            for (int i = 0; i < braziers.Length; i++)
                if (!_brazierDown[i] && braziers[i] != null && LaneTouches(from, to, halfWidth, braziers[i].position, brazierRadius))
                {
                    Topple(i);
                    n++;
                }
            return n;
        }

        static bool LaneTouches(Vector3 from, Vector3 to, float halfWidth, Vector3 p, float radius)
        {
            Vector2 a = XZ(from), b = XZ(to), q = XZ(p);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            float r = halfWidth + radius;
            return (a + ab * t - q).sqrMagnitude < r * r;
        }

        public void BreakBarricade(int i)
        {
            if (i < 0 || i >= barricades.Length || _barricadeBroken[i]) return;
            Map();
            _barricadeBroken[i] = true;
            BarricadesBroken++;
            var t = barricades[i];
            var obs = EnemyManager.Instance != null ? EnemyManager.Instance.Obstacles : null;
            if (obs != null && _boxIndex.Length > i) obs.SetBoxActive(_boxIndex[i], false);
            Vector3 at = t.position + Vector3.up * 0.6f;
            var pop = DeathPopFx.Instance;
            if (pop != null) { pop.Burst(at, 26, 7f, 0.22f, splinterA, splinterB); pop.Burst(at + t.right, 10, 5f, 0.18f, splinterA, splinterB); pop.Burst(at - t.right, 10, 5f, 0.18f, splinterA, splinterB); }
            SfxPlayer.Play(SfxId.Splinter, at);
            ScreenShake.Add(0.25f);
            t.gameObject.SetActive(false); // its collider goes with it: the player walks through too
        }

        public void Topple(int i)
        {
            if (i < 0 || i >= braziers.Length || _brazierDown[i]) return;
            Map();
            _brazierDown[i] = true;
            BraziersToppled++;
            var t = braziers[i];
            var obs = EnemyManager.Instance != null ? EnemyManager.Instance.Obstacles : null;
            if (obs != null && _circleIndex.Length > i) obs.SetCircleActive(_circleIndex[i], false);
            Vector3 at = t.position;
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at + Vector3.up, 24, 6f, 0.16f, emberA, emberB);
            if (HazardZones.Instance != null) HazardZones.Instance.Add(at, fireRadius, fireSeconds, fireDps);
            SfxPlayer.Play(SfxId.FirePot, at);
            t.gameObject.SetActive(false);
        }
    }
}
