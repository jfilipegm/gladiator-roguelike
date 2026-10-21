"""CH_Beastmaster — the Beastmaster (docs/GLADIATORS.md): a frontier hunter in a wolf-head hood and a fur mantle.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged:
  CH_Beastmaster (root, base)
    Body   (hip centre)      leather jerkin, a shaggy fur mantle over the shoulders (cyan-tipped at the back), a
                             strap across the chest carrying a curved hunting horn
      Head (neck)            face, cyan eyes, the wolf-head hood: a grey wolf's skull and snout over his brow, two ears up
      ArmL (left shoulder)   fur cuff
      ArmR (right shoulder)  -> HuntingSpear (fist): short, held low and forward
    LegL, LegR (hip joints)  leather trousers, fur boots
Front = -Y, asset's left = +X. Ear tips ~1.72 m (the Nobody is 1.6 m), head ~37 %.
Silhouette marks (64 px test vs the Nobody, the Net-Weaver and the Pit Cur): the hood's ears and snout, the mantle's
bulk. Readability: the eyes, the horn's band and the mantle's tips are player cyan.
    Tools/bheadless BlenderSrc/characters/ch_beastmaster.py [export=0]
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
    root = K.empty("CH_Beastmaster")

    # --- body: a leather jerkin, a belt, the fur mantle, the horn on its strap
    body = K.box("Body", size=(0.42, 0.27, 0.52), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.2, 1.1), bevel=0.03, swatch="leather_l", parent=root)  # iteration 2: lighter (too dark on dirt)
    K.box("Belt", size=(0.46, 0.31, 0.08), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="leather_d", parent=body)
    K.box("Skirt", size=(0.46, 0.31, 0.2), location=(0, 0, 0.03), pivot=(0, 0, 1), taper=(0.92, 0.95),
          swatch="leather_d", parent=body)
    mantle = K.box("Mantle", size=(0.66, 0.42, 0.24), location=(0, 0.02, 0.32), pivot=(0, 0, -1), taper=(0.75, 0.8),
                   bevel=0.04, swatch="mud_dark", parent=body)
    K.box("MantleBack", size=(0.58, 0.08, 0.42), location=(0, 0.19, 0.2), pivot=(0, 0, 1), taper=(1.0, 1.0),
          swatch="mud_dark", parent=mantle)
    for k, x in enumerate((-0.2, -0.07, 0.07, 0.2)):  # cyan-tipped fur at the back: the camera behind sees it
        K.box("FurTip%d" % k, size=(0.09, 0.05, 0.1), location=(x, 0.24, -0.2), pivot=(0, 0, 1), taper=(0.3, 1.0),
              swatch="player_cyan", parent=mantle)
    strap = K.box("Strap", size=(0.06, 0.3, 0.62), location=(0, 0, 0.26), pivot=(0, 0, 0), swatch="leather_l",
                  parent=body)
    strap.rotation_euler = (0, math.radians(38), 0)
    horn = K.empty("Horn", location=(-0.14, -0.17, 0.16), parent=body)
    K.box("HornBell", size=(0.1, 0.1, 0.12), location=(0.0, 0, 0.0), pivot=(0, 0, 0), taper=(1.4, 1.4),
          swatch="bone", parent=horn)
    seg = K.box("HornBody", size=(0.07, 0.07, 0.18), location=(0.07, 0, 0.08), pivot=(0, 0, -1), taper=(0.6, 0.6),
                swatch="bone", parent=horn)
    seg.rotation_euler = (0, math.radians(-60), 0)
    K.box("HornBand", size=(0.11, 0.11, 0.03), location=(0, 0, 0.05), pivot=(0, 0, 0), swatch="player_cyan",
          parent=horn)

    # --- head: the face, then the wolf-head hood over it
    head = K.box("Head", size=(0.4, 0.38, 0.4), location=(0, -0.02, NECK_Z), pivot=(0, 0, -1), bevel=0.04,
                 swatch="skin_mid", parent=body)
    K.box("Eyes", size=(0.24, 0.03, 0.05), location=(0, -0.195, 0.18), pivot=(0, 0, -1), swatch="glow_cyan",
          parent=head)
    K.box("Beard", size=(0.3, 0.05, 0.1), location=(0, -0.19, 0.0), pivot=(0, 0, -1), taper=(0.8, 1.0),
          swatch="wood_dark", parent=head)
    hood = K.box("Hood", size=(0.46, 0.46, 0.22), location=(0, 0.02, 0.26), pivot=(0, 0, -1), taper=(0.85, 0.9),
                 bevel=0.03, swatch="stone_grey", parent=head)
    K.box("HoodBack", size=(0.46, 0.14, 0.34), location=(0, 0.17, -0.18), pivot=(0, 0, -1), swatch="stone_grey",
          parent=hood)
    K.box("Snout", size=(0.2, 0.22, 0.12), location=(0, -0.26, 0.04), pivot=(0, 0, -1), taper=(0.75, 0.9),
          swatch="ash", parent=hood)
    K.box("SnoutNose", size=(0.08, 0.04, 0.05), location=(0, -0.37, 0.1), pivot=(0, 0, -1), swatch="ink", parent=hood)
    K.box("Fangs", size=(0.16, 0.03, 0.04), location=(0, -0.35, 0.0), pivot=(0, 0, -1), swatch="bone", parent=hood)
    for side, x in (("L", 0.13), ("R", -0.13)):
        K.box("Ear" + side, size=(0.12, 0.08, 0.26), location=(x * 1.1, 0.02, 0.2), pivot=(0, 0, -1), taper=(0.15, 0.4),
              swatch="slate", parent=hood)
        K.box("HoodEye" + side, size=(0.05, 0.02, 0.03), location=(x * 0.6, -0.235, 0.14), pivot=(0, 0, -1),
              swatch="ink", parent=hood)

    # --- arms: bare forearms, fur cuffs
    arms = {}
    for side, x in (("L", 0.27), ("R", -0.27)):
        arm = K.box("Arm" + side, size=(0.13, 0.14, 0.38), location=(x, 0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.05, 1.05), bevel=0.02, swatch="skin_mid", parent=body)
        K.box("Cuff" + side, size=(0.16, 0.17, 0.12), location=(0, 0, -0.26), pivot=(0, 0, 0), swatch="mud_dark",
              parent=arm)
        K.box("Fist" + side, size=(0.13, 0.13, 0.11), location=(0, 0, -0.39), pivot=(0, 0, 0), bevel=0.02,
              swatch="skin_mid", parent=arm)
        arms[side] = arm

    # --- a short hunting spear (right fist), held low and forward
    spear = K.empty("HuntingSpear", location=(0, 0, -0.39), parent=arms["R"])
    spear.rotation_euler = (math.radians(115), 0, 0)  # iteration 2: tip low (the Net-Weaver holds his level)
    K.cylinder("Shaft", radius=0.028, depth=1.15, verts=6, location=(0, 0, -0.35), pivot=(0, 0, -1),
               swatch="wood", parent=spear)
    K.box("Tassel", size=(0.05, 0.05, 0.08), location=(0, 0, 0.74), pivot=(0, 0, 0), swatch="player_cyan", parent=spear)
    K.extrude_poly("Blade", [(0.0, 0.8), (0.06, 0.88), (0.0, 1.05), (-0.06, 0.88)], thickness=0.03, plane="XZ",
                   swatch="steel_l", parent=spear)

    # --- legs: leather trousers, fur boots
    for side, x in (("L", 0.11), ("R", -0.11)):
        leg = K.box("Leg" + side, size=(0.15, 0.17, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1), taper=(1.05, 1.05),
                    swatch="leather", parent=root)
        K.box("Boot" + side, size=(0.18, 0.28, 0.2), location=(0, -0.04, -0.5), pivot=(0, 0, -1), taper=(1.05, 1.0),
              swatch="mud_dark", parent=leg)

    return root


root = build()
info = K.review_render(root, "ch_beastmaster", size=420)
info["tris"] = K.tri_count(K.descendants(root))
if not ("ARGS" in globals() and ARGS.get("export") == "0"):
    info["fbx"] = K.export_fbx(root, "Characters/CH_Beastmaster")
    info["blend"] = K.save_blend("ch_beastmaster")
result = info
