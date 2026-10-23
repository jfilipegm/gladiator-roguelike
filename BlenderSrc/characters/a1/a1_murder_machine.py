"""A1 roster — the Murder Machine in the simple style (pattern: a1_nobody.py).

Identity kept from ch_murdermachine.py: a huge executioner (hood tip ~2.2 m against the Nobody's 1.6), a barrel of
rusty riveted iron with an iron chest plate, the black hood-mantle over the shoulders with its point down the back,
the tall pointed black hood with two glowing cyan eye holes, bare tanned arms with iron pauldrons and vambraces,
the leather apron with a rust stain, short legs in iron greaves and boots, the pale cyan axe sigil on his back and
the slab of a cleaver held low and forward.
Built at the chibi scale with a brute's proportions (a barrel torso, thick limbs, short legs), then scaled x1.38
about his feet (G.grow), so the style's sizes stay consistent with the Nobody.
"""
import importlib
import os
import sys

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import a1_glad_lib as G
importlib.reload(G)

GAP = 2.0
OX = -GAP / 2
SCALE = 1.45


def build():
    K.clear_scene()
    p = dict(H=0.32, S=0.82, SX=0.45, N=0.90, HH=0.40, HW=0.22, TW=0.33, LR=0.12, LX=0.16, AR=0.108, AL=0.36,
             HS=0.098, FL=0.30)
    h = A.humanoid(OX, p=p, skin="skin_tan", skin_shade="skin_dark", top=None, belt=None, legs="trousers",
                   leg_colour="leather_d", hair_style="none", face=False, skip=("Head", "Torso", "Foot", "Strap"))
    parts, a = h["parts"], h["a"]
    H, Sz, N = p["H"], p["S"], p["N"]
    hz = a["hz"]
    by_name = {o.name: o for o in parts}
    wz = Sz - p["AL"]
    for s in ("L", "R"):
        A.paint(by_name["Arm" + s], "iron", lambda c, n: c.z < wz + 0.12, OX)       # vambraces
        K.set_swatch(by_name["Fist" + s], "iron_d")
        A.paint(by_name["Leg" + s], "iron", lambda c, n: c.z < H * 0.5, OX)         # greaves

    # the barrel: rusty iron, widest at the belly, an iron chest plate with a row of bright rivets
    rings = [(H - 0.10, A.oct8(0.29, 0.23)), (H + 0.02, A.oct8(0.33, 0.255)), (0.52, A.oct8(0.36, 0.28)),
             (0.68, A.oct8(0.355, 0.275)), (Sz, A.oct8(0.31, 0.245)), (Sz + 0.05, A.oct8(0.19, 0.16)),
             (N + 0.02, A.oct8(0.10, 0.09))]
    parts.append(A.loft("Barrel", rings, "rusty_iron", OX, cap_top=False))
    parts.append(G.panel("ChestPlate", [(-0.205, 0.0), (0.205, 0.0), (0.19, -0.30), (-0.19, -0.30)], 0.03,
                         (0, -0.29, 0.80), "iron", OX, tilt=4.0))
    for k, x in enumerate((-0.15, -0.05, 0.05, 0.15)):
        parts.append(A.box("Rivet%d" % k, (0.04, 0.025, 0.04), (x, -0.31, 0.755), "steel_l", OX))
    # the belt and the leather apron with a rust stain
    parts.append(A.loft("Belt", [(H - 0.03, A.oct8(0.34, 0.265)), (H + 0.05, A.oct8(0.345, 0.27))], "leather_d", OX,
                        False, False))
    parts.append(A.box("Buckle", (0.10, 0.03, 0.08), (0, -0.275, H + 0.01), "rusty_iron", OX))
    apron = [(-0.19, 0.0), (0.19, 0.0), (0.22, -0.28), (0.08, -0.31), (-0.06, -0.29), (-0.22, -0.28)]
    parts.append(G.panel("Apron", apron, 0.025, (0, -0.28, H - 0.02), "leather", OX, tilt=-7.0))
    stain = [(0.0, -0.10), (0.10, -0.12), (0.13, -0.20), (0.07, -0.26), (-0.02, -0.22)]
    parts.append(G.panel("ApronStain", stain, 0.012, (0, -0.28, H - 0.02), "rust", OX, tilt=-7.0, dy=-0.016))

    # the hood-mantle: black cloth over the shoulders, its point down the back
    parts.append(A.loft("Mantle", [(Sz - 0.08, A.oct8(0.39, 0.30, 0.66)), (Sz + 0.03, A.oct8(0.35, 0.28, 0.64)),
                                   (Sz + 0.08, A.oct8(0.22, 0.19, 0.62)), (N + 0.03, A.oct8(0.15, 0.135, 0.6))],
                        "charcoal", OX, cap_bottom=False, cap_top=False))
    parts.append(G.panel("MantleTail", [(-0.24, 0.0), (0.24, 0.0), (0.0, -0.17)], 0.03, (0, 0.29, Sz - 0.05),
                         "charcoal", OX, tilt=4.0))
    # the player's mark for the camera behind: a pale cyan axe on the back plate, under the mantle's point
    axe = [(-0.022, -0.27), (-0.022, -0.04), (0.022, -0.04), (0.022, -0.06), (0.06, -0.03), (0.14, 0.0),
           (0.14, -0.17), (0.06, -0.14), (0.022, -0.12), (0.022, -0.27)]
    parts.append(G.panel("Sigil", [(x - 0.03, z) for x, z in axe], 0.012, (0, 0.288, 0.64), "player_cyan", OX))

    # iron pauldrons on the bare arms
    for s, sx, arc in (("L", 1, (-115, 115)), ("R", -1, (65, 295))):
        sh = a["shoulder_l"] if sx > 0 else a["shoulder_r"]
        parts.append(A.flat(S.shell("Pauldron" + s, radius=(p["AR"] * 1.75, p["AR"] * 1.7), arc=arc, z=(-0.07, 0.07),
                                    taper=0.6, thickness=0.025, segments=4,
                                    location=(sh[0] + OX - 0.01 * sx, -0.005, sh[2]), swatch="iron", flare=0.015)))

    # the tall pointed executioner's hood (ink), two glowing cyan eye holes
    HW = p["HW"]
    hood = [(hz(-0.02), A.oct8(HW * 0.72, HW * 0.66, 0.64)), (hz(0.16), A.oct8(HW * 0.96, HW * 0.92, 0.62)),
            (hz(0.62), A.oct8(HW, HW * 0.95, 0.62)), (hz(0.95), A.oct8(HW * 0.74, HW * 0.70, 0.6, 0, 0.02)),
            (hz(1.24), A.oct8(HW * 0.36, HW * 0.34, 0.6, 0, 0.06)), (hz(1.52), A.oct8(0.012, 0.012, 0.6, 0, 0.11))]
    parts.append(A.loft("Hood", hood, "ink", OX))
    for s, x in (("L", HW * 0.36), ("R", -HW * 0.36)):
        parts.append(A.box("Eye" + s, (HW * 0.36, 0.02, HW * 0.2), (x, -HW * 0.95 - 0.004, hz(0.5)), "glow_cyan", OX,
                           rot=(0, 0.18 if x > 0 else -0.18, 0)))

    # boots: broad iron_d boots with an iron sole
    for s, sx in (("L", 1), ("R", -1)):
        x = p["LX"] * sx
        b = A.loft("Boot" + s, [(0.0, A.oct8(0.10, 0.17, 0.7, x, -0.05)), (0.07, A.oct8(0.095, 0.155, 0.7, x, -0.04)),
                                (0.12, A.oct8(0.088, 0.09, 0.7, x, 0.0))], "iron_d", OX)
        A.paint(b, "iron", lambda c, n: c.z < 0.03, OX)
        parts.append(b)

    # the executioner's cleaver in the right fist: handle forward and down, the slab rising above it
    hx, hy, hzz = a["hand_r"]
    f = G.frame("Cleaver", (hx, hy, hzz), OX, rot=(-70, 0, 0))
    parts.append(f)
    parts.append(G.child_box("Handle", (0.05, 0.05, 0.34), (0, 0, 0.08), "wood_dark", f, pivot=(0, 0, 1)))
    parts.append(G.child_box("Ferrule", (0.075, 0.075, 0.06), (0, 0, -0.22), "iron_d", f))
    parts.append(G.blade("Blade", [(0.03, -0.23), (0.03, -0.93), (-0.36, -0.93), (-0.36, -0.30), (-0.29, -0.23)],
                         0.065, f, "steel"))
    parts.append(G.child_box("Edge", (0.07, 0.05, 0.66), (0, -0.345, -0.27), "steel_l", f, pivot=(0, 0, 1)))
    parts.append(G.child_box("Spine", (0.085, 0.045, 0.70), (0, 0.03, -0.23), "iron_d", f, pivot=(0, 0, 1)))
    parts.append(G.child_box("Hole", (0.07, 0.09, 0.09), (0, -0.10, -0.80), "ink", f))

    G.grow(parts, OX, SCALE)
    return parts


parts = build()
result = A.review("murder_machine", parts, original_blend="ch_murdermachine", original_root="CH_MurderMachine",
                  height=2.3, gap=GAP)
