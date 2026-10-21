# Worker tracks

Each roadmap item (`docs/ROADMAP.md`) is built by several **worker sessions** at once. The **orchestrator** (the main
session, the only one with a Unity Editor) integrates, tests and approves their work. This folder holds each item's
**contract** (`<item>.md`, written by the orchestrator) and each worker's **notes** (`<item>-<part>.md`, written by
that worker).

## Rules for every worker
1. **Read first:** `CLAUDE.md` (the project's rules, especially §2, §3.3 and §7), this file, your item's contract, and
   the existing code or scripts named in your brief.
2. **Work only in your own worktree, on your own branch.** Never open, edit or run anything in the main checkout
   (`/home/jfilipegm/Gladiator Roguelike`): its Unity Editor would recompile on every file you save there.
3. **Edit only the files your part owns** (listed in the contract). If you need a change in someone else's file, put it
   under "Needs from others" in your notes and in your summary. Don't make it yourself.
4. **No Unity.** No Unity Editor or Unity CLI commands: no `Tools/u`, `ueval`, `ucompile`, `utest`, `perfrun`,
   `unity ...`.
   - **C#:** run `Tools/ccheck` until it prints `errors=0`. It compiles your worktree's code with Unity's compiler; it
     doesn't test anything.
   - **Tests:** write the tests your part needs (EditMode / PlayMode, `Assets/Tests/`). The orchestrator runs them.
   - **Assets:** never hand-write Unity YAML (`.asset`, `.prefab`, `.unity`, `.meta`). Data and scenes come from Editor
     setup scripts in `Assets/Editor/Setup/`, which the orchestrator runs.
5. **Blender only through `Tools/bheadless BlenderSrc/<script>.py [k=v ...]`.** The GUI instance is shared, so no
   `blender_bridge.py` and no Blender MCP.
   - Follow CLAUDE.md §3.3 and §7: kit helpers in `BlenderSrc/gladiator_kit.py`, palette swatches from
     `Assets/Art/Textures/Palette/palette_index.md`, triangle budgets, rigid parts with pivots at the joints, FBX export
     through the kit.
   - **Review every model:** front, side and 3/4 renders, plus the 64 px silhouette test, saved to
     `Screenshots/blender/<asset>_review.png`. At most 3 iterations per asset; past that, ship it and note why.
6. **Shared docs belong to the orchestrator:** PROGRESS, DECISIONS, CONCERNS, GDD, BACKLOG, ART_DEBT, UNLOCKS, ROADMAP,
   `GameInfo.Version`, `docs/releases/`. Write in BOSSES / ARENAS / GLADIATORS only inside the section your contract
   gives you. Everything else goes in your notes file, `docs/tracks/<item>-<part>.md`: what you built, decisions with a
   one-line reason, numbers, verified vs unverified, needs from others.
7. **Git:** commit often with clear messages ending in
   `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, and push your branch. Never commit to `main`, never merge,
   never force-push, never delete files you didn't create. Never commit `ProjectSettings/ProjectSettings.asset`,
   `ProjectSettings/UnityConnectSettings.asset`, `_Recovery_*` or `mono_crash.*`.
8. **Stay inside your part.** If you're blocked, or the contract is wrong or missing something, message the
   orchestrator (SendMessage) instead of guessing broadly. Small sensible defaults are fine: log them in your notes.

## Your final message (what the orchestrator receives)
Under 400 characters, in this shape:
`<branch> @ <short sha>. Done: <what>. Verified: <ccheck errors=0 / renders / tris>. Unverified: <what>. Needs: <none
or what>. Notes: docs/tracks/<file>.md`
