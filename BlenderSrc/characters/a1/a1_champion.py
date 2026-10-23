"""A1 roster — the Provincial Champion in the simple style (Arena 2's boss, docs/BOSSES.md; built at ~2.7 m).

Identity kept from en_champion.py: a broad, armoured murmillo: bronze cuirass with darker bands, the magenta belt (the
enemy accent), a white skirt, bronze pauldrons, a segmented steel manica on the sword arm, a bronze greave on the
leading (shield-side) leg, leather boots; a broad-brimmed steel helm with a face grille and the tall crimson fish crest;
the huge curved crimson scutum (gold rim, spine and boss) that covers him from shin to chin, so from the front the
shield IS the silhouette; a short gladius in the right fist, pointing forward.
Built at the kit's chibi size, then scaled x1.75 about the feet (the head top ~2.7 m, the crest above it).
"""
import importlib
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

SCALE = 1.75
GAP = 2.2
OX = -GAP / 2


def build():
    K.clear_scene()
    h = A.humanoid(0.0, p=dict(TW=0.25, SX=0.3, AR=0.085, LR=0.105, LX=0.12, HS=0.082), skin="skin_mid",
                   skin_shade="skin_tan", top="bronze", top_shade=None, belt="accent_magenta", foot="leather_d",
                   sole="ink", hair_style="none", face=False, skip=("Head", "Buckle", "Strap"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, TW, SX = p["H"], p["S"], p["TW"], p["SX"]
    hz, HW, ry = a["hz"], a["head_w"], a["head_back_y"]
    A.paint(L.get(parts, "Torso"), "cloth_white", lambda c, n: c.z < H + 0.005)       # the skirt
    for k, z in enumerate((a["waist_z"] + 0.07, a["chest_z"] - 0.02)):                # the cuirass bands
        parts.append(L.band("Band%d" % k, z, z + 0.035, TW * (0.92 + 0.08 * k), TW * 0.66, "bronze_d"))
    # bronze pauldrons; the manica on the sword arm (right); the greave on the shield-side leg (left)
    for s, sx in (("L", 1), ("R", -1)):
        sh = a["shoulder_" + s.lower()]
        parts.append(A.flat(S.shell("Pauldron" + s, radius=(p["AR"] * 1.8, p["AR"] * 1.75),
                                    arc=(-100, 100) if sx > 0 else (80, 280), z=(-0.07, 0.07), taper=0.6,
                                    thickness=0.024, segments=4, location=(sh[0] - 0.012 * sx, 0.0, sh[2]),
                                    swatch="bronze_d", flare=0.016)))
    wz = Sz - p["AL"]
    xr = -(SX + 0.02)
    for k in range(3):
        z0 = wz + 0.02 + k * 0.1
        parts.append(L.ring_rows("Manica%d" % k, [(z0, p["AR"] * 1.12, p["AR"] * 1.1, xr, -0.012),
                                                  (z0 + 0.075, p["AR"] * 1.2, p["AR"] * 1.16, xr, -0.012)],
                                 "steel" if k % 2 == 0 else "iron", cap_bottom=False, cap_top=False))
    parts.append(A.flat(S.shell("Greave", radius=(p["LR"] * 0.9, p["LR"] * 0.98), arc=(200, 340), z=(0.12, H * 0.66),
                                taper=1.08, thickness=0.018, segments=3, location=(p["LX"], 0.0, 0.0), swatch="bronze")))
    # the murmillo helm: a steel bowl over the whole head, a broad iron brim, a grille over the face, the fish crest
    parts.append(L.helmet_head(a, "skin_mid", "skin_tan", top=0.5))
    parts.append(A.loft("Helm", [(hz(0.06), A.oct8(HW * 1.04, ry * 1.04, 0.62, 0, 0.0)),
                                 (hz(0.62), A.oct8(HW * 1.08, ry * 1.08, 0.62)),
                                 (hz(0.9), A.oct8(HW * 0.94, ry * 0.94, 0.6)),
                                 (hz(1.06), A.oct8(HW * 0.46, ry * 0.46, 0.6))], "steel", cap_bottom=False))
    parts.append(L.ring_rows("Brim", [(hz(0.6), HW * 1.42, ry * 1.42, 0, -0.01), (hz(0.66), HW * 1.36, ry * 1.36, 0, -0.01)],
                             "iron"))
    parts.append(A.box("Grille", (HW * 1.3, 0.03, hz(0.58) - hz(0.1)), (0, -ry * 1.05, hz(0.1)), "iron_d",
                       pivot=(0, 0, -1)))
    for k, x in enumerate((-0.09, 0.0, 0.09)):
        parts.append(A.box("Bar%d" % k, (0.028, 0.03, hz(0.56) - hz(0.12)), (x, -ry * 1.05 - 0.02, hz(0.12)),
                           "steel_l", pivot=(0, 0, -1)))
    parts.append(A.box("Visor", (HW * 1.1, 0.034, 0.04), (0, -ry * 1.05 - 0.02, hz(0.4)), "steel_l"))
    parts.append(L.fin_crest("Crest", (0, 0.02, hz(1.0)), 0.5, 0.28, 0.08, "crimson", front_h=0.3, back_h=0.12))
    parts.append(A.box("CrestHead", (0.07, 0.12, 0.14), (0, -0.25, hz(1.0) + 0.1), "crimson_d", taper=(0.8, 0.6)))
    # the shield arm reaches forward to the scutum, square in front of him from shin to chin
    sl = (SX + 0.02, 0.0, Sz)
    L.swing([L.get(parts, "ArmL"), L.get(parts, "FistL")], sl, -0.85)
    L.swing([L.get(parts, "PauldronL")], sl, -0.25)
    sh, fy = L.scutum("Shield", width=0.74, height=1.0, radius=0.5, depth=0.055, segs=4, rim=0.065, swatch="crimson",
                      rim_swatch="gold", back_swatch="crimson_d")
    spine = A.box("Spine", (0.06, 0.03, 0.86), (0, fy - 0.008, 0.0), "gold_d")
    knob = L.boss_knob("Boss", 0.12, 0.08, (0, fy - 0.02, 0.0), "gold_l", sides=6)
    L.xform([sh, spine, knob], Matrix.Translation((0.09, -0.4, 0.63)))
    parts += [sh, spine, knob]
    # the gladius in the right fist, its blade forward
    hx, hy, hzz = a["hand_r"]
    blade = A.flat(K.extrude_poly("Blade", [(-0.06, 0.04), (-0.38, 0.05), (-0.5, 0.0), (-0.38, -0.05), (-0.06, -0.04)],
                                  thickness=0.03, plane="YZ", swatch="steel_l"))
    K.set_swatch_where(blade, "steel", lambda c, n: abs(n.x) < 0.5)
    blade.data.transform(Matrix.Translation((hx, hy, hzz)))
    parts.append(blade)
    parts.append(A.box("Guard", (0.06, 0.04, 0.15), (hx, hy - 0.08, hzz), "gold"))
    parts.append(A.box("Pommel", (0.055, 0.05, 0.055), (hx, hy + 0.09, hzz), "gold"))
    return parts


parts = build()
L.finish(parts, SCALE, OX)
result = A.review("champion", parts, original_blend="en_champion", original_root="EN_Champion", height=3.0, gap=GAP)
