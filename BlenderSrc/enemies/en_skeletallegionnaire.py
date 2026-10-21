"""EN_SkeletalLegionnaire — a dead legionary risen from the hypogeum: the Colosseum's undead swarmer (docs/ARENAS.md §6).
Budget <= 300 tris.

~1.7 m and chibi: a big bone skull with black sockets and a gaping jaw under a dented, tilted rusty helmet whose crest
is a broken stump; rusted segmented plates on the chest and shoulders; nothing below them but a bare spine down to a
magenta sash (the enemy accent) and a rotted crimson rag; stick-thin bone arms and legs. A notched gladius held
forward in the right hand. Undead at 64 px: the stick limbs, the gap at the waist where only the spine is, and the
big round skull. Rigid parts on the shared names like the Convict (Body/Head/ArmL/ArmR/LegL/LegR; Body is the sash
at the hip); front = -Y (Unity +Z).
"""
import importlib
import math

import bpy
from mathutils import Euler, Matrix, Vector

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 0.58


def place(obj, loc, rot=(0.0, 0.0, 0.0)):
    """Set obj's WORLD transform (its parent may be rotated)."""
    bpy.context.view_layer.update()
    obj.matrix_world = Matrix.Translation(Vector(loc)) @ Euler(rot).to_matrix().to_4x4()
    bpy.context.view_layer.update()
    return obj


def build():
    K.clear_scene()
    root = K.empty("EN_SkeletalLegionnaire")
    # Body = the magenta sash round the bare pelvis; a rotted rag hangs from it
    body = K.box("Body", size=(0.3, 0.2, 0.08), location=(0, 0.02, HIP_Z), pivot=(0, 0, -1), swatch="accent_magenta",
                 parent=root)
    body.rotation_euler = (0.16, 0, 0)  # a shambling hunch
    rag = K.box("Rag", size=(0.1, 0.04, 0.27), location=(0.04, -0.11, 0.06), pivot=(0, 0, 1), taper=(1.7, 1.0),
                swatch="crimson_d", parent=body)
    rag.rotation_euler = (0.08, 0, 0.12)
    K.box("Spine", size=(0.06, 0.06, 0.26), location=(0, 0.02, 0.06), pivot=(0, 0, -1), swatch="skin_undead",
          parent=body)
    # the rusted segmented plates: the chest, one band of bronze, the shoulder guards
    chest = K.box("Plates", size=(0.34, 0.24, 0.3), location=(0, 0, 0.27), pivot=(0, 0, -1), taper=(1.25, 1.12),
                  swatch="rusty_iron", parent=body)
    K.box("Band", size=(0.38, 0.27, 0.06), location=(0, 0, 0.1), pivot=(0, 0, -1), swatch="bronze_d", parent=chest)
    K.box("Shoulders", size=(0.56, 0.26, 0.07), location=(0, 0.0, 0.26), pivot=(0, 0, -1), taper=(0.8, 0.9),
          swatch="rusty_iron", parent=chest)

    # head: a big skull (a wide cranium over a narrower face with a nose hole), black sockets, a jaw hanging open,
    # under a dented dome helmet knocked askew, its crest a broken stump. The Head's origin is the neck joint.
    head = K.box("Head", size=(0.4, 0.38, 0.27), location=(0, -0.01, 0.62), pivot=(0, 0, -1.74), taper=(0.9, 0.9),
                 swatch="bone", parent=body)  # the cranium, 0.1-0.37 m above the neck
    head.rotation_euler = (0.12, 0, 0)  # pushed forward
    K.box("Face", size=(0.29, 0.28, 0.13), location=(0, -0.04, 0.0), pivot=(0, 0, -1), taper=(1.1, 1.0),
          swatch="bone", parent=head)
    for side, x in (("L", 0.085), ("R", -0.085)):
        K.box("Socket" + side, size=(0.115, 0.03, 0.1), location=(x, -0.185, 0.11), pivot=(0, 0, -1), swatch="ink",
              parent=head)
    K.box("Nose", size=(0.05, 0.03, 0.06), location=(0, -0.185, 0.04), pivot=(0, 0, -1), taper=(0.3, 1.0),
          swatch="ink", parent=head)
    jaw = K.box("Jaw", size=(0.24, 0.22, 0.06), location=(0, -0.05, 0.0), pivot=(0, 0, 1), swatch="skin_undead",
                parent=head)
    jaw.rotation_euler = (-0.38, 0, 0)  # hanging open
    helm = K.box("Helm", size=(0.44, 0.45, 0.14), location=(0, 0.02, 0.29), pivot=(0, 0, -1), taper=(0.7, 0.72),
                 swatch="rusty_iron", parent=head)
    helm.rotation_euler = (0.06, 0.16, 0)  # knocked askew
    K.box("Stump", size=(0.05, 0.14, 0.11), location=(0, 0.04, 0.12), pivot=(0, 0, -1), taper=(1.0, 0.6),
          swatch="bronze_d", parent=helm)

    # arms: stick bones; the right one holds a notched gladius forward, the left one reaches
    for side, x in (("L", 0.25), ("R", -0.25)):
        arm = K.box("Arm" + side, size=(0.055, 0.055, 0.5), location=(x, 0.0, 0.5), pivot=(0, 0, 1),
                    taper=(1.3, 1.3), swatch="bone", parent=body)
        arm.rotation_euler = (-0.55 if side == "R" else -0.35, 0, 0.12 if side == "L" else -0.12)
        if side == "R":
            K.box("Hand" + side, size=(0.09, 0.1, 0.1), location=(0, -0.01, -0.5), pivot=(0, 0, 0), swatch="bone",
                  parent=arm)
        leg = K.box("Leg" + side, size=(0.06, 0.06, HIP_Z - 0.05), location=(x * 0.5, 0.0, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.3, 1.3), swatch="bone", parent=root)
        K.box("Foot" + side, size=(0.09, 0.2, 0.05), location=(0, -0.04, -(HIP_Z - 0.05)), pivot=(0, 0, 1),
              swatch="skin_undead", parent=leg)

    # the gladius: a notched blade held forward from the right fist (part of ArmR)
    arm_r = bpy.data.objects["ArmR"]
    bpy.context.view_layer.update()
    fist = arm_r.matrix_world @ Vector((0, -0.01, -0.5))
    hilt = K.box("Hilt", size=(0.05, 0.16, 0.05), pivot=(0, 0, 0), swatch="bronze_d", parent=arm_r)
    place(hilt, fist, (0.35, 0, 0))
    blade = [(0.0, 0.03), (-0.2, 0.034), (-0.24, 0.01), (-0.27, 0.034), (-0.44, 0.0), (-0.36, -0.03), (0.0, -0.03)]
    k = K.extrude_poly("Gladius", blade, thickness=0.025, plane="YZ", swatch="steel", parent=hilt)
    k.location = (0, -0.06, 0)
    return root


root = build()
bpy.context.view_layer.update()
PARTS = {o.name: [round(-o.matrix_world.translation.x, 3), round(o.matrix_world.translation.z, 3),
                  round(-o.matrix_world.translation.y, 3)]
         for o in K.descendants(root) if o.name in ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR")}
info = K.review_render(root, "en_skeletallegionnaire", size=420)
if ARGS.get("export", "1") == "1":
    info["fbx"] = K.export_fbx(root, "Enemies/EN_SkeletalLegionnaire")
    info["blend"] = K.save_blend("en_skeletallegionnaire")
info["tris"] = K.tri_count(K.descendants(root))
info["parts_unity"] = PARTS
result = info
