# R1 contract — Arena 5, the Imperial Arena + the Emperor's Champion

**Item branch:** `phase5-imperial-arena`, cut from `main` once v0.16.0 is merged. Worker branches merge into it; the
orchestrator does every merge. **Release:** v0.17.0. **Source:** the "Arena 5 design notes" in `docs/PROGRESS.md`
(summarised here) and CLAUDE.md §6.2 row 5. The arena is "One of the greatest alive": gold and crimson, imperial
purple, ceremonial; coordinated formations; a multi-phase boss; imperial events.

## Shared facts (every part uses these numbers and names)
- **Shape, a hippodrome along X, metres, y up.**
  - The **wall line**: straight sides at z = ±44, from x = −95 to x = 51.
  - The **west end** (x = −95) is a straight wall: the **carceres**, a row of arched starting stalls.
  - The **east end** is a semicircle of radius 44 around (51, 0), reaching x = 95: the **sphendone**. Its apex (95, 0)
    holds the **triumphal gate**, where the boss enters.
  - Enemies play inside the wall line inset by 1.1 m.
  - Floor area about 15,900 m², minus the spina ≈ 15,300 m² (1.31× the Grand Arena's 11,674 m²).
- **The spina**, along z = 0, from x = −60 to x = 40:
  - a raised walkway 1.2 m high and 6 m wide, with stairs on both long sides;
  - an **obelisk** at x = −10 (a line-of-sight blocker);
  - the **metae** at both ends, three gold cones each, centred at (−60, 0) and (40, 0);
  - a **lap counter**, seven gold eggs on a frame, at x = 20.
- **The track lane** (the chariot races):
  - a loop 4 m wide whose centre line is 14 m from the spina's axis;
  - straights at z = ±14 from x = −60 to x = 40, joined by half-circles of radius 14 round (−60, 0) and (40, 0);
  - drawn darker on the floor at all times, so it reads as "the track".
- **Palette:** A6 (travertine, travertine_d, purple, purple_l, purple_d) for stone and cloth; A5 (gold, gold_d, gold_l,
  crimson, crimson_d) for trim and the boss. Anything that hurts the player is orange-red (CLAUDE.md §5).
- **Stands:** the same profile as the Grand Arena (`GrandArenaLayout`: podium 3.2 m, cap 0.6, 8 tiers rising 0.45 and
  running 0.8), so the crowd-zone code is reused. Sections are 4 m wide and get scaled to fit the run.
- **Model names** (FBX under `Assets/Art/Models/`):
  - **Environment/:** `SM_ImpFloor`, `SM_ImpStand_4m`, `SM_Carceres` (one 4 m stall bay), `SM_ImpGate` (side gate,
    hinged `DoorL` / `DoorR` children like `SM_GrandGate`), `SM_TriumphalGate` (the boss gate, same door children),
    `SM_Pulvinar` (the imperial box, north side, ~14 m wide), `SM_Spina_10m`, `SM_SpinaEnd` (metae), `SM_SpinaStairs`,
    `SM_Obelisk` (~12 m tall), `PR_LapCounter`.
  - **Props/:** `PR_Chariot` (a quadriga, ~700 tris).
  - **Enemies/:** `EN_Praetorian`, `EN_Centurion` (≤ 300 tris each), `EN_EmperorsChampion` (≤ 2,500).
  - Follow the folders and conventions the existing models use (read where `SM_GrandGate`, `EN_GildedPatron` and
    `EN_Murmillo` live).
- **Ids:** arena `imperial_arena`; boss `emperors_champion`; enemies `praetorian`, `centurion`; unlock
  `UL_ImperialArena` (beat the Gilded Patron, boss id `patron`). Note `champion` is already the Provincial Champion.

## Parts
**Wave 1 (now: design and art, no code):**
| Part | Owns | Done when |
|---|---|---|
| **design** | `docs/BOSSES.md` § "The Emperor's Champion"; `docs/ARENAS.md` § "5. The Imperial Arena"; `docs/tracks/R1-design.md` | The boss has CLAUDE.md §6.4's 10-line spec: silhouette, attacks with telegraphs and numbers per phase, the 3 phases (60 % and 30 %), arena interaction (chariots, Praetorian formations as guards), entrance, personality. The arena section has the shape, the chariot race rules and numbers, the Praetorian and Centurion stats and formation rules, the spawn table, difficulty multipliers, Renown, gates and hazards, all consistent with the shared facts. |
| **env-art** | `BlenderSrc/environment/imperial_arena.py`, every Environment model above, their `.blend`, reviews, `docs/tracks/R1-env-art.md` | Every piece is exported and reviewed. Budgets are close to the Grand Arena's equivalents (read `grand_arena.py`). The doors pivot at their hinges. The floor's track lane is visibly darker. |
| **char-art** | `BlenderSrc/enemies/en_praetorian.py`, `en_centurion.py`, `en_emperorschampion.py`, `BlenderSrc/props/pr_chariot.py`, their models, `.blend` files and reviews, `docs/tracks/R1-char-art.md` | All four within budget, rigid parts with joint pivots (part names like the existing enemies'), 3 angles + 64 px reviews. The chariot has separate wheels and horses, so code can spin and bob them. Each silhouette reads distinct from every other enemy at 64 px. |

**Character looks:**
- **Praetorian:** a tall curved rectangular scutum, a purple crest, travertine and purple armour.
- **Centurion:** a transverse crest running side to side (his silhouette), a red cloak and a vine staff.
- **The Emperor's Champion:** gold and crimson armour, a purple cloak and an eagle-crest helmet, with a spear, a scutum,
  and javelins across his back. He uses all three weapons in his attacks.
- **Chariot:** gold and crimson car with orange-red plumes on the horses (it hurts), four horses, one driver.

**Wave 2 (code, after v0.16.0 merges; contracts written then, from the design):**
| Part | Will own |
|---|---|
| chariot races | `ChariotRace.cs` (new) + tests; the chariot fields in `ArenaLayoutDef.cs`; the hook in `ArenaBuilder.cs` |
| formations | `EnemyDef.cs`, `EnemyManager.Leash.cs` (formation slots in the leader's frame, `freeEscortsOnDeath`) + tests |
| boss | `BossDef.cs` (`phase3At`, `phase3Attacks`), `BossController.cs`, `BossSetup.cs` + tests |
| layout and setup | `ImperialArenaLayout.cs`, `ImperialArenaSetup.cs` (new): layout, run, unlock, spawn table, enemy data |
