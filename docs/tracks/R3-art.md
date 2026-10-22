# R3 art — Asterion, the Chained (worker notes, 2026-10-22)

**Branch:** `claude/zealous-mirzakhani-ed2dcc` (cut from `origin/phase5-asterion`). Blender only through
`Tools/bheadless`; no Unity. Built first from the brief alone, then **rebuilt to the design part's spec**
(`r3-design.md` in the shared notes folder: the white bull's pale hide, the Minoan kilt, lyre horns, the `Chain` part
under the root, `WP_LabyrinthChain` = `Link` + `Weight`). The names below are final.

## Files
| File | What |
|---|---|
| `BlenderSrc/characters/ch_asterion.py` | `CH_Asterion`, with his chain as rigid parts |
| `BlenderSrc/weapons/wp_labyrinthchain.py` | `WP_LabyrinthChain` (`Link`, `Weight`); its builders also make the character's chain |
| `Assets/Art/Models/Characters/CH_Asterion.fbx`, `BlenderSrc/blend/ch_asterion.blend` | exports |
| `Assets/Art/Models/Weapons/WP_LabyrinthChain.fbx`, `BlenderSrc/blend/wp_labyrinthchain.blend` | exports |

Regenerate: `Tools/bheadless BlenderSrc/weapons/wp_labyrinthchain.py`, then
`Tools/bheadless BlenderSrc/characters/ch_asterion.py`. (The first pass's `WP_Chain`, `WP_ChainLink`,
`WP_ChainWeight` are deleted: the design's two-mesh FBX replaces them.)

## CH_Asterion: 1,148 tris (budget 1,200), horn tips 1.81 m, 1.55 m wide
A chibi minotaur, the labyrinth's prisoner: tragic, not monstrous (no fangs, no spikes, no red).
- **Look (the design's palette):** hide `ash` (the white bull's pale grey-cream), `bone` snout and chest, a `charcoal`
  mane on the hump and shoulders, `charcoal` ears and forelock (white-park-cattle points), sad `ink` eyes (drooping
  outer corners), `ink` hooves, a `cloth_blue` kilt with an `ochre` hem on a rope belt, a bronze collar with a rope band
  worn into it and a snapped bronze ring at his left, a bronze nose ring, a broken iron shackle on his left wrist, an
  iron cuff on his right with the chain wound twice round the forearm, `white` scars (chest, left arm, left brow), a
  bull's tail. **Lyre horns** (`bone`, `charcoal` tips): out from the poll, up and forward, the tips flaring out
  again; his right tip chipped short.
- **Player mark:** a `player_cyan` square labyrinth branded on his upper back, under the mane (the camera behind sees
  it). It's one meander turn, not a band across the shoulders: a band cost ~120 tris.
- **Rigid parts** (identity bind rotations). Unity prefab space (x right, y up, z forward, his left = -X):
  - `Body` (0, 0.50, 0); `Head` under Body at (0, 0.50, 0.14); `ArmL` (-0.52, 0.52, 0) and `ArmR` (0.52, 0.52, 0)
    under Body; `LegL` (-0.17, 0.50, 0) and `LegR` (0.17, 0.50, 0) under the root. Long arms: the fists end 0.74 below
    the shoulders (~0.28 m off the ground).
  - **`Chain`**: a **child of the root** (not ArmR), an empty with identity rotation at **(0.52, 0.435, -0.12)**: the
    right hand's rest position (the cuff's bottom, just behind the fist). Under it, nested: `ChainSeg0` → `ChainSeg1`
    → `ChainSeg2` → `ChainSeg3` (two bronze links each, 24 tris) → `ChainEnd` (the stone, 152 tris: the same mesh as
    `WP_LabyrinthChain`'s `Weight`). Hide the whole chain with `SetActive` on `Chain` (it's an empty: the renderers are
    on its children).
  - Each `ChainSegN` runs from its pivot along its **local -Y** (Unity; Blender -Z) for **0.4 m**; the next joint is at
    local (0, -0.4, 0). Bind rotations (bending backward about local X, Blender values): Seg0 35° (hangs from the cuff),
    Seg1 48.9° (reaches the ground), Seg2 6.1° (flat, yaw 4°), Seg3 21.3° (rises to the ring bolt, yaw -5°); `ChainEnd`
    -21.3° (level again, so the stone sits on the ground; its origin is the ring bolt's tip). The trail ends 2.13 m
    behind his hips; the upright links touch the ground.
- **Silhouette marks (64 px):** the lyre horns' width with the tips flaring out, the hump with the head low and forward,
  the stone on the ground behind him.

## WP_LabyrinthChain: one FBX, root `WP_LabyrinthChain` at the origin, two meshes with their origins at the root's
| Mesh | Tris | Layout (Unity: front = +Z) |
|---|---|---|
| `Link` | 36 | 3 interlocked links (link length 0.26, pitch 0.2, 0.15 wide, 0.06 thick), **0.6 m along +Z**, origin at its start. The first and third links stand upright, the middle one lies flat: lay N end to end, each **rolled 90°** from the previous. Broad faces `bronze_d`, edges `bronze`, so upright and flat links alternate dark / light from any side whatever the roll. |
| `Weight` | 152 | The stone: 0.42 wide, 0.42 tall, 0.48 long (`stone_grey`, bevelled), **origin at its centre**, a `bronze` ring bolt on its **-Z face** (tip at z = -0.36), a `bone` meander on top, a lump of `ash` mortar on a corner. Put its centre **0.33 m past the end of the last Link** (the last link's tip then passes through the ring). |

## Reviews (Screenshots/ is git-ignored: copies in the shared notes folder as `r3_art_*.png`)
- `ch_asterion_review.png` — the kit's front / side / 3/4 + 64 px (fitted to the whole model with the 2 m chain, so
  he's small in it).
- `ch_asterion_review_close.png` — fitted to the body: front, his right side, 3/4, back, the game camera, and 64 px
  silhouettes (3/4, front, back). Tool: `r3_inspect.py` in the notes folder.
- `ch_asterion_floors.png` — front, 3/4 and game camera on marble, `sand_l` and `mud`: readable on all three (mid-grey
  hide against marble, the charcoal mane and blue kilt on sand, he pops on mud).
- `r3_lineup.png` — all ten gladiators, same scale. Tool: `r2_lineup.py` in the notes folder.
- `wp_labyrinthchain_review.png` (4 rolled Links + the stone, as code lays them), `wp_labyrinthchain_weight_review.png`.

**Lineup verdict:** distinct from all nine. The Murder Machine is a tall narrow column with a pointed hood and a slab
blade; the Beastmaster is slim with upright wolf ears; the Blood Knight's horns curl *in* above a black helm and a red
cape (Asterion's flare *out*, and he's twice as wide). His pale hide is close to the Ashen Oracle's grey robe in
colour, but not in shape (the Oracle is a slim column with a staff).

## Iterations
First pass (from the brief): 3 iterations (the head read as a helmet → a long snout and a lower neck; a backpack-like
hump → a mane across the shoulders; thin horns → thicker, longer). Rebuild to the design's spec: 2 iterations of the
horns (the design's tall lyre rose straight up and read like the Blood Knight's at 64 px → a longer outward base and
tips flaring out).

## Deviations from the design part's suggestions (kept for the model's sake)
- **Horn tips 1.81 m, ~1.25 m tip to tip** (the design: 1.95 m, ~1.0 m). Taller, narrower lyres read as the Blood
  Knight's horns at 64 px. The portrait still frames them: the head with its horns is ~0.87 m tall and ~1.25 m wide.
- **The chain in segments under `Chain`**, not one mesh: still one part to hide or show, and a drag can be added later.
- **The weight** is 0.42 × 0.42 × 0.48 as asked, but its meander is on the top face only (152 of the 160 tris).
- **The cyan brand** is one labyrinth turn on the upper back, not a band across the shoulders (tris).
- Kept from the first pass: the nose ring, the chipped right horn tip, the chain wound round his right forearm.

## Verified
- Tri counts from Blender: CH_Asterion 1,148 (the chain 248 of it); Link 36, Weight 152.
- FBX round trip in headless Blender: root `CH_Asterion` at the origin with only the kit's 180° yaw; the six parts
  with identity rotations at the positions above; `Chain` a child of the root, its segments and bind rotations as
  listed; `WP_LabyrinthChain` = an empty root at the origin with the meshes `Link` and `Weight`, both at (0, 0, 0).
- Renders: 3 angles + 64 px for every model, the lineup, the three floors.

## Unverified
- Anything in Unity: the import, the palette material under the game's lighting, `PlayerPartAnimator` on him, the
  portrait and Codex bakes.
- While he runs, his right fist swings away from the chain's top (the chain stays at the rest position): a small gap
  by design. A follow-the-leader drag would close it if it shows.

## Needs from others (code part / orchestrator)
1. **Codex full-body bake (`PortraitBaker`, body view):** it frames all renderers, so the 2 m trail makes him ~60 %
   the size of the others and off-centre. Skip the renderers under `Chain` there (or hide `Chain` for the bake). The
   head-and-shoulders portrait frames on `Head` and is fine.
2. If the weapon's form changes again, `wp_labyrinthchain.py` holds every dimension (`LINK`, `PITCH`, `BLOCK`,
   `WEIGHT_OFFSET`) and `ch_asterion.py` builds its chain from them.
