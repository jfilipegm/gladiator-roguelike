"""WP_Trident, the Net-Weaver's trident as drawn by PlayerProjectiles.Lunge (note 34: one lunge, three parallel lanes).
Same layout as WP_Spear (weapons.py): pivot at the grip, butt at +0.7, the head toward Blender -Y (Unity +Z).
Note 34 (v0.28.0 playtest, "the trident looks nothing different from the javelin"): a chunky head that reads from the
camera above at a glance, unlike the javelin's one point: a wide steel crossbar, three thick prongs (the middle one
longest) with flat barbed arrowheads lying in the plane the camera sees, a glowing cyan binding under the head.
At the game's scale (meshScale 1.4) the outer prongs sit about on the outer lanes (prongSpacing 0.45 m).
Player colours only: steel prongs, a wood shaft, bronze fittings, a cyan binding (never orange-red).
    Tools/bheadless BlenderSrc/weapons/wp_trident.py [review=1]
"""
import importlib

import gladiator_kit as K

importlib.reload(K)
OUT = {}

OUTER_X = 0.29          # the outer prongs' axis (the lanes are 0.45 m apart at meshScale 1.4 → 0.32 here)
BAR_Y = -1.16           # the crossbar's centre
MID_TIP, OUT_TIP = -1.62, -1.50   # where the prongs' shafts end (their points go 0.14 further)


def point(name, x, y, width, swatch="steel_l"):
    """A flat 4-sided arrowhead at a prong's end: wide at y, sharp 0.15 m beyond (toward -Y), flat in the XY plane
    (axis "Y" puts the cylinder's +Z end toward the butt: the wide end, radius * taper)."""
    p = K.cylinder(name, radius=0.004, depth=0.15, verts=4, location=(x, y, 0), pivot=(0, 0, 1), axis="Y",
                   taper=width / 0.004, swatch=swatch)
    for v in p.data.vertices:
        v.co.z *= 0.4
    return p


def trident(name):
    parts = [
        K.cylinder(name, radius=0.042, depth=1.80, verts=6, location=(0, 0.66, 0), pivot=(0, 0, 1), axis="Y",
                   swatch="wood"),
        K.cylinder(name + "_Butt", radius=0.06, depth=0.09, verts=6, location=(0, 0.72, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="bronze"),
        K.cylinder(name + "_Grip", radius=0.05, depth=0.28, verts=6, location=(0, 0.14, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="leather"),
        # the glowing binding under the head, and a bronze collar
        K.cylinder(name + "_Binding", radius=0.065, depth=0.1, verts=6, location=(0, -0.98, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="glow_cyan"),
        K.cylinder(name + "_Collar", radius=0.075, depth=0.06, verts=6, location=(0, -1.08, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="bronze"),
        # the crossbar the prongs rise from: wide, a little swept back at its ends
        K.box(name + "_Bar", size=(2 * OUTER_X + 0.1, 0.09, 0.075), location=(0, BAR_Y, 0), pivot=(0, 0, 0),
              swatch="steel"),
    ]
    for side in (-1, 1):
        sweep = K.box(name + "_Sweep%d" % (side + 1), size=(0.09, 0.14, 0.07), location=(side * OUTER_X, BAR_Y + 0.05, 0),
                      pivot=(0, 0, 0), swatch="steel")
        parts.append(sweep)
    # the middle prong, the longest
    mid_len = -MID_TIP + BAR_Y
    parts.append(K.box(name + "_Prong1", size=(0.075, mid_len, 0.065), location=(0, BAR_Y - mid_len / 2, 0),
                       pivot=(0, 0, 0), swatch="steel_l"))
    parts.append(point(name + "_Point1", 0.0, MID_TIP + 0.01, 0.1))
    # the outer prongs, shorter, each with its arrowhead and a barb turned outward
    out_len = -OUT_TIP + BAR_Y
    for k, side in ((0, -1), (2, 1)):
        x = side * OUTER_X
        parts.append(K.box(name + "_Prong%d" % k, size=(0.07, out_len, 0.06), location=(x, BAR_Y - out_len / 2, 0),
                           pivot=(0, 0, 0), swatch="steel_l"))
        parts.append(point(name + "_Point%d" % k, x, OUT_TIP + 0.01, 0.09))
        barb = K.box(name + "_Barb%d" % k, size=(0.035, 0.12, 0.05), location=(x + side * 0.05, OUT_TIP + 0.06, 0),
                     pivot=(0, 0, 0), swatch="steel_l")
        barb.rotation_euler = (0, 0, side * 0.6)
        parts.append(barb)
    return K.join(parts, name)


K.clear_scene()
o = trident("WP_Trident")
if "ARGS" in globals() and ARGS.get("review") == "1":
    OUT["review"] = K.review_render(o, "wp_trident", size=300)
OUT["fbx"] = K.export_fbx(o, "Weapons/WP_Trident")
OUT["tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("wp_trident")
result = OUT
