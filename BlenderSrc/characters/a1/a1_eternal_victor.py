"""A1 roster — the Eternal Victor in the simple style (the Colosseum's boss, docs/BOSSES.md; ~3.6 m to the crest).

Identity kept from en_eternalvictor.py: the arena's first champion, four hundred years dead: tall and gaunt (the
tallest, thinnest boss: the kit body stretched, longer legs and a narrow frame), bone limbs in archaic bronze: a
bronze cuirass over a wasp waist where the bare spine shows, tall flared bronze greaves over bone shins, one great
flared shoulder guard (left) over layered plates, a segmented manica on the right forearm; a thraex helm with a wide
brim tipped up at the front, a visor with darkness behind its grille and a bone jaw under it, a gold laurel wreath,
and the griffin crest curling forward like a hook; a cloak of tattered crimson victory ribbons down his back; a
crimson kilt with bronze straps, the magenta sash (the enemy accent) with its end hanging; and the falx, a long
two-handed crescent held low across his body, right fist at the pommel, left fist lower on the haft, the blade
rising beside his left leg. The hook of the crest and the crescent of the blade are his read.
Built at kit size (stretched), then scaled x1.85 about the feet.
"""
import importlib
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
import a1_tier3boss_lib as L
importlib.reload(L)

SCALE = 1.85
GAP = 2.6
OX = -GAP / 2
P = dict(H=0.5, S=0.98, N=1.07, SX=0.24, TW=0.16, AR=0.042, LR=0.048, LX=0.1, HS=0.06, AL=0.4, HW=0.25)
UPPER, FORE = 0.25, 0.25
GRIP_R = Vector((-0.1, -0.2, 0.6))       # right fist at the pommel end of the haft
GRIP_L = Vector((0.14, -0.22, 0.5))      # left fist lower on the haft
HOOK = [(0.24, -0.04), (0.27, 0.16), (0.2, 0.36), (0.06, 0.5), (-0.14, 0.58), (-0.36, 0.57), (-0.52, 0.48),
        (-0.6, 0.33), (-0.6, 0.16), (-0.54, 0.02), (-0.47, 0.15), (-0.46, 0.27), (-0.39, 0.36), (-0.26, 0.38),
        (-0.15, 0.31), (-0.09, 0.15), (-0.08, -0.04)]
GALERUS = [(-0.06, 0.0), (0.14, 0.0), (0.24, 0.2), (0.34, 0.42), (0.38, 0.56), (0.22, 0.48), (0.08, 0.46),
           (-0.04, 0.4), (-0.09, 0.2)]


def elbow(shoulder, hand, pole):
    """Two-bone IK: the elbow between shoulder and hand, bent toward `pole`."""
    s, t = Vector(shoulder), Vector(hand)
    d = t - s
    dist = min(d.length, UPPER + FORE - 1e-3)
    dn = d.normalized()
    x = (UPPER * UPPER - FORE * FORE + dist * dist) / (2 * dist)
    hh = math.sqrt(max(UPPER * UPPER - x * x, 0.0))
    pv = Vector(pole) - Vector(pole).dot(dn) * dn
    return s + dn * x + pv.normalized() * hh


def crescent(name, root, d, radius, span, width, y):
    """The falx blade in the XZ plane (its flat to the front), from `root` continuing the haft direction d (x, z),
    curving up through `span` degrees and tapering to a point; the inner (concave) rim is the bright edge."""
    dx, dz = d
    a0 = math.atan2(-dx, dz)
    c = (root[0] - radius * math.cos(a0), root[1] - radius * math.sin(a0))
    outer, inner = [], []
    n = 9
    for k in range(n + 1):
        t = k / n
        a = a0 + math.radians(span) * t
        w = width * (1.0 - t) ** 0.8
        ca, sa = math.cos(a), math.sin(a)
        outer.append((c[0] + (radius + w * 0.7) * ca, c[1] + (radius + w * 0.7) * sa))
        inner.append((c[0] + (radius - w * 0.3) * ca, c[1] + (radius - w * 0.3) * sa))
    pts = outer + list(reversed(inner[:-1]))
    o = A.flat(K.extrude_poly(name, pts, thickness=0.022, plane="XZ", swatch="steel"))
    K.set_swatch_where(o, "steel_l", lambda cc, nn: abs(nn.y) < 0.5 and
                       math.hypot(cc.x - c[0], cc.z - c[1]) < radius)
    o.data.transform(Matrix.Translation((0, y, 0)))
    return o


def build():
    K.clear_scene()
    h = A.humanoid(0.0, p=P, skin="bone", skin_shade="ash", top=None, belt=None, hair_style="none", face=False,
                   skip=("Torso", "Belt", "Buckle", "Head", "Strap", "Fist", "Foot", "Arm"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, SX = p["H"], p["S"], p["N"], p["TW"], p["SX"]
    hz, HW = a["hz"], a["head_w"]
    ry = HW * 0.93
    ch = a["chest_z"]
    # bone feet, bone knees, the tall flared bronze greaves with knee caps
    for s, sx in (("L", 1), ("R", -1)):
        x = p["LX"] * sx
        parts.append(A.box("Foot" + s, (0.075, 0.19, 0.055), (x, -0.045, 0.0), "bone", pivot=(0, 0, -1), taper=(0.8, 0.85)))
        parts.append(L.ring_rows("Greave" + s, [(0.07, p["LR"] * 1.05, p["LR"] * 1.1, x, 0.0),
                                                (H * 0.68, p["LR"] * 1.55, p["LR"] * 1.6, x, -0.004)],
                                 "bronze", cap_bottom=False, cap_top=False))
        parts.append(A.box("KneeCap" + s, (0.085, 0.03, 0.08), (x, -p["LR"] * 1.55, H * 0.7), "bronze_l", taper=(1.15, 1.0)))
        parts.append(A.box("Knee" + s, (0.07, 0.07, 0.06), (x, -0.005, H * 0.76), "ash"))
    # the crimson kilt with bronze straps, the magenta sash and its hanging end
    parts.append(L.ring_rows("Kilt", [(H - 0.14, TW * 1.25, TW * 1.0), (H + 0.06, TW * 0.95, TW * 0.72)], "crimson_d",
                             cap_bottom=False))
    for k, x in enumerate((-0.07, 0.0, 0.07)):
        parts.append(A.box("Strap%d" % k, (0.045, 0.02, 0.17), (x, -TW * 1.0 - 0.005, H - 0.11), "bronze_d",
                           pivot=(0, 0, -1), rot=(0.12, 0, 0)))
    parts.append(L.band("Sash", H + 0.03, H + 0.08, TW * 1.02, TW * 0.78, "accent_magenta"))
    parts.append(A.box("SashEnd", (0.06, 0.02, 0.22), (TW * 0.7, -TW * 0.8, H + 0.06), "accent_magenta", pivot=(0, 0, 1),
                       taper=(1.4, 1.0), rot=(0.1, 0, 0.1)))
    # the wasp waist: the bare spine and the bottom ribs, then the bronze cuirass widening to the shoulders
    cb = H + 0.24
    parts.append(L.rod("Spine", (0, 0.02, H + 0.06), (0, 0.025, cb + 0.02), 0.03, "bone"))
    parts.append(L.ring_rows("Ribs", [(cb - 0.06, TW * 0.6, TW * 0.48), (cb + 0.01, TW * 0.78, TW * 0.55)], "ash",
                             cap_top=False))
    cu = L.ring_rows("Cuirass", [(cb, TW * 0.8, TW * 0.58), (ch, TW * 1.2, TW * 0.74), (Sz + 0.02, TW * 1.3, TW * 0.72),
                                 (N - 0.01, TW * 0.55, TW * 0.45)], "bronze")
    A.paint(cu, "bronze_l", lambda c, n: n.y < -0.6 and c.z > cb + 0.05)
    parts.append(cu)
    parts.append(L.rod("Neck", (0, 0.0, N - 0.04), (0, 0.0, hz(0.1)), 0.03, "bone"))
    # the cloak of tattered crimson victory ribbons, hanging from a yoke across his back
    by = TW * 0.72 + 0.02
    parts.append(A.box("Yoke", (TW * 2.5, 0.05, 0.05), (0, by, Sz + 0.02), "crimson_d"))
    lengths = (0.8, 0.94, 0.72, 0.9, 0.97, 0.78, 0.92, 0.7)
    for k, ln in enumerate(lengths):
        x = -0.18 + k * 0.0514
        w = 0.046
        rib = A.flat(K.extrude_poly("Ribbon%d" % k, [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, -ln), (0.0, -ln + 0.06),
                                                      (-w / 2, -ln + 0.015 * (k % 2))], thickness=0.014, plane="XZ",
                                    swatch="crimson" if k % 2 else "crimson_d"))
        rib.data.transform(Matrix.Translation((x, by + 0.02, Sz + 0.02)) @ Matrix.Rotation(-0.1 - 0.02 * (k % 3), 4, "X"))
        parts.append(rib)
    # the thraex helm (it is his head): a bronze bowl, the visor with darkness and a grille, a bone jaw, the brim
    parts.append(A.loft("Helm", [(hz(0.04), A.oct8(HW * 0.8, ry * 0.78, 0.62, 0, -ry * 0.05)),
                                 (hz(0.3), A.oct8(HW * 0.98, ry * 0.97, 0.62)), (hz(0.75), A.oct8(HW, ry, 0.62)),
                                 (hz(0.95), A.oct8(HW * 0.78, ry * 0.78, 0.6)), (hz(1.04), A.oct8(HW * 0.36, ry * 0.36, 0.6))],
                        "bronze"))
    parts.append(A.box("Visor", (HW * 1.5, 0.03, hz(0.62) - hz(0.1)), (0, -ry - 0.01, hz(0.1)), "bronze_d",
                       pivot=(0, 0, -1)))
    parts.append(A.box("Dark", (HW * 1.2, 0.02, hz(0.56) - hz(0.34)), (0, -ry - 0.03, hz(0.34)), "ink", pivot=(0, 0, -1)))
    for k, x in enumerate((-0.06, 0.0, 0.06)):
        parts.append(A.box("Grille%d" % k, (0.02, 0.02, hz(0.58) - hz(0.32)), (x, -ry - 0.045, hz(0.32)), "bronze_l",
                           pivot=(0, 0, -1)))
    parts.append(A.box("Jaw", (HW * 1.0, ry * 0.7, 0.06), (0, -ry * 0.55, hz(0.02)), "bone"))
    parts.append(A.box("Teeth", (HW * 0.8, 0.012, 0.022), (0, -ry * 0.9 - 0.006, hz(0.05)), "ink"))
    brim = L.ring_rows("Brim", [(hz(0.64), HW * 1.5, ry * 1.5, 0, 0.0, 0.7), (hz(0.68), HW * 1.42, ry * 1.42, 0, 0.0, 0.7)],
                       "bronze_d")
    L.swing([brim], (0, 0, hz(0.66)), -0.12, "X")                     # tipped up at the front
    parts.append(brim)
    # the gold laurel round the bowl, open over the brow
    lz = hz(0.82)
    parts.append(L.hoop("Laurel", (0, 0, lz), HW * 0.98, 0.016, "gold_d", axis="Z"))
    for k in range(10):
        a_ = 2 * math.pi * (k + 0.5) / 10
        x, y = math.cos(a_) * HW * 1.0, math.sin(a_) * ry * 1.06
        if y < -ry * 0.7:
            continue
        parts.append(A.box("Leaf%d" % k, (0.075, 0.025, 0.045), (x, y, lz + 0.02), "gold_l" if k % 2 else "gold",
                           taper=(0.4, 1.0), rot=(0.0, 0.0, a_ + math.pi / 2)))
    # the griffin crest curling forward over the visor like a hook, a griffin head at its end, a crimson fringe
    cz = hz(0.98)
    sc = 0.62
    crest = A.flat(K.extrude_poly("Crest", [(y * sc, z * sc) for y, z in HOOK], thickness=0.075, plane="YZ",
                                  swatch="bronze_l"))
    K.set_swatch_where(crest, "bronze", lambda c, n: abs(n.x) < 0.5)
    crest.data.transform(Matrix.Translation((0, 0.03, cz)))
    parts.append(crest)
    gh = (0, 0.03 - 0.56 * sc, cz + 0.42 * sc)
    parts.append(A.box("GriffinHead", (0.12, 0.14, 0.11), gh, "bronze_l", taper=(0.8, 0.8)))
    for s, x in (("L", 0.045), ("R", -0.045)):
        parts.append(A.box("GriffinEye" + s, (0.03, 0.03, 0.03), (x, gh[1] - 0.04, gh[2] + 0.03), "ink"))
    parts.append(L.tip("GriffinBeak", (0, gh[1] - 0.06, gh[2] - 0.01), 0.07, 0.03, "bronze_d", direction=(0, -0.6, -1)))
    for k, (dy, dz) in enumerate(((0.22, 0.47), (0.27, 0.27), (0.2, 0.06))):
        parts.append(A.box("Fringe%d" % k, (0.05, 0.1, 0.035), (0, 0.03 + dy * sc + 0.03, cz + dz * sc), "crimson",
                           taper=(0.5, 0.5), rot=(0.5 - 0.4 * k, 0, 0)))
    # the arms: bare bone, bent to hold the falx; a manica on the right forearm
    for s, sx, grip in (("L", 1, GRIP_L), ("R", -1, GRIP_R)):
        sh = Vector(((SX + 0.02) * sx, 0.0, Sz))
        el = elbow(sh, grip, (0.6 * sx, 0.6, -0.2))
        parts.append(A.box("Shoulder" + s, (0.07, 0.07, 0.07), sh, "ash"))
        parts.append(L.rod("Upper" + s, sh, el, 0.024, "bone"))
        parts.append(A.box("Elbow" + s, (0.05, 0.05, 0.05), el, "ash"))
        parts.append(L.rod("Fore" + s, el, grip, 0.022, "bone", taper=1.2))
        parts.append(A.box("Hand" + s, (0.065, 0.065, 0.075), grip, "bone"))
        if s == "R":
            d = grip - el
            for k in range(3):
                c0 = el + d * (0.15 + 0.25 * k)
                parts.append(L.rod("Manica%d" % k, c0, c0 + d * 0.2, 0.038 - 0.003 * k, "bronze" if k % 2 == 0 else
                                   "bronze_l", sides=6))
    # the left shoulder: layered plates and the great flared guard standing beside the helm
    sl = (SX + 0.02, 0.0, Sz)
    for k in range(2):
        parts.append(A.flat(S.shell("Pauldron%d" % k, radius=(0.075 - 0.008 * k, 0.07 - 0.008 * k), arc=(-100, 100),
                                    z=(-0.035 - 0.06 * k, 0.03 - 0.06 * k), taper=0.65, thickness=0.018, segments=4,
                                    location=(sl[0] + 0.01, 0.0, sl[2] + 0.02), swatch="bronze" if k == 0 else "bronze_d",
                                    flare=0.012)))
    guard = A.flat(K.extrude_poly("Guard", [(x * 0.55, z * 0.55) for x, z in GALERUS], thickness=0.028, plane="XZ",
                                  swatch="bronze_l"))
    K.set_swatch_where(guard, "bronze_d", lambda c, n: abs(n.y) < 0.5)
    guard.data.transform(Matrix.Translation((sl[0] - 0.01, -0.02, sl[2] - 0.02)) @ Matrix.Rotation(0.5, 4, "Z")
                         @ Matrix.Rotation(-0.1, 4, "Y"))
    parts.append(guard)
    # the falx: the haft from behind the right fist through the left, then the crescent rising beside the left leg
    dvec = (GRIP_L - GRIP_R).normalized()
    root = GRIP_L + dvec * 0.17
    back = GRIP_R - dvec * 0.06
    parts.append(L.rod("Haft", back, root, 0.016, "bronze_d", sides=6))
    parts.append(L.rod("Wrap", GRIP_R - dvec * 0.02, GRIP_R + dvec * 0.11, 0.022, "leather_d", sides=6))
    parts.append(A.box("Pommel", (0.045, 0.045, 0.045), back - dvec * 0.02, "bronze_l"))
    parts.append(A.box("Collar", (0.04, 0.05, 0.05), root, "bronze_l"))
    dxz = Vector((dvec.x, dvec.z)).normalized()
    parts.append(crescent("Blade", (root.x, root.z), (dxz.x, dxz.y), 0.22, 175.0, 0.17, root.y))
    return parts


parts = build()
L.finish(parts, SCALE, OX)
result = A.review("eternal_victor", parts, original_blend="en_eternalvictor", original_root="EN_EternalVictor",
                  height=3.9, gap=GAP)
