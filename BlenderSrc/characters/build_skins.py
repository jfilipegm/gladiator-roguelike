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
    Tools/bheadless BlenderSrc/characters/build_skins.py check=rebuild [gladiator=<id>] [style=a1]
        rebuilds each skin into a temp folder and compares it with the committed skin FBX (same nodes, geometry, UV
        cells): proves a change to the table or this driver left that style's skins as they were
Every mode takes style=a1 (default: classic): the A1 models (docs/tracks/A1-ingame-contract.md, "Skins"). A skin's
`a1_script` (BlenderSrc/characters/a1/a1_<id>.py) is run with export=1 and writes
Assets/Art/Models/A1/Characters/Skins/<base><suffix>.fbx; its remap is `remap` plus `a1_remap` (swatches only the A1
model uses, so the skin still reads as itself; `remap` alone never changes, so the classic skins stay as they are).
The A1 review sheet is Screenshots/a1/ingame/a1_skins_<gladiator>.png. An A1 part joins its pieces (the weapon
into its arm), so check=skins on A1 accepts each face kept or remapped (the kept weapon can't be told apart by name).
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
STYLES = ("classic", "a1")
A1_SHOTS = "/home/jfilipegm/Gladiator Roguelike/Screenshots/a1/ingame"  # a1_export.SHOTS (shared by all worktrees)


def script_of(s, style):
    return s["a1_script"] if style == "a1" else s["script"]


def model_dir(style):
    """The base models' folder under Assets/Art/Models for a style (the skins are in its Skins/)."""
    return os.path.join("A1", "Characters") if style == "a1" else "Characters"


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
        if "a1_script" in s and not os.path.exists(os.path.join(HERE, s["a1_script"])):
            errors.append(sid + ": no a1_script " + s["a1_script"])
        if s["base"] not in table["keep"]:
            errors.append(sid + ": no keep entry for " + s["base"])
        if (s["price"] > 0) == bool(s["champion"]):
            errors.append(sid + ": exactly one of price > 0 / champion")
        if len(s["chips"]) != 3 or any(c not in pal for c in s["chips"]):
            errors.append(sid + ": chips must be three palette swatches")
        for m in list(s["remap"]) + list(s.get("a1_remap", ())):
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


def env_of(table, s, style="classic"):
    remap = {m["from"]: m["to"] for m in s["remap"]}
    if style == "a1":
        remap.update({m["from"]: m["to"] for m in s.get("a1_remap", ())})
    return {"suffix": s["suffix"], "remap": remap, "keep": table["keep"][s["base"]]}


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
        elif getattr(m, "__name__", "") == "a1_export" and quiet:
            m.poses = lambda root, made: None  # the base's pose sheet stays the base's
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


def run_script(script, skin_env=None, export=True, out_dir=None, quiet=True, style="classic"):
    """Runs BlenderSrc/characters/<script> (an A1 script with export=1); returns its globals (root, info/result)."""
    args = {"export": "1"} if style == "a1" else {}
    with patched_kit(skin_env, export, out_dir, quiet):
        return runpy.run_path(os.path.join(HERE, script), init_globals={"ARGS": args, "REPO": K.REPO},
                              run_name="gl_script")


def root_of(g, base):
    """The model's root after a script ran (the classic scripts' `root`; a1_nobody.py leaves it by name)."""
    return g.get("root") or bpy.data.objects.get(base)


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


def review(table, gladiator, export=True, style="classic"):
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
        g = run_script(script_of(skins[0], style), env_of(table, v, style) if v else None, export=bool(v) and export,
                       style=style)
        root = root_of(g, skins[0]["base"])
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
    shots = A1_SHOTS if style == "a1" else K.SHOTS_DIR
    os.makedirs(shots, exist_ok=True)
    out["sheet"] = os.path.join(shots, ("a1_skins_%s.png" if style == "a1" else "skins_%s.png") % gladiator)
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


def compare(base, other, remap=None, keep=(), lenient=False):
    """Differences between two read_fbx() dicts; with a remap, other's faces must be base's faces remapped (lenient:
    each face kept or remapped, for A1 parts whose kept weapon is joined into the arm)."""
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
        if lenient and remap is not None:
            want = tuple(o if o in (b, remap.get(b, b)) else w for b, w, o in zip(bf, want, of))
        if want != of:
            bad = sorted({(w, f) for w, f in zip(want, of) if w != f})
            diffs.append("%s: UV cells %s" % (name, bad[:6]))
    return diffs


def check_skins(table, skins, style="classic"):
    out = {}
    for s in skins:
        base = read_fbx(os.path.join(MODELS, model_dir(style), s["base"] + ".fbx"))
        path = os.path.join(MODELS, model_dir(style), "Skins", s["base"] + s["suffix"] + ".fbx")
        if not os.path.exists(path):
            out[s["id"]] = "missing " + path
            continue
        skin = read_fbx(path)
        remapped = sum(1 for n, (_, _, _, f) in skin.items() if n in base for a, b in zip(base[n][3], f) if a != b)
        diffs = compare(base, skin, env_of(table, s, style)["remap"], table["keep"][s["base"]], lenient=style == "a1")
        out[s["id"]] = diffs or "ok (%d tris, %d faces remapped)" % (sum(v[1] for v in skin.values()), remapped)
    return out


def check_base(table, scripts, style="classic"):
    """Each base script with no skin, its outputs in a temp folder, against the committed FBX."""
    out = {}
    for script, base in scripts:
        tmp = tempfile.mkdtemp(prefix="skins_base_")
        run_script(script, None, export=True, out_dir=tmp, quiet=False, style=style)
        fresh = read_fbx(os.path.join(tmp, "Models", model_dir(style), base + ".fbx"))
        committed = read_fbx(os.path.join(MODELS, model_dir(style), base + ".fbx"))
        diffs = compare(committed, fresh)
        cells = sorted({f for v in fresh.values() for f in v[3]})
        out[base] = diffs or "identical: %d nodes, %d tris, UV cells %s" % (
            len(fresh), sum(v[1] for v in fresh.values()), cells)
        shutil.rmtree(tmp, ignore_errors=True)
    return out


def check_rebuild(table, skins, style="classic"):
    """Each skin rebuilt into a temp folder against its committed FBX: the table and the driver still make it."""
    out = {}
    for s in skins:
        tmp = tempfile.mkdtemp(prefix="skins_rebuild_")
        run_script(script_of(s, style), env_of(table, s, style), export=True, out_dir=tmp, style=style)
        rel = os.path.join(model_dir(style), "Skins", s["base"] + s["suffix"] + ".fbx")
        fresh = read_fbx(os.path.join(tmp, "Models", rel))
        committed = read_fbx(os.path.join(MODELS, rel))
        diffs = compare(committed, fresh)
        out[s["id"]] = diffs or "identical: %d nodes, %d tris" % (len(fresh), sum(v[1] for v in fresh.values()))
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
    style = args.get("style", "classic")
    if style not in STYLES:
        return {"errors": ["style must be one of %s" % (STYLES,)]}
    if style == "a1":
        skins = [s for s in skins if s.get("a1_script")]
    if not skins:
        return {"errors": ["no skin matches %s" % args]}
    check = args.get("check")
    if check == "skins":
        return check_skins(table, skins, style)
    if check == "base":
        seen = []
        for s in skins:
            if (script_of(s, style), s["base"]) not in seen:
                seen.append((script_of(s, style), s["base"]))
        return check_base(table, seen, style)
    if check == "rebuild":
        return check_rebuild(table, skins, style)
    export = args.get("export") != "0"
    if args.get("review") == "1":
        out = {}
        for g in dict.fromkeys(s["gladiator"] for s in skins):
            out[g] = review(table, g, export, style)
        return out
    out = {}
    for s in skins:
        g = run_script(script_of(s, style), env_of(table, s, style), export=export, style=style)
        info = g.get("info") or g.get("result") or {}
        out[s["id"]] = {"tris": K.tri_count(K.descendants(root_of(g, s["base"]))), "fbx": info.get("fbx")}
    return out


result = main(ARGS)  # noqa: F821 (injected by Tools/bheadless / blender_bridge.py)
