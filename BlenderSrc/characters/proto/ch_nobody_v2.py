"""CH_Nobody_V2 — A1 art-direction prototype of the starter gladiator (docs/tracks/A1-art-direction.md).

The same nameless pit fighter and the same rig as CH_Nobody (ch_nobody.py), rebuilt from shaped forms instead of boxes:
  * one silhouette idea: a big rounded head, broad shoulders over a narrow waist, a torn tunic skirt, one armoured
    side (the shield arm and its leg), and the cyan focale (a legionary's neck scarf) whose tail runs down the back:
    the player's mark from the gameplay camera, as the old scarf tail was;
  * tapered, softly shaded limbs, chunky hands and feet; creases stay crisp (stylized_kit.soften);
  * restrained detail where the eye goes: the face, the scrap pauldron and greave, the belt, the sandal straps;
  * a cohesive palette: warm skin, cream cloth, dark leather, rusty iron, plus the player's cyan.
Rig contract (unchanged, so PartAnimator, the hitbox and the attack timing are untouched):
  CH_Nobody_V2 (root, base)
    Body (hip centre, z 0.40)
      Head (neck, Body z 0.55)
      ArmL (left shoulder, Body (0.28, 0, 0.46))  -> Shield (hand)
      ArmR (right shoulder, Body (-0.28, 0, 0.46)) -> Gladius (grip at arm z -0.385)
    LegL, LegR (hip joints (+-0.11, 0, 0.40)), children of the root
Each rigid part is merged into one mesh (one renderer per part); the gladius and the shield stay separate children.
Front = -Y, the asset's left = +X. Budget: the player's 1,200 tris (CLAUDE.md §7.2).
ARGS: export=1 writes Assets/Art/Models/Prototype/CH_Nobody_V2.fbx; compare=1 renders it beside the original.
"""
import importlib
import math
import os

import bpy
from mathutils import Vector

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)

HIP_Z = 0.40
SHOULDER_Z = 0.46   # relative to Body
SHOULDER_X = 0.28
NECK_Z = 0.55       # relative to Body
LEG_X = 0.11

SKIN, SKIN_SHADE = "skin_light", "skin_mid"
CLOTH, LEATHER, LEATHER_D = "cloth_white", "leather", "leather_d"
IRON, HAIR, ACCENT = "rusty_iron", "mud_dark", "player_cyan"


def bake_rotation(obj):
    """Moves the object's rotation into its mesh (identity rotation, same look)."""
    r = obj.rotation_euler.to_matrix().to_4x4()
    obj.data.transform(r)
    obj.rotation_euler = (0, 0, 0)
    return obj


def build_body(root):
    # a V: broad shoulders, a narrow waist; the right shoulder and chest bare
    body = S.loft("Body", [(-0.03, 0.170, 0.120), (0.07, 0.155, 0.112), (0.22, 0.200, 0.135, 0, -0.010),
                           (0.36, 0.255, 0.145), (0.45, 0.210, 0.128, 0, 0.012), (0.51, 0.090, 0.080, 0, 0.012)],
                  location=(0, 0, HIP_Z), swatch=SKIN, parent=root, cap_bottom=False, cap_top=False)
    # the exomis: the worker's tunic, over the belly and back and up over the left shoulder only
    S.loft("Tunic", [(-0.04, 0.183, 0.132), (0.07, 0.168, 0.124), (0.26, 0.224, 0.151, 0, -0.009)], swatch=CLOTH, parent=body, cap_bottom=False, cap_top=False)
    strap = S.shell("TunicStrap", radius=(0.21, 0.158), arc=(85, 275), z=(-0.055, 0.055), thickness=0.016,
                    segments=3, location=(0.13, 0.0, 0.26), rotation=(0, math.radians(90), 0), swatch=CLOTH,
                    parent=body)
    bake_rotation(strap)
    # the torn hem over the hip joints and the tops of the thighs
    S.strips("Hem", count=8, sides=False, radius_top=(0.19, 0.138), radius_bottom=(0.238, 0.182), z_top=0.0, length=0.21,
             gap=0.14, jitter=0.04, seed=7, swatch=CLOTH, parent=body)
    # the belt and its bronze buckle
    S.band("Belt", -0.02, 0.07, 0.192, 0.142, swatch=LEATHER_D, parent=body)
    K.box("Buckle", size=(0.08, 0.03, 0.065), location=(0, -0.146, 0.025), pivot=(0, 0, 0), swatch="bronze",
          parent=body)
    # the focale: a cyan neck scarf, its tail down the back (the player's mark from behind)
    S.band("Focale", 0.43, 0.515, 0.150, 0.118, taper=0.75, swatch=ACCENT, parent=body)
    tail = K.box("FocaleTail", size=(0.11, 0.02, 0.30), location=(0.035, 0.135, 0.47), pivot=(0, 0, 1),
                 taper=(0.65, 1.0), swatch=ACCENT, parent=body)
    tail.rotation_euler = (0.16, 0.12, 0)
    return body


def build_head(body):
    # a big rounded head (chibi, ~33 % of the height) with a jaw: narrow chin, broad cheeks, a domed crown
    head = S.loft("Head", [(0.035, 0.120, 0.105, 0, -0.035), (0.12, 0.220, 0.196, 0, -0.012),
                           (0.25, 0.278, 0.255), (0.39, 0.283, 0.264, 0, 0.006), (0.52, 0.242, 0.230, 0, 0.012),
                           (0.60, 0.135, 0.130, 0, 0.012)],
                  location=(0, 0, NECK_Z), swatch=SKIN, parent=body, smooth_angle=62, cap_bottom=False)
    K.set_swatch_where(head, SKIN_SHADE, lambda c, n: c.z < 0.15 and c.y < -0.05)  # stubble; seats the jaw
    S.band("Neck", -0.09, 0.07, 0.08, 0.074, segments=6, swatch=SKIN_SHADE, parent=head)
    # the face: heavy angled brows (determined, not cute), small dark eyes under them, a nose (no ears: the
    # headband and the hair frame the face, and boxes stuck on the sides broke the head's round silhouette)
    for side, x, ang in (("L", 0.10, -0.24), ("R", -0.10, 0.24)):
        b = K.box("Brow" + side, size=(0.15, 0.05, 0.04), location=(x, -0.252, 0.365), pivot=(0, 0, 0),
                  swatch=HAIR, parent=head)
        b.rotation_euler = (0, ang, 0)
        K.box("Eye" + side, size=(0.058, 0.02, 0.05), location=(x, -0.266, 0.305), pivot=(0, 0, 0), swatch="ink",
              parent=head)
    K.box("Nose", size=(0.068, 0.07, 0.1), location=(0, -0.266, 0.19), pivot=(0, 0, -1), taper=(0.55, 0.35),
          swatch=SKIN_SHADE, parent=head)  # narrow and shallow at the bridge, broad at the tip
    # hair: a short cap over the crown and the back of the head
    S.loft("Hair", [(0.36, 0.296, 0.278, 0, 0.014), (0.52, 0.255, 0.244, 0, 0.018), (0.655, 0.085, 0.085, 0, 0.022)], swatch=HAIR, parent=head, cap_bottom=False, smooth_angle=45)
    S.shell("HairBack", radius=(0.29, 0.277), arc=(25, 155), z=(0.15, 0.39), taper=1.02, thickness=0.03,
            segments=4, swatch=HAIR, parent=head, flare=0.004)
    # one tuft swept back off the crown (two upright ones read as cat ears from the front)
    tuft = K.cone("Tuft", radius=0.07, depth=0.16, verts=4, location=(0.0, 0.02, 0.6), pivot=(0, 0, -1),
                  swatch=HAIR, parent=head)
    tuft.rotation_euler = (1.05, 0.0, 0.25)
    # the cyan headband over the hairline, its two tails flying back
    S.band("Headband", 0.395, 0.455, 0.298, 0.281, swatch=ACCENT, parent=head)
    for k, (dx, ln, rx, ry) in enumerate(((0.0, 0.34, 1.0, 0.15), (0.05, 0.27, 0.8, 0.45))):
        t = K.box("HeadbandTail%d" % k, size=(0.06, 0.016, ln), location=(0.08 + dx, 0.29, 0.42), pivot=(0, 0, 1),
                  taper=(0.6, 1.0), swatch=ACCENT, parent=head)
        t.rotation_euler = (rx, ry, 0)
    return head


# shoulder cap, deltoid, above the elbow, the forearm's bulge, the wrist (6-sided: round once softened)
ARM_RINGS = [(0.05, 0.070, 0.068), (-0.04, 0.100, 0.096), (-0.19, 0.074, 0.072, 0, -0.012),
             (-0.25, 0.083, 0.080, 0, -0.012), (-0.32, 0.058, 0.056, 0, -0.006)]


def build_arm(body, side):
    x = SHOULDER_X if side == "L" else -SHOULDER_X
    arm = S.loft("Arm" + side, ARM_RINGS, segments=6, location=(x, 0, SHOULDER_Z), swatch=SKIN, parent=body,
                 cap_bottom=False, smooth_angle=70)  # the shoulder's cap shows from above; the wrist hides in the hand
    S.band("Wrist" + side, -0.345, -0.28, 0.064, 0.062, segments=6, taper=1.08, swatch=LEATHER, parent=arm)
    S.rounded_box("Hand" + side, size=(0.135, 0.145, 0.14), bevel=0.03, location=(0, -0.008, -0.39), pivot=(0, 0, 0),
                  swatch=SKIN, parent=arm)
    if side == "L":
        # the shield side is the armoured side: a two-lame scrap pauldron
        S.shell("Pauldron", radius=(0.13, 0.124), arc=(-105, 105), z=(-0.09, 0.075), taper=0.6, thickness=0.02,
                segments=4, swatch=IRON, parent=arm, flare=0.014)
        S.shell("PauldronLame", radius=(0.116, 0.11), arc=(-95, 95), z=(-0.16, -0.08), taper=1.0, thickness=0.018,
                segments=3, swatch=IRON, parent=arm, flare=0.014)
    return arm


def build_shield(arm):
    """The scrap shield: a slightly domed disc, a rusty rim, a boss and one mismatched cross plank, facing out
    (+X) where CH_Nobody's shield is."""
    frame = K.empty("ShieldFrame", location=(0.095, -0.03, -0.32), parent=arm)
    frame.rotation_euler = (0, math.radians(90), 0)  # the builds below face +Z; this turns them to face +X
    S.loft("Shield", [(-0.022, 0.27, 0.27), (0.02, 0.27, 0.27), (0.05, 0.15, 0.15)], segments=8,
           swatch="wood", parent=frame, smooth_angle=30)
    S.band("ShieldRim", -0.03, 0.034, 0.288, 0.288, swatch=IRON, parent=frame, smooth_angle=30)
    S.loft("ShieldBoss", [(0.045, 0.09, 0.09), (0.09, 0.065, 0.065), (0.115, 0.02, 0.02)], segments=4,
           swatch=IRON, parent=frame, smooth_angle=50, cap_bottom=False)
    for k, y in enumerate((0.13,)):  # one mismatched cross plank: scrap
        K.box("ShieldPlank%d" % k, size=(0.06, 0.48, 0.03), location=(y, 0, 0.036), pivot=(0, 0, 0),
              swatch="wood_light", parent=frame)
    return frame


def build_gladius(arm):
    sword = K.empty("Gladius", location=(0, 0, -0.385), parent=arm)
    K.extrude_poly("Blade", [(-0.09, 0.05), (-0.50, 0.066), (-0.74, 0.038), (-0.87, 0.0), (-0.74, -0.038),
                             (-0.50, -0.066), (-0.09, -0.05)], thickness=0.03, plane="YZ", swatch=IRON, parent=sword)
    K.box("Fuller", size=(0.034, 0.48, 0.022), location=(0, -0.13, 0), pivot=(0, 1, 0), swatch="steel", parent=sword)
    K.box("Guard", size=(0.07, 0.055, 0.21), location=(0, -0.08, 0), pivot=(0, 0, 0), swatch="bronze_d",
          parent=sword)
    grip = S.loft("Grip", [(-0.075, 0.028, 0.028), (0.075, 0.028, 0.028)], segments=4, swatch="bone", parent=sword)
    grip.rotation_euler = (math.radians(90), 0, 0)
    bake_rotation(grip)
    pommel = S.loft("Pommel", [(-0.03, 0.045, 0.045), (0.035, 0.03, 0.03)], segments=4,
                    location=(0, 0.115, 0), swatch="bronze_d", parent=sword)
    pommel.rotation_euler = (math.radians(90), 0, 0)
    bake_rotation(pommel)
    return sword


# the hip, the thigh, the knee, the calf, the ankle
LEG_RINGS = [(0.03, 0.095, 0.100), (-0.08, 0.104, 0.110), (-0.19, 0.078, 0.085), (-0.27, 0.080, 0.088, 0, 0.012),
             (-0.335, 0.057, 0.061, 0, 0.004)]


def build_leg(root, side):
    x = LEG_X if side == "L" else -LEG_X
    leg = S.loft("Leg" + side, LEG_RINGS, segments=6, location=(x, 0, HIP_Z), swatch=SKIN, parent=root,
                 cap_bottom=False, cap_top=False, smooth_angle=70)
    # a caliga: a chunky foot on a dark sole, a strap round the ankle
    foot = S.rounded_box("Foot" + side, size=(0.15, 0.27, 0.085), bevel=0.025, location=(0, -0.05, -0.40),
                         pivot=(0, 0, -1), taper=(0.9, 0.8), swatch=SKIN, parent=leg)
    K.set_swatch_where(foot, LEATHER_D, lambda c, n: c.z < 0.03)  # the sole: the bottom and the chamfer above it
    S.band("Ankle" + side, -0.35, -0.315, 0.061, 0.065, segments=6, swatch=LEATHER_D, parent=leg)
    if side == "L":
        # one scrap greave (an ocrea) on the shield-side leg, as gladiators wore them
        S.shell("Greave", radius=(0.088, 0.096), arc=(205, 335), z=(-0.31, -0.18), taper=1.12, thickness=0.018,
                segments=3, swatch=IRON, parent=leg)
    return leg


def build():
    K.clear_scene()
    root = K.empty("CH_Nobody_V2")
    body = build_body(root)
    head = build_head(body)
    arm_l = build_arm(body, "L")
    arm_r = build_arm(body, "R")
    shield_frame = build_shield(arm_l)
    sword = build_gladius(arm_r)
    leg_l = build_leg(root, "L")
    leg_r = build_leg(root, "R")

    # one mesh per rigid part and per prop: the shield frame's pieces become Shield, the sword's pieces Blade
    pieces = [c for c in shield_frame.children if c.type == "MESH"]
    first = [c for c in pieces if c.name == "Shield"][0]
    shield = K.join([first] + [c for c in pieces if c is not first], "Shield")
    bpy.context.view_layer.update()
    mw = shield.matrix_world.copy()
    shield.parent = arm_l
    shield.matrix_world = mw
    bpy.data.objects.remove(shield_frame, do_unlink=True)
    bake_rotation(shield)  # the frame's turn moves into the mesh: identity rotation, as CH_Nobody's shield
    K.join([c for c in sword.children if c.type == "MESH"], "Blade")
    for part in (head, arm_l, arm_r, leg_l, leg_r):
        S.merge_part(part)
    S.merge_part(body)
    return root


def compare(root):
    """The prototype beside the original (appended from its .blend): front, back, 3/4 and the gameplay camera."""
    path = os.path.join(K.BLEND_DIR, "ch_nobody.blend")
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = list(src.objects)
    old_root = None
    for o in dst.objects:
        if o is None:
            continue
        if o.type not in ("MESH", "EMPTY"):
            continue
        bpy.context.scene.collection.objects.link(o)
        if o.name.startswith("CH_Nobody") and o.parent is None and o.type == "EMPTY":
            old_root = o
    old_root.location = (0.85, 0, 0)    # the original: screen right from the front, screen left from behind
    root.location = (-0.85, 0, 0)
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
        if ortho:
            cam_data.type = "ORTHO"
            cam_data.ortho_scale = ortho
        else:
            cam_data.type = "PERSP"
            cam_data.lens = lens or 35
        cam_data.clip_end = 100
        r.resolution_x, r.resolution_y, r.resolution_percentage = res[0], res[1], 100
        p = os.path.join(K.SHOTS_DIR, "a1_nobody_%s.png" % tag)
        r.filepath = p
        bpy.ops.render.render(write_still=True)
        out[tag] = p

    shoot("front", (0, -8, 0.95), (0, 0, 0.85), ortho=2.6)
    shoot("back", (0, 8, 0.95), (0, 0, 0.85), ortho=2.6)
    shoot("threequarter", (-5.5, -6.5, 3.4), (0, 0, 0.8), ortho=2.9)
    # the gameplay camera: behind and above, as the orbit camera sits in a run
    shoot("gamecam", (0, 6.2, 3.1), (0, 0, 0.9), res=(1920, 1080), lens=30)
    return out


root = build()
info = {"tris": S.tris(root), "renderers": S.renderer_count(root)}
info["parts"] = {o.name: K.tri_count([o]) for o in K.descendants(root) if o.type == "MESH"}
lo, hi = K.world_bounds([o for o in K.descendants(root) if o.type == "MESH"])
info["height"] = round(hi.z - lo.z, 3)
rev = K.review_render(root, "ch_nobody_v2", size=420)
info["review"] = rev["sheet"]
if ARGS.get("export") == "1":
    info["fbx"] = K.export_fbx(root, "Prototype/CH_Nobody_V2")
    info["blend"] = K.save_blend("ch_nobody_v2")
if ARGS.get("compare") == "1":
    info["compare"] = compare(root)
result = info
