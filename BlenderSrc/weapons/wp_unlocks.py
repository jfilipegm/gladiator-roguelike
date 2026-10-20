"""Phase 5.6 unlockable weapons drawn by PlayerProjectiles (docs/UNLOCKS.md). Player colours only (steel, leather,
rope, cool white): never the orange-red hazard row. Front = Blender -Y (arrives as Unity +Z).
- WP_SlingStone: the Sling's shot, a rounded river stone with a white chip (flies straight, pivot at its centre).
- WP_Net: the thrown net, a flat rope octagon with crossing cords and lead weights (spins flat like the chakram).
- WP_Whip: the lash end of Gorm's Whip in the spear's thrust layout (grip end at +0.2, tip at -1.6); the thrust's streak
  draws the rest of the lash from the hand.
    Tools/bheadless BlenderSrc/weapons/wp_unlocks.py [review=1]
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)
OUT = {}


def sling_stone(name):
    stone = K.ico(name, radius=0.09, subdiv=1, location=(0, 0, 0), scale=(1.0, 1.25, 0.85), swatch="stone_grey")
    chip = K.ico(name + "_Chip", radius=0.04, subdiv=1, location=(0.03, -0.07, 0.04), swatch="player_white")
    band = K.cylinder(name + "_Band", radius=0.092, depth=0.03, verts=8, location=(0, 0.02, 0), pivot=(0, 0, 0),
                      axis="Y", swatch="slate")
    return K.join([stone, chip, band], name)


def ring_of_cords(name, radius, thickness, swatch, sides=8):
    """An open polygon of thin boxes (review iteration 2: a solid disc hid the net's cords)."""
    parts = []
    edge = 2 * radius * math.sin(math.pi / sides) + thickness
    for k in range(sides):
        a = math.radians(360.0 / sides * (k + 0.5))
        seg = K.box(name + "_%d" % k, size=(thickness, edge, thickness * 0.8), pivot=(0, 0, 0),
                    location=(radius * math.cos(math.pi / sides) * math.cos(a), radius * math.cos(math.pi / sides) * math.sin(a), 0),
                    swatch=swatch)
        seg.rotation_euler = (0, 0, a)
        parts.append(seg)
    return parts


def net(name):
    r = 0.62
    parts = [K.cylinder(name, radius=0.07, depth=0.07, verts=6, pivot=(0, 0, 0), swatch="player_white")]  # centre knot
    parts += ring_of_cords(name + "_Rim", r, 0.05, "rope")
    parts += ring_of_cords(name + "_Mid", r * 0.55, 0.035, "rope")
    for k in range(4):  # cords across, through the centre
        cord = K.box(name + "_Cord%d" % k, size=(0.035, 2 * r - 0.02, 0.028), location=(0, 0, 0), pivot=(0, 0, 0),
                     swatch="rope")
        cord.rotation_euler = (0, 0, math.radians(22.5 + 45 * k))
        parts.append(cord)
    for k in range(8):  # lead weights on the rim corners
        a = math.radians(45 * k)
        parts.append(K.ico(name + "_Weight%d" % k, radius=0.065, subdiv=1, location=(r * math.cos(a), r * math.sin(a), 0),
                           swatch="iron"))
    return K.join(parts, name)


def whip(name):
    # tapered leather segments from the grip end (+0.2) to the tip (-1.6), slightly kinked so it reads as a lash
    parts = []
    y, rad = 0.2, 0.045
    for k in range(6):
        seg_len = 0.3
        seg = K.cylinder(name if k == 0 else name + "_Seg%d" % k, radius=rad, depth=seg_len + 0.02, verts=5,
                         location=(0.02 * (1 if k % 2 else -1), y, 0.0), pivot=(0, 0, 1), axis="Y", taper=0.82,
                         swatch="leather_d" if k % 2 else "leather")
        parts.append(seg)
        y -= seg_len
        rad *= 0.82
    # the popper: a bright cord tuft at the tip, the part that cracks (the whip's sweet spot)
    popper = K.cone(name + "_Popper", radius=0.05, depth=0.16, verts=4, location=(0, -1.6, 0), pivot=(0, 0, -1),
                    swatch="player_white")
    popper.rotation_euler = (math.radians(90), 0, 0)
    parts.append(popper)
    parts.append(K.ico(name + "_Knot", radius=0.035, subdiv=1, location=(0, -1.58, 0), swatch="glow_white"))
    return K.join(parts, name)


K.clear_scene()
items = {
    "WP_SlingStone": sling_stone("WP_SlingStone"),
    "WP_Net": net("WP_Net"),
    "WP_Whip": whip("WP_Whip"),
}
review = ARGS.get("review") == "1" if "ARGS" in globals() else False
for n, o in items.items():
    if review:
        OUT[n + "_review"] = K.review_render(o, "unl_" + n, size=300)
    OUT[n] = K.export_fbx(o, "Weapons/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("wp_unlocks")
result = OUT
