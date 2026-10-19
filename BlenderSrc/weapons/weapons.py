"""M2.5 player weapons drawn instanced by PlayerProjectiles (single meshes, palette UVs).
Front = Blender -Y (arrives as Unity +Z). Player gear reads steel/bronze/wood with a cool cyan or gold accent so it
never looks like an (orange-red) enemy projectile.
WP_Spear: pivot at the grip, tip 1.6 m ahead (PlayerProjectiles.spearTipOffset). WP_Javelin: pivot at the middle.
WP_Chakram: flat disc in the ground plane, pivot at the centre (spins around Z/Unity Y)."""
import importlib

import gladiator_kit as K

importlib.reload(K)
OUT = {}


def flatten(obj, sx=1.0, sz=1.0):
    for v in obj.data.vertices:
        v.co.x *= sx
        v.co.z *= sz
    return obj


def spear(name):
    # shaft from the butt (+0.6) through the grip (0) to the blade root (-1.30)
    shaft = K.cylinder(name, radius=0.032, depth=1.90, verts=6, location=(0, 0.60, 0), pivot=(0, 0, 1), axis="Y",
                       swatch="wood")
    butt = K.cylinder(name + "_Butt", radius=0.045, depth=0.08, verts=6, location=(0, 0.66, 0), pivot=(0, 0, 1),
                      axis="Y", swatch="bronze")
    collar = K.cylinder(name + "_Collar", radius=0.05, depth=0.07, verts=6, location=(0, -1.27, 0), pivot=(0, 0, 1),
                        axis="Y", swatch="bronze_l")
    # leaf blade: a short widening cone, then a long tapering one to the tip at -1.60, flattened to a blade
    base = K.cylinder(name + "_BladeBase", radius=0.03, depth=0.07, verts=4, location=(0, -1.30, 0), pivot=(0, 0, 1),
                      axis="Y", taper=4.3, swatch="steel_l")
    tip = K.cylinder(name + "_Blade", radius=0.13, depth=0.23, verts=4, location=(0, -1.37, 0), pivot=(0, 0, 1),
                     axis="Y", taper=0.0, swatch="steel_l")
    flatten(base, 1.0, 0.35)
    flatten(tip, 1.0, 0.35)
    grip = K.cylinder(name + "_Grip", radius=0.038, depth=0.22, verts=6, location=(0, 0.11, 0), pivot=(0, 0, 1),
                      axis="Y", swatch="leather")
    tassel = K.box(name + "_Tassel", size=(0.03, 0.04, 0.12), location=(0, -1.22, -0.03), pivot=(0, 0, 1),
                   swatch="glow_cyan")
    return K.join([shaft, butt, collar, base, tip, grip, tassel], name)


def javelin(name):
    shaft = K.cylinder(name, radius=0.024, depth=1.20, verts=5, location=(0, 0.62, 0), pivot=(0, 0, 1), axis="Y",
                       swatch="wood_light")
    head = K.cylinder(name + "_Head", radius=0.05, depth=0.24, verts=4, location=(0, -0.58, 0), pivot=(0, 0, 1),
                      axis="Y", taper=0.0, swatch="steel_l")
    flatten(head, 1.0, 0.6)
    band = K.cylinder(name + "_Band", radius=0.034, depth=0.06, verts=5, location=(0, -0.52, 0), pivot=(0, 0, 1),
                      axis="Y", swatch="bronze")
    fin_a = K.box(name + "_FinA", size=(0.14, 0.16, 0.012), location=(0, 0.58, 0), pivot=(0, 0, 0), swatch="glow_cyan")
    fin_b = K.box(name + "_FinB", size=(0.012, 0.16, 0.14), location=(0, 0.58, 0), pivot=(0, 0, 0), swatch="glow_cyan")
    return K.join([shaft, head, band, fin_a, fin_b], name)


def chakram(name):
    ring = K.cylinder(name, radius=0.30, depth=0.035, verts=8, pivot=(0, 0, 0), swatch="gold")
    hub = K.cylinder(name + "_Hub", radius=0.13, depth=0.06, verts=8, pivot=(0, 0, 0), swatch="iron_d")
    gem = K.cylinder(name + "_Gem", radius=0.05, depth=0.08, verts=6, pivot=(0, 0, 0), swatch="glow_gold")
    parts = [ring, hub, gem]
    for k in range(4):
        # four swept blades: flat triangles pointing out and slightly around
        import math
        a = math.radians(45 + 90 * k)
        blade = K.cone(name + "_Blade%d" % k, radius=0.07, depth=0.20, verts=3, pivot=(0, 0, -1), swatch="steel_l")
        for v in blade.data.vertices:
            v.co.x *= 0.25  # local X ends up along world Z after the 90 deg Y turn: a flat blade in the disc plane
        blade.rotation_euler = (0, math.radians(90), a)
        blade.location = (0.27 * math.cos(a), 0.27 * math.sin(a), 0)
        parts.append(blade)
    return K.join(parts, name)


K.clear_scene()
items = {"WP_Spear": spear("WP_Spear"), "WP_Javelin": javelin("WP_Javelin"), "WP_Chakram": chakram("WP_Chakram")}
for n, o in items.items():
    OUT[n] = K.export_fbx(o, "Weapons/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("weapons")
result = OUT
