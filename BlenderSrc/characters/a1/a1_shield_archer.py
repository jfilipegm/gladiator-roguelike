"""A1 roster — the Shield Archer (EN_ShieldArcher, Tier-2 ranged blocker) in the simple style.

Identity kept from en_shieldarcher.py: a blue-tunic archer in an iron cap with a dark brim, behind a tall straw
wicker pavise with wooden bands standing from the ground to his chin (from the front the shield IS the silhouette:
"flank this one") with the magenta boss (the enemy accent); the bow carried flat in the right hand along the walk
direction, a glowing orange-red arrowhead nocked. Changed: the variant-A body (lean); the pavise has a pointed top
(it tells it from the Shield Thug's square door at a glance); the bow is one bent plate with a string and an arrow.
export=1: the game model Assets/Art/Models/A1/Enemies/EN_ShieldArcher.fbx (docs/tracks/A1-ingame-contract.md).
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
RULES = [("Cap", "Head"), ("Brim", "Head"), ("Pavise", "ArmL"), ("Band", "ArmL"), ("Boss", "ArmL"), ("Bow", "ArmR"),
         ("String", "ArmR"), ("Arrow", "ArmR")]


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.19, AR=0.06, LR=0.08, SX=0.25, HS=0.068), skin="skin_light", skin_shade="skin_mid",
               top="cloth_blue", belt="leather_d", foot="leather_d", legs="trousers", leg_colour="leather_l",
               hair_style="none", head_top="flat")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    HW, ry, hz, S = a["head_w"], a["head_back_y"], a["hz"], p["S"]
    o8 = L.oct8
    L.paint(L.by_name(parts, "Head")[0], "mud_dark", lambda c, n: c.z > hz(0.15) and n.y > 0.35, OX)  # hair at the back
    parts += L.face(a, OX, eyes="ink", brows="mud_dark", nose="skin_mid")
    # the iron cap and its dark brim
    parts.append(L.loft("Cap", [(hz(0.66), o8(HW * 1.07, ry * 1.07, 0.6)), (hz(1.08), o8(HW * 0.72, ry * 0.72, 0.6))],
                        "iron", OX, cap_bottom=False))
    parts.append(L.loft("Brim", [(hz(0.62), o8(HW * 1.18, ry * 1.18, 0.6)), (hz(0.69), o8(HW * 1.18, ry * 1.18, 0.6))],
                        "iron_d", OX, cap_top=False))
    # the pavise standing in front on the left arm: straw wicker, wooden bands, a pointed top, the magenta boss
    L.xform(L.by_name(parts, "ArmL", "FistL"), L.rot_x(-0.45), (a["arm_x"], 0, S), OX)
    cx, cy, w = 0.04, -0.3, 0.6
    pv = K.extrude_poly("Pavise", [(-w / 2, 0.04), (w / 2, 0.04), (w / 2, 0.86), (0.0, 1.0), (-w / 2, 0.86)],
                        thickness=0.06, plane="XZ", swatch="straw", location=(cx + OX, cy, 0.0))
    parts.append(L.flat(pv))
    fy = cy - 0.03 - 0.003
    for k, z in enumerate((0.2, 0.46, 0.74)):
        parts.append(L.decal("Band%d" % k, w - 0.02, 0.05, (cx, fy, z), "wood", OX))
    parts.append(L.spike("Boss", 0.085, 0.06, (cx, cy - 0.03, 0.6), "accent_magenta", OX, n=4,
                         rot=(math.pi / 2, 0, 0)))
    # the bow carried flat in the right fist (limbs along Y), string below, the glowing arrow nocked
    hx, hy, hzz = a["hand_r"]
    bow = K.extrude_poly("Bow", [(-0.42, -0.1), (0.0, 0.06), (0.42, -0.1), (0.4, -0.13), (0.0, 0.025), (-0.4, -0.13)],
                         thickness=0.03, plane="YZ", swatch="wood_dark", location=(hx + OX, hy, hzz))
    parts.append(L.flat(bow))
    sq = L.ngon(4, 0.008, 0.0, 0.0, math.pi / 4)
    st = L.loft_y("String", [(-0.41, sq), (0.41, sq)], "bone", 0.0, False, False)
    st.location = (hx + OX, hy, hzz - 0.115)
    arrow = L.loft_y("Arrow", [(-0.36, L.ngon(4, 0.01, 0, 0, math.pi / 4)), (0.3, L.ngon(4, 0.01, 0, 0, math.pi / 4))],
                     "wood_light", 0.0, False, False)
    arrow.location = (hx + OX, hy, hzz - 0.09)
    head = L.spike("Arrowhead", 0.035, 0.11, (hx, hy - 0.36, hzz - 0.09), "glow_hazard_red", OX, n=4,
                   rot=(math.pi / 2, 0, 0))
    parts += [st, arrow, head]
    L.xform(L.by_name(parts, "ArmR", "FistR", "Bow", "String", "Arrow"), L.rot_x(-0.15), (-a["arm_x"], 0, S), OX)
    J = X.joints(p)
    return parts, J


parts, J = build()
if EXPORT:
    result = L.export_enemy("EN_ShieldArcher", parts, J, 1.23, RULES)
else:
    result = A.review("shield_archer", parts, original_blend="en_shieldarcher", original_root="EN_ShieldArcher")
