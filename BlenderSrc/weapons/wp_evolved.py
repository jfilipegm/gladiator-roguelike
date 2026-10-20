"""Phase 5.5 evolved weapons drawn instanced by PlayerProjectiles (docs/EVOLUTIONS.md). Each is its base weapon's shape
and pivot (so PlayerProjectiles' offsets still fit) re-done in brighter metal and gold, plus ONE added part that names
the twist: the Impaler's barbs, the Thunderbolt's charged head and bolt, Twin Moons' outer ring, the Fire-Pot's clay
pot and flame, Chain of Ruin's second band and extra spikes. Player colours only (steel, gold, cyan, gold flame): never
the orange-red hazard row. Front = Blender -Y (arrives as Unity +Z).
    python3 Tools/blender_bridge.py BlenderSrc/weapons/wp_evolved.py [--arg review=1]
"""
import importlib
import math

import gladiator_kit as K
import mathutils

importlib.reload(K)
OUT = {}


def flatten(obj, sx=1.0, sz=1.0):
    for v in obj.data.vertices:
        v.co.x *= sx
        v.co.z *= sz
    return obj


def impaler(name):  # WP_Spear's layout (grip at 0, tip at -1.60), steel and gold, barbs at the blade root
    shaft = K.cylinder(name, radius=0.034, depth=1.90, verts=6, location=(0, 0.60, 0), pivot=(0, 0, 1), axis="Y",
                       swatch="wood_dark")
    butt = K.cylinder(name + "_Butt", radius=0.05, depth=0.09, verts=6, location=(0, 0.66, 0), pivot=(0, 0, 1),
                      axis="Y", swatch="gold")
    collar = K.cylinder(name + "_Collar", radius=0.06, depth=0.09, verts=6, location=(0, -1.25, 0), pivot=(0, 0, 1),
                        axis="Y", swatch="gold_l")
    base = K.cylinder(name + "_BladeBase", radius=0.035, depth=0.08, verts=4, location=(0, -1.30, 0), pivot=(0, 0, 1),
                      axis="Y", taper=4.6, swatch="steel_l")
    tip = K.cylinder(name + "_Blade", radius=0.16, depth=0.24, verts=4, location=(0, -1.37, 0), pivot=(0, 0, 1),
                     axis="Y", taper=0.0, swatch="steel_l")
    flatten(base, 1.0, 0.35)
    flatten(tip, 1.0, 0.35)
    parts = [shaft, butt, collar, base, tip]
    for side in (1, -1):  # the added part: two barbs hooking back from the blade root
        barb = K.cone(name + "_Barb%d" % side, radius=0.035, depth=0.16, verts=3, pivot=(0, 0, -1), swatch="steel")
        flatten(barb, 1.0, 0.4)
        barb.rotation_euler = (math.radians(-90), 0, math.radians(-150 * side))
        barb.location = (0.07 * side, -1.33, 0)
        parts.append(barb)
    parts.append(K.cylinder(name + "_Grip", radius=0.04, depth=0.22, verts=6, location=(0, 0.11, 0), pivot=(0, 0, 1),
                            axis="Y", swatch="leather_d"))
    parts.append(K.box(name + "_Tassel", size=(0.03, 0.04, 0.14), location=(0, -1.20, -0.03), pivot=(0, 0, 1),
                       swatch="glow_gold"))
    return K.join(parts, name)


def thunderbolt(name):  # WP_Javelin's layout (pivot at the middle), a charged cyan head and a bolt along the shaft
    shaft = K.cylinder(name, radius=0.026, depth=1.20, verts=5, location=(0, 0.62, 0), pivot=(0, 0, 1), axis="Y",
                       swatch="wood_light")
    head = K.cylinder(name + "_Head", radius=0.06, depth=0.28, verts=4, location=(0, -0.58, 0), pivot=(0, 0, 1),
                      axis="Y", taper=0.0, swatch="glow_cyan")
    flatten(head, 1.0, 0.6)
    band = K.cylinder(name + "_Band", radius=0.036, depth=0.07, verts=5, location=(0, -0.52, 0), pivot=(0, 0, 1),
                      axis="Y", swatch="gold")
    parts = [shaft, head, band]
    for k in range(4):  # the added part: a zigzag bolt riding the shaft
        seg = K.box(name + "_Bolt%d" % k, size=(0.012, 0.2, 0.03), location=(0.0, -0.36 + k * 0.17, 0.05),
                    pivot=(0, 0, 0), swatch="glow_white")
        seg.rotation_euler = (math.radians(25 if k % 2 == 0 else -25), 0, 0)
        parts.append(seg)
    parts.append(K.box(name + "_FinA", size=(0.16, 0.16, 0.012), location=(0, 0.58, 0), pivot=(0, 0, 0), swatch="gold"))
    parts.append(K.box(name + "_FinB", size=(0.012, 0.16, 0.16), location=(0, 0.58, 0), pivot=(0, 0, 0), swatch="gold"))
    return K.join(parts, name)


def twin_moon(name):  # WP_Chakram's layout (flat disc, pivot at the centre), steel blades, gold, an outer ring
    ring = K.cylinder(name, radius=0.30, depth=0.035, verts=8, pivot=(0, 0, 0), swatch="gold_l")
    outer = K.cylinder(name + "_Outer", radius=0.36, depth=0.02, verts=10, pivot=(0, 0, 0), swatch="steel_l")
    hub = K.cylinder(name + "_Hub", radius=0.13, depth=0.06, verts=8, pivot=(0, 0, 0), swatch="steel")
    gem = K.cylinder(name + "_Gem", radius=0.06, depth=0.09, verts=6, pivot=(0, 0, 0), swatch="glow_cyan")
    parts = [outer, ring, hub, gem]
    for k in range(4):
        a = math.radians(45 + 90 * k)
        blade = K.cone(name + "_Blade%d" % k, radius=0.08, depth=0.24, verts=3, pivot=(0, 0, -1), swatch="steel_l")
        for v in blade.data.vertices:
            v.co.x *= 0.25
        blade.rotation_euler = (0, math.radians(90), a)
        blade.location = (0.32 * math.cos(a), 0.32 * math.sin(a), 0)
        parts.append(blade)
    return K.join(parts, name)


def fire_pot(name):  # the bomb's size and pivot (base on the ground), a clay fire-pot with a gold flame
    root = K.empty(name)
    parts = [
        K.cylinder(name + "_Mesh", radius=0.17, depth=0.22, verts=8, location=(0, 0, 0.0), taper=0.75, swatch="ochre_d",
                   parent=root),
        K.ico("Belly", radius=0.18, subdiv=1, location=(0, 0, 0.13), scale=(1, 1, 0.8), swatch="ochre"),
        K.cylinder("Neck", radius=0.09, depth=0.06, verts=8, location=(0, 0, 0.25), swatch="ochre_d"),
        K.cylinder("Rim", radius=0.11, depth=0.03, verts=8, location=(0, 0, 0.30), swatch="gold"),
        K.ico("Core", radius=0.06, subdiv=1, location=(0, 0, 0.35), swatch="glow_white"),
    ]
    # flame tongues (review iteration 2: one solid cone read as a gold spire): three leaning cones of different heights
    for k, (yaw, lean, depth, r) in enumerate(((0, 18, 0.24, 0.06), (130, 26, 0.17, 0.05), (245, 24, 0.19, 0.05))):
        tongue = K.cone("Tongue%d" % k, radius=r, depth=depth, verts=4, location=(0, 0, 0.32), swatch="glow_gold")
        tongue.rotation_euler = (math.radians(lean), 0, math.radians(yaw))
        parts.append(tongue)
    K.join(parts, name + "_Mesh")
    return root


def ruin_ball(name):  # WP_FlailBall's layout (pivot at the ball), bigger, a second (crossed) gold band and more spikes
    ball = K.ico(name, radius=0.27, subdiv=1, location=(0, 0, 0), swatch="iron_d")
    band = K.cylinder(name + "_Band", radius=0.275, depth=0.07, verts=8, location=(0, 0, 0), pivot=(0, 0, 0), axis="Y",
                      swatch="gold")
    band2 = K.cylinder(name + "_Band2", radius=0.275, depth=0.07, verts=8, location=(0, 0, 0), pivot=(0, 0, 0),
                       axis="X", swatch="gold_l")
    parts = [ball, band, band2]
    h = math.pi / 2
    dirs = [((1, 0, 0), (0, h, 0)), ((-1, 0, 0), (0, -h, 0)), ((0, -1, 0), (h, 0, 0)), ((0, 0, -1), (math.pi, 0, 0)),
            ((0, 0, 1), (0, 0, 0))]
    for k, (d, rot) in enumerate(dirs):
        spike = K.cone(name + "_Spike%d" % k, radius=0.085, depth=0.24, verts=4,
                       location=(d[0] * 0.23, d[1] * 0.23, d[2] * 0.23), pivot=(0, 0, -1), swatch="steel_l")
        spike.rotation_euler = rot
        parts.append(spike)
    for k, (yaw, pitch) in enumerate(((45, 35), (-45, 35), (135, -35), (-135, -35))):  # the extra spikes between
        y, p = math.radians(yaw), math.radians(pitch)
        d = (math.cos(p) * math.sin(y), -math.cos(p) * math.cos(y), math.sin(p))
        spike = K.cone(name + "_SpikeX%d" % k, radius=0.06, depth=0.17, verts=4,
                       location=(d[0] * 0.22, d[1] * 0.22, d[2] * 0.22), pivot=(0, 0, -1), swatch="steel")
        spike.rotation_mode = "QUATERNION"  # point the cone (+Z) along d
        spike.rotation_quaternion = mathutils.Vector((0, 0, 1)).rotation_difference(mathutils.Vector(d))
        parts.append(spike)
    for k in range(3):
        link = K.box(name + "_Chain%d" % k, size=(0.06, 0.13, 0.11), location=(0, 0.30 + k * 0.12, 0), pivot=(0, 0, 0),
                     swatch="iron")
        link.rotation_euler = (0, (k % 2) * h, 0)
        parts.append(link)
    parts.append(K.box(name + "_Gem", size=(0.07, 0.07, 0.07), location=(0, 0, 0.28), pivot=(0, 0, 0), swatch="glow_cyan"))
    return K.join(parts, name)


K.clear_scene()
items = {
    "WP_EvoSpear": impaler("WP_EvoSpear"),
    "WP_EvoJavelin": thunderbolt("WP_EvoJavelin"),
    "WP_EvoChakram": twin_moon("WP_EvoChakram"),
    "WP_EvoFirePot": fire_pot("WP_EvoFirePot"),
    "WP_EvoFlailBall": ruin_ball("WP_EvoFlailBall"),
}
review = ARGS.get("review") == "1" if "ARGS" in globals() else False
for n, o in items.items():
    if review:
        OUT[n + "_review"] = K.review_render(o, "evo_" + n, size=300)
    OUT[n] = K.export_fbx(o, "Weapons/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("wp_evolved")
result = OUT
