"""PR_BonePile — what a Skeletal Legionnaire leaves when it falls apart for a moment (docs/ARENAS.md §6): a low heap of
crossed bones with the skull on top, still wearing its rusted helmet. Budget <= 80 tris; ~1.0 m across, 0.35 m high.
One mesh (EnemyManager draws the piles with Graphics.RenderMeshInstanced), origin at the base centre, bone-white so it
reads on the Colosseum's sand. The skull faces -Y (Unity +Z); piles get a yaw anyway.
"""
import importlib
import math

import bmesh
import bpy
from mathutils import Matrix

import gladiator_kit as K

importlib.reload(K)


def bone(name, length, loc, yaw, tilt=0.0, w=0.065, swatch="bone"):
    """A long bone: a 3-sided prism (8 tris) lying along local Y, knuckled thinner at one end."""
    b = K.cylinder(name, radius=w * 0.6, depth=length, verts=3, location=(loc[0], loc[1], loc[2] + w * 0.3),
                   pivot=(0, 0, 0), taper=0.75, swatch=swatch, axis="Y")
    b.rotation_euler = (tilt, 0.0, yaw)
    return b


def quad(name, w, h, loc, parent, swatch="ink"):
    """A flat quad facing -Y (2 tris): the skull's sockets."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in ((-w / 2, 0, -h / 2), (w / 2, 0, -h / 2), (w / 2, 0, h / 2), (-w / 2, 0, h / 2))]
    bm.faces.new(vs)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    f = bm.faces[:][0]
    if f.normal.y > 0:
        f.normal_flip()
    return K.object_from_bmesh(name, bm, loc, parent, swatch)


def build():
    K.clear_scene()
    parts = [
        bone("PR_BonePile", 1.0, (0.0, 0.02, 0.0), math.radians(28), w=0.07),       # a thigh bone across the heap
        bone("Shin", 0.88, (-0.04, -0.02, 0.0), math.radians(-48), w=0.065),
        bone("Arm", 0.7, (0.1, -0.06, 0.06), math.radians(95), tilt=0.08, w=0.06, swatch="skin_undead"),
        bone("Rib", 0.6, (-0.14, 0.12, 0.05), math.radians(160), tilt=-0.06, w=0.055),
        K.box("Pelvis", size=(0.28, 0.16, 0.08), location=(-0.04, 0.2, 0.02), pivot=(0, 0, -1), taper=(0.8, 0.9),
              swatch="skin_undead"),
    ]
    parts[-1].rotation_euler = (0.15, 0.1, 0.4)
    skull = K.box("Skull", size=(0.27, 0.27, 0.19), location=(0.02, -0.06, 0.09), pivot=(0, 0, -1), taper=(1.05, 1.0),
                  swatch="bone")
    skull.rotation_euler = (0.16, -0.18, 0.25)  # tipped over on the heap
    sockets = [quad("Socket" + s_, 0.085, 0.075, (x, -0.1375, 0.1), skull) for s_, x in (("L", 0.062), ("R", -0.062))]
    helm = K.box("Helm", size=(0.25, 0.25, 0.08), location=(0.0, 0.04, 0.155), pivot=(0, 0, -1), taper=(0.66, 0.7),
                 swatch="rusty_iron", parent=skull)
    helm.rotation_euler = (-0.12, 0.3, 0)  # askew, slipped back
    bpy.context.view_layer.update()
    for o in [helm] + sockets:
        o.parent = None
        o.matrix_world = skull.matrix_world @ o.matrix_basis
    parts += [skull, helm] + sockets
    pile = K.join(parts, "PR_BonePile")
    bpy.context.view_layer.update()
    pile.data.transform(Matrix.Translation((0, 0, 0.02)) @ pile.matrix_world)  # bake every placement; origin at
    pile.matrix_world = Matrix.Identity(4)                                       # the base centre, nothing below it
    return pile


root = build()
bpy.context.view_layer.update()
info = K.review_render(root, "pr_bonepile", size=360)
if ARGS.get("export", "1") == "1":
    info["fbx"] = K.export_fbx(root, "Props/PR_BonePile")
    info["blend"] = K.save_blend("pr_bonepile")
info["tris"] = K.tri_count([root])
info["objects"] = [o.name for o in bpy.data.objects]
result = info
