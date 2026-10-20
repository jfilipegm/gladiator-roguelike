"""Phase 5.6 secrets (docs/UNLOCKS.md): PR_GoldenRat (the Slave Pit's runaway treasure) and CH_Bookmaker (the Provincial
Pit's shady bettor). Single meshes (no rig): GoldenRat.cs bobs and turns the rat, Bookmaker.cs turns him to face you.
Gold reads as treasure; neither uses the orange-red "hurts you" row. Front = Blender -Y (Unity +Z), origin at the base.
    Tools/bheadless BlenderSrc/props/secrets.py [review=1]
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)
OUT = {}


def golden_rat(name):
    body = K.ico(name, radius=0.17, subdiv=1, location=(0, 0.02, 0.17), scale=(0.85, 1.45, 0.8), swatch="gold")
    head = K.cone(name + "_Head", radius=0.11, depth=0.2, verts=6, location=(0, -0.2, 0.19), pivot=(0, 0, -1),
                  axis="Y", swatch="gold_l")
    head.rotation_euler = (math.radians(180), 0, 0)  # the cone's point forward (-Y)
    nose = K.ico(name + "_Nose", radius=0.03, subdiv=1, location=(0, -0.4, 0.18), swatch="ink")
    parts = [body, head, nose]
    for side in (1, -1):
        ear = K.ico(name + "_Ear%d" % side, radius=0.06, subdiv=1, location=(0.07 * side, -0.2, 0.3),
                    scale=(1, 0.35, 1.1), swatch="gold_d")
        eye = K.box(name + "_Eye%d" % side, size=(0.025, 0.025, 0.025), location=(0.055 * side, -0.29, 0.23), pivot=(0, 0, 0),
                    swatch="ink")
        parts += [ear, eye]
        for fz in (-0.12, 0.13):  # little legs
            parts.append(K.box(name + "_Leg%d_%d" % (side, int(fz * 100)), size=(0.05, 0.06, 0.08),
                               location=(0.09 * side, fz, 0.0), pivot=(0, 0, -1), swatch="gold_d"))
    # the tail (review iteration 2: the segments floated apart): each centred on its stretch of one curling chain
    y, z, a, seg_len = 0.22, 0.13, 15.0, 0.15
    for k in range(4):
        dy, dz = seg_len * math.cos(math.radians(a)), seg_len * math.sin(math.radians(a))
        seg = K.cylinder(name + "_Tail%d" % k, radius=0.024 - k * 0.004, depth=seg_len + 0.02, verts=4,
                         location=(0, y + dy / 2, z + dz / 2), pivot=(0, 0, 0), axis="Y", swatch="gold_d")
        seg.rotation_euler = (math.radians(a), 0, 0)
        parts.append(seg)
        y, z, a = y + dy, z + dz, a + 22.0
    parts.append(K.ico(name + "_Glint", radius=0.035, subdiv=1, location=(0.05, 0.0, 0.31), swatch="glow_white"))
    return K.join(parts, name)


def bookmaker(name):
    parts = []
    for side in (1, -1):
        parts.append(K.box(name + "_Leg%d" % side, size=(0.12, 0.14, 0.32), location=(0.1 * side, 0, 0), pivot=(0, 0, -1),
                           swatch="leather_d"))
    robe = K.cylinder(name, radius=0.3, depth=0.62, verts=8, location=(0, 0.04, 0.28), pivot=(0, 0, -1), taper=0.72,
                      swatch="cloth_green")
    belly = K.ico(name + "_Belly", radius=0.27, subdiv=1, location=(0, -0.05, 0.5), scale=(1.05, 0.95, 0.9), swatch="cloth_green")
    sash = K.cylinder(name + "_Sash", radius=0.29, depth=0.06, verts=8, location=(0, -0.03, 0.55), pivot=(0, 0, 0),
                      swatch="gold")
    head = K.ico(name + "_Head", radius=0.2, subdiv=1, location=(0, -0.02, 1.08), scale=(1, 1, 0.95), swatch="skin_tan")
    nose = K.cone(name + "_Nose", radius=0.06, depth=0.13, verts=5, location=(0, -0.2, 1.06), pivot=(0, 0, -1), axis="Y",
                  swatch="skin_mid")
    nose.rotation_euler = (math.radians(180), 0, 0)
    brim = K.cylinder(name + "_Brim", radius=0.34, depth=0.03, verts=8, location=(0, -0.02, 1.2), pivot=(0, 0, 0),
                      swatch="leather")
    crown = K.cylinder(name + "_Crown", radius=0.16, depth=0.16, verts=8, location=(0, -0.02, 1.2), pivot=(0, 0, -1),
                       taper=0.6, swatch="leather_d")
    parts += [robe, belly, sash, head, nose, brim, crown]
    for side in (1, -1):
        parts.append(K.box(name + "_Eye%d" % side, size=(0.04, 0.02, 0.03), location=(0.07 * side, -0.18, 1.12),
                           pivot=(0, 0, 0), swatch="ink"))
    # the ledger under his left arm, a quill in his right hand, a fat coin pouch on the sash
    arm_l = K.cylinder(name + "_ArmL", radius=0.07, depth=0.36, verts=6, location=(0.3, -0.02, 0.58), pivot=(0, 0, -1),
                       swatch="cloth_green")
    arm_l.rotation_euler = (math.radians(-20), math.radians(-15), 0)
    ledger = K.box(name + "_Ledger", size=(0.08, 0.3, 0.38), location=(0.36, -0.12, 0.62), pivot=(0, 0, 0), swatch="wood_dark")
    pages = K.box(name + "_Pages", size=(0.085, 0.27, 0.34), location=(0.36, -0.12, 0.62), pivot=(0, 0, 0), swatch="bone")
    arm_r = K.cylinder(name + "_ArmR", radius=0.07, depth=0.34, verts=6, location=(-0.3, -0.04, 0.6), pivot=(0, 0, -1),
                       swatch="cloth_green")
    arm_r.rotation_euler = (math.radians(-60), math.radians(20), 0)
    hand = K.ico(name + "_Hand", radius=0.06, subdiv=1, location=(-0.36, -0.3, 0.8), swatch="skin_tan")
    quill = K.cone(name + "_Quill", radius=0.035, depth=0.3, verts=4, location=(-0.36, -0.3, 0.8), pivot=(0, 0, -1),
                   swatch="player_white")
    quill.rotation_euler = (math.radians(25), math.radians(-20), 0)
    pouch = K.ico(name + "_Pouch", radius=0.09, subdiv=1, location=(-0.2, -0.24, 0.45), scale=(1, 1, 1.2), swatch="gold_coin")
    parts += [arm_l, ledger, pages, arm_r, hand, quill, pouch]
    return K.join(parts, name)


K.clear_scene()
items = {"PR_GoldenRat": golden_rat("PR_GoldenRat"), "CH_Bookmaker": bookmaker("CH_Bookmaker")}
review = ARGS.get("review") == "1" if "ARGS" in globals() else False
for n, o in items.items():
    if review:
        OUT[n + "_review"] = K.review_render(o, "sec_" + n, size=300)
    OUT[n] = K.export_fbx(o, ("Props/" if n.startswith("PR_") else "Characters/") + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("secrets")
result = OUT
