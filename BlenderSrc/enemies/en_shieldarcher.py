"""EN_ShieldArcher — Shield Archer, Tier-2 ranged blocker for the Provincial Pit (docs/ARENAS.md). Budget <= 300 tris.

A blue-tunic archer in an iron cap behind a tall wicker pavise held on the left arm, its top at his chin so the cap peeks over: from
the front the shield IS the silhouette, so the player reads "flank this one". The magenta boss on the shield is the enemy accent
patch. The bow is carried flat in the right hand (limbs along the walk direction); the aim pose swings that arm
forward 90°, which stands the bow upright with its glowing orange-red arrow pointing at you (EnemyManager).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.40


def build():
    K.clear_scene()
    root = K.empty("EN_ShieldArcher")
    body = K.box("Body", size=(0.38, 0.26, 0.44), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.1, 1.0),
                 swatch="cloth_blue", parent=root)
    K.box("Belt", size=(0.40, 0.28, 0.06), location=(0, 0, 0.03), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    head = K.box("Head", size=(0.32, 0.30, 0.32), location=(0, -0.01, 0.45), pivot=(0, 0, -1), swatch="skin_light",
                 parent=body)
    cap = K.box("Cap", size=(0.38, 0.36, 0.16), location=(0, 0.01, 0.22), pivot=(0, 0, -1), taper=(0.7, 0.7),
                swatch="iron", parent=head)
    K.box("Brim", size=(0.42, 0.40, 0.04), location=(0, 0, -0.02), pivot=(0, 0, -1), swatch="iron_d", parent=cap)
    K.box("Eyes", size=(0.22, 0.03, 0.05), location=(0, -0.155, 0.14), pivot=(0, 0, -1), swatch="ink", parent=head)

    # shield arm (left = +X) straight down; the pavise stands in front of the whole body, base near the ground
    arm_l = K.box("ArmL", size=(0.11, 0.11, 0.36), location=(0.24, 0, 0.40), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    shield = K.box("Shield", size=(0.66, 0.07, 1.10), location=(-0.16, -0.27, -0.78), pivot=(0, 0, -1),
                   swatch="straw", parent=arm_l)
    for k, z in enumerate((0.18, 0.52, 0.88)):  # woven bands
        K.box("Band%d" % k, size=(0.68, 0.08, 0.06), location=(0, 0, z), pivot=(0, 0, -1), swatch="wood", parent=shield)
    K.box("Boss", size=(0.18, 0.06, 0.22), location=(0, -0.05, 0.62), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=shield)

    # bow arm (right = -X): the bow lies along Y (front-back) in the hand, so the aim pose stands it upright
    arm_r = K.box("ArmR", size=(0.11, 0.11, 0.36), location=(-0.24, 0, 0.40), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    grip = K.box("Grip", size=(0.07, 0.10, 0.07), location=(0, 0, -0.38), pivot=(0, 0, 0), swatch="leather",
                 parent=arm_r)
    for name, y, rx in (("LimbF", -0.24, -0.35), ("LimbB", 0.24, 0.35)):
        limb = K.box(name, size=(0.04, 0.40, 0.04), location=(0, y, 0.06), pivot=(0, 0, 0), swatch="wood_dark",
                     parent=grip)
        limb.rotation_euler = (rx, 0, 0)
    K.box("String", size=(0.015, 0.84, 0.015), location=(0, 0, -0.10), pivot=(0, 0, 0), swatch="bone", parent=grip)
    K.box("Arrowhead", size=(0.05, 0.10, 0.05), location=(0, -0.44, -0.02), pivot=(0, 0, 0), taper=(0.2, 0.2),
          swatch="glow_hazard_red", parent=grip)

    for side, x in (("L", 0.09), ("R", -0.09)):
        leg = K.box("Leg" + side, size=(0.13, 0.14, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="leather_l", parent=root)
        K.box("Foot" + side, size=(0.14, 0.21, 0.06), location=(0, -0.03, -0.40), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)
    return root


root = build()
info = K.review_render(root, "en_shieldarcher", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_ShieldArcher")
info["blend"] = K.save_blend("en_shieldarcher")
result = info
