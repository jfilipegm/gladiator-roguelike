"""A1 roster — the Skeletal Legionnaire in the simple style (tier 3: the Colosseum's undead swarmer).

Identity kept from en_skeletallegionnaire.py: a big bone skull with black sockets and a gaping jaw under a dented,
tilted rusty helmet whose crest is a broken stump; rusted segmented plates on the chest and shoulders with a bronze
band; nothing below them but a bare spine down to the magenta sash (the enemy accent) and a rotted crimson rag;
stick-thin bone arms and legs; a notched gladius held forward in the right hand; a shambling hunch. Undead at a
glance: the stick limbs, the gap at the waist where only the spine is, the big skull.
In-game (export=1): lean limbs (a1_tier12_lib.body), a skull cut flat under the helmet, a decal face: ~540 tris;
Assets/Art/Models/A1/Enemies/EN_SkeletalLegionnaire.fbx (the joints follow the hunch).
"""
import importlib
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
import a1_tier12_lib as T
importlib.reload(T)
import a1_tier3boss_lib as L
importlib.reload(L)
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"
OX = 0.0 if EXPORT else -0.55
HUNCH = 0.14


def build():
    K.clear_scene()
    h = T.body(0.0, p=dict(AR=0.036, LR=0.042, TW=0.19), skin="bone", skin_shade="ash", top=None, belt=None,
               hair_style="none", skip=("Torso", "Belt", "Head", "Fist", "Foot"))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    H, Sz, N, TW, SX = p["H"], p["S"], p["N"], p["TW"], p["SX"]
    hz, HW = a["hz"], a["head_w"]
    ry = HW * 0.93
    wz = Sz - p["AL"]
    # bony hands and feet (plain chamfered blocks: no flesh)
    for s, sx in (("L", 1), ("R", -1)):
        x = (SX + 0.02) * sx
        parts.append(A.box("Hand" + s, (0.07, 0.075, 0.09), (x, -0.012, wz - 0.04), "bone", taper=(1.2, 1.2)))
        parts.append(A.box("Foot" + s, (0.085, 0.2, 0.06), (p["LX"] * sx, -0.045, 0.03), "skin_undead",
                           taper=(0.8, 0.85)))
    # the pelvis, the magenta sash round it, the rotted rag hanging in front
    parts.append(L.ring_rows("Pelvis", [(H - 0.05, TW * 0.78, TW * 0.52), (H + 0.07, TW * 0.7, TW * 0.48)], "bone"))
    parts.append(L.band("Sash", H - 0.025, H + 0.045, TW * 1.0, TW * 0.68, "accent_magenta"))
    rag = A.flat(K.extrude_poly("Rag", [(-0.055, 0.0), (0.055, 0.0), (0.07, -0.21), (0.035, -0.15), (0.005, -0.25),
                                        (-0.03, -0.16), (-0.06, -0.2)], thickness=0.016, plane="XZ",
                                swatch="crimson_d"))
    rag.data.transform(Matrix.Translation((0.05, -TW * 0.72, H + 0.01)) @ Matrix.Rotation(0.12, 4, "Y"))
    parts.append(rag)
    # the bare spine up to the rusted plates
    cb = a["chest_z"] - 0.1
    parts.append(L.rod("Spine", (0, 0.03, H + 0.05), (0, 0.035, cb + 0.02), 0.026, "bone"))
    parts.append(L.ring_rows("Ribs", [(cb - 0.06, TW * 0.62, TW * 0.48), (cb + 0.01, TW * 0.8, TW * 0.56)], "ash",
                             cap_top=False))
    parts.append(L.ring_rows("Plates", [(cb, TW * 0.92, TW * 0.64), (cb + 0.12, TW * 1.04, TW * 0.68),
                                        (Sz + 0.02, TW * 1.12, TW * 0.66), (N - 0.01, TW * 0.5, TW * 0.42)],
                             "rusty_iron"))
    parts.append(L.band("PlateBand", cb + 0.07, cb + 0.12, TW * 1.06, TW * 0.7, "bronze_d"))
    parts.append(A.box("Shoulders", (2 * SX + 0.16, TW * 1.5, 0.06), (0, 0.0, Sz + 0.01), "rusty_iron",
                       taper=(0.82, 0.9)))
    parts.append(L.rod("Neck", (0, 0.0, N - 0.04), (0, 0.0, hz(0.22)), 0.03, "bone"))
    # the skull (cut flat under the helmet): black sockets, a nose hole, teeth, the jaw hanging open
    skull = A.loft("Skull", [(hz(0.2), A.oct8(HW * 0.7, ry * 0.72, 0.64, 0, -ry * 0.1)),
                             (hz(0.4), A.oct8(HW * 0.96, ry * 0.97, 0.62)), (hz(0.72), A.oct8(HW, ry, 0.62))], "bone")
    A.paint(skull, "ash", lambda c, n: c.z < hz(0.3) and n.y < -0.3)
    parts.append(skull)
    for s, x in (("L", HW * 0.38), ("R", -HW * 0.38)):
        parts.append(T.decal("Socket" + s, HW * 0.36, p["HH"] * 0.17, (x, -ry - 0.003, hz(0.5)), "ink"))
    parts.append(T.mesh("NoseHole", [(0, -ry - 0.004, hz(0.41)), (-0.033, -ry - 0.004, hz(0.3)),
                                     (0.033, -ry - 0.004, hz(0.3))], [(0, 1, 2)], "ink"))
    parts.append(T.decal("Mouth", HW * 0.95, 0.05, (0, -ry * 0.84 - 0.006, hz(0.22)), "ink"))
    parts.append(T.decal("Teeth", HW * 0.78, 0.035, (0, -ry * 0.84 - 0.009, hz(0.235)), "bone"))
    jaw = A.box("Jaw", (HW * 1.0, ry * 1.0, 0.06), (0, ry * 0.3, hz(0.17)), "bone", pivot=(0, 1, 1),
                rot=(0.34, 0, 0))                                     # hanging open (hinged at the back)
    parts.append(jaw)
    # the dented rusty helmet knocked askew, its crest a broken stump, a short neck guard
    helm = L.lean_helmet(a, "rusty_iron", low=0.6, top=1.0, s=1.07)
    stump = A.box("Stump", (0.05, 0.14, 0.12), (0, 0.03, hz(1.0)), "bronze_d", pivot=(0, 0, -1), taper=(1.0, 0.55))
    nape = A.flat(S.shell("NeckGuard", radius=(HW * 1.1, ry * 1.1), arc=(25, 155), z=(hz(0.42), hz(0.62)),
                          taper=0.97, thickness=0.025, segments=3, swatch="rusty_iron", flare=0.05, smooth_angle=0))
    L.swing([helm, stump, nape], (0, 0, hz(0.6)), 0.17, "Y")
    L.swing([helm, stump, nape], (0, 0, hz(0.6)), 0.07, "X")
    parts += [helm, stump, nape]
    # the gladius: built along -Y from the right fist, notched; the right arm swings it forward and up
    hx, hy, hzz = a["hand_r"]
    blade = A.flat(K.extrude_poly("Gladius", [(0.0, 0.028), (-0.2, 0.032), (-0.24, 0.008), (-0.27, 0.032),
                                              (-0.44, 0.0), (-0.36, -0.03), (0.0, -0.03)],
                                  thickness=0.022, plane="YZ", swatch="steel"))
    blade.data.transform(Matrix.Translation((hx, hy - 0.07, hzz)))
    hilt = A.box("GladiusHilt", (0.045, 0.14, 0.045), (hx, hy + 0.0, hzz), "bronze_d")
    guard = A.box("GladiusGuard", (0.05, 0.035, 0.12), (hx, hy - 0.065, hzz), "bronze_d")
    sh_r = (-(SX + 0.02), 0.0, Sz)
    L.swing([L.get(parts, "ArmR"), L.get(parts, "HandR"), blade, hilt, guard], sh_r, -0.55)
    L.swing([L.get(parts, "ArmL"), L.get(parts, "HandL")], (SX + 0.02, 0.0, Sz), -0.35)
    parts += [blade, hilt, guard]
    # the shambling hunch: the upper body leans forward over the hips (the joints move with it)
    z0 = H + 0.05
    L.bake(parts)
    arms = {o: Sz for o in parts if o.name.startswith(("ArmL", "ArmR", "HandL", "HandR", "Gladius"))}
    L.shear_forward([o for o in parts if not o.name.startswith(("Leg", "Foot"))], z0, HUNCH, arms)
    shift = {"Head": (0, -(N - z0) * HUNCH, 0), "ArmL": (0, -(Sz - z0) * HUNCH, 0), "ArmR": (0, -(Sz - z0) * HUNCH, 0)}
    return parts, L.scaled_joints(X.joints(p), 1.0, shift)


parts, J = build()
L.finish(parts, 1.0, OX)
if EXPORT:
    result = L.export_rig("EN_SkeletalLegionnaire", parts, J,
                          rules=[("Pelvis", "Body"), ("Rag", "Body"), ("Spine", "Body"), ("Ribs", "Body"),
                                 ("Plate", "Body"), ("Shoulders", "Body"), ("NeckGuard", "Head"), ("Skull", "Head"),
                                 ("Socket", "Head"), ("Teeth", "Head"), ("Jaw", "Head"), ("Stump", "Head"),
                                 ("Gladius", "ArmR")])
else:
    result = A.review("skeletal_legionnaire", parts, original_blend="en_skeletallegionnaire",
                      original_root="EN_SkeletalLegionnaire")
