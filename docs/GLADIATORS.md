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

## The Blood Knight (built, Phase 5.3, v0.4.0)
| | |
|---|---|
| **Fantasy** | A fallen knight who fights for the blood, not the crowd. Grim, heavy, and most dangerous when he should already be dead. |
| **Silhouette** | Tall (1.65 m to the top of the helm, 1.9 m with the horns; the Nobody is 1.6 m) and broad: blackened plate with spiked pauldrons, a horned great-helm with a narrow glowing visor, a tattered crimson cape with a pale cyan sword sigil (the "that's me" mark from the camera behind him), and a 1.4 m greatsword held low and forward. 64 px test against the Nobody, the Goblin, the Brute and Gorm: the horns, the cape and the blade. |
| **Weapon** | **Crimson Greatsword**, `MeleeArc` data: a slow 220° cleave with 3.4 m reach, 1.5 s cooldown, 28 damage and heavy knockback. Every hit applies a Bleed stack (the new `WeaponDef.onHitStatus`, melee patterns). Levels 2–8: damage, area, attack speed, a second swing, crit, knockback. |
| **Passive: Bloodthirst** | Kills heal him: 1 HP per kill, 8 per elite, at most 6 HP/s. Every other healing (regen, amphorae, healing cards) is **halved**: he lives on what he kills. |
| **Passive: Bloodlust** | Stronger at low HP: up to **+60 % damage**, rising as HP falls and full at 20 % HP or less. The HUD shows the bonus beside the Blood meter. |
| **Ability: Blood Rite** | A **Blood meter** (0–100, crimson, under the HP bar) fills from kills (+0.6, elites +8) and from damage taken (+0.5 per HP). When it's full, the next **slide** performs the Rite: he pays 12 % of max HP (never below 1 HP, and it isn't a hit: no hurt flash, no Favor close call), the meter empties, and for 8 s he is **frenzied**: +40 % damage, +25 % attack speed, kill heals doubled. "BLOOD RITE!" and a crimson burst. The meter doesn't fill during the frenzy. |
| **Strength** | Sustain through kills, a huge arc, and enormous damage at low HP. The Rite turns HP into a burst exactly when the horde is thickest. |
| **Weakness** | Slow (92 % move speed). Other healing is halved. The big slow swing misses fast Pit Curs early. |
| **Personality** | Says nothing. The cape trails, the blade rests on his shoulder between swings, and the visor is the only light on him. |
| **Unlock (interim): a bloodied victory** | §6.7 says "Reach Arena 3", which can't happen while the game has one arena. Until it does: **defeat Gorm with less than 30 % HP left**. It's a new condition kind, `DefeatBossBelowHp` (the save keeps the lowest HP each boss was beaten at), so the asset can switch to `ReachArena 2` later without code. Codex hint: *"Gorm must fall to someone already bleeding out (below 30 % HP)."* The unlock also adds the Crimson Greatsword to everyone's level-up pool. |

**Colours:** his blood effects (the meter, the Rite's burst, the frenzy's specks) use the existing Bleed crimson-pink (`SE_Bleed` colour) with white cores, darker and pinker than the orange-red that means "this hurts you". The visor is the player's cyan: from the camera behind him, the cape and the visor say "that's me".

**How it's built:** `GL_BloodKnight` + traits `TR_Bloodthirst` (`Bloodthirst`), `TR_Bloodlust` (`Bloodlust`), `TR_BloodRite` (`BloodRite`, the meter); `PlayerHealth.HealingMul` (Bloodthirst sets it; its own heals bypass it) and `PlayerHealth.Spend` (HP paid, not damage taken). Setup in `GladiatorSetup`; model `BlenderSrc/characters/ch_bloodknight.py` (iteration 2, 980 tris; the greatsword is part of the model, in the right hand).

**Tests:** kill heals and the 6 HP/s cap; other healing halved; the damage bonus at 100 / 50 / 20 % HP; the meter fills, a slide with a full meter pays HP (never kills) and frenzies, and an early slide does nothing; greatsword hits bleed; the unlock condition, and beating Gorm at low HP unlocks the knight while beating him at high HP doesn't.

## The Murder Machine (built, Phase 5.9, v0.10.0)
| | |
|---|---|
| **Fantasy** | The arena's executioner, sent down to the sand because nobody would pay to see him beheading people any more. Absurdly huge, slow, armoured like a furnace, and quite calm about it. |
| **Silhouette** | The biggest gladiator: 2.15 m to the tip of a tall pointed executioner's hood (the Nobody is 1.6 m), a barrel of riveted iron plate as wide as he is tall at the shoulders, short iron legs, a leather apron, and an enormous **rectangular cleaver** (1.3 × 0.55 m blade, a hole in its corner) held in both hands. 64 px test against the Knight, the Brute, Gorm and the Beast Lord: the pointed hood and the slab of a blade. From the camera behind him: a pale cyan axe sigil on his back plate. |
| **Weapon** | **Executioner's Cleaver**, the new **`GroundSlam`** pattern (CLAUDE.md §6.8 "great axe/cleaver: slow huge AoE"). Every 2.1 s he raises it over his head and brings it down 2 m in front of him, toward the nearest enemy: everything in a 2.4 m circle there takes 34 damage and is thrown outward, and a pale ring runs out across the sand. Point blank always connects. Levels 2–8: damage, area, a second chop (Amount), attack speed, crit, knockback. |
| **Passive: Executioner's Plate** | Every hit he takes is **4 lower** (after the arena's multiplier; never below 1), and **nothing knocks him back**: not boars, not bosses, not Brutes. Damage over time (Burn, fire patches) isn't reduced. |
| **Ability: Verdict** | A **Wrath meter** (0–100, pale steel with a cyan edge, under the HP bar) fills **only from hits he takes** (+8 per hit). When it's full, his next **jump landing** is the Verdict: "VERDICT!", a 7 m shockwave around him, 60 × his damage to everything in it, a big knockback, and survivors are **Rooted for 1 s** (bosses aren't rooted, only hurt). The meter empties. |
| **Strength** | Wades into the horde: the swarm's small hits barely scratch him (a Convict's 8 becomes 4, a Cur's 6 becomes 2), every hit brings the Verdict closer, and the cleaver clears a crowd in front of him. 150 HP. |
| **Weakness** | Slow (85 % move speed) with a slow weapon that only hits in front: fast Curs and assassins get round him. The plate does little against big hits (a boss's 24 is still 20). |
| **Personality** | Says nothing; he's done this before. Heavy, deliberate strides; the cleaver held low in front of him, raised over his head with both hands for every chop; every chop makes the ground jump. (Resting it on his shoulder between chops: BACKLOG.) |
| **Unlock: defeat Gorm** | §6.7. `UL_MurderMachine` (`DefeatBoss gorm 1`). Codex hint: *"Gorm keeps an executioner below the pit. Kill Gorm, and he'll want new work."* The unlock also adds the Executioner's Cleaver to everyone's level-up pool. A save that already beat Gorm gets him at the next main menu. |

**Colours:** the cleaver's ring and the Verdict's shockwave are player cyan-white, never orange-red. The meter is pale steel with a cyan edge, so it doesn't read as the Blood meter's crimson.

**How it's built:**
- **`GroundSlam`** in `WeaponSystem`: aim at the nearest enemy, `windup` then a circle at `range` ahead with radius `width × √area`, everything in it hit outward from the centre; Amount = more chops right after, at whoever is nearest then. The ring is a new pooled `SlamRings` drawer (`TelegraphRenderer.Circle` growing over 0.25 s), dust from `DeathPopFx`.
- **Traits:** `TR_ExecutionersPlate` (`ExecutionersPlate`: `PlayerHealth.FlatReduction`, `PlayerMotor.KnockbackTakenMul` = 0) and `TR_Verdict` (`Verdict`: the meter from `DamagedEvent`s that aren't status ticks, `OnLand` with a full meter).
- **Animation:** `PartAnimProfile.overheadChop` (the arm pitches from over the head to the ground instead of sweeping sideways) and `twoHanded` (the left arm follows).
- **Model:** `BlenderSrc/characters/ch_murdermachine.py` (≤ 1,200 tris, the cleaver part of the model in the right hand).
- **Setup:** `GladiatorSetup.MurderMachine()` (`GL_MurderMachine`, `MovementConfig_Machine`, `PartAnim_Machine`, `WP_ExecutionersCleaver` + `UP_W_ExecutionersCleaver`, `UL_MurderMachine`).

**Tests:** a chop hits the circle ahead and not behind, and throws enemies out; hits are 4 lower but never below 1, status ticks aren't reduced; boars and bosses don't knock him back; the meter fills from hits only; a landing with a full meter hits and roots the ring and empties the meter, and a landing with a part-full meter does nothing; beating Gorm unlocks him.

### The Blood Knight's real unlock (Phase 5.9)
Arena 3 exists, so `UL_BloodKnight` switches from the interim "beat Gorm below 30 % HP" to §6.7's **"Reach Arena 3"** (`ReachArena 2`: start a run in the Stone Amphitheater). A save that already unlocked him keeps him (unlocks are saved by id).

## The Thunder Champion (built, Phase 5.10, v0.11.0)
| | |
|---|---|
| **Fantasy** | A champion who swears the storm god fights beside him, and on the sand it looks like he's right. Fast, flashy, never standing still. |
| **Silhouette** | 1.75 m to the top of a bronze storm helm with a tall **zigzag lightning crest** (white-cyan), bronze cuirass and greaves over a blue tunic, a short blue cape, and a **storm spear** held upright with a glowing tip. 64 px test against the Nobody (also a spear in the pool), the Knight and the Net-Thrower: the zigzag crest and the upright spear. From the camera behind him: the crest and the cape's white bolt. |
| **Weapon** | **Storm Spear**, the new **`Chain`** pattern (CLAUDE.md §6.8 "Chain"). Every 0.9 s lightning leaps from the spear's tip to the nearest enemy within 9 m, then jumps to the nearest enemy it hasn't struck within 5 m, 3 more times. Every strike is 11 damage and **Shocks** (the existing `SE_Shock`: they take more damage). Levels 2–8: +1 jump, damage, attack speed, a second bolt (Amount), +1 jump, damage, attack speed + jump range. |
| **Passive: Stormcaller** | Every enemy his weapons strike adds a **Charge**: +1.5 % attack speed each, up to 40 (+60 %). Two and a half seconds without striking anything and the Charge drains away. The HUD bar shows it. |
| **Ability: Lightning Dash** | Starting a slide with the dash ready (every 3 s) is a **blink**: he flashes up to 7 m along the slide (stopped by walls), and everything within 1.2 m of the path takes 25 × his Damage and is Shocked. A white-cyan bolt marks the path. Then he slides on as usual. |
| **Strength** | Attack speed that snowballs in a crowd, chains that clear packs, Shock that makes everything else hit harder, and the best escape in the game. |
| **Weakness** | Fragile (90 HP). Against one big target there's nothing to chain to, so bosses and lone elites take only the first strike. Charge drains if he hides. |
| **Personality** | Long bouncy strides and a lean into the run; a short jab of the spear at whatever he strikes. (The crest flickering at high Charge: BACKLOG.) |
| **Unlock: complete Arena 3** | §6.7. `UL_ThunderChampion` (`DefeatBoss beastlord 1`). Codex hint: *"The storm god is watching the amphitheatre. Win there."* The unlock also adds the Storm Spear to everyone's level-up pool. |

**Colours:** his lightning is the player's white-cyan (the boon sparks' `Bolt`), never orange-red. The Charge bar is electric blue with a white edge when it's near full.

**How it's built:**
- **`Chain`** in `WeaponSystem`: the first strike on the nearest enemy within `acquireRange`, then `Pierce` jumps to the nearest un-struck enemy within `width × √Area` of the last one; each strike is a `Strike` (on-hit status, Boon procs); `PlayerProjectiles.Bolt` for every leg; Amount = more bolts right after.
- **Traits:** `TR_Stormcaller` (`Stormcaller`: Charge from `HitEvent`s with a weapon source, an AttackSpeed `StatMod` updated on change, drained after `graceSeconds`) and `TR_LightningDash` (`LightningDash`: on `SlideEvent` with the cooldown ready, `CharacterController.Move` along the slide so walls stop him, then a lane hit along the path).
- **Model:** `BlenderSrc/characters/ch_thunderchampion.py` (≤ 1,200 tris).

**Tests:** a bolt strikes the nearest and chains to the next ones but not to one beyond jump range, and Shocks them; Charge raises attack speed and drains after the grace time; a slide with the dash ready blinks forward, hits the enemies on the path and not one beside it, and goes on cooldown; a wall stops the blink; beating the Beast Lord unlocks him.
