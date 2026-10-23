"""A1 roster — the Gilded Mercenary in the simple style (tier 3: the Grand Arena's sellsword, the Patron's guard).

Identity kept from en_gildedmercenary.py: a broad professional in a steel cuirass trimmed with gold, a gilded helm
with cheek guards and a tall crimson horsehair crest (front to back), a crimson cloak, a crimson skirt, the fat gold
purse on the magenta belt (the enemy accent: he steals your gold), gold pauldrons and greaves, leather gloves and
boots, and the halberd taller than him held upright, its axe head broadside to the front with a spike above it.
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

OX = -0.55


def build():
    K.clear_scene()
    h = A.humanoid(0.0, p=dict(TW=0.225, SX=0.285, AR=0.074, LR=0.094), skin="skin_mid", skin_shade="skin_tan",
                   top="steel", top_shade=None, belt="accent_magenta", foot="leather", sole="leather_d",
                   hair="mud_dark", hair_style="none", legs="trousers", leg_colour="leather", skip=("Head", "Buckle"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, TW = p["H"], p["S"], p["TW"]
    hz = a["hz"]
    A.paint(L.get(parts, "Torso"), "crimson_d", lambda c, n: c.z < H + 0.005)     # the skirt
    for s in ("L", "R"):
        K.set_swatch(L.get(parts, "Fist" + s), "leather_d")                         # gloves
    # the gold trim round the top of the cuirass
    parts.append(L.band("Trim", Sz - 0.05, Sz - 0.005, TW * 1.07, TW * 0.66, "gold"))
    # the fat purse on the belt's left front
    w = a["waist_z"]
    parts.append(L.ring_rows("Purse", [(w - 0.19, 0.06, 0.04, 0.13, -TW * 0.74), (w - 0.11, 0.095, 0.07, 0.13, -TW * 0.8),
                                       (w - 0.03, 0.06, 0.045, 0.13, -TW * 0.76), (w + 0.02, 0.03, 0.025, 0.13, -TW * 0.72)],
                             "gold_d"))
    # gold pauldrons and greaves
    for s, sx in (("L", 1), ("R", -1)):
        sh = a["shoulder_" + s.lower()]
        pa = S.shell("Pauldron" + s, radius=(p["AR"] * 1.75, p["AR"] * 1.7), arc=(-100, 100) if sx > 0 else (80, 280),
                     z=(-0.06, 0.06), taper=0.6, thickness=0.02, segments=4, location=(sh[0] - 0.01 * sx, 0.0, sh[2]),
                     swatch="gold_d", flare=0.014)
        parts.append(A.flat(pa))
        gr = S.shell("Greave" + s, radius=(p["LR"] * 0.88, p["LR"] * 0.97), arc=(200, 340), z=(0.12, H * 0.62),
                     taper=1.08, thickness=0.016, segments=3, location=(p["LX"] * sx, 0.0, 0.0), swatch="gold_d")
        parts.append(A.flat(gr))
    # the gilded helm: cheek guards, the face open, the tall crimson crest front to back
    parts.append(L.helmet_head(a, "skin_mid", "skin_tan"))
    parts.append(L.helmet(a, "gold", "gold_d", low=0.62))
    parts += L.cheek_guards(a, "gold_d", low=0.22, high=0.68)
    HW, ry = a["head_w"], a["head_back_y"]
    parts.append(A.flat(S.shell("NeckGuard", radius=(HW * 1.08, ry * 1.08), arc=(15, 165), z=(hz(0.06), hz(0.64)),
                                taper=0.96, thickness=0.03, segments=3, swatch="gold_d", flare=0.05)))
    parts.append(L.fin_crest("Crest", (0, 0.02, hz(0.98)), 0.5, 0.3, 0.09, "crimson", front_h=0.22, back_h=0.12))
    # the crimson cloak down his back
    parts.append(L.cape("Cloak", Sz + 0.02, 0.2, TW * 2.3, TW * 2.8, a["back_y"] + 0.01, "crimson", inner="crimson_d",
                        flare=0.1))
    # the halberd: upright in the right fist, the axe broadside to the front on the outside, a spike above it
    hx, hy, _ = a["hand_r"]
    x, y = hx, hy - 0.01
    parts.append(L.rod("Halberd", (x, y, 0.06), (x, y, 2.0), 0.03, "wood_dark"))
    axe = [(0.0, -0.02), (0.0, 0.3), (-0.08, 0.27), (-0.3, 0.38), (-0.36, 0.14), (-0.3, -0.1), (-0.08, 0.01)]
    ax = A.flat(K.extrude_poly("Axe", axe, thickness=0.035, plane="XZ", swatch="steel_l"))
    K.set_swatch_where(ax, "steel", lambda c, n: abs(n.y) < 0.5)
    ax.data.transform(Matrix.Translation((x - 0.02, y, 1.66)))
    parts.append(ax)
    beak = A.flat(K.extrude_poly("Beak", [(0.0, 0.0), (0.0, 0.1), (0.13, 0.02)], thickness=0.03, plane="XZ",
                                 swatch="steel"))
    beak.data.transform(Matrix.Translation((x + 0.02, y, 1.8)))
    parts.append(beak)
    parts.append(A.box("Socket", (0.07, 0.07, 0.08), (x, y, 1.98), "steel"))
    parts.append(L.tip("Spike", (x, y, 2.02), 0.26, 0.04, "steel_l"))
    return parts


parts = build()
L.finish(parts, 1.0, OX)
result = A.review("gilded_mercenary", parts, original_blend="en_gildedmercenary", original_root="EN_GildedMercenary",
                  height=2.1)
