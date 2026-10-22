# R7 / R8 / R6 design — Edicts (run modifiers), the Daily Games (daily seeds), Skins (cosmetics)

Design worker, branch `claude/brave-mayer-0ce941` (fast-forwarded to `origin/main` at e9f72f0, v0.20.0). Docs only: no
code, no Blender, no Unity. The contract (parts, owned files, names, tests, merge order) is `docs/tracks/R7-contract.md`.

**Source:** CLAUDE.md §9 Phase 5 "Nice: … cosmetics, run modifiers, daily seeds", §6.6 (meta unlocks content, never flat
stats; Renown gates cosmetics; run modifiers are unlock content), §4 (shared palette materials, zero GC, versioned JSON
save), `docs/ROADMAP.md` §4 "R7, R8, R6: one spec for all three".

**Built on (read in this worktree):** `SaveData` / `SaveSystem` / `MetaProgress` (`RunSummary`, `RunRewards`, `Apply`,
`Buy`, `Met`, `Progress`, `CheckUnlocks`) / `MetaConfig.RenownFor` / `UnlockDef` (categories, conditions, `addsToPool`) /
`MetaSetup` (collects every `UnlockDef`), `CodexPanel` (7 tabs, Market BUY, `Reward()`, Records), `MainMenu` (screens
Menu / Select / Arena / Codex, `Chrome`, `Launch`, `ShowHero`, the input loop), `RosterGrid`, `GladiatorTablet`,
`ArenaLadder` / `ArenaTablet`, `RunDef` / `GameSession`, `RunState`, `RunEndController.Summary`, `RunEndPanel`,
`LevelUpController` / `UpgradeRoller` / `UpgradeConfig.CardCount`, `Director` (steady, swarm, elite, `SpawnAt`),
`EnemyManager.Spawn` (`hpMul`), `PlayerHealth` (`HealingMul`, `HealUnscaled`, `RescaleFrom`), `Favor.Add`,
`ArenaMaster` (boredom), `Interactables` (chests, altar, own RNG), `CrowdGifts` (own RNG), the secrets (`GoldenRat`,
`AshenStranger`, `RattlingCrate`, `Bookmaker`), `GameRandom` (in `DamageInfo.cs`), `PlaytestBot` / its bootstrap,
`GladiatorDef` / `GladiatorLoadout` (one shared `characterMaterial`), `StatBlock` (`StatMod`, `StatSourceKind`),
`StatText`, `BlenderSrc/gladiator_kit.py` (`set_swatch`, `export_fbx`), `ch_nobody.py`, `palette.json` /
`palette_index.md`, `Tools/build_palette.py`, the Vows (C29), `docs/UNLOCKS.md`, `docs/FAVOR.md`.

---

## 0. The three features in one paragraph each

- **R7, Edicts (run modifiers).** Before a fight, the player may accept any of the Editor's Edicts, decrees that make
  the games harder (enemies with iron hides, a hungrier horde, no healing, two cards instead of three…). Each edict pays
  a share of extra Renown: three edicts at +20 % each make the run pay ×1.6. Edicts are data (`RunModifierDef`); they are
  unlocked as content (by beating Gorm, by challenges, a few bought in the Market) and never make the player stronger.
- **R8, the Daily Games (daily seeds).** Once a day (UTC) the Editor proclaims one fixed fight: an arena and a gladiator
  from what you have earned, two edicts, and one seed. The first attempt is scored (+50 % Renown, a line in the daily
  ledger); retries are practice. Behind it, every random number that shapes a run is drawn from **seeded streams**, so
  the same seed gives the same chests, secrets, swarms, elites, card offers, gifts, whims and boss attacks. Normal runs
  get a fresh seed each time, shown on the end screen, with a "SAME SEED" button to replay it.
- **R6, Skins (cosmetics).** Each gladiator gets two palette skins: one sold in the Market for Renown, one earned by
  winning three runs with them. A skin is the same model with its colours remapped in Blender (same silhouette, same
  shared palette material, zero new materials). The player cycles skins on the gladiator select screen and buys them
  there.

The three are separable (§1.3): each ships alone, in the roadmap's order R7 → R8 → R6.

---

## 1. Shared foundations

### 1.1 What they share
| Thing | R7 Edicts | R8 Daily | R6 Skins |
|---|---|---|---|
| **Save** (`SaveData`, versioned JSON) | chosen edicts, wins under edicts | the daily attempt, ledger, counts | equipped skin per gladiator |
| **Renown** (`MetaConfig.RenownFor`) | × (1 + Σ edict bonus) | × 1.5 on the scored daily | spent (Market skins) |
| **Unlocks** (`UnlockDef`, `MetaProgress.Buy`) | `UL_Edicts` + Market + challenges | `UL_Daily` + two challenges | Market + champion challenges |
| **Menu** (`MainMenu`) | arena screen: EDICTS button + panel | main menu: DAILY button + screen | select screen: the skin strip |
| **Codex** (`CodexPanel`) | Market/Challenges entries, Records row | Records rows | Market/Challenges entries |
| **The run** | applied at scene load (`RunModifiers`) | `GameRandom.BeginRun(seed)` before the load | `GladiatorLoadout` swaps the model |

### 1.2 Reusing what exists (no new purchase or unlock machinery)
- **Every new thing that is earned or bought is an `UnlockDef`.** An edict and a skin point at one (`unlock`); Market
  ones are bought through the existing `MetaProgress.Buy(UnlockDef)`, challenge ones are granted by the existing
  `GrantEarned` at the run's end or by `CheckUnlocks` in the menu, and the Codex's Challenges and Market tabs list them
  with no new tab. Only `CodexPanel.Reward()` learns to name an edict or a skin as the reward.
- **New condition kinds are appended** to `UnlockConditionKind` (assets serialize the index): R7 `WinWithEdicts`
  (index 15), R8 `DailiesPlayed` (16) and `DailiesWon` (17).
- **New lists hang off `MetaConfig`** (`edicts`, `skins`, the daily fields), which the menu (`MainMenu.meta`) and every
  arena (`RunEndController.meta`) already reference: no scene needs rebuilding for R7, R8 or R6.

### 1.3 Separable
- **Order:** R7 → R8 → R6, as `docs/ROADMAP.md` orders them (they share the save, the menu and the Market). Each item
  is its own wave of parts, cut from the previous item's merge; each can be released alone or held for one release.
- **Dependencies between items, all optional:** the daily uses edicts (R8 → R7: one field, `DailyPlan.Edicts`; without
  R7 a daily has none). R6 depends on neither. If the producer wants R6 before R8, nothing changes but the version
  numbers in §5.
- **The R6 art part has no file in common with R7 or R8**, so it may start during R7 (ROADMAP §2 "Overlap").

---

## 2. R7 — Edicts

### 2.1 For the player
The arena screen gets an **EDICTS** button beside FIGHT!. It opens the Editor's decrees: a page of cards, each one a
handicap with its price in Renown. Toggle any number; the footer and the button show the total ("3 EDICTS · RENOWN
×1.65"). The choice is remembered between runs. In the fight, the active edicts sit as small icons under the timer; on
the end screen the Renown line shows the multiplier. Winning under edicts unlocks more edicts and, with R6, skins.

**Why players want it:** it's the difficulty dial of a roguelike (Hades' Heat, Megabonk's tomes of chaos), it gives
Renown a faster route for good players, and it gives veterans goals ("win the Colosseum under four edicts").

### 2.2 Rules
1. **Edicts only take power away.** Every effect is a handicap; none makes the player stronger (CLAUDE.md §6.6: meta
   unlocks content, not flat stats).
2. **They stack freely.** Numbers multiply (two ×1.5 HP edicts would be ×2.25; each edict appears once); the Renown
   bonus adds: Renown × (1 + Σ bonus).
3. **The multiplier applies to the run's Renown however it ends** (time, kills, boss, victory, depth already reward a
   good run, so dying in the first minute under ten edicts still pays almost nothing). Bounties are never multiplied.
4. **Chosen in the menu, fixed for the run.** Retry keeps them; the daily brings its own.
5. **Applied once when the arena loads**, on top of the arena's own difficulty (`ArenaLayoutDef.enemyHpMul`,
   `playerDamageTakenMul`, `densityMul`…): the scene is reloaded on Retry, so nothing compounds.
6. **The arena's cap on enemies alive (`maxAliveOverride` / `hardCap`) always holds**, so no edict breaks the
   performance budget (§8 CLAUDE.md).

### 2.3 The edicts (first values, all in data)
| # | Id | Name | Effect | Fields (`RunModifierDef`) | Renown | Unlock |
|---|---|---|---|---|---|---|
| 1 | `iron_hides` | **Iron Hides** | Enemies +50 % HP; bosses +25 % | `enemyHpMul 1.5`, `bossHpMul 1.25` | +25 % | **The Editor Takes Notice** (beat Gorm) |
| 2 | `hungry_horde` | **The Hungry Horde** | +30 % enemies alive and per swarm (the arena's cap holds) | `densityMul 1.3`, `swarmMul 1.3` | +25 % | The Editor Takes Notice |
| 3 | `blood_sport` | **Blood Sport** | You take +40 % damage | `playerDamageTakenMul 1.4` | +20 % | The Editor Takes Notice |
| 4 | `glass_jaw` | **Glass Jaw** | Max HP ×0.6 | `playerStats [MaxHp Multiply 0.6]` | +25 % | Market, 150 |
| 5 | `swift_foes` | **Swift Foes** | Enemies move +15 % faster | `enemySpeedMul 1.15` | +20 % | Market, 150 |
| 6 | `empty_purses` | **Empty Purses** | Gold ×0.5; chests cost ×1.5 | `playerStats [GoldGain Multiply 0.5]`, `chestPriceMul 1.5` | +15 % | Market, 150 |
| 7 | `fickle_crowd` | **A Fickle Crowd** | Favor fills at half speed; boredom rises twice as fast | `favorGainMul 0.5`, `boredomMul 2` | +15 % | Market, 150 |
| 8 | `meagre_choices` | **Meagre Choices** | Level-ups offer one card fewer (2, or 3 with Luck); no rerolls. Chests, the altar and the boss's reward are unchanged | `levelUpCardDelta −1`, `playerStats [Rerolls Multiply 0]` | +20 % | **Under the Editor's Eye** (win a run under 2 edicts) |
| 9 | `impatient_emperor` | **The Impatient Emperor** | The horde comes a third sooner: the Director's clock runs ×1.33 (spawn windows, the alive curve, swarms, elites); the timer and the boss are unchanged | `clockScale 1.33` | +20 % | Under the Editor's Eye |
| 10 | `champions_day` | **Champions' Day** | Elites come twice as often | `eliteEveryMul 0.5` | +20 % | **A Day of Champions** (reach the Grand Arena) |
| 11 | `no_mercy` | **No Mercy** | No healing of any kind (regen, amphorae, lifesteal, heals on kill) | `healingScale 0` | +30 % | **The Emperor's Pleasure** (win a run under 4 edicts) |

All eleven: ×3.35 Renown. Every edict is `dailyEligible` except No Mercy (too harsh for a forced daily).

**Unlock order:** none before Gorm falls (the feature is unknown until "The Editor Takes Notice"); then three free
edicts; four in the Market (600 Renown in all: a Renown sink that pays back); four through challenges that ask for
wins under edicts, so the system feeds itself.

**Rejected (BACKLOG):** *Night Games* (thick fog: readability and URP risk), *The Purist* (one weapon slot: needs
`UpgradeRoller` and `BuildState` changes and fights the Vows), *Mercy* edicts that make the run easier for less Renown
(power for Renown, against §6.6; difficulty options belong in Options if ever), edict ranks (Hades' Heat levels: data
could carry them later).

### 2.4 How each effect reaches the game (`RunModifiers.ApplyTo`, once, at scene load)
| Field | Applied to | Exists today? |
|---|---|---|
| `playerStats` (`StatMod[]`) | `PlayerStats.Block.Add(mod, 1, new StatSource(StatSourceKind.Edict, def))`; then `PlayerHealth.RescaleFrom(oldMax)` so HP starts full | `StatSourceKind.Edict` is new (appended); the pause stats page shows "Edict: Glass Jaw" |
| `playerDamageTakenMul` | `RunState.Current.playerDamageTakenMul *=` | yes |
| `healingScale` | `PlayerHealth.HealingScale *=` (new): multiplies regen, `Heal` and `HealUnscaled`; never max-HP growth | new; separate from `HealingMul`, which the Blood Knight's trait owns |
| `enemyHpMul` | `EnemyManager.hpMul *=` | yes |
| `bossHpMul` | `EnemyManager.scriptedHpMul *=` (new; Scripted enemies = bosses skip `hpMul` today) | new |
| `enemySpeedMul` | `EnemyManager.speedMul *=` (new; multiplies `_speed[i]` at spawn) | new |
| `densityMul`, `swarmMul` | `Director.densityMultiplier *=`, `swarmMultiplier *=` (before the pools prewarm, so they are sized for it) | yes |
| `eliteEveryMul` | `Director.eliteEveryOverride = (override > 0 ? override : config.eliteEvery) × mul` | yes |
| `clockScale` | `Director.clockScale *=` (new): `PaceClock = Elapsed × clockScale` drives the swarm and elite schedules; `Clock = min(PaceClock, run length)` drives the curves and the spawn table, so the minute-8 horde arrives at 6:00 and holds | new |
| `chestPriceMul` | `Interactables.priceMul *=` (new; `ChestPrice` multiplies) | new |
| `favorGainMul` | `Favor.gainMul *=` (new; positive `Add` amounts) | new |
| `boredomMul` | `ArenaMaster.boredomMul *=` (new; the rise only) | new |
| `levelUpCardDelta` | `LevelUpController.levelUpCardDelta +=` (new; `Open(LevelUp)` only, clamped 1–4) | new |

Every new hook defaults to 1 (or 0), so with no edict nothing changes.

### 2.5 Screens
- **Arena screen (`MainMenu`, Screen.Arena):** a bottom-centre button between BACK and FIGHT!: **"EDICTS · RENOWN
  ×1.65"** (just "EDICTS" with none on). Hidden until `UL_Edicts` is earned. Opens with a click, **E** on the keyboard,
  **Y (North)** on a gamepad. The arena screen always shows when edicts are open, even with one arena earned (it does
  today from the moment Gorm falls, since that also opens the Provincial Pit).
- **The Edicts panel (`EdictsPanel`, a modal over the arena screen):** header "THE EDITOR'S EDICTS", the Renown balance
  top right; a 2 × 6 grid of cards (all eleven fit one page): icon, name, effect line, "+25 % RENOWN".
  - **On:** an orange-red frame and an ON chip (the colour language: it hurts you); off: grey.
  - **Locked, challenge:** dark card, the unlock's hint and its progress ("1 / 2 wins under edicts").
  - **Locked, Market:** lit card (you see what you buy), "BUY · 150 RENOWN" when affordable, else the price in grey.
  - Footer: "3 EDICTS · RENOWN ×1.65", and the keys line "Arrows: move · Enter / A: toggle or buy · X / Backspace:
    clear · Esc / B: done".
  - Arrows / d-pad move the focus; Enter / South toggles (or buys); X (West) or Backspace clears all; Esc / East closes
    and saves the choice (`SaveData.edictsChosen`).
  - In the daily the panel opens read-only with the day's edicts ("PROCLAIMED BY THE EDITOR").
- **Arena tablet (`ArenaTablet.Show`):** one line under the arena's difficulty: "EDICTS  Iron Hides · Blood Sport ·
  RENOWN ×1.45" (nothing when none).
- **HUD:** the active edicts' icons, 28 px, one row under the timer, built once at the run's start (no per-frame work).
- **End screen (`RunEndPanel`):** the Renown row becomes "+124 ×1.65 = +205 (2,310)" when edicts were on (bounties
  after it as today).
- **Codex:** no new tab. The edict unlocks appear in CHALLENGES and MARKET by themselves; `Reward()` reads "Reward:
  Edicts — Iron Hides, The Hungry Horde, Blood Sport" / "Reward: Edict — Glass Jaw". RECORDS gets "Most edicts in a
  win".

### 2.6 Data and code (names in the contract)
- **`RunModifierDef`** (ScriptableObject, menu `Gladiator/Edict`, assets `Assets/Data/Edicts/ED_*.asset`): id, name,
  description (flavour), `effectLine`, icon (the upgrade icon atlas, appended rows `ed_<id>`), `renownBonus`, `unlock`
  (null = open with the Edicts), `dailyEligible`, and the effect fields of §2.4. **New edicts are new assets**; only a new
  *kind* of effect needs code.
- **`RunModifiers`** (static): `Active`, `RenownBonus`, `Set`, `Clear`, the pure `Combine` / `Bonus`, and `ApplyTo` on
  `SceneManager.sceneLoaded` (after every `Awake`, before any `Start` or tick, so the pools prewarm with the edicts'
  numbers). It survives scene loads (static), like `GameSession`.
- **Meta:** `MetaConfig.edicts` + `edictsUnlock`; `RunSummary.Edicts` / `EdictBonus`; `RunRewards.BaseRenown` /
  `EdictBonus`; `MetaProgress.IsOpen(edict)`, `ChosenEdicts(meta)`, `SetChosenEdicts(...)`; condition `WinWithEdicts`.
- **Bot:** `-edicts iron_hides,hungry_horde` picks edicts for a bot run (earned or not); its summary line adds
  ` edicts N (+X %)`.

### 2.7 Performance
Edicts change numbers once at load; nothing per frame. The Hungry Horde and Champions' Day raise the load up to the
arena's cap, which the bot already measures (`-stress` at the cap). Integration runs one bot run in the Colosseum with
`-edicts hungry_horde,champions_day,impatient_emperor,swift_foes` and checks the §8 budget (60 fps, gameplay ≤ 4 ms, 0 B
GC).

---

## 3. R8 — the Daily Games and run seeds

### 3.1 For the player
The main menu gets a **DAILY** button (under PLAY). It opens the day's proclamation: the date, the arena (its
thumbnail), the gladiator (portrait, in your equipped skin with R6), the two edicts, "RENOWN ×1.5 + EDICTS", and below
it the ledger of your last seven dailies (date, who, where, outcome, time, kills, Renown). FIGHT! starts it, skipping
both select screens. The first attempt of the day is **SCORED**; after that the screen says **PRACTICE** and shows the
scored result. A clock says when the next daily comes. Normal runs show their seed on the end screen ("SEED 7F3A-91C2")
with a **SAME SEED** button beside RETRY.

**Why:** a reason to come back each day, a fair comparison with a friend (same seed, same unlocks → same run), and seeds
make bugs and lucky runs replayable.

### 3.2 Every RNG that shapes a run today, and how it is seeded (the inventory)
There is one global generator, **`GameRandom`** (xorshift32, `DamageInfo.cs:31`). **It is never seeded in play**: it
resets to a constant at app start (`SubsystemRegistration`), only tests call `GameRandom.Seed`. So every launch replays
the same sequence from its first run (the secrets' comments say so: "not GameRandom: that one replays the same rolls
every launch"). Three systems keep **their own xorshift with a constant seed**, so they repeat the same choices **every
run**. The secrets use **`UnityEngine.Random`** (unseeded, different every run, not reproducible).

| # | What | Where (file:line) | Today | R8: stream / fork, keyed by |
|---|---|---|---|---|
| 1 | Level-up card count (4th card with Luck) | `LevelUpController.cs:117` | global `GameRandom` | **Cards**, reseeded at each pick by (source, nth pick of that source) |
| 2 | Card rolls, reroll, banish replacement | `UpgradeRoller.cs:82,164` | global | **Cards** (continues within one pick) |
| 3 | Chest, altar and boss reward rolls | same code (`PickSource.Chest/Altar/Boss`) | global | **Cards**, keyed (Chest, n) / (Altar, n) / (Boss, n) |
| 4 | Steady spawns: which type | `Director.cs:295` (`Pick`) | global | fork **Director** (one per run) |
| 5 | Swarm: type, side (ring angle), gate | `Director.cs:242–246`, `ArenaGates.cs:77` | global | fork **Swarm**, keyed by swarm number |
| 6 | Elite: type and affix (timer and the Arena Master's champion) | `Director.cs:222,274` | global | fork **Elite**, keyed by elite number |
| 7 | Spawn positions (ring angle tries, depth, swarm spread, gate spread) | `Director.cs:210,317,342,345`, `ArenaGates.cs:102` | global | **Spawns** |
| 8 | Per-enemy jitter: speed, strafe side, first attack, flank timers | `EnemyManager.cs:399–418,1156,1166` | global | **Enemies** |
| 9 | Gilded Mercenary blink; fire-pot zone scatter | `EnemyManager.Gilded.cs:84–85`, `EnemyProjectiles.cs:372–373` | global | **Enemies** |
| 10 | Gold drop on kill; Gilded coin burst | `PickupSystem.cs:78`, `EnemyManager.Gilded.cs:64` | global | **Loot** |
| 11 | Crits, Boon proc chances | `WeaponSystem.cs:496`, `PlayerProjectiles.cs:501,532`, `VerdictTrait.cs:125`, `BoonEffects.cs:173` | global | **Combat** |
| 12 | Boss attack choice, bomb scatter, guard count, beast packs | `BossController.cs:504,879–880,1235,1237` | global | fork **Boss**, keyed by the boss's id |
| 13 | Hypogeum lifts (packs, counts, angles); beast cages (first cage, spawn spread) | `Hypogeum.cs:708,755,1141`, `BeastCages.cs:139,181` | global | forks **Hazards**, keyed by component |
| 14 | The Pit Goblin's improvised weapon rolls | `ScavengerTrait.cs:96` | global | fork **Traits**, keyed "scavenger" |
| 15 | Chest and altar placement (anchor order, free spots, yaw) | `Interactables.cs:49,127,146,181` | **own xorshift, constant seed 0x51ED27: the same placements every run** | fork **Map**, keyed "interactables" |
| 16 | Crowd gifts: kind, loaned weapon, landing spot | `CrowdGifts.cs:39,118,132,177` | **own, constant 0x6A11F7: the same sequence every run** | fork **Gifts** |
| 17 | Arena Master whims: which whim, the champion's spot | `ArenaMaster.cs:29,106,137` | **own, constant 0xB0BA5: the same sequence every run** | fork **Whims** |
| 18 | The Golden Rat: whether, when, which hole, wander | `GoldenRat.cs:71,72,94,121` | `UnityEngine.Random` | fork **Map**, keyed "golden_rat" |
| 19 | The masked stranger: whether, when, where | `AshenStranger.cs:54,56,90` | `UnityEngine.Random` | fork **Map**, keyed "ashen_stranger" |
| 20 | The rattling crate: spot, rattle timing | `RattlingCrate.cs:61,120` | `UnityEngine.Random` | fork **Map**, keyed "rattling_crate" |
| 21 | The Bookmaker: spot | `Bookmaker.cs:92` | `UnityEngine.Random` | fork **Map**, keyed "bookmaker" |

**Left alone on purpose (they don't shape the run):** death pops, damage-number drift, pickup bob and pop velocity,
projectile spin, hazard-zone shader seeds, the net's and traps' yaw (all `GameRandom` legacy calls → the **Misc**
stream); the admin and debug spawners (Misc); `SfxPlayer`'s pitch jitter and crowd-loop offset (`UnityEngine.Random`,
audio only); the PlaytestBot's own generator (`-seed`, its decisions). Two findings to note in CONCERNS: rows 15–17
repeat every run and the global generator repeats every launch; R8 fixes both by seeding.

### 3.3 Streams and forks
- **One run seed** (a `uint`). `GameRandom.BeginRun(seed)` gives every **stream** its own state:
  `state[s] = Hash(seed, s, 0)`. Drawing from one stream never moves another, so a crit (Combat) never changes the next
  card offer (Cards).
- **Forks** for systems that own their randomness: `Rng r = GameRandom.Fork(RngStream.Map, KeyOf("golden_rat"))`
  returns a small struct generator (`Rng`, a `uint` of xorshift state, no allocation) that the component keeps in a
  field. Unity's `Start` order between components is undefined, so a shared stream would make the secrets' rolls depend
  on it; a fork per component doesn't.
- **Keyed reseeds** for events that must be the same however the run went: before the nth swarm the Director forks
  `Swarm` with key n, so **the third swarm is the same type from the same side** whether you killed 200 or 2,000 before
  it. The same for elites (key n), card picks (key = source and its count), the boss (key = its id).
- **The legacy API stays** (`GameRandom.Value`, `Range`, `Chance`, `NextUInt` = the Misc stream) so untouched cosmetic
  call sites need no edit. **`GameRandom.Seed(int)` keeps its old meaning** for tests: every stream's state = that seed,
  so an EditMode test that seeds 12345 and rolls cards sees exactly the sequence it saw before.
- **Zero GC:** a static `uint[16]` of states, struct forks, string keys hashed once in `Awake`.

### 3.4 What "reproducible" means (honest limits)
Frame times vary, and the player plays differently, so a seed is not a recording. With the same seed **and the same
unlocks**:
- **Exactly the same:** where chests and the altar appear and in what order; whether, when and where each secret
  appears; the nth swarm's type, side and gate choice (the gate also leans away from the player); the nth elite's type
  and affix; the boss's sequence of attacks (per boss); the hazards' sequence (lift packs, cages); the Pit Goblin's
  weapon rolls; the gift kinds and loaned weapons in order (the amphora's weight still leans on your HP); the whims in
  order.
- **The same for the same decisions:** the cards at the nth level-up, the nth chest, the altar and the boss reward,
  given the same build (the pool depends on what you own); rerolls continue that pick's own sequence.
- **Diverges with play:** which steady spawn comes when (it depends on how many are alive), each enemy's jitter, crits,
  gold drops.

### 3.5 Seeds for normal runs
- **A fresh seed per run** (`GameSession.FreshSeed()`: the clock's ticks mixed through `Hash`), so normal runs stay
  different, and now differ from the very first run after launch too.
- **Retry:** a normal run's RETRY gets a fresh seed (it's a new run); **SAME SEED** replays the seed. A daily's RETRY is
  practice on the same seed.
- **Shown** on the end screen and the pause stats page as 8 hex digits, `7F3A-91C2`.
- **Command line:** `-runSeed 7F3A91C2` (hex) for a bot or perf run (the bot's own `-seed` is unchanged). Perf
  comparisons should pass a fixed `-runSeed`.
- **No typed seed in the menu in R8** (Q9): SAME SEED and the daily cover replays, and typing needs a keyboard field the
  gamepad can't use.

### 3.6 The daily's rules
1. **The day is UTC** (yyyymmdd). Everyone in the world has the same daily at the same moment, and changing time zones
   doesn't give a second one.
2. **Seed** = `Hash(FNV-1a(dailySalt), date, 0)` with `MetaConfig.dailySalt = "gladiator-daily-1"`; bumping the salt
   after a content change keeps old dailies from pretending to be comparable.
3. **The plan** (`DailyGames.For`) draws from the seed, each with its own key: the **arena** among the runs you have
   earned; the **gladiator** among the gladiators you have earned; **two edicts** (`MetaConfig.dailyEdicts`) among all
   `dailyEligible` edicts, owned or not (they're handicaps, not power; playing one doesn't unlock it). Every list is
   sorted by id first, so two players with the same unlocks get the same daily.
4. **One scored attempt a day.** Starting the daily marks the attempt in the save at once (quitting doesn't give a new
   one). Later attempts that day are practice: a normal run's Renown, no daily bonus, no ledger line.
5. **The scored attempt pays ×1.5 Renown** (`dailyRenownBonus 0.5`, on top of the edicts) and writes the ledger
   (the last 30 kept).
6. **Unlocked by beating Gorm** (`UL_Daily`, the same moment as the edicts), so a new player's first runs aren't
   handicapped dailies. Before that the DAILY button is greyed with the hint.
7. **Two challenges with Renown bounties:** *Daily Bread* (play 3 scored dailies, +100) and *Champion of the Day* (win a
   scored daily, +150).
8. **The skin** in a daily is the player's equipped skin for that gladiator (cosmetic, R6).

### 3.7 Screens
- **Main menu:** PLAY · **DAILY** · CODEX · OPTIONS · QUIT. DAILY shows a gold dot when today's scored attempt is still
  open.
- **The Daily screen (`DailyPanel`, Screen.Daily):** header "THE DAILY GAMES — 22 OCT 2026". Left: the proclamation
  (arena thumbnail and name; gladiator portrait and name; the two edicts with icons and effect lines; "RENOWN ×1.5 ·
  EDICTS ×1.45"; "SCORED ATTEMPT" in gold or "PRACTICE · scored: VICTORY 8:00, 1,904 kills, +412" in grey; "NEXT DAILY
  IN 5 H 12 M"). Right: the ledger (7 rows). BACK / FIGHT! via the shared `Chrome`. The edicts line opens the edicts
  panel read-only.
- **HUD:** a small "DAILY" chip beside the timer (gold scored, grey practice).
- **End screen:** the note line shows "SEED 7F3A-91C2" or "DAILY 22 OCT · SCORED"; the Renown breakdown includes ×1.5;
  buttons RETRY · SAME SEED · MAIN MENU (a daily: PRACTICE · MAIN MENU).
- **Pause stats page:** the seed in its header.
- **Codex RECORDS:** "Dailies played", "Dailies won".

---

## 4. R6 — Skins

### 4.1 For the player
On the gladiator select screen a strip under the diorama reads **"◀ FREEDMAN ▶ · 2 / 3"**. Q / E (LB / RB on a pad, or
the arrows with the mouse) cycle the picked gladiator's skins on the diorama. An owned skin is equipped at once and
remembered; a Market skin shows "BUY · 250 RENOWN" (F / Y or the button); a champion skin shows its goal and progress
("Win 3 runs as the Nobody · 1 / 3"). FIGHT! always fights in the equipped (owned) skin. The roster tile shows the
equipped skin's portrait.

### 4.2 Rules
1. **Palette only.** A skin is the gladiator's own model with some swatches swapped: the silhouette, the parts and the
   animation are untouched, so "silhouette → weapon → head → motion → VFX" (§7.2) still identifies him and the 64 px
   silhouette test passes by construction.
2. **One shared material, zero new materials** (CLAUDE.md §4): the skin's UVs point at other cells of the same palette
   texture; `GladiatorLoadout` assigns the same `characterMaterial` as today.
3. **The colour language holds:** a skin never uses the `hazard` or `enemy_accent` families or an orange-red as a main
   colour (orange-red = hurts you), and never a glow swatch as more than a small accent.
4. **No VFX cosmetics** (trails, hit colours): player VFX must stay cool white / cyan / gold for readability.
5. **Weapons aren't skinned** (the swung weapon meshes are shared by every gladiator who takes them).
6. **Content, not stats:** a skin changes nothing in play.

### 4.3 The art pipeline (regenerable, CLAUDE.md §3.3)
- **`gladiator_kit.py`** learns one thing: when the environment variable `GLADIATOR_SKIN` holds
  `{"suffix": "_Freedman", "remap": {"cloth_red": "cloth_white", …}}`, `set_swatch` maps every swatch through the remap,
  and `export_fbx` writes to `Characters/Skins/<name><suffix>.fbx`. It reads the variable **on every call**, because each
  character script does `importlib.reload(K)`, which would wipe a module global.
- **`BlenderSrc/characters/skins.json`** is the one table of skins (Blender and Unity both read it): id, gladiator id,
  base script, display name, suffix, remap, three chip swatches, and how it's unlocked (Market price or champion).
- **`BlenderSrc/characters/build_skins.py`** (`Tools/bheadless BlenderSrc/characters/build_skins.py skin=<id>|all`) sets
  the variable and runs the base script with `runpy` per skin. Skins save no `.blend` (regenerable from script + table;
  the base `.blend` exists).
- **New swatches only in free cells of existing rows** of `palette.json` (never move one: every FBX's UVs point at a cell
  centre), then `Tools/build_palette.py`. Proposed: `silver`, `silver_d`, `obsidian` (metal row, cols 8–10);
  `cloth_black`, `cloth_purple`, `cloth_teal` (cloth_leather, cols 9–11); `ivory` (neutral, col 7); `jade`, `jade_d`
  (misc, cols 5–6).
- **Review (time-boxed, one iteration per gladiator):** one lineup per gladiator (default + two skins, front and 3/4) on a
  pale floor and on a dark one, `Screenshots/blender/skins_<gladiator>.png`; the 64 px silhouette is unchanged and isn't
  re-rendered.

### 4.4 The twenty skins (themes; the art part fixes the remaps)
| Gladiator | Market skin (Renown) | Champion skin (win 3 runs with him) |
|---|---|---|
| The Nobody | **Freedman** (bleached tunic, clean bronze), 250 | **Unbowed** (charcoal leathers, gold trim) |
| The Pit Goblin | **Sewer King** (slate and purple rags), 250 | **Ratcatcher** (ochre leathers, a gold tooth) |
| The Blood Knight | **Ashen Knight** (ash and bone plate), 250 | **Night Knight** (obsidian plate, silver edges) |
| The Murder Machine | **Headsman of Rome** (black hood, iron), 350 | **Bronze Colossus** (polished bronze) |
| The Thunder Champion | **Storm-Grey** (slate and silver), 350 | **Olympian** (ivory and gold) |
| The Golden Idol | **Silver Idol**, 350 | **Jade Idol** |
| The Ashen Oracle | **Bone Oracle** (ivory mask, pale robes), 350 | **Violet Oracle** (purple robes) |
| The Net-Weaver | **Deep Sea** (teal and blue), 350 | **Pearl Diver** (ivory and silver) |
| The Beastmaster | **Winter Hunter** (white furs), 350 | **Black Wolf** (charcoal furs) |
| Asterion | **The White Bull** (his father's white hide), 350 | **Bronze Bull** (Talos: bronze hide) |

Market total: 3,200 Renown (a long Renown sink: an average mid-game run pays 150–400). Champion skins use the existing
`WinWithGladiator <id> 3`.

### 4.5 Data, code, screens
- **`SkinDef`** (ScriptableObject, menu `Gladiator/Skin`, `Assets/Data/Skins/SK_*.asset`): id, name, description,
  `gladiator`, `model` (the skin FBX), `portrait` (baked), `unlock`, `chips` (three colours for the strip). Made by
  `SkinSetup` from `skins.json`; their unlocks `UL_Skin_*` (Market or Challenge) too.
- **`MetaConfig.skins`**; **`SaveData.equippedSkins`** (skin ids, one per gladiator at most); `MetaProgress.IsOwned`,
  `EquippedSkin`, `Equip`, `SkinsOf`.
- **`GameSession.Skin`** (set at launch, kept by Retry); **`GladiatorLoadout`** instantiates `skin.model` instead of
  `def.model` when the skin is his (same part names, same material).
- **`PortraitBaker`** bakes each skin's portrait (`PT_<gladiator>_<skin>.png`); the roster tile uses the equipped one.
- **Codex:** skins appear in MARKET and CHALLENGES by themselves; `Reward()` reads "Reward: Skin — Freedman (The Nobody)".
- **Bot:** `-skin <id>`.

---

## 5. Save changes and migration
`SaveData` stays one JSON object (JsonUtility); every new field has a default, and `Migrate` fills missing lists, as
Phase 5.6 did. **`CurrentVersion` goes up by one per item** (R7 → 2, R8 → 3, R6 → 4, in shipping order) so a file says
which schema wrote it; each `Migrate` step is an idempotent fill, so a version-1 file opened by R6 runs every step.

| Item | Fields | Migrate |
|---|---|---|
| R7 | `List<string> edictsChosen`, `List<IdCount> edictWins` (edict id → won runs with it on), `int bestEdictWin` (most edicts in one won run) | fill lists; nothing to convert |
| R8 | `int dailyAttempt` (yyyymmdd of the last scored attempt started), `int dailiesPlayed`, `int dailiesWon`, `List<DailyRecord> dailies` (≤ 30, oldest first) | fill the list |
| R6 | `List<string> equippedSkins` | fill; drop ids no `SkinDef` knows (when the menu loads them, not in `Migrate`, which has no data) |

`DailyRecord` (serializable struct): `int date; string run; string gladiator; string edicts` ("a|b"); `int outcome`
(`RunOutcome`); `float elapsed; int kills; int renown; long seed`.

**Downgrades** (a newer save opened by an older build) lose the new fields on the next save: accepted, as today.
**Bot and Editor saves** stay separate files (`SaveSystem`), so bot dailies never spend the player's attempt.

---

## 6. Decisions (one-line reasons)
### Edicts (R7)
1. **"Edicts", decreed by the Editor of the games (`RunModifierDef` in code).** The editor (munerarius) set the terms of
   real games; "Vow" is taken (the trade-off Boons, C29) and "modifier" isn't a word the arena would use.
2. **Edicts only take power away; no "Mercy" edicts.** Meta unlocks content, never power (§6.6).
3. **Renown × (1 + Σ bonus), applied whatever the outcome.** Simple to read; the base Renown already rewards surviving,
   so an early death under many edicts pays little.
4. **One shared numeric shape for every edict (multipliers + stat mods + a card delta).** New edicts are assets; only a
   new kind of effect is code (CLAUDE.md §2.4).
5. **Applied at `sceneLoaded`, not by a component in each scene.** Six arena scenes need no rebuild, and it runs after
   every `Awake` and before any tick, so pools prewarm with the edicts' numbers.
6. **New hooks are separate fields defaulting to 1** (`speedMul`, `scriptedHpMul`, `clockScale`, `priceMul`, `gainMul`,
   `boredomMul`, `levelUpCardDelta`, `HealingScale`). The arena's values and the traits' values stay theirs; nothing
   is overwritten, so nothing compounds on Retry.
7. **`HealingScale` beside `HealingMul`.** The Blood Knight's trait sets `HealingMul`; an edict that wrote it would be
   undone by him.
8. **The Impatient Emperor uses two clocks.** The schedules run on the fast clock; the curves on a fast clock capped at the
   run's length, so the last two minutes keep their swarms and hold the minute-8 horde.
9. **Unlocked by beating Gorm, then challenges that ask for wins under edicts.** A new player isn't handed a difficulty
   menu before knowing the game; veterans unlock the harshest by using the gentler ones.
10. **The arena screen hosts the edicts (a button + modal), not a new screen in the flow.** No extra click for players
    who don't use them; the choice is remembered.
11. **On = orange-red frame.** The colour language: it hurts you.

### Daily and seeds (R8)
12. **Streams + forks + keyed reseeds instead of one generator.** One generator lets a crit change the next card; streams
    keep systems apart, and keys make events (swarm n, elite n, pick n) the same however the run went.
13. **The legacy `GameRandom` API stays as the Misc stream; `Seed(int)` keeps its meaning.** Cosmetic call sites need no
    edit and the EditMode tests that seed keep their exact rolls.
14. **Forks are structs held by their component.** Unity's `Start` order is undefined; per-component forks make the
    secrets reproducible anyway, with no allocation.
15. **The secrets move from `UnityEngine.Random` to seeded forks.** They stay random per run (fresh seed) and become
    reproducible in a daily; the reason for avoiding `GameRandom` (it repeated every launch) goes away with seeding.
16. **UTC days.** One daily for everyone at a time; no time-zone exploits.
17. **The daily draws from what you've earned (arena, gladiator), plus edicts owned or not.** Always playable; players
    with the same unlocks get the same daily; edicts are handicaps, so previewing one gives nothing away.
18. **One scored attempt, recorded at launch.** The Spelunky rule: it makes the first attempt matter and can't be farmed.
19. **×1.5 Renown on the scored attempt.** A reason to play it daily without a streak mechanic that punishes missing a
    day.
20. **No typed seeds in R8.** SAME SEED and `-runSeed` cover replays; a text field is keyboard-only (BACKLOG).
21. **A fresh seed on a normal RETRY.** Retry has always meant a new run; replaying is the new SAME SEED button.

### Skins (R6)
22. **Skins are palette remaps done in Blender at export, not runtime recolours.** A runtime UV remap needs read/write
    meshes and a mesh copy per spawn; `MaterialPropertyBlock` and per-object materials are forbidden (§4). Remapped
    FBX use the one shared material and batch as today.
23. **One table, `skins.json`, read by Blender and Unity.** The remap and the Codex text can't drift apart.
24. **The kit reads the skin from an environment variable on every call.** Character scripts reload the kit module,
    which would reset a module global.
25. **Two skins per gladiator: one Market, one champion.** Renown gates cosmetics (§6.6) and mastery shows on the model.
26. **The skin strip lives on the select screen, with BUY there.** You buy what you see on the diorama; the Codex Market
    lists them too, for the overview.
27. **No skin `.blend` files.** They're regenerable from the base script and the table, and each would add an LFS blob.

### All three
28. **No new Codex tab.** Everything earned or bought is an `UnlockDef`, which the Challenges and Market tabs already
    show; Records gets three rows.
29. **Items ship in the roadmap's order, each with its own save version and its own wave.** The three share the save, the
    menu and the Market, so parallel waves would fight over the same files.

---

## 7. Out of scope (BACKLOG lines for the orchestrator)
- Edict ranks (Heat levels); *Night Games*; *The Purist*; edicts as Favor whims ("the Editor adds an edict mid-fight").
- Typed seeds in the menu; online leaderboards or a shared daily ledger (needs a server; offline game); daily streaks.
- Weapon skins, accessories (crests, plumes, capes: per-gladiator fitting), titles and announcer callouts, victory
  poses, skin portraits in the Codex's Gladiators tab.

---

## 8. Open questions for the producer (my recommendation first)
- **Q1. The name "Edicts" (recommended)** vs "Decrees" / "Run modifiers".
- **Q2. Renown multiplier always (recommended)** vs only on a victory, or half on a death.
- **Q3. Edicts open when Gorm first falls, 3 free + 4 Market + 4 challenges (recommended)** vs all free from the start
  (no Renown sink, no goals).
- **Q4. No "easier" edicts (recommended).** If an easy mode is wanted, it belongs in Options (no Renown, no records),
  not as content.
- **Q5. The daily's day is UTC (recommended)** vs local midnight (friendlier wording, different dailies per time zone).
- **Q6. The daily fixes the gladiator (recommended)** vs letting the player pick (more comfortable, less variety and
  less comparable).
- **Q7. One scored attempt, retries are practice (recommended)** vs every attempt counts (best of the day).
- **Q8. ×1.5 Renown on the scored daily (recommended)** vs records only (no reward) or a bigger flat bonus.
- **Q9. No typed seed in R8 (recommended; SAME SEED + `-runSeed`)** vs a keyboard seed field on the arena screen now.
- **Q10. Normal RETRY gets a fresh seed (recommended)** vs RETRY replays the seed (then SAME SEED becomes "NEW SEED").
- **Q11. Skins only, two per gladiator (recommended)** vs fewer (one each) or adding accessories (much more art, fitting
  per head).
- **Q12. Skin prices 250 (first three gladiators) / 350 (the rest), champion skins for 3 wins (recommended)** vs other
  numbers.
- **Q13. Three releases (v0.21.0 Edicts, v0.22.0 Daily, v0.23.0 Skins; recommended: each is testable alone and the PRs
  stay small)** vs one release for all three.
- **Q14. Fix the repeating RNG (the constant-seeded chests, gifts and whims; the unseeded global generator) as part of
  R8 (recommended)** vs a small hotfix before R7. The effect today: the chests appear in the same places in the same
  order every run of an arena, and the crowd's gifts and whims come in the same order.
