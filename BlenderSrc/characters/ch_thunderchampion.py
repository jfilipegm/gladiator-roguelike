"""CH_ThunderChampion — the Thunder Champion (docs/GLADIATORS.md): bronze armour, a storm helm, a storm spear.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_ThunderChampion (root, base)
    Body   (hip centre)      bronze cuirass over a blue tunic, a short blue cape with a white bolt on its back
      Head (neck)            bronze storm helm, cheek guards, a tall zigzag lightning crest (white-cyan) side to side
      ArmL (left shoulder)
      ArmR (right shoulder)  -> StormSpear (fist): held upright, a bronze leaf blade with a glowing core above his head
    LegL, LegR (hip joints)  bronze greaves, sandals
Front = -Y, asset's left = +X. Crest top ~1.75 m, spear tip ~2.1 m (the Nobody is 1.6 m), head ~36 %.
Silhouette marks (64 px test vs the Nobody, the Knight and the Net-Thrower): the zigzag crest and the upright spear.
Readability: the crest and the eyes are player white-cyan; from the camera behind, the cape's white bolt.
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.50
SHOULDER_Z = 0.46   # relative to Body
NECK_Z = 0.52       # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_ThunderChampion")

    # --- body: an athlete's bronze cuirass, a blue tunic skirt, belt
    body = K.box("Body", size=(0.42, 0.27, 0.54), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.22, 1.1), bevel=0.03, swatch="bronze_l", parent=root)  # iteration 2: lighter (bronze read as leather)
    K.box("Abs", size=(0.22, 0.03, 0.22), location=(0, -0.145, 0.12), pivot=(0, 0, -1), swatch="gold_l", parent=body)
    K.box("Belt", size=(0.46, 0.31, 0.07), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("Skirt", size=(0.46, 0.31, 0.24), location=(0, 0, 0.02), pivot=(0, 0, 1), taper=(0.92, 0.94),
          swatch="cloth_blue", parent=body)
    for k, x in enumerate((-0.15, -0.05, 0.05, 0.15)):  # leather strips (pteruges) over the skirt
        K.box("Strip%d" % k, size=(0.07, 0.33, 0.2), location=(x, 0, -0.02), pivot=(0, 0, 1), swatch="leather",
              parent=body)
    # the cape: short, from the shoulders, a white bolt on its back for the camera behind him
    cape = K.box("Cape", size=(0.64, 0.04, 0.72), location=(0, 0.16, 0.54), pivot=(0, 0, 1), taper=(0.7, 1.0),
                 swatch="cloth_blue", parent=body)
    cape.rotation_euler = (math.radians(12), 0, 0)
    K.extrude_poly("CapeBolt", [(0.06, -0.06), (-0.06, -0.24), (0.03, -0.24), (-0.07, -0.46), (0.09, -0.2),
                                (0.0, -0.2), (0.1, -0.06)], thickness=0.02, plane="XZ", location=(0, 0.03, 0),
                   swatch="player_white", parent=cape)

    # --- head: face, bronze storm helm with cheek guards, the zigzag crest side to side
    head = K.box("Head", size=(0.4, 0.38, 0.4), location=(0, -0.02, NECK_Z), pivot=(0, 0, -1), bevel=0.04,
                 swatch="skin_mid", parent=body)
    K.box("Eyes", size=(0.24, 0.03, 0.05), location=(0, -0.195, 0.2), pivot=(0, 0, -1), swatch="glow_cyan",
          parent=head)
    K.box("Helm", size=(0.44, 0.42, 0.22), location=(0, 0.01, 0.24), pivot=(0, 0, -1), taper=(0.9, 0.92),
          bevel=0.03, swatch="bronze_l", parent=head)
    K.box("Brow", size=(0.46, 0.08, 0.06), location=(0, -0.19, 0.25), pivot=(0, 0, -1), swatch="bronze_d",
          parent=head)
    for side, x in (("L", 0.19), ("R", -0.19)):
        K.box("Cheek" + side, size=(0.06, 0.2, 0.22), location=(x, -0.08, 0.04), pivot=(0, 0, -1), swatch="bronze",
              parent=head)
    # the crest: a lightning bolt across the helm (visible from the front and from behind)
    # iteration 2: bigger and centred (it was lost at 64 px): a bolt 0.56 m wide, 0.5 m tall
    K.extrude_poly("Crest", [(-0.28, 0.0), (0.02, 0.0), (-0.1, 0.2), (0.12, 0.2), (0.0, 0.5), (0.28, 0.12),
                             (0.06, 0.12), (0.16, 0.0), (0.28, 0.0), (0.28, -0.03), (-0.28, -0.03)],
                   thickness=0.09, plane="XZ", location=(0, 0.02, 0.46), swatch="glow_cyan", parent=head)
    K.box("CrestBase", size=(0.5, 0.1, 0.05), location=(0, 0.02, 0.43), pivot=(0, 0, -1), swatch="bronze_d",
          parent=head)

    # --- arms: bare, bronze bracers, fists (pivot at the shoulder, hanging down)
    arms = {}
    for side, x in (("L", 0.27), ("R", -0.27)):
        arm = K.box("Arm" + side, size=(0.13, 0.14, 0.38), location=(x, 0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.05, 1.05), bevel=0.02, swatch="skin_mid", parent=body)
        K.box("Shoulder" + side, size=(0.18, 0.2, 0.1), location=(0, 0, 0.02), pivot=(0, 0, 0), bevel=0.02,
              swatch="bronze_l", parent=arm)
        K.box("Bracer" + side, size=(0.15, 0.16, 0.13), location=(0, 0, -0.26), pivot=(0, 0, 0), swatch="bronze_l",
              parent=arm)
        K.box("Fist" + side, size=(0.13, 0.13, 0.11), location=(0, 0, -0.39), pivot=(0, 0, 0), bevel=0.02,
              swatch="skin_mid", parent=arm)
        arms[side] = arm

    # --- the storm spear (right fist): held upright, butt near the ground, the blade above his head
    spear = K.empty("StormSpear", location=(0, 0, -0.39), parent=arms["R"])
    K.cylinder("Shaft", radius=0.03, depth=1.6, verts=6, location=(0, 0, -0.45), pivot=(0, 0, -1),
               swatch="wood_dark", parent=spear)
    K.box("Bands", size=(0.075, 0.075, 0.1), location=(0, 0, 0.0), pivot=(0, 0, 0), swatch="bronze_d", parent=spear)
    K.box("Socket", size=(0.08, 0.08, 0.12), location=(0, 0, 1.15), pivot=(0, 0, -1), swatch="bronze", parent=spear)
    K.extrude_poly("Blade", [(0.0, 1.27), (0.09, 1.4), (0.05, 1.56), (0.0, 1.66), (-0.05, 1.56), (-0.09, 1.4)],
                   thickness=0.04, plane="XZ", swatch="bronze_l", parent=spear)
    K.extrude_poly("Core", [(0.0, 1.32), (0.04, 1.42), (0.0, 1.6), (-0.04, 1.42)], thickness=0.06, plane="XZ",
                   swatch="glow_cyan", parent=spear)

    # --- legs: bronze greaves, sandals
    for side, x in (("L", 0.11), ("R", -0.11)):
        leg = K.box("Leg" + side, size=(0.15, 0.17, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.05, 1.05),
                    swatch="skin_mid", parent=root)
        K.box("Greave" + side, size=(0.17, 0.19, 0.2), location=(0, -0.01, -0.3), pivot=(0, 0, 0), swatch="bronze_l",
              parent=leg)
        K.box("Sandal" + side, size=(0.16, 0.26, 0.07), location=(0, -0.04, -0.5), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_thunderchampion", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_ThunderChampion")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("ch_thunderchampion")
result = info
