"""A1 roster — the Brute (EN_Brute, Tier-1 elite-class slammer) in the simple style. ~1.95 m (built at the shared
proportions, then scaled x1.4).

Identity kept from en_brute.py: twice the Convict's mass, a faceted belly, huge shoulders under spiked iron
pauldrons, a small shaved head sunk between them with an ink topknot, yellow eyes under a heavy dark brow and a dark
jaw, long arms ending in huge iron-banded fists, rusty shackle cuffs with broken chains, a leather chest strap, the
magenta loincloth (the enemy accent), thick legs with sack-cloth wraps, bare dark feet. Changed: the variant-A body
(lean) with brute proportions (wide torso, thick limbs, small head: ~33 % of the height instead of 37 %).
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

GAP = 1.5
OX = -GAP / 2
SCALE = 1.4


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.40, SX=0.47, AR=0.12, AL=0.44, LR=0.13, LX=0.16, HS=0.13, S=0.90, N=0.955, HW=0.21,
                          HH=0.42, FW=0.095, FL=0.33), skin="skin_tan", skin_shade="skin_dark", top=None,
               belt="leather_d", foot="skin_dark", hair_style="none", head_top="round")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    TW, H, S, AR, HS, LR = p["TW"], p["H"], p["S"], p["AR"], p["HS"], p["LR"]
    hz, waist, chest = a["hz"], a["waist_z"], a["chest_z"]
    o8, hx6 = L.oct8, L.hex6
    parts += L.face(a, OX, eyes="hazard_yellow", brows="skin_dark", nose="skin_dark", brow_angle=0.22,
                    brow_w=0.62, brow_h=0.1, brow_ledge=0.03, eye_w=0.26, eye_h=0.08)
    # the faceted belly, pushed out in front of the torso
    parts.append(L.loft("Belly", [(H - 0.01, o8(TW * 0.55, TW * 0.26, 0.62, 0, -TW * 0.40)),
                                  (H + 0.07, o8(TW * 0.84, TW * 0.42, 0.62, 0, -TW * 0.43)),
                                  (waist + 0.06, o8(TW * 0.86, TW * 0.42, 0.62, 0, -TW * 0.42)),
                                  (chest - 0.04, o8(TW * 0.58, TW * 0.26, 0.62, 0, -TW * 0.36))], "skin_mid", OX))
    # the magenta loincloth under the belt, the chest strap over the left shoulder
    parts.append(L.loft("Loincloth", [(H - 0.12, o8(TW * 1.1, TW * 0.8)), (waist - 0.02, o8(TW * 0.92, TW * 0.68))],
                        "accent_magenta", OX, False, False))
    parts.append(A.box("Strap", (0.11, TW * 1.5, 0.66), (0.0, 0.0, chest - 0.02), "leather_d", OX, pivot=(0, 0, 0),
                       rot=(0, 0.62, 0)))
    # the topknot
    parts.append(L.loft("Topknot", [(hz(0.86), hx6(0.05, 0.05, 0, 0.09)), (hz(1.14), hx6(0.03, 0.03, 0, 0.17))], "ink",
                        OX, cap_bottom=False))
    # spiked iron pauldrons, tilted outward
    for s, sg in (("L", 1), ("R", -1)):
        cx = (p["SX"] + 0.02) * sg
        pd = L.loft("Pauldron" + s, [(S - 0.07, o8(AR * 1.85, AR * 1.75, 0.62, cx, 0.0)),
                                     (S + 0.06, o8(AR * 1.95, AR * 1.85, 0.62, cx, 0.0)),
                                     (S + 0.15, o8(AR * 1.15, AR * 1.15, 0.62, cx, 0.0))], "iron_d", OX,
                    cap_bottom=False)
        spikes = [A.cone("Spike%s%d" % (s, k), 0.045, 0.2, (cx + 0.05 * sg, dy, S + 0.12), "steel_l", OX, verts=4,
                         rot=(0, 0.45 * sg, 0)) for k, dy in enumerate((-0.08, 0.08))]
        L.xform([pd] + spikes, L.Matrix.Rotation(0.35 * sg, 4, "Y"), (cx, 0, S), OX)
        parts += [pd] + spikes
    # the arms: iron bands round the huge fists, rusty cuffs with broken chains; swung a little out
    wz = a["wz"]
    for s, sg in (("L", 1), ("R", -1)):
        x = a["arm_x"] * sg
        band = L.ring("Band" + s, wz - HS * 0.95, wz - HS * 0.45, o8(HS * 0.98, HS * 1.07, 0.65, x, -0.02), "iron", OX)
        cuff = L.ring("Cuff" + s, wz + 0.0, wz + 0.08, hx6(AR * 1.02, AR * 1.02, x, -0.015), "rusty_iron", OX)
        links = [A.box("Link%s%d" % (s, k), (0.04, 0.08, 0.1), (x + (AR + 0.01) * sg, -0.015, wz - 0.02 - k * 0.09),
                       "iron", OX, pivot=(0, 0, 1), rot=(0, 0, 0.3 * k)) for k in range(2)]
        grp = L.by_name(parts, "Arm" + s, "Fist" + s) + [band, cuff] + links
        L.xform(grp, L.Matrix.Rotation(-0.12 * sg, 4, "Y"), (x, 0, S), OX)
        parts += [band, cuff] + links
    # sack-cloth wraps round the shins
    for s, sg in (("L", 1), ("R", -1)):
        parts.append(L.ring("Wrap" + s, 0.17, 0.30, hx6(LR * 0.93, LR * 0.99, p["LX"] * sg, 0.004), "cloth_sack", OX))
    L.xform(parts, L.Matrix.Scale(SCALE, 4), (0, 0, 0), OX)
    return parts


parts = build()
result = A.review("brute", parts, original_blend="en_brute", original_root="EN_Brute", height=2.1, gap=GAP)
