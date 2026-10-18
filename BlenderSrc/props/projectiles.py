"""Enemy projectiles (CLAUDE.md §5: anything that hurts the player is orange-red).
PR_SlingStone: faceted glowing orange-red stone (Slinger). PR_FirePot: small clay pot with a burning top (Fire-Pot
Thrower). Single meshes (drawn instanced by EnemyProjectiles), origin at the centre (they tumble in flight)."""
import importlib

import gladiator_kit as K

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


K.clear_scene()
items = {"PR_SlingStone": sling_stone("PR_SlingStone"), "PR_FirePot": fire_pot("PR_FirePot")}
for n, o in items.items():
    OUT[n] = K.export_fbx(o, "Props/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
review = K.review_render(items["PR_FirePot"], "pr_firepot", size=300)
OUT["review_pot"] = review.get("sheet")
OUT["blend"] = K.save_blend("projectiles")
result = OUT
