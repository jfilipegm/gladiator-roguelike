# Arenas

Each arena changes gameplay, not just textures (CLAUDE.md §6.2). An arena = an `ArenaLayoutDef` (pieces, gates, zones,
crowd, anchors, its spawn table, its boss) built into a scene by `ArenaBuilder`, plus a `RunDef` the menu offers.
One arena per run (DECISIONS, Q1): the climb happens across runs, through unlocks.

## 1. The Slave Pit (built, slice; rebuilt after the producer's playtest)
**Playtest rebuild (v0.5.x):** the producer found the old ~42 m round pit too small, read it as "a wooden bowl", and
fell off the map (the rim around the pit ended in the void). Now an **84 × 70 m oval stockade on open ground**:
- **Boundary:** 48 sections around the oval, each facing the yard: log **palisade** runs with three log **gates**
  (north, south-east, south-west), **patched fences** (old dark boards, new pale ones, a broken one, a brace, rope),
  **dirt embankments** with a wattle fence on the crest, and **plank platforms** of onlookers. Behind every section an
  **invisible wall** 27 m tall that only the player collides with (`ArenaLayoutDef.boundary`, Props layer): no number
  of air jumps gets you out.
- **A working yard:** a drill yard (training posts, straw dummies, a practice weapon rack, benches), a sparring ring
  of stones, a work corner (water trough, wheelbarrow, hay, sacks, barrel), a woodpile, a lean-to tool shed, cages and
  chain posts by the south wall, crates and barrels by the gates; trodden paths from the gates and worn drill circles in
  the floor. Four earth terraces with ramps, four mounds, six mud patches, four fire pits.
- **Outside (never reachable):** rolling dry grassland rising into hills that fade into the fog, ~150 trees, bushes and
  rocks, the slaves' huts, the slavers' longhouses and tents, three watchtowers, carts on the roads from the gates.
- **~110 locals:** on the platforms, leaning on the fences from outside, on the embankment crests.
- **The arena's shape is data:** `ArenaLayoutDef.aspect` (Z ÷ X; 1 = round) makes the play area an ellipse for the
  enemies, the spawn ring, pickups, chests, gifts, the boss and Gorm's phase-2 fire ring (`BossDef.fireRingInset`
  follows the edge). Fog per arena (`ArenaLayoutDef.fog`: 45-230 m here).
Boss: Gorm, the Pit Master (enters through the north gate). Layout: `SlavePitLayout.cs` → `AL_SlavePit`; models:
`BlenderSrc/environment/slave_pit_yard.py` (`part=enclosure|ground|props|outside`). Tests: `SlavePitTests` (built from
the layout, mud, fire, gates, nobody leaves) and `SlavePitEnclosureTests` (closed at every bearing and height, the
player can't get out over a fence or the berm, the oval keeps enemies in).

## 2. The Provincial Pit (Phase 5.4, v0.5.0; rebuilt bigger and octagonal for v0.8.0)
**Player feels:** "People notice me." A real arena at last: a provincial town walled in a stone square, with wooden
stands full of shouting, betting townsfolk.

**Rebuild (producer, 2026-10-20):** the v0.5.0 pit was a ~49 m circle (≈ 1,900 m²), smaller than the Slave Pit
(≈ 4,400 m²); every arena must be bigger than the last and not every arena round (`docs/GDD.md`). Now an **elongated
octagon**: the wall face runs 100 × 70 m with 14 m cut corners (≈ 6,600 m², the play area ≈ 6,400 m², ~1.45× the
Slave Pit). The play area's outline is data (`ArenaLayoutDef.outline`): `EnemyManager` turns it into a radial edge
table, so enemies, spawns, pickups, chests, gifts, the bot and the boss all use the octagon.

| | |
|---|---|
| **Unlock** | Beat Gorm once (`UL_ProvincialPit`, `DefeatBoss gorm`). Then the menu asks which arena after the gladiator. Locked: *"Beat the Pit Master and the provinces will want you."* |
| **Size / layout** | 100 × 70 m elongated octagon (corners cut 14 m). A sand floor ringed by a **stone foundation wall** (2.4 m) with **wooden stands** on top and stone piers at the eight corners. **4 gates**, one mid-side (north = the boss's), so enemies come from every side of a bigger floor. |
| **Vertical variety** | Six raised stone **platforms** (1.2 m): five with stairs, one with a long ramp; a low central dais. |
| **Pillars** | Ten stone pillars: a colonnade of eight either side of the dais, one toward each end. They block movement, shots and line of sight. The Champion's charge stuns him against one. |
| **Barricades** | Twelve wooden barricades (a chicane in front of each gate, four on the open floor): they split the floor into lanes and choke points. Enemies walk around them. **The Champion's charge smashes any barricade in its path** (gone for the run), so the arena opens up as the fight goes on. |
| **Spike traps (new rule)** | Ten iron grates in the floor (one in each gate lane). Every ~7 s each one telegraphs (orange-red, 1 s), then spikes shoot up for 1.2 s. **They hurt everyone**: the player (15) and enemies (40, ×2 for ordinary ones), so luring the horde over a grate is a tactic. Feet above 0.5 m clear them (jump). Traps are staggered so there's always one live. |
| **Braziers** | Six lit braziers on four platforms and beside the north gate: light and set dressing. If the Champion charges through one, it topples and leaves burning ground for 8 s. |
| **Enemies** | The Slave Pit's six, with tougher odds, plus two new ones: the **Shield Archer** (shield-protected archer) and the **Assassin** (flanker). Enemies have +25 % HP and +15 % damage here (`ArenaLayoutDef.enemyHpMul / playerDamageTakenMul`). |
| **Crowd** | ~400 spectators in the stands (instanced, as before). Cheers on streaks; chants when the Champion enters. |
| **Palette** | A2 ochre / dry grass for the floor and stands, warm sandstone and grey stone for the foundation, red-and-white banners. Distinct from the Slave Pit's mud browns at a glance. |
| **Boss** | **The Provincial Champion** (docs/BOSSES.md). |
| **Renown** | Arena tier 1 (the Slave Pit is 0): Renown × 1.5 (`MetaConfig.deeperArenaBonus`). Reaching it counts for `ReachArena 1` unlocks. |

### New enemies
- **Shield Archer** (`EN_ShieldArcher`, behaviour `ShieldedRanged`): keeps its distance like a Slinger, but plants a
  tall wicker shield in front of itself while it aims. Frontal hits are blocked like the Shield Thug's (85 %). Its
  arrow is a fast, straight, orange-red shot after a 0.9 s draw (a thin telegraph line). Counter: flank it, or use
  area damage and bombs that ignore facing. Silhouette: a tall rectangular shield taller than its body, a bow.
- **Assassin** (`EN_Assassin`, behaviour `Dasher`): fast and fragile. It circles to your side or back (it doesn't run
  at your face), crouches, shows an orange-red dash lane for 0.6 s, then dashes 7 m through you. Then it recovers for
  1.2 s (the window to kill it). Counter: watch the lanes, sidestep or jump. Silhouette: hooded, lean, two daggers,
  a long scarf.

### Building blocks
- **Data:** `RunDef` gains `tier`, `unlock`, `description`; `ArenaLayoutDef` gains `spawnTable`, `enemyHpMul`,
  `playerDamageTakenMul`, barricades, spike traps, braziers. `RunSummary.ArenaIndex` becomes the arena's tier.
- **Menu:** after the gladiator, a "CHOOSE YOUR ARENA" step when more than one arena exists (locked ones show their
  hint). It remembers the last arena.
- **Runtime:** `SpikeTraps` (central tick, instanced spikes, damage both sides), `Barricades` (obstacles that can be
  removed at runtime: the enemy push-out skips broken ones), brazier topple → `HazardZones` burning ground.
- **Models (Blender, `BlenderSrc/environment/provincial_pit.py`):** `SM_ProvFloor`, `SM_ProvPillar`, `SM_ProvWall_4m` (stone
  foundation), `SM_ProvStand_4m` (wooden stand on top), `SM_ProvPlatform` + stairs, `PR_Barricade`, `PR_SpikeTrap`,
  `PR_Banner`; enemies `EN_ShieldArcher`, `EN_Assassin` (`BlenderSrc/enemies/`), arrow `PR_Arrow`; boss `EN_Champion`.

### Tests
- Locked until Gorm falls; then the menu offers it and a run loads the Provincial Pit as the chosen gladiator.
- A run there records arena tier 1 (Renown bonus, `ReachArena 1`).
- Spike traps telegraph, then hurt the player and enemies in their grate; a jump clears them.
- A barricade blocks enemies until it's broken; broken, they walk through.
- The Shield Archer blocks frontal hits and shoots; the Assassin telegraphs, dashes, recovers.
- The Champion's charge breaks a barricade in its lane and stuns him on a pillar.

## 3. The Stone Amphitheater (Phase 5.8, v0.9.0)
**Player feels:** "I'm becoming a real gladiator." A real stone amphitheatre with thousands in the seats, beasts in
the pens, and a stage wall built to impress.

| | |
|---|---|
| **Unlock** | Beat the Provincial Champion (`UL_StoneAmphitheater`, `DefeatBoss champion`). Locked: *"The provinces have a champion. The city wants whoever beats him."* |
| **Size / shape** | A **D**: a half-ellipse bowl of tiered stone seating (south) facing a straight monumental **stage wall** (north). 120 m wide, 88 m deep (wall line); play area 8,513 m², 1.36× the Provincial Pit's 6,274 (`docs/GDD.md` ladder). Pale sandstone (palette A3), sand floor. |
| **The bowl (cavea)** | A 3 m stone podium wall with eight tiers of seats rising behind it, generated along the curve as one mesh (no seams on the bend). **1,711 spectators**, instanced (drawn in batches of 1,023). Three arched **tunnels** (vomitoria) in the podium: south-west, south, south-east. Invisible stone along the podium keeps air jumps in and gives the nav bake its edge. |
| **The stage wall (scaenae frons)** | 7.5 m of columns, niches and cornices along the north side. In the middle the **great gate** (the boss's); either side of it **four beast cages** with iron portcullises. |
| **Gates** | Four for the horde: the great gate and the three tunnels. The cages are not gates: only the beasts use them. |
| **Vertical variety** | The **stage** (pulpitum): two 17 m halves of 1.2 m raised stone along the stage wall, either side of the great gate (the gap is the boss's way in), with stairs at the front of each half and at both outer ends; four raised platforms on the floor (two with stairs, two with ramps). |
| **Pillars and statues** | A ring of twelve tall columns (the portico) around the floor and four gladiator statues on plinths: line-of-sight blockers for the archers and net-throwers, cover from the stampede. |
| **New rule: the beast cages** | From 1:15, every 40 s one cage telegraphs (its lane glows orange-red, its bars rattle, 2 s), then its portcullis rises and a pack of **war boars** charges out. A boar charges in a straight telegraphed lane and **tramples everything in it, the horde included**: lure the stampede through the crowd, or step aside. The pens get busier as the run goes on (pack size 3 → 6). |
| **Elites** | Every 60 s instead of 90, and the table's elites lean toward the heavy new enemies. |
| **Enemies** | The Provincial Pit's eight, tougher, plus the **Murmillo**, the **Net-Thrower** and the cages' **War Boar**. +50 % enemy HP, +25 % damage taken. |
| **Crowd** | Thousands: the bowl. Cheers on streaks, elites and trampled hordes; a roar when a cage opens. |
| **Boss** | **The Beast Lord** (docs/BOSSES.md). |
| **Renown** | Arena tier 2: Renown × 2 (`MetaConfig.deeperArenaBonus`). |

### New enemies
- **Murmillo** (`EN_Murmillo`, `ShieldBearer`): the heavy shield. A broad gladiator with a fish-crested helm and a
  tall rectangular scutum: blocks 90 % from the front, three times a Shield Thug's HP, slow, shrugs off knockback.
  Counter: flank, status ticks, blasts from the side. Silhouette: the crest and the door-shaped shield.
- **Net-Thrower** (`EN_NetThrower`, `Lobber` with a net): keeps its distance, whirls a weighted net and lobs it onto
  your position (an orange-red ring shows where). Whoever's inside on the ground when it lands is **caught**: speed ×0.1 for 1.2 s, and the net lies spread on the
  ring (no fire patch). Counter: keep moving; jump (a net can't catch you mid-air). Silhouette: a bundled net on the shoulder,
  a bare head, a trident.
- **War Boar** (`EN_WarBoar`, `Dasher` that doesn't flank, tramples): only from the cages (and the Beast Lord). A
  low, wide tusked boar: it lines up, shows its lane for 0.7 s, then charges 14 m. It hurts **everything** in the
  lane (enemies take 3× its damage). Counter: sidestep, jump, or let it plough the horde.
- **Beast Handler** (`EN_BeastHandler`, `Lunger`; Phase 5.15, also in the Grand Arena from 1:30): a bald handler in a leather apron
  who walks **two Pit Curs on leashes** (a rope on the sand) and cracks a whip (a short brace, a 3.5 m crack). The curs
  can't stray more than 3.5 m from him; **kill him and they're freed** (they vanish, no fight). From 2:10, 4 at most.
  Counter: go for the handler first. 25 of them unlock the Beastmaster.

### Building blocks
- **Data:** `ArenaLayoutDef.eliteEvery` (→ Director), `cages` (positions/yaws), `cageFirst`, `cageEvery`, `cagePack`, `cageBeast`, per-gate `model`, the D's
  outline and invisible walls; `EnemyDef.lobNet` (net instead of fire), `flank` (false: dash from where it is),
  `trampleMul` (damage to enemies in its lane), `netSlow` / `netTime`; `BossDef` pounce fields, `beastPack`,
  `phase2CagePack`, `scatterOnDefeat`, `shortName` / `deathLine` / `victoryLine` (the run summary).
- **Runtime:** `BeastCages` (central tick: telegraph, portcullis lift, release a pack through the cage's spawn
  point; `Open(i)` / `OpenAll()` for the boss); Dasher trample in `EnemyManager`; net landing in `EnemyProjectiles`.
- **Models (Blender, `BlenderSrc/environment/amphitheater.py`):** `SM_AmphiFloor`, `SM_AmphiCavea` (podium + tiers
  along the curve, one mesh), `SM_AmphiRim`, `SM_AmphiStage_4m`, `SM_GreatGate`, `SM_CageGate` (with a `Bars`
  child), `SM_AmphiTunnel` (gate with `DoorL/DoorR`), `SM_Pulpitum`, `SM_AmphiColumn`, `PR_Statue`; enemies
  `EN_Murmillo`, `EN_NetThrower`, `EN_WarBoar`, boss `EN_BeastLord` (`BlenderSrc/enemies/`).

### Tests
- Locked until the Champion falls; then the menu offers it, and a run there is tier 2 with its own difficulty.
- The D is bigger than the Provincial Pit and keeps the horde in along the stage wall and the curve.
- A cage telegraphs, opens and releases boars; a boar's charge hurts the player and the enemies in its lane.
- The Murmillo blocks from the front; a net catches a standing player and misses a jumping one.
- The Beast Lord: entrance, each attack telegraphs and hits, the pounce lands where it was shown, the horn opens a
  cage, phase 2 opens every cage and he pounces twice; at his fall the beasts scatter. (`AmphitheaterTests` 6,
  `BeastLordTests` 7.)

## 4. The Grand Arena (Phase 5.13, v0.14.0)
**Player feels:** "I'm a star." The richest men in the city built themselves an arena of white marble to watch
fighters like you, from boxes hung with gold.

| | |
|---|---|
| **Unlock** | Beat the Beast Lord (`UL_GrandArena`, `DefeatBoss beastlord`). Locked: *"The city has seen you kill its beasts. Now the rich want to watch."* |
| **Size / shape** | A **Greek cross**: four arms 52 m wide reaching 70 m from the centre (140 m tip to tip), the four inner corners cut on the diagonal by the **patron boxes**. Play area ≈ 11,600 m², ~1.36× the Stone Amphitheater's 8,513 (`docs/GDD.md` ladder). White marble with red bands (palette A4), pale sand floor. |
| **Layout** | A central **plaza** (the cross's middle square) with a round raised **dais** (1.2 m, four stairs) under a gilded statue of the Patron (a line-of-sight blocker); four **arms**, each a hall of sand between two marble walls with stands behind them, ending in a **monumental gate** (north = the boss's). Two raised marble **terraces** (stairs or a ramp) against the walls of each arm. |
| **Patron boxes** | Raised loges with red-and-gold canopies on the four diagonal walls between the arms, full of nobles. The Patron's hired swords jump down from them. |
| **New rule: fire jets** | A row of bronze nozzles across each arm, halfway to its gate. Every 10 s a row telegraphs (its strip across the arm glows orange-red, 1.2 s), then roars a 3.5 m wall of fire for 1.6 s. **Too tall to jump; it hurts everyone**: the player (20) and the horde (45, ×2 for ordinary enemies), so the horde coming down an arm burns. The four rows fire in turn (one every 2.5 s, going round). |
| **New rule: blade sweepers** | Four bronze hubs in the plaza, each with a 5.5 m blade at shin height. Every 12 s two of them (a diagonal pair) spin up: their circle glows orange-red and the blade turns slowly (1.5 s), then it whirls for 5 s. **Jump it** (feet above 0.55 m clear it) or stay out of the circle; it mows the horde (35, ×2 ordinary). |
| **Gates** | Four, one at each arm's end. Swarms and elites come through them. |
| **Enemies** | The Amphitheater's (not the cage boars), tougher and sooner, plus the **Gilded Mercenary** and the **Sorcerer**. +80 % enemy HP, +35 % damage taken. Elites every 60 s. |
| **Pressure (C18)** | Up to 400 alive (the others 300–320), and the Director's target curve and spawn rate ×1.2 here (`ArenaLayoutDef.densityMul`): the biggest floor so far gets more of the horde, within the §8 budget. |
| **Crowd** | ~2,000: nobles and merchants in the stands and the boxes. |
| **Boss** | **The Gilded Patron** (docs/BOSSES.md). |
| **Renown** | Arena tier 3: Renown × 2.5 (`MetaConfig.deeperArenaBonus`). |

### New enemies
- **Gilded Mercenary** (`EN_GildedMercenary`, `Lunger` + `stealGold`): a sellsword in gold-trimmed armour with a
  halberd. Closes in, braces (0.7 s), then thrusts 3 m. **A hit pockets your gold** (up to 12 a hit: "−12" in
  gold); killing him drops everything he took, and he often carries a purse. The Patron's guard: while any of
  them lives, the Patron takes far less damage. Silhouette: a crested gold helm, a halberd taller than him.
- **Sorcerer** (`EN_Sorcerer`, new behaviour `Caster`): a robed seer with a tall hood and an orb staff. Keeps 10–14 m
  away, raises the staff (0.8 s), then a **rune circle** opens under you (2.2 m, orange-red, fills over 1.1 s) and a
  pillar of fire erupts from it (too tall to jump). Get within 5 m and he **blinks** away in a puff (every 5 s at
  most). Fragile: corner him, or kill him from range. Silhouette: the tall hood and the orb.

### Building blocks
- **Data:** `ArenaLayoutDef.densityMul` (→ Director), `fireJets` (rows: centre, yaw, width), `bladeSweepers` (hubs),
  `guardSpots` (where a boss's guards appear: the patron boxes); `EnemyDef.stealGold`, `blinkRange` /
  `blinkCooldown` / `blinkDistance`, `runeDelay`; `BossDef` coin bombs, golden volley, `guardedBy` +
  `guardedDamageMul`, phase-2 hazards.
- **Runtime:** `FireJets`, `BladeSweepers` (central ticks, instanced flames and blades, hurt both sides); enemy
  projectiles gain the rune (Sorcerer), the purse bomb and the hot coin (the Patron).
- **Models (Blender, `BlenderSrc/environment/grand_arena.py`):** `SM_GrandFloor`, `SM_GrandWall_4m` (podium wall +
  stands), `SM_PatronBox`, `SM_GrandGate`, `SM_GrandDais` + `PR_PatronStatue`, `SM_GrandTerrace`, `PR_FireJetRow`,
  `PR_JetFlame`, `PR_BladeHub` + `PR_Blade`; enemies `EN_GildedMercenary`, `EN_Sorcerer`; boss `EN_GildedPatron`.

### Tests
- Locked until the Beast Lord falls; then the menu offers it, a run there is tier 3, and it's bigger than the
  Amphitheater.
- The cross keeps the horde in (arm ends, arm sides, the patron boxes' diagonals).
- A fire-jet row telegraphs, then hurts the player and the enemies in its strip, jump or not.
- A blade sweeper telegraphs, then hits a grounded player and the enemies it passes; a jump clears it.
- The Mercenary steals gold on a hit and gives it back when he dies; the Sorcerer's rune hits a player who stays and
  misses one who moves; he blinks away when you close in.
- The Gilded Patron: entrance, coin bombs (hit + gold), the volley, the shove; hired mercenaries appear at the boxes
  and guard him (less damage) until they die; phase 2 fires every jet and spins every blade; his gold at his fall.

## 5. The Imperial Arena (Phase 5.16, planned: R1 in docs/ROADMAP.md)
**Player feels:** "One of the greatest alive." The Emperor's own hippodrome: travertine and imperial purple under gold,
a spina of obelisk and metae down the middle, chariots thundering round it, the Praetorian Guard marching in
formation, and the Emperor watching from his box. Contract: `docs/tracks/R1-imperial-arena.md`; reasons for every
number: `docs/tracks/R1-design.md`.

| | |
|---|---|
| **Unlock** | Beat the Gilded Patron (`UL_ImperialArena`, `DefeatBoss patron`). Locked: *"The Patron is bankrupt. The Emperor has heard your name."* |
| **Size / shape** | A **hippodrome** along X (metres, y up). The wall line: straight sides at z = ±44 from x = −95 to x = 51, a flat **west end** at x = −95 (the **carceres**), a **semicircle** of radius 44 round (51, 0) reaching x = 95 (the **sphendone**). Wall line 15,889 m²; play area (inset 1.1 m, the curve as 24 segments) **15,315 m², 1.31× the Grand Arena's 11,674** and above the GDD's 13,500. Convex: the radial edge table works from the centre (0, 0). The spina covers ~600 m² of floor, but its top is playable (the Grand Arena's dais isn't subtracted either). Travertine and purple (A6) with gold (A5) trim, pale sand floor. |
| **Layout** | Three bands either side of the spina: the **inner strip** (9 m between the spina and the track), the **track** (4 m, drawn darker), the **outer field** (27 m to the stands). The sphendone (38 m of floor beyond the east turn's lane) holds the triumphal gate; the west end (18 m beyond the west turn's lane) holds the carceres, where the chariots come and go. |
| **The spina** | A raised walkway along z = 0 from x = −60 to x = 40, **1.2 m high and 6 m wide**, with **SM_SpinaStairs on both long sides at x = −45, −20, 5, 30** (8 flights, one every 25 m). On top: the **obelisk** at x = −10 (~12 m tall, Solid: blocks movement, shots and sight; 1.7 m to walk past it on each side), the **lap counter** at x = 20 (Prop), braziers flanking the obelisk. At each end a **meta** (three gold cones on a round plinth, Solid) centred at (−60, 0) and (40, 0). Never touched by a chariot: the safe island, but the horde climbs the stairs too. |
| **Vertical variety** | The spina (100 m of 1.2 m walkway, 8 stairs); four **travertine terraces** (12 × 5 m, 1.2 m, against the long walls at (−75, +41.5), (10, +41.5), (−80, −41.5), (0, −41.5), SM_SpinaStairs on their track side), if env-art adds `SM_ImpTerrace` (else `SM_GrandTerrace`, see R1-design.md Q2); the chariots themselves (jump one). |
| **Gates** | Five: the **triumphal gate** at (95, 0) (`SM_TriumphalGate`, the boss's, gate 0, also used by the horde) and four side gates (`SM_ImpGate`) at x = −50 and x = 30 on both long walls, facing in. The carceres are not a horde gate: two of their 22 bays (z = ±14) are the chariots' stalls. |
| **The pulvinar** | The imperial box, ~14 m wide, in the north wall at x = −10, facing the obelisk across the track: purple awnings, ~16 senators and the Emperor. Decor (no rule). |
| **New rule: the chariot races** | From **1:20**, every **60 s** (start to start: 7 races in an 8-minute run, one per egg on the lap counter): trumpets twice, "CHARIOTS!", and the whole track (with its run-in and run-out lanes) **fills orange-red over 3 s**. Then two quadrigae burst out of the carceres one after the other and race **counter-clockwise** round the spina at **22 m/s**, half a lap apart, **2 laps**, and leave through the carceres. A race lasts ~37 s (27.5 s per chariot, the second 6.5 s behind), leaving ~23 s of quiet. **They trample everything in the lane**: the player (28, knocked out of the lane, once per pass) and the horde (200, ×2 for ordinary enemies: every ordinary type dies; the boss takes 600). **Counters:** step off the track, cross it between chariots, stand on the spina, or **jump a chariot** (feet above 1.1 m clear it: a full jump stays that high for 0.37 s, a chariot takes 0.26 s to pass). While a race runs, only the lane's rims stay lit and a bright 26 m tongue shows where each chariot will be in the next 1.2 s. Trampled enemies drop their gems on the track: go and get them between passes. The boss's phase 3 makes the races run back to back. |
| **The track (numbers)** | Centre line: straights at z = ±14 from x = −60 to 40, half-circles of radius 14 round (−60, 0) and (40, 0); **lap 288 m**; lane 4 m wide (the centre line ± 2 m). Chariots enter at (−97, −14) (inside the stall) heading east along the south straight's extension (the **run-in**, 37 m) and leave west along the north straight's extension to (−97, 14) (the **run-out**). Path per chariot: run-in + 2 laps − the last west turn + run-out = **606 m**. Each point on the straights and the east turn is passed 4 times per race, the west turn twice. Nothing may stand within the lane + 1.5 m: no piece, chest, altar, brazier, gate mouth or spawn point. |
| **Enemies** | The Grand Arena's walkers (not the Beast Handler: beasts belong to arenas 3–4), sooner and with fewer loose shields, plus the **Centurion**, who brings a **Praetorian formation**. **+115 % enemy HP, +45 % damage taken** (`enemyHpMul 2.15`, `playerDamageTakenMul 1.45`: the ladder's steps were +0.25/+0.25/+0.3 HP and +0.15/+0.10/+0.10 damage). Elites every 60 s. |
| **Pressure** | Up to **400** alive, the Director ×**1.25** (`densityMul`: the biggest floor so far), ring spawns within **55 m** of the player (`spawnMaxDistance`: the horde comes from your end, not from 190 m away). |
| **Crowd** | The biggest yet: ~2,200 (0.75 per metre per tier along the long sides and the sphendone; none over the carceres), plus the pulvinar. Roars at every race start and at a pass that tramples 8 or more. |
| **Chests and altars** | 14 chest anchors, all off the track: the spina top (−35, 0) and (5, 0) at 1.2 m; the inner strips (−35, 7.5) and (15, −7.5); the outer fields (−70, 28), (−20, 30), (25, 28), (−45, −28), (0, −30), (45, −28); the sphendone (72, ±22); the west end (−85, ±30). Three altars: the spina top (−25, 0) at 1.2 m, the sphendone (66, 0), the west end (−82, 0). |
| **Player spawn** | (−10, 0.1, −26), facing north: the track, the obelisk and the pulvinar ahead. |
| **Boss** | **The Emperor's Champion** (docs/BOSSES.md), through the triumphal gate. |
| **Renown** | Arena tier 4: Renown × 3 (`MetaConfig.deeperArenaBonus` 0.5 per tier, no code). |

### New enemies
- **Praetorian** (`EN_Praetorian`, `ShieldBearer`): the Emperor's guard. A tall curved scutum, an upright purple
  plume, a pilum held upright, travertine and purple armour (288 tris). 70 HP, 3.0 m/s, radius 0.5, contact 10,
  **blocks 75 % from a 120° front** (less than a Shield Thug's 85 %: they come five at a time), knockback ×0.25, turn
  180°/s. Only ever spawned in a Centurion's line (not in the table). **In formation** he holds his slot (catching up
  at up to 1.6× his speed; rigidly with the line during a push) and faces the line's front, not you: flanking the line
  works. **Line broken:** an ordinary ShieldBearer that turns to face you.
- **Centurion** (`EN_Centurion`, `Lunger`): the officer. A transverse crest (his silhouette), a red cloak, a vine
  staff raised, no shield (284 tris). 160 HP, **2.6 m/s** (the line's march), radius 0.42, contact 10, knockback ×0.3,
  **turns 80°/s** (the line wheels slowly: slide round its end). He walks behind a **line of five Praetorians** (slots
  in his frame, x right and y forward: (−2.6, 1.8), (−1.3, 1.8), (0, 1.8), (1.3, 1.8), (2.6, 1.8): a 6.2 m wall of
  shields 1.8 m ahead of him) and stops 2.6 m from you while his line stands (the shields press you; they don't walk
  through you).
  - **The push (the line's attack):** when you're within 4.2 m of him the line stops and braces: an orange-red lane
    the line's width + 0.8 m (7 m) and 2.6 m deep fills in front of the shields over **0.7 s**, then the whole line
    shoves forward 2 m (8 m/s × 0.25 s): **16 + knockback 9**, once per push, from any shield that touches you. Every
    3.5 s at most. Counter: step aside (3.5 m), step back, jump (above 1.1 m), or be behind them.
  - **Kill him and the line breaks:** every Praetorian still in it is **Staggered** (`SE_Stagger`, 1 s) with its
    **shield down for 3 s** (no block), then fights on as an ordinary ShieldBearer. They are not freed
    (`freeEscortsOnDeath false`; the Beast Handler's curs still vanish). A dead Praetorian leaves a gap (no closing
    ranks). A Centurion whose whole line is dead is a plain Lunger (his own staff strike, no hold, no wide lane).
  - Never an elite (an affix would break the march). From 1:00, at most 3 alive (18 bodies). Gold 0.35.
- **Formations as guards:** the Emperor's Champion calls 2 Centurions (with their lines) through the two gates
  nearest you, from his phase 2.

### Spawn table (`ST_ImperialArena`)
Weights ramp linearly from `from` to the end of the run. Affixes: the Slave Pit's (as every table). Changes from the
Grand Arena: everything a little sooner; fewer Shield Thugs, Murmillos and Mercenaries (the formations are this
arena's shields: the end-of-run shield share stays ~the Grand Arena's); no Beast Handler; the Centurion.

| Enemy | From | Weight start → end | Cap | Swarm | Elite |
|---|---|---|---|---|---|
| Starving Convict | 0:00 | 6 → 2.5 | — | ✓ | ✓ |
| Pit Cur | 0:05 | 3 → 4.5 | 110 | ✓ | ✓ |
| Shield Thug | 0:20 | 1.2 → 1.6 | 30 | | ✓ |
| Slinger | 0:25 | 1.2 → 1.6 | 14 | | ✓ |
| Shield Archer | 0:35 | 1.0 → 1.4 | 12 | | ✓ |
| Murmillo | 0:45 | 0.6 → 1.0 | 12 | | ✓ |
| Fire-Pot Thrower | 0:50 | 0.8 → 1.2 | 8 | | ✓ |
| Assassin | 0:50 | 1.0 → 1.5 | 16 | | ✓ |
| **Centurion** (+5 Praetorians) | 1:00 | 0.15 → 0.25 | 3 | | ✗ |
| Net-Thrower | 1:00 | 0.7 → 1.1 | 10 | | ✓ |
| Gilded Mercenary | 1:00 | 0.5 → 0.9 | 12 | | ✓ |
| Brute | 1:10 | 0.6 → 1.1 | 10 | | ✓ |
| Sorcerer | 1:15 | 0.7 → 1.2 | 12 | | ✓ |

The Centurion's weight is a first guess (the Beast Handler's 0.12–0.2 / cap 3 is being measured to land ~25–35 a
run); the bot logs `formations` / `linesBroken` to tune it.

### Building blocks
- **Data:** `ArenaLayoutDef` chariot fields (the track's metae, radius, width, stall x; first, every, count, laps,
  speed, telegraph; the chariot model); `EnemyDef.formationSlots`, `formationHold`, `freeEscortsOnDeath`,
  `breakStatus`, `breakShieldsDown`; `BossDef.phase3At` / `phase3Attacks` / `phase3Text` / `phase3Speed` /
  `phase3MoveSpeed` / `phase3ChargeRepeats`, `guardsFromPhase`, `guardsNearestGates`, `volleyJavelins`,
  `closeInRange` / `closeInSpeed`. No new enum values (every attack and behaviour exists).
- **Runtime:** `ChariotRace` (central tick: the schedule, the path, telegraphs, trampling both sides, back to back
  from the boss's phase 3, halted at his fall); formations in `EnemyManager.Leash` (slots in the leader's frame, the
  hold, the push, the broken line); a third boss phase in `BossController`.
- **Models (Blender):** `BlenderSrc/environment/imperial_arena.py`: `SM_ImpFloor` (with the darker track lane),
  `SM_ImpStand_4m`, `SM_Carceres`, `SM_ImpGate`, `SM_TriumphalGate`, `SM_Pulvinar`, `SM_Spina_10m`, `SM_SpinaEnd`,
  `SM_SpinaStairs`, `SM_Obelisk`, `PR_LapCounter` (+ `SM_ImpTerrace` if added); `BlenderSrc/props/pr_chariot.py`
  `PR_Chariot`; enemies `EN_Praetorian`, `EN_Centurion`; boss `EN_EmperorsChampion`.
- **Field-by-field list for each code part:** `docs/tracks/R1-design.md`.

### Tests
- Locked until the Gilded Patron falls; then the menu offers it, a run there is tier 4 (Renown ×3) with ×2.15 HP,
  ×1.45 damage, cap 400 and density ×1.25.
- The hippodrome is bigger than the Grand Arena (`PlayArea()` ≈ 15,315 > 11,674, and ≥ 13,500) and keeps the horde
  in along the carceres, both long sides, the sphendone's curve and at the triumphal gate.
- Nothing stands on the track: no piece footprint, chest, altar, brazier, gate mouth or spawn point within the lane
  (run-in and run-out included) + 1.5 m.
- The spina is walkable at 1.2 m and the horde climbs its stairs; the obelisk blocks enemy shots.
- A race telegraphs for 3 s with no chariot on the track, then two chariots leave the carceres ~6.5 s apart, run
  counter-clockwise 2 laps (~606 m each) and leave; the next race starts 60 s after the last one started.
- A chariot hits a grounded player in the lane once per pass (28 × 1.45, knocked out of the lane), misses one beside
  the lane, and misses one whose feet are above 1.1 m.
- A chariot kills an ordinary enemy in its path (a Brute and a Centurion included), takes 200 off an elite, 600 off
  the boss, and leaves an enemy just beside the lane alone.
- A Centurion spawns with five Praetorians in a line 1.8 m ahead; they keep their slots as he walks and turns; they
  block from the front, not from behind; the push telegraphs its lane and hits a player in it, not one beside it;
  killing him staggers the line with its shields down, then they fight on (alive, not freed). The Beast Handler still
  frees his curs.
- The Emperor's Champion: the entrance through the triumphal gate, the stride when you're far; the thrust, bash and
  javelins telegraph and hit (javelins reach a player on the spina); at 60 % two formations come through the two
  gates nearest you and the charge stuns him on the obelisk, a meta or the wall; at 30 % "FOR THE EMPEROR!", the
  races run back to back, he's faster and charges twice; at his fall the races halt and the Guard stands down. Gorm,
  the Champion, the Beast Lord and the Patron stay two-phase.

## 6. The Colosseum (R2, planned)
**Player feels:** "I have become a legend." The oldest arena and the biggest: a towering bowl of weathered travertine,
arches stacked to the sky, forty thousand voices, and a floor of sand-strewn timber laid over the hypogeum, where the
beasts are kept and the dead are buried. The floor drops open under the horde, the lifts bring up beasts and the dead,
and the arena's first champion waits underneath. Contract: `docs/tracks/R2-colosseum.md`; reasons for every number:
`docs/tracks/R2-design.md`.

| | |
|---|---|
| **Unlock** | Beat the Emperor's Champion (`UL_Colosseum`, `DefeatBoss emperors_champion`). Locked: *"The Emperor's champion is dead. One arena is left, and it has been waiting for you."* |
| **Size / shape** | **The great oval**, along X (metres, y up). Wall line: an ellipse with semi-axes **98 × 67** (196 × 134 m), sampled every 3.75°, with each great gate's 22 m span cut by its chord (x = ±96.67; the curve bulges 1.33 m past it). Wall line 20,576 m²; play area (inset 1.1 m) **20,005 m², 1.31× the Imperial Arena's 15,315** (the same step as the last one) and above the GDD's 17,500. Convex: the radial edge table works from (0, 0). Weathered travertine (`travertine_d`, `travertine`, `stone_grey`), charcoal arch voids, a red-and-white velarium, bronze; floor `sand_d` over dark timber. |
| **Why an oval** | The shapes so far: rough oval, octagon, D, cross, hippodrome. The rule is "not *only* circles and ovals", and the ladder ends on the one shape every player already knows, mirroring the first arena (an oval pit) at five times its size. The new geometry is in the floor instead: trapdoors that change the floor's shape through the run and the boss fight (the third phase leaves a broken ring of chasms), a raised stage, scenery hills. |
| **Layout** | Three bands. The **inner arena**, inside the trapdoor ring (an ellipse about 60 × 40 m to the doors' inner edges): the stage, its four statues, the axis trapdoors, four lifts. The **ring** of trapdoors (centre line an ellipse 62 × 42). The **outer band** between the ring and the podium (24 m deep at the sides, 33 m at the ends): four scenery hills, four lifts, the six gates. |
| **The stage** | The **Stage of Victors**: round, r 10 m, at the centre, **1.2 m high**, walkable, with `SM_ColStairs` on each axis (origins at (0, ±12) and (±12, 0), rising inward). The Victor's lift (5 × 5 m) is set in its middle: he rises there. Four bronze **statues of the Victor** (`PR_VictorStatue`, Solid, circle r 1.1) at r = 14 on the diagonals, (±9.9, ±9.9): line-of-sight blockers, and cover from his thrown falx. |
| **Vertical variety** | The stage (1.2 m, four stairs); four **montes**, the scenery hills the hypogeum raises for a beast hunt (14 × 10 m, **2.2 m** high, slopes ≤ 20° on three sides for sliding, a rock face toward the wall): `SM_ColMons_A` at (28, ±50.5), `SM_ColMons_B` at (−28, ±50.5); the **pit edges** of every open trapdoor (and the chasms of the boss's phase 3). |
| **New rule: the trapdoors (the floor opens)** | **30 trapdoors**, 8 × 4 m (8 along the corridor below, 4 across), iron-banded dark timber set flush in the sand: always visible as "the floor that opens". A broken **ring** of 24 round the inner arena (5.7 m of deck between doors) and the **axis**: 3 doors either side of the stage along z = 0 (centres at x = ±20, ±32, ±44; 4 m of deck between them). **Six groups:** the ring's four quarters (6 doors each) and the axis's two halves (3 each). From **0:40**, every **14 s**, the group with the door **nearest you** (never the same group twice running) **telegraphs for 2 s** (its doors fill orange-red, rattle, dust puffs from the seams), then **drops open for 4 s** (the leaves swing down into a black shaft 3 m deep), then closes (0.6 s). ~31 drops a run. **Whatever walks onto an open door falls:** an ordinary enemy dies (a dust plume; its gems land on the door, collect them when it closes), an **elite takes 300** (it claws its way out), the boss takes 500. **The horde doesn't avoid open doors:** lead it across a dropping group and it pours in for 4 s. **You:** grounded on a door as it opens, or landing on an open one, you're **hit (22) and thrown back over the nearer long edge**, once per opening. **Counters:** step off during the 2 s; **jump it** (a running jump carries 4.7 m, the door is 4 m across; a slide-jump carries 7 m); feet in the air as it opens are safe. While a door is open only its rim stays orange-red (alpha 0.35): the black hole is the warning. |
| **New rule: the lifts (the hypogeum opens)** | **8 lifts**, 4 × 4 m iron-framed platforms: L0–L3 in the inner arena at (±34, ±20), L4–L7 in the outer band at (±80, ±24). From **1:30**, every **45 s** (9 waves): trumpets, the announcer calls who's coming ("THE BEASTS RISE!", "THE DEAD RISE!"), and the **3 lifts nearest you that are at least 12 m away** telegraph for 1.5 s (orange-red squares), sink (0.5 s) and rise (0.9 s) with **a pack each**. The horde comes up **in the middle of the floor**, not only from the edges: no corner of a 196 m oval is safe for long. Packs by run time (table below): Pit Curs early, War Boars from 2:30 (they charge from wherever they rise: lead them through the horde), one Chimera from 4:00, Skeletal Legionnaires from 4:30. Packs respect their caps and the arena's cap. In the boss fight the lifts carry his dead instead (docs/BOSSES.md). |
| **Gates** | Six, all horde gates (swarms, elites), counter-clockwise from the east: **G0, the Gate of Death** (96.67, 0), yaw −90, and **G3, the Gate of Life** (−96.67, 0), yaw 90 (`SM_ColGreatGate`, 22 m arches); side gates **G1** (49, 58.02) yaw −158.5, **G2** (−49, 58.02) yaw 158.5, **G4** (−49, −58.02) yaw 21.5, **G5** (49, −58.02) yaw −21.5 (`SM_ColGate`, 12 m, carrying the seats over the tunnel). The boss rises from the stage instead of using a gate; `bossGate` 0 stays as a fallback. |
| **The stands** | The tallest yet (~22 m), the multi-tier bowl of the real thing: a **4 m podium** with a marble balustrade and the senators' terrace, **three maeniana of five tiers** each (rise 0.5, run 0.8) separated by 1.2 m walls with walkways, and on top a **colonnade of arches** under the velarium's masts and sails (numbers: the contract). Generated along the curve as six meshes, one per run between gates. The **imperial box** (`SM_ColPulvinar`) at the north apex (0, 67), the Emperor among his senators, and its twin, the editor's box, at (0, −67): decor (no rule). |
| **Enemies** | Three factions, each with its own way in. **The living** (the ladder's gladiators: Convicts, Thugs, Archers, Murmillos, Assassins, Net-Throwers, Centurions with their lines, Brutes, Sorcerers) through the gates. **The beasts** (Pit Curs, War Boars and the new **Chimera**) up the lifts. **The dead** (the new **Skeletal Legionnaire**) from the ring, the lifts and the boss; their share of the table grows all run until the dead outnumber the living. Fewer types than the Imperial Arena (no Slinger, Fire-Pot Thrower, Gilded Mercenary or Beast Handler): readability at 450. **+155 % enemy HP, +55 % damage taken** (`enemyHpMul 2.55`, `playerDamageTakenMul 1.55`). Elites every 60 s. |
| **Pressure ("huge waves")** | Up to **450** alive (§8: ~500 at peak), the Director ×**1.3** (`densityMul`), ring spawns within **55 m** of you, and **swarms ×1.5** (`swarmMul`, new: up to 120 at the end of the run, through a gate or from the ring); plus the lift waves. The open trapdoors thin the horde as fast as the gates fill it. |
| **Crowd** | The loudest: **~3,700** (the senators on the podium, three maeniana), plus the Emperor's box. Roars at every lift wave, at a drop that swallows 10 or more, and at the Victor's rise. |
| **Light** | Late afternoon: a low, warm sun (`sunEuler` (32, −40, 0)) and long shadows, unlike any other arena. Dusk in the boss's phase 2, last light in phase 3 (never below 0.72 of the day's intensity: telegraphs and accents must read). |
| **Chests and altars** | **18 chest anchors**, all clear of trapdoors, lifts, stairs and gate mouths: (0, 30), (0, −36), (22, 10), (−22, −10), (48, 12), (−48, −12), (48, −14), (−48, 14), (66, 30), (−66, 30), (66, −30), (−66, −30), (84, 8), (−84, −8), (8, 56), (−8, −57), and the tops of two hills (28, 2.2, 50.5), (−28, 2.2, −50.5). **3 altars:** the stage top (0, 1.2, 6.5), the hill tops (−28, 2.2, 50.5) and (28, 2.2, −50.5). |
| **Player spawn** | (0, 0.1, −26), facing north: the stage, its statues and the imperial box ahead. |
| **Boss** | **The Eternal Victor** (docs/BOSSES.md), rising from the hypogeum on the stage's lift. His defeat unlocks Asterion (R3: `DefeatBoss eternal_victor`). |
| **Renown** | Arena tier 5: Renown × 3.5 (`MetaConfig.deeperArenaBonus` 0.5 per tier, no code). |

### New enemies
- **Skeletal Legionnaire** (`EN_SkeletalLegionnaire`, `Chaser` + **reassembles**): the dead of four hundred years of
  games, in the rusted segmented armour of the first legions. A skeleton (thin bone limbs, a big skull under a dented
  helmet with a broken crest stump), a notched gladius, a rotted crimson rag, a magenta sash (the accent). 300 tris.
  **34 HP, 3.7 m/s**, radius 0.36, contact 9, xp 2.5. **When it dies it collapses into a pile of bones** (no kill, no
  XP yet) and **2.5 s later it stands up again with half its HP**; the second death is final. A bone-coloured ring
  round the pile fills as it reassembles (not orange-red: the pile doesn't hurt you). **Counters:** **stomp the pile**
  (walk or slide over it: it scatters, and that's the kill, with its XP); or kill it while it's **burning** (Burn): no
  pile, the dead stay down; or let it fall into an open trapdoor. A swarm type late in the run. Its piles are capped
  (48 at once: past that they stay down).
- **Chimera** (`EN_Chimera`, new behaviour `Breather`): a lion with a goat's head rising from its back and a snake for
  a tail; dark-maned, its jaws glowing (800 tris, ~1.6 m at the shoulder, 2.6 m long). **420 HP, 3.0 m/s**, radius
  0.95, contact 14, knockback ×0.3, turns slowly (160°/s: flank it). Within 7 m it plants and a **cone of fire**
  (8 m, 60°) fills orange-red in front of it over **1.0 s**; then it **breathes for 1.0 s**: **24** to you if you're in
  the cone (once), **72 (×3) to every other enemy in it** (beasts don't care whom they burn), and **three burning
  patches** along the cone (r 1.4, 4 s, 10 dps). Then it stands smoking for 1.4 s: the window. Cooldown 4.5 s.
  **Counters:** get beside or behind it (it turns slowly), or out of the cone; lure it to breathe across the horde. Two
  at most alive, never an elite. From the lifts (4:00+) and the table.
- **Reused from earlier arenas:** the **War Boar** (cage beast of the Amphitheater) and the **Pit Cur** come up the
  lifts; the **Centurion** brings his Praetorian line through the gates (2 at most).

### Spawn table (`ST_Colosseum`)
Weights ramp linearly from `from` to the end of the run. Affixes: the Slave Pit's (as every table). Changes from the
Imperial Arena: the dead's weight climbs to the largest share; no Slinger, Fire-Pot Thrower, Gilded Mercenary (the
Chimera owns fire, the Archer owns range, gold theft is the Grand Arena's); the Centurion rarer.

| Enemy | From | Weight start → end | Cap | Swarm | Elite |
|---|---|---|---|---|---|
| Starving Convict | 0:00 | 6 → 1.5 | — | ✓ | ✓ |
| Pit Cur | 0:05 | 3 → 3 | 100 | ✓ | ✓ |
| Shield Thug | 0:20 | 1.2 → 1.0 | 24 | | ✓ |
| Shield Archer | 0:30 | 1.0 → 1.2 | 12 | | ✓ |
| **Skeletal Legionnaire** | 0:45 | 0.8 → 6.0 | 160 | ✓ | ✓ |
| Assassin | 0:50 | 1.0 → 1.3 | 14 | | ✓ |
| Murmillo | 0:50 | 0.6 → 0.8 | 10 | | ✓ |
| Net-Thrower | 1:00 | 0.7 → 1.0 | 10 | | ✓ |
| Centurion (+5 Praetorians) | 1:15 | 0.12 → 0.2 | 2 | | ✗ |
| Brute | 1:15 | 0.6 → 1.0 | 10 | | ✓ |
| Sorcerer | 1:30 | 0.6 → 1.0 | 10 | | ✓ |
| **Chimera** | 4:00 | 0.08 → 0.15 | 2 | | ✗ |

At 8:00 the Legionnaire is 6 of ~18 weight (a third of the steady horde) and over half of the swarms.

### Lift packs (`AL_Colosseum.liftPacks`)
Each lift in a wave picks its own pack (weighted among the active ones under their caps); the wave's call is the first
lift's.

| Pack | From → to | Weight | Count | Cap | Call |
|---|---|---|---|---|---|
| Pit Cur | 1:30 → 4:00 | 3 | 4–6 | 100 | "THE BEASTS RISE!" |
| War Boar | 2:30 → end | 2 | 2–3 | 12 | "THE BEASTS RISE!" |
| Chimera | 4:00 → end | 1 | 1 | 2 | "A CHIMERA!" |
| Skeletal Legionnaire | 4:30 → end | 3 | 5–7 | 160 | "THE DEAD RISE!" |

### Building blocks
- **Data:** `ArenaLayoutDef` hypogeum fields (trapdoors (x, z, yaw, group) and their size and model, lifts and their
  size and model, the centre lift, the drop and lift-wave clocks, `liftPacks`) and `swarmMul`; `EnemyBehaviour.Breather`
  (appended); `EnemyDef` revive fields (`reviveCount`, `reviveDelay`, `reviveHpMul`, `stompRadius`, `burnStopsRevive`,
  `pileMesh`) and breath fields (`breathAngle`, `breathTime`, `breathPatches`, `breathPatchRadius`,
  `breathKnockback`); `BossAttack.Disc` (appended) and the boss's new fields (docs/BOSSES.md).
- **Runtime:** `Hypogeum` (central tick: the drop schedule, doors, lifts and packs, falling enemies, throwing the player
  out, the boss's phases, the light); walk-only boxes in `StaticObstacles` (block walking, not shots) for doors left
  open; bone piles and the breath in `EnemyManager`; the Director's swarm multiplier; the boss's rise, falx throw and
  false death in `BossController` / `BossFight`.
- **Models (Blender):** `BlenderSrc/environment/colosseum.py`: `SM_ColFloor` (with the holes), `SM_ColCavea_0`–`_5`,
  `SM_ColGate`, `SM_ColGreatGate`, `SM_ColPulvinar`, `SM_ColStage`, `SM_ColStairs`, `SM_ColTrapdoor`, `SM_ColLift`,
  `SM_ColMons_A`, `SM_ColMons_B`, `PR_VictorStatue`; enemies `EN_SkeletalLegionnaire`, `EN_Chimera`, prop
  `PR_BonePile`; boss `EN_EternalVictor`.
- **Field-by-field list for each code part:** `docs/tracks/R2-design.md`.

### Tests
- Locked until the Emperor's Champion falls; then the menu offers it, and a run there is tier 5 (Renown ×3.5) with
  ×2.55 HP, ×1.55 damage, cap 450, density ×1.3 and swarms ×1.5.
- The oval is bigger than the Imperial Arena (`PlayArea()` ≈ 20,005 > 15,315, and ≥ 17,500) and keeps the horde in on
  every bearing and at all six gates.
- Nothing stands on a trapdoor or a lift: no chest, altar, spawn point, stair, statue, hill footprint or gate mouth
  within 1 m of one.
- The stage is walkable at 1.2 m and the hills at 2.2 m, and the horde climbs both.
- A drop telegraphs for 2 s with the doors shut, then opens for 4 s and closes: an ordinary enemy on it (or walking
  in) dies, an elite takes 300 and lives, an enemy just beside it is untouched; a grounded player on it is hit once
  (22 × 1.55) and thrown off, a jumping one isn't; the next drop is the nearest *other* group, 14 s after the last.
- A lift wave raises the 3 lifts nearest the player but none within 12 m, each with a pack from the active table,
  caps respected.
- A Skeletal Legionnaire collapses (no kill, no XP), stands up 2.5 s later with half HP, and its second death is a
  kill; a burning one stays down; a stomped pile is a kill and never rises; one that falls into a trapdoor stays down.
- A Chimera's cone telegraphs, then the breath hits a player in it once (not one beside it), hurts other enemies in it
  ×3 and leaves three fire patches.
- The Eternal Victor (docs/BOSSES.md): the rise, each attack, the guards at the lifts, the floor in each phase, the false
  death, the fall. The other bosses unchanged.
