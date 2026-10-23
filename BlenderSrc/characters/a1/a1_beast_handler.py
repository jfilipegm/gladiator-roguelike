"""A1 roster — the Beast Handler (EN_BeastHandler, Tier 2) in the simple style.

Identity kept from en_beasthandler.py: burly and bald with a heavy dark brow and stubble, bare-chested under a light
leather apron, a spiked iron collar round his own neck (a brag), the magenta belt and armband (the enemy accent),
a coiled whip in the right fist with the lash rising behind his shoulder, two iron chain leashes hanging from the left
fist (his Pit Curs walk on them), leather trousers and boots. Changed: the variant-A body (lean); the stubble is
painted on the jaw; the chains are plain iron bars.
export=1: the game model Assets/Art/Models/A1/Enemies/EN_BeastHandler.fbx (docs/tracks/A1-ingame-contract.md).
"""
import importlib
import math
import os
import sys

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier12_lib as L
importlib.reload(L)
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.55
RULES = [("Apron", "Body"), ("Collar", "Body"), ("Armband", "ArmR"), ("WhipCoil", "ArmR"), ("CoilHole", "ArmR"),
         ("Lash", "ArmR"), ("Chain", "ArmL")]


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.24, AR=0.08, LR=0.095, LX=0.12, SX=0.30, HS=0.078, HW=0.26), skin="skin_light",
               skin_shade="mud_dark", top=None, belt="accent_magenta", foot="leather_d", legs="trousers",
               leg_colour="mud", hair_style="none", head_top="round", torso_cap=False)
    parts, a = h["parts"], h["a"]
    p = a["p"]
    TW, H, S, AR, N = p["TW"], p["H"], p["S"], p["AR"], a["neck_z"]
    o8 = L.oct8
    parts += L.face(a, OX, eyes="ink", brows="skin_dark", nose="skin_mid", brow_w=0.56, brow_h=0.085,
                    brow_angle=0.2, brow_ledge=0.02)
    # the leather apron over the bare chest
    top_z, bot_z = a["chest_z"] + 0.03, H - 0.13
    parts.append(A.box("Apron", (TW * 1.32, 0.03, top_z - bot_z), (0, -TW * 0.70, bot_z), "leather", OX,
                       pivot=(0, 0, -1), taper=(0.82, 1.0)))
    # the spiked iron collar
    parts.append(L.ring("Collar", N - 0.06, N - 0.0, o8(TW * 0.62, TW * 0.55), "iron", OX))
    for k, sg in enumerate((1, -1)):
        parts.append(L.spike("CollarSpike%d" % k, 0.025, 0.07, (TW * 0.5 * sg, -TW * 0.3, N - 0.03), "steel_l", OX,
                             rot=(0.6, 0.9 * sg, 0)))
    # the magenta armband on the right arm; the whip coil in the right fist, the lash slung over the shoulder
    xr = -a["arm_x"]
    parts.append(L.ring("Armband", S - 0.16, S - 0.09, L.hex6(AR * 1.12, AR * 1.1, xr - 0.008, -0.01),
                        "accent_magenta", OX))
    hx, hy, hz = a["hand_r"]
    cy = hy - 0.075
    coil = K.cylinder("WhipCoil", radius=0.11, depth=0.05, verts=6, location=(hx + OX, cy, hz - 0.04),
                      pivot=(0, 0, 0), axis="Y", swatch="leather_d")
    parts.append(L.flat(coil))
    parts.append(L.decal("CoilHole", 0.09, 0.09, (hx, cy - 0.026, hz - 0.04), "ink", OX, rot_y=0.785))
    lash = A.box("Lash", (0.028, 0.028, 0.86), (hx - 0.01, hy - 0.02, hz - 0.02), "leather_d", OX, pivot=(0, 0, -1),
                 rot=(-0.42, -0.08, 0))
    parts.append(lash)
    # two chain leashes hanging from the left fist
    lx, ly, lz = a["hand_l"]
    for k, (dx, dy) in enumerate(((0.025, -0.03), (-0.02, 0.03))):
        sq = L.ngon(4, 0.017, lx + dx, ly + dy, math.pi / 4)
        parts.append(L.loft("Chain%d" % k, [(lz - 0.42 + 0.04 * k, sq), (lz - 0.05, sq)], "iron", OX, True, False))
    J = X.joints(p)
    return parts, J


parts, J = build()
if EXPORT:
    result = L.export_enemy("EN_BeastHandler", parts, J, 1.3, RULES)
else:
    result = A.review("beast_handler", parts, original_blend="en_beasthandler", original_root="EN_BeastHandler")
