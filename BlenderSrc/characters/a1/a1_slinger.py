"""A1 roster — the Slinger (EN_Slinger, Tier-1 ranged) in the simple style.

Identity kept from en_slinger.py: thin and upright, the green pointed hood framing the face (the only green figure in
the pit: "the shooter") with a dark tip, a sack-cloth mask over the lower face, the magenta scarf (the enemy accent),
a green tunic with a leather belt and a stone bag on the hip, light leather trousers, a sling hanging from the right
hand with a glowing orange-red stone in its pouch (the thing that hurts). Changed: the variant-A body (lean); the hood
is one chamfered loft with the face opening cut out; the mask is painted on the jaw.
"""
import importlib
import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import simple_kit as A
importlib.reload(A)
import a1_tier12_lib as L
importlib.reload(L)

OX = -0.55


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.18, AR=0.058, LR=0.078, SX=0.245, HS=0.066), skin="skin_light", skin_shade="skin_mid",
               top="cloth_green", belt="leather_d", foot="leather_d", legs="trousers", leg_colour="leather_l",
               hair_style="none", head_top="flat")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    HW, ry, hz, N, TW = a["head_w"], a["head_back_y"], a["hz"], a["neck_z"], p["TW"]
    o8 = L.oct8
    head = L.by_name(parts, "Head")[0]
    L.paint(head, "cloth_sack", lambda c, n: c.z < hz(0.42) and n.y < 0.3, OX)   # the mask over nose and mouth
    parts += L.face(a, OX, eyes="ink", brows="leaf_d", nose=None, eye_w=0.26, eye_h=0.08)
    # the hood: one loft round the head, the face opening cut out, a pointed tip falling back
    hood = L.loft("Hood", [(N - 0.03, o8(HW * 0.92, ry * 0.98, 0.62, 0, 0.01)),
                           (hz(0.42), o8(HW * 1.12, ry * 1.1, 0.62)), (hz(0.80), o8(HW * 1.12, ry * 1.1, 0.62)),
                           (hz(1.0), o8(HW * 0.6, ry * 0.62, 0.62, 0, 0.03)),
                           (hz(1.27), o8(0.022, 0.022, 0.62, 0, 0.15))], "cloth_green", OX, cap_bottom=False)
    L.delete_faces(hood, lambda c, n: c.z < hz(0.8) and n.y < -0.85, OX)
    L.paint(hood, "leaf_d", lambda c, n: c.z > hz(1.02), OX)
    parts.append(hood)
    # the magenta scarf on the shoulders, under the hood
    parts.append(L.loft("Scarf", [(p["S"] - 0.03, o8(TW * 1.14, TW * 0.74)), (N - 0.0, o8(TW * 0.8, TW * 0.66))],
                        "accent_magenta", OX, False, False))
    # the stone bag on the left hip
    parts.append(A.box("StoneBag", (0.1, 0.1, 0.12), (TW * 0.62, -TW * 0.78, a["waist_z"] - 0.03), "leather", OX,
                       pivot=(0, 0, 1)))
    # the sling arm, a little forward and out; the sling hangs from the fist with the glowing stone
    xr, S = -a["arm_x"], p["S"]
    rot = L.rot_x(-0.25) @ L.Matrix.Rotation(-0.12, 4, "Y")
    L.xform(L.by_name(parts, "ArmR", "FistR"), rot, (xr, 0, S), OX)
    hand = Vector((xr, 0.0, S)) + rot.to_3x3() @ (Vector(a["hand_r"]) - Vector((xr, 0.0, S)))
    fz = hand.z - p["HS"] * 1.0
    ln = 0.3
    parts.append(A.box("SlingStrap", (0.022, 0.022, ln), (hand.x, hand.y, fz), "rope", OX, pivot=(0, 0, 1)))
    pz = fz - ln
    r = 0.068
    c = (hand.x, hand.y, pz - 0.035)
    v = [(c[0] + r, c[1], c[2]), (c[0] - r, c[1], c[2]), (c[0], c[1] + r, c[2]), (c[0], c[1] - r, c[2]),
         (c[0], c[1], c[2] + r * 1.1), (c[0], c[1], c[2] - r * 0.6)]
    f = [(4, 0, 2), (4, 2, 1), (4, 1, 3), (4, 3, 0), (5, 2, 0), (5, 1, 2), (5, 3, 1), (5, 0, 3)]
    parts.append(L.mesh("SlingStone", v, f, "glow_hazard_red", OX))
    return parts


parts = build()
result = A.review("slinger", parts, original_blend="en_slinger", original_root="EN_Slinger")
