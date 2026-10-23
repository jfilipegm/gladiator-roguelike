"""CH_Nobody_V5 — A1 prototype 4: lineup variant A, rigged (docs/tracks/A1-art-direction.md).

The producer picked variant A of the style lineup (2026-10-23, "my favorite so far"): chibi proportions with CH_Nobody's
exact joints, simple OSRS-like angular shapes (chamfered boxy sections, plain tapered limbs, no muscles), flat-faceted
shading. V5 is that body exactly (ch_nobody_lineup.Builder with CHIBI, flat), made one skinned mesh on six bones named
as the old parts, with rule-based weights (each piece follows its bone; the neck, the shoulder corners, the sleeve and
the tunic's hem over the thighs are blended so they bend). One change: the gladius blade is grey iron with bright
edges (the rust-brown blade read as a wooden club). The gladius and the shield are separate objects on the arm bones.
ARGS: export=1 writes Assets/Art/Prototype/CH_Nobody_V5.fbx; compare=1 renders it beside the original and posed.
"""
import importlib
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
sys.path.insert(0, os.path.join(K.REPO, "BlenderSrc", "characters", "proto"))
import ch_nobody_lineup as L
importlib.reload(L)

P = L.CHIBI
H, SZ, SX, N, HH, AL, LX = P["H"], P["S"], P["SX"], P["N"], P["HH"], P["AL"], P["LX"]
AX = SX + 0.02   # the arm's axis (the shoulder joint), 0.285 like CH_Nobody's 0.28
BONES = {
    "Body": ((0, 0, H), (0, 0, SZ), None),
    "Head": ((0, 0, N), (0, 0, N + HH), "Body"),
    "ArmL": ((AX, 0, SZ), (AX, -0.02, SZ - AL - 0.06), "Body"),
    "ArmR": ((-AX, 0, SZ), (-AX, -0.02, SZ - AL - 0.06), "Body"),
    "LegL": ((LX, 0, H), (LX, 0, 0.08), None),
    "LegR": ((-LX, 0, H), (-LX, 0, 0.08), None),
}


def bone_of(name):
    base = name.split(".")[0]
    for prefix, bone in (("BandTail", "Head"), ("Headband", "Head"), ("Hair", "Head"), ("Head", "Head"),
                         ("Brow", "Head"), ("Eye", "Head"), ("Nose", "Head"), ("Sleeve", "ArmL"),
                         ("Pauldron", "ArmL"), ("Greave", "LegL"), ("Tunic", "Body"), ("Belt", "Body"),
                         ("Buckle", "Body"), ("Focale", "Body")):
        if base.startswith(prefix):
            return bone
    for prefix in ("Leg", "Foot", "Strap"):
        if base.startswith(prefix):
            return "Leg" + base[-1]
    for prefix in ("Arm", "Wrap", "Fist"):
        if base.startswith(prefix):
            return "Arm" + base[-1]
    raise ValueError("no bone for " + name)


def weights(bone, co):
    side = "L" if co.x >= 0 else "R"
    if bone == "Body":
        if co.z > N - 0.03:                          # the neck, shared with the head
            return {"Body": 0.5, "Head": 0.5}
        if co.z < H - 0.02:                          # the tunic's hem follows the thighs
            k = min(0.75, (H - co.z) / 0.15)
            if abs(co.x) < 0.03:
                return {"Body": 1 - k, "LegL": k * 0.5, "LegR": k * 0.5}
            return {"Body": 1 - k, "Leg" + side: k}
        if co.z > SZ - 0.05 and abs(co.x) > 0.17:   # the shoulder's corner rides a little with the arm
            return {"Body": 0.7, "Arm" + side: 0.3}
        return {"Body": 1.0}
    if bone in ("ArmL", "ArmR"):
        if co.z > SZ - 0.01:
            return {bone: 0.7, "Body": 0.3}
        return {bone: 1.0}
    if bone == "Head" and co.z < N + 0.04:
        return {"Head": 0.5, "Body": 0.5}
    return {bone: 1.0}


def weigh(obj, bone):
    bpy.context.view_layer.update()
    groups = {}
    for v in obj.data.vertices:
        co = obj.matrix_world @ v.co
        for b, w in weights(bone, co).items():
            g = groups.get(b) or obj.vertex_groups.new(name=b)
            groups[b] = g
            g.add([v.index], w, "REPLACE")


def build_armature():
    arm_data = bpy.data.armatures.new("CH_Nobody_V5_Rig")
    rig = bpy.data.objects.new("CH_Nobody_V5", arm_data)
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


def on_bone(obj, rig, bone):
    bpy.context.view_layer.update()
    mw = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = mw


def build():
    K.clear_scene()
    parts = L.Builder(P, soft=False, ox=0.0).build()
    by = {o.name.split(".")[0]: o for o in parts}
    body_parts = [o for o in parts if o.type == "MESH" and o.name.split(".")[0] not in
                  ("Blade", "Guard", "Pommel", "Shield", "ShieldRim")]
    # the gladius: one mesh, a grey iron blade with bright edges, the bronze guard and pommel
    blade = by["Blade"]
    K.set_swatch(blade, "iron")
    K.set_swatch_where(blade, "steel_l", lambda c, n: abs(n.x) < 0.5)
    sword_empty = blade.parent
    gladius = K.join([blade, by["Guard"], by["Pommel"]], "Gladius")
    bpy.context.view_layer.update()
    mw = gladius.matrix_world.copy()
    gladius.parent = None
    gladius.matrix_world = mw
    bpy.data.objects.remove(sword_empty, do_unlink=True)
    shield = K.join([by["Shield"], by["ShieldRim"]], "Shield")
    for o in body_parts:
        weigh(o, bone_of(o.name))
    rig = build_armature()
    mesh = K.join(body_parts, "Nobody")
    mesh.parent = rig
    mod = mesh.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    on_bone(gladius, rig, "ArmR")
    on_bone(shield, rig, "ArmL")
    return rig, mesh, gladius, shield


def pose(rig, rots):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    for name, (axis, deg) in rots.items():
        pb = rig.pose.bones[name]
        rest = pb.bone.matrix_local.to_3x3()
        pb.rotation_quaternion = (rest.inverted() @ Matrix.Rotation(math.radians(deg), 3, axis) @ rest).to_quaternion()
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

    def shoot(tag, loc, target, ortho, res=(960, 640)):
        cam.location = loc
        d = (Vector(target) - Vector(loc)).normalized()
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        cam_data.type = "ORTHO"
        cam_data.ortho_scale = ortho
        cam_data.clip_end = 100
        r.resolution_x, r.resolution_y, r.resolution_percentage = res[0], res[1], 100
        p = os.path.join(K.SHOTS_DIR, "a1v5_%s.png" % tag)
        r.filepath = p
        bpy.ops.render.render(write_still=True)
        out[tag] = p

    shoot("front", (0, -8, 0.95), (0, 0, 0.82), 2.6)
    shoot("back", (0, 8, 0.95), (0, 0, 0.82), 2.6)
    shoot("threequarter", (-5.5, -6.5, 3.4), (0, 0, 0.8), 2.9)
    old_root.location = (50, 0, 0)
    rig.location = (0, 0, 0)
    pose(rig, {"ArmL": ("X", 35), "ArmR": ("X", -40), "LegL": ("X", -30), "LegR": ("X", 30), "Body": ("X", 8)})
    shoot("run", (-4.2, -5.0, 1.6), (0, 0, 0.72), 1.9)
    pose(rig, {"ArmR": ("X", -125), "ArmL": ("Y", -25), "Body": ("Z", 20), "Head": ("Z", -10)})
    shoot("attack", (-4.2, -5.0, 1.6), (0, 0, 0.8), 2.1)
    pose(rig, {})
    return out


rig, mesh, gladius, shield = build()
info = {"tris": K.tri_count([mesh, gladius, shield]), "body_tris": K.tri_count([mesh]), "renderers": 3,
        "groups": sorted(g.name for g in mesh.vertex_groups)}
lo, hi = K.world_bounds([mesh, gladius, shield])
info["height"] = round(hi.z - lo.z, 3)
if ARGS.get("export") == "1":
    path = os.path.join(K.REPO, "Assets", "Art", "Prototype", "CH_Nobody_V5.fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in (rig, mesh, gladius, shield):
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                             axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             use_armature_deform_only=True, primary_bone_axis="Y", secondary_bone_axis="X",
                             path_mode="STRIP", embed_textures=False)
    info["fbx"] = path
    info["blend"] = K.save_blend("ch_nobody_v5")
if ARGS.get("compare") == "1":
    info["compare"] = compare(rig)
result = info
