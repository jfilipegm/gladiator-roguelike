"""EN_Champion — the Provincial Champion, Arena 2's boss (docs/BOSSES.md). Budget <= 2,500 tris.

A broad, armoured murmillo (~2.7 m, between the Nobody and Gorm): bronze cuirass, a broad-brimmed steel helm with a
face grille and a tall fish crest, a segmented manica on the sword arm, a greave on the leading leg. Left arm (+X): a
huge curved scutum (crimson with a gold rim, spine and boss) that covers him from shin to chin: from the front the
shield IS the silhouette, and nothing else in the pits carries a door-sized shield. Right hand (-X): a short gladius.
The magenta belt is the enemy accent. Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR); the shield and
gladius ride on the arms. Origin at the feet; front = -Y (Unity +Z).
"""
import importlib

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 1.0


def build():
    K.clear_scene()
    root = K.empty("EN_Champion")
    body = K.box("Body", size=(1.0, 0.66, 0.95), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.22, 1.1),
                 bevel=0.03, swatch="bronze", parent=root)
    for k in range(2):  # sculpted cuirass bands
        K.box("Band%d" % k, size=(0.92 + k * 0.08, 0.04, 0.06), location=(0, -0.33, 0.28 + k * 0.24), pivot=(0, 0, -1),
              swatch="bronze_d", parent=body)
    K.box("Belt", size=(1.06, 0.72, 0.16), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Skirt", size=(0.98, 0.64, 0.38), location=(0, 0, -0.36), pivot=(0, 0, -1), taper=(0.92, 0.95),
          swatch="cloth_white", parent=body)
    K.box("Neck", size=(0.36, 0.34, 0.12), location=(0, -0.02, 0.95), pivot=(0, 0, -1), swatch="skin_mid", parent=body)

    head = K.box("Head", size=(0.5, 0.48, 0.46), location=(0, -0.04, 1.02), pivot=(0, 0, -1), swatch="skin_mid",
                 parent=body)
    dome = K.box("Helm", size=(0.6, 0.6, 0.42), location=(0, 0.01, 0.14), pivot=(0, 0, -1), taper=(0.72, 0.72),
                 bevel=0.02, swatch="steel", parent=head)
    K.box("Brim", size=(0.9, 0.9, 0.05), location=(0, 0.01, 0.12), pivot=(0, 0, -1), swatch="iron", parent=head)
    K.box("Grille", size=(0.46, 0.04, 0.36), location=(0, -0.26, -0.22), pivot=(0, 0, -1), swatch="iron_d", parent=head)
    for k, x in enumerate((-0.12, 0.0, 0.12)):
        K.box("Bar%d" % k, size=(0.03, 0.03, 0.34), location=(x, -0.285, -0.21), pivot=(0, 0, -1), swatch="steel_l",
              parent=head)
    # the fish crest: a tall fin front to back, with a blunt "head" at the front
    K.box("Crest", size=(0.15, 0.72, 0.44), location=(0, 0.04, 0.4), pivot=(0, 0, -1), taper=(1.0, 0.55),
          swatch="crimson", parent=dome)
    K.box("CrestHead", size=(0.12, 0.18, 0.24), location=(0, -0.3, 0.38), pivot=(0, 0, -1), taper=(0.8, 0.6),
          swatch="crimson_d", parent=dome)

    for side, x, sgn in (("L", 0.62, 1), ("R", -0.62, -1)):
        K.box("Pauldron" + side, size=(0.4, 0.5, 0.22), location=(x * 0.92, 0, 0.82), pivot=(0, 0, -1),
              taper=(0.7, 0.8), swatch="bronze_d", parent=body)
        arm = K.box("Arm" + side, size=(0.28, 0.3, 0.85), location=(x, 0, 0.9), pivot=(0, 0, 1), taper=(1.1, 1.1),
                    swatch="skin_mid", parent=body)
        arm.rotation_euler = (0, 0, sgn * 0.08)
        K.box("Hand" + side, size=(0.3, 0.32, 0.24), location=(0, -0.02, -0.86), pivot=(0, 0, 1), swatch="skin_mid",
              parent=arm)
        leg = K.box("Leg" + side, size=(0.36, 0.38, 1.0), location=(x * 0.45, 0, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.15, 1.1), swatch="skin_mid", parent=root)
        K.box("Boot" + side, size=(0.4, 0.54, 0.22), location=(0, -0.07, -1.0), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)
        if side == "L":  # the greave on the leading (shield-side) leg
            K.box("Greave", size=(0.4, 0.42, 0.5), location=(0, -0.02, -0.78), pivot=(0, 0, -1), swatch="bronze",
                  parent=leg)

    # sword arm: the manica (segmented guard) and the gladius, blade pointing forward out of the fist
    arm_r = [o for o in root.children_recursive if o.name == "ArmR"][0]
    for k in range(3):
        K.box("Manica%d" % k, size=(0.34, 0.36, 0.2), location=(0, 0, -0.12 - k * 0.24), pivot=(0, 0, 1),
              taper=(0.95, 0.95), swatch="steel" if k % 2 == 0 else "iron", parent=arm_r)
    gladius = K.box("Gladius", size=(0.07, 0.12, 0.12), location=(0, -0.04, -0.98), pivot=(0, 0, 0), swatch="leather_d",
                    parent=arm_r)
    K.box("Guard", size=(0.12, 0.08, 0.26), location=(0, -0.09, 0), pivot=(0, 0, 0), swatch="gold", parent=gladius)
    K.box("Blade", size=(0.04, 0.8, 0.16), location=(0, -0.13, 0), pivot=(0, 1, 0), taper=(1, 1), swatch="steel_l",
          parent=gladius)

    # shield arm: the scutum in front of the body, base at shin height, top at the chin; curved = three panels
    arm_l = [o for o in root.children_recursive if o.name == "ArmL"][0]
    # the centre panel is lighter than the wings, so the flat-shaded scutum reads curved
    shield = K.box("Shield", size=(0.66, 0.1, 1.75), location=(-0.42, -0.66, -1.62), pivot=(0, 0, -1), swatch="crimson",
                   parent=arm_l)
    for s, sx in (("L", 1), ("R", -1)):
        wing = K.box("Wing" + s, size=(0.3, 0.1, 1.75), location=(sx * 0.46, 0.07, 0), pivot=(0, 0, -1),
                     swatch="crimson_d", parent=shield)
        wing.rotation_euler = (0, 0, sx * 0.45)
    K.box("RimT", size=(0.7, 0.12, 0.07), location=(0, 0, 1.72), pivot=(0, 0, -1), swatch="gold", parent=shield)
    K.box("RimB", size=(0.7, 0.12, 0.07), location=(0, 0, 0), pivot=(0, 0, -1), swatch="gold", parent=shield)
    K.box("Spine", size=(0.09, 0.06, 1.6), location=(0, -0.07, 0.08), pivot=(0, 0, -1), swatch="gold_d", parent=shield)
    K.ico("Boss", radius=0.17, subdiv=1, location=(0, -0.1, 0.9), scale=(1, 0.6, 1), swatch="gold_l", parent=shield)
    return root


root = build()
info = K.review_render(root, "en_champion", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Champion")
info["blend"] = K.save_blend("en_champion")
result = info
