"""A1 roster — the Nobody in the simple style (the worked example for every other A1 character script).

Pattern: clear the scene, build the base humanoid at ox = -0.55 with this character's options, add its identity
pieces (headgear, armour, weapon, off-hand) from the anchors, then review() beside the original from its .blend.
Identity kept from ch_nobody.py: the cyan headband and the focale's tail (the player's mark), the exomis, the scrap
pauldron and one greave on the shield side, the rusty gladius, the round scrap shield.
"""
import importlib
import os
import sys

sys.path.insert(0, os.path.join(REPO, "BlenderSrc", "characters", "a1"))
import gladiator_kit as K
importlib.reload(K)
import stylized_kit as S
importlib.reload(S)
import simple_kit as A
importlib.reload(A)
import a1_export as X
importlib.reload(X)

EXPORT = ARGS.get("export") == "1"   # export=1: the game model (Assets/Art/Models/A1/Characters/CH_Nobody.fbx)
OX = 0.0 if EXPORT else -0.55


def build():
    K.clear_scene()
    h = A.humanoid(OX, bare_right_shoulder=True, sleeves=("L",))
    parts, a = h["parts"], h["a"]
    p = a["p"]
    parts += A.headband(a, "player_cyan", OX)
    # the focale: a cyan scarf at the neck, its tail down the back
    N = a["neck_z"]
    parts.append(A.loft("Focale", [(N - 0.07, A.oct8(0.135, 0.1, 0.6)), (N, A.oct8(0.1, 0.085, 0.6))], "player_cyan",
                        OX, False, False))
    parts.append(A.box("FocaleTail", (0.10, 0.02, (p["S"] - p["H"]) * 0.6), (0.03, a["back_y"] + 0.012, N - 0.04),
                       "player_cyan", OX, rot=(0.1, 0.08, 0), pivot=(0, 0, 1), taper=(0.6, 1.0)))
    # the shield side: a scrap pauldron and one greave
    sl = a["shoulder_l"]
    pa = S.shell("Pauldron", radius=(p["AR"] * 1.7, p["AR"] * 1.6), arc=(-100, 100), z=(-0.06, 0.06), taper=0.6,
                 thickness=0.02, segments=4, location=(sl[0] + OX - 0.01, 0.0, sl[2]), swatch="rusty_iron",
                 flare=0.012)
    parts.append(A.flat(pa))
    gr = S.shell("Greave", radius=(p["LR"] * 0.86, p["LR"] * 0.95), arc=(200, 340), z=(0.13, p["H"] * 0.62),
                 taper=1.08, thickness=0.016, segments=3, location=(p["LX"] + OX, 0.0, 0.0), swatch="rusty_iron")
    parts.append(A.flat(gr))
    # the gladius in the right fist (grey iron, bright edges), the shield on the left forearm
    hx, hy, hz = a["hand_r"]
    g = K.empty("GladiusFrame", location=(hx + OX, hy, hz))
    blade = K.extrude_poly("Blade", [(-0.08, 0.045), (-0.42, 0.06), (-0.62, 0.034), (-0.72, 0.0), (-0.62, -0.034),
                                     (-0.42, -0.06), (-0.08, -0.045)], thickness=0.028, plane="YZ", swatch="iron",
                           parent=g)
    K.set_swatch_where(blade, "steel_l", lambda c, n: abs(n.x) < 0.5)
    for o in (blade, K.box("Guard", size=(0.06, 0.05, 0.17), location=(0, -0.07, 0), pivot=(0, 0, 0),
                           swatch="bronze_d", parent=g),
              K.box("Pommel", size=(0.06, 0.05, 0.06), location=(0, 0.1, 0), pivot=(0, 0, 0), swatch="bronze_d",
                    parent=g)):
        parts.append(A.flat(o))
    lx, ly, lz = a["hand_l"]
    parts.append(A.round_shield("Shield", 0.235, (lx + 0.11, -0.04, lz + p["AL"] * 0.35), OX))
    return parts, p


parts, p = build()
if EXPORT:
    result = X.export("CH_Nobody", "Characters", parts, X.joints(p),
                      rules=[("Blade", "ArmR"), ("Guard", "ArmR"), ("Pommel", "ArmR"), ("Shield", "ArmL"),
                             ("Pauldron", "ArmL")])
else:
    result = A.review("nobody", parts, original_blend="ch_nobody", original_root="CH_Nobody")
