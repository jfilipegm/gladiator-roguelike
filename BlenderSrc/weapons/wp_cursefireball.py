"""Note 33: WP_CurseFireball, the Ashen Censer's cursed fireball as drawn by PlayerProjectiles (Kind.Fireball): a violet
comet flying toward Blender -Y (Unity +Z), pivot at the middle of its head (where it touches and bursts).
A faceted glowing purple head with a white-hot core bulging out of its front, and tongues of violet flame swept back
behind it, splayed, darkening to smoke-purple at the tips; a few embers trailing. The game rolls it about its flight
and flickers its size; an additive violet streak and ash motes trail it (PlayerProjectiles).
The Oracle's curse colours (the palette's purples and the cursed glow), never the orange-red of things that hurt the
player, and nothing like the goblin's round bomb (WP_Bomb): it has a front and a tail.
    Tools/bheadless BlenderSrc/weapons/wp_cursefireball.py [review=1]
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)
OUT = {}


def flame(name, radius, length, angle, splay, ring, swatch):
    """A tongue of flame from the head's back toward +Y (the wide end at the head), tilted outward by `splay` (rad)
    at `angle` (rad) round the flight axis, its root `ring` m off the axis."""
    f = K.cylinder(name, radius=radius, depth=length, verts=5, location=(0, 0, 0), pivot=(0, 0, -1), axis="Y",
                   taper=0.05, swatch=swatch)
    # tilt: lean the tongue away from the axis toward `angle` (in the XZ plane), then place its root on the ring
    ax = (math.cos(angle), 0.0, math.sin(angle))
    f.rotation_mode = "AXIS_ANGLE"
    # a lean outward = a rotation about the axis perpendicular to both +Y and the outward direction
    f.rotation_axis_angle = (splay, ax[2], 0.0, -ax[0])
    f.location = (ax[0] * ring, 0.06, ax[2] * ring)
    return f


def fireball(name):
    head = K.ico(name, radius=0.2, subdiv=1, location=(0, 0, 0), scale=(1.0, 1.15, 1.0), swatch="affix_cursed")
    parts = [head,
             K.ico(name + "_Core", radius=0.13, subdiv=1, location=(0, -0.11, 0), swatch="glow_white")]
    tongues = [
        # (radius, length, angle deg, splay deg, ring, swatch)
        (0.14, 0.62, 90, 10, 0.06, "affix_cursed"),   # review 2: more glowing tongues (it's mostly seen from behind)
        (0.12, 0.52, 210, 18, 0.09, "affix_cursed"),
        (0.12, 0.55, 330, 16, 0.09, "purple_l"),
        (0.10, 0.42, 30, 28, 0.12, "affix_cursed"),
        (0.10, 0.40, 150, 26, 0.12, "purple_l"),
        (0.09, 0.46, 270, 24, 0.12, "purple_d"),
        (0.08, 0.80, 0, 4, 0.0, "purple_d"),   # the long smoky middle of the tail
    ]
    for k, (rad, ln, ang, spl, ring, sw) in enumerate(tongues):
        parts.append(flame(name + "_Flame%d" % k, rad, ln, math.radians(ang), math.radians(spl), ring, sw))
    for k, (x, y, z, r, sw) in enumerate(((0.12, 0.62, 0.05, 0.035, "glow_white"), (-0.1, 0.78, -0.06, 0.03, "affix_cursed"),
                                          (0.03, 0.95, 0.1, 0.025, "purple_l"))):
        parts.append(K.ico(name + "_Ember%d" % k, radius=r, subdiv=1, location=(x, y, z), swatch=sw))
    # bake the tongues' rotations into their meshes before the join (join keeps world space)
    import bpy
    bpy.context.view_layer.update()
    for p in parts[2:]:
        p.data.transform(p.matrix_basis)
        p.matrix_basis.identity()
    return K.join(parts, name)


K.clear_scene()
o = fireball("WP_CurseFireball")
if "ARGS" in globals() and ARGS.get("review") == "1":
    OUT["review"] = K.review_render(o, "wp_cursefireball", size=300)
OUT["fbx"] = K.export_fbx(o, "Weapons/WP_CurseFireball")
OUT["tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("wp_cursefireball")
result = OUT
