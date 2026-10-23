# T1 W-menu: admin mode and the menu side of the Playground (worker notes)

Spec: `docs/tracks/T1-playground.md` (row **W-menu**). Branch `t1-menu`, cut from `origin/phase5-playground` @ fbc7d59.
Nothing was compiled or run in Unity (worker rule): the code and tests are written, not run.

## Files
| File | What |
|---|---|
| `Assets/Scripts/Gameplay/Dev/AdminMode.cs` (+ .meta) | `AdminMode`: `On`, `Toggle()`, `event Changed` (the contract), plus `Forced`, `BuildAlwaysOn`, `FromCommandLine`, `Switched`, `StatusText`, `Reload()`; test hooks `SimulateRelease`, `CommandLineOverride`, `PrefKeyOverride`, `ResetTestHooks()`. `AdminCode`: the typed code's state (a two-field struct, `Feed(char, time)`). |
| `Assets/Scripts/Gameplay/Run/RunDef.cs` | `GameSession.KnownRuns` (never null), `Playground`, `StartPlayground(gladiator, run)`; StartRun / StartDaily / Clear reset Playground, Retry keeps it, Clear empties KnownRuns. |
| `Assets/Scripts/UI/MainMenu.cs` | `playgroundRun`, the typed code, the ADMIN tag, the toast, PLAYGROUND, the admin view of the pickers, `KnownRuns` on Start. |
| `Assets/Tests/EditMode/AdminModeTests.cs` (+ .meta) | 8 EditMode tests (below). |

The two .meta files follow this repo's own format (two lines, no MonoImporter block: that's what every .cs.meta here has).

## How it works
- **On** = the Editor or `Debug.isDebugBuild` (unless `SimulateRelease`), or `-admin`, or the PlayerPrefs switch
  `gladiator.admin` (cached after the first read: no allocation, no PlayerPrefs call per frame). `Toggle` flips the
  switch, `PlayerPrefs.Save()`s it, logs and raises `Changed`. In a forced build the switch still flips but On stays true;
  the toast says why ("ADMIN MODE ON (DEV BUILD)" / "(-admin)").
- **The code:** on the main menu screen only, `kb.aKey/dKey/mKey/iKey/nKey.wasPressedThisFrame`, each checked on its own
  (not gated on `anyKey`: rolled keys or Shift would be missed), fed in code order to `AdminCode` (≤ 3 s between keys,
  a wrong letter starts over, an A restarts at once). US key positions, like the menu's other keys (an AZERTY player
  types it at the US places). Complete: `Toggle`, a 1.6 s gold toast (top centre), the Pick sound.
- **The ADMIN tag:** gold outlined Title text, top-left corner, active while `On`.
- **PLAYGROUND:** a sixth plate built last (index 5, so `MenuPlate(0..4)` keep their meaning). Shown when `On` and
  `playgroundRun != null`; it then stands under PLAY and the column re-lays out at step 78 from y 60 (six plates clear
  the subtitle and the key line on 21:9); off, the column is exactly as before (step 92 from y 10) and
  `MenuPlateCount` is 5. Arrow focus walks the screen order. PLAYGROUND → the gladiator screen titled "THE PLAYGROUND"
  → FIGHT! (or Enter, or a second click) → `LaunchPlayground()`: no edicts, the skin on the strip,
  `GameSession.StartPlayground(g, playgroundRun)`. Test hooks: `PlaygroundButtonShown`, `PlaygroundPicking`,
  `OpenPlayground()`, `LaunchPlayground()`, `AdminTagShown`, `AdminToastText`, `SelectHeaderText`, `FeedAdminKey(c, t)`.
- **The admin view:** `MainMenu.IsPickable(g / r)` = `AdminMode.On || IsAvailable`. `PickSlots` gives the gladiator
  screen every gladiator open in roster order (the `locked` placeholders stay locked); the arena screen opens every
  arena; FIGHT! takes the skin on the strip, owned or not (`LaunchSkin`; the strip itself is unchanged). Switching while
  the menu is up rebuilds the gladiator and arena screens (detach + Destroy, then Build again; the pick is kept when
  still pickable).
- **KnownRuns:** the ladder (`runs`, else `run`) without nulls, set at the top of Start.

## Decisions (please log in DECISIONS)
1. **`IsAvailable` stays the save's rule; the pickers use the new `IsPickable`.** If IsAvailable itself honoured admin
   mode, the Codex (its GLADIATORS tab reads `RosterGrid.Slots` → IsAvailable) would show every gladiator as open and
   **mark them seen in the save** (CodexPanel's MarkSeen/SaveSeen), and the Daily Games' plan would draw from unearned
   arenas and gladiators. So the Codex, the Daily Games and the ~30 PlayMode `IsAvailable(...)` asserts are untouched.
2. The Daily's plan now draws from separate earned lists (`_earned`, `_earnedRuns`) instead of the pickers' lists.
3. The Playground skips the arena screen and runs with no edicts.

## The orchestrator must wire or check
1. **PlayMode tests run in the Editor, where admin mode is always on.** Without a hook, menu tests that expect locks
   will fail (locked tiles/arenas are open, `menu.PickArena(locked)` returns true, `MenuPlateCount` is 6 once
   `playgroundRun` is assigned, FIGHT! takes the strip's skin, the ADMIN tag shows). Suggested one-liner set in
   `Assets/Tests/PlayMode/SaveSandbox.cs` (global SetUpFixture):
   `Redirect()`: `AdminMode.SimulateRelease = true; AdminMode.PrefKeyOverride = "gladiator.admin.playmode-test"; PlayerPrefs.DeleteKey("gladiator.admin.playmode-test"); AdminMode.Reload();`
   `Restore()`: `PlayerPrefs.DeleteKey("gladiator.admin.playmode-test"); AdminMode.ResetTestHooks();`
   Tests that need admin on (W-admin's F1 and boss jump, W-play's Playground boot) then set
   `AdminMode.CommandLineOverride = true` (or `SimulateRelease = false`) and put it back.
   Note: `AdminMode`'s SubsystemRegistration reset clears the hooks on entering Play mode (before OneTimeSetUp).
2. **A Playground run must not commit to the save:** `RunEndController` (line ~76, `MetaProgress.CommitRun`) should skip
   when `GameSession.Playground` (records, Renown and unlock checks from a god-mode showroom run). Not my file.
3. **Open question:** a normal run started in admin mode with an unearned gladiator or arena commits its records as
   usual (e.g. beating the Beast Lord as an unearned gladiator unlocks the Grand Arena for real). Fine if admin runs
   count; otherwise also skip CommitRun when the run's gladiator or arena isn't `IsAvailable`.
4. In admin mode the skin strip still shows BUY / the goal on unowned skins (it doesn't know about admin mode); BUY is
   a real purchase. FIGHT! fights in the skin on show either way.
5. Check the column at 21:9 and 4:3 with PLAYGROUND shown (screenshot), and that `Empty` + rebuild leaves no stray
   objects (switch on and off on the menu, then open the gladiator and arena screens).
6. `MainMenu.playgroundRun` is assigned by W-play's setup; until then PLAYGROUND stays hidden.
