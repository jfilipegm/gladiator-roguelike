"""Colosseum kit (R2, docs/tracks/R2-colosseum.md): Arena 6, the great oval. The wall line is the ellipse 98 x 67
(x = 98 cos t, z = 67 sin t) sampled every 3.75 deg, with the two great gates' 22 m spans cut by chords at x = +-96.67.
The floor is timber strewn with sand over the hypogeum: 30 trapdoors (a broken ring of 24 round the inner arena and 3
either side of the stage on the axis), 8 lifts, and the Victor's lift in the middle of the stage. Round it, a 22 m bowl
of weathered travertine: the podium and the senators' terrace, three maeniana of five tiers, a colonnade of arches and
the velarium on top, cut by six gates (two great gates on the axis, four side gates carrying seats over their tunnels),
with the Emperor's box and the editor's box on the minor axis.

Palette: travertine_d (main mass), travertine (lit faces), stone_grey, marble / marble_shadow (the balustrade),
charcoal (voids, tunnels, shafts), ink (shaft bottoms), bronze_d / bronze / bronze_l, iron_d, cloth_white +
red_banner (the velarium), sand_d / sand / dirt_floor (the deck's sand), wood_dark / wood (timber); purple and gold
only on the two boxes. Never orange-red: the telegraphs are the only orange-red on the floor.

ARGS: part=floor|hypogeum|cavea|gates|props|overview|check [review=<model name>|all]. Every piece goes to
Assets/Art/Models/Environment/. The floor and the six stand runs are built in Unity world coordinates (place them at
the origin, yaw 0) and mapped through B(): the export's 180 deg turn puts them back. Every other piece is built round
its origin; "local" below means Unity-local (x right, y up, +z toward the arena), and B() maps it to Blender too.
part=overview builds a preview of the whole arena from the contract's numbers (a stand-in for the layout code) and
renders it; part=check re-imports the FBX and reports hierarchies, pivots, sizes and triangles. Seeded randomness:
re-runs are identical. The numbers the layout code must use: docs/tracks/R2-env-art.md.
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
PART = ARGS.get("part", "floor")  # noqa: F821  (ARGS injected by Tools/bheadless)
OUT = {}

# ------------------------------------------------------------------------------------------------ the oval (Unity XZ)
EA, EB = 98.0, 67.0             # the wall line's semi-axes (x, z)
STEP = 3.75                     # the wall line's sampling (degrees of t)
FLOOR_MARGIN = 1.5              # the floor reaches this far under the walls: the ellipse 99.5 x 68.5
DOOR_L, DOOR_W = 8.0, 4.0       # a trapdoor: along its local X (the corridor below), across (local Z)
LIFT, CENTRE_HOLE = 4.0, 5.0    # a lift's square; the hole under the stage's lift
PLANK = 0.4                     # the dark plank border round every hole
CORRIDOR = 2.2                  # the faint plank lines: the corridors' edges, this far from their centre lines
LINE_HALF = 0.15                # half a plank line's width
STAGE_R, STAGE_H = 10.0, 1.2
# (x, z, yaw, group): 0 ring NE, 1 ring NW, 2 ring SW, 3 ring SE, 4 axis W, 5 axis E. The ring's 24 are in ring order.
TRAPDOORS = [
    (61.18, 6.80, 76.4, 0), (55.23, 19.08, 53.0, 0), (45.40, 28.61, 36.1, 0), (33.46, 35.36, 23.5, 0),
    (20.43, 39.65, 13.3, 0), (6.86, 41.74, 4.3, 0),
    (-6.86, 41.74, -4.3, 1), (-20.43, 39.65, -13.3, 1), (-33.46, 35.36, -23.5, 1), (-45.40, 28.61, -36.1, 1),
    (-55.23, 19.08, -53.0, 1), (-61.18, 6.80, -76.4, 1),
    (-61.18, -6.80, 76.4, 2), (-55.23, -19.08, 53.0, 2), (-45.40, -28.61, 36.1, 2), (-33.46, -35.36, 23.5, 2),
    (-20.43, -39.65, 13.3, 2), (-6.86, -41.74, 4.3, 2),
    (6.86, -41.74, -4.3, 3), (20.43, -39.65, -13.3, 3), (33.46, -35.36, -23.5, 3), (45.40, -28.61, -36.1, 3),
    (55.23, -19.08, -53.0, 3), (61.18, -6.80, -76.4, 3),
    (-20.0, 0.0, 0.0, 4), (-32.0, 0.0, 0.0, 4), (-44.0, 0.0, 0.0, 4),
    (20.0, 0.0, 0.0, 5), (32.0, 0.0, 0.0, 5), (44.0, 0.0, 0.0, 5)]
LIFTS = [(34.0, 20.0), (-34.0, 20.0), (-34.0, -20.0), (34.0, -20.0), (80.0, 24.0), (-80.0, 24.0), (-80.0, -24.0),
         (80.0, -24.0)]
# gates counter-clockwise from the east: (x, z, yaw, half width); the great gates stand on the chords
GATES = [(96.67, 0.0, -90.0, 11.0), (49.00, 58.02, -158.5, 6.0), (-49.00, 58.02, 158.5, 6.0),
         (-96.67, 0.0, 90.0, 11.0), (-49.00, -58.02, 21.5, 6.0), (49.00, -58.02, -21.5, 6.0)]
GREAT = (0, 3)
RUNS = [(9.45, 56.23), (63.77, 116.23), (123.77, 170.55), (189.45, 236.23), (243.77, 296.23), (303.77, 350.55)]
BOXES = [(0.0, 67.0, 180.0), (0.0, -67.0, 0.0)]
BOX_T = (90.0, 270.0)                          # the boxes' t: the balustrade stops one sample either side
BOX_HALF = EA * math.cos(math.radians(90.0 - STEP))  # 6.41: the box spans the wall line's samples at 86.25 / 93.75
# the bowl's profile (docs/tracks/R2-colosseum.md "The stands"): u outward from the wall line, y up
PODIUM_H, BALUS_U, BALUS_H, TERRACE_U = 4.0, 0.3, 4.9, 2.4
TIER_RUN, TIER_RISE, TIERS = 0.8, 0.5, 5
WALL_RISE, WALK = 1.2, 1.0
COLON_U, BACK_U = 16.4, 19.4
ARCH_TOP, ATTIC_TOP = 19.9, 21.9
ARCH_STEP = 4.4                 # one arch per ~4.4 m of the colonnade's line
SAIL_IN, SAIL_OUT = (11.6, 23.0), (18.9, 27.4)  # the velarium's inner and outer edges (u, y)
MAST_U, MAST_TOP = 18.9, 28.2
GATE_OPEN_W, GATE_OPEN_H, GATE_SPRING = 5.0, 7.0, 4.5        # SM_ColGate's opening (the arch's crown at 7.0)
GREAT_OPEN_W, GREAT_OPEN_H, GREAT_SPRING = 5.0, 9.0, 6.5     # SM_ColGreatGate's
TRIBUNAL = 7.7                  # SM_ColGate: the tribunal over the tunnel, level with walkway 1
GREAT_TOP = 24.0                # SM_ColGreatGate's cornice (its crown rises to ~26.6)
TUNNEL_FLOOR = 0.03             # the gates' tunnel floors, over the arena floor's last 1.5 m


def B(x, z, h=0.0):
    """A Unity-space point (x, z, height) in Blender coordinates (the export turns the model 180 deg)."""
    return Vector((-x, -z, h))


def ell(t, a=EA, b=EB):
    """The wall line's ellipse at parameter t (degrees): (x, z)."""
    r = math.radians(t)
    return (a * math.cos(r), b * math.sin(r))


def ell_n(t, a=EA, b=EB):
    """The ellipse's outward unit normal at t."""
    r = math.radians(t)
    nx, nz = math.cos(r) / a, math.sin(r) / b
    m = math.hypot(nx, nz)
    return (nx / m, nz / m)


def axes(yaw):
    """Unity yaw (degrees) -> (local +X, local +Z) as world (x, z) unit vectors."""
    a = math.radians(yaw)
    return (math.cos(a), -math.sin(a)), (math.sin(a), math.cos(a))


def to_local(p, c, yaw):
    ax, az = axes(yaw)
    dx, dz = p[0] - c[0], p[1] - c[1]
    return dx * ax[0] + dz * ax[1], dx * az[0] + dz * az[1]


def at_origin(obj):
    """K.join keeps the first part's origin: move the mesh so the piece's origin is the world origin again."""
    if obj.type != "MESH" or obj.children:
        return obj
    obj.data.transform(Matrix.Translation(obj.location))
    obj.location = (0, 0, 0)
    return obj


def set_origin(obj, pivot, parent=None):
    """Move obj's origin to `pivot` (Blender) without moving its geometry; optionally parent it (identity inverse)."""
    bpy.context.view_layer.update()
    obj.data.transform(Matrix.Translation(Vector(obj.location) - Vector(pivot)))
    obj.location = pivot
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = Matrix.Identity(4)
    return obj


def join(parts, name):
    bpy.context.view_layer.update()
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ tagged meshes

class Tags:
    """Face swatch tags carried through bmesh as material indices, painted after to_mesh."""

    def __init__(self):
        self.names = []

    def __call__(self, sw):
        if sw not in self.names:
            self.names.append(sw)
        return self.names.index(sw)


def tagged_object(name, bm, tags, default="travertine_d", location=(0, 0, 0), parent=None):
    obj = K.object_from_bmesh(name, bm, location, parent)
    K.set_swatch(obj, default)
    uvs = K.palette()
    uvl = obj.data.uv_layers.active.data
    for poly in obj.data.polygons:
        i = poly.material_index
        sw = tags.names[i] if i < len(tags.names) else default
        u, v = uvs[sw]
        for li in poly.loop_indices:
            uvl[li].uv = (u, v)
        poly.material_index = 0
    return obj


def flat_poly(bm, tags, pts, sw, facing):
    """One flat face through Blender points `pts`, its normal turned toward `facing` (a Blender vector)."""
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    f.normal_update()
    if f.normal.dot(facing) < 0:
        f.normal_flip()
    f.material_index = tags(sw)
    if len(pts) > 4:
        bmesh.ops.triangulate(bm, faces=[f], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    return f


def poly_solid(bm, tags, outline, y0, y1, sw_top, sw_side, sw_bottom=None, place=None):
    """A prism: an outline of Unity-local (x, z) points from height y0 to y1. place(x, z, h) -> Blender point (default
    B). The bottom is left open unless sw_bottom is given."""
    place = place or B
    lo = [bm.verts.new(place(x, z, y0)) for x, z in outline]
    hi = [bm.verts.new(place(x, z, y1)) for x, z in outline]
    faces = []
    top = bm.faces.new(hi)
    top.material_index = tags(sw_top)
    faces.append(top)
    if sw_bottom:
        bot = bm.faces.new(list(reversed(lo)))
        bot.material_index = tags(sw_bottom)
        faces.append(bot)
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        f.material_index = tags(sw_side)
        faces.append(f)
    return faces


def fix_normals_up(bm, faces, centre):
    """Turn faces outward from `centre` (Blender), tops up: for open prisms recalc_face_normals can't decide."""
    for f in faces:
        f.normal_update()
        c = f.calc_center_median()
        if abs(f.normal.z) > 0.9:
            want = Vector((0, 0, 1)) if c.z > centre.z else Vector((0, 0, -1))
        else:
            want = Vector((c.x - centre.x, c.y - centre.y, 0))
        if f.normal.dot(want) < 0:
            f.normal_flip()


# ------------------------------------------------------------------------------------------------ the floor

def holes():
    """Every hole in the deck: (name, centre (x, z), yaw, half length (local X), half width (local Z))."""
    out = []
    for i, (x, z, yaw, g) in enumerate(TRAPDOORS):
        out.append(("T%02d_g%d" % (i, g), (x, z), yaw, DOOR_L / 2, DOOR_W / 2))
    for i, (x, z) in enumerate(LIFTS):
        out.append(("L%d" % i, (x, z), 0.0, LIFT / 2, LIFT / 2))
    out.append(("Stage", (0.0, 0.0), 0.0, CENTRE_HOLE / 2, CENTRE_HOLE / 2))
    return out


def rect_pts(c, yaw, hl, hw, step=2.0):
    """The rectangle's outline (x, z), counter-clockwise in its local frame, edges split every <= step m."""
    ax, az = axes(yaw)
    corners = [(-hl, -hw), (hl, -hw), (hl, hw), (-hl, hw)]
    pts = []
    for i in range(4):
        (u0, v0), (u1, v1) = corners[i], corners[(i + 1) % 4]
        n = max(1, int(math.ceil(math.hypot(u1 - u0, v1 - v0) / step)))
        for k in range(n):
            u, v = u0 + (u1 - u0) * k / n, v0 + (v1 - v0) * k / n
            pts.append((c[0] + ax[0] * u + az[0] * v, c[1] + ax[1] * u + az[1] * v))
    return pts


def rect_dist(p, h, pad=0.0):
    """Distance from p to hole h grown by pad (0 inside)."""
    lx, lz = to_local(p, h[1], h[2])
    ox, oz = max(0.0, abs(lx) - h[3] - pad), max(0.0, abs(lz) - h[4] - pad)
    return math.hypot(ox, oz)


def in_rect(p, h, pad=0.0):
    lx, lz = to_local(p, h[1], h[2])
    return abs(lx) <= h[3] + pad and abs(lz) <= h[4] + pad


def plank_lines():
    """The faint plank lines over the hypogeum's corridors, as straight strips (p0, p1): along both edges of the ring's
    corridor from door to door (across the 5.74 m bridges, CORRIDOR m either side of the doors' centre line), and
    along both edges of the axis corridor from the stage to the first door, between the doors, and out to the ring."""
    strips = []
    ring = TRAPDOORS[:24]
    ends = []  # per ring door: its two ends (toward the previous door, toward the next), each with (inner, outer) points
    for i, (x, z, yaw, g) in enumerate(ring):
        ax, az = axes(yaw)
        nx, nz = ring[(i + 1) % 24][:2]
        sx = 1.0 if (nx - x) * ax[0] + (nz - z) * ax[1] > 0 else -1.0  # local X sign toward the next door
        inner = 1.0 if -(x * az[0] + z * az[1]) > 0 else -1.0           # local Z sign toward the oval's centre
        e = DOOR_L / 2 + PLANK + 0.05
        pt = lambda s, w: (x + ax[0] * s * e + az[0] * w * CORRIDOR, z + ax[1] * s * e + az[1] * w * CORRIDOR)  # noqa: E731
        ends.append({"prev": (pt(-sx, inner), pt(-sx, -inner)), "next": (pt(sx, inner), pt(sx, -inner))})
    for i in range(24):
        a, b = ends[i]["next"], ends[(i + 1) % 24]["prev"]
        strips.append((a[0], b[0]))  # inner edge
        strips.append((a[1], b[1]))  # outer edge
    # the axis: x from the stage's edge to the first door, between doors, and from the last door to the ring's inner line
    def ring_inner_x(sign, zz):
        """Where the line z = zz meets the ring's inner plank line across the axis, on the east (+1) or west side."""
        i = 23 if sign > 0 else 11  # the bridge from door 23 to door 0 (east) or 11 to 12 (west)
        (p0, p1) = strips[2 * i]
        t = (zz - p0[1]) / (p1[1] - p0[1])
        return p0[0] + (p1[0] - p0[0]) * t
    for sign in (-1.0, 1.0):
        for zz in (-CORRIDOR, CORRIDOR):
            xs = [9.9] + [v for d in (20.0, 32.0, 44.0) for v in (d - DOOR_L / 2 - PLANK - 0.05, d + DOOR_L / 2 + PLANK + 0.05)]
            xs.append(abs(ring_inner_x(sign, zz)) - LINE_HALF - 0.05)
            for k in range(0, len(xs), 2):
                strips.append(((sign * xs[k], zz), (sign * xs[k + 1], zz)))
    return strips


def strip_quad(s, hw=LINE_HALF):
    (x0, z0), (x1, z1) = s
    L = math.hypot(x1 - x0, z1 - z0)
    nx, nz = -(z1 - z0) / L, (x1 - x0) / L
    return [(x0 + nx * hw, z0 + nz * hw), (x1 + nx * hw, z1 + nz * hw), (x1 - nx * hw, z1 - nz * hw), (x0 - nx * hw, z0 - nz * hw)]


def strip_dist(p, s):
    """(distance along the strip's axis outside its ends, distance from its centre line)."""
    (x0, z0), (x1, z1) = s
    L = math.hypot(x1 - x0, z1 - z0)
    ux, uz = (x1 - x0) / L, (z1 - z0) / L
    dx, dz = p[0] - x0, p[1] - z0
    t = dx * ux + dz * uz
    d = abs(-dx * uz + dz * ux)
    return max(0.0, -t, t - L), d


def subdivide(loop, step):
    out = []
    n = len(loop)
    for i in range(n):
        a, b = loop[i], loop[(i + 1) % n]
        m = max(1, int(math.ceil(math.hypot(b[0] - a[0], b[1] - a[1]) / step)))
        for k in range(m):
            out.append((a[0] + (b[0] - a[0]) * k / m, a[1] + (b[1] - a[1]) * k / m))
    return out


def col_floor(name, seed=71, lattice=2.5):
    """The deck: sand_d over the hypogeum's timber, the ellipse 99.5 x 68.5 (the wall line + 1.5 m), holed by every
    trapdoor (8 x 4), every lift (4 x 4) and the stage lift's 5 x 5 square at the centre. A 0.4 m border of dark planks
    round every hole; faint plank lines (dirt_floor, 0.3 m) along both edges of the ring's and the axis's corridors so
    the hypogeum's pattern reads with the doors shut; a speckle of sand and dirt_floor; height jitter +-1.2 cm, none
    within 0.5 m of a hole. A constrained Delaunay triangulation of a 2.5 m lattice plus every hole, border and line
    edge, so every edge is exact and welded."""
    from mathutils.geometry import delaunay_2d_cdt
    rng = random.Random(seed)
    H = holes()
    strips = plank_lines()
    pts, edges, index = [], [], {}

    def vid(p):
        key = (round(p[0], 4), round(p[1], 4))
        if key not in index:
            index[key] = len(pts)
            pts.append(p)
        return index[key]

    def loop(ps):
        ids = [vid(p) for p in ps]
        edges.extend((ids[i], ids[(i + 1) % len(ids)]) for i in range(len(ids)))
    a, b = EA + FLOOR_MARGIN, EB + FLOOR_MARGIN
    loop([ell(STEP * k, a, b) for k in range(96)])
    for h in H:
        loop(rect_pts(h[1], h[2], h[3], h[4]))
        loop(rect_pts(h[1], h[2], h[3] + PLANK, h[4] + PLANK))
    for s in strips:
        loop(subdivide(strip_quad(s), 2.5))
    # the lattice, kept 0.6 m off every constraint; none under the stage (it hides the floor there)
    n = int(a // lattice) + 1
    m = int(b // lattice) + 1
    for i in range(-n, n + 1):
        for j in range(-m, m + 1):
            p = (i * lattice, j * lattice)
            if (p[0] / (a - 0.8)) ** 2 + (p[1] / (b - 0.8)) ** 2 > 1.0:
                continue
            if math.hypot(*p) < STAGE_R - 1.0:
                continue
            if any(in_rect(p, h, PLANK + 0.6) for h in H):
                continue
            if any(sd[0] < 0.6 and sd[1] < LINE_HALF + 0.6 for sd in (strip_dist(p, s) for s in strips)):
                continue
            vid(p)
    res = delaunay_2d_cdt([Vector(p) for p in pts], edges, [], 0, 1e-5)
    out_v, out_f = res[0], res[2]
    bm = bmesh.new()
    tags = Tags()
    verts = []
    for v in out_v:
        p = (v.x, v.y)
        flat = any(rect_dist(p, h) <= 0.5 for h in H)
        verts.append(bm.verts.new(B(p[0], p[1], 0.0 if flat else rng.uniform(-0.012, 0.012))))
    counts = {}
    for f in out_f:
        vs = [verts[i] for i in f]
        cx = sum(-v.co.x for v in vs) / 3.0
        cz = sum(-v.co.y for v in vs) / 3.0
        c = (cx, cz)
        if any(in_rect(c, h) for h in H):
            continue  # a hole
        if any(in_rect(c, h, PLANK) for h in H):
            sw = "wood_dark"
        elif any(sd[0] == 0.0 and sd[1] <= LINE_HALF for sd in (strip_dist(c, s) for s in strips)):
            sw = "dirt_floor"
        else:
            r = random.Random(int(round(cx * 37.0)) * 92821 + int(round(cz * 37.0)) * 68917 + seed).random()
            sw = "sand" if r < 0.04 else "dirt_floor" if r < 0.055 else "sand_d"
        nrm = (vs[1].co - vs[0].co).cross(vs[2].co - vs[0].co)
        if abs(nrm.z) < 1e-7:
            continue  # a collinear hull sliver
        face = bm.faces.new(vs if nrm.z > 0 else (vs[0], vs[2], vs[1]))
        face.material_index = tags(sw)
        counts[sw] = counts.get(sw, 0) + 1
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    obj = tagged_object(name, bm, tags, default="sand_d")
    OUT["floor_faces_by_swatch"] = counts
    OUT["floor_area_m2"] = round(sum(p.area for p in obj.data.polygons), 1)
    OUT["floor_hole_check"] = measure_holes(obj, H)
    return obj


def measure_holes(obj, H):
    """Reads the floor's boundary loops back (the outer one dropped) and matches each hole: its centre, yaw and size
    as built, against the contract's. Returns the worst errors and the measured list."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bnd = [e for e in bm.edges if len(e.link_faces) == 1]
    adj = {}
    for e in bnd:
        a, b = e.verts
        adj.setdefault(a.index, []).append(b.index)
        adj.setdefault(b.index, []).append(a.index)
    co = {v.index: (-v.co.x, -v.co.y) for v in bm.verts}
    seen, loops = set(), []
    for start in adj:
        if start in seen:
            continue
        lp, prev, cur = [], None, start
        while cur not in seen:
            seen.add(cur)
            lp.append(co[cur])
            nxt = [x for x in adj[cur] if x != prev and x not in seen]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
        loops.append(lp)
    bm.free()
    loops = [lp for lp in loops if max(math.hypot(*p) for p in lp) < 90.0]  # drop the outer edge
    measured = []
    for lp in loops:
        cx = sum(p[0] for p in lp) / len(lp)
        cz = sum(p[1] for p in lp) / len(lp)
        # the yaw from the loop's edge directions (mod 90), then which axis is the long one
        ang = []
        for i in range(len(lp)):
            dx, dz = lp[(i + 1) % len(lp)][0] - lp[i][0], lp[(i + 1) % len(lp)][1] - lp[i][1]
            if math.hypot(dx, dz) > 1e-6:
                ang.append(math.atan2(dz, dx) % (math.pi / 2))
        s = sum(math.sin(4 * a) for a in ang)
        c = sum(math.cos(4 * a) for a in ang)
        th = math.atan2(s, c) / 4.0
        ux, uz = math.cos(th), math.sin(th)
        ex = max(abs((p[0] - cx) * ux + (p[1] - cz) * uz) for p in lp) * 2
        ez = max(abs(-(p[0] - cx) * uz + (p[1] - cz) * ux) for p in lp) * 2
        if ez > ex + 0.5:
            th += math.pi / 2
            ex, ez = ez, ex
        yaw = -math.degrees(th)
        while yaw > 90.0:
            yaw -= 180.0
        while yaw <= -90.0:
            yaw += 180.0
        measured.append((cx, cz, yaw, ex, ez))
    worst = {"centre": 0.0, "yaw": 0.0, "size": 0.0}
    rows = []
    for h in H:
        m = min(measured, key=lambda q: math.hypot(q[0] - h[1][0], q[1] - h[1][1]))
        dyaw = abs((m[2] - h[2] + 90.0) % 180.0 - 90.0)
        if abs(h[3] - h[4]) < 1e-6:  # a square: its yaw only matters mod 90
            dyaw = abs((m[2] - h[2] + 45.0) % 90.0 - 45.0)
        worst["centre"] = max(worst["centre"], math.hypot(m[0] - h[1][0], m[1] - h[1][1]))
        worst["yaw"] = max(worst["yaw"], dyaw)
        worst["size"] = max(worst["size"], abs(m[3] - 2 * h[3]), abs(m[4] - 2 * h[4]))
        rows.append("%s (%.2f, %.2f) yaw %.1f %.2f x %.2f" % (h[0], m[0], m[1], m[2], m[3], m[4]))
    return {"holes_expected": len(H), "holes_found": len(measured), "worst_error": {k: round(v, 4) for k, v in worst.items()},
            "holes": rows}


# ------------------------------------------------------------------------------------------------ the hypogeum's pieces

def box_bm(bm, tags, lo, hi, sw, skip=(), sw_of=None):
    """An axis-aligned Blender box from lo to hi; skip faces by name ('-x', '+x', '-y', '+y', '-z', '+z')."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    v = {(i, j, k): bm.verts.new((x1 if i else x0, y1 if j else y0, z1 if k else z0))
         for i in (0, 1) for j in (0, 1) for k in (0, 1)}
    quads = {"-x": [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)], "+x": [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
             "-y": [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)], "+y": [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)],
             "-z": [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)], "+z": [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]}
    for k, q in quads.items():
        if k in skip:
            continue
        f = bm.faces.new([v[c] for c in q])
        f.material_index = tags(sw_of(k) if sw_of else sw)


def orient(faces, want):
    """Turn each face so its normal points along want(face_centre, normal) (a Blender vector)."""
    for f in faces:
        f.normal_update()
        if f.normal.dot(want(f.calc_center_median(), f.normal)) < 0:
            f.normal_flip()


def away_from(c):
    return lambda cc, n: cc - c


def into_shaft(cc, n):
    """Shaft linings: walls face the shaft's axis, horizontal faces face up."""
    return Vector((0, 0, 1)) if abs(n.z) > 0.9 else Vector((-cc.x, -cc.y, 0))


def trapdoor(name):
    """A trapdoor of the hypogeum, 8 x 4 m (local X along the corridor below). Root -> SM_ColTrapdoor_Frame (an iron_d
    border 0.12 m inside the hole, 0.15 m deep; the shaft: charcoal walls from 0 to -3, the long walls set back to
    local z = +-2.5 so a leaf hanging at 100 deg clears them, an ink bottom, three iron rungs), LeafF (covers local z
    0 -> +2) and LeafB (0 -> -2): dark planks with iron straps and a ring handle, tops flush at y 0, each pivoted at
    its hinge on the hole's long edge (LeafF at (0, 0, +2), LeafB at (0, 0, -2)). Origin at the top centre."""
    root = K.empty(name)
    hl, hw, bw, bd = DOOR_L / 2, DOOR_W / 2, 0.12, 0.15
    il, iw = hl - bw, hw - bw
    bm, tags = bmesh.new(), Tags()
    lining = []
    sq = lambda a, b, z: [bm.verts.new((x, y, z)) for x, y in ((-a, -b), (a, -b), (a, b), (-a, b))]  # noqa: E731
    to, ti, bi = sq(hl, hw, 0.0), sq(il, iw, 0.0), sq(il, iw, -bd)
    for k in range(4):
        j = (k + 1) % 4
        lining.append(bm.faces.new((to[k], to[j], ti[j], ti[k])))
        lining[-1].material_index = tags("iron_d")
        lining.append(bm.faces.new((ti[k], ti[j], bi[j], bi[k])))
        lining[-1].material_index = tags("iron_d")
    depth, sw_ = 3.0, 2.5  # the long walls set back to +-2.5
    st, sb = sq(hl, sw_, 0.0), sq(hl, sw_, -depth)
    for k in range(4):
        j = (k + 1) % 4
        lining.append(bm.faces.new((st[k], st[j], sb[j], sb[k])))
        lining[-1].material_index = tags("charcoal")
    lining.append(bm.faces.new(sb))
    lining[-1].material_index = tags("ink")
    orient(lining, into_shaft)
    for z in (-0.8, -1.5, -2.2):  # rungs on the +x end wall (Blender), proud 0.12, three faces each
        box_bm(bm, tags, (hl - 0.12, -0.3, z - 0.04), (hl, 0.3, z + 0.04), "iron_d", skip=("+x", "-y", "+y"))
    tagged_object(name + "_Frame", bm, tags, default="charcoal", parent=root)
    for side, sgn in (("LeafF", -1.0), ("LeafB", 1.0)):
        leaf(side, root, sgn, il, iw)
    return root


def leaf(name, root, sgn, il, iw, thick=0.1, gap=0.005):
    """One trapdoor leaf (Blender y from the seam toward sgn: LeafF covers Unity z 0 -> +2 = Blender y 0 -> -2):
    four plank rows along X alternating wood_dark / wood with iron edges, three iron straps from the hinge toward the
    seam (1 cm proud), a hexagonal ring handle near the seam. Pivot at its hinge, Blender (0, sgn * 2, 0)."""
    bm, tags = bmesh.new(), Tags()
    y0, y1 = sgn * gap, sgn * iw
    rows = 4
    ys = sorted(y0 + (y1 - y0) * k / rows for k in range(rows + 1))
    for k in range(rows):
        inner = ("-y" if k > 0 else None, "+y" if k < rows - 1 else None)
        top_sw = "wood_dark" if k % 2 == 0 else "wood"
        box_bm(bm, tags, (-il, ys[k], -thick), (il, ys[k + 1], 0.0), "iron_d", skip=tuple(s for s in inner if s),
               sw_of=lambda f, t=top_sw: t if f == "+z" else "wood_dark" if f == "-z" else "iron_d")
    for sx in (-2.6, 0.0, 2.6):  # straps from the hinge to 0.25 m short of the seam
        ya, yb = y1, y0 + sgn * 0.25
        box_bm(bm, tags, (sx - 0.08, min(ya, yb), 0.0), (sx + 0.08, max(ya, yb), 0.01), "iron_d",
               skip=("-z", "-y", "+y"))
    hx, hy, r0, r1 = -sgn * 1.3, y0 + sgn * 0.4, 0.13, 0.19  # the ring handle, 0.4 m from the seam
    ro = [bm.verts.new((hx + r1 * math.cos(math.pi * k / 3), hy + r1 * math.sin(math.pi * k / 3), 0.015)) for k in range(6)]
    ri = [bm.verts.new((hx + r0 * math.cos(math.pi * k / 3), hy + r0 * math.sin(math.pi * k / 3), 0.015)) for k in range(6)]
    ring = []
    for k in range(6):
        j = (k + 1) % 6
        ring.append(bm.faces.new((ro[k], ro[j], ri[j], ri[k])))
        ring[-1].material_index = tags("iron_d")
    orient(ring, lambda cc, n: Vector((0, 0, 1)))
    obj = tagged_object(name, bm, tags, default="wood_dark")
    return set_origin(obj, Vector((0.0, sgn * DOOR_W / 2, 0.0)), parent=root)


def lift(name):
    """A lift of the hypogeum, 4 x 4 m. Root -> SM_ColLift_Frame (an iron_d frame 0.1 m inside the hole, 0.15 m deep;
    the 3 m shaft: charcoal walls with iron corner angles, an ink bottom at -3) and Platform: a 3.8 x 3.8 wood deck
    (planks alternating wood / wood_dark, iron sides and iron edge trims), four bronze corner posts 1.1 m tall with
    pyramid caps. Platform's pivot is the deck's top centre, (0, 0, 0) at rest; code moves it to y -3 and back. Origin
    at the top centre."""
    root = K.empty(name)
    h, bw, bd, depth = LIFT / 2, 0.1, 0.15, 3.0
    i = h - bw
    bm, tags = bmesh.new(), Tags()
    sq = lambda r, z: [bm.verts.new((x, y, z)) for x, y in ((-r, -r), (r, -r), (r, r), (-r, r))]  # noqa: E731
    lining = []

    def quad(vs, sw):
        lining.append(bm.faces.new(vs))
        lining[-1].material_index = tags(sw)
    to, ti, bi = sq(h, 0.0), sq(i, 0.0), sq(i, -bd)
    for k in range(4):
        j = (k + 1) % 4
        quad((to[k], to[j], ti[j], ti[k]), "iron_d")
        quad((ti[k], ti[j], bi[j], bi[k]), "iron_d")
    st, sb = sq(h, -bd), sq(h, -depth)
    for k in range(4):
        j = (k + 1) % 4
        quad((st[k], st[j], sb[j], sb[k]), "charcoal")
    quad(sb, "ink")
    for sx in (-1, 1):  # iron angles in the shaft's corners: a leg 4 cm proud of each wall, 0.18 m wide
        for sy in (-1, 1):
            cx, cy, w, o = sx * h, sy * h, 0.18, 0.04
            leg_a = [(cx - sx * o, cy - sy * w), (cx - sx * o, cy)]
            leg_b = [(cx - sx * w, cy - sy * o), (cx, cy - sy * o)]
            for (p, q) in (leg_a, leg_b):
                quad((bm.verts.new((p[0], p[1], -bd)), bm.verts.new((q[0], q[1], -bd)),
                      bm.verts.new((q[0], q[1], -depth)), bm.verts.new((p[0], p[1], -depth))), "iron_d")
    orient(lining, into_shaft)
    tagged_object(name + "_Frame", bm, tags, default="charcoal", parent=root)
    # the platform: the deck (five planks across, iron sides), iron edge trims, four bronze posts with caps
    bm, tags = bmesh.new(), Tags()
    d, th, planks = i, 0.2, 5
    xs = [-d + 2 * d * k / planks for k in range(planks + 1)]
    for k in range(planks):
        inner = tuple(s for s in ("-x" if k > 0 else None, "+x" if k < planks - 1 else None) if s)
        top_sw = "wood" if k % 2 else "wood_dark"
        box_bm(bm, tags, (xs[k], -d, -th), (xs[k + 1], d, 0.0), "iron_d", skip=inner,
               sw_of=lambda f, t=top_sw: t if f == "+z" else "wood_dark" if f == "-z" else "iron_d")
    e = 0.1
    for lo, hi in (((-d, -d, 0.0), (d, -d + e, 0.01)), ((-d, d - e, 0.0), (d, d, 0.01)),
                   ((-d, -d + e, 0.0), (-d + e, d - e, 0.01)), ((d - e, -d + e, 0.0), (d, d - e, 0.01))):
        box_bm(bm, tags, lo, hi, "iron_d", skip=("-z",))
    caps = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            px, py, pw = sx * (d - 0.12), sy * (d - 0.12), 0.08
            box_bm(bm, tags, (px - pw, py - pw, 0.01), (px + pw, py + pw, 1.0), "bronze", skip=("-z", "+z"))
            apex = bm.verts.new((px, py, 1.1))
            base = [bm.verts.new((px + qx * (pw + 0.02), py + qy * (pw + 0.02), 1.0)) for qx, qy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            for k in range(4):
                caps.append(bm.faces.new((base[k], base[(k + 1) % 4], apex)))
                caps[-1].material_index = tags("bronze_l")
            caps.append(bm.faces.new(list(reversed(base))))
            caps[-1].material_index = tags("bronze")
            orient(caps[-5:], away_from(Vector((px, py, 1.03))))
    tagged_object("Platform", bm, tags, default="wood", parent=root)
    return root


def stage(name, segs=40):
    """The Stage of Victors: round, r 10, top 1.2, walkable, with the 5 x 5 m hole for the Victor's lift at its centre.
    The top in travertine flagstones (two rings of 16, alternating travertine / travertine_d) inside a travertine kerb
    ring, a carved laurel ring round the hole (paired leaves in travertine_d, 5 mm proud); the side in travertine_d
    with a bronze rim band under the kerb. No faces inside the hole or underneath (the lift's shaft lines the hole).
    Origin at the base centre."""
    from mathutils.geometry import delaunay_2d_cdt
    bm, tags = bmesh.new(), Tags()
    R, top = STAGE_R, STAGE_H
    rings = (R, R - 0.6, 7.4, 4.9)
    hh = CENTRE_HOLE / 2
    pts, edges, index = [], [], {}

    def vid(p):
        key = (round(p[0], 5), round(p[1], 5))
        if key not in index:
            index[key] = len(pts)
            pts.append(p)
        return index[key]

    def loop(ps):
        ids = [vid(p) for p in ps]
        edges.extend((ids[k], ids[(k + 1) % len(ids)]) for k in range(len(ids)))
    for r in rings:
        loop([(r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs)) for k in range(segs)])
    loop(subdivide([(-hh, -hh), (hh, -hh), (hh, hh), (-hh, hh)], 1.3))
    for k in range(16):  # the flagstones' radial joints between the kerb and the inner ring
        a = 2 * math.pi * (k + 0.5) / 16
        ids = [vid((r * math.cos(a), r * math.sin(a))) for r in (rings[3], 6.15, rings[2], 8.4, rings[1])]
        edges.extend((ids[q], ids[q + 1]) for q in range(len(ids) - 1))
    res = delaunay_2d_cdt([Vector(p) for p in pts], edges, [], 0, 1e-6)
    vs = [bm.verts.new((v.x, v.y, top)) for v in res[0]]
    for f in res[2]:
        tri = [vs[q] for q in f]
        cx = sum(v.co.x for v in tri) / 3
        cy = sum(v.co.y for v in tri) / 3
        if abs(cx) < hh and abs(cy) < hh:
            continue
        r = math.hypot(cx, cy)
        if r > R:
            continue
        n = (tri[1].co - tri[0].co).cross(tri[2].co - tri[0].co)
        if abs(n.z) < 1e-9:
            continue
        face = bm.faces.new(tri if n.z > 0 else (tri[0], tri[2], tri[1]))
        if r > rings[1]:
            sw = "travertine"
        elif r < rings[3]:
            sw = "travertine"
        else:
            sector = int(math.floor((math.atan2(cy, cx) / (2 * math.pi) * 16 - 0.5) % 16))
            band = 0 if r > rings[2] else 1
            sw = "travertine" if (sector + band) % 2 else "travertine_d"
        face.material_index = tags(sw)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    # the side: travertine_d to 0.92, a bronze band to 1.08, a travertine lip to the top
    hs = (0.0, 0.92, 1.08, top)
    side_sw = ("travertine_d", "bronze", "travertine")
    ringv = [[bm.verts.new((R * math.cos(2 * math.pi * k / segs), R * math.sin(2 * math.pi * k / segs), z)) for k in range(segs)] for z in hs]
    for q in range(3):
        for k in range(segs):
            j = (k + 1) % segs
            f = bm.faces.new((ringv[q][k], ringv[q][j], ringv[q + 1][j], ringv[q + 1][k]))
            f.material_index = tags(side_sw[q])
    # the laurel ring: pairs of leaves (diamonds) leaning along the ring, 5 mm proud, round r 4.25
    for k in range(28):
        a = 2 * math.pi * k / 28
        for lean in (-1, 1):
            c = Vector((4.25 * math.cos(a), 4.25 * math.sin(a), top + 0.005))
            tang = Vector((-math.sin(a), math.cos(a), 0))
            rad = Vector((math.cos(a), math.sin(a), 0))
            axis = (tang + rad * 0.55 * lean).normalized()
            perp = Vector((-axis.y, axis.x, 0))
            p0 = c - axis * 0.22 + rad * 0.12 * lean
            p1 = c + axis * 0.22 + rad * 0.12 * lean
            m = (p0 + p1) / 2
            bm.faces.new((bm.verts.new(p0), bm.verts.new(m + perp * 0.07), bm.verts.new(p1), bm.verts.new(m - perp * 0.07))).material_index = tags("stone_grey")
    for f in bm.faces:
        f.normal_update()
        cc = f.calc_center_median()
        want = Vector((0, 0, 1)) if cc.z > top - 0.001 and abs(f.normal.z) > 0.5 else Vector((cc.x, cc.y, 0))
        if f.normal.dot(want) < 0:
            f.normal_flip()
    return tagged_object(name, bm, tags, default="travertine")


def col_stairs(name, steps=4, rise=STAGE_H / 4, run=0.5, width=3.0):
    """Stairs up the stage (SM_SpinaStairs' shape: low front edge at the origin, rising toward Blender +Y = local -z):
    four steps of 0.3 x 0.5 in travertine_d / travertine, each reaching back to the stage's curve (r 10 round local
    (0, -12)) so it meets the round side with no gap; travertine_d cheeks with bronze copings and bronze cap blocks at
    their feet. Place it with its origin 12 m from the stage's centre, facing out."""
    bm, tags = bmesh.new(), Tags()
    hw = width / 2
    xs = (hw, hw / 2, 0.0, -hw / 2, -hw)
    back = [(x, 12.0 - math.sqrt(STAGE_R ** 2 - x * x)) for x in xs]
    place = lambda x, y, h: Vector((x, y, h))  # noqa: E731  (Blender coordinates directly)
    for k in range(steps):
        outline = [(-hw, run * k), (hw, run * k)] + back
        faces = poly_solid(bm, tags, outline, rise * k, rise * (k + 1), "travertine" if k % 2 == 0 else "travertine_d",
                           "travertine_d", place=place)
        fix_normals_up(bm, faces, Vector((0, (run * k + 2.0) / 2, rise * k + rise / 2)))
        bmesh.ops.triangulate(bm, faces=[f for f in faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    for sx in (-1, 1):
        x0, x1 = sx * hw, sx * (hw + 0.24)
        yb = 12.0 - math.sqrt(STAGE_R ** 2 - (hw + 0.12) ** 2)
        a = [bm.verts.new((x, y, z)) for x in (x0, x1) for y, z in ((0.0, 0.0), (yb, STAGE_H), (yb, 0.0))]
        cheek = []
        for vs, sw in (((0, 1, 2), "travertine_d"), ((3, 5, 4), "travertine_d"), ((0, 3, 4, 1), "bronze"),
                       ((1, 4, 5, 2), "travertine_d"), ((0, 2, 5, 3), "travertine_d")):
            cheek.append(bm.faces.new([a[q] for q in vs]))
            cheek[-1].material_index = tags(sw)
        orient(cheek, away_from(Vector(((x0 + x1) / 2, yb * 0.66, STAGE_H * 0.33))))
        box_bm(bm, tags, (min(x0, x1) - 0.02, -0.06, 0.0), (max(x0, x1) + 0.02, 0.26, 0.2), "bronze", skip=("-z",))
    return tagged_object(name, bm, tags, default="travertine_d")


# ------------------------------------------------------------------------------------------------ the bowl

def tiers(u0, y0, riser="travertine_d", tread="travertine"):
    """A maenianum: five tiers of seats from (u0, y0), each a riser of 0.5 and a tread of 0.8."""
    p = []
    for k in range(TIERS):
        p.append((u0 + TIER_RUN * k, y0 + TIER_RISE * k, riser))
        p.append((u0 + TIER_RUN * k, y0 + TIER_RISE * (k + 1), tread))
    return p


def upper_bowl():
    """The bowl from the foot of maenianum II (u 7.4, y 7.7) to its back: maenianum II, wall 2 and its walkway,
    maenianum III, the colonnade's arcade face (14 -> 19.9), the attic with a cornice to 21.9, the back."""
    u2, y2 = TERRACE_U + TIERS * TIER_RUN + WALK, PODIUM_H + TIERS * TIER_RISE + WALL_RISE  # 7.4, 7.7
    p = tiers(u2, y2)
    u3, y3 = u2 + TIERS * TIER_RUN, y2 + TIERS * TIER_RISE                                  # 11.4, 10.2
    p += [(u3, y3, "marble_shadow"), (u3, y3 + WALL_RISE, "travertine")]
    p += tiers(u3 + WALK, y3 + WALL_RISE)                                                    # to (16.4, 13.9)
    p += [(COLON_U, y3 + WALL_RISE + TIERS * TIER_RISE, "travertine"), (COLON_U, ARCH_TOP, "travertine_d"),
          (COLON_U, ATTIC_TOP - 0.4, "stone_grey"), (COLON_U - 0.3, ATTIC_TOP - 0.4, "travertine"),
          (COLON_U - 0.3, ATTIC_TOP, "travertine_d"), (BACK_U, ATTIC_TOP, "travertine_d"), (BACK_U, -0.3, "travertine_d")]
    return p


def cavea_profile():
    """The bowl's closed cross-section (u outward from the wall line, y up, swatch of the face to the next point):
    the podium (a stone_grey footing, travertine_d face, a marble_shadow band under the senators' terrace), the terrace
    (u 0 -> 2.4 at 4.0; the marble balustrade on its front is a separate sweep), maenianum I, wall 1 and its walkway,
    then upper_bowl()."""
    u1, y1 = TERRACE_U + TIERS * TIER_RUN, PODIUM_H + TIERS * TIER_RISE  # 6.4, 6.5
    p = [(0.0, -0.3, "stone_grey"), (0.0, 0.6, "travertine_d"), (0.0, PODIUM_H - 0.4, "marble_shadow"),
         (0.0, PODIUM_H, "travertine")]
    p += tiers(TERRACE_U, PODIUM_H)
    p += [(u1, y1, "marble_shadow"), (u1, y1 + WALL_RISE, "travertine")]
    return p + upper_bowl()


BALUSTRADE = [(0.0, PODIUM_H, "marble"), (0.0, BALUS_H, "marble"), (BALUS_U, BALUS_H, "marble_shadow"),
              (BALUS_U, PODIUM_H, "marble_shadow")]


def sweep(bm, tags, rings, prof, caps=True, face_sw=None, cap_sw="travertine_d"):
    """A closed profile [(u, y, swatch)] swept through rings [((x, z) on the wall line, (x, z) outward unit)] in Unity
    world space (mapped by B): a closed manifold with outward normals, caps triangulated."""
    vs = [[bm.verts.new(B(o[0] + n[0] * u, o[1] + n[1] * u, y)) for u, y, _ in prof] for o, n in rings]
    m = len(prof)
    faces = []
    for i in range(len(rings) - 1):
        for j in range(m):
            k = (j + 1) % m
            f = bm.faces.new((vs[i][j], vs[i][k], vs[i + 1][k], vs[i + 1][j]))
            f.material_index = tags((face_sw(i, j) if face_sw else None) or prof[j][2])
            faces.append(f)
    capf = []
    if caps:
        for ring in (vs[0], vs[-1]):
            capf.append(bm.faces.new(ring))
            capf[-1].material_index = tags(cap_sw)
    bmesh.ops.recalc_face_normals(bm, faces=faces + capf)
    if capf:
        bmesh.ops.triangulate(bm, faces=capf, quad_method="BEAUTY", ngon_method="EAR_CLIP")
    return faces


def run_rings(k):
    """Run k's rings: from gate k's +X front corner to gate k+1's -X front corner, through the wall line's samples
    between (every 3.75 deg of t, none within 0.5 deg of an end). The end rings run along their gate's axis, so the
    runs' end caps lie flat against the gates' sides. Returns (rings, the samples' t)."""
    t0, t1 = RUNS[k]
    g0, g1 = GATES[k], GATES[(k + 1) % 6]
    ax0, az0 = axes(g0[2])
    ax1, az1 = axes(g1[2])
    start = ((g0[0] + ax0[0] * g0[3], g0[1] + ax0[1] * g0[3]), (-az0[0], -az0[1]))
    end = ((g1[0] - ax1[0] * g1[3], g1[1] - ax1[1] * g1[3]), (-az1[0], -az1[1]))
    ts = []
    t = math.ceil((t0 + 0.5) / STEP) * STEP
    while t < t1 - 0.5:
        ts.append(t)
        t += STEP
    return [start] + [(ell(t), ell_n(t)) for t in ts] + [end], ts


AISLE_HALF = 0.6    # the aisles (scalaria): 1.2 m wide, up the three maeniana at every 7.5 deg of t


def with_aisles(rings, ts, k):
    """The run's rings with two more round every wall-line sample at a multiple of 7.5 deg (none within 4 deg of a
    gate, none at the boxes' axis): 0.6 m either side along the wall line. Returns the rings, the indices of the
    sub-segments inside an aisle, and the aisles' t."""
    out, segs, at = [rings[0]], set(), []
    for i in range(1, len(rings) - 1):
        t = ts[i - 1]
        aisle = (abs(t / 7.5 - round(t / 7.5)) < 1e-6 and t - RUNS[k][0] > 4.0 and RUNS[k][1] - t > 4.0
                 and all(abs(t - bt) > 1.0 for bt in BOX_T))
        if not aisle:
            out.append(rings[i])
            continue
        o, n = rings[i]
        for nb, sign in ((rings[i - 1], -1), (None, 0), (rings[i + 1], 1)):
            if nb is None:
                segs.add(len(out) - 1)
                out.append(rings[i])
                segs.add(len(out) - 1)
                continue
            (o2, n2) = nb
            L = math.hypot(o2[0] - o[0], o2[1] - o[1])
            f = AISLE_HALF / L
            nn = (n[0] + (n2[0] - n[0]) * f, n[1] + (n2[1] - n[1]) * f)
            m = math.hypot(*nn)
            out.append(((o[0] + (o2[0] - o[0]) * f, o[1] + (o2[1] - o[1]) * f), (nn[0] / m, nn[1] / m)))
        at.append(t)
    out.append(rings[-1])
    return out, segs, at


def edge_kinds(prof):
    """Each profile edge's kind: 'riser' (a seat tier's 0.5 m face), 'tread', 'wall' (a 1.2 m wall between maeniana),
    or None."""
    kinds = []
    for j in range(len(prof)):
        (u0, y0, _), (u1, y1, _) = prof[j], prof[(j + 1) % len(prof)]
        if abs(u0 - u1) < 1e-6 and TERRACE_U - 1e-6 < u0 < COLON_U - 1e-6 and abs(abs(y1 - y0) - TIER_RISE) < 1e-6:
            kinds.append("riser")
        elif abs(y0 - y1) < 1e-6 and abs(abs(u1 - u0) - TIER_RUN) < 1e-6:
            kinds.append("tread")
        elif abs(u0 - u1) < 1e-6 and abs(abs(y1 - y0) - WALL_RISE) < 1e-6 and u0 > TERRACE_U:
            kinds.append("wall")
        else:
            kinds.append(None)
    return kinds


def ring_at(rings, u):
    return [(o[0] + n[0] * u, o[1] + n[1] * u) for o, n in rings]


def along(poly, s):
    """The point at arc length s along a polyline, its segment index and the segment's unit direction."""
    acc = 0.0
    for i in range(len(poly) - 1):
        a, b = poly[i], poly[i + 1]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        if s <= acc + L or i == len(poly) - 2:
            f = (s - acc) / L
            d = ((b[0] - a[0]) / L, (b[1] - a[1]) / L)
            return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f), i, f, d
        acc += L


def arch_outline(half, spring, sill, segs=6):
    """A blind arch's outline (x along the wall, y up): a sill, two jambs, a half circle."""
    return [(-half, sill), (half, sill)] + [(half * math.cos(math.pi * q / segs), spring + half * math.sin(math.pi * q / segs))
                                           for q in range(segs + 1)]


def hexagon(r, cx, cy):
    return [(cx + r * math.cos(math.pi * q / 3 + math.pi / 6), cy + r * math.sin(math.pi * q / 3 + math.pi / 6)) for q in range(6)]


def wall_decal(bm, tags, origin, d, inward, outline, sw, proud):
    """A flat polygon on a vertical wall: outline (x along d, y up) at the world point `origin` (x, z), moved `proud` m
    toward `inward`, facing it."""
    pts = [B(origin[0] + d[0] * x + inward[0] * proud, origin[1] + d[1] * x + inward[1] * proud, y) for x, y in outline]
    flat_poly(bm, tags, pts, sw, B(inward[0], inward[1], 0.0))


def cavea(name, k):
    """Stand run k (gate k to gate k+1, counter-clockwise), one mesh in world coordinates: the bowl's profile swept
    along the wall line; the marble balustrade on the podium (broken where a box stands); on the colonnade, a blind
    arch (a charcoal void) every ~4.4 m with a bronze shield above it on the attic; a velarium mast over every third
    arch and the velarium's red-and-white strips slanting in over maenianum III (kept clear of the great gates)."""
    rings, ts = run_rings(k)
    bm, tags = bmesh.new(), Tags()
    prof = cavea_profile()
    aisle_rings, aisle_segs, aisle_t = with_aisles(rings, ts, k)
    kinds = edge_kinds(prof)
    aisle_sw = {"riser": "stone_grey", "wall": "charcoal"}
    sweep(bm, tags, aisle_rings, prof, face_sw=lambda i, j: aisle_sw.get(kinds[j]) if i in aisle_segs else None)
    OUT[name + "_aisles_t"] = aisle_t
    # the balustrade, split round the boxes (their own parapet replaces it between the samples either side)
    cut = []
    for bt in BOX_T:
        if RUNS[k][0] < bt < RUNS[k][1]:
            cut = [ts.index(bt - STEP) + 1, ts.index(bt + STEP) + 1]  # ring indices (ring 0 is the start corner)
    pieces = [rings] if not cut else [rings[:cut[0] + 1], rings[cut[1]:]]
    for piece in pieces:
        sweep(bm, tags, piece, BALUSTRADE, cap_sw="marble_shadow")
    # the colonnade: blind arches, shields; masts every third arch
    great0, great1 = k in GREAT, (k + 1) % 6 in GREAT
    face = ring_at(rings, COLON_U)
    mast_line = ring_at(rings, MAST_U)
    L = sum(math.hypot(face[i + 1][0] - face[i][0], face[i + 1][1] - face[i][1]) for i in range(len(face) - 1))
    n_arch = max(1, int(round(L / ARCH_STEP)))
    sail_from = 2 if great0 else 0
    sail_to = len(rings) - 3 if great1 else len(rings) - 1
    arch = arch_outline(1.2, ARCH_TOP - 1.2, 14.3)
    masts = 0
    for j in range(n_arch):
        p, i, f, d = along(face, (j + 0.5) * L / n_arch)
        n = rings[i][1]
        inward = (-d[1], d[0]) if (-d[1] * n[0] + d[0] * n[1]) < 0 else (d[1], -d[0])
        wall_decal(bm, tags, p, d, inward, arch, "charcoal", 0.06)
        wall_decal(bm, tags, p, d, inward, hexagon(0.42, 0.0, (ARCH_TOP + ATTIC_TOP - 0.4) / 2), "bronze", 0.05)
        if j % 3 == 1 and sail_from <= i < sail_to:
            a, b = mast_line[i], mast_line[i + 1]
            mx, mz = a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f
            c = B(mx, mz, 0.0)
            box_bm(bm, tags, (c.x - 0.18, c.y - 0.18, ATTIC_TOP), (c.x + 0.18, c.y + 0.18, MAST_TOP), "wood_dark", skip=("-z",))
            masts += 1
    # the velarium: strips between half-segment rings, alternating red and white
    sail_rings = []
    for i in range(sail_from, sail_to):
        (o0, n0), (o1, n1) = rings[i], rings[i + 1]
        nm = ((n0[0] + n1[0]) / 2, (n0[1] + n1[1]) / 2)
        m = math.hypot(*nm)
        sail_rings.append((o0, n0))
        sail_rings.append((((o0[0] + o1[0]) / 2, (o0[1] + o1[1]) / 2), (nm[0] / m, nm[1] / m)))
    sail_rings.append(rings[sail_to])
    sail = [(SAIL_IN[0], SAIL_IN[1], "cloth_white"), (SAIL_OUT[0], SAIL_OUT[1], "cloth_white"),
            (SAIL_OUT[0], SAIL_OUT[1] - 0.08, "cloth_white"), (SAIL_IN[0], SAIL_IN[1] - 0.08, "cloth_white")]
    stripe = lambda i, j: "red_banner" if i % 2 else "cloth_white"  # noqa: E731
    sweep(bm, tags, sail_rings, sail, face_sw=stripe, cap_sw="cloth_white")
    OUT[name + "_arches"] = n_arch
    OUT[name + "_masts"] = masts
    return tagged_object(name, bm, tags, default="travertine_d")


# ------------------------------------------------------------------------------------------------ gates

def gate_front():
    """SM_ColGate's front: the face rises to the tribunal over the tunnel (7.7, level with walkway 1) with a marble
    balustrade to 8.6; the tribunal's floor runs back to maenianum II."""
    return [(0.0, -0.3, "stone_grey"), (0.0, 0.6, "travertine_d"), (0.0, TRIBUNAL - 0.6, "marble_shadow"),
            (0.0, TRIBUNAL, "marble"), (0.0, TRIBUNAL + 0.9, "marble"), (BALUS_U, TRIBUNAL + 0.9, "marble_shadow"),
            (BALUS_U, TRIBUNAL, "travertine")]


def clip_above(prof, c, sw_new):
    """The part of a closed profile with y >= c (Sutherland-Hodgman); the new edge along y = c gets sw_new."""
    out = []
    n = len(prof)
    for i in range(n):
        (u0, y0, sw), (u1, y1, _) = prof[i], prof[(i + 1) % n]
        pin, qin = y0 >= c - 1e-9, y1 >= c - 1e-9
        if pin:
            out.append((u0, y0, sw))
        if pin != qin:
            t = (c - y0) / (y1 - y0)
            out.append((u0 + (u1 - u0) * t, c, sw_new if pin else sw))
    return out


def slab(bm, tags, x0, x1, prof, cap0="travertine_d", cap1="travertine_d", face_sw=None):
    """A profile [(u, y, swatch)] extruded along Blender X from x0 to x1 (u along +Y, behind the front face)."""
    r0 = [bm.verts.new((x0, u, y)) for u, y, _ in prof]
    r1 = [bm.verts.new((x1, u, y)) for u, y, _ in prof]
    m = len(prof)
    faces = []
    for j in range(m):
        k = (j + 1) % m
        faces.append(bm.faces.new((r0[j], r0[k], r1[k], r1[j])))
        faces[-1].material_index = tags((face_sw(j) if face_sw else None) or prof[j][2])
    caps = [bm.faces.new(r0), bm.faces.new(r1)]
    caps[0].material_index = tags(cap0)
    caps[1].material_index = tags(cap1)
    bmesh.ops.recalc_face_normals(bm, faces=faces + caps)
    bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="EAR_CLIP")
    return faces


def prism_xz(bm, tags, outline, y0, y1, sw_front, sw_side, sw_back=None):
    """An outline (x, z) in Blender's XZ plane extruded from y0 (front, facing -Y) to y1; the back face only if
    sw_back. Concave outlines are fine: every side faces out of the outline."""
    n = len(outline)
    area = sum(outline[i][0] * outline[(i + 1) % n][1] - outline[(i + 1) % n][0] * outline[i][1] for i in range(n))
    f0 = [bm.verts.new((x, y0, z)) for x, z in outline]
    f1 = [bm.verts.new((x, y1, z)) for x, z in outline]
    caps = [bm.faces.new(f0)]
    caps[0].material_index = tags(sw_front)
    orient(caps, lambda c, nn: Vector((0, -1, 0)))
    if sw_back:
        caps.append(bm.faces.new(f1))
        caps[-1].material_index = tags(sw_back)
        orient(caps[-1:], lambda c, nn: Vector((0, 1, 0)))
    for i in range(n):
        j = (i + 1) % n
        dx, dz = outline[j][0] - outline[i][0], outline[j][1] - outline[i][1]
        out = Vector((dz, 0, -dx)) if area > 0 else Vector((-dz, 0, dx))
        f = bm.faces.new((f0[i], f0[j], f1[j], f1[i]))
        f.material_index = tags(sw_side)
        orient([f], lambda c, nn, o=out: o)
    bmesh.ops.triangulate(bm, faces=[f for f in caps if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")


def arc_pts(r, spring, a0, a1, segs, cx=0.0):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * q / segs)), spring + r * math.sin(math.radians(a0 + (a1 - a0) * q / segs)))
            for q in range(segs + 1)]


def arched_opening(bm, tags, half, spring, top, depth, soffit="travertine_d"):
    """The two spandrels between an arch (radius half, springing at `spring`) and the flat top of its opening, from
    the front face (y 0) back `depth` m; their curved soffits in `soffit`."""
    for sx in (-1, 1):
        a = arc_pts(half, spring, 90.0, 0.0, 6) if sx > 0 else arc_pts(half, spring, 90.0, 180.0, 6)
        outline = [(sx * half, top), (0.0, top)] + a[1:]
        prism_xz(bm, tags, outline, 0.0, depth, "travertine_d", soffit, sw_back="charcoal")


def doors(root, half, spring, leaf_sw="wood_dark", band_sw="iron_d", stud_sw="bronze_l"):
    """DoorL / DoorR, timber leaves filling the opening up to the arch's springing (SM_ImpGate's hinges: (+-half,
    0.25, 0) in Blender, pivots at the hinges, opened by yawing); above them the frame's fixed lunette. Iron bands
    studded with bronze, an iron hinge post. Leaves stay below the springing so their 100 deg swing clears the
    archivolt and the imposts."""
    top = spring - 0.05
    for side, sx in (("L", -1), ("R", 1)):
        bm, tags = bmesh.new(), Tags()
        x0, x1 = sorted((sx * 0.02, sx * (half - 0.02)))
        box_bm(bm, tags, (x0, 0.17, 0.05), (x1, 0.33, top), band_sw,
               sw_of=lambda f: leaf_sw if f in ("-y", "+y") else band_sw)
        for z in (0.9, top * 0.5, top - 0.7):
            box_bm(bm, tags, (min(sx * 0.06, sx * (half - 0.06)), 0.15, z), (max(sx * 0.06, sx * (half - 0.06)), 0.35, z + 0.18), band_sw,
                   skip=("-z",))
            for x in (half * 0.3, half * 0.75):
                cx, cz = sx * x, z + 0.09
                base = [bm.verts.new((cx + qx * 0.09, 0.15, cz + qz * 0.09)) for qx, qz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
                apex = bm.verts.new((cx, 0.06, cz))
                pyr = [bm.faces.new((base[q], base[(q + 1) % 4], apex)) for q in range(4)]
                for f in pyr:
                    f.material_index = tags(stud_sw)
                orient(pyr, away_from(Vector((cx, 0.15, cz))))
        box_bm(bm, tags, (min(sx * (half - 0.12), sx * half), 0.15, 0.05), (max(sx * (half - 0.12), sx * half), 0.35, top), band_sw,
               skip=("-z",))
        obj = tagged_object("Door" + side, bm, tags, default=leaf_sw)
        set_origin(obj, Vector((sx * half, 0.25, 0.0)), parent=root)


def lunette(bm, tags, half, spring, bars=3):
    """The fixed lunette over the leaves: a charcoal half disc in the doors' plane with bronze bars and a bronze
    lintel at the springing (5 cm over the leaves' tops)."""
    flat_poly(bm, tags, [(x, 0.3, z) for x, z in arc_pts(half, spring, 0.0, 180.0, 8)], "charcoal", Vector((0, -1, 0)))
    box_bm(bm, tags, (-half, 0.2, spring), (half, 0.3, spring + 0.2), "bronze_d", skip=("+y",))
    for q in range(bars):
        x = -half + 2 * half * (q + 1) / (bars + 1)
        h = spring + math.sqrt(half * half - x * x) - 0.02
        box_bm(bm, tags, (x - 0.06, 0.22, spring + 0.2), (x + 0.06, 0.3, h), "bronze", skip=("+y", "-z", "+z"))


def col_gate(name, width=12.0):
    """A side gate: a 12 m slice of the bowl's profile with an arched tunnel (5.0 x 7.0 m) through its base and the
    seats carried over it. Over the tunnel the front rises to a tribunal (7.7, level with walkway 1) behind a marble
    balustrade, then maenianum II and III, the colonnade, the velarium continue as the runs'. Travertine engaged
    columns, a travertine archivolt and keystone; a charcoal tunnel 12 m deep with a dirt floor and an ink end.
    Root -> SM_ColGate_Frame, DoorL, DoorR (bronze-studded timber), hinges like SM_ImpGate's. Origin at the bottom
    centre of the front face; front toward -Y (local +Z). The leaves fill the opening to the springing (4.5) under a
    fixed bronze-barred lunette."""
    root = K.empty(name)
    hw, oh = width / 2, GATE_OPEN_W / 2
    prof = gate_front() + upper_bowl()
    bm, tags = bmesh.new(), Tags()
    slab(bm, tags, -hw, -oh, prof, cap1="charcoal")
    slab(bm, tags, oh, hw, prof, cap0="charcoal")
    slab(bm, tags, -oh, oh, clip_above(prof, GATE_OPEN_H, "charcoal"))
    arched_opening(bm, tags, oh, GATE_SPRING, GATE_OPEN_H, 0.6)
    depth = 12.0
    flat_poly(bm, tags, [(-oh, 0.4, TUNNEL_FLOOR), (oh, 0.4, TUNNEL_FLOOR), (oh, depth, TUNNEL_FLOOR), (-oh, depth, TUNNEL_FLOOR)],
              "dirt_floor", Vector((0, 0, 1)))
    lunette(bm, tags, oh, GATE_SPRING, bars=2)
    flat_poly(bm, tags, [(-oh, depth, 0.0), (oh, depth, 0.0), (oh, depth, GATE_OPEN_H), (-oh, depth, GATE_OPEN_H)], "ink",
              Vector((0, -1, 0)))
    # the archivolt (a flat travertine band round the arch), the keystone, the imposts
    # the archivolt, the keystone and the imposts all sit above the leaves' tops (4.45): the 100 deg swing clears them
    ring = arc_pts(oh + 0.45, GATE_SPRING, 0.0, 180.0, 8) + list(reversed(arc_pts(oh, GATE_SPRING, 0.0, 180.0, 8)))
    flat_poly(bm, tags, [(x, -0.03, z) for x, z in ring], "travertine", Vector((0, -1, 0)))
    box_bm(bm, tags, (-0.35, -0.12, GATE_OPEN_H), (0.35, 0.2, GATE_OPEN_H + 0.6), "travertine")
    for sx in (-1, 1):
        box_bm(bm, tags, (min(sx * oh, sx * (oh + 0.55)), -0.1, GATE_SPRING), (max(sx * oh, sx * (oh + 0.55)), 0.2, GATE_SPRING + 0.3),
               "travertine")
    # the colonnade's arches and shields across the slice, a mast and the velarium over it
    arch = arch_outline(1.2, ARCH_TOP - 1.2, 14.3)
    for x in (-4.0, 0.0, 4.0):
        flat_poly(bm, tags, [(x + px, COLON_U - 0.06, py) for px, py in arch], "charcoal", Vector((0, -1, 0)))
        flat_poly(bm, tags, [(x + px, COLON_U - 0.05, py) for px, py in hexagon(0.42, 0.0, (ARCH_TOP + ATTIC_TOP - 0.4) / 2)],
                  "bronze", Vector((0, -1, 0)))
    box_bm(bm, tags, (-0.18, MAST_U - 0.18, ATTIC_TOP), (0.18, MAST_U + 0.18, MAST_TOP), "wood_dark", skip=("-z",))
    sail = [(SAIL_IN[0], SAIL_IN[1], "cloth_white"), (SAIL_OUT[0], SAIL_OUT[1], "cloth_white"),
            (SAIL_OUT[0], SAIL_OUT[1] - 0.08, "cloth_white"), (SAIL_IN[0], SAIL_IN[1] - 0.08, "cloth_white")]
    for q, (x0, x1) in enumerate(((-hw, -hw / 3), (-hw / 3, hw / 3), (hw / 3, hw))):
        sw = "red_banner" if q % 2 else "cloth_white"
        slab(bm, tags, x0, x1, [(u, y, sw) for u, y, _ in sail], cap0=sw, cap1=sw)
    frame = tagged_object(name + "_Frame", bm, tags, default="travertine_d", parent=root)
    # engaged columns (half sunk in the face) on bases, with capitals under the cornice band
    parts = [frame]
    for sx in (-1, 1):
        for x in (oh + 0.75, hw - 0.75):
            parts.append(K.box("Base", size=(0.95, 0.5, 0.6), location=(sx * x, -0.05, 0.0), pivot=(0, 0, -1), swatch="stone_grey"))
            parts.append(K.cylinder("Col", radius=0.36, depth=TRIBUNAL - 0.6 - 0.6 - 0.25, verts=8, location=(sx * x, 0.0, 0.6),
                                    pivot=(0, 0, -1), swatch="travertine"))
            parts.append(K.box("Cap", size=(0.95, 0.55, 0.25), location=(sx * x, -0.05, TRIBUNAL - 0.85), pivot=(0, 0, -1),
                               swatch="travertine_d"))
    f = join(parts, name + "_Frame")
    set_origin(f, Vector((0, 0, 0)), parent=root)
    doors(root, oh, GATE_SPRING)
    return root


def great_gate(name, width=22.0, depth=BACK_U):
    """The Gates of Life and Death: a 22 m monumental arch on the chord, rising above the bowl. A block as deep as the
    bowl (its sides cover the runs' end caps) with a 5.0 x 9.0 m arched tunnel; two storeys of travertine: engaged
    columns and blind arches (charcoal) below, pilasters, a bronze-lettered panel and red banners above; a laurel
    keystone (a bronze_l wreath on a travertine block); a cornice at 24 and a stepped crown carrying a great bronze
    laurel wreath (~26.6 m). Root -> SM_ColGreatGate_Frame, DoorL, DoorR (as SM_ColGate's, 9 m). Origin at the bottom
    centre of the front face; front toward -Y."""
    root = K.empty(name)
    hw, oh = width / 2, GREAT_OPEN_W / 2
    mid, front_u = 13.0, 6.0
    # the facade block rises to GREAT_TOP for its first 6 m; behind, it steps down to the bowl's attic height
    prof = [(0.0, -0.3, "stone_grey"), (0.0, 0.6, "travertine"), (0.0, mid, "travertine_d"), (0.0, mid + 1.2, "travertine"),
            (0.0, GREAT_TOP - 1.2, "travertine_d"), (0.0, GREAT_TOP, "travertine_d"), (front_u, GREAT_TOP, "travertine_d"),
            (front_u, ATTIC_TOP, "travertine_d"), (depth, ATTIC_TOP, "travertine_d"), (depth, -0.3, "travertine_d")]
    bm, tags = bmesh.new(), Tags()
    slab(bm, tags, -hw, -oh, prof, cap1="charcoal")
    slab(bm, tags, oh, hw, prof, cap0="charcoal")
    slab(bm, tags, -oh, oh, clip_above(prof, GREAT_OPEN_H, "charcoal"))
    arched_opening(bm, tags, oh, GREAT_SPRING, GREAT_OPEN_H, 0.9)
    tun = 14.0
    flat_poly(bm, tags, [(-oh, 0.4, TUNNEL_FLOOR), (oh, 0.4, TUNNEL_FLOOR), (oh, tun, TUNNEL_FLOOR), (-oh, tun, TUNNEL_FLOOR)],
              "dirt_floor", Vector((0, 0, 1)))
    lunette(bm, tags, oh, GREAT_SPRING)
    flat_poly(bm, tags, [(-oh, tun, 0.0), (oh, tun, 0.0), (oh, tun, GREAT_OPEN_H), (-oh, tun, GREAT_OPEN_H)], "ink", Vector((0, -1, 0)))
    ring = arc_pts(oh + 0.6, GREAT_SPRING, 0.0, 180.0, 8) + list(reversed(arc_pts(oh, GREAT_SPRING, 0.0, 180.0, 8)))
    flat_poly(bm, tags, [(x, -0.03, z) for x, z in ring], "travertine", Vector((0, -1, 0)))
    for sx in (-1, 1):
        box_bm(bm, tags, (min(sx * oh, sx * (oh + 0.75)), -0.12, GREAT_SPRING), (max(sx * oh, sx * (oh + 0.75)), 0.2, GREAT_SPRING + 0.4),
               "travertine")
        # blind arches in the lower side bays, with travertine archivolts
        cx = sx * 7.4
        flat_poly(bm, tags, [(cx + x, -0.04, z) for x, z in arch_outline(1.5, 5.0, 0.6)], "charcoal", Vector((0, -1, 0)))
        av = arc_pts(1.85, 5.0, 0.0, 180.0, 6, cx) + list(reversed(arc_pts(1.5, 5.0, 0.0, 180.0, 6, cx)))
        flat_poly(bm, tags, [(x, -0.06, z) for x, z in av], "travertine", Vector((0, -1, 0)))
        # red banners in the upper side bays
        bx = sx * 7.4
        box_bm(bm, tags, (bx - 1.0, -0.2, GREAT_TOP - 1.6), (bx + 1.0, -0.04, GREAT_TOP - 1.4), "bronze_d")
        box_bm(bm, tags, (bx - 0.85, -0.16, mid + 2.0), (bx + 0.85, -0.08, GREAT_TOP - 1.4), "red_banner")
        box_bm(bm, tags, (bx - 0.85, -0.18, mid + 2.0), (bx + 0.85, -0.06, mid + 2.35), "cloth_white")
    # the upper storey's panel (bronze lettering on dark bronze) and the laurel keystone
    box_bm(bm, tags, (-4.2, -0.08, mid + 3.2), (4.2, 0.0, mid + 6.6), "bronze_d")
    for row, z in enumerate((mid + 3.8, mid + 4.75, mid + 5.7)):
        w = (3.4, 3.8, 2.6)[row]
        box_bm(bm, tags, (-w, -0.12, z), (w, -0.08, z + 0.42), "bronze_l", skip=("+y",))
    box_bm(bm, tags, (-0.55, -0.25, GREAT_OPEN_H), (0.55, 0.3, GREAT_OPEN_H + 1.4), "travertine")
    wreath = arc_pts(0.62, GREAT_OPEN_H + 0.7, 0.0, 360.0, 10)[:-1]
    inner = arc_pts(0.4, GREAT_OPEN_H + 0.7, 0.0, 360.0, 10)[:-1]
    for q in range(10):
        j = (q + 1) % 10
        flat_poly(bm, tags, [(wreath[q][0], -0.27, wreath[q][1]), (wreath[j][0], -0.27, wreath[j][1]),
                             (inner[j][0], -0.27, inner[j][1]), (inner[q][0], -0.27, inner[q][1])], "bronze_l", Vector((0, -1, 0)))
    frame = tagged_object(name + "_Frame", bm, tags, default="travertine_d", parent=root)
    parts = [frame]
    for sx in (-1, 1):
        for x in (oh + 1.15, hw - 0.9):  # lower storey: engaged columns on bases, capitals under the entablature
            parts.append(K.box("Base", size=(1.3, 0.7, 0.8), location=(sx * x, -0.1, 0.0), pivot=(0, 0, -1), swatch="stone_grey"))
            parts.append(K.cylinder("Col", radius=0.5, depth=mid - 0.8 - 0.4, verts=8, location=(sx * x, 0.0, 0.8),
                                    pivot=(0, 0, -1), swatch="travertine"))
            parts.append(K.box("Cap", size=(1.3, 0.8, 0.4), location=(sx * x, -0.1, mid - 0.4), pivot=(0, 0, -1),
                               swatch="travertine_d"))
            parts.append(K.box("Pilaster", size=(0.9, 0.3, GREAT_TOP - 1.2 - mid - 1.2), location=(sx * x, -0.15, mid + 1.2),
                               pivot=(0, 0, -1), swatch="travertine_d"))
    parts.append(K.box("Entablature", size=(width + 0.6, 0.6, 0.5), location=(0, -0.3, mid + 1.2 - 0.25), pivot=(0, 0, -1),
                       swatch="travertine"))
    parts.append(K.box("Cornice", size=(width + 0.8, 1.2, 0.6), location=(0, -0.6, GREAT_TOP - 0.6), pivot=(0, -1, -1),
                       swatch="travertine"))
    # the crown: a stepped pedestal and the great bronze laurel wreath on a bronze_d base
    parts.append(K.box("Attic", size=(10.0, 3.6, 1.2), location=(0, 0.2, GREAT_TOP), pivot=(0, -1, -1), swatch="travertine"))
    parts.append(K.box("AtticCap", size=(10.4, 4.0, 0.25), location=(0, 0.0, GREAT_TOP + 1.2), pivot=(0, -1, -1), swatch="travertine_d"))
    wr, wz = 1.15, GREAT_TOP + 1.45 + 1.15
    rim = arc_pts(wr, 0.0, 0.0, 360.0, 14)[:-1]
    hole = arc_pts(wr * 0.68, 0.0, 0.0, 360.0, 14)[:-1]
    wreath = K.extrude_poly("Wreath", rim[:8] + list(reversed(hole[:8])), 0.35, plane="XZ", location=(0, 1.8, wz), swatch="bronze_l")
    wreath2 = K.extrude_poly("Wreath2", rim[7:] + rim[:1] + list(reversed(hole[7:] + hole[:1])), 0.35, plane="XZ",
                             location=(0, 1.8, wz), swatch="bronze")
    parts.append(K.box("WreathFoot", size=(1.2, 0.8, 0.5), location=(0, 1.8, GREAT_TOP + 1.45), pivot=(0, 0, -1), swatch="bronze_d"))
    parts += [wreath, wreath2]
    for sx in (-1, 1):
        parts.append(K.cone("Acroterion", radius=0.8, depth=1.6, verts=4, location=(sx * (hw - 0.8), 0.6, GREAT_TOP),
                            pivot=(0, 0, -1), swatch="bronze_d"))
    f = join(parts, name + "_Frame")
    set_origin(f, Vector((0, 0, 0)), parent=root)
    doors(root, oh, GREAT_SPRING)
    return root


def pulvinar(name, half=BOX_HALF, depth=TERRACE_U, over=1.0):
    """The Emperor's box (and its twin, the editor's): on the podium, in front of the continuous bowl. Its loge floor
    (top 4.03, over the terrace's 4.0) runs from 1.0 m over the arena (local z +1) back to the terrace's end (local z
    -2.4), between purple parapets with gold copings; four marble columns with gold capitals carry a purple canopy
    with a gold fringe at 9 m and a purple pitched roof with a gold finial; a gold throne at local (0, 4.0, -0.8);
    two marble benches for 14 senators. 12.8 m wide (the wall line's samples either side of the axis); the bowl's
    balustrade stops at its sides. Origin at the bottom centre of the podium face (on the wall line); front toward
    -Y."""
    fl = PODIUM_H + 0.03
    parts = [K.box(name, size=(2 * half, depth + over, 0.2), location=(0, -over, fl - 0.2), pivot=(0, -1, -1), swatch="purple_d")]
    parts.append(K.box("Carpet", size=(2 * half - 0.5, depth + over - 0.4, 0.015), location=(0, -over + 0.2, fl), pivot=(0, -1, -1),
                       swatch="purple"))
    # parapets: front and sides, purple panels, gold copings
    parts.append(K.box("Front", size=(2 * half, 0.22, 0.85), location=(0, -over, fl), pivot=(0, -1, -1), swatch="purple"))
    parts.append(K.box("FrontCap", size=(2 * half + 0.1, 0.32, 0.12), location=(0, -over - 0.05, fl + 0.85), pivot=(0, -1, -1),
                       swatch="gold"))
    parts.append(K.box("FrontBand", size=(2 * half + 0.04, 0.26, 0.1), location=(0, -over - 0.02, fl - 0.2), pivot=(0, -1, -1),
                       swatch="gold_d"))
    for sx in (-1, 1):
        parts.append(K.box("Side", size=(0.22, depth + over, 0.85), location=(sx * (half - 0.11), -over, fl), pivot=(0, -1, -1),
                           swatch="purple"))
        parts.append(K.box("SideCap", size=(0.32, depth + over, 0.12), location=(sx * (half - 0.11), -over, fl + 0.85),
                           pivot=(0, -1, -1), swatch="gold"))
        for y in (-over + 0.45, depth - 0.35):
            parts.append(K.cylinder("Col", radius=0.24, depth=9.0 - fl - 0.3, verts=8, location=(sx * (half - 0.45), y, fl),
                                    pivot=(0, 0, -1), swatch="marble"))
            parts.append(K.box("Capital", size=(0.62, 0.62, 0.3), location=(sx * (half - 0.45), y, 9.0 - 0.3), pivot=(0, 0, -1),
                               swatch="gold"))
        parts.append(K.box("Bench", size=(4.3, 0.55, 0.45), location=(sx * 3.45, 0.75, fl), pivot=(0, 0, -1), swatch="marble"))
    # the canopy: a purple slab, a valance with a gold fringe round three sides, a pitched roof and a finial
    cy0, cy1 = -over - 0.25, depth + 0.25
    parts.append(K.box("Canopy", size=(2 * half + 0.5, cy1 - cy0, 0.3), location=(0, cy0, 9.0), pivot=(0, -1, -1), swatch="purple"))
    parts.append(K.box("Valance", size=(2 * half + 0.56, 0.08, 0.55), location=(0, cy0 - 0.04, 8.45), pivot=(0, -1, -1),
                       swatch="purple_d"))
    parts.append(K.box("Fringe", size=(2 * half + 0.6, 0.1, 0.14), location=(0, cy0 - 0.06, 8.38), pivot=(0, -1, -1), swatch="gold"))
    for sx in (-1, 1):
        parts.append(K.box("ValanceS", size=(0.08, cy1 - cy0, 0.55), location=(sx * (half + 0.28), cy0, 8.45), pivot=(0, -1, -1),
                           swatch="purple_d"))
        parts.append(K.box("FringeS", size=(0.1, cy1 - cy0, 0.14), location=(sx * (half + 0.29), cy0, 8.38), pivot=(0, -1, -1),
                           swatch="gold"))
    roof = K.extrude_poly("Roof", [(cy0 - 0.1, 0.0), (cy1 + 0.1, 0.0), ((cy0 + cy1) / 2, 1.5)], 2 * half + 0.7, plane="YZ",
                          location=(0, 0, 9.3), swatch="purple_d")
    parts.append(roof)
    parts.append(K.cone("Finial", radius=0.35, depth=1.1, verts=6, location=(0, (cy0 + cy1) / 2, 10.75), pivot=(0, 0, -1),
                        swatch="gold"))
    # the throne at local (0, 4.0, -0.8): gold seat, back and arms
    parts.append(K.box("Throne", size=(1.2, 0.9, 0.55), location=(0, 0.8, fl), pivot=(0, 0, -1), swatch="gold_d"))
    parts.append(K.box("ThroneBack", size=(1.2, 0.25, 1.9), location=(0, 1.25, fl), pivot=(0, 0, -1), taper=(0.75, 1.0), swatch="gold"))
    for sx in (-1, 1):
        parts.append(K.box("ThroneArm", size=(0.18, 0.9, 0.3), location=(sx * 0.6, 0.8, fl + 0.55), pivot=(0, 0, -1), swatch="gold"))
    return at_origin(join(parts, name))


# ------------------------------------------------------------------------------------------------ hills and statues
# The hills: 14 m along local X, 10 m along local Z, top 2.2, a 2.2 m rock face on local -z (the wall side), the
# chests' and altars' anchors at their origin's (x, z) on top. A 6 m-wide top inside 14 m can't have the contract's
# <= 20 deg sides (2.2 / tan 20 = 6.04 m of run a side): the top is 6 x 6 m (x +-3, z -5 .. +1, round the anchor) and
# the three open sides fall 2.2 m over 4 m (28.8 deg; the player's slope limit is 50). docs/tracks/R2-env-art.md.
MONS_HX, MONS_HZ, MONS_TOP = 7.0, 5.0, 2.2
MONS_FLAT_X, MONS_FLAT_Z = 3.0, 1.0


def mons_h(x, z):
    return MONS_TOP * min(1.0, (MONS_HX - abs(x)) / (MONS_HX - MONS_FLAT_X), (MONS_HZ - z) / (MONS_HZ - MONS_FLAT_Z))


def rock(bm, tags, c, size, rng, sw, jit=0.18):
    """A boulder: a box (Blender centre-bottom c, size) with its corners jittered, triangulated."""
    before = set(bm.faces)
    x0, y0 = c[0] - size[0] / 2, c[1] - size[1] / 2
    box_bm(bm, tags, (x0, y0, c[2]), (x0 + size[0], y0 + size[1], c[2] + size[2]), sw, skip=("-z",))
    new = [f for f in bm.faces if f not in before]
    vs = {v for f in new for v in f.verts}
    for v in vs:
        k = Vector((rng.uniform(-jit, jit) * size[0], rng.uniform(-jit, jit) * size[1], rng.uniform(-jit, jit) * size[2]))
        if v.co.z <= c[2] + 1e-6:
            k.z = 0.0
        v.co += k
    bmesh.ops.triangulate(bm, faces=new, quad_method="BEAUTY")


def mons_a(name, seed=83):
    """A sand hill: the top flat at 2.2 (6 x 6 m), slopes of sand with darker patches on three sides, a rock face of
    stone_grey and slate boulders along the back (local -z), two low boulders at its corners. Walkable; origin at the
    base centre."""
    rng = random.Random(seed)
    bm, tags = bmesh.new(), Tags()
    xs = [float(x) for x in range(-7, 8)]
    zs = [float(z) for z in range(-5, 6)]
    v = {}
    for x in xs:
        for z in zs:
            h = mons_h(x, z)
            flat = h >= MONS_TOP - 1e-6 or abs(x) >= MONS_HX or z >= MONS_HZ
            v[(x, z)] = bm.verts.new(B(x, z, h + (0.0 if flat else rng.uniform(-0.07, 0.07))))
    top = []
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            a, b2, c, d = (xs[i], zs[j]), (xs[i + 1], zs[j]), (xs[i + 1], zs[j + 1]), (xs[i], zs[j + 1])
            tris = ((a, b2, c), (a, c, d)) if xs[i] >= 0 else ((a, b2, d), (b2, c, d))
            for t in tris:
                if all(v[q].co.z < 1e-4 for q in t):
                    continue  # flat on the ground at the toe: the floor is there
                f = bm.faces.new([v[q] for q in t])
                cx = sum(q[0] for q in t) / 3
                cz = sum(q[1] for q in t) / 3
                on_top = mons_h(cx, cz) >= MONS_TOP - 1e-6
                r = rng.random()
                f.material_index = tags("sand" if on_top or r > 0.22 else "sand_d" if r > 0.08 else "dirt_floor")
                top.append(f)
    orient(top, lambda cc, n: Vector((0, 0, 1)))
    back = []
    for i in range(len(xs) - 1):  # the rock face's earth behind the boulders (local z = -5 faces local -z = Blender +y)
        p, q = v[(xs[i], -5.0)], v[(xs[i + 1], -5.0)]
        pb = bm.verts.new((p.co.x, p.co.y, 0.0))
        qb = bm.verts.new((q.co.x, q.co.y, 0.0))
        if p.co.z < 1e-4:
            vs = (pb, q, qb)
        elif q.co.z < 1e-4:
            vs = (p, pb, qb)
        else:
            vs = (p, q, qb, pb)
        back.append(bm.faces.new(vs))
        back[-1].material_index = tags("slate")
    orient(back, lambda cc, n: Vector((0, 1, 0)))
    for k, x in enumerate((-4.6, -2.7, -0.9, 0.9, 2.6, 4.4)):  # the rock face: boulders along the back edge
        hgt = mons_h(x, -5.0) + rng.uniform(0.15, 0.4)
        c = B(x + rng.uniform(-0.2, 0.2), -4.55, 0.0)  # kept inside the 10 m footprint (to local z -5.2)
        rock(bm, tags, c, (rng.uniform(1.7, 2.2), rng.uniform(0.9, 1.1), hgt), rng, "stone_grey" if k % 2 else "slate", jit=0.1)
    for sx in (-1, 1):
        rock(bm, tags, B(sx * 6.0, -4.2, 0.0), (1.2, 1.1, 0.8), rng, "stone_grey")
    return tagged_object(name, bm, tags, default="sand")


def frustum(bm, tags, lo_rect, hi_rect, y0, y1, sw_top, sw_bank, sw_back):
    """A terrace of a stepped mound in Unity-local terms: bottom rect (x0, x1, z0, z1) at y0, top rect at y1; the back
    (z0, the same for both) vertical; no bottom."""
    (ax0, ax1, az0, az1), (bx0, bx1, bz0, bz1) = lo_rect, hi_rect
    lo = [bm.verts.new(B(x, z, y0)) for x, z in ((ax0, az0), (ax1, az0), (ax1, az1), (ax0, az1))]
    hi = [bm.verts.new(B(x, z, y1)) for x, z in ((bx0, bz0), (bx1, bz0), (bx1, bz1), (bx0, bz1))]
    faces = [bm.faces.new(hi)]
    faces[0].material_index = tags(sw_top)
    for k in range(4):
        j = (k + 1) % 4
        faces.append(bm.faces.new((lo[k], lo[j], hi[j], hi[k])))
        faces[-1].material_index = tags(sw_back if k == 0 else sw_bank)
    centre = B((ax0 + ax1) / 2, (az0 + az1) / 2, (y0 + y1) / 2)
    orient(faces, lambda cc, n: Vector((0, 0, 1)) if abs(n.z) > 0.9 else Vector((cc.x - centre.x, cc.y - centre.y, 0)))
    return faces


def stump(bm, tags, x, z, y, r, h, rng, sw="travertine", segs=8):
    """A broken column stump (Unity-local base centre (x, y, z)): its top ring jittered like a break."""
    ring0 = [bm.verts.new(B(x + r * math.cos(2 * math.pi * k / segs), z + r * math.sin(2 * math.pi * k / segs), y)) for k in range(segs)]
    ring1 = [bm.verts.new(B(x + r * math.cos(2 * math.pi * k / segs), z + r * math.sin(2 * math.pi * k / segs),
                            y + h + 0.15 * math.sin(2 * math.pi * k / segs + 1.0) + rng.uniform(-0.05, 0.05))) for k in range(segs)]
    faces = []
    for k in range(segs):
        j = (k + 1) % segs
        faces.append(bm.faces.new((ring0[k], ring0[j], ring1[j], ring1[k])))
        faces[-1].material_index = tags(sw)
    cap = bm.faces.new(ring1)
    cap.material_index = tags("travertine_d")
    c = B(x, z, y + h / 2)
    orient(faces, lambda cc, n: Vector((cc.x - c.x, cc.y - c.y, 0)))
    orient([cap], lambda cc, n: Vector((0, 0, 1)))
    bmesh.ops.triangulate(bm, faces=[cap], quad_method="BEAUTY", ngon_method="EAR_CLIP")


def mons_b(name, seed=89):
    """A stepped 'ruin' mound: four terraces of 0.55 m (banks at 45 deg, walkable; treads 0.45 m) to a flat top of
    6.9 x 6.45 m round the anchor (x +-3.45, z -5 .. 1.45), a sheer ruined back wall on local -z, two broken column
    stumps (0.6 and 0.95 m) on the top's back edge, rubble on the treads. Travertine treads, travertine_d banks, a
    stone_grey back. Walkable; origin at the base centre."""
    rng = random.Random(seed)
    bm, tags = bmesh.new(), Tags()
    step = MONS_TOP / 4
    for k in range(4):
        lo = (-(MONS_HX - k), MONS_HX - k, -MONS_HZ, MONS_HZ - k)
        hi = (-(MONS_HX - k - step), MONS_HX - k - step, -MONS_HZ, MONS_HZ - k - step)
        frustum(bm, tags, lo, hi, step * k, step * (k + 1), "sand" if k == 3 else "travertine",
                "stone_grey" if k % 2 else "travertine_d", "stone_grey" if k % 2 else "slate")
    stump(bm, tags, -2.0, -4.3, MONS_TOP, 0.42, 0.5, rng)
    stump(bm, tags, 2.1, -4.25, MONS_TOP, 0.42, 0.78, rng)
    for x, z, k in ((5.9, 1.0, 0), (-4.9, 3.1, 1), (2.0, 3.6, 2), (-5.6, -2.0, 0), (4.4, 2.2, 1)):  # rubble on the treads
        rock(bm, tags, B(x, z, step * (k + 1)), (0.5, 0.4, 0.32), rng, "travertine_d", jit=0.12)
    rock(bm, tags, B(-2.6, -2.9, MONS_TOP), (0.9, 0.5, 0.45), rng, "travertine_d", jit=0.08)  # a fallen block on top
    # a fallen column on the ground at the front corner, in three drums, and a toppled capital by the east side
    for q in range(3):
        c = B(-6.2 + q * 0.75, 4.4 - q * 0.05, 0.0)
        cyl = bmesh.ops.create_cone(bm, cap_ends=True, segments=7, radius1=0.38, radius2=0.38, depth=0.68)["verts"]
        bmesh.ops.rotate(bm, verts=cyl, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(90 + rng.uniform(-8, 8)), 3, "Y"))
        bmesh.ops.translate(bm, verts=cyl, vec=c + Vector((0, 0, 0.38)))
        faces = list({f for v in cyl for f in v.link_faces})
        for f in faces:
            f.material_index = tags("travertine")
        orient(faces, away_from(c + Vector((0, 0, 0.38))))
    rock(bm, tags, B(6.3, -1.2, 0.0), (0.9, 0.9, 0.55), rng, "travertine_d", jit=0.06)
    return tagged_object(name, bm, tags, default="travertine")


def limb(name, p0, p1, w, d, sw, taper=(1.0, 1.0)):
    """A box from Blender point p0 to p1 (w x d section, its top scaled by taper)."""
    p0, p1 = Vector(p0), Vector(p1)
    o = K.box(name, size=(w, d, (p1 - p0).length), pivot=(0, 0, -1), taper=taper, swatch=sw)
    o.rotation_euler = (p1 - p0).normalized().to_track_quat("Z", "Y").to_euler()
    o.location = p0
    return o


def victor_statue(name):
    """A statue of the Eternal Victor: a 1.2 m octagonal travertine_d plinth (2 m across the flats, inside the
    obstacle's r 1.1) with a bronze plaque, and a 4.3 m bronze Victor (5.5 m in all): a gaunt thraex with a hook-crested
    helm, pteruges and a cloak, the falx raised in his right hand, its crescent arching over his head, the left fist
    forward. Origin at the base centre, faces local +Z (Blender -Y)."""
    r8 = 1.0 / math.cos(math.radians(22.5))
    parts = []
    for nm, rf, z0, z1, sw in (("Base", 1.0, 0.0, 0.2, "travertine_d"), ("Die", 0.88, 0.2, 1.0, "travertine_d"),
                               ("Cap", 1.0, 1.0, 1.2, "travertine")):
        o = K.cylinder(nm, radius=rf * r8, depth=z1 - z0, verts=8, location=(0, 0, z0), pivot=(0, 0, -1), swatch=sw)
        o.data.transform(Matrix.Rotation(math.radians(22.5), 4, "Z"))  # flats toward the axes, baked into the mesh
        parts.append(o)
    bm, tags = bmesh.new(), Tags()
    flat_poly(bm, tags, [(-0.45, -0.885, 0.4), (0.45, -0.885, 0.4), (0.45, -0.885, 0.8), (-0.45, -0.885, 0.8)], "bronze_l",
              Vector((0, -1, 0)))
    parts.append(tagged_object("Plaque", bm, tags, default="bronze_l"))
    z = 1.2
    S = lambda x, y, h: (x, y, z + h)  # noqa: E731
    for sx in (-1, 1):  # legs (greaves), stance apart
        parts.append(limb("Leg", S(sx * 0.33, 0.02, 0.0), S(sx * 0.2, 0.0, 1.25), 0.26, 0.3, "bronze_d", taper=(1.15, 1.1)))
    parts.append(K.box("Skirt", size=(0.85, 0.55, 0.5), location=S(0, 0, 1.1), pivot=(0, 0, -1), taper=(0.8, 0.85), swatch="bronze_d"))
    parts.append(K.box("Torso", size=(0.62, 0.42, 0.92), location=S(0, 0, 1.55), pivot=(0, 0, -1), taper=(1.55, 1.15), swatch="bronze"))
    for sx in (-1, 1):
        parts.append(K.box("Pauldron", size=(0.4, 0.48, 0.28), location=S(sx * 0.56, 0, 2.22), pivot=(0, 0, -1), swatch="bronze_l"))
    parts.append(K.box("Helm", size=(0.46, 0.5, 0.52), location=S(0, -0.02, 2.48), pivot=(0, 0, -1), taper=(0.9, 0.9), swatch="bronze"))
    parts.append(K.box("Brim", size=(0.7, 0.72, 0.07), location=S(0, -0.04, 2.6), pivot=(0, 0, -1), swatch="bronze_d"))
    crest = [(0.24, 2.9), (0.32, 3.18), (0.2, 3.42), (-0.06, 3.53), (-0.33, 3.44), (-0.42, 3.22), (-0.27, 3.21),
             (-0.12, 3.32), (0.07, 3.2), (0.1, 2.95)]
    parts.append(K.extrude_poly("Crest", [(y, h + z) for y, h in crest], 0.13, plane="YZ", swatch="bronze_l"))
    # right arm (Blender -X) raised with the falx, left fist forward
    sh_r, el_r, ha_r = S(-0.56, 0.0, 2.3), S(-0.86, -0.05, 2.82), S(-0.8, -0.1, 3.32)
    sh_l, el_l, ha_l = S(0.56, 0.0, 2.3), S(0.8, -0.24, 1.98), S(0.6, -0.52, 2.12)
    for a, b2 in ((sh_r, el_r), (el_r, ha_r), (sh_l, el_l), (el_l, ha_l)):
        parts.append(limb("Arm", a, b2, 0.2, 0.22, "bronze"))
    parts.append(limb("Haft", S(-0.8, -0.1, 2.9), S(-0.74, -0.1, 3.92), 0.09, 0.09, "bronze_d"))
    c, ro, ri = (-0.04, 3.62), 0.74, 0.55
    outer = [(c[0] + ro * math.cos(math.radians(a)), c[1] + ro * math.sin(math.radians(a))) for a in (160, 130, 100, 70, 40, 15)]
    inner = [(c[0] + ri * math.cos(math.radians(a)), c[1] + ri * math.sin(math.radians(a))) for a in (25, 55, 85, 115, 145)]
    blade = outer + [(c[0] + 0.8, c[1] - 0.12)] + inner
    parts.append(K.extrude_poly("Falx", [(x, h + z) for x, h in blade], 0.06, plane="XZ", location=(0, -0.1, 0), swatch="bronze_l"))
    parts.append(K.box("Cloak", size=(0.95, 0.09, 1.55), location=S(0, 0.27, 0.8), pivot=(0, 0, -1), taper=(0.75, 1.0), swatch="bronze_d"))
    return at_origin(join(parts, name))


# ------------------------------------------------------------------------------------------------ preview of the arena

def place(src, x, z, yaw=0.0, h=0.0, scale=1.0, coll=None):
    """A linked copy of src (and its children) at Unity (x, h, z) turned `yaw` degrees (Unity's yaw): Blender rotation
    -yaw (see the export's 180 deg turn)."""
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
    c.scale = (scale, scale, scale)
    return c


def yaw_of(dx, dz):
    return math.degrees(math.atan2(dx, dz))


def preview_layout(lib, open_groups=(), lifts_down=()):
    """The whole arena from the contract's numbers (a stand-in for ColosseumLayout, not the layout). open_groups: the
    trapdoor groups shown open (leaves at 100 deg); lifts_down: lift indices shown at the bottom of their shafts."""
    coll = bpy.data.collections.new("Preview")
    bpy.context.scene.collection.children.link(coll)
    P = lambda name, *a, **kw: place(lib[name], *a, coll=coll, **kw)  # noqa: E731
    count = {}

    def put(name, *a, **kw):
        count[name] = count.get(name, 0) + 1
        return P(name, *a, **kw)
    put("SM_ColFloor", 0.0, 0.0)
    for k in range(6):
        put("SM_ColCavea_%d" % k, 0.0, 0.0)
    for i, (x, z, yaw, half) in enumerate(GATES):
        put("SM_ColGreatGate" if i in GREAT else "SM_ColGate", x, z, yaw)
    for x, z, yaw in BOXES:
        put("SM_ColPulvinar", x, z, yaw)
    put("SM_ColStage", 0.0, 0.0)
    for x, z, yaw in ((0.0, 12.0, 0.0), (12.0, 0.0, 90.0), (0.0, -12.0, 180.0), (-12.0, 0.0, -90.0)):
        put("SM_ColStairs", x, z, yaw)
    for sx, sz in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
        put("PR_VictorStatue", 9.9 * sx, 9.9 * sz, yaw_of(sx, sz))
    put("SM_ColMons_A", 28.0, 50.5, 180.0)
    put("SM_ColMons_A", 28.0, -50.5, 0.0)
    put("SM_ColMons_B", -28.0, 50.5, 180.0)
    put("SM_ColMons_B", -28.0, -50.5, 0.0)
    for x, z, yaw, g in TRAPDOORS:
        c = put("SM_ColTrapdoor", x, z, yaw)
        if g in open_groups:
            for ch in c.children:
                if ch.name.startswith("LeafF"):
                    ch.rotation_euler.x = math.radians(-100.0)
                elif ch.name.startswith("LeafB"):
                    ch.rotation_euler.x = math.radians(100.0)
    for i, (x, z) in enumerate(LIFTS):
        c = put("SM_ColLift", x, z, 0.0)
        if i in lifts_down:
            for ch in c.children:
                if ch.name.startswith("Platform"):
                    ch.location.z = -3.0
    count["SM_ColLift (Victor's, x1.25)"] = 1
    P("SM_ColLift", 0.0, 0.0, 0.0, h=STAGE_H, scale=1.25)
    return coll, count


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
    if part == "floor":
        return {"SM_ColFloor": col_floor("SM_ColFloor")}
    if part == "hypogeum":
        return {"SM_ColTrapdoor": trapdoor("SM_ColTrapdoor"), "SM_ColLift": lift("SM_ColLift"),
                "SM_ColStage": stage("SM_ColStage"), "SM_ColStairs": col_stairs("SM_ColStairs")}
    if part == "cavea":
        return {"SM_ColCavea_%d" % k: cavea("SM_ColCavea_%d" % k, k) for k in range(6)}
    if part == "gates":
        return {"SM_ColGate": col_gate("SM_ColGate"), "SM_ColGreatGate": great_gate("SM_ColGreatGate"),
                "SM_ColPulvinar": pulvinar("SM_ColPulvinar")}
    if part == "props":
        return {"SM_ColMons_A": mons_a("SM_ColMons_A"), "SM_ColMons_B": mons_b("SM_ColMons_B"),
                "PR_VictorStatue": victor_statue("PR_VictorStatue")}
    raise ValueError(part)


def clean_names(root):
    """Blender suffixes a clashing name (DoorL.001 when two gates are built in one scene); the FBX must carry the plain
    names code looks up (DoorL, DoorR, LeafF, LeafB, Platform): move any clashing object aside first."""
    import re
    for d in K.descendants(root):
        m = re.match(r"^(.*)\.\d{3}$", d.name)
        if not m:
            continue
        other = bpy.data.objects.get(m.group(1))
        if other is not None and other is not d:
            other.name = m.group(1) + "_other"
        d.name = m.group(1)


def export_items(items, blend_name):
    for o in items.values():
        at_origin(o)
    x = 0.0  # side by side for the .blend
    for n, o in items.items():
        lo, hi = K.world_bounds(K.descendants(o))
        o.location.x += x - lo.x
        x += (hi.x - lo.x) + 2.0
    review = ARGS.get("review")  # noqa: F821
    for n, o in items.items():
        saved = o.location.copy()
        o.location = (0, 0, 0)
        if review in (n, "all") or (review and n in review.split(",")):
            OUT[n + "_review"] = K.review_render(o, n.lower(), size=420)["sheet"]
        clean_names(o)
        OUT[n] = K.export_fbx(o, "Environment/" + n)
        OUT[n + "_tris"] = K.tri_count(K.descendants(o) or [o])
        lo, hi = K.world_bounds(K.descendants(o))
        OUT[n + "_bounds"] = (tuple(round(v, 2) for v in lo), tuple(round(v, 2) for v in hi))
        o.location = saved
    OUT["blend"] = K.save_blend(blend_name)


PARTS = ("floor", "hypogeum", "cavea", "gates", "props")
MODELS = ["SM_ColFloor", "SM_ColTrapdoor", "SM_ColLift", "SM_ColStage", "SM_ColStairs"] + ["SM_ColCavea_%d" % k for k in range(6)] + \
    ["SM_ColGate", "SM_ColGreatGate", "SM_ColPulvinar", "SM_ColMons_A", "SM_ColMons_B", "PR_VictorStatue"]
K.clear_scene()
if PART in PARTS:
    export_items(build(PART), "colosseum_" + PART)
    result = OUT
elif PART == "overview":
    lib = {}
    for p in PARTS:
        lib.update(build(p))
    for o in lib.values():
        at_origin(o)
        for d in K.descendants(o):
            d.hide_render = True
    shots = K.SHOTS_DIR
    os.makedirs(shots, exist_ok=True)
    tag = ARGS.get("tag", "")  # noqa: F821
    coll, count = preview_layout(lib, open_groups=(4,), lifts_down=(1, 2))
    for o in coll.all_objects:
        o.hide_render = False
    per = {n: K.tri_count(K.descendants(lib[n])) for n in lib}
    per["SM_ColLift (Victor's, x1.25)"] = per["SM_ColLift"]
    OUT["preview_tris"] = K.tri_count(coll.all_objects)
    OUT["preview_pieces"] = len([o for o in coll.objects if o.parent is None])
    OUT["tris_by_model"] = {n: "%d x %d = %d" % (count[n], per[n], count[n] * per[n]) for n in count}
    views = {
        "aerial": ((-175.0, 120.0, -150.0), (0.0, 0.0, 0.0), 30.0),
        "close": ((0.0, 3.2, -36.0), (0.0, 3.5, 12.0), 22.0),
        "deathgate": ((52.0, 5.0, -10.0), (96.67, 12.0, 0.0), 24.0),
        "sidegate": ((26.0, 4.0, 24.0), (49.0, 8.0, 58.0), 24.0),
        "open": ((-20.0, 9.0, -16.0), (-38.0, 0.0, 6.0), 26.0),
        "north": ((0.0, 6.0, 20.0), (0.0, 10.0, 67.0), 24.0),
    }
    for v, (cl, tg, lens) in views.items():
        OUT[v] = render_view(os.path.join(shots, "colosseum_overview_%s%s.png" % (v, tag)), cl, tg, lens=lens)
    OUT["top"] = render_view(os.path.join(shots, "colosseum_overview_top%s.png" % tag), (0.0, 300.0, 0.0), (0.0, 0.0, 0.0),
                             res=(1800, 1260), ortho=245.0, top=True)
    result = OUT
elif PART == "doors":
    # swing each gate's doors as ArenaGates does (yaw about the hinge, into the arena) on the exported FBX and count
    # leaf / frame triangle overlaps in world space; "not_jamb" leaves out the jamb planes (|x| = 2.5), which every
    # hinge-at-the-jamb door touches (SM_ImpGate included). Past ~70 deg the leaf's hinge end also crosses the face
    # plane within ~0.1 m of the hinge, as SM_ImpGate's does: those are the remaining counts.
    from mathutils.bvhtree import BVHTree

    def world_tree(o):
        m = o.matrix_world
        return BVHTree.FromPolygons([m @ v.co for v in o.data.vertices], [list(p.vertices) for p in o.data.polygons])
    for name in ARGS.get("names", "SM_ColGate,SM_ColGreatGate,SM_ImpGate").split(","):  # noqa: F821
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(K.MODELS_DIR, "Environment", name + ".fbx"))
        objs = [o for o in bpy.data.objects if o not in before]
        next(o for o in objs if o.parent is None).rotation_euler = (0, 0, 0)  # Blender coordinates again
        frame = next(o for o in objs if o.name.endswith("_Frame"))
        doors_ = {o.name.split(".")[0]: o for o in objs if o.name.startswith("Door")}
        res = {}
        for ang in (0, 50, 100):
            for nm, d in doors_.items():
                d.rotation_euler = (0, 0, math.radians(ang if nm == "DoorR" else -ang))
            bpy.context.view_layer.update()
            tf = world_tree(frame)
            fp, mw = frame.data.polygons, frame.matrix_world

            def near_hinge(i, j, d):
                c = mw @ fp[i].center
                n = (mw.to_3x3() @ fp[i].normal).normalized()
                return abs(abs(c.x) - GATE_OPEN_W / 2) < 0.01 and abs(n.x) > 0.99
            for nm, d in doors_.items():
                pairs = tf.overlap(world_tree(d))
                res["%s@%d" % (nm, ang)] = "%d (not jamb %d)" % (len(pairs), len([1 for i, j in pairs if not near_hinge(i, j, d)]))
        OUT[name] = res
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    result = OUT
elif PART == "check":
    # re-import every FBX: the hierarchy (children's local positions in the FBX root's frame), triangles, sizes
    names = ARGS.get("names", ",".join(MODELS + ["SM_ImpGate"])).split(",")  # noqa: F821
    for n in names:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(K.MODELS_DIR, "Environment", n + ".fbx"))
        objs = [o for o in bpy.data.objects if o not in before]
        lo, hi = K.world_bounds(objs)
        OUT[n] = {"tris": K.tri_count(objs), "size": tuple(round(v, 2) for v in (hi - lo)),
                  "min": tuple(round(v, 2) for v in lo), "max": tuple(round(v, 2) for v in hi),
                  "children": {o.name: tuple(round(v, 3) for v in o.location) for o in objs if o.parent is not None}}
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    result = OUT
