"""EN_GildedPatron — the Grand Arena's boss (docs/BOSSES.md "The Gilded Patron"). Budget <= 2,500 tris.

The richest man in the city, carried in on a gilded litter under a crimson canopy by four bare-chested bearers. He's
round and lounging in gold and crimson robes, a laurel wreath on a fat bald head, a goblet in his left hand and a purse
in his right. Silhouette: the canopy roof over everything, ~3.6 m up, and the poles ~4.4 m long: nothing else is that
wide or has a roof.

Rigid parts: Body (the litter and the seated Patron, pivot on the platform), Head (his neck), ArmL (goblet), ArmR
(purse; it throws the coin bombs and the volley); no LegL/LegR (he sits). The bearers stand on the root; their legs are
BLeg<i>L / BLeg<i>R (pivot at the hip), swung by BossController while he's carried about. The poles stay on the root
too, so the litter rocks on them. Front = -Y (Unity +Z).
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

# iteration 2: the bearers stand at the poles' ends, in front of and behind the litter (v1 hid them under it), the
# poles on their outer shoulders; a bigger Patron; no stray sash
POLE_Z = 1.24           # the poles' underside (on the bearers' shoulders)
PLATFORM_Z = 1.38       # the litter platform's underside (Body's pivot)
BEARERS = ((0.55, -1.85), (-0.55, -1.85), (0.55, 1.85), (-0.55, 1.85))


def bearer(i, x, y, root):
    hip = 0.74
    torso = K.box("Bearer%d" % i, size=(0.4, 0.28, 0.5), location=(x, y, hip), pivot=(0, 0, -1), taper=(1.15, 1.0),
                  swatch="skin_tan", parent=root)
    K.box("Loin%d" % i, size=(0.42, 0.3, 0.16), location=(0, 0, -0.06), pivot=(0, 0, -1), swatch="cloth_white", parent=torso)
    K.box("Head%d" % i, size=(0.26, 0.26, 0.26), location=(0, -0.01, 0.5), pivot=(0, 0, -1), swatch="skin_tan", parent=torso)
    K.box("Cap%d" % i, size=(0.28, 0.28, 0.08), location=(0, -0.01, 0.72), pivot=(0, 0, -1), swatch="crimson", parent=torso)
    # the arm under the pole reaches up to it (the pole is on the outer shoulder)
    out = 1 if x > 0 else -1
    K.box("Reach%d" % i, size=(0.12, 0.12, 0.42), location=(out * 0.06, 0, 0.44), pivot=(0, 0, -1), swatch="skin_tan",
          parent=torso).rotation_euler = (0, out * math.radians(-12), 0)
    K.box("Hang%d" % i, size=(0.12, 0.12, 0.4), location=(-out * 0.24, 0, 0.48), pivot=(0, 0, 1), swatch="skin_tan",
          parent=torso)
    for side, dx in (("L", 0.1), ("R", -0.1)):
        leg = K.box("BLeg%d%s" % (i, side), size=(0.14, 0.15, 0.66), location=(x + dx, y, hip), pivot=(0, 0, 1),
                    swatch="skin_tan", parent=root)
        K.box("BFoot%d%s" % (i, side), size=(0.14, 0.22, 0.07), location=(0, -0.04, -0.74), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)


def build():
    K.clear_scene()
    root = K.empty("EN_GildedPatron")
    for i, (x, y) in enumerate(BEARERS):
        bearer(i, x, y, root)
    for side, x in (("L", 0.78), ("R", -0.78)):  # the poles, front to back
        pole = K.box("Pole" + side, size=(0.12, 4.4, 0.12), location=(x, 0, POLE_Z), pivot=(0, 0, -1), swatch="gold_d",
                     parent=root)
        for end in (-1, 1):
            K.box("PoleCap%s%d" % (side, end), size=(0.17, 0.12, 0.17), location=(0, end * 2.2, -0.025), pivot=(0, 0, -1),
                  swatch="gold", parent=pole)

    # ---- Body: the litter (platform, cushions, canopy) and the seated Patron, rocking on the poles
    body = K.box("Body", size=(1.9, 2.5, 0.22), location=(0, 0, PLATFORM_Z), pivot=(0, 0, -1), swatch="gold_d",
                 parent=root)
    K.box("Skirt", size=(1.96, 2.56, 0.3), location=(0, 0, -0.28), pivot=(0, 0, -1), swatch="crimson_d", parent=body)
    K.box("Cushion", size=(1.6, 2.1, 0.22), location=(0, 0.05, 0.22), pivot=(0, 0, -1), swatch="crimson", parent=body)
    K.box("Backrest", size=(1.5, 0.3, 0.8), location=(0, 0.95, 0.22), pivot=(0, 0, -1), swatch="gold", parent=body)
    for sx in (-1, 1):
        for sy in (-1, 1):
            K.cylinder("Post%d%d" % (sx, sy), radius=0.05, depth=2.35, verts=4, location=(sx * 0.88, sy * 1.18, 0.22),
                       pivot=(0, 0, -1), swatch="gold", parent=body)
    roof = K.box("Canopy", size=(2.2, 2.75, 0.16), location=(0, 0, 2.57), pivot=(0, 0, -1), taper=(0.92, 0.94),
                 swatch="crimson", parent=body)
    K.box("Fringe", size=(2.24, 2.79, 0.18), location=(0, 0, -0.16), pivot=(0, 0, -1), swatch="gold", parent=roof)
    K.cone("Peak", radius=1.15, depth=0.55, verts=4, location=(0, 0, 0.16), pivot=(0, 0, -1), swatch="crimson_d",
           parent=roof).rotation_euler = (0, 0, math.radians(45))
    K.ico("Finial", radius=0.14, subdiv=1, location=(0, 0, 0.78), swatch="gold_l", parent=roof)
    # the Patron: round, seated, robes of gold and crimson, his crossed legs over the cushion's front
    # iteration 3: the head sits clear above the belly (v2 hid it), the canopy raised to fit
    K.ico("Belly", radius=0.78, subdiv=1, location=(0, 0.12, 0.92), scale=(1.0, 0.88, 0.88), swatch="gold", parent=body)
    K.box("Toga", size=(1.1, 0.6, 0.85), location=(0, 0.35, 0.42), pivot=(0, 0, -1), taper=(0.9, 1.0), swatch="crimson",
          parent=body)
    K.box("Lap", size=(1.15, 0.7, 0.3), location=(0, -0.4, 0.42), pivot=(0, 0, -1), swatch="gold_d", parent=body)
    head = K.ico("Head", radius=0.4, subdiv=1, location=(0, 0.05, 1.98), scale=(1.0, 0.95, 1.05), swatch="skin_light",
                 parent=body)
    K.cylinder("Wreath", radius=0.41, depth=0.1, verts=8, location=(0, 0.0, 0.2), pivot=(0, 0, -1), swatch="gold",
               parent=head)
    K.box("Jowls", size=(0.5, 0.24, 0.2), location=(0, -0.24, -0.3), pivot=(0, 0, -1), swatch="skin_light", parent=head)
    for side, x in (("L", 0.12), ("R", -0.12)):
        K.box("Eye" + side, size=(0.08, 0.04, 0.06), location=(x, -0.37, 0.04), pivot=(0, 0, -1), swatch="ink", parent=head)
    for side, x in (("L", 0.78), ("R", -0.78)):
        arm = K.box("Arm" + side, size=(0.3, 0.32, 0.66), location=(x, 0.1, 1.5), pivot=(0, 0, 1), taper=(1.2, 1.2),
                    swatch="crimson", parent=body)
        K.box("Hand" + side, size=(0.22, 0.22, 0.2), location=(0, 0, -0.68), pivot=(0, 0, 0), swatch="skin_light", parent=arm)
        if side == "L":  # the goblet, raised a little
            K.cylinder("Goblet", radius=0.12, depth=0.26, verts=6, location=(0, -0.05, -0.7), pivot=(0, 0, -1), taper=1.4,
                       swatch="gold_l", parent=arm)
        else:  # the purse he throws from
            K.ico("Purse", radius=0.2, subdiv=1, location=(0, -0.05, -0.84), swatch="gold", parent=arm)
    return root


root = build()
info = K.review_render(root, "en_gildedpatron", size=480)
info["fbx"] = K.export_fbx(root, "Enemies/EN_GildedPatron")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_gildedpatron")
result = info
