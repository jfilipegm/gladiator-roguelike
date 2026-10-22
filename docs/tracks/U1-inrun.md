# U1-B: in-run screens (notes)

Worker U1-B, branch `claude/gifted-goldstine-4b6728` (from `origin/phase5-ui` at 0a61775). Contract:
`docs/tracks/U1-contract.md` ("U1-B in-run screens"); spec: `docs/tracks/U1-design.md` §5–§8. No Unity here: compiled
with `Tools/ccheck` against the contract's stand-in `Assets/Scripts/UI/Theme/U1Stub.cs` (errors=0). **Nothing below
was seen running**: every look and every test is unverified until the orchestrator merges A and runs them.

## Files

| File | What changed |
|---|---|
| `Assets/Scripts/UI/Theme/U1Stub.cs` | The contract's stand-in, byte for byte (first commit, `U1-STUB(a)`). **Delete at merge** (A's files win). |
| `Assets/Scripts/Core/Layout/LevelUpLayout.cs` | New. Layout A's fit maths (spec §8.3), pure, in `Gladiator.Core` beside `MenuLayout`. |
| `Assets/Scripts/UI/LevelUpPanel.cs` | Layout A: 300×520 rarity-framed cards, the halo / focus ring / 1.04 scale, the Vow's split band, ansata buttons with key chips, titles, `SPOILS OF VICTORY`. |
| `Assets/Scripts/UI/StatsPanel.cs` | Pick and Prompt faces on Bronze, Pause on Marble; `PickWidth` 410; `SetPickScale`; the marble colour remap. |
| `Assets/Scripts/UI/Hud.cs` | The HUD in UiKit (plates, troughs, chip tiles, roles), the vexillum banner and its routing, the callout, PAUSED. |
| `Assets/Scripts/UI/DamageNumbers.cs` | Luckiest Guy + its Outline material from the theme; `PoolLabel(i)`. |
| `Assets/Scripts/UI/RunEndPanel.cs` | YOU DIED on a held crimson banner, laurels for SURVIVED! / VICTORY!, wax stats, ansata buttons. |
| `Assets/Tests/EditMode/LevelUpLayoutTests.cs` | New: the spec's five cases, 21:9, a 1600–2600 × 900–1300 × 1–4 cards sweep. |
| `Assets/Tests/PlayMode/UiInRunLookTests.cs` | New: roles, banner routing, card frames, centring, titles, truncation, end screens, the heap log. |
| `Assets/Tests/PlayMode/StatsPanelTests.cs` | The one layout assertion: "the cards moved left" → "the content sits where `LevelUpLayout.For` puts it (x ± 1)". |

## How it works

**LevelUpLayout** (`For(canvas, cards)`): content scale `s = min(1, (H − 40) / 900)`; right margin
`M = (W − R·s)/2 − 48`; column scale `k = min(1, M / 410)`; `k ≥ 0.7` → centred (x 0). Otherwise `k = 0.7`, the content
slides left until the row's right edge is 24 short of the column, and if its left edge would pass 24 it shrinks to the
room (floor 0.6). Extras beyond the contract: `CardX(i, n)`, `HeightScale(H)`, `ColumnTop(H)` (the column hangs from
`354 · HeightScale` above the centre: y 186 at 1080, and it never runs off a 900-tall canvas), `RowLeft / RowRight /
ColumnLeft` for tests, and a cap on `k` for canvases shorter than any target (no effect at 900+). A Python port of the
maths gives the spec's numbers: 16:9 k 1; 16:10 (1822×1138) k 0.938; 4:3 (1663×1247) k 0.745; 16:9 four cards k 0.7,
x −17; 4:3 four cards k 0.7, x −145.6; 21:9 (2217×935) s 0.9948; and the sweep has no failing canvas.

**LevelUpPanel.** `Redraw` → `LayoutContent()`: `LevelUpLayout.For(canvas, ChoiceCount)` sets the content's scale and x,
the title and subtitle boxes (as wide as the canvas allows on both sides of the content's centre, ≤ 1400) and
`StatsPanel.SetPickScale(k)`. Cards (y from the card's centre): band 231 (40 tall, Title 24 `#17120F`, spacing +12),
icon 100 at 151 on a `Halo` in the rarity colour (α .55), Legendary `LaurelWreath` (native size) at 143 behind it, tag
Title 21 at 82, name Title 32 two lines (autosize 26–32) at 35, pips 14 at pitch 20 at −14, description Body 24
(autosize 18–24, 270×174) at −117, key Number 26 `ink` α .5 at −221; the frame's 24-unit foot below. Back to front:
selected `Halo` (rarity colour α .45), `cardInside` fill (the click target), the rarity frame (`CardStone / Bronze /
Silver / Gold`, changed with `UiFrames.Apply` only on a redraw), the `Focus` ring 6 outside (white; crimson in banish).
Banish mode tints every frame 60 %. Buttons: three `UiKit.Plate` Primary 280×66 at x −306 / 0 / 306, keys R / B / X,
words "Reroll ({0})", "Banish ({0})", "Skip" (`SetText`), each with a `UiHit` on its frame (no `Button`), `SetState`
only when the state changes: Disabled at 0 left, Focused on hover, Banish Pressed while banish mode is on. Inputs, grace,
CanvasGroup show / hide and `Presize` are today's (Presize now also blanks the parked cards' labels).

**StatsPanel.** One `Image` frame under the page: `Bronze` for Pick, `Marble` for Pause (`UiFrames.Apply` on a face
change), the body inset by the frame's padding (at least 20 / 16 / 18 / 16). Pick: 410×730, pivot top-right, hanging
from `LevelUpLayout.ColumnTop`, scaled by `SetPickScale` (`PickReserve = 410·k + 24`). Pause: today's 560×920, Engraved
look, `marbleInk` base colour, and after `WritePage` the colour tags are swapped in place for darker inks of the same
hues (`Remap`, same 7 characters: the text and its building are unchanged, no allocation). The prompt: Bronze, 680×60,
Body 28 autosize 20–28.

**HUD.** Top-left Bronze plate (12, −22, 456×120): heart + HP trough 388×32 (Number 26), the special meter trough
252×12 under it with its label (Number 20) at its right, coin + gold (Number 32 `coin`), skull + kills (Number 32).
Top-right Bronze plate (300×112): LV (Number 50), laurel + Favor trough. XP: a full-width trough 18 tall. Timer Number
64 Outline at (0, −22); "BOSS" in `crimsonLight`. Build strip: `Chip` tiles 52 at y −152 (weapons) / −210 (Boons), a
2-unit rarity line (44 → 40 inside), icon 40, level Number 20 (autosize 18–20); an evolution-ready tile's line and chip
pulse gold. Synergy chips: `Chip` frames, Title 18, at y −270 (the P1 test's "≤ −236" holds). Edicts on chips; the
daily chip a `Chip` with "DAILY" in Title 20 (gold scored, dust on a dimmed chip in practice), 108 right of centre (the
Number font is wider). Boss: name Title 30 `ink`, trough 760×26 `crimson` (guarded: `gold`), gold notches placed on the
fill's span, the guard line Title 20 at −178. Prompts Body 30, price tags Number 30, the Bookmaker's tag Body 28
(`coin`, `loss` when short). Toast Shout 84 `goldBright` at −236. Intro: a `Mosaic` plaque (≤ 1560 × 220, as wide as
the canvas allows) behind the name (Title 96 gold, autosize 56–96) and tagline (Body 34, no italic); FIGHT! Shout 160
`goldBright` Outline. PAUSED: Shout 110 + a Body 32 hint label under it (two fonts, two labels). Debug line and dev tags
stay Plain (`UiFactory`).

**The announcer.** `_banner = new VexillumBanner(canvas, top, (0, −204))`, ticked with scaled dt (frozen under the
level-up and the pause). `OnDirector` Swarm / Elite and `AnnounceKind.Whim` → Crimson; BossEnters / BossPhase /
BossGuards / BossFalls → Purple; the event's `Seconds` goes to `Show`. Rank: purple 2, crimson 1. While the banner is
visible, the same rank or higher → `Show` (A's banner rolls up quickly and drops the new one); lower → the one pending
slot (newest wins), shown when the banner is Hidden. Every other kind → the callout (today's label: Shout 48, autosize
30–48, the sender's colour, Outline, 1100×64 at −380; today's pop-and-fade and chatter rule). Run ended → `Clear()`,
the slot emptied, no callout.

**Damage numbers.** `UiTheme.Current.FontFor(Number)` + `MaterialFor(Number, Outline)`, the role's spacing,
`FontStyles.Normal`; the serialized font / material are used only if the theme has no font. Pool, pre-size, sizes,
colours and motion unchanged.

**End screen.** Death: `VexillumBanner(canvas, centre, (0, 372), 900).Hold("YOU DIED", Crimson)` (the cloth centres on
the title's old place, y 300), ticked with unscaled dt; then the banner's words label is restyled to `ink`, up to 96
(see Needs). Survived / victory: Shout 132 `goldBright` (autosize ≥ 72) and `UiKit.Laurels`. Subtitle Body 36. Stats on a
`Wax` frame 640×420 (labels Title 22 `waxInk`, values Number 30, the Renown row gold, autosize 18–30). Unlock line
Title 30 gold between laurels. Note Body 22 `dust`. Buttons `UiKit.Button` 340×66 at ±360 (daily ±180): RETRY /
PRACTICE Primary, SAME SEED and MAIN MENU Secondary, labels autosize 20–28.

## Decisions (one-line reasons)
1. **The end screen's buttons keep today's words ("Retry  [R]", "Same Seed  [S]", "Main Menu  [M]"), no key chips.**
   DailyMenuTests asserts `RetryText == "Retry  [R]"` (not B's file), and a chip would repeat the key.
2. **Card descriptions autosize down to 18, not 20.** A 150-character weapon text + its evolution and two synergy hints
   doesn't fit 270×174 at 20 in Marcellus; 18 is the global floor, and the PlayMode test checks the longest card.
3. **The marble pause page swaps StatText's colour tags for darker inks of the same hues** (in place, same length).
   StatText is Gameplay's (not B's) and its colours are for a dark page; `#FFFFFF` values on marble can't be read.
   Edicts stay in the orange-red family (`#C23A1E`): they hurt you.
4. **The stats page keeps its 13-unit autosize floor.** A late-run Pause page has every source of every stat; the
   18 floor would cut it.
5. **The Pick column stays one Body label written by StatText.** Layout A's Title header, Title group labels and
   right-aligned values need tags in StatText (Gameplay); colours and fonts of the column are the theme's otherwise.
6. **Build-strip tiles use the `Chip` frame, not `Bronze`.** Bronze's 6-art-px rivet border (18 units a side) leaves 16
   units in a 52-unit tile.
7. **FIGHT! uses the Outline material (ink drop), not a crimson drop.** A crimson underlay is a 13th TMP material;
   the contract says Outline.
8. **The toast moved to y −236** (the banner's line) from −170 (over the boss bar). It shows ~0.35 s before the cards
   cover it.
9. **PAUSED and its hint are two labels** (Shout and Body). `PauseTextWidth` = the wider of the two, `PauseLineCount` =
   their lines summed (3), `PauseRect` = PAUSED's box (the hint stretches inside it): RunEndP1Tests unchanged.
10. **A Vow's band words are `ink` with the Outline look** (`#17120F` on the crimson half is unreadable).
11. **The HUD's strip rows moved down** (−152 / −210, chips −270) to sit under the taller bronze plate.
12. **The daily chip is a dark-bronze `Chip` with a gold word (scored) / a dimmed chip with a dust word (practice)**,
    not a solid gold plate (a tinted dark sprite turns muddy).
13. **Troughs re-stretch their fill inside the trough** (insets = the trough's padding, capped at 20 % of the height)
    so a stretched trough (the XP bar) carries its fill and a 12-unit meter keeps a visible fill.
14. **`ForceRedraw()`, `RerollId / BanishId / SkipId` and the label getters are test hooks** (the PlayMode test forces
    rarities on live offers and clicks Banish the way the mouse does).

## Tests (written, not run)
- **EditMode `LevelUpLayoutTests`:** `RowWidth` / `CardX`; the spec's five cases (k ± 0.01, x ± 1, Centred; content
  unscaled for the two four-card cases); 21:9 content scale 0.994 ± 0.002; the sweep (1600–2600 step 50 × 900–1300 step
  25 × 1–4 cards): the row's right edge + 24 ≤ the column's left edge, the row and the column on the canvas, k in
  [0.7, 1], s in [0.6, 1], 900·s ≤ H − 40, the column's top and bottom on the canvas, Centred ⇔ x 0, else x < 0.
- **PlayMode `UiInRunLookTests`** (TestArena, booted as `LevelUpTests` does, plus the spawner off and the intro waited
  out): roles (timer, level, gold, kills, a damage number = Number; callout, LEVEL UP! = Shout; boss name, card name =
  Title; card text = Body; card key = Number) and `TruncatedLabels` = 0 on the HUD after three picks (the Plain debug
  line excluded); Swarm → crimson "SWARM INCOMING!" → BossEnters → Rolling, then purple with its line → a Streak is a
  callout and the banner keeps its line → an Elite while purple holds waits in `PendingBanner`, then drops crimson →
  the run ends: no banner (the ArenaMaster is switched off so no whim takes the slot); offers forced Common / Rare /
  Legendary: `CardRect` 300×520, `CardFrame` CardStone / CardSilver / CardGold, `CardHasWreath` only on the Legendary,
  the row and the buttons share the screen's centre when `Fit.Centred`; Banish (clicked by id after the grace) →
  "BANISH", off → "LEVEL UP!"; a boss reward → "SPOILS OF VICTORY" with today's subtitle; the catalogue's longest
  card (Describe + Hints at Legendary) on card 0 → `TruncatedLabels` = 0 on the panel; death → `TitleBanner` Held,
  Crimson, "YOU DIED", no laurels, `StatsFrame` Wax, still held after a whole banner's time; survived → laurels, no
  banner, Shout title; 5 s of fighting with a banner every 2 s → `GameLoop.HeapReport()` logged, not asserted.
- **`StatsPanelTests`:** the content's x = `LevelUpLayout.For(canvas, n).ContentX` ± 1; "the last card ends before the
  stats column starts" kept.
- Existing tests that touch B's files and should still pass unchanged: `LevelUpTests`, `RunEndAndAdminTests`,
  `RunEndP1Tests` (pause box, admin tag, synergy chips), `FeedbackTests`, `BossTests`, `DailyMenuTests` (RETRY words,
  end screen not truncated, the daily chip), `EdictMenuTests` (edict row: 2 children, on screen), `SecretPlayTests`,
  `RageTests` (`BloodLabel`), `FavorPlayTests`.

## Screenshots for the orchestrator
At 1920×1080 and 1280×720; ★ also 1920×1200 and 1600×1200:
- A Slave Pit fight (~150 enemies) with damage numbers and a crit, a crimson banner mid-sway, a callout under it ★.
- Gorm's entrance: the purple banner under the boss bar, the notches ★.
- Level-up with Common / Rare / Legendary ★ (put it beside `ui-look/levelup_a.png`); four cards (Luck) ★; banish mode;
  a Vow card; ARMORY CHEST; BLOOD ALTAR; SPOILS OF VICTORY.
- The pause page (marble) and options over it ★.
- YOU DIED ★ (check the words' size on the cloth); VICTORY! with an unlock ★.
- The arena intro plaque and FIGHT!; the HUD with a full strip, synergy chips, a special meter (Blood Knight or
  Asterion), the daily chip and two edicts.

## Tunables (most likely to need a look)
- `LevelUpPanel`: the card anatomy constants (`BandY`, `IconY`, `WreathY`, `NameY`, `DescY`, `DescH`, `KeyY`), `IconGlow`
  .55, `SelectedGlow` .45, `BanishTint` .6, the title's autosize floor 72, the description's 18.
- `LevelUpLayout`: `StatsTop` 354 / `StatsH` 730 (the column's top and height), `VerticalMargin` 40.
- `Hud`: `PlatePos/PlateSize`, `LevelPlatePos/LevelPlateSize`, the strip rows, `DailyChipLeft` 108, the toast's y −236,
  the callout's y −380, the trough fill cap (20 %).
- `StatsPanel`: `MarbleInks` (the marble page's colours), `MinInset`.
- `RunEndPanel`: `BannerDrop` 72 (where the cloth sits), `YouDiedSize` 96, the button size 340×66.

## Needs from others
- **U1-A (optional):** a way to style `VexillumBanner`'s words (e.g. `public TMP_Text Words { get; }`). Today
  `RunEndPanel.StyleBannerWords` takes the first TMP label under `Banner.Root` after `Hold` and sets it to `ink`, max 96
  (autosize up to what the cloth's box allows). If A's banner has another TMP label first, YOU DIED keeps A's style.
- **U1-A, assumptions B relies on (all in the contract):** `UiKit.Plate`'s `Frame` takes raycasts when B turns
  `raycastTarget` on (B adds `UiHit` to `Frame`); `UiPlate.State` reflects the last `SetState`; `UiFactory.Label(plate.Button)`
  finds the label; `UiKit.Laurels` returns one container B can `Destroy`; `UiKit.Bar`'s fill is a direct child of the
  trough (B re-stretches it); `UiFrames.KindOf` reports the kind an `Image` shows after `Apply`.
- **Orchestrator:** delete `U1Stub.cs` after merging A; run the tests above; the screenshots.
