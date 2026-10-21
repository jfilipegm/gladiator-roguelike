"""P1 (arena integrity) dev check: places exported FBX pieces the way ArenaBuilder would (Unity positions, yaws and
scales from a JSON list) and draws a boundary loop as a thin red ribbon, then renders oblique views. Lets a worker
without a Unity Editor see whether pieces, pens and the player's loop line up. Read-only: nothing is exported.

ARGS: spec=<path to JSON {"pieces": [{"model", "x", "y", "z", "yaw", "sx"}], "loop": [[x, z], ...],
"cams": [{"from": [x, y, z], "to": [x, y, z], "lens": mm}]}> out=<name> (Screenshots/blender/<name>_<k>.png).
"""
import json
import math
import os

import bmesh
import bpy
import gladiator_kit as K
from mathutils import Matrix, Vector

spec = json.load(open(ARGS["spec"]))  # noqa: F821
out = ARGS.get("out", "arena_preview")  # noqa: F821
MODELS = os.path.join(REPO, "Assets", "Art", "Models")  # noqa: F821

K.clear_scene()
mat = K.palette_material()
cache = {}


def place(p):
    """One piece: its FBX imported once, then linked copies; Unity (x, y, z), yaw (deg, clockwise from above) and
    local x scale map to Blender (x, z, y), a -yaw turn about Z and an x scale under it."""
    model = p["model"]
    if model not in cache:
        before = set(bpy.context.scene.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, model + ".fbx"), axis_forward="Y", axis_up="Z")
        new = [o for o in bpy.context.scene.objects if o not in before]
        for o in new:
            if o.type == "MESH":
                o.data.materials.clear()
                o.data.materials.append(mat)
        roots = [o for o in new if o.parent is None]
        holder = bpy.data.objects.new("Src_" + model, None)
        bpy.context.scene.collection.objects.link(holder)
        for r in roots:
            r.parent = holder
        holder.hide_render = True
        for o in new:
            o.hide_render = True
        cache[model] = holder
    src = cache[model]
    anchor = bpy.data.objects.new("At_" + model, None)
    bpy.context.scene.collection.objects.link(anchor)
    anchor.matrix_world = (Matrix.Translation((p["x"], p["z"], p["y"])) @ Matrix.Rotation(-math.radians(p["yaw"]), 4, "Z")
                           @ Matrix.Diagonal((p.get("sx", 1.0), 1.0, 1.0, 1.0)))

    def copy(o, parent):
        c = o.copy()
        bpy.context.scene.collection.objects.link(c)
        c.hide_render = False
        c.parent = parent
        c.matrix_parent_inverse = o.matrix_parent_inverse.copy()
        for ch in o.children:
            copy(ch, c)
        return c

    for r in src.children:
        copy(r, anchor)


for p in spec["pieces"]:
    place(p)

# the loop: a 2 m red ribbon standing on it
loop = spec.get("loop", [])
if loop:
    bm = bmesh.new()
    n = len(loop)
    bottom = [bm.verts.new((x, z, 0.02)) for x, z in loop]
    top = [bm.verts.new((x, z, 2.0)) for x, z in loop]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    ribbon = K.object_from_bmesh("Loop", bm, swatch="hazard_red")

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.light = "STUDIO"
shading.color_type = "TEXTURE"
shading.show_shadows = False
world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.color = (0.55, 0.65, 0.75)
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
result = {"pieces": len(spec["pieces"]), "shots": []}
for k, c in enumerate(spec["cams"]):
    cd = bpy.data.cameras.new("Cam%d" % k)
    cd.lens = c.get("lens", 28)
    cd.clip_end = 2000
    cam = bpy.data.objects.new("Cam%d" % k, cd)
    scene.collection.objects.link(cam)
    f, t = c["from"], c["to"]
    cam.location = Vector((f[0], f[2], f[1]))
    cam.rotation_euler = (Vector((t[0], t[2], t[1])) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    path = os.path.join(REPO, "Screenshots", "blender", "%s_%d.png" % (out, k))  # noqa: F821
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    result["shots"].append(path)
