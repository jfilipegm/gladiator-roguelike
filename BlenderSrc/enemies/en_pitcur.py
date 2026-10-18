"""EN_PitCur — Pit Cur, the fast Tier-1 dog that lunges (CLAUDE.md §6.3). Budget <= 300 tris.

Low, long quadruped: mangy mud-brown fur, dark hackles, pointed ears, tail up, bone teeth, yellow eyes, spiked
magenta collar = enemy accent patch. Rigid parts keep the shared names so EnemyManager drives it like the others:
front legs = ArmL/ArmR (children of Body, swing with the torso), hind legs = LegL/LegR (children of the root).
Lunge pose: front legs reach forward, hind legs kick back.
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

BODY_Z = 0.30   # belly height
LEG = 0.30      # leg length (shoulder/hip to ground)


def build():
    K.clear_scene()
    root = K.empty("EN_PitCur")
    body = K.box("Body", size=(0.26, 0.60, 0.22), location=(0, 0.04, BODY_Z), pivot=(0, 0, -1), taper=(0.9, 0.95),
                 swatch="mud", parent=root)
    K.box("Hackles", size=(0.12, 0.46, 0.07), location=(0, -0.02, 0.21), pivot=(0, 0, -1), taper=(0.3, 0.85),
          swatch="mud_dark", parent=body)
    K.box("Collar", size=(0.30, 0.09, 0.27), location=(0, -0.27, -0.02), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    tail = K.box("Tail", size=(0.06, 0.06, 0.30), location=(0, 0.29, 0.17), pivot=(0, 0, -1), taper=(0.5, 0.5),
                 swatch="mud_dark", parent=body)
    tail.rotation_euler = (-0.75, 0, 0)  # up and back

    head = K.box("Head", size=(0.24, 0.24, 0.22), location=(0, -0.36, 0.12), pivot=(0, 0, -1), swatch="mud",
                 parent=body)
    K.box("Snout", size=(0.14, 0.18, 0.10), location=(0, -0.19, 0.0), pivot=(0, 0, -1), taper=(0.85, 1.0),
          swatch="dirt_light", parent=head)
    K.box("Nose", size=(0.07, 0.04, 0.05), location=(0, -0.29, 0.06), pivot=(0, 0, -1), swatch="ink", parent=head)
    K.box("Teeth", size=(0.12, 0.03, 0.03), location=(0, -0.275, -0.025), pivot=(0, 0, -1), swatch="bone",
          parent=head)
    for side, x in (("L", 0.075), ("R", -0.075)):
        ear = K.box("Ear" + side, size=(0.07, 0.05, 0.14), location=(x, 0.05, 0.20), pivot=(0, 0, -1),
                    taper=(0.15, 0.4), swatch="mud_dark", parent=head)
        ear.rotation_euler = (0.25, 0, 0.35 if x > 0 else -0.35)
        K.box("Eye" + side, size=(0.06, 0.03, 0.05), location=(x, -0.12, 0.12), pivot=(0, 0, -1),
              swatch="hazard_yellow", parent=head)

    for side, x in (("L", 0.085), ("R", -0.085)):
        front = K.box("Arm" + side, size=(0.08, 0.09, LEG + 0.02), location=(x, -0.21, 0.02), pivot=(0, 0, 1),
                      swatch="mud", parent=body)
        K.box("Paw" + side, size=(0.10, 0.13, 0.05), location=(0, -0.025, -LEG - 0.02), pivot=(0, 0, -1),
              swatch="dirt_light", parent=front)
        hind = K.box("Leg" + side, size=(0.09, 0.13, LEG + 0.04), location=(x, 0.25, BODY_Z + 0.04), pivot=(0, 0, 1),
                     taper=(1.25, 1.5), swatch="mud", parent=root)
        K.box("Foot" + side, size=(0.10, 0.13, 0.05), location=(0, -0.02, -LEG - 0.04), pivot=(0, 0, -1),
              swatch="dirt_light", parent=hind)
    return root


root = build()
info = K.review_render(root, "en_pitcur", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_PitCur")
info["blend"] = K.save_blend("en_pitcur")
result = info
