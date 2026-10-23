"""A1 roster — the Golden Idol in the simple style (pattern: a1_nobody.py).

Identity kept from ch_goldenidol.py: a champion gilded head to toe (bright gold body and arms, darker gold legs and
skirt), the crimson sash across him, a dark gold belt with a crimson gem, the calm golden mask (glowing cyan eye
slits, a small dark smile) under a dark gold laurel band, the sun-crown behind his head (a gold disc ringed with
bright rays, none straight down; its back painted player cyan, his mark from behind), gold shoulder caps, crimson
bangles, crimson boots, and the sceptre in the left fist (a dark gold rod, a big golden orb with a crimson gem).
export=1: the game model Assets/Art/Models/A1/Characters/CH_GoldenIdol.fbx (a1_export; docs/tracks/A1-ingame-contract.md).
"""
import importlib
import math
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


def build():
    K.clear_scene()
    h = A.humanoid(OX, p=dict(TW=0.235, AR=0.072, SX=0.28), skin="gold_l", skin_shade="gold", top="gold_l",
                   top_shade=None, belt="gold_d", foot="crimson_d", sole="crimson_d", legs="trousers",
                   leg_colour="gold_d", hair_style="none", face=False, skip=("Buckle",))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, TW, HW = p["H"], p["S"], p["TW"], p["HW"]
    hz = a["hz"]
    by_name = {o.name: o for o in parts}
    A.paint(by_name["Torso"], "gold_d", lambda c, n: c.z < H - 0.005, OX)   # the skirt
    wz = a["waist_z"]
    parts.append(A.box("Gem", (0.07, 0.03, 0.07), (0, -TW * 0.65 - 0.012, wz), "crimson_d", OX, rot=(0, 0.785, 0)))
    # the crimson sash, left shoulder to right hip, front and back
    sash = [(-0.05, 0.27), (0.05, 0.27), (0.05, -0.25), (-0.05, -0.25)]
    parts.append(G.panel("SashFront", sash, 0.018, (0.0, -TW * 0.66 - 0.012, a["chest_z"] - 0.06), "crimson", OX,
                         roll=34.0))
    parts.append(G.panel("SashBack", sash, 0.018, (0.0, TW * 0.66 + 0.012, a["chest_z"] - 0.06), "crimson", OX,
                         roll=34.0))
    # gold shoulder caps, crimson bangles at the wrists, crimson boot cuffs
    wr = Sz - p["AL"]
    for s, sx, arc in (("L", 1, (-110, 110)), ("R", -1, (70, 290))):
        sh = a["shoulder_l"] if sx > 0 else a["shoulder_r"]
        parts.append(A.flat(S.shell("Pauldron" + s, radius=(p["AR"] * 1.65, p["AR"] * 1.6), arc=arc, z=(-0.05, 0.06),
                                    taper=0.6, thickness=0.02, segments=4,
                                    location=(sh[0] + OX - 0.01 * sx, 0.0, sh[2]), swatch="gold", flare=0.01)))
        x = (p["SX"] + 0.02) * sx
        parts.append(A.loft("Bangle" + s, [(wr + 0.04, A.hex6(p["AR"] * 0.98, p["AR"] * 0.96, x, -0.015)),
                                           (wr + 0.09, A.hex6(p["AR"] * 1.02, p["AR"] * 1.0, x, -0.014))],
                            "crimson", OX, False, False))
        lx = p["LX"] * sx
        parts.append(A.loft("Cuff" + s, [(0.08, A.hex6(p["LR"] * 0.8, p["LR"] * 0.86, lx, 0.0)),
                                         (0.15, A.hex6(p["LR"] * 0.84, p["LR"] * 0.9, lx, 0.0))], "crimson_d", OX,
                            False, False))

    # the calm golden mask: glowing cyan eye slits, a gold nose, a small dark smile; the laurel band
    fy = a["head_front_y"]
    for s, x in (("L", 0.095), ("R", -0.095)):
        parts.append(A.box("Eye" + s, (0.12, 0.016, 0.03), (x, fy - 0.003, a["eye_z"]), "glow_cyan", OX))
    parts.append(A.box("Nose", (HW * 0.22, 0.05, a["p"]["HH"] * 0.15), (0, fy - 0.01, hz(0.33)), "gold", OX,
                       pivot=(0, 0, -1), taper=(0.5, 0.3)))
    parts.append(A.box("Smile", (0.10, 0.016, 0.022), (0, fy - 0.003, hz(0.2)), "gold_d", OX))
    parts += A.headband(a, "gold_d", OX, tails=False)

    # the sun-crown behind the head: a gold disc, bright rays round it (none straight down), a cyan back
    cz, cy, R = hz(0.58), a["head_back_y"] + 0.02, 0.37
    disc = [(R * math.sin(math.radians(15 + 30 * k)), R * math.cos(math.radians(15 + 30 * k))) for k in range(12)]
    parts.append(G.panel("Disc", disc, 0.045, (0, cy, cz), "gold", OX))
    parts.append(G.panel("DiscBack", [(x * 0.82, z * 0.82) for x, z in disc], 0.02, (0, cy, cz), "player_cyan", OX,
                         dy=0.025))
    for k in range(11):
        ang = math.radians(-150 + 30 * k)
        ln = 0.17 if k % 2 else 0.25
        r0 = R - 0.03
        tip = (math.sin(ang) * (R + ln), math.cos(ang) * (R + ln))
        side = math.radians(9)
        b1 = (math.sin(ang - side) * r0, math.cos(ang - side) * r0)
        b2 = (math.sin(ang + side) * r0, math.cos(ang + side) * r0)
        parts.append(G.panel("Ray%d" % k, [b1, tip, b2], 0.035, (0, cy, cz), "gold_l", OX))

    # the sceptre in the left fist: a dark gold rod, a big golden orb with a crimson gem
    hx, hy, hzz = a["hand_l"]
    parts.append(G.tube("Rod", [(hx, hy, hzz - 0.22), (hx, hy, hzz + 0.62)], [0.026, 0.026], "gold_d", OX, sides=6))
    oz = hzz + 0.71
    parts.append(G.ball("Orb", 0.11, (hx, hy, oz), "gold_l", OX))
    parts.append(A.box("OrbGem", (0.06, 0.04, 0.06), (hx, hy - 0.105, oz), "crimson", OX, rot=(0, 0.785, 0)))
    parts.append(A.loft("OrbCollar", [(oz - 0.13, A.ngon(6, 0.04, hx, hy)), (oz - 0.09, A.ngon(6, 0.05, hx, hy))],
                        "gold_d", OX))
    return parts, p


parts, p = build()
if EXPORT:
    meshes = [o for o in parts if o.type == "MESH"]   # not the weapon frames (their pieces are listed)
    result = X.export("CH_GoldenIdol", "Characters", meshes, X.joints(p),
                      rules=[("Gem", "Body"), ("Pauldron", "Arm"), ("Bangle", "Arm"), ("Cuff", "side"), ("Smile", "Head"),
                             ("Disc", "Head"), ("Ray", "Head"), ("Rod", "ArmL"), ("Orb", "ArmL")])
    root = bpy.data.objects["CH_GoldenIdol"]   # build_skins.py reads the model's root
else:
    result = A.review("golden_idol", parts, original_blend="ch_goldenidol", original_root="CH_GoldenIdol", height=1.9,
                      gap=1.2)
