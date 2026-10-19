"""M2.7 map interactables. Front = Blender -Y (arrives as Unity +Z), origin at the base centre.
PR_ArmoryChest: "Body" + "Lid" (the lid's origin is the hinge on the back top edge, so Unity rotates it open about
local X). Dark wood, bronze bands, gold trim and a glowing gold emblem on top: treasure reads from across the arena.
PR_BloodAltar: stepped stone altar, crimson blood bowl, bone horns, red drape. One mesh."""
import importlib
import math

import bpy

import gladiator_kit as K

importlib.reload(K)
OUT = {}

HINGE = (0.0, 0.32, 0.52)


def chest(name):
    root = K.empty(name)
    body = K.box("Body", size=(1.0, 0.64, 0.52), pivot=(0, 0, -1), bevel=0.02, swatch="wood_dark", parent=root)
    parts = [body]
    for x in (-0.3, 0.3):
        parts.append(K.box("Band", size=(0.08, 0.68, 0.54), location=(x, 0, -0.01), swatch="bronze"))
    for x in (-0.47, 0.47):
        parts.append(K.box("Corner", size=(0.1, 0.68, 0.14), location=(x, 0, -0.01), swatch="gold"))
    parts.append(K.box("LockPlate", size=(0.2, 0.05, 0.22), location=(0, -0.33, 0.24), swatch="gold_l"))
    parts.append(K.box("Keyhole", size=(0.05, 0.03, 0.08), location=(0, -0.36, 0.3), swatch="iron_d"))
    K.join(parts, "Body")

    hx, hy, hz = HINGE
    lid = K.box("Lid", size=(1.04, 0.68, 0.16), location=HINGE, pivot=(0, 1, -1), bevel=0.015, swatch="wood_dark",
                parent=root)
    lparts = [lid]
    lparts.append(K.box("Roof", size=(1.04, 0.68, 0.14), location=(hx, hy - 0.34, hz + 0.16), taper=(0.94, 0.5),
                        swatch="wood"))
    for x in (-0.3, 0.3):
        lparts.append(K.box("LidBand", size=(0.085, 0.72, 0.17), location=(x, hy - 0.34, hz - 0.005), swatch="bronze"))
        lparts.append(K.box("RoofBand", size=(0.085, 0.71, 0.145), location=(x, hy - 0.34, hz + 0.155),
                            taper=(1.0, 0.5), swatch="bronze"))
    lparts.append(K.box("Clasp", size=(0.15, 0.05, 0.17), location=(hx, hy - 0.70, hz - 0.08), swatch="gold_l"))
    lparts.append(K.box("Emblem", size=(0.2, 0.2, 0.06), location=(hx, hy - 0.34, hz + 0.29), swatch="glow_gold"))
    K.join(lparts, "Lid")
    return root


def altar(name):
    root = K.empty(name)
    base = K.box(name + "_Mesh", size=(1.6, 1.15, 0.24), pivot=(0, 0, -1), bevel=0.03, swatch="stone_grey",
                 parent=root)
    parts = [base]
    parts.append(K.box("Block", size=(1.1, 0.76, 0.66), location=(0, 0, 0.24), taper=(0.9, 0.9), swatch="sandstone_d"))
    parts.append(K.box("Top", size=(1.32, 0.92, 0.14), location=(0, 0, 0.9), bevel=0.02, swatch="stone_grey"))
    parts.append(K.cylinder("Bowl", radius=0.27, depth=0.16, verts=8, location=(0, 0, 1.04), taper=1.3,
                            swatch="crimson_d"))
    parts.append(K.cylinder("Blood", radius=0.31, depth=0.03, verts=8, location=(0, 0, 1.17), swatch="blood"))
    parts.append(K.box("Drape", size=(0.52, 0.04, 0.6), location=(0, -0.4, 0.3), taper=(1.0, 1.0), swatch="cloth_red"))
    parts.append(K.box("Gem", size=(0.14, 0.05, 0.14), location=(0, -0.47, 0.92), swatch="crimson"))
    for x in (-0.56, 0.56):
        horn = K.cone("Horn", radius=0.085, depth=0.55, verts=5, location=(x, 0, 1.03), swatch="bone")
        horn.rotation_euler = (0, math.radians(-25 if x < 0 else 25), 0)
        parts.append(horn)
    bpy.context.view_layer.update()  # bake the horns' rotation into the join
    K.join(parts, name + "_Mesh")
    return root


K.clear_scene()
items = {"PR_ArmoryChest": chest("PR_ArmoryChest"), "PR_BloodAltar": altar("PR_BloodAltar")}
for n, o in items.items():
    OUT[n] = K.export_fbx(o, "Props/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("interactables")
result = OUT
