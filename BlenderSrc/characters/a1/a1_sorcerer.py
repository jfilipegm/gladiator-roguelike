"""A1 roster — the Sorcerer in the simple style (tier 3: the Grand Arena's spell-caster).

Identity kept from en_sorcerer.py: a thin seer in a deep purple robe flaring to a gold hem, the gold stole down the
front, the magenta belt (the enemy accent), wide sleeves with gold cuffs and pale hands, the TALL pointed purple hood
(nothing else in the horde has a spike for a head) over a face in shadow with two orange-red eyes, the straight staff
with a gold claw holding a glowing orange-red orb (it opens the rune under you: harmful = orange-red).
"""
import importlib
import os
import sys

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier3boss_lib as L
importlib.reload(L)

OX = -0.55


def build():
    K.clear_scene()
    h = A.humanoid(0.0, p=dict(TW=0.175, AR=0.062), skin="skin_pale", skin_shade="skin_light", top="purple_d",
                   top_shade=None, belt="accent_magenta", foot="leather_d", sole="ink", hair_style="none",
                   face=False, skip=("Leg", "Strap", "Buckle"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, TW = p["H"], p["S"], p["TW"]
    hz, HW, ry = a["hz"], a["head_w"], a["head_back_y"]
    K.set_swatch(L.get(parts, "Head"), "ink")                     # the face in the hood's shadow
    for s in ("L", "R"):
        K.set_swatch(L.get(parts, "Arm" + s), "purple_d")         # sleeves
    # the robe: from the waist, flaring to the ankles, a gold hem
    w = a["waist_z"]
    robe = L.ring_rows("Robe", [(0.05, TW * 1.75, TW * 1.35), (0.12, TW * 1.7, TW * 1.3), (H, TW * 1.2, TW * 0.9),
                                (w + 0.02, TW * 0.9, TW * 0.66)], "purple_d", cap_top=False)
    A.paint(robe, "gold_d", lambda c, n: c.z < 0.12)
    parts.append(robe)
    parts.append(L.band("Sash", w - 0.03, w + 0.045, TW * 1.0, TW * 0.74, "accent_magenta"))
    # the gold stole down the front, from the neck to the hem
    parts.append(L.rod("Stole", (0, -TW * 0.66 - 0.012, a["neck_z"] - 0.03), (0, -TW * 1.33, 0.12), 0.042, "gold"))
    # wide sleeve ends with gold cuffs
    wz = Sz - p["AL"]
    for s, sx in (("L", 1), ("R", -1)):
        x = (p["SX"] + 0.02) * sx
        parts.append(L.ring_rows("Cuff" + s, [(wz - 0.01, p["AR"] * 1.75, p["AR"] * 1.7, x, -0.015),
                                              (wz + 0.05, p["AR"] * 1.5, p["AR"] * 1.45, x, -0.015),
                                              (wz + 0.15, p["AR"] * 1.0, p["AR"] * 0.98, x, -0.012)],
                                 "purple_d", cap_bottom=False, cap_top=False))
        A.paint(L.get(parts, "Cuff" + s), "gold_d", lambda c, n: c.z < wz + 0.05)
    # the hood: open at the front (its front pulled back so the dark head shows through), a brow over the eyes,
    # then the tall point leaning back
    top = hz(1.0)
    hood = A.loft("Hood", [(a["neck_z"] - 0.05, A.oct8(HW * 0.95, ry * 0.9, 0.6, 0, 0.02, front=0.55)),
                           (hz(0.3), A.oct8(HW * 1.13, ry * 1.12, 0.6, front=0.52)),
                           (hz(0.64), A.oct8(HW * 1.13, ry * 1.12, 0.6, front=0.52)),
                           (hz(0.8), A.oct8(HW * 1.1, ry * 1.12, 0.6)),
                           (top + 0.04, A.oct8(HW * 0.64, ry * 0.64, 0.6, 0, 0.04)),
                           (top + 0.3, A.oct8(HW * 0.3, ry * 0.3, 0.6, 0, 0.12)),
                           (top + 0.62, A.oct8(0.02, 0.02, 0.6, 0, 0.24))], "purple", cap_bottom=False)
    parts.append(hood)
    for s, x in (("L", HW * 0.36), ("R", -HW * 0.36)):
        parts.append(A.box("Eye" + s, (HW * 0.26, 0.02, p["HH"] * 0.07), (x, -ry - 0.004, a["eye_z"]),
                           "glow_hazard_orange", rot=(0, 0.25 if x > 0 else -0.25, 0)))
    # the staff in the right fist: a gold claw holding the glowing orb
    hx, hy, _ = a["hand_r"]
    x, y = hx, hy - 0.01
    parts.append(L.rod("Staff", (x, y, 0.05), (x, y, 1.72), 0.026, "wood_dark"))
    parts.append(L.ring_rows("Claw", [(1.68, 0.035, 0.035, x, y), (1.76, 0.085, 0.085, x, y),
                                      (1.8, 0.09, 0.09, x, y)], "gold_d", cap_top=False))
    orb = A.loft("Orb", [(1.74, A.ngon(6, 0.035, x, y)), (1.8, A.ngon(6, 0.11, x, y)), (1.92, A.ngon(6, 0.11, x, y)),
                         (1.98, A.ngon(6, 0.035, x, y))], "glow_hazard_orange")
    parts.append(orb)
    return parts


parts = build()
L.finish(parts, 1.0, OX)
result = A.review("sorcerer", parts, original_blend="en_sorcerer", original_root="EN_Sorcerer", height=2.2)
