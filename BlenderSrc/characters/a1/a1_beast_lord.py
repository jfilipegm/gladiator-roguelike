"""A1 roster — the Beast Lord in the simple style (the Stone Amphitheater's boss, docs/BOSSES.md; built at ~3.3 m).

Identity kept from en_beastlord.py: huge and hunched, a whole lion's pelt worn as a cloak with the lion's head as his
hood (snout, black nose, fangs, ears, eye slits) and its dark mane heaped round his shoulders in tufts, the pelt and
its tail down his back; a dark-skinned face with glowing orange eyes and a black beard under the snout; bare dark arms
with iron bracers, a leather strap across the chest, a leather loincloth on the magenta belt (the enemy accent), fur
legs with leather wraps; a bone war horn and a coiled chain at the belt; the long hooked goad-spear held upright in
the LEFT fist, taller than him. Built at the kit's chibi size, hunched, then scaled x2.1 about the feet.
"""
import importlib
import math
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
import a1_tier3boss_lib as L
importlib.reload(L)

SCALE = 2.1
GAP = 2.8
OX = -GAP / 2


def build():
    K.clear_scene()
    h = A.humanoid(0.0, p=dict(TW=0.26, SX=0.32, AR=0.094, LR=0.105, LX=0.125, HS=0.085, AL=0.37, HW=0.27),
                   skin="skin_dark", skin_shade="mud_dark", top=None, belt="accent_magenta", foot="leather_d",
                   sole="ink", hair="ink", brows="ink", eyes="glow_hazard_orange", hair_style="none", legs="trousers",
                   leg_colour="mud_dark", skip=("Buckle",))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, SX = p["H"], p["S"], p["N"], p["TW"], p["SX"]
    hz, HW = a["hz"], a["head_w"]
    ry = HW * 0.93
    w, ch = a["waist_z"], a["chest_z"]
    wz = Sz - p["AL"]
    # the loincloth, the strap across the chest, iron bracers, leather wraps on the fur legs
    parts.append(L.ring_rows("Loin", [(0.2, TW * 1.12, TW * 0.86), (H + 0.02, TW * 0.98, TW * 0.72),
                                      (w - 0.03, TW * 0.9, TW * 0.66)], "leather_d", cap_bottom=False, cap_top=False))
    parts.append(L.rod("Strap", (TW * 0.8, -TW * 0.6, Sz + 0.0), (-TW * 0.85, -TW * 0.68, w + 0.02), 0.03, "leather"))
    for s, sx in (("L", 1), ("R", -1)):
        x = (SX + 0.02) * sx
        parts.append(L.ring_rows("Bracer" + s, [(wz + 0.02, p["AR"] * 0.98, p["AR"] * 0.96, x, -0.015),
                                                (wz + 0.16, p["AR"] * 1.08, p["AR"] * 1.06, x, -0.012)],
                                 "iron", cap_bottom=False, cap_top=False))
        for k, z in enumerate((0.12, 0.26)):
            parts.append(L.ring_rows("Wrap%s%d" % (s, k), [(z, p["LR"] * 0.82, p["LR"] * 0.88, p["LX"] * sx, 0.0),
                                                           (z + 0.06, p["LR"] * 0.9, p["LR"] * 0.95, p["LX"] * sx, -0.003)],
                                     "leather", cap_bottom=False, cap_top=False))
    # the bone war horn at the right hip, the coiled chain at the left
    parts.append(L.tip("Horn", (-TW * 1.0, -0.06, w - 0.02), 0.26, 0.06, "bone", sides=6, direction=(-0.25, 0.55, -0.8)))
    parts.append(L.hoop("HornMouth", (-TW * 1.0, -0.06, w - 0.02), 0.06, 0.012, "bronze", axis="Z"))
    for k in range(3):
        parts.append(L.hoop("Chain%d" % k, (TW * 1.02, -0.04 + k * 0.03, w - 0.06 - k * 0.035), 0.07 - k * 0.008, 0.012,
                            "iron" if k % 2 == 0 else "iron_d", axis="X"))
    # a black beard under the snout
    parts.append(L.ring_rows("Beard", [(hz(-0.12), HW * 0.3, ry * 0.2, 0, -ry * 0.7),
                                       (hz(0.06), HW * 0.66, ry * 0.46, 0, -ry * 0.55),
                                       (hz(0.2), HW * 0.84, ry * 0.68, 0, -ry * 0.38)], "ink", cap_top=False))
    # the lion's head as a hood: the skull over his head, the snout jutting over his brow, nose, fangs, ears, eyes
    parts.append(L.helmet(a, "ochre", None, low=0.62, top=1.12, s=1.16, name="Hood"))
    sn = L.ring_rows("Snout", [(0.0, HW * 0.66, 0.12, 0, 0.0), (0.22, HW * 0.6, 0.11, 0, -0.01),
                               (0.29, HW * 0.44, 0.08, 0, -0.025)], "straw")
    L.horizontal(sn)
    root = (0, -ry * 0.7, hz(0.84))
    tilt = Matrix.Translation(root) @ Matrix.Rotation(-0.38, 4, "X")   # nose down: its top shows from the front
    sn.data.transform(tilt)
    parts.append(sn)
    nose = A.box("Nose", (0.1, 0.03, 0.065), (0, -0.3, 0.035), "ink")
    fangs = [L.tip("Fang" + s_, (x * 0.075, -0.25, -0.08), 0.11, 0.026, "bone", direction=(0, 0, -1))
             for s_, x in (("L", 1), ("R", -1))]
    for o in [nose] + fangs:
        L.xform([o], tilt)
    parts += [nose] + fangs
    for s, x in (("L", 1), ("R", -1)):
        parts.append(A.box("HoodEye" + s, (0.07, 0.02, 0.035), (x * 0.11, -ry * 1.08, hz(0.96)), "ink"))
        ear = A.box("Ear" + s, (0.11, 0.05, 0.12), (x * HW * 0.72, ry * 0.1, hz(1.04)), "ochre_d", pivot=(0, 0, -1),
                    taper=(0.4, 0.6), rot=(0, x * -0.35, 0))
        parts.append(ear)
    # the mane: a dark ruff round the neck and over the shoulders, tufts radiating round the hood
    mane = S.shell("Mane", radius=(TW * 1.5, TW * 1.2), arc=(-25, 205), z=(Sz - 0.1, hz(0.35)), taper=0.85,
                   thickness=0.05, segments=6, location=(0, 0.05, 0), swatch="rust", flare=0.04)
    parts.append(A.flat(mane))
    for k in range(5):
        ang = -1.2 + k * 0.6
        tuft = A.box("Tuft%d" % k, (0.15, 0.13, 0.24), (math.sin(ang) * HW * 0.9, ry * 0.45, hz(0.5) + math.cos(ang) * HW * 0.9),
                     "rust" if k % 2 else "ochre_d", pivot=(0, 0, -1), taper=(0.2, 0.35), rot=(0.3, ang, 0))
        parts.append(tuft)
    # the pelt down his back, its tail at the hem
    parts.append(L.cape("Pelt", N + 0.02, 0.08, TW * 2.4, TW * 2.7, a["back_y"] + 0.03, "ochre_d", inner="leather",
                        flare=0.1))
    parts.append(A.box("PeltTail", (0.07, 0.05, 0.26), (0, a["back_y"] + 0.14, 0.12), "ochre_d", pivot=(0, 0, 1),
                       taper=(1.0, 1.0), rot=(-1.15, 0, 0)))
    # the goad in the left fist: upright, taller than him, a leaf blade and a hook curving back from the socket
    lx, ly, lz = a["hand_l"]
    x, y = lx + 0.005, ly - 0.01
    top = 1.95
    parts.append(L.rod("Goad", (x, y, 0.04), (x, y, top), 0.03, "wood_dark"))
    parts.append(L.rod("Grip", (x, y, lz - 0.08), (x, y, lz + 0.14), 0.036, "leather"))
    parts.append(A.box("Socket", (0.07, 0.07, 0.1), (x, y, top - 0.02), "iron_d"))
    parts.append(L.leaf_blade("Blade", (x, y, top + 0.03), 0.34, 0.13, "steel_l", edge="steel", thickness=0.03))
    hook = A.flat(K.extrude_poly("Hook", [(0.0, 0.0), (0.05, 0.02), (0.14, 0.0), (0.2, -0.08), (0.17, -0.13),
                                          (0.14, -0.06), (0.06, -0.035), (0.0, -0.04)],
                                 thickness=0.028, plane="YZ", swatch="steel"))
    hook.data.transform(Matrix.Translation((x, y + 0.02, top + 0.02)))
    parts.append(hook)
    # the hunch (the goad and both arms move as one piece with the shoulders, so the goad stays upright)
    held = {"Goad", "Grip", "Socket", "Blade", "Hook"}
    rigid = {o: Sz for o in parts if o.name.startswith(("Arm", "Fist", "Bracer")) or o.name in held}
    upper = [o for o in parts if not o.name.startswith(("Leg", "Foot", "Wrap", "Loin", "Horn", "Chain", "PeltTail"))]
    L.shear_forward(upper, H + 0.06, 0.2, rigid)
    return parts


parts = build()
L.finish(parts, SCALE, OX)
result = A.review("beast_lord", parts, original_blend="en_beastlord", original_root="EN_BeastLord", height=4.4,
                  gap=GAP)
