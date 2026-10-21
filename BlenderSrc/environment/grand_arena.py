"""Grand Arena kit (Phase 5.13, docs/ARENAS.md section 4): palette family A4 (white marble + red, gold for the patrons).
Arena 4 is a Greek cross: four arms 52 m wide reaching 70 m from the centre, the inner corners cut on the diagonal by
the patron boxes. Every straight wall run is a podium with eight tiers of seats behind it (SM_GrandStand_4m, scaled to
fit), a monumental gate ends each arm, towers close the outer corners.

ARGS: part=terrain|structures|plaza|hazards. Every piece goes to Assets/Art/Models/Environment/. Origins at the base;
wall-like pieces face -Y (their front, toward the arena, arrives as Unity +Z). The floor is built in Unity coordinates
and mapped through B(): the export's 180 deg turn puts it back. Seeded randomness: re-runs are identical.

The cross's numbers are shared with Assets/Editor/Setup/GrandArenaLayout.cs: keep them in step.
"""
import importlib
import math
import random

import bmesh
import bpy
import gladiator_kit as K
from mathutils import Vector

importlib.reload(K)
PART = ARGS.get("part", "terrain")  # noqa: F821  (ARGS injected by Tools/blender_bridge.py)
OUT = {}

# ------------------------------------------------------------------------------------------------ the cross (Unity XZ)
L, H, C = 70.0, 26.0, 14.0      # an arm's reach, an arm's half width, the patron boxes' cut
GATE_HALF = 5.5                 # half the monumental gate's width
DAIS_R, DAIS_H = 7.0, 1.2
# the stands' profile (outward offset, height, swatch of the face from this point to the next): the podium (a red
# band under its marble cap), eight tiers of seats, the back wall
PODIUM, PODIUM_CAP, TIER_RISE, TIER_RUN, TIERS = 3.2, 0.6, 0.45, 0.8, 8
BACK_TOP, BACK_DEPTH = 8.4, 0.8


def B(x, z, h=0.0):
    """A Unity-space point (x, z, height) in Blender coordinates (the export turns the model 180 deg)."""
    return Vector((-x, -z, h))


def inside_cross(x, z, m=0.0):
    """True if Unity (x, z) is inside the wall line grown by m metres."""
    ax, az = abs(x), abs(z)
    if ax <= H + m and az <= L + m:
        return True
    if az <= H + m and ax <= L + m:
        return True
    return ax <= L + m and az <= L + m and ax + az <= 2 * H + C + m * math.sqrt(2)


def finish(objs):
    bpy.context.view_layer.update()
    return objs


def at_origin(obj):
    """K.join keeps the first part's origin: move the mesh so the piece's origin is the world origin again (the
    stairs' low edge, the blade's hub axis)."""
    if obj.type != "MESH" or obj.children:
        return obj
    from mathutils import Matrix
    obj.data.transform(Matrix.Translation(obj.location))
    obj.location = (0, 0, 0)
    return obj


# ------------------------------------------------------------------------------------------------ terrain

def grand_floor(name, seed=53):
    """Pale sand over the cross in 2 m cells, reaching ~1.5 m under the walls. Marble processional ways run from the
    plaza to each gate and a marble ring circles the dais (checkered marble and its shadow tone): low contrast, the
    horde must read against it."""
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=74, y_segments=74, size=74.0)  # 148 m square, 2 m cells
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not inside_cross(-f.calc_center_median().x, -f.calc_center_median().y, 1.5)],
                     context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    for v in bm.verts:
        v.co.z = rng.uniform(-0.015, 0.015)
    obj = K.object_from_bmesh(name, bm, swatch="sand_l")

    def u(c):  # a face centre (Blender) back to Unity (x, z)
        return -c.x, -c.y

    def way(c, n):
        x, z = u(c)
        ax, az = abs(x), abs(z)
        return (ax < 3.0 and az > DAIS_R + 2.0) or (az < 3.0 and ax > DAIS_R + 2.0)

    def ring(c, n):
        x, z = u(c)
        r = math.hypot(x, z)
        return DAIS_R + 2.0 < r < DAIS_R + 6.0

    def checker(c):
        x, z = u(c)
        return (int(math.floor(x / 2.0)) + int(math.floor(z / 2.0))) % 2 == 0

    # iteration 2: fewer sand patches, and the ways are white marble with a grey joint every third row (v1's
    # white/grey checker was busy right where the horde walks)
    def joint(c):
        x, z = u(c)
        along = z if abs(x) < 3.0 else x
        return int(math.floor(abs(along) / 2.0)) % 3 == 0

    K.set_swatch_where(obj, "sand", lambda c, n: rng.random() < 0.05)
    K.set_swatch_where(obj, "marble", lambda c, n: way(c, n) or (ring(c, n) and checker(c)))
    K.set_swatch_where(obj, "marble_shadow", lambda c, n: (way(c, n) and joint(c)) or (ring(c, n) and not checker(c)))
    return obj


# ------------------------------------------------------------------------------------------------ structures

def profile():
    p = [(0.0, -0.3, "marble"), (0.0, PODIUM - 0.55, "red_banner"), (0.0, PODIUM - 0.15, "gold_d"),
         (0.0, PODIUM, "marble_shadow")]
    o, h = PODIUM_CAP, PODIUM
    p.append((o, h, "marble"))
    for k in range(TIERS):
        h += TIER_RISE
        p.append((o, h, "marble_shadow" if k % 2 else "travertine"))
        o += TIER_RUN
        p.append((o, h, "marble"))
    p.append((o, BACK_TOP, "marble_shadow"))
    p.append((o + BACK_DEPTH, BACK_TOP, "travertine_d"))
    p.append((o + BACK_DEPTH, -0.3, None))
    return p


def stand_section(name, width=4.0):
    """A 4 m run of the stands: the profile swept along X (front face at y = 0, rising toward +Y), a marble pilaster
    at its west edge (they repeat every section), a gold rosette on the podium and a crimson pennant on the back wall."""
    prof = profile()
    bm = bmesh.new()
    rings = [[bm.verts.new((x, o, h)) for o, h, _ in prof] for x in (-width / 2, width / 2)]
    for j in range(len(prof)):
        k = (j + 1) % len(prof)
        f = bm.faces.new((rings[0][j], rings[0][k], rings[1][k], rings[1][j]))
        f.material_index = j
    for ring in rings:
        cap = bm.faces.new(ring)
        cap.material_index = 255
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if f.material_index == 255], quad_method="BEAUTY",
                          ngon_method="EAR_CLIP")
    obj = K.object_from_bmesh(name, bm, swatch="marble")
    uvs = K.palette()
    uvl = obj.data.uv_layers.active.data
    for poly in obj.data.polygons:
        tag = poly.material_index
        sw = "travertine_d" if tag >= len(prof) else (prof[tag][2] or "travertine_d")
        uu, vv = uvs[sw]
        for li in poly.loop_indices:
            uvl[li].uv = (uu, vv)
        poly.material_index = 0
    parts = [obj,
             K.box("Pilaster", size=(0.5, 0.25, PODIUM - 0.2), location=(-width / 2 + 0.25, -0.12, 0), pivot=(0, 0, -1),
                   swatch="marble"),
             K.box("PilasterCap", size=(0.62, 0.34, 0.16), location=(-width / 2 + 0.25, -0.12, PODIUM - 0.2),
                   pivot=(0, 0, -1), swatch="gold_d"),
             K.cylinder("Rosette", radius=0.32, depth=0.08, verts=8, location=(0.6, -0.04, PODIUM - 1.4), pivot=(0, 0, 0),
                        swatch="gold", axis="Y")]
    back_y = PODIUM_CAP + TIER_RUN * TIERS + BACK_DEPTH
    parts.append(K.box("Pennant", size=(0.7, 0.06, 1.6), location=(0.6, back_y - BACK_DEPTH - 0.05, BACK_TOP + 0.2),
                       pivot=(0, 0, 1), swatch="crimson"))
    parts.append(K.box("PennantPole", size=(0.06, 0.06, 2.2), location=(0.6, back_y - BACK_DEPTH * 0.5, BACK_TOP),
                       pivot=(0, 0, -1), swatch="gold_d"))
    finish(parts)
    return K.join(parts, name)


def tower(name, size=8.0, height=11.0):
    """Closes an outer corner of the cross: a marble block with a red band, a cornice and a gilded pyramid roof."""
    parts = [K.box(name, size=(size, size, height), pivot=(0, 0, -1), swatch="marble"),
             K.box("Band", size=(size + 0.06, size + 0.06, 0.6), location=(0, 0, height * 0.55), pivot=(0, 0, -1),
                   swatch="red_banner"),
             K.box("Cornice", size=(size + 0.6, size + 0.6, 0.45), location=(0, 0, height), pivot=(0, 0, -1),
                   swatch="marble_shadow"),
             K.cone("Roof", radius=size * 0.62, depth=3.2, verts=4, location=(0, 0, height + 0.45), pivot=(0, 0, -1),
                    swatch="gold_d"),
             K.cylinder("Finial", radius=0.25, depth=1.2, verts=6, location=(0, 0, height + 3.4), pivot=(0, 0, -1),
                        swatch="gold")]
    parts[3].rotation_euler.z = math.radians(45)
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(K.box("Arch%d%d" % (sx, sy), size=(1.6, 0.1, 2.6), location=(sx * size * 0.22, sy * (size / 2 + 0.03), height * 0.68),
                               pivot=(0, 0, -1), swatch="ink"))
    finish(parts)
    return K.join(parts, name)


def patron_box(name, width=C * math.sqrt(2), depth=7.0):
    """A patron's loge on the diagonal between two arms: a 4.2 m marble podium with a gilded rail, the loge floor the
    nobles stand on, four columns holding a crimson canopy, and a tall palace wall behind with a gold emblem. Front
    face at y = 0."""
    floor_h = 4.2
    parts = [K.box(name, size=(width, depth, floor_h), pivot=(0, -1, -1), swatch="marble")]
    parts.append(K.box("Band", size=(width, 0.06, 0.5), location=(0, -0.03, floor_h - 1.2), pivot=(0, -1, -1),
                       swatch="red_banner"))
    for i in range(9):  # gilded balusters under the rail
        x = -width / 2 + 0.6 + i * (width - 1.2) / 8
        parts.append(K.box("Baluster%d" % i, size=(0.18, 0.18, 0.7), location=(x, 0.15, floor_h), pivot=(0, 0, -1),
                           swatch="gold_d"))
    parts.append(K.box("Rail", size=(width, 0.3, 0.14), location=(0, 0.1, floor_h + 0.7), pivot=(0, -1, -1), swatch="gold"))
    col_y = depth - 2.2
    for i in range(4):
        x = -width / 2 + 1.4 + i * (width - 2.8) / 3
        parts.append(K.cylinder("Column%d" % i, radius=0.35, depth=4.6, verts=8, location=(x, col_y, floor_h),
                                pivot=(0, 0, -1), swatch="marble"))
        parts.append(K.box("Capital%d" % i, size=(0.9, 0.9, 0.3), location=(x, col_y, floor_h + 4.6), pivot=(0, 0, -1),
                           swatch="gold_d"))
    canopy = K.box("Canopy", size=(width + 0.6, depth - 0.4, 0.3), location=(0, 0.6, floor_h + 4.9), pivot=(0, -1, -1),
                   swatch="crimson")
    parts.append(canopy)
    parts.append(K.box("Valance", size=(width + 0.7, 0.12, 0.7), location=(0, 0.55, floor_h + 4.25), pivot=(0, -1, -1),
                       swatch="crimson_d"))
    parts.append(K.box("Fringe", size=(width + 0.72, 0.14, 0.12), location=(0, 0.54, floor_h + 4.2), pivot=(0, -1, -1),
                       swatch="gold"))
    parts.append(K.box("Palace", size=(width + 1.0, 1.2, 12.5), location=(0, depth - 1.2, 0), pivot=(0, -1, -1),
                       swatch="travertine"))
    parts.append(K.box("PalaceCornice", size=(width + 1.6, 1.6, 0.5), location=(0, depth - 1.4, 12.5), pivot=(0, -1, -1),
                       swatch="marble"))
    parts.append(K.extrude_poly("Pediment", [(-(width + 1.6) / 2, 0.0), ((width + 1.6) / 2, 0.0), (0.0, 2.6)], 1.2,
                                plane="XZ", location=(0, depth - 0.6, 13.0), swatch="marble_shadow"))
    parts.append(K.cylinder("Emblem", radius=1.1, depth=0.15, verts=10, location=(0, depth - 1.25, 10.6), pivot=(0, 0, 0),
                            swatch="gold", axis="Y"))
    parts.append(K.box("Drape", size=(2.4, 0.08, 4.0), location=(0, depth - 1.26, floor_h + 0.1), pivot=(0, 0, -1),
                       swatch="crimson_d"))
    finish(parts)
    return K.join(parts, name)


def grand_gate(name, width=2 * GATE_HALF, opening=5.0, open_h=7.0, height=12.0, depth=3.0):
    """The monumental gate ending each arm (north = the boss's): a triumphal arch in marble with paired columns, a
    red frieze, an attic with a gold inscription and a laurel; bronze double doors (DoorL / DoorR, hinged at the jambs,
    opened by yawing, so ArenaGates drives it unchanged). Front (-Y) face at y = 0."""
    root = K.empty(name)
    jamb = (width - opening) / 2
    frame = [K.box(name + "_Frame", size=(jamb, depth, open_h), location=(-(opening + jamb) / 2, 0, 0), pivot=(0, -1, -1),
                   swatch="marble", parent=root)]
    frame.append(K.box("JambR", size=(jamb, depth, open_h), location=((opening + jamb) / 2, 0, 0), pivot=(0, -1, -1),
                       swatch="marble"))
    frame.append(K.box("Arch", size=(width, depth, height - open_h - 2.2), location=(0, 0, open_h), pivot=(0, -1, -1),
                       swatch="marble"))
    frame.append(K.box("Frieze", size=(width + 0.1, 0.1, 0.7), location=(0, -0.05, open_h + 0.4), pivot=(0, -1, -1),
                       swatch="red_banner"))
    frame.append(K.box("Cornice", size=(width + 0.8, depth + 0.6, 0.45), location=(0, -0.3, height - 2.2), pivot=(0, -1, -1),
                       swatch="marble_shadow"))
    frame.append(K.box("Attic", size=(width - 1.0, depth - 0.4, 1.75), location=(0, 0.2, height - 1.75), pivot=(0, -1, -1),
                       swatch="travertine"))
    frame.append(K.box("Inscription", size=(width - 3.0, 0.1, 1.0), location=(0, 0.15, height - 1.4), pivot=(0, -1, -1),
                       swatch="gold_d"))
    frame.append(K.cylinder("Laurel", radius=0.9, depth=0.2, verts=10, location=(0, -0.05, open_h + 1.5), pivot=(0, 0, 0),
                            swatch="gold", axis="Y"))
    for sx in (-1, 1):
        for k in (0, 1):
            x = sx * (opening / 2 + 0.6 + k * 1.4)
            frame.append(K.cylinder("Col%d%d" % (sx, k), radius=0.4, depth=open_h + 0.4, verts=8, location=(x, -0.35, 0),
                                    pivot=(0, 0, -1), swatch="marble_shadow"))
            frame.append(K.box("ColCap%d%d" % (sx, k), size=(1.0, 1.0, 0.3), location=(x, -0.35, open_h + 0.4),
                               pivot=(0, 0, -1), swatch="gold_d"))
    frame.append(K.box("Dark", size=(opening, 0.1, open_h), location=(0, depth + 0.5, 0), pivot=(0, 0, -1), swatch="shadow"))
    K.join(frame, name + "_Frame")
    leaf_w = opening / 2 - 0.02
    for side, sx in (("L", -1), ("R", 1)):
        hinge = (sx * opening / 2, 0.25, 0)
        leaf = [K.box("Leaf" + side, size=(leaf_w, 0.16, open_h - 0.1), location=(hinge[0] - sx * leaf_w / 2, hinge[1], 0.05),
                      pivot=(0, 0, -1), swatch="bronze_d")]
        for z in (0.8, open_h * 0.5, open_h - 0.9):
            leaf.append(K.box("Band", size=(leaf_w, 0.2, 0.2), location=(hinge[0] - sx * leaf_w / 2, hinge[1], z),
                              pivot=(0, 0, -1), swatch="gold_d"))
        leaf.append(K.cylinder("Boss", radius=0.3, depth=0.1, verts=8, location=(hinge[0] - sx * leaf_w * 0.7, hinge[1] - 0.12, open_h * 0.5),
                               pivot=(0, 0, 0), swatch="gold", axis="Y"))
        h = K.box("Door" + side, size=(0.12, 0.18, open_h - 0.15), location=hinge, pivot=(-sx, 0, -1), swatch="bronze_d",
                  parent=root)
        K.join([h] + leaf, "Door" + side)
    return root


# ------------------------------------------------------------------------------------------------ plaza

def dais(name, radius=DAIS_R, height=DAIS_H):
    """The round marble dais in the plaza (its four stairs are SM_GrandStairs): a red band, a marble lip, a gold sun
    inlaid in its top under the statue. Origin at the base centre."""
    parts = [K.cylinder(name, radius=radius, depth=height - 0.12, verts=20, pivot=(0, 0, -1), swatch="marble"),
             K.cylinder("Band", radius=radius + 0.03, depth=0.3, verts=20, location=(0, 0, height * 0.35), pivot=(0, 0, -1),
                        swatch="red_banner"),
             K.cylinder("Lip", radius=radius + 0.12, depth=0.12, verts=20, location=(0, 0, height - 0.12), pivot=(0, 0, -1),
                        swatch="marble_shadow"),
             K.cylinder("Sun", radius=3.2, depth=0.02, verts=12, location=(0, 0, height), pivot=(0, 0, -1), swatch="gold_d")]
    finish(parts)
    return K.join(parts, name)


def grand_stairs(name, steps=4, rise=0.3, run=0.5, width=3.0):
    """Marble stairs, SM_Stairs' shape (low edge at the origin, rising toward +Y = Unity -Z): 1.2 m in 2 m."""
    parts = []
    for i in range(steps):
        parts.append(K.box(name if i == 0 else name + "_Step" + str(i), size=(width, run * (steps - i), rise),
                           location=(0, run * i + run * (steps - i) / 2, rise * i), pivot=(0, 0, -1), bevel=0.02,
                           swatch="marble" if i % 2 == 0 else "marble_shadow"))
    for sx in (-1, 1):  # low marble cheeks either side
        parts.append(K.wedge("Cheek%d" % sx, size=(0.2, run * steps, rise * steps), location=(sx * (width / 2 + 0.1), 0, 0),
                             pivot=(0, -1, -1), swatch="travertine"))
    finish(parts)
    return K.join(parts, name)


def grand_ramp(name, width=3.0, length=4.0, height=1.2):
    """A marble ramp (SM_Ramp's shape: front-bottom centre origin, rising toward +Y = Unity -Z)."""
    parts = [K.wedge(name, size=(width, length, height), pivot=(0, -1, -1), swatch="marble_shadow")]
    for i in range(4):  # gold-trimmed treads
        y = 0.5 + i * 1.0
        parts.append(K.box("Tread%d" % i, size=(width, 0.08, 0.03), location=(0, y, y * height / length + 0.01),
                           pivot=(0, 0, 0), swatch="travertine_d"))
    finish(parts)
    return K.join(parts, name)


def terrace(name, size=(10.0, 6.0), height=1.2):
    """A raised marble terrace against an arm's wall (stairs or a ramp in front): a red band, a gold-trimmed lip, a
    paved top. Origin at the base centre; its front (stairs side) faces -Y."""
    w, d = size
    parts = [K.box(name, size=(w, d, height - 0.12), pivot=(0, 0, -1), swatch="marble"),
             K.box("Band", size=(w + 0.04, d + 0.04, 0.26), location=(0, 0, height * 0.4), pivot=(0, 0, -1),
                   swatch="red_banner"),
             # iteration 2: a marble top with a gold trim under its lip (v1's lip covered the whole top in gold)
             K.box("Lip", size=(w + 0.16, d + 0.16, 0.12), location=(0, 0, height - 0.12), pivot=(0, 0, -1),
                   swatch="marble"),
             K.box("Trim", size=(w + 0.1, d + 0.1, 0.08), location=(0, 0, height - 0.22), pivot=(0, 0, -1),
                   swatch="gold_d")]
    for i in range(5):  # paving joints on the top
        parts.append(K.box("Joint%d" % i, size=(0.06, d - 0.4, 0.015), location=(-w / 2 + (i + 1) * w / 6, 0, height),
                           pivot=(0, 0, -1), swatch="marble_shadow"))
    finish(parts)
    return K.join(parts, name)


def patron_statue(name):
    """The Patron himself, gilded, on a marble plinth on the dais: fat, a laurel wreath, a goblet raised to the crowd,
    a purse in the other hand. ~6 m: a line-of-sight blocker. Origin at the base centre; faces -Y."""
    parts = [K.box(name, size=(2.4, 2.4, 1.7), pivot=(0, 0, -1), bevel=0.06, swatch="marble"),
             K.box("PlinthCap", size=(2.7, 2.7, 0.22), location=(0, 0, 1.7), pivot=(0, 0, -1), swatch="marble_shadow"),
             K.box("PlinthBand", size=(2.44, 2.44, 0.3), location=(0, 0, 0.7), pivot=(0, 0, -1), swatch="red_banner")]
    z0 = 1.92
    for sx in (-1, 1):
        parts.append(K.box("Leg%d" % sx, size=(0.5, 0.55, 1.0), location=(sx * 0.35, 0, z0), pivot=(0, 0, -1), swatch="gold_d"))
    parts.append(K.ico("Belly", radius=1.0, subdiv=1, location=(0, -0.1, z0 + 1.65), scale=(1.0, 0.9, 1.0), swatch="gold"))
    parts.append(K.box("Toga", size=(1.5, 1.2, 1.3), location=(0, 0.1, z0 + 0.9), pivot=(0, 0, -1), taper=(1.1, 1.0),
                       swatch="gold_d"))
    parts.append(K.ico("Head", radius=0.48, subdiv=1, location=(0, -0.05, z0 + 2.95), swatch="gold_l"))
    parts.append(K.cylinder("Wreath", radius=0.5, depth=0.12, verts=8, location=(0, -0.05, z0 + 3.2), pivot=(0, 0, -1),
                            swatch="gold_d"))
    parts.append(K.box("ArmR", size=(0.36, 0.36, 1.2), location=(0.95, 0, z0 + 2.2), pivot=(0, 0, -1), swatch="gold"))
    parts.append(K.cylinder("Goblet", radius=0.22, depth=0.45, verts=6, location=(0.95, 0, z0 + 3.4), pivot=(0, 0, -1),
                            taper=1.4, swatch="gold_l"))
    parts.append(K.box("ArmL", size=(0.36, 0.36, 1.0), location=(-0.95, -0.15, z0 + 1.2), pivot=(0, 0, -1), swatch="gold"))
    parts.append(K.ico("Purse", radius=0.35, subdiv=1, location=(-1.0, -0.3, z0 + 1.05), swatch="gold_d"))
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ hazards

def jet_nozzle(name, size=1.8):
    """One bronze fire-jet nozzle set in the floor (a row of them crosses each arm): a soot-dark plate, a grate, a
    bronze ring with a hot orange throat. Decor; origin at the base centre."""
    parts = [K.box(name, size=(size, size, 0.05), pivot=(0, 0, -1), swatch="iron_d"),
             K.box("Frame", size=(size + 0.1, size + 0.1, 0.03), location=(0, 0, 0.01), pivot=(0, 0, -1), swatch="bronze_d"),
             K.cylinder("Ring", radius=0.42, depth=0.1, verts=8, location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="bronze_l"),
             K.cylinder("Throat", radius=0.26, depth=0.11, verts=8, location=(0, 0, 0.0), pivot=(0, 0, -1),
                        swatch="hazard_orange")]
    for i in (-1, 1):
        parts.append(K.box("Bar%d" % i, size=(size - 0.2, 0.08, 0.07), location=(0, i * 0.62, 0.0), pivot=(0, 0, -1),
                           swatch="iron"))
    finish(parts)
    return K.join(parts, name)


def jet_flame(name, height=3.5, radius=0.7):
    """A jet's flame column (FireJets draws one per nozzle, scaled in height while it roars): an orange outer sheath,
    a yellow-white core, ragged tips. Origin at the base centre."""
    # iteration 3: an orange-red sheath under a yellow tip (v2's fire_mid bloomed to yellow in Unity: hazards read
    # orange-red, CLAUDE.md §5); iteration 2 had brightened v1's rust spire
    parts = [K.cylinder(name, radius=radius, depth=height * 0.62, verts=6, taper=0.6, pivot=(0, 0, -1),
                        swatch="hazard_orange"),
             K.cone("Tip", radius=radius * 0.62, depth=height * 0.5, verts=6, location=(0, 0, height * 0.55),
                    pivot=(0, 0, -1), swatch="fire_mid"),
             K.cylinder("Root", radius=radius * 1.15, depth=0.5, verts=6, taper=0.85, pivot=(0, 0, -1),
                        swatch="glow_hazard_red")]
    parts[1].rotation_euler.z = math.radians(30)
    for k in range(3):  # licks round the column
        a = k * 2.1
        lick = K.cone("Lick%d" % k, radius=0.24, depth=1.2, verts=4, location=(math.cos(a) * radius * 0.7, math.sin(a) * radius * 0.7, height * 0.3 + k * 0.45),
                      pivot=(0, 0, -1), swatch="fire_core" if k == 1 else "hazard_orange")
        lick.rotation_euler = (math.radians(18) * math.sin(a), math.radians(18) * math.cos(a), 0)
        parts.append(lick)
    finish(parts)
    return K.join(parts, name)


def blade_hub(name, radius=0.7, height=0.55):
    """A blade sweeper's hub: a bronze drum with a hazard-orange warning band and a gilded cap. Origin at the base."""
    parts = [K.cylinder(name, radius=radius, depth=height, verts=10, pivot=(0, 0, -1), swatch="bronze_d"),
             K.cylinder("Warn", radius=radius + 0.02, depth=0.12, verts=10, location=(0, 0, height * 0.35), pivot=(0, 0, -1),
                        swatch="hazard_orange"),
             K.cylinder("Cap", radius=radius * 0.6, depth=0.2, verts=10, taper=0.4, location=(0, 0, height), pivot=(0, 0, -1),
                        swatch="gold"),
             K.cylinder("Base", radius=radius + 0.35, depth=0.05, verts=10, pivot=(0, 0, -1), swatch="iron_d")]
    finish(parts)
    return K.join(parts, name)


def blade(name, length=5.5, low=0.15, high=0.5):
    """The sweeping blade, drawn by BladeSweepers: a bronze arm from the hub and a long steel blade at shin height
    along -Y (Unity +Z), its leading edge (Unity +X = Blender -X) painted hazard orange. Origin at the hub's axis."""
    h = high - low
    parts = [K.box(name, size=(0.22, 0.9, 0.22), location=(0, -0.45, low + h * 0.3), pivot=(0, 0, -1), swatch="bronze"),
             K.box("Plate", size=(0.1, length - 0.8, h), location=(0, -(0.8 + (length - 0.8) / 2), low), pivot=(0, 0, -1),
                   swatch="steel_l"),
             # iteration 2: the warning band wraps both faces (v1 painted one side only)
             K.box("Edge", size=(0.13, length - 0.9, h * 0.5), location=(0, -(0.85 + (length - 0.9) / 2), low),
                   pivot=(0, 0, -1), swatch="hazard_orange"),
             K.box("Spine", size=(0.14, length - 0.8, 0.06), location=(0, -(0.8 + (length - 0.8) / 2), high), pivot=(0, 0, -1),
                   swatch="bronze_d"),
             K.box("Weight", size=(0.3, 0.3, 0.42), location=(0, -length + 0.15, low), pivot=(0, 0, -1), swatch="bronze_d")]
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ build

K.clear_scene()
if PART == "terrain":
    items = {"SM_GrandFloor": grand_floor("SM_GrandFloor")}
elif PART == "structures":
    items = {
        "SM_GrandStand_4m": stand_section("SM_GrandStand_4m"),
        "SM_GrandTower": tower("SM_GrandTower"),
        "SM_PatronBox": patron_box("SM_PatronBox"),
        "SM_GrandGate": grand_gate("SM_GrandGate"),
    }
elif PART == "plaza":
    items = {
        "SM_GrandDais": dais("SM_GrandDais"),
        "SM_GrandStairs": grand_stairs("SM_GrandStairs"),
        "SM_GrandRamp": grand_ramp("SM_GrandRamp"),
        "SM_GrandTerrace": terrace("SM_GrandTerrace"),
        "PR_PatronStatue": patron_statue("PR_PatronStatue"),
    }
else:
    items = {
        "PR_FireJetNozzle": jet_nozzle("PR_FireJetNozzle"),
        "PR_JetFlame": jet_flame("PR_JetFlame"),
        "PR_BladeHub": blade_hub("PR_BladeHub"),
        "PR_Blade": blade("PR_Blade"),
    }
for o in items.values():
    at_origin(o)
# lay them out side by side for one review shot of the part
x = 0.0
for n, o in items.items():
    lo, hi = K.world_bounds(K.descendants(o))
    o.location.x += x - lo.x
    x += (hi.x - lo.x) + 2.0
review = ARGS.get("review")  # noqa: F821
for n, o in items.items():
    saved = o.location.copy()
    o.location = (0, 0, 0)
    if review == n:
        OUT["review"] = K.review_render(o, n.lower(), size=420)
    OUT[n] = K.export_fbx(o, "Environment/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o) or [o])
    o.location = saved
OUT["blend"] = K.save_blend("grand_arena_" + PART)
result = OUT
