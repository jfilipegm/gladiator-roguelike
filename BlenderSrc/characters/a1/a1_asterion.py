"""A1 roster — Asterion, the Chained, in the simple style (pattern: a1_nobody.py).

Identity kept from ch_asterion.py: a chibi minotaur, tragic not monstrous: the white bull's pale hide (ash), a bone
chest, the charcoal mane on a shoulder hump, the head hung low and forward of it (a broad poll, a long bone snout,
ink nostrils, a bronze nose ring, sad drooping ink eyes, a charcoal forelock and drooping ears, lyre horns in bone
with charcoal tips, his right tip chipped short), pale scars, the Minoan-blue kilt with an ochre hem on a rope belt,
the bronze collar with a worn rope band and a broken ring, the bull's tail with a charcoal tuft, the cyan labyrinth
branded on his back (the player's mark from behind), long heavy arms with charcoal shoulder fur and iron cuffs (a
broken shackle link on the left, the bronze chain wound round the right forearm), short thick legs and ink hooves,
and the chain trailing from his right hand along the ground to the stone block torn from the labyrinth's wall
(stone grey, a bone meander on top, a bronze ring bolt, a lump of mortar). No fangs, no spikes, no red.
Built at the chibi scale with a heavy build, then scaled x1.1 (G.grow): horn tips ~1.8 m.
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

GAP = 1.6
OX = -GAP / 2
SCALE = 1.1


def build():
    K.clear_scene()
    p = dict(H=0.36, S=0.84, SX=0.36, N=0.92, TW=0.28, LR=0.112, LX=0.15, AR=0.108, AL=0.40, HS=0.095)
    h = A.humanoid(OX, p=p, skin="ash", skin_shade="ash", top=None, belt=None, hair_style="none", face=False,
                   skip=("Head", "Foot", "Strap"))
    parts, a = h["parts"], h["a"]
    H, Sz, N, TW, AR = p["H"], p["S"], p["N"], p["TW"], p["AR"]
    by_name = {o.name: o for o in parts}
    wr = Sz - p["AL"]

    # the bone chest, a pale scar across it
    parts.append(G.panel("Chest", [(-0.15, 0.0), (0.15, 0.0), (0.12, -0.22), (-0.12, -0.22)], 0.02,
                         (0, -TW * 0.66 - 0.006, Sz - 0.02), "bone", OX, tilt=-2.0))
    parts.append(A.box("ScarChest", (0.15, 0.012, 0.022), (-0.04, -TW * 0.66 - 0.02, Sz - 0.12), "white", OX,
                       rot=(0, -0.55, 0)))
    # the charcoal mane: a hump over the shoulders and the upper back
    parts.append(A.loft("Mane", [(Sz - 0.10, A.oct8(TW * 1.12, TW * 0.82, 0.64, 0, 0.03)),
                                 (Sz + 0.02, A.oct8(TW * 1.16, TW * 0.86, 0.64, 0, 0.04)),
                                 (Sz + 0.10, A.oct8(TW * 0.86, TW * 0.70, 0.62, 0, 0.05)),
                                 (N + 0.06, A.oct8(TW * 0.42, TW * 0.40, 0.6, 0, 0.06))],
                        "charcoal", OX, cap_bottom=False))
    # the kilt, its ochre hem, the rope belt
    kilt = A.loft("Kilt", [(H - 0.16, A.oct8(TW * 1.2, TW * 0.9)), (H - 0.11, A.oct8(TW * 1.16, TW * 0.87)),
                           (H + 0.05, A.oct8(TW * 1.0, TW * 0.74))], "cloth_blue", OX, cap_bottom=False, cap_top=False)
    A.paint(kilt, "ochre", lambda c, n: c.z < H - 0.11, OX)
    parts.append(kilt)
    parts.append(A.loft("Belt", [(H + 0.02, A.oct8(TW * 1.02, TW * 0.76)), (H + 0.07, A.oct8(TW * 1.02, TW * 0.76))],
                        "rope", OX, False, False))
    # the bronze collar, the rope band worn into it, the broken ring at his left
    parts.append(A.loft("Collar", [(N - 0.07, A.oct8(0.17, 0.15, 0.62, 0, -0.06)), (N + 0.03, A.oct8(0.16, 0.14, 0.62, 0, -0.05))],
                        "bronze", OX, False, False))
    parts.append(A.loft("CollarRope", [(N - 0.035, A.oct8(0.176, 0.156, 0.62, 0, -0.06)),
                                       (N - 0.005, A.oct8(0.172, 0.152, 0.62, 0, -0.055))], "rope", OX, False, False))
    parts.append(G.tube("CollarRing", [(0.10, -0.21, N - 0.06), (0.11, -0.22, N - 0.16), (0.16, -0.21, N - 0.18)],
                        [0.016, 0.016, 0.016], "bronze_l", OX, sides=4))
    # the tail with its charcoal tuft
    parts.append(G.tube("Tail", [(0, TW * 0.66, H + 0.06), (0, TW * 0.66 + 0.10, H - 0.04), (0.03, TW * 0.66 + 0.14, H - 0.2)],
                        [0.025, 0.022, 0.02], "ash", OX, sides=5))
    parts.append(G.tube("TailTuft", [(0.03, TW * 0.66 + 0.14, H - 0.18), (0.03, TW * 0.66 + 0.15, H - 0.26),
                                     (0.03, TW * 0.66 + 0.15, H - 0.34)], [0.025, 0.045, 0.0], "charcoal", OX, sides=5))
    # the cyan labyrinth branded on his back, under the mane
    by = TW * 0.66 + 0.006
    for k, (x, z, sx, sz) in enumerate(((-0.10, 0.62, 0.035, 0.22), (0.0, 0.715, 0.235, 0.035),
                                        (0.10, 0.62, 0.035, 0.22), (0.035, 0.525, 0.165, 0.035),
                                        (-0.03, 0.585, 0.035, 0.13))):
        parts.append(A.box("Labyrinth%d" % k, (sx, 0.016, sz), (x, by, z), "player_cyan", OX))

    # arms: charcoal fur on the shoulders, iron cuffs; a broken link on the left, the chain wound round the right
    for s, sx, arc in (("L", 1, (-120, 120)), ("R", -1, (60, 300))):
        sh = a["shoulder_l"] if sx > 0 else a["shoulder_r"]
        parts.append(A.flat(S.shell("ShoulderFur" + s, radius=(AR * 1.75, AR * 1.7), arc=arc, z=(-0.08, 0.07), taper=0.62,
                                    thickness=0.03, segments=3, location=(sh[0] + OX - 0.01 * sx, 0.0, sh[2]),
                                    swatch="charcoal", flare=0.02)))
        x = (p["SX"] + 0.02) * sx
        parts.append(A.loft("Cuff" + s, [(wr + 0.0, A.hex6(AR * 0.98, AR * 0.96, x, -0.015)),
                                         (wr + 0.08, A.hex6(AR * 1.02, AR * 1.0, x, -0.014))], "iron", OX, False, False))
    xl = p["SX"] + 0.02
    parts.append(A.box("BrokenLink", (0.03, 0.08, 0.10), (xl + 0.02, 0.05, wr + 0.01), "iron", OX, pivot=(0, 0, 1),
                       rot=(0.2, 0, 0.3)))
    xr = -(p["SX"] + 0.02)
    for k, z in enumerate((wr + 0.13, wr + 0.21)):
        parts.append(A.loft("ChainWrap%d" % k, [(z - 0.025, A.oct8(AR * 1.05, AR * 1.05, 0.6, xr, -0.012)),
                                                (z + 0.025, A.oct8(AR * 1.07, AR * 1.07, 0.6, xr, -0.006))],
                            "bronze" if k else "bronze_d", OX, False, False))

    # short thick legs, ink hooves
    for s, sx in (("L", 1), ("R", -1)):
        x = p["LX"] * sx
        parts.append(A.loft("Hoof" + s, [(0.0, A.oct8(0.092, 0.125, 0.7, x, -0.03)), (0.11, A.oct8(0.08, 0.09, 0.7, x, -0.005))],
                            "ink", OX))

    # the head, low and forward of the hump: a broad poll, a long bone snout, a nose ring, sad eyes, horns
    hy, hb = -0.11, N - 0.03
    skull = [(hb, A.oct8(0.21, 0.17, 0.64, 0, hy)), (hb + 0.29, A.oct8(0.27, 0.21, 0.62, 0, hy - 0.01)),
             (hb + 0.41, A.oct8(0.20, 0.16, 0.6, 0, hy))]
    parts.append(A.loft("Skull", skull, "ash", OX))
    sf = hy - 0.01 - 0.21  # the skull's front
    snout = [(0.0, A.oct8(0.19, 0.12, 0.62)), (0.22, A.oct8(0.16, 0.10, 0.6))]
    parts.append(G.hloft("Snout", snout, "bone", OX, (0, sf + 0.04, hb + 0.11)))
    tip = sf + 0.04 - 0.22
    for s, x in (("L", 0.07), ("R", -0.07)):
        parts.append(A.box("Nostril" + s, (0.06, 0.012, 0.04), (x, tip - 0.002, hb + 0.14), "ink", OX))
    parts.append(G.tube("NoseRing", [(0.05, tip + 0.02, hb + 0.07), (0.05, tip - 0.005, hb - 0.02),
                                     (-0.05, tip - 0.005, hb - 0.02), (-0.05, tip + 0.02, hb + 0.07)],
                        [0.016] * 4, "bronze_l", OX, sides=4))
    ez = hb + 0.27
    for s, x, droop in (("L", 0.135, 0.3), ("R", -0.135, -0.3)):
        parts.append(A.box("Eye" + s, (0.085, 0.014, 0.05), (x, sf - 0.004, ez), "ink", OX, rot=(0, droop, 0)))
    parts.append(A.box("ScarBrow", (0.02, 0.012, 0.14), (0.16, sf - 0.008, ez + 0.01), "white", OX, rot=(0, 0.2, 0)))
    parts.append(A.loft("Forelock", [(hb + 0.36, A.oct8(0.13, 0.08, 0.6, 0, sf + 0.06)),
                                     (hb + 0.47, A.oct8(0.07, 0.05, 0.6, 0, sf + 0.09))], "charcoal", OX))
    for s, sx in (("L", 1), ("R", -1)):
        ear = [(0.0, 0.03), (0.0, -0.04), (sx * 0.16, -0.06), (sx * 0.19, -0.02)]
        parts.append(G.panel("Ear" + s, ear, 0.035, (sx * 0.25, hy - 0.02, hb + 0.27), "charcoal", OX, yaw=-sx * 10))
        # lyre horns: out from the poll, up and forward, the tips flaring out; his right tip chipped short
        base = (sx * 0.20, hy - 0.02, hb + 0.37)
        mid1 = (sx * 0.38, hy - 0.06, hb + 0.43)
        mid2 = (sx * 0.45, hy - 0.12, hb + 0.62)
        parts.append(G.tube("Horn" + s, [base, mid1, mid2], [0.07, 0.06, 0.048], "bone", OX, sides=6, cap_end=False))
        end = (sx * 0.56, hy - 0.15, hb + 0.70) if sx > 0 else (sx * 0.495, hy - 0.135, hb + 0.66)
        parts.append(G.tube("HornTip" + s, [mid2, end], [0.048, 0.0 if sx > 0 else 0.03], "charcoal", OX, sides=6,
                            cap_start=False))

    # the chain from the right hand down to the ground and along it, back to the stone block
    hx, hyy, hz_ = a["hand_r"]
    pts = [(hx, hyy + 0.09, wr - 0.02), (hx - 0.02, 0.22, 0.20), (hx - 0.03, 0.36, 0.035), (hx - 0.05, 0.95, 0.035)]
    parts += G.chain("Link", pts, "bronze", OX, pitch=0.185, size=(0.04, 0.10, 0.21), alt="bronze_d")
    sy, sw, sh_, sl = 1.18, 0.36, 0.34, 0.40
    sx0 = hx - 0.05
    stone = A.loft("Stone", [(0.0, A.oct8(sw / 2, sl / 2, 0.72, sx0, sy)), (sh_, A.oct8(sw / 2, sl / 2, 0.72, sx0, sy))],
                   "stone_grey", OX)
    parts.append(stone)
    parts.append(A.box("RingBolt", (0.12, 0.10, 0.035), (sx0, sy - sl / 2 - 0.03, sh_ * 0.5), "bronze", OX))
    for k, (x, y, w, d) in enumerate(((0.0, 0.11, 0.25, 0.04), (0.11, -0.02, 0.04, 0.22))):
        parts.append(A.box("Key%d" % k, (w, d, 0.02), (sx0 + x, sy + y, sh_ + 0.005), "bone", OX))

    G.grow(parts, OX, SCALE)
    return parts


parts = build()
result = A.review("asterion", parts, original_blend="ch_asterion", original_root="CH_Asterion", height=2.0, gap=GAP)
