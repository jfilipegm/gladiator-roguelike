# W1 — worker notes (the Bookmaker's book)

Branch `claude/w1-wager-ef6283`, from `phase5-wager` (11ef0bf). Written without Unity. Not run: no Play mode, no tests run,
no screenshots. Compiled out of the Editor instead: all six assemblies (Core, Gameplay, UI, Editor, Tests.EditMode,
Tests.PlayMode) built with the Editor's own csc response files (`Library/Bee/artifacts/2400b0aE.dag/*.rsp`) on this
branch's sources, **0 errors**. The script is `w1_compile_check.py` in the session group's shared notes folder.

## What's built
- `Gameplay/Interactables/Bookmaker.cs`: the tier table (`WagerTier[] tiers`, code defaults = the spec's table),
  `maxKills` 600, `cooldownSeconds` 60, `maxWagers` 0 (no limit), `flagMinTier` 1 (tier II), `championGateDistance` 12.
  Phases Absent → Waiting → Book → Wager → Cooldown → Waiting (Closed after `maxWagers`); `Outcome` Won / Lost / Void.
  The UI reads `BookOpen`, `OfferCount`, `Offer(i)` (`WagerOffer`: tier, kind, stake, payout, N, seconds), `CanAfford(i)`,
  `BookVersion`, `LuckOpenedTier`; it calls `Pick(i)` and `CloseBook()`. Tests call `OpenBook()`. Pure maths for EditMode:
  `DefaultTiers()`, `OpenedByKills`, `OfferCountFor`, `TargetFor`, `LabelFor`, `Numerals`.
- `UI/WagerPanel.cs` (new): the menu. Built once, shown with a CanvasGroup. Texts are pre-sized and only written when the
  book opens, so nothing is written per frame (only colours).
- `UI/Hud.cs`: "[E]  THE BOOK" (coin gold) / "COME BACK LATER" (dust) over him; the bottom prompt "[E] Open the Bookmaker's
  book" (only while he's waiting, not during the cooldown). **Plus one line outside the two prompts** (see call 12).
- `Editor/Setup/ArenaBuilder.cs`: the `WagerPanel` is made beside the Bookmaker, under `[Run]`.
- Tests: `EditMode/WagerMathTests.cs` (new, 6 tests); `PlayMode/SecretPlayTests.cs` (the two old Bookmaker tests replaced
  by 5); `PlayMode/UiAuditTests.cs` (`TheBookmakersBook_OneAndFourCards`). `RunEndP1Tests` only names him in a comment.
- `docs/UNLOCKS.md`: the Bookmaker section, the Loaded Dice row, tests, tunables.

## My calls (for DECISIONS when you integrate)
1. **The panel lives in ArenaBuilder, beside the Bookmaker**, not in `ArenaSceneCommon.AddRunSystems`. Only arenas with a
   bookmaker get one, and only the Provincial Pit needs rebuilding. The tests make a panel when the scene has none.
2. **The level-up goes first.** E does nothing while level-up / chest / altar / boss cards are open or queued
   (`CardsBusy`), so the two screens never stack.
3. **The bot never opens the book.** Nothing would close it, and the bot ignores secrets anyway.
4. **The cooldown is game time.** Paused time doesn't count. During it E in his ring only gets "COME BACK LATER,
   CHAMPION." and there is no bottom prompt. Walking away from the book starts no cooldown.
5. **When the run ends,** an open book closes and a running wager is lost quietly (no line over the end screen).
6. **The champion:**
   - `Director.SpawnEliteAt` at a gate at least 12 m away (else any gate; with no gates, the arena centre), and the gate
     swings open for 2.5 s.
   - It wears a gold ring like the Arena Master's champion.
   - If it vanishes without a kill (a pit, a despawn), the wager is void and the stake comes back: "NO CONTEST: YOUR STAKE
     BACK."
   - A switched-off Director still sends it (`FindAnyObjectByType` fallback; the tests switch the Director off).
   - Like the Arena Master's champion, it uses up one of the Director's elite fork numbers. So after a tier IV wager, the
     Director's later elites differ from a run with the same seed and no wager (fine for R8: the wager is your choice).
7. **The Luck roll:** a fork of `RngStream.Loot` keyed "bookmaker_book" + the opening's number. The nth opening rolls the
   same in a seeded run. Luck and the rule come from the player's `PlayerBuild` (`config.fourthCardLuck`, default 0.5).
8. **N rounding:** pace × factor, less 0.001, then rounded up. In floats 50 × 1.2f is 60.0000024, which would ask for 61.
9. **Tier III** loses on any `OnDamaged` with damage > 0, including status ticks such as burning ground ("any damage taken
   loses").
10. **The card:**
    - The frame's material climbs with the stake: stone, bronze, silver, gold.
    - The band reads SMALL BET / FAIR BET / LONG SHOT / HIGH STAKES, on dust / bronze-light / orange-red / orange-red.
    - The numeral is gold for I–II and orange-red for III–IV.
    - Then: the challenge ("KILL 42", "KILL 40, UNHIT", "SLAY HIS CHAMPION"), "IN 30 SECONDS", a line of words, "STAKE 5"
      beside the aureus (the pair is centred when the book opens), "WINS 15", and a bronze **BET plate** ("Bet 5 gold";
      Disabled "Need 5 gold").
    - The key [1]–[4] is at the foot, as on the level-up's cards.
    - The level-up's fit (at least three cards), so a book lines up with a three-card level-up.
11. **The rest of the screen:**
    - WALK AWAY is a marble ansata with an ESC chip. Down / Up (d-pad, stick) move between it and the cards.
    - Card clicks and BET-plate clicks both pick.
    - A refused pick plays `Blocked` and his "NO GOLD, NO WAGER." line, and the book stays open.
    - The canvas sorts at `UiSortOrder.LevelUp + 1`.
12. **Hud.cs, outside the two prompts:** in `Tick`, the timer, edict icons and build strip now also hide while the book is
    open, as they do under the level-up and the pause. Otherwise "THE BOOKMAKER'S BOOK" sits over the clock, as LEVEL UP!
    would. It's one condition; revert it if you'd rather not.

## For you in Unity
1. Let it compile and read the console (it compiled out of the Editor, but the Editor is the judge).
2. **Rebuild the Provincial Pit** (ArenaBuilder). Without it the real game has no `WagerPanel`, and E would pause the run
   with nothing on screen. The scene's old Bookmaker fields (`stake`, `payout`, `seconds`, `paceFactor`, `minKills`) are
   dropped, and the new ones take the code defaults even before the rebuild.
3. Run EditMode (`WagerMathTests`), PlayMode `SecretPlayTests` (5 Bookmaker cases), `UiAuditTests.TheBookmakersBook_OneAndFourCards`,
   then the full suites.
4. Screenshot the book with 1, 2 and 4 cards at 1920×1080 and 4:3, a card you can't pay, and the HUD tag (THE BOOK /
   COME BACK LATER) and the countdown labels. Things I couldn't see:
   - the challenge line's autosize and wrap ("SLAY HIS CHAMPION" may take two lines);
   - the stake + coin centring (`PlaceStake` measures `textBounds` on the hidden canvas);
   - the numeral's size (Shout 92, autosize down to 60);
   - the band words' letter-spacing.
5. A GC check with the book open and idle (expected 0 B/frame), and a bot run (the bot never opens it).
6. DECISIONS (the calls above), PROGRESS, the release notes. The Loaded Dice hint ("Someone in the Provincial Pit takes
   bets. Find him, and beat the odds.") still reads right; I didn't touch `UnlockSetup` or the asset.

## Unsure
- The tier III test hits the player with `PlayerHealth.TakeDamage` (God mode on). It assumes no invulnerability window is
  running at that moment; it asserts the hit landed, so a failure there means a timing problem in the test, not in the
  wager.
- The tier IV test assumes the Provincial Pit's spawn table has an elite type from second 0 (`ST_ProvincialPit` entry 0
  does today).
