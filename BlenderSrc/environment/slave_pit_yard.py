"""Slave Pit, playtest rebuild: a rough oval stockade where slaves are worked and drilled, twice as wide as the old pit
(producer: "a rough enclosure, not a wooden bowl"; "people actually work and train there"; "it shouldn't float").

The boundary is built from 5 m sections that face the yard: a log palisade (with a log gate), a patched fence, a dirt
embankment with a wattle fence on its crest, and crude plank platforms for onlookers. Inside: an oval dirt yard with
trodden paths and worn drill circles, and the furniture of a working yard (training posts, straw dummies, a weapon rack,
a water trough, hay, a chopping block, a wheelbarrow, sacks, a sparring ring, benches, a tool shed). Outside, never
reachable: rolling ground running off into the fog, trees, huts, a longhouse, tents, a watchtower, carts, rocks.

ARGS: part=enclosure|ground|props|outside [review=1]. Every piece goes to Assets/Art/Models/Environment/. Origins at
the base; boundary pieces face -Y (toward the yard; arrives as Unity +Z) with their inner face on y=0. The yard and
the surrounds are centred on the arena (Blender (x, y) = Unity (-x, -z)). Seeded randomness: re-runs are identical.
"""
import importlib
import math
import random

import bmesh
import bpy
import gladiator_kit as K
from mathutils import Vector

importlib.reload(K)
PART = ARGS.get("part", "enclosure")  # noqa: F821  (ARGS injected by Tools/blender_bridge.py)
REVIEW = ARGS.get("review", "0") == "1"  # noqa: F821
OUT = {}

# the arena (Unity metres): the boundary's inner line is this ellipse; gates at these bearings (deg, clockwise from +Z)
RX, RZ = 42.0, 35.0
GATES = (0.0, 127.5, 232.5)
SECTION_W = 5.0


def jitter(obj, amount, seed, keep_bottom=True):
    rng = random.Random(seed)
    for v in obj.data.vertices:
        if keep_bottom and v.co.z < 0.01:
            continue
        v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * amount
    return obj


def finish(objs):
    bpy.context.view_layer.update()
    return objs


def unity_to_blender(x, z):
    return -x, -z


# ------------------------------------------------------------------------------------------------ the boundary

def palisade_section(name, seed=1):
    """Sharpened logs, 3.6-4.3 m, two rails behind; a strip of heaped dirt at the foot."""
    rng = random.Random(seed)
    parts = []
    n = 10
    for i in range(n):
        x = -SECTION_W / 2 + (i + 0.5) * SECTION_W / n
        h = rng.uniform(3.6, 4.3)
        sw = ("wood", "wood_dark", "wood", "wood_light")[rng.randrange(4)]
        body = K.cylinder("Log%d" % i, radius=0.27, depth=h, verts=6, location=(x, 0.27, 0), pivot=(0, 0, -1),
                          taper=rng.uniform(0.85, 0.95), swatch=sw)
        tip = K.cone("Tip%d" % i, radius=0.25, depth=0.45, verts=6, location=(x, 0.27, h), pivot=(0, 0, -1),
                     swatch="wood_light")
        parts += [body, tip]
    for z in (1.2, 2.9):
        parts.append(K.box("Rail", size=(SECTION_W + 0.1, 0.18, 0.2), location=(0, 0.62, z), pivot=(0, 0, 0),
                           swatch="wood_dark"))
    foot = K.box("Foot", size=(SECTION_W + 0.2, 0.9, 0.35), location=(0, 0.05, 0), pivot=(0, 0, -1), taper=(1.0, 0.4),
                 swatch="dirt")
    jitter(foot, 0.05, seed + 7)
    parts.append(foot)
    finish(parts)
    return K.join(parts, name)


def fence_section(name, seed=2):
    """An old plank fence, patched again and again: dark old boards, pale new ones, a broken one, a nailed-on patch,
    a brace, rope lashing. Lower than the palisade, so you see the world past it."""
    rng = random.Random(seed)
    parts = []
    for i, x in enumerate((-2.4, 0.0, 2.4)):
        parts.append(K.box("Post%d" % i, size=(0.22, 0.22, 2.5), location=(x, 0.14, 0), pivot=(0, 0, -1),
                           taper=(0.85, 0.85), swatch="wood_dark"))
    for r, z in enumerate((0.45, 1.15, 1.85)):
        for half in (-1, 1):
            cx = half * 1.2
            broken = (r == 1 and half == 1)
            new = rng.random() < 0.35
            w = 2.3 if not broken else 1.3
            p = K.box("Plank", size=(w, 0.06, 0.3), location=(cx - (0.5 if broken else 0), 0.0, z), pivot=(0, 0, 0),
                      swatch="wood_light" if new else ("wood_dark" if rng.random() < 0.5 else "wood"))
            p.rotation_euler = (0, math.radians(rng.uniform(-3, 3) + (9 if broken else 0)), 0)
            parts.append(p)
    patch = K.box("Patch", size=(0.9, 0.05, 0.55), location=(1.5, -0.05, 1.15), pivot=(0, 0, 0), swatch="wood_light")
    patch.rotation_euler = (0, math.radians(14), 0)
    parts.append(patch)
    brace = K.box("Brace", size=(2.6, 0.08, 0.16), location=(-1.2, 0.09, 1.1), pivot=(0, 0, 0), swatch="wood")
    brace.rotation_euler = (0, math.radians(-32), 0)
    parts.append(brace)
    for z in (0.9, 1.5):
        parts.append(K.cylinder("Rope", radius=0.15, depth=0.1, verts=6, location=(0.0, 0.14, z), pivot=(0, 0, 0),
                                swatch="rope"))
    lean = K.box("Leaning", size=(0.3, 0.05, 2.0), location=(-1.9, -0.35, 0), pivot=(0, 0, -1), swatch="wood_light")
    lean.rotation_euler = (math.radians(-12), 0, math.radians(4))
    parts.append(lean)
    finish(parts)
    return K.join(parts, name)


def berm_section(name, seed=3):
    """Earth dug out of the yard and heaped up: a slope from the yard (toe 1.2 m in front of the line) to a crest
    2.4 m up, a wattle fence along the crest, rocks at the toe. Walkable (its slope feeds the height grid)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    # cross-section (y outward, z up): toe, mid-slope, crest front, crest back, outside foot
    prof = [(-1.2, 0.0), (0.1, 1.1), (1.3, 2.25), (2.1, 2.35), (4.6, 0.3)]
    nx = 5
    grid = []
    for i in range(nx + 1):
        x = -SECTION_W / 2 - 0.15 + i * (SECTION_W + 0.3) / nx
        row = []
        for k, (y, z) in enumerate(prof):
            dz = 0.0 if k in (0, len(prof) - 1) else rng.uniform(-0.12, 0.12)
            dy = rng.uniform(-0.15, 0.15) if 0 < k < len(prof) - 1 else 0.0
            row.append(bm.verts.new((x, y + dy, z + dz)))
        grid.append(row)
    for i in range(nx):
        for k in range(len(prof) - 1):
            bm.faces.new((grid[i][k], grid[i + 1][k], grid[i + 1][k + 1], grid[i][k + 1]))
    earth = K.object_from_bmesh(name, bm, swatch="dirt")
    K.set_swatch_where(earth, "mud", lambda c, n: c.z < 0.9 and rng.random() < 0.45)
    K.set_swatch_where(earth, "grass_dry_d", lambda c, n: c.z > 1.9)
    K.set_swatch_where(earth, "grass_dry_d", lambda c, n: c.y > 2.2)  # the back slope: one quiet colour
    parts = [earth]
    n = 9
    for i in range(n):
        x = -SECTION_W / 2 + (i + 0.5) * SECTION_W / n
        st = K.cylinder("Stake%d" % i, radius=0.06, depth=1.25, verts=4, location=(x, 1.7, 2.1), pivot=(0, 0, -1),
                        swatch="wood_dark")
        st.rotation_euler = (math.radians(rng.uniform(-6, 6)), math.radians(rng.uniform(-5, 5)), 0)
        parts.append(st)
    for z in (2.55, 2.95):
        parts.append(K.box("Wattle", size=(SECTION_W, 0.1, 0.32), location=(0, 1.7, z), pivot=(0, 0, 0), swatch="straw"))
    for i in range(3):
        parts.append(K.ico("Rock%d" % i, radius=rng.uniform(0.22, 0.38), subdiv=1,
                           location=(rng.uniform(-2.2, 2.2), rng.uniform(-1.1, -0.6), 0), scale=(1.3, 1.0, 0.7),
                           pivot_z=-0.5, swatch="stone_grey" if i % 2 else "slate"))
    finish(parts)
    return K.join(parts, name)


def platform_section(name, seed=4):
    """A crude plank platform for onlookers: deck at 2.4 m running back 3 m, boarded front skirt facing the yard,
    a rail, cross braces, a ladder at the back."""
    rng = random.Random(seed)
    h, d = 2.4, 3.0
    parts = [K.box(name, size=(SECTION_W + 0.1, d, 0.16), location=(0, d / 2, h - 0.16), pivot=(0, 0, -1),
                   swatch="wood")]
    n = 8
    for i in range(n):
        x = -SECTION_W / 2 + (i + 0.5) * SECTION_W / n
        parts.append(K.box("Skirt%d" % i, size=(SECTION_W / n - 0.04, 0.08, h - rng.uniform(0.0, 0.2)), location=(x, 0.0, 0),
                           pivot=(0, 0, -1), swatch="wood_dark" if i % 3 else "wood"))
    for sx in (-1, 1):
        for y in (0.15, d - 0.15):
            parts.append(K.box("Leg", size=(0.2, 0.2, h), location=(sx * (SECTION_W / 2 - 0.12), y, 0), pivot=(0, 0, -1),
                               swatch="wood_dark"))
        br = K.box("Brace", size=(0.1, d * 1.15, 0.14), location=(sx * (SECTION_W / 2 - 0.12), d / 2, h * 0.5),
                   pivot=(0, 0, 0), swatch="wood")
        br.rotation_euler = (math.radians(38), 0, 0)
        parts.append(br)
    for x in (-SECTION_W / 2 + 0.1, 0, SECTION_W / 2 - 0.1):
        parts.append(K.box("RailPost", size=(0.1, 0.1, 0.95), location=(x, 0.1, h), pivot=(0, 0, -1), swatch="wood_dark"))
    parts.append(K.box("Rail", size=(SECTION_W, 0.08, 0.1), location=(0, 0.1, h + 0.85), pivot=(0, 0, 0), swatch="wood_light"))
    for z in (0.5, 1.1, 1.7, 2.3):
        parts.append(K.box("Rung", size=(0.6, 0.06, 0.06), location=(1.4, d + 0.25, z), pivot=(0, 0, 0), swatch="wood_light"))
    for sx in (-1, 1):
        parts.append(K.box("Ladder", size=(0.07, 0.07, h + 0.4), location=(1.4 + sx * 0.3, d + 0.25, 0), pivot=(0, 0, -1),
                           swatch="wood"))
    if seed % 2:
        parts.append(K.box("Cloth", size=(0.9, 0.03, 1.0), location=(-1.3, -0.06, h - 1.05), pivot=(0, 0, -1),
                           swatch="cloth_red"))
    finish(parts)
    return K.join(parts, name)


def yard_gate(name, opening=4.0, height=4.6):
    """The palisade's gate: two log towers, a log lintel with a skull, and two plank doors hinged at the towers
    (DoorL / DoorR children: ArenaGates swings them)."""
    root = K.empty(name)
    rng = random.Random(9)
    frame = []
    for side in (-1, 1):
        for k in range(3):
            x = side * (opening / 2 + 0.3 + k * 0.5)
            frame.append(K.cylinder("Tower%d" % k, radius=0.3, depth=height + 0.6 - k * 0.3, verts=6, location=(x, 0.3, 0),
                                    pivot=(0, 0, -1), swatch="wood_dark" if k == 0 else "wood"))
            frame.append(K.cone("Tip", radius=0.28, depth=0.5, verts=6, location=(x, 0.3, height + 0.6 - k * 0.3),
                                pivot=(0, 0, -1), swatch="wood_light"))
    frame.append(K.cylinder("Lintel", radius=0.3, depth=opening + 1.8, verts=6, location=(0, 0.3, height), pivot=(0, 0, 0),
                            axis="X", swatch="wood_dark"))
    frame.append(K.box("Skull", size=(0.36, 0.14, 0.32), location=(0, -0.05, height + 0.15), pivot=(0, 0, 0), swatch="bone"))
    frame[0].parent = root
    finish(frame)
    K.join(frame, name + "_Frame")
    leaf_w = opening / 2 - 0.02
    for side, sx in (("L", -1), ("R", 1)):
        hinge = (sx * opening / 2, 0.1, 0)
        planks = []
        for i in range(5):
            x = hinge[0] - sx * (i + 0.5) * leaf_w / 5
            planks.append(K.box("Door%s%d" % (side, i), size=(leaf_w / 5 - 0.03, 0.1, height - 0.5 - rng.uniform(0, 0.25)),
                                location=(x, hinge[1], 0.05), pivot=(0, 0, -1), swatch="wood" if i % 2 else "wood_dark"))
        for z in (0.6, height - 1.4):
            planks.append(K.box("Band", size=(leaf_w, 0.13, 0.16), location=(hinge[0] - sx * leaf_w / 2, hinge[1], z),
                                pivot=(0, 0, -1), swatch="rusty_iron"))
        h = K.box("Door" + side, size=(0.08, 0.12, height - 0.6), location=hinge, pivot=(-sx, 0, -1),
                  swatch="rusty_iron", parent=root)
        finish(planks)
        K.join([h] + planks, "Door" + side)
    return root


def shed(name):
    """A lean-to tool shed against the boundary: plank back wall, two posts, a sloping plank roof, a shelf of tools."""
    parts = [K.box(name, size=(4.2, 0.12, 2.8), location=(0, 1.6, 0), pivot=(0, 0, -1), swatch="wood_dark")]
    for x in (-1.95, 1.95):
        parts.append(K.box("Post", size=(0.18, 0.18, 2.15), location=(x, -0.4, 0), pivot=(0, 0, -1), swatch="wood"))
    roof = K.box("Roof", size=(4.6, 2.5, 0.12), location=(0, 0.6, 2.45), pivot=(0, 0, 0), swatch="wood")
    roof.rotation_euler = (math.radians(-14), 0, 0)
    parts.append(roof)
    parts.append(K.box("Shelf", size=(3.6, 0.5, 0.08), location=(0, 1.3, 1.1), pivot=(0, 0, 0), swatch="wood_light"))
    for i, x in enumerate((-1.3, -0.6, 0.3, 1.1)):
        parts.append(K.cylinder("Pot%d" % i, radius=0.17, depth=0.32, verts=6, location=(x, 1.3, 1.14), pivot=(0, 0, -1),
                                taper=0.7, swatch="rust" if i % 2 else "mud"))
    for i, x in enumerate((-1.6, -1.2)):
        t = K.box("Tool%d" % i, size=(0.06, 0.06, 1.6), location=(x, 1.45, 0), pivot=(0, 0, -1), swatch="wood_light")
        t.rotation_euler = (math.radians(-8), math.radians(6 - 12 * i), 0)
        parts.append(t)
    parts.append(K.box("Spade", size=(0.26, 0.04, 0.32), location=(-1.55, 1.38, 0.05), pivot=(0, 0, -1), swatch="iron"))
    parts.append(K.cylinder("Barrel", radius=0.36, depth=0.85, verts=8, location=(1.4, 0.6, 0), pivot=(0, 0, -1),
                            swatch="wood_dark"))
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ the ground

def ellipse_d(x, z, rx, rz):
    return math.sqrt((x / rx) ** 2 + (z / rz) ** 2)


def near_segment(px, pz, ax, az, bx, bz):
    vx, vz = bx - ax, bz - az
    t = max(0.0, min(1.0, ((px - ax) * vx + (pz - az) * vz) / (vx * vx + vz * vz)))
    dx, dz = px - (ax + vx * t), pz - (az + vz * t)
    return math.sqrt(dx * dx + dz * dz)


def yard_floor(name, cell=2.0, seed=31):
    """The oval yard, out past the boundary line (the sections stand on it). Low contrast: trodden paths from the
    gates to the middle, worn drill circles, a little dry grass near the edges."""
    rng = random.Random(seed)
    erx, erz = RX + 3.5, RZ + 3.5  # just under the boundary; the surrounds take over outside
    nx, nz = int(erx * 2 / cell) + 2, int(erz * 2 / cell) + 2
    x0, z0 = -nx * cell / 2, -nz * cell / 2
    bm = bmesh.new()
    verts = {}

    def v(i, k):
        if (i, k) not in verts:
            ux, uz = x0 + i * cell, z0 + k * cell
            h = rng.uniform(-0.04, 0.04)
            bx, by = unity_to_blender(ux, uz)
            verts[(i, k)] = bm.verts.new((bx, by, h))
        return verts[(i, k)]

    swatches = []
    gates = [(RX * math.sin(math.radians(a)), RZ * math.cos(math.radians(a))) for a in GATES]
    drills = [(17.0, 14.0, 6.5), (0.0, 4.0, 5.0), (-20.0, -8.0, 5.0)]  # worn circles: the drill yard, the ring, the W post
    for i in range(nx):
        for k in range(nz):
            ux, uz = x0 + (i + 0.5) * cell, z0 + (k + 0.5) * cell
            if ellipse_d(ux, uz, erx, erz) > 1.0:
                continue
            # counter-clockwise seen from above in Blender (x and y are both flipped from Unity: winding is kept)
            bm.faces.new((v(i, k), v(i + 1, k), v(i + 1, k + 1), v(i, k + 1)))
            sw = "dirt_floor"
            if rng.random() < 0.28:
                sw = "dirt_floor_d"
            if any(near_segment(ux, uz, 0, 0, gx, gz) < 1.6 for gx, gz in gates) and rng.random() < 0.75:
                sw = "dirt_floor_d"
            for cx, cz, r in drills:
                d = math.hypot(ux - cx, uz - cz)
                if d < r and rng.random() < 0.8:
                    sw = "dirt_light"
            if ellipse_d(ux, uz, RX, RZ) > 0.93 and rng.random() < 0.3:
                sw = "grass_dry_d"
            swatches.append(sw)
    obj = K.object_from_bmesh(name, bm, swatch="dirt_floor")
    for sw in set(swatches):
        K.set_swatch(obj, sw, [f for f, s in enumerate(swatches) if s == sw])
    return obj


def surrounds(name, seed=41):
    """The world outside the stockade (no colliders: you can't get there): flat by the walls, then rolling dry
    grassland rising into hills that fade into the fog; dirt roads lead away from the gates."""
    rng = random.Random(seed)
    seg = 96
    offsets = [2.5, 9.0, 18.0, 30.0, 46.0, 66.0, 92.0, 125.0, 165.0, 215.0]
    phases = [rng.uniform(0, 2 * math.pi) for _ in range(6)]

    def hill(a, dist):
        n = (math.sin(a * 3 + phases[0]) * 0.5 + math.sin(a * 5 + phases[1]) * 0.3 + math.sin(a * 9 + phases[2]) * 0.2)
        rise = max(0.0, dist - 25.0)
        return max(0.0, (0.55 + 0.45 * n) * rise * 0.17 + rng.uniform(-0.25, 0.25) * min(1.0, rise / 20))

    bm = bmesh.new()
    rings = []
    for k, off in enumerate(offsets):
        ring = []
        for i in range(seg):
            a = i / seg * 2 * math.pi  # bearing, clockwise from +Z (Unity)
            ex, ez = RX * math.sin(a), RZ * math.cos(a)
            el = math.hypot(ex, ez)
            ux, uz = ex + ex / el * off, ez + ez / el * off
            road = min(abs(((math.degrees(a) - g) + 180) % 360 - 180) for g in GATES[:2] + (GATES[2],))
            h = 0.06 if k == 0 else 0.08 if k == 1 else hill(a, off)
            if road < 4 and off < 50:
                h *= 0.4
            bx, by = unity_to_blender(ux, uz)
            ring.append(bm.verts.new((bx, by, h)))
        rings.append(ring)
    for k in range(len(offsets) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    obj = K.object_from_bmesh(name, bm, swatch="grass_dry")
    me = obj.data
    swatches = {}
    for f in me.polygons:
        c = f.center
        ux, uz = -c.x, -c.y
        a = math.degrees(math.atan2(ux, uz)) % 360
        road = min(abs(((a - g) + 180) % 360 - 180) for g in GATES)
        dist = math.hypot(ux, uz)
        # big soft regions, not a quilt: the background stays low-contrast
        nz = (math.sin(ux * 0.035 + 1.3) * math.cos(uz * 0.041 - 0.7) + 0.6 * math.sin((ux + uz) * 0.021 + 2.1)) / 1.6
        if road < 3.2 and dist < 150:
            sw = "dirt"
        elif math.hypot(ux / (RX + 14), uz / (RZ + 14)) < 1.0:
            sw = "dirt_light" if nz > 0.1 else "dirt"  # packed earth around the stockade
        elif nz > 0.55:
            sw = "grass_green"
        elif nz > -0.15:
            sw = "grass_dry"
        else:
            sw = "grass_dry_d"
        swatches.setdefault(sw, []).append(f.index)
    for sw, faces in swatches.items():
        K.set_swatch(obj, sw, faces)
    # make sure every face points up (the flipped axes reverse winding)
    bpy.context.view_layer.objects.active = obj
    bm2 = bmesh.new()
    bm2.from_mesh(me)
    for f in bm2.faces:
        if f.normal.z < 0:
            f.normal_flip()
    bm2.to_mesh(me)
    bm2.free()
    return obj


# ------------------------------------------------------------------------------------------------ yard furniture

def training_post(name):
    """A palus: the thick post recruits hack at all day, scarred and rope-bound."""
    parts = [K.box(name, size=(0.34, 0.34, 1.95), pivot=(0, 0, -1), taper=(0.9, 0.9), swatch="wood")]
    for z, sw in ((0.55, "rope"), (1.25, "rope"), (0.9, "wood_dark"), (1.6, "wood_dark")):
        parts.append(K.box("Band", size=(0.38, 0.38, 0.1), location=(0, 0, z), pivot=(0, 0, 0), swatch=sw))
    parts.append(K.box("Cap", size=(0.38, 0.38, 0.08), location=(0, 0, 1.95), pivot=(0, 0, -1), swatch="wood_light"))
    for i, (x, y) in enumerate(((0.3, 0.1), (-0.25, -0.2))):
        parts.append(K.ico("Stone%d" % i, radius=0.16, subdiv=1, location=(x, y, 0), scale=(1.2, 1, 0.6), pivot_z=-0.6,
                           swatch="stone_grey"))
    finish(parts)
    return K.join(parts, name)


def straw_dummy(name):
    parts = [K.box(name, size=(0.14, 0.14, 1.85), pivot=(0, 0, -1), swatch="wood_dark")]
    parts.append(K.box("Arms", size=(1.25, 0.12, 0.12), location=(0, 0, 1.35), pivot=(0, 0, 0), swatch="wood_dark"))
    parts.append(K.box("Body", size=(0.55, 0.38, 0.75), location=(0, 0, 0.78), pivot=(0, 0, -1), taper=(1.15, 1.1),
                       swatch="straw"))
    parts.append(K.box("Belt", size=(0.6, 0.42, 0.1), location=(0, 0, 0.95), pivot=(0, 0, 0), swatch="rope"))
    parts.append(K.box("Target", size=(0.22, 0.04, 0.22), location=(0, -0.21, 1.2), pivot=(0, 0, 0), swatch="cloth_red"))
    parts.append(K.ico("Head", radius=0.19, subdiv=1, location=(0, 0, 1.6), scale=(1, 1, 1.1), pivot_z=-1,
                       swatch="cloth_sack"))
    for sx in (-1, 1):
        parts.append(K.box("Tuft", size=(0.14, 0.14, 0.2), location=(sx * 0.62, 0, 1.35), pivot=(0, 0, 0), swatch="straw"))
    finish(parts)
    return K.join(parts, name)


def weapon_rack(name):
    """Practice arms: wooden swords and spears leaning in a rack, a round shield propped against it."""
    parts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            l = K.box("Leg", size=(0.09, 0.09, 1.5), location=(sx * 1.0, sy * 0.18, 0), pivot=(0, 0, -1), swatch="wood_dark")
            l.rotation_euler = (math.radians(-sy * 10), 0, 0)
            parts.append(l)
    parts.append(K.box(name, size=(2.2, 0.1, 0.1), location=(0, 0, 1.4), pivot=(0, 0, 0), swatch="wood"))
    parts.append(K.box("Low", size=(2.2, 0.5, 0.08), location=(0, 0, 0.25), pivot=(0, 0, 0), swatch="wood"))
    for i in range(4):
        x = -0.75 + i * 0.4
        s = K.box("Sword%d" % i, size=(0.09, 0.04, 1.15), location=(x, -0.1, 0.25), pivot=(0, 0, -1), swatch="wood_light")
        s.rotation_euler = (math.radians(-12), math.radians(3 - 2 * i), 0)
        g = K.box("Guard%d" % i, size=(0.26, 0.06, 0.05), location=(x, -0.06, 0.55), pivot=(0, 0, 0), swatch="wood_dark")
        parts += [s, g]
    for i, x in enumerate((0.85, 1.0)):
        sp = K.cylinder("Spear%d" % i, radius=0.035, depth=2.3, verts=4, location=(x, 0.1, 0.25), pivot=(0, 0, -1),
                        swatch="wood")
        sp.rotation_euler = (math.radians(10), math.radians(-4 - 3 * i), 0)
        parts.append(sp)
    sh = K.cylinder("Shield", radius=0.42, depth=0.08, verts=8, location=(-1.3, -0.35, 0.42), pivot=(0, 0, 0), axis="Y",
                    swatch="wood")
    sh.rotation_euler = (math.radians(-15), 0, math.radians(20))
    boss = K.cylinder("Boss", radius=0.12, depth=0.06, verts=6, location=(-1.3, -0.4, 0.42), pivot=(0, 0, 0), axis="Y",
                      swatch="iron")
    boss.rotation_euler = sh.rotation_euler
    parts += [sh, boss]
    finish(parts)
    return K.join(parts, name)


def trough(name):
    parts = [K.box(name, size=(2.2, 0.7, 0.55), location=(0, 0, 0.12), pivot=(0, 0, -1), taper=(1.05, 1.1),
                   swatch="wood_dark")]
    parts.append(K.box("Water", size=(2.0, 0.55, 0.02), location=(0, 0, 0.6), pivot=(0, 0, -1), swatch="water"))
    for x in (-0.8, 0.8):
        parts.append(K.box("Leg", size=(0.15, 0.8, 0.14), location=(x, 0, 0), pivot=(0, 0, -1), swatch="wood"))
    parts.append(K.cylinder("Bucket", radius=0.2, depth=0.36, verts=6, location=(1.4, -0.3, 0), pivot=(0, 0, -1),
                            taper=1.15, swatch="wood"))
    finish(parts)
    return K.join(parts, name)


def hay_bales(name):
    parts = []
    for i, (x, y, z, yaw) in enumerate(((-0.55, 0, 0, 3), (0.55, 0.05, 0, -4), (0.0, 0.02, 0.48, 8))):
        b = K.box(name if i == 0 else "Bale%d" % i, size=(1.0, 0.55, 0.48), location=(x, y, z), pivot=(0, 0, -1),
                  bevel=0.05, swatch="straw")
        b.rotation_euler = (0, 0, math.radians(yaw))
        parts.append(b)
        parts.append(K.box("Twine", size=(0.04, 0.57, 0.5), location=(x - 0.2, y, z - 0.01), pivot=(0, 0, -1),
                           swatch="rope"))
    finish(parts)
    return K.join(parts, name)


def chopping_block(name):
    parts = [K.cylinder(name, radius=0.36, depth=0.55, verts=7, pivot=(0, 0, -1), swatch="wood")]
    parts.append(K.cylinder("Top", radius=0.34, depth=0.02, verts=7, location=(0, 0, 0.55), pivot=(0, 0, -1),
                            swatch="wood_light"))
    h = K.box("Haft", size=(0.06, 0.06, 0.75), location=(0.05, 0, 0.55), pivot=(0, 0, -1), swatch="wood_dark")
    h.rotation_euler = (0, math.radians(35), 0)
    head = K.box("Head", size=(0.22, 0.06, 0.16), location=(0.0, 0, 0.6), pivot=(0, 0, 0), swatch="iron")
    head.rotation_euler = (0, math.radians(35), 0)
    parts += [h, head]
    for i in range(5):
        lg = K.cylinder("Log%d" % i, radius=0.13, depth=0.8, verts=5, location=(-0.9, -0.3 + (i % 3) * 0.27,
                        0.13 + (i // 3) * 0.24), pivot=(0, 0, 0), axis="Y", swatch="wood" if i % 2 else "wood_dark")
        parts.append(lg)
    finish(parts)
    return K.join(parts, name)


def wheelbarrow(name):
    parts = [K.box(name, size=(0.75, 1.05, 0.38), location=(0, 0, 0.35), pivot=(0, 0, -1), taper=(1.25, 1.15),
                   swatch="wood")]
    parts.append(K.box("Load", size=(0.7, 0.95, 0.12), location=(0, 0, 0.66), pivot=(0, 0, -1), taper=(0.7, 0.7),
                       swatch="dirt"))
    parts.append(K.cylinder("Wheel", radius=0.26, depth=0.1, verts=8, location=(0, -0.7, 0.26), pivot=(0, 0, 0), axis="X",
                            swatch="wood_dark"))
    for sx in (-1, 1):
        parts.append(K.box("Handle", size=(0.06, 1.4, 0.06), location=(sx * 0.3, 0.45, 0.5), pivot=(0, 0, 0),
                           swatch="wood_dark"))
        parts.append(K.box("Leg", size=(0.06, 0.06, 0.4), location=(sx * 0.3, 0.45, 0), pivot=(0, 0, -1), swatch="wood_dark"))
    finish(parts)
    return K.join(parts, name)


def sacks(name):
    rng = random.Random(12)
    parts = []
    for i, (x, y, z) in enumerate(((0, 0, 0), (0.55, 0.1, 0), (0.25, 0.05, 0.42), (-0.5, 0.15, 0))):
        s = K.ico(name if i == 0 else "Sack%d" % i, radius=0.3, subdiv=1, location=(x, y, z), scale=(1.1, 0.9, 0.85),
                  pivot_z=-1, swatch="cloth_sack")
        jitter(s, 0.03, i)
        parts.append(s)
        parts.append(K.box("Tie", size=(0.12, 0.12, 0.1), location=(x, y, z + 0.5), pivot=(0, 0, -1), swatch="rope"))
    finish(parts)
    return K.join(parts, name)


def sparring_ring(name, radius=4.8):
    """Stones laid in a ring on the floor where pairs spar (flat decor, nothing to trip on)."""
    rng = random.Random(14)
    parts = []
    n = 18
    for i in range(n):
        a = i / n * 2 * math.pi + rng.uniform(-0.05, 0.05)
        parts.append(K.ico(name if i == 0 else "S%d" % i, radius=rng.uniform(0.18, 0.26), subdiv=1,
                           location=(math.cos(a) * radius, math.sin(a) * radius, 0), scale=(1.3, 1.0, 0.45),
                           pivot_z=-0.4, swatch="stone_grey" if i % 3 else "slate"))
    finish(parts)
    return K.join(parts, name)


def log_bench(name):
    parts = [K.cylinder(name, radius=0.22, depth=2.0, verts=6, location=(0, 0, 0.45), pivot=(0, 0, 0), axis="X",
                        swatch="wood")]
    for x in (-0.75, 0.75):
        parts.append(K.cylinder("Stump", radius=0.2, depth=0.3, verts=6, location=(x, 0, 0), pivot=(0, 0, -1),
                                swatch="wood_dark"))
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ outside

def tree_pine(name, seed=51):
    rng = random.Random(seed)
    parts = [K.cylinder(name, radius=0.22, depth=2.0, verts=5, pivot=(0, 0, -1), taper=0.7, swatch="wood_dark")]
    for i, (r, z, h) in enumerate(((2.0, 1.4, 2.8), (1.6, 3.0, 2.6), (1.1, 4.5, 2.6))):
        c = K.cone("Crown%d" % i, radius=r, depth=h, verts=6, location=(0, 0, z), pivot=(0, 0, -1),
                   swatch="leaf_d" if i % 2 == 0 else "leaf")
        c.rotation_euler = (0, 0, rng.uniform(0, 1))
        parts.append(c)
    finish(parts)
    return K.join(parts, name)


def tree_oak(name, seed=52):
    rng = random.Random(seed)
    parts = [K.cylinder(name, radius=0.32, depth=2.6, verts=6, pivot=(0, 0, -1), taper=0.65, swatch="wood_dark")]
    for i, (x, y, z, r) in enumerate(((0, 0, 3.4, 1.9), (1.1, 0.4, 3.0, 1.3), (-0.9, -0.5, 3.1, 1.4))):
        b = K.ico("Crown%d" % i, radius=r, subdiv=1, location=(x, y, z), scale=(1, 1, 0.8), swatch="leaf" if i else "leaf_d")
        jitter(b, 0.12, seed + i, keep_bottom=False)
        parts.append(b)
    finish(parts)
    return K.join(parts, name)


def bush(name, seed=53):
    parts = []
    for i, (x, r) in enumerate(((0, 0.8), (0.7, 0.55))):
        b = K.ico(name if i == 0 else "B%d" % i, radius=r, subdiv=1, location=(x, 0, 0), scale=(1, 1, 0.7), pivot_z=-0.6,
                  swatch="leaf_d" if i == 0 else "leaf")
        parts.append(b)
    finish(parts)
    return K.join(parts, name)


def hut(name):
    """A round wattle-and-daub hut with a thatched cone roof: where the pit's slaves sleep."""
    parts = [K.cylinder(name, radius=2.2, depth=2.0, verts=8, pivot=(0, 0, -1), swatch="dirt_light")]
    parts.append(K.cone("Roof", radius=2.8, depth=2.3, verts=8, location=(0, 0, 1.9), pivot=(0, 0, -1), swatch="straw"))
    parts.append(K.box("Door", size=(0.8, 0.1, 1.5), location=(0, -2.17, 0), pivot=(0, 0, -1), swatch="wood_dark"))
    finish(parts)
    return K.join(parts, name)


def longhouse(name):
    """The slavers' longhouse: timber walls, a steep thatched roof, a door and shutters."""
    parts = [K.box(name, size=(8.0, 4.4, 2.4), pivot=(0, 0, -1), swatch="wood_dark")]
    roof = K.wedge("RoofA", size=(8.6, 2.6, 2.2), location=(0, -1.3, 2.4), pivot=(0, 0, -1), swatch="straw")
    roofb = K.wedge("RoofB", size=(8.6, 2.6, 2.2), location=(0, 1.3, 2.4), pivot=(0, 0, -1), swatch="straw")
    roofb.rotation_euler = (0, 0, math.pi)
    parts += [roof, roofb]
    parts.append(K.box("Door", size=(1.0, 0.1, 1.8), location=(0, -2.23, 0), pivot=(0, 0, -1), swatch="wood"))
    for x in (-2.5, 2.5):
        parts.append(K.box("Shutter", size=(0.7, 0.1, 0.6), location=(x, -2.23, 1.2), pivot=(0, 0, -1), swatch="wood_light"))
    finish(parts)
    return K.join(parts, name)


def tent(name, seed=54):
    bm = bmesh.new()
    w, d, h = 2.4, 3.2, 1.9
    a, b, c, e = (bm.verts.new(p) for p in ((-w / 2, -d / 2, 0), (w / 2, -d / 2, 0), (w / 2, d / 2, 0), (-w / 2, d / 2, 0)))
    t0, t1 = bm.verts.new((0, -d / 2, h)), bm.verts.new((0, d / 2, h))
    bm.faces.new((a, b, t0))
    bm.faces.new((c, e, t1))
    bm.faces.new((b, c, t1, t0))
    bm.faces.new((e, a, t0, t1))
    obj = K.object_from_bmesh(name, bm, swatch="cloth_sack" if seed % 2 else "cloth_white")
    return obj


def watchtower(name):
    """The slavers' watchtower outside the gate: four legs, braces, a railed platform, a thatched roof."""
    h = 6.0
    parts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            leg = K.box("Leg", size=(0.25, 0.25, h + 2.2), location=(sx * 1.1, sy * 1.1, 0), pivot=(0, 0, -1),
                        swatch="wood_dark")
            leg.rotation_euler = (math.radians(-sy * 3), math.radians(sx * 3), 0)
            parts.append(leg)
    for z in (1.6, 3.8):
        for side in range(2):
            br = K.box("Brace", size=(2.6, 0.1, 0.14), location=(0, (-1 if side else 1) * 1.15, z), pivot=(0, 0, 0),
                       swatch="wood")
            br.rotation_euler = (0, math.radians(35 if side else -35), 0)
            parts.append(br)
    parts.insert(0, K.box(name, size=(2.9, 2.9, 0.18), location=(0, 0, h), pivot=(0, 0, -1), swatch="wood"))
    for sy in (-1, 1):
        parts.append(K.box("Wall", size=(2.9, 0.08, 0.9), location=(0, sy * 1.42, h + 0.18), pivot=(0, 0, -1),
                           swatch="wood_dark"))
    parts.append(K.box("WallS", size=(0.08, 2.9, 0.9), location=(1.42, 0, h + 0.18), pivot=(0, 0, -1), swatch="wood_dark"))
    parts.append(K.cone("Roof", radius=2.4, depth=1.6, verts=4, location=(0, 0, h + 2.2), pivot=(0, 0, -1), swatch="straw"))
    finish(parts)
    return K.join(parts, name)


def cart(name):
    parts = [K.box(name, size=(1.6, 2.6, 0.2), location=(0, 0, 0.75), pivot=(0, 0, -1), swatch="wood")]
    for sx in (-1, 1):
        parts.append(K.box("Side", size=(0.08, 2.6, 0.45), location=(sx * 0.78, 0, 0.95), pivot=(0, 0, -1), swatch="wood_dark"))
        parts.append(K.cylinder("Wheel", radius=0.6, depth=0.12, verts=8, location=(sx * 0.92, 0.2, 0.6), pivot=(0, 0, 0),
                                axis="X", swatch="wood_dark"))
        parts.append(K.box("Shaft", size=(0.08, 1.8, 0.08), location=(sx * 0.4, -2.1, 0.55), pivot=(0, 0, 0), swatch="wood"))
    parts.append(K.box("Load", size=(1.3, 1.4, 0.5), location=(0, 0.4, 0.95), pivot=(0, 0, -1), swatch="cloth_sack"))
    finish(parts)
    return K.join(parts, name)


def rock(name, seed=55):
    r = K.ico(name, radius=1.0, subdiv=1, scale=(1.3, 1.0, 0.7), pivot_z=-0.5, swatch="stone_grey")
    jitter(r, 0.18, seed, keep_bottom=False)
    rng = random.Random(seed)
    K.set_swatch_where(r, "slate", lambda c, n: rng.random() < 0.4)
    return r


# ------------------------------------------------------------------------------------------------ build

K.clear_scene()
if PART == "enclosure":
    items = {
        "SM_YardPalisade": palisade_section("SM_YardPalisade"),
        "SM_YardFence": fence_section("SM_YardFence"),
        "SM_YardBerm": berm_section("SM_YardBerm"),
        "SM_YardPlatform": platform_section("SM_YardPlatform", 5),
        "SM_YardPlatformB": platform_section("SM_YardPlatformB", 6),
        "SM_YardGate": yard_gate("SM_YardGate"),
        "PR_Shed": shed("PR_Shed"),
    }
elif PART == "ground":
    items = {
        "SM_PitYard": yard_floor("SM_PitYard"),
        "SM_PitSurrounds": surrounds("SM_PitSurrounds"),
    }
elif PART == "props":
    items = {
        "PR_TrainingPost": training_post("PR_TrainingPost"),
        "PR_StrawDummy": straw_dummy("PR_StrawDummy"),
        "PR_WeaponRack": weapon_rack("PR_WeaponRack"),
        "PR_Trough": trough("PR_Trough"),
        "PR_HayBales": hay_bales("PR_HayBales"),
        "PR_ChoppingBlock": chopping_block("PR_ChoppingBlock"),
        "PR_Wheelbarrow": wheelbarrow("PR_Wheelbarrow"),
        "PR_Sacks": sacks("PR_Sacks"),
        "PR_SparringRing": sparring_ring("PR_SparringRing"),
        "PR_LogBench": log_bench("PR_LogBench"),
    }
else:
    items = {
        "SM_TreePine": tree_pine("SM_TreePine"),
        "SM_TreeOak": tree_oak("SM_TreeOak"),
        "SM_Bush": bush("SM_Bush"),
        "SM_Hut": hut("SM_Hut"),
        "SM_Longhouse": longhouse("SM_Longhouse"),
        "SM_Tent": tent("SM_Tent"),
        "SM_Watchtower": watchtower("SM_Watchtower"),
        "SM_Cart": cart("SM_Cart"),
        "SM_Rock": rock("SM_Rock"),
    }
for n, o in items.items():
    OUT[n] = K.tri_count(K.descendants(o))
    K.export_fbx(o, "Environment/" + n)
if REVIEW and PART != "ground":
    # a lineup of everything, side by side, for the 3-angle review
    lineup = K.empty("Lineup")
    x = 0.0
    for n, o in items.items():
        lo, hi = K.world_bounds([m for m in K.descendants(o) if m.type == "MESH"])
        w = hi.x - lo.x
        o.location.x += x - lo.x
        o.parent = lineup
        x += w + 0.8
    bpy.context.view_layer.update()
    OUT["review"] = K.review_render(lineup, "slave_pit_yard_" + PART, size=640)
OUT["blend"] = K.save_blend("slave_pit_yard_" + PART)
result = OUT
