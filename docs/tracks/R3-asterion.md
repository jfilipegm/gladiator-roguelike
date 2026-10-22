# R3 contract — Asterion, the Chained (orchestrator, 2026-10-22 00:10; DRAFT until the design part answers)

**Item branch:** `phase5-asterion`, cut from `main` (v0.19.0). Worker branches merge into it; the orchestrator does every
merge. **Release:** v0.20.0. **Source:** CLAUDE.md §6.7, the roster's last row: "Asterion, the Chained | Minotaur
gladiator dragging a chain | Rage meter; chain sweeps; tragic/mythological | Defeat the final boss". `docs/ROADMAP.md`
§4 "R3". Every gladiator needs a unique silhouette, weapon, starting ability, passive, strength, weakness, personality and
unlock (§6.7), and is identified by silhouette → weapon → head → motion → VFX (§7.2).

## Shared facts (fixed now)
- **Ids:** gladiator `asterion`; unlock `UL_Asterion` = `DefeatBoss eternal_victor` (the Colosseum's boss, R2).
- **Assets:** `Assets/Data/Gladiators/GL_Asterion.asset`; the unlock in `MetaSetup.Dir`.
- **Models** (FBX under `Assets/Art/Models/`):
  - `Characters/CH_Asterion`: ≤ 1,200 tris; rigid parts named like the other gladiators' (`Body`, `Head`, `ArmL`, `ArmR`,
    `LegL`, `LegR`), plus the chain's parts.
  - His chain weapon: a `WP_*` model.
  - Follow `BlenderSrc/characters/ch_beastmaster.py` and `ch_murdermachine.py` (the conventions, the palette, the export).
- **The portrait:** `Gladiator/Setup/UI/Bake Portraits` renders it from his model, so nothing to draw.
- **The pattern of a gladiator item:** read how the Beastmaster (Phase 5.15) and the Net-Weaver (5.14) were built: their
  `GLADIATORS.md` sections, `BeastmasterSetup.cs` / `GladiatorSetup.cs`, their traits under
  `Assets/Scripts/Gameplay/Gladiators/`, their weapons and tests.

## Parts
**Wave 1 (now: design and art, no code):**
| Part | Owns | Done when |
|---|---|---|
| **design** | `docs/GLADIATORS.md` § "Asterion, the Chained"; `docs/tracks/R3-design.md`; this contract's sections below "Wave 2" (fill them in) | His §6.7 card (silhouette, weapon, starting ability, passive, strength, weakness, personality, unlock) with numbers. The chain-sweep weapon's pattern and levels. The Rage meter (what fills it, what it does when full, how it drains, the HUD). His traits. How he plays differently from the Murder Machine (the other heavy) and the Net-Weaver (the other chain or reach). His look for the art part, and the code parts' split with files, fields, methods and tests. Open questions with a recommendation each. |
| **art** | `BlenderSrc/characters/ch_asterion.py`, the chain weapon's script (beside the other weapons), their FBX and `.blend`, reviews, `docs/tracks/R3-art.md` | CH_Asterion in budget: a chibi minotaur (bull head with horns, a heavy build, a broken shackle and collar, a long chain dragging behind him), rigid parts with joint pivots, 3 angles + 64 px, plus a lineup with every gladiator (he must read distinct, above all from the Murder Machine and the Beastmaster). The chain weapon in segments or a few parts that code can swing. Tragic, not monstrous: a prisoner of the labyrinth, a rope-worn bronze collar, a few scars. |

**Wave 2 (code, after the design; branch from `phase5-asterion`):** filled in by the design part (2026-10-22), then
finalised by the orchestrator. Fields, methods and tests for each part: `docs/tracks/R3-design.md` § "For each code
part"; the card: `docs/GLADIATORS.md` § "Asterion, the Chained".

**Names every part uses:**
- **Pattern:** `WeaponPattern.Sweep` (appended after `Lob`: index 10). **Weapon:** `WP_LabyrinthChain` (id
  `labyrinth_chain`), card `UP_W_LabyrinthChain` (id `weapon_labyrinth_chain`, glyph "LC").
- **Traits:** `TR_Gore` (`GoreTrait` → `Gore`, source id `gore`) and `TR_Rage` (`RageTrait` → `Rage`, source id `bellow`
  for the bellow; `Rage.Mode` Calm / Unchained / Spent).
- **The links between parts:** `WeaponSystem.WhirlSweeps` (Rage sets it, Sweep weapons read it);
  `PlayerPartAnimator.ScaleBoost` / `ExtraLean` / `HeadDrop` (Rage sets them); `WeaponSystem.Sweeps` / `Whirls` /
  `SweepHits` / `WeightHits` (the bot line reads them).
- **Models:** `CH_Asterion` with the part **`Chain`** (the trailing chain and stone; the weapon hides it while it
  swings); `WP_LabyrinthChain` with the meshes **`Link`** and **`Weight`**. If the art part ships other names, its notes
  win and the setup part writes them into the data.
- **The tease:** `BossDef.revealText` / `revealUnlock` / `revealDelay` / `revealHold` / `revealColor`;
  `BossFight.RevealShown`.

| Part | Owns | Done when |
|---|---|---|
| **chain** | `Assets/Scripts/Gameplay/Weapons/WeaponDef.cs` (`Sweep` + its header of fields); `WeaponSystem.cs` (the sweep, backswings, the whirl flag, the drawing, the model part, counters, three pure statics); `PlayerProjectiles.cs` (`MaxOrbiters` 40); `Assets/Scripts/Gameplay/Animation/PlayerPartAnimator.cs` and `PartAnimProfile.cs` (mirror, spin, scale/lean/head hooks, idle head lift, twist scale); new tests `SweepMathTests` (EditMode), `ChainSweepTests` (PlayMode) | The chain sweeps its arc over time, hits what it passes once each, the stone crits and shoves harder, the push is sideways (radial for bosses), sweeps alternate and backswings follow without a wind-up, `WhirlSweeps` makes them 360°; every other weapon and gladiator unchanged; ccheck errors=0; tests written. |
| **rage** | new `Assets/Scripts/Gameplay/Gladiators/GoreTrait.cs`, `RageTrait.cs`; `Assets/Scripts/UI/Hud.cs` (`DrawRage`); `Assets/Scripts/Gameplay/Dev/PlaytestBot.cs` (the summary line); new tests `RageTests` (PlayMode) | Gore (calm and Unchained), the meter (press, hits, cooling), Unchained (bellow, stats, no knockback, the whirl, poses) and Spent, `Halt`; the HUD bar and its labels; the bot line; ccheck errors=0 (only on the chain part's names until it merges); tests written. |
| **setup** (codes against the other two; merges last) | new `Assets/Editor/Setup/AsterionSetup.cs`; `GladiatorSetup.cs` (the call and `All()`); `Assets/Scripts/Gameplay/Boss/BossDef.cs` and `BossFight.cs` (the tease); `PixelSrc/upgrade_icons.py` and the regenerated `Assets/Art/Textures/UI/UpgradeIcons.png` / `.txt`; new tests `AsterionTests` (PlayMode) | Every asset of R3-design's setup list with its numbers and words, the unlock (`DefeatBoss eternal_victor 1`, the chain into the pool), the tease on `BD_EternalVictor` set from `AsterionSetup`, the chain's icon; older bosses fall exactly as before; ccheck errors=0; tests written. |

**No file is owned by two parts.** Not touched by anyone in R3: `PartRig.cs`, `EnemyManager*.cs`, `CompanionPack.cs`,
the other traits, `BossController.cs`, `BossSetup.cs` (the tease is set from `AsterionSetup`), `Hypogeum.cs`,
`CrowdExcitement.cs`, `CodexSetup.cs`, `MenuSceneBuilder.cs`, `PortraitBaker.cs`, `LanistaTablet.cs`, `RosterGrid.cs`,
`IconSetup.cs`, the shared docs (the orchestrator's).

**Merge order:** chain → rage → setup (the art part any time before setup's PlayMode tests). After setup, the
orchestrator runs `Create Gladiator Data`, `Upgrade Icons`, `Bake Portraits`, `Bake Codex Portraits` and rebuilds the
menu scene.

**Open questions for the orchestrator** (Q1–Q11 in `docs/tracks/R3-design.md`, a recommendation each): Unchained
automatic or on a slide; Spent kept; the press radius; Gore while calm; shields; the tease in R3 and the full reveal in
the BACKLOG; the `Chain` part's parent; the card's tag; Underdog; the first-guess numbers; three parts or two.
