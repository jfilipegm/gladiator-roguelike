"""A1 roster — the Ashen Oracle in the simple style (pattern: a1_nobody.py).

Identity kept from ch_ashenoracle.py: a long ash-grey robe flaring to the ground with a charcoal hem, a rope belt,
the charcoal stole down the front with purple ends, the ash hood with its point leaning back, the pale bone mask
with dark eye holes and glowing purple cracks in a dark opening, the cyan rune on the back of the hood (the
player's mark from behind), thin arms in wide ash sleeves with charcoal cuffs, bone-pale hands, bare dark feet under
the hem, and the crooked censer staff (a hook bending forward over his head, a chain, a bronze censer with a purple
ember).
"""
import importlib
import os
import sys

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import a1_glad_lib as G
importlib.reload(G)

OX = -0.6


def build():
    K.clear_scene()
    h = A.humanoid(OX, p=dict(TW=0.20, SX=0.25, AR=0.064, FL=0.30), skin="skin_pale", top="ash", top_shade=None,
                   belt=None, foot="skin_dark", sole="skin_dark", hair_style="none", face=False,
                   skip=("Head", "Arm", "Leg", "Strap"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, HW, AR = p["H"], p["S"], p["N"], p["TW"], p["HW"], p["AR"]
    hz = a["hz"]
    ry = a["head_back_y"]
    wz = a["waist_z"]

    # the robe: from the waist, flaring to the ground, a charcoal hem; the rope belt
    robe = A.loft("Robe", [(0.035, A.oct8(TW * 1.46, TW * 1.0, 0.62, front=0.86)),
                           (0.11, A.oct8(TW * 1.42, TW * 0.98, 0.62, front=0.86)),
                           (H - 0.04, A.oct8(TW * 1.12, TW * 0.80)), (wz, A.oct8(TW * 0.92, TW * 0.68))],
                  "ash", OX, cap_top=False)
    A.paint(robe, "charcoal", lambda c, n: c.z < 0.11, OX)
    parts.append(robe)
    parts.append(A.loft("Belt", [(wz - 0.025, A.oct8(TW * 0.95, TW * 0.7)), (wz + 0.025, A.oct8(TW * 0.95, TW * 0.7))],
                        "rope", OX, False, False))
    # the stole: two charcoal strips down the front, purple ends
    for s, x in (("L", 0.065), ("R", -0.065)):
        parts.append(G.panel("Stole" + s, [(-0.03, 0.0), (0.03, 0.0), (0.03, -0.60), (-0.03, -0.60)], 0.014,
                             (x, -TW * 0.66 - 0.01, Sz + 0.02), "charcoal", OX, tilt=-3.8))
        parts.append(G.panel("StoleEnd" + s, [(-0.034, 0.0), (0.034, 0.0), (0.034, -0.09), (0.0, -0.12),
                                              (-0.034, -0.09)], 0.016, (x, -0.175, Sz - 0.58), "purple", OX, tilt=-3.8))
    # wide ash sleeves over thin arms, charcoal cuffs; the pale hands come out of them (the kit's fists)
    wr = Sz - p["AL"]
    for s, sx in (("L", 1), ("R", -1)):
        x = (p["SX"] + 0.02) * sx
        sl = A.loft("Sleeve" + s, [(wr + 0.01, A.oct8(AR * 1.55, AR * 1.5, 0.66, x + 0.012 * sx, -0.02)),
                                   (wr + 0.06, A.oct8(AR * 1.45, AR * 1.4, 0.66, x + 0.01 * sx, -0.018)),
                                   (Sz - p["AL"] * 0.5, A.oct8(AR * 1.12, AR * 1.1, 0.66, x, -0.008)),
                                   (Sz + 0.04, A.oct8(AR * 1.0, AR * 1.0, 0.66, x - 0.03 * sx))], "ash", OX,
                    cap_bottom=False)
        A.paint(sl, "charcoal", lambda c, n: c.z < wr + 0.06, OX)
        parts.append(sl)
    # the hood's mantle over the shoulders
    parts.append(A.loft("Mantle", [(Sz - 0.05, A.oct8(TW * 1.12, TW * 0.82, 0.64)),
                                   (Sz + 0.04, A.oct8(TW * 0.98, TW * 0.74, 0.62)),
                                   (N + 0.02, A.oct8(HW * 0.82, ry * 0.78, 0.6))], "ash", OX, False, False))

    # the hood: ash, its point leaning back; a dark opening; the bone mask with eye holes and purple cracks
    hood = [(hz(-0.02), A.oct8(HW * 0.92, ry * 0.88, 0.64)), (hz(0.3), A.oct8(HW * 1.1, ry * 1.08, 0.62)),
            (hz(0.76), A.oct8(HW * 1.08, ry * 1.06, 0.62)), (hz(1.0), A.oct8(HW * 0.8, ry * 0.8, 0.6, 0, 0.03)),
            (hz(1.1), A.oct8(HW * 0.42, ry * 0.42, 0.6, 0, 0.13)), (hz(1.17), A.oct8(0.01, 0.01, 0.6, 0, 0.27))]
    parts.append(A.loft("Hood", hood, "ash", OX))
    fy = -ry * 1.08
    opening = [(-0.165, -0.21), (0.165, -0.21), (0.165, 0.09), (0.10, 0.19), (-0.10, 0.19), (-0.165, 0.09)]
    parts.append(G.panel("Opening", opening, 0.01, (0, fy - 0.002, hz(0.42)), "ink", OX))
    mask = [(-0.10, -0.19), (0.10, -0.19), (0.135, -0.05), (0.135, 0.08), (0.08, 0.155), (-0.08, 0.155),
            (-0.135, 0.08), (-0.135, -0.05)]
    parts.append(G.panel("Mask", mask, 0.018, (0, fy - 0.012, hz(0.42)), "bone", OX))
    mz = fy - 0.022
    for s, x in (("L", 0.062), ("R", -0.062)):
        parts.append(A.box("EyeHole" + s, (0.06, 0.012, 0.042), (x, mz, hz(0.52)), "ink", OX))
    for k, (x, z, r) in enumerate(((0.035, hz(0.24), 0.35), (-0.07, hz(0.34), -0.6), (0.09, hz(0.62), 1.0),
                                   (-0.03, hz(0.66), -0.25))):
        parts.append(A.box("Crack%d" % k, (0.016, 0.012, 0.085), (x, mz, z), "purple_l", OX, rot=(0, r, 0)))
    # the cyan rune on the back of the hood
    rune = [(0.0, 0.10), (0.06, 0.02), (0.02, 0.0), (0.02, -0.10), (-0.02, -0.10), (-0.02, 0.0), (-0.06, 0.02)]
    parts.append(G.panel("Rune", rune, 0.012, (0, ry * 1.07 + 0.004, hz(0.55)), "player_cyan", OX))

    # the crooked censer staff in the right fist: a hook bending forward over his head, the censer hanging from it
    hx, hy, hzz = a["hand_r"]
    parts.append(G.tube("Pole", [(hx, hy, 0.06), (hx + 0.02, hy, 0.62), (hx - 0.012, hy + 0.01, 1.25),
                                 (hx, hy, 1.78), (hx, hy - 0.07, 1.93), (hx, hy - 0.19, 1.96), (hx, hy - 0.27, 1.89)],
                        [0.026, 0.026, 0.024, 0.024, 0.022, 0.02, 0.018], "wood_dark", OX, sides=6))
    cy = hy - 0.27
    parts.append(A.box("Chain", (0.014, 0.014, 0.17), (hx, cy, 1.89), "iron", OX, pivot=(0, 0, 1)))
    cz = 1.66
    parts.append(A.loft("Censer", [(cz - 0.07, A.ngon(6, 0.045, hx, cy)), (cz - 0.03, A.ngon(6, 0.08, hx, cy)),
                                   (cz + 0.05, A.ngon(6, 0.08, hx, cy)), (cz + 0.07, A.ngon(6, 0.06, hx, cy))],
                        "bronze_d", OX))
    parts.append(A.loft("Ember", [(cz + 0.07, A.ngon(6, 0.055, hx, cy)), (cz + 0.10, A.ngon(6, 0.03, hx, cy))],
                        "purple_l", OX))
    return parts


parts = build()
result = A.review("ashen_oracle", parts, original_blend="ch_ashenoracle", original_root="CH_AshenOracle", height=2.0,
                  gap=1.2)
