"""a1_tier3boss_lib — helpers shared by the A1 roster's tier 3 enemies and bosses (docs/tracks/A1-roster-tier3boss.md).

Everything here builds in the simple_kit style (flat faces, colour per face, chamfered sections). Pattern: build the
character at ox = 0 (normal chibi size), then finish(parts, scale, ox) bakes every transform into the meshes, scales
the whole figure about its feet (bosses are built at real size: 1.6-2.3x the Nobody) and moves it to ox for review().
Import from a character script with:
    import os, sys; sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
    import a1_tier3boss_lib as L
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K
import simple_kit as A

ROT_Z_TO_NEG_Y = Matrix.Rotation(math.radians(90), 4, "X")   # a loft built along +Z, turned to point at the front
ROT_Z_TO_POS_Y = Matrix.Rotation(math.radians(-90), 4, "X")


def bake(objs):
    """Bakes each mesh's world transform into its vertices; unparents it and leaves it at the identity."""
    bpy.context.view_layer.update()
    for o in objs:
        if o.type != "MESH":
            continue
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
        o.data.transform(mw)
        if mw.determinant() < 0:
            o.data.flip_normals()
        A.flat(o)
    bpy.context.view_layer.update()
    return objs


def finish(objs, scale=1.0, ox=0.0):
    """bake(), then scale about the feet (the origin) and move to x = ox."""
    bake(objs)
    m = Matrix.Translation((ox, 0, 0)) @ Matrix.Scale(scale, 4)
    for o in objs:
        if o.type == "MESH":
            o.data.transform(m)
    return objs


def xform(objs, m):
    """Applies matrix m to the meshes (baked first)."""
    bake(objs)
    for o in objs:
        o.data.transform(m)
    return objs


def shear_forward(objs, z0, k, rigid=None):
    """A hunch: vertices above z0 move forward (-Y) by (z - z0) * k. `rigid` {obj: joint_z}: those move as one piece
    by their joint's offset (arms, held weapons), so they don't skew. Call on baked meshes."""
    rigid = rigid or {}
    bake(objs)
    for o in objs:
        me = o.data
        if o in rigid:
            dz = max(0.0, rigid[o] - z0) * k
            for v in me.vertices:
                v.co.y -= dz
        else:
            for v in me.vertices:
                if v.co.z > z0:
                    v.co.y -= (v.co.z - z0) * k
    return objs


def swing(objs, pivot, angle, axis="X"):
    """Rotates meshes (vertex data in world space: lofts, or baked) about a joint, e.g. an arm about its shoulder:
    axis X with a negative angle swings the hand forward (-Y)."""
    m = Matrix.Translation(Vector(pivot)) @ Matrix.Rotation(angle, 4, axis) @ Matrix.Translation(-Vector(pivot))
    bake(objs)
    for o in objs:
        o.data.transform(m)
    return objs


def get(parts, name):
    for o in parts:
        if o.name == name or o.name.split(".")[0] == name:
            return o
    return None


def drop(parts, *names):
    """Removes parts by name (from the list and the scene)."""
    for n in names:
        o = get(parts, n)
        if o is not None:
            parts.remove(o)
            bpy.data.objects.remove(o, do_unlink=True)
    return parts


def recolour(obj, swatch):
    K.set_swatch(obj, swatch)
    return obj


# --------------------------------------------------------------------------- pieces

def _aim(d):
    """A rotation taking +Z onto d (a flat face kept to the front where it can be)."""
    d = Vector(d).normalized()
    return d.to_track_quat("Z", "Y" if abs(d.y) < 0.9 else "X").to_matrix().to_4x4()


def rod(name, p0, p1, r, swatch, sides=4, taper=1.0):
    """A straight prism (square by default, a flat face to the front) from p0 to p1, flat-shaded."""
    p0, p1 = Vector(p0), Vector(p1)
    ln = (p1 - p0).length
    pts = A.ngon(sides, r, phase=math.pi / sides)
    o = A.loft(name, [(0.0, pts), (ln, [(x * taper, y * taper) for x, y in pts])], swatch)
    o.data.transform(Matrix.Translation(p0) @ _aim(p1 - p0))
    return o


def tip(name, base, length, r, swatch, sides=4, direction=(0, 0, 1)):
    """A pyramid point (a spear head) from `base` along `direction`."""
    d = Vector(direction).normalized()
    pts = A.ngon(sides, r, phase=math.pi / sides)
    bm = bmesh.new()
    ring = [bm.verts.new((x, y, 0.0)) for x, y in pts]
    apex = bm.verts.new((0, 0, length))
    for k in range(sides):
        bm.faces.new((ring[k], ring[(k + 1) % sides], apex))
    bm.faces.new(list(reversed(ring)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = A.flat(K.object_from_bmesh(name, bm, swatch=swatch))
    o.data.transform(Matrix.Translation(Vector(base)) @ _aim(d))
    return o


def leaf_blade(name, base, length, width, swatch, edge=None, thickness=0.04):
    """A flat leaf-shaped spear blade standing up from `base`, its flat to the front (XZ plane)."""
    pts = [(0.0, 0.0), (width * 0.5, length * 0.28), (width * 0.36, length * 0.66), (0.0, length),
           (-width * 0.36, length * 0.66), (-width * 0.5, length * 0.28)]
    o = A.flat(K.extrude_poly(name, pts, thickness=thickness, plane="XZ", swatch=swatch))
    if edge:
        K.set_swatch_where(o, edge, lambda c, n: abs(n.y) < 0.5)
    o.data.transform(Matrix.Translation(Vector(base)))
    return o


def scutum(name, width, height, radius, depth=0.05, segs=4, rim=0.06, swatch="purple", rim_swatch="gold",
           back_swatch="purple_d"):
    """A curved tower shield centred at the origin, convex to the front (-Y): a thick arc lofted up Z, with a rim
    band top and bottom and down both edges (narrow edge columns), a darker inside. Returns (obj, front_y): front_y
    is the y of the front face on the centre line (for the spine and boss)."""
    half = width / 2
    ang = 2 * math.asin(min(0.99, half / radius))
    d_rim = 2 * math.asin(min(0.99, rim / radius))          # the edge columns' angular width
    angles = [-ang / 2] + [-ang / 2 + d_rim + (ang - 2 * d_rim) * k / segs for k in range(segs + 1)] + [ang / 2]
    outer, inner = [], []
    for a in angles:
        x = radius * math.sin(a)
        y = -(radius * math.cos(a) - radius * math.cos(ang / 2))
        outer.append((x, y - depth / 2))
        inner.append((x, y + depth / 2))
    mean = sum(y for _, y in outer + inner) / (2 * len(outer))
    pts = [(x, y - mean) for x, y in outer + list(reversed(inner))]
    hz = height / 2
    rings = [(-hz, pts), (-hz + rim, pts), (hz - rim, pts), (hz, pts)]
    o = A.loft(name, rings, swatch)
    edge = radius * math.sin(ang / 2 - d_rim) + 1e-4
    K.set_swatch_where(o, back_swatch, lambda c, n: n.y > 0.3)
    K.set_swatch_where(o, rim_swatch, lambda c, n: n.y < 0.3 and (abs(c.z) > hz - rim - 1e-4 or abs(c.x) > edge)
                       or abs(n.z) > 0.9)
    front_y = -(radius - radius * math.cos(ang / 2)) - depth / 2 - mean
    return o, front_y


def boss_knob(name, r, h, loc, swatch, sides=4):
    """A shield boss: a low pyramid pointing at the front (-Y), its base centre at loc (a diamond with 4 sides)."""
    return tip(name, loc, h, r, swatch, sides=sides, direction=(0, -1, 0))


def helmet_head(a, skin="skin_light", shade="skin_mid", top=0.66, name="Head"):
    """The kit's head cut flat at hz(top) (the part under a helmet; skip "Head" in humanoid() and add this):
    the same lower rings, ~30 fewer triangles."""
    HW, hz = a["head_w"], a["hz"]
    ry = HW * 0.93
    o = A.loft(name, [(hz(0.04), A.oct8(HW * 0.46, ry * 0.44, 0.66, 0, -ry * 0.18)),
                      (hz(0.17), A.oct8(HW * 0.82, ry * 0.8, 0.64, 0, -ry * 0.06)),
                      (hz(0.42), A.oct8(HW, ry, 0.62)), (hz(top), A.oct8(HW, ry, 0.62))], skin)
    A.paint(o, shade, lambda c, n: c.z < hz(0.2) and n.y < -0.3)
    return o


def helmet(a, swatch, rim_swatch=None, low=0.62, top=1.07, s=1.08, front=1.0, name="Helm"):
    """A chamfered dome over the kit head (from hz(low) up to hz(top)), `s` times the head's width; the lowest band
    in rim_swatch. front < 1 pulls the front in (a cap), > 1 out (a visor brow)."""
    HW, ry, hz = a["head_w"], a["head_back_y"], a["hz"]
    rings = [(hz(low), A.oct8(HW * s, ry * s, 0.6, front=front)),
             (hz(low + 0.1), A.oct8(HW * (s + 0.02), ry * (s + 0.02), 0.6, front=front)),
             (hz(0.88), A.oct8(HW * (s - 0.12), ry * (s - 0.12), 0.6)),
             (hz(top), A.oct8(HW * 0.46, ry * 0.46, 0.6))]
    o = A.loft(name, rings, swatch, cap_bottom=False)
    if rim_swatch:
        A.paint(o, rim_swatch, lambda c, n: c.z < hz(low + 0.1))
    return o


def cheek_guards(a, swatch, low=0.18, high=0.66, depth=0.6, name="Cheek"):
    """Two flat plates down the sides of the head (helmets with cheek guards)."""
    HW, ry, hz = a["head_w"], a["head_back_y"], a["hz"]
    out = []
    for s, sx in (("L", 1), ("R", -1)):
        out.append(A.box(name + s, (0.045, ry * depth * 2, hz(high) - hz(low)), (sx * HW * 1.04, -ry * 0.18, hz(low)),
                         swatch, taper=(1.0, 1.15)))
    return out


def plume(name, base, height, w0, d0, w1, d1, swatch, lean=0.0):
    """An upright brush crest (plumes, horsehair): a tapered box from `base`, w = x size, d = y size, top flaring."""
    o = A.box(name, (w0, d0, height), base, swatch, taper=(w1 / w0, d1 / d0))
    if lean:
        o.rotation_euler = (lean, 0, 0)
    return o


def fin_crest(name, base, length, height, thickness, swatch, front_h=None, back_h=None):
    """A front-to-back fin crest (a horsehair crest seen from the side): an outline in YZ, extruded along X."""
    fh = height if front_h is None else front_h
    bh = height * 0.7 if back_h is None else back_h
    half = length / 2
    pts = [(-half, 0.0), (half, 0.0), (half * 1.05, bh), (half * 0.4, height), (-half * 0.4, height * 1.02),
           (-half * 1.05, fh)]
    o = A.flat(K.extrude_poly(name, pts, thickness=thickness, plane="YZ", swatch=swatch))
    o.data.transform(Matrix.Translation(Vector(base)))
    return o


def band(name, z0, z1, rx, ry, swatch, cx=0.0, cy=0.0, k=0.62):
    """An open chamfered ring (a belt, a collar, a sash) from z0 to z1."""
    return A.loft(name, [(z0, A.oct8(rx, ry, k, cx, cy)), (z1, A.oct8(rx, ry, k, cx, cy))], swatch, 0.0, False, False)


def cape(name, top_z, bottom_z, top_w, bottom_w, back_y, swatch, inner=None, flare=0.12, thickness=0.03, segs=2):
    """A cloak hanging down the back: a slab from the shoulders (top_w wide at y=back_y) to bottom_z (bottom_w wide,
    `flare` further back), slightly curved round the body (segs columns)."""
    bm = bmesh.new()
    rows = []
    for z, w, dy in ((top_z, top_w, 0.0), (bottom_z, bottom_w, flare)):
        row = []
        for k in range(segs + 1):
            t = -1 + 2 * k / segs
            curve = (1 - t * t) * 0.04 * w
            for d in (0.0, thickness):
                row.append(bm.verts.new((t * w / 2, back_y + dy + curve + d, z)))
        rows.append(row)
    n = segs + 1

    def v(r, k, d):
        return rows[r][k * 2 + d]
    for k in range(segs):
        bm.faces.new((v(0, k, 1), v(0, k + 1, 1), v(1, k + 1, 1), v(1, k, 1)))      # outside (+Y)
        bm.faces.new((v(0, k + 1, 0), v(0, k, 0), v(1, k, 0), v(1, k + 1, 0)))      # inside
        bm.faces.new((v(0, k, 0), v(0, k + 1, 0), v(0, k + 1, 1), v(0, k, 1)))      # top edge
        bm.faces.new((v(1, k + 1, 0), v(1, k, 0), v(1, k, 1), v(1, k + 1, 1)))      # bottom edge
    for k in (0, n - 1):
        bm.faces.new((v(0, k, 0), v(0, k, 1), v(1, k, 1), v(1, k, 0)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = A.flat(K.object_from_bmesh(name, bm, swatch=swatch))
    if inner:
        K.set_swatch_where(o, inner, lambda c, n: n.y < -0.5)
    return o


def mirror_y(obj):
    """Mirrors a (baked) mesh front to back (y -> -y), keeping its normals outward: a cape() turned into an apron."""
    bake([obj])
    obj.data.transform(Matrix.Scale(-1, 4, (0, 1, 0)))
    obj.data.flip_normals()
    return obj


def spiked_ball(name, centre, r, swatch, spike_swatch, spike=None, parts=None):
    """A chamfered iron ball with six pyramid spikes (a flail head, a mace). Returns [ball, spikes...]."""
    cx, cy, cz = centre
    ball = ring_rows(name, [(cz - r, r * 0.42, r * 0.42, cx, cy), (cz - r * 0.45, r * 0.95, r * 0.95, cx, cy),
                            (cz + r * 0.45, r * 0.95, r * 0.95, cx, cy), (cz + r, r * 0.42, r * 0.42, cx, cy)], swatch)
    out = [ball]
    ln = spike or r * 0.75
    for k, d in enumerate(((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))):
        base = (cx + d[0] * r * 0.85, cy + d[1] * r * 0.85, cz + d[2] * r * 0.85)
        out.append(tip("%sSpike%d" % (name, k), base, ln, r * 0.3, spike_swatch, direction=d))
    return out


def disc(name, centre, r, thickness, swatch, axis="X", sides=8):
    """A flat chamfered disc (a coil of rope or whip, a medallion) facing along `axis`."""
    pts = A.ngon(sides, r, phase=math.pi / sides)
    o = A.loft(name, [(-thickness / 2, pts), (thickness / 2, pts)], swatch)
    rot = {"X": Matrix.Rotation(math.radians(90), 4, "Y"), "Y": ROT_Z_TO_NEG_Y, "Z": Matrix.Identity(4)}[axis]
    o.data.transform(Matrix.Translation(Vector(centre)) @ rot)
    return o


def hoop(name, centre, r, thick, swatch, axis="Y", sides=8, rot_z=0.0):
    """A chamfered ring with a square section (a whip's coil, a key ring, a wreath) facing along `axis`."""
    bm = bmesh.new()
    sec = [(-thick, -thick), (thick, -thick), (thick, thick), (-thick, thick)]   # (radial, axial) offsets
    rings = []
    for k in range(sides):
        a = 2 * math.pi * (k + 0.5) / sides
        ca, sa = math.cos(a), math.sin(a)
        rings.append([bm.verts.new(((r + dr) * ca, (r + dr) * sa, dz)) for dr, dz in sec])
    for k in range(sides):
        r0, r1 = rings[k], rings[(k + 1) % sides]
        for j in range(4):
            bm.faces.new((r0[j], r0[(j + 1) % 4], r1[(j + 1) % 4], r1[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = A.flat(K.object_from_bmesh(name, bm, swatch=swatch))
    rot = {"X": Matrix.Rotation(math.radians(90), 4, "Y"), "Y": ROT_Z_TO_NEG_Y, "Z": Matrix.Identity(4)}[axis]
    o.data.transform(Matrix.Translation(Vector(centre)) @ Matrix.Rotation(rot_z, 4, "Z") @ rot)
    return o


def lean_humanoid(ox=0.0, oy=0.0, p=None, skin="skin_tan", skin_shade="skin_dark", loin="cloth_white",
                  foot="leather_d", cap=None, eyes="ink", name="B"):
    """A cheap kit body for extras (a litter's bearers): the simple_kit proportions and sections with fewer rings
    (2-ring limbs, block hands and feet, a 4-ring head, eyes only): ~270 tris instead of ~650. Built at (ox, oy).
    cap: a swatch painted over the head's top (a felt cap). Returns {"parts", "a"} like simple_kit.humanoid."""
    p = dict(A.CHIBI, **(p or {}))
    H, Sz, SX, N, HH, HW, TW = p["H"], p["S"], p["SX"], p["N"], p["HH"], p["HW"], p["TW"]
    LR, LX, AR, AL, HS, FL = p["LR"], p["LX"], p["AR"], p["AL"], p["HS"], p["FL"]
    parts = []
    T = Matrix.Translation((ox, oy, 0))

    def put(o):
        o.data.transform(T)
        parts.append(o)
        return o
    for s, sx in (("L", 1), ("R", -1)):
        x = LX * sx
        put(A.loft(name + "Leg" + s, [(0.07, A.hex6(LR * 0.66, LR * 0.72, x, 0.004)), (H + 0.02, A.hex6(LR, LR * 1.04, x))],
                   skin, cap_top=False))
        f = A.box(name + "Foot" + s, (0.12, FL * 0.85, 0.08), (x, -0.045, 0.0), foot, pivot=(0, 0, -1), taper=(0.85, 0.8))
        bake([f])
        put(f)
    waist = H + (Sz - H) * 0.32
    t = put(A.loft(name + "Torso", [(H - 0.02, A.oct8(TW * 0.95, TW * 0.7)), (waist, A.oct8(TW * 0.86, TW * 0.62)),
                                    (Sz + 0.03, A.oct8(TW * 1.06, TW * 0.62)), (N + 0.02, A.oct8(0.085, 0.075))], skin,
                   cap_top=False))
    if loin:
        put(A.loft(name + "Loin", [(H - 0.12, A.oct8(TW * 1.1, TW * 0.8)), (H + 0.05, A.oct8(TW * 0.99, TW * 0.72))],
                   loin, 0.0, False, False))
    wz = Sz - AL
    for s, sx in (("L", 1), ("R", -1)):
        x = (SX + 0.02) * sx
        put(A.loft(name + "Arm" + s, [(wz, A.hex6(AR * 0.74, AR * 0.72, x, -0.015)),
                                      (Sz + 0.03, A.hex6(AR * 1.0, AR * 0.98, x - 0.015 * sx))], skin, cap_bottom=False))
        fi = A.box(name + "Fist" + s, (HS * 1.7, HS * 1.8, HS * 1.6), (x, -0.02, wz - HS * 0.55), skin)
        bake([fi])
        put(fi)

    def hz(f):
        return N + HH * f
    ry = HW * 0.93
    hd = put(A.loft(name + "Head", [(hz(0.04), A.oct8(HW * 0.5, ry * 0.48, 0.66, 0, -ry * 0.14)),
                                    (hz(0.24), A.oct8(HW * 0.95, ry * 0.93, 0.64)), (hz(0.74), A.oct8(HW, ry, 0.62)),
                                    (hz(1.0), A.oct8(HW * 0.44, ry * 0.44, 0.6))], skin))
    A.paint(hd, skin_shade, lambda c, n: c.z < hz(0.2) and n.y < -0.3, ox)
    if cap:
        A.paint(hd, cap, lambda c, n: c.z > hz(0.76), ox)
    for s, x in (("L", HW * 0.38), ("R", -HW * 0.38)):
        e = A.box(name + "Eye" + s, (HW * 0.24, 0.014, HH * 0.075), (x, -ry - 0.002, hz(0.5)), eyes)
        bake([e])
        put(e)
    a = dict(p=p, hand_r=(ox - (SX + 0.02), oy - 0.02, wz - HS * 0.55), hand_l=(ox + (SX + 0.02), oy - 0.02, wz - HS * 0.55),
             shoulder_l=(ox + SX + 0.02, oy, Sz), shoulder_r=(ox - SX - 0.02, oy, Sz), hz=hz, head_w=HW,
             head_back_y=ry)
    return {"parts": parts, "a": a}


def ring_rows(name, rows, swatch, cap_bottom=True, cap_top=True):
    """A loft from (z, rx, ry, cx, cy[, k]) rows in oct8 sections (a shortcut for bodies)."""
    rings = [(r[0], A.oct8(r[1], r[2], r[5] if len(r) > 5 else 0.62, r[3] if len(r) > 3 else 0.0,
                           r[4] if len(r) > 4 else 0.0)) for r in rows]
    return A.loft(name, rings, swatch, 0.0, cap_bottom, cap_top)


def horizontal(obj, about_y=0.0):
    """Turns a loft built along +Z into one along -Y (the body of a quadruped, its head end at -Y)."""
    obj.data.transform(ROT_Z_TO_NEG_Y)
    return obj


def tris(objs):
    return K.tri_count([o for o in objs if o.type == "MESH"])
