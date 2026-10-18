"""CH_Nobody — the starter gladiator (CLAUDE.md §6.7): nameless pit fighter, rusty gladius + scrap shield.

Rigid parts with pivots at the joints (PartAnimator drives them in Unity):
  CH_Nobody (root, base)
    Body   (hip centre)      torso + loincloth
      Head (neck)
      ArmL (left shoulder)   -> Shield (hand)
      ArmR (right shoulder)  -> Gladius (hand / grip)
    LegL, LegR (hip joints)  children of the root so feet stay planted while the body bobs
Front = -Y, asset's left = +X. Height ~1.6 m, head ~37 %.
Readability: cyan scarf = player colour language (§5).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.40
SHOULDER_Z = 0.46   # relative to Body
NECK_Z = 0.55       # relative to Body


def build():
    K.clear_scene()
    root = K.empty("CH_Nobody")

    # --- body: torso (tapered, wider shoulders) + loincloth skirt overlapping the leg tops
    body = K.box("Body", size=(0.44, 0.30, 0.56), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.12, 1.05), bevel=0.03, swatch="cloth_white", parent=root)
    K.box("Belt", size=(0.47, 0.33, 0.08), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("Loincloth", size=(0.40, 0.28, 0.16), location=(0, 0, 0.0), pivot=(0, 0, 1), taper=(1.1, 1.1),
          swatch="leather", parent=body)
    # scrap pauldron on the shield side + player-cyan scarf
    K.box("Pauldron", size=(0.20, 0.30, 0.10), location=(0.22, 0, 0.50), pivot=(0, 0, -1), taper=(0.8, 0.9),
          bevel=0.02, swatch="rusty_iron", parent=body)
    K.box("Scarf", size=(0.40, 0.33, 0.08), location=(0, 0, 0.52), pivot=(0, 0, -1), bevel=0.02,
          swatch="player_cyan", parent=body)
    # long scarf tail down the back: the camera sits behind the player, so this is the "that's me" mark
    K.box("ScarfTail", size=(0.14, 0.05, 0.40), location=(-0.08, 0.17, 0.60), pivot=(0, 0, 1), taper=(0.8, 1.0),
          swatch="player_cyan", parent=body)

    # --- head: big chibi block, face, hair cap, eyes, nose
    head = K.box("Head", size=(0.50, 0.46, 0.52), location=(0, 0, NECK_Z), pivot=(0, 0, -1), bevel=0.06,
                 swatch="skin_light", parent=body)
    K.box("Hair", size=(0.53, 0.49, 0.20), location=(0, 0.02, 0.36), pivot=(0, 0, -1), bevel=0.05,
          taper=(0.9, 0.9), swatch="mud_dark", parent=head)
    K.box("HairBack", size=(0.50, 0.14, 0.30), location=(0, 0.20, 0.12), pivot=(0, 0, -1), bevel=0.03,
          swatch="mud_dark", parent=head)
    for side, x in (("L", 0.11), ("R", -0.11)):
        K.box("Eye" + side, size=(0.07, 0.03, 0.10), location=(x, -0.235, 0.24), pivot=(0, 0, -1),
              swatch="ink", parent=head)
    K.box("Nose", size=(0.08, 0.06, 0.08), location=(0, -0.25, 0.16), pivot=(0, 0, -1), swatch="skin_mid", parent=head)
    K.box("Brow", size=(0.34, 0.04, 0.04), location=(0, -0.24, 0.36), pivot=(0, 0, -1), swatch="mud_dark", parent=head)
    K.box("Headband", size=(0.54, 0.50, 0.07), location=(0, 0, 0.38), pivot=(0, 0, -1), swatch="player_cyan",
          parent=head)
    K.box("HeadbandKnot", size=(0.10, 0.10, 0.16), location=(0.12, 0.26, 0.42), pivot=(0, 0, 1), taper=(0.6, 0.6),
          swatch="player_cyan", parent=head)

    # --- arms (pivot at shoulder, hanging down), hands
    arms = {}
    for side, x in (("L", 0.28), ("R", -0.28)):
        arm = K.box("Arm" + side, size=(0.14, 0.15, 0.36), location=(x, 0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.1, 1.1), bevel=0.02, swatch="skin_light", parent=body)
        K.box("Wrap" + side, size=(0.16, 0.17, 0.10), location=(0, 0, -0.26), pivot=(0, 0, 0), swatch="leather",
              parent=arm)
        K.ico("Hand" + side, radius=0.085, subdiv=1, location=(0, 0, -0.38), swatch="skin_light", parent=arm)
        arms[side] = arm

    # --- shield (left hand), facing outwards (+X) and slightly forward
    shield = K.cylinder("Shield", radius=0.27, depth=0.06, verts=8, location=(0.09, -0.03, -0.32), pivot=(0, 0, 0),
                        axis="X", swatch="wood", parent=arms["L"])
    K.cylinder("ShieldRim", radius=0.29, depth=0.04, verts=8, location=(0.0, 0, 0), pivot=(0, 0, 0), axis="X",
               swatch="rusty_iron", parent=shield)
    K.cylinder("ShieldBoss", radius=0.08, depth=0.08, verts=6, location=(0.04, 0, 0), pivot=(0, 0, 0), axis="X",
               swatch="rusty_iron", parent=shield)
    K.box("ShieldPlank", size=(0.07, 0.06, 0.52), location=(0.035, 0.08, 0), pivot=(0, 0, 0), swatch="wood_light",
          parent=shield)

    # --- gladius (right hand): grip at the origin, blade forward (-Y), chunky and oversized
    sword = K.empty("Gladius", location=(0, 0, -0.38), parent=arms["R"])
    # leaf-shaped gladius blade: edges up/down, flats left/right
    K.extrude_poly("Blade", [(-0.09, 0.045), (-0.50, 0.06), (-0.72, 0.035), (-0.84, 0.0), (-0.72, -0.035),
                             (-0.50, -0.06), (-0.09, -0.045)], thickness=0.03, plane="YZ",
                   swatch="rusty_iron", parent=sword)
    K.box("Fuller", size=(0.034, 0.46, 0.02), location=(0, -0.13, 0), pivot=(0, 1, 0), swatch="steel", parent=sword)
    K.box("Guard", size=(0.06, 0.05, 0.20), location=(0, -0.08, 0), pivot=(0, 0, 0), swatch="bronze_d", parent=sword)
    K.box("Grip", size=(0.05, 0.16, 0.05), location=(0, 0.02, 0), pivot=(0, 0, 0), swatch="wood_dark", parent=sword)
    K.ico("Pommel", radius=0.04, subdiv=1, location=(0, 0.11, 0), swatch="bronze_d", parent=sword)

    # --- legs (root children; pivot at hip joint), boots forward
    for side, x in (("L", 0.11), ("R", -0.11)):
        leg = K.box("Leg" + side, size=(0.16, 0.18, 0.34), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.1, 1.1),
                    swatch="skin_light", parent=root)
        K.box("Shin" + side, size=(0.17, 0.19, 0.16), location=(0, 0, -0.20), pivot=(0, 0, 0), swatch="leather",
              parent=leg)
        K.box("Foot" + side, size=(0.17, 0.26, 0.10), location=(0, -0.04, -0.40), pivot=(0, 0, -1), bevel=0.02,
              swatch="leather_d", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_nobody", size=420)
info["fbx"] = K.export_fbx(root, "Characters/CH_Nobody")
info["blend"] = K.save_blend("ch_nobody")
result = info
