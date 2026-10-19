"""EN_Assassin — Assassin, Tier-2 flanker for the Provincial Pit (docs/ARENAS.md). Budget <= 300 tris.

Lean, dark (charcoal on the sand floor = the strongest contrast in the arena), a pointed hood with a white eye slit,
two steel daggers held low and a long magenta scarf streaming down the back: the scarf tail is the silhouette tell
(nothing else in the pits has one) and the accent patch. The dash pose leans flat forward with both blades back
(EnemyManager).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.42


def build():
    K.clear_scene()
    root = K.empty("EN_Assassin")
    body = K.box("Body", size=(0.30, 0.22, 0.46), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.2, 1.0),
                 swatch="charcoal", parent=root)
    K.box("Sash", size=(0.32, 0.24, 0.06), location=(0, 0, 0.05), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    head = K.box("Head", size=(0.28, 0.27, 0.30), location=(0, -0.01, 0.47), pivot=(0, 0, -1), swatch="slate",
                 parent=body)
    hood = K.box("Hood", size=(0.36, 0.34, 0.22), location=(0, 0.02, 0.24), pivot=(0, 0, -1), taper=(0.3, 0.5),
                 swatch="charcoal", parent=head)
    K.box("HoodTip", size=(0.06, 0.10, 0.12), location=(0, 0.07, 0.18), pivot=(0, 0, -1), taper=(0.2, 0.2),
          swatch="ink", parent=hood)
    K.box("EyeSlit", size=(0.20, 0.03, 0.04), location=(0, -0.14, 0.15), pivot=(0, 0, -1), swatch="white",
          parent=head)
    # the scarf: a collar, then a long tail hanging down the back past the knees
    K.box("Scarf", size=(0.34, 0.30, 0.08), location=(0, 0, 0.40), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    tail = K.box("ScarfTail", size=(0.16, 0.04, 0.80), location=(0.06, 0.15, 0.44), pivot=(0, 0, 1), taper=(0.7, 1.0),
                 swatch="accent_magenta", parent=body)
    tail.rotation_euler = (0.85, 0, 0.12)  # streams out behind, as if running

    for side, x, sx in (("L", 0.20, 1), ("R", -0.20, -1)):
        arm = K.box("Arm" + side, size=(0.09, 0.09, 0.36), location=(x, 0, 0.40), pivot=(0, 0, 1), swatch="charcoal",
                    parent=body)
        arm.rotation_euler = (-0.25, 0, -0.12 * sx)
        fist = K.box("Fist" + side, size=(0.10, 0.10, 0.09), location=(0, 0, -0.38), pivot=(0, 0, 0), swatch="slate",
                     parent=arm)
        blade = K.box("Dagger" + side, size=(0.035, 0.07, 0.38), location=(0, -0.02, -0.03), pivot=(0, 0, 1),
                      taper=(1.0, 1.0), swatch="steel_l", parent=fist)  # reverse grip: blade points down
        blade.rotation_euler = (0.5, 0, 0)

    for side, x in (("L", 0.08), ("R", -0.08)):
        leg = K.box("Leg" + side, size=(0.11, 0.12, 0.40), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="charcoal", parent=root)
        K.box("Foot" + side, size=(0.12, 0.20, 0.06), location=(0, -0.03, -0.42), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)
    return root


root = build()
info = K.review_render(root, "en_assassin", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Assassin")
info["blend"] = K.save_blend("en_assassin")
result = info
