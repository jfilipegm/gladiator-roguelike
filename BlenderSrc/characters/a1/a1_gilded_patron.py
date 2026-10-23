"""A1 roster — the Gilded Patron in the simple style (the Grand Arena's boss, docs/BOSSES.md; ~3.6 m to the finial).

Identity kept from en_gildedpatron.py: the richest man in the city lounging on a gilded litter under a crimson canopy
(the roof nothing else has: a gold fringe, a crimson peak, a gold finial), carried on two long gold poles by four
bare-chested bearers in white loincloths and crimson caps; he is round, in gold robes with a crimson toga, a fat bald
head with a gold laurel wreath and a bored face, a goblet raised in his left hand and a fat purse in his right (he
throws the coin bombs from it). The litter's platform has a crimson skirt, a crimson cushion and a gold backrest.
The bearers are a cheap version of the kit body (a1_tier3boss_lib.lean_humanoid, ~270 tris each) so the whole boss
stays under 2,500. Built in kit units, then scaled x1.15 about the ground (~4.45 m to the finial; the original 4.87).
In-game (export=1): Assets/Art/Models/A1/Enemies/EN_GildedPatron.fbx with the original's parts: Body = the litter
(platform, cushion, posts, canopy) and the seated Patron, so it rocks on the poles; Head and ArmL (goblet) / ArmR
(purse) under it; no LegL/LegR. The bearers' legs are BLeg<i>L / BLeg<i>R (pivot at the hip) for BossController to
swing; the rest of the bearers and the poles are one static mesh ("Root").
"""
import importlib
import math
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
import a1_export as X
importlib.reload(X)

SCALE = 1.15
GAP = 3.2
EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -GAP / 2
BEARERS = ((0.48, -1.62), (-0.48, -1.62), (0.48, 1.62), (-0.48, 1.62))
POLE_X, POLE_Z = 0.86, 1.33


def bearer(i, bx, by):
    h = L.lean_humanoid(bx, by, skin="skin_tan", skin_shade="skin_dark", loin="cloth_white", foot="leather_d",
                        cap="crimson", name="B%d" % i)
    parts, a = h["parts"], h["a"]
    out = "L" if bx > 0 else "R"                      # the arm under the pole reaches up to it
    sh = a["shoulder_" + out.lower()]
    ang = -(math.pi - 0.25) if bx > 0 else (math.pi - 0.25)
    L.swing([L.get(parts, "B%dArm%s" % (i, out)), L.get(parts, "B%dFist%s" % (i, out))], sh, ang, "Y")
    return parts


def build():
    K.clear_scene()
    parts = []
    for i, (bx, by) in enumerate(BEARERS):
        parts += bearer(i, bx, by)
    # the poles on the bearers' raised fists, gold caps at the ends
    for s, sx in (("L", 1), ("R", -1)):
        x = POLE_X * sx
        parts.append(L.rod("Pole" + s, (x, -2.0, POLE_Z), (x, 2.0, POLE_Z), 0.05, "gold_d"))
        for e in (-1, 1):
            parts.append(A.box("PoleCap%s%d" % (s, e), (0.13, 0.08, 0.13), (x, e * 2.0, POLE_Z), "gold"))
    # the litter: platform, skirt, cushion, backrest, posts, canopy with its fringe, peak and finial
    parts.append(A.box("Platform", (1.62, 2.14, 0.14), (0, 0, 1.28), "gold_d", pivot=(0, 0, -1)))
    parts.append(A.box("Skirt", (1.58, 2.1, 0.2), (0, 0, 1.08), "crimson_d", pivot=(0, 0, -1)))
    parts.append(A.box("Cushion", (1.4, 1.8, 0.12), (0, 0.05, 1.42), "crimson", pivot=(0, 0, -1)))
    parts.append(A.box("Backrest", (1.3, 0.16, 0.62), (0, 0.9, 1.54), "gold", pivot=(0, 0, -1), taper=(0.9, 1.0)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(L.rod("Post%d%d" % (sx, sy), (sx * 0.72, sy * 0.98, 1.42), (sx * 0.72, sy * 0.98, 3.2), 0.035,
                               "gold"))
    parts.append(A.box("Fringe", (1.86, 2.36, 0.12), (0, 0, 3.11), "gold", pivot=(0, 0, -1)))
    parts.append(A.box("Canopy", (1.82, 2.32, 0.1), (0, 0, 3.22), "crimson", pivot=(0, 0, -1), taper=(0.96, 0.97)))
    parts.append(L.ring_rows("Peak", [(3.32, 0.86, 1.1, 0, 0, 0.85), (3.72, 0.07, 0.07, 0, 0, 0.85)], "crimson_d"))
    parts.append(L.ring_rows("Finial", [(3.69, 0.04, 0.04), (3.78, 0.09, 0.09), (3.87, 0.03, 0.03)], "gold_l"))
    # the Patron: a round gold body, a crimson toga across it, crossed legs on the cushion (built at 1/1.2 and scaled
    # up about the seat, so he fills his litter)
    n_before = len(parts)
    body = L.ring_rows("Robe", [(1.54, 0.44, 0.38, 0, 0.12), (1.8, 0.52, 0.47, 0, 0.06), (2.04, 0.45, 0.37, 0, 0.08),
                                (2.2, 0.2, 0.16, 0, 0.1)], "gold")
    parts.append(body)
    parts.append(L.rod("Toga", (0.36, -0.12, 2.12), (-0.3, -0.36, 1.66), 0.075, "crimson"))
    parts.append(A.box("Lap", (1.0, 0.5, 0.24), (0, -0.38, 1.54), "gold_d", pivot=(0, 0, -1)))
    for s, sx in (("L", 1), ("R", -1)):
        parts.append(A.box("Sandal" + s, (0.14, 0.2, 0.08), (sx * 0.22, -0.62, 1.54), "leather_d", pivot=(0, 0, -1)))
    # arms in crimson sleeves: the goblet raised in the left hand, the purse in the right
    lh, rh = (0.52, -0.3, 1.98), (-0.62, -0.18, 1.7)
    parts.append(L.rod("ArmL", (0.42, 0.08, 2.1), lh, 0.09, "crimson", sides=6, taper=0.85))
    parts.append(L.rod("ArmR", (-0.42, 0.08, 2.1), rh, 0.09, "crimson", sides=6, taper=0.85))
    for s, c in (("L", lh), ("R", rh)):
        parts.append(A.box("Hand" + s, (0.13, 0.13, 0.12), c, "skin_light"))
    gx, gy, gz = lh
    parts.append(L.ring_rows("Goblet", [(gz + 0.03, 0.05, 0.05, gx, gy), (gz + 0.12, 0.025, 0.025, gx, gy),
                                        (gz + 0.17, 0.09, 0.09, gx, gy), (gz + 0.27, 0.11, 0.11, gx, gy)], "gold_l",
                             cap_top=True))
    px, py, pz = rh
    parts.append(L.ring_rows("Purse", [(pz - 0.27, 0.08, 0.08, px, py), (pz - 0.17, 0.15, 0.14, px, py),
                                       (pz - 0.07, 0.1, 0.1, px, py), (pz - 0.03, 0.04, 0.04, px, py)], "gold"))
    # the fat bald head, a bored face, jowls, the laurel wreath
    hp = dict(N=2.17, HH=0.56, HW=0.31)
    h = A.humanoid(0.0, p=hp, skin="skin_light", skin_shade="skin_mid", hair="mud", brows="mud", hair_style="none",
                   skip=("Leg", "Foot", "Strap", "Torso", "Belt", "Buckle", "Arm", "Fist"))
    hparts, a = h["parts"], h["a"]
    hz, HW = a["hz"], a["head_w"]
    ry = HW * 0.93
    for s, ang in (("L", 0.14), ("R", -0.14)):                        # drooping brows, heavy lids: bored
        L.get(hparts, "Brow" + s).rotation_euler = (0, ang, 0)
        L.get(hparts, "Eye" + s).scale = (1.0, 1.0, 0.6)
    parts += hparts
    for o in hparts:
        o.data.transform(Matrix.Translation((0, 0.08, 0)))
    parts.append(L.ring_rows("Jowls", [(hz(0.02), HW * 0.8, ry * 0.7, 0, 0.0), (hz(0.2), HW * 1.08, ry * 0.95, 0, 0.04),
                                       (hz(0.36), HW * 1.0, ry * 0.9, 0, 0.06)], "skin_light", cap_top=False))
    lz = hz(0.72)
    parts.append(L.hoop("Wreath", (0, 0.08, lz), HW * 1.04, 0.022, "gold_d", axis="Z"))
    for k in range(8):
        a_ = 2 * math.pi * (k + 0.5) / 8
        x, y = math.cos(a_) * HW * 1.06, 0.08 + math.sin(a_) * ry * 1.1
        if y < -ry * 0.6:                                             # the wreath is open over the brow
            continue
        parts.append(A.box("Leaf%d" % k, (0.09, 0.03, 0.06), (x, y, lz + 0.02), "gold_l" if k % 2 else "gold",
                           taper=(0.4, 1.0), rot=(0.0, 0.0, a_ + math.pi / 2)))
    seat = (0, 0.1, 1.54)
    up = Matrix.Translation(seat) @ Matrix.Scale(1.2, 4) @ Matrix.Translation((0, -0.1, -1.54))
    L.xform(parts[n_before:], up)
    # the joints: the litter's underside (Body), the Patron's neck and shoulders (after his x1.2), the bearers' hips
    J = {"Body": (0, 0, 1.28), "Head": up @ Vector((0, 0.08, hp["N"])), "ArmL": up @ Vector((0.42, 0.08, 2.1)),
         "ArmR": up @ Vector((-0.42, 0.08, 2.1))}
    for i, (bx, by) in enumerate(BEARERS):
        for s_, sx in (("L", 1), ("R", -1)):
            J["BLeg%d%s" % (i, s_)] = (bx + sx * A.CHIBI["LX"], by, A.CHIBI["H"])
    return parts, L.scaled_joints(J, SCALE)


parts, J = build()
L.finish(parts, SCALE, OX)
if EXPORT:
    rules = [("PoleCap", "Root"), ("Pole", "Root"), ("Platform", "Body"), ("Cushion", "Body"), ("Backrest", "Body"),
             ("Post", "Body"), ("Fringe", "Body"), ("Canopy", "Body"), ("Peak", "Body"), ("Finial", "Body"),
             ("Robe", "Body"), ("Toga", "Body"), ("Lap", "Body"), ("Sandal", "Body"), ("Goblet", "ArmL"),
             ("Purse", "ArmR"), ("Jowls", "Head"), ("Wreath", "Head"), ("Leaf", "Head")]
    for i in range(len(BEARERS)):
        for s_ in ("L", "R"):
            rules += [("B%dLeg%s" % (i, s_), "BLeg%d%s" % (i, s_)), ("B%dFoot%s" % (i, s_), "BLeg%d%s" % (i, s_))]
        rules.append(("B%d" % i, "Root"))
    result = L.export_rig("EN_GildedPatron", parts, J, rules=rules)
else:
    result = A.review("gilded_patron", parts, original_blend="en_gildedpatron", original_root="EN_GildedPatron",
                  height=3.9, gap=GAP)
