"""CH_Wolf — the Beastmaster's wolf (docs/GLADIATORS.md, Phase 5.15), drawn by CompanionPack. Budget <= 300 tris.

A grey timber wolf, bigger and leaner than the Pit Cur, in the player's colours: a cyan collar and cyan eyes (never
the enemies' magenta accent or yellow eyes), so it reads as an ally among a horde of curs. Rigid parts with the Pit
Cur's names so the same run cycle works: front legs = ArmL/ArmR (children of Body), hind legs = LegL/LegR (root),
Head (a child of Body: the bite lunges it).
    Tools/bheadless BlenderSrc/characters/ch_wolf.py [export=0]
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

BODY_Z = 0.36   # belly height
LEG = 0.36      # leg length (shoulder/hip to ground)


def build():
    K.clear_scene()
    root = K.empty("CH_Wolf")
    body = K.box("Body", size=(0.3, 0.74, 0.26), location=(0, 0.04, BODY_Z), pivot=(0, 0, -1), taper=(0.9, 0.95),
                 swatch="stone_grey", parent=root)
    K.box("Back", size=(0.18, 0.6, 0.08), location=(0, 0.0, 0.24), pivot=(0, 0, -1), taper=(0.5, 0.9),
          swatch="slate", parent=body)
    K.box("Belly", size=(0.26, 0.5, 0.05), location=(0, 0.02, -0.01), pivot=(0, 0, -1), swatch="ash", parent=body)
    K.box("Collar", size=(0.34, 0.1, 0.31), location=(0, -0.32, -0.02), pivot=(0, 0, -1), swatch="player_cyan",
          parent=body)
    tail = K.box("Tail", size=(0.08, 0.08, 0.36), location=(0, 0.36, 0.18), pivot=(0, 0, -1), taper=(0.5, 0.5),
                 swatch="slate", parent=body)
    tail.rotation_euler = (-1.1, 0, 0)  # out and back, a little up

    head = K.box("Head", size=(0.27, 0.27, 0.25), location=(0, -0.44, 0.14), pivot=(0, 0, -1), swatch="stone_grey",
                 parent=body)
    K.box("Snout", size=(0.15, 0.22, 0.11), location=(0, -0.23, 0.0), pivot=(0, 0, -1), taper=(0.8, 1.0),
          swatch="ash", parent=head)
    K.box("Nose", size=(0.07, 0.04, 0.05), location=(0, -0.35, 0.07), pivot=(0, 0, -1), swatch="ink", parent=head)
    K.box("Fangs", size=(0.13, 0.03, 0.03), location=(0, -0.33, -0.025), pivot=(0, 0, -1), swatch="white",
          parent=head)
    for side, x in (("L", 0.085), ("R", -0.085)):
        K.box("Ear" + side, size=(0.08, 0.06, 0.17), location=(x, 0.05, 0.23), pivot=(0, 0, -1), taper=(0.15, 0.4),
              swatch="slate", parent=head)
        K.box("Eye" + side, size=(0.06, 0.03, 0.05), location=(x, -0.135, 0.14), pivot=(0, 0, -1),
              swatch="glow_cyan", parent=head)

    for side, x in (("L", 0.1), ("R", -0.1)):
        front = K.box("Arm" + side, size=(0.09, 0.1, LEG + 0.02), location=(x, -0.25, 0.02), pivot=(0, 0, 1),
                      swatch="stone_grey", parent=body)
        K.box("Paw" + side, size=(0.11, 0.14, 0.05), location=(0, -0.025, -LEG - 0.02), pivot=(0, 0, -1),
              swatch="ash", parent=front)
        hind = K.box("Leg" + side, size=(0.1, 0.15, LEG + 0.04), location=(x, 0.3, BODY_Z + 0.04), pivot=(0, 0, 1),
                     taper=(1.25, 1.5), swatch="stone_grey", parent=root)
        K.box("Foot" + side, size=(0.11, 0.14, 0.05), location=(0, -0.02, -LEG - 0.04), pivot=(0, 0, -1),
              swatch="ash", parent=hind)
    return root


root = build()
info = K.review_render(root, "ch_wolf", size=420)
info["tris"] = K.tri_count(K.descendants(root))
if not ("ARGS" in globals() and ARGS.get("export") == "0"):
    info["fbx"] = K.export_fbx(root, "Characters/CH_Wolf")
    info["blend"] = K.save_blend("ch_wolf")
result = info
