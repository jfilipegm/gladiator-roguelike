using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Blob shadows for all enemies in one instanced draw (CLAUDE.md §4: blob decals for enemies, real shadows only
    /// from the player/environment). Quad mesh is engine plumbing built in code; the shader draws the soft disc.
    /// </summary>
    public sealed class BlobShadowRenderer : MonoBehaviour, ITickable
    {
        public static BlobShadowRenderer Instance { get; private set; }

        public Material material;
        public float sizeMultiplier = 2.6f;
        public float lift = 0.03f;

        const int MaxExtra = 256;
        readonly Vector4[] _extra = new Vector4[MaxExtra]; // x, ground y, z, size — projectiles etc., cleared each draw
        int _extraCount;

        Mesh _quad;
        Matrix4x4[] _matrices = new Matrix4x4[1024];
        RenderParams _rp;

        void Awake()
        {
            Instance = this;
            _quad = new Mesh { name = "BlobQuad" };
            _quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
            _rp = new RenderParams(material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 200f),
            };
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late);
        void OnDisable() { GameLoop.Unregister(this); if (Instance == this) Instance = null; }

        /// <summary>Queue one extra shadow for this frame (`groundY` = height of the floor under it).</summary>
        public void Add(Vector3 position, float groundY, float size)
        {
            if (_extraCount < MaxExtra && Time.deltaTime > 0f) _extra[_extraCount++] = new Vector4(position.x, groundY, position.z, size);
        }

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            if (material == null) { if (dt > 0f) _extraCount = 0; return; }
            int count = 0;
            for (int e = 0; e < _extraCount; e++)
            {
                var x = _extra[e];
                _matrices[count++] = Matrix4x4.TRS(new Vector3(x.x, x.y + lift, x.z), Quaternion.identity, new Vector3(x.w, 1f, x.w));
            }
            if (dt > 0f) _extraCount = 0; // paused: keep last frame's projectile shadows
            for (int i = 0; em != null && i < em.HighWater; i++)
            {
                if (!em.IsAlive(i)) continue;
                if (count == _matrices.Length) System.Array.Resize(ref _matrices, _matrices.Length * 2);
                var p = em.GetPosition(i);
                float s = em.GetRadius(i) * sizeMultiplier;
                _matrices[count++] = Matrix4x4.TRS(new Vector3(p.x, p.y + lift, p.z), Quaternion.identity, new Vector3(s, 1f, s));
            }
            if (count > 0) Graphics.RenderMeshInstanced(_rp, _quad, 0, _matrices, count);
        }
    }
}
