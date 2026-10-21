"""EN_Murmillo — Murmillo, Arena 3's heavy shield (docs/ARENAS.md §3). Budget <= 300 tris.

A broad, bare-chested arena regular: a brimmed steel helm with a face grille and a tall red fish crest, a padded
manica on the sword arm, a bronze greave on the leading leg. Left arm (+X): a tall rectangular blue scutum with bronze rims
(door-shaped, taller than the Shield Thug's plank shield and not the Champion's crimson one) that covers him from
knee to chin. Right hand (-X): a short gladius. The magenta belt is the enemy accent. Silhouette: the crest and the
door. Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR); front = -Y (Unity +Z).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.46


def build():
    K.clear_scene()
    root = K.empty("EN_Murmillo")
    body = K.box("Body", size=(0.62, 0.40, 0.56), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.15, 1.0),
                 swatch="skin_tan", parent=root)
    K.box("Belt", size=(0.64, 0.42, 0.11), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Loin", size=(0.42, 0.06, 0.24), location=(0, -0.20, -0.16), pivot=(0, 0, -1), swatch="cloth_white", parent=body)

    head = K.box("Head", size=(0.42, 0.40, 0.38), location=(0, -0.01, 0.56), pivot=(0, 0, -1), swatch="steel",
                 parent=body)
    K.box("Brim", size=(0.62, 0.58, 0.05), location=(0, 0.0, 0.04), pivot=(0, 0, -1), swatch="steel_l", parent=head)
    K.box("Grille", size=(0.30, 0.04, 0.22), location=(0, -0.205, 0.10), pivot=(0, 0, -1), swatch="iron_d", parent=head)
    K.box("Crest", size=(0.13, 0.50, 0.34), location=(0, 0.06, 0.36), pivot=(0, 0, -1), taper=(1.0, 0.55),
          swatch="red_banner", parent=head)  # the fish crest: tall, the silhouette's top

    # shield arm (left = +X): the scutum held square in front
    arm_l = K.box("ArmL", size=(0.16, 0.16, 0.42), location=(0.38, 0, 0.50), pivot=(0, 0, 1), swatch="skin_tan",
                  parent=body)
    arm_l.rotation_euler = (-0.7, 0, 0)
    shield = K.box("Shield", size=(0.66, 0.09, 1.04), location=(-0.30, -0.14, -0.30), pivot=(0, 0, 0),
                   swatch="cloth_blue", parent=arm_l)
    shield.rotation_euler = (0.7, 0, 0)
    K.box("Spine", size=(0.10, 0.04, 0.96), location=(0, -0.06, 0), pivot=(0, 0, 0), swatch="bronze_l", parent=shield)
    K.box("RimT", size=(0.68, 0.11, 0.06), location=(0, 0, 0.50), pivot=(0, 0, 0), swatch="bronze", parent=shield)
    K.box("RimB", size=(0.68, 0.11, 0.06), location=(0, 0, -0.50), pivot=(0, 0, 0), swatch="bronze", parent=shield)
    K.box("Boss", size=(0.22, 0.08, 0.22), location=(0, -0.08, 0.04), pivot=(0, 0, 0), swatch="bronze_l", parent=shield)

    # sword arm (right = -X): the padded manica, a gladius pointing forward
    arm_r = K.box("ArmR", size=(0.17, 0.17, 0.42), location=(-0.38, 0, 0.50), pivot=(0, 0, 1), swatch="cloth_white",
                  parent=body)
    arm_r.rotation_euler = (-0.35, 0, 0)
    K.box("FistR", size=(0.17, 0.17, 0.13), location=(0, 0, -0.42), pivot=(0, 0, 0), swatch="skin_tan", parent=arm_r)
    blade = K.box("Gladius", size=(0.08, 0.03, 0.50), location=(0, -0.02, -0.42), pivot=(0, 0, -1), taper=(0.6, 1.0),
                  swatch="steel_l", parent=arm_r)
    blade.rotation_euler = (1.2, 0, 0)
    K.box("Guard", size=(0.18, 0.06, 0.04), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="bronze_d", parent=blade)

    for side, x in (("L", 0.15), ("R", -0.15)):
        leg = K.box("Leg" + side, size=(0.19, 0.20, 0.42), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="skin_tan", parent=root)
        K.box("Boot" + side, size=(0.21, 0.28, 0.08), location=(0, -0.04, -0.46), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)
        if side == "L":
            K.box("Greave", size=(0.22, 0.23, 0.30), location=(0, -0.01, -0.38), pivot=(0, 0, -1), swatch="bronze",
                  parent=leg)
    return root


root = build()
info = K.review_render(root, "en_murmillo", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Murmillo")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_murmillo")
result = info
