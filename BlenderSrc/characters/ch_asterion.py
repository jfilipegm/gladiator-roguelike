"""CH_Asterion — Asterion, the Chained (CLAUDE.md §6.7, docs/GLADIATORS.md): the labyrinth's prisoner, a chibi minotaur
dragging the chain he tore out of its wall. Tragic, not monstrous: the pale hide of his father the white bull, a head
hung low and forward, sad dark eyes, a nose ring, a rope-worn bronze collar with a broken ring, a broken shackle, a few
pale scars, his right horn's tip chipped. No fangs, no spikes, no red.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged, plus the chain:
  CH_Asterion (root, base)
    Body   (hip centre)      a heavy pale torso (bone chest), a charcoal mane on the shoulder hump, a Minoan-blue kilt
                             with an ochre hem on a rope belt, the bronze collar (a rope band worn into it, a broken
                             ring), a bull's tail, a cyan labyrinth branded on his back
      Head (neck)            low and forward of the hump: a long bone snout, nose ring, sad ink eyes, charcoal ears and
                             forelock, lyre horns (bone, charcoal tips) ~1.25 m tip to tip
      ArmL (left shoulder)   long and heavy; a broken shackle: an iron cuff, a snapped link hanging from it
      ArmR (right shoulder)  long and heavy; an iron cuff, the chain wound twice round the forearm
    LegL, LegR (hip joints)  short thick legs, ink hooves
    Chain  (root child, at the right hand's rest position, behind the fist; not under ArmR, so it stays on the ground
            while he runs: the weapon code hides it while it draws the swung chain)
      -> ChainSeg0 -> ChainSeg1 -> ChainSeg2 -> ChainSeg3 (nested; two bronze links each, pivot at its joint)
           -> ChainEnd (the stone block of the labyrinth's wall, the ring bolt at its joint)
Front = -Y, asset's left = +X. Horn tips 1.81 m (the Nobody is 1.5 m, the Murder Machine 2.2 m), 1,148 tris.

The chain: every ChainSegN runs from its pivot along its local -Z (Unity: local -Y) for SEG_LEN (0.4 m), the next joint
at local (0, 0, -SEG_LEN). Bind rotations (about local X, bending backward): Seg0 hangs from the cuff, Seg1 reaches the
ground, Seg2 lies flat behind him, Seg3 rises to the stone's ring bolt; ChainEnd turns back level so the stone sits on
the ground. Small yaws on the ground. The links and the stone are WP_LabyrinthChain's (weapons/wp_labyrinthchain.py).
Silhouette marks (64 px test vs the Murder Machine, the Beastmaster, the Blood Knight and the Brute): the lyre horns'
width, the hump with the head forward and low, the stone on the ground behind him. Readability: the cyan labyrinth on
his back (the camera behind sees it).
    Tools/bheadless BlenderSrc/characters/ch_asterion.py [export=0]
"""
import importlib
import math

from mathutils import Matrix, Vector

import gladiator_kit as K
importlib.reload(K)
W = importlib.reload(importlib.import_module("weapons.wp_labyrinthchain"))  # the chain's links and stone

HIP_Z = 0.50
SHOULDER_Z = 0.52   # relative to Body
NECK_Z = 0.50       # relative to Body
SEG_LEN = 2 * W.PITCH  # one chain segment: two links, joint to joint (0.4 m)
LINE_Z = W.LINK[1] / 2 - 0.01  # the chain's line above the ground: the upright links touch it


def horn(head, name, s, segs):
    """A horn of straight tapered prisms, each a child of the head at its start point. segs: (dir, length, radius,
    taper, swatch); each segment starts 2 cm back along its own direction so the bends don't open."""
    p = Vector((s * 0.24, -0.12, 0.40))
    for k, (d, ln, rad, taper, swatch) in enumerate(segs):
        d = Vector((s * d[0], d[1], d[2])).normalized()
        start = p - d * 0.02
        seg = K.cylinder(name + ("", "Mid", "Tip")[k], radius=rad, depth=ln + 0.02, verts=6, location=start,
                         pivot=(0, 0, -1), taper=taper, swatch=swatch, parent=head)
        seg.rotation_euler = Vector((0, 0, 1)).rotation_difference(d).to_euler()
        p = p + d * ln


def build():
    K.clear_scene()
    root = K.empty("CH_Asterion")

    # --- body: a heavy pale torso (V: wide at the shoulders), the charcoal mane, kilt, collar, tail, the brand
    body = K.box("Body", size=(0.62, 0.42, 0.62), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.45, 1.15), bevel=0.04, swatch="ash", parent=root)
    K.box("Chest", size=(0.42, 0.05, 0.30), location=(0, -0.225, 0.16), pivot=(0, 0, -1), taper=(1.15, 1.0),
          swatch="bone", parent=body)
    K.box("Hump", size=(0.98, 0.44, 0.26), location=(0, 0.06, 0.50), pivot=(0, 0, -1), taper=(0.62, 0.62),
          bevel=0.03, swatch="charcoal", parent=body)  # the shoulders' mane: the head hangs forward of it
    K.box("Belt", size=(0.66, 0.46, 0.07), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="rope", parent=body)
    K.box("Kilt", size=(0.68, 0.48, 0.20), location=(0, 0, 0.04), pivot=(0, 0, 1), taper=(0.92, 0.92),
          swatch="cloth_blue", parent=body)
    K.box("KiltHem", size=(0.70, 0.50, 0.05), location=(0, 0, -0.165), pivot=(0, 0, -1), swatch="ochre",
          parent=body)
    K.box("ScarChest", size=(0.22, 0.02, 0.028), location=(-0.06, -0.252, 0.34), pivot=(0, 0, 0),
          swatch="white", parent=body).rotation_euler = (0, math.radians(-32), 0)
    # the collar: bronze, a rope band worn into it, a broken ring hanging at his left
    collar = K.cylinder("Collar", radius=0.25, depth=0.14, verts=8, location=(0, -0.14, 0.50), pivot=(0, 0, 0),
                        swatch="bronze", parent=body)
    collar.rotation_euler = (math.radians(14), 0, math.radians(22.5))  # the neck juts forward: front lower
    K.cylinder("CollarRope", radius=0.262, depth=0.05, verts=8, location=(0, 0, -0.03), pivot=(0, 0, 0),
               swatch="rope", parent=collar)
    ring = K.box("CollarRing", size=(0.035, 0.035, 0.12), location=(0.22, -0.31, 0.45), pivot=(0, 0, 1),
                 swatch="bronze_l", parent=body)
    ring.rotation_euler = (0, math.radians(-12), 0)
    K.box("CollarRingHalf", size=(0.09, 0.035, 0.035), location=(0.035, 0, -0.11), pivot=(-1, 0, 0),
          swatch="bronze_l", parent=ring).rotation_euler = (0, math.radians(-25), 0)  # snapped open
    tail = K.box("Tail", size=(0.05, 0.05, 0.34), location=(0, 0.21, 0.14), pivot=(0, 0, 1), swatch="ash",
                 parent=body)
    tail.rotation_euler = (math.radians(28), 0, 0)
    K.box("TailTuft", size=(0.10, 0.10, 0.14), location=(0, 0, -0.32), pivot=(0, 0, 1), taper=(1.4, 1.4),
          swatch="charcoal", parent=tail)
    # the "that's me" mark for the camera behind him: a cyan labyrinth branded on his upper back
    for k, (x, z, sx, sz) in enumerate(((-0.13, 0.34, 0.045, 0.30), (0.0, 0.47, 0.30, 0.045),
                                        (0.13, 0.34, 0.045, 0.30), (0.05, 0.21, 0.20, 0.045),
                                        (-0.04, 0.29, 0.045, 0.18))):
        K.box("Labyrinth%d" % k, size=(sx, 0.04, sz), location=(x, 0.226 + 0.05 * z, z), pivot=(0, 0, 0),
              swatch="player_cyan", parent=body)

    # --- head: the white bull's head, low and forward of the hump: a broad poll, a long bone snout
    head = K.box("Head", size=(0.56, 0.46, 0.46), location=(0, -0.14, NECK_Z), pivot=(0, 0, -1), taper=(1.08, 0.94),
                 bevel=0.04, swatch="ash", parent=body)
    head.data.transform(Matrix.Translation((0, -0.10, 0.02)))  # the skull sits forward of the neck joint
    K.box("Snout", size=(0.42, 0.30, 0.25), location=(0, -0.40, 0.0), pivot=(0, 0, -1), taper=(0.94, 0.86),
          swatch="bone", parent=head)
    for side, x in (("L", 0.09), ("R", -0.09)):
        K.box("Nostril" + side, size=(0.075, 0.02, 0.05), location=(x, -0.553, 0.15), pivot=(0, 0, 0),
              swatch="ink", parent=head)
    # the nose ring: a U of bronze hanging under the snout
    K.box("NoseRing", size=(0.15, 0.035, 0.035), location=(0, -0.56, -0.03), pivot=(0, 0, 0), swatch="bronze_l",
          parent=head)
    for side, x in (("L", 0.0575), ("R", -0.0575)):
        K.box("NoseRing" + side, size=(0.035, 0.035, 0.10), location=(x, -0.56, 0.02), pivot=(0, 0, 0),
              swatch="bronze_l", parent=head)
    K.box("Brow", size=(0.58, 0.10, 0.08), location=(0, -0.33, 0.385), pivot=(0, 0, 0), swatch="ash", parent=head)
    for side, x, droop in (("L", 0.15, 16), ("R", -0.15, -16)):
        eye = K.box("Eye" + side, size=(0.10, 0.03, 0.065), location=(x, -0.335, 0.30), pivot=(0, 0, 0),
                    swatch="ink", parent=head)
        eye.rotation_euler = (0, math.radians(droop), 0)  # the outer corners droop: sad, not angry
    K.box("ScarBrow", size=(0.03, 0.02, 0.20), location=(0.17, -0.345, 0.31), pivot=(0, 0, 0), swatch="white",
          parent=head).rotation_euler = (0, math.radians(12), 0)
    K.box("Forelock", size=(0.30, 0.22, 0.10), location=(0, -0.17, 0.46), pivot=(0, 0, -1), taper=(0.7, 0.7),
          swatch="charcoal", parent=head)
    for side, s in (("L", 1), ("R", -1)):  # ears out sideways beside the eyes, under the horns, drooping
        ear = K.box("Ear" + side, size=(0.20, 0.06, 0.12), location=(s * 0.27, -0.17, 0.25), pivot=(-s, 0, 0),
                    swatch="charcoal", parent=head)
        ear.rotation_euler = (0, math.radians(s * 12), 0)
    # lyre horns: out from the poll, up and forward, the tips flaring out again (the Blood Knight's curl in); his
    # right tip is chipped off short
    for side, s in (("L", 1), ("R", -1)):
        tip = (0.13, 0.0001) if s > 0 else (0.05, 0.6)
        horn(head, "Horn" + side, s, (((1, -0.2, 0.25), 0.20, 0.085, 0.85, "bone"),
                                      ((0.25, -0.35, 1.0), 0.30, 0.072, 0.75, "bone"),
                                      ((1, -0.1, 0.35), tip[0], 0.054, tip[1], "charcoal")))

    # --- arms: long and heavy, charcoal fur on the shoulders, iron cuffs at the wrists
    arms = {}
    for side, s in (("L", 1), ("R", -1)):
        arm = K.box("Arm" + side, size=(0.25, 0.27, 0.32), location=(s * 0.52, 0.0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.1, 1.1), swatch="ash", parent=body)
        K.box("ShoulderFur" + side, size=(0.36, 0.36, 0.17), location=(s * 0.01, 0.01, 0.04), pivot=(0, 0, 0),
              taper=(0.8, 0.85), swatch="charcoal", parent=arm)
        K.box("Forearm" + side, size=(0.30, 0.30, 0.26), location=(0, 0, -0.31), pivot=(0, 0, 1), taper=(0.95, 0.95),
              swatch="ash", parent=arm)
        K.cylinder("Cuff" + side, radius=0.175, depth=0.11, verts=6, location=(0, 0, -0.53), pivot=(0, 0, 0),
                   swatch="iron", parent=arm)
        K.box("Fist" + side, size=(0.25, 0.25, 0.18), location=(0, -0.01, -0.56), pivot=(0, 0, 1), swatch="ash",
              parent=arm)
        arms[side] = arm
    K.box("ScarArm", size=(0.02, 0.03, 0.18), location=(0.127, 0.0, -0.15), pivot=(0, 0, 0), swatch="white",
          parent=arms["L"]).rotation_euler = (math.radians(25), 0, 0)
    # the broken shackle (left): a snapped link hanging off the cuff, half a link below it
    K.box("BrokenLink", size=(0.04, 0.11, 0.13), location=(0, 0.06, -0.58), pivot=(0, 0, 1), swatch="iron",
          parent=arms["L"]).rotation_euler = (math.radians(10), 0, math.radians(15))
    K.box("BrokenHalf", size=(0.11, 0.04, 0.07), location=(0.01, 0.07, -0.68), pivot=(0, 0, 1), swatch="iron_d",
          parent=arms["L"]).rotation_euler = (0, math.radians(30), 0)
    # the chain wound twice round the right forearm (he holds it: it's his weapon)
    for k, z in enumerate((-0.36, -0.44)):
        K.box("ChainWrap%d" % k, size=(0.33, 0.33, 0.05), location=(0, 0, z), pivot=(0, 0, 0), swatch="bronze",
              parent=arms["R"]).rotation_euler = (math.radians(8 if k else -8), 0, 0)

    # --- the chain: a child of the root at the right hand's rest position (the cuff's bottom, behind the fist),
    # down to the ground, flat behind him, then up to the stone's ring bolt
    bpy_update()
    hand = arms["R"].matrix_world @ Vector((0, 0.12, -0.585))
    chain = K.empty("Chain", location=tuple(hand), parent=root)
    drop = hand.z - LINE_Z
    a0 = math.radians(35)
    a1 = math.acos(max(-1.0, min(1.0, drop / SEG_LEN - math.cos(a0))))
    rise = math.asin((W.BLOCK[1] / 2 - LINE_Z) / SEG_LEN)  # the stone's centre, ring and all, sits that high
    segs = ((a0, 0.0), (a1 - a0, 0.0), (math.radians(90) - a1, math.radians(4)), (rise, math.radians(-5)))
    parent = chain
    for k, (bend, yaw) in enumerate(segs):
        parent = W.link_run("ChainSeg%d" % k, 2, along=(0, 0, -1), up=(0, 1, 0),
                            location=(0, 0, 0) if k == 0 else (0, 0, -SEG_LEN), parent=parent)
        parent.rotation_euler = (bend, yaw, 0)
    end = W.weight_on_chain("ChainEnd")
    end.parent = parent
    end.location = (0, 0, -SEG_LEN)
    end.rotation_euler = (-rise, 0, 0)  # level again: the stone sits on the ground

    # --- legs: short and thick, ink hooves
    for side, s in (("L", 1), ("R", -1)):
        leg = K.box("Leg" + side, size=(0.25, 0.27, 0.28), location=(s * 0.17, 0, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.08, 1.08), swatch="ash", parent=root)
        K.box("Shin" + side, size=(0.25, 0.27, 0.16), location=(0, 0, -0.27), pivot=(0, 0, 1), taper=(1.05, 1.05),
              swatch="ash", parent=leg)
        K.box("Hoof" + side, size=(0.24, 0.30, 0.10), location=(0, -0.03, -0.50), pivot=(0, 0, -1), taper=(0.92, 0.9),
              swatch="ink", parent=leg)

    return root


def bpy_update():
    import bpy
    bpy.context.view_layer.update()


root = build()
info = K.review_render(root, "ch_asterion", size=420)
info["tris"] = K.tri_count(K.descendants(root))
info["parts"] = {o.name: round(K.tri_count([o]), 0) for o in K.descendants(root) if o.type == "MESH"}
if not ("ARGS" in globals() and ARGS.get("export") == "0"):  # noqa: F821
    info["fbx"] = K.export_fbx(root, "Characters/CH_Asterion")
    info["blend"] = K.save_blend("ch_asterion")
result = info
