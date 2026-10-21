using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The arena crowd (CLAUDE.md §4): instanced draws of the spectator mesh at baked spots. Per-instance tunic colour
    /// and animation phase come from a hash of each spot's position inside the Crowd shader, and the global
    /// _CrowdExcitement makes them cheer; nothing per spectator runs on the CPU. Drawn every frame, paused or not.
    /// </summary>
    public sealed class CrowdRenderer : MonoBehaviour, ITickable
    {
        public Mesh mesh;
        public Material material;
        [Tooltip("x, y, z = feet, w = yaw (deg). Baked by ArenaBuilder from the layout's crowd zones.")]
        public Vector4[] spots = System.Array.Empty<Vector4>();
        [Tooltip("Size jitter between spectators (±).")]
        public float scaleJitter = 0.08f;

        const int MaxPerDraw = 1023;
        Matrix4x4[] _matrices;
        RenderParams _rp;

        public int Count => _matrices != null ? _matrices.Length : 0;

        void Awake()
        {
            _matrices = new Matrix4x4[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var s = spots[i];
                float k = 1f + (Mathf.Repeat(s.x * 12.9898f + s.z * 78.233f, 1f) - 0.5f) * 2f * scaleJitter;
                _matrices[i] = Matrix4x4.TRS(new Vector3(s.x, s.y, s.z), Quaternion.Euler(0f, s.w, 0f), Vector3.one * k);
            }
            if (material != null)
                _rp = new RenderParams(material)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 200f),
                };
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 80);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            if (mesh == null || material == null || _matrices.Length == 0) return;
            // the amphitheatre seats ~1,700: draw in chunks of the classic instancing cap
            for (int s = 0; s < _matrices.Length; s += MaxPerDraw)
                Graphics.RenderMeshInstanced(_rp, mesh, 0, _matrices, Mathf.Min(MaxPerDraw, _matrices.Length - s), s);
        }
    }
}
