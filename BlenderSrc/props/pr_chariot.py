"""PR_Chariot — the Imperial Arena's quadriga (docs/ARENAS.md §5, the chariot races). Budget ~700 tris.

A racing chariot that tramples everything in the track lane: four dark horses abreast with orange-red plumes (it hurts:
CLAUDE.md §5) and crimson blankets, a gold-and-crimson car with a high curved front, gold-spoked wheels and a charioteer
in purple with a gold helm, reins in one hand and a whip in the other. About 4.5 m nose to tail and ~2.3 m wide, so it
fits the 4 m track lane.

Origin at ground level, at the centre of the footprint (half-way between the horses' noses and the car's back).
Front = -Y (Unity +Z). Parts (children of the root, so code can drive each):
  Car            the car, pole, yoke and axle; pivot on the ground under the axle. The Driver rides on it.
  Driver         the charioteer (reins and whip included); pivot at his feet on the car floor.
  WheelL/WheelR  pivot at the axle centre; spin about local X.
  Horse0..3      one horse each (barrel, neck, head, mane, tail, plume, blanket); pivot at the barrel's centre.
                 Horse0 is the far left (+X here, Unity -X), Horse3 the far right.
  Horse<i>LegsF / Horse<i>LegsB   front / hind pair, children of the horse, pivot at the shoulder / hip (swing about
                 local X for a gallop; optional).
"""
import importlib
import math

import bmesh

import gladiator_kit as K

importlib.reload(K)

# iteration 2: the axle further back and the wheels smaller, so the car's high front shows from the side (v1's wheel
# hid the whole car); a bigger driver
AXLE_Y, WHEEL_R = 1.68, 0.54        # the axle: 1.68 m behind the origin, 0.54 m up
CAR_FRONT = 0.48                    # the car's front edge (y)
WHEEL_X = 0.8
HORSE_X = (0.92, 0.3, -0.3, -0.92)  # Horse0 (left, +X) .. Horse3 (right)
HORSE_Y, HORSE_Z = -0.72, 1.15      # the barrels' centre


def leg_pair(name, y, parent, swatch):
    """Two legs (open-ended square prisms: the tops hide in the barrel, the bottoms stand on the ground), one object
    pivoted at the barrel's underside (shoulder or hip)."""
    bm = bmesh.new()
    top, bot = 0.0, -(HORSE_Z - 0.3)
    for x in (-0.14, 0.14):
        hw, hd = 0.065, 0.075
        ring_t = [bm.verts.new((x + sx * hw, sy * hd, top)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        ring_b = [bm.verts.new((x + sx * hw * 0.85, sy * hd * 0.9, bot)) for sx, sy in ((-1, -1), (1, -1), (1, 1),
                                                                                        (-1, 1))]
        for k in range(4):
            j = (k + 1) % 4
            bm.faces.new([ring_b[k], ring_b[j], ring_t[j], ring_t[k]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K.object_from_bmesh(name, bm, (0, y, -0.3), parent, swatch)


def horse(i, x, root):
    h = K.box("Horse%d" % i, size=(0.5, 1.5, 0.6), location=(x, HORSE_Y, HORSE_Z), pivot=(0, 0, 0), swatch="charcoal",
              parent=root)
    K.set_swatch_where(h, "crimson", lambda c, n: n.z > 0.5)  # the blanket on his back
    pieces = [h]
    neck = K.box("Neck", size=(0.3, 0.4, 0.78), location=(0, -0.6, 0.1), pivot=(0, 0, -1), taper=(0.85, 0.8),
                 swatch="charcoal", parent=h)
    neck.rotation_euler = (0.45, 0, 0)  # leaning forward
    pieces.append(neck)
    head = K.box("HorseHead", size=(0.28, 0.7, 0.32), location=(0, -1.12, 0.82), pivot=(0, 0, 0), taper=(0.85, 1.0),
                 swatch="charcoal", parent=h)
    head.rotation_euler = (0.35, 0, 0)  # the muzzle dips
    pieces.append(head)
    mane = K.box("Mane", size=(0.1, 0.16, 0.72), location=(0, -0.48, 0.2), pivot=(0, 0, -1), swatch="ink", parent=h)
    mane.rotation_euler = (0.45, 0, 0)
    pieces.append(mane)
    plume = K.box("Plume", size=(0.12, 0.14, 0.42), location=(0, -0.92, 0.95), pivot=(0, 0, -1), taper=(1.6, 1.8),
                  swatch="hazard_red", parent=h)  # orange-red: it hurts
    pieces.append(plume)
    tail = K.box("Tail", size=(0.12, 0.14, 0.62), location=(0, 0.76, 0.18), pivot=(0, 0, 1), taper=(1.4, 1.2),
                 swatch="ink", parent=h)
    tail.rotation_euler = (0.5, 0, 0)  # streaming back
    pieces.append(tail)
    K.join(pieces, "Horse%d" % i)
    leg_pair("Horse%dLegsF" % i, -0.52, h, "charcoal")
    leg_pair("Horse%dLegsB" % i, 0.52, h, "charcoal")
    return h


def wheel(name, x, root):
    w = K.cylinder(name, radius=WHEEL_R, depth=0.12, verts=8, location=(x, AXLE_Y, WHEEL_R), pivot=(0, 0, 0),
                   swatch="crimson_d", parent=root, axis="X")
    K.set_swatch_where(w, "gold_d", lambda c, n: abs(n.x) < 0.5)  # the tyre
    spokes = [w]
    for k, size in enumerate(((0.16, 2 * WHEEL_R * 0.95, 0.09), (0.16, 0.09, 2 * WHEEL_R * 0.95))):  # a gold cross: shows the wheel turning
        spokes.append(K.box("Spoke%d" % k, size=size, pivot=(0, 0, 0), swatch="gold", parent=w))
    return K.join(spokes, name)


def driver(car):
    d = K.box("Driver", size=(0.42, 0.3, 0.52), location=(0, 1.42 - AXLE_Y, 0.86), pivot=(0, 0, -1), taper=(1.15, 1.0),
              swatch="purple", parent=car)
    d.rotation_euler = (0.22, 0, 0)  # leaning into the race
    pieces = [d]
    pieces.append(K.box("DHead", size=(0.42, 0.4, 0.4), location=(0, -0.02, 0.52), pivot=(0, 0, -1),
                        swatch="skin_mid", parent=d))
    pieces.append(K.box("DHelm", size=(0.46, 0.44, 0.18), location=(0, 0.0, 0.86), pivot=(0, 0, -1), taper=(0.75, 0.75),
                        swatch="gold", parent=d))
    pieces.append(K.box("DArms", size=(0.54, 0.4, 0.12), location=(0, -0.25, 0.3), pivot=(0, 0, 0), swatch="skin_mid",
                        parent=d))
    # the reins: from his left fist to the horses' heads, rising forward
    reins = K.box("Reins", size=(0.05, 3.0, 0.04), location=(0.18, -0.42, 0.3), pivot=(0, 1, 0), swatch="leather_d",
                  parent=d)
    reins.rotation_euler = (-0.49, 0, 0)  # with his lean: rising ~15 deg to the heads
    pieces.append(reins)
    whip = K.box("Whip", size=(0.04, 0.04, 0.95), location=(-0.25, -0.3, 0.3), pivot=(0, 0, -1), taper=(0.5, 0.5),
                 swatch="leather", parent=d)
    whip.rotation_euler = (0.3, -0.55, 0)  # raised over his right shoulder
    pieces.append(whip)
    return K.join(pieces, "Driver")


def build():
    K.clear_scene()
    root = K.empty("PR_Chariot")
    for i, x in enumerate(HORSE_X):
        horse(i, x, root)

    # the car: a side profile extruded across its width, high curved front, low open back
    car = K.box("Car", size=(1.4, 1.5, 0.06), location=(0, AXLE_Y, 0.0), pivot=(0, 0, -1), swatch="gold_d",
                parent=root)
    shift = (CAR_FRONT + 0.75) - AXLE_Y
    car.data.vertices.foreach_set("co", [c for v in car.data.vertices for c in
                                         (v.co.x, v.co.y + shift, v.co.z + 0.46)])  # the floor plank, at z 0.46
    profile = [(0.0, 0.0), (1.48, 0.0), (1.48, 0.3), (0.78, 0.44), (0.3, 0.9), (0.06, 1.06), (-0.06, 0.96),
               (0.04, 0.25)]  # (y from the car's front, z from its floor)
    shell = K.extrude_poly("Shell", profile, thickness=1.4, plane="YZ", location=(0, CAR_FRONT - AXLE_Y, 0.46),
                           swatch="crimson", parent=car)
    K.set_swatch_where(shell, "gold", lambda c, n: n.z > 0.3 and c.z > 0.2)  # the rails along the top
    pieces = [car, shell]
    for side in (1, -1):
        pieces.append(K.box("Medallion", size=(0.06, 0.34, 0.34), location=(side * 0.71, CAR_FRONT + 0.28 - AXLE_Y, 0.92),
                            pivot=(0, 0, 0), swatch="gold_l", parent=car))
    pieces.append(K.box("Axle", size=(1.5, 0.1, 0.1), location=(0, 0, WHEEL_R), pivot=(0, 0, 0), swatch="iron_d",
                        parent=car))
    pieces.append(K.box("Pole", size=(0.09, 1.86, 0.09), location=(0, CAR_FRONT + 0.1 - AXLE_Y, 1.36), pivot=(0, 1, 0),
                        swatch="wood_dark", parent=car))
    pieces.append(K.box("Yoke", size=(1.1, 0.1, 0.1), location=(0, HORSE_Y - 0.5 - AXLE_Y, 1.46), pivot=(0, 0, 0),
                        swatch="gold_d", parent=car))
    K.join(pieces, "Car")
    driver(car)

    wheel("WheelL", WHEEL_X, root)
    wheel("WheelR", -WHEEL_X, root)
    return root


root = build()
K.world_bounds([root])  # refresh the world matrices
PARTS = {o.name: [round(v, 3) for v in o.matrix_world.translation] for o in K.descendants(root)}
info = K.review_render(root, "pr_chariot", size=480)
info["fbx"] = K.export_fbx(root, "Props/PR_Chariot")
info["tris"] = K.tri_count(K.descendants(root))
info["parts"] = PARTS
info["blend"] = K.save_blend("pr_chariot")
result = info
