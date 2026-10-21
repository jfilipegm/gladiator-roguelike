"""Phase 5.14: WP_Trident, the Net-Weaver's trident as drawn by PlayerProjectiles.Thrust (one per jab of a flurry).
Same layout as WP_Spear (weapons.py): pivot at the grip, butt at +0.6, the head toward Blender -Y (Unity +Z), the
middle prong's tip at -1.6. Player colours only: steel prongs, a wood shaft, a cyan binding (never orange-red).
    Tools/bheadless BlenderSrc/weapons/wp_trident.py [review=1]
"""
import importlib

import gladiator_kit as K

importlib.reload(K)
OUT = {}


def trident(name):
    parts = [
        K.cylinder(name, radius=0.03, depth=1.78, verts=6, location=(0, 0.60, 0), pivot=(0, 0, 1), axis="Y",
                   swatch="wood"),
        K.cylinder(name + "_Butt", radius=0.042, depth=0.07, verts=6, location=(0, 0.65, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="bronze"),
        K.cylinder(name + "_Grip", radius=0.037, depth=0.22, verts=6, location=(0, 0.11, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="leather"),
        K.box(name + "_Binding", size=(0.08, 0.08, 0.08), location=(0, -1.14, 0), pivot=(0, 0, 0), swatch="glow_cyan"),
        # the crossbar the three prongs rise from
        K.box(name + "_Bar", size=(0.36, 0.06, 0.05), location=(0, -1.2, 0), pivot=(0, 0, 0), swatch="steel"),
    ]
    for k, x in enumerate((-0.16, 0.0, 0.16)):
        tip = -1.6 if x == 0.0 else -1.52
        length = -tip - 1.2
        parts.append(K.box(name + "_Prong%d" % k, size=(0.035, length, 0.035), location=(x, -1.2 - length / 2, 0),
                           pivot=(0, 0, 0), swatch="steel_l"))
        # a barbed point: a flat 4-sided cone at the prong's end, wide at the prong and sharp beyond it (axis "Y"
        # puts the cylinder's +Z end toward the butt, so the narrow end is radius, the wide end radius * taper)
        point = K.cylinder(name + "_Point%d" % k, radius=0.003, depth=0.1, verts=4, location=(x, tip + 0.02, 0),
                           pivot=(0, 0, 1), axis="Y", taper=18.0, swatch="steel_l")
        for v in point.data.vertices:
            v.co.z *= 0.35
        parts.append(point)
    return K.join(parts, name)


K.clear_scene()
o = trident("WP_Trident")
if "ARGS" in globals() and ARGS.get("review") == "1":
    OUT["review"] = K.review_render(o, "wp_trident", size=300)
OUT["fbx"] = K.export_fbx(o, "Weapons/WP_Trident")
OUT["tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("wp_trident")
result = OUT
