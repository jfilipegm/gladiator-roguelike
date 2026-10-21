# R2 contract — Arena 6, the Colosseum + the Eternal Victor (FINAL, orchestrator, 2026-10-21 13:55)

**Item branch:** `phase5-colosseum`, cut from `main` once R1 (v0.17.0) is merged. Worker branches merge into it; the
orchestrator does every merge. **Release:** v0.18.0. **Source:** CLAUDE.md §6.2 row 6 ("I have become a legend":
hypogeum trapdoors, huge arches, multi-tier stands, huge waves, multiple factions, beasts, supernatural enemies, floor
events, the loudest crowd, a big entrance; the boss is the arena's first champion and the Colosseum transforms across
his phases), the GDD ladder (≥ 17,500 m², bigger than Arena 5), `docs/ARENAS.md` §6, `docs/BOSSES.md` "The Eternal
Victor", and the reasons in `docs/tracks/R2-design.md`.

## Shared facts (every part uses these numbers and names)
Metres, y up, X east, Z north. Unity yaw about +Y: a piece's local **+Z** points to (sin yaw, cos yaw) and its local
**+X** to (cos yaw, −sin yaw). Blender → Unity as every existing model (`K.export_fbx`: Blender −Y arrives as +Z).

### The oval
- **Wall line:** the ellipse x = 98 cos t, z = 67 sin t (196 × 134 m), sampled every **3.75°** (96 points), except at the
  two great gates, where the 22 m span is replaced by its **chord at x = ±96.67** (z from −11 to 11; t = ±9.45° round
  0° and 180°). Wall-line polygon 20,576 m².
- **Play area:** the wall line inset **1.1 m** (`GrandArenaLayout.Inset`): **20,005 m²**, 1.31× the Imperial Arena's
  15,315. Convex; the radial edge table works from (0, 0).
- **Floor level** y = 0 everywhere except the stage (1.2) and the hills (up to 2.2).

### The floor: the deck and its holes
`SM_ColFloor` is one Walkable mesh in world coordinates (origin, yaw 0): the ellipse + 1.5 m (semi-axes 99.5 × 68.5),
with **holes**: every trapdoor's 8 × 4 m rectangle (centre, yaw below), every lift's 4 × 4 m square (yaw 0), and a
5 × 5 m square at (0, 0) under the stage's lift. Nothing else has a hole. The invisible floor over each hole is a box
collider the builder adds (the hypogeum part), so the player never falls in.

### The trapdoors (30): (x, z, yaw, group)
Each is 8 m along its local X (the corridor below), 4 m along its local Z. Groups: 0 ring NE, 1 ring NW, 2 ring SW,
3 ring SE, 4 axis W, 5 axis E. The ring doors sit on the ellipse 62 × 42 (their centre line), spaced by arc length
with **5.74 m of deck** between neighbours (a bridge, also at both ends of each quarter).

| Group | Doors (x, z, yaw) |
|---|---|
| 0 ring NE | (61.18, 6.80, 76.4), (55.23, 19.08, 53.0), (45.40, 28.61, 36.1), (33.46, 35.36, 23.5), (20.43, 39.65, 13.3), (6.86, 41.74, 4.3) |
| 1 ring NW | (−6.86, 41.74, −4.3), (−20.43, 39.65, −13.3), (−33.46, 35.36, −23.5), (−45.40, 28.61, −36.1), (−55.23, 19.08, −53.0), (−61.18, 6.80, −76.4) |
| 2 ring SW | (−61.18, −6.80, 76.4), (−55.23, −19.08, 53.0), (−45.40, −28.61, 36.1), (−33.46, −35.36, 23.5), (−20.43, −39.65, 13.3), (−6.86, −41.74, 4.3) |
| 3 ring SE | (6.86, −41.74, −4.3), (20.43, −39.65, −13.3), (33.46, −35.36, −23.5), (45.40, −28.61, −36.1), (55.23, −19.08, −53.0), (61.18, −6.80, −76.4) |
| 4 axis W | (−20, 0, 0), (−32, 0, 0), (−44, 0, 0) |
| 5 axis E | (20, 0, 0), (32, 0, 0), (44, 0, 0) |

The closest two doors are 4.0 m apart (along the axis); doors cover 960 m² (4.8 % of the floor).

### The lifts (8 + the Victor's)
- **L0–L7**, 4 × 4 m, yaw 0, top at y = 0: (34, 20), (−34, 20), (−34, −20), (34, −20), (80, 24), (−80, 24), (−80, −24),
  (80, −24). They are also the boss's **guard spots** (`ArenaLayoutDef.guardSpots`).
- **The Victor's lift:** the same `SM_ColLift` at **(0, 1.2, 0)**, scaled **1.25** (5 × 5 m), in the stage's hole. Its
  shaft reaches 3.75 m down (to y −2.55), through the floor's 5 × 5 hole.

### The stage, stairs, statues, hills
- **Stage:** `SM_ColStage` at (0, 0, 0), yaw 0, Walkable: round, r **10**, top **1.2**, with the 5 × 5 m hole in its
  centre.
- **Stairs:** `SM_ColStairs` (3.0 m wide, 2.0 m deep, 4 steps, rising toward local −z, origin at the low front edge's
  centre, like `SM_SpinaStairs`) at (0, 12) yaw 0, (12, 0) yaw 90, (0, −12) yaw 180, (−12, 0) yaw −90.
- **Statues:** `PR_VictorStatue` (Solid, footprint circle r 1.1) at (9.9, 9.9), (−9.9, 9.9), (−9.9, −9.9), (9.9, −9.9),
  facing outward (yaw = atan2(x, z)).
- **Hills:** 14 m along local X, 10 m along local Z, top **2.2**, origin at the base centre, the rock face on local −z,
  slopes ≤ 20° on the other three sides, a flat top of at least 6 × 4 m. `SM_ColMons_A` at (28, 50.5) yaw 180 and
  (28, −50.5) yaw 0; `SM_ColMons_B` at (−28, 50.5) yaw 180 and (−28, −50.5) yaw 0 (the rock faces toward the near wall).

### Gates (6), counter-clockwise from the east
| Gate | Position | Yaw | Model |
|---|---|---|---|
| G0 the Gate of Death | (96.67, 0) | −90 | `SM_ColGreatGate` |
| G1 | (49.00, 58.02) | −158.5 | `SM_ColGate` |
| G2 | (−49.00, 58.02) | 158.5 | `SM_ColGate` |
| G3 the Gate of Life | (−96.67, 0) | 90 | `SM_ColGreatGate` |
| G4 | (−49.00, −58.02) | 21.5 | `SM_ColGate` |
| G5 | (49.00, −58.02) | −21.5 | `SM_ColGate` |

Each stands with its front face on the wall line (the great gates on their chords). `bossGate` 0 (the boss rises from
the stage; the gate is the fallback).

### The stands (the cavea)
- **Six runs** between the gates, each one Decor mesh in world coordinates (origin, yaw 0), `SM_ColCavea_<k>` from gate
  k to gate k+1 counter-clockwise: run 0 t = 9.45° → 56.23°, run 1 63.77° → 116.23°, run 2 123.77° → 170.55°, run 3
  189.45° → 236.23°, run 4 243.77° → 296.23°, run 5 303.77° → 350.55° (the side gates' 12 m = ±3.77° round t = 60, 120,
  240, 300°). End caps at the gaps. The runs pass behind the two boxes without a gap.
- **Profile**, distance u outward from the wall line and height y (the crowd zones use these numbers):
  - **Podium:** face at u = 0 up to y = **4.0**; a marble balustrade at u 0–0.3 up to 4.9; the **senators' terrace**
    u 0.3–2.4 at y 4.0.
  - **Maenianum I:** 5 tiers, tier k (1–5) from u = 2.4 + 0.8(k−1), top y = 4.0 + 0.5k (to 6.5 at u 6.4).
  - **Wall 1:** at u 6.4, rising from 6.5 to 7.7; walkway u 6.4–7.4 at y 7.7.
  - **Maenianum II:** tier k from u = 7.4 + 0.8(k−1), top y = 7.7 + 0.5k (to 10.2 at u 11.4).
  - **Wall 2:** at u 11.4, rising from 10.2 to 11.4; walkway u 11.4–12.4 at y 11.4.
  - **Maenianum III:** tier k from u = 12.4 + 0.8(k−1), top y = 11.4 + 0.5k (to 13.9 at u 16.4).
  - **Colonnade:** u 16.4–19.4, floor 13.9, arches to 19.9 (one arch per ~4.4 m of its line, charcoal voids), attic to
    21.9, velarium masts every third arch to ~28, sails (red and white strips) slanting in over maenianum III.
  - Total ~22 m high, 19.4 m deep behind the wall line.
- **Boxes:** `SM_ColPulvinar` at (0, 67) yaw 180 (the Emperor's) and (0, −67) yaw 0 (the editor's): its origin at the
  bottom centre of the podium face (on the wall line). The loge floor at 4.0 overhangs the podium by 1.0 m; nothing
  below 4 m enters the arena.
- **Crowd** (instanced, ~3,700): senators 0.5 per metre of terrace; maenianum I 0.5, II 0.45, III 0.35 per metre per
  tier; zones over the side gates too (they carry seats), none over the great gates; ~14 senators per box.
- **Invisible stone:** `solidWalls` and `boundary` along the wall line (as the Grand and Imperial arenas), the boundary
  ≥ 30 m tall (the bowl is 22 m).

### Light
`sunEuler` (32, −40, 0), a warm late-afternoon sun. In the boss fight the `Hypogeum` lerps the sun over 3 s: phase 2
**dusk** (colour (1.0, 0.62, 0.42), intensity × 0.85), phase 3 **last light** ((0.95, 0.48, 0.36), × 0.72), back to
the day at his defeat. Fog (90, 380).

### Palette (`Assets/Art/Textures/Palette/palette_index.md`)
- **Stone:** `travertine_d` (main mass), `travertine` (lit faces, the stage top), `stone_grey` (worn blocks, hill
  rocks), `marble` / `marble_shadow` (the balustrade), `charcoal` (arch voids, tunnels, shafts), `ink` (shaft bottoms).
- **Metal and cloth:** `bronze`, `bronze_d`, `bronze_l` (statues, the stage rim, door studs, the Victor), `iron_d`
  (door frames, lift frames, bands), `cloth_white` + `red_banner` (the velarium, banners); `purple`, `purple_d`, `gold`,
  `gold_d` on the two boxes only.
- **Floor:** `sand_d` (the deck's sand), `sand` and `dirt_floor` (speckle), `wood_dark` + `wood` (the timber round
  every hole, the trapdoor leaves, the lift decks).
- **Never orange-red** (`hazard_*`, `telegraph`, `fire_*`) in the static art: the telegraphs are the only orange-red on
  the floor. Bone-white enemies must read against `sand_d`.

### Model names (FBX under `Assets/Art/Models/`), budgets and looks
**Environment/** (`BlenderSrc/environment/colosseum.py`):
| Model | Tris | Look, hierarchy, origin |
|---|---|---|
| `SM_ColFloor` | ≤ 12,000 | The deck (above), `sand_d` with speckle; a 0.4 m border of dark planks round every hole; faint plank lines along the ring's centre line and the axis so the hypogeum's pattern reads with the doors shut; height jitter ±1.2 cm except within 0.5 m of a hole. |
| `SM_ColCavea_0`–`_5` | ≤ 6,000 each | The runs (above). |
| `SM_ColGate` | ≤ 1,000 | A 12 m slice of the bowl's profile with an arched tunnel through its base (opening 5.0 × 7.0 m) and the seats carried over it. Root → `SM_ColGate_Frame`, `DoorL`, `DoorR` (bronze-studded timber), hinges at (±2.5, 0.25 m behind the front face, 0) like `SM_ImpGate`. Origin at the bottom centre of the front face. |
| `SM_ColGreatGate` | ≤ 1,600 | The Gates of Life and Death: a 22 m monumental arch rising above the bowl (~26 m), opening 5.0 × 9.0 m, a laurel keystone. Same door children and hinges as `SM_ColGate`. |
| `SM_ColPulvinar` | ≤ 600 | The box (above): purple-and-gold canopy on four columns to 9 m, a gold throne at local (0, 4.0, −0.8), seats for 14. |
| `SM_ColStage` | ≤ 900 | Round, r 10, top 1.2, walkable; `travertine_d` flagstones, a bronze rim band, a carved laurel ring round the 5 × 5 hole. Origin at the base centre. |
| `SM_ColStairs` | ≤ 220 | 3.0 × 2.0 × 1.2 m, 4 steps (0.3 × 0.5), `travertine_d` with bronze cheek caps (above). |
| `SM_ColTrapdoor` | ≤ 220 | Root → `SM_ColTrapdoor_Frame` (an `iron_d` border 0.12 m inside the 8 × 4 hole, and the shaft: `charcoal` walls from y 0 to −3, an `ink` bottom, a few iron rungs), **`LeafF`** (covers local z 0 → +2) and **`LeafB`** (z 0 → −2): `wood_dark` planks, `iron_d` bands, a ring handle, tops flush at y 0. **Pivots at their hinges:** `LeafF` at (0, 0, +2), `LeafB` at (0, 0, −2); each swings down about local X so its free edge (z = 0) drops into the shaft (~100°). Origin at the top centre. |
| `SM_ColLift` | ≤ 300 | Root → `SM_ColLift_Frame` (an `iron_d` frame round the 4 × 4 hole and a 3 m shaft) and **`Platform`**: a 3.8 × 3.8 `wood` deck with iron edges and four bronze corner posts 1.1 m tall; **pivot at the deck's top centre**, (0, 0, 0) at rest; code moves it down to y −3 and back. Origin at the top centre. |
| `SM_ColMons_A` | ≤ 700 | A sand hill with `stone_grey` boulders and a 2.2 m rock face (above). |
| `SM_ColMons_B` | ≤ 700 | A stepped "ruin" mound: low terraces of 0.55 m, a flat 6 × 4 m top, two broken column stumps (≤ 1 m, part of the mesh) on the back edge. |
| `PR_VictorStatue` | ≤ 350 | A 1.2 m `travertine_d` plinth (2 × 2 m) and a 4.3 m bronze Victor with the falx raised (5.5 m in all). Origin at the base centre, faces local +Z. |

Target for the whole arena: **≤ 100k static tris** (estimate ~72k).

**Enemies/** and **Props/** (`BlenderSrc/enemies/`, `BlenderSrc/props/`):
| Model | Tris | Look and parts |
|---|---|---|
| `EN_EternalVictor` | ≤ 2,500 | ~3.5 m to the crest top, gaunt. Parts `Body` (hip), `Head` (neck), `ArmL`, `ArmR`, `LegL`, `LegR`, plus **`Falx`** (child of ArmR, pivot at the grip in the right fist: the blade must fly alone for the throw), **`Crest`** and **`Laurel`** (children of Head), **`Cloak`** (child of Body, pivot at the shoulders). Bone limbs (`bone`), archaic bronze armour (`bronze`, `bronze_d`, `bronze_l`), a thraex helm with a forward-curling griffin crest, a gold laurel (`gold`), darkness behind the visor (`ink`), a crimson ribbon cloak (`crimson`, `crimson_d`), a magenta sash; the falx a ~2.6 m two-handed crescent (`steel_l` edge, `bronze_d` haft) held low across the body, the left fist lower on the haft at bind. Radius ~1.0. **No disc or wings** (the Golden Idol and the Emperor's Champion own those). |
| `EN_SkeletalLegionnaire` | ≤ 300 | ~1.7 m; parts `Body`, `Head`, `ArmL`, `ArmR`, `LegL`, `LegR` (like the Convict). Thin bone limbs (`bone`, `skin_undead`), a big skull (`ink` sockets) under a dented `rusty_iron` helmet with a broken crest stump, rusted segmented plates (`rusty_iron`, `bronze_d`), a rotted `crimson_d` rag, a magenta sash, a notched gladius (`steel`) in the right hand. Radius 0.36. |
| `EN_Chimera` | ≤ 800 | A quadruped like `EN_WarBoar` (front legs = `ArmL`/`ArmR` under `Body`, hind legs = `LegL`/`LegR` under the root, `Head` = the lion's head under `Body`), plus **`GoatHead`** (under Body, on its back, curling horns), **`Tail`** (under Body, a snake, pivot at its base) and **`TailHead`** (under Tail). Dark fur (`mud_dark`, `leather_d`), a `charcoal` mane, goat head and horns `bone` / `ash`, snake `slate` / `iron`, the jaws' inside `fire_mid` (it breathes fire), a magenta harness strap. ~1.6 m at the shoulder, 2.6 m long; radius 0.95. |
| `PR_BonePile` | ≤ 80 | A low heap of bones and a skull with the rusted helmet on top, ~1.0 m across, 0.35 m high (`bone`, `rusty_iron`); one mesh, origin at the base centre (Props/). |

Each enemy silhouette must read distinct at 64 px from every existing enemy and boss (render a lineup).

### Ids, paths and strings
- **Ids:** arena `colosseum`; boss `eternal_victor`; enemies `skeletal_legionnaire`, `chimera`; unlock `UL_Colosseum`
  (`DefeatBoss emperors_champion`). R3's Asterion will use `DefeatBoss eternal_victor`.
- **Assets:** `Assets/Data/Arenas/AL_Colosseum.asset`, `ST_Colosseum.asset`; `Assets/Data/Runs/RD_Colosseum.asset`;
  `MetaSetup.Dir + "UL_Colosseum.asset"`; `Assets/Data/Bosses/BD_EternalVictor.asset`; `Assets/Data/Enemies/`
  `EN_EternalVictor.asset`, `EN_SkeletalLegionnaire.asset`, `EN_Chimera.asset`; scene `Assets/Scenes/Arenas/Colosseum.unity`.
- **Damage source ids** (strings hashed with `GameInfo.StableId`, so no part depends on another for them): `"hypogeum"`
  (a fall into a trapdoor: the dead don't reassemble), `"stomp"` (a bone pile stomped), `"breath"` (the Chimera's fire
  on other enemies).
- **Enum values appended, never inserted:** `EnemyBehaviour.Breather` (after `Caster`), `BossAttack.Disc` (after
  `Volley`), `BossController.State.Remains` (after `Dead`).

## Orchestrator's answers to the design's open questions (docs/tracks/R2-design.md)
All ten recommendations accepted:
- **Q1:** (a) walk-only boxes. Few guards, the boss leaps, the player moves; revisit only if it looks bad in play.
- **Q2:** keep the false death. It is the first thing cut if the boss part runs long.
- **Q3:** the **hypogeum** part owns the swarm-multiplier change in `Director.cs`.
- **Q4:** an invisible floor over the holes, not real falling.
- **Q5:** no crowd hush in R2 (BACKLOG).
- **Q6:** the Codex bestiary's tiers 3–5 go to the BACKLOG, not R2.
- **Q7:** elite Legionnaires reassemble with their affix (one rule for all).
- **Q8:** lifts every 45 s are first guesses; the bot's `lifts` counters tune them.
- **Q9:** 2 Chimeras alive.
- **Q10:** the rise, not the Gate of Death.

**Branches:** the item branch is `phase5-colosseum`. It is cut from R1's branch plus this design; once R1 merges, the
orchestrator merges `main` into it. Every worker starts with
`git fetch origin && git merge --ff-only origin/phase5-colosseum` (or a plain merge if the fast-forward fails).

## Parts
**Wave 1 (design and art, no code; may start while R1 is tested):**
| Part | Owns | Done when |
|---|---|---|
| **design** | `docs/ARENAS.md` § "6. The Colosseum"; `docs/BOSSES.md` § "The Eternal Victor"; `docs/tracks/R2-design.md`; this draft | Done (this branch). |
| **env-art** | `BlenderSrc/environment/colosseum.py`, every Environment model above, their `.blend`, reviews, `docs/tracks/R2-env-art.md` | Every piece exported and reviewed within budget. The floor's holes match the trapdoor and lift lists exactly (the script prints the hole centres and yaws). The leaves and the lift platform pivot where stated; the gates' doors hinge like `SM_ImpGate`'s. A preview of the whole arena from these numbers, with the total tris printed. |
| **char-art** | `BlenderSrc/enemies/en_eternalvictor.py`, `en_skeletallegionnaire.py`, `en_chimera.py`, `BlenderSrc/props/pr_bonepile.py`, their FBX, `.blend` files and reviews, `docs/tracks/R2-char-art.md` | All four within budget, rigid parts with joint pivots and the names above, 3 angles + 64 px each, a lineup with the bosses and the Tier 1–3 enemies. |

**Wave 2 (code, after R1 merges; branch from `phase5-colosseum`).** Fields, methods and tests for each part:
`docs/tracks/R2-design.md` § "For each code part".
| Part | Owns | Done when |
|---|---|---|
| **hypogeum** | `Assets/Scripts/Gameplay/Arena/Hypogeum.cs` (new); the hypogeum fields and `swarmMul` in `ArenaLayoutDef.cs`; the hook in `ArenaBuilder.cs`; `Assets/Scripts/Core/Spatial/StaticObstacles.cs` (walk-only boxes); `Assets/Scripts/Gameplay/Director/Director.cs` (the swarm multiplier); new tests `HypogeumMathTests` (EditMode), `HypogeumTests` (PlayMode) | Drops, lift waves, falls, the player thrown out, the boss's phases read from `BossController`, the light; ccheck errors=0; tests written. |
| **enemies** | `EnemyDef.cs`; `EnemyManager.cs` (the hooks); `EnemyManager.Undead.cs` and `EnemyManager.Breath.cs` (new); new tests `BreathConeTests` (EditMode), `UndeadTests`, `ChimeraTests` (PlayMode) | The bone piles (collapse, rise, stomp, burn, fall), the Breather, `SetHpFloor` / `SetHp`; ccheck errors=0; tests written. |
| **boss** | `BossDef.cs`, `BossController.cs`, `BossFight.cs`, `BossSetup.cs` (`EnsureEternalVictor()`); new tests `EternalVictorTests` (PlayMode), additions to `BossPhaseDataTests` | The rise, the falx throw, the grasp, the phase-3 repeats, the false death; every older boss unchanged; ccheck errors=0 (or only on the enemies part's two names until it merges: mark stubs as R1 did). |
| **layout and setup** (starts after the other three, or codes against their names) | `ColosseumLayout.cs`, `ColosseumSetup.cs` (new); `PlaytestBot.cs` (the log line, `-arena colosseum`, avoiding doors about to drop); the run list in `ProvincialPitSetup.cs`; the scene list in `BuildScripts.cs`; new tests `ColosseumTests`; the arena in `ArenaNavTests` | The layout from these numbers, the enemies' and the run's data, the table and the lift packs; ccheck errors=0; tests written. |

**No file is owned by two parts.** Not touched by anyone in R2: `EnemyManager.Leash.cs`, `ChariotRace.cs`,
`FireJets.cs`, the other hazards, `CrowdExcitement.cs`, `CodexSetup.cs` (its bestiary stops at tier 2: a BACKLOG item,
not R2's), the shared docs (the orchestrator's).
