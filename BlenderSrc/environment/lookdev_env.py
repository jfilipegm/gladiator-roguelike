"""Look-dev environment kit (M0.4): enough pieces to judge the style in Unity, and a head start on the Slave Pit kit.

ARGS: part=floor|props|structures (each call stays short). Every piece is exported to Assets/Art/Models/Environment/.
Origins at the base. Deterministic randomness (seeded) so re-runs produce identical meshes.
"""
import importlib
import math
import random

import bmesh
import gladiator_kit as K
from mathutils import Vector

importlib.reload(K)
PART = ARGS.get("part", "props")  # noqa: F821  (ARGS injected by Tools/blender_bridge.py)
OUT = {}


def floor():
    """SM_PitFloor: 46 m disc of 2 m cells, gentle height noise, patchy dirt swatches per face."""
    rng = random.Random(7)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=24, y_segments=24, size=24.0)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().length > 23.0], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    for v in bm.verts:
        r = v.co.length
        v.co.z = rng.uniform(-0.05, 0.05) + (0.25 * max(0.0, (r - 18.0) / 5.0))  # rises slightly at the rim
    # Swatches are assigned per 2 m QUAD (not per triangle) and kept close in value: variation without a checkerboard.
    obj = K.object_from_bmesh("SM_PitFloor", bm, swatch="dirt_floor")
    K.set_swatch_where(obj, "dirt_floor_d", lambda c, n: rng.random() < 0.22)
    K.set_swatch_where(obj, "mud", lambda c, n: (c - Vector((5, -4, 0))).length < 2.6)
    OUT["floor"] = K.export_fbx(obj, "Environment/SM_PitFloor")
    OUT["floor_tris"] = K.tri_count([obj])


def mound(name, radius, height, seed):
    rng = random.Random(seed)
    obj = K.ico(name, radius=radius, subdiv=2, scale=(1, 1, height / radius), pivot_z=-0.15, swatch="dirt")
    me = obj.data
    for v in me.vertices:
        if v.co.z < 0:
            v.co.z = 0.0
        else:
            v.co.x += rng.uniform(-0.08, 0.08) * radius
            v.co.y += rng.uniform(-0.08, 0.08) * radius
    K.set_swatch_where(obj, "grass_dry_d", lambda c, n: n.z > 0.85 and rng.random() < 0.35)
    K.set_swatch_where(obj, "dirt_floor_d", lambda c, n: n.z < 0.45)
    return obj


def rock(name, size, seed):
    rng = random.Random(seed)
    obj = K.ico(name, radius=size, subdiv=1, scale=(1.2, 1.0, 0.7), pivot_z=-0.6, swatch="stone_grey")
    for v in obj.data.vertices:
        v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.5, 0.5))) * size * 0.18
        v.co.z = max(v.co.z, 0.0)
    K.set_swatch_where(obj, "slate", lambda c, n: n.z < 0.2)
    return obj


def crate(name):
    root = K.box(name, size=(0.9, 0.9, 0.9), pivot=(0, 0, -1), bevel=0.04, swatch="wood")
    for z in (0.06, 0.84):
        K.box(name + "_Band" + str(z), size=(0.94, 0.94, 0.12), location=(0, 0, z), pivot=(0, 0, 0), swatch="wood_dark",
              parent=root)
    K.box(name + "_Slat", size=(0.94, 0.94, 0.08), location=(0, 0, 0.45), pivot=(0, 0, 0), swatch="wood_dark", parent=root)
    return root


def barrel(name):
    root = K.cylinder(name, radius=0.34, depth=0.95, verts=8, pivot=(0, 0, -1), swatch="wood_light")
    for z in (0.15, 0.80):
        K.cylinder(name + "_Hoop" + str(z), radius=0.36, depth=0.07, verts=8, location=(0, 0, z), pivot=(0, 0, 0),
                   swatch="rusty_iron", parent=root)
    return root


def fence(name, length=3.0):
    root = K.empty(name)
    for x in (-length / 2, length / 2):
        K.box(name + "_Post" + ("L" if x < 0 else "R"), size=(0.16, 0.16, 1.35), location=(x, 0, 0), pivot=(0, 0, -1),
              taper=(0.8, 0.8), swatch="wood_dark", parent=root)
    for z in (0.45, 0.95):
        K.box(name + "_Rail" + str(z), size=(length + 0.1, 0.09, 0.11), location=(0, 0, z), pivot=(0, 0, 0),
              swatch="wood", parent=root)
    K.cylinder(name + "_Rope", radius=0.03, depth=length, verts=4, location=(0, -0.06, 1.18), pivot=(0, 0, 0), axis="X",
               swatch="rope", parent=root)
    return root


def brazier(name):
    root = K.empty(name)
    for i in range(3):
        a = i * 2 * math.pi / 3
        leg = K.box(name + "_Leg" + str(i), size=(0.08, 0.08, 0.9), location=(math.cos(a) * 0.22, math.sin(a) * 0.22, 0),
                    pivot=(0, 0, -1), swatch="iron_d", parent=root)
        leg.rotation_euler = (math.sin(a) * -0.18, math.cos(a) * 0.18, 0)
    K.cylinder(name + "_Bowl", radius=0.42, depth=0.28, verts=8, location=(0, 0, 0.82), pivot=(0, 0, -1), taper=1.25,
               swatch="iron", parent=root)
    K.cylinder(name + "_Coals", radius=0.42, depth=0.06, verts=8, location=(0, 0, 1.05), pivot=(0, 0, -1),
               swatch="ember", parent=root)
    K.cone(name + "_FlameOuter", radius=0.36, depth=0.75, verts=6, location=(0, 0, 1.08), pivot=(0, 0, -1),
           swatch="fire_mid", parent=root)
    K.cone(name + "_FlameInner", radius=0.2, depth=0.5, verts=5, location=(0.04, -0.02, 1.08), pivot=(0, 0, -1),
           swatch="fire_core", parent=root)
    return root


def ramp(name):
    root = K.wedge(name, size=(2.2, 4.0, 1.2), pivot=(0, -1, -1), swatch="wood")
    # wedge: low edge at -Y (front). pivot (0,-1,-1) = front-bottom centre.
    for i in range(5):
        y = 0.4 + i * 0.8
        K.box(name + "_Plank" + str(i), size=(2.25, 0.08, 0.03), location=(0, y, y * 1.2 / 4.0 + 0.012), pivot=(0, 0, 0),
              swatch="wood_dark", parent=root)
    return root


def stairs(name, steps=4, rise=0.3, run=0.5, width=2.2):
    root = K.empty(name)
    for i in range(steps):
        K.box(name + "_Step" + str(i), size=(width, run * (steps - i), rise), location=(0, run * i + run * (steps - i) / 2, rise * i),
              pivot=(0, 0, -1), bevel=0.02, swatch="sandstone" if i % 2 == 0 else "sand_d", parent=root)
    return root


def platform(name, size=4.0, height=1.2):
    root = K.empty(name)  # origin at the base; posts reach the ground
    deck = K.box(name + "_Deck", size=(size, size, 0.2), location=(0, 0, height - 0.2), pivot=(0, 0, -1), bevel=0.03,
                 swatch="wood", parent=root)
    for sx in (-1, 1):
        for sy in (-1, 1):
            K.box(name + "_Post%d%d" % (sx, sy), size=(0.25, 0.25, height - 0.2),
                  location=(sx * (size / 2 - 0.2), sy * (size / 2 - 0.2), 0), pivot=(0, 0, -1), swatch="wood_dark",
                  parent=root)
    for i in range(4):
        K.box(name + "_Board" + str(i), size=(size + 0.02, 0.05, 0.03), location=(0, -size / 2 + 0.5 + i * 1.0, 0.2),
              pivot=(0, 0, -1), swatch="wood_dark", parent=deck)
    return root


if PART == "floor":
    K.clear_scene()
    floor()
elif PART == "props":
    K.clear_scene()
    items = {
        "SM_Mound_A": mound("SM_Mound_A", 2.6, 1.0, 11),
        "SM_Rock_A": rock("SM_Rock_A", 0.7, 3),
        "PR_Crate": crate("PR_Crate"),
        "PR_Barrel": barrel("PR_Barrel"),
        "SM_Fence_3m": fence("SM_Fence_3m"),
        "PR_Brazier": brazier("PR_Brazier"),
    }
    sheets = {}
    for i, (n, o) in enumerate(items.items()):
        sheets[n] = K.review_render(o, n.lower(), size=256)["tris"]
        OUT[n] = K.export_fbx(o, "Environment/" + n)
    OUT["tris"] = sheets
elif PART == "structures":
    K.clear_scene()
    items = {"SM_Ramp": ramp("SM_Ramp"), "SM_Stairs": stairs("SM_Stairs"), "SM_Platform": platform("SM_Platform")}
    for n, o in items.items():
        OUT[n + "_tris"] = K.review_render(o, n.lower(), size=256)["tris"]
        OUT[n] = K.export_fbx(o, "Environment/" + n)

OUT["blend"] = K.save_blend("lookdev_env_" + PART)
result = OUT
