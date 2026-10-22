# R3 setup — notes (Asterion, the Chained)

Branch `claude/sleepy-pare-af0042` (fast-forwarded to `origin/phase5-asterion` at 2823019). Code only: no Unity, no
Blender. Built against the contract's names (`docs/tracks/R3-asterion.md`) and the design part's § "Setup"
(`docs/tracks/R3-design.md`), modelled on `BeastmasterSetup.cs` / `GladiatorSetup.cs`.

## What I built
- **`Assets/Editor/Setup/AsterionSetup.cs`** (new; menu `Gladiator/Setup/Gladiators/Create Asterion Data`). Every asset
  is created once and is designer-owned afterwards; only empty references are filled in (the meshes, the model, the
  links on `GL_Asterion`).
  - `WP_LabyrinthChain` (`labyrinth_chain`, "Labyrinth Chain", the design's description): `Sweep`; damage 15, crit
    0.05 ×2, knockback 7, cooldown 1.6, swing 0.8, windup 0.32, range 5.2, arc 240, width 0.5, acquire 5.6, amount 1,
    tipLength 1.4, meshScale 1; the Sweep header: sweepTime 0.3, whirlTimeMul 1.5, sweepOutward 0.35, tipKnockbackMul
    1.6, linkLength 0.6, chainHeight (1.05, 0.55), weightOffset 0.33, modelPart `"Chain"`; `mesh` = the FBX's `Weight`,
    `linkMesh` = its `Link` (found by name; a warning if `Weight` is missing); the seven levels as designed (max level 8).
  - `UP_W_LabyrinthChain` (`weapon_labyrinth_chain`, glyph "LC", Weapon, maxLevel 8, weight 8, tags `Heavy`: Q8).
  - `TR_Gore` ("Gore", id `gore`) and `TR_Rage` ("Rage", id `rage`) with the design's words; their numbers are the rage
    part's class defaults (the setup doesn't repeat them).
  - `MovementConfig_Asterion` (a copy of the player's: capsule 1.85 / slide 1.1 / radius 0.45) and `PartAnim_Asterion`
    (a copy: stride 1.45, bob 0.08, lean 16, armSwing ×0.6, weaponArmSwingScale 0.25, yaw 120 → −120, raise 20,
    bodyTwistScale 0.5, idleHeadLift 18, swingCurve (0,0) (0.4,0.04) (0.78,1) (1,1), poseSharpness 24).
  - `GL_Asterion` (`asterion`, "Asterion, the Chained", the design's description and tradeoff; MaxHp 130, MoveSpeed
    0.95; cameraTargetHeight 1.5; `CH_Asterion`; the chain; traits { Gore, Rage }; `UL_Asterion`).
  - `UL_Asterion` (`asterion`, the design's description and hint; `DefeatBoss eternal_victor 1`; addsToPool { the card }).
  - **The tease:** loads `BossSetup.EternalVictorPath`; only if its `revealText` is empty: "BELOW THE SAND, A CHAIN
    SNAPS.", `revealUnlock` = `UL_Asterion`, `revealDelay` 1.2, `revealHold` 1.8 (`BossSetup.cs` untouched; the order of
    the setups doesn't matter; a missing Victor logs a warning).
  - Then `UpgradeSetup.Refresh()`, `MetaSetup.CreateOrUpdate()`, `SaveAssets`.
- **`GladiatorSetup.cs`:** `AsterionSetup.CreateOrUpdate()` right after the Beastmaster's; `GL_Asterion` last in `All()`.
- **`PortraitBaker.cs`:** `public static readonly string[] PortraitHidden = { "Chain" }`; `Rig.Render` sets those parts
  inactive on its instance and, only when it hid one, frames and renders the active renderers only. Both views (the
  select screen's head and the Codex atlas, which uses the same rig). A model with no such part bakes exactly as before.
- **`BossDef.cs`:** header "Reveal (R3: a tease at his fall)": `revealText` "", `revealUnlock` null, `revealDelay` 1.2,
  `revealHold` 0, `revealColor` (0.86, 0.78, 0.62).
- **`BossFight.cs`:** in `Stage.Falling`, once `_t >= revealDelay` (checked once per fall): if there's a `revealText`
  and the unlock is null or locked (`MetaProgress.IsUnlocked`), raise it (`AnnounceKind.Line`, `revealColor`, 2.6 s),
  `Rattle` + `BossRoar` at `EnemyManager.arenaCenter`, `ScreenShake.Add(0.15)`, `RevealShown = true`; the fall then ends
  at `fallSeconds + revealHold`. `public bool RevealShown { get; private set; }`. A boss with no `revealText` never reads
  the save and falls at `fallSeconds`, as before.
- **The icon:** `ICONS["weapon_labyrinth_chain"]` appended last in `PixelSrc/upgrade_icons.py` (two bronze rings and
  their links down to a grey block with a pale meander, a ring bolt on top); the atlas regenerated: 65 rows, the new one
  is row 64. Before adding it I regenerated the atlas unchanged and it was byte-identical to the committed PNG; after,
  the first 64 rows are pixel-identical (no other row moved). Iteration 1 read as a stick; iteration 2 shipped
  (preview in the shared notes folder, `r3_setup_icon.png`).
- **`Assets/Tests/PlayMode/AsterionTests.cs`** (new; runs after the setup, the icons, the bakes and the menu rebuild):
  - `HisData_IsTheCard` (id, name, 130 HP, 0.95 speed, `CH_Asterion` with a `Chain` part, capsule 0.45, his own anim
    profile with the ±120° yaw, the chain's pattern / range / arc / tip / damage / cooldown / max level / `Weight` +
    `Link` meshes / `modelPart`, the traits Gore then Rage, the unlock, **last in `GladiatorSetup.All()`**, portraits
    baked);
  - `TheChainsCard_IsHeavy_WithAnIcon` (id, glyph, kind, weapon, max level 8, `Heavy`, an icon row, in the catalog);
  - `TheUnlock_IsTheEternalVictor_AndHoldsTheChainBack` (the condition, the pool, in MetaConfig; pure: a fresh save and
    one with only Gorm / the Emperor's Champion beaten don't meet it, one with `eternal_victor` does; the card is locked
    until `asterion` is unlocked);
  - `TheTease_IsOnTheEternalVictor_AndNoOtherBoss` (the Victor's five fields, not orange-red; every other BossDef has
    no `revealText`);
  - `ThePortraitBakes_HideTheChain` (`PortraitHidden` contains `Chain`; his model has exactly that part; no other
    gladiator's model has a part with those names);
  - `TheSelectScreen_EndsWithHim` (the rebuilt menu's `gladiators` ends with him; locked on a fresh save);
  - `ARunAsHim_HasHisModel_Capsule_StatsAndTraits_AndTheChainSweeps` (TestArena: `CH_Asterion` under Visual with
    `Head` / `ArmR` / `Chain`, the 0.45 m × 1.85 m capsule, 130 HP, MoveSpeed 0.95, the chain as his one weapon, owned at
    level 1, `Gore` and `Rage` installed; a rooted Brute 4.5 m ahead is swept and hit; the model's `Chain` is hidden
    during the sweep and shown again 0.6 s later);
  - `BeatingTheEternalVictor_UnlocksHim_AndTheChainJoinsThePool` (Slave Pit, fresh save: locked, card held back;
    `MarkBossDefeated("eternal_victor")` + a survived run → `asterion` in NewUnlocks, on disk, available, the card free);
  - the tease, in the **Slave Pit's** boss fight with a copy of Gorm's BossDef (the test arena has no BossFight):
    `TheTease_ComesOnceDuringTheFall_WhileItsUnlockIsLocked_AndHoldsTheFall` (one Line with the text, ≥ revealDelay in
    real time, the fall ≥ fallSeconds + revealHold), `TheTease_StaysQuiet_OnceItsUnlockIsEarned` (none, no hold),
    `AnOlderBoss_FallsExactlyAsBefore` (Gorm as he is: none, the fall ≈ fallSeconds, then the hoard).

## Stubs (`// R3-STUB(chain|rage)`): the only errors ccheck shows until the chain and rage parts merge
- **chain:** `WeaponPattern.Sweep`; `WeaponDef.sweepTime`, `whirlTimeMul`, `sweepOutward`, `tipKnockbackMul`,
  `linkLength`, `chainHeight`, `weightOffset`, `modelPart`, `linkMesh`; `PartAnimProfile.bodyTwistScale`,
  `idleHeadLift`; `WeaponSystem.Sweeps` (tests).
  `AsterionSetup.cs` lines 63, 64, 107, 114 (`Sweep(w)`, whose body sets the eight fields, 131–139), 122;
  `AsterionTests.cs` 115, 124–126, 276, 279, 282.
- **rage:** `GoreTrait`, `RageTrait` (setup 69, 71; tests 130, 131), `Gore`, `Rage` (tests 263, 264).

## Verified
- `Tools/ccheck` on this branch: Core, Gameplay, UI ok; **Gladiator.Editor errors=15, every one a chain or rage name
  above** (AsterionSetup.cs only); the test assemblies come after it, so ccheck stops there.
- **With temporary local stubs of exactly those names** (an enum value, the fields, `Sweeps`, four empty classes; never
  committed, removed after), ccheck gave **errors=0 for all six assemblies, including both test assemblies**: my code
  compiles against the contract's names as they're spelled.
- The icon atlas: regeneration is deterministic, the old 64 rows unchanged, the new row 64 `weapon_labyrinth_chain`.
- Read, not run: no existing test counts the roster, the unlocks or the icon rows in a way the new ones break; no
  existing test waits on the Eternal Victor's hoard (his fall is 1.8 s longer while Asterion is locked).

## Unverified (needs the orchestrator's Editor)
- Every asset: none exists until `Create Gladiator Data` runs. The FBX's mesh names (`Link`, `Weight`) are read from the
  FBX's strings, not from an import.
- All the tests (none has run). The sweep test depends on the chain part's timeline; the tease timing tests allow +0.5 s
  (reveal) and +0.8 s (fall) of frame slack.
- The tease on screen (the HUD shows the new Line over "THE ETERNAL VICTOR FALLS!" at 1.2 s, as the design says), its
  sounds at the stage, and whether 1.8 s of hold reads well.

## Decisions (one-line reasons)
1. **The traits' numbers stay in the rage part's classes** — the assets get them from the field defaults; repeating
   them in setup would be two sources of truth (and more stubs).
2. **The tease checks the unlock once, at `revealDelay`** — the unlocks are only granted after the run ends, so the
   answer can't change during the fall, and a boss with no line never touches the save.
3. **`PortraitBaker` filters renderers only when it hid a part** — so every other model bakes from exactly the same
   renderer list as before (inactive renderers included, as today).
4. **The tease tests use the Slave Pit's BossFight with a copy of Gorm's BossDef** — the test arena has no BossFight
   (its scene has no BossFight component).
5. **`AsterionSetup` calls `UpgradeSetup.Refresh` and `MetaSetup.CreateOrUpdate` itself** (as the design asks), so its
   own menu item works alone; `GladiatorSetup` calls them again after it (harmless).
6. **The trait asset ids are `gore` and `rage`** (the source ids `gore` / `bellow` are the rage part's constants).
7. **Tests that need `Gladiator.Editor` (`GladiatorSetup.All()`, `PortraitBaker.PortraitHidden`) use reflection** —
   the test asmdefs don't reference the editor assembly, and they aren't mine to change.

## After the merge (chain → rage → setup), the orchestrator runs, in order
1. `Gladiator/Setup/Gladiators/Create Gladiator Data` (calls `AsterionSetup`: every asset above, the tease on
   `BD_EternalVictor`, the catalog, MetaConfig, and `IconSetup.Apply`). `Create Asterion Data` alone does all but the icons.
2. `Gladiator/Setup/Upgrade Icons` (the chain's card gets row 64; harmless if step 1 already did it).
3. `Gladiator/Setup/UI/Bake Portraits` (PT_asterion, with the chain hidden).
4. `Gladiator/Setup/Meta/Bake Codex Portraits` (the Codex atlas; same rig, chain hidden).
5. `Gladiator/Setup/Build Main Menu` (`menu.gladiators = GladiatorSetup.All()`: Asterion last).
6. Then the PlayMode tests: `AsterionTests` (after the art's FBX are imported) plus `ChainSweepTests` / `RageTests`.

## Needs from others
- **chain / rage:** nothing beyond the contract's names as spelled (listed under "Stubs").
- **orchestrator:** the menu items above; `.meta` files for the two new `.cs` files come from the Editor's import.
