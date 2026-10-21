"""The Gilded Patron's projectiles (Phase 5.13, docs/BOSSES.md; CLAUDE.md §5: anything that hurts the player is
orange-red). PR_PurseBomb: a fat gold purse with a crimson tie and a burning orange fuse (lobbed, it bursts into real
gold). PR_HotCoin: a red-hot coin, a glowing orange face in a gold rim, flat in the XY plane (Unity XZ; EnemyProjectiles
turns it to face its flight). Single meshes drawn instanced, origin at the centre."""
import importlib

import gladiator_kit as K

importlib.reload(K)
OUT = {}


def purse_bomb(name):
    bag = K.ico(name, radius=0.26, subdiv=1, scale=(1.0, 1.0, 0.9), swatch="gold")
    neck = K.cylinder(name + "_Neck", radius=0.1, depth=0.12, verts=6, location=(0, 0, 0.2), pivot=(0, 0, -1), taper=1.6,
                      swatch="gold_d")
    tie = K.cylinder(name + "_Tie", radius=0.11, depth=0.05, verts=6, location=(0, 0, 0.2), pivot=(0, 0, -1),
                     swatch="crimson")
    fuse = K.cone(name + "_Fuse", radius=0.07, depth=0.16, verts=4, location=(0, 0, 0.32), pivot=(0, 0, -1),
                  swatch="glow_hazard_orange")
    K.set_swatch_where(bag, "gold_d", lambda c, n: n.z < -0.6)
    return K.join([bag, neck, tie, fuse], name)


def hot_coin(name):
    face = K.cylinder(name, radius=0.26, depth=0.06, verts=8, pivot=(0, 0, 0), swatch="glow_hazard_orange")
    rim = K.cylinder(name + "_Rim", radius=0.3, depth=0.045, verts=8, pivot=(0, 0, 0), swatch="gold")
    return K.join([face, rim], name)


K.clear_scene()
items = {"PR_PurseBomb": purse_bomb("PR_PurseBomb"), "PR_HotCoin": hot_coin("PR_HotCoin")}
x = 0.0
for n, o in items.items():
    o.location.x = x
    x += 1.0
review = ARGS.get("review")  # noqa: F821
for n, o in items.items():
    saved = o.location.copy()
    o.location = (0, 0, 0)
    if review == n:
        OUT["review"] = K.review_render(o, n.lower(), size=300)
    OUT[n] = K.export_fbx(o, "Props/" + n)
    OUT[n + "_tris"] = K.tri_count([o])
    o.location = saved
OUT["blend"] = K.save_blend("patron_projectiles")
result = OUT
