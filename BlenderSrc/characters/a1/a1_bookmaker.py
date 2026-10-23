"""A1 roster — the Bookmaker (the Provincial Pit's shady bettor, CH_Bookmaker in BlenderSrc/props/secrets.py) in the
simple style (pattern: a1_nobody.py).

Identity kept from secrets.py's bookmaker(): short and fat, a green robe over a round belly with a gold sash, dark
leather legs under it, a tanned round face with small dark eyes and a big nose, a leather hat with a wide brim, the
ledger (dark wooden covers, bone pages) under his left arm, a white quill raised in his right hand, a fat gold coin
pouch on the sash. No player cyan (he isn't the player), no orange-red (he doesn't hurt you).
The original is one joined mesh in secrets.blend beside PR_GoldenRat, so the review appends it here and drops the rat.
export=1: the game model Assets/Art/Models/A1/Characters/CH_Bookmaker.fbx (a1_export): the original is one static mesh
with no parts (Bookmaker.cs only turns him to face you), so every piece goes on one "Root" mesh under CH_Bookmaker.
"""
import importlib
import os
import sys

import bpy

import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import a1_glad_lib as G
importlib.reload(G)
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
GAP = 1.2
OX = 0.0 if EXPORT else -GAP / 2


def build():
    K.clear_scene()
    p = dict(H=0.32, S=0.80, SX=0.27, N=0.88, HH=0.45, HW=0.225, TW=0.25, LR=0.085, LX=0.10)
    h = A.humanoid(OX, p=p, skin="skin_mid", skin_shade="skin_tan", top="cloth_green", top_shade=None, belt=None,
                   foot="leather_d", sole="ink", legs="trousers", leg_colour="leather_d", hair_style="none", face=False,
                   skip=("Arm", "Fist"))
    parts, a = h["parts"], h["a"]
    H, Sz, N, TW, HW = p["H"], p["S"], p["N"], p["TW"], p["HW"]
    hz = a["hz"]
    ry = a["head_back_y"]
    # the robe flaring down to the shins, the round belly, the gold sash, the coin pouch
    parts.append(A.loft("Robe", [(0.17, A.oct8(TW * 1.3, TW * 1.02, 0.62)), (H, A.oct8(TW * 1.14, TW * 0.9)),
                                 (a["waist_z"], A.oct8(TW * 0.95, TW * 0.70))], "cloth_green", OX, cap_bottom=False,
                        cap_top=False))
    bz = a["waist_z"] + 0.02
    parts.append(A.loft("Belly", [(bz - 0.17, A.oct8(TW * 0.9, TW * 0.7, 0.62, 0, -0.03)),
                                  (bz - 0.06, A.oct8(TW * 1.14, TW * 0.98, 0.62, 0, -0.07)),
                                  (bz + 0.08, A.oct8(TW * 1.12, TW * 0.96, 0.62, 0, -0.06)),
                                  (bz + 0.19, A.oct8(TW * 0.95, TW * 0.72, 0.62, 0, -0.02))], "cloth_green", OX))
    parts.append(A.loft("Sash", [(bz - 0.035, A.oct8(TW * 1.18, TW * 1.02, 0.62, 0, -0.07)),
                                 (bz + 0.035, A.oct8(TW * 1.17, TW * 1.01, 0.62, 0, -0.065))], "gold", OX, False, False))
    parts.append(G.ball("Pouch", 0.075, (-0.13, -0.355, bz - 0.10), "gold_coin", OX, n=6, rz=0.09))
    parts.append(A.box("PouchTie", (0.05, 0.05, 0.04), (-0.13, -0.345, bz - 0.01), "leather", OX))

    # the face: small dark eyes, heavy brows, a big nose; the wide-brimmed leather hat
    fy = -ry * 0.93
    for s, x in (("L", 0.075), ("R", -0.075)):
        parts.append(A.box("Eye" + s, (0.055, 0.014, 0.04), (x, fy - 0.002, hz(0.52)), "ink", OX))
        parts.append(A.box("Brow" + s, (0.09, 0.025, 0.025), (x * 1.05, fy - 0.004, hz(0.6)), "mud_dark", OX,
                           rot=(0, 0.25 if x > 0 else -0.25, 0)))
    parts.append(G.tube("Nose", [(0, fy + 0.03, hz(0.44)), (0, fy - 0.06, hz(0.36)), (0, fy - 0.10, hz(0.27))],
                        [0.05, 0.045, 0.0], "skin_tan", OX, sides=5))
    parts.append(A.box("Smirk", (0.10, 0.014, 0.018), (0.015, fy - 0.002, hz(0.18)), "skin_dark", OX, rot=(0, -0.12, 0)))
    parts.append(A.loft("Brim", [(hz(0.74), A.ngon(8, HW * 1.45, 0, 0, 0.39)), (hz(0.79), A.ngon(8, HW * 1.42, 0, 0, 0.39))],
                        "leather", OX))
    parts.append(A.loft("Crown", [(hz(0.70), A.oct8(HW * 1.04, ry * 1.04, 0.62)), (hz(0.98), A.oct8(HW * 0.82, ry * 0.82, 0.6)),
                                  (hz(1.12), A.oct8(HW * 0.62, ry * 0.62, 0.6))], "leather_d", OX, cap_bottom=False))

    # the left arm hugging the ledger (dark covers, bone pages) to his side
    parts.append(G.tube("ArmL", [(0.27, 0.0, Sz + 0.03), (0.31, 0.03, Sz - 0.14), (0.30, -0.04, Sz - 0.29)],
                        [0.07, 0.065, 0.06], "cloth_green", OX, sides=6))
    parts.append(G.ball("HandL", 0.055, (0.31, -0.10, Sz - 0.31), "skin_mid", OX, n=6))
    lz, lx = Sz - 0.17, 0.39
    parts.append(A.box("Ledger", (0.085, 0.28, 0.36), (lx, -0.06, lz), "wood_dark", OX, rot=(0, -0.12, 0)))
    parts.append(A.box("Pages", (0.065, 0.29, 0.34), (lx, -0.06, lz), "bone", OX, rot=(0, -0.12, 0)))
    # the right arm raised forward with the white quill
    hand = (-0.30, -0.25, Sz - 0.06)
    parts.append(G.tube("ArmR", [(-0.27, 0.0, Sz + 0.03), (-0.32, -0.04, Sz - 0.17), hand], [0.07, 0.065, 0.058],
                        "cloth_green", OX, sides=6))
    parts.append(G.ball("HandR", 0.058, (hand[0], hand[1] - 0.03, hand[2]), "skin_mid", OX, n=6))
    qb = (hand[0], hand[1] - 0.03, hand[2] + 0.02)
    parts.append(G.tube("Nib", [(qb[0] + 0.012, qb[1] - 0.03, qb[2] - 0.11), (qb[0] + 0.006, qb[1] - 0.015, qb[2] - 0.05)],
                        [0.0, 0.012], "ink", OX, sides=4))
    parts.append(G.tube("Shaft", [(qb[0] + 0.006, qb[1] - 0.015, qb[2] - 0.05), (qb[0] - 0.01, qb[1] + 0.02, qb[2] + 0.07)],
                        [0.01, 0.01], "ivory", OX, sides=4))
    parts.append(G.tube("Quill", [(qb[0] - 0.01, qb[1] + 0.02, qb[2] + 0.06), (qb[0] - 0.03, qb[1] + 0.07, qb[2] + 0.16),
                                  (qb[0] - 0.06, qb[1] + 0.15, qb[2] + 0.28), (qb[0] - 0.09, qb[1] + 0.26, qb[2] + 0.36),
                                  (qb[0] - 0.11, qb[1] + 0.36, qb[2] + 0.38)], [0.03, 0.055, 0.05, 0.035, 0.0],
                        "ivory", OX, sides=4, squash=0.2, roll=0.785))
    return parts


parts = build()
if EXPORT:
    result = X.export("CH_Bookmaker", "Characters", parts, {}, rules=[("", "Root")])
    root = bpy.data.objects["CH_Bookmaker"]
else:
    A._append_original("secrets", "CH_Bookmaker", (GAP / 2, 0, 0))
    for o in list(bpy.data.objects):
        if o.name.startswith("PR_"):
            bpy.data.objects.remove(o, do_unlink=True)
    result = A.review("bookmaker", parts, height=1.6, gap=GAP)
