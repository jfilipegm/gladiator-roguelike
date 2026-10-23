"""CH_Nobody_V3 — A1 prototype 2: one continuous, skinned body (docs/tracks/A1-art-direction.md).

The producer on V2: "it looks like a Playmobil character, not fluid, still different blocks fitting together". The cause
was the rig, not the shapes: six rigid meshes meet at the neck, shoulders and hips, so seams show however each piece
is shaped. V3 is one continuous mesh grown from a skeleton (Blender's Skin modifier + one subdivision): the neck flows
into the chest, the arms out of the shoulders, the thighs out of the hips. The tunic, the hair and the headband are
layers lifted off that same surface (so they hug it); the skirt flares from the waist. Six bones at CH_Nobody's joints
(Body hip 0.40, Head neck 0.95, ArmL/R shoulders (+-0.28, 0.86), LegL/R hips (+-0.11, 0.40), the legs not under the
Body, as in the rigid rig) deform it smoothly; the face, the pauldron and the greave ride their bone rigidly; the
gladius and the shield are separate objects on the arm bones. Front = -Y, the asset's left = +X.
ARGS: export=1 writes Assets/Art/Prototype/CH_Nobody_V3.fbx (outside Assets/Art/Models, so ModelImportRules —
built for rigid parts — leave it alone); compare=1 renders it beside the original, at rest and posed.
"""
import importlib
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)

SKIN, SKIN_SHADE = "skin_light", "skin_mid"
CLOTH, LEATHER, LEATHER_D = "cloth_white", "leather", "leather_d"
IRON, HAIR, ACCENT = "rusty_iron", "mud_dark", "player_cyan"

# the skeleton graph the body grows from: name -> (position, (radius x, radius y))
# (the subdivision rounds each square skin section in to ~0.9 of its radius: the radii allow for it)
NODES = {
    "pelvis": ((0, 0.0, 0.43), (0.165, 0.125)),
    "waist": ((0, 0.0, 0.56), (0.140, 0.108)),
    "chest": ((0, -0.008, 0.70), (0.200, 0.135)),
    "yoke": ((0, 0.004, 0.83), (0.200, 0.122)),
    "neck": ((0, 0.008, 1.00), (0.085, 0.080)),
}
for side, sx in (("L", 1), ("R", -1)):
    NODES.update({
        "shoulder" + side: ((0.255 * sx, 0.0, 0.85), (0.105, 0.100)),
        "elbow" + side: ((0.295 * sx, -0.012, 0.67), (0.078, 0.076)),
        "forearm" + side: ((0.300 * sx, -0.016, 0.60), (0.085, 0.082)),
        "wrist" + side: ((0.302 * sx, -0.020, 0.525), (0.062, 0.060)),
        "hand" + side: ((0.304 * sx, -0.026, 0.455), (0.088, 0.094)),
        "hip" + side: ((0.105 * sx, 0.0, 0.385), (0.118, 0.122)),
        "knee" + side: ((0.112 * sx, -0.012, 0.215), (0.086, 0.092)),
        "ankle" + side: ((0.112 * sx, 0.006, 0.08), (0.066, 0.070)),
        "toe" + side: ((0.112 * sx, -0.125, 0.048), (0.075, 0.056)),
    })
EDGES = [("pelvis", "waist"), ("waist", "chest"), ("chest", "yoke"), ("yoke", "neck")]
for side in "LR":
    EDGES += [("yoke", "shoulder" + side), ("shoulder" + side, "elbow" + side), ("elbow" + side, "forearm" + side),
              ("forearm" + side, "wrist" + side),
              ("wrist" + side, "hand" + side), ("pelvis", "hip" + side), ("hip" + side, "knee" + side),
              ("knee" + side, "ankle" + side), ("ankle" + side, "toe" + side)]

# bones: name -> (head, tail, parent); heads at CH_Nobody's joints, so the animation pivots are the same
BONES = {
    "Body": ((0, 0, 0.40), (0, 0, 0.84), None),
    "Head": ((0, 0, 0.95), (0, 0, 1.36), "Body"),
    "ArmL": ((0.28, 0, 0.86), (0.305, -0.02, 0.45), "Body"),
    "ArmR": ((-0.28, 0, 0.86), (-0.305, -0.02, 0.45), "Body"),
    "LegL": ((0.11, 0, 0.40), (0.112, 0.0, 0.05), None),
    "LegR": ((-0.11, 0, 0.40), (-0.112, 0.0, 0.05), None),
}


def evaluated_copy(obj, name):
    """A new object with obj's modifiers applied (no operators: works headless)."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    me.name = name
    new = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(new)
    return new


def grow_body():
    names = list(NODES)
    me = bpy.data.meshes.new("Skeleton")
    me.from_pydata([NODES[n][0] for n in names], [(names.index(a), names.index(b)) for a, b in EDGES], [])
    skel = bpy.data.objects.new("Skeleton", me)
    bpy.context.scene.collection.objects.link(skel)
    skin = skel.modifiers.new("Skin", "SKIN")
    skin.branch_smoothing = 0.6
    skin.use_smooth_shade = True
    for i, n in enumerate(names):
        sv = me.skin_vertices[0].data[i]
        sv.radius = NODES[n][1]
        sv.use_root = n == "pelvis"
    sub = skel.modifiers.new("Sub", "SUBSURF")
    sub.levels = 1
    sub.render_levels = 1
    body = evaluated_copy(skel, "BodySkin")
    bpy.data.objects.remove(skel, do_unlink=True)
    for p in body.data.polygons:
        p.use_smooth = True
    body.data.materials.clear()
    K.set_swatch(body, SKIN)
    return body


def paint(obj, swatch, test):
    """Faces whose centre (world) passes test(c) get the swatch."""
    K.set_swatch_where(obj, swatch, lambda c, n: test(c))


def lift(src, name, test, offset, swatch, solid=0.0):
    """A layer lifted off src's surface: its faces passing test(centre), pushed out along their normals by `offset`
    (so it hugs the body), optionally given a thickness. Smooth-shaded, one swatch."""
    bm = bmesh.new()
    bm.from_mesh(src.data)
    drop = [f for f in bm.faces if not test(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset
    if solid > 0:
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=solid)
    obj = K.object_from_bmesh(name, bm, swatch=swatch)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def surface(body, x, z, from_front=True):
    """The body surface point at (x, z) seen from the front (-Y) or the back."""
    bvh = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())
    o = Vector((x, -2.0 if from_front else 2.0, z))
    d = Vector((0, 1.0 if from_front else -1.0, 0))
    hit, normal, _, _ = bvh.ray_cast(o, d)
    return hit, normal


def rigid_group(obj, bone):
    g = obj.vertex_groups.new(name=bone)
    g.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    return obj


NECK_Z = 0.95


def build_head():
    """A big rounded head (~34 % of the height: the chibi proportion) with a narrow chin, broad cheeks and a domed
    crown, 12-sided and smooth-shaded. Its jaw sits over the body's neck, which flows out of the shoulders."""
    head = S.loft("Head", [(0.045, 0.125, 0.11, 0, -0.04), (0.13, 0.215, 0.195, 0, -0.016),
                           (0.26, 0.272, 0.252), (0.38, 0.279, 0.261, 0, 0.005), (0.44, 0.272, 0.256, 0, 0.008),
                           (0.53, 0.232, 0.221, 0, 0.012), (0.59, 0.145, 0.138, 0, 0.012)],
                  segments=12, location=(0, 0, NECK_Z), swatch=SKIN, smooth_angle=70)
    K.set_swatch_where(head, SKIN_SHADE, lambda c, n: c.z < 0.14 and c.y < -0.05)  # stubble along the jaw
    bpy.context.view_layer.update()
    head.data.transform(Matrix.Translation(head.location))  # into world space, as the lifted layers are
    head.location = (0, 0, 0)
    return head


def build_face(body):
    parts = []
    for side, x, ang in (("L", 0.10, -0.26), ("R", -0.10, 0.26)):
        p, n = surface(body, x, NECK_Z + 0.335)
        brow = K.box("Brow" + side, size=(0.15, 0.06, 0.045), location=p + Vector((0, 0.014, 0)), pivot=(0, 0, 0),
                     swatch=HAIR)
        brow.rotation_euler = (0, ang, 0)
        parts.append(brow)
        p, n = surface(body, x, NECK_Z + 0.275)
        parts.append(K.box("Eye" + side, size=(0.062, 0.03, 0.05), location=p + Vector((0, 0.01, 0)),
                           pivot=(0, 0, 0), swatch="ink"))
    p, n = surface(body, 0.0, NECK_Z + 0.20)
    parts.append(K.box("Nose", size=(0.066, 0.07, 0.095), location=p + Vector((0, 0.028, -0.04)), pivot=(0, 0, -1),
                       taper=(0.55, 0.3), swatch=SKIN_SHADE))
    for o in parts:
        S.soften(o, 50)
    return parts


def build_accessories(body, head):
    """The rigid pieces that ride one bone: the pauldron (ArmL), the greave (LegL), the buckle, the focale's tail."""
    out = []
    pa = S.shell("Pauldron", radius=(0.125, 0.118), arc=(-105, 105), z=(-0.085, 0.07), taper=0.6, thickness=0.02,
                 segments=6, location=(0.265, 0.0, 0.875), swatch=IRON, flare=0.014)
    out.append(rigid_group(pa, "ArmL"))
    la = S.shell("PauldronLame", radius=(0.11, 0.104), arc=(-95, 95), z=(-0.15, -0.075), taper=1.0, thickness=0.018,
                 segments=5, location=(0.265, 0.0, 0.875), swatch=IRON, flare=0.014)
    out.append(rigid_group(la, "ArmL"))
    gr = S.shell("Greave", radius=(0.084, 0.092), arc=(205, 335), z=(0.10, 0.24), taper=1.1, thickness=0.018,
                 segments=4, location=(0.112, -0.008, 0.0), swatch=IRON)
    out.append(rigid_group(gr, "LegL"))
    p, n = surface(body, 0.0, 0.565)
    bu = K.box("Buckle", size=(0.075, 0.03, 0.06), location=p + Vector((0, -0.012, 0)), pivot=(0, 0, 0),
               swatch="bronze")
    out.append(rigid_group(bu, "Body"))
    p, n = surface(body, 0.035, 0.84, from_front=False)
    tail = K.box("FocaleTail", size=(0.11, 0.02, 0.30), location=p + Vector((0, 0.022, 0.07)), pivot=(0, 0, 1),
                 taper=(0.6, 1.0), swatch=ACCENT)
    tail.rotation_euler = (0.14, 0.1, 0)
    out.append(rigid_group(tail, "Body"))
    for k, (dx, ln, rx, ry) in enumerate(((0.0, 0.32, 1.0, 0.15), (0.05, 0.26, 0.8, 0.45))):
        p, n = surface(head, 0.07 + dx, NECK_Z + 0.42, from_front=False)
        t = K.box("BandTail%d" % k, size=(0.06, 0.016, ln), location=p + Vector((0, 0.04, 0)), pivot=(0, 0, 1),
                  taper=(0.6, 1.0), swatch=ACCENT)
        t.rotation_euler = (rx, ry, 0)
        out.append(rigid_group(t, "Head"))
    for o in out:
        S.soften(o, 45)
    return out


def build_skirt():
    """The torn tunic skirt flaring from the waist over the thighs (single surface; weights follow the legs)."""
    return S.strips("Skirt", count=10, radius_top=(0.178, 0.138), radius_bottom=(0.255, 0.205), z_top=0.56,
                    length=0.25, gap=0.07, jitter=0.03, seed=5, swatch=CLOTH, sides=True)


def build_armature():
    arm_data = bpy.data.armatures.new("CH_Nobody_V3_Rig")
    rig = bpy.data.objects.new("CH_Nobody_V3", arm_data)
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


def bind(rig, mesh_auto, rigid_meshes):
    """Automatic (heat) weights for the organic surface, then the rigid pieces joined in with their own groups."""
    bpy.ops.object.select_all(action="DESELECT")
    mesh_auto.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    mesh = K.join([mesh_auto] + rigid_meshes, "Nobody")
    mesh.parent = rig
    if not any(m.type == "ARMATURE" for m in mesh.modifiers):
        mod = mesh.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    return mesh


def build_props(rig):
    """The gladius and the shield as rigid objects on the arm bones (separate, as weapons swap)."""
    sword = K.empty("Gladius", location=(0, 0, 0))
    K.extrude_poly("Blade", [(-0.09, 0.05), (-0.50, 0.066), (-0.74, 0.038), (-0.87, 0.0), (-0.74, -0.038),
                             (-0.50, -0.066), (-0.09, -0.05)], thickness=0.03, plane="YZ", swatch=IRON, parent=sword)
    K.box("Fuller", size=(0.034, 0.48, 0.022), location=(0, -0.13, 0), pivot=(0, 1, 0), swatch="steel", parent=sword)
    K.box("Guard", size=(0.07, 0.055, 0.21), location=(0, -0.08, 0), pivot=(0, 0, 0), swatch="bronze_d", parent=sword)
    K.box("Grip", size=(0.05, 0.16, 0.05), location=(0, 0.02, 0), pivot=(0, 0, 0), swatch="bone", parent=sword)
    K.box("Pommel", size=(0.07, 0.06, 0.07), location=(0, 0.11, 0), pivot=(0, 0, 0), swatch="bronze_d", parent=sword)
    blade = K.join([c for c in sword.children], "Gladius_Mesh")
    bpy.data.objects.remove(sword, do_unlink=True)
    blade.name = "Gladius"
    blade.location = (-0.305, -0.026, 0.455)
    shield = S.loft("Shield", [(-0.022, 0.27, 0.27), (0.02, 0.27, 0.27), (0.05, 0.15, 0.15)], segments=10,
                    swatch="wood", smooth_angle=30)
    rim = S.band("ShieldRim", -0.03, 0.034, 0.288, 0.288, segments=10, swatch=IRON, smooth_angle=30)
    boss = S.loft("ShieldBoss", [(0.045, 0.09, 0.09), (0.09, 0.065, 0.065), (0.115, 0.02, 0.02)], segments=6,
                  swatch=IRON, smooth_angle=50, cap_bottom=False)
    plank = K.box("ShieldPlank", size=(0.06, 0.48, 0.03), location=(0.13, 0, 0.036), pivot=(0, 0, 0),
                  swatch="wood_light")
    shield = K.join([shield, rim, boss, plank], "Shield")
    shield.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))  # face out (+X)
    shield.location = (0.40, -0.056, 0.54)
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
    body = grow_body()
    head = build_head()
    # colour the skin surface: sandals, a wrist wrap each, the hands stay skin
    paint(body, LEATHER_D, lambda c: c.z < 0.085)
    paint(body, LEATHER, lambda c: 0.505 < c.z < 0.55 and abs(c.x) > 0.24)
    paint(body, LEATHER_D, lambda c: 0.115 < c.z < 0.14 and abs(c.x) > 0.05)
    # the layers lifted off the body: the exomis (belly, back, left shoulder), the belt, the hair, the headband
    tunic = lift(body, "Tunic", lambda c: (0.47 < c.z < 0.74 and abs(c.x) < 0.215) or
                 (0.74 <= c.z < 0.93 and 0.03 < c.x < 0.23), 0.012, CLOTH)
    belt = lift(body, "Belt", lambda c: 0.535 < c.z < 0.60 and abs(c.x) < 0.2, 0.024, LEATHER_D)
    # the hair above the headband and down the back of the head; the band is the head's 0.38-0.44 ring
    hair = lift(head, "Hair", lambda c: c.z > NECK_Z + 0.44 or (c.z > NECK_Z + 0.12 and c.y > 0.09), 0.022, HAIR)
    band = lift(head, "Headband", lambda c: NECK_Z + 0.39 < c.z < NECK_Z + 0.43, 0.034, ACCENT)
    # the focale: a narrow ring of cyan at the base of the neck (its tail runs down the back)
    focale = lift(body, "Focale", lambda c: 0.905 < c.z < 0.985 and abs(c.x) < 0.13, 0.024, ACCENT)
    skirt = build_skirt()
    face = build_face(head)
    acc = build_accessories(body, head)
    for o in face:
        rigid_group(o, "Head")
    for o in (hair, band, head):
        rigid_group(o, "Head")
    rig = build_armature()
    organic = K.join([body, tunic, belt, skirt, focale], "Nobody")
    mesh = bind(rig, organic, face + acc + [head, hair, band])
    blade, shield = build_props(rig)
    return rig, mesh, blade, shield


def pose(rig, rots):
    """Pose bones by world-axis rotations at their heads: {bone: (axis, degrees)}."""
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    for name, (axis, deg) in rots.items():
        pb = rig.pose.bones[name]
        rest = pb.bone.matrix_local.to_3x3()
        world = Matrix.Rotation(math.radians(deg), 3, axis)
        q = (rest.inverted() @ world @ rest).to_quaternion()
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
        p = os.path.join(K.SHOTS_DIR, "a1v3_%s.png" % tag)
        r.filepath = p
        bpy.ops.render.render(write_still=True)
        out[tag] = p

    shoot("front", (0, -8, 0.95), (0, 0, 0.85), ortho=2.6)
    shoot("back", (0, 8, 0.95), (0, 0, 0.85), ortho=2.6)
    shoot("threequarter", (-5.5, -6.5, 3.4), (0, 0, 0.8), ortho=2.9)
    shoot("gamecam", (0, 6.2, 3.1), (0, 0, 0.9), res=(1920, 1080), lens=30)
    # posed: a run frame (arms and legs swinging, as PartPose.Swing does) and an overhead swing of the gladius
    old_root.location = (50, 0, 0)
    rig.location = (0, 0, 0)
    pose(rig, {"ArmL": ("X", 35), "ArmR": ("X", -40), "LegL": ("X", -30), "LegR": ("X", 30), "Body": ("X", 8)})
    shoot("run", (-4.2, -5.0, 1.6), (0, 0, 0.75), ortho=2.0)
    pose(rig, {"ArmR": ("X", -125), "ArmL": ("Y", -25), "Body": ("Z", 20), "Head": ("Z", -10)})
    shoot("attack", (-4.2, -5.0, 1.6), (0, 0, 0.85), ortho=2.2)
    pose(rig, {})
    return out


rig, mesh, blade, shield = build()
info = {"tris": K.tri_count([mesh, blade, shield]), "body_tris": K.tri_count([mesh]),
        "renderers": 3, "groups": [g.name for g in mesh.vertex_groups]}
lo, hi = K.world_bounds([mesh, blade, shield])
info["height"] = round(hi.z - lo.z, 3)
if ARGS.get("export") == "1":
    path = os.path.join(K.REPO, "Assets", "Art", "Prototype", "CH_Nobody_V3.fbx")
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
    info["blend"] = K.save_blend("ch_nobody_v3")
if ARGS.get("compare") == "1":
    info["compare"] = compare(rig)
result = info
