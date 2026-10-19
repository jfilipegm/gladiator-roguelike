# Arenas

Each arena changes gameplay, not just textures (CLAUDE.md §6.2). An arena = an `ArenaLayoutDef` (pieces, gates, zones,
crowd, anchors, its spawn table, its boss) built into a scene by `ArenaBuilder`, plus a `RunDef` the menu offers.
One arena per run (DECISIONS, Q1): the climb happens across runs, through unlocks.

## 1. The Slave Pit (built, slice)
~42 m dirt pit, earthen walls, 2 gates, 3 terraces with ramps, mounds, mud (slows), fire pits, cages; ~40 locals.
Boss: Gorm, the Pit Master. Layout: `SlavePitLayout.cs` → `AL_SlavePit`.

## 2. The Provincial Pit (Phase 5.4, v0.5.0)
**Player feels:** "People notice me." A real arena at last, small and provincial: a stone foundation with wooden
stands full of shouting, betting townsfolk.

| | |
|---|---|
| **Unlock** | Beat Gorm once (`UL_ProvincialPit`, `DefeatBoss gorm`). Then the menu asks which arena after the gladiator. Locked: *"Beat the Pit Master and the provinces will want you."* |
| **Size / layout** | ~50 m across (play radius 24.5 m vs 20.4). A sand floor ringed by a **stone foundation wall** (2.4 m) with **wooden stands** on top. **3 gates** (north, south-east, south-west), so enemies come from three sides. |
| **Vertical variety** | Two raised stone **platforms** (1.2 m) with stairs, one long ramp, and a low central dais. |
| **Pillars** | Six stone pillars: they block movement, shots and line of sight. The Champion's charge stuns him against one. |
| **Barricades** | Eight wooden barricades in short lines: they split the floor into lanes and choke points. Enemies walk around them. **The Champion's charge smashes any barricade in its path** (gone for the run), so the arena opens up as the fight goes on. |
| **Spike traps (new rule)** | Six iron grates in the floor. Every ~7 s each one telegraphs (orange-red, 1 s), then spikes shoot up for 1.2 s. **They hurt everyone**: the player (15) and enemies (40, ×2 for ordinary ones), so luring the horde over a grate is a tactic. Feet above 0.5 m clear them (jump). Traps are staggered so there's always one live. |
| **Braziers** | Four lit braziers on the stone platforms and by the gates: light and set dressing. If the Champion charges through one, it topples and leaves burning ground for 8 s. |
| **Enemies** | The Slave Pit's six, with tougher odds, plus two new ones: the **Shield Archer** (shield-protected archer) and the **Assassin** (flanker). Enemies have +25 % HP and +15 % damage here (`ArenaLayoutDef.enemyHpMul / playerDamageTakenMul`). |
| **Crowd** | ~200 spectators in the stands (instanced, as before). Cheers on streaks; chants when the Champion enters. |
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
