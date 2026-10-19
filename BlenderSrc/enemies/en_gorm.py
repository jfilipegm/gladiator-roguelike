"""EN_Gorm — Gorm, the Pit Master: Arena 1's boss (docs/BOSSES.md). Budget <= 2,500 tris (aim ~1,000).

A huge, hunched slaver (~3.4 m, twice the Nobody): bald, black beard, gold earring, iron slaver's collar, studded leather
apron over bare skin, magenta sash (the enemy accent), a ring of keys at the belt. Right hand (-X): chain flail — a
spiked iron ball on a chain, the silhouette nothing else in the pit has. Left hand (+X): a coiled bullwhip.
Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR) plus "Flail" (ball + chain, child of ArmR, pivot at
the hand so BossController can swing it). Origin at the feet; front = -Y (Unity +Z).
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 1.25


def build():
    K.clear_scene()
    root = K.empty("EN_Gorm")
    body = K.box("Body", size=(1.25, 0.82, 1.05), location=(0, 0.06, HIP_Z), pivot=(0, 0, -1), taper=(1.3, 1.12),
                 bevel=0.03, swatch="skin_mid", parent=root)
    body.rotation_euler = (0.18, 0, 0)  # hunched forward
    cape = K.box("Cape", size=(1.3, 0.1, 1.15), location=(0, 0.5, -0.1), pivot=(0, 0, -1), taper=(1.15, 1.0),
                 swatch="red_banner_d", parent=body)  # iteration 2: a crimson back so he isn't another brown block
    cape.rotation_euler = (-0.12, 0, 0)
    K.ico("Gut", radius=0.55, subdiv=1, location=(0, -0.16, 0.32), scale=(1.0, 0.8, 0.85), swatch="skin_tan", parent=body)
    K.box("Apron", size=(0.92, 0.1, 1.05), location=(0, -0.47, -0.45), pivot=(0, 0, -1), taper=(1.1, 1.0),
          swatch="charcoal", parent=body)
    for k in range(3):
        K.box("Stud%d" % k, size=(0.09, 0.05, 0.09), location=(-0.26 + k * 0.26, -0.53, 0.25), pivot=(0, 0, -1),
              swatch="iron", parent=body)
    K.box("Sash", size=(1.32, 0.88, 0.16), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Collar", size=(0.72, 0.66, 0.16), location=(0, -0.05, 1.0), pivot=(0, 0, -1), swatch="iron_d", parent=body)
    for k in range(4):  # the ring of keys at his right hip
        key = K.box("Key%d" % k, size=(0.04, 0.03, 0.18), location=(-0.6, -0.2 + k * 0.05, -0.06), pivot=(0, 0, 1),
                    swatch="gold", parent=body)
        key.rotation_euler = (0, 0, k * 0.4)
    K.cylinder("KeyRing", radius=0.1, depth=0.03, verts=6, location=(-0.62, -0.12, 0.02), pivot=(0, 0, 0), axis="X",
               swatch="gold_d", parent=body)

    head = K.box("Head", size=(0.62, 0.6, 0.6), location=(0, -0.34, 1.08), pivot=(0, 0, -1), bevel=0.03,
                 swatch="skin_tan", parent=body)  # iteration 2: bigger, higher, ahead of the hunch
    K.box("Brow", size=(0.58, 0.12, 0.09), location=(0, -0.27, 0.34), pivot=(0, 0, -1), swatch="skin_dark", parent=head)
    for side, x in (("L", 0.12), ("R", -0.12)):
        K.box("Eye" + side, size=(0.1, 0.03, 0.05), location=(x, -0.285, 0.27), pivot=(0, 0, -1), swatch="hazard_yellow",
              parent=head)
    K.box("Nose", size=(0.12, 0.1, 0.14), location=(0, -0.31, 0.14), pivot=(0, 0, -1), swatch="skin_mid", parent=head)
    K.box("Beard", size=(0.52, 0.22, 0.42), location=(0, -0.22, -0.2), pivot=(0, 0, -1), taper=(0.7, 0.9), swatch="ink",
          parent=head)
    K.box("Earring", size=(0.04, 0.08, 0.1), location=(-0.3, -0.02, 0.12), pivot=(0, 0, -1), swatch="gold_l", parent=head)

    for side, x, sgn in (("L", 0.78, 1), ("R", -0.78, -1)):
        K.box("Shoulder" + side, size=(0.5, 0.56, 0.32), location=(x * 0.9, 0.02, 0.78), pivot=(0, 0, -1),
              taper=(0.75, 0.8), swatch="skin_mid", parent=body)
        arm = K.box("Arm" + side, size=(0.34, 0.36, 1.1), location=(x, 0.02, 1.05), pivot=(0, 0, 1), taper=(1.2, 1.15),
                    swatch="skin_mid", parent=body)
        arm.rotation_euler = (-0.25, 0, sgn * 0.15)
        K.box("Bracer" + side, size=(0.4, 0.42, 0.3), location=(0, 0, -0.8), pivot=(0, 0, 0), swatch="leather_d", parent=arm)
        K.box("Hand" + side, size=(0.36, 0.38, 0.32), location=(0, -0.02, -1.08), pivot=(0, 0, 1), bevel=0.02,
              swatch="skin_mid", parent=arm)
        leg = K.box("Leg" + side, size=(0.44, 0.46, 1.2), location=(x * 0.45, 0.06, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.2, 1.1), swatch="red_banner_d", parent=root)
        K.box("Boot" + side, size=(0.46, 0.62, 0.3), location=(0, -0.08, -1.25), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)
        K.box("Wrap" + side, size=(0.48, 0.5, 0.12), location=(0, 0, -0.75), pivot=(0, 0, 0), swatch="cloth_sack", parent=leg)

    # right hand: the chain flail. "Flail" pivots at the grip (child of ArmR); the ball hangs 1.1 m below.
    arm_r = [o for o in root.children_recursive if o.name == "ArmR"][0]
    flail = K.box("Flail", size=(0.1, 0.1, 0.42), location=(0, -0.05, -1.25), pivot=(0, 0, 1), swatch="wood_dark",
                  parent=arm_r)
    for k in range(5):
        link = K.box("Chain%d" % k, size=(0.06, 0.12, 0.13), location=(0, 0, -0.45 - k * 0.12), pivot=(0, 0, 1),
                     swatch="iron", parent=flail)
        link.rotation_euler = (0, 0, (k % 2) * math.pi / 2)
    ball = K.ico("Ball", radius=0.32, subdiv=1, location=(0, 0, -1.35), swatch="rusty_iron", parent=flail)
    h = math.pi / 2  # cones point +Z; turn each onto its axis
    for k, (d, rot) in enumerate((((1, 0, 0), (0, h, 0)), ((-1, 0, 0), (0, -h, 0)), ((0, 1, 0), (-h, 0, 0)),
                                  ((0, -1, 0), (h, 0, 0)), ((0, 0, -1), (math.pi, 0, 0)), ((0, 0, 1), (0, 0, 0)))):
        spike = K.cone("Spike%d" % k, radius=0.09, depth=0.24, verts=4, location=(d[0] * 0.28, d[1] * 0.28, d[2] * 0.28),
                       pivot=(0, 0, -1), swatch="steel_l", parent=ball)
        spike.rotation_euler = rot

    # left hand: the bullwhip, coiled
    arm_l = [o for o in root.children_recursive if o.name == "ArmL"][0]
    whip = K.box("Whip", size=(0.08, 0.08, 0.36), location=(0, -0.05, -1.2), pivot=(0, 0, 1), swatch="leather_d",
                 parent=arm_l)
    for k in range(3):
        coil = K.cylinder("Coil%d" % k, radius=0.32 - k * 0.04, depth=0.07, verts=8, location=(0.0, 0, -0.34 - k * 0.1),
                          pivot=(0, 0, 0), axis="X", swatch="leather_l" if k % 2 else "leather", parent=whip)
        coil.scale = (1, 1, 0.7)
    return root


root = build()
info = K.review_render(root, "en_gorm", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Gorm")
info["blend"] = K.save_blend("en_gorm")
result = info
