"""Phase 5.1 crowd gifts (docs/FAVOR.md). Front = Blender -Y (arrives as Unity +Z), origin at the base centre.
Each is one mesh, readable from the third-person camera at ~10 m and as a 64 px silhouette:
PR_GiftAmphora (heal): terracotta amphora, two handles, a glowing heal-red band.
PR_GiftPouch (gold shower): fat sack tied with gold cord, coins spilling over the top.
PR_GiftLaurel (XP): an upright laurel wreath on a short stake, gold ribbon, XP-green berries.
PR_GiftWeapon (weapon in the sand): a gladius standing point-down in a sand mound, gold hilt, white-glow fuller.
ARGS: out=<models dir> to export somewhere other than Assets/Art/Models (e.g. a scratch folder for review)."""
import importlib
import math

import bpy

import gladiator_kit as K

importlib.reload(K)
if ARGS.get("out"):  # noqa: F821 (injected by the bridge)
    K.MODELS_DIR = ARGS["out"]  # noqa: F821
OUT = {}


def amphora(name):
    root = K.empty(name)
    parts = [
        K.cylinder(name + "_Mesh", radius=0.13, depth=0.08, verts=8, location=(0, 0, 0), taper=1.25, swatch="rust",
                   parent=root),
        K.cylinder("Belly", radius=0.2, depth=0.24, verts=8, location=(0, 0, 0.08), taper=1.4, swatch="rust_light"),
        K.cylinder("Shoulder", radius=0.28, depth=0.22, verts=8, location=(0, 0, 0.32), taper=0.5, swatch="rust_light"),
        K.cylinder("Band", radius=0.285, depth=0.07, verts=8, location=(0, 0, 0.29), taper=0.97, swatch="heal_red"),
        K.cylinder("Neck", radius=0.1, depth=0.2, verts=8, location=(0, 0, 0.54), taper=1.0, swatch="rust_light"),
        K.cylinder("Lip", radius=0.14, depth=0.05, verts=8, location=(0, 0, 0.74), taper=1.0, swatch="rust"),
        K.cylinder("Stopper", radius=0.08, depth=0.06, verts=6, location=(0, 0, 0.79), taper=0.8, swatch="cloth_sack"),
    ]
    for x in (-1, 1):
        parts.append(K.box("HandleUp", size=(0.16, 0.05, 0.05), location=(x * 0.16, 0, 0.66), swatch="rust"))
        parts.append(K.box("HandleDown", size=(0.05, 0.05, 0.24), location=(x * 0.23, 0, 0.44), swatch="rust"))
    K.join(parts, name + "_Mesh")
    return root


def pouch(name):
    root = K.empty(name)
    parts = [
        K.cylinder(name + "_Mesh", radius=0.24, depth=0.12, verts=8, location=(0, 0, 0), taper=1.3, swatch="cloth_sack",
                   parent=root),
        K.cylinder("Body", radius=0.31, depth=0.24, verts=8, location=(0, 0, 0.12), taper=0.62, swatch="cloth_sack"),
        K.cylinder("Neck", radius=0.12, depth=0.08, verts=8, location=(0, 0, 0.36), taper=0.85, swatch="cloth_sack"),
        K.cylinder("Cord", radius=0.135, depth=0.04, verts=8, location=(0, 0, 0.37), swatch="gold_l"),
        K.cylinder("Frill", radius=0.1, depth=0.1, verts=8, location=(0, 0, 0.44), taper=1.6, swatch="cloth_sack"),
        K.box("Patch", size=(0.16, 0.03, 0.14), location=(0, -0.27, 0.13), swatch="gold_d"),
    ]
    coins = [(0.0, 0.0, 0.56, 0), (0.08, -0.05, 0.6, 25), (-0.07, 0.03, 0.61, -20), (0.03, 0.07, 0.65, 40),
             (0.27, -0.12, 0.03, 70), (-0.25, -0.18, 0.02, -60)]
    for x, y, z, tilt in coins:
        c = K.cylinder("Coin", radius=0.075, depth=0.025, verts=8, location=(x, y, z), swatch="gold_coin")
        c.rotation_euler = (math.radians(tilt), 0, math.radians(tilt * 2))
        parts.append(c)
    bpy.context.view_layer.update()
    K.join(parts, name + "_Mesh")
    return root


def laurel(name):
    root = K.empty(name)
    parts = [K.box(name + "_Mesh", size=(0.09, 0.09, 0.36), location=(0, 0, 0), swatch="wood", parent=root)]
    centre_z, radius, steps = 0.7, 0.3, 16
    for i in range(steps):
        a = (i + 0.5) / steps * math.pi * 2
        if abs(a - math.pi * 1.5) < 0.3:  # gap at the bottom where the ribbon ties
            continue
        along = -(a + math.pi / 2)  # Y rotation that lays a box's long (X) axis along the circle's tangent
        x, z = math.cos(a) * radius, centre_z + math.sin(a) * radius
        core = K.box("Core", size=(0.14, 0.12, 0.06), location=(x, 0, z), pivot=(0, 0, 0), swatch="leaf_d")
        core.rotation_euler = (0, along, 0)
        parts.append(core)
        for side, sw in ((1, "grass_green"), (-1, "leaf")):  # leaves fan out of the core, outside and inside
            r = radius + side * 0.075
            leaf = K.box("Leaf", size=(0.15, 0.1, 0.055), location=(math.cos(a) * r, 0, centre_z + math.sin(a) * r),
                         pivot=(0, 0, 0), swatch=sw)
            leaf.rotation_euler = (0, along + side * math.radians(30), 0)
            parts.append(leaf)
        if i % 3 == 1:
            parts.append(K.ico("Berry", radius=0.04, subdiv=1, location=(x, -0.07, z), swatch="xp_green"))
    parts.append(K.box("Knot", size=(0.14, 0.14, 0.11), location=(0, 0, centre_z - radius - 0.03), pivot=(0, 0, 0),
                       swatch="gold_l"))
    for x in (-1, 1):
        tail = K.box("Ribbon", size=(0.07, 0.04, 0.2), location=(x * 0.07, -0.03, centre_z - radius - 0.15),
                     pivot=(0, 0, 0), swatch="gold")
        tail.rotation_euler = (0, math.radians(18 * x), 0)
        parts.append(tail)
    bpy.context.view_layer.update()
    K.join(parts, name + "_Mesh")
    return root


def sand_weapon(name):
    root = K.empty(name)
    parts = [
        K.cylinder(name + "_Mesh", radius=0.42, depth=0.14, verts=8, location=(0, 0, 0), taper=0.5, swatch="sand",
                   parent=root),
        K.cylinder("MoundTop", radius=0.21, depth=0.06, verts=8, location=(0, 0, 0.14), taper=0.6, swatch="sand_l"),
        K.box("Blade", size=(0.12, 0.04, 0.6), location=(0, 0, 0.1), swatch="steel_l"),
        K.box("Fuller", size=(0.025, 0.035, 0.5), location=(0, 0, 0.14), swatch="glow_white"),
        K.box("Guard", size=(0.3, 0.07, 0.06), location=(0, 0, 0.7), bevel=0.01, swatch="gold"),
        K.box("Grip", size=(0.05, 0.05, 0.2), location=(0, 0, 0.76), swatch="leather"),
        K.ico("Pommel", radius=0.055, subdiv=1, location=(0, 0, 1.0), swatch="gold_l"),
    ]
    K.join(parts, name + "_Mesh")
    return root


K.clear_scene()
builders = {"PR_GiftAmphora": amphora, "PR_GiftPouch": pouch, "PR_GiftLaurel": laurel, "PR_GiftWeapon": sand_weapon}
part = ARGS.get("part")  # noqa: F821  one asset per call keeps each bridge call short
for n, build in builders.items():
    if part and part != n:
        continue
    o = build(n)
    OUT[n] = K.export_fbx(o, "Props/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
    if ARGS.get("review"):  # noqa: F821
        OUT[n + "_review"] = K.review_render(o, n.lower())
    if part:
        break
OUT["blend"] = K.save_blend("gifts" if not part else "gifts_" + part)
result = OUT
