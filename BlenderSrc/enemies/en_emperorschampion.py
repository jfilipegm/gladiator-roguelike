"""EN_EmperorsChampion — the Emperor's Champion, the Imperial Arena's boss (docs/BOSSES.md). Budget <= 2,500 tris.

The Emperor's own champion: a huge, chibi-heavy figure (~3.4 m to the helm, ~4 m to the eagle's wingtips, the spear
~4.5 m): a gold muscle cuirass, gold layered pauldrons and greaves, a fringe of crimson and gold pteruges, a long purple
cloak to the ground, a stern face under a gold helm with cheek guards, and on the helm a gold eagle with its wings
spread in a V: nothing else in the game has wings over its head. Right hand (-X): a tall spear held upright, leaf blade
and crimson tassel. Left arm (+X): a curved purple scutum with a gold rim, boss and wings, held at his side (the
Provincial Champion holds his crimson one square in front). On his back, over the cloak: a crimson quiver of javelins
fanning up past his left shoulder (he throws them in fans). The magenta belt is the enemy accent.

Rigid parts on the shared names (Body/Head/ArmL/ArmR/LegL/LegR), plus parts code can drive on their own:
  Spear      child of ArmR, pivot at the grip (in the fist); upright at bind; the blade's tip on its local +Z.
  Shield     child of ArmL, pivot at the grip (in the fist).
  Javelins   child of Body, pivot at the quiver's centre (on his back).
  Cloak      child of Body, pivot at the shoulders (top centre).
  Eagle      child of Head, pivot at its feet on the helm; WingL / WingR its children, pivot at the wing roots.
Origin at the feet; front = -Y (Unity +Z).
"""
import importlib
import math

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 1.15


def place(obj, loc, rot=(0.0, 0.0, 0.0)):
    """Set obj's WORLD transform (its parent may be rotated)."""
    bpy.context.view_layer.update()
    obj.matrix_world = Matrix.Translation(Vector(loc)) @ Euler(rot).to_matrix().to_4x4()
    bpy.context.view_layer.update()


def curved_shield(name, width, height, depth, radius, segs, rim, location, parent, swatch, rim_swatch, back_swatch):
    """A scutum bent round a vertical axis, convex to the front (-Y), rimmed top, bottom and sides (same as
    en_praetorian.py). Origin at its centre. Returns (obj, front_y at the centre line)."""
    bm = bmesh.new()
    half = width / 2
    ang = 2 * math.asin(min(0.99, half / radius))
    cols = []
    for k in range(segs + 1):
        a = -ang / 2 + ang * k / segs
        cols.append((radius * math.sin(a), -(radius * math.cos(a) - radius * math.cos(ang / 2))))
    off = sum(y for _, y in cols) / len(cols)
    cols = [(x, y - off) for x, y in cols]
    hz = height / 2
    fz = (-hz, -hz + rim, hz - rim, hz)
    front = [[bm.verts.new((x, y - depth / 2, z)) for z in fz] for x, y in cols]
    back = [[bm.verts.new((x, y + depth / 2, z)) for z in (-hz, hz)] for x, y in cols]
    for k in range(segs):
        for r in range(3):
            bm.faces.new([front[k][r], front[k + 1][r], front[k + 1][r + 1], front[k][r + 1]])
        bm.faces.new([back[k + 1][0], back[k][0], back[k][1], back[k + 1][1]])
        bm.faces.new([front[k][3], front[k + 1][3], back[k + 1][1], back[k][1]])
        bm.faces.new([front[k + 1][0], front[k][0], back[k][0], back[k + 1][0]])
    for col, b in ((0, back[0]), (segs, back[segs])):
        f = front[col]
        bm.faces.new([f[0], f[1], f[2], f[3], b[1], b[0]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = K.object_from_bmesh(name, bm, location, parent, swatch)
    ex = cols[-1][0] - 1e-4
    K.set_swatch_where(obj, rim_swatch, lambda c, n: abs(c.z) > hz - rim - 1e-4 or abs(c.x) >= ex)
    K.set_swatch_where(obj, back_swatch, lambda c, n: n.y > 0.3 and abs(c.z) < hz - rim and abs(c.x) < ex)
    mid = cols[segs // 2][1] if segs % 2 == 0 else (cols[segs // 2][1] + cols[segs // 2 + 1][1]) / 2
    return obj, mid - depth / 2


# a spread eagle's wing seen from the front, root at the origin, reaching +X; three primaries at the tip
WING = [(0.0, -0.06), (0.26, -0.1), (0.42, -0.03), (0.56, -0.13), (0.68, 0.0), (0.8, -0.07), (0.9, 0.16),
        (1.0, 0.52), (0.84, 0.44), (0.6, 0.34), (0.3, 0.24), (0.0, 0.15)]


def build():
    K.clear_scene()
    root = K.empty("EN_EmperorsChampion")

    # ---- legs: stocky, gold greaves and knee guards, sandal boots
    for side, x in (("L", 0.36), ("R", -0.36)):
        leg = K.box("Leg" + side, size=(0.5, 0.52, 1.15), location=(x, 0.02, HIP_Z), pivot=(0, 0, 1),
                    taper=(1.15, 1.1), swatch="skin_mid", parent=root)
        K.box("Knee" + side, size=(0.46, 0.14, 0.24), location=(0, -0.27, -0.56), pivot=(0, 0, 0), swatch="gold_l",
              parent=leg)
        K.box("Greave" + side, size=(0.54, 0.56, 0.52), location=(0, -0.01, -1.0), pivot=(0, 0, -1),
              taper=(1.06, 1.06), swatch="gold", parent=leg)
        K.box("Boot" + side, size=(0.56, 0.78, 0.2), location=(0, -0.1, -1.15), pivot=(0, 0, -1),
              swatch="leather_d", parent=leg)

    # ---- body: the gold muscle cuirass, the magenta belt, the pteruges fringe
    body = K.box("Body", size=(1.3, 0.86, 1.05), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.22, 1.1),
                 bevel=0.05, swatch="gold", parent=root)
    for side, x in (("L", 0.26), ("R", -0.26)):
        K.box("Pec" + side, size=(0.5, 0.1, 0.34), location=(x, -0.47, 0.62), pivot=(0, 0, -1), taper=(0.92, 1.0),
              swatch="gold_l", parent=body)
    K.box("Abs", size=(0.56, 0.08, 0.4), location=(0, -0.44, 0.17), pivot=(0, 0, -1), taper=(1.1, 1.0),
          swatch="gold_l", parent=body)
    K.box("Belt", size=(1.36, 0.92, 0.16), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta",
          parent=body)
    K.box("SkirtCore", size=(1.16, 0.8, 0.42), location=(0, 0, 0.02), pivot=(0, 0, 1), swatch="crimson_d", parent=body)
    for k in range(10):  # the fringe of tabs round his hips, crimson and gold
        a = 2 * math.pi * (k + 0.5) / 10
        tab = K.box("Tab%d" % k, size=(0.3, 0.07, 0.48), location=(0.6 * math.sin(a), -0.42 * math.cos(a), 0.02),
                    pivot=(0, 0, 1), taper=(0.9, 1.0), swatch="crimson" if k % 2 else "gold_d", parent=body)
        tab.rotation_euler = (0, 0, -a)
    K.box("Neck", size=(0.4, 0.38, 0.14), location=(0, -0.04, 1.03), pivot=(0, 0, -1), swatch="skin_mid", parent=body)
    scab = K.box("Scabbard", size=(0.12, 0.14, 0.62), location=(-0.72, 0.05, 0.06), pivot=(0, 0, 1), swatch="gold_d",
                 parent=body)  # a gladius at his right hip
    scab.rotation_euler = (-0.35, 0, 0)
    K.box("Hilt", size=(0.1, 0.1, 0.22), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="crimson_d", parent=scab)

    # ---- the cloak: imperial purple from the shoulders to the ground, flaring back and out past his legs
    # iteration 2: wider and fuller (v1's hid behind him)
    cloak = K.box("Cloak", size=(2.1, 0.12, 2.16), location=(0, 0.47, 1.0), pivot=(0, 0, 1), taper=(0.66, 1.0),
                  swatch="purple", parent=body)
    K.set_swatch_where(cloak, "purple_d", lambda c, n: n.y < -0.5)  # the inside
    cloak.rotation_euler = (0.16, 0, 0)
    K.box("Hem", size=(2.12, 0.15, 0.1), location=(0, 0, -2.1), pivot=(0, 0, 0), swatch="gold", parent=cloak)

    # ---- the javelins: a crimson quiver on his back, five javelins fanning up past his left shoulder
    quiver = K.box("Javelins", size=(0.26, 0.22, 0.95), location=(0, 0.64, 0.5), pivot=(0, 0, 0), swatch="crimson",
                   parent=body)
    quiver.rotation_euler = (0, 0.5, 0)
    K.box("QuiverRim", size=(0.3, 0.26, 0.1), location=(0, 0, 0.44), pivot=(0, 0, 0), swatch="gold", parent=quiver)
    for k in range(5):
        fan = (k - 2) * 0.09
        jav = K.cylinder("Javelin%d" % k, radius=0.03, depth=2.25, verts=4, location=((k - 2) * 0.04, 0, -0.7),
                         pivot=(0, 0, -1), swatch="wood_light", parent=quiver)
        jav.rotation_euler = (0.04 * abs(k - 2), fan, 0)
        K.cone("JavTip%d" % k, radius=0.065, depth=0.24, verts=4, location=(0, 0, 2.25), pivot=(0, 0, -1),
               swatch="steel_l", parent=jav, cap=False)

    # ---- head: a stern face under a gold helm with cheek guards and a brow visor
    head = K.box("Head", size=(0.86, 0.8, 0.78), location=(0, -0.05, 1.08), pivot=(0, 0, -1), swatch="skin_mid",
                 parent=body)
    K.box("Chin", size=(0.5, 0.16, 0.2), location=(0, -0.42, 0.0), pivot=(0, 0, -1), swatch="skin_mid", parent=head)
    K.box("Eyes", size=(0.52, 0.03, 0.09), location=(0, -0.405, 0.44), pivot=(0, 0, -1), swatch="ink", parent=head)
    K.box("Brows", size=(0.58, 0.05, 0.07), location=(0, -0.41, 0.55), pivot=(0, 0, -1), taper=(1.0, 1.0),
          swatch="ink", parent=head)
    K.box("Mouth", size=(0.26, 0.03, 0.04), location=(0, -0.405, 0.19), pivot=(0, 0, -1), swatch="ink", parent=head)
    helm = K.box("Helm", size=(0.96, 0.9, 0.44), location=(0, 0.01, 0.62), pivot=(0, 0, -1), taper=(0.74, 0.74),
                 bevel=0.03, swatch="gold", parent=head)
    K.box("Visor", size=(0.98, 0.3, 0.08), location=(0, -0.33, 0.6), pivot=(0, 0, -1), swatch="gold_d", parent=head)
    K.box("Band", size=(0.97, 0.91, 0.06), location=(0, 0.01, 0.62), pivot=(0, 0, -1), swatch="crimson", parent=head)
    for side, x in (("L", 0.45), ("R", -0.45)):
        K.box("Cheek" + side, size=(0.1, 0.46, 0.56), location=(x, -0.12, 0.08), pivot=(0, 0, -1), taper=(1.0, 0.8),
              swatch="gold", parent=head)
    K.box("NeckGuard", size=(0.92, 0.12, 0.3), location=(0, 0.42, 0.34), pivot=(0, 0, -1), swatch="gold_d",
          parent=head)

    # ---- the eagle on the helm, wings spread in a V
    eagle = K.box("Eagle", size=(0.3, 0.5, 0.36), location=(0, 0.03, 0.44), pivot=(0, 0, -1), taper=(0.8, 0.7),
                  swatch="gold_l", parent=helm)
    K.box("EagleHead", size=(0.22, 0.26, 0.22), location=(0, -0.24, 0.32), pivot=(0, 0, -1), swatch="gold_l",
          parent=eagle)
    beak = K.box("Beak", size=(0.09, 0.18, 0.1), location=(0, -0.44, 0.36), pivot=(0, 0, -1), taper=(0.4, 0.3),
                 swatch="gold_d", parent=eagle)
    beak.rotation_euler = (0.5, 0, 0)
    K.box("EagleTail", size=(0.26, 0.32, 0.06), location=(0, 0.3, 0.06), pivot=(0, 0, -1), taper=(1.5, 1.0),
          swatch="gold_d", parent=eagle).rotation_euler = (-0.4, 0, 0)
    wing = K.extrude_poly("WingL", [(x * 1.2, z * 1.2) for x, z in WING], thickness=0.07, plane="XZ", location=(0.1, 0.0, 0.22), swatch="gold_l",
                          parent=eagle)
    K.set_swatch_where(wing, "gold_d", lambda c, n: abs(n.y) < 0.5)  # the feather edges
    wing.rotation_euler = (0, -0.5, 0)  # raised: the V (iteration 2: 1.2x bigger, steeper)
    K.mirror_x(wing, "WingR")

    # ---- arms: bare and huge, gold vambraces, layered gold pauldrons
    arms = {}
    for side, x, sgn in (("L", 0.8, 1), ("R", -0.8, -1)):
        arm = K.box("Arm" + side, size=(0.4, 0.42, 1.0), location=(x, 0, 0.95), pivot=(0, 0, 1), taper=(1.15, 1.1),
                    swatch="skin_mid", parent=body)
        K.box("Pauldron" + side, size=(0.66, 0.76, 0.3), location=(sgn * 0.04, 0, -0.04), pivot=(0, 0, -1),
              taper=(0.72, 0.8), bevel=0.03, swatch="gold", parent=arm)
        K.box("PauldronB" + side, size=(0.7, 0.8, 0.14), location=(sgn * 0.04, 0, -0.14), pivot=(0, 0, -1),
              swatch="crimson", parent=arm)
        K.box("PauldronC" + side, size=(0.66, 0.76, 0.1), location=(sgn * 0.04, 0, -0.24), pivot=(0, 0, -1),
              swatch="gold_d", parent=arm)
        K.box("Vambrace" + side, size=(0.46, 0.48, 0.42), location=(0, 0, -0.5), pivot=(0, 0, 1), swatch="gold",
              parent=arm)
        K.box("Fist" + side, size=(0.4, 0.42, 0.28), location=(0, -0.02, -0.98), pivot=(0, 0, 0), swatch="skin_mid",
              parent=arm)
        arms[side] = arm
    arms["L"].rotation_euler = (-0.25, 0, 0.1)
    arms["R"].rotation_euler = (-0.3, 0, -0.1)

    # ---- the spear: upright in his right fist, leaf blade, gold collar, crimson tassel
    bpy.context.view_layer.update()
    grip = arms["R"].matrix_world @ Vector((0, -0.02, -0.98))
    spear = K.cylinder("Spear", radius=0.05, depth=3.6, verts=6, location=(0, 0, 0), pivot=(0, 0, -0.444),
                       swatch="wood_dark", parent=arms["R"])  # 1.0 m below the grip, 2.6 m above
    place(spear, grip)
    K.cone("Butt", radius=0.06, depth=0.18, verts=4, location=(0, 0, -1.0), pivot=(0, 0, -1), swatch="gold_d",
           parent=spear).rotation_euler = (math.pi, 0, 0)  # the spike points down
    K.cylinder("Collar", radius=0.085, depth=0.16, verts=6, location=(0, 0, 2.55), pivot=(0, 0, -1), swatch="gold",
               parent=spear)
    K.box("Tassel", size=(0.2, 0.2, 0.34), location=(0, 0, 2.6), pivot=(0, 0, 1), taper=(0.5, 0.5), swatch="crimson",
          parent=spear)
    blade = [(0.0, 0.0), (0.14, 0.2), (0.12, 0.46), (0.0, 0.74), (-0.12, 0.46), (-0.14, 0.2)]
    K.extrude_poly("Blade", blade, thickness=0.05, plane="XZ", location=(0, 0, 2.7), swatch="steel_l", parent=spear)

    # ---- the scutum: curved, purple, gold rim and boss and wings, held at his left side
    grip_l = arms["L"].matrix_world @ Vector((0, -0.02, -0.98))
    shield, fy = curved_shield("Shield", width=1.2, height=1.7, depth=0.08, radius=0.85, segs=6, rim=0.09,
                               location=(0, 0, 0), parent=arms["L"], swatch="purple", rim_swatch="gold",
                               back_swatch="purple_d")
    place(shield, grip_l + Vector((0.12, -0.34, 0.0)), (0.0, 0.0, -0.18))  # its face turned a little toward his front
    K.cylinder("ShieldBoss", radius=0.17, depth=0.14, verts=8, location=(0, fy, 0.02), pivot=(0, 0, 0),
               swatch="gold_l", parent=shield, axis="Y")
    for side, sn in ((1, "L"), (-1, "R")):  # the gold wings on its face, each bent to follow the curve
        w = K.extrude_poly("ShieldWing" + sn, [(x * 0.42, z * 0.42) for x, z in WING], thickness=0.04, plane="XZ",
                           location=(side * 0.12, fy + 0.012, 0.08), swatch="gold", parent=shield)
        w.rotation_euler = (0, 0, side * 0.2)
        if side < 0:
            w.scale = (-1, 1, 1)
    K.box("ShieldSpine", size=(0.08, 0.05, 1.5), location=(0, fy - 0.01, 0), pivot=(0, 0, 0), swatch="gold_d",
          parent=shield)
    return root


root = build()
bpy.context.view_layer.update()
for o in K.descendants(root):  # bake the mirrored shield wing's negative scale into its mesh
    if o.type == "MESH" and o.scale.x < 0:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.scale(bm, vec=(-1, 1, 1), verts=bm.verts)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
        bm.to_mesh(o.data)
        bm.free()
        o.scale = (1, 1, 1)
bpy.context.view_layer.update()
spear = bpy.data.objects["Spear"]
tip = spear.matrix_world @ Vector((0, 0, 2.7 + 0.74))
PARTS = {o.name: [round(v, 3) for v in o.matrix_world.translation] for o in K.descendants(root)
         if o.name in ("Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Spear", "Shield", "Javelins", "Cloak", "Eagle",
                       "WingL", "WingR")}
info = K.review_render(root, "en_emperorschampion", size=480)
info["fbx"] = K.export_fbx(root, "Enemies/EN_EmperorsChampion")
info["tris"] = K.tri_count(K.descendants(root))
info["parts"] = PARTS
info["spear_tip_world"] = [round(v, 3) for v in tip]
info["blend"] = K.save_blend("en_emperorschampion")
result = info
