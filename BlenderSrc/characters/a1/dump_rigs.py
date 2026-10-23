"""A1 in-game: dump the original characters' rigid-part rigs (what the A1 exports must match).

For every original .blend (ch_*, en_*, secrets.blend's CH_Bookmaker): the root, every named part the game animates
(Body, Head, ArmL, ArmR, LegL, LegR) with its parent and world pivot, the other direct children of each part (held
weapons and accessories: name only), the root's other children, and the model's height. Writes
docs/tracks/A1-rigs.json. Run: Tools/bheadless BlenderSrc/characters/a1/dump_rigs.py
"""
import json
import os

import bpy

import gladiator_kit as K

PARTS = ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR")


def base(o):
    return o.name.split(".")[0]


def load(path):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = list(src.objects)
    objs = [o for o in dst.objects if o is not None]
    for o in objs:
        bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.update()
    return objs


out = {}
for fn in sorted(os.listdir(K.BLEND_DIR)):
    if not fn.endswith(".blend") or not (fn.startswith("ch_") or fn.startswith("en_") or fn == "secrets.blend"):
        continue
    if "_v" in fn[:-6].split("_")[-1] or fn.startswith("ch_nobody_"):
        continue
    objs = load(os.path.join(K.BLEND_DIR, fn))
    roots = [o for o in objs if o.parent is None and (base(o).startswith("CH_") or base(o).startswith("EN_"))]
    if fn == "secrets.blend":
        roots = [o for o in roots if base(o) == "CH_Bookmaker"]
    for root in roots:
        desc = [root] + list(root.children_recursive)
        meshes = [o for o in desc if o.type == "MESH"]
        lo, hi = K.world_bounds(meshes)
        rec = {"blend": fn, "root": base(root), "root_type": root.type, "height": round(hi.z - lo.z, 3),
               "bounds_x": [round(lo.x, 3), round(hi.x, 3)], "bounds_y": [round(lo.y, 3), round(hi.y, 3)],
               "renderers": len(meshes), "tris": K.tri_count(meshes), "parts": {},
               "root_children": [base(c) for c in root.children if base(c) not in PARTS]}
        for o in desc:
            b = base(o)
            if b in PARTS:
                w = o.matrix_world.translation
                rec["parts"][b] = {"parent": base(o.parent) if o.parent else None, "type": o.type,
                                   "pivot": [round(w.x, 3), round(w.y, 3), round(w.z, 3)],
                                   "rot": [round(a, 3) for a in o.matrix_world.to_euler()],
                                   "children": [base(c) for c in o.children if base(c) not in PARTS]}
        out[base(root)] = rec
path = os.path.join(K.REPO, "docs", "tracks", "A1-rigs.json")
with open(path, "w") as f:
    json.dump(out, f, indent=1)
result = {"rigs": len(out), "path": path, "names": sorted(out)}
