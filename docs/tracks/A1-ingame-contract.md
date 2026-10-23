# A1 in-game — the simple-style cast as the game's models

The producer approved the A1 cast (2026-10-23 13:05: "It sure feels like an improvement you can implement it"); they
test it at home and point out flaws. Branch `phase5-artdir`. The roster scripts (`BlenderSrc/characters/a1/a1_<tag>.py`)
now also export the game's models. **No game code changes for the models:** they use the existing rigid-part pipeline.

## How the game animates characters (why this works without code)
`PartRig` (Assets/Scripts/Gameplay/Animation/PartRig.cs) finds the parts **by name** under the model: `Body`, `Head`,
`ArmL`, `ArmR`, `LegL`, `LegR` (Head and the arms under Body, the legs under the root; a beast's front legs are
ArmL/ArmR and its hind legs LegL/LegR, as in `CH_Wolf`, `EN_PitCur`, `EN_WarBoar`, `EN_Chimera`). It rotates each part
about its origin (the joint) on top of its rest pose. A missing part is fine (the Gilded Patron has no legs). Gameplay
(hitboxes, radius, speed, telegraphs) lives in the defs, not the models.

## The exporter: `BlenderSrc/characters/a1/a1_export.py` (orchestrator's; don't edit, ask)
- `X.export(root_name, folder, parts, J, rules=[...])` bakes every piece (and children of empties), assigns each to a
  part by name (your `rules` first, then the defaults for the kit's piece names), joins each part's pieces into ONE
  mesh with its origin at the joint, builds the hierarchy, writes
  `Assets/Art/Models/A1/<folder>/<root_name>.fbx` (`folder`: `Characters` or `Enemies`), and renders a **pose sheet**
  `Screenshots/a1/ingame/a1_<root_name>_poses.png` (rest, run, swing, hit).
- `X.joints(p, scale=1, offset=(0,0,0))`: the humanoid's joints for the proportions `p`. If your script scaled, moved,
  hunched or stretched the body, give the joints that match what you built (pass `scale`/`offset`, or write the dict
  yourself: `{"Body": (x, y, z), ...}` in world space, front = -Y).
- `rules`: `[("Blade", "ArmR"), ("Shield", "ArmL"), ("Cape", "Body"), ("Litter", "Root"), ...]`, prefix → part;
  `"side"` = ArmL/ArmR or LegL/LegR by the piece's L/R suffix (or which side of x = 0 it's on). `"Root"` = doesn't
  move (rare: a litter, a base). Unknown pieces stop the export with their names. Watch the defaults: `Arm*` catches
  "Armour..." and `Leg*` catches "Legion..." — rename the piece or add a rule before the default can catch it.
- **The worked example:** `a1_nobody.py` (`export=1`; root `CH_Nobody`, 906 tris, 6 renderers). Copy its pattern:
  `EXPORT = ARGS.get("export") == "1"`, `OX = 0.0 if EXPORT else -0.55`, `build()` returns the pieces (and `p`), then
  export or review. Running without `export=1` must still make the review sheet as before.

## Rules for each character
1. **Root name = the original's root** (`CH_Nobody`, `EN_Convict`, ... see `docs/tracks/A1-rigs.json`: the original's
   root, parts, joints, height, renderers, tris). The same **set of parts** as the original (if the original has no
   legs, neither does yours; if it has six, so does yours).
2. **Size: height within ±10 % of the original** (A1-rigs.json `height`; the hitbox, health bar and the select
   screen were tuned to it). If yours differs more, scale the build (and the joints) and say so.
3. **Every piece follows the right part.** Read the pose sheet: a weapon or shield moves with its arm; a cape,
   cloak, skirt, belt, quiver with the body; hair, helmet, crest, hood with the head; boots and greaves with their
   leg. A long skirt or robe over the legs belongs to Body (the legs swing under it, as in the originals).
   Floating or left-behind pieces = fix the rules. At most 3 iterations.
4. Joint positions: at the shoulder/hip/neck you built (the arm turns about the top of the arm, the head about
   the neck), so the rotation looks right in the run and swing poses.
5. Front = -Y, origin at the feet (x = y = 0, z = 0 at the soles), built at ox = 0 when exporting.
6. Don't touch the originals (scripts, .blend, `Assets/Art/Models/Characters|Enemies/*.fbx`), Unity, `Assets/` other
   than your new FBX files under `Assets/Art/Models/A1/`, ProjectSettings, docs/PROGRESS.md.
7. Commit per character or two (script + FBX; let git pick up the `.fbx` as it does today), push your branch, never
   force-push. Notes: one line per character added to your batch's notes file (tris, renderers, height vs the
   original, anything doubtful).

## Skins (R-glad only)
The gladiators' 20 skins (`BlenderSrc/characters/skins.json`, `build_skins.py`) are palette remaps run over the
base script. For A1: add an `a1_script` per gladiator (e.g. `"a1/a1_nobody.py"`) and a `style=a1` argument to
`build_skins.py` that runs it with `export=1`; `K.export_fbx` now writes A1 skins to
`Assets/Art/Models/A1/Characters/Skins/<base><suffix>.fbx` (classic skins stay in `Characters/Skins/`). Where the A1
script uses other swatches than the classic one, add the A1 swatches to the skin's remap (the same idea: the
skin's colours, the weapon kept) so each skin still reads as itself. Review sheet per gladiator (default + its two
skins) in `Screenshots/a1/ingame/`. Don't change the classic skins' output.

## Batches (the same workers and branches as the roster)
| Worker | Branch | Characters |
|---|---|---|
| R-glad | `a1-roster-glad` | 11 gladiators (+ the Wolf, the Bookmaker; the Nobody is done) + the 20 skins |
| R-tier12 | `a1-roster-tier12` | 13 tier 1–2 enemies |
| R-tier3boss | `a1-roster-tier3boss` | 5 tier 3 enemies + 6 bosses |

The orchestrator then (Unity): imports, switches every def/skin/scene reference from each classic model to its A1
model of the same name (one menu item, reversible), re-bakes portraits, checks every character in Play mode, runs the
tests and the 300-enemy stress run, and asks the producer to merge and release.
