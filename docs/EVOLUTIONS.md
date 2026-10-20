# Weapon evolutions and tag synergies (Phase 5.5)

Build depth (CLAUDE.md §9, first "Should"): a run should have two goals beyond "pick the biggest number". First, **finish a weapon**: get it to level 8 with its partner Boon to evolve it. Second, **commit to a theme**: own three upgrades of one tag to switch on that tag's synergy. Both are data. New behaviour lives in a few generic pieces of code that the data composes.

## Evolutions

**Rule.** A weapon at its max level (8) plus its **catalyst** Boon (owned, any level) makes its evolution available. The next **chest, Blood Altar or boss reward** puts the evolution card in its first slot. It's a Legendary card with a gold aura and an "EVOLUTION" banner. Ordinary level-ups never offer it, so chests and gold have a late-run purpose.

Taking the card swaps the weapon for its evolved form **in the same slot**. Slots don't change and the catalyst stays. An evolved weapon starts from the level-8 numbers plus its own changes, and has no more levels.

**Telling the player.** A level-8 weapon's card is never shown again. Instead, the HUD slot of a maxed weapon whose catalyst you don't own shows a small "+ <catalyst>" hint, and the slot pulses gold once the evolution is ready ("open a chest"). The Codex lists the pairs as you discover them (BACKLOG).

| Weapon (tags) | Catalyst | Evolution | What changes (twist) |
|---|---|---|---|
| Rusty Gladius (CRIT) | Lucky Cut | **Champion's Gladius** (CRIT) | **Echo:** every swing is followed 0.12 s later by a mirrored second arc; +10 % crit chance |
| Pit Spear (HEAVY) | Long Reach | **Impaler** (HEAVY) | 1.5× reach; every enemy run through is **Rooted** 0.8 s |
| Javelins | Static Charge | **Thunderbolts** (LIGHTNING) | **Chain:** each javelin hit arcs to 3 more enemies within 6 m and Shocks them |
| Chakram | Quick Hands | **Twin Moons** | **Echo:** a second disc follows 0.25 s later on the mirrored side; +1 pierce |
| Bomb Satchel (FIRE, HEAVY) | Kindling | **Fire-Pot Barrage** (FIRE, HEAVY) | **Ground:** each blast leaves burning ground (2.4 m, 3 s) that Burns whatever walks in |
| Gorm's Flail (HEAVY) | Brute Force | **Chain of Ruin** (HEAVY) | +1 ball, 1.3× size; every hit **Slows** 40 % for 1.5 s |
| Crimson Greatsword (BLOOD, HEAVY) | Open Wounds | **Sanguine Reaver** (BLOOD, HEAVY) | **Echo:** a wider blood cleave follows 0.25 s later; 2 Bleed stacks per hit |

**Generic twists (code), composed by `WeaponDef` data:**
- **Echo** (MeleeArc, Thrust, Returning): repeat the attack after `echoDelay`, mirrored, at `echoScale` size.
- **Status on hit for every pattern:** today only MeleeArc/Thrust apply `onHitStatus`; projectiles, discs, orbit balls and blasts will too.
- **Chain on hit:** a hit arcs to `chainCount` enemies within `chainRadius` for `chainDamage` × the hit, applying `chainStatus`. This reuses the ChainSpark code.
- **Ground on hit** (Lob blasts): a lingering zone (`groundRadius`, `groundSeconds`) applying `groundStatus`. This reuses the slide-trail marks.

## Tag synergies

**Counting.** A tag's count is the number of **distinct owned upgrades** carrying it. Weapons count too (they get tags in this phase) and evolved weapons keep theirs. At **3** the tag's synergy switches on for the rest of the run, announced with "FIRE SYNERGY — WILDFIRE!", a crowd spike and an icon in the HUD's synergy row. Level-up cards show the tag's progress on a chip ("FIRE 2/3") so the choice is visible.

| Tag | Carriers today | Synergy at 3 | How |
|---|---|---|---|
| FIRE | Kindling, Pyre, Conductive Oil, Bomb Satchel | **Wildfire:** each second a burning enemy sets one neighbour within 2.5 m alight | new effect `StatusSpread` |
| BLOOD | Open Wounds, Serrated Edge, Blood Slide, Crimson Greatsword | **Bloodbath:** a bleeding enemy that dies sprays 2 Bleed stacks on everything within 2.5 m | existing `BurstOnDeath` (data) |
| LIGHTNING | Static Charge, Conductive Oil, Thunderbolts | **Storm Front:** every 4th hit forks to 3 enemies within 6 m and Shocks them | existing `ChainSpark` (data) |
| CRIT | Lucky Cut, Serrated Edge, Rusty Gladius | **Executioner:** a crit on a non-boss enemy below 20 % HP kills it outright | new effect `ExecuteOnCrit` |
| HEAVY | Brute Force, Long Reach, Spear, Flail, Bomb Satchel, Greatsword | **Juggernaut:** +20 % area, +40 % knockback, +10 % damage | stat mods (data) |
| DEFENSE | Iron Hide, Second Wind, Thick Skin | **Bulwark:** +3 armor, +15 % max HP, +0.5 HP/s | stat mods (data) |

Movement, XP and Gold have only 1–2 carriers. Their synergies wait until there are more Boons with those tags (BACKLOG). Lightning reaches 3 only through the evolved Thunderbolts, which makes it a late-run goal on purpose.

**Data:** `TagSynergyDef` (tag, count, name, text, icon row, `StatMod[]`, `BoonEffect[]`) listed in `UpgradeCatalog.synergies`. `TagSynergies` (on the player) counts tags whenever the build changes and applies a synergy once when it switches on. Owned upgrades never go away, so nothing has to be undone.

## Art (time-boxed, CLAUDE.md §2.9)
- Evolved weapons reuse their base mesh with the gold/crimson palette swatches and **one** added part each (a guard, a hook, a second disc…), all in `BlenderSrc/weapons/wp_evolved.py`. Silhouette test against the base weapon.
- 7 evolution icons + 6 synergy icons go into the pixel atlas (`PixelSrc/upgrade_icons.py`). The evolution cards always use the Legendary gold aura.

## Tests
- **EditMode:**
  - evolution eligibility (maxed + catalyst; not in level-ups; first slot in chest, altar and boss rewards);
  - tag counting (distinct upgrades, weapons included);
  - each synergy applies once at 3;
  - `StatusSpread` and `ExecuteOnCrit` maths.
- **PlayMode:**
  - taking an evolution swaps the slot and keeps the catalyst;
  - Echo strikes twice;
  - Thunderbolts chain;
  - Fire-Pot ground burns;
  - Wildfire spreads;
  - Executioner kills below 20 % but not a boss.
- **Bot:** prefers an evolution card when offered and logs evolutions taken and synergies active. A 10-minute run in the Slave Pit and in the Provincial Pit.

## Tunables
- `TagSynergyDef.count` (3);
- each synergy's numbers;
- the evolutions' `WeaponDef` twist fields (`echoDelay`, `echoScale`, `chain*`, `ground*`, `onHitStatus*`);
- `UpgradeConfig.evolutionInRewards` (on), and whether level-ups may offer evolutions (off).

## Risks
- **Balance:** evolutions land in the last third of a run, when the horde peaks. That's intended, but it may make the boss easy (C16).
- **Readability:** Echo, Chain and Ground add more player VFX at 300 enemies. Keep them in the player's cool palette and cheap: the same pooled slash, spark and trail systems.
