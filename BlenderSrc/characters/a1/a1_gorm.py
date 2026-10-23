"""A1 roster — Gorm, the Pit Master, in the simple style (Arena 1's boss, docs/BOSSES.md; built at ~3.3 m).

Identity kept from en_gorm.py: a huge, hunched slaver, twice the Nobody: bald, a black beard, a gold earring, angry
yellow eyes under heavy brows, an iron slaver's collar, a bare torso with a big gut, a studded charcoal leather apron,
the magenta sash (the enemy accent), a ring of gold keys at the right hip, a dark red cape down his back, dark red
trousers with sack-cloth wraps, leather boots and bracers; the chain flail in the right hand (a wooden grip, iron
links, a spiked rusty ball resting by his foot: the silhouette nothing else in the pit has) and the coiled bullwhip
in the left. Built at the kit's chibi size with a wide, long-armed body, hunched, then scaled x2.15 about the feet.
"""
import importlib
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier3boss_lib as L
importlib.reload(L)

SCALE = 2.15
GAP = 2.6
OX = -GAP / 2


def build():
    K.clear_scene()
    h = A.humanoid(0.0, p=dict(TW=0.27, SX=0.33, AR=0.098, LR=0.11, LX=0.13, HS=0.088, AL=0.39, HW=0.27),
                   skin="skin_mid", skin_shade="skin_tan", top=None, belt=None, foot="leather_d", sole="ink",
                   hair="ink", brows="ink", eyes="hazard_yellow", hair_style="none", legs="trousers",
                   leg_colour="red_banner_d")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, SX = p["H"], p["S"], p["N"], p["TW"], p["SX"]
    hz, HW = a["hz"], a["head_w"]
    ry = HW * 0.93
    w, ch = a["waist_z"], a["chest_z"]
    # the gut, the sash under it, the studded apron hanging from the sash
    parts.append(L.ring_rows("Gut", [(H + 0.0, TW * 0.92, TW * 0.72, 0, -0.03), (w, TW * 1.1, TW * 0.98, 0, -0.08),
                                     (ch - 0.02, TW * 1.04, TW * 0.82, 0, -0.05), (ch + 0.07, TW * 0.9, TW * 0.6, 0, 0.0)],
                             "skin_mid"))
    parts.append(L.band("Sash", H - 0.02, H + 0.06, TW * 1.02, TW * 0.82, "accent_magenta", cy=-0.03))
    apron = L.cape("Apron", H + 0.04, 0.14, TW * 1.6, TW * 1.9, TW * 0.8 + 0.035, "charcoal", flare=0.03,
                   thickness=0.025)
    L.mirror_y(apron)
    parts.append(apron)
    for k in range(3):
        parts.append(A.box("Stud%d" % k, (0.035, 0.02, 0.035), (-0.09 + k * 0.09, -TW * 0.8 - 0.07, H - 0.02), "iron"))
    # the ring of gold keys at the right hip
    kx = -TW * 1.02
    parts.append(L.disc("KeyRing", (kx - 0.01, -0.06, H + 0.01), 0.045, 0.014, "gold_d"))
    for k in range(3):
        parts.append(A.box("Key%d" % k, (0.018, 0.016, 0.08), (kx - 0.02, -0.1 + k * 0.035, H - 0.01), "gold",
                           pivot=(0, 0, 1), rot=(0, 0, 0.3 * k)))
    # the cape down his back
    parts.append(L.cape("Cape", Sz + 0.02, 0.18, TW * 2.2, TW * 2.6, a["back_y"] + 0.02, "red_banner_d",
                        inner="crimson_d", flare=0.08))
    # the iron collar, the beard, the earring
    parts.append(L.ring_rows("Collar", [(N - 0.06, 0.15, 0.13), (N + 0.01, 0.14, 0.12)], "iron_d"))
    parts.append(L.ring_rows("Beard", [(hz(-0.22), HW * 0.26, ry * 0.2, 0, -ry * 0.72),
                                       (hz(0.04), HW * 0.68, ry * 0.5, 0, -ry * 0.52),
                                       (hz(0.21), HW * 0.86, ry * 0.7, 0, -ry * 0.36)], "ink", cap_top=False))
    parts.append(A.box("Earring", (0.02, 0.03, 0.05), (-HW * 1.0 - 0.01, 0.0, hz(0.3)), "gold_l"))
    # leather bracers; sack-cloth wraps on the shins
    wz = Sz - p["AL"]
    for s, sx in (("L", 1), ("R", -1)):
        x = (SX + 0.02) * sx
        parts.append(L.ring_rows("Bracer" + s, [(wz + 0.03, p["AR"] * 0.95, p["AR"] * 0.93, x, -0.015),
                                                (wz + 0.15, p["AR"] * 1.05, p["AR"] * 1.03, x, -0.012)],
                                 "leather_d", cap_bottom=False, cap_top=False))
        parts.append(L.ring_rows("Wrap" + s, [(0.14, p["LR"] * 0.8, p["LR"] * 0.85, p["LX"] * sx, 0.0),
                                              (0.21, p["LR"] * 0.88, p["LR"] * 0.93, p["LX"] * sx, -0.003)],
                                 "cloth_sack", cap_bottom=False, cap_top=False))
    # the flail: a wooden grip out of the right fist, iron links down to the spiked ball resting by his right foot
    # the flail arm is held forward and a little out, so the chain hangs free below the fist
    sr = (-(SX + 0.02), 0.0, Sz)
    arm_r = [L.get(parts, n) for n in ("ArmR", "FistR", "BracerR")]
    hand = Vector(a["hand_r"])
    m = (Matrix.Translation(sr) @ Matrix.Rotation(0.22, 4, "Y") @ Matrix.Rotation(-0.95, 4, "X") @
         Matrix.Translation(-Vector(sr)))
    L.xform(arm_r, m)
    hx, hy, hzz = m @ hand
    grip_end = (hx - 0.01, hy - 0.02, hzz - 0.12)
    flail = [L.rod("FlailGrip", (hx, hy + 0.01, hzz + 0.06), grip_end, 0.026, "wood_dark")]
    ball_c = (hx - 0.04, hy - 0.03, 0.16)
    n = 5
    for k in range(n):
        p0 = [grip_end[i] + (ball_c[i] - grip_end[i]) * (k / n) for i in range(3)]
        p1 = [grip_end[i] + (ball_c[i] - grip_end[i]) * ((k + 0.85) / n) for i in range(3)]
        flail.append(L.rod("Link%d" % k, p0, p1, 0.026 if k % 2 == 0 else 0.018, "iron" if k % 2 == 0 else "iron_d"))
    flail += L.spiked_ball("Ball", ball_c, 0.15, "rusty_iron", "steel_l", spike=0.1)
    # the bullwhip coiled in the left fist
    lx, ly, lzz = a["hand_l"]
    whip = [L.rod("WhipGrip", (lx, ly, lzz + 0.05), (lx + 0.01, ly - 0.02, lzz - 0.16), 0.02, "leather_d")]
    for k in range(3):
        whip.append(L.hoop("Coil%d" % k, (lx + 0.03 + (k - 1) * 0.022, ly - 0.06 - k * 0.012, lzz - 0.2 - k * 0.025),
                           0.12 - k * 0.012, 0.017, "leather" if k % 2 == 0 else "leather_l", axis="Y", rot_z=0.5))
    parts += flail + whip
    # the hunch: everything above the hips leans forward; the arms and what they hold move as one piece
    rigid = {o: Sz for o in parts if o.name.startswith(("Arm", "Fist", "Bracer")) or o in flail or o in whip}
    upper = [o for o in parts if not o.name.startswith(("Leg", "Foot", "Wrap", "Apron", "Stud", "Key"))]
    L.shear_forward(upper, H + 0.06, 0.26, rigid)
    return parts


parts = build()
L.finish(parts, SCALE, OX)
result = A.review("gorm", parts, original_blend="en_gorm", original_root="EN_Gorm", height=3.6, gap=GAP)
