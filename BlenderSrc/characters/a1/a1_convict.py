"""A1 roster — the Starving Convict (EN_Convict, Tier-1 melee swarmer) in the simple style.

Identity kept from en_convict.py: thin, hunched, grey starving skin, sack-cloth rags, the magenta rag round the chest
(the enemy accent), yellow eyes and a grimace, charcoal hair, iron shackles on both wrists with broken chain stubs,
arms reaching forward, bare feet, no weapon. Changed: the boxes become the variant-A body (lean: tier12 lib), the
loincloth becomes a ragged sack tunic with a dirt hem.
export=1: the game model Assets/Art/Models/A1/Enemies/EN_Convict.fbx (docs/tracks/A1-ingame-contract.md).
"""
import importlib
import os
import sys

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
RULES = [("Rag", "Body"), ("ShackleL", "ArmL"), ("ShackleR", "ArmR"), ("LinkL", "ArmL"), ("LinkR", "ArmR")]


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.17, AR=0.056, LR=0.074, SX=0.235, HS=0.066), skin="skin_undead",
               skin_shade="stone_grey", top="cloth_sack", top_shade="dirt", belt=None, foot="skin_undead",
               hair="charcoal", head_top="flat")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    TW = p["TW"]
    face = L.face(a, OX, eyes="hazard_yellow", brows="charcoal", nose="stone_grey", mouth="ink", brow_angle=0.35,
                  eye_w=0.26, eye_h=0.09)
    parts += face
    # the magenta rag tied round the chest (the enemy accent)
    c = a["chest_z"]
    parts.append(L.ring("Rag", c - 0.06, c + 0.03, L.oct8(TW * 1.06, TW * 0.72), "accent_magenta", OX))
    # shackles at the wrists, one broken link hanging from each
    arms = []
    for s, sx in (("L", 1), ("R", -1)):
        x = a["arm_x"] * sx
        wz = a["wz"]
        sh = L.ring("Shackle" + s, wz - 0.02, wz + 0.05, L.hex6(p["HS"] * 1.12, p["HS"] * 1.18, x, -0.018), "iron",
                    OX)
        link = L.box("Link" + s, (0.03, 0.06, 0.09), (x + 0.03 * sx, -0.018, wz - 0.06), "iron_d", OX,
                     rot=(0, 0.35 * sx, 0))
        arms.append((s, x, [sh, link] + L.by_name(parts, "Arm" + s, "Fist" + s)))
        parts += [sh, link]
    # the arms reach forward from the shoulders
    for s, x, objs in arms:
        L.xform(objs, L.rot_x(-1.0), (x, 0.0, p["S"]), OX)
    # the hunch: everything above the hips tips forward; the head tips back up to look ahead
    head = L.by_name(parts, "Head", "Hair", "Eye", "Brow", "Nose", "Mouth")
    L.xform(head, L.rot_x(-0.2), (0, 0, a["neck_z"]), OX)
    upper = [o for o in parts if not o.name.startswith(("Leg", "Foot"))]
    L.xform(upper, L.rot_x(0.36), (0, 0.0, p["H"] - 0.06), OX)
    # the joints, moved like the pieces (the arms and the head turn about their own joints: only the hunch moves them)
    J = L.move_joints(X.joints(p), ("Head", "ArmL", "ArmR"), L.rot_x(0.36), (0, 0.0, p["H"] - 0.06))
    return parts, J


parts, J = build()
if EXPORT:
    result = L.export_enemy("EN_Convict", parts, J, 1.295, RULES)
else:
    result = A.review("convict", parts, original_blend="en_convict", original_root="EN_Convict")
