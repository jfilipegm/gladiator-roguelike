"""EN_Convict — Starving Convict, the Tier-1 melee swarmer (CLAUDE.md §6.3). Budget <= 300 tris.

Hunched, thin, sack cloth, broken shackles. Magenta rag = enemy accent patch so it reads against the dirt floor (§7.2).
Same rigid-part layout as the player (Body/Head/ArmL/ArmR/LegL/LegR) so one PartAnimator profile drives both.
No bevels (tri budget), no weapon (claws at you).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.36


def build():
    K.clear_scene()
    root = K.empty("EN_Convict")
    body = K.box("Body", size=(0.36, 0.26, 0.46), location=(0, 0.02, HIP_Z), pivot=(0, 0, -1), taper=(1.15, 1.0),
                 swatch="cloth_sack", parent=root)
    body.rotation_euler = (0.30, 0, 0)  # hunched forward (rotate about X toward -Y)
    K.box("Rag", size=(0.38, 0.28, 0.10), location=(0, 0, 0.30), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    head = K.box("Head", size=(0.40, 0.38, 0.42), location=(0, -0.03, 0.46), pivot=(0, 0, -1), swatch="skin_undead",
                 parent=body)
    K.box("Hair", size=(0.42, 0.40, 0.10), location=(0, 0.02, 0.36), pivot=(0, 0, -1), taper=(0.7, 0.8),
          swatch="charcoal", parent=head)
    for side, x in (("L", 0.09), ("R", -0.09)):
        K.box("Eye" + side, size=(0.07, 0.03, 0.05), location=(x, -0.195, 0.22), pivot=(0, 0, -1),
              swatch="hazard_yellow", parent=head)
    K.box("Mouth", size=(0.16, 0.03, 0.04), location=(0, -0.195, 0.08), pivot=(0, 0, -1), swatch="ink", parent=head)
    for side, x in (("L", 0.23), ("R", -0.23)):
        arm = K.box("Arm" + side, size=(0.10, 0.10, 0.40), location=(x, 0, 0.40), pivot=(0, 0, 1),
                    swatch="skin_undead", parent=body)
        arm.rotation_euler = (-0.5, 0, 0)  # reaching forward
        shackle = K.box("Shackle" + side, size=(0.14, 0.14, 0.07), location=(0, 0, -0.30), pivot=(0, 0, 0),
                        swatch="iron", parent=arm)
        for k in range(2):  # M3.3: a broken chain dangling from each shackle (an escaped convict)
            K.box("Link%s%d" % (side, k), size=(0.035, 0.07, 0.08), location=(0, 0.03, -0.07 - k * 0.075),
                  pivot=(0, 0, 0), swatch="iron_d", parent=shackle)
        leg = K.box("Leg" + side, size=(0.12, 0.13, 0.34), location=(x * 0.45, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="skin_undead", parent=root)
        K.box("Foot" + side, size=(0.13, 0.20, 0.06), location=(0, -0.03, -0.36), pivot=(0, 0, -1), swatch="skin_tan",
              parent=leg)
    K.box("Loincloth", size=(0.30, 0.24, 0.14), location=(0, 0, 0.02), pivot=(0, 0, 1), swatch="leather",
          parent=body)
    return root


root = build()
info = K.review_render(root, "en_convict", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Convict")
info["blend"] = K.save_blend("en_convict")
result = info
