"""Phase 5.2 Pit Goblin gear (docs/GLADIATORS.md). Front = Blender -Y (arrives as Unity +Z), origin at the base centre.
Each is one joined mesh (drawn instanced from code, so a single Mesh asset):
WP_Bomb (Bomb Satchel projectile): a round iron bomb with a bronze cap, a rope fuse and a glowing gold spark. Sits on its
  base when it lands; readable at ~10 m as a dark ball with a bright tip.
PR_Snare (Trap Slide): a flat iron jaw trap, ~0.7 m across, teeth up, a cyan rag on the chain (player-owned, §5).
ARGS: part=<name> builds one; review=1 adds the 3-angle + 64 px sheet; out=<models dir> for a scratch export."""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)
if ARGS.get("out"):  # noqa: F821 (injected by the bridge)
    K.MODELS_DIR = ARGS["out"]  # noqa: F821
OUT = {}


def bomb(name):
    root = K.empty(name)
    parts = [
        K.ico(name + "_Mesh", radius=0.17, subdiv=1, location=(0, 0, 0.17), swatch="iron_d", parent=root),
        K.ico("Shine", radius=0.05, subdiv=1, location=(-0.07, -0.11, 0.25), swatch="iron"),
        K.cylinder("Cap", radius=0.06, depth=0.06, verts=6, location=(0, 0, 0.32), swatch="bronze"),
        K.box("Fuse", size=(0.03, 0.03, 0.12), location=(0, 0, 0.37), swatch="rope"),
        K.ico("Spark", radius=0.045, subdiv=1, location=(0, 0, 0.5), swatch="glow_gold"),
    ]
    K.join(parts, name + "_Mesh")
    return root


def snare(name):
    root = K.empty(name)
    parts = [K.cylinder(name + "_Mesh", radius=0.08, depth=0.04, verts=6, location=(0, 0, 0), swatch="iron", parent=root)]
    # base plate + the two open jaws lying flat (half-rings), teeth pointing up
    parts.append(K.cylinder("Plate", radius=0.14, depth=0.03, verts=6, location=(0, 0, 0.0), swatch="rusty_iron"))
    for side in (1, -1):
        for k in range(5):
            a = math.radians(-70 + k * 35)
            x, y = math.sin(a) * 0.30 * side, math.cos(a) * 0.30 * side
            parts.append(K.box("Jaw%d%d" % (side, k), size=(0.15, 0.05, 0.04), location=(x, y, 0.0),
                               swatch="iron_d"))
            parts[-1].rotation_euler = (0, 0, -a)
            parts.append(K.cone("Tooth%d%d" % (side, k), radius=0.035, depth=0.11, verts=4,
                                location=(x * 0.93, y * 0.93, 0.03), swatch="steel_l"))
    parts.append(K.box("Spring", size=(0.62, 0.06, 0.04), location=(0, 0, 0.0), swatch="iron"))
    parts.append(K.box("Chain", size=(0.04, 0.24, 0.03), location=(0.0, 0.42, 0.0), swatch="iron_d"))
    parts.append(K.box("Rag", size=(0.12, 0.08, 0.05), location=(0.0, 0.55, 0.0), swatch="player_cyan"))
    K.join(parts, name + "_Mesh")
    return root


K.clear_scene()
builders = {"WP_Bomb": (bomb, "Weapons/"), "PR_Snare": (snare, "Props/")}
part = ARGS.get("part")  # noqa: F821  one asset per call keeps each bridge call short
for n, (build, folder) in builders.items():
    if part and part != n:
        continue
    o = build(n)
    OUT[n] = K.export_fbx(o, folder + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
    if ARGS.get("review"):  # noqa: F821
        OUT[n + "_review"] = K.review_render(o, n.lower())
    if part:
        break
OUT["blend"] = K.save_blend("goblin_gear" if not part else "goblin_gear_" + part)
result = OUT
