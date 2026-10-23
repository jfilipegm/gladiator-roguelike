"""CH_Nobody_V4 — A1 prototype 3: the OSRS feel, made ours (docs/tracks/A1-art-direction.md).

The producer's target (2026-10-23): Old School RuneScape's characters, in 3D, with that simple feel, not a rip-off.
What that feel is made of, and how it is translated here:
  * angular forms: a few big, slightly irregular facets (chamfered boxy sections, tapered limbs, trapezoid fists,
    wedge feet), shaded with soft gradients across them (smooth normals on very low-poly shapes, hard edges past 70°);
  * stylized adult proportions: broad shoulders, a narrow waist, big hands, long legs; the head bigger than OSRS's
    (~24 % of the height) so it still reads from our camera;
  * clothing as the main shapes, colour per face, muted earthy tones;
  * a minimal face: slit eyes, a wedge nose, angular hair; heavy angled brows for attitude.
Ours, not RuneScape's: a Roman pit fighter (the exomis tunic over one shoulder, sandals, a scrap pauldron and one
greave, a gladius and a round scrap shield), our palette, and the player's cyan headband and focale.
One skinned mesh on six bones named as the old parts (Body, Head, ArmL/R, LegL/R; legs not under the Body), with
rule-based weights: each piece follows its bone, blended where cloth or skin crosses a joint (the shoulders, the
neck, the tunic's hem over the thighs). The gladius and the shield are separate objects on the arm bones.
Front = -Y, the asset's left = +X. Height 1.6 m (the capsule).
ARGS: export=1 writes Assets/Art/Prototype/CH_Nobody_V4.fbx; compare=1 renders it beside the original and posed.
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
TUNIC, TUNIC_SHADE = "cloth_white", "ash"  # the Nobody's off-white: contrast against tan skin and leather
LEATHER, LEATHER_D = "leather", "leather_d"
IRON, HAIR, ACCENT = "rusty_iron", "mud_dark", "player_cyan"

HIP, SHOULDER_Z, SHOULDER_X, NECK = 0.66, 1.13, 0.275, 1.20
BONES = {
    "Body": ((0, 0, HIP), (0, 0, 1.15), None),
    "Head": ((0, 0, NECK), (0, 0, 1.58), "Body"),
    "ArmL": ((SHOULDER_X, 0, SHOULDER_Z), (0.29, -0.02, 0.58), "Body"),
    "ArmR": ((-SHOULDER_X, 0, SHOULDER_Z), (-0.29, -0.02, 0.58), "Body"),
    "LegL": ((0.11, 0, HIP), (0.11, 0.0, 0.08), None),
    "LegR": ((-0.11, 0, HIP), (-0.11, 0.0, 0.08), None),
}


# --------------------------------------------------------------------------- low-poly sections

def oct8(rx, ry, k=0.62, front=1.0, back=1.0, cx=0.0, cy=0.0):
    """A chamfered box section (8 points, CCW from +X): flat front/back/sides, cut corners. front/back scale the
    front (-Y) and back (+Y) depths, so a chest can push forward and a back stay flat."""
    f, b = ry * front, ry * back
    pts = [(rx, -f * 0.42), (rx, b * 0.42), (rx * k, b), (-rx * k, b), (-rx, b * 0.42), (-rx, -f * 0.42),
           (-rx * k, -f), (rx * k, -f)]
    return [(x + cx, y + cy) for x, y in pts]


def hex6(rx, ry, cx=0.0, cy=0.0):
    """A limb section: 6 points, CCW, a flat front and back."""
    return [(rx, 0.0 + cy), (rx * 0.5 + cx * 0, ry), (-rx * 0.5, ry), (-rx, 0.0), (-rx * 0.5, -ry), (rx * 0.5, -ry)] \
        if False else [(cx + rx, cy), (cx + rx * 0.5, cy + ry), (cx - rx * 0.5, cy + ry), (cx - rx, cy),
                       (cx - rx * 0.5, cy - ry), (cx + rx * 0.5, cy - ry)]


def rings_loft(name, rings, swatch, cap_bottom=True, cap_top=True, smooth=70.0):
    """Rings [(z, [(x, y), ...]), ...] bottom to top, all with the same point count, joined by quads."""
    bm = bmesh.new()
    loops = [[bm.verts.new((x, y, z)) for x, y in pts] for z, pts in rings]
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
    if smooth:
        S.soften(obj, smooth)
    return obj


def paint(obj, swatch, test):
    K.set_swatch_where(obj, swatch, lambda c, n: test(c, n))


def weigh(obj, fn):
    """Vertex groups from fn(world co) -> {bone: weight}."""
    groups = {}
    for v in obj.data.vertices:
        co = obj.matrix_world @ v.co
        for bone, w in fn(co).items():
            if w <= 0:
                continue
            g = groups.get(bone) or obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            groups[bone] = g
            g.add([v.index], w, "REPLACE")
    return obj


def side(x):
    return "L" if x >= 0 else "R"


# --------------------------------------------------------------------------- the body, piece by piece

def build_legs():
    out = []
    for s, sx in (("L", 1), ("R", -1)):
        x = 0.11 * sx
        leg = rings_loft("Leg" + s, [(0.10, hex6(0.052, 0.058, x, 0.004)), (0.24, hex6(0.078, 0.088, x, 0.014)),
                                     (0.37, hex6(0.066, 0.074, x, -0.008)), (0.52, hex6(0.098, 0.104, x)),
                                     (0.68, hex6(0.096, 0.100, x))], SKIN, cap_top=False)  # inside the skirt
        # the knee a touch darker: it shapes the leg from afar
        paint(leg, SKIN_SHADE, lambda c, n: 0.33 < c.z < 0.42 and n.y < -0.3)
        # a caliga: a wedge of a foot on a dark sole, straps over it and round the ankle
        foot = rings_loft("Foot" + s, [(0.0, oct8(0.065, 0.15, 0.7, 1.0, 0.55, x, -0.055)),
                                       (0.045, oct8(0.06, 0.135, 0.7, 1.0, 0.55, x, -0.05)),
                                       (0.105, oct8(0.05, 0.065, 0.7, 1.0, 1.0, x, 0.0))], SKIN, smooth=55)
        paint(foot, LEATHER_D, lambda c, n: c.z < 0.03)
        paint(foot, LEATHER, lambda c, n: 0.03 <= c.z < 0.09 and n.z > 0.3)
        strap = rings_loft("Strap" + s, [(0.095, hex6(0.056, 0.061, x, 0.004)), (0.125, hex6(0.06, 0.066, x, 0.006))],
                           LEATHER_D, cap_bottom=False, cap_top=False)
        strap2 = rings_loft("Strap2" + s, [(0.17, hex6(0.074, 0.082, x, 0.011)), (0.195, hex6(0.077, 0.086, x, 0.012))],
                            LEATHER_D, cap_bottom=False, cap_top=False)
        bone = "Leg" + s
        for o in (leg, foot, strap, strap2):
            weigh(o, lambda co, b=bone: {b: 1.0})
            out.append(o)
    # one scrap greave on the shield-side shin
    gr = S.shell("Greave", radius=(0.082, 0.094), arc=(200, 340), z=(0.14, 0.34), taper=1.12, thickness=0.016,
                 segments=3, location=(0.11, 0.012, 0.0), swatch=IRON, smooth_angle=50)
    weigh(gr, lambda co: {"LegL": 1.0})
    out.append(gr)
    return out


def build_torso():
    """The tunic: a chamfered box flaring into a short skirt, the chest forward, broad square shoulders; the exomis
    leaves the right shoulder and the right of the chest bare."""
    t = rings_loft("Tunic", [(0.46, oct8(0.27, 0.205, 0.68)), (0.60, oct8(0.24, 0.17, 0.68)),
                             (0.70, oct8(0.205, 0.14, 0.66)), (0.80, oct8(0.168, 0.12, 0.62)),
                             (0.97, oct8(0.245, 0.142, 0.62, 1.14, 0.95)),
                             (1.10, oct8(0.275, 0.14, 0.66, 1.0, 0.95)), (1.17, oct8(0.215, 0.115, 0.6)),
                             (1.21, oct8(0.088, 0.078, 0.7))], TUNIC, cap_bottom=False, cap_top=False, smooth=62)
    # the exomis: bare right shoulder and right of the chest; a darker fold down the left side and the hem
    paint(t, SKIN, lambda c, n: c.z > 0.92 and c.x < 0.03 and not (c.y > 0.04 and c.z < 1.02))
    paint(t, TUNIC_SHADE, lambda c, n: 0.46 < c.z < 0.53)
    # the neck: skin, the part of the torso's top that rises into the head
    paint(t, SKIN, lambda c, n: c.z > 1.16)

    def w(co):
        if co.z > 1.17:                       # the neck: shared with the head, so it bends
            return {"Body": 0.5, "Head": 0.5}
        if co.z < HIP - 0.02:                 # the skirt follows the thighs as they swing
            k = min(0.75, (HIP - co.z) / 0.2)
            if abs(co.x) < 0.04:
                return {"Body": 1 - k, "LegL": k * 0.5, "LegR": k * 0.5}
            return {"Body": 1 - k, "Leg" + side(co.x): k}
        if co.z > 1.05 and abs(co.x) > 0.21:  # the shoulder's corner rides a little with the arm
            return {"Body": 0.7, "Arm" + side(co.x): 0.3}
        return {"Body": 1.0}
    weigh(t, w)
    belt = rings_loft("Belt", [(0.775, oct8(0.176, 0.127, 0.62)), (0.845, oct8(0.18, 0.13, 0.62))], LEATHER_D,
                      cap_bottom=False, cap_top=False)
    weigh(belt, lambda co: {"Body": 1.0})
    buckle = K.box("Buckle", size=(0.07, 0.03, 0.055), location=(0.0, -0.138, 0.81), pivot=(0, 0, 0), swatch="bronze")
    weigh(buckle, lambda co: {"Body": 1.0})
    # the focale: a cyan scarf knotted at the base of the neck, its tail down the back (the player's mark)
    foc = rings_loft("Focale", [(1.13, oct8(0.14, 0.105, 0.6)), (1.19, oct8(0.105, 0.088, 0.6))], ACCENT,
                     cap_bottom=False, cap_top=False, smooth=60)
    weigh(foc, lambda co: {"Body": 1.0})
    tail = K.box("FocaleTail", size=(0.10, 0.02, 0.30), location=(0.03, 0.15, 1.16), pivot=(0, 0, 1),
                 taper=(0.6, 1.0), swatch=ACCENT)
    tail.rotation_euler = (0.1, 0.08, 0)
    weigh(tail, lambda co: {"Body": 1.0})
    return [t, belt, buckle, foc, tail]


def build_arms():
    out = []
    for s, sx in (("L", 1), ("R", -1)):
        x = 0.30 * sx
        arm = rings_loft("Arm" + s, [(0.625, hex6(0.052, 0.050, x, -0.018)), (0.75, hex6(0.080, 0.072, x, -0.016)),
                                     (0.865, hex6(0.062, 0.060, x - 0.004 * sx, -0.006)),
                                     (0.99, hex6(0.084, 0.078, x - 0.010 * sx, 0.0)),
                                     (1.09, hex6(0.090, 0.084, x - 0.018 * sx, 0.0)),
                                     (1.16, hex6(0.07, 0.07, x - 0.03 * sx, 0.0))], SKIN, cap_bottom=False)
        bone = "Arm" + s

        def w(co, b=bone):
            if co.z > 1.10:
                return {b: 0.7, "Body": 0.3}
            return {b: 1.0}
        weigh(arm, w)
        wrap = rings_loft("Wrap" + s, [(0.615, hex6(0.058, 0.056, x, -0.019)), (0.69, hex6(0.072, 0.066, x, -0.017))],
                          LEATHER, cap_bottom=False, cap_top=False)
        # a big trapezoid fist, the knuckles forward, a thumb on top
        fist = rings_loft("Fist" + s, [(0.49, oct8(0.058, 0.066, 0.65, 1.15, 0.9, x, -0.026)),
                                       (0.575, oct8(0.068, 0.076, 0.65, 1.15, 0.9, x, -0.024)),
                                       (0.625, oct8(0.054, 0.058, 0.65, 1.0, 1.0, x, -0.02))], SKIN, smooth=55)
        thumb = K.box("Thumb" + s, size=(0.045, 0.055, 0.055), location=(x - 0.05 * sx, -0.08, 0.58), pivot=(0, 0, 0),
                      swatch=SKIN)
        thumb.rotation_euler = (0.3, 0, 0.4 * sx)
        for o in (wrap, fist, thumb):
            weigh(o, lambda co, b=bone: {b: 1.0})
        out += [arm, wrap, fist, thumb]
    # the shield side: a short flared sleeve of the tunic and a two-plate scrap pauldron over it
    sleeve = rings_loft("Sleeve", [(0.97, oct8(0.10, 0.094, 0.66, 1.0, 1.0, 0.292, 0.0)),
                                   (1.09, oct8(0.10, 0.092, 0.66, 1.0, 1.0, 0.28, 0.0)),
                                   (1.18, oct8(0.07, 0.075, 0.66, 1.0, 1.0, 0.25, 0.0))], TUNIC,
                        cap_bottom=False, cap_top=False, smooth=60)
    weigh(sleeve, lambda co: {"ArmL": 0.85, "Body": 0.15} if co.z > 1.12 else {"ArmL": 1.0})
    pa = S.shell("Pauldron", radius=(0.122, 0.116), arc=(-100, 100), z=(-0.07, 0.06), taper=0.62, thickness=0.02,
                 segments=4, location=(0.28, 0.0, 1.14), swatch=IRON, flare=0.014, smooth_angle=45)
    la = S.shell("PauldronLame", radius=(0.112, 0.106), arc=(-90, 90), z=(-0.13, -0.065), taper=1.0, thickness=0.018,
                 segments=3, location=(0.28, 0.0, 1.14), swatch=IRON, flare=0.014, smooth_angle=45)
    for o in (pa, la):
        weigh(o, lambda co: {"ArmL": 1.0})
    out += [sleeve, pa, la]
    return out


def build_head():
    """A chamfered, angular head with a jaw and a chin, slit eyes under heavy brows, a wedge nose, short angular hair
    and the cyan headband with its tails."""
    h = rings_loft("Head", [(1.215, oct8(0.075, 0.065, 0.66, 1.0, 1.0, 0, -0.035)),
                            (1.27, oct8(0.135, 0.125, 0.62, 1.0, 1.0, 0, -0.018)),
                            (1.37, oct8(0.160, 0.152, 0.60)), (1.455, oct8(0.158, 0.150, 0.60)),
                            (1.53, oct8(0.128, 0.125, 0.60, 1.0, 1.0, 0, 0.006)),
                            (1.585, oct8(0.06, 0.06, 0.6, 1.0, 1.0, 0, 0.008))], SKIN, smooth=62)
    paint(h, SKIN_SHADE, lambda c, n: c.z < 1.30 and n.y < -0.2)            # the jaw: stubble
    hair = rings_loft("Hair", [(1.43, oct8(0.168, 0.162, 0.6, 1.0, 1.0, 0, 0.006)),
                               (1.535, oct8(0.138, 0.135, 0.6, 1.0, 1.0, 0, 0.01)),
                               (1.61, oct8(0.065, 0.065, 0.6, 1.0, 1.0, 0, 0.012))], HAIR, cap_bottom=False, smooth=55)
    back = S.shell("HairBack", radius=(0.165, 0.16), arc=(15, 165), z=(1.29, 1.45), taper=1.03, thickness=0.025,
                   segments=4, swatch=HAIR, smooth_angle=50)
    spikes = []
    for k, (x, y, rx, ry, ht) in enumerate(((0.06, -0.06, -0.55, 0.3, 0.09), (-0.05, -0.08, -0.6, -0.25, 0.08),
                                            (0.0, 0.05, 0.5, 0.1, 0.1))):
        sp = K.cone("Spike%d" % k, radius=0.05, depth=ht, verts=4, location=(x, y, 1.57), pivot=(0, 0, -1),
                    swatch=HAIR)
        sp.rotation_euler = (rx, ry, 0)
        spikes.append(sp)
    band = rings_loft("Headband", [(1.43, oct8(0.166, 0.159, 0.6)), (1.47, oct8(0.164, 0.157, 0.6))], ACCENT,
                      cap_bottom=False, cap_top=False, smooth=60)
    tails = []
    for k, (dx, ln, rx, ry) in enumerate(((0.0, 0.24, 0.95, 0.15), (0.04, 0.19, 0.75, 0.45))):
        t = K.box("BandTail%d" % k, size=(0.05, 0.014, ln), location=(0.05 + dx, 0.165, 1.45), pivot=(0, 0, 1),
                  taper=(0.6, 1.0), swatch=ACCENT)
        t.rotation_euler = (rx, ry, 0)
        tails.append(t)
    face = []
    for s, x, ang in (("L", 0.065, -0.3), ("R", -0.065, 0.3)):
        b = K.box("Brow" + s, size=(0.09, 0.03, 0.03), location=(x, -0.152, 1.425), pivot=(0, 0, 0), swatch=HAIR)
        b.rotation_euler = (0, ang, 0)
        e = K.box("Eye" + s, size=(0.05, 0.012, 0.016), location=(x, -0.153, 1.39), pivot=(0, 0, 0), swatch="ink")
        e.rotation_euler = (0, ang * 0.5, 0)
        face += [b, e]
    nose = K.box("Nose", size=(0.045, 0.05, 0.07), location=(0, -0.152, 1.31), pivot=(0, 0, -1), taper=(0.5, 0.25),
                 swatch=SKIN_SHADE)
    face.append(nose)
    parts = [h, hair, back, band] + spikes + tails + face
    for o in parts:
        weigh(o, lambda co: {"Head": 0.5, "Body": 0.5} if co.z < 1.235 else {"Head": 1.0})
    return parts


def build_armature():
    arm_data = bpy.data.armatures.new("CH_Nobody_V4_Rig")
    rig = bpy.data.objects.new("CH_Nobody_V4", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    for name, (h, t, parent) in BONES.items():
        b = eb.new(name)
        b.head, b.tail = Vector(h), Vector(t)
        b.roll = 0.0
    for name, (h, t, parent) in BONES.items():
        if parent:
            eb[name].parent = eb[parent]
            eb[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def build_props(rig):
    sword = K.empty("Gladius", location=(0, 0, 0))
    K.extrude_poly("Blade", [(-0.09, 0.048), (-0.48, 0.064), (-0.70, 0.036), (-0.82, 0.0), (-0.70, -0.036),
                             (-0.48, -0.064), (-0.09, -0.048)], thickness=0.028, plane="YZ", swatch=IRON, parent=sword)
    K.box("Fuller", size=(0.032, 0.46, 0.02), location=(0, -0.13, 0), pivot=(0, 1, 0), swatch="steel", parent=sword)
    K.box("Guard", size=(0.065, 0.05, 0.19), location=(0, -0.075, 0), pivot=(0, 0, 0), swatch="bronze_d", parent=sword)
    K.box("Grip", size=(0.045, 0.15, 0.045), location=(0, 0.02, 0), pivot=(0, 0, 0), swatch="bone", parent=sword)
    K.box("Pommel", size=(0.065, 0.055, 0.065), location=(0, 0.105, 0), pivot=(0, 0, 0), swatch="bronze_d",
          parent=sword)
    blade = K.join([c for c in sword.children], "Gladius")
    bpy.data.objects.remove(sword, do_unlink=True)
    blade.name = "Gladius"
    blade.location = (-0.30, -0.03, 0.56)
    shield = rings_loft("Shield", [(-0.02, [(0.25 * math.cos(a), 0.25 * math.sin(a)) for a in
                                            [math.radians(22.5 + 45 * i) for i in range(8)]]),
                                   (0.02, [(0.25 * math.cos(a), 0.25 * math.sin(a)) for a in
                                           [math.radians(22.5 + 45 * i) for i in range(8)]]),
                                   (0.045, [(0.14 * math.cos(a), 0.14 * math.sin(a)) for a in
                                            [math.radians(22.5 + 45 * i) for i in range(8)]])], "wood", smooth=35)
    rim = rings_loft("ShieldRim", [(-0.028, [(0.265 * math.cos(a), 0.265 * math.sin(a)) for a in
                                              [math.radians(22.5 + 45 * i) for i in range(8)]]),
                                   (0.03, [(0.265 * math.cos(a), 0.265 * math.sin(a)) for a in
                                           [math.radians(22.5 + 45 * i) for i in range(8)]])], IRON,
                     cap_bottom=False, cap_top=False, smooth=35)
    boss = S.loft("ShieldBoss", [(0.04, 0.08, 0.08), (0.08, 0.06, 0.06), (0.1, 0.02, 0.02)], segments=4,
                  swatch=IRON, smooth_angle=50, cap_bottom=False)
    plank = K.box("ShieldPlank", size=(0.055, 0.44, 0.03), location=(0.12, 0, 0.032), pivot=(0, 0, 0),
                  swatch="wood_light")
    shield = K.join([shield, rim, boss, plank], "Shield")
    shield.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))  # face out (+X)
    shield.location = (0.405, -0.04, 0.72)
    for prop, bone in ((blade, "ArmR"), (shield, "ArmL")):
        mw = prop.matrix_world.copy()
        prop.parent = rig
        prop.parent_type = "BONE"
        prop.parent_bone = bone
        bpy.context.view_layer.update()
        prop.matrix_world = mw
    return blade, shield


def build():
    K.clear_scene()
    pieces = build_legs() + build_torso() + build_arms() + build_head()
    rig = build_armature()
    bpy.context.view_layer.update()
    mesh = K.join(pieces, "Nobody")
    mesh.parent = rig
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    blade, shield = build_props(rig)
    return rig, mesh, blade, shield


def pose(rig, rots):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    for name, (axis, deg) in rots.items():
        pb = rig.pose.bones[name]
        rest = pb.bone.matrix_local.to_3x3()
        q = (rest.inverted() @ Matrix.Rotation(math.radians(deg), 3, axis) @ rest).to_quaternion()
        pb.rotation_quaternion = q
    bpy.context.view_layer.update()


def compare(rig):
    path = os.path.join(K.BLEND_DIR, "ch_nobody.blend")
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = list(src.objects)
    old_root = None
    for o in dst.objects:
        if o is None or o.type not in ("MESH", "EMPTY"):
            continue
        bpy.context.scene.collection.objects.link(o)
        if o.name.startswith("CH_Nobody") and o.parent is None and o.type == "EMPTY":
            old_root = o
    old_root.location = (0.85, 0, 0)
    rig.location = (-0.85, 0, 0)
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
    cam_data = bpy.data.cameras.new("_CmpCam")
    cam = bpy.data.objects.new("_CmpCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    out = {}

    def shoot(tag, loc, target, ortho=None, res=(960, 640), lens=None):
        cam.location = loc
        d = (Vector(target) - Vector(loc)).normalized()
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam_data.type = "ORTHO" if ortho else "PERSP"
        if ortho:
            cam_data.ortho_scale = ortho
        else:
            cam_data.lens = lens or 35
        cam_data.clip_end = 100
        r.resolution_x, r.resolution_y, r.resolution_percentage = res[0], res[1], 100
        p = os.path.join(K.SHOTS_DIR, "a1v4_%s.png" % tag)
        r.filepath = p
        bpy.ops.render.render(write_still=True)
        out[tag] = p

    shoot("front", (0, -8, 0.95), (0, 0, 0.85), ortho=2.6)
    shoot("back", (0, 8, 0.95), (0, 0, 0.85), ortho=2.6)
    shoot("threequarter", (-5.5, -6.5, 3.4), (0, 0, 0.8), ortho=2.9)
    shoot("gamecam", (0, 6.2, 3.1), (0, 0, 0.9), res=(1920, 1080), lens=30)
    old_root.location = (50, 0, 0)
    rig.location = (0, 0, 0)
    pose(rig, {"ArmL": ("X", 35), "ArmR": ("X", -40), "LegL": ("X", -28), "LegR": ("X", 28), "Body": ("X", 8)})
    shoot("run", (-4.2, -5.0, 1.6), (0, 0, 0.8), ortho=2.1)
    pose(rig, {"ArmR": ("X", -125), "ArmL": ("Y", -25), "Body": ("Z", 20), "Head": ("Z", -10)})
    shoot("attack", (-4.2, -5.0, 1.6), (0, 0, 0.9), ortho=2.3)
    pose(rig, {})
    return out


rig, mesh, blade, shield = build()
info = {"tris": K.tri_count([mesh, blade, shield]), "body_tris": K.tri_count([mesh]), "renderers": 3,
        "groups": [g.name for g in mesh.vertex_groups]}
lo, hi = K.world_bounds([mesh, blade, shield])
info["height"] = round(hi.z - lo.z, 3)
if ARGS.get("export") == "1":
    path = os.path.join(K.REPO, "Assets", "Art", "Prototype", "CH_Nobody_V4.fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in (rig, mesh, blade, shield):
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                             axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             use_armature_deform_only=True, primary_bone_axis="Y", secondary_bone_axis="X",
                             path_mode="STRIP", embed_textures=False)
    info["fbx"] = path
    info["blend"] = K.save_blend("ch_nobody_v4")
if ARGS.get("compare") == "1":
    info["compare"] = compare(rig)
result = info
