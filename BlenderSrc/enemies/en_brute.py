"""EN_Brute — Brute, the slow elite-class Tier-1 enemy (CLAUDE.md §6.3). Budget <= 800 tris.

Twice the Convict's mass: faceted belly, huge shoulders, a small head sunk between them (topknot, yellow eyes),
long arms ending in iron-banded fists with broken shackle chains (the slam), thick legs, a chest strap, magenta loincloth =
accent. Wind-up raises both fists overhead; the slam brings them down (EnemyManager). Origin at the feet, ~1.95 m tall.
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.72


def build():
    K.clear_scene()
    root = K.empty("EN_Brute")
    body = K.box("Body", size=(0.86, 0.60, 0.66), location=(0, 0.04, HIP_Z), pivot=(0, 0, -1), taper=(1.35, 1.1),
                 swatch="skin_tan", parent=root)
    K.ico("Belly", radius=0.46, subdiv=2, location=(0, -0.10, 0.26), scale=(1.0, 0.85, 0.80), swatch="skin_mid",
          parent=body)
    K.box("Loincloth", size=(0.70, 0.56, 0.20), location=(0, 0.0, -0.10), pivot=(0, 0, -1), taper=(1.05, 1.05),
          swatch="accent_magenta", parent=body)
    K.box("Belt", size=(0.74, 0.60, 0.08), location=(0, 0.0, 0.06), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("Strap", size=(0.14, 0.64, 0.10), location=(0.30, 0.0, 0.56), pivot=(0, 0, -1), swatch="leather_d",
          parent=body)
    head = K.box("Head", size=(0.38, 0.38, 0.36), location=(0, -0.14, 0.64), pivot=(0, 0, -1), swatch="skin_tan",
                 parent=body)
    K.box("Brow", size=(0.38, 0.10, 0.07), location=(0, -0.16, 0.20), pivot=(0, 0, -1), swatch="skin_dark", parent=head)
    for side, x in (("L", 0.08), ("R", -0.08)):
        K.box("Eye" + side, size=(0.07, 0.03, 0.04), location=(x, -0.185, 0.15), pivot=(0, 0, -1),
              swatch="hazard_yellow", parent=head)
    K.box("Jaw", size=(0.30, 0.08, 0.10), location=(0, -0.17, 0.0), pivot=(0, 0, -1), swatch="skin_dark", parent=head)
    knot = K.box("Topknot", size=(0.12, 0.12, 0.16), location=(0, 0.06, 0.33), pivot=(0, 0, -1), taper=(0.6, 0.6),
                 swatch="ink", parent=head)
    knot.rotation_euler = (-0.4, 0, 0)

    for side, x, sgn in (("L", 0.56, 1), ("R", -0.56, -1)):
        K.box("Shoulder" + side, size=(0.36, 0.42, 0.30), location=(x * 0.88, 0.02, 0.50), pivot=(0, 0, -1),
              taper=(0.75, 0.8), swatch="skin_tan", parent=body)
        arm = K.box("Arm" + side, size=(0.26, 0.26, 0.80), location=(x, 0.02, 0.70), pivot=(0, 0, 1),
                    taper=(1.25, 1.2), swatch="skin_tan", parent=body)
        arm.rotation_euler = (-0.15, 0, sgn * 0.12)
        fist = K.box("Fist" + side, size=(0.36, 0.38, 0.34), location=(0, -0.02, -0.76), pivot=(0, 0, 1),
                     swatch="skin_tan", parent=arm)
        K.box("Band" + side, size=(0.40, 0.42, 0.10), location=(0, 0, 0.0), pivot=(0, 0, 1), swatch="iron",
              parent=fist)
        cuff = K.box("Cuff" + side, size=(0.30, 0.30, 0.10), location=(0, 0, -0.60), pivot=(0, 0, 0),
                     swatch="rusty_iron", parent=arm)
        for k in range(2):  # broken chain hanging from the shackle
            K.box("Link%s%d" % (side, k), size=(0.05, 0.10, 0.12), location=(sgn * 0.16, 0.0, -0.08 - k * 0.11),
                  pivot=(0, 0, 0), swatch="iron", parent=cuff)
        leg = K.box("Leg" + side, size=(0.30, 0.32, 0.67), location=(x * 0.42, 0.04, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.2, 1.1), swatch="skin_tan", parent=root)
        K.box("Wrap" + side, size=(0.32, 0.34, 0.12), location=(0, 0, -0.40), pivot=(0, 0, 0), swatch="cloth_sack",
              parent=leg)
        K.box("Foot" + side, size=(0.32, 0.42, 0.10), location=(0, -0.06, -0.72), pivot=(0, 0, -1),
              swatch="skin_dark", parent=leg)
    return root


root = build()
info = K.review_render(root, "en_brute", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Brute")
info["blend"] = K.save_blend("en_brute")
result = info
