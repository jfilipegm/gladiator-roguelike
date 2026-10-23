"""A1 roster — the Chimera in the simple style (the Colosseum's fire-breathing beast; built at real size: ~1.5 m at
the shoulder, ~2.6 m long, the snake's head ~2.1 m up).

Identity kept from en_chimera.py: a big dark lion (dark fur, a heavy charcoal mane with spikes, jaws open on a
fire-orange throat: it breathes fire, yellow eyes, leather-brown chest and haunch, dark paws), the magenta harness
round its barrel (the enemy accent); from its back a goat's neck rises on its left with a pale goat head, a beard and
horns curling back; its tail is a slate snake arching up over the rump on its right with its own head facing forward,
so the three heads show from the front, the side and 3/4. In the simple style: chamfered lofts along the body (built
up Z and turned to run along -Y), hex-section legs, plain tapered horns and spikes, flat faces.
"""
import importlib
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier3boss_lib as L
importlib.reload(L)

GAP = 2.2
OX = -GAP / 2


def along(name, rows, swatch, cap_bottom=True, cap_top=True):
    """A loft along the body: rows (s, half-width, half-height, height[, x]) from the rump (s small) toward the head
    (s large = further to the front, -Y)."""
    o = L.ring_rows(name, [(r[0], r[1], r[2], r[4] if len(r) > 4 else 0.0, r[3]) for r in rows], swatch, cap_bottom,
                    cap_top)
    return L.horizontal(o)


def build():
    K.clear_scene()
    parts = []
    # the barrel: haunch, belly, deep chest; leather-brown chest and haunch faces, the magenta harness
    body = along("Body", [(-0.88, 0.24, 0.24, 1.0), (-0.62, 0.34, 0.34, 1.02), (0.0, 0.34, 0.33, 0.98),
                          (0.48, 0.41, 0.44, 1.08), (0.74, 0.34, 0.36, 1.16)], "mud_dark")
    A.paint(body, "leather_d", lambda c, n: (c.y < -0.4 and n.y < -0.2) or (c.z < 0.85 and n.z < -0.5))
    parts.append(body)
    parts.append(along("Harness", [(-0.08, 0.375, 0.365, 0.98), (0.08, 0.385, 0.375, 0.99)], "accent_magenta",
                       False, False))
    # legs: heavy lion legs (hex sections), dark paws
    for s, sx in (("L", 1), ("R", -1)):
        x = 0.24 * sx
        parts.append(A.loft("Arm" + s, [(0.08, A.hex6(0.12, 0.13, x, -0.5)), (0.6, A.hex6(0.13, 0.14, x, -0.48)),
                                        (1.05, A.hex6(0.15, 0.17, x, -0.44))], "leather_d", cap_top=False))
        parts.append(A.box("Paw" + s, (0.24, 0.3, 0.1), (x, -0.56, 0.0), "charcoal", pivot=(0, 0, -1), taper=(0.9, 0.8)))
        parts.append(A.loft("Leg" + s, [(0.08, A.hex6(0.12, 0.13, x * 0.95, 0.5)), (0.5, A.hex6(0.14, 0.16, x * 0.95, 0.56)),
                                        (1.05, A.hex6(0.17, 0.22, x * 0.95, 0.56))], "leather_d", cap_top=False))
        parts.append(A.box("Foot" + s, (0.23, 0.3, 0.1), (x * 0.95, 0.44, 0.0), "charcoal", pivot=(0, 0, -1),
                           taper=(0.9, 0.8)))
    # the mane: a deep charcoal ruff round the neck, spikes all round but under the jaw
    parts.append(along("Mane", [(0.6, 0.46, 0.5, 1.28), (0.92, 0.48, 0.5, 1.36), (1.04, 0.3, 0.32, 1.38)], "charcoal"))
    for k in range(9):
        a_ = math.radians(-130 + k * 32.5)
        d = Vector((math.sin(a_) * 0.9, 0.35, math.cos(a_)))
        base = Vector((math.sin(a_) * 0.4, -0.86, 1.36 + math.cos(a_) * 0.42))
        parts.append(L.tip("Spike%d" % k, base, 0.2, 0.09, "charcoal", direction=d))
    # the lion's head: skull, snout, the jaw hanging open on a fire-orange throat, fangs, nose, yellow eyes, ears
    head = along("Head", [(0.96, 0.24, 0.23, 1.38), (1.18, 0.24, 0.22, 1.4), (1.3, 0.16, 0.13, 1.44),
                          (1.42, 0.13, 0.1, 1.45)], "mud_dark")
    A.paint(head, "leather_d", lambda c, n: c.y < -1.22)
    parts.append(head)
    jaw = A.box("Jaw", (0.22, 0.32, 0.06), (0, -1.12, 1.24), "leather_d", pivot=(0, 1, 1), rot=(0.4, 0, 0))
    parts.append(jaw)
    parts.append(A.box("Throat", (0.22, 0.24, 0.1), (0, -1.28, 1.22), "fire_mid", rot=(0.22, 0, 0)))
    parts.append(A.box("Nose", (0.11, 0.04, 0.07), (0, -1.45, 1.5), "ink"))
    for s, x in (("L", 1), ("R", -1)):
        parts.append(L.tip("Fang" + s, (x * 0.075, -1.4, 1.36), 0.1, 0.025, "bone", direction=(0, 0, -1)))
        parts.append(A.box("Eye" + s, (0.07, 0.03, 0.045), (x * 0.12, -1.19, 1.5), "hazard_yellow", rot=(0, x * 0.2, 0)))
        parts.append(A.box("Ear" + s, (0.09, 0.05, 0.12), (x * 0.2, -1.0, 1.58), "mud_dark", pivot=(0, 0, -1),
                           taper=(0.3, 0.5)))
    # the goat: a neck rising from the back on its left, a pale head looking forward and down, a beard, curled horns
    gb, gt = Vector((0.17, -0.18, 1.3)), Vector((0.27, -0.42, 1.98))
    parts.append(L.rod("GoatNeck", gb, gt, 0.085, "ash", sides=6, taper=0.85))
    gd = Vector((0.1, -1.0, -0.45)).normalized()
    skull = L.ring_rows("GoatSkull", [(0.0, 0.1, 0.11), (0.18, 0.09, 0.1), (0.3, 0.06, 0.07)], "bone")
    skull.data.transform(Matrix.Translation(gt + Vector((0, 0.04, 0.06))) @ L._aim(gd))
    parts.append(skull)
    tipc = gt + Vector((0, 0.04, 0.06)) + gd * 0.3
    parts.append(A.box("Muzzle", (0.1, 0.06, 0.08), tipc, "ash"))
    parts.append(A.box("Beard", (0.06, 0.04, 0.13), tipc + Vector((0, 0.03, -0.04)), "ash", pivot=(0, 0, 1),
                       taper=(1.6, 1.0)))
    for s, x in (("L", 1), ("R", -1)):
        parts.append(A.box("GoatEye" + s, (0.03, 0.04, 0.035), gt + Vector((x * 0.1, -0.1, 0.11)), "hazard_yellow"))
        h0 = gt + Vector((x * 0.07, -0.03, 0.17))
        h1 = h0 + Vector((x * 0.08, 0.1, 0.16))
        h2 = h1 + Vector((x * 0.12, 0.17, -0.02))
        h3 = h2 + Vector((x * 0.07, 0.04, -0.15))
        parts.append(L.rod("Horn%s0" % s, h0, h1, 0.04, "bone", taper=0.85))
        parts.append(L.rod("Horn%s1" % s, h1, h2, 0.034, "ash", taper=0.8))
        parts.append(L.tip("Horn%s2" % s, h2, (h3 - h2).length, 0.028, "bone", direction=h3 - h2))
    # the snake tail: from the rump, back and up, arching over it on the right, its head facing forward
    pts = [Vector(q) for q in ((-0.04, 0.82, 1.1), (-0.08, 1.12, 1.3), (-0.15, 1.3, 1.62), (-0.21, 1.26, 1.95),
                               (-0.25, 1.06, 2.1))]
    for k in range(4):
        parts.append(L.rod("Tail%d" % k, pts[k], pts[k + 1] + (pts[k + 1] - pts[k]).normalized() * 0.04,
                           0.09 - 0.012 * k, "slate" if k % 2 == 0 else "iron", sides=6, taper=0.88))
    sh = pts[4]
    sk = L.ring_rows("SnakeSkull", [(0.0, 0.07, 0.055), (0.16, 0.085, 0.06), (0.25, 0.05, 0.04)], "slate")
    sd = Vector((0, -1.0, -0.2)).normalized()
    sk.data.transform(Matrix.Translation(sh) @ L._aim(sd))
    parts.append(sk)
    parts.append(A.box("SnakeJaw", (0.1, 0.16, 0.03), sh + Vector((0, -0.12, -0.08)), "iron", rot=(-0.3, 0, 0)))
    parts.append(L.tip("SnakeFang", sh + Vector((0, -0.2, -0.05)), 0.07, 0.016, "bone", direction=(0, 0, -1)))
    for s, x in (("L", 1), ("R", -1)):
        parts.append(A.box("SnakeEye" + s, (0.03, 0.04, 0.03), sh + Vector((x * 0.075, -0.12, 0.05)), "hazard_yellow"))
    return parts


parts = build()
L.finish(parts, 1.0, OX)
result = A.review("chimera", parts, original_blend="en_chimera", original_root="EN_Chimera", height=2.4, gap=GAP)
