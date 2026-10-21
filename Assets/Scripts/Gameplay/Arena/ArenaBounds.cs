using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The player's boundary as one closed polygon in world XZ (P1, the producer's playtest of v0.17.0: "i shouldnt be
    /// able to go back here"). ArenaBuilder stands invisible walls along it; this is the last resort behind them:
    /// PlayerMotor calls <see cref="Clamp"/> after every move, so no collider leak, shove or teleport can ever leave the
    /// player outside. O(1) near the middle (a circle round the centre that fits inside), O(n) near the edge; no GC.
    /// </summary>
    public sealed class ArenaBounds : MonoBehaviour
    {
        public static ArenaBounds Instance { get; private set; }

        [Tooltip("The boundary loop (world XZ, in order round the arena, either winding): the inner face of the player's walls.")]
        public Vector2[] polygon = System.Array.Empty<Vector2>();
        [Tooltip("A point well inside the loop (the arena's centre, XZ).")]
        public Vector2 center;

        float _innerR2 = -1f; // the biggest circle round the centre inside the loop, squared (-1: none)
        float _winding = 1f;  // +1 counter-clockwise (the inside is left of each edge), -1 clockwise
        Vector2 _min, _max;

        /// <summary>How many times Clamp has had to put the player back: each one means the walls leaked.</summary>
        public int Clamps { get; private set; }
        /// <summary>Where the last clamp found the player (world).</summary>
        public Vector3 LastLeak { get; private set; }

        public bool Valid => polygon != null && polygon.Length >= 3;

        void OnEnable()
        {
            Instance = this;
            Prepare();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Tables the winding, the bounds and the inscribed circle (OnEnable; again after changing polygon).</summary>
        public void Prepare()
        {
            _innerR2 = -1f;
            if (!Valid) return;
            float twice = 0f;
            _min = _max = polygon[0];
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                twice += polygon[j].x * polygon[i].y - polygon[i].x * polygon[j].y;
                _min = Vector2.Min(_min, polygon[i]);
                _max = Vector2.Max(_max, polygon[i]);
            }
            _winding = twice >= 0f ? 1f : -1f;
            if (!Contains(center.x, center.y)) return;
            float r = DistanceToEdge(center.x, center.y);
            _innerR2 = r * r;
        }

        /// <summary>True if (x, z) is inside the loop (crossing number).</summary>
        public bool Contains(float x, float z)
        {
            if (!Valid) return true;
            float dx = x - center.x, dz = z - center.y;
            if (dx * dx + dz * dz < _innerR2) return true;
            if (x < _min.x || x > _max.x || z < _min.y || z > _max.y) return false;
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Distance (XZ) from (x, z) to the nearest point of the loop's line, inside or out.</summary>
        public float DistanceToEdge(float x, float z) => Mathf.Sqrt(Nearest(new Vector2(x, z), out _, out _, out _));

        /// <summary>
        /// If `pos` (XZ) is outside the loop, moves it onto the nearest point of the loop and `inset` metres in, and returns
        /// true with the inward direction there (XZ). Y is left alone.
        /// </summary>
        public bool Clamp(ref Vector3 pos, float inset, out Vector2 inward)
        {
            inward = Vector2.zero;
            if (!Valid || Contains(pos.x, pos.z)) return false;
            Nearest(new Vector2(pos.x, pos.z), out var q, out int edge, out float s);
            int n = polygon.Length;
            inward = InwardOf(edge);
            float push = inset;
            if (s <= 1e-4f || s >= 1f - 1e-4f)
            {
                // nearest to a corner: in along the corner's bisector, far enough to be `inset` from both its edges
                var m = inward + InwardOf(s <= 1e-4f ? (edge + n - 1) % n : (edge + 1) % n);
                if (m.sqrMagnitude > 1e-6f)
                {
                    m.Normalize();
                    push = inset / Mathf.Max(0.3f, Vector2.Dot(m, inward));
                    inward = m;
                }
            }
            Vector2 t = q + inward * push;
            for (int it = 0; it < 4 && inward != Vector2.zero && Contains(t.x, t.y); it++)
            {
                // next to a corner the push can end up short of the other edge: away from whichever edge is nearest
                float d2 = Nearest(t, out var q2, out _, out _);
                if (d2 >= inset * inset * 0.98f) break;
                Vector2 away = t - q2;
                float l = away.magnitude;
                if (l < 1e-5f) break;
                t = q2 + away / l * inset;
            }
            if (inward == Vector2.zero || !Contains(t.x, t.y))
            {
                // a corner: its edge's normal can point out again; head for the centre instead
                Vector2 toC = center - q;
                float l = toC.magnitude;
                inward = l > 1e-4f ? toC / l : Vector2.right;
                t = q + inward * Mathf.Min(inset, l);
            }
            Clamps++;
            LastLeak = pos;
            pos.x = t.x;
            pos.z = t.y;
            return true;
        }

        /// <summary>Edge k's inward unit normal (edge k joins point k to k + 1).</summary>
        Vector2 InwardOf(int k)
        {
            Vector2 d = polygon[(k + 1) % polygon.Length] - polygon[k];
            var v = new Vector2(-d.y, d.x) * _winding;
            return v.sqrMagnitude > 1e-12f ? v.normalized : Vector2.zero;
        }

        /// <summary>Squared distance from p to the loop's line, the nearest point, the edge it's on (edge k joins point k
        /// to k + 1) and how far along it (0..1).</summary>
        float Nearest(Vector2 p, out Vector2 nearest, out int edge, out float along)
        {
            float best = float.MaxValue;
            nearest = p;
            edge = 0;
            along = 0f;
            int n = polygon.Length;
            for (int k = 0; k < n; k++)
            {
                Vector2 a = polygon[k], ab = polygon[(k + 1) % n] - a;
                float len2 = ab.sqrMagnitude;
                float s = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                Vector2 c = a + ab * s;
                float d2 = (p - c).sqrMagnitude;
                if (d2 < best) { best = d2; nearest = c; edge = k; along = s; }
            }
            return best;
        }
    }
}
