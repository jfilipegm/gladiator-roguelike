"""EN_BeastLord — the Beast Lord, the Stone Amphitheater's boss (docs/BOSSES.md). Budget <= 2,500 tris.

Huge and hunched (~3.3 m): a whole lion's pelt worn as a cloak, the lion's head as his hood (snout, fangs, ears over
his brow) and its dark mane heaped over his shoulders; bare dark-skinned arms with iron bracers, fur-wrapped legs.
Left arm (+X): a long hooked goad-spear held upright, taller than him (the sweep and the hook thrust swing that arm).
At the belt: a coiled chain and a bone war horn. The magenta belt is the enemy accent. Nothing else wears a mane or
carries a pole that long. Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR). Origin at the feet;
front = -Y (Unity +Z).
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 1.1


def build():
    K.clear_scene()
    root = K.empty("EN_BeastLord")
    body = K.box("Body", size=(1.15, 0.78, 1.05), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.25, 1.15),
                 bevel=0.03, swatch="skin_dark", parent=root)
    body.rotation_euler = (0.14, 0, 0)  # hunched forward
    K.box("Belt", size=(1.2, 0.84, 0.18), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Loin", size=(1.1, 0.8, 0.42), location=(0, 0, -0.4), pivot=(0, 0, -1), taper=(0.95, 0.95),
          swatch="leather_d", parent=body)
    K.box("Strap", size=(0.16, 0.82, 1.1), location=(0.22, 0, 0.05), pivot=(0, 0, -1), swatch="leather", parent=body)
    body.children[-1].rotation_euler = (0, 0.55, 0)  # a strap across the chest
    # the lion pelt: a cloak down his back, the mane heaped over the shoulders
    K.box("Pelt", size=(1.4, 0.16, 1.6), location=(0, 0.44, -0.5), pivot=(0, 0, -1), taper=(0.75, 1.0),
          swatch="ochre_d", parent=body)
    K.box("PeltTail", size=(0.18, 0.12, 0.7), location=(0, 0.48, -0.95), pivot=(0, 0, -1), taper=(0.6, 0.6),
          swatch="ochre_d", parent=body)
    K.box("Mane", size=(1.85, 1.05, 0.5), location=(0, 0.08, 0.78), pivot=(0, 0, -1), taper=(0.78, 0.85), bevel=0.04,
          swatch="rust", parent=body)
    # the horn and the chain at the belt (right hip)
    horn = K.cone("Horn", radius=0.11, depth=0.62, verts=6, location=(-0.62, -0.1, 0.02), pivot=(0, 0, -1),
                  swatch="bone", parent=body)
    horn.rotation_euler = (0.2, -1.2, 0)
    K.box("HornMouth", size=(0.2, 0.2, 0.06), location=(0, 0, -0.04), pivot=(0, 0, -1), swatch="bronze", parent=horn)
    for k in range(3):
        K.box("Chain%d" % k, size=(0.42 - k * 0.06, 0.12, 0.09), location=(0.55, -0.2 + k * 0.08, -0.12 - k * 0.1),
              pivot=(0, 0, -1), swatch="iron_d" if k % 2 else "iron", parent=body)

    head = K.box("Head", size=(0.56, 0.54, 0.52), location=(0, -0.2, 1.1), pivot=(0, 0, -1), swatch="skin_dark",
                 parent=body)
    K.box("Beard", size=(0.5, 0.14, 0.34), location=(0, -0.26, -0.08), pivot=(0, 0, -1), taper=(0.7, 1.0),
          swatch="ink", parent=head)
    K.box("Eyes", size=(0.34, 0.03, 0.06), location=(0, -0.275, 0.3), pivot=(0, 0, -1), swatch="glow_hazard_orange",
          parent=head)
    # the lion's head as a hood: skull over his brow, snout jutting over his face, fangs, ears, its mane round it
    hood = K.box("Hood", size=(0.9, 0.86, 0.5), location=(0, 0.04, 0.36), pivot=(0, 0, -1), taper=(0.85, 0.8),
                 bevel=0.03, swatch="ochre", parent=head)
    K.box("Snout", size=(0.5, 0.38, 0.3), location=(0, -0.48, 0.02), pivot=(0, 0, -1), taper=(0.85, 0.9),
          swatch="straw", parent=hood)
    K.box("Nose", size=(0.18, 0.06, 0.11), location=(0, -0.68, 0.18), pivot=(0, 0, -1), swatch="ink", parent=hood)
    for x in (-0.13, 0.13):
        K.box("Fang", size=(0.07, 0.07, 0.2), location=(x, -0.6, -0.14), pivot=(0, 0, -1), taper=(0.2, 0.2),
              swatch="bone", parent=hood)
        ear = K.box("Ear", size=(0.2, 0.09, 0.22), location=(x * 2.5, 0.08, 0.42), pivot=(0, 0, -1), taper=(0.4, 0.6),
                    swatch="ochre_d", parent=hood)
        ear.rotation_euler = (0, 0, 0.3 if x > 0 else -0.3)
        K.box("HoodEye", size=(0.12, 0.03, 0.07), location=(x * 1.2, -0.44, 0.3), pivot=(0, 0, -1), swatch="ink",
              parent=hood)
    # the mane: shaggy tufts radiating round the hood, so his outline has a ruff nobody else has
    for k in range(7):
        ang = -1.35 + k * 0.45
        tuft = K.box("Tuft%d" % k, size=(0.26, 0.3, 0.55), location=(math.sin(ang) * 0.5, 0.2, 0.12 + math.cos(ang) * 0.42),
                     pivot=(0, 0, -1), taper=(0.25, 0.4), swatch="rust" if k % 2 else "ochre_d", parent=hood)
        tuft.rotation_euler = (0, ang, 0)
    K.box("HoodMane", size=(1.15, 0.55, 0.8), location=(0, 0.3, -0.4), pivot=(0, 0, -1), taper=(0.85, 0.7),
          swatch="rust", parent=hood)

    for side, x, sgn in (("L", 0.74, 1), ("R", -0.74, -1)):
        arm = K.box("Arm" + side, size=(0.32, 0.34, 1.0), location=(x, -0.05, 0.92), pivot=(0, 0, 1), taper=(1.15, 1.1),
                    swatch="skin_dark", parent=body)
        arm.rotation_euler = (0, 0, sgn * 0.1)
        K.box("Bracer" + side, size=(0.38, 0.4, 0.34), location=(0, 0, -0.56), pivot=(0, 0, 1), swatch="iron",
              parent=arm)
        K.box("Hand" + side, size=(0.34, 0.36, 0.26), location=(0, -0.02, -1.0), pivot=(0, 0, 1), swatch="skin_dark",
              parent=arm)
        leg = K.box("Leg" + side, size=(0.42, 0.44, 1.1), location=(x * 0.5, 0.05, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.2, 1.1), swatch="mud_dark", parent=root)
        for k in range(2):  # fur wraps
            K.box("Wrap%s%d" % (side, k), size=(0.47, 0.49, 0.12), location=(0, 0, -0.35 - k * 0.35), pivot=(0, 0, 1),
                  swatch="leather", parent=leg)
        K.box("Foot" + side, size=(0.46, 0.62, 0.22), location=(0, -0.09, -1.1), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)

    # the goad: upright in the left fist, taller than him, a spear blade with a back-hook at the top
    arm_l = [o for o in root.children_recursive if o.name == "ArmL"][0]
    shaft = K.cylinder("Goad", radius=0.06, depth=4.1, verts=6, location=(0, -0.12, -1.95), pivot=(0, 0, -1),
                       swatch="wood_dark", parent=arm_l)
    shaft.rotation_euler = (-0.22, 0, -0.1)  # counter the hunch and the arm: upright
    K.box("Grip", size=(0.15, 0.15, 0.5), location=(0, 0, 1.7), pivot=(0, 0, -1), swatch="leather", parent=shaft)
    K.box("Socket", size=(0.16, 0.16, 0.22), location=(0, 0, 4.05), pivot=(0, 0, -1), swatch="iron_d", parent=shaft)
    K.box("Blade", size=(0.18, 0.06, 0.62), location=(0, 0, 4.25), pivot=(0, 0, -1), taper=(0.15, 1.0),
          swatch="steel_l", parent=shaft)
    hook = K.box("Hook", size=(0.07, 0.07, 0.42), location=(0, 0, 4.2), pivot=(0, 0, -1), taper=(0.4, 1.0),
                 swatch="steel", parent=shaft)
    hook.rotation_euler = (-1.0, 0, 0)  # curving back and down from the socket
    K.box("HookTip", size=(0.06, 0.06, 0.24), location=(0, 0, 0.38), pivot=(0, 0, -1), taper=(0.2, 1.0),
          swatch="steel_l", parent=hook).rotation_euler = (-1.6, 0, 0)
    return root


root = build()
info = K.review_render(root, "en_beastlord", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_BeastLord")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_beastlord")
result = info
