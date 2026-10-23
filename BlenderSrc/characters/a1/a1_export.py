"""A1 in-game: turns an A1 character (loose pieces from simple_kit) into the game's rigid-part model.

The game animates characters as rigid parts found by name (Assets/Scripts/Gameplay/Animation/PartRig.cs): Body,
Head, ArmL, ArmR, LegL, LegR under the model's root, Head and the arms under Body, the legs under the root (a beast's
front legs are ArmL/ArmR, its hind legs LegL/LegR; a part may be missing, e.g. a litter with no legs). Each A1 part is
ONE mesh (its pieces joined) whose origin is the joint it turns about, so a character is 4-8 renderers instead of the
originals' 20-90, with no code change in the game. Pieces that must not move with any part (rare) go to "Root".

Use (docs/tracks/A1-ingame-contract.md):
    parts = build()                                   # the script's pieces, built at ox = 0 when exporting
    J = X.joints(a["p"])                              # the humanoid's joints (scale/offset them if the script did)
    info = X.export("CH_Nobody", "Characters", parts, J, rules=[("Gladius", "ArmR"), ("Shield", "ArmL")])
`rules` are (name prefix, part) checked before the defaults; a part of "side" means ArmL/ArmR, LegL/LegR by the
piece's own L/R suffix. Unknown pieces stop the export with their names. The result has the FBX, tris, renderers,
height and a pose sheet (Screenshots/a1/ingame/a1_<root>_poses.png: rest, run, swing and hit poses, every part
turned) to prove each piece follows its part.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K

PARTS = ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR")
PARENT = {"Body": None, "Head": "Body", "ArmL": "Body", "ArmR": "Body", "LegL": None, "LegR": None}
SHOTS = "/home/jfilipegm/Gladiator Roguelike/Screenshots/a1/ingame"

# simple_kit.humanoid's piece names -> part ("side": the piece's L/R suffix picks ArmL/ArmR or LegL/LegR)
DEFAULT_RULES = (
    ("BandTail", "Head"), ("Headband", "Head"), ("Hair", "Head"), ("Head", "Head"), ("Brow", "Head"),
    ("Eye", "Head"), ("Nose", "Head"), ("Mouth", "Head"), ("Ear", "Head"), ("Beard", "Head"), ("Helm", "Head"),
    ("Hood", "Head"), ("Crest", "Head"), ("Plume", "Head"),
    ("Torso", "Body"), ("Chest", "Body"), ("Tunic", "Body"), ("Belt", "Body"), ("Buckle", "Body"), ("Focale", "Body"),
    ("Neck", "Body"), ("Skirt", "Body"), ("Cape", "Body"), ("Cloak", "Body"), ("Sash", "Body"), ("Hips", "Body"),
    ("Leg", "Leg"), ("Foot", "Leg"), ("Strap", "Leg"), ("Greave", "Leg"), ("Shin", "Leg"),
    ("Arm", "Arm"), ("Fist", "Arm"), ("Hand", "Arm"), ("Wrap", "Arm"), ("Sleeve", "Arm"), ("Bracer", "Arm"),
)


def base(o):
    return o.name.split(".")[0]


def joints(p, scale=1.0, offset=(0.0, 0.0, 0.0)):
    """simple_kit.humanoid's joints for proportions p (the arm's axis 0.02 outside the shoulder, as CH_Nobody_V5).
    If the script scaled or moved the whole character after building it, pass the same scale (about the origin)
    and offset."""
    ox, oy, oz = offset
    H, S, SX, N, LX = p["H"], p["S"], p["SX"], p["N"], p["LX"]
    ax = SX + 0.02
    raw = {"Body": (0, 0, H), "Head": (0, 0, N), "ArmL": (ax, 0, S), "ArmR": (-ax, 0, S), "LegL": (LX, 0, H),
           "LegR": (-LX, 0, H)}
    return {k: Vector((x * scale + ox, y * scale + oy, z * scale + oz)) for k, (x, y, z) in raw.items()}


def _part_of(name, centre_x, rules):
    for prefix, part in tuple(rules) + DEFAULT_RULES:
        if name.startswith(prefix):
            if part in ("side", "Arm", "Leg"):
                kind = part if part != "side" else ("Arm" if name.startswith(("Arm", "Fist", "Hand")) else "Leg")
                s = name[-1] if name[-1] in "LR" else ("L" if centre_x >= 0 else "R")
                return kind + s
            return part
    return None


def _bake(objs):
    """Every MESH in objs and their descendants, with modifiers and parents applied into the mesh (world space)."""
    bpy.context.view_layer.update()
    seen, out = set(), []
    for o in objs:
        for d in K.descendants(o):
            if d.type == "MESH" and d.name not in seen:
                seen.add(d.name)
                out.append(d)
    dg = bpy.context.evaluated_depsgraph_get()
    baked = []
    for o in out:
        ev = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        me.transform(o.matrix_world)
        n = bpy.data.objects.new(base(o), me)
        bpy.context.scene.collection.objects.link(n)
        baked.append(n)
    for o in list(objs):
        for d in reversed(K.descendants(o)):
            bpy.data.objects.remove(d, do_unlink=True)
    return baked


def export(root_name, folder, parts, J, rules=(), export_fbx=True, pose_sheet=True):
    """Builds root_name (an EMPTY at the origin) with one mesh per part, origins at the joints J, and exports
    Assets/Art/Models/A1/<folder>/<root_name>.fbx. Returns info."""
    pieces = _bake(parts)
    groups, unknown = {}, []
    for o in pieces:
        c = sum((Vector(v.co) for v in o.data.vertices), Vector()) / max(1, len(o.data.vertices))
        part = _part_of(base(o), c.x, rules)
        if part is None:
            unknown.append(base(o))
        else:
            groups.setdefault(part, []).append(o)
    if unknown:
        raise ValueError("no part for: " + ", ".join(sorted(set(unknown))) + " (add rules)")
    missing = [p for p in groups if p in PARTS and p not in J]
    if missing:
        raise ValueError("no joint for: " + ", ".join(missing))
    root = K.empty(root_name)
    made, pivots = {}, {}
    order = [p for p in PARTS if p in groups] + [p for p in groups if p not in PARTS]
    for part in order:
        objs = groups[part]
        obj = K.join(objs, part) if len(objs) > 1 else objs[0]
        obj.name, obj.data.name = part, part
        pivots[part] = Vector(J[part]) if part in J else Vector((0, 0, 0))
        obj.data.transform(Matrix.Translation(-pivots[part]))
        made[part] = obj
    for part, obj in made.items():
        pp = PARENT.get(part)
        if pp is not None and pp not in made:
            raise ValueError(part + " needs " + pp)
        obj.parent = made[pp] if pp is not None else root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.location = pivots[part] - (pivots[pp] if pp is not None else Vector())
    bpy.context.view_layer.update()
    meshes = [o for o in K.descendants(root) if o.type == "MESH"]
    lo, hi = K.world_bounds(meshes)
    info = {"root": root_name, "tris": K.tri_count(meshes), "renderers": len(meshes), "parts": sorted(made),
            "height": round(hi.z - lo.z, 3), "width": round(hi.x - lo.x, 3), "depth": round(hi.y - lo.y, 3)}
    if pose_sheet:
        info["poses"] = poses(root, made)
    if export_fbx:
        info["fbx"] = K.export_fbx(root, os.path.join("A1", folder, root_name))
    return info


def _turn(made, rots):
    for part, o in made.items():
        o.rotation_euler = (0, 0, 0)
    for part, (axis, deg) in rots.items():
        if part in made:
            e = [0.0, 0.0, 0.0]
            e["XYZ".index(axis)] = math.radians(deg)
            made[part].rotation_euler = e
    bpy.context.view_layer.update()


POSES = (
    ("rest", {}),
    # Blender: front = -Y; an arm/leg hanging down swings forward with a negative X rotation
    ("run", {"ArmL": ("X", 40), "ArmR": ("X", -40), "LegL": ("X", -35), "LegR": ("X", 35), "Body": ("X", -8),
             "Head": ("X", 6)}),
    ("swing", {"ArmR": ("X", -120), "ArmL": ("X", 30), "Body": ("Z", 25), "Head": ("Z", -12)}),
    ("hit", {"Body": ("X", 18), "Head": ("X", 15), "ArmL": ("Y", -40), "ArmR": ("Y", 40), "LegL": ("X", 20),
             "LegR": ("X", -20)}),
)


def poses(root, made):
    """Renders the model in four poses from the front three-quarter into one sheet; every piece must follow its
    part (a sword left floating, a cape that stays put while the body leans, a sleeve that leaves the arm = a rule
    to fix)."""
    import numpy as np
    os.makedirs(SHOTS, exist_ok=True)
    scene = bpy.context.scene
    K._set_engine(scene)
    r = scene.render
    r.film_transparent = False
    r.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    sh = scene.display.shading
    sh.light, sh.color_type, sh.show_cavity, sh.show_shadows = "STUDIO", "TEXTURE", False, False
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.color = (0.36, 0.33, 0.29)
    cam_data = bpy.data.cameras.new("_PoseCam")
    cam = bpy.data.objects.new("_PoseCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    meshes = [o for o in K.descendants(root) if o.type == "MESH"]
    lo, hi = K.world_bounds(meshes)
    h = max(hi.z - lo.z, hi.y - lo.y, hi.x - lo.x)
    cz = (lo.z + hi.z) * 0.5
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = h * 1.9
    cam_data.clip_end = 200
    loc = Vector((-0.75, -1.0, 0.55)).normalized() * (h * 8 + 6) + Vector((0, 0, cz))
    cam.location = loc
    cam.rotation_euler = (Vector((0, 0, cz)) - loc).normalized().to_track_quat("-Z", "Y").to_euler()
    r.resolution_x, r.resolution_y, r.resolution_percentage = 480, 520, 100
    tiles, paths = [], []
    for tag, rots in POSES:
        _turn(made, rots)
        pth = os.path.join(SHOTS, "_%s_%s.png" % (base(root), tag))
        r.filepath = pth
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(pth, check_existing=False)
        w, hh = img.size
        tiles.append(np.array(img.pixels[:], dtype=np.float32).reshape(hh, w, 4))
        bpy.data.images.remove(img)
        os.remove(pth)
    _turn(made, {})
    bpy.data.objects.remove(cam, do_unlink=True)
    sheet = np.concatenate(tiles, axis=1)
    out = bpy.data.images.new("_poses", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels = sheet.ravel()
    path = os.path.join(SHOTS, "a1_%s_poses.png" % base(root))
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    return path
