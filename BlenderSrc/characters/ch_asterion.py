"""CH_Asterion — Asterion, the Chained (CLAUDE.md §6.7, docs/GLADIATORS.md): the labyrinth's prisoner, a chibi minotaur
dragging the chain he tore out of its wall. Tragic, not monstrous: a lowered head, soft drooping eyes, one horn snapped,
a nose ring and a rope-worn bronze collar, a broken shackle, a few pale scars. No fangs, no spikes, no red.

Same rigid parts and pivots as CH_Nobody so PlayerPartAnimator drives it unchanged, plus the chain:
  CH_Asterion (root, base)
    Body   (hip centre)      a heavy furred torso, a dark hump of shoulder fur, sackcloth kilt on a rope belt, the
                             bronze collar (a rope band worn into it, the frayed rope end hanging), a bull's tail, a
                             cyan labyrinth on his back
      Head (neck)            the bull head low and forward of the hump: a long pale snout, nose ring, heavy brow,
                             drooping cyan eyes, ears out sideways, wide horns (his LEFT one whole, his RIGHT snapped)
      ArmL (left shoulder)   a broken shackle: an iron cuff, a snapped link hanging from it
      ArmR (right shoulder)  an iron cuff with the chain wound twice round the forearm
        -> Chain (empty at the cuff, behind the fist; the sweep pivot)
             -> ChainSeg0 -> ChainSeg1 -> ... -> ChainSeg6 (nested; each a pair of links, pivot at its joint)
                  -> ChainEnd (a block of labyrinth masonry with the iron staple still in it, a cyan glyph on top)
    LegL, LegR (hip joints)  short thick legs, dark fur shins, split hooves
Front = -Y, asset's left = +X. Horn tip 1.83 m (the Nobody is 1.5 m, the Murder Machine 2.2 m), head ~40 %. 1,112 tris.

The chain: every ChainSegN runs from its pivot along its local -Z (Unity: local -Y) for SEG_LEN, and the next joint
sits at local (0, 0, -SEG_LEN). Bind rotations bend it (about local X: 14, 28.2, 47.8 deg): Seg0/Seg1 hang from the
cuff and bend back to the ground, Seg2 lies flat behind him, Seg3..Seg6 snake a little on the ground (small yaws).
ChainEnd continues Seg6's line. Code can rotate `Chain` for a rigid sweep, or drive each segment (a whip wave, or a
follow-the-leader drag clamped to the ground; the run cycle swings ArmR, so without a drag the trailing chain dips into
the floor and lifts with the arm).
Silhouette marks (64 px test vs the Murder Machine, the Beastmaster, the Blood Knight and the Brute): the wide bull
horns over a hump, the head forward and low, the chain line trailing on the ground behind. Readability: the eyes, the
labyrinth on his back and the glyph on the masonry are player cyan.
    Tools/bheadless BlenderSrc/characters/ch_asterion.py [export=0]
"""
import importlib
import math

from mathutils import Matrix, Vector

import gladiator_kit as K
importlib.reload(K)
W = importlib.reload(importlib.import_module("weapons.wp_chain"))  # the chain's pieces, shared with WP_Chain

HIP_Z = 0.50
SHOULDER_Z = 0.52   # relative to Body
NECK_Z = 0.50       # relative to Body
SEG_LEN = W.SEG_LEN  # one chain segment (two links), joint to joint
TRAIL_SEGS = 5       # segments lying on the ground behind him (after the two hanging ones)


def chain_segment(name, location, rotation, parent):
    seg = W.link_pair(name, location=location, parent=parent)
    seg.rotation_euler = rotation
    return seg


def build():
    K.clear_scene()
    root = K.empty("CH_Asterion")

    # --- body: a heavy torso (V: wide at the shoulders), the hump, kilt, collar, tail, the labyrinth on his back
    body = K.box("Body", size=(0.62, 0.42, 0.62), location=(0, 0, HIP_Z), pivot=(0, 0, -1),
                 taper=(1.45, 1.15), bevel=0.04, swatch="skin_tan", parent=root)
    K.box("ChestFur", size=(0.42, 0.05, 0.30), location=(0, -0.225, 0.16), pivot=(0, 0, -1), taper=(1.15, 1.0),
          swatch="dirt_light", parent=body)
    K.box("Hump", size=(0.98, 0.44, 0.26), location=(0, 0.06, 0.50), pivot=(0, 0, -1), taper=(0.62, 0.62),
          bevel=0.03, swatch="skin_dark", parent=body)  # the shoulders' mane: the head hangs forward of it
    K.box("Belt", size=(0.66, 0.46, 0.07), location=(0, 0, 0.02), pivot=(0, 0, -1), swatch="rope", parent=body)
    K.box("Kilt", size=(0.68, 0.48, 0.22), location=(0, 0, 0.04), pivot=(0, 0, 1), taper=(0.92, 0.92),
          swatch="cloth_sack", parent=body)
    flap = K.box("KiltFlap", size=(0.24, 0.05, 0.26), location=(0.05, -0.245, 0.04), pivot=(0, 0, 1),
                 swatch="cloth_sack", parent=body)
    flap.rotation_euler = (0, math.radians(4), 0)
    # scars: pale lines across the chest
    K.box("ScarChest", size=(0.22, 0.02, 0.028), location=(-0.06, -0.252, 0.34), pivot=(0, 0, 0),
          swatch="skin_pale", parent=body).rotation_euler = (0, math.radians(-32), 0)
    # the collar: bronze, a rope band worn into it, the rope's frayed end hanging at the front
    collar = K.cylinder("Collar", radius=0.25, depth=0.14, verts=8, location=(0, -0.14, 0.50), pivot=(0, 0, 0),
                        swatch="bronze", parent=body)
    collar.rotation_euler = (math.radians(14), 0, math.radians(22.5))  # the neck juts forward: front lower
    K.cylinder("CollarRope", radius=0.262, depth=0.05, verts=8, location=(0, 0, -0.03), pivot=(0, 0, 0),
               swatch="rope", parent=collar)
    rope = K.box("CollarRope_End", size=(0.045, 0.045, 0.22), location=(0.13, -0.355, 0.46), pivot=(0, 0, 1),
                 swatch="rope", parent=body)  # the frayed end of the rope he was tied with
    rope.rotation_euler = (math.radians(-10), math.radians(-14), 0)
    # tail: a bull's tail, a dark tuft
    tail = K.box("Tail", size=(0.05, 0.05, 0.34), location=(0, 0.21, 0.14), pivot=(0, 0, 1), swatch="skin_tan",
                 parent=body)
    tail.rotation_euler = (math.radians(28), 0, 0)
    K.box("TailTuft", size=(0.10, 0.10, 0.14), location=(0, 0, -0.32), pivot=(0, 0, 1), taper=(1.4, 1.4),
          swatch="mud_dark", parent=tail)
    # the "that's me" mark for the camera behind him: a cyan square labyrinth on his upper back
    for k, (x, z, sx, sz) in enumerate(((-0.13, 0.34, 0.045, 0.30), (0.0, 0.47, 0.30, 0.045),
                                        (0.13, 0.34, 0.045, 0.30), (0.05, 0.21, 0.20, 0.045),
                                        (-0.04, 0.29, 0.045, 0.18))):
        K.box("Labyrinth%d" % k, size=(sx, 0.04, sz), location=(x, 0.226 + 0.05 * z, z), pivot=(0, 0, 0),
              swatch="player_cyan", parent=body)

    # --- head: the bull head, low and forward of the hump: a broad poll, a long pale snout
    head = K.box("Head", size=(0.56, 0.46, 0.46), location=(0, -0.14, NECK_Z), pivot=(0, 0, -1), taper=(1.08, 0.94),
                 bevel=0.04, swatch="skin_dark", parent=body)
    head.data.transform(Matrix.Translation((0, -0.10, 0.02)))  # the skull sits forward of the neck joint
    K.box("Snout", size=(0.42, 0.30, 0.25), location=(0, -0.40, 0.0), pivot=(0, 0, -1), taper=(0.94, 0.86),
          swatch="sand", parent=head)
    for side, x in (("L", 0.09), ("R", -0.09)):
        K.box("Nostril" + side, size=(0.075, 0.02, 0.05), location=(x, -0.553, 0.15), pivot=(0, 0, 0),
              swatch="ink", parent=head)
    # the nose ring: a U of bronze hanging under the snout
    K.box("NoseRing", size=(0.15, 0.035, 0.035), location=(0, -0.56, -0.03), pivot=(0, 0, 0), swatch="bronze_l",
          parent=head)
    for side, x in (("L", 0.0575), ("R", -0.0575)):
        K.box("NoseRing" + side, size=(0.035, 0.035, 0.10), location=(x, -0.56, 0.02), pivot=(0, 0, 0),
              swatch="bronze_l", parent=head)
    K.box("Brow", size=(0.58, 0.10, 0.09), location=(0, -0.33, 0.38), pivot=(0, 0, 0), swatch="mud_dark",
          parent=head)
    for side, x, droop in (("L", 0.15, 14), ("R", -0.15, -14)):
        eye = K.box("Eye" + side, size=(0.11, 0.03, 0.065), location=(x, -0.335, 0.30), pivot=(0, 0, 0),
                    swatch="glow_cyan", parent=head)
        eye.rotation_euler = (0, math.radians(droop), 0)  # the outer corners droop: sad, not angry
    K.box("ScarBrow", size=(0.03, 0.02, 0.20), location=(0.17, -0.345, 0.31), pivot=(0, 0, 0), swatch="skin_pale",
          parent=head).rotation_euler = (0, math.radians(12), 0)
    for side, s in (("L", 1), ("R", -1)):  # ears out sideways beside the eyes, under the horns, drooping
        ear = K.box("Ear" + side, size=(0.20, 0.06, 0.12), location=(s * 0.27, -0.17, 0.25), pivot=(-s, 0, 0),
                    swatch="skin_dark", parent=head)  # dark: they sit against his lighter torso from the front
        ear.rotation_euler = (0, math.radians(s * 12), 0)
    # horns: out sideways from the poll, then forward and up. His RIGHT horn (-X) is snapped off: a flat stump.
    for side, s in (("L", 1), ("R", -1)):
        horn = K.cylinder("Horn" + side, radius=0.10, depth=0.22, verts=6, location=(s * 0.25, -0.12, 0.40),
                          pivot=(0, 0, -1), taper=0.8, axis="X", swatch="ash", parent=head)
        if s < 0:
            for v in horn.data.vertices:
                v.co.x = -v.co.x
            horn.data.flip_normals()
        broken = s < 0
        tip = K.cylinder("HornTip" + side, radius=0.085, depth=0.15 if broken else 0.44, verts=6,
                         location=(s * 0.21, 0, 0), pivot=(0, 0, -1), taper=0.65 if broken else 0.0001,
                         swatch="bone", parent=horn)
        d = Vector((s * 0.48, -0.30, 0.86)).normalized()  # forward and up: a point above the head at 64 px
        tip.rotation_euler = Vector((0, 0, 1)).rotation_difference(d).to_euler()

    # --- arms: massive, shaggy fur pads on the shoulders, iron cuffs at the wrists
    arms = {}
    for side, s in (("L", 1), ("R", -1)):
        arm = K.box("Arm" + side, size=(0.25, 0.27, 0.30), location=(s * 0.52, 0.0, SHOULDER_Z), pivot=(0, 0, 1),
                    taper=(1.1, 1.1), swatch="skin_tan", parent=body)
        K.box("ShoulderFur" + side, size=(0.36, 0.36, 0.17), location=(s * 0.01, 0.01, 0.04), pivot=(0, 0, 0),
              taper=(0.8, 0.85), swatch="skin_dark", parent=arm)
        K.box("Forearm" + side, size=(0.30, 0.30, 0.22), location=(0, 0, -0.27), pivot=(0, 0, 1), taper=(0.95, 0.95),
              swatch="skin_tan", parent=arm)
        K.cylinder("Cuff" + side, radius=0.175, depth=0.11, verts=6, location=(0, 0, -0.47), pivot=(0, 0, 0),
                   swatch="iron", parent=arm)
        K.box("Fist" + side, size=(0.25, 0.25, 0.18), location=(0, -0.01, -0.50), pivot=(0, 0, 1), swatch="skin_tan",
              parent=arm)
        arms[side] = arm
    K.box("ScarArm", size=(0.02, 0.03, 0.18), location=(0.127, 0.0, -0.14), pivot=(0, 0, 0), swatch="skin_pale",
          parent=arms["L"]).rotation_euler = (math.radians(25), 0, 0)
    # the broken shackle (left): a snapped link hanging off the cuff, half a link below it
    K.box("BrokenLink", size=(0.04, 0.11, 0.13), location=(0, 0.06, -0.52), pivot=(0, 0, 1), swatch="iron",
          parent=arms["L"]).rotation_euler = (math.radians(10), 0, math.radians(15))
    K.box("BrokenHalf", size=(0.11, 0.04, 0.07), location=(0.01, 0.07, -0.62), pivot=(0, 0, 1), swatch="iron_d",
          parent=arms["L"]).rotation_euler = (0, math.radians(30), 0)
    # the chain wound twice round the right forearm (he holds it, it's his weapon)
    for k, z in enumerate((-0.31, -0.39)):
        K.box("ChainWrap%d" % k, size=(0.33, 0.33, 0.05), location=(0, 0, z), pivot=(0, 0, 0), swatch="steel",
              parent=arms["R"]).rotation_euler = (math.radians(8 if k else -8), 0, 0)

    # --- the chain: from the cuff (behind the fist) down to the ground, then trailing behind him
    chain = K.empty("Chain", location=(0, 0.12, -0.52), parent=arms["R"])
    bpy_update()
    drop = chain.matrix_world.translation.z - (W.LINK[1] / 2 - 0.01)  # the upright links touch the ground
    a0 = math.radians(14)
    a1 = math.acos(max(-1.0, min(1.0, drop / SEG_LEN - math.cos(a0))))
    bends = [a0, a1 - a0, math.radians(90) - a1]  # cumulative 14 deg, a1, then flat on the ground
    yaws = [0.0, 0.0, 0.0, math.radians(5), math.radians(-6), math.radians(4), math.radians(-5)]
    parent = chain
    for k in range(2 + TRAIL_SEGS):
        bend = bends[k] if k < len(bends) else 0.0
        loc = (0, 0, 0) if k == 0 else (0, 0, -SEG_LEN)
        parent = chain_segment("ChainSeg%d" % k, loc, (bend, yaws[k] if k >= 3 else 0.0, 0), parent)
    end = W.masonry("ChainEnd")
    end.parent = parent
    end.location = (0, 0, -SEG_LEN)

    # --- legs: short and thick, dark fur shins, split hooves
    for side, s in (("L", 1), ("R", -1)):
        leg = K.box("Leg" + side, size=(0.25, 0.27, 0.28), location=(s * 0.17, 0, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.08, 1.08), swatch="skin_tan", parent=root)
        K.box("Shin" + side, size=(0.25, 0.27, 0.16), location=(0, 0, -0.27), pivot=(0, 0, 1), taper=(1.05, 1.05),
              swatch="skin_dark", parent=leg)
        K.box("Hoof" + side, size=(0.24, 0.30, 0.10), location=(0, -0.03, -0.50), pivot=(0, 0, -1), taper=(0.92, 0.9),
              swatch="charcoal", parent=leg)
        K.box("HoofSplit" + side, size=(0.03, 0.10, 0.09), location=(0, -0.155, -0.495), pivot=(0, 0, -1),
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
