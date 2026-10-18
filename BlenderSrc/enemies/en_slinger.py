"""EN_Slinger — Slinger, Tier-1 ranged enemy (CLAUDE.md §6.3). Budget <= 300 tris.

Thin, upright, green pointed hood (only green figure in the pit: reads as "the shooter"), magenta scarf = accent,
a sling hanging from the right hand with a glowing orange-red stone in the pouch (the thing that will hurt you),
a stone bag on the hip. Wind-up pose whirls the right arm overhead (EnemyManager).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.38


def build():
    K.clear_scene()
    root = K.empty("EN_Slinger")
    body = K.box("Body", size=(0.34, 0.24, 0.44), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.1, 1.0),
                 swatch="cloth_green", parent=root)
    K.box("Scarf", size=(0.38, 0.28, 0.09), location=(0, 0, 0.37), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    K.box("Belt", size=(0.36, 0.26, 0.06), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("StoneBag", size=(0.12, 0.12, 0.14), location=(0.20, 0.02, -0.04), pivot=(0, 0, -1), swatch="leather",
          parent=body)
    head = K.box("Head", size=(0.32, 0.30, 0.32), location=(0, -0.01, 0.45), pivot=(0, 0, -1), swatch="skin_mid",
                 parent=body)
    # hood = frame around the face (top + sides + back) so the face shows through the opening
    top = K.box("HoodTop", size=(0.44, 0.40, 0.14), location=(0, 0.02, 0.31), pivot=(0, 0, -1), taper=(0.5, 0.6),
                swatch="cloth_green", parent=head)
    K.box("HoodTip", size=(0.10, 0.10, 0.16), location=(0, 0.08, 0.13), pivot=(0, 0, -1), taper=(0.2, 0.2),
          swatch="leaf_d", parent=top)
    for side, x in (("L", 0.19), ("R", -0.19)):
        K.box("HoodSide" + side, size=(0.06, 0.38, 0.40), location=(x, 0.03, -0.06), pivot=(0, 0, -1),
              swatch="cloth_green", parent=head)
    K.box("HoodBack", size=(0.44, 0.08, 0.40), location=(0, 0.19, -0.06), pivot=(0, 0, -1), swatch="cloth_green",
          parent=head)
    for side, x in (("L", 0.075), ("R", -0.075)):
        K.box("Eye" + side, size=(0.06, 0.03, 0.05), location=(x, -0.155, 0.17), pivot=(0, 0, -1), swatch="ink",
              parent=head)
    K.box("Mask", size=(0.30, 0.03, 0.10), location=(0, -0.155, 0.03), pivot=(0, 0, -1), swatch="cloth_sack",
          parent=head)

    arm_l = K.box("ArmL", size=(0.10, 0.10, 0.38), location=(0.22, 0, 0.40), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    arm_l.rotation_euler = (-0.2, 0, -0.1)
    arm_r = K.box("ArmR", size=(0.10, 0.10, 0.38), location=(-0.22, 0, 0.40), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    arm_r.rotation_euler = (-0.35, 0, 0.12)
    K.box("Wrap", size=(0.12, 0.12, 0.10), location=(0, 0, -0.30), pivot=(0, 0, -1), swatch="leather", parent=arm_r)
    strap = K.box("SlingStrap", size=(0.03, 0.03, 0.34), location=(0, 0, -0.38), pivot=(0, 0, 1), swatch="rope",
                  parent=arm_r)
    K.ico("SlingStone", radius=0.075, subdiv=1, location=(0, 0, -0.36), swatch="glow_hazard_red", parent=strap)

    for side, x in (("L", 0.09), ("R", -0.09)):
        leg = K.box("Leg" + side, size=(0.12, 0.13, 0.36), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="leather_l", parent=root)
        K.box("Foot" + side, size=(0.13, 0.20, 0.06), location=(0, -0.03, -0.38), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)
    return root


root = build()
info = K.review_render(root, "en_slinger", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Slinger")
info["blend"] = K.save_blend("en_slinger")
result = info
