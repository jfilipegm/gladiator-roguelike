"""M1.1 feel-test pieces: a hill with a 30 deg slide face + 20 deg back ramp, a stone wall, a pillar.
Front = -Y (Unity +Z). Origins at the base."""
import importlib
import math
import random

import gladiator_kit as K

importlib.reload(K)
OUT = {}
K.clear_scene()

# --- SM_TestHill: profile in YZ, extruded 5 m along X. Steep 30 deg face toward -Y (front), flat top, 20 deg back.
h = 3.4
front_run = h / math.tan(math.radians(30))
back_run = h / math.tan(math.radians(20))
pts = [(-front_run - 1.0, 0.0), (-1.0, h), (1.0, h), (1.0 + back_run, 0.0)]
hill = K.extrude_poly("SM_TestHill", pts, thickness=5.0, plane="YZ", swatch="dirt")
K.set_swatch_where(hill, "dirt_floor_d", lambda c, n: abs(n.x) > 0.9)
K.set_swatch_where(hill, "grass_dry_d", lambda c, n: n.z > 0.99)
OUT["SM_TestHill"] = K.export_fbx(hill, "Environment/SM_TestHill")
OUT["hill_front_run"] = front_run
OUT["hill_back_run"] = back_run

# --- SM_Wall_Stone_4m: 4 x 0.9 x 3.2 m, blocky courses for readability
rng = random.Random(5)
wall = K.box("SM_Wall_Stone_4m", size=(4.0, 0.9, 3.2), pivot=(0, 0, -1), swatch="stone_grey")
for row in range(4):
    z = 0.2 + row * 0.8
    off = 0.5 if row % 2 else 0.0
    x = -2.0 + off
    i = 0
    while x < 2.0:
        w = min(rng.uniform(0.7, 1.2), 2.0 - x)
        if w > 0.25:
            K.box("SM_Wall_Stone_4m_B%d_%d" % (row, i), size=(w - 0.06, 0.96, 0.72), location=(x + w / 2, 0, z),
                  pivot=(0, 0, -1), swatch=rng.choice(["stone_grey", "stone_grey", "slate", "ash"]), parent=wall)
        x += w
        i += 1
OUT["SM_Wall_Stone_4m"] = K.export_fbx(wall, "Environment/SM_Wall_Stone_4m")

# --- SM_Pillar: 8-sided column with base and capital
pillar = K.cylinder("SM_Pillar", radius=0.45, depth=3.6, verts=8, location=(0, 0, 0.3), pivot=(0, 0, -1), swatch="sandstone")
K.box("SM_Pillar_Base", size=(1.2, 1.2, 0.3), location=(0, 0, -0.3), pivot=(0, 0, -1), bevel=0.04, swatch="sandstone_d", parent=pillar)
K.box("SM_Pillar_Cap", size=(1.2, 1.2, 0.3), location=(0, 0, 3.6), pivot=(0, 0, -1), bevel=0.04, swatch="sandstone_d", parent=pillar)
OUT["SM_Pillar"] = K.export_fbx(pillar, "Environment/SM_Pillar")

for n, o in (("sm_testhill", hill), ("sm_wall_stone_4m", wall), ("sm_pillar", pillar)):
    for other in (hill, wall, pillar):
        for d in K.descendants(other):
            d.hide_render = other is not o
    OUT[n + "_tris"] = K.review_render(o, n, size=256)["tris"]
for o in (hill, wall, pillar):
    for d in K.descendants(o):
        d.hide_render = False
OUT["blend"] = K.save_blend("test_arena_pieces")
result = OUT
