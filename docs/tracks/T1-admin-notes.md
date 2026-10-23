# T1 — W-admin notes (the admin panel's new tools)

Branch `t1-admin` (from `phase5-playground`). Not compiled or run here: it codes against W-menu's `AdminMode.On` and
`GameSession.KnownRuns`, so it compiles only once those are merged.

## What I built
- **F1 in admin mode.** `AdminController.Available` → `AdminMode.On`. `allowInReleaseBuilds` is removed (no code read
  it). The arena scenes still have a stale `allowInReleaseBuilds: 0` line, which Unity ignores and drops on the next
  rebuild or save.
- **FREEZE** (`DevSettings.FreezeEnemies`, reset in `ResetAll`). The panel's ENEMIES row is now
  `Freeze: off/ON | Kill all | Clear pickups`. "Kill all enemies" became "Kill all" so the row fits three buttons and
  the first column stays the same height (it already runs ~50 px past a 1080 screen).
  - `EnemyManager.Tick`: a frozen enemy goes to the new `TickFrozen` (right after the Scripted branch) instead of
    Think/steer/contact. That clears its velocity and knockback, resets its AI state to Move, keeps the elite ring,
    flash restore and recoil, places the view, and animates (standing, so the recoil shows). Nothing allocates.
    `ApplyDamage`, `Kill`, the status DoT ticks, death pops and bone piles are untouched.
  - **My call: knockback is dropped.** A frozen enemy stays exactly where it stands, a statue to look at and a dummy to
    hit. If it kept the knockback it would drift away under fire and never walk back.
  - **Bosses:** `BossController.Tick` has its own brain, so I added one line to it (`if (DevSettings.FreezeEnemies)
    return;` after the `Alive`/`dt` checks), which the brief allowed for a tiny guard. A frozen boss's death still goes
    through `Die()`. A frozen boss doesn't roar into his next phase until the freeze ends. The Eternal Victor's HP
    floor holds him at 1 HP until then.
  - Turning FREEZE on also calls `EnemyProjectiles.Clear()`, so no shot fired earlier is left in the air.
  - **Not frozen:**
    - an Explosive elite's death blast, which comes from the player's kill;
    - arena hazards (chariots, jets, blades, spikes, fire);
    - the Director, which keeps spawning; its toggle sits next to FREEZE.
- **FIGHT A BOSS** (second column, under UPGRADES). There is one full-width button per `GameSession.KnownRuns` entry
  (8 slots at most), labelled `THE SLAVE PIT: Gorm` (the arena name, then `BossDef.shortName`). The labels and the
  column height are rebuilt only when the list changes. With no list (null or empty), one greyed-out row reads "No
  arena list: start the run from the menu". A run with no arena or no boss shows its button greyed out.
  - `FightBoss(k)` sets the session's gladiator and skin to what the player wears now (`GladiatorLoadout.Current` /
    `CurrentSkin`, so a test override or a skin also carries over). It then closes the panel, sets the static
    `AdminController.PendingBossFight` and calls `GameSession.StartRun(gladiator, run)`, which gives a fresh seed and
    clears `Playground` per W-menu's contract.
  - In the new arena, `AdminController.Start` takes the flag (`TakeBossFight`). It switches `DevSettings.Timeless`
    off, because the clock must run out for the boss to come. It then sets the timer to `BossJumpSeconds` = 3.
    - The boss trigger is `RunState.TimerDone`: Elapsed reaches runDuration with `holdForBoss` set by `BossFight`.
    - The clock waits behind `RunIntro` (paused until FIGHT!), so the entrance starts ~3 s after FIGHT!.
    - In an arena without a `BossFight`, the flag is still spent and the clock is left alone (a warning is logged).
  - God mode and the buffs stay as the player set them, because the `DevSettings` statics survive the load. For the
    3 s before the boss, the Director spawns at its late-run rate; `BossFight.Begin` drags that horde off as usual.
  - `PendingBossFight` is also reset on `SubsystemRegistration`, which covers Enter Play Mode without a domain reload.

## For the orchestrator to check
1. **It compiles after the W-menu merge**: `AdminMode.On` and `GameSession.KnownRuns` (`RunDef[]`).
   `AdminToolsTests.TearDown` sets `KnownRuns = null` after `GameSession.Clear()`.
2. **Run `AdminToolsTests` (3 tests):**
   - `Freeze_…` uses the TestArena with EN_Convict, EN_Slinger and EN_Brute.
   - `FightABoss_StartsTheMenuRun_…` uses SuppressLoad, the Blood Knight in the Night Knight skin, and then a reload
     of the TestArena.
   - `FightABoss_TheArenaBringsItsBoss_…` does a real load of the Slave Pit through `FightBoss`. Gorm must come 2–5 s
     after FIGHT!; it then freezes him.
3. Also run `RunEndAndAdminTests` and `BossTests`, which cover the touched files.
4. **Look at the panel** at 1080p:
   - the three-button ENEMIES row with "Freeze: ON" / "Kill all" / "Clear pickups" (does the font fit 181 px?);
   - the second column's FIGHT A BOSS rows: are the longest labels, e.g. "THE STONE AMPHITHEATER: The Beast Lord",
     inside 560 px?
5. **Save effect (not changed):** a boss jump is a normal run, so a win records in the save (Renown, the boss's
   unlocks) like any run the admin tools make easier. If admin runs shouldn't count, that's a separate guard
   (RunEndController / MetaProgress).
