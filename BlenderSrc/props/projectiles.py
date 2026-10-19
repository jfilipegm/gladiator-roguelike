"""Enemy projectiles (CLAUDE.md §5: anything that hurts the player is orange-red).
PR_SlingStone: faceted glowing orange-red stone (Slinger). PR_FirePot: small clay pot with a burning top (Fire-Pot
Thrower). PR_Arrow: orange-red arrow with a glowing head (Shield Archer), pointing -Y so it arrives facing Unity +Z
(drawn along its velocity, no tumble). Single meshes (drawn instanced by EnemyProjectiles), origin at the centre."""
import math
import importlib

import gladiator_kit as K
from mathutils import Matrix

importlib.reload(K)
OUT = {}


def sling_stone(name):
    return K.ico(name, radius=0.12, subdiv=1, scale=(1.0, 0.9, 0.82), swatch="glow_hazard_red")


def fire_pot(name):
    # bulging clay belly (two frustums), dark neck, a stubby flame: reads as "burning pot" at a glance
    low = K.cylinder(name, radius=0.10, depth=0.10, verts=6, pivot=(0, 0, 1), taper=1.55, swatch="rust_light")
    up = K.cylinder(name + "_Up", radius=0.155, depth=0.09, verts=6, pivot=(0, 0, -1), taper=0.55, swatch="rust_light")
    neck = K.cylinder(name + "_Neck", radius=0.075, depth=0.05, verts=6, location=(0, 0, 0.09), pivot=(0, 0, -1),
                      swatch="rust")
    flame = K.cone(name + "_Flame", radius=0.085, depth=0.15, verts=5, location=(0, 0, 0.14), pivot=(0, 0, -1),
                   swatch="fire_mid")
    K.set_swatch_where(neck, "fire_core", lambda c, n: n.z > 0.5)
    K.set_swatch_where(low, "rust", lambda c, n: n.z < -0.5)
    return K.join([low, up, neck, flame], name)


def arrow(name, length=0.8):
    # built along +Y, then turned to point -Y (Blender front = Unity +Z)
    shaft = K.cylinder(name, radius=0.03, depth=length, verts=5, pivot=(0, 0, 0), axis="Y", swatch="hazard_orange")
    head = K.cone(name + "_Head", radius=0.075, depth=0.2, verts=5, location=(0, length / 2, 0), pivot=(0, 0, -1),
                  axis="Y", swatch="glow_hazard_red")
    fl = [K.box(name + "_Fl%d" % k, size=(0.012 if k == 0 else 0.15, 0.18, 0.15 if k == 0 else 0.012),
                location=(0, -length / 2 + 0.1, 0), pivot=(0, 0, 0), swatch="white") for k in range(2)]
    obj = K.join([shaft, head] + fl, name)
    obj.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
    return obj


K.clear_scene()
items = {"PR_SlingStone": sling_stone("PR_SlingStone"), "PR_FirePot": fire_pot("PR_FirePot"), "PR_Arrow": arrow("PR_Arrow")}
for n, o in items.items():
    OUT[n] = K.export_fbx(o, "Props/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
review = K.review_render(items["PR_Arrow"], "pr_arrow", size=300)
OUT["review_arrow"] = review.get("sheet")
OUT["blend"] = K.save_blend("projectiles")
result = OUT
