# R2 env-art — the Colosseum's environment models

**Source:** `BlenderSrc/environment/colosseum.py` (headless, through `Tools/bheadless`). Rebuild one part:

```
Tools/bheadless BlenderSrc/environment/colosseum.py part=floor|hypogeum|cavea|gates|props [review=<model>|<m1>,<m2>|all]
Tools/bheadless BlenderSrc/environment/colosseum.py part=overview [tag=_vN]  # the whole arena: renders + total tris
Tools/bheadless BlenderSrc/environment/colosseum.py part=check               # re-imports every FBX: hierarchy, sizes
Tools/bheadless BlenderSrc/environment/colosseum.py part=doors               # swings both gates' doors 0-100 deg
```

Each part takes 1 to 4 s. Outputs:
- 17 FBX in `Assets/Art/Models/Environment/`, without `.meta` files (Unity makes them on import);
- `BlenderSrc/blend/colosseum_{floor,hypogeum,cavea,gates,props}.blend`;
- reviews in `Screenshots/blender/`, which is gitignored: read them in this worktree.

`part=floor` also reads the floor's hole edges back from the built mesh and compares them with the contract's tables
(see the floor below).

## Conventions (the same as `imperial_arena.py`)
- 1 Blender unit = 1 m. Export through `K.export_fbx`: the model's front (Blender −Y) arrives as Unity **+Z**.
- Below, "local" means **Unity-local**: x right, y up, +z toward the arena.
- **World-space pieces:** the floor and the six stand runs are built in Unity world coordinates. Place them at the
  origin with yaw 0.
- **Local pieces:** every other piece stands at the contract's position and yaw. Its origin is at the bottom centre of
  its front face, or at its base centre.

## The pieces

| Model | Tris (budget) | Size (x × z × height, m) | Origin |
|---|---|---|---|
| `SM_ColFloor` | 8,508 (12,000) | 199 × 137 (the ellipse 99.5 × 68.5) | world origin |
| `SM_ColCavea_0` / `_2` / `_3` / `_5` | 2,703 each (6,000) | the end runs | world origin |
| `SM_ColCavea_1` / `_4` | 2,731 each (6,000) | the box runs | world origin |
| `SM_ColGate` | 996 (1,000) | 12.0 × 19.4 deep × 21.9 (masts 28.2) | bottom centre of the front face |
| `SM_ColGreatGate` | 1,035 (1,600) | 22.0 × 19.4 deep × 24.0 (attic 25.45, wreath 27.7) | bottom centre of the front face |
| `SM_ColPulvinar` | 452 (600) | 12.82 × 3.4 (the loge) × 4.03 floor, 9.0 canopy, 11.85 finial | bottom centre of the podium face |
| `SM_ColStage` | 872 (900) | r 10 × 1.2 | base centre |
| `SM_ColStairs` | 112 (220) | 3.0 (cheeks ±1.74) × 2.0 × 1.2 | low front edge, centre |
| `SM_ColTrapdoor` | 176 (220) | 8 × 4 hole; its shaft 8 × 5 × 3 deep | top centre |
| `SM_ColLift` | 182 (300) | 4 × 4; posts to 1.1; shaft 3 deep | top centre |
| `SM_ColMons_A` | 386 (700) | 14 × 10 × 2.2 (rock-face boulders to 2.8) | base centre |
| `SM_ColMons_B` | 226 (700) | 14 × 10 × 2.2 (column stumps to 3.14) | base centre |
| `PR_VictorStatue` | 334 (350) | 2.0 × 2.0 (octagonal plinth) × 5.55 | base centre |

**The whole arena** in the preview layout: **67 pieces, 42,538 tris**. CLAUDE.md §8's static budget is about 100k; the
contract estimated ~72k. Per model (count × tris):

| Model | Count × tris |
|---|---|
| floor | 1 × 8,508 |
| stand runs | 4 × 2,703 + 2 × 2,731 |
| side gates | 4 × 996 |
| great gates | 2 × 1,035 |
| boxes | 2 × 452 |
| stage | 1 × 872 |
| stairs | 4 × 112 |
| statues | 4 × 334 |
| hills A | 2 × 386 |
| hills B | 2 × 226 |
| trapdoors | 30 × 176 |
| lifts | 9 × 182 (the eight, plus the Victor's at ×1.25) |

### Hierarchies (checked by re-importing the FBX, `part=check`)
Every FBX root node carries the export's 180° yaw, like every other model. Children's positions below are in Blender
coordinates in that root's frame, as the re-import reports them.

- **`SM_ColGate`, `SM_ColGreatGate`:** root → `<name>_Frame`, `DoorL`, `DoorR`.
  - `DoorL` sits at (−2.5, 0.25, 0) and `DoorR` at (2.5, 0.25, 0), **identical to `SM_ImpGate`'s** (re-imported side by
    side). `ArenaGates`' 100° swing and `ArenaBuilder`'s 4.4 m `GateBlocker` work unchanged.
  - The frames' origins are at the root.
  - Both gates are built in one Blender scene, so the script renames the doors before export (`clean_names`). A first
    export had carried `DoorL.001`, which `ArenaGates.FindDeep` would never have found.
- **`SM_ColTrapdoor`:** root → `SM_ColTrapdoor_Frame` (origin at the root), `LeafF` and `LeafB`.
  - `LeafF` is at Blender (0, −2, 0), which is local **(0, 0, +2)** in the model's frame. It covers local z 0.005 → 1.88.
  - `LeafB` is at Blender (0, 2, 0), local **(0, 0, −2)**. It covers z −0.005 → −1.88.
  - Each pivot is on the hole's long edge, on the hinge line.
- **`SM_ColLift`:** root → `SM_ColLift_Frame` (origin at the root) and `Platform`, whose pivot is the deck's top centre,
  (0, 0, 0) at rest.
- Every other piece is a single mesh.

## Dimensions the layout code must use

### The floor (`SM_ColFloor`)
- **Placement:** at the origin, yaw 0, Walkable. It's the 96-gon of the ellipse 99.5 × 68.5, sampled every 3.75°.
- **Holes:** exactly the contract's 39: the 30 trapdoors (8 × 4 at (x, z, yaw)), the 8 lifts (4 × 4, yaw 0) and the
  5 × 5 at (0, 0).
  - `part=floor` reads the built mesh's boundary loops back and matches each one: **39 of 39 found, worst error 0.0000
    m in centre, 0.0° in yaw and 0.0000 m in size**. It prints every hole as built.
- **Plank border:** a 0.4 m border of `wood_dark` round every hole, at exactly y 0.
- **Height:** jitter is ±1.2 cm, with none within 0.5 m of a hole.
- **Area:** 20,284.6 m², which is the 96-gon's area less the holes.
- **Faint plank lines** (`dirt_floor`, 0.3 m wide):
  - along both edges of the hypogeum's corridors, 2.2 m either side of the corridor's centre line;
  - on the ring, straight from door to door across every 5.74 m bridge, starting 0.05 m off each door's plank border;
  - on the axis, at z = ±2.2 from x = ±9.9 (under the stage's edge) to the first door, between the doors, and out to
    x ≈ ±59.9, where they meet the plank borders of the ring doors either side of the axis.
  - With the doors shut they draw the corridors' pattern: a ring and a cross-axis.

### The trapdoors (`SM_ColTrapdoor`, 30)
- **Placement:** at (x, 0, z) with the contract's yaw.
- **The leaves:**
  - Tops are flush at y 0, 0.1 m thick.
  - Each leaf has four plank rows along X (alternating `wood_dark` / `wood`), three iron straps across it (1 cm proud)
    and a ring handle (1.5 cm proud).
  - Within 0.12 m of the hole's edge, an `iron_d` border, 0.15 m deep, runs round the leaves.
- **Opening the leaves** (unverified in Unity; derived from the kit's axis conversion):
  - `LeafF.localRotation = Quaternion.Euler(100, 0, 0)`.
  - `LeafB.localRotation = Quaternion.Euler(−100, 0, 0)`.
  - These are in each leaf's own frame, which is the FBX root node's, turned 180° like every model's. Each free edge
    (local z 0) drops into the shaft, hanging 10° past vertical.
  - A PlayMode check that an open leaf's renderer bounds reach below y −1.5 would catch a sign error.
- **The shaft:**
  - `charcoal` walls from y 0 to −3, with an `ink` bottom at −3.
  - The long walls stand at local z = **±2.5**, behind the hole's ±2.0, under the deck's plank border. A leaf hanging at
    100° reaches 0.45 m past its hinge line, and a wall at ±2.0 would cut through it.
  - Three iron rungs sit on one end wall.
  - None of it needs a collider: the hypogeum part's invisible floor covers the 8 × 4 hole.

### The lifts (`SM_ColLift`)
- **Placement:** L0–L7 at (x, 0, z), yaw 0.
- **The Victor's lift:** the same model at (0, 1.2, 0), scaled 1.25. Its 5 × 5 frame fits the stage's hole, and its
  shaft reaches y −2.55.
- **The frame:** an `iron_d` border 0.1 m inside the hole, so the opening is 3.8 m. The shaft has `charcoal` walls at
  ±2.0 with iron corner angles, and an `ink` bottom at −3.
- **The platform:**
  - A 3.8 × 3.8 × 0.2 deck: five planks (`wood` / `wood_dark`), iron sides and iron edge trims.
  - Four bronze posts, 0.16 m square, at (±1.78, ±1.78), 1.1 m tall to their pyramid caps.
  - To lower it: `Platform.localPosition.y` goes from 0 to −3 (−3.75 m in the world for the ×1.25 lift). At the bottom
    the deck's 0.2 m sinks into the shaft's floor, out of sight.

### The stage (`SM_ColStage`)
- **Placement:** at (0, 0, 0), Walkable.
- **The top:** flat at exactly 1.2.
  - A 0.6 m travertine kerb ring.
  - Two rings of 16 flagstones, alternating `travertine` / `travertine_d`.
  - A `stone_grey` laurel ring of 56 leaves round the hole at r ≈ 4.25. The leaves are 5 mm proud: walking won't notice.
- **The side:** `travertine_d` to 0.92, a `bronze` band from 0.92 to 1.08, a travertine lip to the top.
- **No faces** inside the 5 × 5 hole or underneath: the ×1.25 lift's shaft lines the hole. A wall of the stage's own
  there would z-fight with it.

### The stairs (`SM_ColStairs`)
- **Placement:** the contract's four, with origins at r = 12.
- **The steps:** each step's back follows the stage's circle (r 10 round local (0, −12)), so they meet the round side
  with no gap and no coplanar overlap. The top step is the stage's height, 1.2.
- **The cheeks:** `travertine_d` from |x| 1.5 to 1.74, their slopes capped in `bronze`, a bronze block at each foot
  (±1.76).

### The statues (`PR_VictorStatue`)
- **Placement:** at (±9.9, ±9.9), yaw atan2(x, z).
- **The plinth:** **octagonal**, 2.0 m across the flats with a circumradius of 1.08, so the obstacle circle r 1.1 covers
  it. A 2 × 2 square plinth would put its corners 0.31 m outside the circle.
- **The figure:** its right arm reaches r 1.01, inside the circle. The plinth is 1.2 m with a bronze plaque on its front
  (local +z).

### The hills
Placement as the contract, with the rock face on local −z. The chests' and altars' anchors are at each origin's
(x, z), at y 2.2: **that point is on the flat top**.
- **`SM_ColMons_A`:**
  - Top flat at exactly 2.2 over local x ±3, z −5 → +1 (6 × 6 m).
  - The three open sides fall 2.2 m over 4 m: 28.8°, walkable (the slope limit is 50°). See the decisions for why not
    20°.
  - The back (local z −5) is a 2.2 m face of `slate` earth behind `stone_grey` / `slate` boulders. The boulders reach
    local z −5.2 and stand up to 0.6 m over the top's back edge, so the clear top is z −4.0 → +1 (6 × 5 m).
- **`SM_ColMons_B`:**
  - Four terraces of 0.55 m with **45° banks** (0.55 m of run) and 0.45 m treads. The banks are walkable: a vertical
    0.55 m riser would exceed the player's 0.35 m `stepOffset`.
  - The top is flat at 2.2 over local x ±3.45, z −5 → +1.45.
  - The back is a sheer stepped `stone_grey` / `slate` wall.
  - Two broken column stumps sit on the top's back edge at local (−2.0, −4.3) and (2.1, −4.25), 0.5 and 0.8 m tall
    (≤ 1.0 with the break).
  - Rubble lies on the treads; a fallen column in three drums lies at the front-left foot, inside the footprint.

### The gates
**Gate geometry** (both at the contract's positions and yaws):

| | `SM_ColGate` (G1, G2, G4, G5) | `SM_ColGreatGate` (G0, G3) |
|---|---|---|
| **Width** | 12.0 (`GateHalf` **6.0**) | 22.0 (half **11.0**); cornice ±11.4 |
| **Opening** | **5.0** wide, the arch springing at 4.5, crown 7.0 | 5.0 wide, springing 6.5, crown 9.0 |
| **Leaves** | rectangular, to **4.45** | to **6.45** |
| **Tunnel** | 5.0 × 7.0, flat ceiling at 7.0 behind a 0.6 m-deep arch | 5.0 × 9.0, behind a 0.9 m-deep arch |
| **Tunnel floor** | `dirt_floor` at y 0.03, from local z −0.4 (behind the hinge posts) to −12 | the same, to −14 |
| **End of the tunnel** | `ink` wall at local z −12 | `ink` wall at local z −14 |
| **Front face** | rises to 8.6 | rises to 24.0 (block 6 m deep), then 21.9 back to 19.4 |
| **Proud of the face** | columns, bases and imposts, up to 0.36 m in front | bases and cornice, up to 0.6 m in front |

- **Above the leaves:** a fixed lunette, charcoal with bronze bars and a bronze lintel 5 cm over the leaves.
- **The tunnel floor** clears the arena floor's last 1.5 m by 2–4 cm (no z-fighting).
- **Over the side gate's tunnel**, the front rises to a **tribunal**:
  - its floor is at **7.7** (walkway 1's level) from u 0.3 to 7.4, behind a marble balustrade (u 0–0.3, 7.7 → 8.6);
  - from u 7.4 back, maenianum II, wall 2, maenianum III, the colonnade (3 blind arches and shields) and the velarium
    (one mast, three stripes) continue exactly as the runs'.
- **The great gate:** two storeys:
  - engaged columns and blind arches below;
  - pilasters, a bronze-lettered panel and two red banners above;
  - a travertine keystone with a bronze laurel wreath;
  - a raised attic carrying a 2.3 m bronze laurel wreath.
- **Inside the inset band:** all the proud details lie within the 1.1 m between the wall line and the play area. The
  side gates' faces lie 0.15–0.16 m behind the ellipse at their ends, so their columns poke ≤ 0.3 m past the wall line.
- **Door swing**, checked on the FBX by `part=doors` (yaw 0 → 100° into the arena, world-space BVH overlap):
  - The leaves touch nothing except the jamb at the hinge. Past ~70°, their hinge end also crosses the face plane
    within 0.1 m of the hinge.
  - `SM_ImpGate` has the same contacts, and more of them (17 against 10 at 100°).
  - The open leaves reach 2.21 m in front of the face, as `SM_ImpGate`'s do.
- **Set `gates[i].model`** to `Environment/SM_ColGreatGate` for G0 and G3, and `Environment/SM_ColGate` for the rest.

### The stands (`SM_ColCavea_0`–`_5`)
**Placement and ends:**
- At the origin, yaw 0, Decor.
- Run k runs from gate k's local +X front corner to gate k+1's −X front corner. In between, it passes through the wall
  line's samples (every 3.75°, none within 0.5° of an end).
- **The end rings run along their gates' axes, not the ellipse's normals.** Each run's end cap lies flat against its
  gate's side (x = ±6 or ±11 local), with no wedge-shaped gap behind the gate. At the great gates the ellipse's normal
  is 13.7° off the gate's axis, which would open a 4.6 m slot at the top.
- The great gates' corners are exactly the chords' ends, (±96.67, ±11). The side gates' corners are 0.15–0.16 m behind
  the ellipse, so the podium face eases back by up to 0.16 m over the last metres before a side gate.

**The profile, exactly the contract's.** Crowd zones can use these numbers:

| Element | u (outward from the wall line) | y |
|---|---|---|
| Podium face | 0 | footing −0.3 → 0.6, face to 3.6, band 3.6 → 4.0 |
| Balustrade | 0 → 0.3 | 4.0 → 4.9 |
| Senators' terrace | 0.3 → 2.4 | 4.0 |
| Maenianum I, tier k (1–5) | 2.4 + 0.8(k−1) → 2.4 + 0.8k | 4.0 + 0.5k |
| Wall 1 (marble face) | 6.4 | 6.5 → 7.7 |
| Walkway 1 | 6.4 → 7.4 | 7.7 |
| Maenianum II, tier k | 7.4 + 0.8(k−1) → +0.8 | 7.7 + 0.5k |
| Wall 2 | 11.4 | 10.2 → 11.4 |
| Walkway 2 | 11.4 → 12.4 | 11.4 |
| Maenianum III, tier k | 12.4 + 0.8(k−1) → +0.8 | 11.4 + 0.5k |
| Colonnade face | 16.4 | 13.9 → 21.5 |
| Cornice | 16.1 (0.3 proud) | 21.5 → 21.9 |
| Top | to 19.4 | 21.9 |

- **The colonnade is a blind arcade:**
  - a `charcoal` arch every ~4.4 m (19 per end run, 23 per box run), each 2.4 wide from 14.3 to its crown at 19.9;
  - a bronze shield above each arch on the attic;
  - **no gallery floor, so no crowd up there.**
- **The velarium:**
  - strips from (u 11.6, y 23.0) to (u 18.9, y 27.4), half a segment each, alternating `cloth_white` / `red_banner`;
  - a `wood_dark` mast (0.36 m square, to 28.2) at u 18.9 over every third arch;
  - kept two segments clear of the great gates, which rise through the gap.
- **Boxes:** the balustrade stops between the wall line's samples at 86.25° and 93.75° (and 266.25° / 273.75°): local
  x ±6.41 round each box, where the box's own parapet replaces it. Senators' terrace zones should skip those spans.
- **Aisles (scalaria):**
  - 1.2 m wide (±0.6 m along the wall line), centred on the samples at multiples of 7.5°: t = 15, 22.5, 30, 37.5, 45 /
    75, 82.5, 97.5, 105 / 135 … 165 / 195 … 225 / 255, 262.5, 277.5, 285 / 315 … 345.
  - None within 4° of a gate, none behind a box.
  - The aisles' risers are `stone_grey` up all three maeniana. Where an aisle crosses wall 1 and wall 2, the wall face is
    `charcoal` (a vomitorium).
  - Crowd zones may leave the aisles empty (optional; 1.2 m in 6–7 m of tier).
- **Invisible stone:** nothing of the stands, gates or boxes enters the arena below 3.83 m beyond the 1.1 m inset band.
  The highest thing is a velarium mast at 28.2, so a boundary ≥ 30 m is enough.

### The boxes (`SM_ColPulvinar`)
- **Placement:** (0, 67) yaw 180 and (0, −67) yaw 0.
- **Width:** **12.82** (local x ±6.41), the wall line's samples either side of the axis.
- **The loge:**
  - The floor's top is at **4.03**, 3 cm over the terrace it covers (no z-fighting). It runs from local z **+1.0** (the
    overhang into the arena) to **−2.4** (the terrace's end).
  - Its slab's underside is at 3.83 over the arena: the lowest thing there.
- **Parapets and columns:**
  - Parapets on three sides, purple with gold copings, to 5.0.
  - Marble columns at local (±5.96, +0.55) and (±5.96, −2.05) to 9.0, with gold capitals.
- **The canopy:**
  - It spans 9.0 → 9.3 over local z +1.25 → −2.65.
  - A valance and a gold fringe hang to 8.38.
  - A purple roof rises to 10.8, with a gold finial to 11.85.
- **The throne:** at local **(0, 4.03, −0.8)**: the seat top at 4.58, the back to 5.93.
- **The senators** (two marble benches): seat tops at **4.48**, local z −0.75, x ±1.3 → ±5.6. That's 7 a bench at
  x = ±(1.6 + 0.6k), k = 0…6.
- **The Emperor (or the editor):** on the throne.

## Palette swatches
| Group | Swatches |
|---|---|
| Stone | `travertine_d` (mass, risers, caps), `travertine` (treads, lit faces, the stage top), `stone_grey` (footings, aisle risers, the laurel ring, rock), `slate` (darker rock and the ruin's back), `marble` / `marble_shadow` (the balustrade, the walls between maeniana, the podium band, the boxes' columns) |
| Dark | `charcoal` (arch voids, tunnels, shafts, vomitoria, the lunettes), `ink` (shaft bottoms, tunnel ends) |
| Metal | `bronze_d` / `bronze` / `bronze_l` (statues, the stage rim, studs, shields, the great gate's wreath and panel, lift posts); `iron_d` (trapdoor borders, straps, rungs, lift frames, door bands) |
| Cloth | `cloth_white` + `red_banner` (the velarium, the great gate's banners) |
| Floor | `sand_d` with 4 % `sand` and 1.5 % `dirt_floor` speckle; `dirt_floor` plank lines and tunnel floors; `wood_dark` / `wood` (plank borders, leaves, lift decks, masts) |
| The boxes only | `purple`, `purple_d`, `gold`, `gold_d` |

**No orange-red anywhere.** `slate` isn't in the contract's list: it's a neutral dark grey, used for contrast in the
rock faces.

## Decisions (one line each)
- **The floor:** a constrained Delaunay triangulation of a 2.5 m lattice plus every hole, border and line edge. The
  holes are exact and welded, and it stays at 8.5k tris; a 2 m lattice would pass 12k.
- **The plank lines run along the corridors' edges (±2.2 m), not their centre lines:** that's the corridor pattern
  under the doors, and it meets each door's border.
- **Each trapdoor shaft is 5 m across under a 4 m hole:** a leaf hinged on the hole's edge and swung 100° hangs 0.45 m
  past that edge.
- **The leaves' plank rows are separate boxes:** a single plate with a planked top would need ngon ends with collinear
  vertices, which triangulate into slivers.
- **The stands' end rings follow the gates' axes**, so every end cap lies flat against a gate. The alternative is
  wedge gaps up to 4.6 m behind the great gates.
- **The side gate carries maenianum II and III over its tunnel; maenianum I gives way to a tribunal at 7.7:** a 7 m
  arch can't pass under maenianum I's tiers (4.5–6.5 m).
- **Rectangular leaves to the springing under a fixed lunette (as `SM_ImpGate`), not arched leaves:** arched leaves
  rise into the arch near the hinge and swept through the archivolt and the imposts when they opened. `part=doors`
  found it.
- **The great gate's block is the bowl's full depth, 24 m tall for its first 6 m and 21.9 behind:** its sides hide the
  runs' end caps, and the step gives it a crown seen from the arena. A wreath on a raised attic tops it at 27.7, a
  little over the contract's "~26": it has to read over the velarium masts (28.2).
- **Aisles with vomitoria and marble walls between maeniana (review iteration 2):** from the floor, v1's bowl read as
  one flat wall.
- **The box's loge floor is at 4.03, not 4.0:** it covers the terrace without z-fighting.
- **The hills:**
  - A 6 m-wide top inside a 14 m footprint can't have the contract's ≤ 20° sides: 2.2 / tan 20° = 6.04 m of run a side.
  - The contract's own anchors at the origin need the top to reach local z ≥ +1.
  - I kept the footprint (other parts' clearances use it), the top (≥ 6 × 4) and the anchors, and let the slopes be
    28.8° on all three open sides.
- **Hill B's terraces have 45° banks**, so they're walkable without jumping.
- **The statue's plinth is octagonal**, so the contract's obstacle circle r 1.1 covers it.

## Review log (front / side / 3/4 + 64 px silhouette, `Screenshots/blender/<model>_review.png`)
| Model | Iterations | What happened |
|---|---|---|
| `SM_ColCavea_0`–`_5` | 2 | v1 read as a flat wall from the floor. v2 added marble walls between the maeniana, aisles and vomitoria: compare `colosseum_overview_close_v1.png` / `_v2.png` and `…_sidegate_v1` / `_v2`. |
| `SM_ColFloor` | 2 | v1's 7 % sand / 3 % dirt speckle read as confetti up close; v2 has 4 % / 1.5 %. |
| `SM_ColStage` | 2 | v1's laurel ring (`travertine_d`) was invisible; v2's is `stone_grey`. |
| `SM_ColGreatGate` | 2 | v1 was a plain 24 m cube at 64 px. v2 steps down behind a 6 m front and has a raised attic. Seen 3/4 at 64 px it is still a block, because it must be the bowl's depth; in place its sides are buried in the bowl and it reads front-on (`colosseum_overview_deathgate.png`). |
| `SM_ColMons_B` | 2 | v1's terraces were too low-contrast; v2 alternates the banks' stone and adds a fallen column. |
| `SM_ColGate` | 2 | Budget only: v1 was 1,007 tris, v2 is 996. |

Not look iterations:
- the doors' rework on both gates (a clearance fix, after `part=doors`);
- the box's front band (kept above 3.83);
- hill A's boulders (kept inside the footprint);
- the statue's root, which had kept a 22.5° rotation from its first part.

**Passed on iteration 1:** `SM_ColTrapdoor`, `SM_ColLift`, `SM_ColStairs`, `SM_ColPulvinar`, `SM_ColMons_A`,
`PR_VictorStatue`.
- At 64 px the statue reads as a crested figure with a crescent over its head, and the box as a canopy on posts.
- The trapdoor and lift silhouettes are their underground shafts. They're judged in place instead: dark plank
  rectangles in the sand, the open ones black pits with the leaves hanging (`colosseum_overview_open.png`).

**Overview renders** (`Screenshots/blender/colosseum_overview_*.png`). These show the preview layout, a stand-in for
`ColosseumLayout`, with group 4 (axis W) open and lifts L1 and L2 down:

| Render | What it shows |
|---|---|
| `aerial` | the whole oval from the south-west |
| `close` | player height at the spawn, looking north: the stage, the statues, the box |
| `deathgate` | G0 across the east floor |
| `sidegate` | G1 and hill A |
| `open` | the open axis doors and a lowered lift, up close |
| `north` | the Emperor's box |
| `top` | an orthographic top view, north up |

## Verified / unverified
**Verified (in Blender):**
- All 17 FBX exported and re-imported, with the hierarchies, pivots and sizes listed.
- Triangle counts.
- The floor's 39 holes measured on the mesh against the contract.
- Door clearance at 0 / 50 / 100° against the frames.
- 3-angle and 64 px reviews for every piece.
- The whole-arena preview: the runs meet every gate, the boxes sit on the podium, everything at the contract's
  coordinates.

**Unverified:**
- Nothing here has been in Unity: import scale and facing (the kit's convention, covered for other models by
  `ModelPipelineTests`), batching, the look under URP light and fog, and the doors actually swinging in `ArenaGates`.
- The leaves' opening signs (derived above).
- The lift platform moving.
- Workbench renders are flatter than the game.
- The preview places pieces from the contract's numbers; it isn't `ColosseumLayout`.

## Needs from others
- **Layout and setup (wave 2):**
  - use the dimensions above;
  - `gates[i].model` as above;
  - the Victor's lift at (0, 1.2, 0) ×1.25;
  - the trapdoors, lifts, stage, stairs and statues as Decor or Walkable as the hypogeum part decides;
  - crowd zones from the profile table, skipping the boxes' spans and the colonnade (aisles optional);
  - the gates' tribunal (7.7, 12 m wide, u 0.3–7.4) carries seats;
  - the boxes' senators on the benches.
- **Hypogeum:** the leaves' rotation signs (above). The shafts need no colliders.
- **Design / orchestrator**, contract changes made as defaults:
  - the hills' slopes are 28.8°, not ≤ 20° (impossible within 14 m with a 6 m top);
  - hill B's banks are 45°;
  - the side gate has a tribunal instead of maenianum I;
  - the gates' leaves are rectangular to the springing, under a fixed lunette;
  - the great gate's wreath reaches 27.7;
  - the statue's plinth is octagonal;
  - the loge floor is at 4.03.
- **Orchestrator:**
  - after import, check the doors swing clear and the leaves open downward in Play mode;
  - with the 32° late sun, the 22 m bowl throws ~35 m shadows, so check the shadow distance and that the velarium
    doesn't darken maenianum III too much.
