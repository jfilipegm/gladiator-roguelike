"""Stone Amphitheater kit (Phase 5.8, docs/ARENAS.md section 3): palette family A3 (sandstone, travertine, marble).
Arena 3 is a D: a half-ellipse bowl of tiered stone seating (south) facing a straight stage wall (north), 120 m wide
and 88 m deep. The bowl (cavea) is ONE mesh generated along the curve, with gaps where the three tunnels go; the
stage wall is modular.

ARGS: part=terrain|structures|props. Every piece goes to Assets/Art/Models/Environment/. Origins at the base; wall-like
pieces face -Y (their front, toward the arena, arrives as Unity +Z). The D is NOT point-symmetric, so the generated
terrain is built in Unity coordinates and mapped through B(): the export's 180 deg turn puts it back. Seeded
randomness: re-runs are identical.

The D's numbers are shared with Assets/Editor/Setup/AmphitheaterLayout.cs: keep them in step.
"""
import importlib
import math
import random

import bmesh
import bpy
import gladiator_kit as K
from mathutils import Vector

importlib.reload(K)
PART = ARGS.get("part", "terrain")  # noqa: F821  (ARGS injected by Tools/blender_bridge.py)
OUT = {}

# ------------------------------------------------------------------------------------------------ the D (Unity XZ)
AX, ZN, ZC, BZ = 60.0, 44.0, 20.0, 64.0     # half width; stage wall line; where the curve starts; the curve's depth
TUNNELS = (math.pi / 2 - 0.75, math.pi / 2, math.pi / 2 + 0.75)  # curve parameters of the three tunnels
TUNNEL_HALF = 2.5                             # half the tunnel piece's width (m along the curve)
# the cavea's profile, (outward offset, height): podium, eight tiers of seats, the back wall, the outer facade
PODIUM, PODIUM_CAP, TIER_RISE, TIER_RUN, TIERS = 3.0, 0.6, 0.45, 0.8, 8
BACK_TOP, BACK_DEPTH = 8.4, 0.8


def B(x, z, h=0.0):
    """A Unity-space point (x, z, height) in Blender coordinates (the export turns the model 180 deg)."""
    return Vector((-x, -z, h))


def arc(t):
    """The curve at parameter t (0 = east end, pi/2 = the south apex, pi = west end): (point, outward normal)."""
    x, z = AX * math.cos(t), ZC - BZ * math.sin(t)
    n = Vector((math.cos(t) / AX, -math.sin(t) / BZ))
    n.normalize()
    return Vector((x, z)), n


def arc_speed(t):
    return math.hypot(AX * math.sin(t), BZ * math.cos(t))


def profile():
    """The cavea's closed cross-section: (outward offset, height, swatch of the face from this point to the next)."""
    p = [(0.0, -0.3, "sandstone_d"), (0.0, PODIUM - 0.4, "red_banner"), (0.0, PODIUM, "marble")]
    o, h = PODIUM_CAP, PODIUM
    p.append((o, h, "sandstone_d"))
    for k in range(TIERS):
        h += TIER_RISE
        p.append((o, h, "sandstone"))
        o += TIER_RUN
        p.append((o, h, "sandstone_d" if k < TIERS - 1 else "sandstone"))
    p.append((o, BACK_TOP, "marble"))
    p.append((o + BACK_DEPTH, BACK_TOP, "sandstone_d"))
    p.append((o + BACK_DEPTH, -0.3, None))  # the bottom: never seen
    return p


def cavea_path(arc_steps=96):
    """Samples (point, outward normal) along the cavea, split into sections at the tunnels' gaps: east side (stage line
    down to the curve), the curve, west side (back up to the stage line)."""
    sections, cur = [], []
    for i in range(7):  # east side: x = AX, z from ZN down to ZC
        cur.append((Vector((AX, ZN - (ZN - ZC) * i / 6)), Vector((1.0, 0.0))))
    gaps = [(t - TUNNEL_HALF / arc_speed(t), t + TUNNEL_HALF / arc_speed(t)) for t in TUNNELS]
    ts = [math.pi * i / arc_steps for i in range(1, arc_steps)]
    for a, b in gaps:
        ts += [a, b]
    ts.sort()
    for t in ts:
        inside_gap = any(a < t < b for a, b in gaps)
        if inside_gap:
            continue
        cur.append(arc(t))
        if any(abs(t - a) < 1e-9 for a, _ in gaps):  # a gap starts: close this section
            sections.append(cur)
            cur = []
    for i in range(7):  # west side
        cur.append((Vector((-AX, ZC + (ZN - ZC) * i / 6)), Vector((-1.0, 0.0))))
    sections.append(cur)
    return sections


def d_distance(x, z):
    """Distance inside the D's wall line (negative behind it); approximate on the curve."""
    d = ZN - z
    if z >= ZC:
        d = min(d, AX - abs(x))
    else:
        e = math.hypot(x / AX, (z - ZC) / BZ)
        d = min(d, (1.0 - e) * min(AX, BZ))
    return d


# ------------------------------------------------------------------------------------------------ terrain

def amphi_floor(name, seed=31):
    """Pale sand over the D in 2 m cells, clipped 1 m behind the wall line (under the podium and the stage)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=62, y_segments=62, size=62.0)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if abs(f.calc_center_median().y) > ZN + 2.0], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # the D + 1 m, as Unity-space edges: north line, the sides, then the curve; clip each in Blender space
    edges = [((-AX - 1, ZN + 1), (AX + 1, ZN + 1)), ((AX + 1, ZN + 1), (AX + 1, ZC)), ((-AX - 1, ZC), (-AX - 1, ZN + 1))]
    steps = 48
    for i in range(steps):
        t0, t1 = math.pi * i / steps, math.pi * (i + 1) / steps
        p0 = Vector(((AX + 1) * math.cos(t0), ZC - (BZ + 1) * math.sin(t0)))
        p1 = Vector(((AX + 1) * math.cos(t1), ZC - (BZ + 1) * math.sin(t1)))
        edges.append((tuple(p0), tuple(p1)))
    for (ax, az), (bx, bz) in edges:
        mid = B((ax + bx) / 2, (az + bz) / 2)
        # outward normal in Unity space (the D runs counter-clockwise here), mapped like the points
        ex, ez = bx - ax, bz - az
        out_u = Vector((ez, -ex)).normalized()
        if out_u.dot(Vector(((ax + bx) / 2, (az + bz) / 2 - 0.0))) < 0:  # make sure it points away from the middle
            out_u = -out_u
        plane_no = Vector((-out_u.x, -out_u.y, 0.0))
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=mid, plane_no=plane_no, clear_outer=True)
    for v in bm.verts:
        d = d_distance(-v.co.x, -v.co.y)
        v.co.z = rng.uniform(-0.03, 0.03) + 0.1 * max(0.0, min(1.3, (3.0 - d) / 4.0))
    obj = K.object_from_bmesh(name, bm, swatch="sand_l")
    K.set_swatch_where(obj, "sand", lambda c, n: rng.random() < 0.18)  # pale and low-contrast: the stone is the show
    K.set_swatch_where(obj, "sand_d", lambda c, n: d_distance(-c.x, -c.y) < 3.0 and rng.random() < 0.5)
    return obj


def amphi_cavea(name):
    """The seating bowl: a 3 m podium (a painted red band under its marble cap) and eight tiers of seats rising
    behind it, then the back wall and the outer facade, swept along the curve in sections between the tunnels."""
    prof = profile()
    bm = bmesh.new()
    for sec in cavea_path():
        rings = []
        for p, n in sec:
            rings.append([bm.verts.new(B(p.x + n.x * o, p.y + n.y * o, h)) for o, h, _ in prof])
        for a, b in zip(rings, rings[1:]):
            for j in range(len(prof)):
                k = (j + 1) % len(prof)
                f = bm.faces.new((a[j], a[k], b[k], b[j]))
                f.material_index = j  # swatch tag, resolved below
        for ring in (rings[0], rings[-1]):  # the section's end caps
            cap = bm.faces.new(ring)
            cap.material_index = 255
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    caps = [f for f in bm.faces if f.material_index == 255]
    bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="EAR_CLIP")  # concave (the steps)
    obj = K.object_from_bmesh(name, bm, swatch="sandstone")
    # the face tags survive to_mesh as material indices: one swatch per profile face, caps in the darker stone
    uvs = K.palette()
    uvl = obj.data.uv_layers.active.data
    for poly in obj.data.polygons:
        tag = poly.material_index
        sw = "sandstone_d" if tag >= len(prof) else (prof[tag][2] or "sandstone_d")
        u, v = uvs[sw]
        for li in poly.loop_indices:
            uvl[li].uv = (u, v)
        poly.material_index = 0
    return obj


def amphi_rim(name, seed=37):
    """The ground outside the building, under everything (0.15 m below the floor): trodden dirt near the walls, dry
    grass out to a circle past the fog."""
    rng = random.Random(seed)
    bm = bmesh.new()
    radii = (0.0, 75.0, 110.0, 160.0, 260.0)
    seg = 48
    rings = []
    for k, r in enumerate(radii):
        if r == 0.0:
            rings.append([bm.verts.new((0.0, 0.0, -0.15))])
            continue
        rings.append([bm.verts.new((math.cos(2 * math.pi * i / seg) * r, math.sin(2 * math.pi * i / seg) * r,
                                    -0.15 - (0.0 if k < 2 else rng.uniform(0.2, 0.9) * (k - 1)))) for i in range(seg)])
    for i in range(seg):
        bm.faces.new((rings[0][0], rings[1][i], rings[1][(i + 1) % seg]))
    for k in range(1, len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((rings[k][i], rings[k + 1][i], rings[k + 1][j], rings[k][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    obj = K.object_from_bmesh(name, bm, swatch="grass_dry")
    K.set_swatch_where(obj, "dirt_light", lambda c, n: c.length < 70.0)
    K.set_swatch_where(obj, "grass_dry_d", lambda c, n: c.length > 70.0 and rng.random() < 0.35)
    return obj


def pulpitum(name, length=40.0, depth=5.5, height=1.2, seed=41):
    """The stage platform along the stage wall: an ashlar front with pilasters, a marble top. Origin at the base
    centre; its front (the long side the stairs come up) faces -Y."""
    rng = random.Random(seed)
    parts = [K.box(name, size=(length, depth, height), pivot=(0, 0, -1), bevel=0.03, swatch="sandstone")]
    parts.append(K.box("Top", size=(length + 0.2, depth + 0.2, 0.12), location=(0, 0, height - 0.12), pivot=(0, 0, -1),
                       swatch="marble"))
    n = int(length / 4.0)
    for i in range(n + 1):
        x = -length / 2 + i * length / n
        parts.append(K.box("Pilaster%d" % i, size=(0.5, 0.16, height - 0.15), location=(x, -depth / 2 - 0.06, 0),
                           pivot=(0, 0, -1), swatch="travertine_d"))
    for i in range(n):
        x = -length / 2 + (i + 0.5) * length / n
        parts.append(K.box("Panel%d" % i, size=(length / n - 0.9, 0.05, height * 0.45), location=(x, -depth / 2 - 0.02, 0.3),
                           pivot=(0, 0, -1), swatch=rng.choice(("sandstone_d", "red_banner_d"))))
    K.join(parts, name)
    return parts[0]


# ------------------------------------------------------------------------------------------------ structures

def stage_section(name, width=4.6, height=7.5, depth=1.6):
    """A section of the stage wall (scaenae frons): a plinth, a travertine wall with two niches, an engaged column in
    front, a marble cornice. Front (-Y) face of the wall at y = 0; the plinth, cornice and column stand proud of it."""
    parts = [K.box(name, size=(width, depth + 0.3, 1.2), location=(0, depth / 2 - 0.15, 0), pivot=(0, 0, -1),
                   swatch="sandstone_d")]
    parts.append(K.box("Body", size=(width, depth, height - 0.5), location=(0, depth / 2, 1.2), pivot=(0, 0, -1),
                       swatch="travertine"))
    parts.append(K.box("Cornice", size=(width, depth + 0.35, 0.35), location=(0, depth / 2 - 0.175, height - 0.5),
                       pivot=(0, 0, -1), swatch="marble"))
    parts.append(K.box("Attic", size=(width, depth - 0.2, 0.25), location=(0, depth / 2 + 0.1, height - 0.15),
                       pivot=(0, 0, -1), swatch="travertine_d"))
    for sx in (-1, 1):
        parts.append(K.box("Niche", size=(1.1, 0.06, 2.4), location=(sx * 1.35, -0.02, 2.6), pivot=(0, 0, -1),
                           swatch="travertine_d"))
        parts.append(K.box("NicheTop", size=(1.3, 0.12, 0.18), location=(sx * 1.35, -0.05, 5.0), pivot=(0, 0, -1),
                           swatch="marble"))
    parts.append(K.box("ColBase", size=(0.95, 0.95, 1.4), location=(0, -0.55, 0), pivot=(0, 0, -1), swatch="marble"))
    parts.append(K.cylinder("Shaft", radius=0.32, depth=height - 0.5 - 1.4 - 0.35, verts=10, location=(0, -0.55, 1.4),
                            pivot=(0, 0, -1), taper=0.9, swatch="travertine_d"))
    parts.append(K.box("Capital", size=(0.85, 0.85, 0.35), location=(0, -0.55, height - 0.85), pivot=(0, 0, -1),
                       swatch="marble"))
    K.join(parts, name)
    return parts[0]


def cage_gate(name, width=4.6, opening=3.2, open_h=3.0, height=7.5, depth=1.6):
    """A beast pen in the stage wall: the wall around an opening, a dark pen behind it, and an iron portcullis
    ("Bars") that BeastCages lifts. A hazard-red band over the opening: something dangerous comes out of here. Front
    (-Y) face at y = 0."""
    root = K.empty(name)
    side = (width - opening) / 2
    frame = [K.box(name + "_Wall", size=(side, depth, height - 0.5), location=(-(opening + side) / 2, depth / 2, 0),
                   pivot=(0, 0, -1), swatch="travertine", parent=root)]
    frame.append(K.box("WallR", size=(side, depth, height - 0.5), location=((opening + side) / 2, depth / 2, 0),
                       pivot=(0, 0, -1), swatch="travertine"))
    frame.append(K.box("Over", size=(opening, depth, height - 0.5 - open_h), location=(0, depth / 2, open_h),
                       pivot=(0, 0, -1), swatch="travertine"))
    frame.append(K.box("Lintel", size=(opening + 0.6, 0.3, 0.45), location=(0, -0.15, open_h), pivot=(0, 0, -1),
                       swatch="marble"))
    frame.append(K.box("Band", size=(opening + 0.6, 0.06, 0.16), location=(0, -0.33, open_h + 0.15), pivot=(0, 0, -1),
                       swatch="hazard_red"))
    frame.append(K.box("Cornice", size=(width, depth + 0.35, 0.35), location=(0, depth / 2 - 0.175, height - 0.5),
                       pivot=(0, 0, -1), swatch="marble"))
    pen = K.box("Pen", size=(opening, 3.0, open_h), location=(0, depth + 1.5, 0), pivot=(0, 0, -1), swatch="shadow")
    bmp = bmesh.new()
    bmp.from_mesh(pen.data)
    for f in bmp.faces:
        f.normal_flip()  # seen from inside
    bmp.to_mesh(pen.data)
    bmp.free()
    frame.append(pen)
    K.join(frame, name + "_Wall")
    bars = [K.box("Bars", size=(opening - 0.1, 0.08, 0.12), location=(0, 0.1, 0.15), pivot=(0, 0, -1), swatch="iron_d",
                  parent=root)]
    for i in range(9):
        x = -opening / 2 + 0.15 + i * (opening - 0.3) / 8
        bars.append(K.box("Bar%d" % i, size=(0.09, 0.09, open_h - 0.05), location=(x, 0.1, 0.0), pivot=(0, 0, -1),
                          swatch="iron"))
    for z in (1.1, 2.2):
        bars.append(K.box("Rail", size=(opening - 0.1, 0.1, 0.1), location=(0, 0.1, z), pivot=(0, 0, -1), swatch="iron_d"))
    K.join(bars, "Bars")
    return root


def great_gate(name, opening=3.4, open_h=5.0, height=7.5, depth=1.6):
    """The boss's gate in the middle of the stage wall: marble posts, a pediment, bronze double doors (DoorL / DoorR
    hinged at the posts, opened by yawing, so ArenaGates drives it unchanged). Front (-Y) face at y = 0."""
    root = K.empty(name)
    post = (4.6 - opening) / 2
    frame = [K.box(name + "_Frame", size=(post, depth + 0.2, open_h), location=(-(opening + post) / 2, depth / 2 - 0.1, 0),
                   pivot=(0, 0, -1), swatch="marble", parent=root)]
    frame.append(K.box("PostR", size=(post, depth + 0.2, open_h), location=((opening + post) / 2, depth / 2 - 0.1, 0),
                       pivot=(0, 0, -1), swatch="marble"))
    frame.append(K.box("Lintel", size=(4.6, depth + 0.4, 0.6), location=(0, depth / 2 - 0.2, open_h), pivot=(0, 0, -1),
                       swatch="travertine_d"))
    frame.append(K.box("Over", size=(4.6, depth, height - 0.5 - open_h - 0.6), location=(0, depth / 2, open_h + 0.6),
                       pivot=(0, 0, -1), swatch="travertine"))
    frame.append(K.box("Cornice", size=(4.6, depth + 0.35, 0.35), location=(0, depth / 2 - 0.175, height - 0.5),
                       pivot=(0, 0, -1), swatch="marble"))
    frame.append(K.extrude_poly("Pediment", [(-2.6, 0.0), (2.6, 0.0), (0.0, 1.3)], 0.5, plane="XZ",
                                location=(0, 0.0, height - 0.15), swatch="marble"))
    frame.append(K.box("Shield", size=(0.8, 0.1, 0.8), location=(0, -0.45, open_h + 0.65), pivot=(0, 0, -1), swatch="gold"))
    dark = K.box("Dark", size=(opening, 0.1, open_h), location=(0, depth + 0.6, 0), pivot=(0, 0, -1), swatch="shadow")
    frame.append(dark)
    K.join(frame, name + "_Frame")
    leaf_w = opening / 2 - 0.02
    for side, sx in (("L", -1), ("R", 1)):
        hinge = (sx * opening / 2, 0.15, 0)
        leaf = [K.box("Leaf" + side, size=(leaf_w, 0.12, open_h - 0.1), location=(hinge[0] - sx * leaf_w / 2, hinge[1], 0.05),
                      pivot=(0, 0, -1), swatch="bronze_d")]
        for z in (0.6, open_h * 0.5, open_h - 0.7):
            leaf.append(K.box("Band", size=(leaf_w, 0.16, 0.16), location=(hinge[0] - sx * leaf_w / 2, hinge[1], z),
                              pivot=(0, 0, -1), swatch="bronze_l"))
        leaf.append(K.box("Boss", size=(0.3, 0.2, 0.3), location=(hinge[0] - sx * leaf_w * 0.75, hinge[1] - 0.04, open_h * 0.5 - 0.15),
                          pivot=(0, 0, -1), swatch="gold"))
        h = K.box("Door" + side, size=(0.1, 0.14, open_h - 0.15), location=hinge, pivot=(-sx, 0, -1), swatch="bronze_d",
                  parent=root)
        K.join([h] + leaf, "Door" + side)
    return root


def tunnel(name, width=5.0, opening=3.2, open_h=2.6):
    """A vomitorium: a straight 5 m chunk of the cavea (same profile, so it carries the seats over) with an arched
    passage through the podium, a dark tunnel behind and a wooden double door (DoorL / DoorR, hinged)."""
    root = K.empty(name)
    prof = profile()
    parts = []

    def slab(x0, x1, clip=None):
        """The profile extruded from x0 to x1 (Blender X); clip: drop everything below this height."""
        pts = [(o, h) for o, h, _ in prof]
        if clip is not None:
            pts = [(0.0, clip)] + [(o, h) for o, h in pts if h > clip] + [(pts[-1][0], clip)]
        bm = bmesh.new()
        ring0 = [bm.verts.new((x0, o, h)) for o, h in pts]
        ring1 = [bm.verts.new((x1, o, h)) for o, h in pts]
        for j in range(len(pts)):
            k = (j + 1) % len(pts)
            bm.faces.new((ring0[j], ring0[k], ring1[k], ring1[j]))
        bm.faces.new(ring0)
        bm.faces.new(ring1)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], ngon_method="EAR_CLIP")
        return bm

    for i, (x0, x1, clip) in enumerate(((-width / 2, -opening / 2, None), (opening / 2, width / 2, None),
                                        (-opening / 2, opening / 2, open_h))):
        o = K.object_from_bmesh(name + "_Seats" if i == 0 else "Seats%d" % i, slab(x0, x1, clip),
                                parent=root if i == 0 else None, swatch="sandstone")
        K.set_swatch_where(o, "sandstone_d", lambda c, n: abs(n.z) < 0.5)
        K.set_swatch_where(o, "marble", lambda c, n: n.z > 0.5 and abs(c.z - PODIUM) < 0.05)
        parts.append(o)
    parts.append(K.box("Lintel", size=(opening + 0.8, 0.25, 0.4), location=(0, -0.1, open_h - 0.05), pivot=(0, 0, -1),
                       swatch="marble"))
    parts.append(K.box("Keystone", size=(0.5, 0.3, 0.55), location=(0, -0.15, open_h - 0.1), pivot=(0, 0, -1),
                       swatch="travertine_d"))
    dark = K.box("Tunnel", size=(opening, 7.0, open_h), location=(0, 3.6, 0), pivot=(0, 0, -1), swatch="shadow")
    bmt = bmesh.new()
    bmt.from_mesh(dark.data)
    for f in bmt.faces:
        f.normal_flip()
    bmt.to_mesh(dark.data)
    bmt.free()
    parts.append(dark)
    K.join(parts, name + "_Seats")
    leaf_w = opening / 2 - 0.02
    for side, sx in (("L", -1), ("R", 1)):
        hinge = (sx * opening / 2, 0.12, 0)
        planks = []
        for i in range(4):
            x = hinge[0] - sx * (i + 0.5) * leaf_w / 4
            planks.append(K.box("Door%s%d" % (side, i), size=(leaf_w / 4 - 0.03, 0.1, open_h - 0.15),
                                location=(x, hinge[1], 0.05), pivot=(0, 0, -1), swatch="wood" if i % 2 else "wood_dark"))
        for z in (0.4, open_h * 0.5, open_h - 0.5):
            planks.append(K.box("Band", size=(leaf_w, 0.13, 0.14), location=(hinge[0] - sx * leaf_w / 2, hinge[1], z),
                                pivot=(0, 0, -1), swatch="iron_d"))
        h = K.box("Door" + side, size=(0.08, 0.12, open_h - 0.2), location=hinge, pivot=(-sx, 0, -1), swatch="iron_d",
                  parent=root)
        K.join([h] + planks, "Door" + side)
    return root


def amphi_column(name, height=6.6, radius=0.6):
    """A tall portico column (a line-of-sight blocker): marble plinth and capital, a travertine shaft in four drums.
    Origin at the base centre; ~1.7 m footprint."""
    parts = [K.box(name, size=(1.7, 1.7, 0.45), pivot=(0, 0, -1), bevel=0.05, swatch="marble"),
             K.cylinder("Torus", radius=radius + 0.14, depth=0.2, verts=12, location=(0, 0, 0.45), pivot=(0, 0, -1),
                        swatch="marble_shadow")]
    z, drum = 0.65, (height - 0.65 - 0.75) / 4
    for i in range(4):
        parts.append(K.cylinder("Drum%d" % i, radius=radius - 0.02 * i, depth=drum - 0.04, verts=12, location=(0, 0, z),
                                pivot=(0, 0, -1), taper=0.98, swatch="travertine" if i % 2 == 0 else "sandstone"))
        z += drum
    parts.append(K.cylinder("Echinus", radius=radius - 0.06, depth=0.3, verts=12, location=(0, 0, z), pivot=(0, 0, -1),
                            taper=1.35, swatch="marble"))
    parts.append(K.box("Abacus", size=(1.7, 1.7, 0.45), location=(0, 0, z + 0.3), pivot=(0, 0, -1), bevel=0.05,
                       swatch="marble"))
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ props

def statue(name):
    """A bronze gladiator on a marble plinth, sword raised: a line-of-sight blocker and the crowd's hero. Origin at
    the base centre; faces -Y."""
    parts = [K.box(name, size=(1.6, 1.6, 1.3), pivot=(0, 0, -1), bevel=0.05, swatch="marble"),
             K.box("Cap", size=(1.8, 1.8, 0.18), location=(0, 0, 1.3), pivot=(0, 0, -1), swatch="marble_shadow")]
    z0 = 1.48
    for sx in (-1, 1):
        parts.append(K.box("Leg", size=(0.32, 0.36, 0.95), location=(sx * 0.22, 0, z0), pivot=(0, 0, -1), swatch="bronze"))
    parts.append(K.box("Kilt", size=(0.8, 0.5, 0.35), location=(0, 0, z0 + 0.8), pivot=(0, 0, -1), swatch="bronze_d"))
    parts.append(K.box("Torso", size=(0.85, 0.5, 0.8), location=(0, 0, z0 + 1.1), pivot=(0, 0, -1), taper=(1.0, 1.15),
                       swatch="bronze"))
    parts.append(K.ico("Head", radius=0.32, subdiv=1, location=(0, 0, z0 + 2.15), swatch="bronze_l"))
    parts.append(K.box("Crest", size=(0.1, 0.6, 0.35), location=(0, 0.05, z0 + 2.4), pivot=(0, 0, -1), swatch="bronze_d"))
    parts.append(K.box("ArmR", size=(0.24, 0.24, 0.9), location=(0.55, 0, z0 + 1.7), pivot=(0, 0, -1), swatch="bronze"))
    parts.append(K.box("Sword", size=(0.1, 0.06, 1.1), location=(0.55, 0, z0 + 2.55), pivot=(0, 0, -1), swatch="bronze_l"))
    parts.append(K.box("ArmL", size=(0.24, 0.24, 0.7), location=(-0.55, -0.1, z0 + 1.1), pivot=(0, 0, -1), swatch="bronze"))
    parts.append(K.box("Shield", size=(0.14, 0.9, 1.2), location=(-0.72, -0.15, z0 + 0.85), pivot=(0, 0, -1),
                       swatch="bronze_d"))
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ build

K.clear_scene()
if PART == "terrain":
    items = {
        "SM_AmphiFloor": amphi_floor("SM_AmphiFloor"),
        "SM_AmphiCavea": amphi_cavea("SM_AmphiCavea"),
        "SM_AmphiRim": amphi_rim("SM_AmphiRim"),
        "SM_Pulpitum": pulpitum("SM_Pulpitum"),
    }
elif PART == "structures":
    items = {
        "SM_AmphiStage_4m": stage_section("SM_AmphiStage_4m"),
        "SM_CageGate": cage_gate("SM_CageGate"),
        "SM_GreatGate": great_gate("SM_GreatGate"),
        "SM_AmphiTunnel": tunnel("SM_AmphiTunnel"),
        "SM_AmphiColumn": amphi_column("SM_AmphiColumn"),
    }
else:
    items = {"PR_Statue": statue("PR_Statue")}
review = ARGS.get("review")  # noqa: F821
for n, o in items.items():
    if review == n:
        OUT["review"] = K.review_render(o, n.lower(), size=420)
    OUT[n] = K.export_fbx(o, "Environment/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o) or [o])
OUT["blend"] = K.save_blend("amphitheater_" + PART)
result = OUT
