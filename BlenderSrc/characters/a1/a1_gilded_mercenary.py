"""A1 roster — the Gilded Mercenary in the simple style (tier 3: the Grand Arena's sellsword, the Patron's guard).

Identity kept from en_gildedmercenary.py: a broad professional in a steel cuirass trimmed with gold, a gilded helm
with cheek guards and a tall crimson horsehair crest (front to back), a crimson cloak, a crimson skirt, the fat gold
purse on the magenta belt (the enemy accent: he steals your gold), gold pauldrons and greaves, leather gloves and
boots, and the halberd taller than him held upright, its axe head broadside to the front with a spike above it.
In-game (export=1): the lean enemy body (a1_tier12_lib.body, decal face), the helm's back painted on the head instead
of a neck guard, gold shoulder plates as sleeves: ~530 tris; Assets/Art/Models/A1/Enemies/EN_GildedMercenary.fbx.
"""
import importlib
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier12_lib as T
importlib.reload(T)
import a1_tier3boss_lib as L
importlib.reload(L)
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.55


def build():
    K.clear_scene()
    h = T.body(0.0, p=dict(TW=0.225, SX=0.285, AR=0.074, LR=0.094), skin="skin_mid", skin_shade="skin_tan",
               top="steel", top_shade=None, belt="accent_magenta", foot="leather", hair_style="none", head_top="flat",
               sleeves=("L", "R"), legs="trousers", leg_colour="leather")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, TW = p["H"], p["S"], p["TW"]
    hz, HW, ry = a["hz"], a["head_w"], a["head_back_y"]
    torso = L.get(parts, "Torso")
    A.paint(torso, "crimson_d", lambda c, n: c.z < H + 0.005)                     # the skirt
    A.paint(torso, "gold", lambda c, n: c.z > Sz)                                 # the gold trim round the top
    for s in ("L", "R"):
        K.set_swatch(L.get(parts, "Fist" + s), "leather_d")                         # gloves
        K.set_swatch(L.get(parts, "Sleeve" + s), "gold_d")                          # gold shoulder plates
        parts.append(T.ring("Greave" + s, 0.07, H * 0.66, T.hex6(p["LR"] * 0.9, p["LR"] * 0.97, p["LX"] * (1 if s == "L" else -1),
                                                                   0.0), "gold_d"))
    parts += T.face(a, 0.0, eyes="ink", brows="mud_dark", nose="skin_tan", brow_angle=0.3)
    # the fat purse on the belt's left front
    w = a["waist_z"]
    parts.append(T.octa("Purse", 0.075, (0.13, -TW * 0.8, w - 0.1), "gold_d", scale=(1.0, 0.75, 1.15)))
    # the gilded helm: cheek guards, its back down over the nape, the tall crimson crest front to back
    head = L.get(parts, "Head")
    A.paint(head, "gold_d", lambda c, n: n.y > 0.35 and c.z > hz(0.12))
    parts.append(L.lean_helmet(a, "gold", low=0.6))
    parts += L.cheek_guards(a, "gold_d", low=0.22, high=0.66)
    parts.append(L.fin_crest("Crest", (0, 0.02, hz(0.98)), 0.5, 0.3, 0.09, "crimson", front_h=0.22, back_h=0.12))
    # the crimson cloak down his back
    parts.append(L.cape("Cloak", Sz + 0.02, 0.2, TW * 2.3, TW * 2.8, a["back_y"] + 0.01, "crimson", inner="crimson_d",
                        flare=0.1))
    # the halberd: upright in the right fist, the axe broadside to the front on the outside, a spike above it
    hx, hy, _ = a["hand_r"]
    x, y = hx, hy - 0.01
    top = 2.2
    parts.append(L.rod("Halberd", (x, y, 0.06), (x, y, top), 0.03, "wood_dark"))
    axe = [(0.0, -0.04), (0.0, 0.3), (-0.3, 0.4), (-0.36, 0.13), (-0.3, -0.12)]
    ax = A.flat(K.extrude_poly("HalberdAxe", axe, thickness=0.035, plane="XZ", swatch="steel_l"))
    K.set_swatch_where(ax, "steel", lambda c, n: abs(n.y) < 0.5)
    ax.data.transform(Matrix.Translation((x - 0.02, y, top - 0.36)))
    parts.append(ax)
    parts.append(L.tip("HalberdSpike", (x, y, top), 0.28, 0.04, "steel_l"))
    return parts, X.joints(p)


parts, J = build()
L.finish(parts, 1.0, OX)
if EXPORT:
    result = L.export_rig("EN_GildedMercenary", parts, J, rules=[("Cheek", "Head"), ("Halberd", "ArmR"),
                                                                 ("Purse", "Body")])
else:
    result = A.review("gilded_mercenary", parts, original_blend="en_gildedmercenary",
                      original_root="EN_GildedMercenary", height=2.4)
