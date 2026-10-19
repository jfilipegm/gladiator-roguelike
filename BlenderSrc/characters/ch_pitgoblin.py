"""CH_PitGoblin — the Pit Goblin (docs/GLADIATORS.md): tiny, chaotic scavenger with a bomb satchel.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_PitGoblin (root, base)
    Body   (hip centre)      hunched torso + rag loincloth + the bulging satchel on the back
      Head (neck)            big head, huge triangular ears, long nose, grin
      ArmL (left shoulder)
      ArmR (right shoulder)  -> Bomb (hand)
    LegL, LegR (hip joints)  short bowed legs, big feet
Front = -Y, asset's left = +X. Height ~1.05 m (the Nobody is 1.6), head ~40 %.
Silhouette marks (64 px test vs the Nobody and the Pit Cur): the ears and the satchel towering over the head.
Readability: a player-cyan rag on the satchel and a cyan headband knot (§5 player colour), seen from the camera behind.
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.30
SHOULDER_Z = 0.25   # relative to Body
NECK_Z = 0.29       # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_PitGoblin")

    # --- body: short, pot-bellied, hunched (a hump on the back), rag loincloth
    body = K.box("Body", size=(0.34, 0.28, 0.30), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.08, 0.95), bevel=0.03, swatch="skin_goblin", parent=root)
    K.box("Belly", size=(0.28, 0.10, 0.18), location=(0, -0.13, 0.04), pivot=(0, 0, -1), bevel=0.03,
          swatch="skin_goblin", parent=body)
    K.box("Hump", size=(0.30, 0.12, 0.18), location=(0, 0.14, 0.14), pivot=(0, 0, -1), taper=(0.8, 0.8), bevel=0.03,
          swatch="skin_goblin", parent=body)
    K.box("Rag", size=(0.37, 0.31, 0.15), location=(0, 0, 0.02), pivot=(0, 0, 1), taper=(1.15, 1.15),
          swatch="cloth_sack", parent=body)
    K.box("RagBelt", size=(0.37, 0.31, 0.05), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="rope", parent=body)

    # --- the satchel: bulging sack on the back, taller than the head, straps over the shoulders, junk poking out
    sack = K.box("Satchel", size=(0.42, 0.32, 0.66), location=(0, 0.30, 0.02), pivot=(0, 0, -1), taper=(0.82, 0.86),
                 bevel=0.06, swatch="cloth_sack", parent=body)
    K.box("SatchelFlap", size=(0.36, 0.30, 0.10), location=(0, 0.0, 0.63), pivot=(0, 0, -1), taper=(0.7, 0.8),
          bevel=0.03, swatch="leather_l", parent=sack)
    K.box("SatchelRag", size=(0.44, 0.34, 0.08), location=(0, 0.0, 0.30), pivot=(0, 0, -1), swatch="player_cyan",
          parent=sack)
    K.box("RagTail", size=(0.10, 0.04, 0.26), location=(0.14, 0.17, 0.36), pivot=(0, 0, 1), taper=(0.6, 1.0),
          swatch="player_cyan", parent=sack)
    for side, x in (("L", 0.12), ("R", -0.12)):
        # down the chest from the shoulder to the belt (iteration 2: they pointed forward like sticks)
        K.box("Strap" + side, size=(0.05, 0.03, 0.27), location=(x, -0.145, 0.03), pivot=(0, 0, -1), swatch="leather_d",
              parent=body)
    # junk out of the top: a bent stick and a sword hilt (the Scavenger)
    stick = K.box("JunkStick", size=(0.04, 0.04, 0.34), location=(-0.10, 0.02, 0.62), pivot=(0, 0, -1),
                  swatch="wood_light", parent=sack)
    stick.rotation_euler = (math.radians(-12), math.radians(-18), 0)
    hilt = K.box("JunkHilt", size=(0.05, 0.05, 0.22), location=(0.10, -0.02, 0.62), pivot=(0, 0, -1),
                 swatch="wood_dark", parent=sack)
    hilt.rotation_euler = (math.radians(10), math.radians(22), 0)
    K.box("JunkGuard", size=(0.18, 0.05, 0.04), location=(0, 0, 0.20), pivot=(0, 0, -1), swatch="rusty_iron",
          parent=hilt)
    K.ico("JunkBomb", radius=0.09, subdiv=1, location=(0.02, 0.06, 0.66), swatch="iron_d", parent=sack)

    # --- head: big, slightly forward (the hunch), long nose, grin, yellow eyes, huge ears
    head = K.box("Head", size=(0.42, 0.38, 0.38), location=(0, -0.04, NECK_Z), pivot=(0, 0, -1), taper=(1.08, 1.0),
                 bevel=0.06, swatch="skin_goblin", parent=body)
    K.box("Nose", size=(0.08, 0.20, 0.08), location=(0, -0.26, 0.15), pivot=(0, 0, -1), taper=(0.5, 0.4),
          swatch="skin_goblin", parent=head)
    for side, x in (("L", 0.10), ("R", -0.10)):
        K.box("Eye" + side, size=(0.09, 0.03, 0.07), location=(x, -0.19, 0.21), pivot=(0, 0, -1),
              swatch="straw", parent=head)
        K.box("Pupil" + side, size=(0.035, 0.03, 0.05), location=(x * 0.85, -0.20, 0.22), pivot=(0, 0, -1),
              swatch="ink", parent=head)
        K.box("Brow" + side, size=(0.12, 0.04, 0.04), location=(x, -0.19, 0.29), pivot=(0, 0, -1),
              swatch="leaf_d", parent=head)
    K.box("Grin", size=(0.24, 0.03, 0.06), location=(0, -0.19, 0.05), pivot=(0, 0, -1), swatch="ink", parent=head)
    for k, x in enumerate((-0.08, 0.0, 0.08)):
        K.box("Tooth%d" % k, size=(0.035, 0.035, 0.04), location=(x, -0.20, 0.07), pivot=(0, 0, -1),
              swatch="bone", parent=head)
    # ears: big flat triangles sweeping out and up (drawn in the XZ plane, thin along Y)
    for side, sx in (("L", 1), ("R", -1)):
        ear = K.extrude_poly("Ear" + side, [(0.0, 0.10), (0.0, 0.28), (sx * 0.42, 0.40)], thickness=0.05, plane="XZ",
                             location=(sx * 0.19, 0.02, 0.0), swatch="skin_goblin", parent=head)
        ear.rotation_euler = (0, 0, math.radians(sx * -14))  # swept back a little
        K.extrude_poly("EarIn" + side, [(sx * 0.03, 0.15), (sx * 0.03, 0.25), (sx * 0.30, 0.335)], thickness=0.02,
                       plane="XZ", location=(0, -0.03, 0.0), swatch="skin_tan", parent=ear)
    K.box("Headband", size=(0.44, 0.40, 0.05), location=(0, 0, 0.30), pivot=(0, 0, -1), swatch="player_cyan",
          parent=head)
    K.box("HeadbandKnot", size=(0.08, 0.08, 0.12), location=(-0.10, 0.20, 0.33), pivot=(0, 0, 1), taper=(0.6, 0.6),
          swatch="player_cyan", parent=head)

    # --- arms: long and thin with big hands (pivot at the shoulder, hanging down)
    arms = {}
    for side, x in (("L", 0.21), ("R", -0.21)):
        arm = K.box("Arm" + side, size=(0.09, 0.10, 0.30), location=(x, -0.02, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.1, 1.1), swatch="skin_goblin", parent=body)
        K.box("Wrap" + side, size=(0.11, 0.12, 0.07), location=(0, 0, -0.22), pivot=(0, 0, 0), swatch="cloth_sack",
              parent=arm)
        K.box("Hand" + side, size=(0.13, 0.13, 0.11), location=(0, 0, -0.32), pivot=(0, 0, 0), bevel=0.02,
              swatch="skin_goblin", parent=arm)
        arms[side] = arm

    # --- the bomb in the right hand: grip at the hand, fuse up
    bomb = K.empty("Bomb", location=(0, -0.04, -0.38), parent=arms["R"])
    K.ico("BombBall", radius=0.10, subdiv=1, location=(0, 0, 0), swatch="iron_d", parent=bomb)
    K.cylinder("BombCap", radius=0.035, depth=0.04, verts=6, location=(0, 0, 0.09), pivot=(0, 0, -1),
               swatch="bronze", parent=bomb)
    K.box("BombFuse", size=(0.02, 0.02, 0.07), location=(0, 0, 0.12), pivot=(0, 0, -1), swatch="rope", parent=bomb)

    # --- legs: short, bowed, big flat feet
    for side, x in (("L", 0.10), ("R", -0.10)):
        leg = K.box("Leg" + side, size=(0.11, 0.12, 0.20), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.1, 1.1),
                    swatch="skin_goblin", parent=root)
        K.box("Knee" + side, size=(0.12, 0.13, 0.08), location=(x * 0.25, 0, -0.17), pivot=(0, 0, 0),
              swatch="cloth_sack", parent=leg)
        K.box("Foot" + side, size=(0.15, 0.26, 0.08), location=(x * 0.3, -0.06, -0.30), pivot=(0, 0, -1), bevel=0.02,
              taper=(0.8, 0.9), swatch="skin_goblin", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_pitgoblin", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_PitGoblin")
info["blend"] = K.save_blend("ch_pitgoblin")
result = info
