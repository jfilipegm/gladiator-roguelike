# Concerns

Design / scope / perf / readability problems spotted, each with a proposed fix. Status: OPEN · WATCH · RESOLVED.

## C1 — Slave Pit size vs. 300 enemies (OPEN)
A ~35 m pit has ~960 m² of floor. 300 enemies at ~0.45 m radius cover roughly 20 % of it, before mounds, mud and fences. With slide momentum and a third-person camera, that risks a "wall of bodies" where positioning (the player's main job) disappears.
**Proposal:** make the Slave Pit's *playable* floor ~42 m across, and give the Director a per-arena `maxAlive` (Slave Pit ≈ 200–250, rising in bigger arenas). Hold the 300-enemy perf budget in the stress test and the larger arenas. Pressure in the pit comes from enemy HP/speed instead of raw count. Decide after M2.3 with real numbers.

## C2 — Off-screen threats with a free-orbit camera (MITIGATED in M2.2)
With no lock-on and auto-aim, the player will often look away from enemies closing in from behind.
**Done:** `ThreatIndicators` — up to 16 screen-edge chevrons: pulsing orange-red for any wind-up (Slinger, Fire-Pot, Brute), affix colour for elites. Not shown for plain melee swarmers on purpose (noise). **Still to judge in playtest:** whether 16 is too many in a 300-enemy swarm, and whether the default camera pitch should be higher.

## C3 — Camera squeeze against thin obstacles (WATCH)
When the player backs onto a pillar with the pillar between them and the camera, the only clear spot is right behind the player's head (≈0.45 m). That's handled: the player turns shadows-only below 0.75 m. It can still feel cramped. Arena 3 uses pillars as line-of-sight blockers, so watch it there.
**Proposal if it bothers at Gate 1:** fade small occluders (dithered transparency on a shared material) instead of pulling the camera in.

## C4 — Measuring performance (RESOLVED in M1.2)
Editor Play-mode frame times swing between ~18 fps and ~180 fps depending on focus and bridge traffic, so they're useless for the §8 budget.
**Done:** `BuildScripts.BuildLinuxDev` produces a standalone development build; `-bot -profile` / `-stress -stressCount N` runs write the CSV + summary. Gate reports quote those numbers only. Caveat: they're from this PC (not a "mid-range" reference machine).

## C5 — Enemy rendering cost at 1,000 enemies (WATCH)
Each enemy is a pooled GameObject with ~6 rigid-part MeshRenderers. At 1,000 enemies frame time is bimodal: ~6 ms when the crowd is partly off-screen, ~15–16 ms when the camera faces all of it (draw submission, not gameplay; gameplay is 3.5 ms). 500 enemies stays at ~5 ms / p95 8 ms, so the §8 budget (300 → 500 peak at 60 fps) holds with a wide margin on this PC.
**Proposal if an arena really needs 1,000+ on screen (Colosseum):** draw enemies with `Graphics.RenderMeshInstanced` per (enemy type × part), with part matrices computed in the central tick (the SoA state already holds everything); keep GameObjects only for elites/bosses. Decide in the Phase 4 perf pass with real arena numbers.

## C6 — Kiting enemies pile up (MITIGATED in M2.3)
Slingers and Fire-Pot Throwers keep 6–11 m away, so a melee build rarely reaches them. In a 60 s mixed stress run they accumulated until their pools ran dry. In real play that becomes a wall of ranged fire nobody can clear.
**Proposal (M2.3 Director):** a per-type max-alive in the arena's spawn table (ranged/area types low, e.g. ≤ 8–12 each in Arena 1), and ranged enemies slowly advancing when they've been alive too long. Weapons with reach (Spear, Javelins in M2.5) are the player-side answer.
**Status (M2.3):** the Slave Pit table caps slingers at 10 and fire-pots at 6; the 8-minute Director run never exceeded them. Still open: ranged enemies never advance, so a melee-only build has to walk out to them (watch at Gate 2; reach weapons arrive in M2.5).

## C7 — Upgrade numbers are first guesses (NEW in M2.4)
Every Boon step (+12 % damage, +10 % attack speed, +20 max HP…), the rarity odds (62/26/10/2) and magnitudes (×1/1.4/1.8/2.5) are starting values, not tuned. With 11 stat-only Boons and one weapon, picks feel like small number bumps until M2.5 (weapons and weapon levels) and M2.6 (behaviour Boons) give choices real texture.
**Proposal:** keep everything in `UpgradeConfig` / `UP_*` assets (done), and judge it at Gate 2. The admin panel's UPGRADES column gives any Boon at any rarity without waiting for it to roll.

## C8 — Builds max out by minute 6 (TUNED in Phase 4 — judge at Gate 2)
The 8-minute bot run (M2.7) reached **level 38** with 6,279 kills and opened 6 chests: 43 picks for 10 slots (4 weapons up to 8, 6 Boons up to 5), so the build is complete around 6:00 and the last minutes offer only filler cards (heal / gold). The XP curve (`ProgressionConfig`) was set for M1.3's single weapon; four weapons and synergies kill far faster, so XP floods in.
**Proposal:** in the Phase 4 balance pass, steepen the level curve so a strong 8-minute run ends around level 25–30 (build nearly, not fully, complete when Gorm arrives), and re-check with the bot's end-of-run summary (level, kills, chests). Not changed now: the curve should be tuned against Arena 1 and Gorm, not the test arena.
**Phase 4:** curve steepened to `5 + 5·(L−1)^1.6`; the 10-minute bot run now ends at level 27 (7,895 kills, 7 chests) with 6 weapon/Boon slots still short of max. Judge the pacing in the playtest: if the middle minutes feel slow, lower the exponent to 1.5.

## C9 — Placeholder audio is unheard (NEW in Phase 4)
Every sound is synthesised and its volume, pitch jitter and repeat limit are first guesses. I can verify that sounds play, are rate-limited and route through the mixer, but not how they sound or balance against each other.
**Proposal:** at Gate 2, note the 2–3 worst offenders (too loud / grating / missing); levels live in `Assets/Data/Audio/SfxBank.asset`, the crowd loop's range in the same asset (`crowdQuiet`/`crowdLoud`). Real audio stays a later task.

## C10 — Announcer frequency depends on kill rate (NEW in Phase 4)
The kill-streak ladder (40 / 80 / 140 / 220 kills in 4 s) was set against the bot's kill rates (~15–25 kills/s late in a run). A weaker build hears few lines; a monster build hears one every ~6–20 s.
**Proposal:** judge in the playtest; everything is in `Assets/Data/Config/AnnouncerConfig.asset` (thresholds, window, minimum gap, ladder reset).


## C11 — Gorm's fight length swings with the build (NEW at Gate 2)
Three bot runs against the same 9,000 HP Gorm: a level-47 build did 21 % in 20 s (M3.4), a level-40 build left him at 63 % after ~2 minutes (M3.6), and the final level-28 build (Phase 4) killed him ~40 s after the 8:00 mark, entrance and reward included, before his first guard wave. A strong human build could skip half his moves; a weak one faces a long slog.
**Proposal:** judge in the Gate 2 playtest. If he melts, raise `EN_Gorm.maxHp` to ~15,000 and bring the first guard wave forward. If the swing is still wide, cap the damage he takes per second (e.g. 4 % HP/s) so every fight shows both phases and the guards.

## C12 — The Blood Knight's kill heals may make him unkillable mid-run (NEW in Phase 5.3)
The 10-minute bot run (God mode, so it couldn't die) healed 3,900 HP from kills: about 7 HP/s on average, i.e. the 6 HP/s cap nearly every second, doubled during his 27 Rites. With 120 max HP, that's a full heal every ~17 s while the horde is thick. The halved "other healing" doesn't matter when kills do all the work. On the other hand, Bloodlust wants him at low HP, so a knight who is always full also never hits his hardest.
**Proposal:** judge in the playtest. If he never feels in danger, lower `TR_Bloodthirst.maxHealPerSecond` to ~3–4 and/or `frenzyHealMul` to 1.5. If he dies to the first swarm, raise `healPerKill` (it matters early, when kills are slow), not the cap.

## C13 — The Champion may be a slog for builds that can't get behind him (NEW in Phase 5.4)
His shield takes 90 % of frontal damage and he turns slowly toward you, so a single-target weapon build that can't reposition does a tenth of its damage most of the fight. The counters are positioning, damage over time, blasts beside him, and luring his charge into stone (+50 % for 2 s). That is the fight's identity, but untested: 13,000 HP is a guess. Data point: in the first 10-minute bot run (immortal bot, level 31, gladius + chakram + bombs + Gorm's flail) he was at 24 % after about a minute of fight.
**Proposal:** judge in the playtest. If it drags, lower `EN_Champion.maxHp` (~10,000) or `blockReduction` (0.8) before touching the stun, which is the fun part; if he melts, raise HP first.

## C14 — Hit-stop adds up in long fights (NEW in Phase 5.4)
The 10-minute bot run in the Provincial Pit logged 1,113 hit-stops totalling 45.9 s of frozen time (≈ 7.6 % of the run, ~1.9 per second). Each one is short, but late-run crit builds against elites and a boss trigger them almost continuously, which may read as stutter rather than impact.
**Proposal:** judge in the playtest. If it stutters, raise `HitFeedbackConfig.hitStopInterval` (fewer stops) before shortening each stop, or restrict hit-stop to crits on elites and bosses.

## C15 — The bigger Slave Pit may thin the pressure (NEW, playtest fixes)
The Slave Pit is now 84 × 70 m (was a 42 m circle) with the same spawn curve and the same 300 cap. Enemies spawn on the oval's edge away from the player, so they walk further to reach you, and the horde is spread over ~3.5× the floor area. The bot run measures frame time, not how crowded it feels.
**Data (bot run 3, 2026-10-20):** alive enemies per minute 53 / 78 / 145 / 220 / 254 / 264 / 283 / 299, 300 reached at 5:24 (the §8 target is 300 by minute 5); the bot's kills and level match the old pit's runs once it fights instead of kiting. So the totals hold; what the bot can't tell is whether the first two minutes (under 80 enemies on 3.5× the floor) feel empty.
**Proposal:** judge in the playtest. If the first minutes feel empty, raise the early budget in `DirectorConfig` (or the Slave Pit's `maxAlive`) or lower `DirectorConfig.ringInset`'s distance from the player (`minPlayerDistance`) so spawns land closer, before shrinking the arena again.

## C18 — Big arenas may thin out the horde (NEW, v0.8.0)
- **What changed:** the Provincial Pit's play area went from 1,870 to 6,274 m² with the same 300-enemy cap. The Director spawns on the arena's edge, so on a big floor more of the 300 are walking in at any moment.
- **Done:** `ArenaLayoutDef.spawnMaxDistance` (55 m here) keeps ring spawns on the near side; four gates instead of three.
- **Measured early (v0.8.0 smoke runs, 40 s):** the Nobody killed 56 in the Slave Pit but 31–38 in the new Provincial Pit, so the opening fills up more slowly. Over 10 minutes the bot's kills are capped by its own DPS, so they can't show pressure. **Proposal:** the producer's playtest decides; if it feels empty, raise `maxAlive` here (300 → 380, within the 500 peak budget) or lower `spawnMaxDistance` to 40. Every later arena is bigger again, so this needs an answer before Arena 3.
- **More data (v0.12.0, the Golden Idol in the Stone Amphitheater):** a strong build (Coin Toss, Storm Spear, flail, Pyre) kept the floor under 180 alive for most of the run (52 / 90 / 119 / 117 / 157 / 125 / 179 / 134 at the end of minutes 1–8): on the big floor the horde arrives slower than a good build kills it.
- **Measured in Arena 3 (v0.9.0 dev build, two 10-minute bot runs, 320 cap, ring spawns within 55 m):** enemies alive at the end of minutes 1–8 were 50 / 104 / 154 / 208 / 262 / 255 / 276 / 319 for the Nobody (300 first reached at 6:13 of real time) and 54 / 99 / 116 / 132 / 236 / 258 / 193 / 206 for the Blood Knight, who kills faster (300 at 7:10). The §8 target is 300 by minute 5, so the bigger floors fill up about a minute late. Same proposal: the playtest decides between a higher early budget, a higher cap, and closer spawns.

- **Phase 5.13 (the Grand Arena):** answered for the new arena by default: `ArenaLayoutDef.densityMul` 1.2 (the Director's target curve and spawn rate) under a 400 cap. Arenas 1–3 keep 1.0. If the playtest says the Amphitheater feels empty, give it 1.1 and a 360 cap: data only.

## C17 — The Renown economy is a guess (NEW, Phase 5.6)
- **What's guessed:** Market Boons cost 300 Renown, and challenge bounties pay 100–200. A winning Slave Pit run earns about 150–290 Renown, kills dominate it (2 per 100 kills), and the Provincial Pit pays ×1.5.
- **The risk:** if kill counts grow (evolved builds kill 9–10 k a run), the Market empties in two or three runs and Renown loses its use again.
- **Proposal:** after the producer's playtest, either cap the kill share of Renown or add Renown sinks that scale: run modifiers and cosmetics (BACKLOG). Tunables: `MetaConfig.renown*`, `UnlockDef.renownCost` / `renownBounty`.

## C16 — Evolutions may make the boss easy (NEW, Phase 5.5)
Evolutions arrive after 8 weapon levels plus a catalyst plus a chest, so late in the run, right before the boss. A Thunderbolts + Storm Front build could melt Gorm. **Proposal:** measure the boss fight's length in bot runs with and without evolutions. If it halves, give bosses an evolution-aware HP scale (`BossDef.hpPerEvolution`) rather than nerfing the evolutions.
- **Measured (v0.6.0 dev build, Slave Pit, `Tools/perfrun bot 600 -timed`, 2026-10-20):**
  - **With `-evolved`** (4 evolutions from the first frame: Champion's Gladius, Fire-Pot Barrage, Twin Moons, Sanguine Reaver; Wildfire, Bloodbath and Executioner on): Gorm died about 20 s after his entrance, in phase 2 after 5 attacks. Over the run: 2,033 executions, 886 Wildfire spreads.
  - **Without** (the bot's random build, no weapon above level 5, 0 evolutions, Juggernaut only): Gorm was at 68 % in phase 1 after about 57 s. In v0.5.0's run 3 a similar build beat him in the last second of the recording.
- **Reading:** the "halves" line is crossed, but the `-evolved` build is far beyond a real run (4 evolutions at minute 0, against 0 in the natural run). **Recommendation:** no `hpPerEvolution` yet. Let the producer's playtest decide with real runs (0–2 evolutions), and revisit if a real run with 1–2 evolutions kills Gorm in under 30 s. Executioner may be the strongest piece (2,033 kills in one run); it's the first number to turn down.

## C19 — The Beast Lord's HP and his beasts' XP are untested against real builds (NEW, Phase 5.8)
- **HP:** 15,000 is the Champion's 13,000 plus a little, with no shield to hide behind but a pounce that takes him out of reach. Only scripted damage has hit him so far; the bot runs measure how long a real build takes.
- **XP:** every boar is an enemy with 3 XP and gold, and he releases 3–5 every 20 s, then 12 at once in phase 2. A strong build may farm several levels during the fight, which the boss reward (pick 1 of 3 Rare+) doesn't need on top.
- **Measured (v0.9.0 dev build, two 10-minute bot runs, immortal bot, no evolutions):** the Nobody (level 32, Gorm's flail L7, javelins, chakram) had him at 49 %, in phase 2, about 55 s after his entrance when the recording ended. The Blood Knight (level 28, greatsword, whip, chakram, Juggernaut + Bloodbath) killed him about 40 s after his entrance, in 12 attacks and one beast call. So the fight runs from ~40 s to ~2 min with no evolutions, in line with the Champion.
- **Proposal:** judge in the playtest. If he melts, raise `EN_BeastLord.maxHp`; if he drags, lower it or lengthen `pounceRecover` (a longer window to punish him). If the boss fight levels you up twice, set the boars' `xp` to 0 while a boss is up (a flag on the spawn), not in the rest of the run.

## C20 — The Verdict may be too strong, or the Murder Machine too slow to kill (NEW, Phase 5.9)
- **Measured (v0.10.0 dev build, 10-minute bot run in the Slave Pit, immortal bot that jumps every few seconds):** 46 Verdicts hit 1,455 enemies in total (about 32 per Verdict, one every ~12 s), and he took 430 HP of damage in the whole run with the plate on. The bot never levelled the cleaver (it stayed at level 1) and Gorm was at 65 % when the recording ended. In the Stone Amphitheater: 60 Verdicts, 2,903 hits (~48 each), 337 HP taken, the Beast Lord at 35 % in phase 2 at the end.
- **The risk:** a horde wipe every 12 s plus a −4 plate may make him the easy mode. Or the slow, forward-only cleaver may make him the slow mode when the Verdict isn't ready.
- **Proposal:** judge in the playtest. Tunables: `TR_Verdict` (`perHit` 8, `radius` 7, `damage` 60, `knockback` 14), `TR_ExecutionersPlate.flatReduction` (4), `WP_ExecutionersCleaver` (34 damage, 2.1 s, `width` 2.4), `GL_MurderMachine` (150 HP, 85 % speed). If he's too safe, raise `perHit`'s cost first (fewer Verdicts), before touching the plate.

## C21 — The Golden Idol may be weak until he's rich, or snowball once he is (NEW, Phase 5.11)
- **The risk:** at 0 gold he's a Nobody with a weaker weapon; at 1,000+ he does double damage on top of every other multiplier. A run that buys chests stays weak; one that hoards may trivialise the boss.
- **Proposal:** judge in the playtest. Tunables: `TR_MidasBody` (`goldPerPercent` 10, `maxBonus` 1), `TR_MakeItRain` (`goldCost` 40, `damage` 20), `TR_GoldenStreak.goldByTier`, `WP_CoinToss`. If he snowballs, lower `maxBonus` before the rate; if he's weak early, give the Coin Toss a 4th coin at level 1.

## C22 — The Ashen Oracle's curses may be too slow, or Contagion may chain through the whole horde (NEW, Phase 5.12)
- **The risk:** curse damage takes seconds to stack, so early waves may overrun him; later, a cursed death bursts and curses its neighbours, whose deaths burst again, which could wipe a packed horde in one chain (`maxBurstsPerFrame` 12 caps the frame cost, not the chain).
- **Proposal:** judge in the playtest. Tunables: `SE_Curse` (duration 4, maxStacks 5), `WP_AshenCenser` (damage 8, `hitEffects` powers), `TR_Contagion` (burst power 6, radius 3.5), `TR_SmokeVeil`. If chains wipe hordes, lower the burst radius first.
- **Measured (v0.13.0 dev build, two 10-minute bot runs as the Oracle):** 8,474 of 9,723 kills (Slave Pit) and 8,889 of 9,705 (Stone Amphitheater) were cursed-death bursts, and the horde rarely reached 250 alive; both bosses fell. So the chain is real and strong. Recommendation: try `TR_Contagion` radius 3.5 → 2.5 in the playtest if he feels like easy mode.

## C23 — The Grand Arena's hazards may be unfair, or ignorable (NEW, Phase 5.13)
- **The risk:** a fire-jet row is a 49 m wall you can't jump, and the four rows cycle every 10 s; the blades sweep 5.5 m circles round the dais. With 400 enemies on screen the telegraphs may get lost, or a player who stays in the plaza may never meet a jet.
- **Proposal:** judge in the playtest. Tunables: `FireJets` (`period` 10, `telegraph` 1.2, `burn` 1.6, `playerDamage` 20 before the arena's ×1.35), `BladeSweepers` (`period` 12, `telegraph` 1.5, `spin` 5, `playerDamage` 16, `jumpClear` 0.55). If the jets feel unfair, lengthen the telegraph before cutting the damage; if they're ignorable, move the rows toward the plaza (`GrandArenaLayout.JetRow`).

## C24 — The Gilded Patron's guard may drag the fight, or barely matter (NEW, Phase 5.13)
- **The risk:** 4–6 mercenaries every 22 s, each with 95 × 1.8 HP, cut his damage taken to 60 % (first 40 %). A weak build may never get him unguarded, and a strong build may kill the guard in a second.
- **Proposal:** judge in the playtest. Tunables: `BD_GildedPatron` (`guardsEvery` 22, `guardCount` 4–6, `guardedDamageMul` 0.6, 15,000 HP), `EN_GildedMercenary.maxHp`. If it drags, lower the count before the protection.
- **Measured (v0.14.0 dev build, 3 bot runs):** the Patron was at 63 % after 40 s (the Nobody with Gorm's flail L7), 99 % after 57 s (the Nobody) and 100 % after 59 s (the Golden Idol), 2 guard waves each. The bot kites and hits whatever is nearest, so it's a poor proxy, but the guard plus 18,000 HP looks too tanky. Next default tried: 15,000 HP and/or `guardedDamageMul` 0.6.
- **Retuned (Phase 5.13): 15,000 HP and `guardedDamageMul` 0.6.** A 4th bot run (the Nobody) logs the fight itself: in 56 s the bot was within 10 m of him for only 7 s and chased pickups for 48 s (his purses scatter gold, and the bot always goes for the nearest pickup); he was guarded for 26 s; he ended at **76 %** (before: 99–100 %). So most of the earlier tankiness was the bot, not him: a player who walks up and hits him should do far better. Recommendation: playtest at these numbers; if he still drags, cut `guardCount` to 3–4 before touching his HP.

## C25 — The Net-Weaver's net may trivialise crowds, or the trident's reach may be too short (NEW, Phase 5.14)
- **The risk:** every 4 s a slide gathers everything within 3.2 m into one rooted knot that takes +50 % from all his hits, so the net plus any area weapon could make the early waves trivial. The other way round, the Trident reaches only 4.2 m, so without the net ready he has to stand in the crowd.
- **Proposal:** judge in the playtest. Tunables: `TR_CastNet` (`cooldown` 4, `radius` 3.2, `pull` 0.7, `throwDistance` 5, `damage` 6), `TR_Caught` (`DamageVsStatus` 0.5 Root, 0.25 Slow), `WP_Trident` (`range` 4.2, `arcDegrees` 36, `prongs` 3, `damage` 8, `cooldown` 1.2). If the net feels too strong, lengthen its cooldown before shrinking it: the haul is the fun part.
