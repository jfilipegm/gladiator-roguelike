"""A1 roster — the Blood Knight in the simple style (pattern: a1_nobody.py).

Identity kept from ch_bloodknight.py: a fallen knight in blackened plate (iron_d, iron trim), the crimson tabard, the
horned great-helm with the glowing cyan T visor, spiked pauldrons, the tattered crimson cape with the pale cyan sword
sigil on its back (read from the camera behind), the crimson-fullered greatsword held low and forward.
Built broader than the Nobody (wider torso, thicker limbs); the helm replaces the head (no face, no hair); scaled
x1.06 about the feet (G.grow) so the horns stand within 10 % of the original's 1.91 m.
export=1: the game model Assets/Art/Models/A1/Characters/CH_BloodKnight.fbx (a1_export; docs/tracks/A1-ingame-contract.md).
"""
import importlib
import os
import sys

import bpy

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import a1_glad_lib as G
importlib.reload(G)
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.55
SCALE = 1.06


def build():
    K.clear_scene()
    h = A.humanoid(OX, p=dict(TW=0.23, SX=0.285, AR=0.078, LR=0.098, LX=0.115),
                   skin="iron_d", skin_shade="iron_d", top="iron_d", top_shade="iron", foot="iron_d", sole="iron",
                   legs="trousers", leg_colour="iron_d", hair_style="none", face=False,
                   skip=("Head", "Belt", "Buckle"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    TW, H, Sz = p["TW"], p["H"], p["S"]
    hz = a["hz"]
    by_name = {o.name: o for o in parts}
    # gauntlets and greaves: the forearms, fists and shins in the lighter iron
    wz = Sz - p["AL"]
    for s in ("L", "R"):
        A.paint(by_name["Arm" + s], "iron", lambda c, n: c.z < wz + 0.12, OX)
        K.set_swatch(by_name["Fist" + s], "iron")
        A.paint(by_name["Leg" + s], "iron", lambda c, n: c.z < H * 0.42, OX)

    # the crimson tabard down the front, under a belt with a gold buckle
    tab = [(-0.115, 0.0), (0.115, 0.0), (0.10, -0.50), (0.0, -0.58), (-0.10, -0.50)]
    parts.append(G.panel("Tabard", tab, 0.02, (0, -0.172, a["chest_z"] - 0.02), "crimson", OX, tilt=-6.0))
    wz_ = a["waist_z"]
    parts.append(A.loft("Belt", [(wz_ - 0.035, A.oct8(TW * 0.96, TW * 0.80)), (wz_ + 0.035, A.oct8(TW * 0.97, TW * 0.81))],
                        "leather_d", OX, False, False))
    parts.append(A.box("Buckle", (0.075, 0.03, 0.06), (0, -TW * 0.81 - 0.005, wz_), "gold_d", OX))

    # spiked pauldrons (iron over a crimson_d rim), one big steel spike each
    for s, sx, arc in (("L", 1, (-115, 115)), ("R", -1, (65, 295))):
        sh = a["shoulder_l"] if sx > 0 else a["shoulder_r"]
        cx = sh[0] + OX - 0.005 * sx
        parts.append(A.flat(S.shell("Pauldron" + s, radius=(p["AR"] * 2.15, p["AR"] * 2.05), arc=arc, z=(-0.05, 0.09),
                                    taper=0.55, thickness=0.024, segments=4, location=(cx, 0.0, sh[2]),
                                    swatch="iron", flare=0.015)))
        parts.append(A.flat(S.shell("PauldronRim" + s, radius=(p["AR"] * 2.25, p["AR"] * 2.15), arc=arc,
                                    z=(-0.085, -0.045), taper=0.98, thickness=0.02, segments=4,
                                    location=(cx, 0.0, sh[2]), swatch="crimson_d")))
        x0 = sh[0] + 0.06 * sx
        parts.append(G.tube("Spike" + s, [(x0, 0.0, sh[2] + 0.07), (x0 + 0.07 * sx, 0.0, sh[2] + 0.2)], [0.042, 0.0],
                            "steel", OX, sides=5))

    # the horned great-helm: blackened iron, an iron crown, the glowing cyan T visor
    helm = G.helm("Helm", a, "iron_d", OX, scale=1.08, bottom=0.02, top=1.05, crown=0.5)
    A.paint(helm, "iron", lambda c, n: c.z > hz(0.88), OX)
    parts.append(helm)
    fy = a["head_front_y"] * 1.08 - 0.004
    parts.append(A.box("VisorSlit", (a["head_w"] * 1.15, 0.02, 0.05), (0, fy, hz(0.55)), "glow_cyan", OX))
    parts.append(A.box("VisorDrop", (0.05, 0.02, 0.2), (0, fy, hz(0.55)), "glow_cyan", OX, pivot=(0, 0, 1)))
    parts.append(A.box("Ridge", (0.05, 0.05, 0.16), (0, fy + 0.005, hz(0.62)), "iron", OX, pivot=(0, 0, -1)))
    hw = a["head_w"] * 1.08
    for s, sx in (("L", 1), ("R", -1)):
        parts.append(G.tube("Horn" + s, [(sx * (hw - 0.03), 0.01, hz(0.66)), (sx * (hw + 0.11), 0.02, hz(0.76)),
                                         (sx * (hw + 0.19), 0.03, hz(0.98)), (sx * (hw + 0.2), 0.04, hz(1.28))],
                            [0.075, 0.064, 0.045, 0.0], "bone", OX, sides=6))

    # the tattered crimson cape from the shoulders, the pale cyan sword sigil on its back
    cape_top = (0, a["back_y"] + 0.035, Sz + 0.03)
    parts.append(G.cape("Cape", 0.44, 0.64, 0.70, cape_top, "crimson_d", OX, cols=8, depth=0.1, tilt=12.0, bend=0.07,
                        seed=3))
    sigil = [(0.0, -0.08), (0.035, -0.115), (0.016, -0.14), (0.016, -0.19), (0.11, -0.19), (0.11, -0.235),
             (0.032, -0.235), (0.032, -0.47), (0.0, -0.53), (-0.032, -0.47), (-0.032, -0.235), (-0.11, -0.235),
             (-0.11, -0.19), (-0.016, -0.19), (-0.016, -0.14), (-0.035, -0.115)]
    parts.append(G.panel("Sigil", sigil, 0.012, cape_top, "player_cyan", OX, tilt=12.0, dy=0.024))

    # the crimson greatsword in the right fist: blade forward, tilted 15 degrees down
    hx, hy, hzz = a["hand_r"]
    f = G.frame("Greatsword", (hx, hy, hzz), OX, rot=(15, 0, 0))
    parts.append(f)
    parts.append(G.blade("Blade", [(-0.12, 0.09), (-1.08, 0.09), (-1.30, 0.0), (-1.08, -0.09), (-0.12, -0.09)], 0.04,
                         f, "steel", edge="steel_l"))
    parts.append(G.child_box("Fuller", (0.048, 0.82, 0.04), (0, -0.56, 0), "crimson", f))
    parts.append(G.child_box("Guard", (0.07, 0.07, 0.44), (0, -0.1, 0), "iron_d", f))
    parts.append(G.child_box("Gem", (0.085, 0.085, 0.085), (0, -0.1, 0), "blood", f))
    parts.append(G.child_box("Grip", (0.045, 0.22, 0.045), (0, 0.02, 0), "leather_d", f))
    parts.append(G.child_box("Pommel", (0.07, 0.07, 0.07), (0, 0.15, 0), "iron", f))
    G.grow(parts, OX, SCALE)
    return parts, p


parts, p = build()
if EXPORT:
    meshes = [o for o in parts if o.type == "MESH"]   # not the weapon's frame (its pieces are in the list)
    result = X.export("CH_BloodKnight", "Characters", meshes, X.joints(p, scale=SCALE),
                      rules=[("Tabard", "Body"), ("Sigil", "Body"), ("Pauldron", "Body"), ("Spike", "Body"),
                             ("Visor", "Head"), ("Ridge", "Head"), ("Horn", "Head"),
                             ("Blade", "ArmR"), ("Fuller", "ArmR"), ("Guard", "ArmR"), ("Gem", "ArmR"),
                             ("Grip", "ArmR"), ("Pommel", "ArmR")])
    root = bpy.data.objects["CH_BloodKnight"]   # build_skins.py reads the model's root
else:
    result = A.review("blood_knight", parts, original_blend="ch_bloodknight", original_root="CH_BloodKnight")
