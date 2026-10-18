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
        public Material material;
        public float sizeMultiplier = 2.6f;
        public float lift = 0.03f;

        Mesh _quad;
        Matrix4x4[] _matrices = new Matrix4x4[1024];
        RenderParams _rp;

        void Awake()
        {
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
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            if (em == null || material == null) return;
            int count = 0;
            for (int i = 0; i < em.HighWater; i++)
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
