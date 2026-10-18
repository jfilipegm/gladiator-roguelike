using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Ground decals for everything that will hurt the player (CLAUDE.md §5: orange-red telegraphs) plus elite affix
    /// rings and fire patches. Systems call Circle(...) during their tick; everything is drawn in one instanced call
    /// in the Late group, then cleared. One shared material; per-instance colour/params via an MPB array (FX system,
    /// not a hot mesh).
    /// </summary>
    public sealed class TelegraphRenderer : MonoBehaviour, ITickable
    {
        public enum Style { Telegraph = 0, Fire = 1, Ring = 2 }
        public const int Max = 512;

        public static TelegraphRenderer Instance { get; private set; }

        public Material material;
        public float lift = 0.04f;

        static readonly int ColorId = Shader.PropertyToID("_TColor");
        static readonly int ParamsId = Shader.PropertyToID("_TParams");

        readonly Matrix4x4[] _matrices = new Matrix4x4[Max];
        readonly Vector4[] _colors = new Vector4[Max];
        readonly Vector4[] _params = new Vector4[Max];
        int _count;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        RenderParams _rp;

        public int Count => _count;

        void Awake()
        {
            Instance = this;
            _quad = new Mesh { name = "TelegraphQuad" };
            _quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
            _mpb = new MaterialPropertyBlock();
            _mpb.SetVectorArray(ColorId, _colors); // fixes the array size once
            _mpb.SetVectorArray(ParamsId, _params);
            _rp = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                matProps = _mpb,
            };
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 80);

        void OnDisable()
        {
            GameLoop.Unregister(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Queue a ground circle for this frame. `fill` 0..1 for telegraphs (how close the hit is), life fraction for
        /// fire, &gt;0 for a fast-pulsing ring. `center.y` should be the ground height.
        /// </summary>
        public void Circle(Vector3 center, float radius, float fill, Color color, Style style, float thickness = 0.07f, float seed = 0f)
        {
            if (_count >= Max) return;
            float s = radius * 2f;
            _matrices[_count] = Matrix4x4.TRS(new Vector3(center.x, center.y + lift, center.z), Quaternion.identity, new Vector3(s, 1f, s));
            _colors[_count] = color;
            _params[_count] = new Vector4(fill, (float)style, seed, thickness);
            _count++;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (_count == 0 || material == null) { _count = 0; return; }
            _mpb.SetVectorArray(ColorId, _colors);
            _mpb.SetVectorArray(ParamsId, _params);
            Graphics.RenderMeshInstanced(_rp, _quad, 0, _matrices, _count);
            _count = 0;
        }
    }
}
