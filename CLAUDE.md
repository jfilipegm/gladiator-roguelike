# MASTER BUILD PROMPT — Fantasy Gladiator Roguelike (Unity + Blender MCP both already opened)

**Engine/tools:** Unity (URP) via Unity MCP/CLI bridge · Blender via Blender MCP
**Reference game:** Megabonk (look and moment-to-moment gameplay)
**Identity:** an unknown pit fighter climbs a gladiatorial hierarchy through ever-grander, ever-stranger arenas, unlocking very different fantasy gladiators along the way.

> **How to use this file:** save it as `CLAUDE.md` in the root of the Unity project folder (so it survives context resets), then send the short kickoff message in Section 13.

---

## 1. Role and mission

You are the lead developer, technical artist and design partner on a solo-developer game. Starting from an empty folder, you will **build** (not just plan) a playable 3D horde-survival roguelike. You drive **Unity** through its MCP/CLI bridge and **Blender** through the Blender MCP. I am the producer and the only playtester.

You own: all C#, Unity project/scene/prefab/UI/VFX setup, all 3D modelling and export in Blender, performance, tests, and documentation.

Mission, in priority order:

1. A **vertical slice** that is genuinely fun for ~10 minutes: one gladiator, one arena (the Slave Pit), a handful of enemy types, XP → level-up → build choices, one real boss, a complete run loop (menu → run → death/victory → summary → menu), and one unlock.
2. An architecture where adding a gladiator, weapon, upgrade, enemy, boss or arena is mostly **adding data**, so the expansion in Section 9 is cheap.
3. Only after I confirm the slice is fun: expand toward the full vision.

**Be my design partner, not a yes-man.** If something I specified would make the game worse, harder to build, or unreadable, say so, propose the better option, and proceed with your recommendation (log it in `docs/DECISIONS.md`). Do not silently comply, and do not stall waiting for a debate.

The most important question at every step: **is it fun to play, and is it still readable with 300 enemies on screen?**

---

## 2. Operating rules

1. **Small vertical increments.** Every increment ends *compiling, runnable, verified, committed*. Never end a turn with compile errors.
2. **Verify before claiming.** Check the Unity console after every code change, enter Play mode, and confirm behaviour through logs, state dumps or screenshots. Say "verified" only for what you actually observed; otherwise say "unverified" and why.
3. **You can't feel the game.** Never claim something "feels great". Expose every feel parameter in a ScriptableObject (e.g. `MovementConfig`, `CameraConfig`, `HitFeedbackConfig`), start from sensible values, and list the tunables for my playtest.
4. **Data-driven content.** Gladiators, weapons, upgrades, enemies, bosses, arenas, waves and unlocks are ScriptableObjects. New *behaviour* needs code; new *content* should not.
5. **Reproducible scenes.** Build scenes, prefabs and materials with Editor scripts or MCP API calls (kept in `Assets/Editor/Setup/`), never by hand-editing `.unity`/`.prefab` YAML. Any arena must be rebuildable from its layout data with one menu item.
6. **Persistent memory.** Maintain these in `docs/` and read them at the start of every session:
   - `PROGRESS.md` — current phase/milestone, what's done, what's next, known issues.
   - `DECISIONS.md` — each non-obvious decision with a one-line reason.
   - `CONCERNS.md` — design/scope/perf/readability problems you've spotted and a proposed fix.
   - `BACKLOG.md` — good ideas that are *not* in the current phase.
   - `GDD.md` — a concise (≤ 3 pages) living design doc distilled from Section 6, with open questions resolved as you go.
7. **Git.** `git init` with a Unity `.gitignore` in Phase 0; commit at each green milestone with a clear message. Never force-push, never delete files I created.
8. **Scope discipline.** If it isn't in the current phase, it goes in `BACKLOG.md`, not in code.
9. **Time-box art.** Max 3 iterations per asset. If it still fails review, ship the simplest acceptable version and note it in `docs/ART_DEBT.md`. Don't spiral on one model.
10. **Autonomy.** Do not ask permission between milestones inside a phase. Stop only at (a) a **gate** (Section 9), (b) something only I can provide (MCP not connected, a licence or paid-asset decision), or (c) a destructive or irreversible action. Otherwise pick the sensible default, log it, and continue.
11. **No third-party art.** Generate every mesh procedurally in Blender. No Asset Store, Sketchfab, Hyper3D or Poly Haven models, no AI-generated models. This keeps the style consistent and the licensing clean. Unity registry packages are fine.
12. **Be honest about status.** If a test fails, a feature is half-working or a budget is missed, say so plainly in the report.

---

## 3. Tooling protocol

### 3.1 First thing you do
List the tools you actually have for Unity and Blender (names vary by MCP server) and tell me about gaps. Then run smoke tests:

- **Unity:** read the console, create a scene, create a cube, enter/exit Play mode, capture a Game-view screenshot. If there's no screenshot tool, write an Editor script that renders the camera to a PNG and read that file.
- **Blender:** get scene info, clear the scene, create a box with a direction marker, take a viewport screenshot, export FBX into `Assets/Art/Models/`, import in Unity, and **verify scale and facing** (a 1×2×1 m box with a marker on its front must arrive as 1×2×1 m facing +Z in Unity).

### 3.2 Unity rules
- After writing or editing C#, refresh the AssetDatabase and **wait for the compile/domain reload to finish** before the next MCP call. Always read the console afterwards.
- Check the Editor state (Play mode, compiling, reloading) before issuing commands.
- Use assembly definitions: `Gladiator.Core`, `Gladiator.Gameplay`, `Gladiator.UI`, `Gladiator.Editor`, `Gladiator.Tests`.
- Tests (Unity Test Framework): EditMode tests for the stat pipeline, upgrade rolling/rarity and unlock conditions; one PlayMode smoke test that boots Arena 1, runs the `PlaytestBot` for 60 s, and asserts no exceptions and a frame-time budget.
- Build `PlaytestBot` in Phase 0: a component that moves the player (collect gems, avoid enemies), picks random level-up cards, and has a `--stress` mode. Log CPU frame time, enemy count and GC allocations to a CSV with `ProfilerRecorder`. This is how you "see" performance without me.

### 3.3 Blender rules
- All modelling is **Python (`bpy`) scripts saved in `BlenderSrc/`** in the repo, so every asset is regenerable and diffable. Run them through the Blender MCP.
- Keep each execution short (a few seconds). Split big builds into several calls; long scripts can time out the MCP socket. Save the `.blend` after each asset.
- Shared helper library `BlenderSrc/gladiator_kit.py`: primitives with bevel/taper, mirror helpers, palette-UV assignment, naming, FBX export wrapper.
- **Review loop for every asset:** viewport screenshots from front, side and 3/4, plus a **silhouette test** (flat single-colour render at ~64 px tall). If you can't tell what it is at 64 px, redo the silhouette before adding detail.
- Export defaults to try first, then verify empirically with the Phase 0 round trip: FBX, selection only, apply unit scale, `-Z` forward / `Y` up, bake space transform, flat (face) normals. Naming: `CH_` characters, `EN_` enemies, `WP_` weapons, `SM_` environment, `PR_` props.
- Unity-side `AssetPostprocessor` enforces import settings: scale factor 1, no embedded materials (use the shared palette materials), no colliders, mesh compression on, read/write off unless needed.

---

## 4. Technical decisions (already made — don't relitigate without a concrete blocker)

- **Unity 6 LTS** (latest installed 6000.x), **URP**, linear colour space, **Input System** (keyboard+mouse and gamepad), **Cinemachine**, **UGUI + TextMeshPro**, Unity Test Framework. PC standalone first.
- **No ECS/DOTS in the slice.** Use pooled GameObjects with a **central tick** (no per-enemy `Update`). Keep hot data in plain arrays/structs so Burst/Jobs can be added later *only if profiling demands it*.
- **Enemy movement:** no NavMesh. Seek + separation using a **spatial hash grid**, push-out from baked static obstacles (circles/boxes), ground height from a **baked height grid** (one raycast pass in an Editor bake step; O(1) lookup at runtime). Ranged enemies use a simple keep-distance state machine. Add a flow field only if an arena layout truly needs it.
- **Player controller:** custom kinematic controller on `CharacterController`: acceleration/deceleration, gravity, coyote time, jump buffer, **slide** (momentum, speed gain on slopes), air control, optional double-jump as an upgrade. All numbers in `MovementConfig`. Camera: third-person orbit (mouse / right stick), wall-clip handling (deocclusion or fade).
- **Combat:** `DamageInfo` struct through a central damage pipeline. Targeting and hit detection use the same spatial hash (not `Physics.Overlap` per enemy). Projectiles are pooled and stepped centrally.
- **Stats:** `StatBlock` with layered modifiers (flat → additive % → multiplicative). Every upgrade = stat modifiers + optional hook ids. A typed **event bus**: `OnHit, OnKill, OnCrit, OnDamaged, OnLevelUp, OnJump, OnSlide, OnPickup`. Status effects (Burn, Shock, Bleed, Curse, Slow, Root) are data with stacking rules.
- **Pooling for everything spawned at runtime** (enemies, projectiles, VFX, damage numbers, gems, coins). **Zero GC allocation per frame** in steady-state gameplay: no LINQ, no per-frame `new`, no string building.
- **Rendering:** SRP-Batcher friendly, few shared **palette materials** (≈ 6–8 total, one per palette family — never per-object materials, never `MaterialPropertyBlock` on hot meshes), GPU instancing on. Blob-shadow decals for enemies; real shadows from one directional light at short range; no baked GI; light fog; mild bloom/vignette/colour grade; render-scale option.
- **Crowd:** `Graphics.RenderMeshInstanced` of a 40–80-tri spectator mesh with per-instance colour and animation phase. Excitement is a **global shader parameter**. Individual GameObject spectators only for hand-placed foreground NPCs in Arenas 1–2.
- **Audio:** placeholder SFX synthesised procedurally in an Editor script (sine/noise envelopes); `AudioMixer` with Master/SFX/Music/Crowd. Real audio is a later task — say so in reports, don't pretend.
- **Save:** versioned JSON in `persistentDataPath`. **Options:** volumes, mouse sensitivity, FOV, screen-shake slider, render scale.
- **Run structure is data:** a `RunDef` is an ordered list of `ArenaDef`s. The slice's run has exactly one arena (8-minute timer → boss; timer is configurable). Don't decide the final run structure yet (one arena per run vs. a chain of arenas); keep it data-driven and flag it as an open question.

---

## 5. Target look and feel ("Megabonk-like")

**Gameplay feel**
- Third-person camera behind the character, free orbit, **no lock-on**.
- Movement is a skill *and* a power source: sprint, jump, **slide**, air control, momentum. Some upgrades trigger on jump/slide.
- Weapons **fire automatically** (nearest-enemy auto-aim, or facing direction for some). The player's job is positioning, dodging, collecting and choosing.
- XP gems with a magnet radius → level-up pauses the game → pick 1 of 3 cards.
- Enemy pressure scales with the run timer; swarm spikes; elite enemies at intervals; a boss when the timer ends. Hundreds of enemies on screen.
- Map interactables: chests, shrines (risk/reward), gold.
- Juice: pooled floating damage numbers (crit styling), hit flash, small death pop, gem vacuum, level-up burst, kill-streak announcer text, restrained screen shake (slider), hit-stop **only** on elite/crit/boss hits and very short.

**Visual style**
- Chunky **low-poly**, flat-shaded, saturated palette, slightly retro-3D: tiny **point-filtered** palette textures, no PBR realism, clean readable shapes.
- Chibi proportions (head ≈ 35–40 % of height), oversized weapons, strong silhouettes, bright simple VFX.
- Colour language for readability: **anything that hurts the player is orange-red** (hazards, enemy projectiles, telegraphs); player VFX are cool white/cyan/gold; XP is green/cyan; gold is yellow; floor and background stay low-contrast and uncluttered.

**Adaptation (important):** Megabonk uses large open maps; ours are bounded arenas. To keep jump/slide meaningful, **every arena needs vertical variety** (mounds, ramps, stairs, raised platforms, pit edges) while the playfield stays readable.

---

## 6. Design bible (condensed)

### 6.1 Fantasy and tone
Start as nobody → fight to survive → become famous → become a legend. The arenas communicate the climb without exposition. This is **not** a historical simulator: Roman gladiatorial culture + dark fantasy + mythology + exaggerated, weird, sometimes ridiculous characters. The world starts grounded and gets steadily stranger. Tone ranges from terrifying cursed champion to tiny goblin throwing bombs, held together by the shared gladiator world.

### 6.2 Arena ladder
Each arena changes **gameplay**, not just textures. Reuse the modular kit; give each a distinct palette, silhouette and rule-set.

| # | Name | Player feels | Size / layout | Gameplay identity & hazards | Crowd | Boss seed |
|---|---|---|---|---|---|---|
| 1 | **Slave Pit** | "I need to survive." | ~35 m irregular dirt pit, rope/fence boundary, mounds, 1–2 entrances | Claustrophobic; mud patches (slow); basic enemies surround fast; small fires | 20–40 hand-placed locals on rough platforms; sparse cheers | **Gorm, the Pit Master** — slaver-champion, chain flail + whip, summons guards, enrages; burns the fence in phase 2 |
| 2 | **Provincial Pit** | "People notice me." | ~50 m, stone foundation, wooden stands, 3 entrances | Barricades, pillars, braziers, crude spike traps; shield-protected archers; flankers | A few hundred; chants, gambling energy | **The Provincial Champion** — fortress-shield murmillo; charges, wrecks barricades |
| 3 | **Stone Amphitheater** | "I'm becoming a real gladiator." | ~75 m oval, sand floor, pillars as LOS blockers, tiered seating, arches | Elites, mini-bosses, area denial, assassins, heavy shields | Thousands; reacts to streaks/elites/bosses | **The Beast Lord** — giant beast + handlers; cage gates open |
| 4 | **Grand Arena** | "I'm a star." | ~90 m, marble, patron boxes, monumental gates | Timed gate openings, fire jets, swinging blades, rising spikes, special events | Massive; nobles, merchants; excited on entrance | **The Gilded Patron** — wealth-themed, coin bombs, hired mercenaries |
| 5 | **Imperial Arena** | "One of the greatest alive." | ~110 m, gold/crimson, ceremonial entrances | Coordinated formations, multi-phase bosses, imperial events | Huge; senators, commanders | **The Emperor's Champion** — multi-phase, formation commander |
| 6 | **Colosseum** | "I have become a legend." | ~140 m, multi-tier, hypogeum trapdoors, huge arches | Huge waves, multiple factions, beasts, supernatural enemies, floor events | Entire arena packed; deafening; big entrance presentation | **The Eternal Victor** — the arena's first champion; the Colosseum itself transforms across phases |

Readability rule: telegraph every hazard in the orange-red language and never let set dressing break the silhouette of enemies against the floor.

### 6.3 Enemy ladder
- **Tier 1 (Arenas 1–2, grounded):** Starving Convict (melee swarmer), Pit Cur (fast dog), Shield Thug (frontal block), Slinger (ranged), Brute (slow, big, elite-class).
- **Tier 2 (Arenas 2–3):** Murmillo (shield+sword), Javelineer, Net-Thrower (slow/root), Assassin (dash), Beast Handler + beast, Fire-Pot Thrower (area denial).
- **Tier 3 (Arenas 4–5):** Named champions, Cursed Gladiators (curse zones), Praetorian shield-wall formations, Gilded Mercenaries, Sorcerers.
- **Tier 4 (Arena 6):** Minotaurs, harpy swarms, chimeras, skeletal legionnaires, giants, mythological beasts.
- **Elite affixes** (cheap variety): Armored, Fast, Explosive, Cursed, Gilded (drops gold), Berserk. Any enemy + one affix = elite.
- Slice: 6 enemy types + 3 affixes (see Phase 2).

### 6.4 Bosses
Never "big enemy with more HP". Each boss: distinct silhouette, unique attacks with telegraphs, arena interaction, ≥ 2 phases, a memorable entrance (camera beat + crowd reaction), personality. Before building any boss, write a 10-line spec in `docs/BOSSES.md`. The slice builds **Gorm** fully.

### 6.5 Roguelike systems (defaults — tune freely, log changes)
- **XP/levels:** gems from kills, magnet radius stat, level curve defined in data.
- **Level-up:** pause → 3 cards (4 with Luck) mixing new weapons, weapon upgrades and **Boons** (passives). Limited **rerolls** and **banishes** per run.
- **Slots:** 4 weapons (levels 1–8), 6 Boons (levels 1–5).
- **Rarity:** Common / Uncommon / Rare / Legendary; weighted odds shifted by Luck.
- **Synergy:** every upgrade has tags (FIRE, LIGHTNING, BLOOD, CURSE, SUMMON, HEAVY, CRIT, GOLD, MOVEMENT…). Synergy comes from *interactions through status effects and events*, e.g. "Burning enemies take +X% lightning damage", "Slide leaves a trail that applies Bleed". Aim for combos that surprise. Include a few high-risk/high-reward items.
- **Gold and chests:** gold drops; Armory Chests on the map cost gold (price rises per chest) and give a rarity-weighted upgrade. **Shrines** are risk/reward (e.g. lose 25 % max HP for a Rare; enemies +20 % HP for +30 % XP/gold).
- **Director:** a spawn-budget curve over time picks enemies from the arena's weighted table, with swarm events, an elite every ~90 s, and the boss at timer end.
- **Boss reward:** pick 1 of 3 Rare+ upgrades, plus unlock checks.
- **Evolutions / tag-threshold bonuses:** Phase 5, not the slice.

### 6.6 Meta progression and unlocks
- **Renown** is earned per run (more for deeper arenas and boss kills). It gates arena tiers and cosmetics.
- Meta unlocks **content, not flat stats** (no "+2 % damage" grinding): gladiators, weapons entering the pool, arenas, enemies, bosses, run modifiers, challenges, cosmetics.
- Unlocks are `UnlockDef` assets with pluggable conditions: defeat boss X · reach arena N · survive T minutes · kill N enemies with weapon W · win with gladiator G · lifetime gold · find a hidden object / trigger a secret encounter · purchase with Renown (only some).
- A **Codex** screen shows locked gladiators as silhouettes with a cryptic hint; some unlocks are discoveries ("What *is* that thing?").
- Slice: save file + Renown + exactly one unlock (e.g. beating Gorm adds a new weapon to the pool) to prove the pipeline.

### 6.7 Gladiator roster (architect for it now; build only the first in the slice)
Each is a `GladiatorDef` + optional `GladiatorBehaviour` for the unique mechanic. Each needs a unique silhouette, weapon, starting ability, passive, strength, weakness, personality and unlock.

| Gladiator | Concept / weapon | Signature mechanic | Unlock |
|---|---|---|---|
| **The Nobody** *(starter)* | Nameless pit fighter, rusty gladius + scrap shield | *Underdog:* bonus damage/XP when outnumbered | Default |
| **The Murder Machine** | Absurdly huge armoured executioner, giant cleaver; slow | Ground-slam AoE, flat damage reduction, can't be knocked back; slam charges by taking hits | Defeat Gorm |
| **The Pit Goblin** | Tiny, chaotic; bombs, traps, junk weapons | *Scavenger:* random improvised weapon rolls; traps interact with arena | Secret: a rattling crate in Arena 1 (discovery) |
| **The Blood Knight** | Dark vampiric knight, greatsword | Blood meter; spend HP for damage; heal on kill; stronger at low HP | Reach Arena 3 |
| **The Beastmaster** | Helmet of beast trophies; wolf/boar/hawk companions | Summon count/damage scaling; mark targets for companions | Kill a Beast Handler elite 25 times |
| **The Thunder Champion** | Bronze armour, storm helm; lightning | Chain lightning, lightning dash, attack-speed scaling | Complete Arena 3 |
| **The Ashen Oracle** | Masked, smoking, glowing cracks; curse staff | Cursed zones, smoke explosions, DoT, curses that spread on death | Secret encounter |
| **The Golden Idol** | Absurdly gilded champion | Convert gold into power; kill-streak gold multiplier | 10,000 lifetime gold |
| **The Net-Weaver** | Retiarius with trident + net | Crowd control (root/pull), multi-target spears | Win with the spear/trident |
| **Asterion, the Chained** | Minotaur gladiator dragging a chain | Rage meter; chain sweeps; tragic/mythological | Defeat the final boss |

Build order after the slice: Pit Goblin and Blood Knight first (most mechanically different from the starter — they prove the character system), then the rest by difficulty.

### 6.8 Weapons
Distinct attack **patterns**, not reskins: Gladius (fast arcs) · Spear (long thrust) · Trident (3-prong multi-hit) · Great axe/cleaver (slow huge AoE) · Net (root/slow) · Chakram (thrown, returns) · Javelins (volley) · Sling (ranged) · Whip (line hit) · Staff (magic projectile) · later cursed and mythological weapons. Implement a small set of **pattern behaviours** (`MeleeArc`, `Thrust`, `MultiHit`, `GroundSlam`, `Projectile`, `Returning`, `Orbit`, `Chain`, `Summon`) that weapon data composes.

### 6.9 Crowd and Favor (the identity hook)
The crowd is a **gameplay system**, not wallpaper. **Favor** rises with kill streaks, variety of kills, near-death survival, no-hit stretches and elite/boss kills. At thresholds the crowd throws **gifts** (healing amphorae, gold shower, a temporary weapon in the sand). When the player kites passively, the Arena Master adds **whims** (an extra gate opens). Crowd size, chants, reactions and the announcer escalate by arena tier.
- **Slice:** only a `CrowdExcitement` value (0–1) driven by kill rate, near-death and boss events; it drives the crowd shader, a crowd audio loop and the announcer text.
- **Phase 5:** full Favor / gifts / whims.

---

## 7. Art pipeline

### 7.1 Palette and materials
One master palette texture (64×64 or 128×128, **point filtered**) with named swatches; every face's UV is mapped to a swatch centre so each asset uses one material and batches. Palette families: A1 mud/rust/browns · A2 ochre/dry grass · A3 sandstone · A4 marble white + red · A5 gold + crimson · A6 travertine + imperial purple, plus faction and VFX palettes. Define the palette in a single file so it can be retuned globally.

### 7.2 Characters and enemies
- Budgets: player ≤ 1,200 tris; regular enemy ≤ 300; elite/boss ≤ 800 (boss up to 2,500).
- **Rigid-part procedural animation, not skeletal rigs.** Build each character as separate meshes (Torso, Head, ArmL/R, LegL/R, Weapon, accessories) with pivots at the joints, exported as one FBX with the hierarchy preserved. A Unity `PartAnimator`, driven by data, handles idle bob, run cycle, attack swing, hit recoil, death and squash/stretch using sine waves and `AnimationCurve`s. Reason: it's robust through Blender-MCP/FBX, trivially cheap for hundreds of enemies, and fits the retro low-poly look. Move to skinned animation only for a specific proven need (e.g. a boss) and log it.
- Identify every gladiator by, in order: **silhouette → weapon → head/helmet → motion → VFX**. Run the 64 px silhouette test against the other gladiators and enemies.
- Enemies get a bright accent patch for readability against the floor; elites are larger with an affix-coloured glow.

### 7.3 Environment kit
Modular pieces as separate meshes: stone/wood walls, fences and rope, gates, pillars, arches, stands (wood/stone), stairs, ramps, mounds, braziers, weapon racks, banners, statues, barrels/crates, rubble, debris. An `ArenaBuilder` Editor tool reads an `ArenaLayoutDef` (piece placements, spawn gates, obstacle list, hazard volumes, crowd zones) and builds the arena, bakes the height grid and obstacle list, and sets static batching. Reusing the kit must cut cost without making arenas look the same — vary palette, silhouette and rules.

### 7.4 Scale
1 Blender unit = 1 Unity unit = 1 m. Player ≈ 1.6 m tall. Apply all transforms before export; origin at the base (characters) or the joint (rigid parts).

---

## 8. Performance budget (PC, 1080p, mid-range GPU)

- **60 FPS** with **300 enemies active by minute 5**, ~500 at peak, and no crash at a 1,000-enemy stress test.
- Gameplay logic ≤ 4 ms/frame (enemy tick, damage, spawner, weapons combined).
- **0 B/frame GC allocation** in steady state (verify with the profiler CSV).
- Arena 1 static geometry ≤ ~100k tris; ≤ ~8 shared materials in the scene; no per-enemy dynamic lights; particles capped and pooled.
- Report real measurements from the `PlaytestBot` stress mode, not estimates.

---

## 9. Roadmap, phases and gates

Each milestone has a **testable acceptance**. Proceed through milestones inside a phase without asking me. **Stop at gates.**

### Phase 0 — Foundations
- **M0.1** Unity project created, packages installed, folder structure, asmdefs, `.gitignore`, `git init`, docs scaffolding (Section 2.6), this file as `CLAUDE.md`.
- **M0.2** MCP smoke tests pass (Section 3.1), including the **Blender → Unity round trip** with verified scale/facing.
- **M0.3** `PlaytestBot` + profiling CSV + test scaffolding working.
- **M0.4** Look-dev scene: palette material, flat-shaded test models, light, fog, post-processing, point-filtered texture — it should already *look* like the target style.

### Phase 1 — Feel
- **M1.1** Player controller + third-person camera on a test arena with a slope, stairs and a raised platform. Acceptance: jump/slide/air control behave per `MovementConfig` (logged numbers: top speed, jump height, slide boost), no camera clipping through walls.
- **M1.2** Placeholder enemies + one weapon (gladius arc) with hit feedback (flash, damage numbers, death pop, short hit-stop on crits).
- **M1.3** HUD: HP, XP bar, timer, gold, level. XP gems with magnet and a stub level-up.
- **GATE 1 — "Feel check."** Report and stop. I playtest movement, camera and combat feel and give feedback.

### Phase 2 — Horde and roguelike core
- **M2.1** `EnemyManager`: pooling, central tick, spatial hash, separation, obstacle push-out, height-grid snapping. Acceptance: 300 enemies at 60 FPS in the test arena (measured).
- **M2.2** Six enemy types (Convict, Cur, Shield Thug, Slinger, Brute, plus one assassin/area-denial type) and 3 elite affixes.
- **M2.3** Director: spawn budget curve, swarm events, elite cadence, run timer.
- **M2.4** Stat pipeline + event bus + upgrade data + **level-up UI** (3 cards, reroll, banish, rarity).
- **M2.5** Four weapons with clearly different patterns + weapon levelling.
- **M2.6** ~14 Boons, including at least 3 status-effect interactions that create real synergies. EditMode tests for stacking/rarity math.
- **M2.7** Chests, gold, one shrine type.

### Phase 3 — Slave Pit, boss, run loop
- **M3.1** Blender kit, palette and modular pieces for Arena 1.
- **M3.2** Arena 1 built with `ArenaBuilder`: irregular pit, vertical variety, mud patches, fences, fires, ~30 spectators, props, lighting. Height grid + obstacles baked.
- **M3.3** Final player and enemy models with `PartAnimator`; silhouette tests passed.
- **M3.4** **Gorm** boss: entrance beat, ≥ 2 phases, telegraphed unique attacks, arena interaction, guard summons, crowd reaction, boss reward.
- **M3.5** Full run flow: main menu → gladiator select (one) → run → death/victory → summary → menu.
- **M3.6** Save file, Renown, Codex stub, one unlock working end-to-end.

### Phase 4 — Slice polish and performance
- Juice pass, placeholder SFX, crowd-excitement reactions, announcer text, options menu.
- Performance pass to the Section 8 budget; 10-minute `PlaytestBot` run is stable; tests green; a **standalone build** is produced and smoke-run.
- **GATE 2 — "Is it fun?"** Report (Section 11) and stop. I playtest. **Do not start Phase 5 until I say so.**

### Phase 5+ — Expansion (do not begin before Gate 2 approval)

| Priority | Item | Why this order |
|---|---|---|
| Must | Pit Goblin + Blood Knight | Prove the character system with mechanically different gladiators |
| Must | Arena 2 (Provincial Pit) with hazards and multiple gates | Proves kit reuse and arena-specific rules |
| Must | Favor system (gifts/whims) | The identity hook |
| Should | Weapon evolutions + tag-threshold synergies | Build depth |
| Should | Unlock variety (secret encounters, challenges) + Codex | Discovery-driven progression |
| Should | Arenas 3–6, one at a time, each with its boss | Spectacle ladder |
| Should | Remaining roster | Replayability |
| Nice | Music, real audio, cosmetics, run modifiers, daily seeds | Polish |

---

## 10. Verification and "done" for every milestone

1. Console clean (no errors; warnings triaged).
2. Behaviour confirmed in Play mode via logs/state dumps/screenshots — and `PlaytestBot` for anything involving runtime scale.
3. Relevant EditMode/PlayMode tests pass.
4. Perf numbers recorded for anything that spawns many things.
5. For every new Blender asset: 3-angle review and the 64 px silhouette test.
6. `PROGRESS.md` updated; git commit made.

---

## 11. Gate report format (keep it short and honest)

1. **What now works** (verified vs. unverified).
2. **What to test** — exact steps and what to look for.
3. **Tunables** — the ScriptableObject fields most likely to need adjusting.
4. **Measurements** — FPS, frame time, GC, enemy counts.
5. **Top concerns** from `CONCERNS.md` with your recommendation.
6. **Proposed next phase** and any scope changes.

---

## 12. Out of scope until I say otherwise

DOTS/ECS · multiplayer · procedural map generation · localization · cinematic cutscenes · environment destruction · per-spectator crowd simulation · Asset Store or third-party models · skeletal animation or Mixamo retargeting (unless a proven need) · more than one arena or gladiator before Gate 2 · meta-progression that only adds flat stat bonuses.

---

## 13. Kickoff message (send this after saving the file as `CLAUDE.md`)

> Read `CLAUDE.md` fully. First, list the Unity and Blender MCP tools you have and tell me about any gaps. Then run Phase 0 end-to-end, continue through Phase 1, and stop at Gate 1 with the report format from Section 11. Follow the autonomy rules in Section 2 — only stop for gates, things only I can provide, or irreversible actions. Keep `docs/PROGRESS.md` current so you can resume after a context reset.

---

## 14. Environment notes (appendix added by Claude — how to reach the tools on this machine)

The Unity and Blender MCP *tools* are not exposed to Claude Code sessions on this machine, so both apps are driven through equivalent local bridges. Full details: `docs/TOOLING.md`.

- **Unity** (6000.6.6f1, GUI Editor open on this project): Unity CLI + `com.unity.pipeline` package. `Tools/u <command> [--param v]` wraps `unity command`; `Tools/ueval '<C#>'` runs C# in the Editor; `Tools/uwait` blocks until not compiling/reloading; `Tools/ucompile` refreshes, recompiles and prints console errors. Editor auto-tick is enabled so it works unfocused.
- **Blender** (5.2 LTS, Blender Lab MCP add-on on localhost:9876): `python3 Tools/blender_bridge.py BlenderSrc/<script>.py [--arg k=v]` or `-c '<code>'`. Same socket protocol the Blender MCP server uses; assign a dict to `result` to return data.
- Read `docs/PROGRESS.md` first on every session start.
