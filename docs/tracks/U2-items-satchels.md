# U2 — Items, Satchels, Epic, and three sources of power (spec for the producer's OK)

The producer's note 31 on v0.28.0 and their answers (2026-10-24, `N29-v028-notes.md`). After v0.29.0 (the quick fixes).
Status: **approved by the producer 2026-10-24** ("The items and satchels plan is OK, build it after v0.29.0"), with the
open points as written: 2 / 3 / 3 starting slots, 6 / 9 / 12 caps, the Blood Altar gives gear. Numbers are starting
values; all of them are data.

## 1. Three sources, three kinds of power
| Source | Gives | Never gives |
|---|---|---|
| **Level-up** (pick 1 of 3, rerolls/banishes as now) | **Upgrades:** new Boons (while a boon slot is free) and **levels** for the Boons and weapons you carry ("Gladius → Lv 4") | new weapons, items |
| **Armory Chest** (gold; Uncommon or better, as now) | **Gear:** new weapons (while a weapon slot is free), **items**, **satchels**; evolutions stay in its first card as today | Boons, levels |
| **Boss kill** (pick 1 of 3) | **Treasure:** Rare, Epic or Legendary weapons, items or satchels | Boons, levels, anything below Rare |
| Blood Altar (risk/reward) | Rare-or-better **gear**, as a chest that costs blood | Boons, levels |

Other sources (crowd gifts, the Market, the Bookmaker, challenges) keep what they give today, except that any weapon they
would hand out follows the chest's rule. When a source has nothing left to offer (all slots full, all levels maxed), it
falls back to today's Consumable cards (gold, heal).

## 2. Epic rarity
Common · Uncommon · Rare · **Epic (purple)** · Legendary. Base weights 60 / 25 / 10 / 4 / 1 (Luck shifts them as today);
stat magnitudes ×1.0 / 1.4 / 1.8 / 2.2 / 2.6. Boss treasure rolls with its own weights: Rare 60 / Epic 30 / Legendary 10.
Epic slots in before Legendary in the enum; every asset that stores a rarity is migrated once by an Editor step
(Legendary stays Legendary), with a test.

## 3. Items (new)
- Passive gear, one clear effect each, built from what the game already has (stat modifiers and the event-bus effects
  the Boons use), so most items are data.
- Each item **has** a rarity (a Lucky Coin is always Common), and the chest picks the item after rolling the rarity.
- **Stacking:** taking an item you own adds a stack (shown "×3"); every stack adds its effect again. A few strong ones
  cap (e.g. the Phoenix Feather at 3).
- First set: about 24 items, roughly 8 Common, 6 Uncommon, 5 Rare, 3 Epic, 2 Legendary. Examples:
  - Common: Whetstone (+6 % damage), Bread Ration (+15 max HP), Lucky Coin (+5 Luck), Sandal Straps (+6 % move speed).
  - Uncommon: Lion's Tooth (+5 % crit), Coin Purse (+12 % gold).
  - Rare: Laurel Wreath (+1 reroll and +1 banish), Wolf Pelt (+20 % summon damage).
  - Epic: Tyche's Dice (+25 Luck).
  - Legendary: Phoenix Feather (rise once at 50 % HP per stack), Mars' Spearhead (+1 projectile, +15 % damage).
  - Every item gets a pixel icon and synergy tags like the Boons.

## 4. Satchels: slots are earned
You start with **2 weapon slots, 3 boon slots, 3 item slots** (today: 4 weapons, 6 boons, no items). Satchels are gear
(chests, bosses, the altar) and don't take an item slot. Each satchel card names its kind, and its rarity sets how many
slots it adds:

| Rarity | Weapons | Boons | Items |
|---|---|---|---|
| Common | — | +1 | +1 |
| Uncommon | +1 | +1 | +2 |
| Rare | +1 | +2 | +2 |
| Epic | +2 | +2 | +3 |
| Legendary | +2 | +3 | +4 |

Caps: 6 weapons, 9 boons, 12 items; a satchel for a full kind isn't offered. Names by kind:
- weapons: Baldric (a sword belt);
- boons: Scrip (a shepherd's bag);
- items: Belt Pouch, up to a Legion Sarcina (a marching pack) at Legendary.

Gladiators who start with two weapons (the Beastmaster after note 37) start with both slots filled.

## 5. Gold: the first chest comes early
Every enemy's gold chance ×3 (a Convict 5 % → 15 %; elites and Gilded unchanged), through one `goldChanceMul` in
the progression config so it can be tuned in one place. Chest price 15, then ×1.3 per chest (today ×1.4). Target,
measured with bot runs: **the first chest affordable by ~1:00, 6–9 chests in an 8-minute run.**

## 6. What the player sees
- **Cards:** a kind label on each ("BOON", "WEAPON", "ITEM", "SATCHEL"); levels show "Lv 3 → 4"; Epic in purple.
- **Pause screen:** weapons, boons and items, each row as wide as its slots, with empty slots shown, so a satchel
  visibly adds room. Items show their stack count.
- **In-run strip:** a third row with small item icons.
- **Codex:** the Arsenal tab lists items too.

## 7. Build plan (after the OK)
- Contracts first: `Rarity.Epic`, `UpgradeKind.Item` / `Satchel`, `BuildState` slot counts and item stacks, and the
  roller's source filter (`OfferSource.LevelUp` / `Chest` / `Boss` / `Altar`).
- Then three workers on disjoint files:
  - **W-core:** enums, migration, `BuildState`, `UpgradeRoller` and `UpgradeConfig`, the source filters, EditMode tests;
  - **W-items:** `ItemSetup.cs`, the items and satchels as data, item effects, `CardText`, icons via `PixelSrc/`;
  - **W-ui:** card labels and Epic colour, the pause inventory rows, the HUD item row, the Codex.
- The orchestrator integrates, re-runs the setups, measures the gold and chest timing with bot runs, then runs the
  suites, takes screenshots and opens the PR.

