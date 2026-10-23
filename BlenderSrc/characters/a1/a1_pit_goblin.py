"""A1 roster — the Pit Goblin in the simple style (pattern: a1_nobody.py).

Identity kept from ch_pitgoblin.py: tiny (~1.05 m against the Nobody's 1.6) with a big head (~40 %), green skin,
huge triangular ears swept out and up, a long nose, a toothy grin and yellow eyes, the head pushed forward (the
hunch), a pot belly, a sackcloth rag on a rope belt, long thin arms with sack wraps and big hands, short bowed legs
and big flat feet, the bulging satchel on his back towering over his head (a leather flap, junk poking out of it:
a stick, a sword hilt, a bomb), the cyan headband and knot and the cyan rag round the satchel (the player's mark
from behind), and a bomb in the right hand.
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

OX = -0.55
HEAD_SHIFT = (0.0, -0.045, -0.02)   # the hunch: the head sits forward and a little low


def build():
    K.clear_scene()
    p = dict(H=0.24, S=0.54, SX=0.165, N=0.60, HH=0.42, HW=0.245, TW=0.15, LR=0.066, LX=0.085, AR=0.046, AL=0.33,
             HS=0.068, FL=0.24)
    h = A.humanoid(OX, p=p, skin="skin_goblin", skin_shade="grass_green", top=None, belt=None, hair_style="none",
                   face=False, skip=("Leg", "Foot", "Strap"))
    parts, a = h["parts"], h["a"]
    H, Sz, N, HW, TW = p["H"], p["S"], p["N"], p["HW"], p["TW"]
    hz = a["hz"]
    head = [o for o in parts if o.name == "Head"]
    body = [o for o in parts if o.name != "Head"]

    # the pot belly, the rope belt and the sackcloth rag
    wz = a["waist_z"]
    body.append(A.loft("Belly", [(wz - 0.10, A.oct8(0.115, 0.07, 0.62, 0, -0.035)),
                                 (wz - 0.04, A.oct8(0.135, 0.09, 0.62, 0, -0.055)),
                                 (wz + 0.05, A.oct8(0.11, 0.07, 0.62, 0, -0.035))], "skin_goblin", OX))
    body.append(A.loft("Rope", [(H - 0.005, A.oct8(TW * 1.0, TW * 0.74)), (H + 0.04, A.oct8(TW * 1.0, TW * 0.74))],
                       "rope", OX, False, False))
    rag = A.loft("Rag", [(H - 0.12, A.oct8(TW * 1.16, TW * 0.86)), (H - 0.06, A.oct8(TW * 1.06, TW * 0.8)),
                         (H + 0.01, A.oct8(TW * 0.97, TW * 0.72))], "cloth_sack", OX, cap_bottom=False, cap_top=False)
    A.paint(rag, "dirt_light", lambda c, n: c.z < H - 0.08 and abs(c.x) > 0.06, OX)
    body.append(rag)

    # short bowed legs with sack knee wraps, big flat feet
    for s, sx in (("L", 1), ("R", -1)):
        x = p["LX"] * sx
        body.append(G.tube("Leg" + s, [(x, 0.0, 0.05), (x * 1.32, 0.0, H * 0.5), (x, 0.0, H + 0.01)],
                           [p["LR"] * 0.7, p["LR"] * 0.8, p["LR"]], "skin_goblin", OX, sides=6))
        body.append(A.loft("Knee" + s, [(H * 0.4, A.hex6(p["LR"] * 0.98, p["LR"] * 0.98, x * 1.3)),
                                        (H * 0.62, A.hex6(p["LR"] * 1.0, p["LR"] * 1.0, x * 1.28))],
                           "cloth_sack", OX, False, False))
        body.append(A.loft("Foot" + s, [(0.0, A.oct8(0.075, 0.13, 0.7, x * 1.08, -0.06)),
                                        (0.045, A.oct8(0.068, 0.12, 0.7, x * 1.08, -0.055)),
                                        (0.08, A.oct8(0.05, 0.05, 0.7, x, 0.0))], "skin_goblin", OX))
    # sack wraps on the wrists
    wr = Sz - p["AL"]
    for s, sx in (("L", 1), ("R", -1)):
        x = (p["SX"] + 0.02) * sx
        body.append(A.loft("Wrap" + s, [(wr + 0.03, A.hex6(p["AR"] * 0.95, p["AR"] * 0.93, x, -0.015)),
                                        (wr + 0.09, A.hex6(p["AR"] * 1.0, p["AR"] * 0.98, x, -0.013))],
                           "cloth_sack", OX, False, False))
    # satchel straps down the chest
    for s, x in (("L", 0.07), ("R", -0.07)):
        body.append(A.box("Strap" + s, (0.035, 0.02, Sz - wz + 0.04), (x, -TW * 0.66 - 0.006, wz - 0.02), "leather_d",
                          OX, pivot=(0, 0, -1), rot=(0.06, 0, 0)))

    # the satchel: a bulging sack on the back, leaning back so it clears the head and towers over it
    sack = [(H - 0.04, A.oct8(0.15, 0.10, 0.66, 0, 0.20)), (H + 0.14, A.oct8(0.20, 0.14, 0.64, 0, 0.235)),
            (N + 0.10, A.oct8(0.215, 0.15, 0.64, 0, 0.30)), (N + 0.38, A.oct8(0.19, 0.135, 0.62, 0, 0.35)),
            (N + 0.47, A.oct8(0.15, 0.11, 0.62, 0, 0.36))]
    body.append(A.loft("Satchel", sack, "cloth_sack", OX))
    body.append(A.loft("SatchelFlap", [(N + 0.40, A.oct8(0.195, 0.14, 0.62, 0, 0.352)),
                                       (N + 0.49, A.oct8(0.155, 0.115, 0.62, 0, 0.362))], "leather_l", OX,
                       cap_bottom=False))
    body.append(A.loft("SatchelRag", [(N + 0.04, A.oct8(0.224, 0.158, 0.64, 0, 0.29)),
                                      (N + 0.12, A.oct8(0.222, 0.156, 0.64, 0, 0.302))], "player_cyan", OX,
                       False, False))
    body.append(A.box("RagTail", (0.08, 0.02, 0.22), (0.12, 0.45, N + 0.09), "player_cyan", OX, pivot=(0, 0, 1),
                      rot=(0.12, 0.1, 0), taper=(0.6, 1.0)))
    top = N + 0.47
    body.append(G.tube("JunkStick", [(-0.08, 0.36, top - 0.05), (-0.16, 0.40, top + 0.30)], [0.022, 0.018],
                       "wood_light", OX, sides=5))
    body.append(G.tube("JunkHilt", [(0.07, 0.34, top - 0.05), (0.13, 0.32, top + 0.16)], [0.025, 0.025],
                       "wood_dark", OX, sides=5))
    body.append(A.box("JunkGuard", (0.15, 0.04, 0.035), (0.10, 0.33, top + 0.07), "rusty_iron", OX, rot=(0, -0.4, 0)))
    body.append(G.ball("JunkBomb", 0.075, (0.0, 0.38, top + 0.03), "iron_d", OX, n=6))

    # the face: yellow eyes with pupils, leaf brows, a long nose, a toothy grin
    fy = -HW * 0.93
    for s, x in (("L", 1), ("R", -1)):
        head.append(A.box("Eye" + s, (0.085, 0.02, 0.065), (x * 0.095, fy - 0.003, hz(0.5)), "straw", OX))
        head.append(A.box("Pupil" + s, (0.032, 0.02, 0.045), (x * 0.08, fy - 0.009, hz(0.5)), "ink", OX))
        head.append(A.box("Brow" + s, (0.11, 0.03, 0.03), (x * 0.1, fy - 0.006, hz(0.64)), "leaf_d", OX,
                          rot=(0, -0.35 * x, 0)))
    head.append(G.tube("Nose", [(0, fy + 0.02, hz(0.42)), (0, fy - 0.10, hz(0.32)), (0, fy - 0.17, hz(0.25))],
                       [0.04, 0.026, 0.0], "grass_green", OX, sides=5))
    head.append(A.box("Grin", (0.21, 0.02, 0.05), (0, fy - 0.002, hz(0.16)), "ink", OX))
    for k, x in enumerate((-0.065, 0.0, 0.065)):
        head.append(A.box("Tooth%d" % k, (0.03, 0.02, 0.035), (x, fy - 0.008, hz(0.19)), "bone", OX))
    # huge ears swept out and up
    for s, sx in (("L", 1), ("R", -1)):
        ear = [(0.0, -0.06), (0.0, 0.08), (sx * 0.30, 0.17)]
        head.append(G.panel("Ear" + s, ear, 0.04, (sx * HW * 0.92, 0.02, hz(0.5)), "skin_goblin", OX, yaw=sx * 14))
        inner = [(sx * 0.02, -0.025), (sx * 0.02, 0.05), (sx * 0.21, 0.12)]
        head.append(G.panel("EarIn" + s, inner, 0.02, (sx * HW * 0.92, 0.02, hz(0.5)), "skin_tan", OX, yaw=sx * 14,
                            dy=-0.022))
    # the cyan headband and its knot at the back
    head += A.headband(a, "player_cyan", OX, tails=False)
    head.append(A.box("HeadbandKnot", (0.07, 0.05, 0.10), (-0.08, a["head_back_y"] + 0.02, a["band_z"][1]),
                      "player_cyan", OX, pivot=(0, 0, 1), taper=(0.6, 0.6)))
    G.move(head, HEAD_SHIFT)

    # the bomb in the right hand, its fuse up
    hx, hy, hzz = a["hand_r"]
    bz = hzz - 0.075
    body.append(G.ball("BombBall", 0.085, (hx, hy - 0.04, bz), "iron_d", OX, n=6))
    body.append(A.loft("BombCap", [(bz + 0.07, A.ngon(6, 0.03, hx, hy - 0.04)),
                                   (bz + 0.11, A.ngon(6, 0.03, hx, hy - 0.04))], "bronze", OX))
    body.append(A.box("BombFuse", (0.016, 0.016, 0.06), (hx, hy - 0.04, bz + 0.11), "rope", OX, pivot=(0, 0, -1)))
    return head + body


parts = build()
result = A.review("pit_goblin", parts, original_blend="ch_pitgoblin", original_root="CH_PitGoblin", height=1.4)
