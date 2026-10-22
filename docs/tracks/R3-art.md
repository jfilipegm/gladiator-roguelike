# R3 art — Asterion, the Chained (worker notes, 2026-10-22)

**Branch:** `claude/zealous-mirzakhani-ed2dcc` (cut from `origin/phase5-asterion`). Blender only through
`Tools/bheadless`; no Unity.

## What I built
| File | What |
|---|---|
| `BlenderSrc/characters/ch_asterion.py` | `CH_Asterion`, the character, with his chain as rigid parts |
| `BlenderSrc/weapons/wp_chain.py` | the chain's pieces (shared with the character) and the three `WP_` meshes |
| `Assets/Art/Models/Characters/CH_Asterion.fbx`, `BlenderSrc/blend/ch_asterion.blend` | exports |
| `Assets/Art/Models/Weapons/WP_Chain.fbx`, `WP_ChainLink.fbx`, `WP_ChainWeight.fbx`, `BlenderSrc/blend/wp_chain.blend` | exports |

Regenerate: `Tools/bheadless BlenderSrc/weapons/wp_chain.py` and `Tools/bheadless BlenderSrc/characters/ch_asterion.py`.

### CH_Asterion: 1,112 tris (budget 1,200), horn tip 1.83 m
A chibi minotaur, the labyrinth's prisoner: tragic, not monstrous (no fangs, no spikes, no red).
- **Silhouette:** wide bull horns over a dark hump of shoulder fur, the head low and forward with a long pale snout,
  a heavy V-shaped body on short legs with split hooves, the chain trailing on the ground behind him to a block of
  masonry. He's the widest gladiator (1.48 m across the arms) and the only one with a snout.
- **Story details:** his RIGHT horn snapped to a stump; drooping cyan eyes under a heavy brow; a bronze nose ring; a
  bronze collar with a rope band worn into it and the frayed rope end hanging; a broken shackle on his left wrist (an
  iron cuff with a snapped link); pale scars across the chest, the left arm and the left brow; a sackcloth kilt on a
  rope belt; a bull's tail.
- **Player readability (cyan):** the eyes (front), a square labyrinth on his upper back (the camera behind sees it),
  a cyan glyph on the masonry block.
- **Rigid parts** (same names and joints as the other gladiators; all identity bind rotations; Blender front -Y,
  left +X). Unity prefab space (x right, y up, z forward, his left = -X):
  - `Body` (0, 0.50, 0); `Head` under Body at local (0, 0.50, 0.14); `ArmL` (-0.52, 0.52, 0) and `ArmR`
    (0.52, 0.52, 0) under Body; `LegL` (-0.17, 0.50, 0) and `LegR` (0.17, 0.50, 0) under the root.
  - Fists end ~0.68 below the shoulders (world ~0.34 m); the cuffs sit at -0.47.
- **The chain** (child of `ArmR`): `Chain` (an empty, identity, at local (0, -0.52, -0.12) in Unity: the bottom of the
  right cuff, behind the fist) → `ChainSeg0` → … → `ChainSeg6` (nested, 24 tris each: two links, light steel / dark
  iron) → `ChainEnd` (84 tris: the masonry block, its iron staple, the cyan glyph).
  - Each `ChainSegN` runs from its pivot along its **local -Y** (Unity; Blender -Z) for **0.26 m**; the next joint is
    at local (0, -0.26, 0). `ChainEnd` continues `ChainSeg6`'s line.
  - Bind rotations (about local X, bending backward): Seg0 14°, Seg1 28.2°, Seg2 47.8° (cumulative 90°: flat on the
    ground from Seg2 on), Seg3..Seg6 small yaws (+5, -6, +4, -5°) about their parent's up axis so it snakes. The flat
    links' line is 5.5 cm above the ground, the upright links touch it; the trail reaches 2.05 m behind his hips.
  - Code can rotate `Chain` for a rigid sweep, or drive each segment (a whip wave: per-joint delayed rotations; or a
    follow-the-leader drag, see "Needs").

### The chain weapon: three `WP_` meshes (one mesh each, origin at the grip, front = Unity +Z)
| Mesh | Tris | Layout |
|---|---|---|
| `WP_Chain` | 276 | the whole chain straight out: 8 segments of 0.26 m, then the block; far end **2.47 m** ahead. A rigid sweep = draw it rotating around the player (`meshScale` stretches the reach). |
| `WP_ChainLink` | 24 | one segment (two links), 0.26 m from its inner joint to the next. For a chain drawn link by link along an arc or a curve: segment i at i × 0.26, the weight after the last. |
| `WP_ChainWeight` | 84 | the masonry block: staple at the joint, block 0.05–0.39 m ahead, 0.40 wide, 0.30 tall, centred on the chain's line (y −0.15…+0.23 with the chip). |

The same builders (`link_pair`, `masonry` in `wp_chain.py`) make the character's chain, so the in-hand chain and the
`WP_` meshes match. Colours: steel/iron links, grey stone (`stone_grey`/`ash`), cyan glyph: never the hazard row.

## Reviews (Screenshots/ is git-ignored: copies in the shared notes folder as `r3_art_*.png`)
- `Screenshots/blender/ch_asterion_review.png` — the kit's front / side / 3/4 + 64 px (fitted to the whole model, so
  the 2 m chain makes him small in it).
- `Screenshots/blender/ch_asterion_review_close.png` — fitted to the body: front, his right side, 3/4, back, the game
  camera (behind and above), and 64 px silhouettes (3/4, front, back). Tool: `r3_inspect.py` in the notes folder.
- `Screenshots/blender/r3_lineup.png` — every gladiator, same scale (front, 3/4, 3/4 silhouettes, and each one's
  fitted 64 px 3/4 + front). Tool: `r2_lineup.py` in the notes folder, with `models=` set to the ten gladiators.
- `Screenshots/blender/wp_chain_review.png`, `wp_chainlink_review.png`, `wp_chainweight_review.png`.

**Lineup verdict:** distinct from all nine. The Murder Machine is a tall narrow column with a pointed hood and a slab
blade (2.2 m); the Beastmaster is slim with upright wolf ears and a spear; the Blood Knight's horns rise from a black
helm over a red cape. Asterion is the widest and lowest mass, horns spreading sideways, a snout forward, the only
brown-and-sackcloth heavy, and from behind the chain on the ground.

## Iterations (3 of 3)
1. First build: the head read as a box (a "helmet" with a small muzzle), thin horns lost at 64 px, the hump read as a
   backpack from behind, the ears were invisible, and the chain snaked out to his side so the block showed beside him.
2. The head redesigned: a broad poll, a long pale snout, a lower neck (the head forward of the hump), thicker horns
   curving forward; the hump widened into a mane across the shoulders; the trail straightened; the collar ring and
   link replaced by a frayed rope end (two rings under the snout read as a goatee).
3. Horns longer and more upright (a point above the head in the 3/4 silhouette); dark ears (pale ones vanished against
   his torso from the front).

## Verified
- Tri counts from Blender: CH_Asterion 1,112; WP_Chain 276, WP_ChainLink 24, WP_ChainWeight 84.
- FBX round trip in headless Blender (re-import): the root `CH_Asterion` at the origin with only the kit's 180° yaw;
  Body/Head/ArmL/ArmR/LegL/LegR with identity rotations at the positions above; the chain hierarchy intact
  (`Chain` → `ChainSeg0..6` → `ChainEnd`, joints 0.26 apart); each `WP_` mesh with its origin at (0, 0, 0)
  (root-at-origin export, `at_origin` as in `imperial_arena.py`).
- Renders: 3 angles + 64 px for every model, the lineup.

## Unverified
- Anything in Unity: the import, the palette material, the look under the game's lighting, `PlayerPartAnimator`
  driving him, the portrait bake.
- The 64 px 3/4 silhouette reads as "a wide horned beast" rather than clearly "a bull": the horns and snout carry it
  front-on and in profile; at 3/4 from above he's a broad mass with horn points. Distinct from everyone, so shipped at
  iteration 3 (the cap).

## Needs from others (code part / orchestrator)
1. **Chain drag (code):** the chain is rigid parts under `ArmR`, so the run cycle's arm swing (and the body bob)
   swings the whole trail: it dips into the floor and lifts. Recommend a small component that, outside sweeps, runs a
   follow-the-leader on the joints (Seg2..ChainEnd follow the previous joint at 0.26 m, clamped to the ground height,
   each segment aimed at the next joint; Seg0/Seg1 hang from the cuff to the first ground joint). Cheap fallback: a
   `PartAnimProfile` with `weaponArmSwingScale` and `idleArmSway` near 0, accepting a little bob.
2. **Codex full-body bake (`PortraitBaker`, body view):** it frames all renderers, so the 2 m trail makes him ~60 %
   the size of the others and off-centre. Skip the renderers under `Chain` there (or fold the chain for the bake).
   The head-and-shoulders portrait frames on `Head` and is fine (the horns widen it a little).
3. If the design part changes the weapon's form (e.g. no weight, a shorter chain, a hook), tell me: `wp_chain.py`
   holds every chain dimension (`SEG_LEN`, `LINK`, `WP_SEGS`) and the character's `TRAIL_SEGS`.
