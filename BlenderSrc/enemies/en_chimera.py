"""EN_Chimera — the Chimera, the Colosseum's mythical beast that breathes a cone of fire (docs/ARENAS.md §6).
Budget <= 800 tris.

A big dark lion (~1.6 m at the shoulder, ~2.6 m long): dark fur, a heavy charcoal mane, its jaws open on a fire-orange
throat (it breathes fire), yellow eyes; from the middle of its back a goat's neck rises with a pale goat head and
curling horns (offset to its left); its tail is a slate snake arching up over its rump with its own head (offset to its
right), so the three heads show from the front, the side and 3/4. A magenta harness strap round the barrel (the enemy
accent). Rigid parts like the War Boar so EnemyManager drives it: front legs = ArmL/ArmR (children of Body), hind legs
= LegL/LegR (children of the root), Head = the lion's head (child of Body, pivot at the neck), plus
  GoatHead   child of Body, pivot at the base of the goat's neck on the back.
  Tail       child of Body, the snake, pivot at its base on the rump.
  TailHead   child of Tail, pivot at the snake's neck.
Every named part has an identity bind rotation at its joint. Origin at the ground under the barrel's centre;
front = -Y (Unity +Z).
"""
import importlib
import math

import bpy
from mathutils import Euler, Matrix, Vector

import gladiator_kit as K

importlib.reload(K)

BODY_Z = 0.68   # belly height
LEG = 0.68      # belly to ground (the leg joints sit a little inside the barrel)


def place(obj, loc, rot=None):
    """Set obj's WORLD transform (its parent may be rotated). rot: Euler tuple or Quaternion."""
    bpy.context.view_layer.update()
    m3 = Matrix.Identity(3) if rot is None else (rot.to_matrix() if hasattr(rot, "to_matrix") and
                                                 not isinstance(rot, tuple) else Euler(rot).to_matrix())
    obj.matrix_world = Matrix.Translation(Vector(loc)) @ m3.to_4x4()
    bpy.context.view_layer.update()
    return obj


def wbox(name, size, loc, swatch, parent, pivot=(0, 0, 0), taper=(1.0, 1.0), rot=None):
    """K.box placed at a WORLD location."""
    return place(K.box(name, size=size, pivot=pivot, taper=taper, swatch=swatch, parent=parent), loc, rot)


def seg(name, p0, p1, w, swatch, parent, taper=1.0, verts=0, d=None):
    """A box (or a `verts`-sided prism) from world p0 to p1, origin at p0, local +Z along it."""
    p0, p1 = Vector(p0), Vector(p1)
    ln = (p1 - p0).length
    if verts:
        obj = K.cylinder(name, radius=w / 2, depth=ln, verts=verts, pivot=(0, 0, -1), taper=taper, swatch=swatch,
                         parent=parent)
    else:
        obj = K.box(name, size=(w, d or w, ln), pivot=(0, 0, -1), taper=(taper, taper), swatch=swatch, parent=parent)
    return place(obj, p0, (p1 - p0).normalized().to_track_quat("Z", "Y"))


def toward(d):
    """Rotation whose local -Y points along d (heads and snouts face their local front)."""
    return Vector(d).normalized().to_track_quat("-Y", "Z")


def build():
    K.clear_scene()
    root = K.empty("EN_Chimera")

    # ---- the lion's barrel: deep chest, lower haunch, the mane's hump over the withers, the magenta harness
    body = wbox("Body", (0.72, 1.46, 0.68), (0, 0.02, BODY_Z), "mud_dark", root, pivot=(0, 0, -1), taper=(0.92, 0.95))
    wbox("Chest", (0.82, 0.74, 0.86), (0, -0.4, BODY_Z + 0.02), "leather_d", body, pivot=(0, 0, -1),
         taper=(0.86, 0.8))
    wbox("Haunch", (0.72, 0.7, 0.7), (0, 0.42, BODY_Z + 0.02), "leather_d", body, pivot=(0, 0, -1),
         taper=(0.85, 0.8))
    wbox("Withers", (0.86, 0.56, 0.3), (0, -0.52, BODY_Z + 0.7), "charcoal", body, pivot=(0, 0, -1),
         taper=(0.7, 0.75))
    wbox("Harness", (0.88, 0.14, 0.8), (0, -0.02, BODY_Z - 0.03), "accent_magenta", body, pivot=(0, 0, -1),
         taper=(0.92, 1.0))

    # ---- the lion's head: a mane ruff with spikes, jaws open on a fire-orange throat
    neck = Vector((0, -0.8, 1.32))
    head = wbox("Head", (0.5, 0.48, 0.46), (0, -1.02, 1.32), "mud_dark", body, taper=(0.9, 0.9))
    head.data.transform(Matrix.Translation(Vector((0, -1.02 - neck.y, 0))))  # origin -> the neck joint
    head.location = (0, 0, 0)
    place(head, neck)
    # the ruff: a deep collar of mane round the face, narrowing toward the snout, spikes all round but under the jaw
    ruff = wbox("Ruff", (0.9, 0.56, 0.86), (0, -0.78, 1.36), "charcoal", head, rot=(-math.pi / 2, 0, 0),
                pivot=(0, 0, 0), taper=(0.62, 0.62))  # its tapered end toward the snout
    for k in range(10):
        a = math.radians(-130 + k * (260 / 9) + 0.001)
        dirv = Vector((math.sin(a) * 0.9, 0.35, math.cos(a)))
        s = K.box("Mane%d" % k, size=(0.2, 0.2, 0.3), pivot=(0, 0, -1), taper=(0.2, 0.3), swatch="charcoal",
                  parent=ruff)
        place(s, Vector((0, -0.7, 1.38)) + Vector((math.sin(a) * 0.36, 0, math.cos(a) * 0.36)),
              dirv.normalized().to_track_quat("Z", "Y"))
    wbox("Snout", (0.32, 0.32, 0.17), (0, -1.33, 1.37), "leather_d", head, taper=(0.9, 1.0))
    wbox("Nose", (0.13, 0.05, 0.08), (0, -1.5, 1.43), "ink", head)
    jaw = wbox("Jaw", (0.28, 0.32, 0.08), (0, -1.17, 1.2), "leather_d", head, pivot=(0, 1, 0))
    jaw.rotation_euler = (0.42, 0, 0)  # hanging open (its back edge is the hinge)
    wbox("Throat", (0.26, 0.24, 0.14), (0, -1.3, 1.22), "fire_mid", head, rot=(0.2, 0, 0))
    for side, x in (("L", 0.09), ("R", -0.09)):
        fang = K.cone("Fang" + side, radius=0.035, depth=0.11, verts=4, pivot=(0, 0, -1), swatch="bone", parent=head,
                      cap=False)
        place(fang, (x, -1.46, 1.3), (math.pi, 0, 0))
        wbox("Eye" + side, (0.08, 0.04, 0.05), (x * 1.35, -1.26, 1.47), "hazard_yellow", head)
        wbox("Ear" + side, (0.1, 0.06, 0.13), (x * 2.4, -1.02, 1.56), "mud_dark", head, pivot=(0, 0, -1),
             taper=(0.3, 0.5))

    # ---- the goat: a neck rising from the back (to its left), a pale head, a beard, horns curling back
    gbase = Vector((0.16, -0.14, 1.34))
    goat = wbox("GoatHead", (0.2, 0.2, 0.12), gbase, "mud_dark", body)  # the neck's root, in the fur
    gtop = Vector((0.27, -0.4, 2.0))
    seg("GoatNeck", gbase, gtop, 0.17, "ash", goat, taper=0.85, d=0.2)
    ghead = wbox("GoatSkull", (0.2, 0.36, 0.22), gtop + Vector((0.0, -0.12, 0.04)), "bone", goat,
                 rot=toward((0.12, -1.0, -0.45)))
    wbox("Muzzle", (0.15, 0.16, 0.14), gtop + Vector((0.03, -0.3, -0.06)), "ash", ghead,
         rot=toward((0.12, -1.0, -0.6)))
    wbox("Beard", (0.08, 0.06, 0.16), gtop + Vector((0.03, -0.3, -0.13)), "ash", ghead, pivot=(0, 0, 1),
         taper=(1.6, 1.0))
    for side, x in (("L", 0.09), ("R", -0.09)):
        wbox("GoatEye" + side, (0.04, 0.05, 0.04), gtop + Vector((x * 1.1 + 0.01, -0.18, 0.1)), "hazard_yellow",
             ghead)
        # a horn in three bone segments: up and back from the brow, then back and down, then down and out
        h0 = gtop + Vector((x * 0.7, -0.08, 0.15))
        h1 = h0 + Vector((x * 0.5, 0.12, 0.22))
        h2 = h1 + Vector((x * 0.6, 0.22, -0.02))
        h3 = h2 + Vector((x * 0.5, 0.06, -0.18))
        seg("Horn%s0" % side, h0, h1, 0.08, "bone", ghead, taper=0.85)
        seg("Horn%s1" % side, h1, h2, 0.068, "ash", ghead, taper=0.8)
        seg("Horn%s2" % side, h2, h3, 0.054, "bone", ghead, taper=0.5)

    # ---- the snake tail: from the rump, back and up over it (to its right), its head facing forward
    pts = [Vector(p) for p in ((-0.04, 0.74, 1.12), (-0.08, 1.1, 1.3), (-0.15, 1.3, 1.62), (-0.21, 1.26, 1.95),
                               (-0.25, 1.06, 2.12))]
    tail = seg("Tail", pts[0], pts[1], 0.2, "slate", body, taper=0.9, verts=6)
    # keep the Tail part's bind rotation at identity: bake its rotation into the mesh, keep the origin at the base
    bpy.context.view_layer.update()
    mw = tail.matrix_world.copy()
    tail.data.transform(mw.to_3x3().to_4x4())
    place(tail, pts[0])
    widths = (0.18, 0.16, 0.13)
    for k in range(3):
        seg("Coil%d" % k, pts[k + 1], pts[k + 2], widths[k], "iron" if k % 2 == 0 else "slate", tail,
            taper=0.88, verts=6)
    thead = wbox("TailHead", (0.12, 0.12, 0.1), pts[4], "slate", tail)
    sk = wbox("SnakeSkull", (0.17, 0.26, 0.11), pts[4] + Vector((0, -0.14, 0.02)), "slate", thead, taper=(0.8, 0.9),
              rot=toward((0, -1.0, -0.25)))
    wbox("SnakeJaw", (0.13, 0.2, 0.05), pts[4] + Vector((0, -0.12, -0.07)), "iron", sk, rot=toward((0, -1.0, 0.35)))
    wbox("SnakeFang", (0.03, 0.03, 0.08), pts[4] + Vector((0, -0.25, -0.04)), "bone", sk, pivot=(0, 0, 1))
    for side, x in (("L", 0.065), ("R", -0.065)):
        wbox("SnakeEye" + side, (0.04, 0.05, 0.04), pts[4] + Vector((x * 1.15, -0.16, 0.07)), "hazard_yellow", sk)

    # ---- legs: heavy lion legs, dark paws (front = ArmL/ArmR under Body, hind = LegL/LegR under the root)
    for side, x in (("L", 0.25), ("R", -0.25)):
        front = wbox("Arm" + side, (0.24, 0.28, LEG + 0.14), (x, -0.56, LEG + 0.14), "leather_d", body,
                     pivot=(0, 0, 1), taper=(1.3, 1.3))
        wbox("Paw" + side, (0.28, 0.36, 0.11), (x, -0.63, 0.0), "charcoal", front, pivot=(0, 0, -1))
        hind = wbox("Leg" + side, (0.24, 0.32, LEG + 0.14), (x * 0.94, 0.52, LEG + 0.14), "leather_d", root,
                    pivot=(0, 0, 1), taper=(1.4, 1.6))
        wbox("Foot" + side, (0.27, 0.36, 0.11), (x * 0.94, 0.46, 0.0), "charcoal", hind, pivot=(0, 0, -1))
    return root


root = build()
bpy.context.view_layer.update()
NAMED = ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "GoatHead", "Tail", "TailHead")
PARTS = {o.name: [round(-o.matrix_world.translation.x, 3), round(o.matrix_world.translation.z, 3),
                  round(-o.matrix_world.translation.y, 3)] for o in K.descendants(root) if o.name in NAMED}
ROTS = {o.name: [round(math.degrees(a), 1) for a in o.matrix_world.to_euler()] for o in K.descendants(root)
        if o.name in NAMED}
info = K.review_render(root, "en_chimera", size=480)
if ARGS.get("export", "1") == "1":
    info["fbx"] = K.export_fbx(root, "Enemies/EN_Chimera")
    info["blend"] = K.save_blend("en_chimera")
info["tris"] = K.tri_count(K.descendants(root))
info["parts_unity"] = PARTS
info["part_rot_deg"] = ROTS
result = info
