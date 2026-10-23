"""A1 roster — the War Boar (EN_WarBoar, the Stone Amphitheater's cage beast, elite-class) in the simple style.

Identity kept from en_warboar.py: a low, wide, heavy boar (a shoulder hump, twice the Pit Cur's bulk), near-black
hide with a bristling mud-brown mane along the spine, a big wedge head held low to gore with a pale snout disc, long
bone tusks curving up, small glaring red eyes (angry brows, like the other enemies), the magenta war harness round
the barrel (the enemy accent; now with two iron studs), ink hooves, a short tail. Changed: chibi, so a big chamfered
head; oct8 sections lofted along Y with a slate belly; the mane a saw-tooth crest.
"""
import importlib
import math
import os
import sys

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier12_lib as L
importlib.reload(L)

GAP = 1.7
OX = -GAP / 2


def build():
    K.clear_scene()
    parts = []
    add = parts.append
    o8 = L.oct8
    # the barrel: a high hump at the shoulders sloping down to the rump; slate belly
    b = L.loft_y("Body", [(-0.42, o8(0.22, 0.22, 0.62, 0, 0.62)), (-0.27, o8(0.31, 0.31, 0.62, 0, 0.65)),
                          (0.06, o8(0.29, 0.27, 0.62, 0, 0.60)), (0.38, o8(0.25, 0.23, 0.62, 0, 0.55)),
                          (0.50, o8(0.16, 0.15, 0.62, 0, 0.56))], "charcoal", OX)
    L.paint(b, "slate", lambda c, n: n.z < -0.6, OX)
    add(b)
    # the mane: a saw-tooth crest of bristles along the spine, tallest over the hump
    teeth = [(-0.44, 0.0), (0.30, 0.0), (0.30, 0.05), (0.18, 0.04), (0.10, 0.13), (0.0, 0.07), (-0.10, 0.19),
             (-0.20, 0.10), (-0.30, 0.22), (-0.38, 0.11), (-0.46, 0.16)]
    mane = K.extrude_poly("Mane", teeth, thickness=0.11, plane="YZ", swatch="mud_dark", location=(OX, 0.0, 0.9))
    add(L.flat(mane))
    # the magenta war harness round the barrel, two iron studs on top
    # (its front ring is smaller, so it frames the barrel from the front like the original's harness block)
    add(L.loft_y("Harness", [(-0.2, o8(0.30, 0.29, 0.62, 0, 0.64)), (-0.15, o8(0.35, 0.345, 0.62, 0, 0.64)),
                             (0.02, o8(0.335, 0.32, 0.62, 0, 0.62))], "accent_magenta", OX, False, False))
    for k, x in enumerate((0.1, -0.1)):
        add(L.spike("Stud%d" % k, 0.035, 0.07, (x, -0.08, 0.95), "steel", OX))
    # the head: big, low, a wedge; snout with a pale disc and nostrils; tusks; red eyes; ears
    hz = 0.5
    add(L.loft_y("Head", [(-0.80, o8(0.2, 0.17, 0.62, 0, hz + 0.02)), (-0.58, o8(0.25, 0.23, 0.62, 0, hz + 0.04)),
                          (-0.36, o8(0.21, 0.21, 0.62, 0, hz + 0.08))], "charcoal", OX, cap_back=False))
    sn = L.loft_y("Snout", [(-0.97, o8(0.12, 0.095, 0.6, 0, hz - 0.06)), (-0.78, o8(0.14, 0.11, 0.6, 0, hz - 0.04))],
                  "mud_dark", OX, cap_back=False)
    L.paint(sn, "skin_tan", lambda c, n: n.y < -0.9, OX)
    add(sn)
    for s, x, sg in (("L", 0.045, 1), ("R", -0.045, -1)):
        add(L.decal("Nostril" + s, 0.035, 0.05, (x, -0.974, hz - 0.06), "ink", OX))
        add(L.spike("Tusk" + s, 0.045, 0.32, (0.12 * sg, -0.9, hz - 0.11), "bone", OX, rot=(-0.45, 0.5 * sg, 0)))
        add(L.decal("Eye" + s, 0.06, 0.035, (0.11 * sg, -0.804, hz + 0.08), "glow_hazard_red", OX))
        add(L.decal("Brow" + s, 0.09, 0.025, (0.11 * sg, -0.808, hz + 0.125), "ink", OX, rot_y=-0.45 * sg,
                    ledge=0.015))
        add(L.spike("Ear" + s, 0.06, 0.16, (0.17 * sg, -0.48, hz + 0.22), "charcoal", OX, rot=(0.5, 0.75 * sg, 0)))
    # four short thick legs on ink hooves
    for s, x in (("L", 0.17), ("R", -0.17)):
        for end, y, r1 in (("Arm", -0.25, (0.085, 0.1)), ("Leg", 0.32, (0.09, 0.12))):
            add(L.loft(end + s, [(0.07, L.hex6(0.06, 0.068, x, y)), (0.5, L.hex6(r1[0], r1[1], x, y))], "charcoal", OX,
                       False, False))
            add(L.loft("Hoof" + s + end, [(0.0, L.hex6(0.07, 0.085, x, y - 0.015)),
                                           (0.09, L.hex6(0.062, 0.07, x, y - 0.01))], "ink", OX, cap_bottom=False))
    tail = L.loft("Tail", [(0.0, L.hex6(0.03, 0.03)), (0.18, L.hex6(0.014, 0.014))], "charcoal", 0.0, False, True)
    tail.rotation_euler = (-1.1, 0, 0)
    tail.location = (OX, 0.5, 0.64)
    add(tail)
    L.xform(parts, L.Matrix.Scale(1.15, 4), (0, 0, 0), OX)   # elite bulk, as big as the original
    return parts


parts = build()
result = A.review("war_boar", parts, original_blend="en_warboar", original_root="EN_WarBoar", height=1.2, gap=GAP)
