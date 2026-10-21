"""EN_BeastHandler — the Beast Handler, Tier 2 (docs/GLADIATORS.md "The Beastmaster", Phase 5.15). Budget <= 300 tris.

A burly, bald handler in a leather apron who drives beasts into the sand: a coiled whip in his right fist, a fistful of
iron chain leashes in his left (his two Pit Curs walk on them), a spiked iron collar round his own neck as a brag. The
magenta armband and belt are the enemy accent. Silhouette: the wide shoulders and bare head, the whip coil, the chains
hanging from the left fist. Rigid parts on the shared names; front = -Y (Unity +Z).
    Tools/bheadless BlenderSrc/enemies/en_beasthandler.py [export=0]
"""
import importlib
import gladiator_kit as K
importlib.reload(K)

HIP_Z = 0.42


def build():
    K.clear_scene()
    root = K.empty("EN_BeastHandler")
    body = K.box("Body", size=(0.52, 0.34, 0.5), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.2, 1.05),
                 swatch="skin_light", parent=root)
    # iteration 2: a shorter apron (it hid the body) and lighter skin (he was one brown box)
    K.box("Apron", size=(0.42, 0.06, 0.46), location=(0, -0.17, 0.36), pivot=(0, 0, 1), taper=(1.0, 1.0),
          swatch="leather_l", parent=body)
    K.box("Belt", size=(0.56, 0.38, 0.09), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    K.box("Collar", size=(0.34, 0.3, 0.07), location=(0, -0.01, 0.5), pivot=(0, 0, -1), swatch="iron", parent=body)

    head = K.box("Head", size=(0.32, 0.31, 0.32), location=(0, -0.02, 0.56), pivot=(0, 0, -1), swatch="skin_light",
                 parent=body)
    K.box("Brow", size=(0.3, 0.05, 0.06), location=(0, -0.16, 0.2), pivot=(0, 0, -1), swatch="skin_dark", parent=head)
    K.box("Eyes", size=(0.2, 0.03, 0.04), location=(0, -0.16, 0.15), pivot=(0, 0, -1), swatch="ink", parent=head)
    K.box("Stubble", size=(0.26, 0.04, 0.1), location=(0, -0.155, 0.0), pivot=(0, 0, -1), swatch="mud_dark",
          parent=head)

    # right arm (-X): the whip, coiled in the fist with a tail hanging
    arm_r = K.box("ArmR", size=(0.14, 0.14, 0.38), location=(-0.33, 0, 0.46), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    K.box("Armband", size=(0.16, 0.16, 0.08), location=(0, 0, -0.1), pivot=(0, 0, 0), swatch="accent_magenta",
          parent=arm_r)
    K.cylinder("WhipCoil", radius=0.16, depth=0.08, verts=8, location=(0, -0.07, -0.42), pivot=(0, 0, 0), axis="Y",
               swatch="leather_d", parent=arm_r)  # iteration 2: facing front (edge-on, it vanished)
    lash = K.box("Lash", size=(0.035, 0.035, 0.9), location=(0, -0.02, -0.42), pivot=(0, 0, -1), swatch="leather_d",
                 parent=arm_r)  # the lash, slung up over his right shoulder: a line above the silhouette
    lash.rotation_euler = (0.35, 0.55, 0)
    K.box("LashEnd", size=(0.03, 0.03, 0.34), location=(0, 0, 0.9), pivot=(0, 0, 1), swatch="leather_d", parent=lash)

    # left arm (+X): the chain leashes hanging from the fist
    arm_l = K.box("ArmL", size=(0.14, 0.14, 0.38), location=(0.33, 0, 0.46), pivot=(0, 0, 1), swatch="skin_light",
                  parent=body)
    K.box("FistL", size=(0.15, 0.15, 0.12), location=(0, 0, -0.4), pivot=(0, 0, 0), swatch="skin_light", parent=arm_l)
    for k, (x, y) in enumerate(((0.03, -0.03), (-0.03, 0.03))):
        K.box("Chain%d" % k, size=(0.035, 0.035, 0.38), location=(x, y, -0.44), pivot=(0, 0, 1), swatch="iron",
              parent=arm_l)

    for side, x in (("L", 0.12), ("R", -0.12)):
        leg = K.box("Leg" + side, size=(0.16, 0.17, 0.4), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="leather", parent=root)
        K.box("Boot" + side, size=(0.17, 0.25, 0.14), location=(0, -0.03, -0.42), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)
    return root


root = build()
info = K.review_render(root, "en_beasthandler", size=420)
info["tris"] = K.tri_count(K.descendants(root))
if not ("ARGS" in globals() and ARGS.get("export") == "0"):
    info["fbx"] = K.export_fbx(root, "Enemies/EN_BeastHandler")
    info["blend"] = K.save_blend("en_beasthandler")
result = info
