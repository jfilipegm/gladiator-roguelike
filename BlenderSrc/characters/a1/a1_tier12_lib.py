"""a1_tier12_lib — shared helpers for the A1 roster's tier 1-2 enemies (docs/tracks/A1-roster-tier12.md).

The enemies are drawn hundreds at a time (regular <= 450 tris, elites <= 800), so `body()` is a lean copy of
simple_kit.humanoid(): the same variant-A proportions, sections and anchors (oct8 torso/head/fists/feet, hex6 limbs,
flat facets, colour per face), with fewer rings (two-ring legs and fists, a five-ring head, no hidden caps) and a decal
face (quad eyes and brows, a three-triangle wedge nose): ~370 tris with hair and a tunic, against the kit's ~640.
Also: rigid transforms for whole groups of parts (a hunch, an arm swung forward, a scaled-up brute), quadruped lofts
along Y, single-sided decals and face deletion (hood openings). simple_kit itself is unchanged.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K
import simple_kit as A

oct8, hex6, ngon, loft, box, cone, paint, flat = A.oct8, A.hex6, A.ngon, A.loft, A.box, A.cone, A.paint, A.flat


# --------------------------------------------------------------------------- small pieces

def mesh(name, verts, faces, swatch, ox=0.0):
    """A raw flat-shaded mesh from verts (character space, ox added) and faces (index tuples)."""
    bm = bmesh.new()
    vs = [bm.verts.new((x + ox, y, z)) for x, y, z in verts]
    for f in faces:
        bm.faces.new([vs[i] for i in f])
    return flat(K.object_from_bmesh(name, bm, swatch=swatch))


def decal(name, w, h, loc, swatch, ox=0.0, rot_y=0.0, ledge=0.0):
    """A one-sided quad facing -Y (a face's eye or brow), centred at loc, tilted rot_y about Y. ledge > 0 adds a top
    face that deep (a brow's ledge, seen from above)."""
    c, s = math.cos(rot_y), math.sin(rot_y)
    pts = []
    for dx, dz in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)):
        pts.append((loc[0] + dx * c + dz * s, loc[1], loc[2] - dx * s + dz * c))
    faces = [(0, 1, 2, 3)]
    if ledge > 0:
        pts += [(pts[3][0], loc[1] + ledge, pts[3][2]), (pts[2][0], loc[1] + ledge, pts[2][2])]
        faces.append((3, 2, 5, 4))
    o = mesh(name, pts, faces, swatch, ox)
    return o


def wedge_nose(name, w, h, d, loc, swatch, ox=0.0):
    """Three triangles: a wedge nose whose top sits on the face at loc (x, y_face, z_top), pointing -Y."""
    x, y, z = loc
    v = [(x, y, z), (x - w / 2, y, z - h), (x + w / 2, y, z - h), (x, y - d, z - h)]
    return mesh(name, v, [(0, 1, 3), (0, 3, 2), (1, 2, 3)], swatch, ox)


def spike(name, r, h, loc, swatch, ox=0.0, n=4, rot=(0.0, 0.0, 0.0), phase=0.0):
    """A true n-sided pyramid (n + n-2 triangles; simple_kit.cone is a frustum with a tiny top, twice the cost):
    base centred at loc, tip +Z before the object rotation `rot` (Euler XYZ about the base)."""
    v = [(r * math.cos(phase + 2 * math.pi * i / n), r * math.sin(phase + 2 * math.pi * i / n), 0.0) for i in range(n)]
    v.append((0.0, 0.0, h))
    faces = [(i, (i + 1) % n, n) for i in range(n)] + [tuple(reversed(range(n)))]
    o = mesh(name, v, faces, swatch, 0.0)
    o.location = (loc[0] + ox, loc[1], loc[2])
    o.rotation_euler = rot
    return o


def octa(name, r, loc, swatch, ox=0.0, scale=(1.0, 1.0, 1.0)):
    """An octahedron (8 triangles): a faceted stone, a balled net."""
    sx, sy, sz = (r * k for k in scale)
    x, y, z = loc
    v = [(x + sx, y, z), (x - sx, y, z), (x, y + sy, z), (x, y - sy, z), (x, y, z + sz), (x, y, z - sz)]
    f = [(4, 0, 2), (4, 2, 1), (4, 1, 3), (4, 3, 0), (5, 2, 0), (5, 1, 2), (5, 3, 1), (5, 0, 3)]
    return mesh(name, v, f, swatch, ox)


def ring(name, z0, z1, sec, swatch, ox=0.0):
    """An open band (no caps) between z0 and z1 with the same section: belts, cuffs, collars."""
    return loft(name, [(z0, sec), (z1, sec)], swatch, ox, False, False)


def delete_faces(obj, test, ox=0.0):
    """Deletes the faces whose centre (character space) and normal pass test(c, n): hood openings."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    dead = [f for f in bm.faces if test(Vector((f.calc_center_median().x - ox, f.calc_center_median().y,
                                                f.calc_center_median().z)), f.normal)]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def loft_y(name, rings, swatch, ox=0.0, cap_front=True, cap_back=True):
    """A loft along Y (quadruped bodies, heads, snouts, weapons held forward): rings [(y, [(x, z), ...])] from front
    (-Y) to back. Built along Z by simple_kit.loft and turned -90 deg about X."""
    rr = [(y, [(x, -z) for x, z in pts]) for y, pts in rings]
    o = loft(name, rr, swatch, ox, cap_front, cap_back)
    o.data.transform(Matrix.Rotation(-math.pi / 2, 4, "X"))
    o.data.update()
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data)
    bm.free()
    return flat(o)


def loft_x(name, rings, swatch, ox=0.0, cap0=True, cap1=True):
    """A loft along X (crossbars, transverse crests): rings [(x, [(y, z), ...])]. Built along Z and turned +90 deg
    about Y (z -> x)."""
    rr = [(x, [(-z, y) for y, z in pts]) for x, pts in rings]
    o = loft(name, rr, swatch, 0.0, cap0, cap1)
    o.data.transform(Matrix.Rotation(math.pi / 2, 4, "Y"))
    o.data.transform(Matrix.Translation((ox, 0, 0)))
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data)
    bm.free()
    return flat(o)


def bake(obj):
    """Applies an object's own transform into its mesh (so groups can be moved with xform() without surprises)."""
    if obj.type == "MESH":
        obj.data.transform(obj.matrix_basis)
        obj.matrix_basis = Matrix.Identity(4)
    return obj


def xform(objs, mat, pivot, ox=0.0):
    """Applies a rigid/scale matrix about a pivot (character space) to every object in objs whose parent is not
    also in objs (children follow their parents)."""
    piv = Vector((pivot[0] + ox, pivot[1], pivot[2]))
    M = Matrix.Translation(piv) @ mat @ Matrix.Translation(-piv)
    ids = {o.name for o in objs}
    bpy.context.view_layer.update()
    for o in objs:
        if o.parent is not None and o.parent.name in ids:
            continue
        o.matrix_world = M @ o.matrix_world
    bpy.context.view_layer.update()


def rot_x(angle):
    """Rotation about X: negative swings a hanging limb forward (-Y) / tips a body forward with positive."""
    return Matrix.Rotation(angle, 4, "X")


def by_name(parts, *names):
    return [o for o in parts if any(o.name.startswith(n) for n in names)]


# --------------------------------------------------------------------------- the lean body

def body(ox=0.0, p=None, skin="skin_light", skin_shade="skin_mid", top="cloth_white", top_shade=None,
         belt="leather_d", foot="leather_d", hair="mud_dark", legs="skin", leg_colour=None, hair_style="cap",
         sleeves=(), head_top="round", skip=(), torso_cap=True):
    """simple_kit.humanoid's variant-A body with fewer triangles. Same anchors (hand_r/hand_l, hz(f), head_front_y,
    chest_z, waist_z, hem_z, neck_z, shoulder_l/r, back_y, front_y, p) plus arm_x (the arm centres' |x|) and wz (the
    wrist height). top=None: a bare torso; top_shade: a hem band. hair_style "cap" | "none"; head_top "round" (an
    extra ring for bald heads) | "flat" (hidden under hair or a helmet); torso_cap=False drops a bare torso's bottom
    cap (when a loincloth hides it). Faces are added by face()."""
    p = dict(A.CHIBI, **(p or {}))
    H, Sz, SX, N, HH, HW, TW = p["H"], p["S"], p["SX"], p["N"], p["HH"], p["HW"], p["TW"]
    LR, LX, AR, AL, HS, FL = p["LR"], p["LX"], p["AR"], p["AL"], p["HS"], p["FL"]
    FW = p.get("FW", 0.068)
    parts = []
    add = parts.append
    leg_sw = skin if legs == "skin" else (leg_colour or "cloth_sack")
    for s, sx in (("L", 1), ("R", -1)):
        x = LX * sx
        if "Leg" not in skip:
            add(loft("Leg" + s, [(0.06, hex6(LR * 0.66, LR * 0.72, x, 0.004)), (H - 0.06 if top else H + 0.02, hex6(LR, LR * 1.04, x))],
                     leg_sw, ox, False, False))
        if "Foot" not in skip:
            add(loft("Foot" + s, [(0.0, oct8(FW, FL * 0.5, 0.7, x, -0.05)),
                                  (0.075, oct8(FW * 0.8, FL * 0.34, 0.7, x, -0.028))], foot, ox, cap_bottom=False))
    waist, chest = H + (Sz - H) * 0.32, H + (Sz - H) * 0.72
    hem = H - 0.15
    if "Torso" not in skip:
        rings = [(hem, oct8(TW * 1.18, TW * 0.86))] if top else [(H - 0.02, oct8(TW * 0.95, TW * 0.7))]
        if top and top_shade:
            rings.append((hem + 0.07, oct8(TW * 1.08, TW * 0.78)))
        rings += [(waist, oct8(TW * 0.86, TW * 0.62)), (chest, oct8(TW * 1.0, TW * 0.66)),
                  (Sz + 0.03, oct8(TW * 1.06, TW * 0.62)), (N + 0.02, oct8(0.085 * TW / 0.2, 0.075 * TW / 0.2))]
        t = loft("Torso", rings, top or skin, ox, cap_bottom=not top and torso_cap, cap_top=False)
        if top and top_shade:
            paint(t, top_shade, lambda c, n: c.z < hem + 0.06, ox)
        paint(t, skin, lambda c, n: c.z > N - 0.03, ox)
        add(t)
    if belt and "Belt" not in skip:
        add(ring("Belt", waist - 0.03, waist + 0.035, oct8(TW * 0.9, TW * 0.65), belt, ox))
    wz = Sz - AL
    ax = SX + 0.02
    for s, sx in (("L", 1), ("R", -1)):
        x = ax * sx
        if "Arm" not in skip:
            add(loft("Arm" + s, [(wz, hex6(AR * 0.76, AR * 0.74, x, -0.015)),
                                 (Sz - 0.02, hex6(AR * 1.05, AR * 1.02, x - 0.01 * sx)),
                                 (Sz + 0.04, hex6(AR * 0.8, AR * 0.8, x - 0.03 * sx))], skin, ox, cap_bottom=False))
        if "Fist" not in skip:
            add(loft("Fist" + s, [(wz - HS * 1.5, oct8(HS * 0.9, HS * 1.0, 0.65, x, -0.022)),
                                  (wz + 0.005, oct8(HS * 0.85, HS * 0.92, 0.65, x, -0.018))], skin, ox,
                     cap_bottom=False))
        if s in sleeves and top:
            add(loft("Sleeve" + s, [(Sz - 0.13, oct8(AR * 1.42, AR * 1.36, 0.66, x - 0.005 * sx)),
                                    (Sz + 0.06, oct8(AR * 1.05, AR * 1.05, 0.66, x - 0.03 * sx))], top, ox, False,
                     False))

    def hz(f):
        return N + HH * f
    ry = HW * 0.93
    if "Head" not in skip:
        rings = [(hz(0.04), oct8(HW * 0.46, ry * 0.44, 0.66, 0, -ry * 0.18)),
                 (hz(0.17), oct8(HW * 0.82, ry * 0.8, 0.64, 0, -ry * 0.06)),
                 (hz(0.42), oct8(HW, ry, 0.62)), (hz(0.70), oct8(HW, ry, 0.62))]
        if head_top == "round":
            rings += [(hz(0.90), oct8(HW * 0.82, ry * 0.82, 0.6)), (hz(1.0), oct8(HW * 0.4, ry * 0.4, 0.6))]
        else:
            rings += [(hz(0.97), oct8(HW * 0.6, ry * 0.6, 0.6))]
        h = loft("Head", rings, skin, ox, cap_bottom=False)
        paint(h, skin_shade, lambda c, n: c.z < hz(0.2) and n.y < -0.3, ox)
        if hair_style == "cap":
            paint(h, hair, lambda c, n: c.z > hz(0.15) and n.y > 0.35, ox)
        add(h)
        if hair_style == "cap":
            add(loft("Hair", [(hz(0.70), oct8(HW * 1.06, ry * 1.06, 0.6)), (hz(0.92), oct8(HW * 0.88, ry * 0.88, 0.6)),
                              (hz(1.05), oct8(HW * 0.42, ry * 0.42, 0.6))], hair, ox, cap_bottom=False))
    a = dict(p=p, hand_r=(-ax, -0.03, wz - HS * 0.5), hand_l=(ax, -0.03, wz - HS * 0.5), arm_x=ax, wz=wz,
             head_top=hz(1.0), head_front_y=-ry, head_back_y=ry, head_w=HW, brow_z=hz(0.6), eye_z=hz(0.51),
             band_z=(hz(0.64), hz(0.73)), chest_z=chest, waist_z=waist, hem_z=hem, neck_z=N,
             shoulder_l=(ax, 0.0, Sz + 0.02), shoulder_r=(-ax, 0.0, Sz + 0.02), back_y=TW * 0.66, front_y=-TW * 0.66,
             hz=hz)
    return {"parts": parts, "a": a}


def face(a, ox=0.0, eyes="ink", brows="mud_dark", nose="skin_mid", brow_angle=0.3, eye_w=0.24, eye_h=0.075,
         eye_z=None, brow_z=None, mouth=None, mouth_w=0.36, brow_w=0.48, brow_h=0.06, brow_ledge=0.012):
    """The variant-A minimal face as decals: two slit eyes, two angled brows (None: none), a wedge nose (None: none),
    an optional mouth bar. Positive brow_angle = angry (outer ends up)."""
    HW, HH, ry = a["head_w"], a["p"]["HH"], a["head_back_y"]
    hz = a["hz"]
    out = []
    ez = a["eye_z"] if eye_z is None else eye_z
    bz = a["brow_z"] if brow_z is None else brow_z
    for s, x, sg in (("L", HW * 0.38, 1), ("R", -HW * 0.38, -1)):
        out.append(decal("Eye" + s, HW * eye_w, HH * eye_h, (x, -ry - 0.004, ez), eyes, ox))
        if brows:
            out.append(decal("Brow" + s, HW * brow_w, HH * brow_h, (x, -ry - 0.008, bz), brows, ox,
                             rot_y=-brow_angle * sg, ledge=brow_ledge))
    if nose:
        x, y, z = 0.0, face_y(a, 0.47), hz(0.47)
        yb = face_y(a, 0.33)
        w, d = HW * 0.24, 0.045
        v = [(x, y + 0.004, z), (x - w / 2, yb + 0.004, hz(0.33)), (x + w / 2, yb + 0.004, hz(0.33)),
             (x, yb - d, hz(0.33))]
        out.append(mesh("Nose", v, [(0, 1, 3), (0, 3, 2), (1, 2, 3)], nose, ox))
    if mouth:
        out.append(decal("Mouth", HW * mouth_w, HH * 0.05, (0, face_y(a, 0.24) - 0.006, hz(0.24)), mouth, ox))
    return out


def face_y(a, f):
    """The head's front surface y at height fraction f (the lean head's rings: chin 0.04, jaw 0.17, face 0.42-0.70)."""
    ry = a["head_back_y"]
    pts = ((0.04, -0.62 * ry), (0.17, -0.86 * ry), (0.42, -ry), (0.70, -ry))
    if f <= pts[0][0]:
        return pts[0][1]
    for (f0, y0), (f1, y1) in zip(pts, pts[1:]):
        if f <= f1:
            return y0 + (y1 - y0) * (f - f0) / (f1 - f0)
    return -ry


def tris(objs):
    return K.tri_count([o for o in objs if o.type == "MESH"])


# --------------------------------------------------------------------------- the game models (export=1)

def move_joints(J, names, mat, pivot):
    """Applies the same rigid transform a script gave a group of pieces (xform(objs, mat, pivot)) to the joints
    `names` of J (a1_export's {"Body": Vector, ...}, character space, ox = 0)."""
    piv = Vector(pivot)
    M = Matrix.Translation(piv) @ mat @ Matrix.Translation(-piv)
    for k in names:
        if k in J:
            J[k] = M @ Vector(J[k])
    return J


def fit_height(parts, J, target, tol=0.08):
    """In-game size: if the built height is more than `tol` off the original's (A1-rigs.json `height`), scales the
    pieces and the joints about the origin (the soles) to exactly that height. Returns (scale, built height)."""
    bpy.context.view_layer.update()
    zs = [(o.matrix_world @ v.co).z for o in parts if o.type == "MESH" for v in o.data.vertices]
    h = max(zs) - min(zs)   # exact (a rotated piece's bound_box overshoots)
    s = 1.0 if abs(h / target - 1.0) <= tol else target / h
    if s != 1.0:
        xform(parts, Matrix.Scale(s, 4), (0.0, 0.0, 0.0), 0.0)
        for k in list(J):
            J[k] = Vector(J[k]) * s
    return round(s, 4), round(h, 3)


def export_enemy(root, parts, J, height, rules):
    """fit_height() then a1_export.export() into Assets/Art/Models/A1/Enemies/<root>.fbx (+ the pose sheet).
    `height`: the original's (docs/tracks/A1-rigs.json)."""
    import a1_export as X
    s, h = fit_height(parts, J, height)
    info = X.export(root, "Enemies", parts, J, rules=rules)
    info.update(fit_scale=s, built_height=h, original_height=height)
    return info
