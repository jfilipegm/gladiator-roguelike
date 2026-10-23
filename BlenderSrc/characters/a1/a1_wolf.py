"""A1 roster — the Beastmaster's Wolf in the simple style (pattern: a1_nobody.py; a quadruped, so the body is a
horizontal loft, G.hloft).

Identity kept from ch_wolf.py: a grey timber wolf, bigger and leaner than the Pit Cur, a slate stripe down the back,
a pale belly, the player's cyan collar and glowing cyan eyes (an ally among the curs, never their magenta), a pale
snout with a dark nose and white fangs, slate ears up, slate tail out and back, pale paws. Budget <= 450 tris.
export=1: the game model Assets/Art/Models/A1/Characters/CH_Wolf.fbx (a1_export; docs/tracks/A1-ingame-contract.md).
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
OX = 0.0 if EXPORT else -0.55
BZ = 0.50     # the body's centre line
# CH_Wolf's rig: the front legs are ArmL/ArmR (under Body), the hind legs LegL/LegR (under the root), the Head under Body
WOLF_JOINTS = {"Body": (0.0, 0.04, 0.40), "Head": (0.0, -0.38, BZ + 0.07), "ArmL": (0.095, -0.255, BZ - 0.02),
               "ArmR": (-0.095, -0.255, BZ - 0.02), "LegL": (0.095, 0.30, BZ), "LegR": (-0.095, 0.30, BZ)}


def build():
    K.clear_scene()
    parts = []
    # the body: chest (rising to the neck) to rump, lean at the waist; a slate back, a pale belly
    body = G.hloft("Body", [(0.0, A.oct8(0.11, 0.12, 0.62, 0, 0.07)), (0.10, A.oct8(0.15, 0.145, 0.62, 0, 0.02)),
                            (0.42, A.oct8(0.125, 0.115, 0.62, 0, 0.01)), (0.66, A.oct8(0.14, 0.13, 0.62, 0, 0.02)),
                            (0.78, A.oct8(0.10, 0.09, 0.62, 0, 0.03))], "stone_grey", OX, (0, -0.36, BZ), back=True)
    A.paint(body, "slate", lambda c, n: n.z > 0.6, OX)
    A.paint(body, "ash", lambda c, n: n.z < -0.6, OX)
    parts.append(body)
    # the cyan collar round the neck
    parts.append(G.hloft("Collar", [(0.0, A.oct8(0.175, 0.17, 0.62)), (0.09, A.oct8(0.17, 0.165, 0.62))], "player_cyan",
                         OX, (0, -0.33, BZ + 0.07), cap_start=False, cap_end=False))
    # the head: a grey skull, a pale snout, dark nose, white fangs, cyan eyes, slate ears
    hy, hz0 = -0.47, BZ + 0.02
    parts.append(A.loft("Head", [(hz0, A.oct8(0.125, 0.125, 0.64, 0, hy)), (hz0 + 0.15, A.oct8(0.155, 0.142, 0.62, 0, hy)),
                                 (hz0 + 0.29, A.oct8(0.115, 0.115, 0.6, 0, hy + 0.01))], "stone_grey", OX))
    sf = hy - 0.142
    parts.append(G.hloft("Snout", [(0.0, A.oct8(0.085, 0.062, 0.62)), (0.18, A.oct8(0.066, 0.05, 0.6))], "ash", OX,
                         (0, sf + 0.03, hz0 + 0.06)))
    tip = sf + 0.03 - 0.18
    parts.append(A.box("Nose", (0.055, 0.03, 0.04), (0, tip - 0.004, hz0 + 0.085), "ink", OX))
    parts.append(A.box("Fangs", (0.10, 0.025, 0.025), (0, tip + 0.02, hz0 + 0.005), "white", OX))
    for s, x in (("L", 1), ("R", -1)):
        parts.append(A.box("Eye" + s, (0.055, 0.016, 0.034), (x * 0.07, sf - 0.003, hz0 + 0.18), "glow_cyan", OX,
                           rot=(0, -0.25 * x, 0)))
        parts.append(G.tube("Ear" + s, [(x * 0.085, hy + 0.03, hz0 + 0.23), (x * 0.10, hy + 0.05, hz0 + 0.43)],
                            [0.055, 0.0], "slate", OX, sides=4, squash=0.55))
    # legs: front under the chest, hind with a thigh; pale paws
    for s, x in (("L", 0.095), ("R", -0.095)):
        parts.append(A.loft("Arm" + s, [(0.045, A.hex6(0.038, 0.042, x, -0.25)), (0.25, A.hex6(0.042, 0.048, x, -0.255)),
                                        (BZ - 0.02, A.hex6(0.052, 0.062, x, -0.26))], "stone_grey", OX, cap_top=False))
        parts.append(A.loft("Leg" + s, [(0.045, A.hex6(0.038, 0.044, x, 0.31)), (0.21, A.hex6(0.044, 0.058, x, 0.335)),
                                        (BZ, A.hex6(0.06, 0.10, x, 0.30))], "stone_grey", OX, cap_top=False))
        for nm, y in (("Paw", -0.265), ("Foot", 0.30)):
            parts.append(A.box(nm + s, (0.085, 0.11, 0.05), (x, y, 0.0), "ash", OX, pivot=(0, 0, -1)))
    # the tail: out and back, a little up, bushy
    parts.append(G.tube("Tail", [(0, 0.40, BZ + 0.06), (0, 0.56, BZ + 0.10), (0, 0.74, BZ + 0.07)], [0.04, 0.06, 0.0],
                        "slate", OX, sides=5))
    p = None   # a quadruped: its joints are WOLF_JOINTS
    return parts, p


parts, p = build()
if EXPORT:
    meshes = [o for o in parts if o.type == "MESH"]   # not the weapon frames (their pieces are listed)
    result = X.export("CH_Wolf", "Characters", meshes, WOLF_JOINTS,
                      rules=[("Body", "Body"), ("Collar", "Body"), ("Tail", "Body"), ("Snout", "Head"), ("Fangs", "Head"),
                             ("Paw", "Arm")])
    root = bpy.data.objects["CH_Wolf"]   # build_skins.py reads the model's root
else:
    result = A.review("wolf", parts, original_blend="ch_wolf", original_root="CH_Wolf", height=1.2, gap=1.1)
