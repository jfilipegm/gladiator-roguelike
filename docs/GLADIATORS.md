# Gladiators

Each gladiator is a `GladiatorDef` (data: stats, model, starting weapon, select-screen text, unlock) plus an optional `GladiatorBehaviour` for its signature mechanic (CLAUDE.md §6.7). Identify them by, in order: silhouette → weapon → head → motion → VFX.

## The Nobody (built, starter)
A nameless pit fighter with a rusty gladius and a scrap shield. The baseline: 100 HP, every stat at its default, the Gladius.
- *Underdog* (bonus damage/XP when outnumbered) is **not built yet**. It stays in the backlog until a gladiator comparison shows the Nobody needs it.

## The Pit Goblin (built, Phase 5.2, v0.3.0)
| | |
|---|---|
| **Fantasy** | A tiny, cackling scavenger who has been living under the Slave Pit's floor. Chaotic, greedy and fast. |
| **Silhouette** | Knee-high (1.05 m; the Nobody is 1.6 m), with huge triangular ears, a hunched back and a bulging satchel taller than its head. Green skin and rag wraps. 64 px test against the Nobody and the Pit Cur (also small): the ears and the satchel. |
| **Weapon** | **Bomb Satchel**, a new `Lob` pattern. It lobs a bomb at the densest nearby group. The bomb arcs for ~0.6 s, lands, and its fuse ring shrinks over 0.4 s. The ring is player cyan-white, never orange-red, because it isn't a threat to you. Then it explodes in a 2.2 m blast with knockback. Levels 2–8 add a second and third bomb, a bigger radius, more damage, a shorter fuse and a faster cooldown. |
| **Passive: Scavenger** | Every 25 s the goblin pulls a random improvised weapon out of its sack. It's an unlocked pool weapon it doesn't hold, lent for 15 s at level 1 + (run level ÷ 6), up to 6. This reuses the crowd's weapon-in-the-sand loan, and the loan tile shows it. |
| **Ability: Trap Slide** | Sliding drops a snare behind you (at most 3 on the ground, each lasts 10 s). The first enemy to step in is rooted for 2 s and bleeds. Bosses aren't rooted, only hurt. |
| **Strength** | Speed (+12 % move), reach (+25 % pickup range), area damage, and free weapons from Scavenger. |
| **Weakness** | Fragile: 70 max HP and −10 % damage. Bombs land late, so fast Pit Curs get inside the blast. |
| **Personality** | Cackles on a level-up and when Scavenger fires; its ears flop when it runs; it grabs gems with both hands. |
| **Unlock: the rattling crate** | A discovery, not a goal on a list. One crate in the Slave Pit sits at one of 3 layout spots (`ArenaLayoutDef.secretSpots`), chosen per run. It rattles (a shake plus a clatter) whenever you're within 10 m. Walk into it, or press E beside it. The goblin bursts out, cackles, grabs up to 5 of your gold and dives out of the pit. "WHAT WAS THAT?!" The save gets the flag `rattling_crate` at once, so it's kept even if the run is lost or quit. `UL_PitGoblin` (Flag) unlocks the goblin when the run ends, or at the next main menu. Its reward also puts the Bomb Satchel in everyone's level-up pool. Once found, the crate stops appearing. Codex hint: *"Something rattles in the Slave Pit…"* |

**How it's built:**
- **`GladiatorDef`** holds the model, `MovementConfig` (capsule size), `PartAnimProfile`, camera target height, starting weapon, traits, unlock and select-screen text.
- **`GladiatorLoadout`** (on the one player prefab, runs first) swaps them in at spawn. The goblin keeps the Nobody's part names, so `PlayerPartAnimator` drives it unchanged.
- **Traits** are `GladiatorTrait` assets that install their runtime component: `TR_Scavenger` (`Scavenger`) and `TR_TrapSlide` (`SnareLayer`). A snare holds an enemy with the new **Root** status (speed 0, stops it dead). Scripted enemies, the bosses, ignore Root but still bleed.
- **The Bomb Satchel** uses the new `Lob` pattern in `WeaponSystem` + `PlayerProjectiles`:
  - Of the 8 nearest enemies, it targets the one whose blast would catch the most.
  - `width` is the blast radius. Projectile Speed shortens the flight and the fuse.
  - Bombs never hurt the player.
- **Gladiator select and the Codex** list every `GladiatorDef`. Locked ones show their unlock's hint.
- **Setup:** `Gladiator/Setup/Gladiators/Create Gladiator Data` (`GladiatorSetup`). The models come from `BlenderSrc/characters/ch_pitgoblin.py` (904 tris) and `BlenderSrc/props/goblin_gear.py` (`WP_Bomb` 92 tris, `PR_Snare` 316 tris).
- **Tunables:** `GL_PitGoblin`, `WP_BombSatchel`, `TR_Scavenger`, `TR_TrapSlide`, `MovementConfig_Goblin`, `PartAnim_Goblin`, the `RattlingCrate` component in the Slave Pit scene, and `SE_Root`.

**Systems it needed (original plan):**
- **`GladiatorDef` gains** a model prefab, a starting weapon, an optional behaviour, an unlock, a select-screen line and a locked hint.
- **`GladiatorAppearance`** swaps the player's rigid-part model. The goblin keeps the same part names, so `PlayerPartAnimator` works unchanged. A smaller `CharacterController` and camera target height come from the def.
- **Gladiator select** lists every `GladiatorDef`, locked or unlocked, from data, instead of the hard-coded locked silhouettes.
- **Two new pieces:** the `Lob` weapon pattern and a snare system (pooled, central tick).
- **The secret crate:** `RunState` collects run flags, and `MetaProgress.CommitRun` saves them.

**Tests:**
- A lobbed bomb lands near its target and explodes after its fuse, hitting the whole radius.
- Snares root and expire.
- Scavenger lends a weapon on its timer.
- The crate rattles only within 10 m, breaks, and saves the flag.
- Once the flag is saved, the goblin appears unlocked on the select screen. A run with it uses its model, stats and the Bomb Satchel.

## The Blood Knight (Phase 5.3, v0.4.0)
| | |
|---|---|
| **Fantasy** | A fallen knight who fights for the blood, not the crowd. Grim, heavy, and most dangerous when he should already be dead. |
| **Silhouette** | Tall (1.75 m) and broad: blackened plate with spiked pauldrons, a horned great-helm with a narrow glowing visor, a tattered crimson cape, and a greatsword as long as he is tall carried over the right shoulder. 64 px test against the Nobody, the Goblin, the Brute and Gorm: the horns, the cape and the blade. |
| **Weapon** | **Crimson Greatsword**, `MeleeArc` data: a slow 220° cleave with 3.4 m reach, 1.5 s cooldown, 28 damage and heavy knockback. Every hit applies a Bleed stack (the new `WeaponDef.onHitStatus`, melee patterns). Levels 2–8: damage, area, attack speed, a second swing, crit, knockback. |
| **Passive: Bloodthirst** | Kills heal him: 1 HP per kill, 8 per elite, at most 6 HP/s. Every other healing (regen, amphorae, healing cards) is **halved**: he lives on what he kills. |
| **Passive: Bloodlust** | Stronger at low HP: up to **+60 % damage**, rising as HP falls and full at 20 % HP or less. The HUD shows the bonus beside the Blood meter. |
| **Ability: Blood Rite** | A **Blood meter** (0–100, crimson, under the HP bar) fills from kills (+0.6, elites +8) and from damage taken (+0.5 per HP). When it's full, the next **slide** performs the Rite: he pays 12 % of max HP (never below 1 HP, and it isn't a hit: no hurt flash, no Favor close call), the meter empties, and for 8 s he is **frenzied**: +40 % damage, +25 % attack speed, kill heals doubled. "BLOOD RITE!" and a crimson burst. The meter doesn't fill during the frenzy. |
| **Strength** | Sustain through kills, a huge arc, and enormous damage at low HP. The Rite turns HP into a burst exactly when the horde is thickest. |
| **Weakness** | Slow (92 % move speed). Other healing is halved. The big slow swing misses fast Pit Curs early. |
| **Personality** | Says nothing. The cape trails, the blade rests on his shoulder between swings, and the visor is the only light on him. |
| **Unlock (interim): a bloodied victory** | §6.7 says "Reach Arena 3", which can't happen while the game has one arena. Until it does: **defeat Gorm with less than 30 % HP left**. It's a new condition kind, `DefeatBossBelowHp` (the save keeps the lowest HP each boss was beaten at), so the asset can switch to `ReachArena 2` later without code. Codex hint: *"Gorm must fall to someone already bleeding out (below 30 % HP)."* The unlock also adds the Crimson Greatsword to everyone's level-up pool. |

**Colours:** his blood effects (the meter, the Rite's burst, the frenzy's specks) use the existing Bleed crimson-pink (`SE_Bleed` colour) with white cores, darker and pinker than the orange-red that means "this hurts you". The visor is the player's cyan: from the camera behind him, the cape and the visor say "that's me".

**How it's built:** `GL_BloodKnight` + traits `TR_Bloodthirst` (`Bloodthirst`), `TR_Bloodlust` (`Bloodlust`), `TR_BloodRite` (`BloodRite`, the meter); `PlayerHealth.HealingMul` (Bloodthirst sets it; its own heals bypass it) and `PlayerHealth.Spend` (HP paid, not damage taken). Setup in `GladiatorSetup`; models `BlenderSrc/characters/ch_bloodknight.py` (the greatsword is part of the model, in the right hand).

**Tests:** kill heals and the 6 HP/s cap; other healing halved; the damage bonus at 100 / 50 / 20 % HP; the meter fills, a slide with a full meter pays HP (never kills) and frenzies, and an early slide does nothing; greatsword hits bleed; the unlock condition, and beating Gorm at low HP unlocks the knight while beating him at high HP doesn't.
