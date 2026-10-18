using System.Collections.Generic;
using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>
    /// Static obstacles on the XZ plane (circles + oriented boxes) collected once from scene colliders.
    /// Enemies are pushed out of them each tick — no physics, no NavMesh.
    /// </summary>
    public sealed class StaticObstacles
    {
        struct Circle { public Vector2 C; public float R; }
        struct Box { public Vector2 C; public Vector2 Ax; public Vector2 Az; public Vector2 Half; }

        Circle[] _circles = new Circle[0];
        Box[] _boxes = new Box[0];

        public int Count => _circles.Length + _boxes.Length;

        /// <summary>
        /// Collect obstacles from colliders on `mask` whose top is at least `minHeight` above the ground
        /// (walls, pillars, crates). Walkable shapes (floor, hills, ramps) are left to the HeightGrid.
        /// Roundish footprints become circles, the rest oriented boxes.
        /// </summary>
        public static StaticObstacles FromScene(int mask, float minHeight, HeightGrid ground, ICollection<Collider> exclude = null)
        {
            var circles = new List<Circle>();
            var boxes = new List<Box>();
            foreach (var col in Object.FindObjectsByType<Collider>())
            {
                if (((1 << col.gameObject.layer) & mask) == 0 || col.isTrigger || !col.enabled) continue;
                if (exclude != null && exclude.Contains(col)) continue;
                var b = col.bounds;
                float groundY = ground != null ? ground.Sample(b.center.x, b.center.z) : 0f;
                if (b.max.y - groundY < minHeight) continue;
                if (b.min.y - groundY > 1.6f) continue; // overhead (e.g. a deck): enemies walk under it

                // Footprint in the collider's own XZ frame.
                var t = col.transform;
                Vector3 size;
                Vector3 center;
                if (col is BoxCollider bc) { size = Vector3.Scale(bc.size, t.lossyScale); center = t.TransformPoint(bc.center); }
                else if (col is MeshCollider mc && mc.sharedMesh != null) { size = Vector3.Scale(mc.sharedMesh.bounds.size, t.lossyScale); center = t.TransformPoint(mc.sharedMesh.bounds.center); }
                else { size = b.size; center = b.center; }
                var ax = new Vector2(t.right.x, t.right.z).normalized;
                var az = new Vector2(t.forward.x, t.forward.z).normalized;
                var half = new Vector2(Mathf.Abs(size.x) * 0.5f, Mathf.Abs(size.z) * 0.5f);
                if (half.x < 0.05f || half.y < 0.05f) continue;
                float ratio = half.x / half.y;
                if (ratio > 0.8f && ratio < 1.25f && Mathf.Max(half.x, half.y) < 1.2f)
                    circles.Add(new Circle { C = new Vector2(center.x, center.z), R = Mathf.Max(half.x, half.y) * 0.95f });
                else
                    boxes.Add(new Box { C = new Vector2(center.x, center.z), Ax = ax, Az = az, Half = half });
            }
            return new StaticObstacles { _circles = circles.ToArray(), _boxes = boxes.ToArray() };
        }

        /// <summary>Flat arrays for baking into an asset: circles (x, z, r, 0); boxes A (cx, cz, axX, axZ), B (azX, azZ, halfX, halfZ).</summary>
        public void ToArrays(out Vector4[] circles, out Vector4[] boxesA, out Vector4[] boxesB)
        {
            circles = new Vector4[_circles.Length];
            for (int i = 0; i < circles.Length; i++) circles[i] = new Vector4(_circles[i].C.x, _circles[i].C.y, _circles[i].R, 0f);
            boxesA = new Vector4[_boxes.Length];
            boxesB = new Vector4[_boxes.Length];
            for (int i = 0; i < _boxes.Length; i++)
            {
                var bx = _boxes[i];
                boxesA[i] = new Vector4(bx.C.x, bx.C.y, bx.Ax.x, bx.Ax.y);
                boxesB[i] = new Vector4(bx.Az.x, bx.Az.y, bx.Half.x, bx.Half.y);
            }
        }

        public static StaticObstacles FromArrays(Vector4[] circles, Vector4[] boxesA, Vector4[] boxesB)
        {
            var o = new StaticObstacles { _circles = new Circle[circles.Length], _boxes = new Box[boxesA.Length] };
            for (int i = 0; i < circles.Length; i++) o._circles[i] = new Circle { C = new Vector2(circles[i].x, circles[i].y), R = circles[i].z };
            for (int i = 0; i < boxesA.Length; i++)
                o._boxes[i] = new Box
                {
                    C = new Vector2(boxesA[i].x, boxesA[i].y), Ax = new Vector2(boxesA[i].z, boxesA[i].w),
                    Az = new Vector2(boxesB[i].x, boxesB[i].y), Half = new Vector2(boxesB[i].z, boxesB[i].w),
                };
            return o;
        }

        /// <summary>True if a circle of `radius` at p (XZ) overlaps any obstacle (projectiles hitting walls/pillars).</summary>
        public bool Overlaps(Vector2 p, float radius)
        {
            for (int i = 0; i < _circles.Length; i++)
            {
                float r = _circles[i].R + radius;
                if ((p - _circles[i].C).sqrMagnitude < r * r) return true;
            }
            for (int i = 0; i < _boxes.Length; i++)
            {
                var b = _boxes[i];
                var d = p - b.C;
                if (Mathf.Abs(Vector2.Dot(d, b.Ax)) < b.Half.x + radius && Mathf.Abs(Vector2.Dot(d, b.Az)) < b.Half.y + radius) return true;
            }
            return false;
        }

        /// <summary>Push a circle of `radius` at pos out of every obstacle. Returns true if moved.</summary>
        public bool PushOut(ref Vector3 pos, float radius)
        {
            bool moved = false;
            var p = new Vector2(pos.x, pos.z);
            for (int i = 0; i < _circles.Length; i++)
            {
                var c = _circles[i];
                var d = p - c.C;
                float r = c.R + radius;
                float d2 = d.sqrMagnitude;
                if (d2 >= r * r) continue;
                float len = Mathf.Sqrt(d2);
                p = len > 1e-4f ? c.C + d / len * r : c.C + new Vector2(r, 0f);
                moved = true;
            }
            for (int i = 0; i < _boxes.Length; i++)
            {
                var b = _boxes[i];
                var d = p - b.C;
                float lx = Vector2.Dot(d, b.Ax), lz = Vector2.Dot(d, b.Az);
                float ex = b.Half.x + radius, ez = b.Half.y + radius;
                if (lx <= -ex || lx >= ex || lz <= -ez || lz >= ez) continue;
                // Shortest exit along the local axes.
                float px = ex - Mathf.Abs(lx), pz = ez - Mathf.Abs(lz);
                if (px < pz) lx = Mathf.Sign(lx == 0 ? 1 : lx) * ex; else lz = Mathf.Sign(lz == 0 ? 1 : lz) * ez;
                p = b.C + b.Ax * lx + b.Az * lz;
                moved = true;
            }
            pos.x = p.x;
            pos.z = p.y;
            return moved;
        }
    }
}
