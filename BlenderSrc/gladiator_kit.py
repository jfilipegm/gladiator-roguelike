"""gladiator_kit — shared helpers for procedural asset scripts (run inside Blender via Tools/blender_bridge.py).

Conventions (CLAUDE.md §3.3, §7):
  * 1 Blender unit = 1 m. Z up. An asset's FRONT faces -Y (Blender "Front" view looks at it).
    After export (see export_fbx) the front faces +Z in Unity and Blender +X (the character's left) lands on
    Unity -X (the character's left). Verified by the EditMode test ModelPipelineTests.
  * Every face gets its UVs collapsed onto one palette swatch centre (palette.json), so each asset
    uses the single shared palette material and batches.
  * Origins: characters/props at the base, rigid parts at their joint.
  * Naming: CH_ characters, EN_ enemies, WP_ weapons, SM_ environment, PR_ props.
"""
import json
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PALETTE_DIR = os.path.join(REPO, "Assets", "Art", "Textures", "Palette")
MODELS_DIR = os.path.join(REPO, "Assets", "Art", "Models")
BLEND_DIR = os.path.join(REPO, "BlenderSrc", "blend")
SHOTS_DIR = os.path.join(REPO, "Screenshots", "blender")
PALETTE_MAT = "M_Palette"

_palette_cache = None


# --------------------------------------------------------------------------- palette

def palette():
    """swatch name -> (u, v) at the swatch centre. Reloads if palette.json changed."""
    global _palette_cache
    path = os.path.join(PALETTE_DIR, "palette.json")
    mtime = os.path.getmtime(path)
    if _palette_cache and _palette_cache[0] == mtime:
        return _palette_cache[1]
    data = json.load(open(path))
    grid = data["size"] // data["cell"]
    uv = {}
    for r, row in enumerate(data["rows"]):
        for c, sw in enumerate(row["swatches"]):
            uv[sw[0]] = ((c + 0.5) / grid, 1.0 - (r + 0.5) / grid)
    _palette_cache = (mtime, uv)
    return uv


def palette_material():
    """Preview material: PAL_Master.png, closest filtering. Unity ignores it (shared materials are assigned there)."""
    mat = bpy.data.materials.get(PALETTE_MAT)
    if mat:
        return mat
    mat = bpy.data.materials.new(PALETTE_MAT)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    img_path = os.path.join(PALETTE_DIR, "PAL_Master.png")
    img = bpy.data.images.get("PAL_Master.png") or bpy.data.images.load(img_path)
    img.reload()
    img.alpha_mode = "NONE"  # palette alpha = 1 - emission, not transparency
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0
    return mat


def set_swatch(obj, swatch, faces=None):
    """Collapse the UVs of `faces` (indices; default all) of obj onto one palette swatch."""
    u, v = palette()[swatch]
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uvl = me.uv_layers.active.data
    targets = range(len(me.polygons)) if faces is None else faces
    for fi in targets:
        for li in me.polygons[fi].loop_indices:
            uvl[li].uv = (u, v)
    if PALETTE_MAT not in [m.name for m in me.materials if m]:
        me.materials.append(palette_material())


def set_swatch_where(obj, swatch, predicate):
    """Assign a swatch to faces whose (local) centre/normal satisfy predicate(center, normal)."""
    faces = [p.index for p in obj.data.polygons if predicate(p.center, p.normal)]
    set_swatch(obj, swatch, faces)
    return len(faces)


# --------------------------------------------------------------------------- scene

def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.cameras, bpy.data.lights, bpy.data.curves):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)
    for c in list(bpy.context.scene.collection.children):
        bpy.context.scene.collection.children.unlink(c)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0


def _link(obj, parent=None):
    bpy.context.scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse = Matrix.Identity(4)
    return obj


def object_from_bmesh(name, bm, location=(0, 0, 0), parent=None, swatch=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False  # flat shading
    obj = bpy.data.objects.new(name, me)
    obj.location = location
    _link(obj, parent)
    if swatch:
        set_swatch(obj, swatch)
    return obj


def empty(name, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_size = 0.15
    obj.location = location
    return _link(obj, parent)


# --------------------------------------------------------------------------- primitives
# All primitives take `pivot`: a point in the shape's own unit box space, (0,0,0)=centre,
# (0,0,-1)=bottom centre, (0,1,0)=back centre etc. The object origin sits there.

def _pivot_offset(size, pivot):
    return Vector((-pivot[0] * size[0] / 2, -pivot[1] * size[1] / 2, -pivot[2] * size[2] / 2))


def box(name, size=(1, 1, 1), location=(0, 0, 0), pivot=(0, 0, -1), bevel=0.0, taper=(1.0, 1.0),
        swatch="stone_grey", parent=None, segments=1):
    """Box of `size` (x, y, z). taper scales the top face (x, y). bevel chamfers edges (metres)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        if v.co.z > 0:
            v.co.x *= taper[0]
            v.co.y *= taper[1]
        v.co.x *= size[0]
        v.co.y *= size[1]
        v.co.z *= size[2]
    off = _pivot_offset(size, pivot)
    for v in bm.verts:
        v.co += off
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segments, affect="EDGES",
                        profile=0.5, clamp_overlap=True)
    return object_from_bmesh(name, bm, location, parent, swatch)


def cylinder(name, radius=0.5, depth=1.0, verts=8, location=(0, 0, 0), pivot=(0, 0, -1), taper=1.0,
             swatch="stone_grey", parent=None, axis="Z", cap=True):
    """Low-poly prism along `axis`. taper scales the +axis end radius. pivot in unit-box space."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=verts, radius1=radius,
                          radius2=radius * taper, depth=depth)
    off = _pivot_offset((radius * 2, radius * 2, depth), pivot)
    for v in bm.verts:
        v.co += off
    if axis != "Z":
        rot = Matrix.Rotation(math.radians(90), 4, "Y" if axis == "X" else "X")
        if axis == "Y":
            rot = Matrix.Rotation(math.radians(-90), 4, "X")
        bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
    return object_from_bmesh(name, bm, location, parent, swatch)


def cone(name, radius=0.5, depth=1.0, verts=8, **kw):
    return cylinder(name, radius=radius, depth=depth, verts=verts, taper=0.0001, **kw)


def ico(name, radius=0.5, subdiv=1, location=(0, 0, 0), scale=(1, 1, 1), pivot_z=0.0, swatch="stone_grey",
        parent=None):
    """Faceted sphere (icosphere). pivot_z: -1 puts the origin at the bottom."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    for v in bm.verts:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2] - pivot_z * radius * scale[2]))
    return object_from_bmesh(name, bm, location, parent, swatch)


def wedge(name, size=(1, 1, 1), location=(0, 0, 0), pivot=(0, 0, -1), swatch="stone_grey", parent=None):
    """Ramp: full height at +Y (back), zero height at -Y (front)."""
    bm = bmesh.new()
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    pts = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz), (-sx, sy, sz), (sx, sy, sz)]
    vs = [bm.verts.new(p) for p in pts]
    for f in ((0, 3, 2, 1), (0, 1, 5, 4), (1, 2, 5), (0, 4, 3), (2, 3, 4, 5)):
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    off = _pivot_offset(size, pivot)
    for v in bm.verts:
        v.co += off
    return object_from_bmesh(name, bm, location, parent, swatch)


def extrude_poly(name, points, thickness, plane="YZ", location=(0, 0, 0), swatch="stone_grey", parent=None):
    """Convex-or-not 2D outline extruded symmetrically along the plane's normal axis.

    plane="YZ": points are (y, z), thickness along X (blades held forward: edges up/down, flats left/right).
    plane="XZ": points are (x, z), thickness along Y (banners, shield faces seen from the front).
    plane="XY": points are (x, y), thickness along Z (floor decals, flat plates).
    The origin is the 2D origin of the outline."""
    bm = bmesh.new()
    h = thickness / 2

    def to3(p, d):
        if plane == "YZ":
            return Vector((d, p[0], p[1]))
        if plane == "XZ":
            return Vector((p[0], d, p[1]))
        return Vector((p[0], p[1], d))

    front = [bm.verts.new(to3(p, -h)) for p in points]
    back = [bm.verts.new(to3(p, h)) for p in points]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(points)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([front[i], front[j], back[j], back[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    return object_from_bmesh(name, bm, location, parent, swatch)


# --------------------------------------------------------------------------- ops

def mirror_x(obj, new_name=None):
    """Mirror copy across the X axis (world), keeping flat normals outward. Returns the new object."""
    dup = obj.copy()
    dup.data = obj.data.copy()
    dup.name = new_name or obj.name.replace("L", "R", 1)
    bpy.context.scene.collection.objects.link(dup)
    dup.parent = obj.parent
    dup.location.x = -obj.location.x
    bm = bmesh.new()
    bm.from_mesh(dup.data)
    bmesh.ops.scale(bm, vec=(-1, 1, 1), verts=bm.verts)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(dup.data)
    bm.free()
    dup.rotation_euler = (obj.rotation_euler.x, -obj.rotation_euler.y, -obj.rotation_euler.z)
    return dup


def join(objs, name):
    """Join meshes into one object (keeps the first object's origin)."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    objs[0].name = name
    objs[0].data.name = name
    return objs[0]


def descendants(obj):
    out = [obj]
    for c in obj.children:
        out.extend(descendants(c))
    return out


def tri_count(objs):
    n = 0
    for o in objs:
        if o.type == "MESH":
            n += sum(len(p.vertices) - 2 for p in o.data.polygons)
    return n


def world_bounds(objs):
    bpy.context.view_layer.update()  # matrix_world of freshly created/parented objects is stale until this
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


# --------------------------------------------------------------------------- export / save

def export_fbx(root, rel_path):
    """Export `root` and its descendants to Assets/Art/Models/<rel_path>.fbx. Returns the absolute path.

    Axis handling (verified M0.4, see docs/DECISIONS.md): write Blender's native Z-up axes and let Unity's
    `bakeAxisConversion` (ModelImportRules) convert, because Blender's `bake_space_transform` mis-converts EMPTY
    nodes (rigid-part rigs are full of them). Unity's conversion maps Blender -Y to -Z, so the root gets a temporary
    180 deg yaw here: asset front (Blender -Y) arrives at Unity +Z, asset left (Blender +X) at Unity -X, every
    part keeps identity rotation, and only the FBX root node carries the 180 deg yaw (under an identity prefab root).
    """
    import math
    path = os.path.join(MODELS_DIR, rel_path if rel_path.endswith(".fbx") else rel_path + ".fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in descendants(root):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    saved_rot = root.rotation_euler.copy()
    root.rotation_euler.z += math.pi
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
            axis_forward="Y", axis_up="Z", bake_space_transform=False,
            mesh_smooth_type="FACE", use_mesh_modifiers=True, use_tspace=False, colors_type="NONE",
            add_leaf_bones=False, bake_anim=False, use_custom_props=False,
            path_mode="STRIP", embed_textures=False)
    finally:
        root.rotation_euler = saved_rot
    return path


def save_blend(name):
    os.makedirs(BLEND_DIR, exist_ok=True)
    path = os.path.join(BLEND_DIR, name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, check_existing=False)
    return path


# --------------------------------------------------------------------------- review renders

_VIEWS = {
    # name: direction FROM the target TO the camera (asset front faces -Y)
    "front": Vector((0, -1, 0.15)),
    "side": Vector((1, 0, 0.15)),
    "three_quarter": Vector((0.8, -1, 0.55)),
}


def _set_engine(scene):
    for eng in ("BLENDER_WORKBENCH", "BLENDER_EEVEE"):
        try:
            scene.render.engine = eng
            return eng
        except TypeError:
            continue
    return scene.render.engine


def review_render(root, name, size=480, sil_px=64):
    """Front / side / 3-4 renders + a flat 64 px silhouette, composed into Screenshots/blender/<name>_review.png.

    Returns dict with paths, tri count and bounds."""
    import numpy as np

    os.makedirs(SHOTS_DIR, exist_ok=True)
    scene = bpy.context.scene
    objs = [o for o in descendants(root) if o.type == "MESH"]
    lo, hi = world_bounds(objs)
    center = (lo + hi) / 2
    extent = max(hi - lo)

    hidden = []
    for o in scene.objects:
        if o.type == "MESH" and o not in objs and not o.hide_render:
            o.hide_render = True
            hidden.append(o)

    cam_data = bpy.data.cameras.new("_ReviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = extent * 1.7
    cam = bpy.data.objects.new("_ReviewCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    engine = _set_engine(scene)
    r = scene.render
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "Standard"
    shading = scene.display.shading
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world

    def aim(direction):
        d = direction.normalized()
        cam.location = center + d * (extent * 4)
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        cam_data.clip_end = extent * 10

    def render_to(path, res_x, res_y):
        r.resolution_x, r.resolution_y, r.resolution_percentage = res_x, res_y, 100
        r.filepath = path
        bpy.ops.render.render(write_still=True)
        return path

    paths = {}
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_cavity = False
    shading.show_object_outline = False
    shading.show_shadows = False
    world.color = (0.32, 0.36, 0.40)
    for view, d in _VIEWS.items():
        aim(d)
        paths[view] = render_to(os.path.join(SHOTS_DIR, f"{name}_{view}.png"), size, size)

    # silhouette: flat black on white, sil_px tall, from 3/4
    shading.light = "FLAT"
    shading.color_type = "SINGLE"
    shading.single_color = (0, 0, 0)
    world.color = (1, 1, 1)
    aim(_VIEWS["three_quarter"])
    paths["silhouette"] = render_to(os.path.join(SHOTS_DIR, f"{name}_sil{sil_px}.png"), sil_px, sil_px)

    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)
    for o in hidden:
        o.hide_render = False

    # contact sheet: [front | side | 3/4 | silhouette upscaled (nearest)]
    def load(p):
        img = bpy.data.images.load(p, check_existing=False)
        w, h = img.size
        arr = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
        bpy.data.images.remove(img)
        return arr

    tiles = [load(paths[v]) for v in ("front", "side", "three_quarter")]
    sil = load(paths["silhouette"])
    k = size // sil_px
    sil_up = np.repeat(np.repeat(sil, k, axis=0), k, axis=1)
    pad = np.ones((size, size, 4), dtype=np.float32)
    pad[: sil_up.shape[0], : sil_up.shape[1]] = sil_up
    sheet = np.concatenate(tiles + [pad], axis=1)
    out = bpy.data.images.new("_sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels = sheet.ravel()
    sheet_path = os.path.join(SHOTS_DIR, f"{name}_review.png")
    out.filepath_raw = sheet_path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)

    return {"sheet": sheet_path, "engine": engine, "tris": tri_count(objs),
            "bounds_min": tuple(round(x, 4) for x in lo), "bounds_max": tuple(round(x, 4) for x in hi)}
