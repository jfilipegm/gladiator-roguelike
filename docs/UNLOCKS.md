# Unlock variety + Codex (Phase 5.6)

The next "Should" in CLAUDE.md §9 is discovery-driven progression. It addresses three problems today:
1. **Renown has no use.** Every run earns it, and nothing spends it.
2. **There are only 4 unlocks, all from one boss or one crate.** Beating the Provincial Champion unlocks nothing.
3. **The Codex is a static three-column stub.**

After this phase, every run should move something forward in the Codex, through three kinds of goal:
- **Challenges:** visible goals with a progress bar. They test skill and builds.
- **Secrets:** discoveries in the arenas. "What *is* that thing?"
- **The Market:** a few unlocks bought with Renown. Only some unlocks can be bought, per CLAUDE.md §6.6.

Every reward is **content** (a weapon or Boon entering the pool) or **Renown** (a challenge's bounty, spent in the Market). Never flat stats.

## Unlock data
- **`UnlockDef.category`:** Challenge / Secret / Market. The Codex tab it lives in, and how it's shown:
  - **Challenge:** full text and a progress bar;
  - **Secret:** a cryptic hint until found;
  - **Market:** price and BUY.
- **`UnlockDef.renownCost`** (Market only): it unlocks only when bought in the Codex (`MetaProgress.Buy`), which spends that much Renown.
- **`UnlockDef.renownBounty`:** Renown paid once, when it unlocks.
- **New condition kinds**, appended to `UnlockConditionKind`:
  - `RunKills` (best kills in one run);
  - `Evolve` (id = evolution card id, empty = any; amount = times);
  - `SynergiesFound` (amount = distinct synergies ever switched on);
  - Phase 5.15: `KillEnemies` (id = an EnemyDef id, `"a|b"` = any; amount = lifetime kills of that type, from the Bestiary's `enemyKills`).
  - Phase 5.14: `WinWithWeapon` (id = a weapon held at the end of a won run, `"a|b"` = any of them, e.g. a weapon and its evolution; amount = wins). The save's `weaponWins` counts them (filled by Migrate in older files).
- **`MetaProgress.Progress(condition, save)`** returns current / target for the bar. For several conditions the bar shows how many are met (e.g. "2 / 3").
- **Save (added fields, no version bump; Migrate fills them):**
  - `bestRunKills`;
  - `evolutions` and `synergies` (IdCount);
  - `enemyKills` (IdCount by EnemyDef id);
  - `upgradesTaken` (IdCount);
  - `codexSeen` (ids already shown in the Codex, for the NEW markers).
- **`RunSummary`** also carries the run's kills per enemy type, the evolution cards taken, the synergies switched on and the upgrades taken.

## New content (the rewards)

| Reward | Kind | Unlock | How |
|---|---|---|---|
| **Sling** (CRIT) | weapon: one fast stone at the farthest target in range, long reach, big crits | Secret: **the Golden Rat** | catch it in the Slave Pit |
| **Loaded Dice** (GOLD) | Boon: +Luck, +gold | Secret: **the Bookmaker** | win one of his wagers at tier II or higher in the Provincial Pit |
| **Gorm's Whip** (BLOOD) | weapon: a 6 m thin crack through everything in a line, at the two nearest enemies in turn; Bleeds; **the tip** (last 1.5 m) always crits, so you fight at whip's length | Challenge **Pit Master's Bane** | defeat Gorm 3 times |
| **Net** | weapon: a slow thrown net, wide, pierces, Roots for 1.5 s | Challenge **Champion of the Provinces** | defeat the Provincial Champion |
| **Winged Sandals** (MOVEMENT) | Boon: +1 jump in mid-air per level (max 2) | Market | 300 Renown |
| **Wax Tablet** (XP) | Boon: +XP, +1 reroll per level | Market | 300 Renown |
| **Gilded Palm** (GOLD) | Boon: +gold, +pickup range | Market | 300 Renown |

**Renown challenges** (bounty only): First Evolution (evolve any weapon, +100); Synergist (3 different synergies, +150); Butcher (5,000 kills in one run, +100); Every Gladiator Wins (win with the Nobody, the Goblin and the Knight, +200).

**The existing unlocks are recategorised:**
- Challenges: Gorm's Flail (beat Gorm), the Provincial Pit (beat Gorm), the Blood Knight (beat Gorm below 30 % HP; **Phase 5.9: reach the Stone Amphitheater**, §6.7's "Reach Arena 3"), the Murder Machine (Phase 5.9: beat Gorm; also adds the Executioner's Cleaver to the pool), the Thunder Champion (Phase 5.10: beat the Beast Lord, §6.7's "Complete Arena 3"; also adds the Storm Spear), the Golden Idol (Phase 5.11: 10,000 lifetime gold; also adds the Coin Toss), the Net-Weaver (Phase 5.14: win a run holding the Spear or the Impaler, `WinWithWeapon spear|impaler`; also adds the Trident), the Beastmaster (Phase 5.15: kill 25 Beast Handlers, `KillEnemies beasthandler 25`; also adds the Wolf Pack), Asterion (R3: defeat the Eternal Victor, `DefeatBoss eternal_victor 1`; also adds the Labyrinth Chain; while he is locked the Victor's fall shows "BELOW THE SAND, A CHAIN SNAPS."). The Grand Arena (Phase 5.13: beat the Beast Lord, `UL_GrandArena`).
- Secret: the Pit Goblin (the rattling crate); the Ashen Oracle (Phase 5.12: the masked stranger in the Stone Amphitheater, flag `ashen_stranger`; also adds the Ashen Censer).

**Three new synergies.** The new Boons give MOVEMENT, GOLD and XP their third carrier:

| Synergy | Tag | Carriers | Effect |
|---|---|---|---|
| **Whirlwind** | MOVEMENT | Light Feet, Blood Slide, Winged Sandals | +10 % speed; slides leave a trail that Slows (existing `SlideTrail`) |
| **Midas** | GOLD | Fortune's Favour, Loaded Dice, Gilded Palm | +25 % gold, +25 % pickup range, +0.1 Luck |
| **Prodigy** | XP | Greedy Hands, Quick Study, Wax Tablet | +15 % XP, +1 reroll, +1 banish |

## Secret encounters

**The Golden Rat (Slave Pit).**
- **When:** with a 50 % chance per run, at a random moment between 1:30 and 6:00, a golden rat scurries out of a hole at one of the layout's secret spots.
- **How it behaves:** it flees the player at 8.5 m/s, faster than a run (7.5) but slower than a slide, so you catch it with movement. Every 4 s it stops for 0.6 s to sniff. It slides along the arena's edge and the obstacles.
- **Readability:** it's gold (treasure in the colour language), shown 1.6× life size (a real rat's size was lost on screen), sparkles every 0.5 s and squeaks. Weapons and enemies ignore it.
- **The catch:** touch it within 18 s and it bursts into coins (+30 gold), with "THE GOLDEN RAT!", a crowd cheer and Favor. The first catch also says "WHAT WAS THAT?!" and sets the flag `golden_rat` (unlocks the Sling).
- **The escape:** after 18 s it dives back into a hole ("IT GOT AWAY!").
- It keeps appearing after you've caught it (for the gold).

**The Bookmaker (Provincial Pit).** A hunched bookmaker with a ledger stands at one of the arena's secret spots (a corner under the stands).
W1 (the producer's note 23, docs/tracks/W1-wager.md) replaced his single 30-gold bet with **his book**.
- **The call:** within 9 m he calls "PSST! A WAGER, CHAMPION?", "[E]  THE BOOK" shows over him, and his gold chalk ring glows.
- **The book:** E (gamepad North) in his ring opens it, costing nothing; a chest in reach still takes the press. The run pauses
  under "THE BOOKMAKER'S BOOK": 1–4 wager cards in the level-up's row, and WALK AWAY (Esc, gamepad East) to leave. With no
  gold he still opens it: a card you can't pay shows "Need n gold" on its disabled BET plate.
- **The wagers** (the stake doubling; N is worked out from your kills of the last 30 s each time the book opens, at most 600):

  | Tier | Stake | Challenge | Time | Pays | Opens at |
  |---|---|---|---|---|---|
  | I | 5 | kill N: 0.8 × your pace, at least 25 | 30 s | 15 | always |
  | II | 10 | kill N: 1.2 × your pace, at least 50 | 30 s | 35 | 100 kills this run |
  | III | 20 | kill N **without being hit** (1.0 × pace, at least 40); any damage loses | 30 s | 80 | 300 kills |
  | IV | 40 | **his champion**: an elite (the Director's elite pick, with an affix) comes in at a gate; kill it | 45 s | 200 | 600 kills |

  **Luck** may open one more tier than your kills did: chance Luck / 0.5 (the level-up's fourth-card rule), rolled once per
  opening on a seeded fork. Fewer than four cards: "MORE KILLS, BIGGER WAGERS" under the row.
- **A pick:** he takes the stake and the HUD counts the wager down under the timer ("WAGER I", "WAGER II",
  "WAGER III · NO HITS", "THE CHAMPION").
- **Win:** he pays the tier's payout, "THE BOOKMAKER PAYS UP!", a cheer, Favor. The first win **at tier II or higher** sets the
  flag `bookmaker_paid` (unlocks Loaded Dice): tier I is too easy to be the secret.
- **Lose:** "THE HOUSE WINS.", the crowd boos (time out, a hit on tier III, or the run ending). If his champion leaves the
  arena without being killed, it's no contest and the stake comes back.
- **Again:** after each wager he waits 60 s ("COME BACK LATER" over him; E only gets his line), then calls again.
  No limit per run (`maxWagers` 0).

Both are new components in the RattlingCrate mould: spots from `ArenaLayoutDef.secretSpots`, a flag saved at once, and API hooks for tests. They have models (`PR_GoldenRat`, `CH_Bookmaker`) and placeholder SFX (squeak, coin shower, quill).

## The Codex
- **Layout:** a tabbed screen: RECORDS · GLADIATORS · ARSENAL · BESTIARY · CHALLENGES · SECRETS · MARKET.
- **Header:** the Renown balance.
- **NEW markers:** on entries unlocked or discovered since you last looked (`codexSeen`), and on the menu's CODEX button ("CODEX • 3 NEW").
- **Records:** the current numbers plus best kills in a run, evolutions and synergies found.
- **Arsenal:** every weapon, evolution, Boon and synergy as an icon card.
  - Locked ones show their unlock: "Secret", "Challenge: …" or "Market: 300".
  - Evolutions and synergies stay "???" until found, with a hint: "Max the Spear with the right Boon" / "3 FIRE upgrades".
  - Taken ones show how many times.
- **Bestiary:** every enemy and boss. Killed ones show their name, a line of text and the kill count; unseen ones are a black silhouette and "???". Portraits are rendered from the models by an Editor bake into an atlas, so a silhouette is the same picture in black.
- **Challenges:** name, goal, progress bar, reward.
- **Secrets:** found = what it was; unfound = "???" plus the hint.
- **Market:** price, BUY (only when affordable), then OWNED.
- **Controls:** pages, not scrolling (◀ ▶; 9 cards a page, 6 on the list tabs), for the gamepad. Left/Right (or the shoulder buttons) change tab, Up/Down change page (in the Market: the item), Enter buys. All of it is built from data (MetaConfig, UpgradeCatalog, enemy/boss lists), so a new unlock or enemy appears by itself.
- **The end screen:** names each new unlock with its category and any bounty ("CHALLENGE COMPLETE: First Evolution +100 Renown").

## Tests
- **EditMode:**
  - the new condition kinds and Progress;
  - a bounty is paid once;
  - Market: Buy spends Renown, refuses when you can't afford it, and conditions never grant a Market unlock;
  - the RunSummary records feed the save;
  - Migrate fills the new lists.
- **PlayMode:**
  - **the Golden Rat:** appears, flees faster than a run, a touch catches it (the test teleports; in play a slide closes the gap), the flag is set, and the Sling unlocks at the run's end; it dives away when its time is up;
  - **the Bookmaker** (W1): E opens the book and pauses, walking away costs nothing, a pick takes the stake and starts
    the countdown, a tier I win pays but keeps the flag, the cooldown then a second wager, a card you can't pay is
    refused, tier III loses on a hit, tier IV sends one elite and its kill pays and sets the flag; the book passes the
    UI audit with 1 and 4 cards. EditMode `WagerMathTests`: the table, the offer count from kills + Luck, N from the pace;
  - **each new weapon fires** (Net roots, Whip pierces a line and Bleeds, Sling hits the farthest target);
  - **the new synergies switch on;**
  - **the Codex:** builds every tab with no errors, shows NEW once, and Market BUY works.
- **Bot:** unchanged (it ignores secrets); 10-minute Slave Pit run for performance.

## Art (time-boxed, CLAUDE.md §2.9)
- **Models:** `WP_Sling` (stone + strap), `WP_Net` (weighted net disc), `WP_Whip` (handle + segmented lash), `PR_GoldenRat`, `CH_Bookmaker`. Silhouette test for each.
- **Icons:** 10 pixel icons (3 weapons, 4 Boons, 3 synergies).
- **Codex portraits:** baked from the existing models.

## Tunables
- the rat's `chance`, `window`, `speed`, `life`, `gold`;
- the Bookmaker's `tiers` (per tier: `stake`, `payout`, `seconds`, `paceFactor`, `minKills`, `unlockKills`), `maxKills`,
  `cooldownSeconds`, `maxWagers`, `flagMinTier`, `championGateDistance`;
- Market prices and bounties (`UnlockDef`);
- `MetaConfig` Renown per run, unchanged.

## Out of scope (BACKLOG)
- run modifiers and cosmetics bought with Renown;
- more secrets per arena;
- the Ashen Oracle's encounter;
- per-gladiator records;
- a Codex 3D model viewer.

## Edicts (R7, v0.21.0)
- **The Editor Takes Notice** (beat Gorm): opens the Edicts with Iron Hides, The Hungry Horde and Blood Sport.
- **Market, 150 Renown each:** Glass Jaw, Swift Foes, Empty Purses, A Fickle Crowd.
- **Challenges:** Under the Editor's Eye (win a run under 2 edicts: Meagre Choices, The Impatient Emperor), A Day of Champions (reach the Grand Arena: Champions' Day), The Emperor's Pleasure (win a run under 4 edicts: No Mercy). Condition `WinWithEdicts` (index 15).

## The Daily Games (R8, v0.22.0)
- **UL_Daily** (beat Gorm, with the Edicts): the DAILY button opens.
- **Challenges:** Daily Bread (play 3 scored dailies, `DailiesPlayed 3`, +100 Renown), Champion of the Day (win a scored daily, `DailiesWon 1`, +150).

## Skins (R6, v0.23.0)
Twenty skins, two per gladiator, made by `Gladiator/Setup/Gladiators/Create Skin Data` from `BlenderSrc/characters/skins.json`: one `SK_*` + `UL_Skin_*` pair each. Worn from the strip under the select screen's diorama (Q / E, LB / RB; F / gamepad North buys).
- **Market (10):** Freedman, Sewer King, Ashen Knight (250 each); Headsman of Rome, Storm-Grey, Silver Idol, Bone Oracle, Deep Sea, Winter Hunter, The White Bull (350 each). 3,200 Renown in all.
- **Champion (10, `WinWithGladiator <gladiator> 3`):** Unbowed, Ratcatcher, Night Knight, Bronze Colossus, Olympian, Jade Idol, Violet Oracle, Pearl Diver, Black Wolf, Bronze Bull.
