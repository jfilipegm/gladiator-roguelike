# W1 — the Bookmaker's book (the producer's note 23, after P3)

Branch `phase5-wager` (cut from `phase5-playtest3` at 9ef38ab; P3 merges into main first). One worker writes the code
without Unity; the orchestrator integrates, rebuilds the Provincial Pit, tests and screenshots. Ask the producer before
merging.

## The note (the producer's words, condensed)
The Bookmaker in the Provincial Pit isn't intuitive. Make the entry fee lower, like 5 gold, and double it for every next
wager. Pressing E on him should open a **Wager menu** where you pick from 1 or more wagers (based on Luck / how many
kills you have so far), at most 4, and the challenges get harder the bigger the wager.

## Today (Phase 5.6 + P1, `Bookmaker.cs`)
E in his gold chalk ring takes a fixed 30-gold stake and posts one bet, "kill 1.3 × your last 30 s of kills (at least
60) in 30 s"; win pays 90 and (the first time) sets `bookmaker_paid` (Loaded Dice's unlock); one wager per run. The HUD
shows "[E]  WAGER 30 GOLD" over him, a bottom prompt in his ring, and the countdown under the timer (`RunObjective`).

## The design (the orchestrator's calls where the note leaves a choice; log them in DECISIONS when integrating)

### Opening the book
- E (gamepad North) in his ring, as today, **opens the book** instead of taking a bet. It costs nothing. The run pauses
  while it's open, the same way a chest's pick does (find how the chest/altar cards pause and block player input).
- Esc / gamepad East / a **WALK AWAY** plate closes it, costing nothing.
- If a chest is in reach at the same time, the chest still takes the press (keep today's rule).
- With no gold at all he still opens the book (the cards show what they'd cost); the broke line stays for the E press
  only if you want, but the menu itself teaches the prices.

### The wagers: four tiers, the stake doubling (5, 10, 20, 40)
| Tier | Stake | Challenge (shown on the card) | Time | Pays |
|---|---|---|---|---|
| I | 5 | KILL N IN 30 S — N = 0.8 × your last 30 s of kills, at least 25 | 30 s | 15 |
| II | 10 | KILL N IN 30 S — N = 1.2 × your pace, at least 50 | 30 s | 35 |
| III | 20 | KILL N IN 30 S **WITHOUT BEING HIT** — N = 1.0 × your pace, at least 40; any damage taken loses | 30 s | 80 |
| IV | 40 | **THE CHAMPION** — he sends an elite from the arena's own elite pick (the director's elite path, with an affix) in at a gate; kill it within 45 s | 45 s | 200 |

- "Double it for every next wager" = the tiers: each card's stake is twice the one before. All the numbers are data
  (a serializable tier table on the Bookmaker, or a small ScriptableObject if that's cleaner; code defaults must equal
  this table because the orchestrator rebuilds the arena with them).
- N caps at `maxKills` (600) like today. The card shows the actual N for this opening (computed when the book opens).
- A pace of 0 early in the run still gives the minimums.

### How many cards (1 to 4)
- Tier I is always offered. Tiers II, III, IV open at **100, 300, 600 kills this run** (`RunState.Kills`).
- **Luck** can open one more tier beyond what your kills open: chance = Luck / 0.5 (the same rule as the level-up's 4th
  card, `UpgradeConfig.fourthCardLuck`), rolled once each time the book opens, on a seeded RNG stream (no
  `UnityEngine.Random`).
- Only offered tiers are shown as cards; when fewer than 4 are offered, a line under the row says
  "MORE KILLS, BIGGER WAGERS".
- A card you can't afford is shown disabled with "NEED n GOLD" (the plate's Disabled state; contrast still ≥ 3:1).

### After you pick
- He takes the stake and the wager runs exactly like today's bet: his line, the coin and quill sounds, the countdown
  under the timer via `RunObjective`. Labels must be **constant strings** (the HUD formats numbers without allocating):
  e.g. "WAGER I", "WAGER II", "WAGER III · NO HITS", "THE CHAMPION" (target 1).
- Win: he pays the tier's payout, the burst, cheer, Favor and crowd spike as today. **The `bookmaker_paid` flag (Loaded
  Dice) needs a win at tier II or higher** (tier I is too easy to be the secret); first win line as today.
- Lose: the house keeps the stake, "THE HOUSE WINS." (tier III: also lose the moment you take damage; tier IV: lose if
  the timer runs out; if the run ends, lose).
- **He takes more than one wager per run:** one at a time; after a wager ends he waits **60 s** ("COME BACK LATER,
  CHAMPION" if you press E sooner), then calls again. The N values are recomputed from your pace at each opening.
  `cooldownSeconds` and a `maxWagers` (0 = no limit) are data.

### The menu's look (match the level-up's cards; read `LevelUpPanel.cs`, `Core/Layout/LevelUpLayout.cs`,
`docs/tracks/P3-contract.md`, `docs/tracks/U1-contract.md`)
- Title "THE BOOKMAKER'S BOOK" in the level-up title's role and place; a sub-line "place your wager" like "choose one".
- 1–4 cards in the level-up's card row (`LevelUpLayout.For(canvas, n)`, centred), each: the tier numeral (I–IV), the
  challenge in words, the time, the stake with the `IconAureus` icon, and "WINS n". The bigger tiers can read more
  dangerous (an orange-red accent on III and IV, the colour language for "this hurts").
- No inventory / stats columns.
- Keys: mouse, 1–4, arrows/gamepad like the level-up cards; Enter/A picks the focused card; the focus glow as on every
  plate (`AnsataGlow`).
- The padding rule and the contrast audit: every label inside its frame's `UiKit.ContentRect`, contrast ≥ 3:1; add
  the screen to `UiAuditTests` (with 1 and 4 cards).
- Zero GC per frame while it's open and idle (pre-size texts; no string building per frame).

### HUD (`Hud.cs`: only the Bookmaker's prompts, around `_bookTag` / `_prompt`; don't touch the plates at the top)
- Over his head while he calls: "[E]  THE BOOK" (gold), or "COME BACK LATER" during the cooldown.
- The bottom prompt in his ring: "[E] Open the Bookmaker's book".

## Files (the worker's)
- `Assets/Scripts/Gameplay/Interactables/Bookmaker.cs` (the tiers, the book state the UI reads, the challenge kinds,
  the cooldown; Gameplay must not reference UI).
- New `Assets/Scripts/UI/WagerPanel.cs` (the menu; it reads `Bookmaker.Instance`).
- `Assets/Scripts/UI/Hud.cs` (the two Bookmaker prompts only).
- Wherever the panel gets created with the other run panels (find where `LevelUpPanel` is added).
- `Assets/Editor/Setup/ArenaBuilder.cs` only if the Bookmaker's setup needs new references.
- Tests: update the Bookmaker tests in `Assets/Tests/PlayMode/SecretPlayTests.cs` (and `RunEndP1Tests.cs` if they touch
  him); new EditMode tests for the pure maths (stakes, payouts, the offer count from kills + a luck roll, N from the
  pace); PlayMode: E opens the book and pauses, walk away costs nothing, a pick takes the stake and starts the objective,
  can't afford is refused, tier III loses on a hit, tier IV spawns one elite and wins on its kill, the flag only at
  tier ≥ II, the cooldown then a second wager; `UiAuditTests` over the panel.
- `docs/UNLOCKS.md` (the Bookmaker section) and a short `docs/tracks/W1-notes.md` (what you built, the calls you made,
  anything left for the orchestrator).
