"""CH_NetWeaver — the Net-Weaver (docs/GLADIATORS.md): a retiarius, a fisherman with a net and a trident.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_NetWeaver (root, base)
    Body   (hip centre)      bare chest, a sea-blue tunic skirt, a wide belt; the bronze galerus rising above
                             his left shoulder; the net hanging down his back like a cape, cyan cords, lead weights
      Head (neck)            bare head: a knotted cyan headband, a dark topknot, a short beard, cyan eyes
      ArmL (left shoulder)   wrapped manica (cloth bands) to the fist
      ArmR (right shoulder)  -> Trident (fist): held slanted forward, the three prongs ahead of him at head height
    LegL, LegR (hip joints)  wrapped shins, sandals
Front = -Y, asset's left = +X. Head top ~1.55 m, galerus top ~1.4 m (the Nobody is 1.6 m), head ~37 %.
Silhouette marks (64 px test vs the Net-Thrower enemy, the Thunder Champion and the Nobody): the galerus over one
shoulder, the net cape, the trident slanted forward (the Champion's spear is upright).
Readability: the headband, the eyes and the net's cords are player cyan; from the camera behind, the net cape.
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.50
SHOULDER_Z = 0.44   # relative to Body
NECK_Z = 0.50       # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_NetWeaver")

    # --- body: a lean, tanned torso, a sea-blue tunic skirt, a wide leather belt with a bronze plate
    body = K.box("Body", size=(0.4, 0.25, 0.52), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.25, 1.08), bevel=0.03, swatch="skin_mid", parent=root)
    K.box("Belt", size=(0.44, 0.29, 0.1), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("Buckle", size=(0.14, 0.03, 0.08), location=(0, -0.15, 0.01), pivot=(0, 0, -1), swatch="bronze_l",
          parent=body)
    K.box("Skirt", size=(0.44, 0.29, 0.26), location=(0, 0, 0.02), pivot=(0, 0, 1), taper=(0.9, 0.94),
          swatch="water", parent=body)
    # the galerus: a tall bronze plate standing up from the left shoulder, flaring outward at the top
    # iteration 2: bigger and steel (bronze was lost against the tanned skin and the rope at 64 px)
    gal = K.box("Galerus", size=(0.08, 0.3, 0.5), location=(0.29, 0.0, 0.34), pivot=(0, 0, -1),
                taper=(1.0, 1.3), bevel=0.015, swatch="steel_l", parent=body)
    gal.rotation_euler = (0, math.radians(-20), 0)
    K.box("GaleRim", size=(0.1, 0.4, 0.06), location=(0, 0, 0.48), pivot=(0, 0, -1), swatch="bronze_d", parent=gal)
    K.box("GaleStrap", size=(0.44, 0.27, 0.05), location=(0.02, 0, 0.36), pivot=(0, 0, -1), swatch="leather",
          parent=body)
    # the net, slung from the left shoulder down his back like a cape: rope with cyan cords and lead weights
    net = K.box("Net", size=(0.5, 0.04, 0.62), location=(0.04, 0.15, 0.5), pivot=(0, 0, 1), taper=(0.75, 1.0),
                swatch="rope", parent=body)
    net.rotation_euler = (math.radians(10), math.radians(8), 0)
    for k, x in enumerate((-0.14, 0.0, 0.14)):   # cords down the back, cyan for the camera behind him
        K.box("NetCordV%d" % k, size=(0.03, 0.02, 0.6), location=(x, 0.025, -0.3), pivot=(0, 0, 0),
              swatch="player_cyan", parent=net)
    for k, z in enumerate((-0.18, -0.4)):        # cords across
        K.box("NetCordH%d" % k, size=(0.48, 0.02, 0.03), location=(0, 0.025, z), pivot=(0, 0, 0),
              swatch="player_cyan", parent=net)
    for k, x in enumerate((-0.2, -0.07, 0.07, 0.2)):  # lead weights along the hem
        K.ico("NetWeight%d" % k, radius=0.04, subdiv=1, location=(x, 0.0, -0.64), swatch="iron", parent=net)

    # --- head: bare, a knotted headband, a topknot, a short beard
    head = K.box("Head", size=(0.4, 0.38, 0.4), location=(0, -0.02, NECK_Z), pivot=(0, 0, -1), bevel=0.04,
                 swatch="skin_mid", parent=body)
    K.box("Eyes", size=(0.24, 0.03, 0.05), location=(0, -0.195, 0.2), pivot=(0, 0, -1), swatch="glow_cyan",
          parent=head)
    K.box("Hair", size=(0.42, 0.4, 0.12), location=(0, 0.01, 0.32), pivot=(0, 0, -1), taper=(0.85, 0.85),
          swatch="charcoal", parent=head)
    K.box("HairBack", size=(0.42, 0.12, 0.26), location=(0, 0.15, 0.14), pivot=(0, 0, -1), swatch="charcoal",
          parent=head)
    K.box("Band", size=(0.43, 0.41, 0.06), location=(0, 0.0, 0.29), pivot=(0, 0, -1), swatch="player_cyan",
          parent=head)
    for k, (y, z) in enumerate(((0.24, 0.27), (0.27, 0.2))):  # the band's knot tails behind
        K.box("BandTail%d" % k, size=(0.05, 0.12, 0.04), location=(0.06 - 0.1 * k, y, z), pivot=(0, 0, 0),
              swatch="player_cyan", parent=head)
    K.cylinder("Topknot", radius=0.08, depth=0.12, verts=6, location=(0, 0.04, 0.42), pivot=(0, 0, -1),
               taper=0.7, swatch="charcoal", parent=head)
    K.box("Beard", size=(0.22, 0.05, 0.08), location=(0, -0.19, 0.0), pivot=(0, 0, -1), taper=(0.8, 1.0),
          swatch="charcoal", parent=head)  # iteration 2: a chin beard (a full one blacked out the face)

    # --- arms: the left wrapped in the manica (cloth bands), the right bare with a bracer
    arms = {}
    for side, x in (("L", 0.26), ("R", -0.26)):
        arm = K.box("Arm" + side, size=(0.13, 0.14, 0.38), location=(x, 0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.05, 1.05), bevel=0.02, swatch="skin_mid", parent=body)
        K.box("Fist" + side, size=(0.13, 0.13, 0.11), location=(0, 0, -0.39), pivot=(0, 0, 0), bevel=0.02,
              swatch="skin_mid", parent=arm)
        arms[side] = arm
    for k in range(4):
        K.box("Manica%d" % k, size=(0.155, 0.165, 0.07), location=(0, 0, -0.05 - 0.085 * k), pivot=(0, 0, 0),
              swatch="cloth_white" if k % 2 == 0 else "leather_l", parent=arms["L"])
    K.box("BracerR", size=(0.15, 0.16, 0.12), location=(0, 0, -0.26), pivot=(0, 0, 0), swatch="leather",
          parent=arms["R"])

    # --- the trident (right fist): slanted forward, the prongs ahead of him at head height
    tri = K.empty("Trident", location=(0, 0, -0.39), parent=arms["R"])
    tri.rotation_euler = (math.radians(40), 0, 0)  # tip forward (-Y) and up (iteration 2: -55 slanted it backward)
    K.cylinder("Shaft", radius=0.03, depth=1.55, verts=6, location=(0, 0, -0.45), pivot=(0, 0, -1),
               swatch="wood", parent=tri)
    K.box("Binding", size=(0.075, 0.075, 0.08), location=(0, 0, 1.06), pivot=(0, 0, 0), swatch="glow_cyan", parent=tri)
    K.box("Bar", size=(0.36, 0.06, 0.05), location=(0, 0, 1.1), pivot=(0, 0, -1), swatch="steel", parent=tri)
    for k, x in enumerate((-0.16, 0.0, 0.16)):
        h = 0.42 if x == 0.0 else 0.34
        K.box("Prong%d" % k, size=(0.04, 0.04, h), location=(x, 0, 1.15), pivot=(0, 0, -1), swatch="steel_l",
              parent=tri)
        K.cone("Point%d" % k, radius=0.05, depth=0.1, verts=4, location=(x, 0, 1.15 + h), pivot=(0, 0, -1),
               swatch="steel_l", parent=tri)

    # --- legs: wrapped shins, sandals
    for side, x in (("L", 0.11), ("R", -0.11)):
        leg = K.box("Leg" + side, size=(0.15, 0.17, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.05, 1.05),
                    swatch="skin_mid", parent=root)
        K.box("Wrap" + side, size=(0.165, 0.185, 0.18), location=(0, -0.005, -0.3), pivot=(0, 0, 0),
              swatch="cloth_white", parent=leg)
        K.box("Sandal" + side, size=(0.16, 0.26, 0.07), location=(0, -0.04, -0.5), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_netweaver", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_NetWeaver")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("ch_netweaver")
result = info
