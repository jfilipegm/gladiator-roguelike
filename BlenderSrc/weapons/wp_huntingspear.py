"""Note 37: WP_HuntingSpear, the Beastmaster's own weapon as drawn by PlayerProjectiles.Thrust.
A short boar spear, unlike the Pit Spear (WP_Spear: a long shaft and a slim leaf): a short shaft, a broad leaf blade
with a ridge, and the boar spear's crossbar lugs under it (the silhouette's tell), bound in his cyan with a cyan
tassel like the spear he holds (a1_beastmaster.py). Same layout as WP_Spear (weapons.py): pivot at the grip, butt at
+0.48, the head toward Blender -Y (Unity +Z), the tip at -1.27. Player colours only, never orange-red.
    Tools/bheadless BlenderSrc/weapons/wp_huntingspear.py [review=1]
"""
import importlib

import gladiator_kit as K

importlib.reload(K)
OUT = {}


def hunting_spear(name):
    parts = [
        # shaft from the butt (+0.48) through the grip (0) to the socket (-0.84)
        K.cylinder(name, radius=0.03, depth=1.32, verts=6, location=(0, 0.48, 0), pivot=(0, 0, 1), axis="Y",
                   swatch="wood"),
        K.cylinder(name + "_Butt", radius=0.042, depth=0.06, verts=6, location=(0, 0.50, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="bronze"),
        K.cylinder(name + "_Grip", radius=0.038, depth=0.24, verts=6, location=(0, 0.12, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="leather"),
        K.cylinder(name + "_Binding", radius=0.042, depth=0.06, verts=6, location=(0, -0.74, 0), pivot=(0, 0, 1),
                   axis="Y", swatch="player_cyan"),
        K.cylinder(name + "_Socket", radius=0.04, depth=0.12, verts=6, location=(0, -0.84, 0), pivot=(0, 0, 1),
                   axis="Y", taper=0.8, swatch="iron"),
        # the boar spear's lugs: a crossbar under the blade, so a charging beast can't run up the shaft
        K.box(name + "_Lugs", size=(0.3, 0.05, 0.045), location=(0, -0.94, 0), pivot=(0, 0, 0), swatch="iron_d"),
        # a broad leaf, flat side up (as WP_Spear's): wide in X, thin in Z
        K.extrude_poly(name + "_Blade", [(0.0, -0.95), (0.065, -0.99), (0.11, -1.06), (0.09, -1.15), (0.0, -1.27),
                                         (-0.09, -1.15), (-0.11, -1.06), (-0.065, -0.99)], 0.03, plane="XY",
                       swatch="steel_l"),
        K.box(name + "_Ridge", size=(0.022, 0.25, 0.05), location=(0, -1.09, 0), pivot=(0, 0, 0), swatch="steel"),
        # the cyan tassel hanging from the binding
        K.box(name + "_Tassel", size=(0.035, 0.045, 0.14), location=(0, -0.76, -0.03), pivot=(0, 0, 1),
              swatch="glow_cyan"),
    ]
    return K.join(parts, name)


K.clear_scene()
o = hunting_spear("WP_HuntingSpear")
if "ARGS" in globals() and ARGS.get("review") == "1":
    OUT["review"] = K.review_render(o, "wp_huntingspear", size=300)
OUT["fbx"] = K.export_fbx(o, "Weapons/WP_HuntingSpear")
OUT["tris"] = K.tri_count(K.descendants(o))
lo, hi = K.world_bounds([o])
OUT["bounds"] = [tuple(round(x, 3) for x in lo), tuple(round(x, 3) for x in hi)]
OUT["blend"] = K.save_blend("wp_huntingspear")
result = OUT
