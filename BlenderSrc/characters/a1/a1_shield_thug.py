"""A1 roster — the Shield Thug (EN_ShieldThug, Tier-1 frontal blocker) in the simple style.

Identity kept from en_shieldthug.py: a stocky brawler in dark leather, an iron pot helmet with a spike, a brim and a
nose guard, a dark beard, iron-shod boots; a big light wooden door-shield held square in front on the left arm (it IS
the gameplay, so it owns the front silhouette) with iron rims and the magenta boss (the enemy accent); a knobbed
club up and forward in the right fist. Changed: the variant-A body (lean), the pot helmet a chamfered frustum.
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

OX = -0.55


def build():
    K.clear_scene()
    h = L.body(OX, p=dict(TW=0.235, AR=0.08, LR=0.1, LX=0.12, SX=0.29, HS=0.08, HW=0.27), skin="skin_mid",
               skin_shade="skin_tan", top="leather_d", belt=None, foot="iron_d", legs="trousers",
               leg_colour="leather", hair_style="none", head_top="flat")
    parts, a = h["parts"], h["a"]
    p = a["p"]
    HW, ry, hz = a["head_w"], a["head_back_y"], a["hz"]
    o8 = L.oct8
    parts += L.face(a, OX, eyes="ink", brows=None, nose=None, eye_w=0.26, eye_h=0.08, eye_z=hz(0.47))
    # the iron pot helmet: a chamfered frustum, a wide brim, a spike; the nose guard; the beard
    parts.append(L.loft("Helmet", [(hz(0.62), o8(HW * 1.08, ry * 1.08, 0.6)), (hz(1.0), o8(HW * 0.74, ry * 0.74, 0.6))],
                        "iron", OX, cap_bottom=False))
    parts.append(L.loft("Brim", [(hz(0.57), o8(HW * 1.24, ry * 1.24, 0.6)), (hz(0.63), o8(HW * 1.24, ry * 1.24, 0.6))],
                        "iron_d", OX, cap_top=False))
    parts.append(A.cone("Spike", 0.045, 0.13, (0, 0, hz(0.99)), "steel", OX, verts=4))
    parts.append(A.box("NoseGuard", (0.05, 0.035, HH(p) * 0.3), (0, L.face_y(a, 0.45) - 0.02, hz(0.58)), "iron", OX,
                       pivot=(0, 0, 1)))
    parts.append(A.box("Beard", (HW * 1.1, 0.07, HH(p) * 0.2), (0, L.face_y(a, 0.12) - 0.01, hz(0.3)), "mud_dark", OX,
                       pivot=(0, 0, 1), taper=(1.15, 1.0), rot=(0.25, 0, 0)))
    # the door-shield on the left arm, held square in front: light planks, iron rims (painted bands), magenta boss
    w, t, z0, z1, cx, cy = 0.56, 0.06, 0.16, 0.84, 0.05, -0.34
    rect = [(cx + w / 2, cy - t / 2), (cx + w / 2, cy + t / 2), (cx - w / 2, cy + t / 2), (cx - w / 2, cy - t / 2)]
    sh = L.loft("Shield", [(z0, rect), (z0 + 0.06, rect), (z1 - 0.06, rect), (z1, rect)], "wood_light", OX,
                cap_bottom=False)
    L.paint(sh, "iron", lambda c, n: c.z < z0 + 0.05 or c.z > z1 - 0.05, OX)
    parts.append(sh)
    for k, dx in enumerate((-0.095, 0.095)):
        parts.append(L.decal("Plank%d" % k, 0.018, z1 - z0 - 0.13, (cx + dx, cy - t / 2 - 0.003, (z0 + z1) / 2),
                             "wood", OX))
    boss = A.cone("Boss", 0.1, 0.07, (cx, cy - t / 2, (z0 + z1) / 2 + 0.03), "accent_magenta", OX, verts=4,
                  rot=(1.5708, 0, 0))
    parts.append(boss)
    xl = a["arm_x"]
    L.xform(L.by_name(parts, "ArmL", "FistL"), L.rot_x(-0.9), (xl, 0, p["S"]), OX)
    # the club, up and forward out of the right fist
    hx, hy, hzz = a["hand_r"]
    club = L.loft("Club", [(-0.07, L.hex6(0.028, 0.028)), (0.5, L.hex6(0.07, 0.07))], "wood_dark", 0.0,
                  cap_bottom=False)
    club.location = (hx + OX, hy, hzz)
    club.rotation_euler = (0.75, 0, 0)
    parts.append(club)
    L.xform(L.by_name(parts, "ArmR", "FistR", "Club"), L.rot_x(-0.25), (-xl, 0, p["S"]), OX)
    return parts


def HH(p):
    return p["HH"]


parts = build()
result = A.review("shield_thug", parts, original_blend="en_shieldthug", original_root="EN_ShieldThug")
