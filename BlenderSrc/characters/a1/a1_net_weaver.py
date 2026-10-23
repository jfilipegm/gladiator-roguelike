"""A1 roster — the Net-Weaver in the simple style (pattern: a1_nobody.py).

Identity kept from ch_netweaver.py: a retiarius, bare-chested with a sea-blue tunic skirt and a wide leather belt with
a bronze plate, the steel galerus standing up from his left shoulder (flaring, a dark bronze rim) on a leather strap,
the rope net hung down his back like a cape with cyan cords and lead weights along its hem (his mark from behind),
dark hair with a topknot, a cyan headband with its tails behind, glowing cyan eyes, a dark chin beard, the manica
(cloth and leather bands) wrapping the left arm, a leather bracer on the right, white shin wraps and sandals, and the
trident slanted forward, its three steel prongs ahead of him at head height with a cyan binding.
"""
import importlib
import math
import os
import sys

from mathutils import Vector

import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import a1_glad_lib as G
importlib.reload(G)

OX = -0.6


def build():
    K.clear_scene()
    h = A.humanoid(OX, p=dict(TW=0.205, SX=0.27, AR=0.07), skin="skin_mid", skin_shade="skin_tan", top=None,
                   belt=None, foot="skin_mid", sole="leather_d", hair="charcoal", eyes="glow_cyan", brows="charcoal")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, HW, AR = p["H"], p["S"], p["N"], p["TW"], p["HW"], p["AR"]
    hz = a["hz"]
    ry = a["head_back_y"]
    by_name = {o.name: o for o in parts}
    # the sea-blue skirt, the wide belt and its bronze plate
    parts.append(A.loft("Skirt", [(H - 0.16, A.oct8(TW * 1.2, TW * 0.9)), (H + 0.06, A.oct8(TW * 0.98, TW * 0.72))],
                        "water", OX, cap_bottom=False, cap_top=False))
    parts.append(A.loft("Belt", [(H + 0.03, A.oct8(TW * 1.0, TW * 0.74)), (H + 0.12, A.oct8(TW * 0.95, TW * 0.70))],
                        "leather_d", OX, False, False))
    parts.append(A.box("Buckle", (0.10, 0.03, 0.07), (0, -TW * 0.74 - 0.005, H + 0.075), "bronze_l", OX))
    # white shin wraps; the manica on the left arm; a leather bracer on the right
    for s in ("L", "R"):
        A.paint(by_name["Leg" + s], "cloth_white", lambda c, n: 0.1 < c.z < H * 0.55, OX)
    wr = Sz - p["AL"]
    xl = p["SX"] + 0.02
    for k, (z0, sw) in enumerate(((wr + 0.02, "cloth_white"), (wr + 0.10, "leather_l"), (wr + 0.18, "cloth_white"),
                                  (wr + 0.26, "leather_l"))):
        f = 0.9 + 0.12 * k / 3
        parts.append(A.loft("Manica%d" % k, [(z0, A.hex6(AR * f * 1.08, AR * f * 1.06, xl - 0.002 * k, -0.014 + 0.002 * k)),
                                             (z0 + 0.075, A.hex6(AR * f * 1.1, AR * f * 1.08, xl - 0.002 * k, -0.013 + 0.002 * k))],
                            sw, OX, False, False))
    parts.append(A.loft("BracerR", [(wr + 0.03, A.hex6(AR * 1.0, AR * 0.98, -xl, -0.014)),
                                    (wr + 0.14, A.hex6(AR * 1.06, AR * 1.04, -xl, -0.011))], "leather", OX, False, False))

    # the head: the cyan headband with its tails, a topknot, a chin beard
    parts += A.headband(a, "player_cyan", OX)
    parts.append(A.loft("Topknot", [(hz(1.0), A.oct8(0.07, 0.07, 0.6, 0, 0.03)), (hz(1.17), A.oct8(0.05, 0.05, 0.6, 0, 0.04))],
                        "charcoal", OX))
    parts.append(G.panel("Beard", [(-0.07, 0.0), (0.07, 0.0), (0.05, -0.08), (-0.05, -0.08)], 0.04,
                         (0, -ry * 0.93 + 0.004, hz(0.17)), "charcoal", OX))

    # the galerus: a steel plate standing up from the left shoulder, flaring, leaning out; its dark bronze rim
    sl = a["shoulder_l"]
    gal = K.extrude_poly("Galerus", [(-0.13, 0.0), (0.13, 0.0), (0.22, 0.46), (-0.22, 0.46)], thickness=0.06,
                         plane="YZ", location=(sl[0] + OX - 0.02, 0.0, sl[2] - 0.03), swatch="steel_l")
    K.set_swatch_where(gal, "bronze_d", lambda c, n: c.z > 0.42 or n.z > 0.5)
    gal.rotation_euler = (0, math.radians(20), 0)
    parts.append(A.flat(gal))
    strap = [(-0.035, 0.29), (0.035, 0.29), (0.035, -0.27), (-0.035, -0.27)]
    for nm, y in (("StrapFront", -TW * 0.66 - 0.012), ("StrapBack", TW * 0.66 + 0.012)):
        parts.append(G.panel(nm, strap, 0.016, (0.0, y, a["chest_z"] - 0.04), "leather", OX, roll=36.0))

    # the net down his back: rope, cyan cords, lead weights along its hem
    top, wt, wb, L, tilt, bend = (0.02, TW * 0.66 + 0.035, Sz + 0.02), 0.40, 0.52, 0.60, 10.0, 0.05
    parts.append(G.cape("Net", wt, wb, L, top, "rope", OX, cols=6, depth=0.0, tilt=tilt, bend=bend, thickness=0.025))

    def on(f, d):
        return G.on_cape(top, wt, wb, L, f, d, tilt, bend, 0.025, 0.006)
    for k, f in enumerate((-0.55, 0.0, 0.55)):
        parts.append(G.tube("CordV%d" % k, [on(f, 0.02), on(f, L - 0.02)], [0.013, 0.013], "player_cyan", OX, sides=4))
    for k, d in enumerate((0.20, 0.42)):
        parts.append(G.tube("CordH%d" % k, [on(f / 2.0, d) for f in (-1.9, -1, 0, 1, 1.9)], [0.013] * 5, "player_cyan",
                            OX, sides=4))
    for k, f in enumerate((-0.85, -0.3, 0.3, 0.85)):
        parts.append(A.box("Weight%d" % k, (0.06, 0.05, 0.06), on(f, L), "iron", OX, rot=(math.radians(tilt), 0, 0.785)))

    # the trident, slanted forward, the prongs ahead of him at head height
    hx, hy, hzz = a["hand_r"]
    hand = Vector((hx, hy, hzz))
    d = Vector((0.0, -math.sin(math.radians(40)), math.cos(math.radians(40))))
    parts.append(G.tube("Shaft", [tuple(hand - d * 0.42), tuple(hand + d * 1.08)], [0.022, 0.022], "wood", OX, sides=6))
    parts.append(G.tube("Binding", [tuple(hand + d * 0.98), tuple(hand + d * 1.06)], [0.034, 0.034], "glow_cyan", OX,
                        sides=6))
    bar = hand + d * 1.09
    parts.append(A.box("Bar", (0.30, 0.045, 0.045), tuple(bar), "steel", OX, rot=(math.radians(40), 0, 0)))
    for k, x in enumerate((-0.13, 0.0, 0.13)):
        hgt = 0.36 if x == 0.0 else 0.28
        b = bar + Vector((x, 0, 0))
        parts.append(G.tube("Prong%d" % k, [tuple(b), tuple(b + d * hgt), tuple(b + d * (hgt + 0.09))],
                            [0.018, 0.018, 0.0], "steel_l", OX, sides=4))
    return parts


parts = build()
result = A.review("net_weaver", parts, original_blend="ch_netweaver", original_root="CH_NetWeaver", height=1.9, gap=1.2)
