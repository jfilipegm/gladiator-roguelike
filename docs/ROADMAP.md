# Roadmap — one lane, parallel workers inside each item (approved 2026-10-21)

**The model (the producer's):** this session is the only lane and handles **one roadmap item at a time**. For each
item it starts several worker sessions (Opus 5.5, extra effort) that build different parts of that item at once.
The workers never use Unity. **Everything that needs the Unity Editor (compiling in Unity, building scenes, tests,
screenshots, bot runs) happens here.** This session tests and approves every part before the item moves on.
Workers send back short summaries only, to keep this session's context small.

This file reorganises the existing roadmap (CLAUDE.md §9 Phase 5 and `docs/PROGRESS.md` "Next"). **No item is added
or removed.**

## 1. The items, in order
| # | Id | Item | Why here |
|---|---|---|---|
| 1 | R0 | Finish the Beastmaster: PlayMode suite, PR, merge, v0.16.0, docs PR | in progress; this session alone, no workers |
| 2 | R1 | Arena 5, the Imperial Arena + the Emperor's Champion | Should; design notes already in PROGRESS |
| 3 | R2 | Arena 6, the Colosseum + the Eternal Victor | Should; must be bigger than R1 (GDD ≥ 17,500 m²) |
| 4 | R3 | Asterion, the Chained | Should; his unlock is "defeat the final boss", R2's boss, so R2 comes first |
| 5 | R7 | Run modifiers | Nice; R6–R8 share the save, the menu and the Renown market, so they run one after another |
| 6 | R8 | Daily seeds | Nice |
| 7 | R6 | Cosmetics | Nice; last of the three because it needs the most art |
| 8 | R4 + R5 | Music, then real audio | Nice; generated in code (decided below) |

Unchanged and not in this plan: the playtest concerns (C13–C26) wait on your playtest; `docs/BACKLOG.md` stays
unscheduled.

**Audio source (decided 2026-10-21):** the producer chose to **generate music and sounds in code**, like today's
placeholders (Editor synthesis scripts). No bought or recorded audio.

## 2. How one item runs
1. **Contract (this session, short).** Write the item's spec where CLAUDE.md wants it (BOSSES.md for a boss, ARENAS.md
   for an arena, GLADIATORS.md for a gladiator), plus a **contract**: the class, field and asset names, model names and
   triangle budgets that the parts share. Each part gets a list of files it owns.
2. **Parts in parallel (workers).** One worker per part, each in its own git worktree, on its own branch cut from the
   item branch.
3. **Integration (this session).**
   - Merge each finished part into the item branch.
   - Compile, run the setup menu items, then run the part's tests and the screenshots.
   - If something is wrong, send it back to the same worker, which still has its context.
4. **Close (this session).** Full EditMode and PlayMode suites, bot runs, PR, merge (ask outside a merge window),
   release, docs PR, then the next item.

**Overlap (approved 2026-10-21):** while this session tests item N, the workers for item N+1's spec and art may
already start. They use no Unity and touch other files. Item N+1's code parts still wait until item N is merged.

## 3. Rules for workers
- **Never touch the main checkout.** The Unity Editor is open there and would recompile on every file a worker saves.
  Each worker uses its own worktree, which has no Editor and no `Library/`.
- **Only edit its own files** (listed in the brief). If it needs a change elsewhere, it says so in its report and this
  session decides.
- **No Unity.**
  - Workers write C# and tests; this session runs them.
  - For a compile check, `Tools/ccheck` will compile the worktree's C# with the .NET 8 compiler that ships inside the
    Unity install. That compiler exists, but the script isn't written yet; it's the first setup task. Until then, code
    is first compiled when this session integrates it.
- **Blender only through `Tools/bheadless`.** The GUI instance is shared. Each model still gets the 3-angle review
  and the 64 px silhouette test, rendered to `Screenshots/blender/`.
- **Shared docs belong to this session:** PROGRESS, DECISIONS, CONCERNS, GDD, BACKLOG, ART_DEBT and UNLOCKS, as well as
  `GameInfo.Version` and the release notes. A worker writes its notes in `docs/tracks/<item>-<part>.md`.
- **Report:** the last message is a summary under 400 characters (branch, done, verified vs not, open questions). That
  is all this session receives automatically; the full report goes in the track file.
- **Commits:** commit and push its branch. Never `main`, never merge, never force-push.

**Starting workers (learned 2026-10-21):** the app starts a session directly only on a turn that one of the producer's
messages started. On a turn started by a worker's report or another automated event, it can only **propose** one, as a
card the producer starts with a click. Cards can't choose the branch to start from: they start from `main`. So every
brief begins with `git fetch origin && git merge --ff-only origin/<item branch>`.
**Standing permission (producer, 2026-10-21):** start workers at the orchestrator's discretion, never ask. To avoid stalls on cards, start
every part of a wave in the same producer-started turn; dependent parts code against the contract's names (with marked
stubs if needed, as the boss part did). If only cards are possible, ask the producer to click them: at 17:30 on 2026-10-21 the app refused even an explicit
"start the R2 art workers" (probably because a background bot run was still going), so a click on the card is the
reliable path. At 19:15 a plain "please continue" from the producer (no background task running) started both waiting cards (`start_session` with their `proposal_id`) and four new `user_asked` workers at once.

**Pausing and resuming workers (2026-10-21):** `stop_session` pauses a worker mid-turn (its context and worktree stay);
`SendMessage` resumes it, and that works on any turn, not only the producer's. Use it to stay under the 5-hour limit:
six workers at once burned ~80 %/h for their first 15 minutes (each reads the contract and design notes), then ~40 %/h.
A worker's message to a paused peer also resumes it. Since 2026-10-22, even turns the producer started were sometimes
treated as automated: cards are the dependable path, and the producer clicks them.

## 4. The parts of each item
The exact split is fixed in each item's contract. This is the starting proposal, with file ownership.

**R1 — Arena 5, the Imperial Arena** (design notes in PROGRESS "Arena 5 design notes")
| Part | Owns |
|---|---|
| Environment art: floor with the track lane, stands, carceres, gates, pulvinar, spina, metae, obelisk | `BlenderSrc/environment/imperial_arena.py`, `Assets/Art/Models/Environment/SM_Imp*`, `SM_Spina*`, `SM_Obelisk`, `SM_Carceres`, `SM_Pulvinar` |
| Character art: chariot (~700 tris), Praetorian, Centurion (≤ 300 each), the Emperor's Champion (≤ 2,500) | their `BlenderSrc` scripts and FBX |
| Chariot races (the `ChariotRace` hazard) + tests | `ChariotRace.cs` (new), the chariot fields in `ArenaLayoutDef.cs`, the hook in `ArenaBuilder.cs` |
| Formations: escort slots in the leader's frame, `freeEscortsOnDeath` + tests | `EnemyDef.cs`, `EnemyManager.Leash.cs` |
| The boss: 3 phases (`phase3At`), his attacks, his setup + tests | `BossDef.cs`, `BossController.cs`, `BossSetup.cs` |
| Layout and setup: hippodrome layout, run, unlock, spawn table, Praetorian and Centurion data | `ImperialArenaLayout.cs`, `ImperialArenaSetup.cs` (new) |

**R2 — Arena 6, the Colosseum.** Same shape as R1: environment art (hypogeum trapdoors), character art (the Eternal
Victor and the supernatural enemies the spec picks), the floor-event hazard, new enemy behaviours, the boss (the
Colosseum changes with his phases), layout and setup. The spec comes first: ≥ 17,500 m² and bigger than Arena 5, not
necessarily round or oval.

**R3 — Asterion.** Model and chain (art); the chain-sweep weapon; the Rage meter, its traits and HUD; setup, unlock
(`DefeatBoss` = the Eternal Victor's id) and tests.

**R7, R8, R6 — run modifiers, daily seeds, cosmetics.** One spec for all three first. Then for each item: its data
and rules, its menu or Codex screens, and its save and unlock changes, each part owning its own files.

**R4 + R5 — audio, generated in code.** A music generator and player; sound effects redone by category
(combat, crowd, UI, bosses); mixer and bank.

## 5. What stays in this session
- **The only Unity Editor:** compiles, setup menu items, scene builds, tests, screenshots.
- **The measurement slot:** bot runs and the full PlayMode suite. With only one GPU, fps numbers are only valid one
  run at a time.
- **Merges, versions, releases, smoke runs** and the shared docs.
- **Merge window:** until 19:00 WEST on 2026-10-21. After that, ask the producer before every merge.

## 5b. Usage pacing (approved by the producer, 2026-10-21)
Every session (the orchestrator and every worker) draws on the same plan limits: a 5-hour window and a weekly limit.
Running parts in parallel doesn't make an item cheaper; it only spends the budget faster.
- **When to check:** `get_usage` each time a worker reports, and before starting any worker.
- **The 5-hour window:** burn rate = change in percent ÷ time between readings. Projected = now + rate × time to reset.
  - Projected under ~85 %: more workers may start, if there's parallel work and memory for them.
  - Projected over ~90 %: no new workers; the running ones finish.
  - Always keep ~10 % in reserve for the orchestrator to finish testing, committing and pushing.
- **Weekly:** spread what's left evenly over the days to the weekly reset (the daily share). Don't go over it unless the
  producer says so.
- **Measure** how much of the weekly limit one full 5-hour window costs, and refine the numbers above.

## 6. Setup before the first item
1. ~~Write `Tools/ccheck`~~ (done 2026-10-21: all six assemblies in ~6 s; tried in a worktree with an injected error).
2. ~~Create `docs/tracks/` and the worker rules~~ (done: `docs/tracks/README.md`; R1's contract `docs/tracks/R1-imperial-arena.md`).
3. ~~Agree on the overlap and the audio source~~ (both decided 2026-10-21).
