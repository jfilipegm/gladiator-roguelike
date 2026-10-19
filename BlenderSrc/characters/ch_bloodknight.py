"""CH_BloodKnight — the Blood Knight (docs/GLADIATORS.md): a fallen knight in blackened plate with a greatsword.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_BloodKnight (root, base)
    Body   (hip centre)      plate torso, crimson tabard, faulds, spiked pauldrons, the tattered cape
      Head (neck)            horned great-helm with a glowing T visor
      ArmL (left shoulder)
      ArmR (right shoulder)  -> Greatsword (hand / grip): blade forward (-Y), held low
    LegL, LegR (hip joints)
Front = -Y, asset's left = +X. Helm top ~1.65 m, horns ~1.9 m (the Nobody is 1.6 m), head ~34 %.
Silhouette marks (64 px test vs the Nobody, the Goblin, the Brute and Gorm): the horns, the cape and the blade.
Readability: the visor is player cyan from the front; from the camera behind, a pale cyan sword sigil on the cape.
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.48
SHOULDER_Z = 0.52   # relative to Body
NECK_Z = 0.60       # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_BloodKnight")

    # --- body: broad plate torso, crimson tabard, belt, faulds
    body = K.box("Body", size=(0.50, 0.34, 0.62), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.24, 1.08), bevel=0.03, swatch="iron_d", parent=root)
    K.box("Tabard", size=(0.30, 0.04, 0.74), location=(0, -0.185, 0.54), pivot=(0, 0, 1), taper=(0.8, 1.0),
          swatch="crimson", parent=body)
    K.box("Gorget", size=(0.36, 0.30, 0.10), location=(0, 0, 0.56), pivot=(0, 0, -1), swatch="iron", parent=body)
    K.box("Belt", size=(0.54, 0.38, 0.07), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("Buckle", size=(0.09, 0.03, 0.07), location=(0, -0.195, 0.0), pivot=(0, 0, -1), swatch="gold_d", parent=body)
    K.box("Faulds", size=(0.58, 0.40, 0.20), location=(0, 0, 0.02), pivot=(0, 0, 1), taper=(0.9, 0.92),
          swatch="iron", parent=body)

    # spiked pauldrons on the body (they don't swing with the arms)
    for side, sx in (("L", 1), ("R", -1)):
        p = K.box("Pauldron" + side, size=(0.28, 0.40, 0.16), location=(sx * 0.31, 0, 0.47), pivot=(0, 0, -1),
                  taper=(0.72, 0.82), bevel=0.03, swatch="iron", parent=body)
        K.box("PauldronRim" + side, size=(0.30, 0.42, 0.04), location=(0, 0, 0.0), pivot=(0, 0, -1),
              swatch="crimson_d", parent=p)
        for k, y in enumerate((-0.08, 0.08)):
            spike = K.cone("Spike%s%d" % (side, k), radius=0.045, depth=0.17, verts=6, location=(sx * 0.03, y, 0.13),
                           swatch="steel", parent=p)
            spike.rotation_euler = (0, math.radians(sx * 20), 0)

    # the cape: hangs from the shoulders, wider at the bottom, tattered hem, a pale sigil for the camera behind
    # iteration 2: flared wider and swung back off the body so it reads in the side and 3/4 silhouettes
    cape = K.box("Cape", size=(0.74, 0.05, 0.76), location=(0, 0.20, 0.58), pivot=(0, 0, 1), taper=(0.6, 1.0),
                 swatch="crimson_d", parent=body)
    cape.rotation_euler = (math.radians(14), 0, 0)
    for k, (x, h) in enumerate(((-0.22, 0.16), (-0.05, 0.22), (0.12, 0.14), (0.26, 0.19))):
        K.extrude_poly("Tatter%d" % k, [(-0.07, 0.0), (0.07, 0.0), (0.0, -h)], thickness=0.05, plane="XZ",
                       location=(x, 0.0, -0.74), swatch="crimson_d", parent=cape)
    K.box("SigilBlade", size=(0.05, 0.02, 0.34), location=(0, 0.03, -0.52), pivot=(0, 0, -1),
          swatch="player_cyan", parent=cape)
    K.box("SigilGuard", size=(0.18, 0.02, 0.04), location=(0, 0.03, -0.30), pivot=(0, 0, -1),
          swatch="player_cyan", parent=cape)

    # --- head: horned great-helm, T visor glowing player cyan
    head = K.box("Head", size=(0.50, 0.48, 0.56), location=(0, -0.02, NECK_Z), pivot=(0, 0, -1), taper=(0.92, 0.95),
                 bevel=0.05, swatch="iron_d", parent=body)
    K.box("HelmRim", size=(0.52, 0.50, 0.06), location=(0, 0, 0.46), pivot=(0, 0, -1), swatch="iron", parent=head)
    K.box("VisorSlit", size=(0.36, 0.03, 0.05), location=(0, -0.245, 0.29), pivot=(0, 0, -1), swatch="glow_cyan",
          parent=head)
    K.box("VisorDrop", size=(0.05, 0.03, 0.16), location=(0, -0.245, 0.14), pivot=(0, 0, -1), swatch="glow_cyan",
          parent=head)
    K.box("Ridge", size=(0.05, 0.05, 0.14), location=(0, -0.245, 0.35), pivot=(0, 0, -1), swatch="iron", parent=head)
    for side, sx in (("L", 1), ("R", -1)):
        # iteration 2: thicker, so they survive the 64 px test
        horn = K.cylinder("Horn" + side, radius=0.1, depth=0.34, verts=6, taper=0.55, location=(sx * 0.21, 0.03, 0.38),
                          swatch="bone", parent=head)
        horn.rotation_euler = (0, math.radians(sx * 58), 0)
        tip = K.cone("HornTip" + side, radius=0.065, depth=0.28, verts=6, location=(sx * 0.46, 0.03, 0.55),
                     swatch="bone", parent=head)
        tip.rotation_euler = (0, math.radians(sx * -6), 0)

    # --- arms: plate, elbow cops, gauntlets (pivot at the shoulder, hanging down)
    arms = {}
    for side, x in (("L", 0.31), ("R", -0.31)):
        arm = K.box("Arm" + side, size=(0.16, 0.17, 0.40), location=(x, 0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.1, 1.1), bevel=0.02, swatch="iron_d", parent=body)
        K.box("Elbow" + side, size=(0.19, 0.20, 0.08), location=(0, 0, -0.19), pivot=(0, 0, 0), swatch="iron",
              parent=arm)
        K.box("Gauntlet" + side, size=(0.20, 0.21, 0.13), location=(0, 0, -0.32), pivot=(0, 0, 0), swatch="iron",
              parent=arm)
        K.box("Hand" + side, size=(0.16, 0.16, 0.13), location=(0, 0, -0.43), pivot=(0, 0, 0), bevel=0.02,
              swatch="iron_d", parent=arm)
        arms[side] = arm

    # --- the Crimson Greatsword (right hand): grip at the origin, blade forward (-Y) tilted 15° down, a blood groove
    sword = K.empty("Greatsword", location=(0, 0, -0.43), parent=arms["R"])
    sword.rotation_euler = (math.radians(15), 0, 0)
    # iteration 2: a broader blade (0.2 m) that reads as a greatsword, not a spear, at 64 px
    K.extrude_poly("Blade", [(-0.15, 0.10), (-1.22, 0.10), (-1.42, 0.0), (-1.22, -0.10), (-0.15, -0.10)],
                   thickness=0.045, plane="YZ", swatch="steel", parent=sword)
    K.box("Fuller", size=(0.05, 1.0, 0.045), location=(0, -0.2, 0), pivot=(0, 1, 0), swatch="crimson", parent=sword)
    K.box("Guard", size=(0.08, 0.08, 0.50), location=(0, -0.12, 0), pivot=(0, 0, 0), swatch="iron_d", parent=sword)
    K.box("Gem", size=(0.08, 0.08, 0.08), location=(0, -0.12, 0), pivot=(0, 0, 0), swatch="blood", parent=sword)
    K.box("Grip", size=(0.05, 0.26, 0.05), location=(0, 0.03, 0), pivot=(0, 0, 0), swatch="leather_d", parent=sword)
    K.ico("Pommel", radius=0.05, subdiv=1, location=(0, 0.18, 0), swatch="iron", parent=sword)

    # --- legs: plate, knee cops, sabatons
    for side, x in (("L", 0.13), ("R", -0.13)):
        leg = K.box("Leg" + side, size=(0.18, 0.20, 0.40), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.1, 1.1),
                    swatch="iron_d", parent=root)
        K.box("Knee" + side, size=(0.20, 0.22, 0.09), location=(0, -0.01, -0.21), pivot=(0, 0, 0), swatch="iron",
              parent=leg)
        K.box("Foot" + side, size=(0.19, 0.30, 0.10), location=(0, -0.05, -0.48), pivot=(0, 0, -1), bevel=0.02,
              taper=(0.85, 0.9), swatch="iron_d", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_bloodknight", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_BloodKnight")
info["blend"] = K.save_blend("ch_bloodknight")
result = info
