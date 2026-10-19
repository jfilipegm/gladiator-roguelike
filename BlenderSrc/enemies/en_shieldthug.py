"""EN_ShieldThug — Shield Thug, Tier-1 frontal blocker (CLAUDE.md §6.3). Budget <= 300 tris.

Stocky brawler in leather with an iron pot helmet, a big wooden door-shield on the left arm held in front, and a club.
The shield IS the gameplay (hits from the front are blocked) so it dominates the front silhouette; its magenta boss is
the enemy accent patch. Shield arm swings very little in the run cycle (EnemyManager).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.42


def build():
    K.clear_scene()
    root = K.empty("EN_ShieldThug")
    body = K.box("Body", size=(0.56, 0.38, 0.52), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.12, 1.0),
                 swatch="leather_d", parent=root)  # M3.3: darker body so the light shield stands out
    K.box("Belt", size=(0.58, 0.40, 0.08), location=(0, 0, 0.03), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    head = K.box("Head", size=(0.40, 0.38, 0.36), location=(0, -0.02, 0.52), pivot=(0, 0, -1), swatch="skin_mid",
                 parent=body)
    helmet = K.box("Helmet", size=(0.46, 0.44, 0.22), location=(0, 0.01, 0.20), pivot=(0, 0, -1), taper=(0.62, 0.62),
                   swatch="iron", parent=head)
    K.box("Spike", size=(0.08, 0.08, 0.14), location=(0, 0, 0.22), pivot=(0, 0, -1), taper=(0.1, 0.1), swatch="steel",
          parent=helmet)
    K.box("Brim", size=(0.48, 0.46, 0.05), location=(0, 0.01, 0.18), pivot=(0, 0, -1), swatch="iron_d", parent=head)
    K.box("NoseGuard", size=(0.06, 0.05, 0.17), location=(0, -0.215, 0.06), pivot=(0, 0, -1), swatch="iron",
          parent=head)
    K.box("Eyes", size=(0.26, 0.03, 0.05), location=(0, -0.195, 0.13), pivot=(0, 0, -1), swatch="ink", parent=head)
    K.box("Beard", size=(0.30, 0.06, 0.12), location=(0, -0.20, -0.01), pivot=(0, 0, -1), taper=(0.7, 1.0),
          swatch="mud_dark", parent=head)

    # shield arm (left = +X): forearm forward, shield counter-rotated to face front
    arm_l = K.box("ArmL", size=(0.15, 0.15, 0.40), location=(0.34, 0, 0.47), pivot=(0, 0, 1), swatch="skin_mid",
                  parent=body)
    arm_l.rotation_euler = (-0.7, 0, 0)
    shield = K.box("Shield", size=(0.64, 0.08, 0.78), location=(-0.30, -0.12, -0.34), pivot=(0, 0, 0),
                   swatch="wood_light", parent=arm_l)
    shield.rotation_euler = (0.7, 0, 0)
    K.box("ShieldRimT", size=(0.66, 0.10, 0.06), location=(0, 0, 0.37), pivot=(0, 0, 0), swatch="iron", parent=shield)
    K.box("ShieldRimB", size=(0.66, 0.10, 0.06), location=(0, 0, -0.37), pivot=(0, 0, 0), swatch="iron", parent=shield)
    K.box("ShieldBoss", size=(0.20, 0.08, 0.20), location=(0, -0.06, 0.02), pivot=(0, 0, 0), taper=(1, 1),
          swatch="accent_magenta", parent=shield)

    # club arm (right = -X)
    arm_r = K.box("ArmR", size=(0.15, 0.15, 0.40), location=(-0.34, 0, 0.47), pivot=(0, 0, 1), swatch="skin_mid",
                  parent=body)
    arm_r.rotation_euler = (-0.25, 0, 0)
    K.box("FistR", size=(0.17, 0.17, 0.14), location=(0, 0, -0.40), pivot=(0, 0, 0), swatch="skin_mid", parent=arm_r)
    club = K.cylinder("Club", radius=0.055, depth=0.62, verts=6, location=(0, -0.02, -0.40), pivot=(0, 0, -1),
                      taper=1.9, swatch="wood_dark", parent=arm_r)
    club.rotation_euler = (0.85, 0, 0)  # mesh runs up (+Z); with the arm's -0.25 it points up-forward out of the fist

    for side, x in (("L", 0.13), ("R", -0.13)):
        leg = K.box("Leg" + side, size=(0.18, 0.19, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="leather_d", parent=root)
        K.box("Boot" + side, size=(0.20, 0.27, 0.10), location=(0, -0.04, -0.42), pivot=(0, 0, -1), swatch="iron_d",
              parent=leg)
    return root


root = build()
info = K.review_render(root, "en_shieldthug", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_ShieldThug")
info["blend"] = K.save_blend("en_shieldthug")
result = info
