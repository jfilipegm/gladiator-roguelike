"""PR_ArmoryChest redesign: four Roman takes on the Armory Chest for the producer to pick from (v0.28.0 note: the
chest "shouldn't look like a pirate chest"; the classic model in props/interactables.py, a domed lid over iron bands and
a lock plate, stays there unchanged).

  A  Legion strongbox (arca): an iron-plated box on bronze paw feet, a bronze frame and gold rivets, a glowing gold
     eagle over a tabula ansata plaque on the front, a flat lid with a low hipped top and a glowing phalera on it.
  B  Weapon crate: a slatted army crate (dark gaps between the slats, corner posts, a diagonal brace), a blue parma
     with a glowing boss strapped to the front, a hinged plank board for a lid with two big gladii lying crossed on it.
  C  Shield chest: a wooden kit trunk with a bronze plinth and rim, crossed pila on both ends and three phalerae on a
     strap; the lid is a red scutum (gently curved, bronze rim, spine, gold wings and a glowing boss).
  D  Marble sarcophagus: a pale marble box on a plinth, pilasters at the front corners, a glowing gold laurel wreath,
     lion-mask ring handles on the ends, a temple-roof lid with gold pediment discs and glowing gold corner acroteria.

Contract (Interactables.cs finds the child named "Lid" and rotates it -105 deg about local X): two meshes exactly,
"Body" and "Lid", under a root empty "PR_ArmoryChest"; the Lid's origin is the hinge on the back top edge; front =
Blender -Y; origin at the base centre, z = 0 at the bottom; one palette material. Every chest also holds a glowing gold
hoard (a low mound) just below its rim (seen when the lid opens). Budget: <= 400 tris.

Usage: Tools/bheadless BlenderSrc/props/chest_variants.py key=value ...
  variant=A|B|C|D           build one and render its review sheet: Screenshots/chest/chest_<V>.png
                            (closed front / side / 3-4, open 3-4, 64 px silhouette)
  variant=all  or lineup=1  the lineup sheet Screenshots/chest/chest_lineup.png: the classic chest ("now") and A-D,
                            closed and open (3-4 view on the sand floor), and the 64 px silhouettes
  variant=<V> export=1      the game model Assets/Art/Models/A1/Props/PR_ArmoryChest_<V>.fbx through K.export_fbx
                            (+ BlenderSrc/blend/pr_armorychest.blend); no renders
  variant=<V|all> check=1   builds the game model(s) and checks the contract (names, origins, hinge, budget); no
                            renders, no export
  cull=1                    (with a render mode) renders with back faces culled, as Unity does
Screenshots/ lives in the main checkout (shared by all worktrees, git-ignored).
"""
import importlib
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

import gladiator_kit as K

importlib.reload(K)

ARGS = globals().get("ARGS", {})
_REPO = globals().get("REPO", K.REPO)
SHOTS = os.path.join(_REPO.split(os.sep + ".claude" + os.sep + "worktrees" + os.sep)[0], "Screenshots", "chest")
ROOT_NAME = "PR_ArmoryChest"
OPEN_DEG = 100.0          # the lid angle in the "open" renders (the game opens it to 105)
TRI_BUDGET = 400

TITLES = {"now": "Now (classic)", "A": "A  Legion strongbox", "B": "B  Weapon crate", "C": "C  Shield chest",
          "D": "D  Marble sarcophagus"}


# --------------------------------------------------------------------------- geometry helpers

def rect(hx, hy, cx=0.0, cy=0.0):
    """A rectangle section, CCW seen from +Z."""
    return [(cx + hx, cy - hy), (cx + hx, cy + hy), (cx - hx, cy + hy), (cx - hx, cy - hy)]


def ngon(n, r, cx=0.0, cy=0.0, phase=0.0):
    return [(cx + r * math.cos(phase + 2 * math.pi * i / n), cy + r * math.sin(phase + 2 * math.pi * i / n))
            for i in range(n)]


def _tri_ngons(bm):
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="EAR_CLIP")


def loft(name, rings, swatch, cap0=True, cap1=True):
    """Rings [(z, [(x, y), ...]), ...] (same point count, CCW seen from +Z), joined by quads. The winding alone makes
    the normals right, so open shells work: a ring above the last faces out (a wall), a smaller ring at the same z
    faces up (a rim), a ring below inside it faces in (a pit wall); a larger ring at the same z faces down (a ledge)."""
    bm = bmesh.new()
    loops = [[bm.verts.new((x, y, z)) for x, y in pts] for z, pts in rings]
    n = len(loops[0])
    for a, b in zip(loops, loops[1:]):
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    if cap0:
        bm.faces.new(list(reversed(loops[0])))
    if cap1:
        bm.faces.new(loops[-1])
    _tri_ngons(bm)
    return K.object_from_bmesh(name, bm, swatch=swatch)


def loft3(name, rings, swatch):
    """Closed solid through 3D rings [[(x, y, z), ...], ...] with both end caps; normals recalculated (closed only)."""
    bm = bmesh.new()
    loops = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(loops[0])
    for a, b in zip(loops, loops[1:]):
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _tri_ngons(bm)
    return K.object_from_bmesh(name, bm, swatch=swatch)


def prism(name, pts, axis, a0, a1, swatch, keep0=True, keep1=True):
    """A 2D outline extruded along `axis` from a0 to a1: axis "Y" takes (x, z) points (plates on the front or back),
    "X" takes (y, z) (plates on the ends), "Z" takes (x, y) (plates lying on a lid). keep0 / keep1 = False drops the
    cap at a0 / a1 (the face pressed against the body, never seen)."""
    def v3(p, a):
        if axis == "X":
            return (a, p[0], p[1])
        if axis == "Y":
            return (p[0], a, p[1])
        return (p[0], p[1], a)
    bm = bmesh.new()
    r0 = [bm.verts.new(v3(p, a0)) for p in pts]
    r1 = [bm.verts.new(v3(p, a1)) for p in pts]
    n = len(pts)
    for k in range(n):
        bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
    c0 = bm.faces.new(list(reversed(r0)))
    c1 = bm.faces.new(r1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    drop = [f for f, keep in ((c0, keep0), (c1, keep1)) if not keep]
    if drop:
        bmesh.ops.delete(bm, geom=drop, context="FACES_ONLY")
    _tri_ngons(bm)
    return K.object_from_bmesh(name, bm, swatch=swatch)


def torus(name, center, R, r, axis, swatch, n=6, m=3):
    """A ring (ring handles): major radius R in the plane normal to `axis`, tube radius r, n x m quads."""
    u, v, w = {"X": ((0, 1, 0), (0, 0, 1), (1, 0, 0)), "Y": ((1, 0, 0), (0, 0, 1), (0, 1, 0)),
               "Z": ((1, 0, 0), (0, 1, 0), (0, 0, 1))}[axis]
    u, v, w, c = Vector(u), Vector(v), Vector(w), Vector(center)
    bm = bmesh.new()
    vs = []
    for i in range(n):
        a = 2 * math.pi * i / n
        radial = u * math.cos(a) + v * math.sin(a)
        vs.append([bm.verts.new(c + radial * (R + r * math.cos(2 * math.pi * j / m)) +
                                w * (r * math.sin(2 * math.pi * j / m))) for j in range(m)])
    for i in range(n):
        for j in range(m):
            i2, j2 = (i + 1) % n, (j + 1) % m
            bm.faces.new((vs[i][j], vs[i2][j], vs[i2][j2], vs[i][j2]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K.object_from_bmesh(name, bm, swatch=swatch)


def laurel(name, cx, cz, y_front, depth, r_in, r_out, r_tip, a0, a1, m, swatch):
    """One laurel branch in relief on a front face (the wall is at y_front + depth): m leaves along the arc a0 -> a1
    (radians, either way round) round (cx, cz), each leaf's tip leaning toward a1, so the leaves overlap like scales.
    Open at the back. 9 m + 4 tris."""
    bm = bmesh.new()
    angles = [a0 + (a1 - a0) * i / m for i in range(m + 1)]

    def pt(r, a, y):
        return bm.verts.new((cx + r * math.cos(a), y, cz + r * math.sin(a)))

    def layer(y):
        return ([pt(r_in, a, y) for a in angles], [pt(r_out, a, y) for a in angles],
                [pt(r_tip, a + 0.8 * (b - a), y) for a, b in zip(angles, angles[1:])])
    fi, fb, ft = layer(y_front)
    bi, bb, bt = layer(y_front + depth)
    for i in range(m):
        j = i + 1
        bm.faces.new((fi[i], fi[j], fb[j], ft[i], fb[i]))
        bm.faces.new((bi[i], bb[i], bt[i], bb[j], bi[j]))
        bm.faces.new((fi[i], bi[i], bi[j], fi[j]))
        bm.faces.new((fb[i], ft[i], bt[i], bb[i]))
        bm.faces.new((ft[i], fb[j], bb[j], bt[i]))
    for i in (0, m):
        bm.faces.new((fi[i], fb[i], bb[i], bi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    back = [f for f in bm.faces if f.normal.y > 0.9 and f.calc_center_median().y > y_front + depth * 0.5]
    bmesh.ops.delete(bm, geom=back, context="FACES_ONLY")
    _tri_ngons(bm)
    return K.object_from_bmesh(name, bm, swatch=swatch)


def box(name, size, loc, swatch, pivot=(0, 0, -1), taper=(1.0, 1.0), rot=None):
    o = K.box(name, size=size, location=loc, pivot=pivot, taper=taper, swatch=swatch)
    if rot:
        o.rotation_euler = rot
    return o


def paint(obj, swatch, test):
    """Faces whose local centre / normal pass test(c, n) get the swatch (parts are built at the origin, so local =
    chest space until they are turned)."""
    return K.set_swatch_where(obj, swatch, test)


def bake(o):
    """Moves an object's transform into its mesh (identity transform afterwards)."""
    bpy.context.view_layer.update()
    o.data.transform(o.matrix_world)
    o.matrix_world = Matrix.Identity(4)
    return o


def turn(objs, pivot, euler):
    """Rotates a group of parts rigidly about `pivot` (chest space)."""
    p = Vector(pivot)
    m = Matrix.Translation(p) @ Euler(euler).to_matrix().to_4x4() @ Matrix.Translation(-p)
    for o in objs:
        bake(o).data.transform(m)
    return objs


def tub(name, hx, hy, z0, z1, wall, rim=0.04, sink=0.065, rim_sw=None, inner="shadow", fill="glow_gold"):
    """The chest body: a box whose top is a rim around a shallow pit holding a low mound of glowing gold (the hoard,
    only seen when the lid is open; it stays below the rim). 36 tris."""
    ri = rect(hx - rim, hy - rim)
    mound = rect((hx - rim) * 0.4, (hy - rim) * 0.22)
    o = loft(name, [(z0, rect(hx, hy)), (z1, rect(hx, hy)), (z1, ri), (z1 - sink, ri), (z1 - 0.008, mound)], wall)
    if rim_sw:
        paint(o, rim_sw, lambda c, n: n.z > 0.9 and c.z > z1 - 1e-4)
    paint(o, inner, lambda c, n: abs(n.z) < 0.3 and c.z > z1 - sink - 1e-4 and abs(c.x) < hx - rim + 1e-4
          and abs(c.y) < hy - rim + 1e-4)
    paint(o, fill, lambda c, n: n.z > 0.3 and z0 + 1e-4 < c.z < z1 - 1e-4)
    return o


def band(name, hx, hy, z0, z1, out, swatch):
    """An open moulding band round a body section: bottom ledge, outer wall, top ledge (24 tris)."""
    return loft(name, [(z0, rect(hx, hy)), (z0, rect(hx + out, hy + out)), (z1, rect(hx + out, hy + out)),
                       (z1, rect(hx, hy))], swatch, cap0=False, cap1=False)


def assemble(name, root, body_parts, lid_parts, hinge):
    """Joins the parts into exactly two meshes under `root`: Body (origin at the base centre) and Lid (origin on the
    hinge). Both keep identity rotation and scale."""
    out = {}
    for part, parts, origin in (("Body", body_parts, (0.0, 0.0, 0.0)), ("Lid", lid_parts, hinge)):
        bpy.context.view_layer.update()
        o = K.join(parts, part)
        bake(o)
        o.data.transform(Matrix.Translation(-Vector(origin)))
        o.parent = root
        o.matrix_parent_inverse = Matrix.Identity(4)
        o.location = origin
        out[part] = o
    return out


# --------------------------------------------------------------------------- A: legion strongbox (arca)

def build_A(name):
    root = K.empty(name)
    hx, hy, zf, H = 0.50, 0.31, 0.09, 0.60
    b = [tub("Body", hx, hy, zf, H, "iron", rim=0.045, rim_sw="bronze")]
    for sx in (-1, 1):                                    # paw feet
        for sy in (-1, 1):
            b.append(box("Foot", (0.15, 0.15, zf + 0.02), (sx * (hx - 0.06), sy * (hy - 0.06), 0), "bronze_d",
                         taper=(0.72, 0.72)))
    b.append(box("Plinth", (2 * hx + 0.05, 2 * hy + 0.05, 0.07), (0, 0, zf), "bronze"))
    b.append(band("TopBand", hx, hy, H - 0.07, H, 0.02, "bronze"))
    for sx in (-1, 1):                                    # bronze corner angles, gold rivets on the front ones
        for sy in (-1, 1):
            b.append(box("Corner", (0.09, 0.09, H - zf - 0.14), (sx * hx, sy * hy, zf + 0.07), "bronze"))
            if sy < 0:
                b.append(box("Rivet", (0.05, 0.03, 0.05), (sx * hx, -hy - 0.055, (zf + H) / 2), "gold",
                             pivot=(0, 0, 0)))
    # the tabula ansata (a dovetailed plaque) and the glowing eagle over it
    yf = -hy
    tab = [(-0.17, -0.06), (0.17, -0.06), (0.17, -0.022), (0.225, -0.055), (0.225, 0.055), (0.17, 0.022),
           (0.17, 0.06), (-0.17, 0.06), (-0.17, 0.022), (-0.225, 0.055), (-0.225, -0.055), (-0.17, -0.022)]
    b.append(prism("Plaque", [(x, z + zf + 0.13) for x, z in tab], "Y", yf - 0.022, yf + 0.004, "bronze_l",
                   keep1=False))
    half = [(0.0, 0.10), (0.026, 0.083), (0.026, 0.054), (0.07, 0.072), (0.15, 0.118), (0.168, 0.074),
            (0.125, 0.026), (0.06, -0.004), (0.036, -0.048), (0.066, -0.098), (0.0, -0.078)]
    eagle = half + [(-x, z) for x, z in reversed(half[1:-1])]
    ez = H - 0.24
    b.append(prism("Eagle", [(x * 1.15, z * 1.15 + ez) for x, z in eagle], "Y", yf - 0.03, yf + 0.004, "glow_gold",
                   keep1=False))
    for sx in (-1, 1):                                    # drop handles on the ends
        b.append(box("Handle", (0.04, 0.24, 0.045), (sx * (hx + 0.045), 0, H - 0.2), "bronze_d"))
        b.append(box("Lug", (0.06, 0.03, 0.06), (sx * (hx + 0.02), 0, H - 0.18), "bronze_d", pivot=(0, 0, 0)))
    # the lid: a flat slab with an iron edge, a low hipped top in bronze, a glowing phalera
    hinge = (0.0, hy, H)
    ld = 2 * hy + 0.04
    lid = box("LidSlab", (2 * hx + 0.07, ld, 0.09), (0, hy - ld / 2, H), "iron_d")
    paint(lid, "bronze", lambda c, n: abs(n.z) < 0.5)
    hip = box("LidHip", (2 * hx + 0.07, ld, 0.065), (0, hy - ld / 2, H + 0.09), "iron", taper=(0.74, 0.56))
    paint(hip, "iron_d", lambda c, n: n.z > 0.99)
    phal = K.cylinder("Phalera", radius=0.1, depth=0.035, verts=8, location=(0, hy - ld / 2, H + 0.15),
                      swatch="glow_gold")
    return root, assemble(name, root, b, [lid, hip, phal], hinge)


# --------------------------------------------------------------------------- B: weapon crate

def gladius(prefix, length=0.7):
    """A big gladius lying flat (in XY, edge up), hilt toward -X, centred on the origin, bottom at z = 0."""
    blade_l, guard_l, grip_l, pom_l = length * 0.62, 0.05, length * 0.2, 0.06
    x0 = -(blade_l + guard_l + grip_l + pom_l) / 2 + pom_l + grip_l + guard_l      # where the blade starts
    w = 0.1
    blade = prism(prefix + "Blade", [(x0, -w / 2), (x0 + blade_l * 0.78, -w / 2 * 1.06), (x0 + blade_l, 0.0),
                                     (x0 + blade_l * 0.78, w / 2 * 1.06), (x0, w / 2)], "Z", 0.0, 0.022, "steel",
                  keep0=False)
    paint(blade, "steel_l", lambda c, n: n.z > 0.9)
    guard = box(prefix + "Guard", (guard_l, 0.17, 0.05), (x0 - guard_l / 2, 0, 0), "gold", pivot=(0, 0, -1))
    grip = box(prefix + "Grip", (grip_l, 0.045, 0.04), (x0 - guard_l - grip_l / 2, 0, 0), "leather_d")
    pom = box(prefix + "Pommel", (pom_l, 0.085, 0.06), (x0 - guard_l - grip_l - pom_l / 2, 0, 0), "glow_gold")
    return [blade, guard, grip, pom]


def build_B(name):
    root = K.empty(name)
    hx, hy, z0, H = 0.46, 0.28, 0.0, 0.60
    b = [tub("Body", hx, hy, z0, H, "shadow", rim=0.035, rim_sw="wood")]
    for sx in (-1, 1):                                    # corner posts
        for sy in (-1, 1):
            b.append(box("Post", (0.08, 0.08, H - z0), (sx * hx, sy * hy, z0), "wood"))
    for z_lo, z_hi in ((0.03, 0.19), (0.225, 0.385), (0.42, 0.58)):    # slats, dark gaps between them
        b.append(band("Slats", hx, hy, z_lo, z_hi, 0.025, "wood_light"))
    yb = -(hy + 0.025)                                    # the slats' front face
    ang = math.atan2(0.42, 0.78)
    b.append(box("Brace", (0.9, 0.025, 0.075), (0, yb - 0.0125, 0.32), "wood", pivot=(0, 0, 0), rot=(0, -ang, 0)))
    # a blue parma strapped to the front: bronze rim, glowing boss
    sz, ys = 0.33, yb - 0.025
    b.append(prism("ShieldRim", ngon(8, 0.19, 0, sz, math.pi / 8), "Y", ys - 0.022, ys + 0.004, "bronze_l",
                   keep1=False))
    b.append(prism("Shield", ngon(8, 0.165, 0, sz, math.pi / 8), "Y", ys - 0.04, ys - 0.02, "cloth_blue",
                   keep1=False))
    b.append(prism("Boss", ngon(8, 0.055, 0, sz, math.pi / 8), "Y", ys - 0.085, ys - 0.03, "glow_gold",
                   keep1=False))
    # the lid: a board of three planks on two battens, two big gladii lashed on top
    hinge = (0.0, hy + 0.04, H)
    ld, pt = 2 * hy + 0.09, 0.07
    planks = []
    pw = (ld - 0.02) / 3
    for i in range(3):
        cy = hinge[1] - pw / 2 - i * (pw + 0.01)
        planks.append(box("Plank", (2 * hx + 0.1, pw, pt), (0, cy, H), "wood_light"))
    lid = planks
    for sx in (-1, 1):
        lid.append(box("Batten", (0.07, ld, 0.04), (sx * 0.465, hinge[1] - ld / 2, H + pt), "wood"))
    yc = hinge[1] - ld / 2
    for k, a in enumerate((0.52, -0.52)):
        sw = gladius("Gladius%d" % k, 0.9)
        for o in sw:
            o.location.z += H + pt + 0.012 * k        # the second one lies over the first
        turn(sw, (0, 0, 0), (0, 0, a))
        for o in sw:
            o.data.transform(Matrix.Translation((0, yc, 0)))
        lid += sw
    return root, assemble(name, root, b, lid, hinge)


# --------------------------------------------------------------------------- C: shield chest

def pilum(prefix, length=0.74):
    """A pilum along +Z centred on the origin: wooden shaft, then a thin iron shank tapering to the point."""
    shaft_l = length * 0.58
    shaft = box(prefix + "Shaft", (0.05, 0.05, shaft_l), (0, 0, -length / 2), "wood_light")
    shank = box(prefix + "Shank", (0.034, 0.034, length - shaft_l), (0, 0, -length / 2 + shaft_l), "iron",
                taper=(0.15, 0.15))
    return [shaft, shank]


def build_C(name):
    root = K.empty(name)
    hx, hy, H = 0.49, 0.31, 0.56
    b = [tub("Body", hx, hy, 0.0, H, "wood", rim=0.04, rim_sw="bronze_l")]
    b.append(box("Plinth", (2 * hx + 0.04, 2 * hy + 0.04, 0.07), (0, 0, 0), "bronze_d"))
    b.append(band("TopBand", hx, hy, H - 0.06, H, 0.015, "bronze_l"))
    yf = -hy
    b.append(box("Strap", (2 * hx + 0.01, 0.02, 0.07), (0, yf - 0.005, 0.28), "leather", pivot=(0, 0, 0)))
    for x, glow in ((-0.26, False), (0.0, True), (0.26, False)):      # phalerae on the strap
        r = 0.085 if glow else 0.07
        b.append(prism("Phalera", ngon(8 if glow else 6, r, x, 0.28, math.pi / 8 if glow else 0), "Y",
                       yf - 0.04, yf - 0.01, "glow_gold" if glow else "gold", keep1=False))
    for sx in (-1, 1):                                    # crossed pila on the ends
        for a in (-0.52, 0.52):
            p = pilum("Pilum", 0.76)
            for o in p:
                o.location += Vector((sx * (hx + 0.04), 0, 0.35))
            turn(p, (sx * (hx + 0.04), 0, 0.35), (a, 0, 0))
            b += p
    # the lid: a scutum lying on the box, curved across, long axis along X
    hinge = (0.0, hy, H)
    yb, yfr = hy, -(hy + 0.05)
    yc, half = (yb + yfr) / 2, (yb - yfr) / 2
    t, s, xe, rim = 0.06, 0.075, 0.50, 0.045

    def zt(y):
        u = (y - yc) / half
        return H + t + s * (1 - u * u)
    ys = [yb, yb - rim, yc + half * 0.45, yc, yc - half * 0.45, yfr + rim, yfr]
    sec = [(yfr, H), (yb, H)] + [(y, zt(y)) for y in ys]
    rings = [[(x, y, z) for y, z in sec] for x in (-xe, -xe + rim, xe - rim, xe)]
    sc = loft3("Scutum", rings, "crimson")
    paint(sc, "bronze_l", lambda c, n: abs(c.x) > xe - rim or c.y > yb - rim or c.y < yfr + rim)
    paint(sc, "crimson_d", lambda c, n: abs(n.x) > 0.9)
    crown = H + t + s
    spine = box("Spine", (2 * xe - 0.12, 0.05, 0.02), (0, yc, crown - 0.006), "bronze_l")
    boss = K.cylinder("Boss", radius=0.11, depth=0.08, verts=8, location=(0, yc, crown - 0.006), taper=0.5,
                      swatch="glow_gold")
    wing = [(0.14, 0.045), (0.2, 0.115), (0.34, 0.125), (0.27, 0.055), (0.39, 0.02), (0.27, -0.02), (0.34, -0.09),
            (0.2, -0.085), (0.14, -0.035)]
    wings = []
    for sx in (-1, 1):
        pts = [(sx * x, yc + y) for x, y in wing]
        if sx < 0:
            pts.reverse()
        wings.append(prism("Wing", pts, "Z", crown - 0.025, crown + 0.012, "gold", keep0=False))
    return root, assemble(name, root, b, [sc, spine, boss] + wings, hinge)


# --------------------------------------------------------------------------- D: marble sarcophagus

def build_D(name):
    root = K.empty(name)
    hx, hy, zp, H = 0.48, 0.30, 0.07, 0.54
    b = [tub("Body", hx, hy, zp, H, "marble", rim=0.04, rim_sw="marble_shadow")]
    b.append(box("Plinth", (2 * hx + 0.08, 2 * hy + 0.08, zp), (0, 0, 0), "marble_shadow"))
    for sx in (-1, 1):                                    # pilasters on the front corners
        b.append(box("Pilaster", (0.09, 0.09, H - zp - 0.02), (sx * hx, -hy, zp), "marble_vein"))
    yf = -hy
    wz = 0.31
    for a0, a1 in ((math.radians(-100), math.radians(62)), (math.radians(280), math.radians(118))):
        b.append(laurel("Laurel", 0.0, wz, yf - 0.032, 0.036, 0.075, 0.105, 0.15, a0, a1, 5, "glow_gold"))
    b.append(box("Knot", (0.07, 0.03, 0.05), (0, yf - 0.03, wz - 0.09), "gold", pivot=(0, 0, 0)))
    for sx in (-1, 1):                                    # lion-head ring handles on the ends
        xs = sx * hx
        b.append(prism("Mane", ngon(6, 0.075, 0.0, 0.35, math.pi / 6), "X", xs, xs + sx * 0.03, "bronze",
                       keep0=False))
        b.append(torus("Ring", (xs + sx * 0.036, 0, 0.27), 0.062, 0.014, "X", "gold"))
    # the lid: a slab, a temple roof (ridge along X), pediment discs and gold corner acroteria
    hinge = (0.0, hy, H)
    yb, yfr = hy, -(hy + 0.06)
    lid = [box("LidSlab", (2 * hx + 0.06, yb - yfr, 0.07), (0, (yb + yfr) / 2, H), "marble_shadow")]
    ridge_y, ze, zr = (yb + yfr) / 2, H + 0.11, H + 0.24
    sec = [(yfr, H + 0.07), (yb, H + 0.07), (yb, ze), (ridge_y, zr), (yfr, ze)]
    roof = loft3("Roof", [[(x, y, z) for y, z in sec] for x in (-hx - 0.01, hx + 0.01)], "marble")
    paint(roof, "marble_shadow", lambda c, n: abs(n.y) > 0.9)
    lid.append(roof)
    for sx in (-1, 1):
        xs = sx * (hx + 0.01)
        lid.append(prism("Disc", ngon(6, 0.045, ridge_y, H + 0.155, math.pi / 6), "X", xs, xs + sx * 0.018,
                         "gold", keep0=False))
        for y in (yfr + 0.05, yb - 0.05):
            lid.append(box("Acroterion", (0.1, 0.1, 0.14), (sx * (hx - 0.03), y, H + 0.07), "glow_gold",
                           taper=(0.45, 0.45)))
    return root, assemble(name, root, b, lid, hinge)


BUILDERS = {"A": build_A, "B": build_B, "C": build_C, "D": build_D}


def build(variant, name):
    root, parts = BUILDERS[variant](name)
    return root, parts


def stats(root, parts):
    lo, hi = K.world_bounds([parts["Body"]])
    alo, ahi = K.world_bounds([parts["Body"], parts["Lid"]])
    off = root.matrix_world.translation
    return {"tris": K.tri_count([parts["Body"], parts["Lid"]]), "tris_body": K.tri_count([parts["Body"]]),
            "tris_lid": K.tri_count([parts["Lid"]]),
            "body_size": [round(v, 3) for v in (hi - lo)], "total_height": round(ahi.z - off.z, 3),
            "footprint": [round(v, 3) for v in (ahi - alo)[:2]], "bottom_z": round(alo.z - off.z, 3),
            "hinge": [round(v, 3) for v in parts["Lid"].location]}


# --------------------------------------------------------------------------- the classic chest (for comparison)

def build_classic(name):
    """The current PR_ArmoryChest, rebuilt from chest() in props/interactables.py (only that function and HINGE are
    run: the module itself exports on import, and its .blend may be a git-lfs pointer in a worktree)."""
    import ast
    path = os.path.join(K.REPO, "BlenderSrc", "props", "interactables.py")
    tree = ast.parse(open(path).read())
    body = [n for n in tree.body if (isinstance(n, ast.FunctionDef) and n.name == "chest") or
            (isinstance(n, ast.Assign) and any(getattr(t, "id", None) == "HINGE" for t in n.targets))]
    ns = {"K": K, "math": math, "bpy": bpy}
    exec(compile(ast.Module(body=body, type_ignores=[]), path, "exec"), ns)
    root = ns["chest"](name)
    bpy.context.view_layer.update()
    return root, {c.name.split(".")[0]: c for c in root.children}


def check(variant):
    """Builds one chest as the game model (clean scene, root PR_ArmoryChest) and checks the contract without exporting:
    two meshes Body and Lid under the root, identity rotations, Body at the origin, nothing below z = 0, the Lid's
    origin on the body's back top edge with nothing of the Lid behind or below the hinge, and the tri budget."""
    K.clear_scene()
    root, parts = build(variant, ROOT_NAME)
    kids = sorted(c.name for c in root.children)
    body, lid = parts["Body"], parts["Lid"]
    blo, bhi = K.world_bounds([body])
    llo, lhi = K.world_bounds([lid])
    h = lid.location
    tub_o = body.data
    rim_z = max(v.co.z for v in tub_o.vertices if abs(v.co.x) < 0.499 and abs(v.co.y) < 0.33)   # the body's top
    s = stats(root, parts)
    errors = []
    if kids != ["Body", "Lid"] or any(c.type != "MESH" for c in root.children):
        errors.append("children %s" % kids)
    for o in (root, body, lid):
        if any(abs(a) > 1e-6 for a in o.rotation_euler) or any(abs(c - 1) > 1e-6 for c in o.scale):
            errors.append("%s not identity" % o.name)
    if body.location.length > 1e-6 or root.location.length > 1e-6:
        errors.append("Body/root not at the origin")
    if abs(blo.z) > 1e-3:
        errors.append("Body bottom at %.3f" % blo.z)
    if lhi.y > h.y + 1e-3 or llo.z < h.z - 1e-3:
        errors.append("Lid reaches behind/below the hinge")
    if abs(h.z - rim_z) > 1e-3:
        errors.append("hinge z %.3f != rim %.3f" % (h.z, rim_z))
    if s["tris"] > TRI_BUDGET:
        errors.append("%d tris" % s["tris"])
    return {"ok": not errors, "errors": errors, "children": kids, "materials": len(body.data.materials) +
            len(lid.data.materials), **s}


# --------------------------------------------------------------------------- rendering

def _palette_glow():
    """Render-only: palette faces whose alpha marks emission (the glow_* swatches) emit, as in the game."""
    for m in bpy.data.materials:
        nt = m.node_tree if m.use_nodes else None
        if nt is None or m.get("_glow"):
            continue
        tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image and "PAL_Master" in n.image.name), None)
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if tex is None or bsdf is None:
            continue
        img = bpy.data.images.load(os.path.join(K.PALETTE_DIR, "PAL_Master.png"), check_existing=False)
        img.alpha_mode = "STRAIGHT"
        t2 = nt.nodes.new("ShaderNodeTexImage")
        t2.image, t2.interpolation = img, "Closest"
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation, inv.inputs[0].default_value = "SUBTRACT", 1.0
        nt.links.new(t2.outputs["Alpha"], inv.inputs[1])
        mul = nt.nodes.new("ShaderNodeMath")
        mul.operation, mul.inputs[1].default_value = "MULTIPLY", 0.55
        nt.links.new(inv.outputs[0], mul.inputs[0])
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        nt.links.new(mul.outputs[0], bsdf.inputs["Emission Strength"])
        m["_glow"] = 1


class Stage:
    """EEVEE: a sun from the front-left and a warm ambient over a sand floor; an ortho camera; Workbench for the flat
    silhouettes. Labels are text objects parented to the camera."""

    VIEW_34 = Vector((0.72, -1.0, 0.82))

    def __init__(self):
        sc = self.sc = bpy.context.scene
        self.ground = K.box("_Ground", size=(80, 80, 0.02), location=(0, 0, -0.02), swatch="sand")
        sun_d = bpy.data.lights.new("_Sun", "SUN")
        sun_d.energy, sun_d.angle = 2.1, math.radians(4)
        self.sun = bpy.data.objects.new("_Sun", sun_d)
        sc.collection.objects.link(self.sun)
        self.sun.rotation_euler = Euler((math.radians(42), 0, math.radians(-32)))
        w = sc.world or bpy.data.worlds.new("World")
        sc.world = w
        w.use_nodes = True
        self.bg = w.node_tree.nodes.get("Background")
        cam_d = bpy.data.cameras.new("_Cam")
        cam_d.type, cam_d.clip_end = "ORTHO", 200
        self.cam = bpy.data.objects.new("_Cam", cam_d)
        sc.collection.objects.link(self.cam)
        sc.camera = self.cam
        r = sc.render
        r.film_transparent = False
        r.image_settings.file_format = "PNG"
        r.image_settings.color_mode = "RGBA"
        r.resolution_percentage = 100
        sc.view_settings.view_transform = "Standard"
        sc.view_settings.look = "None"
        _palette_glow()
        if ARGS.get("cull") == "1":           # back faces culled, as in Unity: flipped faces show as holes
            for m in bpy.data.materials:
                m.use_backface_culling = True
        self.ink = bpy.data.materials.new("_Ink")
        self.ink.use_nodes = True
        nt = self.ink.node_tree
        nt.nodes.clear()
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = (0.92, 0.86, 0.72, 1)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(em.outputs[0], out.inputs[0])
        font_path = os.path.join(_REPO, "Assets", "Art", "Fonts", "MarcellusSC", "MarcellusSC-Regular.ttf")
        self.font = bpy.data.fonts.load(font_path) if os.path.exists(font_path) else None

    def eevee(self):
        for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
            try:
                self.sc.render.engine = e
                break
            except TypeError:
                continue
        try:
            self.sc.eevee.taa_render_samples = 32
        except AttributeError:
            pass
        self.bg.inputs[0].default_value = (0.78, 0.74, 0.68, 1)
        self.bg.inputs[1].default_value = 0.52

    def aim(self, target, direction, ortho):
        d = Vector(direction).normalized()
        self.cam.location = Vector(target) + d * 30
        self.cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        self.cam.data.ortho_scale = ortho

    def render(self, path, w, h):
        r = self.sc.render
        r.resolution_x, r.resolution_y = w, h
        r.filepath = path
        bpy.ops.render.render(write_still=True)
        return path

    def shot(self, path, target, direction, ortho, w, h):
        self.eevee()
        self.ground.hide_render = False
        self.aim(target, direction, ortho)
        return self.render(path, w, h)

    def silhouette(self, path, target, ortho, px=64):
        """Flat black on white, px x px, from the 3/4 view (the CLAUDE.md 64 px test)."""
        K._set_engine(self.sc)
        sh = self.sc.display.shading
        sh.light, sh.color_type, sh.single_color = "FLAT", "SINGLE", (0, 0, 0)
        sh.show_shadows = sh.show_cavity = False
        self.sc.world.color = (1, 1, 1)
        self.ground.hide_render = True
        self.aim(target, self.VIEW_34, ortho)
        out = self.render(path, px, px)
        self.ground.hide_render = False
        return out

    def label(self, path, lines, w, h, bg=(0.10, 0.08, 0.07)):
        """A text-only tile: lines [(text, size_px, (r, g, b)), ...] centred, on a dark band."""
        self.eevee()
        self.bg.inputs[0].default_value = (*bg, 1)
        self.bg.inputs[1].default_value = 1.0
        hidden = [o for o in self.sc.objects if o.type in ("MESH", "LIGHT") and not o.hide_render]
        for o in hidden:
            o.hide_render = True
        self.cam.location, self.cam.rotation_euler = (0, 0, 50), (0, 0, 0)
        self.cam.data.ortho_scale = w / 100.0
        texts = []
        total = sum(s for _, s, _ in lines) * 1.25
        y = total / 2
        for text, size, col in lines:
            y -= size * 1.25 / 2
            cu = bpy.data.curves.new("_Label", type="FONT")
            cu.body, cu.size, cu.align_x, cu.align_y = text, size / 100.0, "CENTER", "CENTER"
            if self.font:
                cu.font = self.font
            mat = self.ink.copy()
            mat.node_tree.nodes["Emission"].inputs["Color"].default_value = (*col, 1)
            cu.materials.append(mat)
            o = bpy.data.objects.new("_Label", cu)
            self.sc.collection.objects.link(o)
            o.location = (0, y / 100.0, 40)
            texts.append(o)
            y -= size * 1.25 / 2
        out = self.render(path, w, h)
        for o in texts:
            bpy.data.objects.remove(o, do_unlink=True)
        for o in hidden:
            o.hide_render = False
        return out


def _load(path):
    import numpy as np
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return np.flipud(a)          # top row first


def _save(arr, path):
    import numpy as np
    out = bpy.data.images.new("_sheet", arr.shape[1], arr.shape[0], alpha=True)
    out.pixels = np.flipud(arr).ravel()
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    return path


def _sil_tile(sil, w, h, k=3, bg=(1.0, 1.0, 1.0)):
    """The 64 px silhouette scaled up k times (nearest) with the 1:1 one beside it, on a w x h tile."""
    import numpy as np
    tile = np.ones((h, w, 4), dtype=np.float32)
    tile[..., :3] = bg
    big = np.repeat(np.repeat(sil, k, axis=0), k, axis=1)
    bh, bw = big.shape[:2]
    x0 = (w - bw - sil.shape[1] - 16) // 2
    y0 = (h - bh) // 2
    tile[y0:y0 + bh, x0:x0 + bw] = big
    sx = x0 + bw + 16
    tile[y0 + bh - sil.shape[0]:y0 + bh, sx:sx + sil.shape[1]] = sil
    return tile


def set_open(lid, opened):
    lid.rotation_euler = (math.radians(-OPEN_DEG) if opened else 0.0, 0.0, 0.0)
    bpy.context.view_layer.update()


def _isolate(chests, keep):
    for root, parts in chests.values():
        vis = root is keep
        for o in parts.values():
            o.hide_render = not vis


TARGET = (0.0, 0.0, 0.62)
ORTHO_TILE = 2.4
ORTHO_SIL = 1.45


def render_lineup(chests, order, x_of):
    import numpy as np
    tiles = os.path.join(SHOTS, "tiles")
    os.makedirs(tiles, exist_ok=True)
    st = Stage()
    cw, th, sh_ = 380, 330, 230
    head_w = 150
    cols = []
    for key in order:
        root, parts = chests[key]
        _isolate(chests, root)
        tgt = (x_of[key] + TARGET[0], TARGET[1], TARGET[2])
        set_open(parts["Lid"], False)
        closed = st.shot(os.path.join(tiles, "%s_closed.png" % key), tgt, Stage.VIEW_34, ORTHO_TILE, cw, th)
        sil = st.silhouette(os.path.join(tiles, "%s_sil64.png" % key), (tgt[0], 0, 0.4), ORTHO_SIL)
        set_open(parts["Lid"], True)
        opened = st.shot(os.path.join(tiles, "%s_open.png" % key), tgt, Stage.VIEW_34, ORTHO_TILE, cw, th)
        set_open(parts["Lid"], False)
        s = stats(root, parts)
        sub = "%d tris   %.2f x %.2f m   %.2f m tall" % (s["tris"], s["footprint"][0], s["footprint"][1],
                                                         s["total_height"])
        head = st.label(os.path.join(tiles, "%s_head.png" % key),
                        [(TITLES[key], 30, (0.98, 0.86, 0.5)), (sub, 17, (0.85, 0.8, 0.72))], cw, 84)
        cols.append((key, head, closed, opened, sil, s))
    rows = {}
    for name, h in (("closed", th), ("open", th), ("64 px", sh_)):
        rows[name] = st.label(os.path.join(tiles, "row_%s.png" % name.replace(" ", "")),
                              [(name, 19, (0.9, 0.84, 0.7))], head_w, h)
    corner = st.label(os.path.join(tiles, "corner.png"), [("", 10, (1, 1, 1))], head_w, 84)
    gap = 4
    col_arrays = []
    for key, head, closed, opened, sil, s in cols:
        sil_arr = _load(sil)
        col = [_load(head), _load(closed), _load(opened), _sil_tile(sil_arr, cw, sh_)]
        col_arrays.append(col)
    left = [_load(corner), _load(rows["closed"]), _load(rows["open"]), _load(rows["64 px"])]
    dark = np.array([0.10, 0.08, 0.07, 1.0], dtype=np.float32)

    def stack(col):
        parts = []
        for i, a in enumerate(col):
            parts.append(a)
            if i < len(col) - 1:
                parts.append(np.tile(dark, (gap, a.shape[1], 1)))
        return np.concatenate(parts, axis=0)
    columns = [stack(left)] + [stack(c) for c in col_arrays]
    hgt = columns[0].shape[0]
    pieces = []
    for i, c in enumerate(columns):
        pieces.append(c)
        if i < len(columns) - 1:
            pieces.append(np.tile(dark, (hgt, gap, 1)))
    sheet = np.concatenate(pieces, axis=1)
    path = _save(sheet, os.path.join(SHOTS, "chest_lineup.png"))
    return path, {key: s for key, _, _, _, _, s in cols}


def render_review(variant, root, parts):
    """One chest: closed front / side / 3-4, open 3-4, and the 64 px silhouette, in one sheet."""
    import numpy as np
    tiles = os.path.join(SHOTS, "tiles")
    os.makedirs(tiles, exist_ok=True)
    st = Stage()
    w, h = 420, 360
    paths = []
    set_open(parts["Lid"], False)
    for view, d in (("front", (0, -1, 0.18)), ("side", (1, 0, 0.18)), ("34", Stage.VIEW_34)):
        paths.append(st.shot(os.path.join(tiles, "%s_review_%s.png" % (variant, view)), TARGET, d, ORTHO_TILE, w, h))
    set_open(parts["Lid"], True)
    paths.append(st.shot(os.path.join(tiles, "%s_review_open.png" % variant), TARGET, Stage.VIEW_34, ORTHO_TILE, w, h))
    set_open(parts["Lid"], False)
    sil = st.silhouette(os.path.join(tiles, "%s_review_sil64.png" % variant), (0, 0, 0.4), ORTHO_SIL)
    row = [_load(p) for p in paths] + [_sil_tile(_load(sil), w, h)]
    return _save(np.concatenate(row, axis=1), os.path.join(SHOTS, "chest_%s.png" % variant))


# --------------------------------------------------------------------------- main

def main():
    variant = str(ARGS.get("variant", "A")).upper()
    if ARGS.get("lineup") == "1":
        variant = "ALL"
    if ARGS.get("export") == "1":
        if variant not in BUILDERS:
            raise ValueError("export=1 needs variant=A|B|C|D")
        K.clear_scene()
        root, parts = build(variant, ROOT_NAME)
        s = stats(root, parts)
        if s["tris"] > TRI_BUDGET:
            raise ValueError("over budget: %d tris" % s["tris"])
        # one file per design (the producer's pick, 2026-10-24: all four, a different one per arena; ArenaLayoutDef.chestModel)
        path = K.export_fbx(root, "A1/Props/" + ROOT_NAME + "_" + variant)
        return {"variant": variant, "fbx": path, "blend": K.save_blend("pr_armorychest_" + variant.lower()), **s}
    if ARGS.get("check") == "1":
        return {v: check(v) for v in (BUILDERS if variant == "ALL" else [variant])}
    K.clear_scene()
    if variant == "ALL":
        order = ["now", "A", "B", "C", "D"]
        x_of = {k: i * 5.0 for i, k in enumerate(order)}
        chests = {"now": build_classic(ROOT_NAME + "_now")}
        chests["now"][0].location.x = x_of["now"]
        for k in order[1:]:
            root, parts = build(k, "%s_%s" % (ROOT_NAME, k))
            root.location.x = x_of[k]
            chests[k] = (root, parts)
        bpy.context.view_layer.update()
        path, s = render_lineup(chests, order, x_of)
        return {"lineup": path, "stats": s}
    if variant not in BUILDERS:
        raise ValueError("variant must be A, B, C, D or all")
    root, parts = build(variant, "%s_%s" % (ROOT_NAME, variant))
    s = stats(root, parts)
    return {"variant": variant, "sheet": render_review(variant, root, parts), **s}


result = main()
