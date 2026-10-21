"""EN_GildedMercenary — the Grand Arena's sellsword, the Gilded Patron's hired guard (docs/ARENAS.md §4). Budget <= 300
tris.

A broad professional in a steel cuirass trimmed with gold, a gilded helm with cheek guards and a tall crimson horsehair
crest, a crimson cloak, a fat purse on the magenta belt (the enemy accent; he steals your gold). Right hand (-X): a
halberd taller than he is, held upright (the Lunger's attack swings it forward into a thrust). Silhouette: the crest
and the halberd's axe head above it. Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR); front = -Y.
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.46


def build():
    K.clear_scene()
    root = K.empty("EN_GildedMercenary")
    body = K.box("Body", size=(0.56, 0.38, 0.56), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.18, 1.05),
                 swatch="steel", parent=root)
    K.box("Trim", size=(0.6, 0.4, 0.07), location=(0, 0, 0.48), pivot=(0, 0, -1), swatch="gold", parent=body)
    K.box("Belt", size=(0.6, 0.42, 0.1), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Purse", size=(0.16, 0.12, 0.16), location=(0.2, -0.22, -0.08), pivot=(0, 0, -1), swatch="gold_d", parent=body)
    K.box("Skirt", size=(0.5, 0.36, 0.2), location=(0, 0, -0.18), pivot=(0, 0, -1), taper=(0.9, 0.9), swatch="crimson_d",
          parent=body)
    K.box("Cloak", size=(0.58, 0.06, 0.78), location=(0, 0.22, 0.5), pivot=(0, 0, 1), taper=(1.0, 1.0), swatch="crimson",
          parent=body)

    head = K.box("Head", size=(0.38, 0.38, 0.38), location=(0, -0.01, 0.56), pivot=(0, 0, -1), swatch="gold",
                 parent=body)
    K.box("Face", size=(0.2, 0.04, 0.16), location=(0, -0.19, 0.08), pivot=(0, 0, -1), swatch="skin_mid", parent=head)
    K.box("Crest", size=(0.1, 0.56, 0.3), location=(0, 0.04, 0.36), pivot=(0, 0, -1), taper=(1.0, 0.7), swatch="crimson",
          parent=head)

    arm_l = K.box("ArmL", size=(0.15, 0.15, 0.4), location=(0.36, 0, 0.5), pivot=(0, 0, 1), swatch="skin_mid",
                  parent=body)
    K.box("PauldronL", size=(0.22, 0.24, 0.12), location=(0, 0, -0.02), pivot=(0, 0, 0), swatch="gold_d", parent=arm_l)
    K.box("FistL", size=(0.15, 0.15, 0.12), location=(0, 0, -0.4), pivot=(0, 0, 0), swatch="leather_d", parent=arm_l)

    arm_r = K.box("ArmR", size=(0.15, 0.15, 0.4), location=(-0.36, 0, 0.5), pivot=(0, 0, 1), swatch="skin_mid",
                  parent=body)
    K.box("PauldronR", size=(0.22, 0.24, 0.12), location=(0, 0, -0.02), pivot=(0, 0, 0), swatch="gold_d", parent=arm_r)
    K.box("FistR", size=(0.15, 0.15, 0.12), location=(0, 0, -0.4), pivot=(0, 0, 0), swatch="leather_d", parent=arm_r)
    # the halberd: held upright in the fist, the axe head well above the crest
    # iteration 2: the axe blade broadside to the front (v1 was edge-on and vanished at 64 px); 4-sided pole, no
    # cheek guards or collar (v1 was 360 tris)
    pole = K.cylinder("Halberd", radius=0.035, depth=2.3, verts=4, location=(0, -0.04, -0.95), pivot=(0, 0, -1),
                      swatch="wood_dark", parent=arm_r)
    K.box("Axe", size=(0.42, 0.05, 0.36), location=(-0.2, 0, 1.92), pivot=(0, 0, -1), taper=(0.55, 1.0), swatch="steel_l",
          parent=pole)
    K.cone("Spike", radius=0.05, depth=0.32, verts=4, location=(0, 0, 2.3), pivot=(0, 0, -1), swatch="steel_l",
           parent=pole)

    for side, x in (("L", 0.14), ("R", -0.14)):
        leg = K.box("Leg" + side, size=(0.18, 0.2, 0.42), location=(x, 0, HIP_Z), pivot=(0, 0, 1), swatch="leather",
                    parent=root)
        K.box("Greave" + side, size=(0.2, 0.22, 0.28), location=(0, -0.01, -0.38), pivot=(0, 0, -1), swatch="gold_d",
              parent=leg)
        K.box("Boot" + side, size=(0.2, 0.28, 0.08), location=(0, -0.04, -0.46), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)
    return root


root = build()
info = K.review_render(root, "en_gildedmercenary", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_GildedMercenary")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_gildedmercenary")
result = info
