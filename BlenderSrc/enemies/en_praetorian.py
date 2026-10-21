"""EN_Praetorian — the Imperial Arena's heavy shield infantryman (docs/ARENAS.md §5). Budget <= 300 tris.

The Emperor's guard, spawned in a line behind a Centurion: pale travertine segmented armour over a purple tunic, a
travertine helm with cheek guards and a tall upright purple plume. Left arm (+X): a tall curved scutum (purple, gold
rim and boss) that wraps round him from shin to eye: taller and more curved than the Murmillo's flat blue door, and it
bends round his side, so a line of them reads as a wall. Right hand (-X): a pilum held upright, its iron tip well
above the plume (the attack swings the arm forward into a thrust). The magenta belt is the enemy accent. Silhouette:
the curved wall of a shield with the pilum and the plume above it. Rigid parts on the shared names
(Body/Head/ArmL/ArmR/LegL/LegR); front = -Y (Unity +Z).
"""
import importlib
import math

import bmesh

import gladiator_kit as K

importlib.reload(K)

HIP_Z = 0.48


def curved_shield(name, width, height, depth, radius, segs, rim, location, parent, swatch, rim_swatch, back_swatch):
    """A scutum: a plate bent round a vertical axis, convex to the front (-Y), with a rim band top and bottom and on
    both edges (rim_swatch) and a darker inside (back_swatch). Origin at its centre. Returns (obj, front_y): front_y is
    the local y of the front face at the centre line (for the boss and spine)."""
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
    fz = (-hz, -hz + rim, hz - rim, hz)  # the front has three rows: the bottom rim, the face, the top rim
    front = [[bm.verts.new((x, y - depth / 2, z)) for z in fz] for x, y in cols]
    back = [[bm.verts.new((x, y + depth / 2, z)) for z in (-hz, hz)] for x, y in cols]
    for k in range(segs):
        for r in range(3):
            bm.faces.new([front[k][r], front[k + 1][r], front[k + 1][r + 1], front[k][r + 1]])
        bm.faces.new([back[k + 1][0], back[k][0], back[k][1], back[k + 1][1]])
        bm.faces.new([front[k][3], front[k + 1][3], back[k + 1][1], back[k][1]])  # top edge
        bm.faces.new([front[k + 1][0], front[k][0], back[k][0], back[k + 1][0]])  # bottom edge
    for col, b in ((0, back[0]), (segs, back[segs])):  # the side edges (one hexagon each)
        f = front[col]
        bm.faces.new([f[0], f[1], f[2], f[3], b[1], b[0]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = K.object_from_bmesh(name, bm, location, parent, swatch)
    ex = cols[-1][0] - 1e-4
    K.set_swatch_where(obj, rim_swatch, lambda c, n: abs(c.z) > hz - rim - 1e-4 or abs(c.x) >= ex)
    K.set_swatch_where(obj, back_swatch, lambda c, n: n.y > 0.3 and abs(c.z) < hz - rim and abs(c.x) < ex)
    mid = cols[segs // 2][1] if segs % 2 == 0 else (cols[segs // 2][1] + cols[segs // 2 + 1][1]) / 2
    return obj, mid - depth / 2


def build():
    K.clear_scene()
    root = K.empty("EN_Praetorian")
    body = K.box("Body", size=(0.6, 0.4, 0.58), location=(0, 0, HIP_Z), pivot=(0, 0, -1), taper=(1.14, 1.0),
                 swatch="travertine", parent=root)
    K.box("Belt", size=(0.62, 0.42, 0.1), location=(0, 0, 0.0), pivot=(0, 0, -1), swatch="accent_magenta", parent=body)
    K.box("Skirt", size=(0.56, 0.38, 0.22), location=(0, 0, -0.2), pivot=(0, 0, -1), taper=(0.92, 0.95),
          swatch="purple", parent=body)

    head = K.box("Head", size=(0.42, 0.4, 0.38), location=(0, -0.01, 0.58), pivot=(0, 0, -1), swatch="skin_mid",
                 parent=body)
    helm = K.box("Helm", size=(0.47, 0.45, 0.26), location=(0, 0.01, 0.2), pivot=(0, 0, -1), taper=(0.78, 0.78),
                 swatch="travertine", parent=head)
    for side, x in (("L", 0.19), ("R", -0.19)):
        K.box("Cheek" + side, size=(0.07, 0.24, 0.2), location=(x, -0.06, 0.0), pivot=(0, 0, -1),
              swatch="travertine_d", parent=head)
    K.box("Eyes", size=(0.24, 0.03, 0.05), location=(0, -0.205, 0.12), pivot=(0, 0, -1), swatch="ink", parent=head)
    # the plume: an upright purple brush rising off the crown, flaring at the top (the Murmillo's is a front-to-back fin)
    K.box("Plume", size=(0.1, 0.16, 0.42), location=(0, 0.02, 0.24), pivot=(0, 0, -1), taper=(1.5, 2.1),
          swatch="purple_l", parent=helm)

    # shield arm (left = +X): the curved scutum held square in front, like the Murmillo's (EnemyManager's ShieldBearer
    # pose keeps this arm still)
    arm_l = K.box("ArmL", size=(0.16, 0.16, 0.42), location=(0.38, 0, 0.52), pivot=(0, 0, 1), swatch="travertine_d",
                  parent=body)
    arm_l.rotation_euler = (-0.7, 0, 0)
    shield, fy = curved_shield("Shield", width=0.74, height=1.12, depth=0.06, radius=0.5, segs=3, rim=0.07,
                               location=(-0.32, -0.056, -0.49), parent=arm_l, swatch="purple", rim_swatch="gold",
                               back_swatch="purple_d")
    shield.rotation_euler = (0.7, 0, 0)
    K.box("Spine", size=(0.07, 0.05, 0.98), location=(0, fy - 0.015, 0), pivot=(0, 0, 0), swatch="gold", parent=shield)
    K.box("Boss", size=(0.2, 0.08, 0.2), location=(0, fy - 0.02, 0.04), pivot=(0, 0, 0), swatch="gold_l", parent=shield)

    # pilum arm (right = -X): upright, the iron shank and pyramid tip above the plume
    # iteration 2: 392 -> under 300 tris (no plates, neck guard, brow, greaves or pilum weight; a 3-segment shield),
    # the shield 6 cm lower so the eyes show over it
    arm_r = K.box("ArmR", size=(0.16, 0.16, 0.42), location=(-0.38, 0, 0.52), pivot=(0, 0, 1), swatch="travertine_d",
                  parent=body)
    K.box("FistR", size=(0.17, 0.17, 0.13), location=(0, 0, -0.42), pivot=(0, 0, 0), swatch="skin_mid", parent=arm_r)
    shaft = K.cylinder("Pilum", radius=0.03, depth=1.45, verts=4, location=(0, -0.03, -0.95), pivot=(0, 0, -1),
                       swatch="wood_dark", parent=arm_r)
    K.cylinder("Shank", radius=0.02, depth=0.45, verts=3, location=(0, 0, 1.4), pivot=(0, 0, -1), swatch="iron",
               parent=shaft)
    K.cone("Tip", radius=0.055, depth=0.14, verts=4, location=(0, 0, 1.85), pivot=(0, 0, -1), swatch="steel_l",
           parent=shaft, cap=False)

    for side, x in (("L", 0.15), ("R", -0.15)):
        leg = K.box("Leg" + side, size=(0.19, 0.2, 0.44), location=(x, 0, HIP_Z), pivot=(0, 0, 1),
                    swatch="travertine_d", parent=root)  # greaved legs (the skirt covers the thighs)
        K.box("Boot" + side, size=(0.21, 0.28, 0.08), location=(0, -0.04, -0.48), pivot=(0, 0, -1), swatch="leather_d",
              parent=leg)
    return root


root = build()
info = K.review_render(root, "en_praetorian", size=420)
info["fbx"] = K.export_fbx(root, "Enemies/EN_Praetorian")
info["tris"] = K.tri_count(K.descendants(root))
info["blend"] = K.save_blend("en_praetorian")
result = info
