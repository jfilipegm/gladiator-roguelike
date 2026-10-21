"""EN_NetThrower — Net-Thrower, Arena 3's catcher (docs/ARENAS.md §3). Budget <= 300 tris.

A lean retiarius: bare head (no helmet: the only bare head among the arena's fighters), a tall steel galerus guarding
the left shoulder, a bundled net slung over the right shoulder and a weighted net balled in the right hand (the lob),
a long trident upright in the left. Blue loincloth, the magenta belt is the enemy accent. Silhouette: the trident's
prongs over the head, the shoulder plate, the bundle. Rigid parts on the shared names; front = -Y (Unity +Z).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.40


def build():
    K.clear_scene()
    root = K.empty("EN_NetThrower")
    body = K.box("Body", size=(0.44, 0.30, 0.48), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.12, 1.0),
                 swatch="skin_light", parent=root)
    K.box("Belt", size=(0.46, 0.32, 0.09), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Loin", size=(0.46, 0.33, 0.14), location=(0, 0, -0.12), pivot=(0, 0, -1), swatch="cloth_blue", parent=body)
    K.box("Galerus", size=(0.08, 0.30, 0.34), location=(0.27, 0.0, 0.36), pivot=(0, 0, -1), taper=(1.0, 0.8),
          swatch="steel_l", parent=body)  # the shoulder plate
    bundle = K.box("Bundle", size=(0.24, 0.40, 0.22), location=(-0.16, 0.08, 0.38), pivot=(0, 0, -1), taper=(0.8, 0.8),
                   swatch="rope", parent=body)
    bundle.rotation_euler = (0.3, 0, -0.5)

    head = K.box("Head", size=(0.32, 0.31, 0.32), location=(0, -0.02, 0.48), pivot=(0, 0, -1), swatch="skin_light",
                 parent=body)
    K.box("Hair", size=(0.34, 0.33, 0.09), location=(0, 0.02, 0.26), pivot=(0, 0, -1), swatch="mud_dark", parent=head)
    K.box("Eyes", size=(0.20, 0.03, 0.04), location=(0, -0.16, 0.16), pivot=(0, 0, -1), swatch="ink", parent=head)

    # left arm (+X): the trident, upright, prongs over the head
    arm_l = K.box("ArmL", size=(0.11, 0.11, 0.36), location=(0.28, 0, 0.44), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    arm_l.rotation_euler = (-0.5, 0, 0)
    shaft = K.cylinder("Trident", radius=0.025, depth=1.55, verts=5, location=(0.0, -0.04, -0.75), pivot=(0, 0, -1),
                       swatch="wood_dark", parent=arm_l)
    shaft.rotation_euler = (0.5, 0, 0)  # counter the arm: upright
    K.box("Prongs", size=(0.26, 0.04, 0.05), location=(0, 0, 1.52), pivot=(0, 0, -1), swatch="steel", parent=shaft)
    for x in (-0.11, 0.0, 0.11):
        K.box("Prong", size=(0.035, 0.035, 0.24 if x == 0.0 else 0.20), location=(x, 0, 1.56), pivot=(0, 0, -1),
              taper=(0.3, 0.3), swatch="steel_l", parent=shaft)

    # right arm (-X): the net, balled in the fist (wound back and lobbed)
    arm_r = K.box("ArmR", size=(0.11, 0.11, 0.36), location=(-0.28, 0, 0.44), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    arm_r.rotation_euler = (-0.25, 0, 0)
    K.ico("HandNet", radius=0.15, subdiv=1, location=(0, -0.04, -0.42), swatch="rope", parent=arm_r)

    for side, x in (("L", 0.10), ("R", -0.10)):
        leg = K.box("Leg" + side, size=(0.13, 0.14, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="skin_light", parent=root)
        K.box("Foot" + side, size=(0.14, 0.22, 0.06), location=(0, -0.03, -0.40), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)
        if side == "R":
            K.box("Wrap", size=(0.15, 0.16, 0.14), location=(0, 0, -0.30), pivot=(0, 0, -1), swatch="cloth_white",
                  parent=leg)
    return root


root = build()
info = K.review_render(root, "en_netthrower", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_NetThrower")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_netthrower")
result = info
