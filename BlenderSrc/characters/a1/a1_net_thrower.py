"""A1 roster — the Net-Thrower (EN_NetThrower, Arena 3's catcher) in the simple style.

Identity kept from en_netthrower.py: a lean retiarius with a bare head (the only bare head among the arena's
fighters: dark hair, no helmet), the tall steel galerus guarding the left shoulder, a rope net bundle slung over the
right shoulder and a weighted net balled in the right fist (the lob), a long trident upright in the left hand, the
blue loincloth under the magenta belt (the enemy accent), sandals. Changed: the variant-A body (lean); the nets are
faceted rope lumps; no leg wrap (triangles).
export=1: the game model Assets/Art/Models/A1/Enemies/EN_NetThrower.fbx (docs/tracks/A1-ingame-contract.md).
"""
import importlib
import math
import os
import sys

from mathutils import Vector

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
RULES = [("Loincloth", "Body"), ("Galerus", "Body"), ("Bundle", "Body"), ("Shaft", "ArmL"), ("Crossbar", "ArmL"),
         ("Prong", "ArmL"), ("HandNet", "ArmR")]


def net_ball(name, r, loc, scale, ox):
    o = K.ico(name, radius=r, subdiv=1, location=(loc[0] + ox, loc[1], loc[2]), scale=scale, swatch="rope")
    K.set_swatch_where(o, "wood_light", lambda c, n: (n.x + 2 * n.y + 3 * n.z) % 1.0 < 0.35)  # a knotted weave
    return L.flat(o)


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.19, AR=0.062, LR=0.08, SX=0.25, HS=0.068), skin="skin_light", skin_shade="skin_mid",
               top=None, belt="accent_magenta", foot="leather_d", hair="mud_dark", head_top="flat", torso_cap=False)
    parts, a = h["parts"], h["a"]
    p = a["p"]
    TW, H, S = p["TW"], p["H"], p["S"]
    o8 = L.oct8
    parts += L.face(a, OX, eyes="ink", brows="mud_dark", nose="skin_mid", brow_ledge=0.0)
    parts.append(L.loft("Loincloth", [(H - 0.13, o8(TW * 1.08, TW * 0.82)), (a["waist_z"] - 0.02, o8(TW * 0.92, TW * 0.68))],
                        "cloth_blue", OX, False, False))
    # the galerus: a tall steel plate standing up off the left shoulder
    xl = a["arm_x"]
    gal = K.extrude_poly("Galerus", [(-0.15, 0.0), (0.15, 0.0), (0.13, 0.34), (-0.12, 0.31)], thickness=0.04,
                         plane="YZ", swatch="steel_l", location=(xl + 0.035 + OX, 0.0, S - 0.07))
    gal.rotation_euler = (0, 0.25, -0.6)   # tilted out and turned to face front-outward
    parts.append(L.flat(gal))
    # the net bundle slung over the right shoulder
    parts.append(net_ball("Bundle", 0.14, (-xl * 0.62, 0.07, S + 0.02), (0.85, 1.35, 0.75), OX))
    # the trident upright in the left fist (the forearm a little forward)
    rot = L.rot_x(-0.4)
    L.xform(L.by_name(parts, "ArmL", "FistL"), rot, (xl, 0, S), OX)
    hand = Vector((xl, 0.0, S)) + rot.to_3x3() @ (Vector(a["hand_l"]) - Vector((xl, 0.0, S)))
    # in game the body is scaled to the original's (0.85) and the trident lengthened so its tip stays near the
    # original's 2.0 m (its height is the trident)
    top = 2.02 if EXPORT else 1.62
    sq = L.ngon(4, 0.022, hand.x, hand.y, math.pi / 4)
    parts.append(L.loft("Shaft", [(0.12, sq), (top, sq)], "wood_dark", OX, False, False))
    parts.append(A.box("Crossbar", (0.26, 0.04, 0.045), (hand.x, hand.y, top), "steel", OX, pivot=(0, 0, 0)))
    for k, (dx, ln) in enumerate(((-0.11, 0.2), (0.0, 0.26), (0.11, 0.2))):
        parts.append(L.spike("Prong%d" % k, 0.026, ln, (hand.x + dx, hand.y, top + 0.02), "steel_l", OX, n=3,
                             phase=math.pi / 2))
    # the weighted net balled in the right fist
    xr = -xl
    rot = L.rot_x(-0.3)
    L.xform(L.by_name(parts, "ArmR", "FistR"), rot, (xr, 0, S), OX)
    hand = Vector((xr, 0.0, S)) + rot.to_3x3() @ (Vector(a["hand_r"]) - Vector((xr, 0.0, S)))
    ball = L.octa("HandNet", 0.14, (hand.x, hand.y - 0.04, hand.z - 0.07), "rope", OX, scale=(1.0, 0.9, 0.85))
    L.paint(ball, "wood_light", lambda c, n: n.x * n.y > 0, OX)   # a knotted weave
    parts.append(ball)
    J = X.joints(p)
    return parts, J


parts, J = build()
if EXPORT:
    result = L.export_enemy("EN_NetThrower", parts, J, 2.001, RULES, scale=0.85)
else:
    result = A.review("net_thrower", parts, original_blend="en_netthrower", original_root="EN_NetThrower")
