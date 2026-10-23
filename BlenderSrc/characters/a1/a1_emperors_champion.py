"""A1 roster — the Emperor's Champion in the simple style (the Imperial Arena's boss, docs/BOSSES.md; ~3.3 m to the
helm, ~4.6 m to the wingtips).

Identity kept from en_emperorschampion.py: upright and armoured, a soldier not a brute (straight back, no hunch):
a gold cuirass, layered gold-and-crimson pauldrons, gold vambraces, greaves and knee guards, a fringe of crimson and
gold pteruges, the magenta belt (the enemy accent), a long purple cloak to the ground with a gold hem; a stern face
under a gold helm with a crimson band, cheek guards, a brow visor and a neck guard, and on the helm the gold EAGLE
with its wings raised in a V (nothing else has wings); the spear taller than he is, upright in the right fist (leaf
blade, gold collar, crimson tassel, gold butt spike); the curved purple scutum (gold rim, spine, boss) at his left
side, not square in front; a crimson quiver of javelins fanning up past his left shoulder; a gladius at the right hip.
Built at the kit's chibi size (broad), then scaled x2.1 about the feet.
In-game (export=1): Assets/Art/Models/A1/Enemies/EN_EmperorsChampion.fbx, the kit body (a boss), with the parts
BossController drives by name: "Spear" under ArmR (pivot in the fist), "WingL"/"WingR" under Head (pivots at the wing
roots on the eagle); the shield on ArmL, the quiver, javelins, cloak and pteruges on Body.
"""
import importlib
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
import a1_tier3boss_lib as L
importlib.reload(L)
import a1_export as X
importlib.reload(X)

SCALE = 2.1
GAP = 2.9
EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -GAP / 2
WING = [(0.0, -0.06), (0.26, -0.1), (0.42, -0.03), (0.56, -0.13), (0.68, 0.0), (0.8, -0.07), (0.9, 0.16),
        (1.0, 0.52), (0.84, 0.44), (0.6, 0.34), (0.3, 0.24), (0.0, 0.15)]


def build():
    K.clear_scene()
    h = A.humanoid(0.0, p=dict(TW=0.25, SX=0.31, AR=0.088, LR=0.105, LX=0.125, HS=0.085), skin="skin_mid",
                   skin_shade="skin_tan", top="gold", top_shade=None, belt="accent_magenta", foot="leather_d",
                   sole="ink", hair="ink", brows="ink", hair_style="none", skip=("Head", "Buckle", "Strap"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, SX = p["H"], p["S"], p["N"], p["TW"], p["SX"]
    hz, HW = a["hz"], a["head_w"]
    ry = HW * 0.93
    w, ch = a["waist_z"], a["chest_z"]
    wz = Sz - p["AL"]
    torso = L.get(parts, "Torso")
    A.paint(torso, "crimson_d", lambda c, n: c.z < H + 0.005)
    A.paint(torso, "gold_l", lambda c, n: n.y < -0.6 and w < c.z < Sz)            # the breastplate's face
    parts.append(L.band("CuirassEdge", H + 0.06, H + 0.1, TW * 0.99, TW * 0.71, "gold_d"))
    # the pteruges: crimson and gold tabs round the hips
    parts.append(A.flat(S.strips("Pteruges", count=10, radius_top=(TW * 1.0, TW * 0.74), radius_bottom=(TW * 1.18, TW * 0.9),
                                 z_top=H + 0.01, length=0.17, gap=0.12, thickness=0.02, swatch="crimson",
                                 alt_swatch="gold_d")))
    # layered pauldrons, vambraces, greaves and knee guards
    for s, sx in (("L", 1), ("R", -1)):
        sh = a["shoulder_" + s.lower()]
        arc = (-100, 100) if sx > 0 else (80, 280)
        for k, (sw, dz, rr) in enumerate((("gold", 0.0, 1.85), ("crimson", -0.07, 1.95))):
            parts.append(A.flat(S.shell("Pauldron%s%d" % (s, k), radius=(p["AR"] * rr, p["AR"] * rr * 0.97), arc=arc,
                                        z=(-0.06 + dz, 0.06 + dz), taper=0.62 if k == 0 else 0.9, thickness=0.024,
                                        segments=4, location=(sh[0] - 0.012 * sx, 0.0, sh[2]), swatch=sw,
                                        flare=0.016)))
        x = (SX + 0.02) * sx
        parts.append(L.ring_rows("Vambrace" + s, [(wz + 0.01, p["AR"] * 0.98, p["AR"] * 0.96, x, -0.015),
                                                  (wz + 0.17, p["AR"] * 1.1, p["AR"] * 1.08, x, -0.012)],
                                 "gold", cap_bottom=False, cap_top=False))
        parts.append(A.flat(S.shell("Greave" + s, radius=(p["LR"] * 0.9, p["LR"] * 0.98), arc=(195, 345),
                                    z=(0.1, H * 0.66), taper=1.1, thickness=0.018, segments=3,
                                    location=(p["LX"] * sx, 0.0, 0.0), swatch="gold")))
        parts.append(A.box("Knee" + s, (p["LR"] * 1.5, 0.04, 0.07), (p["LX"] * sx, -p["LR"] * 1.02, H * 0.72), "gold_l"))
    # the helm: gold bowl with a crimson band, cheek guards, a brow visor, a neck guard
    parts.append(L.helmet_head(a, "skin_mid", "skin_tan"))
    parts.append(L.helmet(a, "gold", "crimson", low=0.62))
    parts += L.cheek_guards(a, "gold", low=0.2, high=0.68)
    parts.append(A.box("Visor", (HW * 2.0, 0.09, 0.035), (0, -ry * 1.08, hz(0.66)), "gold_d"))
    parts.append(A.flat(S.shell("NeckGuard", radius=(HW * 1.08, ry * 1.08), arc=(15, 165), z=(hz(0.1), hz(0.64)),
                                taper=0.96, thickness=0.03, segments=3, swatch="gold_d", flare=0.05)))
    # the eagle on the helm: body, head, beak, tail, the wings raised in a V
    top = hz(1.04)
    parts.append(L.ring_rows("Eagle", [(top - 0.02, 0.06, 0.1), (top + 0.1, 0.075, 0.12, 0, -0.01),
                                       (top + 0.17, 0.05, 0.07, 0, -0.04)], "gold_l"))
    parts.append(A.box("EagleHead", (0.075, 0.09, 0.075), (0, -0.09, top + 0.2), "gold_l"))
    parts.append(L.tip("Beak", (0, -0.13, top + 0.2), 0.06, 0.025, "gold_d", direction=(0, -1, -0.5)))
    parts.append(A.box("EagleTail", (0.1, 0.12, 0.02), (0, 0.13, top + 0.04), "gold_d", taper=(1.6, 1.0),
                       rot=(-0.5, 0, 0)))
    for s, sx in (("L", 1), ("R", -1)):
        wing = A.flat(K.extrude_poly("Wing" + s, [(x * 0.52 * sx, z * 0.52) for x, z in WING], thickness=0.03,
                                     plane="XZ", swatch="gold_l"))
        K.set_swatch_where(wing, "gold_d", lambda c, n: abs(n.y) < 0.5)
        if sx < 0:
            wing.data.flip_normals()
        wing.data.transform(Matrix.Translation((sx * 0.04, 0.0, top + 0.1)) @ Matrix.Rotation(-0.62 * sx, 4, "Y"))
        parts.append(wing)
    # the cloak: imperial purple from the shoulders to the ground, a gold hem
    parts.append(L.cape("Cloak", Sz + 0.03, 0.04, TW * 2.5, TW * 3.4, a["back_y"] + 0.03, "purple", inner="purple_d",
                        flare=0.2))
    parts.append(A.box("Hem", (TW * 3.45, 0.05, 0.05), (0, a["back_y"] + 0.03 + 0.2 + 0.02, 0.06), "gold"))
    # the quiver on his back, five javelins fanning up past the left shoulder
    qc = (0.1, a["back_y"] + 0.15, ch)
    quiver = [A.box("Quiver", (0.11, 0.09, 0.42), qc, "crimson"),
              A.box("QuiverRim", (0.13, 0.11, 0.04), (qc[0], qc[1], qc[2] + 0.2), "gold")]
    for k in range(5):
        fan = (k - 2) * 0.1
        base = (qc[0] + (k - 2) * 0.02, qc[1], qc[2] - 0.15)
        d = (math.sin(fan), 0.05 * abs(k - 2), math.cos(fan))
        end = tuple(base[i] + d[i] * 0.85 for i in range(3))
        quiver.append(L.rod("Javelin%d" % k, base, end, 0.012, "wood_light"))
        quiver.append(L.tip("JavTip%d" % k, end, 0.08, 0.026, "steel_l", direction=d))
    L.swing(quiver, qc, 0.5, "Y")
    parts += quiver
    # the gladius at the right hip
    parts.append(A.box("Scabbard", (0.05, 0.06, 0.26), (-TW * 1.05, 0.0, w - 0.02), "gold_d", pivot=(0, 0, 1),
                       rot=(-0.35, 0, 0)))
    parts.append(A.box("Hilt", (0.045, 0.045, 0.1), (-TW * 1.05, 0.0, w - 0.02), "crimson_d", pivot=(0, 0, -1),
                       rot=(-0.35, 0, 0)))
    # the spear: upright in the right fist, taller than him
    hx, hy, _ = a["hand_r"]
    x, y = hx, hy - 0.01
    st = 1.92
    parts.append(L.rod("Spear", (x, y, 0.06), (x, y, st), 0.026, "wood_dark", sides=6))
    parts.append(L.tip("Butt", (x, y, 0.07), 0.07, 0.03, "gold_d", direction=(0, 0, -1)))
    parts.append(L.ring_rows("Collar", [(st - 0.05, 0.04, 0.04, x, y), (st + 0.01, 0.04, 0.04, x, y)], "gold"))
    parts.append(A.box("Tassel", (0.08, 0.08, 0.15), (x, y, st - 0.04), "crimson", pivot=(0, 0, 1), taper=(0.4, 0.4)))
    parts.append(L.leaf_blade("Blade", (x, y, st + 0.01), 0.3, 0.12, "steel_l", edge="steel", thickness=0.03))
    # the scutum at his left side, its face turned to the front-left; the shield arm a little forward
    L.swing([L.get(parts, "ArmL"), L.get(parts, "FistL"), L.get(parts, "VambraceL")], (SX + 0.02, 0.0, Sz), -0.25)
    sh, fy = L.scutum("Shield", width=0.58, height=0.82, radius=0.42, depth=0.045, segs=4, rim=0.05, swatch="purple",
                      rim_swatch="gold", back_swatch="purple_d")
    spine = A.box("ShieldSpine", (0.04, 0.025, 0.7), (0, fy - 0.006, 0.0), "gold_d")
    knob = L.boss_knob("ShieldBoss", 0.085, 0.07, (0, fy - 0.015, 0.02), "gold_l", sides=6)
    L.xform([sh, spine, knob], Matrix.Translation((SX + 0.17, -0.16, 0.45)) @ Matrix.Rotation(0.5, 4, "Z"))
    parts += [sh, spine, knob]
    J = dict(X.joints(p))
    J["Spear"] = Vector(a["hand_r"])
    J["WingL"], J["WingR"] = Vector((0.04, 0.0, top + 0.1)), Vector((-0.04, 0.0, top + 0.1))
    return parts, L.scaled_joints(J, SCALE)


parts, J = build()
L.finish(parts, SCALE, OX)
if EXPORT:
    result = L.export_rig("EN_EmperorsChampion", parts, J, nest={"Spear": "ArmR", "WingL": "Head", "WingR": "Head"},
                          rules=[("CuirassEdge", "Body"), ("Pteruges", "Body"), ("Hem", "Body"), ("Quiver", "Body"),
                                 ("Jav", "Body"), ("Scabbard", "Body"), ("Hilt", "Body"), ("PauldronL", "ArmL"),
                                 ("PauldronR", "ArmR"), ("VambraceL", "ArmL"), ("VambraceR", "ArmR"),
                                 ("Knee", "side"), ("Cheek", "Head"), ("Visor", "Head"), ("NeckGuard", "Head"),
                                 ("Eagle", "Head"), ("Beak", "Head"), ("WingL", "WingL"), ("WingR", "WingR"),
                                 ("Spear", "Spear"), ("Butt", "Spear"), ("Collar", "Spear"), ("Tassel", "Spear"),
                                 ("Blade", "Spear"), ("Shield", "ArmL")])
else:
    result = A.review("emperors_champion", parts, original_blend="en_emperorschampion",
                      original_root="EN_EmperorsChampion", height=4.7, gap=GAP)
