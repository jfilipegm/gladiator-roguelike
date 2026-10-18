"""EN_FirePot — Fire-Pot Thrower, area denial (CLAUDE.md §6.3 Tier 2, pulled into the slice; DECISIONS M2.2).
Budget <= 300 tris.

Hunched in rust-red rags, face wrapped in bandages with yellow eyes, a wooden back-frame carrying clay pots, and a
burning pot in the right hand (glowing = this one throws fire). Magenta sash = accent. Wind-up swings the pot back
overhead; the throw is a lob onto a telegraphed spot (EnemyProjectiles).
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.36


def pot(name, radius, location, parent, burning=False):
    p = K.cylinder(name, radius=radius, depth=radius * 1.3, verts=6, location=location, pivot=(0, 0, -1), taper=0.6,
                   swatch="rust_light", parent=parent)
    if burning:
        K.set_swatch_where(p, "fire_core", lambda c, n: n.z > 0.5)
    return p


def build():
    K.clear_scene()
    root = K.empty("EN_FirePot")
    body = K.box("Body", size=(0.36, 0.28, 0.44), location=(0, 0.02, HIP_Z), pivot=(0, 0, -1), taper=(1.1, 1.0),
                 swatch="rust", parent=root)
    body.rotation_euler = (0.22, 0, 0)  # hunched under the load
    K.box("Sash", size=(0.40, 0.31, 0.08), location=(0, 0, 0.12), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    frame = K.box("Frame", size=(0.40, 0.06, 0.56), location=(0, 0.19, 0.08), pivot=(0, 0, -1), swatch="wood",
                  parent=body)
    K.box("FrameBar", size=(0.46, 0.08, 0.05), location=(0, 0.03, 0.18), pivot=(0, 0, -1), swatch="wood_dark",
          parent=frame)
    pot("PotA", 0.10, (0.11, 0.12, 0.23), frame)
    pot("PotB", 0.10, (-0.11, 0.12, 0.23), frame)
    pot("PotC", 0.09, (0.0, 0.12, 0.40), frame)

    head = K.box("Head", size=(0.34, 0.32, 0.32), location=(0, -0.04, 0.44), pivot=(0, 0, -1), swatch="skin_tan",
                 parent=body)
    K.box("Mask", size=(0.36, 0.34, 0.12), location=(0, 0, 0.01), pivot=(0, 0, -1), swatch="cloth_white",
          parent=head)  # rag over nose and mouth against the smoke
    K.box("Cap", size=(0.37, 0.35, 0.10), location=(0, 0.01, 0.25), pivot=(0, 0, -1), taper=(0.8, 0.8),
          swatch="rust_light", parent=head)
    for side, x in (("L", 0.075), ("R", -0.075)):
        K.box("Eye" + side, size=(0.06, 0.03, 0.04), location=(x, -0.165, 0.17), pivot=(0, 0, -1),
              swatch="hazard_yellow", parent=head)

    arm_l = K.box("ArmL", size=(0.10, 0.10, 0.36), location=(0.23, 0, 0.40), pivot=(0, 0, 1), swatch="skin_tan",
                  parent=body)
    arm_l.rotation_euler = (-0.3, 0, -0.1)
    arm_r = K.box("ArmR", size=(0.10, 0.10, 0.36), location=(-0.23, 0, 0.40), pivot=(0, 0, 1), swatch="skin_tan",
                  parent=body)
    arm_r.rotation_euler = (-0.55, 0, 0.1)
    # burning pot held upright in front of the fist (counter-rotated so the flame points up)
    hand_pot = pot("HandPot", 0.13, (0, -0.10, -0.40), arm_r, burning=True)
    hand_pot.rotation_euler = (0.55, 0, -0.1)
    K.cone("HandFlame", radius=0.08, depth=0.17, verts=5, location=(0, 0, 0.165), pivot=(0, 0, -1), swatch="fire_mid",
           parent=hand_pot)

    for side, x in (("L", 0.09), ("R", -0.09)):
        leg = K.box("Leg" + side, size=(0.12, 0.13, 0.34), location=(x, 0, HIP_Z), pivot=(0, 0, 1), swatch="rust_light",
                    parent=root)
        K.box("Foot" + side, size=(0.13, 0.20, 0.06), location=(0, -0.03, -0.36), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)
    return root


root = build()
info = K.review_render(root, "en_firepot", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_FirePot")
info["blend"] = K.save_blend("en_firepot")
result = info
