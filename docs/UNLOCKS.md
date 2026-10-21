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
  - `SynergiesFound` (amount = distinct synergies ever switched on).
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
| **Loaded Dice** (GOLD) | Boon: +Luck, +gold | Secret: **the Bookmaker** | win his wager in the Provincial Pit |
| **Gorm's Whip** (BLOOD) | weapon: a 6 m thin crack through everything in a line, at the two nearest enemies in turn; Bleeds; **the tip** (last 1.5 m) always crits, so you fight at whip's length | Challenge **Pit Master's Bane** | defeat Gorm 3 times |
| **Net** | weapon: a slow thrown net, wide, pierces, Roots for 1.5 s | Challenge **Champion of the Provinces** | defeat the Provincial Champion |
| **Winged Sandals** (MOVEMENT) | Boon: +1 jump in mid-air per level (max 2) | Market | 300 Renown |
| **Wax Tablet** (XP) | Boon: +XP, +1 reroll per level | Market | 300 Renown |
| **Gilded Palm** (GOLD) | Boon: +gold, +pickup range | Market | 300 Renown |

**Renown challenges** (bounty only): First Evolution (evolve any weapon, +100); Synergist (3 different synergies, +150); Butcher (5,000 kills in one run, +100); Every Gladiator Wins (win with the Nobody, the Goblin and the Knight, +200).

**The existing unlocks are recategorised:**
- Challenges: Gorm's Flail (beat Gorm), the Provincial Pit (beat Gorm), the Blood Knight (beat Gorm below 30 % HP; **Phase 5.9: reach the Stone Amphitheater**, §6.7's "Reach Arena 3"), the Murder Machine (Phase 5.9: beat Gorm; also adds the Executioner's Cleaver to the pool), the Thunder Champion (Phase 5.10: beat the Beast Lord, §6.7's "Complete Arena 3"; also adds the Storm Spear).
- Secret: the Pit Goblin (the rattling crate).

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
- **The call:** within 9 m he calls "PSST! A WAGER, CHAMPION?" once per run, and his gold chalk ring glows.
- **The wager:** stand in the ring for 1.2 s and he takes a 30-gold stake (no gold, no wager) and posts it: "KILL N IN 30 S". N is 1.3 × your kills in the last 30 s, at least 60, so he bets against your own pace.
- **Win:** he pays 90 gold, "THE BOOKMAKER PAYS UP!", and the flag `bookmaker_paid` (unlocks Loaded Dice).
- **Lose:** "THE HOUSE WINS.", the crowd boos.
- One wager per run, and the HUD shows its countdown.

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
  - **the Bookmaker:** stake, win and lose;
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
- the Bookmaker's `stake`, `payout`, `seconds`, `paceFactor`, `minKills`;
- Market prices and bounties (`UnlockDef`);
- `MetaConfig` Renown per run, unchanged.

## Out of scope (BACKLOG)
- run modifiers and cosmetics bought with Renown;
- more secrets per arena;
- the Ashen Oracle's encounter;
- per-gladiator records;
- a Codex 3D model viewer.
