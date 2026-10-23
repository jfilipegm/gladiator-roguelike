"""A1 roster — the Pit Cur (EN_PitCur, Tier-1 fast dog) in the simple style.

Identity kept from en_pitcur.py: a low, lean quadruped in mangy mud-brown with a dark bristling ridge along the spine,
pointed dark ears, a pale snout with a black nose and bone fangs, yellow eyes (angry brows, like the other enemies),
the magenta collar with iron spikes (the enemy accent), the tail up, light paws, a lunge stance (front legs reaching,
hind legs pushing back). Changed: chibi, so a big chamfered head; oct8 body sections lofted along Y with a pale belly,
hex6 legs.
export=1: the game model Assets/Art/Models/A1/Enemies/EN_PitCur.fbx (front legs = ArmL/ArmR, hind legs = LegL/LegR,
as in the original).
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

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.55
RULES = [("Body", "Body"), ("Hackles", "Body"), ("Collar", "Body"), ("Spike", "Body"), ("Tail", "Body"),
         ("Snout", "Head"), ("Fang", "Head"), ("PawLArm", "ArmL"), ("PawRArm", "ArmR"), ("PawLLeg", "LegL"),
         ("PawRLeg", "LegR")]


def build():
    K.clear_scene()
    parts = []
    add = parts.append
    o8 = L.oct8
    # the body: a deep chest, a tucked waist, a smaller rump; pale belly (chibi: short and low)
    bz = 0.42
    b = L.loft_y("Body", [(-0.24, o8(0.10, 0.10, 0.62, 0, bz)), (-0.14, o8(0.13, 0.14, 0.62, 0, bz)),
                          (0.05, o8(0.10, 0.105, 0.62, 0, bz)), (0.22, o8(0.11, 0.115, 0.62, 0, bz)),
                          (0.26, o8(0.08, 0.085, 0.62, 0, bz + 0.01))], "mud", OX)
    L.paint(b, "dirt_light", lambda c, n: n.z < -0.6, OX)
    add(b)
    # the hackles: a triangular ridge along the spine, highest at the shoulders
    tri = lambda w, h, z: [(w, z), (0.0, z + h), (-w, z)]
    add(L.loft_y("Hackles", [(-0.20, tri(0.03, 0.03, bz + 0.10)), (-0.10, tri(0.05, 0.08, bz + 0.11)),
                             (0.16, tri(0.03, 0.03, bz + 0.085))], "mud_dark", OX))
    # the head: big (chibi), chamfered, on the chest; a pale snout, black nose, bone fangs
    hz, hy = 0.60, -0.25
    add(L.loft_y("Head", [(hy - 0.26, o8(0.14, 0.13, 0.62, 0, hz)), (hy - 0.13, o8(0.155, 0.145, 0.62, 0, hz)),
                          (hy, o8(0.12, 0.115, 0.62, 0, hz + 0.01))], "mud", OX))
    sn = L.loft_y("Snout", [(hy - 0.42, o8(0.065, 0.05, 0.6, 0, hz - 0.06)),
                            (hy - 0.25, o8(0.085, 0.065, 0.6, 0, hz - 0.05))], "dirt_light", OX, cap_back=False)
    L.paint(sn, "ink", lambda c, n: n.y < -0.9, OX)
    add(sn)
    for s, x in (("L", 0.042), ("R", -0.042)):
        add(A.cone("Fang" + s, 0.015, 0.05, (x, hy - 0.405, hz - 0.105), "bone", OX, verts=3, rot=(math.pi, 0, 0)))
    fy = hy - 0.26
    for s, x, sg in (("L", 0.064, 1), ("R", -0.064, -1)):
        add(L.decal("Eye" + s, 0.05, 0.028, (x, fy - 0.004, hz + 0.035), "hazard_yellow", OX))
        add(L.decal("Brow" + s, 0.066, 0.02, (x, fy - 0.008, hz + 0.075), "mud_dark", OX, rot_y=-0.4 * sg,
                    ledge=0.012))
        add(A.cone("Ear" + s, 0.055, 0.14, (x * 1.5, hy - 0.11, hz + 0.11), "mud_dark", OX, verts=4,
                   rot=(-0.25, 0.4 * sg, 0)))
    # the magenta collar (wide, seen under the chin from the front) with two iron spikes
    # (a frustum: its faces slope forward, so it shows as a magenta frame round the neck from the front)
    add(L.loft_y("Collar", [(hy - 0.07, o8(0.095, 0.095, 0.62, 0, 0.45)), (hy + 0.0, o8(0.14, 0.135, 0.62, 0, 0.46)),
                            (hy + 0.06, o8(0.13, 0.125, 0.62, 0, 0.46))], "accent_magenta", OX, False, False))
    for s, sg in (("L", 1), ("R", -1)):
        add(A.cone("Spike" + s, 0.024, 0.075, (0.13 * sg, hy + 0.02, 0.47), "steel", OX, verts=4,
                   rot=(0, 1.2 * sg, 0)))
    # legs: short and stocky; front ones reach forward, hind ones push back; light paws
    for s, x in (("L", 0.075), ("R", -0.075)):
        for end, y, r0, r1, ang in (("Arm", -0.15, (0.04, 0.045), (0.056, 0.066), -0.2),
                                    ("Leg", 0.17, (0.04, 0.045), (0.07, 0.09), 0.22)):
            leg = L.loft(end + s, [(0.04, L.hex6(r0[0], r0[1], x, y)), (bz + 0.02, L.hex6(r1[0], r1[1], x, y))], "mud",
                         OX, False, False)
            paw = L.loft("Paw" + s + end, [(0.0, L.hex6(0.052, 0.068, x, y - 0.02)),
                                            (0.055, L.hex6(0.046, 0.055, x, y - 0.012))], "dirt_light", OX,
                         cap_bottom=False)
            L.xform([leg, paw], L.rot_x(ang), (x, y, bz), OX)
            parts += [leg, paw]
    # the tail, up and back
    tail = L.loft("Tail", [(0.0, L.hex6(0.03, 0.03)), (0.24, L.hex6(0.012, 0.012))], "mud_dark", 0.0, False, True)
    tail.rotation_euler = (-0.75, 0, 0)
    tail.location = (OX, 0.25, 0.47)
    add(tail)
    # the joints: the body at its middle, the head at the neck, each leg at the top where its stance was turned
    J = {"Body": (0.0, 0.0, bz), "Head": (0.0, hy - 0.02, 0.56), "ArmL": (0.075, -0.15, bz), "ArmR": (-0.075, -0.15, bz),
         "LegL": (0.075, 0.17, bz), "LegR": (-0.075, 0.17, bz)}
    return parts, {k: L.Vector(v) for k, v in J.items()}


parts, J = build()
if EXPORT:
    result = L.export_enemy("EN_PitCur", parts, J, 0.762, RULES)
else:
    result = A.review("pit_cur", parts, original_blend="en_pitcur", original_root="EN_PitCur", height=0.9)
