using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Note 32: Blood Slide's trail drawn as blood smeared on the sand instead of telegraph rings. BoonEffects queues one
    /// smear per Bleed slide-trail mark during its tick (Smear); everything is drawn in one instanced call in the Late
    /// group, then cleared (paused: the last frame's smears stay on screen), as TelegraphRenderer does. Each smear is a
    /// quad lying on the ground the player slid on, stretched along the slide (twice the distance slid since the last
    /// mark, so every smear overlaps half of each neighbour and the marks run together into one streak), sitting a little
    /// behind its mark, and showing one of the smear shapes of PixelSrc/blood_smears.py (ragged edges, drag streaks,
    /// pools, droplets), mirrored at random. Gladiator/BloodSmear dries it from crimson to maroon over the mark's life
    /// and crumbles it away at the end. Lives on the player prefab beside BoonEffects. One shared material; per-instance
    /// age and shape through an MPB array (FX system, not a hot mesh). No allocation per frame.
    /// </summary>
    public sealed class BloodTrailRenderer : MonoBehaviour, ITickable
    {
        public const int Max = 128;
        /// <summary>Smear shapes in the atlas (BloodSmear.shader's _Cells squared; PixelSrc/blood_smears.py's SHAPES).</summary>
        public const int Shapes = 4;

        [Tooltip("M_FxBloodTrail (Gladiator/BloodSmear + BloodSmears.png). None = BoonEffects draws the trail as rings.")]
        public Material material;
        [Tooltip("Smear width across the slide, in mark radii (2 = as wide as the bleeding area, droplets included).")]
        public float widthPerRadius = 2f;
        [Tooltip("Smear length along the slide, in distances slid between two marks (2 = each smear overlaps half of each neighbour: the smear shapes join seamlessly there).")]
        public float lengthPerSpacing = 2f;
        [Tooltip("How far the smear sits behind its mark, as a share of its length (it trails the player instead of running ahead).")]
        [Range(0f, 0.5f)] public float trailBack = 0.25f;
        [Tooltip("Height above the ground (m); the shader also pulls it forward in depth so the floor never wins.")]
        public float lift = 0.02f;

        static readonly int ParamsId = Shader.PropertyToID("_SmearParams");

        readonly Matrix4x4[] _matrices = new Matrix4x4[Max];
        readonly Vector4[] _params = new Vector4[Max];
        int _count, _lastCount;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        RenderParams _rp;

        /// <summary>Smears queued this frame.</summary>
        public int Count => _count;
        /// <summary>Smears drawn by the last draw (tests).</summary>
        public int DrawnLastFrame => _lastCount;

        void Awake()
        {
            _quad = new Mesh { name = "BloodSmearQuad" };
            _quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
            _mpb = new MaterialPropertyBlock();
            _mpb.SetVectorArray(ParamsId, _params); // fixes the array size once
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 78);

        void OnDisable() => GameLoop.Unregister(this);

        void OnDestroy()
        {
            if (_quad != null) Destroy(_quad);
        }

        /// <summary>
        /// Queue one smear for this frame: the mark at `pos` (on the ground), slid along `dir` over ground with normal
        /// `up`, bleeding within `radius`, `spacing` metres after the previous mark; `age` 0..1 of its `life` seconds;
        /// `serial` (the mark's running number) picks the shape and whether it's mirrored.
        /// </summary>
        public void Smear(Vector3 pos, Vector3 dir, Vector3 up, float radius, float spacing, float age, float life, int serial)
        {
            if (_count >= Max || Time.deltaTime <= 0f) return; // paused: last frame's smears stay on screen
            int shape = ShapeOf(serial, out bool mirrored);
            _matrices[_count] = SmearMatrix(pos, dir, up, radius * widthPerRadius, spacing * lengthPerSpacing, trailBack, lift);
            _params[_count] = new Vector4(Mathf.Clamp01(age), Mathf.Max(0.1f, life), shape, mirrored ? -1f : 1f);
            _count++;
        }

        /// <summary>
        /// The smear quad's transform: on the plane through `pos` with normal `up` (lifted along it; a missing or steep
        /// normal lies flat), its length along `dir` flattened onto that plane, its centre `back` of its length behind
        /// the mark. Local x = across the slide (width), z = along it (length), y = the ground normal (scale 1).
        /// </summary>
        public static Matrix4x4 SmearMatrix(Vector3 pos, Vector3 dir, Vector3 up, float width, float length, float back, float lift)
        {
            if (up.y < 0.5f) up = Vector3.up;
            else up.Normalize();
            Vector3 fwd = dir - up * Vector3.Dot(dir, up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(Vector3.right, up); // no direction: any along the ground
            fwd.Normalize();
            return Matrix4x4.TRS(pos + up * lift - fwd * (length * back), Quaternion.LookRotation(fwd, up), new Vector3(width, 1f, length));
        }

        /// <summary>Mark `serial` → its smear shape (atlas cell) and mirror; an integer hash, so neighbours differ
        /// without a pattern. PixelSrc/blood_smears.py's shape_of is the same hash (its preview): keep them in step.</summary>
        public static int ShapeOf(int serial, out bool mirrored)
        {
            unchecked
            {
                uint h = (uint)serial * 0x9E3779B1u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                mirrored = ((h >> 8) & 1u) != 0;
                return (int)(h % Shapes);
            }
        }

        public void Tick(float dt, float unscaledDt)
        {
            // While paused (level-up, pause menu) nothing ticks, so redraw the last frame instead of going blank.
            int n = dt > 0f ? _count : _lastCount;
            if (n > 0 && material != null)
            {
                if (_rp.material != material)
                    _rp = new RenderParams(material)
                    {
                        shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                        receiveShadows = true,
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                        matProps = _mpb,
                    };
                _mpb.SetVectorArray(ParamsId, _params);
                Graphics.RenderMeshInstanced(_rp, _quad, 0, _matrices, n);
            }
            if (dt > 0f) { _lastCount = _count; _count = 0; }
        }
    }
}
