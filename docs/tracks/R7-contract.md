# R7 / R8 / R6 contract — Edicts, the Daily Games, Skins (DRAFT by the design worker, 2026-10-22)

**Spec:** `docs/tracks/R7-design.md` (what each feature is, the rules, numbers, screens, save, decisions, Q1–Q14). This
file splits the code into parts by file ownership and fixes the names they share. The orchestrator finalises it after
the producer's answers.

**Three waves, in the roadmap's order** (`docs/ROADMAP.md` §1: they share the save, the menu and the Market):
| Wave | Item | Item branch (proposed) | Release (proposed) | Parts |
|---|---|---|---|---|
| 1 | R7 Edicts | `phase5-edicts` from `main` | v0.21.0 | **R7-A** rules · **R7-B** meta + setup · **R7-C** menu |
| 2 | R8 Daily Games + seeds | `phase5-daily` from `main` after wave 1 merges | v0.22.0 | **R8-A** streams · **R8-B** daily core · **R8-C** daily screens |
| 3 | R6 Skins | `phase5-skins` from `main` after wave 2 merges | v0.23.0 | **R6-B** data + loadout · **R6-C** menu (+ **R6-art**, which may start during wave 1: it shares no file) |

**8 code parts + 1 art part.** Within a wave no file is owned by two parts. Across waves some files are edited by each
wave in turn (listed at the end); a later wave always starts from the earlier one's merge.

## Rules for every part
- **Start:** `git fetch origin && git merge --ff-only origin/<item branch>` (a plain merge if the fast-forward fails).
- Read `docs/tracks/README.md`, CLAUDE.md §2 and §4, the spec's section for your item, then this file's.
- **Parts of one wave start together** and code against the names below. A call to a name another part of the same wave
  adds is marked `// <item>-STUB(<part>)` (e.g. `// R7-STUB(a)`); `Tools/ccheck` may fail only on those until that part
  merges, then you merge the item branch and are clean.
- **Gameplay code allocates nothing per frame** (CLAUDE.md §4). Menu and end-screen code may allocate.
- **Append, never reorder:** `UnlockConditionKind`, `StatSourceKind`, `RngStream`, the icon atlas rows, palette cells.
- No Unity, no hand-written YAML: assets come from the `Assets/Editor/Setup/` scripts the orchestrator runs.

---

## Wave 1 — R7 Edicts

### Names every R7 part uses
- **`RunModifierDef`** (`Assets/Scripts/Gameplay/Run/RunModifierDef.cs`, namespace `Gladiator.Gameplay`, menu
  `Gladiator/Edict`, `fileName = "ED_"`):
  ```csharp
  public string id = "edict"; public string displayName = "Edict";
  [TextArea] public string description;            // flavour
  public string effectLine;                        // "Enemies +50 % HP; bosses +25 %"
  public Texture2D icon; public int iconRow = -1;  // the upgrade icon atlas, row "ed_<id>"
  [Range(0f, 1f)] public float renownBonus = 0.2f;
  public UnlockDef unlock;                         // null = open once MetaConfig.edictsUnlock is earned
  public bool dailyEligible = true;
  [Header("Player")] public StatMod[] playerStats = Array.Empty<StatMod>();
  public float playerDamageTakenMul = 1f, healingScale = 1f;
  [Header("Enemies")] public float enemyHpMul = 1f, bossHpMul = 1f, enemySpeedMul = 1f;
  [Header("Director")] public float densityMul = 1f, swarmMul = 1f, eliteEveryMul = 1f, clockScale = 1f;
  [Header("Economy and crowd")] public float chestPriceMul = 1f, favorGainMul = 1f, boredomMul = 1f;
  [Header("Cards")] public int levelUpCardDelta;
  ```
- **`RunModifiers`** (static, `Assets/Scripts/Gameplay/Run/RunModifiers.cs`): `Active` (never null), `RenownBonus`,
  `Current` (`Totals`), `Set(RunModifierDef[])`, `Clear()`, `ActiveIds()`, `ApplyTo(RunState)`, pure `Combine` / `Bonus`.
- **Assets** (`Assets/Data/Edicts/`): `ED_IronHides`, `ED_HungryHorde`, `ED_BloodSport`, `ED_GlassJaw`, `ED_SwiftFoes`,
  `ED_EmptyPurses`, `ED_FickleCrowd`, `ED_MeagreChoices`, `ED_ImpatientEmperor`, `ED_ChampionsDay`, `ED_NoMercy` (ids
  and numbers: spec §2.3). **Unlocks** (`MetaSetup.Dir`): `UL_Edicts` (Challenge "The Editor Takes Notice",
  `DefeatBoss gorm 1`), `UL_Edict_GlassJaw` / `_SwiftFoes` / `_EmptyPurses` / `_FickleCrowd` (Market, 150),
  `UL_EditorsEye` (Challenge "Under the Editor's Eye", `WinWithEdicts 2`: Meagre Choices + the Impatient Emperor),
  `UL_DayOfChampions` (`ReachArena 3`: Champions' Day), `UL_EmperorsPleasure` (`WinWithEdicts 4`: No Mercy).
- **New hooks** (all default to "no change"): `EnemyManager.speedMul`, `EnemyManager.scriptedHpMul`,
  `Director.clockScale` + `Director.PaceClock`, `Interactables.priceMul`, `Favor.gainMul`, `ArenaMaster.boredomMul`,
  `LevelUpController.levelUpCardDelta`, `PlayerHealth.HealingScale`, `StatSourceKind.Edict` (appended after `Admin`).
- **Meta:** `MetaConfig.edicts`, `MetaConfig.edictsUnlock`, `MetaConfig.BaseRenownFor`; `RunSummary.Edicts` /
  `EdictBonus`; `RunRewards.BaseRenown` / `EdictBonus`; `SaveData.edictsChosen` / `edictWins` / `bestEdictWin`;
  `UnlockConditionKind.WinWithEdicts` (index 15); `MetaProgress.EdictsOpen` / `IsOpen` / `ChosenEdicts` /
  `SetChosenEdicts`.
- **Menu:** `EdictsPanel` (UI), `MainMenu.Edicts` / `EdictsButtonShown` / `OpenEdicts()` / `EdictsForLaunch()`.
- **Command line:** `-edicts iron_hides,hungry_horde` (bot / perf runs; earned or not).

### Parts
| Part | Owns | Done when |
|---|---|---|
| **R7-A rules** | new `Assets/Scripts/Gameplay/Run/RunModifierDef.cs`, `RunModifiers.cs`; `Assets/Scripts/Gameplay/Enemies/EnemyManager.cs`; `Assets/Scripts/Gameplay/Director/Director.cs`; `Assets/Scripts/Gameplay/Interactables/Interactables.cs`; `Assets/Scripts/Gameplay/Run/LevelUpController.cs`; `Assets/Scripts/Gameplay/Crowd/Favor.cs`; `Assets/Scripts/Gameplay/Crowd/ArenaMaster.cs`; `Assets/Scripts/Gameplay/Combat/PlayerHealth.cs`; `Assets/Scripts/Core/Stats/StatBlock.cs` (the enum value only); `Assets/Scripts/Gameplay/Stats/StatText.cs`; `Assets/Scripts/Gameplay/Dev/PlaytestBot.cs` (the summary line); new tests `Assets/Tests/EditMode/EdictMathTests.cs`, `Assets/Tests/PlayMode/EdictRunTests.cs` | Every field of §2.4 reaches the game once per loaded run, with no edict nothing changes, Retry doesn't compound, zero GC; ccheck errors=0; tests written. |
| **R7-B meta + setup** | `Assets/Scripts/Gameplay/Meta/SaveData.cs`, `MetaProgress.cs`, `UnlockDef.cs`, `MetaConfig.cs`; `Assets/Scripts/Gameplay/Run/RunEndController.cs`; new `Assets/Editor/Setup/EdictSetup.cs`; `PixelSrc/upgrade_icons.py` + the regenerated `Assets/Art/Textures/UI/UpgradeIcons.png` / `.txt`; new tests `Assets/Tests/EditMode/EdictMetaTests.cs` | Save v2 with Migrate, the Renown formula, records and the new condition, the eleven edicts and seven unlocks with their texts, eleven icons appended; ccheck errors=0 (only R7-STUB(a)); tests written. |
| **R7-C menu** | new `Assets/Scripts/UI/EdictsPanel.cs`; `Assets/Scripts/UI/MainMenu.cs`; `Assets/Scripts/UI/ArenaLadder.cs` (`ArenaTablet`); `Assets/Scripts/UI/Hud.cs`; `Assets/Scripts/UI/RunEndPanel.cs`; `Assets/Scripts/UI/CodexPanel.cs`; new tests `Assets/Tests/PlayMode/EdictMenuTests.cs` | The arena screen's EDICTS button and panel (toggle, buy, clear, read-only), the tablet line, the HUD icons, the end screen's multiplier, the Codex's reward text and Records row, `-edicts`; ccheck errors=0 (only R7-STUBs); tests written. |

### R7-A rules
- **`RunModifiers`:**
  - `public struct Totals { public float PlayerDamageTakenMul, HealingScale, EnemyHpMul, BossHpMul, EnemySpeedMul,
    DensityMul, SwarmMul, EliteEveryMul, ClockScale, ChestPriceMul, FavorGainMul, BoredomMul; public int LevelUpCardDelta;
    public static Totals None { get; } }` (every float 1, the delta 0).
  - `public static Totals Combine(RunModifierDef[] defs)` (pure: products of the floats, sum of the deltas, nulls skipped);
    `public static float Bonus(RunModifierDef[] defs)` (pure: Σ `max(0, renownBonus)`).
  - `public static RunModifierDef[] Active { get; }` (empty array by default), `RenownBonus`, `Current`,
    `public static RunState AppliedTo { get; }`.
  - `public static void Set(RunModifierDef[] defs)` (copies; drops nulls and duplicates; recomputes `Current` and the bonus);
    `public static void Clear()`; `public static string[] ActiveIds()` (allocates; run end only).
  - `public static bool ApplyTo(RunState run)`: false when `run == null`, `Active` is empty or `run == AppliedTo`. Else, each
    target null-safe: `run.playerDamageTakenMul *=`; `EnemyManager.Instance.hpMul *=`, `.scriptedHpMul *=`, `.speedMul *=`;
    `Director.Instance.densityMultiplier *=`, `.swarmMultiplier *=`, `.clockScale *=`, and when `EliteEveryMul != 1`
    `.eliteEveryOverride = (override > 0 ? override : config.eliteEvery) × EliteEveryMul`; `Interactables.Instance.priceMul *=`;
    `Favor.Instance.gainMul *=`; `ArenaMaster.Instance.boredomMul *=`; `LevelUpController.Instance.levelUpCardDelta +=`;
    on `Player.Main`: every def's `playerStats` through `PlayerStats.Block.Add(mod, 1f, new StatSource(StatSourceKind.Edict, def))`,
    then `PlayerHealth.RescaleFrom(oldMaxHp)` (HP starts full at the new max), `PlayerHealth.HealingScale *=`. Logs
    `[Edicts] applied N (+X %)`. Sets `AppliedTo = run`.
  - `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`: reset the statics and subscribe once (`-=` then `+=`) to
    `SceneManager.sceneLoaded`, whose handler calls `ApplyTo(RunState.Current)`.
- **`EnemyManager`:** `[Tooltip] public float speedMul = 1f;` multiplies `_speed[i]` in `Spawn` (line 399);
  `public float scriptedHpMul = 1f;` replaces the `1f` for `EnemyBehaviour.Scripted` in the HP line (398).
- **`Director`:** `public float clockScale = 1f;` `public float PaceClock => (RunState.Current != null ? Elapsed :
  Time.timeSinceLevelLoad) × clockScale` (uncapped) drives `_nextSwarm`, `_nextElite`, `TriggerSwarmNow/EliteNow` and the
  "clock jumped" check; `Clock` becomes `min(PaceClock, RunLength)` when `clockScale > 1` (identical to today at 1) and
  drives `TargetAlive`, `spawnPerSecond`, `swarmSize`, `Pick`/`Weight`. `NextSwarmAt` / `NextEliteAt` stay in
  `PaceClock` units (doc comment).
- **`Interactables`:** `public float priceMul = 1f;` `ChestPrice => round(config.PriceFor(ChestsOpened) × priceMul)`.
- **`Favor`:** `public float gainMul = 1f;` multiplies positive amounts in `Add`.
- **`ArenaMaster`:** `public float boredomMul = 1f;` multiplies `config.boredomRise` (not the fall).
- **`LevelUpController`:** `public int levelUpCardDelta;` in `Open`, when `source == PickSource.LevelUp`:
  `n = Mathf.Clamp(n + levelUpCardDelta, 1, 4)`.
- **`PlayerHealth`:** `public float HealingScale { get; set; } = 1f;` multiplies the regen line, `Heal` and
  `HealUnscaled`; never the max-HP growth in the MaxHp-changed handler.
- **`StatBlock.cs`:** append `Edict` to `StatSourceKind`. **`StatText.cs`:** label `"Edict: " + ((RunModifierDef)Ref).displayName`.
- **`PlaytestBot`:** after the existing lines, ` edicts N (+X %)` when `RunModifiers.Active.Length > 0`.
- **Tests.** EditMode **`EdictMathTests`**: `Combine` of {hp 1.5, hp 1.2} = 1.8; deltas add (−1 + −1 = −2); `Bonus` of
  {0.25, 0.2} = 0.45; nulls skipped; `Totals.None`; `Set` drops duplicates and nulls. PlayMode **`EdictRunTests`** (the test
  arena; defs made with `ScriptableObject.CreateInstance` and the spec's numbers; `RunModifiers.Set` before the load,
  `Clear` in TearDown): each of the eleven effects shows its number (a spawned Convict's max HP ×1.5 and a Scripted test
  enemy's ×1.25; `TargetAlive` ×1.3 below the cap; damage taken ×1.4; Glass Jaw 60 / 60 HP at start; speed ×1.15; chest
  price ×1.5 and a coin's gold ×0.5; Favor from a scripted `Add(10)` = 5; boredom rise ×2; a level-up offers 2 cards, a
  queued chest reward 3, `RerollsLeft` 0; `PaceClock` 8 s at 6 s elapsed, `Clock` capped at the run length, a swarm
  still starts after the cap; the elite interval halves; No Mercy: `Heal` and `HealUnscaled` add 0 and regen adds 0);
  with no edict every number equals the baseline; loading the scene twice doesn't compound; the stats page lists
  "Edict: Glass Jaw"; no GC allocation per frame across 5 s with all eleven on and 200 enemies (ProfilerRecorder "GC
  Allocated In Frame" after a warm-up).

### R7-B meta + setup
- **`SaveData`:** `public List<string> edictsChosen`; `public List<IdCount> edictWins` (edict id → won runs with it on);
  `public int bestEdictWin`; `Migrate` fills the lists; `CurrentVersion = 2` (the v1 → v2 step is the fill).
- **`UnlockDef.cs`:** append `WinWithEdicts, // amount = edicts on in one won run (any arena; id unused) (R7)`.
- **`MetaConfig`:** `[Header("Edicts (R7)")] public RunModifierDef[] edicts = Array.Empty<RunModifierDef>();`
  `[Tooltip("The Edicts open with this (The Editor Takes Notice). Empty = open.")] public UnlockDef edictsUnlock;`
  `public int BaseRenownFor(in RunSummary run)` (today's formula); `RenownFor` = round(base float × (1 + max(0,
  run.EdictBonus))).
- **`MetaProgress`:** `RunSummary`: `public string[] Edicts; public float EdictBonus;` `RunRewards`: `public int BaseRenown;
  public float EdictBonus;` `Apply`: fills both; on a won run with edicts, `edictWins` +1 each and `bestEdictWin = max`.
  `Met` / `Progress` for `WinWithEdicts` (`bestEdictWin` against `amount`). New:
  `public static bool EdictsOpen(MetaConfig meta, SaveData d)` (`edictsUnlock == null || unlocked`);
  `public static bool IsOpen(RunModifierDef e, MetaConfig meta, SaveData d)` (open feature and `e.unlock == null ||
  unlocked`) plus the `Data` overloads; `public static RunModifierDef[] ChosenEdicts(MetaConfig meta, SaveData d)` (open
  ones whose id is in `edictsChosen`, in `meta.edicts` order) plus the `Data` overload;
  `public static void SetChosenEdicts(RunModifierDef[] chosen)` (writes the ids, saves).
- **`RunEndController.Summary`:** `s.Edicts = RunModifiers.ActiveIds(); s.EdictBonus = RunModifiers.RenownBonus;`
- **`EdictSetup`** (`Assets/Editor/Setup/EdictSetup.cs`, menu `Gladiator/Setup/Meta/Create Edict Data`): `Dir =
  "Assets/Data/Edicts/"`; creates or updates the eleven `ED_*` with the spec's ids, names, effect lines, numbers and short
  flavour texts; the seven unlocks with their kinds, prices, hints and descriptions; each edict's `unlock`
  (`UL_Edicts`'s three point at none: open with the feature); `cfg.edicts` in the spec's order; `cfg.edictsUnlock =
  UL_Edicts`; icons: the atlas `Assets/Art/Textures/UI/UpgradeIcons.png` and the row of `ed_<id>` in `UpgradeIcons.txt`;
  then `MetaSetup.CreateOrUpdate()` (collects the unlocks); `SetDirty` + `SaveAssets`; idempotent.
- **`PixelSrc/upgrade_icons.py`:** eleven glyphs appended as `ed_<id>` (an orange-red border: they hurt you); regenerate
  the PNG and TXT (append only: existing rows keep their index).
- **Tests.** EditMode **`EdictMetaTests`**: `BaseRenownFor` equals today's formula on three summaries; `RenownFor` ×(1 +
  bonus), also on a death; `Apply` never multiplies bounties; `edictWins` / `bestEdictWin` only on won runs;
  `WinWithEdicts` `Met` and `Progress`; `IsOpen` (feature locked → false; a Market edict before `Buy` false, after true);
  `ChosenEdicts` drops locked and unknown ids and keeps `meta.edicts` order; a v1 JSON with none of the fields migrates
  (lists non-null, version 2); `MetaProgress.Buy` on a Market edict's unlock spends Renown.

### R7-C menu
- **`EdictsPanel`** (`Assets/Scripts/UI/EdictsPanel.cs`, a plain class like `CodexPanel`):
  `public EdictsPanel(TMP_FontAsset font, Material fontMat)`; `public void Build(Transform root, MetaConfig meta,
  SaveData save, Action onClose)`; `public void Open(RunModifierDef[] forced = null)` (forced = read-only, the daily);
  `public void Close()` (saves through `SetChosenEdicts` unless read-only); `IsOpen`, `ReadOnly`, `Count`,
  `RunModifierDef Def(int i)`, `bool IsOn(int i)`, `bool IsLocked(int i)`, `bool Toggle(int i)` (false when locked or
  read-only), `BuyResult Buy(int i)`, `void ClearAll()`, `RunModifierDef[] Chosen` (meta order), `float RenownMul`,
  `bool HandleInput(bool left, bool right, bool up, bool down, bool confirm, bool back, bool clear)` (true = closed),
  `public static string MulText(float mul)` ("×1.65"). Layout, colours and texts: spec §2.5. Truncate, never Ellipsis
  (README lessons).
- **`MainMenu`:** the bottom-centre EDICTS button on the arena screen (text "EDICTS · RENOWN ×1.65" or "EDICTS"; hidden
  unless `MetaProgress.EdictsOpen`), a CanvasGroup over the arena screen for the panel; E / gamepad North opens it; input
  goes to the panel while it's open (X / West or Backspace = clear); the arena screen's key line adds "E / Y: edicts";
  `AsksForArena` also true when the edicts are open; `public RunModifierDef[] EdictsForLaunch()` (bot `-edicts` ids, any;
  else `MetaProgress.ChosenEdicts(meta)`); `Launch` calls `RunModifiers.Set(EdictsForLaunch())` before
  `GameSession.StartRun`; test API `Edicts`, `EdictsButtonShown`, `OpenEdicts()`.
- **`ArenaTablet`** (`ArenaLadder.cs`): `Show(RunDef r, bool open, int tier, SaveData save, MetaConfig meta,
  RunModifierDef[] edicts)`; the old overload passes `null`; one line "EDICTS  a · b   RENOWN ×1.45" when any.
- **`Hud`:** at Start, one 28 px icon per `RunModifiers.Active` (atlas + row, as the weapon row draws icons) in a row
  under the timer; nothing per frame.
- **`RunEndPanel`:** with `rewards.EdictBonus > 0` the Renown row reads "+{base} ×{mul} = +{renown}" then the bounty
  and total as today.
- **`CodexPanel`:** `Reward(u)` names the edicts an unlock opens ("Reward: Edict — Glass Jaw", "Reward: Edicts — Iron
  Hides, The Hungry Horde, Blood Sport"; `UL_Edicts` opens the three with `unlock == null`); RECORDS row "Most edicts in a
  win" (`bestEdictWin`).
- **Tests.** PlayMode **`EdictMenuTests`** (the menu scene, a temp save through `SaveSystem.PathOverride`): without
  `UL_Edicts` the button is hidden; with it, shown, and the panel has `meta.edicts.Length` cards; the three free ones
  toggle; a Market one is locked until `Buy` (with Renown it spends; without, `TooExpensive`); a challenge one never
  toggles; two on → the button shows "×1.45"; Close saves (reload the save: both ids); `Open(forced)` refuses `Toggle`;
  `EdictsForLaunch()` returns the chosen; in the test arena with two edicts set, the HUD shows two icons; after
  `RunState.End` with edicts the end screen's Renown row contains "×".

### Wave 1 merge order and the orchestrator's steps
**A → B → C.** Then in Unity: `Gladiator/Setup/Upgrade Icons` (atlas import), `Gladiator/Setup/Meta/Create Edict Data`
(it ends with `Create Meta Data`). No scene rebuild (the menu and every arena reference `MetaConfig`). Run `EdictMathTests`,
`EdictMetaTests`, the full EditMode suite, `EdictRunTests`, `EdictMenuTests`, then the full PlayMode suite. Bot runs: the
Slave Pit with `-edicts iron_hides,blood_sport` (survives? Renown line), and the perf check in the Colosseum with
`-edicts hungry_horde,champions_day,impatient_emperor,swift_foes` (§8 budget). Screenshots: the arena screen with the
button, the panel (1080p and 720p), the HUD row, the end screen. Shared docs: DECISIONS (spec §6.1–11), GDD Meta line,
UNLOCKS (the seven), BACKLOG (spec §7), PROGRESS, release notes.

---

## Wave 2 — R8 Daily Games and run seeds

### Names every R8 part uses
- **`RngStream`** (byte, append-only): `Misc, Cards, Combat, Loot, Enemies, Spawns, Director, Swarm, Elite, Map, Gifts,
  Whims, Boss, Hazards, Traits`.
- **`Rng`** (struct: `public uint State; public Rng(uint state)` (0 → `DefaultSeed`); `NextUInt()`, `Next01()`,
  `Range(float, float)`, `Range(int, int)`, `Chance(float)`, with today's `GameRandom` formulas).
- **`GameRandom`** (moved to its own file): `DefaultSeed`, `StreamCount = 16`, `RunSeed`, `BeginRun(uint)`, `Seed(int)`
  (legacy), `Reseed(RngStream, uint key)`, `Fork(RngStream, uint key)`, the stream draws (`NextUInt(s)`, `Next01(s)`,
  `Range(s, …)`, `Chance(s, p)`), the legacy Misc draws (`NextUInt()`, `Value`, `Range(…)`, `Chance(p)`), `Hash(uint,
  uint, uint)`, `KeyOf(string)`, `SeedText(uint)`, `TryParseSeed(string, out uint)`.
- **`GameSession`:** `Seed`, `Daily` (`DailyPlan`), `HasDaily`, `DailyScored`, `FreshSeed()`, `StartRun(g, run)`,
  `StartRun(g, run, seed)`, `StartDaily(in DailyPlan plan, bool scored)`, `Retry()`, `RetrySameSeed()`, `SuppressLoad`,
  `LastLoadPath`.
- **`DailyPlan`** (struct) and **`DailyGames`** (static): `Today()`, `DateKey(DateTime)`, `DateText(int)`,
  `UntilNext(DateTime)`, `SeedFor(int, string)`, `For(int date, MetaConfig meta, IList<RunDef> earnedRuns,
  IList<GladiatorDef> earnedGladiators)`.
- **Meta:** `MetaConfig.dailyUnlock`, `dailyRenownBonus` (0.5), `dailyEdicts` (2), `dailySalt` ("gladiator-daily-1"),
  `dailyLedger` (30); `SaveData.dailyAttempt` / `dailiesPlayed` / `dailiesWon` / `dailies` + `DailyRecord`;
  `RunSummary.Seed` / `DailyDate` / `DailyScored`; `RunRewards.DailyBonus`; `UnlockConditionKind.DailiesPlayed` (16),
  `DailiesWon` (17); `MetaProgress.DailyOpen` / `DailyAttempted` / `BeginDaily`.
- **Unlocks:** `UL_Daily` (Challenge "The Daily Games", `DefeatBoss gorm 1`), `UL_DailyBread` (`DailiesPlayed 3`, +100),
  `UL_ChampionOfTheDay` (`DailiesWon 1`, +150).
- **Command line:** `-runSeed 7F3A91C2`, `-daily 20261022` (a practice daily of that date; never spends an attempt).

### Parts
| Part | Owns | Done when |
|---|---|---|
| **R8-A streams** | new `Assets/Scripts/Gameplay/Combat/GameRandom.cs`; `DamageInfo.cs` (the class moves out); every run-shaping call site: `LevelUpController.cs`, `Upgrades/UpgradeRoller.cs`, `Director.cs`, `Arena/ArenaGates.cs`, `EnemyManager.cs`, `EnemyManager.Gilded.cs`, `EnemyProjectiles.cs`, `Boss/BossController.cs`, `Arena/Hypogeum.cs`, `Arena/BeastCages.cs`, `Interactables.cs`, `Crowd/CrowdGifts.cs`, `ArenaMaster.cs`, `Interactables/GoldenRat.cs`, `AshenStranger.cs`, `RattlingCrate.cs`, `Bookmaker.cs`, `Gladiators/ScavengerTrait.cs`, `Weapons/WeaponSystem.cs`, `Weapons/PlayerProjectiles.cs`, `Gladiators/VerdictTrait.cs`, `Upgrades/BoonEffects.cs`, `Pickups/PickupSystem.cs`; new tests `Assets/Tests/EditMode/RandomStreamTests.cs`, `Assets/Tests/PlayMode/SeedReplayTests.cs`; and, only where an assertion depends on a roll that moved, `InteractableTests.cs`, `StatAndUpgradeTests.cs`, `StatsAndVowTests.cs`, `EvolutionTests.cs` | Spec §3.2's table done row by row, the legacy API and `Seed(int)` unchanged in meaning, zero GC; ccheck errors=0; tests written. |
| **R8-B daily core** | `Assets/Scripts/Gameplay/Run/RunDef.cs` (`GameSession`); new `Assets/Scripts/Gameplay/Meta/DailyPlan.cs`; `SaveData.cs`, `MetaProgress.cs`, `UnlockDef.cs`, `MetaConfig.cs`; `RunEndController.cs`; new `Assets/Editor/Setup/DailySetup.cs`; `PlaytestBot.cs`; new tests `Assets/Tests/EditMode/DailyTests.cs` | Seeds for every run (fresh, `-runSeed`, daily), `BeginRun` before every load, the plan, one scored attempt, save v3, the bonus, the records and conditions, the three unlocks; ccheck errors=0 (only R8-STUB(a)); tests written. |
| **R8-C daily screens** | new `Assets/Scripts/UI/DailyPanel.cs`; `MainMenu.cs`; `RunEndPanel.cs`; `Hud.cs`; `CodexPanel.cs`; `Assets/Scripts/UI/StatsPanel.cs`; new tests `Assets/Tests/PlayMode/DailyMenuTests.cs` | The DAILY button and screen, the ledger and countdown, the end screen's seed / daily line, SAME SEED and PRACTICE, the HUD chip, the seed on the pause page, the Codex rows, `-daily`; ccheck errors=0 (only R8-STUBs); tests written. |

### R8-A streams
- **`GameRandom.cs`:** as named above. `BeginRun(seed)`: `RunSeed = seed`, `state[s] = Hash(seed, s, 0)` for every
  stream. `Seed(int seed)`: `RunSeed = (uint)seed` and **every** state = `seed` (0 → `DefaultSeed`), exactly the old
  behaviour per stream. `Reseed(s, key)`: `state[s] = Hash(RunSeed, s, key)`. `Fork(s, key)`: `new Rng(Hash(RunSeed,
  (uint)s | 0x100u, key))` (forks never equal a stream). `Hash`: murmur3-style mixing of the three words, never 0.
  `KeyOf(name)`: `(uint)GameInfo.StableId(name)`. Legacy draws use `RngStream.Misc`. `SubsystemRegistration` reset:
  `BeginRun(DefaultSeed)`. States in a static `uint[StreamCount]`.
- **Call sites** (spec §3.2; keep every other line as it is):
  - `LevelUpController`: `readonly int[] _opens = new int[4]`; `Open` first calls `GameRandom.Reseed(RngStream.Cards,
    ((uint)source << 16) | (uint)_opens[(int)source]++)`; its `CardCount` draw and `UpgradeRoller`'s two draws use
    `RngStream.Cards`; `public int OpensOf(PickSource s)` for tests.
  - `Director`: `Rng _steady` (`Fork(Director, 0)` in Awake) for the steady `Pick`; at `BeginSwarm` `_swarm =
    Fork(Swarm, (uint)SwarmsStarted)` for the type, the angle, the gate and the swarm's spread; per elite
    `Fork(Elite, (uint)_eliteRolls++)` for the type and affix (timer elites and `SpawnEliteAt`); `Pick(…, ref Rng rng)`;
    `RandomAngle()` and `SpawnAt`'s tries and depth use `RngStream.Spawns`; test getters `public int SwarmEntry`,
    `public float SwarmAngle`.
  - `ArenaGates`: `public int Pick(Vector3 player, float minDistance, ref Rng rng)`; the old overload uses
    `RngStream.Spawns`; `SpawnPoint` spread uses `RngStream.Spawns`.
  - `EnemyManager` (every draw in `Spawn` and the AI timers), `EnemyManager.Gilded` (blink), `EnemyProjectiles` (zone
    scatter, 372–373): `RngStream.Enemies`. Gilded coin angles and `PickupSystem`'s gold chance: `RngStream.Loot`.
    Projectile spin, pickup bob and pop velocity stay legacy (Misc).
  - `BossController`: `Rng _rng = Fork(Boss, KeyOf(def.id))` when the fight starts; every draw through it.
  - `Hypogeum` (`KeyOf("hypogeum")`), `BeastCages` (`"beast_cages"`): `Fork(Hazards, …)` in Awake.
  - `Interactables` (`Fork(Map, KeyOf("interactables"))`), `CrowdGifts` (`Fork(Gifts, KeyOf("crowd_gifts"))`),
    `ArenaMaster` (`Fork(Whims, KeyOf("arena_master"))`): the `uint _rng` fields and `Rand01()` become an `Rng _rng`
    made in Awake.
  - `GoldenRat` (`"golden_rat"`), `AshenStranger` (`"ashen_stranger"`), `RattlingCrate` (`"rattling_crate"`),
    `Bookmaker` (`"bookmaker"`): `Fork(Map, …)` at the top of `Start`; every `UnityEngine.Random` through it; delete the
    "not GameRandom" comments; test getters `GoldenRat.AppearAt` / `Hole`.
  - `ScavengerTrait`: `Fork(Traits, KeyOf("scavenger"))` when installed.
  - `WeaponSystem`, `PlayerProjectiles`, `VerdictTrait`, `BoonEffects`: crits and procs through `RngStream.Combat`.
  - A struct `Rng` held in a field is used in place (`_rng.Next01()`), never through a property or a copy.
- **Tests.** EditMode **`RandomStreamTests`**: after `Seed(12345)`, five `Next01(Cards)` and five `Value` each equal a
  hand-rolled xorshift32 from 12345; `BeginRun(s)` gives pairwise different first values across streams; the same `s`
  twice gives the same 100 values per stream; 1,000 `Combat` draws don't change the next `Cards` value; `Reseed(Cards, k)`
  twice → equal, `k` ≠ `k'` → different; `Fork(Map, KeyOf("a"))` ≠ `Fork(Map, KeyOf("b"))` and repeats after the same
  `BeginRun`; `Hash` is never 0 over 10,000 inputs including zeros and flips about half the bits on a one-bit change;
  `SeedText` / `TryParseSeed` round-trip ("7F3A-91C2", "7f3a91c2", "0x7F3A91C2"); `Range(int)` never returns its max; no
  allocation across 10,000 draws. PlayMode **`SeedReplayTests`** (the test arena, and the Slave Pit for the chests and
  the Golden Rat; `GameRandom.BeginRun(S)` before each load): record the first three chest placements, three swarms
  (`TriggerSwarmNow`: entry and angle), two elites (`TriggerEliteNow`: type and affix from `OnDirector`), four
  `CrowdGifts.Throw(null)` kinds, two whims, the first level-up's offer ids, the rat's `AppearAt` and `Hole`; load again
  with `S`, this time making 300 `Combat` draws and killing 20 enemies between events: every record is equal; load with
  `S'`: at least one differs; no GC allocation per frame across 5 s.

### R8-B daily core
- **`GameSession`** (`RunDef.cs`): `public static uint Seed { get; private set; }`; `public static DailyPlan Daily { get;
  private set; }`; `public static bool HasDaily => Daily.IsValid;` `public static bool DailyScored { get; private set; }`;
  `public static bool SuppressLoad;` (tests: record instead of loading) `public static string LastLoadPath { get; private
  set; }`; `public static uint FreshSeed()` (`GameRandom.Hash` of the clock's ticks and a counter); `StartRun(g, run)` =
  `StartRun(g, run, -runSeed if given else FreshSeed())`; `StartRun(g, run, uint seed)` clears the daily;
  `StartDaily(in DailyPlan plan, bool scored)` sets the daily, `Seed = plan.Seed`, `RunModifiers.Set(plan.Edicts)`, loads
  `plan.Run`'s first arena; `Retry()`: a daily → same seed, `DailyScored = false`; else `FreshSeed()`; `RetrySameSeed()`;
  `Load` (and Retry's reload) calls `GameRandom.BeginRun(Seed)` right before `LoadScene`; `Clear()` resets all of it.
- **`DailyPlan.cs`:** `public struct DailyPlan { public int Date; public uint Seed; public RunDef Run; public GladiatorDef
  Gladiator; public RunModifierDef[] Edicts; public bool IsValid => Date > 0 && Run != null && Gladiator != null; }`;
  `public static class DailyGames` with `Today()` (UTC), `DateKey(DateTime utc)` (yyyymmdd), `DateText(int)` ("22 OCT
  2026", invariant culture), `UntilNext(DateTime utcNow)`, `SeedFor(int date, string salt)` (`Hash(FNV-1a(salt),
  (uint)date, 0)`), `For(…)`: copies each list sorted by id (ordinal); arena = `earnedRuns[Hash(seed, 1, 0) % n]`,
  gladiator = `earnedGladiators[Hash(seed, 2, 0) % n]`, `meta.dailyEdicts` distinct edicts from the `dailyEligible`
  ones in `meta.edicts` (owned or not; partial Fisher–Yates with `Hash(seed, 3, i)`); empty lists → an invalid plan.
- **`SaveData`:** `public int dailyAttempt, dailiesPlayed, dailiesWon; public List<DailyRecord> dailies;` and
  `[Serializable] public struct DailyRecord { public int date; public string run, gladiator, edicts; public int outcome;
  public float elapsed; public int kills, renown; public long seed; }`; `Migrate` fills; `CurrentVersion = 3`.
- **`UnlockDef.cs`:** append `DailiesPlayed, DailiesWon` (scored dailies; amount).
- **`MetaConfig`:** `[Header("Daily Games (R8)")] public UnlockDef dailyUnlock; public float dailyRenownBonus = 0.5f;
  public int dailyEdicts = 2; public string dailySalt = "gladiator-daily-1"; public int dailyLedger = 30;` `RenownFor` also
  × `(1 + dailyRenownBonus)` when `run.DailyScored`.
- **`MetaProgress`:** `RunSummary`: `public uint Seed; public int DailyDate; public bool DailyScored;` `RunRewards`:
  `public float DailyBonus;` `Apply`: when `DailyScored`, `dailiesPlayed++`, a win `dailiesWon++`, append a
  `DailyRecord`, trim to `meta.dailyLedger`; `Met` / `Progress` for the two kinds; `public static bool DailyOpen(MetaConfig
  meta)`; `public static bool DailyAttempted(int date)`; `public static bool BeginDaily(int date)` (true = scored: sets
  `dailyAttempt` and saves at once; false = already attempted, practice) and its pure overload with a `SaveData`.
- **`RunEndController.Summary`:** `s.Seed = GameRandom.RunSeed; s.DailyDate = GameSession.HasDaily ? GameSession.Daily.Date
  : 0; s.DailyScored = GameSession.DailyScored;`
- **`DailySetup`** (`Gladiator/Setup/Meta/Create Daily Data`): the three unlocks with texts; `cfg.dailyUnlock = UL_Daily`;
  `MetaSetup.CreateOrUpdate()`; idempotent.
- **`PlaytestBot`:** the summary adds ` seed XXXX-XXXX` and, in a daily, ` daily yyyymmdd`.
- **Tests.** EditMode **`DailyTests`**: `For` is the same for the same date and lists, changes with the date, ignores the
  lists' order, picks only from the given (earned) lists, gives `dailyEdicts` distinct `dailyEligible` edicts, and is
  invalid with no arena or gladiator; `DateKey` / `DateText` / `UntilNext` at 23:59:30 UTC; `SeedFor` changes with the
  salt; `BeginDaily` true once then false the same day, true the next; `Apply` with `DailyScored` counts, records and trims
  the ledger to 30, without it touches none; `RenownFor` ×1.5 only when scored (and stacks with edicts); `Met` / `Progress`
  for the two kinds; a v2 JSON migrates to v3; `GameSession` with `SuppressLoad`: `StartRun` sets a seed and
  `LastLoadPath`, `Retry` of a normal run changes the seed, `RetrySameSeed` keeps it, `Retry` of a daily keeps it and turns
  `DailyScored` off, `-runSeed` is honoured (through a test hook on the parsed value, not the real command line).

### R8-C daily screens
- **`DailyPanel`** (`Assets/Scripts/UI/DailyPanel.cs`): `public DailyPanel(TMP_FontAsset font, Material fontMat)`;
  `Build(Transform root, CodexConfig codex)`; `Show(in DailyPlan plan, SaveData save, MetaConfig meta, DateTime utcNow)`;
  `Tick(DateTime utcNow)` (the countdown, once a second); test API `Scored`, `StatusText`, `LedgerRows`. Layout and texts:
  spec §3.7.
- **`MainMenu`:** DAILY as the second menu button (the others move down one slot), greyed with the `UL_Daily` hint until
  `DailyOpen`, a gold dot while today's attempt is open; `Screen.Daily` with `Chrome("THE DAILY GAMES — …", …)`; FIGHT! →
  `GameSession.StartDaily(plan, MetaProgress.BeginDaily(plan.Date))`; the plan from `DailyGames.For(Today(), meta,
  earned runs, earned gladiators)`; clicking the edicts line opens `EdictsPanel.Open(plan.Edicts)` (read-only); bot
  `-daily yyyymmdd` → `StartDaily(plan, false)`; test API `Daily`, `DailyShown`, `OpenDaily()`, `TodayPlan`.
- **`RunEndPanel`:** the note line "SEED 7F3A-91C2" or "DAILY 22 OCT · SCORED / PRACTICE"; the Renown breakdown includes
  "×1.5 daily"; buttons RETRY [R] · SAME SEED [S] · MAIN MENU [M] for a normal run (SAME SEED →
  `GameSession.RetrySameSeed()`), PRACTICE [R] · MAIN MENU [M] for a daily.
- **`Hud`:** a "DAILY" chip beside the timer (gold scored, grey practice), built at Start.
- **`StatsPanel`:** the seed in the header ("SEED 7F3A-91C2").
- **`CodexPanel`:** RECORDS rows "Dailies played", "Dailies won".
- **Tests.** PlayMode **`DailyMenuTests`** (the menu scene, temp save, `GameSession.SuppressLoad`): DAILY greyed before
  `UL_Daily`, open after; the screen shows the plan's arena, gladiator and two edicts; FIGHT! starts the plan (session
  gladiator, run, edicts, seed) and marks the attempt; opening again says PRACTICE and FIGHT! doesn't spend another; the
  ledger shows a record put in the save; after a run's end the end screen shows the seed and SAME SEED keeps it; the HUD
  chip shows in a daily; the pause page shows the seed.

### Wave 2 merge order and the orchestrator's steps
**A → B → C.** Then: `Gladiator/Setup/Meta/Create Daily Data`. No scene rebuild. Tests: `RandomStreamTests`, `DailyTests`,
full EditMode; `SeedReplayTests`, `DailyMenuTests`, full PlayMode (PlayMode tests that leaned on the old constant chest or
gift order may need `GameRandom.BeginRun(1)` in their SetUp: fix them here). Bot runs: two Slave Pit runs with the same
`-runSeed` (compare the chest spots, the swarms' types and the first offers in the logs) and one with another seed; a
`-daily` run; perf runs now pass a fixed `-runSeed` (`Tools/perfrun`). Screenshots: the main menu with DAILY, the daily
screen (scored and practice), the end screen with the seed. Shared docs: CONCERNS (the repeating RNG found in spec §3.2,
closed by R8), DECISIONS §6.12–21, GDD, UNLOCKS, BACKLOG, PROGRESS, release notes.

---

## Wave 3 — R6 Skins (R6-art may start during wave 1)

### Names every R6 part uses
- **`SkinDef`** (`Assets/Scripts/Gameplay/Stats/SkinDef.cs`, menu `Gladiator/Skin`, `fileName = "SK_"`): `public string
  id, displayName; [TextArea] public string description; public GladiatorDef gladiator; public GameObject model; public
  Sprite portrait; [Tooltip("Empty = free.")] public UnlockDef unlock; public Color[] chips = new Color[3];`
- **The skin table** `BlenderSrc/characters/skins.json` (owned by R6-art, read by `SkinSetup`):
  ```json
  { "skins": [ { "id": "nobody_freedman", "gladiator": "nobody", "script": "ch_nobody.py", "base": "CH_Nobody",
      "name": "Freedman", "suffix": "_Freedman", "description": "…", "chips": ["cloth_white", "bronze_l", "skin_light"],
      "price": 250, "champion": false,
      "remap": [ { "from": "cloth_red", "to": "cloth_white" } ] } ] }
  ```
  Ids `<gladiator id>_<name in snake case>`; gladiator ids as in `GL_*.asset` (`nobody`, `pit_goblin`, `blood_knight`,
  `murder_machine`, `thunder_champion`, `golden_idol`, `ashen_oracle`, `net_weaver`, `beastmaster`, `asterion`).
  `price` > 0 = Market; `"champion": true` = `WinWithGladiator <gladiator> 3`.
- **Files:** models `Assets/Art/Models/Characters/Skins/<base><suffix>.fbx`; data `Assets/Data/Skins/SK_<Base without
  CH_><Suffix>.asset` (e.g. `SK_Nobody_Freedman`); unlocks `MetaSetup.Dir + "UL_Skin_<Nobody>_<Freedman>.asset"`;
  portraits `Assets/Art/UI/Portraits/PT_<gladiator id>_<suffix lower, no underscore>.png`.
- **Meta:** `MetaConfig.skins`; `SaveData.equippedSkins`; `MetaProgress.IsOwned` / `EquippedSkin` / `Equip` / `Unequip` /
  `SkinsOf`; `GameSession.Skin`; `GladiatorLoadout.NextSkinOverride` / `CurrentSkin`.
- **Menu:** `SkinStrip` (UI); `MainMenu.Skins` / `CycleSkin(int)` / `BuySkin()`. **Command line:** `-skin <id>`.

### Parts
| Part | Owns | Done when |
|---|---|---|
| **R6-art** | `BlenderSrc/gladiator_kit.py` (the skin variable only); new `BlenderSrc/characters/skins.json`, `BlenderSrc/characters/build_skins.py`; `Assets/Art/Textures/Palette/palette.json` + the regenerated `PAL_Master.png` and `palette_index.md`; the 20 FBX under `Assets/Art/Models/Characters/Skins/`; `Screenshots/blender/skins_*.png`; `docs/tracks/R6-art.md` | The twenty skins of spec §4.4 (themes may change; the rules of §4.2 may not), new swatches only in free cells, every base script still exports exactly as before with no variable set (re-export one and diff its tri count and UV cells), one lineup per gladiator on a pale and a dark floor. |
| **R6-B data + loadout** | new `Assets/Scripts/Gameplay/Stats/SkinDef.cs`; `SaveData.cs`, `MetaProgress.cs`, `MetaConfig.cs`; `RunDef.cs` (`GameSession.Skin`); `Assets/Scripts/Gameplay/Gladiators/GladiatorLoadout.cs`; `Assets/Editor/Setup/PortraitBaker.cs`; new `Assets/Editor/Setup/SkinSetup.cs`; new tests `Assets/Tests/EditMode/SkinTests.cs`, `Assets/Tests/PlayMode/SkinLoadoutTests.cs` | Save v4, ownership and equip rules, the loadout swap with the one shared material, skin portraits baked, `SkinSetup` builds every `SkinDef` and unlock from `skins.json`; ccheck errors=0; tests written. |
| **R6-C menu** | new `Assets/Scripts/UI/SkinStrip.cs`; `MainMenu.cs`; `RosterGrid.cs`; `CodexPanel.cs`; new tests `Assets/Tests/PlayMode/SkinMenuTests.cs` | The strip under the diorama (cycle, preview, equip, buy), the diorama and roster tile in the equipped skin, the launch (normal and daily) with the skin, the Codex's reward text, `-skin`; ccheck errors=0 (only R6-STUB(b)); tests written. |

### R6-art
- **`gladiator_kit.py`:** `set_swatch(obj, swatch, …)` maps `swatch` through the remap in `os.environ["GLADIATOR_SKIN"]`
  (JSON `{"suffix": "…", "remap": {"from": "to", …}}`, parsed on every call: the character scripts reload the kit);
  `export_fbx(root, rel_path)` with a skin set writes `Characters/Skins/<basename><suffix>.fbx`; with no variable both
  behave exactly as today.
- **`build_skins.py`** (`Tools/bheadless BlenderSrc/characters/build_skins.py skin=<id>|all [review=1]`): for each skin,
  set the variable (the list `remap` becomes the dict), run `BlenderSrc/characters/<script>` with `runpy.run_path`, skip
  its `save_blend` (a skin variable makes `save_blend` a no-op, or the driver patches it), clear the variable; `review=1`
  renders the lineup per gladiator.
- **Palette:** append `silver`, `silver_d`, `obsidian` (metal), `cloth_black`, `cloth_purple`, `cloth_teal`
  (cloth_leather), `ivory` (neutral), `jade`, `jade_d` (misc) in free columns (or other free cells: never move one), then
  `Tools/build_palette.py`. No glow, hazard or enemy_accent swatch as a main colour.

### R6-B data + loadout
- **`SaveData`:** `public List<string> equippedSkins;` `Migrate` fills; `CurrentVersion = 4`.
- **`MetaConfig`:** `[Header("Skins (R6)")] public SkinDef[] skins = Array.Empty<SkinDef>();`
- **`MetaProgress`:** `public static bool IsOwned(SkinDef s, SaveData d)` (`unlock == null || unlocked`) + `Data`
  overload; `public static SkinDef EquippedSkin(GladiatorDef g, MetaConfig meta, SaveData d)` (the owned skin of `g`
  whose id is in `equippedSkins`, else null = the default) + overload; `public static bool Equip(SkinDef s)` (owned only;
  removes `g`'s other ids; saves) + pure overload; `public static void Unequip(GladiatorDef g)`; `public static int
  SkinsOf(GladiatorDef g, MetaConfig meta, List<SkinDef> into)` (meta order).
- **`GameSession`:** `public static SkinDef Skin { get; set; }` (kept by Retry, reset by `Clear`).
- **`GladiatorLoadout`:** `public static SkinDef NextSkinOverride;` (tests; cleared after use) `public SkinDef CurrentSkin
  { get; private set; }`; the model is `skin.model` when `skin != null && skin.gladiator == def && skin.model != null`,
  else `def.model`; every renderer still gets `characterMaterial`.
- **`PortraitBaker`:** also bakes every `MetaConfig.skins` entry with its gladiator's framing (and its `PortraitHidden`
  parts) to the portrait path above, and sets `SkinDef.portrait`.
- **`SkinSetup`** (`Gladiator/Setup/Gladiators/Create Skin Data`): reads `skins.json` (JsonUtility on a wrapper class);
  per skin: the `SkinDef` (gladiator by id from `GladiatorSetup.All()`, model by path, chips from `palette.json` by
  swatch name), its unlock (Market with `renownCost = price`, or Challenge `WinWithGladiator <gladiator> 3`, hint and
  description in the UNLOCKS style); a missing FBX logs an error and skips that skin; `cfg.skins`; then
  `MetaSetup.CreateOrUpdate()`; idempotent.
- **Tests.** EditMode **`SkinTests`**: `IsOwned` (free, Market before / after `Buy`, champion before / after the wins);
  `Equip` refuses an unowned skin, keeps one per gladiator, `Unequip` returns the default; `EquippedSkin` ignores unknown
  or locked ids; a v3 JSON migrates to v4. PlayMode **`SkinLoadoutTests`** (the test arena; a `SkinDef` made in the test
  whose model is a copy of the Nobody's prefab): the player spawns with the skin's model; the animator finds every part;
  every renderer's `sharedMaterial` is the one `characterMaterial` (one distinct material on the player); a skin of
  another gladiator is ignored; no skin → the gladiator's own model.

### R6-C menu
- **`SkinStrip`** (`Assets/Scripts/UI/SkinStrip.cs`): `public SkinStrip(TMP_FontAsset font, Material fontMat)`;
  `Build(Transform root, Vector2 anchor, Vector2 pos, Action<SkinDef> onPreview)`; `Show(GladiatorDef g, MetaConfig meta,
  SaveData save)` (the equipped skin first); `Count` (default + skins), `Index`, `SkinDef Current` (null = default),
  `bool CurrentOwned`, `bool Cycle(int dir)` (previews; equips when owned, `Unequip` on the default), `BuyResult Buy()`
  (Market only; equips after buying). Texts: spec §4.1.
- **`MainMenu`:** the strip under the diorama on the select screen; Q / E and LB / RB cycle, F / gamepad North buys, the
  arrows on the strip for the mouse; `ShowHero(g, skin)` (the cache keys by model prefab, so skins cache too); `Launch`
  and the daily's launch set `GameSession.Skin = MetaProgress.EquippedSkin(g, meta)` (bot `-skin <id>`: any); the key
  line adds "Q / E: skin"; test API `Skins`, `CycleSkin(int)`, `BuySkin()`.
- **`RosterGrid`:** `public void SetPortrait(int slot, Sprite s)`; the menu sets each earned tile to its equipped skin's
  portrait (fallback: the gladiator's).
- **`CodexPanel`:** `Reward(u)` names a skin ("Reward: Skin — Freedman (The Nobody)").
- **Tests.** PlayMode **`SkinMenuTests`** (menu scene, temp save, `SuppressLoad`): cycling changes the diorama's model; a
  Market skin shows its price and `Buy` with Renown owns and equips it (without Renown: `TooExpensive`, nothing changes);
  a champion skin can't be bought or equipped before its wins; the equipped skin survives a menu reload; FIGHT! sets
  `GameSession.Skin`; the roster tile shows the skin's portrait.

### Wave 3 merge order and the orchestrator's steps
**art (any time) → B → C.** Then: let Unity import the palette and the twenty FBX (check `ModelImportRules` applied: scale
1, no materials), `Gladiator/Setup/Gladiators/Create Skin Data`, `Gladiator/Setup/UI/Bake Portraits`. No scene rebuild.
Tests: `SkinTests`, full EditMode; `SkinLoadoutTests`, `SkinMenuTests`, full PlayMode. A bot run with `-skin` per
gladiator (spawns, no errors), the material count in a scene stays ≤ 8. Screenshots: the select screen with the strip
(1080p, 720p), each gladiator's lineup in game on a pale and a dark arena. Shared docs: ART_DEBT if any skin shipped
after 3 iterations, DECISIONS §6.22–27, GDD, UNLOCKS, PROGRESS, release notes.

---

## Files edited by more than one wave (each wave in turn, never at once)
| File | Wave 1 | Wave 2 | Wave 3 |
|---|---|---|---|
| `SaveData.cs`, `MetaProgress.cs`, `MetaConfig.cs` | R7-B | R8-B | R6-B |
| `UnlockDef.cs`, `RunEndController.cs` | R7-B | R8-B | — |
| `RunDef.cs` (`GameSession`) | — | R8-B | R6-B |
| `MainMenu.cs`, `CodexPanel.cs` | R7-C | R8-C | R6-C |
| `RunEndPanel.cs`, `Hud.cs` | R7-C | R8-C | — |
| `PlaytestBot.cs` | R7-A | R8-B | — |
| `Director.cs`, `EnemyManager.cs`, `Interactables.cs`, `LevelUpController.cs`, `ArenaMaster.cs` | R7-A | R8-A | — |

**Not touched by anyone in R7, R8 or R6:** the scenes and `MenuSceneBuilder.cs`, `ArenaBuilder.cs`, the arena setups and
layouts, `BossDef.cs`, `BossFight.cs`, `GladiatorDef.cs`, `GladiatorSetup.cs`, `IconSetup.cs`, `CodexSetup.cs`,
`CodexConfig.cs`, `LanistaTablet.cs`, `UpgradeConfig.cs`, the base character scripts (`ch_*.py`), and the shared docs
(the orchestrator's).

## Open questions
Q1–Q14 are in `docs/tracks/R7-design.md` §8, each with a recommendation. For the orchestrator only: **Q15. Eight code
parts in three waves (recommended)**, or fewer sessions to save usage: fold R7-B into R7-A, R8-C into R8-B and R6-C into
R6-B (five code parts; each then owns more files and runs longer).
