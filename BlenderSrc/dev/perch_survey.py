"""P1 (arena integrity): surveys exported models for surfaces a player could stand on (a perch audit helper).

Imports each FBX under Assets/Art/Models/ and reports, in the model's Unity space (x right, y up, z forward; origin as
placed), its bounds and every standable patch above `min_h` metres: faces whose normal is within the player's slope
limit (50 deg) of up, grouped by height (10 cm bins), with their area and XZ extent. Read-only: nothing is exported.

ARGS: models=Environment/SM_YardPlatform,Props/PR_Crate,... (paths under Assets/Art/Models without .fbx), or
dir=Environment (every FBX in that folder); min_h=0.9; slope=50.
"""
import math
import os

import bpy

MODELS = os.path.join(REPO, "Assets", "Art", "Models")  # noqa: F821  (REPO injected by the runner)
min_h = float(ARGS.get("min_h", "0.9"))  # noqa: F821
cos_lim = math.cos(math.radians(float(ARGS.get("slope", "50"))))  # noqa: F821
names = [m for m in ARGS.get("models", "").split(",") if m]  # noqa: F821
if ARGS.get("dir"):  # noqa: F821
    d = os.path.join(MODELS, ARGS["dir"])  # noqa: F821
    names += [ARGS["dir"] + "/" + f[:-4] for f in sorted(os.listdir(d)) if f.endswith(".fbx")]  # noqa: F821


def survey(rel):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, rel + ".fbx"), axis_forward="Y", axis_up="Z")
    bpy.context.view_layer.update()
    lo = [1e9] * 3
    hi = [-1e9] * 3
    bins = {}
    tris = 0
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        m = o.matrix_world
        rot = m.to_3x3()
        for v in o.data.vertices:
            w = m @ v.co
            u = (w.x, w.z, w.y)  # the export's 180 deg root yaw comes back on import: Blender (x, y) is Unity (x, z)
            for k in range(3):
                lo[k] = min(lo[k], u[k])
                hi[k] = max(hi[k], u[k])
        for p in o.data.polygons:
            tris += len(p.vertices) - 2
            n = (rot @ p.normal).normalized()
            c = m @ p.center
            if n.z < cos_lim or c.z < min_h:
                continue
            area = p.area * abs(m.determinant()) ** (2.0 / 3.0)
            key = round(c.z, 1)
            b = bins.setdefault(key, [0.0, 1e9, -1e9, 1e9, -1e9, 0])
            b[0] += area
            for vi in p.vertices:
                w = m @ o.data.vertices[vi].co
                b[1] = min(b[1], w.x); b[2] = max(b[2], w.x); b[3] = min(b[3], w.y); b[4] = max(b[4], w.y)
            b[5] = max(b[5], round(math.degrees(math.acos(max(-1.0, min(1.0, n.z))))))
    tops = [dict(h=h, area=round(b[0], 2), x=(round(b[1], 2), round(b[2], 2)), z=(round(b[3], 2), round(b[4], 2)), maxdeg=b[5])
            for h, b in sorted(bins.items()) if b[0] >= 0.02]
    # the FBX root node's own offset: K.export_fbx yaws the root 180 deg about its origin, so a root whose origin isn't
    # the world origin arrives displaced by twice its (x, y) offset (Unity: (x, z))
    roots = [o for o in bpy.context.scene.objects if o.parent is None]
    root = [round(roots[0].location.x, 2), round(roots[0].location.y, 2)] if len(roots) == 1 else None
    return dict(min=[round(v, 2) for v in lo], max=[round(v, 2) for v in hi], tris=tris, tops=tops, root=root)


result = {}
for rel in names:
    try:
        result[rel] = survey(rel)
    except Exception as e:  # a missing model: say so, keep going
        result[rel] = dict(error=str(e))
