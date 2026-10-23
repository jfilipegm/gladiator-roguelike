# A1 roster — every character in the producer's chosen style (screenshots only)

The producer picked **lineup variant A** for the Nobody (`docs/tracks/A1-art-direction.md` §3d: "an improvement but
not the final version") and asked to see **every other character in that design, as screenshots**, before anything
goes into the game. Branch `phase5-artdir`. **Nothing in the game changes:** new Blender scripts and renders only.

## The style (keep it; it's what the producer liked)
- **Chibi proportions on the shared joints:** hips 0.40, shoulders 0.86, neck 0.95, head ~37 % of the height
  (`simple_kit.CHIBI`). Bigger or smaller characters scale `p` (e.g. a brute: wider `TW`, thicker `AR`/`LR`; a goblin:
  shorter legs, bigger head; a boss: the whole thing ×1.4–2 via a bigger `p`, or build at scale and say so).
- **OSRS-simple shapes:** chamfered boxy sections (`oct8`), plain tapered limbs (`hex6`), no muscles, no anatomy,
  no smooth blobs, no realism. A few big clear pieces (helmet, armour plate, cape, weapon), not many small ones.
- **Flat-faceted shading** (every face flat), **colour per face** from the palette (`Assets/Art/Textures/Palette/palette.json`).
- **Minimal faces:** slit eyes, angled brows, a wedge nose (or a helmet / mask / skull instead).
- **Identity from the original:** read the original script (`BlenderSrc/characters/ch_*.py`, `BlenderSrc/enemies/en_*.py`)
  and keep its silhouette idea, colours, headgear, weapon and accessories, its accent patch (enemies' magenta/lime
  readability patches stay), the gameplay colour language (anything harmful orange-red; the player's cyan only on
  the player's gladiators). Translate the details into the simple style; don't copy the boxes.
- **Triangles (guides, report them):** gladiators ≤ 1,200, regular enemies ≤ 450, elites ≤ 800, bosses ≤ 2,500.

## The kit: `BlenderSrc/simple_kit.py` (use it; don't edit it — ask the orchestrator if it needs a change)
- `humanoid(ox, p=None, skin=, top=, belt=, foot=, sole=, hair=, bare_right_shoulder=, sleeves=, legs=, leg_colour=,
  hair_style=, face=, skip=)` → `{"parts": [...], "a": anchors}`: the base body; anchors give hand positions, head
  sizes and heights (`hz(f)`: a height f of the way up the head), band, chest, waist, hem, shoulders, back/front y.
- Pieces: `loft(name, rings, swatch, ox)` (rings `[(z, points)]` from `oct8` / `hex6` / `ngon`), `box`, `cone`,
  `paint(obj, swatch, test(c, n), ox)`, `flat(obj)`, `headband(a, colour, ox)`, `round_shield(...)`; also
  `stylized_kit.shell` (curved plates: pauldrons, helmets' cheek guards) and `gladiator_kit.extrude_poly` (blades,
  crests, outlines) — pass their results through `flat()`.
- Lofts run along Z. For horizontal bodies (quadrupeds: the Pit Cur, the Wolf, the War Boar, the Chimera), loft
  along Z and rotate the mesh 90° about X (`obj.data.transform(Matrix.Rotation(...))`), or build from boxes.
- `review(tag, parts, original_blend="en_brute", original_root="EN_Brute")`: renders the new design (build it at
  `ox = -0.55`) beside the original appended from `BlenderSrc/blend/<original_blend>.blend`, front / back /
  three-quarter, into `Screenshots/a1/a1_<tag>.png` (an absolute shared folder, git-ignored; all worktrees write
  there). For big characters pass `height=` and `gap=` (and build at `ox = -gap/2`).
- **The worked example:** `BlenderSrc/characters/a1/a1_nobody.py` (the Nobody). Copy its pattern.

## How to run
- `Tools/bheadless BlenderSrc/characters/a1/a1_<id>.py` (headless Blender 5.2; prints the script's `result`).
  **Never open Unity.** Look at your sheets (read the PNG) and judge them; **at most 3 iterations per character**.
- One script per character: `BlenderSrc/characters/a1/a1_<id>.py`, tag `<id>` = the original's id in snake case
  (`blood_knight`, `murder_machine`, `pit_cur`, ...). Commit your scripts on your branch, push, never force-push.
- Notes: `docs/tracks/A1-roster-<batch>.md`: per character, one line (tris, what was kept, what was changed, any doubt).

## Batches
| Worker | Characters (original blend → tag) |
|---|---|
| R-glad1 | ch_bloodknight, ch_murdermachine, ch_pitgoblin, ch_thunderchampion, ch_goldenidol |
| R-glad2 | ch_ashenoracle, ch_asterion, ch_beastmaster, ch_wolf (the Beastmaster's companion), ch_netweaver, the Bookmaker (`CH_Bookmaker` in `secrets.blend`) |
| R-tier1 | en_convict, en_pitcur, en_shieldthug, en_slinger, en_brute, en_firepot, en_assassin |
| R-tier2 | en_murmillo, en_netthrower, en_shieldarcher, en_beasthandler, en_warboar, en_centurion |
| R-tier3 | en_praetorian, en_gildedmercenary, en_sorcerer, en_skeletallegionnaire, en_champion |
| R-boss | en_gorm, en_beastlord, en_gildedpatron, en_emperorschampion, en_eternalvictor, en_chimera |

The orchestrator collects the sheets into a gallery for the producer.
