"""A1 style lineup: four variants of one simple Nobody, so the producer can point instead of me guessing.

After V4 ("trying too hard to be close to reality ... a mobile weird character"): simple angular shapes in the OSRS
spirit (chamfered boxy sections, plain tapered limbs, no muscles, a few clear clothing pieces, a minimal face), along
two axes:
  * proportions: CHIBI (the game's identity and CH_Nobody's exact joints: hips 0.40, shoulders 0.86, neck 0.95, the
    head ~37 % of the height) or MID (hips 0.55, shoulders 1.02, neck 1.09, head ~31 %);
  * shading: FLAT (faceted, crafted low-poly) or SOFT (smooth gradients across the facets, the OSRS look).
  A = chibi + flat, B = chibi + soft, C = mid + flat, D = mid + soft; the original CH_Nobody stands left of them.
Meshes only (no rig): this is a look test. Renders: Screenshots/blender/a1lineup_*.png.
"""
import importlib
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)

SKIN, SKIN_SHADE = "skin_light", "skin_mid"
TUNIC, TUNIC_SHADE = "cloth_white", "ash"
LEATHER, LEATHER_D = "leather", "leather_d"
IRON, HAIR, ACCENT = "rusty_iron", "mud_dark", "player_cyan"

CHIBI = dict(H=0.40, S=0.86, SX=0.265, N=0.95, HH=0.60, HW=0.265, TW=0.20, LR=0.088, LX=0.105, AR=0.068,
             AL=0.335, HS=0.072, FL=0.25)
MID = dict(H=0.55, S=1.02, SX=0.27, N=1.09, HH=0.50, HW=0.215, TW=0.215, LR=0.092, LX=0.11, AR=0.066,
           AL=0.40, HS=0.068, FL=0.26)


def oct8(rx, ry, k=0.62, cx=0.0, cy=0.0, front=1.0):
    f = ry * front
    pts = [(rx, -f * 0.42), (rx, ry * 0.42), (rx * k, ry), (-rx * k, ry), (-rx, ry * 0.42), (-rx, -f * 0.42),
           (-rx * k, -f), (rx * k, -f)]
    return [(x + cx, y + cy) for x, y in pts]


def hex6(rx, ry, cx=0.0, cy=0.0):
    return [(cx + rx, cy), (cx + rx * 0.5, cy + ry), (cx - rx * 0.5, cy + ry), (cx - rx, cy),
            (cx - rx * 0.5, cy - ry), (cx + rx * 0.5, cy - ry)]


class Builder:
    def __init__(self, p, soft, ox):
        self.p, self.soft, self.ox = p, soft, ox
        self.parts = []

    def shade(self, obj, angle=66.0):
        if self.soft:
            S.soften(obj, angle)
        else:
            for f in obj.data.polygons:
                f.use_smooth = False
        return obj

    def loft(self, name, rings, swatch, cap_bottom=True, cap_top=True, angle=66.0):
        bm = bmesh.new()
        loops = [[bm.verts.new((x + self.ox, y, z)) for x, y in pts] for z, pts in rings]
        n = len(loops[0])
        for a, b in zip(loops, loops[1:]):
            for k in range(n):
                bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
        if cap_bottom:
            bm.faces.new(list(reversed(loops[0])))
        if cap_top:
            bm.faces.new(loops[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        obj = K.object_from_bmesh(name, bm, swatch=swatch)
        self.shade(obj, angle)
        self.parts.append(obj)
        return obj

    def box(self, name, size, loc, swatch, rot=(0, 0, 0), pivot=(0, 0, 0), taper=(1.0, 1.0)):
        o = K.box(name, size=size, location=(loc[0] + self.ox, loc[1], loc[2]), pivot=pivot, taper=taper,
                  swatch=swatch)
        o.rotation_euler = rot
        self.shade(o, 45)
        self.parts.append(o)
        return o

    def paint(self, obj, swatch, test):
        ox = self.ox
        K.set_swatch_where(obj, swatch, lambda c, n: test(Vector((c.x - ox, c.y, c.z)), n))

    def build(self):
        p = self.p
        H, Sz, SX, N, HH, HW, TW = p["H"], p["S"], p["SX"], p["N"], p["HH"], p["HW"], p["TW"]
        LR, LX, AR, AL, HS, FL = p["LR"], p["LX"], p["AR"], p["AL"], p["HS"], p["FL"]
        # legs: a plain taper with a knee, a wedge foot on a dark sole, an ankle strap
        for s, sx in (("L", 1), ("R", -1)):
            x = LX * sx
            self.loft("Leg" + s, [(0.08, hex6(LR * 0.62, LR * 0.68, x, 0.004)),
                                  (H * 0.5, hex6(LR * 0.80, LR * 0.86, x, -0.006)),
                                  (H + 0.02, hex6(LR * 1.0, LR * 1.04, x))], SKIN, cap_top=False)
            foot = self.loft("Foot" + s, [(0.0, oct8(0.068, FL * 0.5, 0.7, x, -0.05)),
                                          (0.05, oct8(0.062, FL * 0.45, 0.7, x, -0.045)),
                                          (0.10, oct8(0.05, 0.06, 0.7, x, 0.0))], SKIN, angle=55)
            self.paint(foot, LEATHER_D, lambda c, n: c.z < 0.028)
            self.loft("Strap" + s, [(0.085, hex6(LR * 0.66, LR * 0.72, x, 0.004)),
                                    (0.12, hex6(LR * 0.68, LR * 0.74, x, 0.004))], LEATHER_D, False, False)
        # the tunic: a chamfered box, the chest square, a short flared skirt; the exomis bares the right shoulder
        waist, chest = H + (Sz - H) * 0.32, H + (Sz - H) * 0.72
        hem = H - 0.15 if H < 0.5 else H - 0.17
        t = self.loft("Tunic", [(hem, oct8(TW * 1.18, TW * 0.86)), (H, oct8(TW * 0.98, TW * 0.70)),
                                (waist, oct8(TW * 0.86, TW * 0.62)), (chest, oct8(TW * 1.0, TW * 0.66)),
                                (Sz + 0.03, oct8(TW * 1.06, TW * 0.62)), (N + 0.02, oct8(0.085, 0.075))],
                      TUNIC, cap_bottom=False, cap_top=False, angle=62)
        self.paint(t, SKIN, lambda c, n: c.z > chest - 0.04 and c.x < 0.03)
        self.paint(t, SKIN, lambda c, n: c.z > N - 0.03)
        self.paint(t, TUNIC_SHADE, lambda c, n: c.z < hem + 0.06)
        self.loft("Belt", [(waist - 0.03, oct8(TW * 0.88, TW * 0.64)), (waist + 0.035, oct8(TW * 0.9, TW * 0.65))],
                  LEATHER_D, False, False)
        self.box("Buckle", (0.065, 0.03, 0.055), (0, -TW * 0.66, waist), "bronze")
        # arms: a plain taper (no muscles), a wrap, a big simple fist
        for s, sx in (("L", 1), ("R", -1)):
            x = (SX + 0.02) * sx
            wz = Sz - AL
            self.loft("Arm" + s, [(wz, hex6(AR * 0.74, AR * 0.72, x, -0.015)),
                                  (Sz - AL * 0.5, hex6(AR * 0.86, AR * 0.84, x, -0.008)),
                                  (Sz - 0.02, hex6(AR * 1.05, AR * 1.02, x - 0.01 * sx)),
                                  (Sz + 0.04, hex6(AR * 0.8, AR * 0.8, x - 0.03 * sx))], SKIN, cap_bottom=False)
            self.loft("Wrap" + s, [(wz - 0.005, hex6(AR * 0.8, AR * 0.78, x, -0.016)),
                                   (wz + 0.06, hex6(AR * 0.84, AR * 0.82, x, -0.015))], LEATHER, False, False)
            self.loft("Fist" + s, [(wz - HS * 1.55, oct8(HS * 0.85, HS * 0.95, 0.65, x, -0.022)),
                                   (wz - HS * 0.5, oct8(HS, HS * 1.08, 0.65, x, -0.02)),
                                   (wz + 0.005, oct8(HS * 0.78, HS * 0.82, 0.65, x, -0.018))], SKIN, angle=55)
        # the shield side: a short sleeve and a scrap pauldron
        sl = SX + 0.02
        self.loft("Sleeve", [(Sz - 0.13, oct8(AR * 1.42, AR * 1.36, 0.66, sl - 0.005)),
                             (Sz - 0.01, oct8(AR * 1.44, AR * 1.36, 0.66, sl - 0.012)),
                             (Sz + 0.06, oct8(AR * 1.0, AR * 1.05, 0.66, sl - 0.035))], TUNIC, False, False, angle=60)
        pa = S.shell("Pauldron", radius=(AR * 1.7, AR * 1.6), arc=(-100, 100), z=(-0.06, 0.06), taper=0.6,
                     thickness=0.02, segments=4, location=(sl + self.ox - 0.01, 0.0, Sz + 0.02), swatch=IRON,
                     flare=0.012)
        self.shade(pa, 45)
        self.parts.append(pa)
        gr = S.shell("Greave", radius=(LR * 0.86, LR * 0.95), arc=(200, 340), z=(0.13, H * 0.62), taper=1.08,
                     thickness=0.016, segments=3, location=(LX + self.ox, 0.0, 0.0), swatch=IRON)
        self.shade(gr, 50)
        self.parts.append(gr)
        # the head: a chamfered block that narrows to a jaw, a minimal face, hair, the cyan headband
        def hz(f):
            return N + HH * f
        ry = HW * 0.93
        h = self.loft("Head", [(hz(0.04), oct8(HW * 0.46, ry * 0.44, 0.66, 0, -ry * 0.18)),
                               (hz(0.17), oct8(HW * 0.82, ry * 0.8, 0.64, 0, -ry * 0.06)),
                               (hz(0.42), oct8(HW, ry, 0.62)), (hz(0.70), oct8(HW, ry, 0.62)),
                               (hz(0.90), oct8(HW * 0.82, ry * 0.82, 0.6)),
                               (hz(1.0), oct8(HW * 0.38, ry * 0.38, 0.6))], SKIN, angle=62)
        self.paint(h, SKIN_SHADE, lambda c, n: c.z < hz(0.2) and n.y < -0.3)
        self.loft("Hair", [(hz(0.70), oct8(HW * 1.06, ry * 1.06, 0.6)), (hz(0.92), oct8(HW * 0.88, ry * 0.88, 0.6)),
                           (hz(1.05), oct8(HW * 0.42, ry * 0.42, 0.6))], HAIR, cap_bottom=False, angle=55)
        # the back of the head is hair, face by face (a shell round an octagonal head let its corners poke through)
        self.paint(h, HAIR, lambda c, n: c.z > hz(0.15) and n.y > 0.35)
        self.loft("Headband", [(hz(0.64), oct8(HW * 1.05, ry * 1.05, 0.6)),
                               (hz(0.73), oct8(HW * 1.04, ry * 1.04, 0.6))], ACCENT, False, False, angle=60)
        for k, (dx, ln, rx, rz) in enumerate(((0.0, HH * 0.42, 0.95, 0.15), (0.04, HH * 0.34, 0.75, 0.45))):
            self.box("BandTail%d" % k, (0.05, 0.014, ln), (0.05 + dx, ry + 0.01, hz(0.69)), ACCENT, rot=(rx, rz, 0),
                     pivot=(0, 0, 1), taper=(0.6, 1.0))
        for s, x, ang in (("L", HW * 0.38, -0.3), ("R", -HW * 0.38, 0.3)):
            self.box("Brow" + s, (HW * 0.48, 0.03, HH * 0.06), (x, -ry - 0.004, hz(0.6)), HAIR, rot=(0, ang, 0))
            self.box("Eye" + s, (HW * 0.24, 0.014, HH * 0.075), (x, -ry - 0.002, hz(0.51)), "ink")
        self.box("Nose", (HW * 0.24, 0.05, HH * 0.15), (0, -ry - 0.01, hz(0.33)), SKIN_SHADE, pivot=(0, 0, -1),
                 taper=(0.5, 0.3))
        # the focale (cyan scarf) and its tail down the back
        self.loft("Focale", [(N - 0.07, oct8(0.135, 0.1, 0.6)), (N, oct8(0.1, 0.085, 0.6))], ACCENT, False, False,
                  angle=60)
        self.box("FocaleTail", (0.10, 0.02, (Sz - H) * 0.6), (0.03, TW * 0.66 + 0.012, N - 0.04), ACCENT,
                 rot=(0.1, 0.08, 0), pivot=(0, 0, 1), taper=(0.6, 1.0))
        # the gladius in the right fist, the shield on the left forearm
        wz = Sz - AL
        g = K.empty("Gladius", location=(-(SX + 0.02) + self.ox, -0.03, wz - HS * 0.5))
        K.extrude_poly("Blade", [(-0.08, 0.045), (-0.42, 0.06), (-0.62, 0.034), (-0.72, 0.0), (-0.62, -0.034),
                                 (-0.42, -0.06), (-0.08, -0.045)], thickness=0.028, plane="YZ", swatch=IRON, parent=g)
        K.box("Guard", size=(0.06, 0.05, 0.17), location=(0, -0.07, 0), pivot=(0, 0, 0), swatch="bronze_d", parent=g)
        K.box("Pommel", size=(0.06, 0.05, 0.06), location=(0, 0.1, 0), pivot=(0, 0, 0), swatch="bronze_d", parent=g)
        for c in g.children:
            self.shade(c, 40)
            self.parts.append(c)
        sh_pts = [(0.235 * math.cos(math.radians(22.5 + 45 * i)), 0.235 * math.sin(math.radians(22.5 + 45 * i)))
                  for i in range(8)]
        sh = self.loft("Shield", [(-0.02, sh_pts), (0.02, sh_pts),
                                  (0.045, [(x * 0.58, y * 0.58) for x, y in sh_pts])], "wood", angle=35)
        rim = self.loft("ShieldRim", [(-0.028, [(x * 1.07, y * 1.07) for x, y in sh_pts]),
                                      (0.03, [(x * 1.07, y * 1.07) for x, y in sh_pts])], IRON, False, False, angle=35)
        for o in (sh, rim):
            o.data.transform(Matrix.Translation((-self.ox, 0, 0)))
            o.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))
            o.location = (SX + 0.13 + self.ox, -0.04, wz + AL * 0.35)
        return self.parts


def lineup():
    K.clear_scene()
    variants = [("A", CHIBI, False, -1.35), ("B", CHIBI, True, 0.0), ("C", MID, False, 1.35), ("D", MID, True, 2.7)]
    tris = {}
    for tag, p, soft, ox in variants:
        parts = Builder(p, soft, ox).build()
        tris[tag] = K.tri_count([o for o in parts if o.type == "MESH"])
    # the original on the far left
    path = os.path.join(K.BLEND_DIR, "ch_nobody.blend")
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = list(src.objects)
    for o in dst.objects:
        if o is None or o.type not in ("MESH", "EMPTY"):
            continue
        bpy.context.scene.collection.objects.link(o)
        if o.name.startswith("CH_Nobody") and o.parent is None and o.type == "EMPTY":
            o.location = (-2.7, 0, 0)
    bpy.context.view_layer.update()
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
    cam_data = bpy.data.cameras.new("_Cam")
    cam = bpy.data.objects.new("_Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    out = {}

    def shoot(tag, loc, target, ortho, res):
        cam.location = loc
        d = (Vector(target) - Vector(loc)).normalized()
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = ortho
        cam_data.clip_end = 100
        r.resolution_x, r.resolution_y, r.resolution_percentage = res[0], res[1], 100
        pth = os.path.join(K.SHOTS_DIR, "a1lineup_%s.png" % tag)
        r.filepath = pth
        bpy.ops.render.render(write_still=True)
        out[tag] = pth

    shoot("front", (0, -10, 0.9), (0, 0, 0.82), 6.6, (1800, 640))
    shoot("threequarter", (-6.0, -9.0, 4.2), (0, 0, 0.8), 6.9, (1800, 700))
    shoot("back", (0, 10, 0.9), (0, 0, 0.82), 6.6, (1800, 640))
    return {"tris": tris, "renders": out}


result = lineup()
