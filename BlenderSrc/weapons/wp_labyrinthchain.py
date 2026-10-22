"""R3: WP_LabyrinthChain, Asterion's chain (docs/GLADIATORS.md "Asterion, the Chained"): the bronze chain he tore out
of the labyrinth's wall, a block of its masonry still bolted to the end. Drawn by code for whoever swings it; the same
pieces make CH_Asterion's chain (BlenderSrc/characters/ch_asterion.py), so the two match.
One FBX, a root at the origin with two meshes (each with its own origin at the root's):
- Link: a straight run of 3 interlocked links, LINK_RUN (0.6 m) along Unity +Z (Blender -Y), origin at its start.
  Lay N of them end to end from the hand, each rolled 90 deg about its axis from the previous one (the middle link of
  one run then interlocks with the ends of the next). Each link's broad faces are bronze_d and its edges bronze, so the
  upright and the flat links alternate dark / light from any side, whatever the roll.
- Weight: the stone block (0.42 wide, 0.42 tall, 0.48 long), origin at its centre, a bronze ring bolt on its Unity -Z
  face (Blender +Y) for the chain, a bone meander carved on its top, a lump of mortar torn out with it. Its centre goes
  WEIGHT_OFFSET (0.33 m) past the end of the last Link (the last link's tip then passes through the ring).
Player colours only: bronze, stone, bone (never the orange-red hazard row).
    Tools/bheadless BlenderSrc/weapons/wp_labyrinthchain.py [review=0]
"""
import importlib
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K

importlib.reload(K)

LINK = (0.06, 0.15, 0.26)   # one link: thickness, width, length
PITCH = 0.20                # link to link along the chain
LINK_RUN = 3 * PITCH        # the Link mesh: 3 links, 0.6 m
BLOCK = (0.42, 0.42, 0.48)  # the stone: width, height, length along the chain
RING_TIP = 0.3575           # from the stone's centre to the far end of its ring bolt
WEIGHT_OFFSET = 0.33        # the stone's centre past the end of the last Link


def at_origin(obj):
    """K.join keeps the first part's origin: move the mesh so the piece's origin is the world origin again."""
    obj.data.transform(Matrix.Translation(obj.location))
    obj.location = (0, 0, 0)
    return obj


def link_run(name, n, along=(0, -1, 0), up=(0, 0, 1), location=(0, 0, 0), parent=None):
    """n interlocked links from the origin along `along` (a unit axis). Even links stand upright (thin across X),
    odd ones lie flat (thin across `up`). Broad faces bronze_d, edges bronze."""
    t, w, ln = LINK
    ax, upv, cross = Vector(along), Vector(up), Vector((1, 0, 0))
    bm = bmesh.new()
    for k in range(n):
        res = bmesh.ops.create_cube(bm, size=1.0)
        thin, wide = (cross, upv) if k % 2 == 0 else (upv, cross)
        centre = ln / 2 + k * PITCH
        for v in res["verts"]:
            c = v.co.copy()
            v.co = thin * (c.x * t) + wide * (c.y * w) + ax * (c.z * ln + centre)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = K.object_from_bmesh(name, bm, location=location, parent=parent, swatch="bronze")
    broad = [p.index for p in obj.data.polygons
             if abs(p.normal.dot(cross if (p.index // 6) % 2 == 0 else upv)) > 0.9]
    K.set_swatch(obj, "bronze_d", broad)
    return obj


def weight(name):
    """The stone, centred on the origin: Blender -Y is forward (away from the chain), +Y back to the ring bolt."""
    bw, bh, bl = BLOCK
    parts = [K.box(name, size=(bw, bl, bh), location=(0, 0, 0), pivot=(0, 0, 0), bevel=0.03, swatch="stone_grey")]
    chip = K.box(name + "_Mortar", size=(0.16, 0.20, 0.14), location=(0.19, -0.12, 0.12), pivot=(0, 0, 0),
                 swatch="ash")
    chip.rotation_euler = (0.35, 0.3, 0.25)
    parts.append(chip)
    # the ring bolt: a bronze plate let into the back face, a U of bronze standing out of it
    parts.append(K.box(name + "_Plate", size=(0.16, 0.03, 0.16), location=(0, bl / 2 + 0.005, 0), pivot=(0, 0, 0),
                       swatch="bronze_d"))
    for k, x in enumerate((-0.055, 0.055)):
        parts.append(K.box(name + "_Ring%d" % k, size=(0.035, 0.10, 0.035), location=(x, bl / 2 + 0.065, 0),
                           pivot=(0, 0, 0), swatch="bronze"))
    parts.append(K.box(name + "_RingEnd", size=(0.145, 0.035, 0.035), location=(0, RING_TIP - 0.0175, 0),
                       pivot=(0, 0, 0), swatch="bronze"))
    # a meander (one turn of the Greek key) carved on top, in bone
    z = bh / 2 + 0.008
    for k, (x, y, sx, sy) in enumerate(((0.0, 0.14, 0.30, 0.045), (0.13, 0.0, 0.045, 0.28),
                                        (0.03, -0.14, 0.24, 0.045), (-0.07, -0.045, 0.045, 0.19))):
        parts.append(K.box(name + "_Key%d" % k, size=(sx, sy, 0.025), location=(x, y, z), pivot=(0, 0, 0),
                           swatch="bone"))
    return at_origin(K.join(parts, name))


def weight_on_chain(name):
    """The same stone for CH_Asterion's chain, whose segments run along local -Z with local +Y up: origin at the ring
    bolt's tip (the joint), the stone along -Z. (x, y, z) -> (-x, z, y) is a rotation (a mirror in X and one in Y-Z)."""
    obj = weight(name)
    obj.data.transform(Matrix.Translation((0, 0, -RING_TIP)) @ Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0),
                                                                          (0, 0, 0, 1))))
    return obj


if not __name__.endswith("wp_labyrinthchain"):  # run as a script (not imported by ch_asterion.py)
    review = not ("ARGS" in globals() and ARGS.get("review") == "0")  # noqa: F821
    K.clear_scene()
    OUT = {}
    if review:  # the chain as code lays it out: 4 Links, each rolled 90 deg, then the stone
        prev = K.empty("WP_LabyrinthChain_Preview")
        for k in range(4):
            run = link_run("Prev%d" % k, 3, location=(0, -k * LINK_RUN, 0), parent=prev)
            run.rotation_euler = (0, math.radians(90 * (k % 2)), 0)
        w = weight("PrevWeight")
        w.parent = prev
        w.location = (0, -(4 * LINK_RUN + WEIGHT_OFFSET), 0)
        OUT["review"] = K.review_render(prev, "wp_labyrinthchain", size=360)["sheet"]
        for o in K.descendants(prev):
            bpy.data.objects.remove(o, do_unlink=True)
    root = K.empty("WP_LabyrinthChain")
    link = link_run("Link", 3)
    stone = weight("Weight")
    for o in (link, stone):
        o.parent = root
    if review:
        OUT["review_weight"] = K.review_render(stone, "wp_labyrinthchain_weight", size=300)["sheet"]
    OUT["tris"] = {"Link": K.tri_count([link]), "Weight": K.tri_count([stone])}
    lo, hi = K.world_bounds([link])
    OUT["link_bounds"] = (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi))
    lo, hi = K.world_bounds([stone])
    OUT["weight_bounds"] = (tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi))
    OUT["fbx"] = K.export_fbx(root, "Weapons/WP_LabyrinthChain")
    OUT["blend"] = K.save_blend("wp_labyrinthchain")
    result = OUT
