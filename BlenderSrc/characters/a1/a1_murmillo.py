"""A1 roster — the Murmillo (EN_Murmillo, Arena 3's heavy shield) in the simple style.

Identity kept from en_murmillo.py: broad and bare-chested, a brimmed steel helm with a dark face grille and a tall red
fish crest (the silhouette's top), the padded white manica on the sword arm, a bronze greave on the leading leg, a
white loincloth under the magenta belt (the enemy accent), leather boots; the tall blue scutum with bronze rims and
spine held square in front from knee to chin (the door), and a short gladius pointing forward. Changed: the
variant-A body (lean); the helm is one chamfered loft standing in for the head; the scutum is a two-facet bent plate
(it reads curved in flat shading), rims and spine painted on it.
export=1: the game model Assets/Art/Models/A1/Enemies/EN_Murmillo.fbx (docs/tracks/A1-ingame-contract.md).
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
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.55
RULES = [("Bar", "Head"), ("Brim", "Head"), ("Loincloth", "Body"), ("Scutum", "ArmL"), ("Boss", "ArmL"),
         ("Gladius", "ArmR")]


def scutum(name, cx, cy, w, z0, z1, ox, bulge=0.06, t=0.035, sw="cloth_blue", rim="bronze", spine="bronze_l"):
    """A tall door shield bent into two facets (convex to the front), rims top and bottom, a spine down the middle."""
    c = 0.045
    pts = [(cx + w / 2, cy), (cx + w / 2, cy + t), (cx + c, cy - bulge + t), (cx - c, cy - bulge + t),
           (cx - w / 2, cy + t), (cx - w / 2, cy), (cx - c, cy - bulge), (cx + c, cy - bulge)]
    o = L.loft(name, [(z0, pts), (z0 + 0.05, pts), (z1 - 0.05, pts), (z1, pts)], sw, ox)
    L.paint(o, spine, lambda q, n: abs(q.x - cx) < c and n.y < -0.9, ox)
    L.paint(o, rim, lambda q, n: q.z < z0 + 0.045 or q.z > z1 - 0.045, ox)
    return o


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.23, AR=0.078, LR=0.095, LX=0.12, SX=0.29, HS=0.075), skin="skin_tan",
               skin_shade="skin_dark", top=None, belt="accent_magenta", foot="leather_d", hair_style="none",
               skip=("Head",), torso_cap=False)
    parts, a = h["parts"], h["a"]
    p = a["p"]
    HW, ry, hz, TW, H, S = a["head_w"], a["head_back_y"], a["hz"], p["TW"], p["H"], p["S"]
    o8 = L.oct8
    # the helm (standing in for the head): steel, a dark grille with two bars, a sloped brim, the red fish crest
    hm = L.loft("Helm", [(hz(0.0), o8(HW * 0.74, ry * 0.74, 0.64, 0, -0.01)), (hz(0.25), o8(HW * 1.02, ry * 1.02, 0.62)),
                         (hz(0.76), o8(HW * 1.04, ry * 1.04, 0.62)), (hz(1.0), o8(HW * 0.62, ry * 0.62, 0.62))],
                "steel", OX, cap_bottom=False)
    L.paint(hm, "iron_d", lambda c, n: n.y < -0.9 and c.z < hz(0.76), OX)
    parts.append(hm)
    gy = -ry * 1.04 - 0.004
    for k, x in enumerate((-0.05, 0.05)):
        parts.append(L.decal("Bar%d" % k, 0.018, p["HH"] * 0.46, (x, gy, hz(0.5)), "steel_l", OX))
    parts.append(L.loft("Brim", [(hz(0.68), o8(HW * 1.48, ry * 1.42, 0.62)), (hz(0.79), o8(HW * 1.06, ry * 1.06, 0.62))],
                        "steel_l", OX, False, False))
    crest = K.extrude_poly("Crest", [(-0.21, 0.0), (0.24, 0.0), (0.22, 0.2), (0.07, 0.33), (-0.13, 0.22)],
                           thickness=0.07, plane="YZ", swatch="red_banner", location=(OX, 0.0, hz(0.9)))
    parts.append(L.flat(crest))
    # white loincloth under the belt, the manica (the sword arm padded white), the bronze greave on the left shin
    parts.append(L.loft("Loincloth", [(H - 0.13, o8(TW * 1.06, TW * 0.8)), (a["waist_z"] - 0.02, o8(TW * 0.9, TW * 0.66))],
                        "cloth_white", OX, False, False))
    L.paint(L.by_name(parts, "ArmR")[0], "cloth_white", lambda c, n: True, OX)
    parts.append(L.ring("Greave", 0.1, H * 0.62, L.hex6(p["LR"] * 0.9, p["LR"] * 0.96, p["LX"], 0.0), "bronze", OX))
    # the scutum on the left arm, held square in front; the left forearm reaches forward to its grip
    xl = a["arm_x"]
    L.xform(L.by_name(parts, "ArmL", "FistL"), L.rot_x(-0.85), (xl, 0, S), OX)
    cx, cy, z0, z1 = 0.05, -0.33, 0.2, 0.95
    parts.append(scutum("Scutum", cx, cy, 0.56, z0, z1, OX))
    parts.append(A.cone("Boss", 0.07, 0.06, (cx, cy - 0.06, 0.6), "bronze_l", OX, verts=4, rot=(1.5708, 0, 0)))
    # the gladius in the right fist, pointing forward (bronze at the hilt)
    hx, hy, hzz = a["hand_r"]
    g = K.extrude_poly("Gladius", [(-0.02, 0.04), (-0.46, 0.05), (-0.6, 0.0), (-0.46, -0.05), (-0.02, -0.04)],
                       thickness=0.026, plane="YZ", swatch="steel_l", location=(hx + OX, hy, hzz))
    L.paint(g, "bronze_d", lambda c, n: c.y > -0.1, 0.0)
    parts.append(L.flat(g))
    L.xform(L.by_name(parts, "ArmR", "FistR", "Gladius"), L.rot_x(-0.3), (-xl, 0, S), OX)
    J = X.joints(p)
    return parts, J


parts, J = build()
if EXPORT:
    result = L.export_enemy("EN_Murmillo", parts, J, 1.72, RULES)
else:
    result = A.review("murmillo", parts, original_blend="en_murmillo", original_root="EN_Murmillo")
