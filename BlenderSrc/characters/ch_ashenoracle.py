"""CH_AshenOracle — the Ashen Oracle (docs/GLADIATORS.md): a masked seer with a censer staff. Also the masked stranger.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_AshenOracle (root, base)
    Body   (hip centre)      a long ash-grey robe flaring at the hem, a rope belt, a stole with purple ends
      Head (neck)            a hood, a pale bone mask with glowing purple cracks; a cyan rune on the back of the hood
      ArmL (left shoulder)
      ArmR (right shoulder)  -> CenserStaff (fist): a crooked staff, a hook at the top, a smoking censer hung from it
    LegL, LegR (hip joints)  mostly hidden by the robe; bare dark feet
Front = -Y, asset's left = +X. Hood top ~1.65 m, the staff's hook ~2.0 m (the Nobody is 1.6 m), head ~35 %.
Silhouette marks (64 px test vs the Net-Thrower, the Slinger and the Goblin): the hook-and-censer and the robe's flare.
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.5
SHOULDER_Z = 0.44   # relative to Body
NECK_Z = 0.5        # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_AshenOracle")

    # --- body: a thin chest, a long robe flaring to the ground (on the body, so it sways with him)
    body = K.box("Body", size=(0.38, 0.26, 0.52), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.12, 1.05), bevel=0.03, swatch="ash", parent=root)
    K.box("Robe", size=(0.44, 0.32, 0.5), location=(0, 0, 0.02), pivot=(0, 0, 1), taper=(0.8, 0.85),
          swatch="ash", parent=body)  # taper < 1 at the top = flaring at the hem below
    K.box("Hem", size=(0.58, 0.42, 0.06), location=(0, 0, -0.46), pivot=(0, 0, 0), swatch="charcoal", parent=body)
    K.box("Belt", size=(0.42, 0.3, 0.05), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="rope", parent=body)
    for side, x in (("L", 0.09), ("R", -0.09)):  # the stole: two strips down the front, purple ends
        K.box("Stole" + side, size=(0.07, 0.03, 0.62), location=(x, -0.145, 0.5), pivot=(0, 0, 1), swatch="charcoal",
              parent=body)
        K.box("StoleEnd" + side, size=(0.08, 0.035, 0.1), location=(x, -0.15, -0.14), pivot=(0, 0, 1), swatch="purple",
              parent=body)

    # --- head: the hood, the bone mask with purple cracks, the cyan rune behind
    head = K.box("Head", size=(0.38, 0.36, 0.4), location=(0, -0.02, NECK_Z), pivot=(0, 0, -1), bevel=0.04,
                 swatch="ink", parent=body)
    hood = K.box("Hood", size=(0.46, 0.44, 0.36), location=(0, 0.03, 0.12), pivot=(0, 0, -1), taper=(0.7, 0.8),
                 bevel=0.03, swatch="ash", parent=head)
    K.cone("HoodTip", radius=0.12, depth=0.16, verts=4, location=(0, 0.06, 0.34), pivot=(0, 0, -1), swatch="ash",
           parent=hood).rotation_euler = (math.radians(-25), 0, math.radians(45))
    mask = K.box("Mask", size=(0.3, 0.05, 0.32), location=(0, -0.19, 0.05), pivot=(0, 0, -1), taper=(0.85, 1.0),
                 swatch="bone", parent=head)
    for side, x in (("L", 0.07), ("R", -0.07)):
        K.box("EyeHole" + side, size=(0.07, 0.03, 0.05), location=(x, -0.02, 0.18), pivot=(0, 0, -1), swatch="ink",
              parent=mask)
    for k, (x, z, rot) in enumerate(((0.03, 0.06, 20), (-0.06, 0.11, -35), (0.09, 0.22, 60))):  # glowing cracks
        crack = K.box("Crack%d" % k, size=(0.02, 0.025, 0.12), location=(x, -0.03, z), pivot=(0, 0, -1),
                      swatch="purple_l", parent=mask)
        crack.rotation_euler = (0, math.radians(rot), 0)
    K.box("Rune", size=(0.12, 0.02, 0.14), location=(0, 0.24, 0.22), pivot=(0, 0, -1), swatch="player_cyan",
          parent=hood)

    # --- arms: thin, in wide sleeves, bone-pale hands (pivot at the shoulder, hanging down)
    arms = {}
    for side, x in (("L", 0.24), ("R", -0.24)):
        arm = K.box("Arm" + side, size=(0.14, 0.15, 0.36), location=(x, 0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.3, 1.25), bevel=0.02, swatch="ash", parent=body)
        K.box("Cuff" + side, size=(0.19, 0.2, 0.06), location=(0, 0, -0.33), pivot=(0, 0, 0), swatch="charcoal",
              parent=arm)
        K.box("Hand" + side, size=(0.1, 0.11, 0.1), location=(0, 0, -0.41), pivot=(0, 0, 0), swatch="skin_pale",
              parent=arm)
        arms[side] = arm

    # --- the censer staff (right fist): crooked, a hook at the top, the censer hanging from it
    staff = K.empty("CenserStaff", location=(0, 0, -0.41), parent=arms["R"])
    K.cylinder("Pole", radius=0.03, depth=1.55, verts=6, location=(0, 0, -0.4), pivot=(0, 0, -1), swatch="wood_dark",
               parent=staff)
    hook = K.box("Hook", size=(0.05, 0.05, 0.3), location=(0, 0, 1.15), pivot=(0, 0, -1), swatch="wood_dark",
                 parent=staff)
    hook.rotation_euler = (math.radians(70), 0, 0)  # bending forward over his head (iteration 2: it bent backwards)
    K.box("Chain", size=(0.02, 0.02, 0.22), location=(0, -0.28, 1.26), pivot=(0, 0, 1), swatch="iron", parent=staff)
    K.cylinder("Censer", radius=0.09, depth=0.16, verts=6, location=(0, -0.28, 1.04), pivot=(0, 0, 0),
               swatch="bronze_d", parent=staff)
    K.box("Ember", size=(0.1, 0.1, 0.05), location=(0, -0.28, 1.12), pivot=(0, 0, 0), swatch="purple_l",
          parent=staff)
    # iteration 2: no smoke cone (it read as a spearhead from the front); the purple ember is the censer's glow

    # --- legs: under the robe, bare dark feet showing
    for side, x in (("L", 0.1), ("R", -0.1)):
        leg = K.box("Leg" + side, size=(0.13, 0.14, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1), swatch="charcoal",
                    parent=root)
        K.box("Foot" + side, size=(0.13, 0.22, 0.07), location=(0, -0.04, -0.5), pivot=(0, 0, -1),
              swatch="skin_dark", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_ashenoracle", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_AshenOracle")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("ch_ashenoracle")
result = info
