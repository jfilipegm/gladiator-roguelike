"""A1 roster — the Fire-Pot Thrower (EN_FirePot, area denial) in the simple style.

Identity kept from en_firepot.py: hunched under his load, soot-black rags, a white rag over nose and mouth against the
smoke, an ash cloth cap, yellow eyes, the magenta sash (the enemy accent), slate trousers; a wooden board on his back
carrying clay pots, and a burning clay pot held out in the right hand (glowing top, orange flame: this one throws
fire). Changed: the variant-A body (lean); the mask and cap are painted on the head (no extra triangles); two pots on
the back instead of three.
export=1: the game model Assets/Art/Models/A1/Enemies/EN_FirePot.fbx (docs/tracks/A1-ingame-contract.md).
"""
import importlib
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
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.55
RULES = [("Frame", "Body"), ("Pot", "Body"), ("HandPot", "ArmR"), ("Flame", "ArmR")]


def pot(name, r, base, ox, burning=False, clay="rust_light"):
    """A bulbous hexagonal clay pot standing on `base` (x, y, z)."""
    x, y, z = base
    o = L.loft(name, [(z, L.hex6(r * 0.66, r * 0.66, x, y)), (z + r * 0.75, L.hex6(r, r, x, y)),
                      (z + r * 1.35, L.hex6(r * 0.6, r * 0.6, x, y))], clay, ox, cap_bottom=False)
    if burning:
        L.paint(o, "fire_core", lambda c, n: n.z > 0.9, ox)
    return o


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.19, AR=0.06, LR=0.08, SX=0.25, HS=0.068), skin="skin_tan", skin_shade="skin_dark",
               top="charcoal", belt="accent_magenta", foot="leather_d", legs="trousers", leg_colour="slate",
               hair_style="none", head_top="flat")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    hz, TW, S = a["hz"], p["TW"], p["S"]
    head = L.by_name(parts, "Head")[0]
    L.paint(head, "cloth_white", lambda c, n: c.z < hz(0.42), OX)   # the rag over nose and mouth
    L.paint(head, "ash", lambda c, n: c.z > hz(0.7), OX)            # the cloth cap
    parts += L.face(a, OX, eyes="hazard_yellow", brows="charcoal", nose=None, eye_w=0.26, eye_h=0.085)
    # the board on his back with two clay pots tied on
    by = a["back_y"] + 0.04
    parts.append(A.box("Frame", (0.36, 0.05, 0.56), (0, by, a["waist_z"] - 0.06), "wood", OX, pivot=(0, 0, -1)))
    for k, x in enumerate((0.085, -0.085)):
        parts.append(pot("Pot%d" % k, 0.1, (x, by + 0.12, a["waist_z"] + 0.08 + 0.14 * k), OX))
    # the burning pot held out in the right hand
    xr = -a["arm_x"]
    rot = L.rot_x(-0.9) @ L.Matrix.Rotation(0.22, 4, "Y")
    L.xform(L.by_name(parts, "ArmR", "FistR"), rot, (xr, 0, S), OX)
    hand = Vector((xr, 0.0, S)) + rot.to_3x3() @ (Vector(a["hand_r"]) - Vector((xr, 0.0, S)))
    r = 0.1
    base = (hand.x, hand.y - 0.05, hand.z - 0.02)
    parts.append(pot("HandPot", r, base, OX, burning=True, clay="rust"))
    parts.append(A.cone("Flame", 0.075, 0.24, (base[0], base[1], base[2] + r * 1.3), "hazard_orange", OX, verts=5))
    # the hunch under the load
    upper = [o for o in parts if not o.name.startswith(("Leg", "Foot"))]
    head_grp = L.by_name(parts, "Head", "Eye", "Brow")
    L.xform(head_grp, L.rot_x(-0.12), (0, 0, a["neck_z"]), OX)
    L.xform(upper, L.rot_x(0.24), (0, 0, p["H"] - 0.06), OX)
    J = L.move_joints(X.joints(p), ("Head", "ArmL", "ArmR"), L.rot_x(0.24), (0, 0, p["H"] - 0.06))   # the hunch
    return parts, J


parts, J = build()
if EXPORT:
    result = L.export_enemy("EN_FirePot", parts, J, 1.163, RULES)
else:
    result = A.review("fire_pot", parts, original_blend="en_firepot", original_root="EN_FirePot")
