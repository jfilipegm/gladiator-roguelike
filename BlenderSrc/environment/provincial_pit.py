"""Provincial Pit kit (Phase 5.4, docs/ARENAS.md): palette family A2/A3 (ochre, dry grass, sandstone). Arena 2 is a
~50 m sand arena ringed by a stone foundation wall with tiered wooden stands on top, three stone gates, stone platforms
and a dais, wooden barricades (the Champion smashes them), iron spike grates (they hurt everyone) and banners.

ARGS: part=structures|terrain|props. Every piece goes to Assets/Art/Models/Environment/. Origins at the base; wall-like
pieces face -Y (their front, toward the arena centre, arrives as Unity +Z). Seeded randomness: re-runs are identical.
"""
import importlib
import math
import random

import bmesh
import bpy
import gladiator_kit as K
from mathutils import Matrix, Vector

importlib.reload(K)
PART = ARGS.get("part", "structures")  # noqa: F821  (ARGS injected by Tools/blender_bridge.py)
OUT = {}


def finish(objs):
    bpy.context.view_layer.update()  # bake parts' rotations into joins
    return objs


# ------------------------------------------------------------------------------------------------ structures

def prov_wall(name, width=4.6, height=2.4, depth=1.0, seed=5):
    """Stone foundation: four courses of ashlar blocks (offset every other course) under a grey coping. Front (-Y)
    face at y=0, origin at the base of that face."""
    rng = random.Random(seed)
    core = K.box(name, size=(width, depth, height), pivot=(0, -1, -1), swatch="sand_d")  # the mortar behind the blocks
    parts = [core]
    rows, course = 4, height / 4
    for r in range(rows):
        n = 3 if r % 2 == 0 else 4
        w = width / (n - (0 if r % 2 == 0 else 0.5))
        x0 = -width / 2 + (0 if r % 2 == 0 else -w / 2)
        for i in range(n):
            a = max(-width / 2, x0 + i * w)
            b = min(width / 2, x0 + (i + 1) * w)
            if b - a < 0.2:
                continue
            sw = rng.choice(("sandstone", "sandstone", "sand", "sandstone_d"))
            parts.append(K.box("Block%d_%d" % (r, i), size=(b - a - 0.05, 0.1, course - 0.05),
                               location=((a + b) / 2, -0.05, r * course + 0.025), pivot=(0, 0, -1), bevel=0.02, swatch=sw))
    parts.append(K.box("Coping", size=(width + 0.04, depth + 0.16, 0.18), location=(0, depth / 2 - 0.08, height),
                       pivot=(0, 0, -1), bevel=0.02, swatch="stone_grey"))
    finish(parts)
    return K.join(parts, name)


def prov_gate(name, opening=3.2, height=3.3, depth=1.2):
    """Stone gateway with a wooden double door. DoorL / DoorR are children hinged at the posts (open by yawing), like
    the Slave Pit's gate, so ArenaGates drives it unchanged."""
    root = K.empty(name)
    frame = [
        K.box(name + "_Frame", size=(0.7, depth + 0.1, height + 0.3), location=(-opening / 2 - 0.35, 0, 0),
              pivot=(0, 0, -1), bevel=0.03, swatch="sandstone", parent=root),
        K.box("PostR", size=(0.7, depth + 0.1, height + 0.3), location=(opening / 2 + 0.35, 0, 0), pivot=(0, 0, -1),
              bevel=0.03, swatch="sandstone"),
        K.box("Lintel", size=(opening + 1.8, depth + 0.3, 0.55), location=(0, 0, height + 0.3), pivot=(0, 0, -1),
              bevel=0.03, swatch="stone_grey"),
        K.box("Keystone", size=(0.6, 0.14, 0.7), location=(0, -(depth / 2 + 0.16), height + 0.15), pivot=(0, 0, -1),
              swatch="sandstone_d"),
        K.box("Shield", size=(0.5, 0.08, 0.5), location=(0, -(depth / 2 + 0.24), height + 0.25), pivot=(0, 0, -1),
              swatch="red_banner"),
    ]
    for x in (-opening / 2 - 0.35, opening / 2 + 0.35):
        frame.append(K.box("Cap", size=(0.85, depth + 0.25, 0.2), location=(x, 0, height + 0.85), pivot=(0, 0, -1),
                           swatch="stone_grey"))
    finish(frame)
    K.join(frame, name + "_Frame")
    leaf_w = opening / 2 - 0.02
    for side, sx in (("L", -1), ("R", 1)):
        hinge = (sx * opening / 2, -depth / 2 + 0.12, 0)
        planks = []
        for i in range(4):
            x = hinge[0] - sx * (i + 0.5) * leaf_w / 4
            planks.append(K.box("Door%s%d" % (side, i), size=(leaf_w / 4 - 0.03, 0.1, height - 0.15),
                                location=(x, hinge[1], 0.05), pivot=(0, 0, -1), swatch="wood" if i % 2 else "wood_dark"))
        for z in (0.5, height * 0.5, height - 0.6):
            planks.append(K.box("Band", size=(leaf_w, 0.13, 0.14), location=(hinge[0] - sx * leaf_w / 2, hinge[1], z),
                                pivot=(0, 0, -1), swatch="iron_d"))
        h = K.box("Door" + side, size=(0.08, 0.12, height - 0.2), location=hinge, pivot=(-sx, 0, -1),
                  swatch="iron_d", parent=root)
        finish(planks)
        K.join([h] + planks, "Door" + side)
    return root


def prov_stand(name, width=4.6, tiers=3, rise=0.45, run=0.8):
    """Tiered wooden benches for the townsfolk, standing on the wall top behind the coping. Front (-Y) edge at y=0;
    tier k's seat is at z = (k+1) * rise, k * run back."""
    parts = []
    for k in range(tiers):
        parts.append(K.box(name if k == 0 else "Tier%d" % k, size=(width, run, (k + 1) * rise),
                           location=(0, k * run + run / 2, 0), pivot=(0, 0, -1), bevel=0.02,
                           swatch="wood" if k % 2 == 0 else "wood_light"))
        parts.append(K.box("Edge%d" % k, size=(width + 0.02, 0.08, 0.06), location=(0, k * run + 0.04, (k + 1) * rise),
                           pivot=(0, 0, 0), swatch="wood_dark"))
    for x in (-width / 2 + 0.1, width / 2 - 0.1):
        parts.append(K.box("Post", size=(0.14, 0.14, tiers * rise + 1.6), location=(x, tiers * run - 0.1, 0),
                           pivot=(0, 0, -1), swatch="wood_dark"))
    parts.append(K.box("Pennant", size=(1.0, 0.04, 0.7), location=(width / 2 - 0.65, tiers * run - 0.1, tiers * rise + 0.85),
                       pivot=(0, 0, -1), swatch="red_banner"))
    parts.append(K.box("PennantStripe", size=(1.02, 0.05, 0.16), location=(width / 2 - 0.65, tiers * run - 0.1, tiers * rise + 1.12),
                       pivot=(0, 0, -1), swatch="cloth_white"))
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ terrain

def prov_pillar(name, height=4.2, radius=0.5):
    """Stone column (a line-of-sight blocker): grey plinth and capital, a sandstone shaft of three drums of slightly
    different girth so it reads as stacked stone, not a wooden post. Origin at the base centre; ~1.3 m footprint."""
    parts = [K.box(name, size=(1.3, 1.3, 0.32), pivot=(0, 0, -1), bevel=0.04, swatch="stone_grey"),
             K.cylinder("Torus", radius=radius + 0.1, depth=0.16, verts=12, location=(0, 0, 0.32), pivot=(0, 0, -1),
                        swatch="stone_grey")]
    z, drum = 0.48, (height - 0.48 - 0.55) / 3
    for i, (r, sw) in enumerate(((radius, "sandstone"), (radius - 0.03, "sand_d"), (radius - 0.05, "sandstone"))):
        parts.append(K.cylinder("Drum%d" % i, radius=r, depth=drum - 0.03, verts=12, location=(0, 0, z), pivot=(0, 0, -1),
                                taper=0.97, swatch=sw))
        parts.append(K.cylinder("Joint%d" % i, radius=r - 0.06, depth=0.03, verts=12, location=(0, 0, z + drum - 0.03),
                                pivot=(0, 0, -1), swatch="sandstone_d"))
        z += drum
    parts.append(K.cylinder("Echinus", radius=radius - 0.06, depth=0.25, verts=12, location=(0, 0, z), pivot=(0, 0, -1),
                            taper=1.35, swatch="stone_grey"))
    parts.append(K.box("Abacus", size=(1.3, 1.3, 0.3), location=(0, 0, z + 0.25), pivot=(0, 0, -1), bevel=0.04,
                       swatch="stone_grey"))
    finish(parts)
    return K.join(parts, name)


def prov_floor(name, radius=26.4, seed=17):
    """Packed sand disc of 2 m cells: nearly flat (the arena is built, not dug), a little darker at the rim."""
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=27, y_segments=27, size=27.0)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().length > radius], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    for v in bm.verts:
        r = v.co.length
        v.co.z = rng.uniform(-0.03, 0.03) + 0.12 * max(0.0, (r - 22.0) / 4.4)
    obj = K.object_from_bmesh(name, bm, swatch="sand")
    K.set_swatch_where(obj, "sand_l", lambda c, n: rng.random() < 0.07)  # a few light cells: floors stay low-contrast
    K.set_swatch_where(obj, "sand_d", lambda c, n: c.length > 21.5 and rng.random() < 0.5)
    return obj


def prov_rim(name, inner=26.3, outer=58.0, height=2.4, segments=40, seed=19):
    """The ground behind the wall at wall-top height: a paved walkway ring, then dry grass toward the horizon."""
    rng = random.Random(seed)
    radii = (inner, inner + 4.0, inner + 10.0, inner + 18.0, outer)
    bm = bmesh.new()
    rings = []
    for k, r in enumerate(radii):
        ring = []
        for i in range(segments):
            a = i / segments * 2 * math.pi
            z = height + (rng.uniform(-0.08, 0.12) if k > 1 else 0.0) - 0.04 * max(0.0, r - inner - 12.0)
            ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)))
        rings.append(ring)
    for k in range(len(radii) - 1):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((rings[k][i], rings[k + 1][i], rings[k + 1][j], rings[k][j]))
    obj = K.object_from_bmesh(name, bm, swatch="grass_dry")
    K.set_swatch_where(obj, "stone_grey", lambda c, nn: c.length < inner + 4.0)
    K.set_swatch_where(obj, "grass_dry_d", lambda c, nn: c.length > inner + 4.0 and rng.random() < 0.35)
    return obj


def prov_platform(name, size=(6.0, 4.0), height=1.2, seed=23):
    """Raised stone platform (jump up or take the stairs): ashlar sides, a sandstone top. Origin at the base centre."""
    rng = random.Random(seed)
    w, d = size
    top = K.box(name, size=(w, d, height), pivot=(0, 0, -1), bevel=0.04, swatch="sandstone")
    K.set_swatch_where(top, "sand_l", lambda c, n: n.z > 0.8)
    parts = [top]
    for side, (sx, sy, along) in enumerate(((0, -1, w), (0, 1, w), (-1, 0, d), (1, 0, d))):
        n = max(2, int(along / 1.5))
        for i in range(n):
            t = -along / 2 + (i + 0.5) * along / n
            loc = (t, sy * (d / 2 + 0.02), 0.04) if sx == 0 else (sx * (w / 2 + 0.02), t, 0.04)
            size_b = (along / n - 0.06, 0.06, height * 0.5 - 0.05) if sx == 0 else (0.06, along / n - 0.06, height * 0.5 - 0.05)
            parts.append(K.box("Course%d_%d" % (side, i), size=size_b, location=loc, pivot=(0, 0, -1),
                               swatch=rng.choice(("sandstone_d", "sand_d"))))
    parts.append(K.box("Lip", size=(w + 0.12, d + 0.12, 0.12), location=(0, 0, height - 0.12), pivot=(0, 0, -1),
                       swatch="stone_grey"))
    finish(parts)
    return K.join(parts, name)


def prov_dais(name, radius=3.6, height=0.45):
    """Low round dais in the centre, one step up (the boss's stage before the fight). Its seal is ochre, not red: red
    means 'this hurts you' (CLAUDE.md §5)."""
    base = K.cylinder(name, radius=radius, depth=height * 0.5, verts=12, pivot=(0, 0, -1), swatch="sandstone_d")
    parts = [base,
             K.cylinder("Top", radius=radius - 0.6, depth=height * 0.5, verts=12, location=(0, 0, height * 0.5),
                        pivot=(0, 0, -1), swatch="sandstone"),
             K.cylinder("Seal", radius=0.9, depth=0.03, verts=8, location=(0, 0, height), pivot=(0, 0, -1),
                        swatch="ochre")]
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ props

def barricade(name, length=2.6):
    """Cheval de frise: a log on crossed, sharpened stakes. ~1.2 m tall, ~1.3 m deep. Origin at the base centre,
    long axis along X."""
    parts = [K.cylinder(name, radius=0.14, depth=length, verts=6, location=(0, 0, 0.62), pivot=(0, 0, 0), axis="X",
                        swatch="wood_dark")]
    n = 5
    for i in range(n):
        x = -length / 2 + 0.25 + i * (length - 0.5) / (n - 1)
        for k, tilt in enumerate((48, -48)):
            stake = K.cylinder("Stake%d_%d" % (i, k), radius=0.055, depth=1.5, verts=5, location=(x, 0, 0.62),
                               pivot=(0, 0, 0), swatch="wood_light" if (i + k) % 2 else "wood")
            stake.rotation_euler = (math.radians(tilt), 0, 0)
            parts.append(stake)
            # the sharpened tips: the stake runs along its local Z, so the tips sit at ±0.75 along it
            for sgn in (1, -1):
                tip = K.cone("Tip%d_%d_%d" % (i, k, sgn > 0), radius=0.055, depth=0.16, verts=5,
                             location=(x, -sgn * 0.75 * math.sin(math.radians(tilt)), 0.62 + sgn * 0.75 * math.cos(math.radians(tilt))),
                             pivot=(0, 0, -1), swatch="bone")
                tip.rotation_euler = (math.radians(tilt) + (0 if sgn > 0 else math.pi), 0, 0)
                parts.append(tip)
    for x in (-length / 2 + 0.3, length / 2 - 0.3):
        parts.append(K.box("Rope", size=(0.06, 0.32, 0.32), location=(x, 0, 0.62), pivot=(0, 0, 0), swatch="rope"))
    finish(parts)
    return K.join(parts, name)


def spike_grate(name, size=1.8):
    """Iron floor grate of a spike trap, flush with the sand, with a painted orange-red border: it's dangerous."""
    parts = [K.box(name, size=(size, size, 0.04), pivot=(0, 0, -1), swatch="iron_d")]
    parts.append(K.box("Border", size=(size + 0.16, size + 0.16, 0.03), pivot=(0, 0, -1), swatch="hazard_red"))
    for i in range(5):
        t = -size / 2 + (i + 0.5) * size / 5
        parts.append(K.box("BarX%d" % i, size=(size - 0.08, 0.06, 0.05), location=(0, t, 0), pivot=(0, 0, -1), swatch="iron"))
        parts.append(K.box("BarY%d" % i, size=(0.06, size - 0.08, 0.05), location=(t, 0, 0), pivot=(0, 0, -1), swatch="iron"))
    finish(parts)
    return K.join(parts, name)


def spikes(name, size=1.8, height=0.8):
    """The spikes that shoot up through the grate (drawn by the SpikeTraps system). Base at z = 0, 4×4 grid."""
    parts = []
    for i in range(4):
        for j in range(4):
            x = -size / 2 + (i + 0.5) * size / 4
            y = -size / 2 + (j + 0.5) * size / 4
            h = height * (0.85 + 0.15 * ((i * 3 + j * 5) % 4) / 3)
            parts.append(K.cone(name if not parts else "Spike%d%d" % (i, j), radius=0.08, depth=h, verts=4,
                                location=(x, y, 0), pivot=(0, 0, -1), swatch="steel_l"))
    finish(parts)
    o = K.join(parts, name)
    # the join keeps the first spike's origin (a corner); SpikeTraps draws the mesh at the grate's centre, so the
    # origin must be the grid's centre or the spikes come up off the grate
    o.data.transform(Matrix.Translation(o.location))
    o.location = (0, 0, 0)
    return o


def banner(name, height=3.2):
    """A red-and-white town banner on a pole, for the wall top and the gates."""
    parts = [K.cylinder(name, radius=0.06, depth=height, verts=6, pivot=(0, 0, -1), swatch="wood_dark"),
             K.box("Cross", size=(1.0, 0.06, 0.06), location=(0, 0, height - 0.15), pivot=(0, 0, 0), swatch="wood_dark"),
             K.box("Cloth", size=(0.9, 0.04, 1.5), location=(0, -0.05, height - 0.2), pivot=(0, 0, 1), swatch="red_banner"),
             K.box("Stripe", size=(0.92, 0.05, 0.22), location=(0, -0.06, height - 0.75), pivot=(0, 0, 0), swatch="cloth_white"),
             K.cone("Tail", radius=0.45, depth=0.35, verts=3, location=(0, -0.05, height - 1.7), pivot=(0, 0, 1),
                    swatch="red_banner")]
    parts[-1].rotation_euler = (0, math.pi, math.pi / 2)
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ build

K.clear_scene()
if PART == "structures":
    items = {
        "SM_ProvWall_4m": prov_wall("SM_ProvWall_4m"),
        "SM_ProvGate": prov_gate("SM_ProvGate"),
        "SM_ProvStand_4m": prov_stand("SM_ProvStand_4m"),
        "SM_ProvPillar": prov_pillar("SM_ProvPillar"),
    }
elif PART == "terrain":
    items = {
        "SM_ProvFloor": prov_floor("SM_ProvFloor"),
        "SM_ProvRim": prov_rim("SM_ProvRim"),
        "SM_ProvPlatform": prov_platform("SM_ProvPlatform"),
        "SM_ProvDais": prov_dais("SM_ProvDais"),
    }
else:
    items = {
        "PR_Barricade": barricade("PR_Barricade"),
        "PR_SpikeGrate": spike_grate("PR_SpikeGrate"),
        "PR_Spikes": spikes("PR_Spikes"),
        "PR_Banner": banner("PR_Banner"),
    }
review = ARGS.get("review")  # noqa: F821
for n, o in items.items():
    if review == n:
        OUT["review"] = K.review_render(o, n.lower(), size=360)
    OUT[n] = K.export_fbx(o, "Environment/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o) or [o])
OUT["blend"] = K.save_blend("provincial_pit_" + PART)
result = OUT
