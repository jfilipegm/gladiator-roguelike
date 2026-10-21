"""Imperial Arena kit (R1, docs/tracks/R1-imperial-arena.md): palette families A6 (travertine + imperial purple) for
stone and cloth, A5 (gold + crimson) for trim and the boss's gate. Arena 5 is a hippodrome along Unity X: straight
sides at z = +-44 from x = -95 (the carceres, a straight wall of arched starting stalls) to x = 51, a semicircle of
radius 44 round (51, 0) to x = 95 (the sphendone, the triumphal gate at its apex). The spina runs along z = 0 from
x = -60 to 40 (a 1.2 m walkway, 6 m wide, the metae at both ends, the obelisk at -10, the lap counter at 20); the
chariots' track lane, 4 m wide, loops 14 m round the spina's axis and is drawn darker on the floor.

ARGS: part=terrain|structures|gates|spina|overview|reference [review=<model name>|all]. Every piece goes to
Assets/Art/Models/Environment/. Origins at the base; wall-like pieces face -Y (their front, toward the arena, arrives
as Unity +Z). The floor is built in Unity coordinates and mapped through B(): the export's 180 deg turn puts it back.
part=overview builds a preview of the whole arena (a stand-in for the layout code, not the layout) and renders it;
part=reference measures the Grand Arena's FBX for the triangle budget. Seeded randomness: re-runs are identical.

The hippodrome's numbers are shared with the layout code (ImperialArenaLayout.cs, wave 2): keep them in step, and see
docs/tracks/R1-env-art.md for the dimensions it must use.
"""
import importlib
import math
import os
import random

import bmesh
import bpy
import gladiator_kit as K
from mathutils import Matrix, Vector

importlib.reload(K)
PART = ARGS.get("part", "terrain")  # noqa: F821  (ARGS injected by Tools/bheadless / blender_bridge.py)
OUT = {}

# ------------------------------------------------------------------------------------------------ the hippodrome (Unity XZ)
WALL_Z, WEST_X = 44.0, -95.0    # the straight sides' z, the carceres' x
EAST_C, EAST_R = 51.0, 44.0     # the sphendone: a semicircle round (EAST_C, 0)
SPINA_X0, SPINA_X1 = -60.0, 40.0
SPINA_W, SPINA_H = 6.0, 1.2
LANE_C, LANE_W = 14.0, 4.0      # the track lane: its centre line's distance from the spina's axis, its width
LANE_IN, LANE_OUT = LANE_C - LANE_W / 2, LANE_C + LANE_W / 2
RUTS = ((12.85, 13.15), (14.85, 15.15))  # two wheel ruts in the lane, 1.8 m apart round its centre line
OBELISK_X, COUNTER_X = -10.0, 20.0
FLOOR_MARGIN = 1.5              # the floor reaches this far under the walls
# the stands' profile: GrandArenaLayout's (podium, cap, tiers), so the crowd-zone code is reused
PODIUM, PODIUM_CAP, TIER_RISE, TIER_RUN, TIERS = 3.2, 0.6, 0.45, 0.8, 8
BACK_TOP, BACK_DEPTH = 8.4, 0.8
STAND_W = 4.0
GATE_HALF = 5.5                 # SM_ImpGate: 11 m wide, a 5 m opening (ArenaBuilder's 4.4 m GateBlocker fits it)
TRIUMPH_HALF = 10.5             # SM_TriumphalGate: 21 m wide, the same 5 m opening, taller (8 m doors)
STAIRS_RUN, STAIRS_STEPS, STAIRS_W = 0.5, 4, 3.0  # SM_SpinaStairs: 4 x 0.3 m in 2 m


def B(x, z, h=0.0):
    """A Unity-space point (x, z, height) in Blender coordinates (the export turns the model 180 deg)."""
    return Vector((-x, -z, h))


def spina_d(x, z):
    """Distance from Unity (x, z) to the spina's axis (the segment between the metae's centres)."""
    cx = min(max(x, SPINA_X0), SPINA_X1)
    return math.hypot(x - cx, z)


def wall_d(x, z):
    """Signed distance inside the wall line (negative behind it)."""
    d = min(WALL_Z - abs(z), x - WEST_X)
    if x > EAST_C:
        d = min(d, EAST_R - math.hypot(x - EAST_C, z))
    return d


def finish(objs):
    bpy.context.view_layer.update()
    return objs


def at_origin(obj):
    """K.join keeps the first part's origin: move the mesh so the piece's origin is the world origin again."""
    if obj.type != "MESH" or obj.children:
        return obj
    obj.data.transform(Matrix.Translation(obj.location))
    obj.location = (0, 0, 0)
    return obj


def at_root(obj):
    """A gate's frame (a child of the root empty) with its origin on the root's, like the doors' hinges are."""
    obj.data.transform(Matrix.Translation(obj.location))
    obj.location = (0, 0, 0)
    return obj


def paint(obj, swatch_of_face):
    """Collapse each face's UVs onto the swatch swatch_of_face(poly) returns (None = leave it)."""
    uvs = K.palette()
    uvl = obj.data.uv_layers.active.data
    for poly in obj.data.polygons:
        sw = swatch_of_face(poly)
        if sw is None:
            continue
        u, v = uvs[sw]
        for li in poly.loop_indices:
            uvl[li].uv = (u, v)


def arc(cx, cz, r, a0, a1, n):
    """n + 1 points on a circle (x, z) from angle a0 to a1 (radians), both ends included."""
    return [(cx + r * math.cos(a0 + (a1 - a0) * i / n), cz + r * math.sin(a0 + (a1 - a0) * i / n)) for i in range(n + 1)]


def d_prism(name, radius, height, z0=0.0, segs=8, swatch="travertine"):
    """A half disc prism (the metae's base): the round side toward -Y (the front), the flat side on y = 0."""
    pts = [(radius * math.cos(math.pi + math.pi * i / segs), radius * math.sin(math.pi + math.pi * i / segs))
           for i in range(segs + 1)]
    return K.extrude_poly(name, pts, height, plane="XY", location=(0, 0, z0 + height / 2), swatch=swatch)


def arch_outline(half, spring, top, edge, segs=6):
    """A wall's outline (x, z) above and beside an arched opening: from the bottom of the left edge round the arch to
    the right edge and over the top. `half` = the opening's half width (the arch's radius), `edge` = the wall's half
    width. Concave; extrude_poly fills it."""
    pts = [(-edge, 0.0), (-half, 0.0)]
    pts += [(x, z) for x, z in arc(0.0, spring, half, math.pi, 0.0, segs)]
    pts += [(half, 0.0), (edge, 0.0), (edge, top), (-edge, top)]
    return pts


def half_annulus(r0, r1, spring, segs=6):
    """An archivolt band (x, z): a half ring between radii r0 < r1 round (0, spring)."""
    outer = arc(0.0, spring, r1, 0.0, math.pi, segs)
    inner = arc(0.0, spring, r0, math.pi, 0.0, segs)
    return outer + inner


def half_disc(r, spring, segs=6):
    return [(r, spring)] + arc(0.0, spring, r, 0.0, math.pi, segs)[1:]


def soffits(obj, swatch, front_swatch=None):
    """Faces of an extruded wall that don't face front or back (the arch's soffit, the jambs) get `swatch`."""
    def f(p):
        if abs(p.normal.y) < 0.5:
            return swatch
        return front_swatch
    paint(obj, f)


# ------------------------------------------------------------------------------------------------ terrain

def stadium_loop(r, step=2.0, arc_step=1.9):
    """The closed loop (Unity x, z) at distance r from the spina's axis, clockwise from above: the north straight
    west to east (on the 2 m lattice), the east half circle, the south straight, the west half circle."""
    pts = []
    n = int(round((SPINA_X1 - SPINA_X0) / step))
    m = max(8, int(math.ceil(math.pi * r / arc_step)))
    pts += [(SPINA_X0 + i * step, r) for i in range(n)]
    pts += arc(SPINA_X1, 0.0, r, math.pi / 2, -math.pi / 2, m)[:-1]
    pts += [(SPINA_X1 - i * step, -r) for i in range(n)]
    pts += arc(SPINA_X0, 0.0, r, -math.pi / 2, -3 * math.pi / 2, m)[:-1]
    return pts


def outer_loop(margin=FLOOR_MARGIN, step=2.0):
    """The wall line grown by `margin` (where the floor ends, under the stands), clockwise from above."""
    zt, xw, r = WALL_Z + margin, WEST_X - margin, EAST_R + margin
    n = int(math.ceil((EAST_C - xw) / step))
    m = int(math.ceil(math.pi * r / step))
    k = int(math.ceil(2 * zt / step))
    pts = [(xw + (EAST_C - xw) * i / n, zt) for i in range(n)]
    pts += arc(EAST_C, 0.0, r, math.pi / 2, -math.pi / 2, m)[:-1]
    pts += [(EAST_C - (EAST_C - xw) * i / n, -zt) for i in range(n)]
    pts += [(xw, -zt + 2 * zt * i / k) for i in range(k)]
    return pts


def lane_swatch(d):
    """The floor's swatch at distance d from the spina's axis (None = the open sand)."""
    # iteration 3: packed grey-brown earth with mud ruts. v1's sandstone leaned orange (orange-red is the hazard
    # colour, and the race's telegraph has to stand out on the lane); v2's darker sand faded at grazing angles
    for a, b in RUTS:
        if a < d < b:
            return "mud"
    if LANE_IN < d < LANE_OUT:
        return "dirt_floor"
    return None


def imp_floor(name, seed=67):
    """Pale sand over the hippodrome, reaching 1.5 m under the walls, and the chariots' track lane in a darker sand
    with two wheel ruts. A constrained Delaunay triangulation of a 2 m lattice plus the lane's edge loops (exact
    stadium curves: straights at |z| = 12 and 16, half circles round the metae) and the outer edge, so the lane's
    edges are crisp and welded. Sand patches keep away from the lane so it reads as "the track"."""
    from mathutils.geometry import delaunay_2d_cdt
    rng = random.Random(seed)
    loops = [outer_loop()] + [stadium_loop(r) for r in (LANE_IN, RUTS[0][0], RUTS[0][1], RUTS[1][0], RUTS[1][1], LANE_OUT)]
    radii = (LANE_IN, LANE_OUT) + tuple(r for ab in RUTS for r in ab)
    pts, edges = [], []
    for loop in loops:
        base = len(pts)
        pts += loop
        edges += [(base + i, base + (i + 1) % len(loop)) for i in range(len(loop))]
    for xi in range(-48, 49):
        for zi in range(-23, 24):
            x, z = xi * 2.0, zi * 2.0
            if wall_d(x, z) + FLOOR_MARGIN < 0.9:
                continue
            d = spina_d(x, z)
            if any(abs(d - r) < 0.7 for r in radii):
                continue
            pts.append((x, z))
    res = delaunay_2d_cdt([Vector(p) for p in pts], edges, [], 0, 1e-4)
    out_v, out_f = res[0], res[2]
    bm = bmesh.new()
    verts = [bm.verts.new(B(v.x, v.y, rng.uniform(-0.012, 0.012))) for v in out_v]
    for f in out_f:
        a, b, c = (verts[i] for i in f)
        n = (b.co - a.co).cross(c.co - a.co)
        if abs(n.z) < 1e-6:
            continue  # collinear hull points
        face = bm.faces.new((a, b, c) if n.z > 0 else (a, c, b))
    obj = K.object_from_bmesh(name, bm, swatch="sand_l")

    def u(c):  # a face centre (Blender) back to Unity (x, z)
        return -c.x, -c.y

    def cell_patch(x, z):  # a 2 m cell's sand patch, the same for every triangle in it
        cx, cz = int(math.floor(x / 2.0)), int(math.floor(z / 2.0))
        return random.Random(cx * 7919 + cz * 104729 + seed).random() < 0.05

    def swatch(p):
        x, z = u(p.center)
        d = spina_d(x, z)
        sw = lane_swatch(d)
        if sw:
            return sw
        if (d < LANE_IN - 1.5 or d > LANE_OUT + 1.5) and cell_patch(x, z):
            return "sand"
        return None
    paint(obj, swatch)
    # the measured lane, for the notes: its area and the extent of its faces' distances
    area, dmin, dmax = 0.0, 1e9, 0.0
    for p in obj.data.polygons:
        x, z = u(p.center)
        d = spina_d(x, z)
        if LANE_IN < d < LANE_OUT:
            area += p.area
            for vi in p.vertices:
                co = obj.data.vertices[vi].co
                dv = spina_d(-co.x, -co.y)
                dmin, dmax = min(dmin, dv), max(dmax, dv)
    OUT["lane_area_m2"] = round(area, 1)
    OUT["lane_area_expected_m2"] = round(2 * (SPINA_X1 - SPINA_X0) * LANE_W + math.pi * (LANE_OUT ** 2 - LANE_IN ** 2), 1)
    OUT["lane_vertex_d_range"] = (round(dmin, 3), round(dmax, 3))
    OUT["floor_area_m2"] = round(sum(p.area for p in obj.data.polygons), 1)
    return obj


# ------------------------------------------------------------------------------------------------ structures

def profile():
    """The stands' section (outward offset, height, swatch of the face from this point to the next): the podium (a
    purple band between gold trims under a travertine cap), eight tiers of seats, the purple parapet at the back."""
    p = [(0.0, -0.3, "travertine"), (0.0, PODIUM - 0.85, "gold_d"), (0.0, PODIUM - 0.75, "purple"),
         (0.0, PODIUM - 0.2, "gold_d"), (0.0, PODIUM - 0.05, "travertine_d"), (0.0, PODIUM, "travertine")]
    o, h = PODIUM_CAP, PODIUM
    p.append((o, h, "travertine"))
    for k in range(TIERS):
        h += TIER_RISE
        p.append((o, h, "travertine_d" if k % 2 else "travertine"))
        o += TIER_RUN
        p.append((o, h, "purple" if k == TIERS - 1 else "travertine"))
    p.append((o, BACK_TOP - 0.2, "gold_d"))
    p.append((o, BACK_TOP, "travertine_d"))
    p.append((o + BACK_DEPTH, BACK_TOP, "travertine_d"))
    p.append((o + BACK_DEPTH, -0.3, None))
    return p


def straight_at(width):
    def pos(t, o, h):
        return Vector((t * width / 2, o, h))
    return pos


def curved_at(radius, chord):
    """Ring positions for a section on a curve of `radius` (the wall line): the front face is the chord (y = 0), the
    sides run toward the curve's centre, so sections tile round it. Scaling X keeps them tiling for other chords."""
    s = chord / (2 * radius)
    c = math.sqrt(1 - s * s)

    def pos(t, o, h):
        return Vector((t * (radius + o) * s, o * c, h))
    return pos


def stand_section(name, pos=None, width=STAND_W):
    """A 4 m run of the stands: the profile swept between two rings (front face at y = 0, rising toward +Y), a
    travertine pilaster with a gold capital at its west edge (they repeat every section), a gold rosette on the purple
    band, a purple banner flying on a gilded pole above the parapet."""
    pos = pos or straight_at(width)
    prof = profile()
    bm = bmesh.new()
    rings = [[bm.verts.new(pos(t, o, h)) for o, h, _ in prof] for t in (-1, 1)]
    for j in range(len(prof)):
        k = (j + 1) % len(prof)
        f = bm.faces.new((rings[0][j], rings[0][k], rings[1][k], rings[1][j]))
        f.material_index = j
    for ring in rings:
        cap = bm.faces.new(ring)
        cap.material_index = 255
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if f.material_index == 255], quad_method="BEAUTY",
                          ngon_method="EAR_CLIP")
    obj = K.object_from_bmesh(name, bm, swatch="travertine")
    tags = {p.index: p.material_index for p in obj.data.polygons}
    paint(obj, lambda p: "travertine_d" if tags[p.index] >= len(prof) else (prof[tags[p.index]][2] or "travertine_d"))
    for poly in obj.data.polygons:
        poly.material_index = 0
    wx = pos(-1, 0, 0).x
    back_o = PODIUM_CAP + TIER_RUN * TIERS
    pole = pos(0.3, back_o + BACK_DEPTH * 0.5, BACK_TOP)
    parts = [obj,
             K.box("Pilaster", size=(0.5, 0.25, PODIUM - 0.25), location=(wx + 0.25, -0.12, 0), pivot=(0, 0, -1),
                   swatch="travertine_d"),
             K.box("PilasterCap", size=(0.62, 0.34, 0.16), location=(wx + 0.25, -0.12, PODIUM - 0.25), pivot=(0, 0, -1),
                   swatch="gold_d"),
             K.cylinder("Rosette", radius=0.2, depth=0.08, verts=8, location=(0.6, -0.04, PODIUM - 0.475), pivot=(0, 0, 0),
                        swatch="gold", axis="Y"),
             K.box("PennantPole", size=(0.07, 0.07, 2.6), location=tuple(pole), pivot=(0, 0, -1), swatch="gold_d"),
             K.box("Pennant", size=(0.9, 0.08, 1.5), location=(pole.x + 0.48, pole.y, BACK_TOP + 2.55), pivot=(0, 0, 1),
                   swatch="purple_l")]
    finish(parts)
    return K.join(parts, name)


def tower(name, size=8.0, height=11.0):
    """Closes a corner where two wall runs meet (the carceres' oppida): a travertine block with a purple band, a
    cornice, dark arches and a gilded pyramid roof (SM_GrandTower's shape in A6)."""
    parts = [K.box(name, size=(size, size, height), pivot=(0, 0, -1), swatch="travertine"),
             K.box("Band", size=(size + 0.06, size + 0.06, 0.7), location=(0, 0, height * 0.55), pivot=(0, 0, -1),
                   swatch="purple"),
             K.box("Cornice", size=(size + 0.6, size + 0.6, 0.45), location=(0, 0, height), pivot=(0, 0, -1),
                   swatch="travertine_d"),
             K.cone("Roof", radius=size * 0.62, depth=3.2, verts=4, location=(0, 0, height + 0.45), pivot=(0, 0, -1),
                    swatch="gold_d"),
             K.cylinder("Finial", radius=0.25, depth=1.2, verts=6, location=(0, 0, height + 3.4), pivot=(0, 0, -1),
                        swatch="gold")]
    parts[3].rotation_euler.z = math.radians(45)
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(K.box("Arch%d%d" % (sx, sy), size=(1.6, 0.1, 2.6), location=(sx * size * 0.22, sy * (size / 2 + 0.03), height * 0.68),
                               pivot=(0, 0, -1), swatch="shadow"))
    finish(parts)
    return K.join(parts, name)


def carceres_bay(name, width=4.0, depth=1.0, opening=2.8, spring=3.0):
    """One starting stall of the carceres (the west end's straight wall, 4 m a bay): an arched opening with a
    purple archivolt and a gold keystone, bronze stall gates closed in front of a dark stall, a purple frieze under
    the entablature, an attic with a gold plaque, a cornice at the stands' height (8.4 m) and a purple banner above.
    A travertine pilaster centred on the bay's west edge (it overhangs by 0.3 m: the next bay's pier). Front at
    y = 0, origin at the front face's bottom centre."""
    half, edge = opening / 2, width / 2
    top = 5.2
    wall = K.extrude_poly(name, arch_outline(half, spring, top, edge), depth, plane="XZ", location=(0, depth / 2, 0),
                          swatch="travertine")
    soffits(wall, "travertine_d")
    parts = [wall,
             K.extrude_poly("Archivolt", half_annulus(half, half + 0.28, spring), 0.1, plane="XZ", location=(0, -0.04, 0),
                            swatch="purple"),
             K.box("Keystone", size=(0.4, 0.16, 0.5), location=(0, -0.06, spring + half - 0.05), pivot=(0, 0, -1),
                   taper=(1.3, 1.0), swatch="gold_d"),
             K.box("Stall", size=(opening, 0.1, spring + half), location=(0, depth, 0), pivot=(0, -1, -1), swatch="shadow")]
    for i in range(4):  # the stall gates: four bronze bars on two gilded rails
        x = -half + 0.45 + i * (opening - 0.9) / 3
        parts.append(K.box("Bar%d" % i, size=(0.12, 0.12, 2.5), location=(x, 0.5, 0), pivot=(0, 0, -1), swatch="bronze_d"))
    for z in (0.6, 2.2):
        parts.append(K.box("Rail%d" % int(z * 10), size=(opening, 0.14, 0.14), location=(0, 0.5, z), pivot=(0, 0, -1),
                           swatch="gold_d"))
    parts += [K.box("Frieze", size=(width, 0.06, 0.4), location=(0, -0.03, top - 0.45), pivot=(0, 0, -1), swatch="purple"),
              K.box("Entablature", size=(width, depth + 0.2, 0.5), location=(0, -0.1, top), pivot=(0, -1, -1),
                    swatch="travertine_d"),
              K.box("Attic", size=(width, depth, 2.4), location=(0, 0, top + 0.5), pivot=(0, -1, -1), swatch="travertine"),
              K.box("Panel", size=(2.4, 0.05, 1.3), location=(0, -0.02, top + 1.05), pivot=(0, 0, -1), swatch="purple_d"),
              K.cylinder("Plaque", radius=0.38, depth=0.08, verts=8, location=(0, -0.06, top + 1.7), pivot=(0, 0, 0),
                         swatch="gold", axis="Y"),
              K.box("Cornice", size=(width, depth + 0.3, 0.3), location=(0, -0.15, BACK_TOP - 0.3), pivot=(0, -1, -1),
                    swatch="travertine_d"),
              K.box("Pilaster", size=(0.6, 0.18, top), location=(-edge, -0.09, 0), pivot=(0, 0, -1), swatch="travertine_d"),
              K.box("PilasterCap", size=(0.74, 0.3, 0.2), location=(-edge, -0.12, top - 0.2), pivot=(0, 0, -1),
                    swatch="gold_d"),
              K.box("PennantPole", size=(0.07, 0.07, 2.6), location=(0, depth * 0.5, BACK_TOP), pivot=(0, 0, -1),
                    swatch="gold_d"),
              K.box("Pennant", size=(0.9, 0.08, 1.5), location=(0.48, depth * 0.5, BACK_TOP + 2.55), pivot=(0, 0, 1),
                    swatch="purple_l")]
    finish(parts)
    return K.join(parts, name)


def pulvinar(name, width=14.0, depth=7.0):
    """The imperial box on the north side: a 4.2 m travertine podium with a purple band and a gilded rail, the box's
    floor where the court stands, four travertine columns with gold capitals under a purple canopy with a gold fringe,
    an empty gold throne, and a tall palace wall behind with a gold eagle in its pediment's field. Front face at
    y = 0, origin at its bottom centre (SM_PatronBox's convention)."""
    floor_h = 4.2
    parts = [K.box(name, size=(width, depth, floor_h), pivot=(0, -1, -1), swatch="travertine")]
    parts.append(K.box("Band", size=(width, 0.06, 0.6), location=(0, -0.03, floor_h - 1.3), pivot=(0, -1, -1),
                       swatch="purple"))
    parts.append(K.box("Trim", size=(width, 0.08, 0.14), location=(0, -0.04, floor_h - 0.7), pivot=(0, -1, -1),
                       swatch="gold_d"))
    for i in range(7):  # gilded balusters under the rail
        x = -width / 2 + 0.6 + i * (width - 1.2) / 6
        parts.append(K.box("Baluster%d" % i, size=(0.18, 0.18, 0.7), location=(x, 0.15, floor_h), pivot=(0, 0, -1),
                           swatch="gold_d"))
    parts.append(K.box("Rail", size=(width, 0.3, 0.14), location=(0, 0.1, floor_h + 0.7), pivot=(0, -1, -1), swatch="gold"))
    col_y = depth - 2.2
    for i in range(4):
        x = -width / 2 + 1.2 + i * (width - 2.4) / 3
        parts.append(K.cylinder("Column%d" % i, radius=0.35, depth=4.8, verts=8, location=(x, col_y, floor_h),
                                pivot=(0, 0, -1), swatch="travertine"))
        parts.append(K.box("Capital%d" % i, size=(0.9, 0.9, 0.3), location=(x, col_y, floor_h + 4.8), pivot=(0, 0, -1),
                           swatch="gold_d"))
    parts.append(K.box("Canopy", size=(width + 0.6, depth - 0.4, 0.3), location=(0, 0.6, floor_h + 5.1), pivot=(0, -1, -1),
                       swatch="purple"))
    parts.append(K.box("Valance", size=(width + 0.7, 0.12, 0.8), location=(0, 0.55, floor_h + 4.35), pivot=(0, -1, -1),
                       swatch="purple_d"))
    parts.append(K.box("Fringe", size=(width + 0.72, 0.14, 0.14), location=(0, 0.54, floor_h + 4.3), pivot=(0, -1, -1),
                       swatch="gold"))
    parts.append(K.box("Throne", size=(1.4, 1.0, 0.6), location=(0, depth - 2.6, floor_h), pivot=(0, 0, -1), swatch="gold_d"))
    parts.append(K.box("ThroneBack", size=(1.4, 0.3, 2.0), location=(0, depth - 2.0, floor_h), pivot=(0, 0, -1),
                       taper=(0.8, 1.0), swatch="gold"))
    parts.append(K.box("Palace", size=(width + 1.0, 1.2, 12.5), location=(0, depth - 1.2, 0), pivot=(0, -1, -1),
                       swatch="travertine"))
    parts.append(K.box("Drape", size=(width - 1.0, 0.08, 4.4), location=(0, depth - 1.26, floor_h), pivot=(0, 0, -1),
                       swatch="purple_d"))
    parts.append(K.box("PalaceCornice", size=(width + 1.6, 1.6, 0.5), location=(0, depth - 1.4, 12.5), pivot=(0, -1, -1),
                       swatch="travertine_d"))
    parts.append(K.extrude_poly("Pediment", [(-(width + 1.6) / 2, 0.0), ((width + 1.6) / 2, 0.0), (0.0, 2.6)], 1.2,
                                plane="XZ", location=(0, depth - 0.6, 13.0), swatch="travertine"))
    eagle = K.extrude_poly("Eagle", eagle_outline(1.0), 0.2, plane="XZ", location=(0, depth - 1.35, 9.4), swatch="gold")
    parts.append(eagle)
    parts.append(K.box("EagleField", size=(5.4, 0.06, 3.0), location=(0, depth - 1.22, 9.1), pivot=(0, 0, -1),
                       swatch="purple"))
    finish(parts)
    return K.join(parts, name)


def eagle_outline(s=1.0):
    """A heraldic eagle, wings spread and raised, head turned to its right (x, z), ~5 m x 2.7 m at s = 1; the origin
    is its tail's bottom."""
    right = [(0.3, 0.0), (0.45, 0.55), (1.2, 0.65), (2.45, 1.5), (2.5, 2.6), (1.95, 2.15), (1.45, 2.45), (0.95, 1.95),
             (0.42, 1.85)]
    head = [(0.3, 2.25), (0.05, 2.6), (-0.3, 2.5), (-0.55, 2.3), (-0.28, 2.22)]
    left = [(-x, z) for x, z in reversed(right)]
    return [(x * s, z * s) for x, z in right + head + left]


# ------------------------------------------------------------------------------------------------ gates

def doors(root, opening, open_h, leaf_sw, band_sw, boss_sw):
    """DoorL / DoorR (SM_GrandGate's): leaves hinged at the jambs (x = +-opening/2, y = 0.25), each pivot at its
    hinge, opened by yawing (ArenaGates swings them into the arena)."""
    leaf_w = opening / 2 - 0.02
    for side, sx in (("L", -1), ("R", 1)):
        hinge = (sx * opening / 2, 0.25, 0)
        leaf = [K.box("Leaf" + side, size=(leaf_w, 0.16, open_h - 0.1), location=(hinge[0] - sx * leaf_w / 2, hinge[1], 0.05),
                      pivot=(0, 0, -1), swatch=leaf_sw)]
        for z in (0.8, open_h * 0.5, open_h - 0.9):
            leaf.append(K.box("Band", size=(leaf_w, 0.2, 0.2), location=(hinge[0] - sx * leaf_w / 2, hinge[1], z),
                              pivot=(0, 0, -1), swatch=band_sw))
        leaf.append(K.cylinder("Boss", radius=0.3, depth=0.1, verts=8, location=(hinge[0] - sx * leaf_w * 0.7, hinge[1] - 0.12, open_h * 0.5),
                               pivot=(0, 0, 0), swatch=boss_sw, axis="Y"))
        h = K.box("Door" + side, size=(0.12, 0.18, open_h - 0.15), location=hinge, pivot=(-sx, 0, -1), swatch=band_sw,
                  parent=root)
        K.join([h] + leaf, "Door" + side)


def imp_gate(name, width=2 * GATE_HALF, opening=5.0, open_h=7.0, height=13.0, depth=3.0):
    """A side gate: a travertine arch (the opening's top a half circle over the doors' lintel, its lunette purple
    with a gold sun), paired travertine columns with gold capitals, a purple frieze, an attic with a gold inscription
    and a gold wreath; bronze double doors with gold bands (DoorL / DoorR). Front (-Y) face at y = 0."""
    root = K.empty(name)
    half, edge = opening / 2, width / 2
    body_top = height - 2.2
    body = K.extrude_poly(name + "_Frame", arch_outline(half, open_h, body_top, edge, segs=8), depth, plane="XZ",
                          location=(0, depth / 2, 0), swatch="travertine", parent=root)
    soffits(body, "travertine_d")
    frame = [body,
             K.extrude_poly("Archivolt", half_annulus(half, half + 0.35, open_h, segs=8), 0.12, plane="XZ",
                            location=(0, -0.05, 0), swatch="gold_d"),
             K.extrude_poly("Lunette", half_disc(half, open_h, segs=8), 0.1, plane="XZ", location=(0, 0.3, 0),
                            swatch="purple_d"),
             K.extrude_poly("Sun", half_disc(1.0, open_h, segs=6), 0.1, plane="XZ", location=(0, 0.18, 0), swatch="gold"),
             K.box("Lintel", size=(opening, 0.3, 0.3), location=(0, 0.3, open_h - 0.02), pivot=(0, 0, -1), swatch="gold_d"),
             K.box("Frieze", size=(width + 0.1, 0.1, 0.7), location=(0, -0.05, body_top - 1.0), pivot=(0, -1, -1),
                   swatch="purple"),
             K.box("Cornice", size=(width + 0.8, depth + 0.6, 0.45), location=(0, -0.3, body_top), pivot=(0, -1, -1),
                   swatch="travertine_d"),
             K.box("Attic", size=(width - 1.0, depth - 0.4, 1.75), location=(0, 0.2, height - 1.75), pivot=(0, -1, -1),
                   swatch="travertine"),
             K.box("Inscription", size=(width - 4.0, 0.1, 0.9), location=(0, 0.15, height - 1.35), pivot=(0, -1, -1),
                   swatch="gold_d"),
             K.cylinder("Wreath", radius=0.55, depth=0.12, verts=8, location=(0, -0.08, height - 0.9), pivot=(0, 0, 0),
                        swatch="gold", axis="Y")]
    for sx in (-1, 1):
        for k in (0, 1):
            x = sx * (half + 0.7 + k * 1.4)
            frame.append(K.cylinder("Col%d%d" % (sx, k), radius=0.4, depth=body_top - 1.0, verts=8, location=(x, -0.35, 0),
                                    pivot=(0, 0, -1), swatch="travertine_d"))
            frame.append(K.box("ColCap%d%d" % (sx, k), size=(1.0, 1.0, 0.3), location=(x, -0.35, body_top - 1.3),
                               pivot=(0, 0, -1), swatch="gold_d"))
    frame.append(K.box("Dark", size=(opening, 0.1, open_h), location=(0, depth + 0.5, 0), pivot=(0, 0, -1), swatch="shadow"))
    at_root(K.join(frame, name + "_Frame"))
    doors(root, opening, open_h, "bronze_d", "gold_d", "gold")
    return root


def triumphal_gate(name, width=2 * TRIUMPH_HALF, opening=5.0, open_h=8.0, depth=4.0):
    """The boss's gate at the sphendone's apex: a triple triumphal arch, gold and crimson. The middle arch (doors
    8 m tall under a crimson lunette with a gold sun) between crimson columns with gold capitals; two blind side
    arches (dark, gold archivolts, gold shields above); a crimson frieze, a tall attic with a gold inscription, and a
    gold eagle (7.8 m wingspan) on a plinth crowning it (~21 m). Crimson banners hang on the outer piers. Doors: crimson leaves with gold
    bands (DoorL / DoorR, hinged like SM_GrandGate's). Front (-Y) face at y = 0."""
    root = K.empty(name)
    half, edge = opening / 2, width / 2
    body_top = 12.5
    body = K.extrude_poly(name + "_Frame", arch_outline(half, open_h, body_top, edge, segs=8), depth, plane="XZ",
                          location=(0, depth / 2, 0), swatch="travertine", parent=root)
    soffits(body, "travertine_d")
    frame = [body,
             K.extrude_poly("Archivolt", half_annulus(half, half + 0.45, open_h, segs=8), 0.14, plane="XZ",
                            location=(0, -0.06, 0), swatch="gold"),
             K.extrude_poly("Lunette", half_disc(half, open_h, segs=8), 0.1, plane="XZ", location=(0, 0.3, 0),
                            swatch="crimson_d"),
             K.extrude_poly("Sun", half_disc(1.2, open_h, segs=6), 0.1, plane="XZ", location=(0, 0.18, 0), swatch="gold"),
             K.box("Lintel", size=(opening, 0.3, 0.3), location=(0, 0.3, open_h - 0.02), pivot=(0, 0, -1), swatch="gold_d"),
             K.box("Keystone", size=(0.7, 0.3, 1.0), location=(0, -0.12, open_h + half - 0.15), pivot=(0, 0, -1),
                   taper=(1.3, 1.0), swatch="gold_d")]
    side_c, side_half, side_spring = half + 4.4, 1.2, 4.2
    for sx in (-1, 1):
        cx = sx * side_c
        blind = K.extrude_poly("Blind%d" % sx, [(-side_half, 0.0), (side_half, 0.0)] + arc(0.0, side_spring, side_half, 0.0, math.pi, 6)[:-1] + [(-side_half, side_spring)],
                               0.06, plane="XZ", location=(cx, -0.03, 0), swatch="shadow")
        frame.append(blind)
        frame.append(K.extrude_poly("BlindArch%d" % sx, half_annulus(side_half, side_half + 0.25, side_spring), 0.1, plane="XZ",
                                    location=(cx, -0.05, 0), swatch="gold_d"))
        for k in range(3):  # a bronze grille in the blind arch
            frame.append(K.box("Grille%d%d" % (sx, k), size=(0.1, 0.1, side_spring + 0.6), location=(cx + (k - 1) * 0.7, -0.08, 0),
                               pivot=(0, 0, -1), swatch="bronze_d"))
        frame.append(K.cylinder("Shield%d" % sx, radius=0.75, depth=0.14, verts=10, location=(cx, -0.08, 7.4), pivot=(0, 0, 0),
                                swatch="gold", axis="Y"))
        frame.append(K.cylinder("ShieldBoss%d" % sx, radius=0.3, depth=0.1, verts=6, location=(cx, -0.17, 7.4), pivot=(0, 0, 0),
                                swatch="crimson", axis="Y"))
        for k in (0, 1):  # crimson columns on the inner piers
            x = sx * (half + 0.85 + k * 1.3)
            frame.append(K.cylinder("Col%d%d" % (sx, k), radius=0.42, depth=body_top - 1.4, verts=8, location=(x, -0.38, 0.4),
                                    pivot=(0, 0, -1), swatch="crimson"))
            frame.append(K.box("ColBase%d%d" % (sx, k), size=(1.0, 1.0, 0.4), location=(x, -0.38, 0), pivot=(0, 0, -1),
                               swatch="gold_d"))
            frame.append(K.box("ColCap%d%d" % (sx, k), size=(1.05, 1.05, 0.3), location=(x, -0.38, body_top - 1.3),
                               pivot=(0, 0, -1), swatch="gold"))
        bx = sx * (edge - 1.05)  # a crimson banner on each outer pier
        frame.append(K.box("BannerBar%d" % sx, size=(1.8, 0.16, 0.16), location=(bx, -0.12, body_top - 1.6), pivot=(0, 0, -1),
                           swatch="gold"))
        frame.append(K.box("Banner%d" % sx, size=(1.5, 0.06, 5.6), location=(bx, -0.1, body_top - 1.55), pivot=(0, 0, 1),
                           swatch="crimson"))
        frame.append(K.box("BannerHem%d" % sx, size=(1.5, 0.08, 0.25), location=(bx, -0.11, body_top - 7.15), pivot=(0, 0, -1),
                           swatch="gold_d"))
    frame += [K.box("Frieze", size=(width + 0.1, 0.1, 0.9), location=(0, -0.05, body_top - 1.0), pivot=(0, -1, -1),
                    swatch="crimson"),
              K.box("FriezeTrim", size=(width + 0.14, 0.12, 0.12), location=(0, -0.06, body_top - 1.12), pivot=(0, -1, -1),
                    swatch="gold_d"),
              K.box("Cornice", size=(width + 0.8, depth + 0.6, 0.5), location=(0, -0.3, body_top), pivot=(0, -1, -1),
                    swatch="travertine_d"),
              K.box("Attic", size=(width - 1.4, depth - 0.4, 3.0), location=(0, 0.2, body_top + 0.5), pivot=(0, -1, -1),
                    swatch="travertine"),
              K.box("Inscription", size=(width - 6.0, 0.1, 1.7), location=(0, 0.15, body_top + 1.15), pivot=(0, -1, -1),
                    swatch="gold"),
              K.box("AtticCornice", size=(width - 1.0, depth, 0.3), location=(0, 0.0, body_top + 3.5), pivot=(0, -1, -1),
                    swatch="gold_d"),
              # iteration 2: a bigger eagle (v1's 5 m one vanished in the 64 px silhouette)
              K.box("Plinth", size=(4.4, 2.2, 0.9), location=(0, 1.0, body_top + 3.8), pivot=(0, 0, -1), swatch="crimson_d"),
              K.extrude_poly("Eagle", eagle_outline(1.55), 0.6, plane="XZ", location=(0, 1.0, body_top + 4.7), swatch="gold"),
              K.box("Dark", size=(opening, 0.1, open_h), location=(0, depth + 0.5, 0), pivot=(0, 0, -1), swatch="shadow")]
    for sx in (-1, 1):  # gilded trophies on the attic's corners
        frame.append(K.cone("Trophy%d" % sx, radius=0.55, depth=1.6, verts=6, location=(sx * (edge - 1.6), 1.6, body_top + 3.8),
                            pivot=(0, 0, -1), swatch="gold_d"))
    at_root(K.join(frame, name + "_Frame"))
    doors(root, opening, open_h, "crimson_d", "gold", "gold_l")
    return root


# ------------------------------------------------------------------------------------------------ spina

def spina_segment(name, length=10.0, width=SPINA_W, height=SPINA_H):
    """10 m of the spina's raised walkway (1.2 m, 6 m wide), along X: travertine sides with a purple band and a gold
    trim under the lip, two purple relief panels a side, a paved top with a purple carpet down its axis. Bands and
    lip run the full length only (no overhang at the ends), so segments abut without coplanar overlaps. Origin at the
    base centre; walkable top at exactly 1.2 m."""
    parts = [K.box(name, size=(length, width, height - 0.12), pivot=(0, 0, -1), swatch="travertine"),
             K.box("Band", size=(length, width + 0.06, 0.26), location=(0, 0, height * 0.32), pivot=(0, 0, -1),
                   swatch="purple"),
             K.box("Trim", size=(length, width + 0.1, 0.08), location=(0, 0, height - 0.22), pivot=(0, 0, -1),
                   swatch="gold_d"),
             K.box("Lip", size=(length, width + 0.16, 0.12), location=(0, 0, height - 0.12), pivot=(0, 0, -1),
                   swatch="travertine_d"),
             K.box("Carpet", size=(length, 1.6, 0.015), location=(0, 0, height), pivot=(0, 0, -1), swatch="purple")]
    for i in range(3):  # paving joints across the top
        parts.append(K.box("Joint%d" % i, size=(0.06, width - 0.2, 0.012), location=(-length / 2 + (i + 1) * length / 4, 0, height),
                           pivot=(0, 0, -1), swatch="travertine_d"))
    for sy in (-1, 1):  # relief panels on both long sides
        for sx in (-1, 1):
            parts.append(K.box("Panel%d%d" % (sx, sy), size=(2.6, 0.04, 0.22), location=(sx * 2.5, sy * (width / 2 + 0.05), height * 0.62),
                               pivot=(0, 0, -1), swatch="gold_d"))
    finish(parts)
    return K.join(parts, name)


def spina_end(name, radius=SPINA_W / 2, height=SPINA_H):
    """The metae, ending the spina: a half-round base continuing the walkway (1.2 m, the same bands and lip), the
    flat side on y = 0 against the last segment's end; a raised plinth on it and three tall gold cones (each on a
    gilded drum, an egg-shaped finial) on an arc toward the round side (-Y, the turn). Origin at the meta's centre on
    the floor (the spina's end). The cones block: three drums of radius 0.6 (positions in docs/tracks/R1-env-art.md)."""
    parts = [d_prism(name, radius, height - 0.12, swatch="travertine"),
             d_prism("Band", radius + 0.03, 0.26, z0=height * 0.32, swatch="purple"),
             d_prism("Trim", radius + 0.05, 0.08, z0=height - 0.22, swatch="gold_d"),
             d_prism("Lip", radius + 0.08, 0.12, z0=height - 0.12, swatch="travertine_d"),
             d_prism("Plinth", radius - 0.7, 0.5, z0=height, swatch="travertine_d"),
             d_prism("PlinthTrim", radius - 0.66, 0.1, z0=height + 0.32, swatch="purple")]
    top = height + 0.5
    for k, a in enumerate((-52.0, 0.0, 52.0)):
        x, y = 1.45 * math.sin(math.radians(a)), -1.45 * math.cos(math.radians(a))
        parts.append(K.cylinder("Drum%d" % k, radius=0.6, depth=0.45, verts=6, location=(x, y, top), pivot=(0, 0, -1),
                                swatch="gold_d"))
        parts.append(K.cylinder("Cone%d" % k, radius=0.5, depth=5.2, verts=6, taper=0.12, location=(x, y, top + 0.45),
                                pivot=(0, 0, -1), swatch="gold"))
        parts.append(K.ico("Egg%d" % k, radius=0.24, subdiv=1, location=(x, y, top + 5.85), scale=(1.0, 1.0, 1.35),
                           swatch="gold_l"))
    finish(parts)
    return K.join(parts, name)


def spina_stairs(name, steps=STAIRS_STEPS, rise=SPINA_H / STAIRS_STEPS, run=STAIRS_RUN, width=STAIRS_W):
    """Travertine stairs up the spina's side (SM_GrandStairs' shape: low edge at the origin, rising toward +Y =
    Unity -Z): 1.2 m in 2 m, 3 m wide, purple cheeks with gold copings. Place it with its back (y = 2 m) flush
    against the walkway's side, facing out."""
    parts = []
    for i in range(steps):
        parts.append(K.box(name if i == 0 else name + "_Step" + str(i), size=(width, run * (steps - i), rise),
                           location=(0, run * i + run * (steps - i) / 2, rise * i), pivot=(0, 0, -1), bevel=0.02,
                           swatch="travertine" if i % 2 == 0 else "travertine_d"))
    for sx in (-1, 1):
        parts.append(K.wedge("Cheek%d" % sx, size=(0.24, run * steps, rise * steps), location=(sx * (width / 2 + 0.12), 0, 0),
                             pivot=(0, -1, -1), swatch="purple"))
        parts.append(K.box("Post%d" % sx, size=(0.3, 0.3, 0.3), location=(sx * (width / 2 + 0.12), run * steps - 0.15, rise * steps),
                           pivot=(0, 0, -1), swatch="gold_d"))
    finish(parts)
    return K.join(parts, name)


def obelisk(name, height=12.0):
    """The obelisk at the spina's middle (a line-of-sight blocker), ~12 m from its base on the walkway: a stepped
    travertine plinth with a purple band and gold corners, a tapering porphyry (purple) shaft with gold inscription
    strips, a gold pyramidion. Origin at the base centre (it stands on the spina: place it at y = 1.2)."""
    plinth_h, shaft_h = 1.7, height - 1.7 - 1.1
    parts = [K.box(name, size=(2.9, 2.9, 0.3), pivot=(0, 0, -1), swatch="travertine_d"),
             K.box("Plinth", size=(2.4, 2.4, plinth_h - 0.3), location=(0, 0, 0.3), pivot=(0, 0, -1), swatch="travertine"),
             K.box("PlinthBand", size=(2.44, 2.44, 0.35), location=(0, 0, 0.75), pivot=(0, 0, -1), swatch="purple"),
             K.box("PlinthCap", size=(2.6, 2.6, 0.14), location=(0, 0, plinth_h - 0.14), pivot=(0, 0, -1), swatch="gold_d"),
             K.box("Shaft", size=(1.3, 1.3, shaft_h), location=(0, 0, plinth_h), pivot=(0, 0, -1), taper=(0.62, 0.62),
                   swatch="purple"),
             K.cone("Pyramidion", radius=1.3 * 0.62 / math.sqrt(2) * 1.02, depth=1.1, verts=4,
                    location=(0, 0, plinth_h + shaft_h), pivot=(0, 0, -1), swatch="gold")]
    parts[-1].rotation_euler.z = math.radians(45)
    lean = math.atan(1.3 * (1 - 0.62) / 2 / shaft_h)
    z0 = shaft_h * 0.14
    for sy in (-1, 1):  # gold inscription strips down the front and back faces, leaning with them
        face_y = 0.65 - z0 * math.tan(lean)
        parts.append(K.box("Glyphs%d" % sy, size=(0.32, 0.05, shaft_h * 0.62), location=(0, sy * (face_y + 0.005), plinth_h + z0),
                           pivot=(0, 0, -1), taper=(0.8, 1.0), swatch="gold_d"))
        parts[-1].rotation_euler.x = lean * sy
    finish(parts)
    return K.join(parts, name)


def lap_counter(name, width=6.0):
    """The lap counter on the spina (seven gold eggs on a frame): two travertine columns with gold capitals under a
    purple-faced architrave, seven eggs on it. Each egg is a child (Egg0..Egg6, west to east in Unity, pivot at its
    bottom) so code can take one down per lap; the frame is <name>_Frame. Along X (the spina's axis), front (-Y) and
    back alike. Origin at the base centre (place it on the spina at y = 1.2)."""
    root = K.empty(name)
    col_h, beam_z = 3.0, 3.25
    frame = [K.box(name + "_Frame", size=(width + 0.4, 0.9, 0.25), pivot=(0, 0, -1), swatch="travertine_d", parent=root)]
    for sx in (-1, 1):
        x = sx * (width / 2 - 0.3)
        frame.append(K.cylinder("Col%d" % sx, radius=0.22, depth=col_h, verts=8, location=(x, 0, 0.25), pivot=(0, 0, -1),
                                swatch="travertine"))
        frame.append(K.box("Cap%d" % sx, size=(0.6, 0.6, 0.2), location=(x, 0, 0.25 + col_h - 0.2), pivot=(0, 0, -1),
                           swatch="gold_d"))
    frame += [K.box("Architrave", size=(width + 0.2, 0.6, 0.45), location=(0, 0, beam_z), pivot=(0, 0, -1),
                    swatch="travertine_d"),
              K.box("Frieze", size=(width + 0.24, 0.64, 0.22), location=(0, 0, beam_z + 0.1), pivot=(0, 0, -1),
                    swatch="purple"),
              K.box("Shelf", size=(width + 0.3, 0.7, 0.1), location=(0, 0, beam_z + 0.45), pivot=(0, 0, -1), swatch="gold_d")]
    K.join(frame, name + "_Frame")
    top = beam_z + 0.55
    for i in range(7):
        x = (i - 3) * (width - 0.6) / 6
        # Blender +X is Unity -X: Egg0 (Unity west) sits at Blender +X
        K.ico("Egg%d" % i, radius=0.26, subdiv=1, location=(-x, 0, top), scale=(1.0, 1.0, 1.4), pivot_z=-1.0, swatch="gold",
              parent=root)
    return root


# ------------------------------------------------------------------------------------------------ preview of the arena

def place(src, x, z, yaw=0.0, h=0.0, sx=1.0, coll=None):
    """A linked copy of src (and its children) at Unity (x, h, z) turned `yaw` degrees (Unity's yaw, model +Z toward
    the direction it names): Blender rotation -yaw (see the export's 180 deg turn)."""
    def dup(o, parent):
        c = o.copy()
        coll.objects.link(c)
        if parent is not None:
            c.parent = parent
        for ch in o.children:
            dup(ch, c)
        return c
    c = dup(src, None)
    c.location = B(x, z, h)
    c.rotation_euler = (0, 0, -math.radians(yaw))
    c.scale = (sx, 1, 1)
    return c


def yaw_of(dx, dz):
    return math.degrees(math.atan2(dx, dz))


def preview_layout(lib):
    """A stand-in for the layout code, from the contract's numbers (NOT the layout: gate positions here are guesses):
    stand runs along the straight sides and chords round the sphendone, the carceres with a central gate, the
    triumphal gate at the apex, side gates, the pulvinar mid-north, towers at the carceres' corners, the spina."""
    coll = bpy.data.collections.new("Preview")
    bpy.context.scene.collection.children.link(coll)
    P = lambda name, *a, **kw: place(lib[name], *a, coll=coll, **kw)  # noqa: E731
    P("SM_ImpFloor", 0, 0)

    def run(a, b, model="SM_ImpStand_4m", unit=STAND_W):
        dx, dz = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dz)
        ux, uz = dx / length, dz / length
        inward = (uz, -ux)  # clockwise runs: inward is on the right
        m = max(1, round(length / unit))
        w = length / m
        for i in range(m):
            t = w * (i + 0.5)
            P(model, a[0] + ux * t, a[1] + uz * t, yaw_of(*inward), sx=w / unit)

    side_gates = (-55.0, 15.0)
    pulv_x, pulv_w = -10.0, 14.0
    # north side (z = +44), clockwise from above = west to east; south side east to west
    north = [WEST_X, side_gates[0] - GATE_HALF, side_gates[0] + GATE_HALF, pulv_x - pulv_w / 2, pulv_x + pulv_w / 2,
             side_gates[1] - GATE_HALF, side_gates[1] + GATE_HALF, EAST_C]
    for i in range(0, len(north), 2):
        run((north[i], WALL_Z), (north[i + 1], WALL_Z))
    for g in side_gates:
        P("SM_ImpGate", g, WALL_Z, 180.0)
        P("SM_ImpGate", g, -WALL_Z, 0.0)
    P("SM_Pulvinar", pulv_x, WALL_Z, 180.0)
    south = [EAST_C, side_gates[1] + GATE_HALF, side_gates[1] - GATE_HALF, side_gates[0] + GATE_HALF,
             side_gates[0] - GATE_HALF, WEST_X]
    for i in range(0, len(south), 2):
        run((south[i], -WALL_Z), (south[i + 1], -WALL_Z))
    # the sphendone: chords from the north end to the triumphal gate, and from it to the south end
    ga = math.asin(TRIUMPH_HALF / EAST_R)
    for a0, a1 in ((math.pi / 2, ga), (-ga, -math.pi / 2)):
        span = abs(a1 - a0)
        m = round(span * EAST_R / STAND_W)
        for i in range(m):
            t0, t1 = a0 + (a1 - a0) * i / m, a0 + (a1 - a0) * (i + 1) / m
            p0 = (EAST_C + EAST_R * math.cos(t0), EAST_R * math.sin(t0))
            p1 = (EAST_C + EAST_R * math.cos(t1), EAST_R * math.sin(t1))
            mid = ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2)
            chord = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
            P("SM_ImpStand_Curve", mid[0], mid[1], yaw_of(EAST_C - mid[0], -mid[1]), sx=chord / STAND_W)
    P("SM_TriumphalGate", EAST_C + EAST_R, 0.0, -90.0)
    # the carceres: bays from the south-west corner north, a gate in the middle
    for z0, z1 in ((-WALL_Z, -GATE_HALF), (GATE_HALF, WALL_Z)):
        m = round((z1 - z0) / 4.0)
        w = (z1 - z0) / m
        for i in range(m):
            P("SM_Carceres", WEST_X, z0 + w * (i + 0.5), 90.0, sx=w / 4.0)
    P("SM_ImpGate", WEST_X, 0.0, 90.0)
    for sz in (-1, 1):
        P("SM_ImpTower", WEST_X - 4.0, sz * (WALL_Z + 4.0), 90.0)
    # the spina
    for i in range(10):
        P("SM_Spina_10m", SPINA_X0 + 5.0 + 10.0 * i, 0.0)
    P("SM_SpinaEnd", SPINA_X0, 0.0, -90.0)
    P("SM_SpinaEnd", SPINA_X1, 0.0, 90.0)
    for sx in (-40.0, -25.0, 5.0, 30.0):
        P("SM_SpinaStairs", sx, SPINA_W / 2 + STAIRS_RUN * STAIRS_STEPS, 0.0)
        P("SM_SpinaStairs", sx, -(SPINA_W / 2 + STAIRS_RUN * STAIRS_STEPS), 180.0)
    P("SM_Obelisk", OBELISK_X, 0.0, 0.0, h=SPINA_H)
    P("PR_LapCounter", COUNTER_X, 0.0, 0.0, h=SPINA_H)
    return coll


def render_view(path, cam_loc, target, lens=28.0, res=(1600, 900), ortho=None, top=False):
    """One workbench render of the scene (palette texture, studio light) from Unity-space camera/target points."""
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("_PreviewCam")
    if ortho:
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = ortho
    cam_data.lens = lens
    cam_data.clip_end = 2000.0
    cam = bpy.data.objects.new("_PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    loc = B(cam_loc[0], cam_loc[2], cam_loc[1])
    tgt = B(target[0], target[2], target[1])
    cam.location = loc
    cam.rotation_euler = (0.0, 0.0, math.pi) if top else (tgt - loc).to_track_quat("-Z", "Y").to_euler()
    K._set_engine(scene)
    r = scene.render
    r.resolution_x, r.resolution_y, r.resolution_percentage = res[0], res[1], 100
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "Standard"
    sh = scene.display.shading
    sh.light, sh.color_type = "STUDIO", "TEXTURE"
    sh.show_cavity = sh.show_object_outline = False
    sh.show_shadows = True
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.55, 0.62, 0.72)
    r.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    return path


# ------------------------------------------------------------------------------------------------ build

def build(part):
    if part == "terrain":
        return {"SM_ImpFloor": imp_floor("SM_ImpFloor")}
    if part == "structures":
        return {
            "SM_ImpStand_4m": stand_section("SM_ImpStand_4m"),
            "SM_ImpStand_Curve": stand_section("SM_ImpStand_Curve", pos=curved_at(EAST_R, STAND_W)),
            "SM_Carceres": carceres_bay("SM_Carceres"),
            "SM_ImpTower": tower("SM_ImpTower"),
            "SM_Pulvinar": pulvinar("SM_Pulvinar"),
        }
    if part == "gates":
        return {"SM_ImpGate": imp_gate("SM_ImpGate"), "SM_TriumphalGate": triumphal_gate("SM_TriumphalGate")}
    if part == "spina":
        return {
            "SM_Spina_10m": spina_segment("SM_Spina_10m"),
            "SM_SpinaEnd": spina_end("SM_SpinaEnd"),
            "SM_SpinaStairs": spina_stairs("SM_SpinaStairs"),
            "SM_Obelisk": obelisk("SM_Obelisk"),
            "PR_LapCounter": lap_counter("PR_LapCounter"),
        }
    raise ValueError(part)


K.clear_scene()
if PART == "reference":
    # the Grand Arena's pieces, for the budget: triangles and bounds of each FBX
    names = ARGS.get("names", "SM_GrandFloor,SM_GrandStand_4m,SM_GrandGate,SM_PatronBox,SM_GrandTower,SM_GrandStairs,"  # noqa: F821
                     "SM_GrandTerrace,SM_GrandDais,PR_PatronStatue").split(",")
    for n in names:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(K.MODELS_DIR, "Environment", n + ".fbx"))
        objs = [o for o in bpy.data.objects if o not in before]
        lo, hi = K.world_bounds(objs)
        OUT[n] = {"tris": K.tri_count(objs), "size": tuple(round(v, 2) for v in (hi - lo)),
                  "children": {o.name: tuple(round(v, 3) for v in o.location) for o in objs if o.parent is not None}}
    result = OUT
elif PART == "overview":
    lib = {}
    for p in ("terrain", "structures", "gates", "spina"):
        lib.update(build(p))
    for o in lib.values():
        at_origin(o)
        for d in K.descendants(o):
            d.hide_render = True
    coll = preview_layout(lib)
    for o in coll.all_objects:
        o.hide_render = False
    OUT["preview_tris"] = K.tri_count(coll.all_objects)
    OUT["preview_pieces"] = len([o for o in coll.objects if o.parent is None])
    shots = K.SHOTS_DIR
    os.makedirs(shots, exist_ok=True)
    OUT["close"] = render_view(os.path.join(shots, "imperial_overview_close.png"), (-36.0, 7.0, -26.0), (-8.0, 4.0, 22.0), lens=24.0)
    OUT["aerial"] = render_view(os.path.join(shots, "imperial_overview_aerial.png"), (-150.0, 95.0, -120.0), (2.0, 0.0, 0.0), lens=30.0)
    OUT["east"] = render_view(os.path.join(shots, "imperial_overview_east.png"), (30.0, 9.0, -8.0), (95.0, 8.0, 0.0), lens=24.0)
    OUT["top"] = render_view(os.path.join(shots, "imperial_overview_top.png"), (0.0, 300.0, 0.0), (0.0, 0.0, 0.0),
                             res=(1800, 900), ortho=210.0, top=True)
    result = OUT
else:
    items = build(PART)
    for o in items.values():
        at_origin(o)
    # lay them out side by side for one review shot of the part
    x = 0.0
    for n, o in items.items():
        lo, hi = K.world_bounds(K.descendants(o))
        o.location.x += x - lo.x
        x += (hi.x - lo.x) + 2.0
    review = ARGS.get("review")  # noqa: F821
    for n, o in items.items():
        saved = o.location.copy()
        o.location = (0, 0, 0)
        if review in (n, "all"):
            OUT[n + "_review"] = K.review_render(o, n.lower(), size=420)["sheet"]
        OUT[n] = K.export_fbx(o, "Environment/" + n)
        OUT[n + "_tris"] = K.tri_count(K.descendants(o) or [o])
        lo, hi = K.world_bounds(K.descendants(o))
        OUT[n + "_bounds"] = (tuple(round(v, 2) for v in lo), tuple(round(v, 2) for v in hi))
        o.location = saved
    OUT["blend"] = K.save_blend("imperial_arena_" + PART)
    result = OUT
