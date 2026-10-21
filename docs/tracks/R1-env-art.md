# R1 env-art — the Imperial Arena's environment models

**Source:** `BlenderSrc/environment/imperial_arena.py` (headless, through `Tools/bheadless`). Rebuild one part:

```
Tools/bheadless BlenderSrc/environment/imperial_arena.py part=terrain|structures|gates|spina [review=<model>|all]
Tools/bheadless BlenderSrc/environment/imperial_arena.py part=overview     # preview renders, exports nothing
Tools/bheadless BlenderSrc/environment/imperial_arena.py part=reference    # the Grand Arena's FBX tris, for budgets
```

Each part takes 1 to 5 s. Outputs: 15 FBX in `Assets/Art/Models/Environment/` (no `.meta`: Unity makes them on
import), `BlenderSrc/blend/imperial_arena_{terrain,structures,gates,spina}.blend`, reviews in `Screenshots/blender/`
(gitignored: read them in this worktree).

## Conventions (same as `grand_arena.py`)
- 1 Blender unit = 1 m. Export through `K.export_fbx`: the model's front (Blender −Y) arrives as Unity **+Z**. Place
  every wall piece **on the wall line with `yaw = YawOf(inward)`**, as `GrandArenaLayout` does.
- Below, "local" means **Unity-local** (x right, y up, +z toward the arena) unless it says Blender.
- The floor is built in Unity world coordinates. Place it at the origin with yaw 0, `PieceRole.Walkable`.

## The pieces

| Model | Tris | Grand Arena equivalent | Size (x × z × height, m) | Origin |
|---|---|---|---|---|
| `SM_ImpFloor` | 9,942 | `SM_GrandFloor` 6,664 | 193 × 91 (the wall line + 1.5 m) | world origin |
| `SM_ImpStand_4m` | 180 | `SM_GrandStand_4m` 168 | 4.0 × 7.8 deep × 8.4 (banners 11.0) | bottom centre of the front face |
| `SM_ImpStand_Curve` *(extra)* | 180 | — | 4.0 chord × 7.8 deep × 8.4 | bottom centre of the front face (the chord) |
| `SM_Carceres` | 332 | — (≈ 2 stand sections) | 4.0 × 1.15 × 8.4 (banner 11.0) | bottom centre of the front face |
| `SM_CarceresOpen` *(R1 follow-up, Q7)* | 344 | — | 4.0 × 6.1 × 8.4 (banner 11.0) | bottom centre of the front face |
| `SM_ImpTower` *(extra)* | 116 | `SM_GrandTower` 116 | 8.6 × 8.6 × 15.6 | base centre |
| `SM_Pulvinar` | 496 | `SM_PatronBox` 420 | 15.6 × 7.2 × 15.6 | bottom centre of the front face |
| `SM_ImpGate` | 616 | `SM_GrandGate` 468 | 11.8 × 3.5 (+0.85 proud) × 13.0 | bottom centre of the front face |
| `SM_TriumphalGate` | 1,236 | `SM_GrandGate` 468 (one instance) | 21.8 × 4.5 (+0.91 proud) × 21.2 | bottom centre of the front face |
| `SM_Spina_10m` | 144 | `SM_GrandTerrace` 108 | 10.0 × 6.16 × 1.2 | base centre |
| `SM_SpinaEnd` | 372 | `SM_GrandDais` 272 | 6.16 × 3.08 × 7.9 | the meta's centre (the spina's end), floor level |
| `SM_SpinaStairs` | 216 | `SM_GrandStairs` 192 | 3.5 × 2.0 × 1.2 (posts 1.5) | low front edge, centre |
| `SM_ImpTerrace` *(R1 follow-up, Q2)* | 144 | `SM_GrandTerrace` 108 | 12.16 × 5.16 × 1.2 | base centre |
| `SM_Obelisk` | 96 | `PR_PatronStatue` 236 | 2.9 × 2.9 × 12.0 | base centre |
| `PR_LapCounter` | 268 | — | 6.4 × 0.9 × 4.5 | base centre |

**The whole arena** in the preview layout: 150 pieces, **43,434 tris** (CLAUDE.md §8's static budget is about 100k).

### Hierarchies (checked by re-importing the FBX in Blender)
- `SM_ImpGate` and `SM_TriumphalGate`: root → `<name>_Frame`, `DoorL`, `DoorR`, the same as `SM_GrandGate`. The
  hinges are at **(±2.5, 0.25 m behind the front face, 0)**: `DoorL` (−2.5, 0.25, 0) and `DoorR` (2.5, 0.25, 0) in
  Blender, identical to `SM_GrandGate`'s values. So `ArenaGates` (100° swing into the arena) and
  `ArenaBuilder.PlaceGate` (the 4.4 m `GateBlocker`) work unchanged. The frames' origins are at the root.
- `PR_LapCounter`: root → `PR_LapCounter_Frame`, `Egg0`…`Egg6`. Each egg's pivot is at its bottom, so code can hide,
  drop or tip one per lap. `Egg0` is at local **x = −2.7** (west when yaw = 0) and `Egg6` at +2.7, spaced 0.9 m, with
  their bottoms at local y = 3.8. Eggs are 0.52 m wide and 0.73 m tall.
- Every other piece is a single mesh.

## Dimensions the layout code must use
- **Floor** covers the wall line + 1.5 m: x from −96.5 to 96.5, z ±45.5, and the east half circle r = 45.5 round
  (51, 0). Its height is jittered ±1.2 cm. Floor area 16,674 m² (exactly the analytic area of that shape).
- **Track lane:** the floor faces between 12 and 16 m from the spina's axis (the segment (−60, 0)–(40, 0)). That's
  straights at |z| 12–16 for x from −60 to 40 and half rings round (−60, 0) and (40, 0). The edges are exact and
  welded (constrained Delaunay, half circles in ~1.9 m chords). Two wheel ruts (0.3 m wide) lie at 12.85–13.15 and
  14.85–15.15 m.
- **Run-in and run-out (Q1):** two strips at |z| 12–16 (ruts at the same |z|) from the floor's west edge
  **x = −96.5** (under the carceres' facade, so they run into the open bays) to **x = −60**, where they meet the
  straights. West of x = −60 they overlap the half rings, so the ruts fork there like real tracks. Measured over the
  whole track (loop + strips): faces' area **1,386.5 m²** against **1,385.8 m²** analytic (loop 1,151.9 + strips 292
  − the two overlaps), and **0 track vertices** outside the 12–16 m bands.
- **Stands** (`SM_ImpStand_4m`): the Grand Arena's profile exactly: podium 3.2, cap 0.6, 8 tiers rising 0.45 and
  running 0.8, back wall top at 8.4 and 0.8 thick, so the stand is **7.8 m deep** behind the wall line. The pilaster
  is 0.25 m proud. Sections are 4 m wide; scale X to fit a run (`m = round(len / 4)`), as `GrandArenaLayout.Stands`
  does. `GrandArenaLayout.Seats` crowd zones fit unchanged.
- **Curved stands** (`SM_ImpStand_Curve`, for the sphendone, r = 44): the front face is a 4 m chord, and the sides
  run toward the curve's centre (each section subtends 5.21°), so sections tile round the curve with no gaps at the
  back. Place each section at its chord's midpoint (4.5 cm inside the arc), yaw toward (51, 0), scale X = chord / 4.
  Scaling keeps the sides radial for any chord, so the same `m = round(arc / 4)` logic works. Crowd zones: the same
  offsets, but lengths scale with (44 + offset) / 44.
- **Side gate** (`SM_ImpGate`): **11 m** wide (GateHalf **5.5**, cut the stand run ±5.5 round its centre), opening
  5.0 × 7.0 m (doors 7 m), filled half-round lunette to 9.5 m, 13 m tall, 3.0 m deep (dark plate at 3.5).
- **Triumphal gate** (`SM_TriumphalGate`): **21 m** wide (half **10.5**), opening 5.0 × 8.0 m (doors 8 m), 4.0 m deep
  (dark plate at 4.5), 21.2 m to the eagle's wing tips. On the sphendone the curve at z = ±10.5 is at
  x = 51 + √(44² − 10.5²) = **93.73**, which is 1.27 m in front of the apex (95, 0). The layout can either:
  - **(recommended)** flatten the wall line's apex into the chord from (93.73, −10.5) to (93.73, 10.5) and stand the
    gate on it at (93.73, 0), yaw −90. The curved stand runs then go from ±90° to ±13.81° (15 sections a side,
    3.90 m chords, scale 0.975); or
  - stand it at (95, 0) and accept a 1.27 m recess at its ends, where the stands' end caps show.
- **Carceres** (`SM_Carceres`): **4 m** a bay, the front face on the wall line x = −95, yaw 90. The facade is 1.0 m
  thick (cornice to 1.15), so the solid walls belong on the wall line as usual. 88 m = 22 bays (the design's
  layout), bay centres at z = −42 + 4i. The pilaster is centred on the bay's local **+x** edge (Blender
  −X) and overhangs 0.3 m into the neighbour: the last bay at one end has no pilaster on its far side, and the corner
  tower covers that. Stall opening 2.8 m wide, arch apex 4.4 m, bronze stall gates 2.5 m tall, 0.5 m behind the front.
- **Chariot bays (Q7): a variant piece, `SM_CarceresOpen`, for the two bays at z = ±14** (i = 7 and 14); the other
  20 keep their stall gates. It's the same facade with the same arch, so the row's rhythm doesn't change, but with
  no gates, and the arch runs on as a **6 m vaulted tunnel** (world x −95 → −101). The vault is charcoal past the
  facade's first metre, a dark wall closes its end, and the floor is the track's packed earth and ruts (from 0.9 m in,
  3 cm above the arena floor, with the ruts 1 m either side of the bay's centre, i.e. at |z| 13 and 15).
  - **Clearance:** **2.8 m wide up to 3.0 m** (the spring), 4.4 m at the crown. The chariot (2.34 m wide, 2.6 m to the
    plumes) has 0.23 m each side and 0.4 m above its plumes when it drives the centre line.
  - **Where to spawn it:** the chariot is 4.44 m long, so it's wholly inside the tunnel while its centre is between
    x = **−98.78 and −97.22**. The design's `trackStallX` −97 leaves its nose 0.22 m out of the facade, so I
    recommend **−98**.
  - **Colliders:** a Decor piece has none. As Solid, its tunnel floor and walls would get mesh colliders (harmless:
    the invisible stone on the wall line keeps the player out).
- **Corner towers** (`SM_ImpTower`): 8 × 8 m. Put them behind both wall lines at the carceres' corners, at
  **(−99, ±48)**, covering x −103 to −95, |z| 44 to 52.
- **Pulvinar** (`SM_Pulvinar`): **14 m** wide (cut the north stand run ±7 round its centre; palace wall and pediment
  15.6 m wide), 7.2 m deep, loge floor at **4.2 m**. Court crowd zone: centre local **(0, 4.2, −2.1)**, size about
  12 × 2.6. The gold throne is at local (0, 4.2, −4.4), seat top at 4.8: an emperor NPC goes there, or leave it
  empty. Front rail at 4.2–4.9.
- **Spina segment** (`SM_Spina_10m`): **10 m** long (x), 6 m wide (z ±3.0; lip ±3.08, band ±3.03), walkable top at
  **1.2 m** (a 1.6 m purple carpet strip down the axis adds 1.5 cm). Ten segments at x = **−55, −45, …, 35**, z = 0,
  yaw 0. The bands and lip end flush at x = ±5, so segments abut with no overlap.
- **Metae** (`SM_SpinaEnd`): origin at **(−60, 0)** with yaw **−90**, and **(40, 0)** with yaw **90**. The round side
  (+z local) faces the turn, and the flat side sits at local z = 0 against the last segment's end. A half disc r 3.0
  (lip 3.08) with its top at 1.2 (walkable), and a plinth r 2.3 from 1.2 to 1.7 on top. The cones block: three drums
  r **0.6** at local (x, z) **(0, 1.45)** and **(±1.143, 0.893)**, cones to 7.9 m. One circle r 1.75 round local
  (0, 0.9) also covers them.
- **Spina stairs** (`SM_SpinaStairs`): 3.0 m wide (cheeks to ±1.74), **2.0 m** deep, 4 steps of 0.3 × 0.5, rising
  toward local −z. Put the origin at **z = ±5.0** (yaw 0 on the north side, 180 on the south), so the top step meets
  the walkway's side at |z| = 3. The 8 cm the lip overhangs coincides with the top step's top in the same swatch
  (`travertine_d`), so there's no visible z-fight. The gold posts (0.3 m cubes on the cheeks' tops) stand outside the
  3 m path. The preview puts stairs at x = −40, −25, 5 and 30, clear of the obelisk and the lap counter; the design
  decides.
- **Terraces (Q2)** (`SM_ImpTerrace`): origin at the base centre, footprint **12 × 5 m** (lip 12.16 × 5.16), walkable
  top at **1.2 m** (a 1.5 cm purple carpet strip along its length), the spina's look. Walkable, like the spina.
  - **Front:** local **+z** (its long sides run along x) is the stairs' side. Gold panels mark the front and both
    ends; the back is plain, for standing against a wall.
  - **Stairs:** `SM_SpinaStairs` with the terrace's yaw, origin at local **z = +4.5** (2.5 + the stairs' 2.0), anywhere
    within local x ±4.2.
  - **At the design's spots** ((−75, 41.5) and (10, 41.5) with yaw 180; (−80, −41.5) and (0, −41.5) with yaw 0), the
    back face lies exactly on the wall line, flush with the podium (the band and lip overhang 3–8 cm into it, hidden).
    The stairs go at **z = ±37.0** with the same yaw.
- **Obelisk** (`SM_Obelisk`): on the spina top at **(−10, 1.2, 0)**. Footprint 2.9 × 2.9 m (plinth 2.4), leaving
  1.55 m of walkway each side; 12 m tall. Solid, a line-of-sight blocker: an obstacle box of 2.9 × 2.9.
- **Lap counter** (`PR_LapCounter`): on the spina top at **(20, 1.2, 0)**, yaw 0 (its long axis along x). Its base
  slab is 6.4 × 0.9 × 0.25, leaving 2.55 m of walkway each side; the columns are at local x ±2.7.

## Palette swatches
- **A6:** `travertine` (stone), `travertine_d` (caps, soffits, cornices, alternate treads), `purple` (podium band,
  parapet, spina band and carpet, lunette frames, obelisk), `purple_l` (banners), `purple_d` (panels, valance, drape,
  side-gate lunette).
- **A5:** `gold_d` (trims, capitals, posts), `gold` (rosettes, eagles, metae cones, eggs, pyramidion), `gold_l` (cone
  finials, triumphal door bosses), `crimson` (triumphal frieze, columns, banners), `crimson_d` (triumphal doors,
  lunette, plinth).
- **Floor:** `sand_l` (sand), `sand` (5 % of 2 m cells as patches, kept 1.5 m off the lane), `dirt_floor` (the lane),
  `mud` (the ruts).
- **Other:** `bronze_d` (carceres stall bars, side-gate doors, triumphal grilles), `shadow` (dark interiors).
- **No hazard swatches.** Orange-red stays free for the race's telegraph.

## Review log (front / side / 3/4 + 64 px silhouette, `Screenshots/blender/<model>_review.png`)
- `SM_ImpFloor`, 3 iterations:
  - v1: a `sandstone` lane. It leaned orange, the hazard colour.
  - v2: `sand_d`. It faded at grazing angles.
  - v3 (shipped): `dirt_floor` with `mud` ruts, darker at every angle and neutral. Compare
    `imperial_overview_{top,close}_v1/_v2.png` with the final `imperial_overview_{top,close}.png`.
- `SM_TriumphalGate`, 2 iterations: v1's 5 m eagle vanished at 64 px; v2's 7.8 m eagle crowns the silhouette.
- `SM_Spina_10m`, 2 iterations: v1's `purple_d` carpet was too heavy for a walkable top, so v2 uses `purple`.
- R1 follow-up (Q1, Q2, Q7):
  - `SM_ImpFloor` gained the run-in and run-out strips. That's a content addition, not a review iteration: the lane's
    look is v3's.
  - `SM_CarceresOpen` and `SM_ImpTerrace` passed on iteration 1. At 64 px the open bay reads as the closed bay's
    block with a deep box behind it (the tunnel is hidden from the arena), and the terrace as a flat slab.
- Every other piece passed on iteration 1. At 64 px:
  - the metae read as three spikes, the obelisk as a needle, the lap counter as a frame with seven bumps;
  - the stands, carceres and pulvinar read as building blocks with a banner or pediment;
  - the gates read as blocks, and the triumphal one is crowned.
- **Overview renders** (`Screenshots/blender/`). These show the preview layout, which is a stand-in, not
  `ImperialArenaLayout`:
  - `imperial_overview_aerial.png`: the whole hippodrome from the south-west;
  - `imperial_overview_close.png`: player height, looking at the spina, obelisk, stairs, north stands, a side gate
    and the pulvinar;
  - `imperial_overview_east.png`: the triumphal gate across the east meta;
  - `imperial_overview_top.png`: an orthographic top view with north up, showing the lane loop, the run-in and
    run-out, the two tunnels behind the carceres and the four terraces;
  - `imperial_overview_west.png`: the carceres from the arena, with the two open bays and the strips running in;
  - `imperial_overview_field.png`: the north-west terrace with its stairs, the carceres and the corner tower.

## Decisions (one line each)
- **The floor is a constrained Delaunay triangulation** of a 2 m lattice plus the lane's edge loops: a clipped 2 m
  grid (the Grand floor's method) would stair-step the lane's half circles.
- **The lane is `dirt_floor` with `mud` ruts:** "visibly darker" without using orange, so the orange-red race
  telegraph stands out on it.
- **I added `SM_ImpStand_Curve`:** straight 4 m sections on the r 44 curve leave 0.6–0.7 m slits between the top tiers;
  a radial-sided section tiles exactly and still scales in X.
- **I added `SM_ImpTower`:** it closes the carceres' corners, the Grand towers' job, in A6 so `SM_GrandTower`'s red
  band doesn't leak in.
- **Both gates keep a 5 m opening:** `GateBlocker` (4.4 m) and the door swing work unchanged. The triumphal gate is
  grander by height (8 m doors, 21 m with the eagle) and by width (a 21 m triple arch), not by a wider opening.
- **The lap counter's eggs are separate children:** the race code can count laps without a new model.
- **The spina's bands and lip run exactly 10 m:** abutting segments have no coplanar overlaps, and the metae base
  continues the same bands round a half disc.
- **The obelisk is purple (porphyry):** imperial, and a strong dark silhouette for a line-of-sight blocker.
- **The triumphal gate's side arches are blind** (dark panels with gold archivolts and bronze grilles): no holes into
  the void behind, and fewer triangles.
- **The chariot bays are a variant piece, not every bay open:** 20 gated stalls still read as "starting stalls",
  and the two dark tunnels on the track's line show where the chariots come from.
- **The open bay keeps the 2.8 m arch:** it already clears the chariot (2.8 m up to 3.0 m against 2.34 × 2.6), and an
  identical arch keeps the row uniform.
- **The door leaves clear the frame at 100°.** I checked the geometry against `ArenaGates.doorOpenAngle`: the
  triumphal gate's inner columns sit 0.85 m off the jamb and their bases are 1.0 m wide.

## Verified / unverified
- **Verified (in Blender):**
  - all 13 first-delivery FBX exported and re-imported (sizes, door hinges and frame origins as listed), and the 2
    follow-up FBX exported with the bounds listed;
  - triangle counts, with the Grand Arena's FBX measured the same way;
  - the track's placement and area, run-in and run-out included (numbers above);
  - 3-angle and 64 px reviews for every piece;
  - the whole-arena preview (curved stands tile, the spina's parts align, the gates cut the runs).
- **Unverified:**
  - Nothing here has been in Unity: import scale and facing (the kit's convention, which `ModelPipelineTests` covers
    for other models), batching, the look under URP lighting and fog (workbench renders are darker than the game),
    and the doors actually swinging in `ArenaGates`.
  - The follow-up pieces (`SM_CarceresOpen`, `SM_ImpTerrace`, the new floor) haven't been in Unity, nor has a chariot
    driving through the open arch.
  - The preview now follows the design for the side gates (x = −50 and 30), the 22 bays and the terraces; its spina
    stairs (x = −40, −25, 5, 30) are still my guesses.

## Needs from others
- **layout and setup (wave 2):**
  - use the dimensions above;
  - choose where the triumphal gate stands (I recommend the 93.73 m chord);
  - use `SM_ImpStand_Curve` round the sphendone;
  - place `SM_ImpTower` at the carceres' corners;
  - set `gates[i].model` to `Environment/SM_TriumphalGate` for the boss gate and `Environment/SM_ImpGate` for the
    others (or as `gateModel`).
- **design:**
  - Q6 is answered: the eggs are **`Egg0`…`Egg6`** (not `Egg1`–`Egg7`).
  - Q8 is answered by `SM_ImpStand_Curve`, the wedge piece you called `SM_ImpStand_Arc`.
  - `trackStallX`: −98 (see the chariot bays above).
- **orchestrator:** after import, check the doors swing clear in Play mode, and check that the lane reads as darker
  under the game's light.
- **If the chariot code wants a lane-shaped mesh for its 3 s telegraph** (`TelegraphRenderer` has straight lanes and
  circles, not half rings), I can export the 4 m loop alone as a thin overlay piece on request.
