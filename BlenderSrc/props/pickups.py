"""Pickups (CLAUDE.md §5 colour language: XP green/cyan, gold yellow). Small, chunky, emissive palette swatches.
PR_GemXP_S/M/L: octahedron gems (1 / 5 / 25 xp). PR_Coin: thick 8-sided coin with a raised rim.
Origins at the centre (they float and spin)."""
import importlib
import math

import bmesh
import gladiator_kit as K
from mathutils import Vector

importlib.reload(K)
OUT = {}


def gem(name, radius, height, swatch, core):
    bm = bmesh.new()
    top = bm.verts.new((0, 0, height / 2))
    bot = bm.verts.new((0, 0, -height / 2))
    ring = [bm.verts.new((math.cos(a) * radius, math.sin(a) * radius, 0)) for a in (i * math.pi / 2 + math.pi / 4 for i in range(4))]
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((ring[i], ring[j], top))
        bm.faces.new((ring[j], ring[i], bot))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = K.object_from_bmesh(name, bm, swatch=swatch)
    K.set_swatch_where(obj, core, lambda c, n: n.z > 0 and (n.x + n.y) > 0)  # one lit facet pair = sparkle
    return obj


def coin(name):
    # Single mesh: pickups are drawn with one instanced call per kind.
    root = K.cylinder(name, radius=0.17, depth=0.06, verts=8, pivot=(0, 0, 0), axis="Y", swatch="gold_coin")
    face = K.cylinder(name + "_Face", radius=0.12, depth=0.075, verts=8, pivot=(0, 0, 0), axis="Y", swatch="gold_l")
    return K.join([root, face], name)


K.clear_scene()
items = {
    "PR_GemXP_S": gem("PR_GemXP_S", 0.11, 0.26, "xp_green", "glow_white"),
    "PR_GemXP_M": gem("PR_GemXP_M", 0.15, 0.36, "xp_cyan", "glow_white"),
    "PR_GemXP_L": gem("PR_GemXP_L", 0.2, 0.48, "xp_blue", "glow_white"),
    "PR_Coin": coin("PR_Coin"),
}
for n, o in items.items():
    OUT[n] = K.export_fbx(o, "Props/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("pickups")
result = OUT
