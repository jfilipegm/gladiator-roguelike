"""R3: Asterion's chain (docs/GLADIATORS.md "Asterion, the Chained"): the chain he tore out of the labyrinth's wall, a
block of its masonry still moored to the end. Player colours only: alternating steel / iron links, grey stone, a cyan
glyph on the block (never the orange-red hazard row). Front = Blender -Y (arrives as Unity +Z), like WP_Spear.
- WP_Chain: the whole chain straight out in front, one mesh: pivot at the grip (the first joint), WP_SEGS segments of
  SEG_LEN (two links each) to the masonry; the far end of the block sits WP_LENGTH ahead. For a rigid sweep: draw it
  rotating around the player (meshScale stretches the reach).
- WP_ChainLink: one segment (two links), pivot at its inner joint, SEG_LEN ahead to the next joint. For a chain drawn
  link by link (instanced along an arc or a curve): segment i at i * SEG_LEN, the weight after the last one.
- WP_ChainWeight: the masonry block, pivot at its joint (where the last segment ends), ~0.42 m ahead.
CH_Asterion's chain (BlenderSrc/characters/ch_asterion.py) is built from the same pieces, so they match.
    Tools/bheadless BlenderSrc/weapons/wp_chain.py [review=0]
"""
import importlib

import bmesh
from mathutils import Matrix, Vector

import gladiator_kit as K

importlib.reload(K)

SEG_LEN = 0.26              # one segment (two links), joint to joint
LINK = (0.055, 0.13, 0.17)  # a link: thickness, width, length
WP_SEGS = 8
WEIGHT_LEN = 0.39           # joint to the far face of the block
WP_LENGTH = WP_SEGS * SEG_LEN + WEIGHT_LEN


def at_origin(obj):
    """K.join keeps the first part's origin: move the mesh so the piece's origin is the world origin again."""
    obj.data.transform(Matrix.Translation(obj.location))
    obj.location = (0, 0, 0)
    return obj


def link_pair_bm(seg_len=SEG_LEN, along=(0, 0, -1), offset=0.0):
    """Two interlocked links as one bmesh, from the origin (+ offset along the axis) along `along` (a unit axis):
    link A thin in X (its flat faces left/right), link B turned 90 deg (thin in the other cross axis).
    Faces 0..5 are A, 6..11 B."""
    t, w, ln = LINK
    ax = Vector(along)
    other = Vector((0, 1, 0)) if abs(ax.y) < 0.5 else Vector((0, 0, 1))
    bm = bmesh.new()
    for k, centre in enumerate((0.075, 0.075 + seg_len / 2)):
        res = bmesh.ops.create_cube(bm, size=1.0)
        sx, so = (t, w) if k == 0 else (w, t)
        for v in res["verts"]:
            c = v.co.copy()
            v.co = Vector((c.x * sx, 0, 0)) + other * (c.y * so) + ax * (c.z * ln + centre + offset)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def link_pair(name, location=(0, 0, 0), parent=None, along=(0, 0, -1), offset=0.0):
    seg = K.object_from_bmesh(name, link_pair_bm(along=along, offset=offset), location=location, parent=parent,
                              swatch="steel")
    K.set_swatch(seg, "iron", list(range(6, 12)))  # alternate light / dark links so the chain reads as links
    return seg


def masonry(name, along_y=False):
    """The block of the labyrinth's wall, the iron staple the chain was moored to still in it, a cyan glyph. One mesh,
    origin at its joint (the world origin while it's built). It runs from there along -Z (CH_Asterion's chain) or -Y
    (the WP_ meshes, along_y=True); its "up" side faces up once the chain lies on the ground (local +Y in the CH chain,
    whose trailing segments are turned 90 deg; +Z for the WP_ meshes)."""
    def V(x, u, a):  # (cross, up, along) -> local xyz
        return (x, a, u) if along_y else (x, u, a)

    lift = 0.0 if along_y else 0.10  # on the ground the block rests on the floor, below the chain's line
    parts = [K.box(name, size=V(0.40, 0.30, 0.34), location=V(0, lift, -0.22), pivot=(0, 0, 0), swatch="stone_grey")]
    chip = K.box(name + "Chip", size=V(0.20, 0.16, 0.18), location=V(0.15, lift + 0.10, -0.10), pivot=(0, 0, 0),
                 swatch="ash")
    chip.rotation_euler = (0.3, 0.4, 0.2)
    parts.append(chip)
    # the staple: a U of iron standing out of the block's face toward the chain
    for k, x in enumerate((-0.06, 0.06)):
        parts.append(K.box(name + "Staple%d" % k, size=V(0.04, 0.04, 0.12), location=V(x, 0, -0.03), pivot=(0, 0, 0),
                           swatch="iron_d"))
    parts.append(K.box(name + "StapleTop", size=V(0.16, 0.04, 0.04), location=V(0, 0, 0.0), pivot=(0, 0, 0),
                       swatch="iron_d"))
    # a cyan glyph (a turn of the labyrinth) on the up face: the player's mark on his weapon
    parts.append(K.box(name + "Glyph0", size=V(0.22, 0.03, 0.045), location=V(-0.02, lift + 0.15, -0.14),
                       pivot=(0, 0, 0), swatch="glow_cyan"))
    parts.append(K.box(name + "Glyph1", size=V(0.045, 0.03, 0.20), location=V(0.07, lift + 0.15, -0.22),
                       pivot=(0, 0, 0), swatch="glow_cyan"))
    return at_origin(K.join(parts, name))


def whole_chain(name):
    parts = [link_pair(name, along=(0, -1, 0))]
    for k in range(1, WP_SEGS):
        parts.append(link_pair(name + "_Seg%d" % k, along=(0, -1, 0), offset=k * SEG_LEN))
    weight = masonry(name + "_Weight", along_y=True)
    weight.data.transform(Matrix.Translation((0, -WP_SEGS * SEG_LEN, 0)))
    parts.append(weight)
    return at_origin(K.join(parts, name))


if not __name__.endswith("wp_chain"):  # run as a script (not imported by ch_asterion.py)
    review = not ("ARGS" in globals() and ARGS.get("review") == "0")  # noqa: F821
    K.clear_scene()
    OUT = {}
    for n, make in (("WP_Chain", whole_chain), ("WP_ChainLink", lambda n: link_pair(n, along=(0, -1, 0))),
                    ("WP_ChainWeight", lambda n: masonry(n, along_y=True))):
        o = make(n)
        lo, hi = K.world_bounds([o])
        OUT[n] = {"tris": K.tri_count([o]), "origin": tuple(o.location),
                  "min": tuple(round(v, 3) for v in lo), "max": tuple(round(v, 3) for v in hi)}
        if review:
            OUT[n]["review"] = K.review_render(o, n.lower(), size=300)["sheet"]
        OUT[n]["fbx"] = K.export_fbx(o, "Weapons/" + n)
    OUT["blend"] = K.save_blend("wp_chain")
    result = OUT
