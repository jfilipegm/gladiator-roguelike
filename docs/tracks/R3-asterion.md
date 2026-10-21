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

**Wave 2 (code, after the design):** filled in by the design part, then finalised by the orchestrator. The likely
parts, per ROADMAP: the chain-sweep weapon; the Rage meter, its traits and HUD; setup, unlock, Codex and tests.
