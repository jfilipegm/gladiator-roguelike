"""A1 roster — the Beastmaster in the simple style (pattern: a1_nobody.py).

Identity kept from ch_beastmaster.py: a frontier hunter in a light leather jerkin with a dark leather skirt and belt,
the shaggy dark fur mantle over the shoulders with a drape down the back ending in cyan-tipped fur (his mark from
behind), the strap across the chest carrying a bone hunting horn with a cyan band, glowing cyan eyes, a dark beard,
the wolf-head hood (a grey wolf's skull over his brow, a pale snout with a dark nose and bone fangs, two slate ears
up, the hood hanging down the back of his head), bare arms with fur cuffs, leather trousers, fur boots, and the
short hunting spear held low and forward with a cyan tassel.
export=1: the game model Assets/Art/Models/A1/Characters/CH_Beastmaster.fbx (a1_export; docs/tracks/A1-ingame-contract.md).
"""
import importlib
import math
import os
import sys

import bpy

from mathutils import Vector

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
    h = A.humanoid(OX, p=dict(TW=0.21, SX=0.27, AR=0.07), skin="skin_mid", skin_shade="skin_tan", top="leather_l",
                   top_shade=None, belt="leather_d", foot="mud_dark", sole="leather_d", legs="trousers",
                   leg_colour="leather", hair_style="none", eyes="glow_cyan", brows="wood_dark", skip=("Buckle",))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, HW, AR = p["H"], p["S"], p["N"], p["TW"], p["HW"], p["AR"]
    hz = a["hz"]
    ry = a["head_back_y"]
    by_name = {o.name: o for o in parts}
    A.paint(by_name["Torso"], "leather_d", lambda c, n: c.z < H - 0.005, OX)   # the skirt
    # the beard
    parts.append(G.panel("Beard", [(-0.12, 0.0), (0.12, 0.0), (0.09, -0.10), (0.0, -0.14), (-0.09, -0.10)], 0.04,
                         (0, -ry * 0.93 + 0.005, hz(0.25)), "wood_dark", OX))
    # fur cuffs on the forearms, fur boots
    wr = Sz - p["AL"]
    for s, sx in (("L", 1), ("R", -1)):
        x = (p["SX"] + 0.02) * sx
        parts.append(A.loft("Cuff" + s, [(wr + 0.03, A.oct8(AR * 1.3, AR * 1.28, 0.64, x, -0.014)),
                                         (wr + 0.13, A.oct8(AR * 1.22, AR * 1.2, 0.64, x, -0.01))], "mud_dark", OX,
                            False, False))
        lx = p["LX"] * sx
        parts.append(A.loft("Boot" + s, [(0.07, A.oct8(p["LR"] * 1.15, p["LR"] * 1.2, 0.64, lx, -0.005)),
                                         (0.20, A.oct8(p["LR"] * 1.1, p["LR"] * 1.12, 0.64, lx, 0.0))], "mud_dark", OX,
                            False, False))

    # the fur mantle: a shaggy collar over the shoulders, a drape down the back with cyan-tipped fur
    parts.append(A.loft("Mantle", [(Sz - 0.08, A.oct8(TW * 1.55, TW * 1.12, 0.66)), (Sz + 0.04, A.oct8(TW * 1.42, TW * 1.02, 0.64)),
                                   (Sz + 0.09, A.oct8(TW * 0.95, TW * 0.78, 0.62)), (N + 0.02, A.oct8(0.12, 0.10, 0.6))],
                        "mud_dark", OX, cap_bottom=False, cap_top=False))
    top, L, tilt = (0, TW * 0.72 + 0.03, Sz + 0.02), 0.34, 8.0
    parts.append(G.cape("MantleBack", 0.44, 0.48, L, top, "mud_dark", OX, cols=6, depth=0.03, tilt=tilt, bend=0.05,
                        thickness=0.04, seed=4))
    t = math.radians(tilt)
    top2 = (0, top[1] + (L - 0.02) * math.sin(t), top[2] - (L - 0.02) * math.cos(t))
    parts.append(G.cape("FurTips", 0.48, 0.48, 0.07, top2, "player_cyan", OX, cols=6, depth=0.06, tilt=tilt, bend=0.05,
                        thickness=0.03, seed=5))

    # the strap across the chest (right shoulder to left hip) and the hunting horn at his left hip
    strap = [(-0.035, 0.30), (0.035, 0.30), (0.035, -0.28), (-0.035, -0.28)]
    parts.append(G.panel("Strap", strap, 0.016, (0.0, -TW * 0.66 - 0.012, a["chest_z"] - 0.05), "leather_d", OX,
                         roll=-36.0))
    hx0, hy0, hz0 = 0.17, -TW * 0.75 - 0.03, a["waist_z"] - 0.12
    parts.append(G.tube("Horn", [(hx0 - 0.02, hy0, hz0 + 0.02), (hx0 + 0.07, hy0, hz0 - 0.03), (hx0 + 0.12, hy0 + 0.01, hz0 + 0.06),
                                 (hx0 + 0.12, hy0 + 0.01, hz0 + 0.14)], [0.05, 0.035, 0.022, 0.0], "bone", OX, sides=6))
    parts.append(A.loft("HornBand", [(hz0 - 0.03, A.ngon(6, 0.042, hx0 + 0.035, hy0)),
                                     (hz0 + 0.02, A.ngon(6, 0.044, hx0 + 0.035, hy0))], "player_cyan", OX, False, False))

    # the wolf-head hood: a grey skull over the head, the hood down the back, a pale snout, ears up
    hood = A.loft("Hood", [(hz(0.55), A.oct8(HW * 1.08, ry * 1.08, 0.62)), (hz(0.86), A.oct8(HW * 1.08, ry * 1.08, 0.62)),
                           (hz(1.0), A.oct8(HW * 0.86, ry * 0.86, 0.6)), (hz(1.1), A.oct8(HW * 0.42, ry * 0.42, 0.6))],
                  "stone_grey", OX, cap_bottom=False)
    parts.append(hood)
    parts.append(A.flat(S.shell("HoodBack", radius=(HW * 1.2, ry * 1.2), arc=(-12, 192), z=(hz(-0.08), hz(0.80)),
                                taper=1.0, thickness=0.03, segments=6, location=(OX, 0.0, 0.0), swatch="stone_grey",
                                flare=0.02)))
    fy = -ry * 1.08
    snout = [(0.0, A.oct8(0.12, 0.08, 0.62)), (0.17, A.oct8(0.085, 0.06, 0.6))]
    parts.append(G.hloft("Snout", snout, "ash", OX, (0, fy + 0.03, hz(0.74))))
    parts.append(A.box("SnoutNose", (0.06, 0.03, 0.04), (0, fy + 0.03 - 0.17, hz(0.77)), "ink", OX))
    parts.append(A.box("Fangs", (0.13, 0.03, 0.035), (0, fy - 0.10, hz(0.66)), "bone", OX))
    for s, x in (("L", 0.075), ("R", -0.075)):
        parts.append(A.box("HoodEye" + s, (0.04, 0.02, 0.025), (x, fy - 0.005, hz(0.84)), "ink", OX))
        sx = 1 if x > 0 else -1
        parts.append(G.tube("Ear" + s, [(sx * HW * 0.58, 0.01, hz(0.96)), (sx * HW * 0.8, 0.02, hz(1.42))], [0.085, 0.0],
                            "slate", OX, sides=4, squash=0.5))

    # the short hunting spear, held low and forward
    hx, hy, hzz = a["hand_r"]
    hand = Vector((hx, hy, hzz))
    d = Vector((0.0, -0.906, -0.423))
    a0, a1, a2 = hand - d * 0.30, hand + d * 0.76, hand + d * 0.98
    parts.append(G.tube("HuntingSpearShaft", [tuple(a0), tuple(a1)], [0.022, 0.022], "wood", OX, sides=6))
    parts.append(G.tube("HuntingSpearTassel", [tuple(a1 - d * 0.06), tuple(a1)], [0.036, 0.036], "player_cyan", OX, sides=6))
    parts.append(G.tube("HuntingSpearBlade", [tuple(a1), tuple(a1 + d * 0.07), tuple(a2)], [0.024, 0.06, 0.0], "steel_l", OX,
                        sides=4, squash=0.3))
    return parts, p


parts, p = build()
if EXPORT:
    meshes = [o for o in parts if o.type == "MESH"]   # not the weapon frames (their pieces are listed)
    result = X.export("CH_Beastmaster", "Characters", meshes, X.joints(p),
                      rules=[("Cuff", "Arm"), ("Boot", "Leg"), ("Mantle", "Body"), ("FurTips", "Body"), ("Strap", "Body"),
                             ("Horn", "Body"), ("Snout", "Head"), ("Fangs", "Head"), ("HuntingSpear", "ArmR")])
    root = bpy.data.objects["CH_Beastmaster"]   # build_skins.py reads the model's root
else:
    result = A.review("beastmaster", parts, original_blend="ch_beastmaster", original_root="CH_Beastmaster", height=1.9,
                      gap=1.2)
