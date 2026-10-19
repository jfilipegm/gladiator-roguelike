"""M3.6 unlock weapon: WP_FlailBall, Gorm's Flail in the player's hands (pattern Orbit — the balls whirl around them).
One mesh, palette UVs, drawn instanced by PlayerProjectiles. Pivot at the ball's centre; front (Blender -Y, Unity +Z)
points away from the player, the chain stub trails back toward them (+Y). Gorm's rusty iron, re-accented in the player's
gold and cyan so it never reads as an (orange-red) enemy hazard. Budget: ~150 tris.
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)


def flail_ball(name):
    ball = K.ico(name, radius=0.24, subdiv=1, location=(0, 0, 0), swatch="iron_d")
    band = K.cylinder(name + "_Band", radius=0.245, depth=0.07, verts=8, location=(0, 0, 0), pivot=(0, 0, 0), axis="Y",
                      swatch="gold")
    parts = [ball, band]
    h = math.pi / 2  # cones point +Z; turn each onto its axis
    for k, (d, rot) in enumerate((((1, 0, 0), (0, h, 0)), ((-1, 0, 0), (0, -h, 0)), ((0, -1, 0), (h, 0, 0)),
                                  ((0, 0, -1), (math.pi, 0, 0)), ((0, 0, 1), (0, 0, 0)))):
        spike = K.cone(name + "_Spike%d" % k, radius=0.075, depth=0.2, verts=4,
                       location=(d[0] * 0.2, d[1] * 0.2, d[2] * 0.2), pivot=(0, 0, -1), swatch="steel_l")
        spike.rotation_euler = rot
        parts.append(spike)
    for k in range(3):  # the chain stub back toward the player's hand
        link = K.box(name + "_Chain%d" % k, size=(0.05, 0.12, 0.1), location=(0, 0.27 + k * 0.11, 0), pivot=(0, 0, 0),
                     swatch="iron")
        link.rotation_euler = (0, (k % 2) * h, 0)
        parts.append(link)
    parts.append(K.box(name + "_Gem", size=(0.06, 0.06, 0.06), location=(0, -0.0, 0.245), pivot=(0, 0, 0),
                       swatch="glow_cyan"))
    return K.join(parts, name)


K.clear_scene()
obj = flail_ball("WP_FlailBall")
info = K.review_render(obj, "wp_flail", size=300)
info["fbx"] = K.export_fbx(obj, "Weapons/WP_FlailBall")
info["tris"] = K.tri_count(K.descendants(obj))
info["blend"] = K.save_blend("wp_flail")
result = info
