"""Note 34: WP_CastNet, the Net-Weaver's cast net as drawn by CastNet (CastNetTrait): 1 m in radius (the trait scales
it to its catch radius), lying flat in the XY plane with its underside on z = 0 (pivot at the middle of its underside,
so it sits on the sand), seen from above by the game's camera.
Before (v0.28.0, "the net throw doesn't really work") the cast used WP_Net, the rope-coloured net the Net weapon and the
Net-Thrower throw: rope on sand, half sunk in the floor. This one is the player's: white cords (the rim and the
spokes), cyan rings, glowing cyan knots where they cross, and iron weights on the rim, so it reads on every floor.
Player colours only, never orange-red.
    Tools/bheadless BlenderSrc/weapons/wp_castnet.py [review=1]
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)
OUT = {}

SIDES = 16  # review 2: 12 spokes and 3 rings read as a cartwheel; a finer mesh reads as a net


def ring(name, radius, thickness, swatch):
    """A closed polygon of thin boxes lying on z = 0 (its corners land on the spokes)."""
    parts = []
    edge = 2 * radius * math.sin(math.pi / SIDES) + thickness
    apothem = radius * math.cos(math.pi / SIDES)
    for k in range(SIDES):
        a = math.radians(360.0 / SIDES * (k + 0.5))
        seg = K.box(name + "_%d" % k, size=(thickness, edge, thickness * 0.75), pivot=(0, 0, -1),
                    location=(apothem * math.cos(a), apothem * math.sin(a), 0), swatch=swatch)
        seg.rotation_euler = (0, 0, a)
        parts.append(seg)
    return parts


def cast_net(name):
    parts = [K.cylinder(name, radius=0.07, depth=0.06, verts=6, pivot=(0, 0, -1), swatch="glow_cyan")]  # the centre knot
    parts += ring(name + "_Rim", 1.0, 0.06, "player_white")
    parts += ring(name + "_Outer", 0.76, 0.04, "player_cyan")
    parts += ring(name + "_Mid", 0.52, 0.04, "player_cyan")
    parts += ring(name + "_In", 0.28, 0.035, "player_cyan")
    for k in range(SIDES):  # the spokes, centre to rim
        a = math.radians(360.0 / SIDES * k)
        spoke = K.box(name + "_Spoke%d" % k, size=(0.035, 1.0, 0.03), pivot=(0, 0, -1),
                      location=(0.5 * math.cos(a) - 0.0, 0.5 * math.sin(a), 0), swatch="player_white")
        spoke.rotation_euler = (0, 0, a - math.pi / 2)
        parts.append(spoke)
        # a glowing knot where the spoke crosses the middle ring, and a lead weight on the rim
        parts.append(K.ico(name + "_Knot%d" % k, radius=0.05, subdiv=1,
                           location=(0.52 * math.cos(a), 0.52 * math.sin(a), 0.04), swatch="glow_cyan"))
        parts.append(K.ico(name + "_Weight%d" % k, radius=0.075, subdiv=1,
                           location=(1.0 * math.cos(a), 1.0 * math.sin(a), 0.06), swatch="iron"))
    import bpy
    bpy.context.view_layer.update()
    for p in parts[1:]:
        p.data.transform(p.matrix_basis)
        p.matrix_basis.identity()
    return K.join(parts, name)


K.clear_scene()
o = cast_net("WP_CastNet")
if "ARGS" in globals() and ARGS.get("review") == "1":
    OUT["review"] = K.review_render(o, "wp_castnet", size=300)
OUT["fbx"] = K.export_fbx(o, "Weapons/WP_CastNet")
OUT["tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("wp_castnet")
result = OUT
