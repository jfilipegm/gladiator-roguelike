"""CH_MurderMachine — the Murder Machine (docs/GLADIATORS.md): a huge armoured executioner with a giant cleaver.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_MurderMachine (root, base)
    Body   (hip centre)      a barrel of riveted iron plate, a leather apron, a black hood-mantle over the shoulders
      Head (neck)            a tall pointed black executioner's hood, two eye holes glowing player cyan
      ArmL (left shoulder)   (follows the right arm: a two-handed grip)
      ArmR (right shoulder)  -> Cleaver (in the fist): handle and a rectangular slab of a blade, held low and forward
    LegL, LegR (hip joints)  short iron legs
Front = -Y, asset's left = +X. Hood tip ~2.15 m (the Nobody is 1.6 m), head ~36 %.
Silhouette marks (64 px test vs the Knight, the Brute, Gorm and the Beast Lord): the pointed hood and the slab of a blade.
Readability: the eye holes are player cyan from the front; from the camera behind, a pale cyan axe sigil on his back.
The cleaver extends from the fist 70 deg forward of straight down, so the overhead chop (PartAnimProfile.overheadChop)
brings it from behind the head down in front of him, and at rest it's held low without touching the ground.
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.56
SHOULDER_Z = 0.74   # relative to Body
NECK_Z = 0.84       # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_MurderMachine")

    # --- body: a barrel of iron plate, wider at the chest, rivets, belt, apron
    body = K.box("Body", size=(0.86, 0.62, 0.88), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.22, 1.12), bevel=0.05, swatch="rusty_iron", parent=root)  # iteration 2: warm rust, not the Knight's black
    K.box("ChestPlate", size=(0.80, 0.06, 0.46), location=(0, -0.33, 0.36), pivot=(0, 0, -1), taper=(1.12, 1.0),
          swatch="iron", parent=body)
    for k, x in enumerate((-0.3, -0.1, 0.1, 0.3)):
        K.box("Rivet%d" % k, size=(0.06, 0.04, 0.06), location=(x, -0.37, 0.76), pivot=(0, 0, -1), swatch="steel_l",
              parent=body)
    K.box("Belt", size=(0.92, 0.68, 0.10), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("Buckle", size=(0.14, 0.04, 0.10), location=(0, -0.35, 0.02), pivot=(0, 0, -1), swatch="rusty_iron",
          parent=body)
    apron = K.box("Apron", size=(0.56, 0.05, 0.58), location=(0, -0.345, 0.06), pivot=(0, 0, 1), taper=(1.15, 1.0),
                  swatch="leather", parent=body)
    K.box("ApronStain", size=(0.3, 0.02, 0.2), location=(0.06, -0.03, -0.36), pivot=(0, 0, 0), swatch="rust",
          parent=apron)
    # the hood's mantle: black cloth over the shoulders, a point down the back
    K.box("Mantle", size=(1.02, 0.72, 0.18), location=(0, 0.02, 0.74), pivot=(0, 0, -1), taper=(0.82, 0.9),
          bevel=0.03, swatch="charcoal", parent=body)
    K.extrude_poly("MantleTail", [(-0.32, 0.0), (0.32, 0.0), (0.0, -0.42)], thickness=0.05, plane="XZ",
                   location=(0, 0.37, 0.80), swatch="charcoal", parent=body)
    # the "that's me" mark for the camera behind him: a pale cyan axe on the back plate
    K.box("SigilHaft", size=(0.06, 0.03, 0.40), location=(0, 0.325, 0.18), pivot=(0, 0, -1), swatch="player_cyan",
          parent=body)
    K.box("SigilHead", size=(0.22, 0.03, 0.16), location=(0.07, 0.325, 0.44), pivot=(0, 0, -1), swatch="player_cyan",
          parent=body)

    # --- head: the tall pointed executioner's hood, two cyan eye holes
    head = K.box("Head", size=(0.46, 0.46, 0.42), location=(0, -0.04, NECK_Z), pivot=(0, 0, -1), taper=(0.86, 0.95),
                 bevel=0.04, swatch="ink", parent=body)
    point = K.cone("HoodPoint", radius=0.24, depth=0.40, verts=4, location=(0, 0.02, 0.40), pivot=(0, 0, -1),
                   swatch="ink", parent=head)
    point.rotation_euler = (math.radians(-8), 0, math.radians(45))  # a square hood, the tip leaning back a little
    for side, x in (("L", 0.1), ("R", -0.1)):
        K.box("Eye" + side, size=(0.11, 0.03, 0.06), location=(x, -0.235, 0.24), pivot=(0, 0, -1),
              swatch="glow_cyan", parent=head)

    # --- arms: thick, iron vambraces, big fists (pivot at the shoulder, hanging down)
    arms = {}
    for side, x in (("L", 0.62), ("R", -0.62)):  # iteration 2: out past the barrel so the bare arms show
        arm = K.box("Arm" + side, size=(0.26, 0.28, 0.56), location=(x, -0.02, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.1, 1.1), bevel=0.03, swatch="skin_tan", parent=body)
        K.box("Pauldron" + side, size=(0.34, 0.36, 0.14), location=(0, 0, 0.02), pivot=(0, 0, 0), bevel=0.03,
              swatch="iron", parent=arm)
        K.box("Vambrace" + side, size=(0.30, 0.32, 0.24), location=(0, 0, -0.34), pivot=(0, 0, 0), swatch="iron",
              parent=arm)
        K.box("Fist" + side, size=(0.26, 0.26, 0.18), location=(0, 0, -0.56), pivot=(0, 0, 0), bevel=0.03,
              swatch="iron_d", parent=arm)
        arms[side] = arm

    # --- the Executioner's Cleaver (right fist): a handle on down along the fist, then the slab of the blade
    cleaver = K.empty("Cleaver", location=(0, 0, -0.56), parent=arms["R"])
    cleaver.rotation_euler = (math.radians(-70), 0, 0)  # local -Z (along the handle) points forward and down
    K.box("Handle", size=(0.07, 0.07, 0.44), location=(0, 0, 0.12), pivot=(0, 0, 1), swatch="wood_dark",
          parent=cleaver)
    K.box("Ferrule", size=(0.10, 0.10, 0.08), location=(0, 0, -0.30), pivot=(0, 0, 0), swatch="iron_d",
          parent=cleaver)
    # the blade: (y, z) in the cleaver's frame; the spine runs along the handle line, the slab rises on the -Y side
    K.extrude_poly("Blade", [(0.04, -0.32), (0.04, -1.28), (-0.50, -1.28), (-0.50, -0.42), (-0.40, -0.32)],
                   thickness=0.09, plane="YZ", swatch="steel", parent=cleaver)  # iteration 2: thick enough to read from behind
    K.box("Edge", size=(0.095, 0.07, 0.92), location=(0, -0.48, -0.34), pivot=(0, 0, 1), swatch="steel_l",
          parent=cleaver)
    K.box("Spine", size=(0.12, 0.06, 0.96), location=(0, 0.04, -0.32), pivot=(0, 0, 1), swatch="iron_d",
          parent=cleaver)
    K.box("Hole", size=(0.1, 0.12, 0.12), location=(0, -0.13, -1.12), pivot=(0, 0, 0), swatch="ink",
          parent=cleaver)

    # --- legs: short and thick, iron greaves, broad boots
    for side, x in (("L", 0.22), ("R", -0.22)):
        leg = K.box("Leg" + side, size=(0.30, 0.32, 0.46), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.08, 1.08),
                    swatch="leather_d", parent=root)
        K.box("Greave" + side, size=(0.33, 0.35, 0.22), location=(0, -0.01, -0.30), pivot=(0, 0, 0), swatch="iron",
              parent=leg)
        K.box("Boot" + side, size=(0.34, 0.46, 0.12), location=(0, -0.06, -0.56), pivot=(0, 0, -1), bevel=0.02,
              swatch="iron_d", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_murdermachine", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_MurderMachine")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("ch_murdermachine")
result = info
