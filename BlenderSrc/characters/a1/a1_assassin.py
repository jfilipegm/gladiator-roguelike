"""A1 roster — the Assassin (EN_Assassin, Tier-2 flanker) in the simple style.

Identity kept from en_assassin.py: lean and all charcoal (the strongest contrast on the sand), a pointed hood with an
ink tip over a dark slate face and a white eye slit, a leather sash, two steel daggers held low in reverse grip, and
the long magenta scarf streaming down the back (the silhouette tell and the enemy accent). Changed: the variant-A body
(lean); the hooded head is one chamfered loft (head and hood in one), the face a painted panel.
"""
import importlib
import os
import sys

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier12_lib as L
importlib.reload(L)

OX = -0.55


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.17, AR=0.055, LR=0.075, SX=0.23, HS=0.062), skin="charcoal", skin_shade="slate",
               top="charcoal", belt="leather_d", foot="leather_d", legs="trousers", leg_colour="charcoal",
               hair_style="none", skip=("Head",))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    HW, ry, hz, N, TW = a["head_w"], a["head_back_y"], a["hz"], a["neck_z"], p["TW"]
    o8 = L.oct8
    for f in L.by_name(parts, "Fist"):
        L.paint(f, "slate", lambda c, n: True, OX)
    # the hooded head: head and hood in one loft, a pointed ink tip; a dark slate face panel, a white eye slit
    hd = L.loft("Hood", [(hz(0.04), o8(HW * 0.5, ry * 0.48, 0.66, 0, -ry * 0.14)),
                         (hz(0.17), o8(HW * 0.88, ry * 0.86, 0.64, 0, -ry * 0.04)),
                         (hz(0.42), o8(HW * 1.04, ry * 1.04, 0.62)), (hz(0.74), o8(HW * 1.04, ry * 1.04, 0.62)),
                         (hz(0.98), o8(HW * 0.62, ry * 0.64, 0.62, 0, 0.04)),
                         (hz(1.22), o8(0.02, 0.02, 0.62, 0, 0.13))], "charcoal", OX, cap_bottom=False)
    L.paint(hd, "slate", lambda c, n: n.y < -0.85 and hz(0.1) < c.z < hz(0.74), OX)
    L.paint(hd, "ink", lambda c, n: c.z > hz(1.0), OX)
    parts.append(hd)
    parts.append(L.decal("EyeSlit", HW * 1.05, p["HH"] * 0.07, (0, -ry * 1.04 - 0.004, hz(0.52)), "white", OX))
    # the magenta scarf: a collar, and a long tail streaming down the back in two pieces
    parts.append(L.loft("Scarf", [(p["S"] - 0.03, o8(TW * 1.14, TW * 0.76)), (N + 0.01, o8(TW * 0.8, TW * 0.68))],
                        "accent_magenta", OX, False, False))
    t1 = A.box("ScarfTail1", (0.14, 0.024, 0.36), (0.05, a["back_y"] + 0.03, N - 0.02), "accent_magenta", OX,
               pivot=(0, 0, 1), rot=(0.22, 0.08, 0))
    end = t1.matrix_basis @ L.Vector((0, 0, -0.36))
    t2 = A.box("ScarfTail2", (0.12, 0.022, 0.4), (end.x - OX, end.y, end.z + 0.01), "accent_magenta", OX,
               pivot=(0, 0, 1), rot=(0.55, 0.12, 0))
    parts += [t1, t2]
    # two daggers in reverse grip, blades down and back along the forearms
    for s, sg in (("L", 1), ("R", -1)):
        hx, hy, hzz = a["hand_l"] if sg > 0 else a["hand_r"]
        d = K.extrude_poly("Dagger" + s, [(-0.022, -0.04), (0.022, -0.04), (0.028, -0.12), (0.0, -0.40),
                                          (-0.028, -0.12)], thickness=0.018, plane="YZ", swatch="steel_l",
                           location=(hx + OX, hy, hzz))
        d.rotation_euler = (0.55, 0, 0)
        parts.append(L.flat(d))
        L.xform(L.by_name(parts, "Arm" + s, "Fist" + s, "Dagger" + s), L.Matrix.Rotation(-0.1 * sg, 4, "Y"),
                (a["arm_x"] * sg, 0, p["S"]), OX)
    return parts


parts = build()
result = A.review("assassin", parts, original_blend="en_assassin", original_root="EN_Assassin")
