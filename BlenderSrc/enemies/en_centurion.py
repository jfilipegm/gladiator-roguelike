"""EN_Centurion — the Imperial Arena's formation leader (docs/ARENAS.md §5). Budget <= 300 tris.

The officer who walks a line of Praetorians into the fight: a silver scale cuirass with a gold harness of medals, a
purple tunic, a long crimson cloak down his back, and a steel helm under a broad red crest that runs SIDE TO SIDE
(every other crest in the game runs front to back): his silhouette is a wide fan over his head. Right hand (-X): the
vine staff, his badge of rank, raised up and forward to drive his men on. No shield: kill him first. The magenta belt
is the enemy accent. Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR); front = -Y (Unity +Z).
"""
import importlib

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 0.5


def build():
    K.clear_scene()
    root = K.empty("EN_Centurion")
    body = K.box("Body", size=(0.62, 0.42, 0.58), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.16, 1.02),
                 swatch="steel_l", parent=root)
    K.box("Phalerae", size=(0.34, 0.04, 0.24), location=(0, -0.225, 0.22), pivot=(0, 0, -1), swatch="gold",
          parent=body)  # the harness of medals
    K.box("Belt", size=(0.64, 0.44, 0.1), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Skirt", size=(0.58, 0.4, 0.22), location=(0, 0, -0.2), pivot=(0, 0, -1), taper=(0.92, 0.95),
          swatch="purple", parent=body)
    K.box("Cloak", size=(0.74, 0.07, 0.92), location=(0, 0.25, 0.58), pivot=(0, 0, 1), taper=(0.86, 1.0),
          swatch="crimson", parent=body)  # flaring from the shoulders down his back

    head = K.box("Head", size=(0.42, 0.4, 0.38), location=(0, -0.01, 0.58), pivot=(0, 0, -1), swatch="skin_light",
                 parent=body)
    helm = K.box("Helm", size=(0.47, 0.45, 0.24), location=(0, 0.01, 0.2), pivot=(0, 0, -1), taper=(0.8, 0.8),
                 swatch="steel", parent=head)
    for side, x in (("L", 0.19), ("R", -0.19)):
        K.box("Cheek" + side, size=(0.07, 0.24, 0.2), location=(x, -0.06, 0.0), pivot=(0, 0, -1), swatch="steel_l",
              parent=head)
    K.box("Eyes", size=(0.24, 0.03, 0.05), location=(0, -0.205, 0.12), pivot=(0, 0, -1), swatch="ink", parent=head)
    # the transverse crest: a broad arched fan across the helm, wider than his shoulders
    fan = [(-0.3, 0.0), (-0.44, 0.1), (-0.4, 0.24), (-0.22, 0.34), (0.0, 0.37), (0.22, 0.34), (0.4, 0.24),
           (0.44, 0.1), (0.3, 0.0)]
    K.extrude_poly("Crest", fan, thickness=0.1, plane="XZ", location=(0, 0.02, 0.2), swatch="red_banner", parent=helm)

    # left arm: a fist on his hip
    arm_l = K.box("ArmL", size=(0.16, 0.16, 0.42), location=(0.39, 0, 0.52), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    arm_l.rotation_euler = (0, 0.2, 0)  # elbow out
    K.box("FistL", size=(0.17, 0.17, 0.13), location=(0, 0, -0.42), pivot=(0, 0, 0), swatch="leather_d", parent=arm_l)

    # right arm: the vine staff raised beside him, leaning up, forward and out
    # iteration 2: longer and more upright (v1 pointed at the camera and vanished from the front)
    arm_r = K.box("ArmR", size=(0.16, 0.16, 0.42), location=(-0.39, 0, 0.52), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    arm_r.rotation_euler = (-0.5, 0.25, 0)
    K.box("FistR", size=(0.17, 0.17, 0.13), location=(0, 0, -0.42), pivot=(0, 0, 0), swatch="leather_d", parent=arm_r)
    staff = K.cylinder("Vitis", radius=0.04, depth=1.15, verts=4, location=(0, 0, -0.42), pivot=(0, 0, -0.45),
                       swatch="wood_light", parent=arm_r)
    staff.rotation_euler = (0.8, -0.5, 0)  # with the arm's (-0.5, 0.25): ~20 deg forward and out from upright
    K.box("Knot", size=(0.09, 0.09, 0.09), location=(0, 0, 0.3), pivot=(0, 0, 0), swatch="wood", parent=staff)
    K.box("Cap", size=(0.1, 0.1, 0.1), location=(0, 0, 0.83), pivot=(0, 0, 0), swatch="gold_l", parent=staff)

    for side, x in (("L", 0.15), ("R", -0.15)):
        leg = K.box("Leg" + side, size=(0.19, 0.2, 0.46), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="gold_d", parent=root)  # gilded greaves (the skirt covers the thighs)
        K.box("Boot" + side, size=(0.21, 0.28, 0.08), location=(0, -0.04, -0.5), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)
    return root


root = build()
info = K.review_render(root, "en_centurion", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Centurion")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_centurion")
result = info
