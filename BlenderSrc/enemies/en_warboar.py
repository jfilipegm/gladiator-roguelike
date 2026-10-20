"""EN_WarBoar — War Boar, the Stone Amphitheater's cage beast (docs/ARENAS.md §3). Budget <= 300 tris.

A low, wide, heavy boar (~1 m at the shoulder, twice the Pit Cur's bulk): near-black hide with a bristling mud-brown
mane along the spine, a big wedge head with a pale snout disc and long bone tusks curling up, small glaring red eyes,
a magenta war harness strapped round the barrel (the enemy accent). Rigid parts like the Pit Cur so EnemyManager
drives it: front legs = ArmL/ArmR (children of Body), hind legs = LegL/LegR (children of the root). Its charge leans
the head down. Front = -Y (Unity +Z).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

BODY_Z = 0.36   # belly height
LEG = 0.36      # leg length (shoulder/hip to ground)


def build():
    K.clear_scene()
    root = K.empty("EN_WarBoar")
    body = K.box("Body", size=(0.62, 1.10, 0.56), location=(0, 0.06, BODY_Z), pivot=(0, 0, -1), taper=(0.85, 0.75),
                 swatch="charcoal", parent=root)
    K.box("Mane", size=(0.18, 0.86, 0.16), location=(0, -0.10, 0.50), pivot=(0, 0, -1), taper=(0.3, 0.7),
          swatch="mud_dark", parent=body)
    K.box("Harness", size=(0.66, 0.14, 0.60), location=(0, -0.10, -0.03), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    tail = K.box("Tail", size=(0.05, 0.05, 0.20), location=(0, 0.55, 0.36), pivot=(0, 0, -1), taper=(0.5, 0.5),
                 swatch="charcoal", parent=body)
    tail.rotation_euler = (-1.1, 0, 0)

    head = K.box("Head", size=(0.46, 0.40, 0.42), location=(0, -0.58, 0.08), pivot=(0, 0, -1), taper=(0.8, 0.85),
                 swatch="charcoal", parent=body)
    head.rotation_euler = (0.25, 0, 0)  # low, ready to gore
    K.box("Snout", size=(0.26, 0.24, 0.22), location=(0, -0.28, 0.02), pivot=(0, 0, -1), taper=(0.9, 0.9),
          swatch="mud_dark", parent=head)
    K.box("Disc", size=(0.22, 0.04, 0.17), location=(0, -0.41, 0.045), pivot=(0, 0, -1), swatch="skin_tan", parent=head)
    for side, x in (("L", 0.12), ("R", -0.12)):
        tusk = K.box("Tusk" + side, size=(0.06, 0.06, 0.32), location=(x, -0.33, 0.04), pivot=(0, 0, -1),
                     taper=(0.25, 0.25), swatch="bone", parent=head)
        tusk.rotation_euler = (-0.55, 0, 0.35 if x > 0 else -0.35)  # forward and up, splayed
        ear = K.box("Ear" + side, size=(0.10, 0.05, 0.14), location=(x + (0.08 if x > 0 else -0.08), 0.06, 0.34),
                    pivot=(0, 0, -1), taper=(0.3, 0.5), swatch="charcoal", parent=head)
        ear.rotation_euler = (0.4, 0, 0.6 if x > 0 else -0.6)
        K.box("Eye" + side, size=(0.06, 0.03, 0.05), location=(x * 1.05, -0.21, 0.26), pivot=(0, 0, -1),
              swatch="glow_hazard_red", parent=head)

    for side, x in (("L", 0.20), ("R", -0.20)):
        front = K.box("Arm" + side, size=(0.15, 0.17, LEG + 0.04), location=(x, -0.34, 0.04), pivot=(0, 0, 1),
                      taper=(0.8, 0.9), swatch="charcoal", parent=body)
        K.box("Hoof" + side, size=(0.14, 0.17, 0.07), location=(0, -0.02, -LEG - 0.04), pivot=(0, 0, -1),
              swatch="ink", parent=front)
        hind = K.box("Leg" + side, size=(0.16, 0.20, LEG + 0.06), location=(x, 0.40, BODY_Z + 0.06), pivot=(0, 0, 1),
                     taper=(0.8, 1.2), swatch="charcoal", parent=root)
        K.box("Foot" + side, size=(0.14, 0.17, 0.07), location=(0, -0.01, -LEG - 0.06), pivot=(0, 0, -1),
              swatch="ink", parent=hind)
    return root


root = build()
info = K.review_render(root, "en_warboar", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_WarBoar")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_warboar")
result = info
