"""R6 Skins: builds the skins of BlenderSrc/characters/skins.json (docs/tracks/R7-design.md §4, R7-contract.md wave 3).

A skin is its gladiator's own model with some palette swatches swapped (§4.2): for each skin this driver sets
GLADIATOR_SKIN (gladiator_kit.active_skin: suffix, remap, the held weapon's parts to keep) and runs the base character
script with runpy, so the parts, pivots, tris and animation are the base's and only the UVs point at other cells of
PAL_Master.png. One shared material, no new material; the base's .blend and review sheet are left alone.

    Tools/bheadless BlenderSrc/characters/build_skins.py skin=<id>|all [gladiator=<id>] [export=0]
        export Assets/Art/Models/Characters/Skins/<base><suffix>.fbx (export=0: build only)
    Tools/bheadless BlenderSrc/characters/build_skins.py gladiator=<id> review=1
        also renders Screenshots/blender/skins_<gladiator>.png: default, Market skin, champion skin (left to right),
        each front / 3-4 / from behind (the game camera), on a pale floor (marble) and a dark one (mud); the bottom
        row is the 64 px silhouette (3/4) of each, which must not change (result "silhouette_diff_px")
    Tools/bheadless BlenderSrc/characters/build_skins.py check=skins [gladiator=<id>]
        imports every skin FBX and its base FBX: same nodes, parents, tris and vertices; every face's UV cell is the
        base's cell through the remap (kept weapon parts: unchanged)
    Tools/bheadless BlenderSrc/characters/build_skins.py check=base [gladiator=<id>]
        runs the base scripts with no skin (their FBX, .blend and review sheet go to a temp folder) and compares the
        FBX with the committed one the same way: proves the kit's skin hook changes nothing without the variable
Keep each run short (one gladiator, or skin=all without review).
"""
import contextlib
import importlib
import json
import os
import runpy
import shutil
import tempfile

import bpy

import gladiator_kit as K

importlib.reload(K)

HERE = os.path.join(K.REPO, "BlenderSrc", "characters")
MODELS = os.path.join(K.REPO, "Assets", "Art", "Models")  # the committed models (K.MODELS_DIR can be redirected)
TABLE_PATH = os.path.join(HERE, "skins.json")
FORBIDDEN_FAMILIES = {"hazard", "enemy_accent", "pickup", "debug"}  # §4.2 rule 3 (+ pickups and debug)
REDS = {"rust", "rust_light", "red_banner", "red_banner_d", "crimson", "crimson_d", "blood"}  # orange-red = hurts you
FLOORS = (("pale", "marble"), ("dark", "mud_dark"))
VIEWS = (("front", (0, -1, 0.2)), ("three_quarter", (0.8, -1, 0.55)), ("back", (0.3, 1, 0.75)))
TILE, SIL = 240, 64


def load_table():
    return json.load(open(TABLE_PATH))


def palette_rows():
    """swatch -> (family, hex, emission)."""
    data = json.load(open(os.path.join(K.PALETTE_DIR, "palette.json")))
    out = {}
    for row in data["rows"]:
        for sw in row["swatches"]:
            out[sw[0]] = (row["family"], sw[1], float(sw[2]) if len(sw) > 2 else 0.0)
    return out


def validate(table):
    """The rules of §4.2 that data can break: names, palette-only targets, the colour language."""
    pal, errors, ids = palette_rows(), [], set()
    for s in table["skins"]:
        sid = s["id"]
        if sid in ids:
            errors.append(sid + ": duplicate id")
        ids.add(sid)
        if not sid.startswith(s["gladiator"] + "_"):
            errors.append(sid + ": id must be <gladiator>_<name>")
        if not s["suffix"].startswith("_") or not os.path.exists(os.path.join(HERE, s["script"])):
            errors.append(sid + ": bad suffix or script")
        if s["base"] not in table["keep"]:
            errors.append(sid + ": no keep entry for " + s["base"])
        if (s["price"] > 0) == bool(s["champion"]):
            errors.append(sid + ": exactly one of price > 0 / champion")
        if len(s["chips"]) != 3 or any(c not in pal for c in s["chips"]):
            errors.append(sid + ": chips must be three palette swatches")
        for m in s["remap"]:
            a, b = m["from"], m["to"]
            if a not in pal or b not in pal:
                errors.append("%s: %s -> %s not in the palette" % (sid, a, b))
                continue
            if pal[b][0] in FORBIDDEN_FAMILIES or b in REDS:
                errors.append("%s: %s is hazard / enemy / red (§4.2 rule 3)" % (sid, b))
            if pal[b][2] > 0 and pal[a][2] == 0:
                errors.append("%s: glow %s only replaces a glow (§4.2 rule 3)" % (sid, b))
            if pal[a][0] == "player_fx" and pal[b][0] != "player_fx":
                errors.append("%s: the player-colour mark %s must stay a player colour" % (sid, a))
    return errors


def env_of(table, s):
    return {"suffix": s["suffix"], "remap": {m["from"]: m["to"] for m in s["remap"]}, "keep": table["keep"][s["base"]]}


@contextlib.contextmanager
def patched_kit(skin_env=None, export=True, out_dir=None, quiet=True):
    """Runs a character script with GLADIATOR_SKIN set (or unset). The scripts reload gladiator_kit, so the kit is
    patched after every reload: quiet = no .blend (skins have none, §6.27; the base's is the base's) and no review
    sheet (the base's would be overwritten); export=False skips the FBX; out_dir sends the FBX, .blend and review
    sheet to a temp folder. Afterwards the kit is reloaded clean."""
    real = importlib.reload

    def reload(module):
        m = real(module)
        if getattr(m, "__name__", "") == "gladiator_kit":
            if out_dir:
                m.MODELS_DIR = os.path.join(out_dir, "Models")
                m.BLEND_DIR = os.path.join(out_dir, "blend")
                m.SHOTS_DIR = os.path.join(out_dir, "shots")
            if quiet:
                m.save_blend = lambda name: None
                m.review_render = lambda root, name, **kw: {"tris": m.tri_count(m.descendants(root))}
            if not export:
                m.export_fbx = lambda root, rel_path: None
        return m

    importlib.reload = reload
    if skin_env:
        os.environ["GLADIATOR_SKIN"] = json.dumps(skin_env)
    else:
        os.environ.pop("GLADIATOR_SKIN", None)
    try:
        yield
    finally:
        importlib.reload = real
        os.environ.pop("GLADIATOR_SKIN", None)
        real(K)


def run_script(script, skin_env=None, export=True, out_dir=None, quiet=True):
    """Runs BlenderSrc/characters/<script>; returns its globals (root, info, result)."""
    with patched_kit(skin_env, export, out_dir, quiet):
        return runpy.run_path(os.path.join(HERE, script), init_globals={"ARGS": {}, "REPO": K.REPO},
                              run_name="gl_script")


# --------------------------------------------------------------------------- review lineup

def _linear(hexcol):
    c = [int(hexcol.lstrip("#")[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def _render(path, res):
    r = bpy.context.scene.render
    r.resolution_x = r.resolution_y = res
    r.resolution_percentage = 100
    r.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def _pixels(path):
    import numpy as np
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    arr = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return arr


def render_variant(root, frame, tmp, tag):
    """Front / 3-4 / back on each floor, and the 64 px silhouette, of the model under root. Returns arrays."""
    from mathutils import Vector
    scene = bpy.context.scene
    center, extent = frame
    cam = scene.camera
    shading, world = scene.display.shading, scene.world
    pal = palette_rows()
    tiles = {}

    def aim(d):
        d = Vector(d).normalized()
        cam.location = center + d * (extent * 4)
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()

    shading.light, shading.color_type = "STUDIO", "TEXTURE"
    for floor, swatch in FLOORS:
        world.color = _linear(pal[swatch][1])
        for view, d in VIEWS:
            aim(d)
            tiles[(floor, view)] = _pixels(_render(os.path.join(tmp, "%s_%s_%s.png" % (tag, floor, view)), TILE))
    shading.light, shading.color_type, shading.single_color = "FLAT", "SINGLE", (0, 0, 0)
    world.color = (1, 1, 1)
    aim(VIEWS[1][1])
    sil = _pixels(_render(os.path.join(tmp, "%s_sil.png" % tag), SIL))
    return tiles, sil


def review(table, gladiator, export=True):
    """Builds the default and both skins of `gladiator`, renders the lineup sheet. Returns the sheet path + checks."""
    import numpy as np
    from mathutils import Vector
    skins = [s for s in table["skins"] if s["gladiator"] == gladiator]
    variants = [None] + skins
    tmp = tempfile.mkdtemp(prefix="skins_review_")
    scene = bpy.context.scene
    K._set_engine(scene)
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "Standard"
    scene.world = scene.world or bpy.data.worlds.new("World")
    d = scene.display.shading
    d.show_cavity = d.show_object_outline = d.show_shadows = False
    renders, frame, out = [], None, {}
    for v in variants:
        g = run_script(skins[0]["script"], env_of(table, v) if v else None, export=bool(v) and export)
        root = g["root"]
        if frame is None:
            lo, hi = K.world_bounds([o for o in K.descendants(root) if o.type == "MESH"])
            frame = ((lo + hi) / 2, max(hi - lo))
        cam_data = bpy.data.cameras.new("_LineupCam")
        cam_data.type, cam_data.ortho_scale, cam_data.clip_end = "ORTHO", frame[1] * 1.25, frame[1] * 10
        cam = bpy.data.objects.new("_LineupCam", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        renders.append(render_variant(root, frame, tmp, v["id"] if v else "default"))
        bpy.data.objects.remove(cam, do_unlink=True)
        bpy.data.cameras.remove(cam_data)
    # silhouettes: identical by construction (same mesh); counted anyway
    base_sil = renders[0][1][..., :3].mean(axis=2) < 0.5
    out["silhouette_diff_px"] = {v["id"]: int(np.count_nonzero((r[1][..., :3].mean(axis=2) < 0.5) != base_sil))
                                 for v, r in zip(variants[1:], renders[1:])}
    # sheet: rows pale / dark / silhouettes; per variant: front | 3-4 | back, a grey gap between variants
    gap = np.full((TILE, 8, 4), 0.5, dtype=np.float32)
    gap[..., 3] = 1
    rows = []
    for floor, _ in FLOORS:
        parts = []
        for k, (tiles, _) in enumerate(renders):
            if k:
                parts.append(gap)
            parts.extend(tiles[(floor, view)] for view, _ in VIEWS)
        rows.append(np.concatenate(parts, axis=1))
    sil_row = np.ones((SIL * 3, rows[0].shape[1], 4), dtype=np.float32)
    for k, (_, sil) in enumerate(renders):
        up = np.repeat(np.repeat(sil, 3, axis=0), 3, axis=1)
        x = k * (3 * TILE + 8) + TILE + (TILE - SIL * 3) // 2  # under the 3/4 view
        sil_row[:, x:x + SIL * 3] = up
    sheet = np.concatenate([sil_row] + rows[::-1], axis=0)  # Blender images are bottom-up: pale ends on top
    img = bpy.data.images.new("_skins_sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels = sheet.ravel()
    os.makedirs(K.SHOTS_DIR, exist_ok=True)
    out["sheet"] = os.path.join(K.SHOTS_DIR, "skins_%s.png" % gladiator)
    img.filepath_raw = out["sheet"]
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    shutil.rmtree(tmp, ignore_errors=True)
    return out


# --------------------------------------------------------------------------- checks

def _cells():
    """UV (rounded) -> swatch name, and the grid size."""
    data = json.load(open(os.path.join(K.PALETTE_DIR, "palette.json")))
    grid = data["size"] // data["cell"]
    names = {}
    for r, row in enumerate(data["rows"]):
        for c, sw in enumerate(row["swatches"]):
            names[(c, r)] = sw[0]
    return names, grid


def read_fbx(path):
    """{object name: (parent name, tris, sorted rounded verts, [swatch per face])} of an FBX, imported then removed."""
    names, grid = _cells()
    K.clear_scene()
    bpy.ops.import_scene.fbx(filepath=path)
    out = {}
    for o in bpy.data.objects:
        if o.type != "MESH":
            out[o.name] = (o.parent.name if o.parent else None, 0, (), ())
            continue
        me = o.data
        uv = me.uv_layers.active.data
        faces = []
        for p in me.polygons:
            u, v = uv[p.loop_indices[0]].uv
            faces.append(names.get((int(u * grid), int((1.0 - v) * grid)), "?"))
        verts = tuple(sorted(tuple(round(c, 4) for c in vt.co) for vt in me.vertices))
        out[o.name] = (o.parent.name if o.parent else None, sum(len(p.vertices) - 2 for p in me.polygons), verts,
                       tuple(faces))
    K.clear_scene()
    for coll in (bpy.data.meshes, bpy.data.materials):
        for d in list(coll):
            if d.users == 0 and d.name != K.PALETTE_MAT:
                coll.remove(d)
    return out


def compare(base, other, remap=None, keep=()):
    """Differences between two read_fbx() dicts; with a remap, other's faces must be base's faces remapped."""
    diffs = []
    if set(base) != set(other):
        diffs.append("nodes differ: %s" % sorted(set(base) ^ set(other)))
    for name in sorted(set(base) & set(other)):
        bp, bt, bv, bf = base[name]
        op, ot, ov, of = other[name]
        if bp != op or bt != ot or bv != ov:
            diffs.append("%s: parent/tris/verts differ (%s %s / %s %s)" % (name, bp, bt, op, ot))
            continue
        kept, n = False, name
        while n is not None:
            kept = kept or any(n.startswith(k) for k in keep)
            n = base[n][0] if n in base else None
        want = bf if (remap is None or kept) else tuple(remap.get(f, f) for f in bf)
        if want != of:
            bad = sorted({(w, f) for w, f in zip(want, of) if w != f})
            diffs.append("%s: UV cells %s" % (name, bad[:6]))
    return diffs


def check_skins(table, skins):
    out = {}
    for s in skins:
        base = read_fbx(os.path.join(MODELS, "Characters", s["base"] + ".fbx"))
        path = os.path.join(MODELS, "Characters", "Skins", s["base"] + s["suffix"] + ".fbx")
        if not os.path.exists(path):
            out[s["id"]] = "missing " + path
            continue
        skin = read_fbx(path)
        remapped = sum(1 for n, (_, _, _, f) in skin.items() if n in base for a, b in zip(base[n][3], f) if a != b)
        diffs = compare(base, skin, env_of(table, s)["remap"], table["keep"][s["base"]])
        out[s["id"]] = diffs or "ok (%d tris, %d faces remapped)" % (sum(v[1] for v in skin.values()), remapped)
    return out


def check_base(table, scripts):
    """Each base script with no skin, its outputs in a temp folder, against the committed FBX."""
    out = {}
    for script, base in scripts:
        tmp = tempfile.mkdtemp(prefix="skins_base_")
        run_script(script, None, export=True, out_dir=tmp, quiet=False)
        fresh = read_fbx(os.path.join(tmp, "Models", "Characters", base + ".fbx"))
        committed = read_fbx(os.path.join(MODELS, "Characters", base + ".fbx"))
        diffs = compare(committed, fresh)
        cells = sorted({f for v in fresh.values() for f in v[3]})
        out[base] = diffs or "identical: %d nodes, %d tris, UV cells %s" % (
            len(fresh), sum(v[1] for v in fresh.values()), cells)
        shutil.rmtree(tmp, ignore_errors=True)
    return out


# --------------------------------------------------------------------------- main

def main(args):
    table = load_table()
    errors = validate(table)
    if errors:
        return {"errors": errors}
    skins = table["skins"]
    if args.get("gladiator"):
        skins = [s for s in skins if s["gladiator"] == args["gladiator"]]
    if args.get("skin", "all") != "all":
        skins = [s for s in skins if s["id"] == args["skin"]]
    if not skins:
        return {"errors": ["no skin matches %s" % args]}
    check = args.get("check")
    if check == "skins":
        return check_skins(table, skins)
    if check == "base":
        seen = []
        for s in skins:
            if (s["script"], s["base"]) not in seen:
                seen.append((s["script"], s["base"]))
        return check_base(table, seen)
    export = args.get("export") != "0"
    if args.get("review") == "1":
        out = {}
        for g in dict.fromkeys(s["gladiator"] for s in skins):
            out[g] = review(table, g, export)
        return out
    out = {}
    for s in skins:
        g = run_script(s["script"], env_of(table, s), export=export)
        out[s["id"]] = {"tris": K.tri_count(K.descendants(g["root"])), "fbx": g["info"].get("fbx")}
    return out


result = main(ARGS)  # noqa: F821 (injected by Tools/bheadless / blender_bridge.py)
