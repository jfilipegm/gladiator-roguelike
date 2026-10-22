using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// P3 (the producer's note 16): a wave travelling across the vexillum's cloth while it drops and settles. A mesh
    /// effect on the cloth's Image: every quad of its (sliced) mesh is cut into columns ColumnWidth wide, on a grid from
    /// the cloth's left edge so neighbouring quads share their seams, and each column's vertices move up or down by
    /// Offset at their x. The words on the cloth ride the same function (VexillumBanner moves their TMP vertices).
    /// With Amplitude 0 the mesh passes through untouched. Buffers are kept: no allocation once they have grown.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent] // the attribute isn't inherited: EditMode tests bend the cloth too
    public sealed class ClothWave : BaseMeshEffect
    {
        /// <summary>The wave's height in the graphic's units (0 = flat).</summary>
        [System.NonSerialized] public float Amplitude;
        /// <summary>One wave's length in the graphic's units.</summary>
        [System.NonSerialized] public float Length = 400f;
        /// <summary>The wave's phase in cycles (the banner adds speed × dt every tick).</summary>
        [System.NonSerialized] public float Phase;
        /// <summary>The columns' width: narrower bends the cloth more smoothly for more vertices.</summary>
        [System.NonSerialized] public float ColumnWidth = 16f;

        readonly List<UIVertex> _in = new List<UIVertex>(64);
        readonly List<UIVertex> _out = new List<UIVertex>(1536);

        /// <summary>How many vertices the last bent mesh had (tests).</summary>
        public int LastVertexCount { get; private set; }

        /// <summary>The vertical offset at `x` (units from the cloth's left edge): A·sin(2π(x/λ − phase)).</summary>
        public static float Offset(float x, float amplitude, float length, float phase) =>
            amplitude * Mathf.Sin(2f * Mathf.PI * (x / Mathf.Max(1e-3f, length) - phase));

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Amplitude == 0f || vh.currentVertCount == 0)
            {
                LastVertexCount = vh.currentVertCount;
                return;
            }
            _in.Clear();
            vh.GetUIVertexStream(_in);
            _out.Clear();
            float left = graphic.rectTransform.rect.xMin;
            float col = Mathf.Max(2f, ColumnWidth);
            for (int q = 0; q + 5 < _in.Count; q += 6) Quad(q, left, col);
            vh.Clear();
            vh.AddUIVertexTriangleStream(_out);
            LastVertexCount = _out.Count;
        }

        /// <summary>Two triangles of an axis-aligned quad (UGUI's order: BL TL TR, TR BR BL) → columns on the grid.</summary>
        void Quad(int q, float left, float col)
        {
            var t = _in[q];
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            Vector4 uvMin = t.uv0, uvMax = t.uv0;
            for (int i = 0; i < 6; i++)
            {
                var p = _in[q + i].position;
                xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
            }
            // the UVs at the bottom-left and top-right corners
            for (int i = 0; i < 6; i++)
            {
                var v = _in[q + i];
                if (Mathf.Approximately(v.position.x, xMin) && Mathf.Approximately(v.position.y, yMin)) uvMin = v.uv0;
                if (Mathf.Approximately(v.position.x, xMax) && Mathf.Approximately(v.position.y, yMax)) uvMax = v.uv0;
            }
            float w = xMax - xMin;
            if (w <= 0f || yMax <= yMin) return;
            float a = xMin;
            while (a < xMax - 1e-3f)
            {
                // the next grid line right of a (columns share their seams across quads)
                float b = left + (Mathf.Floor((a - left) / col + 1e-4f) + 1f) * col;
                if (b > xMax) b = xMax;
                Column(t, a, b, xMin, w, yMin, yMax, uvMin, uvMax, left);
                a = b;
            }
        }

        void Column(UIVertex t, float a, float b, float xMin, float w, float yMin, float yMax, Vector4 uvMin, Vector4 uvMax, float left)
        {
            float ka = (a - xMin) / w, kb = (b - xMin) / w;
            float ua = Mathf.Lerp(uvMin.x, uvMax.x, ka), ub = Mathf.Lerp(uvMin.x, uvMax.x, kb);
            float da = Offset(a - left, Amplitude, Length, Phase), db = Offset(b - left, Amplitude, Length, Phase);
            var bl = Vert(t, a, yMin + da, ua, uvMin.y);
            var tl = Vert(t, a, yMax + da, ua, uvMax.y);
            var tr = Vert(t, b, yMax + db, ub, uvMax.y);
            var br = Vert(t, b, yMin + db, ub, uvMin.y);
            _out.Add(bl); _out.Add(tl); _out.Add(tr);
            _out.Add(tr); _out.Add(br); _out.Add(bl);
        }

        static UIVertex Vert(UIVertex t, float x, float y, float u, float v)
        {
            t.position = new Vector3(x, y, 0f);
            t.uv0 = new Vector4(u, v, t.uv0.z, t.uv0.w);
            return t;
        }
    }
}
