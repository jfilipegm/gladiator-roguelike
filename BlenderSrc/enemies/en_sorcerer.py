"""EN_Sorcerer — the Grand Arena's spell-caster (docs/ARENAS.md §4, behaviour Caster). Budget <= 300 tris.

A thin seer in a deep purple robe with gold hems, a TALL pointed hood (the silhouette's mark: nothing else in the
horde has a spike for a head), a shadowed face with two orange-red eyes, the magenta belt (the enemy accent). Right
hand (-X): a straight staff topped by a glowing orange-red orb (it opens the rune under you: hurts = orange-red). Not
the Ashen Oracle (ash robe, bone mask, a crook with a censer). Rigid parts on the shared names; front = -Y.
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.46


def build():
    K.clear_scene()
    root = K.empty("EN_Sorcerer")
    body = K.box("Body", size=(0.36, 0.26, 0.5), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.1, 1.05),
                 swatch="purple_d", parent=root)
    K.box("Robe", size=(0.42, 0.32, 0.5), location=(0, 0, 0.04), pivot=(0, 0, 1), taper=(0.78, 0.8), swatch="purple_d",
          parent=body)  # flaring to the hem below
    K.box("Hem", size=(0.56, 0.42, 0.06), location=(0, 0, -0.46), pivot=(0, 0, 0), swatch="gold_d", parent=body)
    K.box("Belt", size=(0.4, 0.3, 0.07), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Stole", size=(0.1, 0.03, 0.5), location=(0, -0.14, 0.48), pivot=(0, 0, 1), swatch="gold", parent=body)

    head = K.box("Head", size=(0.32, 0.32, 0.32), location=(0, -0.01, 0.5), pivot=(0, 0, -1), swatch="ink", parent=body)
    K.box("Hood", size=(0.4, 0.4, 0.36), location=(0, 0.03, 0.0), pivot=(0, 0, -1), taper=(0.75, 0.8), swatch="purple",
          parent=head)
    K.cone("Point", radius=0.2, depth=0.62, verts=4, location=(0, 0.06, 0.34), pivot=(0, 0, -1), swatch="purple",
           parent=head).rotation_euler = (0.18, 0, 0.785)
    for side, x in (("L", 0.065), ("R", -0.065)):
        K.box("Eye" + side, size=(0.06, 0.03, 0.04), location=(x, -0.165, 0.13), pivot=(0, 0, -1),
              swatch="glow_hazard_orange", parent=head)

    for side, x in (("L", 0.23), ("R", -0.23)):
        arm = K.box("Arm" + side, size=(0.13, 0.14, 0.36), location=(x, 0, 0.44), pivot=(0, 0, 1), taper=(1.3, 1.25),
                    swatch="purple_d", parent=body)
        K.box("Cuff" + side, size=(0.18, 0.19, 0.05), location=(0, 0, -0.33), pivot=(0, 0, 0), swatch="gold_d", parent=arm)
        K.box("Hand" + side, size=(0.09, 0.1, 0.09), location=(0, 0, -0.4), pivot=(0, 0, 0), swatch="skin_pale", parent=arm)
        if side == "R":  # the orb staff, upright in the fist
            staff = K.cylinder("Staff", radius=0.028, depth=1.75, verts=4, location=(0, -0.02, -0.95), pivot=(0, 0, -1),
                               swatch="wood_dark", parent=arm)
            K.box("Claw", size=(0.16, 0.16, 0.1), location=(0, 0, 1.72), pivot=(0, 0, -1), taper=(1.4, 1.4),
                  swatch="gold_d", parent=staff)
            K.ico("Orb", radius=0.13, subdiv=1, location=(0, 0, 1.92), swatch="glow_hazard_orange", parent=staff)

    for side, x in (("L", 0.09), ("R", -0.09)):
        leg = K.box("Leg" + side, size=(0.12, 0.13, 0.38), location=(x, 0, HIP_Z), pivot=(0, 0, 1), swatch="ink",
                    parent=root)
        K.box("Foot" + side, size=(0.12, 0.2, 0.07), location=(0, -0.04, -0.46), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)
    return root


root = build()
info = K.review_render(root, "en_sorcerer", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Sorcerer")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_sorcerer")
result = info
