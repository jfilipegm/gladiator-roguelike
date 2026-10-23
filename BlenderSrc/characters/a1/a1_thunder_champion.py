"""A1 roster — the Thunder Champion in the simple style (pattern: a1_nobody.py).

Identity kept from ch_thunderchampion.py: a bronze cuirass over a blue tunic skirt with leather strips, a leather
belt, the short blue cape with the white bolt on its back (the player's mark from behind), the bronze storm helm with
a dark brow, cheek guards and the tall glowing white-cyan zigzag crest side to side, glowing cyan eyes, bare arms
with bronze shoulder caps and bracers, bronze greaves and sandals, the storm spear held upright (a bronze leaf blade
with a glowing core above his head).
Changed: the cuirass's gold abs plate is a gold bolt emblem (no anatomy in this style).
export=1: the game model Assets/Art/Models/A1/Characters/CH_ThunderChampion.fbx (a1_export; docs/tracks/A1-ingame-contract.md).
"""
import importlib
import os
import sys

import bpy

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import a1_glad_lib as G
importlib.reload(G)
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.6
BOLT = [(0.06, 0.0), (-0.06, -0.18), (0.03, -0.18), (-0.07, -0.40), (0.09, -0.14), (0.0, -0.14), (0.10, 0.0)]


def build():
    K.clear_scene()
    h = A.humanoid(OX, p=dict(TW=0.21, AR=0.07, SX=0.27), skin="skin_mid", skin_shade="skin_tan", top="bronze_l",
                   top_shade=None, belt="leather_d", foot="skin_mid", sole="leather_d", hair_style="none",
                   eyes="glow_cyan", brows="bronze_d", skip=("Buckle",))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, TW, HW = p["H"], p["S"], p["TW"], p["HW"]
    hz = a["hz"]
    ry = a["head_back_y"]
    by_name = {o.name: o for o in parts}
    # the blue tunic skirt under the cuirass, leather strips over it
    A.paint(by_name["Torso"], "cloth_blue", lambda c, n: c.z < H - 0.005, OX)
    pt = S.strips("Pteruges", count=10, radius_top=(TW * 1.0, TW * 0.73), radius_bottom=(TW * 1.24, TW * 0.92),
                  z_top=H + 0.01, length=0.15, gap=0.35, thickness=0.012, location=(OX, 0, 0), swatch="leather",
                  skip=())
    parts.append(A.flat(pt))
    # a gold bolt on the chest
    parts.append(G.panel("Emblem", [(x * 0.8, z * 0.8) for x, z in BOLT], 0.014,
                         (0, -TW * 0.66 - 0.004, a["chest_z"] + 0.13), "gold_l", OX))
    # bronze shoulder caps, bracers and greaves on the bare limbs
    wz = Sz - p["AL"]
    for s, sx, arc in (("L", 1, (-110, 110)), ("R", -1, (70, 290))):
        A.paint(by_name["Arm" + s], "bronze_l", lambda c, n: c.z < wz + 0.12, OX)
        A.paint(by_name["Leg" + s], "bronze_l", lambda c, n: 0.1 < c.z < H * 0.55, OX)
        sh = a["shoulder_l"] if sx > 0 else a["shoulder_r"]
        parts.append(A.flat(S.shell("Shoulder" + s, radius=(p["AR"] * 1.6, p["AR"] * 1.55), arc=arc, z=(-0.05, 0.06),
                                    taper=0.6, thickness=0.02, segments=4,
                                    location=(sh[0] + OX - 0.01 * sx, 0.0, sh[2]), swatch="bronze_l", flare=0.01)))

    # the storm helm: a bronze bowl, a dark brow band, cheek guards
    helm = A.loft("Helm", [(hz(0.63), A.oct8(HW * 1.08, ry * 1.08, 0.62)), (hz(0.80), A.oct8(HW * 1.08, ry * 1.08, 0.62)),
                           (hz(0.96), A.oct8(HW * 0.9, ry * 0.9, 0.6)), (hz(1.07), A.oct8(HW * 0.44, ry * 0.44, 0.6))],
                  "bronze_l", OX, cap_bottom=False)
    A.paint(helm, "bronze_d", lambda c, n: c.z < hz(0.72), OX)
    parts.append(helm)
    for s, arc in (("L", (-44, 14)), ("R", (166, 224))):
        parts.append(A.flat(S.shell("Cheek" + s, radius=(HW * 1.07, ry * 1.07), arc=arc, z=(hz(0.16), hz(0.66)),
                                    taper=1.0, thickness=0.02, segments=2, location=(OX, 0.0, 0.0),
                                    swatch="bronze_l")))
    # the zigzag crest, side to side across the helm, on a dark bronze base
    crest = [(-0.27, 0.0), (0.02, 0.0), (-0.09, 0.17), (0.11, 0.17), (0.0, 0.42), (0.27, 0.10), (0.06, 0.10),
             (0.15, 0.0), (0.27, 0.0), (0.27, -0.03), (-0.27, -0.03)]
    parts.append(G.panel("Crest", crest, 0.08, (0, 0.01, hz(1.05)), "glow_cyan", OX))
    parts.append(A.box("CrestBase", (0.44, 0.10, 0.05), (0, 0.01, hz(1.0)), "bronze_d", OX, pivot=(0, 0, -1)))

    # the short blue cape, the white bolt on its back
    top = (0, a["back_y"] + 0.03, Sz + 0.02)
    parts.append(G.cape("Cape", 0.40, 0.54, 0.58, top, "cloth_blue", OX, cols=6, depth=0.03, tilt=10.0, bend=0.05,
                        seed=2))
    parts.append(G.panel("CapeBolt", [(x, z - 0.08) for x, z in BOLT], 0.012, top, "player_white", OX, tilt=10.0,
                         dy=0.022))

    # the storm spear, upright in the right fist: a bronze leaf blade with a glowing core above his head
    hx, hy, hzz = a["hand_r"]
    parts.append(G.tube("StormSpearShaft", [(hx, hy, 0.07), (hx, hy, 1.80)], [0.024, 0.024], "wood_dark", OX, sides=6))
    for k, z in enumerate((hzz + 0.085, hzz - 0.105)):
        parts.append(A.loft("StormSpearBand%d" % k, [(z - 0.025, A.ngon(6, 0.034, hx, hy)), (z + 0.025, A.ngon(6, 0.034, hx, hy))],
                            "bronze_d", OX))
    parts.append(A.loft("StormSpearSocket", [(1.76, A.ngon(6, 0.036, hx, hy)), (1.86, A.ngon(6, 0.03, hx, hy))], "bronze", OX))
    blade = [(0.0, 0.0), (0.075, 0.11), (0.045, 0.27), (0.0, 0.38), (-0.045, 0.27), (-0.075, 0.11)]
    parts.append(G.panel("StormSpearBlade", blade, 0.035, (hx, hy, 1.84), "bronze_l", OX))
    core = [(0.0, 0.05), (0.035, 0.14), (0.0, 0.30), (-0.035, 0.14)]
    parts.append(G.panel("StormSpearCore", core, 0.05, (hx, hy, 1.84), "glow_cyan", OX))
    return parts, p


parts, p = build()
if EXPORT:
    meshes = [o for o in parts if o.type == "MESH"]   # not the weapon frames (their pieces are listed)
    result = X.export("CH_ThunderChampion", "Characters", meshes, X.joints(p),
                      rules=[("Pteruges", "Body"), ("Emblem", "Body"), ("Shoulder", "Arm"), ("Cheek", "Head"),
                             ("StormSpear", "ArmR")])
    root = bpy.data.objects["CH_ThunderChampion"]   # build_skins.py reads the model's root
else:
    result = A.review("thunder_champion", parts, original_blend="ch_thunderchampion", original_root="CH_ThunderChampion",
                      height=2.0, gap=1.2)
