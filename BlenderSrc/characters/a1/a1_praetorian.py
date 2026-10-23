"""A1 roster — the Praetorian in the simple style (tier 3: the Imperial Arena's heavy shield infantry).

Identity kept from en_praetorian.py: pale travertine segmented armour over a purple skirt, the magenta belt (the
enemy accent), a travertine helm with cheek guards and a tall upright purple plume, the tall curved purple scutum
with a gold rim, spine and boss held square in front (shin to the eyes: a line of them is a wall), the pilum upright
in the right fist with its iron shank and point well above the plume, greaved legs, leather boots.
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
    h = A.humanoid(0.0, skin="skin_mid", skin_shade="skin_tan", top="travertine", top_shade=None,
                   belt="accent_magenta", foot="leather", sole="leather_d", hair="mud_dark", hair_style="none",
                   sleeves=("L", "R"), legs="trousers", leg_colour="travertine_d", skip=("Head",))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz = p["H"], p["S"]
    hz = a["hz"]
    torso = L.get(parts, "Torso")
    A.paint(torso, "purple", lambda c, n: c.z < H + 0.005)            # the skirt under the armour
    K.set_swatch(L.get(parts, "Buckle"), "gold")
    # the segmented plates: one darker band across the chest, the sleeves as shoulder plates
    parts.append(L.band("Segment", a["chest_z"] - 0.05, a["chest_z"] - 0.01, p["TW"] * 1.02, p["TW"] * 0.68,
                        "travertine_d"))
    # the helm: a travertine dome, a darker brow band, cheek guards, a neck guard, the upright purple plume
    parts.append(L.helmet_head(a, "skin_mid", "skin_tan"))
    parts.append(L.helmet(a, "travertine", "travertine_d", low=0.62))
    parts += L.cheek_guards(a, "travertine_d", low=0.2, high=0.68)
    HW, ry = a["head_w"], a["head_back_y"]
    parts.append(A.flat(S.shell("NeckGuard", radius=(HW * 1.08, ry * 1.08), arc=(15, 165), z=(hz(0.06), hz(0.64)),
                                taper=0.96, thickness=0.03, segments=3, swatch="travertine_d", flare=0.05)))
    parts.append(L.plume("Plume", (0, 0.03, hz(1.03)), 0.46, 0.08, 0.14, 0.15, 0.42, "purple_l"))
    # the shield arm reaches forward to the scutum's grip
    arm_l = [L.get(parts, n) for n in ("ArmL", "FistL")]
    L.swing(arm_l, (p["SX"] + 0.02, 0.0, Sz), -0.8)
    L.swing([L.get(parts, "SleeveL")], (p["SX"] + 0.02, 0.0, Sz), -0.3)
    sh, fy = L.scutum("Shield", width=0.66, height=1.0, radius=0.46, depth=0.05, segs=4, rim=0.06, swatch="purple",
                      rim_swatch="gold", back_swatch="purple_d")
    spine = A.box("Spine", (0.06, 0.03, 0.88), (0, fy - 0.008, 0.0), "gold")
    knob = L.boss_knob("Boss", 0.11, 0.07, (0, fy - 0.02, 0.02), "gold_l")
    for o in (sh, spine, knob):
        L.bake([o])
        o.data.transform(Matrix.Translation((0.17, -0.38, 0.64)))
    parts += [sh, spine, knob]
    # the pilum: upright in the right fist, a wooden shaft, a long iron shank, a pyramid point
    hx, hy, hzz = a["hand_r"]
    parts.append(L.rod("Pilum", (hx, hy - 0.01, 0.08), (hx, hy - 0.01, 1.72), 0.028, "wood_dark"))
    parts.append(L.rod("Shank", (hx, hy - 0.01, 1.72), (hx, hy - 0.01, 2.08), 0.016, "iron"))
    parts.append(L.tip("PilumTip", (hx, hy - 0.01, 2.08), 0.16, 0.045, "steel_l"))
    return parts


parts = build()
L.finish(parts, 1.0, OX)
result = A.review("praetorian", parts, original_blend="en_praetorian", original_root="EN_Praetorian", height=2.0)
