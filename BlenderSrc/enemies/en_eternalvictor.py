"""EN_EternalVictor — the Eternal Victor, the Colosseum's boss (docs/BOSSES.md). Budget <= 2,500 tris.

The arena's first champion, four hundred years dead and still on his feet: tall and gaunt (~3.5 m to the crest, the
tallest and thinnest boss), bone limbs in archaic bronze: a muscled cuirass over a wasp waist where the bare spine
shows, tall thraex greaves over bone shins, one great flared shoulder guard (left) and a segmented manica (right
forearm). A thraex helm with a wide brim, a visor with nothing but darkness behind its grille, a bone jaw under it,
a gold laurel wreath round the bowl and a griffin crest that curls forward over the face like a hook. A cloak of
tattered crimson victory ribbons down his back; a magenta sash (the enemy accent). The falx: a ~2.6 m two-handed
crescent held low across his body, right fist at the pommel, left fist lower on the haft, the blade rising beside his
left leg. The hook of the crest and the crescent of the blade are his read: no disc, no wings, no shield.

Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR), every one with an identity bind rotation at its joint
(so PartPose offsets act in character space), plus:
  Falx    child of ArmR, pivot at the grip in the right fist. Built FLAT in its own frame: the haft along local -Y
          (Unity +Z), the crescent in the local XY plane (Unity XZ), so a world rotation of pure yaw lays it flat for
          the throw. At bind it is turned into the held pose.
  Crest   child of Head, pivot at its base on the helm top.
  Laurel  child of Head, pivot at the wreath's centre.
  Cloak   child of Body, pivot at the shoulders (top centre of the ribbons' yoke).
Both shoulder pivots lie on one X line (same height and depth): equal Swing on ArmL and ArmR keeps the left fist on
the haft. Origin at the feet; front = -Y (Unity +Z).
"""
import importlib
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 1.38
SH_Z, SH_X = 2.14, 0.45      # shoulder joints (both on one X line)
NECK_Z = 2.27
UPPER, FORE = 0.60, 0.58     # bone arm lengths (shoulder-elbow, elbow-wrist)
GRIP_R = Vector((-0.30, -0.44, 1.46))   # right fist on the haft (the Falx pivot)
GRIP_L = Vector((0.26, -0.5, 1.2))      # left fist, lower on the haft
BLADE_ROLL = -30.0                      # the blade's plane rolled about the haft: its tip swings back, clear of him
HAFT_BACK, HAFT_FWD = 0.13, 0.98        # haft behind the right fist / ahead of it to the blade's root
BLADE_R, BLADE_SPAN, BLADE_W = 0.72, 148.0, 0.36   # crescent: centre-line radius, arc (deg), width at the root


def place(obj, loc, rot=None):
    """Set obj's WORLD transform (its parent may be rotated). rot: Euler tuple, Quaternion or 3x3 Matrix."""
    bpy.context.view_layer.update()
    if rot is None:
        m3 = Matrix.Identity(3)
    elif isinstance(rot, Matrix):
        m3 = rot
    elif hasattr(rot, "to_matrix"):
        m3 = rot.to_matrix()
    else:
        from mathutils import Euler
        m3 = Euler(rot).to_matrix()
    obj.matrix_world = Matrix.Translation(Vector(loc)) @ m3.to_4x4()
    bpy.context.view_layer.update()
    return obj


def seg(name, p0, p1, w, d, swatch, parent, taper=(1.0, 1.0), verts=0):
    """A box (or a `verts`-sided prism) from world p0 to p1, origin at p0, its local +Z along the segment."""
    p0, p1 = Vector(p0), Vector(p1)
    length = (p1 - p0).length
    if verts:
        obj = K.cylinder(name, radius=w / 2, depth=length, verts=verts, pivot=(0, 0, -1), taper=taper[0],
                         swatch=swatch, parent=parent)
    else:
        obj = K.box(name, size=(w, d, length), pivot=(0, 0, -1), taper=taper, swatch=swatch, parent=parent)
    return place(obj, p0, (p1 - p0).normalized().to_track_quat("Z", "Y"))


def spin_mesh(obj, angle):
    """Turn obj's mesh (not the object) about its local Z: octagons squared to the front, the part's bind rotation
    left at identity."""
    obj.data.transform(Matrix.Rotation(angle, 4, "Z"))
    return obj


def wbox(name, size, loc, swatch, parent, pivot=(0, 0, 0), taper=(1.0, 1.0), bevel=0.0, rot=None):
    """K.box placed at a WORLD location (rot as in place())."""
    obj = K.box(name, size=size, pivot=pivot, taper=taper, bevel=bevel, swatch=swatch, parent=parent)
    return place(obj, loc, rot)


def elbow(shoulder, hand, pole):
    """Two-bone IK: the elbow for UPPER/FORE between shoulder and hand, bent toward `pole`."""
    s, t = Vector(shoulder), Vector(hand)
    d = t - s
    dist = min(d.length, UPPER + FORE - 1e-3)
    dn = d.normalized()
    x = (UPPER * UPPER - FORE * FORE + dist * dist) / (2 * dist)
    h = math.sqrt(max(UPPER * UPPER - x * x, 0.0))
    p = Vector(pole) - Vector(pole).dot(dn) * dn
    return s + dn * x + p.normalized() * h


def falx_frame():
    """World rotation of the Falx at bind: local -Y along the haft (down-left across his body), local +X 'up' in the
    blade's plane, local +Z the plane's normal."""
    d = (GRIP_L - GRIP_R).normalized()
    up = Vector((0, 0, 1)) - Vector((0, 0, 1)).dot(d) * d
    up.normalize()
    r = math.radians(BLADE_ROLL)
    up = up * math.cos(r) + d.cross(up) * math.sin(r)
    x, y = up, -d
    z = x.cross(y)
    return Matrix((x, y, z)).transposed()


def crescent(name, parent):
    """The falx blade, flat in the Falx's local XY plane: from the haft's end (u = HAFT_FWD on local -Y) it curves
    toward local +X through BLADE_SPAN degrees, tapering to a point. The inner (concave) band is the bright edge."""
    bm = bmesh.new()
    band_layer = bm.faces.layers.int.new("band")
    n = 11
    half_t = 0.025
    cu, cv = HAFT_FWD, BLADE_R  # arc centre in (u, v); local = (v, -u, z)
    rows = []
    for k in range(n + 1):
        t = k / n
        a = math.radians(-90.0 + BLADE_SPAN * t)
        w = BLADE_W * (1.0 - t) ** 0.75
        rr = (BLADE_R - w * 0.30, BLADE_R - w * 0.05, BLADE_R + w * 0.70)  # edge / body / back (inner -> outer)
        row = []
        for r in rr:
            u, v = cu + r * math.cos(a), cv + r * math.sin(a)
            row.append((bm.verts.new((v, -u, -half_t)), bm.verts.new((v, -u, half_t))))
        rows.append(row)

    def face(vs, band):
        f = bm.faces.new(vs)
        f[band_layer] = band
        return f

    for k in range(n):
        a, b = rows[k], rows[k + 1]
        for band in (0, 1):  # 0 = edge band (inner), 1 = body band
            face([a[band][1], a[band + 1][1], b[band + 1][1], b[band][1]], band)
            face([a[band][0], b[band][0], b[band + 1][0], a[band + 1][0]], band)
        face([a[0][0], a[0][1], b[0][1], b[0][0]], 0)  # inner rim (the cutting edge)
        face([a[2][1], a[2][0], b[2][0], b[2][1]], 1)  # outer rim (the back)
    r0 = rows[0]
    face([r0[0][0], r0[2][0], r0[2][1], r0[0][1]], 1)  # root cap
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)  # the tip collapses to a point
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    edge = [f.index for f in bm.faces if f[band_layer] == 0]
    bm.faces.ensure_lookup_table()
    edge = [i for i, f in enumerate(bm.faces) if f[band_layer] == 0]
    obj = K.object_from_bmesh(name, bm, (0, 0, 0), parent, "steel")
    K.set_swatch(obj, "steel_l", edge)
    return obj


def build():
    K.clear_scene()
    root = K.empty("EN_EternalVictor")

    # ---- legs: bare bone thighs, knee knobs, tall flared bronze greaves, bone feet
    for side, x in (("L", 0.19), ("R", -0.19)):
        leg = wbox("Leg" + side, (0.085, 0.085, HIP_Z - 0.76), (x, 0, HIP_Z), "bone", root, pivot=(0, 0, 1))
        wbox("Knee" + side, (0.13, 0.13, 0.12), (x, -0.01, 0.78), "ash", leg)
        greave = K.cylinder("Greave" + side, radius=0.07, depth=0.7, verts=6, pivot=(0, 0, -1), taper=1.55,
                            swatch="bronze", parent=leg)
        place(greave, (x, 0.0, 0.1))
        wbox("KneeCap" + side, (0.17, 0.05, 0.17), (x, -0.115, 0.76), "bronze_l", leg, taper=(1.15, 1.0))
        wbox("Foot" + side, (0.12, 0.30, 0.08), (x, -0.07, 0.0), "bone", leg, pivot=(0, 0, -1), taper=(0.8, 0.9))

    # ---- body: the belt at the hip, a wasp waist of bare spine, the bronze muscled cuirass
    body = wbox("Body", (0.5, 0.36, 0.13), (0, 0, HIP_Z), "bronze_d", root, pivot=(0, 0, -1))
    wbox("Sash", (0.54, 0.4, 0.09), (0, 0, HIP_Z + 0.07), "accent_magenta", body, rot=(0, 0.14, 0))
    wbox("SashEnd", (0.12, 0.04, 0.46), (0.19, -0.2, HIP_Z + 0.08), "accent_magenta", body, pivot=(0, 0, 1),
         taper=(1.4, 1.0), rot=(0.1, 0, 0.1))
    wbox("Kilt", (0.38, 0.28, 0.2), (0, 0, HIP_Z + 0.01), "crimson_d", body, pivot=(0, 0, 1), taper=(1.25, 1.25))
    for k, x in enumerate((-0.14, 0.0, 0.14)):  # bronze-studded leather straps over the kilt's front
        wbox("Strap%d" % k, (0.09, 0.03, 0.3), (x, -0.19, HIP_Z), "bronze_d", body, pivot=(0, 0, 1))
    wbox("Spine", (0.1, 0.1, 0.2), (0, 0.04, HIP_Z + 0.11), "bone", body, pivot=(0, 0, -1))
    wbox("Ribs", (0.28, 0.18, 0.06), (0, 0.0, HIP_Z + 0.25), "ash", body, taper=(1.3, 1.0))
    cuirass = wbox("Cuirass", (0.42, 0.32, 0.58), (0, 0.0, HIP_Z + 0.27), "bronze", body, pivot=(0, 0, -1),
                   taper=(1.76, 1.34), bevel=0.03)
    for side, x in (("L", 0.14), ("R", -0.14)):
        wbox("Pec" + side, (0.28, 0.07, 0.19), (x, -0.225, HIP_Z + 0.62), "bronze_l", cuirass, taper=(0.9, 1.0))
    wbox("Abs", (0.22, 0.06, 0.22), (0, -0.19, HIP_Z + 0.42), "bronze_l", cuirass, taper=(1.25, 1.0))
    wbox("Neck", (0.11, 0.11, 0.16), (0, -0.01, NECK_Z - 0.06), "bone", body, pivot=(0, 0, -1))

    # ---- the cloak: tattered crimson victory ribbons hanging from a yoke across his back
    cloak = wbox("Cloak", (0.78, 0.1, 0.1), (0, 0.23, SH_Z + 0.04), "crimson_d", body)
    lengths = (1.62, 2.0, 1.42, 1.9, 2.06, 1.55, 1.96, 1.38)
    for k, ln in enumerate(lengths):
        x = -0.36 + k * 0.103
        w = 0.08
        outline = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, -ln), (0.0, -ln + 0.13), (-w / 2, -ln + 0.03 * (k % 2))]
        rib = K.extrude_poly("Ribbon%d" % k, outline, thickness=0.025, plane="XZ", swatch="crimson" if k % 2 else
                             "crimson_d", parent=cloak)
        place(rib, (x, 0.26, SH_Z + 0.03), (0.12 + 0.02 * (k % 3), -0.26 * x, 0.0))

    # ---- head: a round thraex helm (the Head part is its bowl, origin at the neck), a broad brim, darkness behind
    # the visor's grille, a bone jaw under it
    head = K.cylinder("Head", radius=0.29, depth=0.6, verts=8, pivot=(0, 0, -1), taper=0.8, swatch="bronze",
                      parent=body)
    place(spin_mesh(head, math.pi / 8), (0, -0.02, NECK_Z))
    dome = K.cylinder("Dome", radius=0.232, depth=0.12, verts=8, pivot=(0, 0, -1), taper=0.45, swatch="bronze",
                      parent=head)
    place(spin_mesh(dome, math.pi / 8), (0, -0.02, NECK_Z + 0.6))
    brim = K.cylinder("Brim", radius=0.42, depth=0.05, verts=8, pivot=(0, 0, 0), taper=0.8, swatch="bronze_d",
                      parent=head)
    place(spin_mesh(brim, math.pi / 8), (0, 0.0, NECK_Z + 0.38), (-0.1, 0, 0))  # tipped up at the front
    wbox("NeckFlare", (0.56, 0.12, 0.3), (0, 0.3, NECK_Z + 0.1), "bronze_d", head, rot=(-0.45, 0, 0))
    wbox("Visor", (0.44, 0.07, 0.36), (0, -0.28, NECK_Z + 0.02), "bronze_d", head, pivot=(0, 0, -1))
    wbox("Dark", (0.34, 0.03, 0.17), (0, -0.32, NECK_Z + 0.21), "ink", head)
    for k, x in enumerate((-0.1, 0.0, 0.1)):
        wbox("Grille%d" % k, (0.03, 0.03, 0.2), (x, -0.335, NECK_Z + 0.21), "bronze_l", head)
    wbox("Jaw", (0.3, 0.2, 0.08), (0, -0.19, NECK_Z - 0.02), "bone", head)
    wbox("Teeth", (0.24, 0.02, 0.03), (0, -0.295, NECK_Z + 0.0), "ink", head)

    # the griffin crest: up from the back of the bowl, over, and curling forward into a hooked beak far in front of
    # the visor (an open hook: a gap between the beak and the crest's neck so it reads from 3/4 too)
    cz = NECK_Z + 0.68
    hook = [(0.24, -0.04), (0.27, 0.16), (0.2, 0.36), (0.06, 0.5), (-0.14, 0.58), (-0.36, 0.57), (-0.52, 0.48),
            (-0.6, 0.33), (-0.6, 0.16), (-0.54, 0.02), (-0.47, 0.15), (-0.46, 0.27), (-0.39, 0.36), (-0.26, 0.38),
            (-0.15, 0.31), (-0.09, 0.15), (-0.08, -0.04)]
    hook = [(y * 1.12, z * 1.12) for y, z in hook]  # iteration 3: 12 % bigger
    crest = K.extrude_poly("Crest", hook, thickness=0.14, plane="YZ", swatch="bronze_l", parent=head)
    place(crest, (0, 0.04, cz))
    K.set_swatch_where(crest, "bronze", lambda c, n: abs(n.x) < 0.5)
    gh = wbox("GriffinHead", (0.22, 0.26, 0.22), (0, 0.04 - 0.56, cz + 0.4), "bronze_l", crest, taper=(0.8, 0.8))
    for side, x in (("L", 0.085), ("R", -0.085)):
        wbox("GriffinEye" + side, (0.06, 0.06, 0.05), (x, 0.04 - 0.6, cz + 0.45), "ink", gh)
    for k in range(3):  # a crimson fringe down the crest's back edge
        f = K.box("Fringe%d" % k, size=(0.08, 0.18, 0.06), pivot=(0, -1, 0), taper=(0.5, 0.5), swatch="crimson",
                  parent=crest)
        place(f, (0, 0.04 + (0.22, 0.28, 0.22)[k], cz + (0.47, 0.27, 0.05)[k]), (0.55 - 0.4 * k, 0, 0))

    # the gold laurel: a band round the bowl, two rows of leaves running from the back to meet at the front
    lz = NECK_Z + 0.46
    laurel = K.cylinder("Laurel", radius=0.27, depth=0.06, verts=8, pivot=(0, 0, 0), swatch="gold_d", parent=head,
                        cap=False)
    place(spin_mesh(laurel, math.pi / 8), (0, -0.02, lz))
    for side in (1, -1):
        for k in range(6):
            phi = math.radians(20 + k * 29)
            px, py = side * 0.275 * math.sin(phi), -0.02 + 0.285 * math.cos(phi)
            tangent = Vector((side * math.cos(phi), -math.sin(phi), 0.0))
            out = Vector((side * math.sin(phi), math.cos(phi), 0.0))
            row = k % 2
            dirv = (tangent + Vector((0, 0, 0.55 if row else 0.2)) + out * 0.45).normalized()
            leaf = K.box("Leaf%s%d" % ("L" if side > 0 else "R", k), size=(0.085, 0.2, 0.025), pivot=(0, 1, 0),
                         taper=(0.35, 1.0), swatch="gold_l" if row else "gold", parent=laurel)
            place(leaf, (px, py, lz + (0.025 if row else -0.015)), dirv.to_track_quat("-Y", "Z"))
    for side, x in (("L", 0.05), ("R", -0.05)):  # the wreath's ribbon tails at the back
        t = K.box("Tie" + side, size=(0.05, 0.02, 0.28), pivot=(0, 0, 1), swatch="gold_d", parent=laurel)
        place(t, (x, 0.27, lz), (-0.35, x * 4, 0))

    # ---- arms: bare bone, bent at the elbow to hold the falx; a great shoulder guard (L), a manica (R)
    arms = {}
    for side, sx, grip in (("L", SH_X, GRIP_L), ("R", -SH_X, GRIP_R)):
        sh = Vector((sx, 0.0, SH_Z))
        arm = wbox("Arm" + side, (0.15, 0.15, 0.15), sh, "bone", body)
        el = elbow(sh, grip, (math.copysign(0.6, sx), 0.6, -0.2))
        seg("Humerus" + side, sh, el, 0.075, 0.075, "bone", arm)
        wbox("Elbow" + side, (0.1, 0.1, 0.1), el, "ash", arm)
        seg("Forearm" + side, el, grip, 0.07, 0.07, "bone", arm, taper=(1.25, 1.25))
        d = (grip - el).normalized()
        wbox("Fist" + side, (0.13, 0.13, 0.15), grip + d * 0.02, "bone", arm, rot=d.to_track_quat("Z", "Y"))
        arms[side] = (arm, el, grip)
    arm_l, _, _ = arms["L"]
    for k in range(3):  # layered bronze plates down the left shoulder
        pl = K.box("Pauldron%d" % k, size=(0.3 - 0.03 * k, 0.32 - 0.03 * k, 0.07), pivot=(0, 0, 0), taper=(0.8, 0.85),
                   swatch="bronze" if k != 1 else "bronze_d", parent=arm_l)
        place(pl, (SH_X + 0.05 + 0.03 * k, 0.0, SH_Z + 0.06 - 0.11 * k), (0, -0.45, 0))
    # the great shoulder guard (a galerus): a flared plate standing up from the left shoulder beside the helm, its
    # top edge sweeping out to a point; its face turned to the front-left so it reads from the front and 3/4
    galerus = [(-0.06, 0.0), (0.14, 0.0), (0.24, 0.2), (0.34, 0.42), (0.38, 0.56), (0.22, 0.48), (0.08, 0.46),
               (-0.04, 0.4), (-0.09, 0.2)]
    guard = K.extrude_poly("Guard", galerus, thickness=0.05, plane="XZ", swatch="bronze_l", parent=arm_l)
    place(guard, (SH_X + 0.02, -0.03, SH_Z - 0.04), (0, -0.12, 0.62))
    K.set_swatch_where(guard, "bronze_d", lambda c, n: abs(n.y) < 0.5)
    arm_r, el_r, grip_r = arms["R"]
    d = (grip_r - el_r)
    for k in range(3):  # the manica: three bronze rings down the right forearm
        ring = K.cylinder("Manica%d" % k, radius=0.07 - 0.005 * k, depth=0.11, verts=6, pivot=(0, 0, -1),
                          taper=0.85, swatch="bronze" if k % 2 == 0 else "bronze_l", parent=arm_r)
        place(ring, el_r + d * (0.12 + 0.24 * k), d.normalized().to_track_quat("Z", "Y"))

    # ---- the falx: built flat (haft along local -Y from the grip), then turned into the held pose
    falx = K.cylinder("Falx", radius=0.035, depth=HAFT_BACK + HAFT_FWD, verts=6, swatch="bronze_d", parent=arm_r,
                      axis="Y", pivot=(0, 0, 2 * (HAFT_FWD - (HAFT_BACK + HAFT_FWD) / 2) / (HAFT_BACK + HAFT_FWD)))
    # the pivot formula puts the cylinder's span on local y in [-HAFT_FWD, +HAFT_BACK] (see K.cylinder's pivot)
    pommel = K.box("Pommel", size=(0.09, 0.09, 0.09), pivot=(0, 0, 0), swatch="bronze_l", parent=falx)
    pommel.location = (0, HAFT_BACK + 0.03, 0)
    wrap = K.cylinder("Wrap", radius=0.045, depth=0.36, verts=6, pivot=(0, 0, 0), swatch="leather_d", parent=falx,
                      axis="Y")
    wrap.location = (0, -0.27, 0)
    collar = K.box("Collar", size=(0.12, 0.1, 0.12), pivot=(0, 0, 0), swatch="bronze_l", parent=falx)
    collar.location = (0, -HAFT_FWD + 0.03, 0)
    crescent("Blade", falx)
    place(falx, GRIP_R, falx_frame())
    return root


root = build()
bpy.context.view_layer.update()
bpy.context.view_layer.update()

falx = bpy.data.objects["Falx"]
fl_lo = Vector((1e9, 1e9, 1e9))
fl_hi = -fl_lo
inv = falx.matrix_world.inverted()
for o in K.descendants(falx):
    for v in o.data.vertices:
        p = inv @ (o.matrix_world @ v.co)
        fl_lo = Vector(map(min, fl_lo, p))
        fl_hi = Vector(map(max, fl_hi, p))
fl_c = (fl_lo + fl_hi) / 2


def unity(v):
    """Blender (x, y, z) -> Unity (-x, z, -y): the export's axis change (front -Y -> +Z, left +X -> -X)."""
    return [round(-v.x, 3), round(v.z, 3), round(-v.y, 3)]


PARTS = {o.name: unity(o.matrix_world.translation) for o in K.descendants(root)
         if o.name in ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Falx", "Crest", "Laurel", "Cloak")}
info = K.review_render(root, "en_eternalvictor", size=480)
if ARGS.get("export", "1") == "1":
    info["fbx"] = K.export_fbx(root, "Enemies/EN_EternalVictor")
    info["blend"] = K.save_blend("en_eternalvictor")
info["tris"] = K.tri_count(K.descendants(root))
info["parts_unity"] = PARTS
info["falx_tris"] = K.tri_count(K.descendants(falx))
info["falx_local_bounds_unity"] = [unity(fl_lo), unity(fl_hi)]
info["falx_local_centre_unity"] = unity(fl_c)
info["grip_l_unity"] = unity(GRIP_L)
info["part_rot_deg"] = {o.name: [round(math.degrees(a), 1) for a in o.matrix_world.to_euler()] for o in
                        K.descendants(root) if o.name in PARTS}
info["names"] = sorted(o.name for o in K.descendants(root))
result = info
