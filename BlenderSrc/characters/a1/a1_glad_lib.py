"""a1_glad_lib — small shared helpers for the A1 roster's gladiator scripts (a1_<tag>.py), on top of simple_kit.

simple_kit stays the style's kit (and is shared by other workers, so it isn't edited); these are only the shapes the
gladiators needed beyond it, all flat-shaded, built in world space at x offset `ox`, front = -Y, left = +X:
  * tube()      a tapered tube along a polyline (horns, tails, shafts, chains' runs, staffs);
  * panel()     an outline (x, z) extruded along Y and tilted about X (capes, tabards, aprons, loincloths, banners);
  * blade()     an outline (y, z) extruded along X, under a frame at the hand (swords, cleavers, spear heads);
  * spin()      rotate a mesh's data about its own origin (for pieces built axis-aligned and then turned).
Import from a character script with `sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))`.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import gladiator_kit as K
import simple_kit as A


def spin(obj, rx=0.0, ry=0.0, rz=0.0):
    """Rotates the mesh data (degrees, XYZ order) about the object's origin; the object's transform is untouched."""
    m = Matrix.Rotation(math.radians(rz), 4, "Z") @ Matrix.Rotation(math.radians(ry), 4, "Y") @ \
        Matrix.Rotation(math.radians(rx), 4, "X")
    obj.data.transform(m)
    return obj


def tube(name, pts, radii, swatch, ox=0.0, sides=6, cap_start=True, cap_end=True, squash=1.0, roll=0.0):
    """A tube through the points pts [(x, y, z)] (character space) with radii [r] per point (a radius of 0 ends in a
    point). sides: 4-8; squash < 1 flattens it along the ring's second axis (a flat strap, a blade-like horn)."""
    P = [Vector(p) for p in pts]
    n = len(P)
    T = []
    for i in range(n):
        a = P[max(i - 1, 0)]
        b = P[min(i + 1, n - 1)]
        T.append((b - a).normalized())
    up = Vector((0, 0, 1)) if abs(T[0].z) < 0.9 else Vector((0, 1, 0))
    U = T[0].cross(up).normalized()
    frames = []
    for i in range(n):
        if i:
            # parallel transport: keep the ring's frame from twisting along the curve
            U = (U - T[i] * U.dot(T[i])).normalized()
        V = T[i].cross(U).normalized()
        frames.append((U.copy(), V))
    bm = bmesh.new()
    rings = []
    for i in range(n):
        r = radii[i]
        if r <= 1e-6:
            rings.append([bm.verts.new(P[i] + Vector((ox, 0, 0)))])
            continue
        U, V = frames[i]
        ring = []
        for k in range(sides):
            a = roll + 2 * math.pi * (k + 0.5) / sides
            ring.append(bm.verts.new(P[i] + U * (r * math.cos(a)) + V * (r * squash * math.sin(a)) +
                                     Vector((ox, 0, 0))))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        if len(b) == 1:
            for k in range(sides):
                bm.faces.new((a[k], a[(k + 1) % sides], b[0]))
        elif len(a) == 1:
            for k in range(sides):
                bm.faces.new((a[0], b[(k + 1) % sides], b[k]))
        else:
            for k in range(sides):
                bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))
    if cap_start and len(rings[0]) > 2:
        bm.faces.new(list(reversed(rings[0])))
    if cap_end and len(rings[-1]) > 2:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return A.flat(K.object_from_bmesh(name, bm, swatch=swatch))


def panel(name, outline, thickness, loc, swatch, ox=0.0, tilt=0.0, yaw=0.0, bend=0.0, dy=0.0):
    """An outline [(x, z)] (relative to loc; z down is negative) extruded `thickness` along Y, tilted `tilt` degrees
    about X (positive swings its bottom backwards, +Y) and turned `yaw` about Z. bend (metres): pushes the outline's
    side points backwards in proportion to |x| (a cape curving round the back, a tabard round the belly; negative
    curves the sides forward). dy: a Y offset before the tilt (a badge on a tilted cape: the cape's tilt, a dy)."""
    bm = bmesh.new()
    h = thickness / 2
    xs = max(abs(x) for x, _ in outline) or 1.0
    front, back = [], []
    for x, z in outline:
        b = bend * (abs(x) / xs) ** 2 + dy
        front.append(bm.verts.new((x, -h + b, z)))
        back.append(bm.verts.new((x, h + b, z)))
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    m = len(outline)
    for i in range(m):
        j = (i + 1) % m
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    rot = Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Rotation(math.radians(tilt), 4, "X")
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
    o = K.object_from_bmesh(name, bm, location=(loc[0] + ox, loc[1], loc[2]), swatch=swatch)
    return A.flat(o)


def cape(name, width_top, width_bottom, length, loc, swatch, ox=0.0, cols=8, depth=0.08, thickness=0.03, tilt=10.0,
         bend=0.06, seed=0, hem_swatch=None):
    """A cloth panel hanging from loc (its top centre) as `cols` columns, so it curves cleanly round the back: the
    sides pushed back by `bend` (metres, ~x squared), the bottom swung back `tilt` degrees, the hem cut into points
    (every other column's bottom `depth` higher, jittered by seed). hem_swatch: the hem's faces in another colour."""
    import random
    rnd = random.Random(seed)
    wt, wb = width_top / 2, width_bottom / 2
    top, bot = [], []
    for i in range(cols + 1):
        f = i / cols
        top.append((-wt + 2 * wt * f, 0.0))
        z = -length + (depth * rnd.uniform(0.7, 1.2) if i % 2 else -depth * rnd.uniform(0.0, 0.35))
        bot.append((-wb + 2 * wb * f, z))
    bm = bmesh.new()
    h = thickness / 2

    def v(x, z, side):
        b = bend * (abs(x) / max(wt, wb)) ** 2
        return bm.verts.new((x, side * h + b, z))
    F = [[v(x, z, -1) for x, z in row] for row in (top, bot)]
    B = [[v(x, z, 1) for x, z in row] for row in (top, bot)]
    hem_faces = []
    for i in range(cols):
        bm.faces.new((F[0][i], F[0][i + 1], F[1][i + 1], F[1][i]))
        bm.faces.new((B[0][i + 1], B[0][i], B[1][i], B[1][i + 1]))
        bm.faces.new((F[0][i + 1], F[0][i], B[0][i], B[0][i + 1]))          # top edge
        hem_faces.append(bm.faces.new((F[1][i], F[1][i + 1], B[1][i + 1], B[1][i])))  # hem edge
    bm.faces.new((F[0][0], F[1][0], B[1][0], B[0][0]))
    bm.faces.new((F[1][cols], F[0][cols], B[0][cols], B[1][cols]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rot = Matrix.Rotation(math.radians(tilt), 4, "X")
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
    bm.faces.index_update()
    hem_ids = [f.index for f in hem_faces]
    o = K.object_from_bmesh(name, bm, location=(loc[0] + ox, loc[1], loc[2]), swatch=swatch)
    if hem_swatch:
        K.set_swatch(o, hem_swatch, faces=hem_ids)
    return A.flat(o)


def tattered(width_top, width_bottom, length, teeth=4, depth=0.08, seed=0):
    """A cape/tabard outline (x, z) from z=0 down to -length, wider at the bottom, its hem cut into `teeth` points."""
    import random
    rnd = random.Random(seed)
    wt, wb = width_top / 2, width_bottom / 2
    pts = [(-wt, 0.0), (wt, 0.0)]
    hem = []
    for k in range(teeth * 2 + 1):
        f = k / (teeth * 2)
        x = wb - 2 * wb * f
        z = -length if k % 2 == 0 else -length + depth * rnd.uniform(0.7, 1.3)
        if k % 2 == 0 and 0 < k < teeth * 2:
            z -= depth * rnd.uniform(0.0, 0.4)
        hem.append((x, z))
    return pts + hem


def blade(name, outline, thickness, frame, swatch, edge=None):
    """An outline [(y, z)] extruded along X under the empty `frame` (gladiator_kit.extrude_poly, flat-shaded);
    edge: a lighter swatch on the faces round the rim (the bright edge)."""
    b = K.extrude_poly(name, outline, thickness=thickness, plane="YZ", swatch=swatch, parent=frame)
    if edge:
        K.set_swatch_where(b, edge, lambda c, n: abs(n.x) < 0.5)
    return A.flat(b)


def frame(name, loc, ox=0.0, rot=(0.0, 0.0, 0.0)):
    """An empty at loc (character space) rotated rot (degrees, XYZ): the hand's grip for weapons built on it."""
    f = K.empty(name, location=(loc[0] + ox, loc[1], loc[2]))
    f.rotation_euler = tuple(math.radians(v) for v in rot)
    return f


def grow(parts, ox, s, name="Root"):
    """Builds-at-chibi-scale-then-scales: parents every top-level part to an empty at (ox, 0, 0) scaled by s (big
    characters keep the style's proportions and helper sizes, then stand at their real height). Returns the empty."""
    import bpy
    root = K.empty(name, location=(ox, 0.0, 0.0))
    bpy.context.view_layer.update()
    inv = root.matrix_world.inverted()
    for o in parts:
        if o.parent is None and o is not root:
            o.parent = root
            o.matrix_parent_inverse = inv
    root.scale = (s, s, s)
    bpy.context.view_layer.update()
    return root


def child_box(name, size, loc, swatch, parent, pivot=(0, 0, 0), taper=(1.0, 1.0), rot=(0.0, 0.0, 0.0)):
    o = K.box(name, size=size, location=loc, pivot=pivot, taper=taper, swatch=swatch, parent=parent)
    o.rotation_euler = tuple(math.radians(v) for v in rot)
    return A.flat(o)


def helm(name, a, swatch, ox=0.0, scale=1.08, bottom=0.08, top=1.04, crown=0.45, front=1.0):
    """A closed helmet loft round the head: oct8 rings a little bigger than the head from hz(bottom) to hz(top)."""
    HW, ry = a["head_w"] * scale, a["head_back_y"] * scale
    hz = a["hz"]
    rings = [(hz(bottom), A.oct8(HW * 0.9, ry * 0.88, 0.64, front=front)),
             (hz(0.3), A.oct8(HW, ry, 0.62, front=front)), (hz(0.75), A.oct8(HW, ry, 0.62, front=front)),
             (hz(0.93), A.oct8(HW * 0.84, ry * 0.84, 0.6, front=front)),
             (hz(top), A.oct8(HW * crown, ry * crown, 0.6, front=front))]
    return A.loft(name, rings, swatch, ox)
