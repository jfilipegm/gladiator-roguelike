"""CH_GoldenIdol — the Golden Idol (docs/GLADIATORS.md): a champion so rich he had himself gilded.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_GoldenIdol (root, base)
    Body   (hip centre)      a round gilded body, a crimson sash, a gold belt with a gem
      Head (neck)            a golden mask (calm, eyes slits glowing player cyan) and, behind it, the sun-crown:
                             a golden disc ringed with spikes, its back painted player cyan
      ArmL (left shoulder)   -> Sceptre (fist): a gold rod with a big golden orb on top
      ArmR (right shoulder)  the coin-flicking hand, a gold ring
    LegL, LegR (hip joints)  gilded legs, crimson boots
Front = -Y, asset's left = +X. Crown top ~1.75 m (the Nobody is 1.6 m), head ~36 %.
Silhouette marks (64 px test vs the Nobody, the Champion and the Beast Lord): the spiked disc behind his head.
Readability: from the camera behind him, the disc's cyan back says "that's me".
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.48
SHOULDER_Z = 0.5    # relative to Body
NECK_Z = 0.56       # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_GoldenIdol")

    # --- body: round and gleaming, a crimson sash across it, a belt with a gem
    body = K.box("Body", size=(0.5, 0.36, 0.58), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.12, 1.06), bevel=0.07, swatch="gold_l", parent=root)  # iteration 2: brighter (gold read ochre)
    sash = K.box("Sash", size=(0.12, 0.4, 0.6), location=(0.02, 0, 0.06), pivot=(0, 0, -1), swatch="crimson",
                 parent=body)
    sash.rotation_euler = (0, math.radians(-32), 0)  # iteration 2: shorter, inside the body's outline
    K.box("Belt", size=(0.54, 0.4, 0.08), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="gold_d", parent=body)
    K.box("Gem", size=(0.1, 0.04, 0.1), location=(0, -0.21, -0.01), pivot=(0, 0, -1), swatch="crimson_d", parent=body)
    K.box("Skirt", size=(0.54, 0.4, 0.2), location=(0, 0, 0.02), pivot=(0, 0, 1), taper=(0.95, 0.95),
          swatch="gold_d", parent=body)

    # --- head: the golden mask; behind it the sun-crown
    head = K.box("Head", size=(0.42, 0.4, 0.42), location=(0, -0.02, NECK_Z), pivot=(0, 0, -1), bevel=0.06,
                 swatch="gold_l", parent=body)
    K.box("Eyes", size=(0.26, 0.03, 0.04), location=(0, -0.205, 0.22), pivot=(0, 0, -1), swatch="glow_cyan",
          parent=head)
    K.box("Smile", size=(0.14, 0.03, 0.03), location=(0, -0.205, 0.1), pivot=(0, 0, -1), swatch="gold_d", parent=head)
    K.box("Laurel", size=(0.46, 0.44, 0.06), location=(0, 0, 0.36), pivot=(0, 0, -1), swatch="gold_d", parent=head)
    crown = K.empty("SunCrown", location=(0, 0.24, 0.3), parent=head)
    K.cylinder("Disc", radius=0.36, depth=0.05, verts=12, location=(0, 0, 0), pivot=(0, 0, 0), axis="Y",
               swatch="gold", parent=crown)
    K.cylinder("DiscBack", radius=0.3, depth=0.02, verts=12, location=(0, 0.035, 0), pivot=(0, 0, 0), axis="Y",
               swatch="player_cyan", parent=crown)
    for k in range(11):  # spikes round the rim, none straight down (the neck)
        a = math.radians(-150 + k * 30)
        spike = K.cone("Ray%d" % k, radius=0.07, depth=0.22 if k % 2 else 0.3, verts=4,
                       location=(math.sin(a) * 0.34, 0, math.cos(a) * 0.34), pivot=(0, 0, -1), swatch="gold_l",
                       parent=crown)
        spike.rotation_euler = (0, a, 0)

    # --- arms: gilded, bracelets, fists (pivot at the shoulder, hanging down)
    arms = {}
    for side, x in (("L", 0.31), ("R", -0.31)):
        arm = K.box("Arm" + side, size=(0.15, 0.16, 0.38), location=(x, 0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.05, 1.05), bevel=0.03, swatch="gold_l", parent=body)
        K.box("Pauldron" + side, size=(0.2, 0.22, 0.1), location=(0, 0, 0.02), pivot=(0, 0, 0), bevel=0.03,
              swatch="gold_l", parent=arm)
        K.box("Bangle" + side, size=(0.17, 0.18, 0.06), location=(0, 0, -0.28), pivot=(0, 0, 0), swatch="crimson",
              parent=arm)
        K.box("Fist" + side, size=(0.15, 0.15, 0.12), location=(0, 0, -0.4), pivot=(0, 0, 0), bevel=0.03,
              swatch="gold_l", parent=arm)
        arms[side] = arm

    # --- the sceptre (left fist): upright, a big golden orb on top
    sceptre = K.empty("Sceptre", location=(0, 0, -0.4), parent=arms["L"])
    K.cylinder("Rod", radius=0.035, depth=1.0, verts=6, location=(0, 0, -0.3), pivot=(0, 0, -1), swatch="gold_d",
               parent=sceptre)
    K.ico("Orb", radius=0.12, subdiv=1, location=(0, 0, 0.78), swatch="gold_l", parent=sceptre)
    K.box("OrbGem", size=(0.08, 0.08, 0.08), location=(0, -0.11, 0.78), pivot=(0, 0, 0), swatch="crimson",
          parent=sceptre)

    # --- legs: gilded, crimson boots
    for side, x in (("L", 0.13), ("R", -0.13)):
        leg = K.box("Leg" + side, size=(0.17, 0.19, 0.36), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.05, 1.05),
                    swatch="gold_d", parent=root)
        K.box("Boot" + side, size=(0.18, 0.28, 0.14), location=(0, -0.04, -0.48), pivot=(0, 0, -1), bevel=0.02,
              swatch="crimson_d", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_goldenidol", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_GoldenIdol")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("ch_goldenidol")
result = info
