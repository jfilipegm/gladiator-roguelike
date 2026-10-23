"""simple_kit — the A1 "simple" character style (docs/tracks/A1-roster-contract.md): the producer's pick, lineup variant A.

Style rules (what made variant A work; keep them):
  * chibi proportions on CH_Nobody's joints (hips 0.40, shoulders 0.86, neck 0.95, the head ~37 % of the height);
  * OSRS-simple shapes: chamfered boxy sections (oct8) and plain tapered limbs (hex6), no muscles, no anatomy;
  * FLAT-faceted shading (every face flat), colour per face from the shared palette, few clear clothing pieces;
  * a minimal face: two dark slit eyes, angled brows, a small wedge nose;
  * identity by silhouette, headgear, weapon and one accent colour, not by detail.
Everything is built in world space at x offset `ox` (so several characters can stand in one scene), front = -Y,
the character's left = +X, 1 unit = 1 m. Meshes only (the rig comes later, if a design is approved).
Outputs: review sheets (the original beside the new one, front / back / three-quarter) in SHOTS (shared by all
worktrees, git-ignored).
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K

SHOTS = "/home/jfilipegm/Gladiator Roguelike/Screenshots/a1"

# CH_Nobody's joints and the variant-A proportions (scale these per character, e.g. a brute's TW and AR up)
CHIBI = dict(H=0.40, S=0.86, SX=0.265, N=0.95, HH=0.60, HW=0.265, TW=0.20, LR=0.088, LX=0.105, AR=0.068,
             AL=0.335, HS=0.072, FL=0.25)


# --------------------------------------------------------------------------- sections and pieces

def oct8(rx, ry, k=0.62, cx=0.0, cy=0.0, front=1.0):
    """A chamfered box section, 8 points CCW from +X: flat front/back/sides, cut corners (k = corner inset)."""
    f = ry * front
    pts = [(rx, -f * 0.42), (rx, ry * 0.42), (rx * k, ry), (-rx * k, ry), (-rx, ry * 0.42), (-rx, -f * 0.42),
           (-rx * k, -f), (rx * k, -f)]
    return [(x + cx, y + cy) for x, y in pts]


def hex6(rx, ry, cx=0.0, cy=0.0):
    """A limb section, 6 points CCW, flat front and back."""
    return [(cx + rx, cy), (cx + rx * 0.5, cy + ry), (cx - rx * 0.5, cy + ry), (cx - rx, cy),
            (cx - rx * 0.5, cy - ry), (cx + rx * 0.5, cy - ry)]


def ngon(n, r, cx=0.0, cy=0.0, phase=0.0, ry=None):
    ry = r if ry is None else ry
    return [(cx + r * math.cos(phase + 2 * math.pi * i / n), cy + ry * math.sin(phase + 2 * math.pi * i / n))
            for i in range(n)]


def flat(obj):
    for f in obj.data.polygons:
        f.use_smooth = False
    return obj


def loft(name, rings, swatch, ox=0.0, cap_bottom=True, cap_top=True):
    """Rings [(z, [(x, y), ...]), ...] bottom to top (same point count), joined by quads; flat-shaded."""
    bm = bmesh.new()
    loops = [[bm.verts.new((x + ox, y, z)) for x, y in pts] for z, pts in rings]
    n = len(loops[0])
    for a, b in zip(loops, loops[1:]):
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    if cap_bottom:
        bm.faces.new(list(reversed(loops[0])))
    if cap_top:
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return flat(K.object_from_bmesh(name, bm, swatch=swatch))


def box(name, size, loc, swatch, ox=0.0, rot=(0, 0, 0), pivot=(0, 0, 0), taper=(1.0, 1.0)):
    o = K.box(name, size=size, location=(loc[0] + ox, loc[1], loc[2]), pivot=pivot, taper=taper, swatch=swatch)
    o.rotation_euler = rot
    return flat(o)


def cone(name, radius, depth, loc, swatch, ox=0.0, verts=4, rot=(0, 0, 0), pivot=(0, 0, -1)):
    o = K.cone(name, radius=radius, depth=depth, verts=verts, location=(loc[0] + ox, loc[1], loc[2]), pivot=pivot,
               swatch=swatch)
    o.rotation_euler = rot
    return flat(o)


def paint(obj, swatch, test, ox=0.0):
    """Faces whose centre (relative to the character, x without ox) and normal pass test(c, n) get the swatch."""
    K.set_swatch_where(obj, swatch, lambda c, n: test(Vector((c.x - ox, c.y, c.z)), n))


# --------------------------------------------------------------------------- the base humanoid

def humanoid(ox=0.0, p=None, skin="skin_light", skin_shade="skin_mid", top="cloth_white", top_shade="ash",
             belt="leather_d", foot="skin_light", sole="leather_d", hair="mud_dark", eyes="ink", brows=None,
             bare_right_shoulder=False, sleeves=(), legs="skin", leg_colour=None, hair_style="cap",
             face=True, skip=()):
    """The variant-A body. Returns {"parts": [objects], "a": anchors} where anchors (character space, add ox
    yourself or use the `ox=` of the helpers) give: hand_r / hand_l (fist centres), head_top, head_front_y,
    brow_z, eye_z, chest_z, waist_z, hem_z, shoulder_l / shoulder_r (tops), back_y (the torso's back surface),
    and the proportions p. Options:
      top: the tunic/shirt colour (None = no tunic: a bare torso in `skin`); top_shade for its hem band;
      bare_right_shoulder: the exomis (the Nobody); sleeves: ("L",) / ("L", "R") short sleeves in `top`;
      legs: "skin" (bare) or "trousers" (leg_colour); hair_style: "cap" | "none" (bald / helmeted heads);
      face: False skips the eyes/brows/nose (masks, helmets that cover the face); skip: part names to leave out
      ("Belt", "Buckle", "Strap", "Foot", ...)."""
    p = dict(CHIBI, **(p or {}))
    H, Sz, SX, N, HH, HW, TW = p["H"], p["S"], p["SX"], p["N"], p["HH"], p["HW"], p["TW"]
    LR, LX, AR, AL, HS, FL = p["LR"], p["LX"], p["AR"], p["AL"], p["HS"], p["FL"]
    parts = []
    add = parts.append
    leg_sw = skin if legs == "skin" else (leg_colour or "cloth_sack")
    for s, sx in (("L", 1), ("R", -1)):
        x = LX * sx
        if "Leg" not in skip:
            add(loft("Leg" + s, [(0.08, hex6(LR * 0.62, LR * 0.68, x, 0.004)),
                                 (H * 0.5, hex6(LR * 0.80, LR * 0.86, x, -0.006)),
                                 (H + 0.02, hex6(LR * 1.0, LR * 1.04, x))], leg_sw, ox, cap_top=False))
        if "Foot" not in skip:
            f = loft("Foot" + s, [(0.0, oct8(0.068, FL * 0.5, 0.7, x, -0.05)),
                                  (0.05, oct8(0.062, FL * 0.45, 0.7, x, -0.045)),
                                  (0.10, oct8(0.05, 0.06, 0.7, x, 0.0))], foot, ox)
            paint(f, sole, lambda c, n: c.z < 0.028, ox)
            add(f)
        if "Strap" not in skip and legs == "skin":
            add(loft("Strap" + s, [(0.085, hex6(LR * 0.66, LR * 0.72, x, 0.004)),
                                   (0.12, hex6(LR * 0.68, LR * 0.74, x, 0.004))], sole, ox, False, False))
    waist, chest = H + (Sz - H) * 0.32, H + (Sz - H) * 0.72
    hem = H - 0.15
    if "Torso" not in skip:
        t = loft("Torso", [(hem if top else H - 0.02, oct8(TW * 1.18 if top else TW * 0.95, TW * 0.86 if top else TW * 0.7)),
                           (H, oct8(TW * 0.98, TW * 0.70)), (waist, oct8(TW * 0.86, TW * 0.62)),
                           (chest, oct8(TW * 1.0, TW * 0.66)), (Sz + 0.03, oct8(TW * 1.06, TW * 0.62)),
                           (N + 0.02, oct8(0.085, 0.075))], top or skin, ox, cap_bottom=not top, cap_top=False)
        if top:
            if bare_right_shoulder:
                paint(t, skin, lambda c, n: c.z > chest - 0.04 and c.x < 0.03, ox)
            if top_shade:
                paint(t, top_shade, lambda c, n: c.z < hem + 0.06, ox)
        paint(t, skin, lambda c, n: c.z > N - 0.03, ox)
        add(t)
    if belt and "Belt" not in skip:
        add(loft("Belt", [(waist - 0.03, oct8(TW * 0.88, TW * 0.64)), (waist + 0.035, oct8(TW * 0.9, TW * 0.65))],
                 belt, ox, False, False))
        if "Buckle" not in skip:
            add(box("Buckle", (0.065, 0.03, 0.055), (0, -TW * 0.66, waist), "bronze", ox))
    wz = Sz - AL
    for s, sx in (("L", 1), ("R", -1)):
        x = (SX + 0.02) * sx
        if "Arm" not in skip:
            add(loft("Arm" + s, [(wz, hex6(AR * 0.74, AR * 0.72, x, -0.015)),
                                 (Sz - AL * 0.5, hex6(AR * 0.86, AR * 0.84, x, -0.008)),
                                 (Sz - 0.02, hex6(AR * 1.05, AR * 1.02, x - 0.01 * sx)),
                                 (Sz + 0.04, hex6(AR * 0.8, AR * 0.8, x - 0.03 * sx))], skin, ox, cap_bottom=False))
        if "Fist" not in skip:
            add(loft("Fist" + s, [(wz - HS * 1.55, oct8(HS * 0.85, HS * 0.95, 0.65, x, -0.022)),
                                  (wz - HS * 0.5, oct8(HS, HS * 1.08, 0.65, x, -0.02)),
                                  (wz + 0.005, oct8(HS * 0.78, HS * 0.82, 0.65, x, -0.018))], skin, ox))
        if s in sleeves and top:
            sl = (SX + 0.02) * sx
            add(loft("Sleeve" + s, [(Sz - 0.13, oct8(AR * 1.42, AR * 1.36, 0.66, sl - 0.005 * sx)),
                                    (Sz - 0.01, oct8(AR * 1.44, AR * 1.36, 0.66, sl - 0.012 * sx)),
                                    (Sz + 0.06, oct8(AR * 1.0, AR * 1.05, 0.66, sl - 0.035 * sx))], top, ox, False, False))

    def hz(f):
        return N + HH * f
    ry = HW * 0.93
    if "Head" not in skip:
        h = loft("Head", [(hz(0.04), oct8(HW * 0.46, ry * 0.44, 0.66, 0, -ry * 0.18)),
                          (hz(0.17), oct8(HW * 0.82, ry * 0.8, 0.64, 0, -ry * 0.06)),
                          (hz(0.42), oct8(HW, ry, 0.62)), (hz(0.70), oct8(HW, ry, 0.62)),
                          (hz(0.90), oct8(HW * 0.82, ry * 0.82, 0.6)),
                          (hz(1.0), oct8(HW * 0.38, ry * 0.38, 0.6))], skin, ox)
        paint(h, skin_shade, lambda c, n: c.z < hz(0.2) and n.y < -0.3, ox)
        if hair_style == "cap":
            paint(h, hair, lambda c, n: c.z > hz(0.15) and n.y > 0.35, ox)
        add(h)
        if hair_style == "cap":
            add(loft("Hair", [(hz(0.70), oct8(HW * 1.06, ry * 1.06, 0.6)), (hz(0.92), oct8(HW * 0.88, ry * 0.88, 0.6)),
                              (hz(1.05), oct8(HW * 0.42, ry * 0.42, 0.6))], hair, ox, cap_bottom=False))
    if face:
        for s, x, ang in (("L", HW * 0.38, -0.3), ("R", -HW * 0.38, 0.3)):
            add(box("Brow" + s, (HW * 0.48, 0.03, HH * 0.06), (x, -ry - 0.004, hz(0.6)), brows or hair, ox, rot=(0, ang, 0)))
            add(box("Eye" + s, (HW * 0.24, 0.014, HH * 0.075), (x, -ry - 0.002, hz(0.51)), eyes, ox))
        add(box("Nose", (HW * 0.24, 0.05, HH * 0.15), (0, -ry - 0.01, hz(0.33)), skin_shade, ox, pivot=(0, 0, -1),
                taper=(0.5, 0.3)))
    a = dict(p=p, hand_r=(-(SX + 0.02), -0.03, wz - HS * 0.5), hand_l=((SX + 0.02), -0.03, wz - HS * 0.5),
             head_top=hz(1.0), head_front_y=-ry, head_back_y=ry, head_w=HW, brow_z=hz(0.6), eye_z=hz(0.51),
             band_z=(hz(0.64), hz(0.73)), chest_z=chest, waist_z=waist, hem_z=hem, neck_z=N,
             shoulder_l=((SX + 0.02), 0.0, Sz + 0.02), shoulder_r=(-(SX + 0.02), 0.0, Sz + 0.02),
             back_y=TW * 0.66, front_y=-TW * 0.66, hz=hz)
    return {"parts": parts, "a": a}


def headband(a, colour, ox=0.0, tails=True):
    """A band round the head at a["band_z"], optionally with two tails flying back."""
    HW, ry = a["head_w"], a["head_back_y"]
    z0, z1 = a["band_z"]
    out = [loft("Headband", [(z0, oct8(HW * 1.05, ry * 1.05, 0.6)), (z1, oct8(HW * 1.04, ry * 1.04, 0.6))], colour,
                ox, False, False)]
    if tails:
        HH = a["p"]["HH"]
        for k, (dx, ln, rx, rz) in enumerate(((0.0, HH * 0.42, 0.95, 0.15), (0.04, HH * 0.34, 0.75, 0.45))):
            out.append(box("BandTail%d" % k, (0.05, 0.014, ln), (0.05 + dx, ry + 0.01, (z0 + z1) / 2), colour, ox,
                           rot=(rx, rz, 0), pivot=(0, 0, 1), taper=(0.6, 1.0)))
    return out


def round_shield(name, radius, loc, ox=0.0, wood="wood", rim="rusty_iron", sides=8, facing=1):
    """A flat round shield facing +X (facing=1, on the left arm) or -X, centred at loc."""
    pts = ngon(sides, radius, phase=math.pi / sides)
    sh = loft(name, [(-0.02, pts), (0.02, pts), (0.045, [(x * 0.58, y * 0.58) for x, y in pts])], wood)
    rm = loft(name + "Rim", [(-0.028, [(x * 1.07, y * 1.07) for x, y in pts]), (0.03, [(x * 1.07, y * 1.07) for x, y in pts])],
              rim, 0.0, False, False)
    out = K.join([sh, rm], name)
    out.data.transform(Matrix.Rotation(math.radians(90 * facing), 4, "Y"))
    out.location = (loc[0] + ox, loc[1], loc[2])
    return flat(out)


# --------------------------------------------------------------------------- review renders

def _append_original(blend_name, root_prefix, location):
    """Appends an original asset's objects from BlenderSrc/blend/<blend_name>.blend; moves its root empty."""
    path = os.path.join(K.BLEND_DIR, blend_name + ".blend")
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = list(src.objects)
    roots = []
    for o in dst.objects:
        if o is None or o.type not in ("MESH", "EMPTY"):
            continue
        bpy.context.scene.collection.objects.link(o)
        if o.parent is None and (root_prefix is None or o.name.startswith(root_prefix)):
            roots.append(o)
    for r in roots:
        r.location = Vector(location) + Vector(r.location if r.type == "MESH" else (0, 0, 0))
    bpy.context.view_layer.update()
    return roots


def review(tag, new_objects, original_blend=None, original_root=None, height=1.7, gap=1.1):
    """Renders the original (left from the front) beside the new design: front, back, three-quarter, plus the new
    one's 64 px silhouette, into SHOTS/a1_<tag>_{front,back,threequarter}.png and a sheet SHOTS/a1_<tag>.png.
    new_objects stand at x=-gap/2 (build them at ox=-gap/2); the original goes to x=+gap/2."""
    import numpy as np
    os.makedirs(SHOTS, exist_ok=True)
    if original_blend:
        _append_original(original_blend, original_root, (gap / 2, 0, 0))
    scene = bpy.context.scene
    K._set_engine(scene)
    r = scene.render
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "TEXTURE"
    sh.show_cavity = False
    sh.show_shadows = False
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.36, 0.33, 0.29)
    cam_data = bpy.data.cameras.new("_RevCam")
    cam = bpy.data.objects.new("_RevCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    span = max(height * 1.55, gap * 2.6)
    cz = height * 0.5
    paths = []
    for view, loc in (("front", (0, -10, cz + 0.1)), ("back", (0, 10, cz + 0.1)),
                      ("threequarter", (-6.5, -7.5, cz + 4.0))):
        cam.location = loc
        d = (Vector((0, 0, cz)) - Vector(loc)).normalized()
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = span * (1.08 if view == "threequarter" else 1.0)
        cam_data.clip_end = 100
        r.resolution_x, r.resolution_y, r.resolution_percentage = 720, 600, 100
        pth = os.path.join(SHOTS, "a1_%s_%s.png" % (tag, view))
        r.filepath = pth
        bpy.ops.render.render(write_still=True)
        paths.append(pth)
    tiles = []
    for pth in paths:
        img = bpy.data.images.load(pth, check_existing=False)
        w, h = img.size
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4))
        bpy.data.images.remove(img)
    sheet = np.concatenate(tiles, axis=1)
    out = bpy.data.images.new("_sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels = sheet.ravel()
    sheet_path = os.path.join(SHOTS, "a1_%s.png" % tag)
    out.filepath_raw = sheet_path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    tris = K.tri_count([o for o in new_objects if o.type == "MESH"])
    return {"sheet": sheet_path, "tris": tris, "views": paths}
