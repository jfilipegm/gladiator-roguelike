# Gate 2 — "Is it fun?" report

*2026-10-19 · Phases 2, 3 and 4 complete: the vertical slice*

## 1. What works

**Verified** (by automated tests, bot runs, logged numbers or screenshots):
- **The full run loop:**
  - main menu → gladiator select (The Nobody, plus 3 locked silhouettes with hints) → arena intro → FIGHT!;
  - 8:00 of survival → Gorm → victory, or death at any point;
  - end screen (time, kills, level, Gorm's state, Renown earned) → Retry or Main Menu.
  - Checked by `RunFlowTests`, 10-minute bot runs and screenshots `m35_*`.
- **The Slave Pit:** a 42 m dirt pit with:
  - mounds, ramps and pit edges;
  - mud patches (slow) and fire pits;
  - a closed fence with two gates;
  - ~30 spectators and the instanced crowd.
- **Enemies:**
  - Six types: Convict, Pit Cur, Shield Thug, Slinger, Brute and Fire-Pot Thrower.
  - Three elite affixes: Armored, Fast and Explosive.
  - Off-screen wind-ups and elites show orange-red edge markers.
- **The Director (the system that spawns enemies over time):**
  - enemies on the field: 12 at the start → 300 by 5:00, capped at 300 in the pit;
  - a swarm from one side every 70 s;
  - an elite every 90 s.
- **Builds:**
  - **Weapons:** 4 in the pool with different attack patterns: Gladius (arc), Spear (thrust), Javelins (volley), Chakram (returning). Each levels 1–8.
  - **Cards:** 18 Boons, plus heal and gold filler cards.
  - **Statuses:** Burn, Bleed, Shock and Slow, with synergies (Conductive Oil, Open Wounds, Pyre, Static Charge, Blood Slide…).
  - **Level-up cards:** reroll, banish, skip and rarity.
  - **Map pickups:** chests (cost gold, price rises per chest) and the Blood Altar shrine.
- **Gorm, the Pit Master** (`BossTests`, screenshots `m34_*`):
  - **Entrance:** the horde clears, a camera turn, then Gorm comes through a gate.
  - **Attacks:** four, each with an orange-red telegraph.
  - **Guards:** waves come through the gates.
  - **Phase 2 at 55 % HP:** he roars (invulnerable), sets the fence's foot on fire and gets faster.
  - **Defeat:** "GORM FALLS!" in slow motion, then pick 1 of 3 Rare-or-better upgrades, then victory.
- **Save, Renown, Codex and one unlock** (`MetaTests`, plus the PlayMode test `BeatingGorm_UnlocksTheFlail_ForTheNextRun`):
  - Each run earns Renown and is saved.
  - Beating Gorm unlocks **Gorm's Flail**: a new Orbit weapon with spiked balls that circle you. It joins the level-up cards from the next run on.
  - The Codex lists your records and the unlocks: earned ones in full, the rest as "???" with a hint.
- **Phase 4 polish** (`FeedbackTests`, `OptionsTests`, screenshots `p4_*`):
  - **Sound:** placeholder sound effects through a mixer; a crowd loop that gets louder with excitement; cheers on spikes.
  - **Announcer:** kill-streak lines (KILLING SPREE! → UNSTOPPABLE!), STILL STANDING! after a close call, THE CROWD ROARS!
  - **Hit feedback:** an orange-red screen-edge flash when you're hit, and a pulse below 30 % HP.
  - **Options:** volumes, look sensitivity, invert Y, FOV, screen shake, render scale. They're saved between sessions.
- **The release build** (`Builds/Linux`, no admin menu) smoke-ran: menu → run → death → the run recorded, 0 exceptions.
- **Tests:** EditMode 50/50, PlayMode 70/70.

**Built but not verified by me:**
- **Fun, feel and difficulty for a human.** The bot isn't a human: it kites, collects and picks cards at random. This gate is for exactly this.
- **Sound by ear.** I can verify that sounds play, are rate-limited and are routed through the mixer, but not how they sound (C9).
- **A real gamepad and a real mouse** on the menus and cards. They use the same code paths as the tests, but no real controller or click has driven them.
- **Any machine but this one** (i5-12400F, RX 6600).

## 2. What to test

**Start:** run `Builds/Linux/Gladiator.x86_64`, the build a player would get.
- `Builds/LinuxDev/Gladiator.x86_64` is the same game plus the **F1 admin menu**: God mode, % buffs, **Timer to 0:10** (jumps straight to Gorm), spawn any enemy.
- Both builds use one save file: `~/.config/unity3d/SoloDev/Gladiator/save.json`. It doesn't exist yet, so your first run starts clean. Delete it later to start over. Admin-assisted runs count too.

**New controls** (movement and camera are unchanged since Gate 1):

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Open a chest / use the altar | E | North button |
| Level-up: pick | Mouse, 1–4, or A/D + Enter | Stick + South button |
| Level-up: reroll / banish / skip | R / B / X | West / North / Select |
| Pause → options / main menu | Esc → O / Q | Start |
| End screen: retry / main menu | R / M | South / East |
| FPS counter | F3 | — |

**Try, in this order:**
1. **A real run, no admin.** Menu → PLAY → The Nobody → FIGHT! Play until you die or win. Notes to bring back:
   - Where did it drag, and where did it spike?
   - When did your build feel "done"?
   - Did the level-ups come too often, or too rarely?
2. **Readability at 300 enemies (minutes 5–8).**
   - Can you still see what's hitting you, what's telegraphed, and where the gems are?
   - Is the pit too crowded to move (C1)?
3. **Chests and the altar.**
   - Chests cost gold, and the price rises each time.
   - The altar costs 15 % max HP for a Rare-or-better card. Was it ever worth it?
4. **Gorm (8:00).** His four attacks:
   - **Flail Sweep:** a ring around him; jump it.
   - **Whip Lash:** a lane; it slows you.
   - **Chain Slam:** a circle that tracks you.
   - **Charge:** a lane; he stuns himself if he hits the wall.
   - **Phase 2:** the fence catches fire and he's faster. Guards come through the gates every 22 s.

   Tell me: was the fight too short, too long, fair, and readable? Did you see his guards at all (C11)? If he's too hard to reach, use the dev build's Timer to 0:10.
5. **The unlock.**
   - Beat Gorm once: the end screen should show **UNLOCKED: Gorm's Flail** and your Renown.
   - Then main menu → CODEX: check what it shows.
   - Start a new run: the flail card should start appearing. Is the orbit weapon fun?
6. **Options.** Change a few, quit the game, and start it again: the settings should be kept. Is the default FOV (62) and look speed right?
7. **Sound.** Name the 2–3 worst sounds: too loud, grating or missing. The announcer: too often, or too rare (C10)?

The core question for this gate: **were the 10 minutes fun, and would you play another run?** A sentence per item above is plenty.

## 3. Tunables

Every value lives in an asset under `Assets/Data/`, so you can edit it in the Inspector, even in Play mode. Changes to assets stay after you stop Play.

| Asset | Fields (current value) | What it changes |
|---|---|---|
| `Config/ProgressionConfig` | `exponent` 1.6, `perLevelXp` 5, `baseXp` 5; `magnetRadius` 2.2; `runDuration` 480 | Level-up pace (C8). If the middle feels slow, try an `exponent` of 1.5. Run length. |
| `Config/DirectorConfig` | `targetAlive` curve (12 → 300 at 5:00 → 460); `spawnPerSecond` 4 → 40; `swarmEvery` 70; `eliteEvery` 90 | Pressure over time |
| `Arenas/AL_SlavePit` | `maxAlive` 300 | Crowding in the pit (C1). Try 220–250 if it's a wall of bodies. |
| `Arenas/ST_SlavePit` | per-type weight and cap (Slingers 10, Fire-Pots 6) | Enemy mix |
| `Enemies/EN_Gorm` · `Bosses/BD_Gorm` | `maxHp` 9,000; `guardsEvery` 22; attack damage and wind-ups (`sweepWindup` 1.1, `slamWindup` 1.0…); `enrageSpeed` 1.3 | Boss length, difficulty and readability (C11) |
| `Config/UpgradeConfig` | rarity weights 62 / 26 / 10 / 2; magnitudes ×1 / 1.4 / 1.8 / 2.5 | Card quality |
| `Upgrades/UP_*`, `Weapons/WP_*` | each card's numbers and each weapon's level table | Build strength (C7) |
| `Config/InteractableConfig` | chest `basePrice` 15, `priceGrowth` 1.4, `respawnDelay` 35; `altarMaxHpCost` 0.15 | Gold economy, altar risk |
| `Weapons/WP_GormsFlail` | `damage` 12, `amount` 2 balls, `range` 2.6, `speed` 9, `cooldown` 0.55 | The unlock's strength |
| `Config/AnnouncerConfig` | streaks 40 / 80 / 140 / 220 kills in `window` 4 s; `minGap` 6; `streakCooldown` 20 | How often the announcer speaks (C10) |
| `Audio/SfxBank` | per-sound volume, pitch jitter, repeat interval; `crowdQuiet` 0.18 / `crowdLoud` 0.75 | Sound balance (C9) |
| `Config/HitFeedbackConfig` | `hurtFlashAlpha` 0.32, `lowHpThreshold` 0.3, `lowHpPulseAlpha` 0.2 (plus the Gate 1 fields) | The hurt overlay |
| `Meta/MetaConfig` | Renown: 4 / min, 2 / 100 kills, 40 / boss, 20 / win | Renown pace |
| `Config/MovementConfig`, `Config/CameraConfig` | unchanged since Gate 1 | Feel |

## 4. Measurements

All numbers are from the PlaytestBot on this PC (i5-12400F, RX 6600):
- dev build at 1920×1080;
- rendered on the real GPU through a headless compositor (no window);
- uncapped frame rate.

| Run | Enemies | FPS (avg) | Frame ms avg / p95 / p99 / worst | Gameplay ms avg / p95 |
|---|---|---|---|---|
| Full run, 10 min (menu → pit → Gorm) | up to 300 | 480 | 2.08 / 3.80 / 4.74 / 19.0 | 0.51 / 1.14 |
| … only frames with ≥ 250 enemies | 250–300 | 247 | 4.05 / 6.25 / 7.13 | 1.23 / 1.63 |
| Stress, Slave Pit, 60 s | 1,000 | 111 | 8.97 / 19.4 / 22.2 / 56.6 | 3.11 / 4.58 |

- **Budgets** (CLAUDE.md §8):
  - **60 FPS with 300 enemies:** met with a ~4× margin.
  - **No crash at 1,000:** met, still above 60 fps at p95.
  - **Gameplay ≤ 4 ms:** 1.2 ms at 300 enemies. At 1,000 it's 3.1 ms on average, and its p95 of 4.6 ms is just over budget.
  - **Pools:** 0 pool growths, with a maximum of 300 alive.
- **The bot's run:**
  - level 28, 9,653 kills, 7 chests;
  - Gorm DEFEATED, with the run ending at 8:42;
  - +288 Renown.
  - That save was the bot's own file, deleted afterwards.
- **GC** (target 0 B per frame):
  - **Steady state:** 0 B per frame.
  - **One-off allocations during the 10-minute run:** about 14 KB in total. The first level-up was ~7.5 KB (the game is paused then) and the boss entrance 1.8 KB.
  - **The victory frames:** ~238 KB for the end screen and the save write, after the run is over.
  - **The stress run:** 10 KB in 57 s, mostly the first level-up.
- **Tests:** EditMode 50/50, PlayMode 70/70. The PlayMode tests run on a sandbox save, and I checked that your real save is untouched.

## 5. Top concerns

| # | Concern | Recommendation |
|---|---|---|
| C11 *(new)* | **Gorm's fight length depends heavily on the build.** Across three bot runs:<br>• the final run's level-28 build killed him in about 40 s, entrance included, before his first guard wave;<br>• a level-40 build left him at 63 % after about 2 minutes;<br>• a level-47 build did 21 % in 20 s.<br>A strong human build could skip half his moves. | Judge it in your run. If he melts, raise `EN_Gorm.maxHp` from 9,000 to ~15,000, and start the first guard wave sooner. A damage-taken cap (e.g. at most 4 % HP per second) would make the fight length predictable. I'd add the cap only if raising his HP isn't enough. |
| C8 | Pacing: the steeper XP curve now ends a strong run at level 27–28, with the build nearly complete when Gorm arrives. | Tell me where the middle minutes dragged; lower `exponent` to 1.5 if they did. |
| C1 / Q2 | 300 enemies in a 42 m pit may become a wall of bodies. | If it does, set `AL_SlavePit.maxAlive` to 220–250 and keep the pressure through enemy HP and speed. |
| C9 | No sound has been heard by anyone. | Name the worst 2–3; I'll fix those from `SfxBank`. Real audio stays a later task. |
| C10 | The announcer's frequency follows your kill rate. A weak build hears little; a monster build hears a line every 6–20 s. | Judge in play; the thresholds are in `AnnouncerConfig`. |
| C6 | Ranged enemies (Slingers, Fire-Pots) never close in, so a melee build has to walk out to them. | They're capped at 10 and 6. If chasing them is annoying, I'll make them advance after 20 s alive. |
| C7 | Every card number is a first guess. | Name the cards that felt useless or broken. |

## 6. Proposed next phase

**First:** a small tuning commit from your notes. Then **Phase 5** in this order:
1. **Favor:** gifts (healing amphora, gold shower, a weapon in the sand) and whims (an extra gate opens when you kite passively).
   - It's the identity hook, and `CrowdExcitement` already feeds it.
2. **The Pit Goblin**, with its discovery unlock: a rattling crate hidden in the Slave Pit.
   - It's the most different gladiator from the Nobody, so it proves the character system. It also proves secret unlocks.
3. **The Blood Knight** (unlock: reach Arena 3), then **Arena 2, the Provincial Pit**, with barricades, braziers, spike traps, three gates and its champion.
   - Arena 2 proves kit reuse and arena-specific rules.

**A decision I need from you (Q1, run structure):** one arena per run, or a chain of arenas per run?
- My recommendation is **one arena per run**:
  - it keeps sessions at ~10 minutes, like Megabonk;
  - balance stays per-arena;
  - the climb happens across runs, through Renown and unlocks.
- A chain would make runs 20–40 minutes and the balance much harder.
- Both are already just data (`RunDef`). I'd build Arena 2 as its own run unless you say otherwise.

**No other scope changes.** Nothing from Phase 5 is started.
