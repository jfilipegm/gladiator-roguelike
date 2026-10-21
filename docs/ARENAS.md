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
_Spec in progress (docs/tracks/R1-imperial-arena.md)._
