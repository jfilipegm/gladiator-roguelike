"""A1 roster — the Centurion (EN_Centurion, the Imperial Arena's formation leader, elite) in the simple style.

Identity kept from en_centurion.py: a silver cuirass with a grid of gold medals (phalerae), a purple skirt, the
magenta belt (the enemy accent), a long crimson cloak down his back, a steel helm with silver cheek guards under the
broad red crest that runs SIDE TO SIDE (his silhouette: a wide fan over the head), the vine staff (wood, gold cap)
raised up and out in the right hand, the left fist out at the hip in a dark glove, gilded greaves, leather boots, no
shield. Changed: the variant-A body (lean, x1.1 for an officer's presence); the cloak is a curved back plate.
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
import stylized_kit as S
importlib.reload(S)
import a1_tier12_lib as L
importlib.reload(L)

GAP = 1.3
OX = -GAP / 2
SCALE = 1.1


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.23, AR=0.075, LR=0.09, LX=0.115, SX=0.29, HS=0.074, HW=0.27), skin="skin_light",
               skin_shade="skin_mid", top="steel_l", top_shade="purple_d", belt="accent_magenta", foot="leather_d",
               hair_style="none", head_top="flat")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    HW, ry, hz, TW, H, Sz = a["head_w"], a["head_back_y"], a["hz"], p["TW"], p["H"], p["S"]
    waist, chest = a["waist_z"], a["chest_z"]
    o8 = L.oct8
    torso = L.by_name(parts, "Torso")[0]
    L.paint(torso, "purple", lambda c, n: a["hem_z"] + 0.06 < c.z < waist - 0.02, OX)   # the skirt
    L.paint(torso, "silver_d", lambda c, n: waist - 0.02 < c.z < chest - 0.02 and abs(n.z) < 0.5, OX)  # scale shade
    for f in L.by_name(parts, "Fist"):
        L.paint(f, "leather_d", lambda c, n: True, OX)
    parts += L.face(a, OX, eyes="ink", brows="mud_dark", nose="skin_mid", brow_angle=0.18)
    # the phalerae: a 3 x 2 grid of gold medals on the chest
    fy = -TW * 0.66 - 0.006
    for i, x in enumerate((-0.07, 0.0, 0.07)):
        for j, z in enumerate((chest + 0.05, chest - 0.03)):
            parts.append(L.decal("Medal%d%d" % (i, j), 0.045, 0.045, (x, fy, z), "gold", OX, rot_y=0.785))
    # the helm, silver cheek guards, the transverse red crest
    parts.append(L.loft("Helm", [(hz(0.56), o8(HW * 1.08, ry * 1.08, 0.6)), (hz(0.86), o8(HW * 1.0, ry * 1.0, 0.6)),
                                 (hz(1.08), o8(HW * 0.56, ry * 0.56, 0.6))], "steel", OX, cap_bottom=False))
    for s, sg in (("L", 1), ("R", -1)):
        parts.append(A.box("Cheek" + s, (0.035, ry * 1.0, p["HH"] * 0.48), (HW * 1.04 * sg, -ry * 0.25, hz(0.6)),
                           "steel_l", OX, pivot=(0, 0, 1), taper=(1.0, 1.0)))
    # (a narrow foot on the helm, fanning out wider than the shoulders)
    fan = [(-0.12, 0.0), (-0.36, 0.08), (-0.44, 0.2), (-0.36, 0.3), (0.0, 0.35), (0.36, 0.3), (0.44, 0.2),
           (0.36, 0.08), (0.12, 0.0)]
    crest = K.extrude_poly("Crest", fan, thickness=0.09, plane="XZ", swatch="red_banner", location=(OX, 0.02, hz(1.0)))
    parts.append(L.flat(crest))
    # the crimson cloak: a curved plate down his back from the shoulders to the knees
    cl = S.shell("Cloak", radius=(TW * 1.32, TW * 0.98), arc=(15, 165), z=(0.2, Sz + 0.03), taper=0.86,
                 thickness=0.025, segments=4, location=(OX, 0.03, 0.0), swatch="crimson", smooth_angle=0, flare=0.04)
    parts.append(L.flat(cl))
    # gilded greaves
    for s, sg in (("L", 1), ("R", -1)):
        parts.append(L.ring("Greave" + s, 0.07, H * 0.72, L.hex6(p["LR"] * 0.88, p["LR"] * 0.95, p["LX"] * sg, 0.0),
                            "gold_d", OX))
    # the left fist out at the hip
    xl = a["arm_x"]
    L.xform(L.by_name(parts, "ArmL", "FistL"), L.Matrix.Rotation(-0.32, 4, "Y"), (xl, 0, Sz), OX)
    # the vine staff raised up, forward and out in the right fist
    xr = -xl
    rot = L.rot_x(-0.55) @ L.Matrix.Rotation(0.2, 4, "Y")
    L.xform(L.by_name(parts, "ArmR", "FistR"), rot, (xr, 0, Sz), OX)
    hand = Vector((xr, 0.0, Sz)) + rot.to_3x3() @ (Vector(a["hand_r"]) - Vector((xr, 0.0, Sz)))
    sq = L.ngon(4, 0.022, 0.0, 0.0, math.pi / 4)
    staff = L.loft("Vitis", [(-0.2, sq), (0.95, sq)], "wood_light", 0.0, False, False)
    knot = L.octa("Knot", 0.04, (0.0, 0.0, 0.35), "wood", 0.0)
    cap = L.octa("StaffCap", 0.05, (0.0, 0.0, 0.97), "gold_l", 0.0)
    for o in (staff, knot, cap):
        o.location = (hand.x + OX, hand.y, hand.z)
        o.rotation_euler = (0.35, -0.3, 0)   # leaning up, forward and out
    parts += [staff, knot, cap]
    L.xform(parts, L.Matrix.Scale(SCALE, 4), (0, 0, 0), OX)
    return parts


parts = build()
result = A.review("centurion", parts, original_blend="en_centurion", original_root="EN_Centurion", height=2.0, gap=GAP)
