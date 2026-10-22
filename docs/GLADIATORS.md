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

## The Golden Idol (built, Phase 5.11, v0.12.0)
| | |
|---|---|
| **Fantasy** | A champion so rich he had himself gilded. The crowd adores him; the other fighters can't believe he's real. Gold is his armour, his weapon and his religion. |
| **Silhouette** | 1.7 m, round and gleaming: a gilded body, a golden mask for a face, a crimson sash, and a huge **sun-crown** (a spiked golden disc) behind his head, the biggest head-piece in the roster. A sceptre with a golden orb in his left hand. 64 px test against the Nobody, the Champion and the Beast Lord: the spiked disc. From the camera behind him: the disc again, its back painted player cyan. |
| **Weapon** | **Coin Toss** (`Projectile` data, no new code): every 1.1 s he flicks 3 gold coins at the 3 nearest enemies within 12 m; each coin does 9 damage and goes through 1 enemy. Levels 2–8: +1 coin, damage, +1 pierce, attack speed, +1 coin, damage, attack speed. |
| **Passive: Midas Body** | **Gold is power:** +1 % damage for every 10 gold he's carrying, up to +100 % at 1,000. Spending gold (chests, Market... and Make It Rain) spends power. The HUD bar shows the bonus. |
| **Passive: Golden Streak** | The announcer's kill streaks pay: **KILLING SPREE!** ×1.5 gold for 8 s, **MASSACRE!** ×2, **BLOODBATH!** ×2.5, **UNSTOPPABLE!** ×3 (a GoldGain multiplier). |
| **Ability: Make It Rain** | Starting a slide with at least 40 gold (every 2 s): he pays 40 gold and **16 coins spray out in a ring**, 20 × his Damage each, through 1 enemy. Gold turned straight into damage, at a cost to Midas Body. |
| **Strength** | Scales without limit in a rich run, and streaks feed it; Make It Rain clears a ring round him on demand. |
| **Weakness** | Weak early (no gold, no bonus), and every chest is a choice between an upgrade and his damage. 100 HP. |
| **Personality** | Struts with a bounce; flicks coins with a wave of the wrist. |
| **Unlock: 10,000 lifetime gold** | §6.7. `UL_GoldenIdol` (`LifetimeGold 10000`). Codex hint: *"Gold, and plenty of it: 10,000 over all your runs."* The unlock also adds the Coin Toss to everyone's level-up pool. |

**Colours:** his coins are the pickup gold (yellow), never orange-red.

**How it's built:** `WP_CoinToss` is `Projectile` data with the `PR_Coin` mesh. Traits: `TR_MidasBody` (`MidasBody`: a Damage `StatMod` from `RunState.Gold`, like Bloodlust), `TR_GoldenStreak` (`GoldenStreak`: a GoldGain `StatMod` from `AnnounceKind.Streak` tiers, for 8 s), `TR_MakeItRain` (`MakeItRain`: on `SlideEvent`, `RunState.SpendGold` then 16 `PlayerProjectiles.FireStraight` coins). Model `BlenderSrc/characters/ch_goldenidol.py`.

**Tests:** the damage bonus at 0 / 250 / 2,000 gold; a streak announce multiplies GoldGain and it wears off; a slide with 40+ gold sprays 16 coins and costs 40, a slide with less does nothing; Coin Toss fires coins at the nearest; 10,000 lifetime gold unlocks him.

## The Ashen Oracle (built, Phase 5.12, v0.13.0)
| | |
|---|---|
| **Fantasy** | A masked seer who breathes smoke and reads the future in the ash of the dead. Nobody knows where he came from; he only shows himself to fighters he's chosen. |
| **Silhouette** | 1.65 m, thin and hunched in a long ash-grey robe that flares at the hem, a **pale bone mask** with glowing purple cracks, a hood, and a crooked **censer staff** (a hook at the top with a smoking censer swinging from it). 64 px test against the Net-Thrower, the Slinger and the Goblin: the hook-and-censer staff and the robe's flare. From the camera behind him: a cyan rune on his hood. |
| **Weapon** | **Ashen Censer** (`Lob` data plus hit effects): every 1.8 s he lobs a smoking censer-orb into the thickest nearby crowd; it bursts in a 2.4 m cloud for 8 damage, **Curses** everything it hits (2 stacks), and leaves a **cursed patch** on the ground for 3 s that keeps cursing whatever walks in. Levels 2–8: a second orb, damage, area, attack speed, a third orb, damage, area. |
| **New status: Curse** | Damage over time that stacks (up to 5, 4 s), purple. On its own it's slow. |
| **Passive: Contagion** | A **cursed enemy that dies bursts**: 6 × his Damage within 3.5 m, and the curse passes to everything in the burst. Curses that spread on death, so the horde does his killing for him. |
| **Ability: Smoke Veil** | The first hit he takes every 5 s makes smoke **explode out of him**: everything within 4.5 m is Cursed (3 stacks), Slowed for 2 s and pushed back. He's fragile; the veil buys him space. |
| **Strength** | The bigger the crowd, the faster it dies: curses chain through it. Area denial that keeps working after he's moved on. |
| **Weakness** | Fragile (85 HP) and slow to kill one big target (damage over time, and bosses don't spread). Early on, before the curses stack, he's weak. |
| **Personality** | Silent; drifts rather than runs (a low, smooth stride); the censer smokes. |
| **Unlock: a secret encounter** | §6.7. **The Masked Stranger:** on about half the runs in the **Stone Amphitheater**, once, between 2:00 and 5:00, a masked figure stands in a ring of smoke at one of the arena's secret spots for 25 s (smoke rises from it, so it's seen). Stand **inside his ring for 3 s** while he reads you (the ring fills) and he vanishes in a burst of ash: "WHO WAS THAT?". The save gets the flag `ashen_stranger` at once; `UL_AshenOracle` (Flag) unlocks the Oracle at the run's end. Once found, he stops appearing. Codex hint: *"A masked stranger watches the amphitheatre's fights. He wants a closer look at you."* The unlock also adds the Ashen Censer to everyone's level-up pool. |

**Colours:** curses and smoke are purple and ash-grey, the player's side of the palette's cursed family, never orange-red. The stranger's waiting ring is pale purple.

**How it's built:**
- **`StatusKind.Curse`** (a sixth enemy status slot) and `SE_Curse`.
- **`TraitEffects`**: a gladiator trait that adds Boon effects (`BoonEffects` reads them with the Boons'). Contagion is a `BurstOnDeath` on Curse, so it's data.
- **`WP_AshenCenser`** is `Lob` data with `hitEffects` (`StatusOnHit` Curse, `GroundOnHit` Curse), so no new weapon code.
- **`TR_SmokeVeil`** (`SmokeVeil`: `DamagedEvent`, cooldown, a radial curse + slow + push).
- **`AshenStranger`** (`ArenaSecrets.AshenStranger`, placed by `ArenaBuilder` at the layout's `secretSpots`; the Stone Amphitheater gets spots in its three tunnel mouths and by the statues).
- **Model:** `BlenderSrc/characters/ch_ashenoracle.py`; the stranger uses the same model.

**Tests:** a censer burst curses what it hits and leaves a patch that curses an enemy walking in; a cursed enemy's death bursts and passes the curse on; a hit releases the veil (curses, slows, pushes) and a second hit inside the cooldown doesn't; standing in the stranger's ring for 3 s saves the flag, stepping out resets the reading, and he leaves after 25 s; the flag unlocks the Oracle.

## The Net-Weaver (Phase 5.14)
| | |
|---|---|
| **Fantasy** | A fisherman who walked into the arena with his own net and never lost a fight he wanted to win. A retiarius: no helmet, no shield, only reach, patience and a mocking grin. He catches men like tuna, then spears them in the net. |
| **Silhouette** | 1.65 m, bare-headed (a fisherman's knotted headband, a topknot), a tall bronze **galerus** rising above his left shoulder, a wrapped manica on the left arm, a wide belt and a short tunic. A **net** hangs from his left shoulder down his back like a cape with lead weights on its fringe, and a long **trident** (three prongs) is held across his body. 64 px test against the Net-Thrower (an enemy that also carries a trident and a net), the Thunder Champion (an upright spear) and the Nobody: the galerus over one shoulder and the net cape. From the camera behind him: the net cape, its cords player cyan. |
| **Weapon** | **Trident**, the new **`MultiHit`** pattern (CLAUDE.md §6.8 "Trident (3-prong multi-hit)"). Every 1.2 s, **three quick jabs fanned across 36°** at the nearest enemy (left, centre, right, 0.07 s apart), each a 4.2 m line, 8 damage. Up close all three land on the same enemy (24); further out they spread across three. Levels 2–8: damage, area, attack speed, a second flurry (Amount), damage, crit chance, area + damage. |
| **Passive: Caught** | His hits do **+50 % damage to Rooted** enemies and **+25 % to Slowed** ones (both Boon effects, so they work with every weapon he picks up: the Net, the snares...). |
| **Ability: Cast Net** | Starting a slide with the net ready (every 4 s) **flings a weighted net 5 m ahead** along the slide. Where it lands, everything within 3.2 m is **hauled toward the middle and Rooted** (2 s), and takes 6 × his Damage. Bosses can't be held: they're only Slowed (and so Caught). The net lies on the sand while it holds them. |
| **Strength** | Control: he gathers a crowd into one spot and holds it there for the trident. Brutal up close against a single target. |
| **Weakness** | Short reach (4.2 m, the Spear reaches further), no armour, 95 HP. Without the net ready he has to stand close to the horde to hurt it. |
| **Personality** | A low, wide stance; light on his feet (105 % speed); a showman's twirl of the trident between flurries. |
| **Unlock: win with the spear** | §6.7 "Win with the spear/trident". `UL_NetWeaver` (new condition **`WinWithWeapon spear 1`**: beat a run's boss while holding the Spear). Codex hint: *"A fisherman watches the spearmen from the stands. Win a run with a spear in your hands."* The unlock also adds the Trident to everyone's level-up pool. |

**Colours:** the net, its ring and the trident's jabs are the player's cool white-cyan, never orange-red.

**How it's built:**
- **`MultiHit`** in `WeaponSystem`: on impact, `prongs` thrusts at `arcDegrees` / (prongs − 1) apart, `prongInterval` s apart (a thrust line each, like the Spear's `ThrustHit`, with the thrust visual per prong); Amount = more flurries at whoever is nearest next.
- **`WinWithWeapon`** unlock condition: `SaveData.weaponWins` counts a win for every weapon held at the end of a won run (`RunSummary.weapons`).
- **Traits:** `TR_Caught` (`EffectsTrait`: `DamageVsStatus` Root 0.5, Slow 0.25) and `TR_CastNet` (`CastNet`: on `SlideEvent` with the net ready, a 0.3 s throw arc, then the haul: a knockback toward the landing point, Root (Slow for bosses), damage, and the net drawn on the sand while it holds).
- **Model:** `BlenderSrc/characters/ch_netweaver.py` (≤ 1,200 tris), `WP_Trident`, `PR_NetCast`.

**Tests:** a flurry close up hits one enemy three times; at range the prongs hit three enemies spread across the fan and miss one outside it; Caught adds 50 % against a rooted enemy; a slide with the net ready hauls a group toward the landing point and roots it, a second slide inside the cooldown doesn't; a boss is slowed, not rooted; a won run holding the Spear unlocks him and a won run without it doesn't.

## The Beastmaster (Phase 5.15)
| | |
|---|---|
| **Fantasy** | A hunter from the frontier forests who walked into the arena with his wolves and refused to fight without them. He hates the handlers who whip beasts into the sand, and the crowd loves his animals more than him. |
| **Silhouette** | 1.65 m, a **wolf-head hood** (the snout over his brow, two ears up), a shaggy fur mantle over the shoulders, a leather jerkin, a curved **hunting horn** on a strap across his chest, and a short hunting spear held low. 64 px test against the Nobody, the Net-Weaver and the Pit Cur: the ears on the hood and the mantle's bulk. From the camera behind him: the mantle, its fur tipped player cyan. |
| **Weapon** | **Wolf Pack**, the new **`Summon`** pattern (CLAUDE.md §6.8 "Summon"). Two wolves fight beside him for the whole run. Each runs at the nearest enemy within 10 m of him, bites for 9 every 0.8 s, and never strays more than 12 m from him (it comes back). Enemies don't target them: they're his weapon, not units to babysit. Levels 2–8: +1 wolf, damage, bite speed, +1 wolf, run speed + reach, damage, +1 wolf (5 at most). |
| **Passive: Feral Bond** | When he's hit, his wolves **frenzy**: +60 % bite speed and +25 % run speed for 4 s (their eyes and collars flash). Hurting him is how the crowd gets the show. |
| **Ability: Hunting Horn** | Starting a slide with the horn ready (every 5 s) blows it: every wolf **pounces** on the enemy nearest the slide's end (8 m at most), a leap that deals 3 bites and **Staggers**. |
| **Strength** | Damage that keeps working while he runs and dodges; the wolves fight on their own, and the horn turns them into a burst on demand. |
| **Weakness** | The pack is his damage: early he has two wolves and nothing else. They can't reach what's more than 12 m from him, and a boss shrugs off the bites. 100 HP. |
| **Personality** | A loping, wide stride; the wolves circle him while there's nothing to hunt. |
| **Unlock: free the beasts** | §6.7 lists "kill a Beast Handler elite 25 times"; elites of one type are too rare for 25 (about one handler elite every few runs), so: **kill 25 Beast Handlers** (any). New enemy **the Beast Handler** (Tier 2, the Stone Amphitheater and the Grand Arena): a whip-cracking handler who walks two Pit Curs on leashes; kill him and his curs are **freed**: they bolt from the arena instead of fighting on. New condition `KillEnemies` (id = EnemyDef id, amount = lifetime kills; the Bestiary already counts them). Codex hint: *"Somewhere a man is waiting for someone to free the beasts. Kill their handlers."* The unlock also adds the Wolf Pack to everyone's pool. |

**Colours:** the wolves are grey with player-cyan collars and eyes (the Pit Curs have magenta collars and yellow eyes); the pounce's landing burst is white-cyan. The Beast Handler's leashes are plain rope on the sand (not a hazard colour).

**How it's built:**
- **`Summon`** in `WeaponSystem`: `TickSummon` drives a `CompanionPack` on the player from the weapon's numbers (Amount = wolves, Damage = a bite, cooldown = seconds between one wolf's bites, speed = run speed, acquireRange = hunting radius around the player, range = the leash). Bites go through `WeaponSystem.CompanionHit` (the weapon's damage, crits, on-hit status, Boon procs and kill credit). The companions are a handful of pooled GameObjects with rigid parts (`CH_Wolf`), driven centrally; enemies ignore them.
- **Traits:** `TR_FeralBond` (`FeralBond`: on a `DamagedEvent`, `CompanionPack.Frenzy`) and `TR_HuntingHorn` (`HuntingHorn`: on a `SlideEvent` with the horn ready, `CompanionPack.PounceAll` at the point 4 m along the slide; the horn waits if there's nothing to hunt).
- **The Beast Handler:** `EN_BeastHandler` (a Lunger) with the new `EnemyDef.escort` / `escortCount` / `leashLength`: `EnemyManager.Leash.cs` spawns the escorts with him, keeps them within the leash while he lives (a rope drawn with `TelegraphRenderer.Lane`), and frees them (a quiet despawn, no kill) when he dies.
- **Unlock:** new condition `KillEnemies` (the Bestiary's `enemyKills`); `UL_Beastmaster` = `KillEnemies beasthandler 25`.
- **Models:** `BlenderSrc/characters/ch_beastmaster.py`, `ch_wolf.py`, `BlenderSrc/enemies/en_beasthandler.py`.

**Tests:** he spawns with two wolves that hunt the nearest enemy and bite it (the weapon's kills count); levels add wolves; a wolf past the leash runs back; the horn makes every wolf pounce on a different enemy ahead, which takes three bites and is Staggered, and it waits when there's nothing to hunt; a hit on him frenzies the pack; a handler spawns with two leashed curs that stay within the leash, and killing him frees them (no kills counted); 25 handler kills unlock him.

## Asterion, the Chained (R3, v0.20.0)
| | |
|---|---|
| **Fantasy** | On the Colosseum's opening day, four hundred years ago, the Eternal Victor's first fight was against a beast dragged out of a labyrinth across the sea: Asterion, half man, half bull, who had never hurt anyone who didn't come into the dark to kill him. The Victor didn't kill him. He chained him under the stage as a trophy, and for four centuries the crowd has heard him roar beneath the sand before every games. Gentle and lonely when he's calm, a storm when the crowd closes in. He doesn't fight for glory: the arena is the only world he has. |
| **Silhouette** | 1.81 m to the horn tips, but stooped: the head hangs low and forward of a huge shoulder hump, with the widest head in the roster, **horns sweeping out ~1.25 m tip to tip** (wide, not tall: tall horns read as the Blood Knight's at 64 px), the right tip chipped, a bronze ring in his nose. A pale ash-grey hide (his father was the white bull from the sea) with a charcoal mane on the hump and a pale muzzle; long heavy arms, short thick legs on hooves; a Minoan-blue loincloth with a meander hem; a rope-worn bronze collar with a broken ring, a broken shackle on the left wrist, and on the right wrist the shackle of **the chain that drags behind him to a block of the labyrinth's wall** (a carved stone, a meander on its top): he carries a piece of his prison. A few pale scars. 64 px test against the Murder Machine (the other heavy), the Beastmaster (another beast's head), the Blood Knight (horns on a helm) and the Brute: the horns' width, the forward hump and the stone dragging behind. From the camera behind him: a **cyan labyrinth brand** on his upper back. |
| **Weapon** | **Labyrinth Chain**, the new **`Sweep`** pattern (CLAUDE.md §6.8 "chain sweeps"). Every 1.6 s he hauls the chain round: a 0.32 s wind-up, then the chain **sweeps 240° across the ground in 0.3 s**, from one side of the nearest enemy to the other (the next sweep goes the other way), **5.2 m** long. Everything it passes takes **15** and is **shoved sideways**, the way the chain is moving: the crowd sways aside like grass under a scythe. **The stone** (the chain's last 1.4 m) always crits (×2 = 30) and shoves 1.6× harder: fight at the chain's length. Shields only stop it if they face the way it comes from, so it swings round the ones that face you (a boss's shield still blocks). Levels 2–8: +20 % damage, +15 % reach, **the backswing** (a second sweep straight back the other way, no wind-up), +15 % attack speed, +25 % damage and +20 % knockback, +15 % reach, a third sweep and +15 % damage. |
| **Passive: Gore** | Running (4 m/s or faster) into enemies head-first gores them: anything his horns meet within 60° of where he's going takes **8 × his Damage** and is **tossed aside** out of his path, each enemy at most once a second. Bosses only take the damage. A thin line can't stop him; he goes through it, at the cost of a scrape or two (their contact damage still lands). |
| **Ability: Rage** | A **Rage meter** (0–100, bronze, under the HP bar) fills from **the press of the crowd**: every second, **+1 for every enemy within 3.5 m** of him (an elite or a boss counts 4), at most +5 a second ("CORNERED" by the bar at the cap), and **+3 for every hit he takes** (status ticks don't count). After 2.5 s with nobody that close and no hit, it cools at 8 a second. Steam snorts from his nostrils past half. When it's full he is **Unchained**. It isn't a button: the rage takes him. "UNCHAINED!": a **bellow** throws back everything within 4 m (8 × his Damage). For **8 s**, while the bar drains: the chain **whirls all the way round him** (360°, every sweep), +30 % attack speed, +20 % move speed, nothing knocks him back, and he **gores everything he runs into from any side** (14 × his Damage, every 0.5 s per enemy, at 2.5 m/s or faster). He swells (×1.1) and sheds white-gold specks. Then he's **Spent** for 2 s: 75 % move speed, head bowed, and the meter can't fill. |
| **Strength** | Owns the ring round him: 5.2 m sweeps across 240° (360° when Unchained) clear a crowd on both sides at once, the stone kills a Convict in one hit at the chain's length, and Gore takes him through a line instead of round it. The thicker the press, the sooner he's Unchained, and Unchained is the strongest crowd-clearing state in the roster. 130 HP. |
| **Weakness** | Big and heavy: 95 % speed, the widest capsule (0.45 m) after the Machine's, no armour, and every knockback lands except while Unchained. The sweep is slow to come round: enemies hugging him take only the weak inner hits (a Convict survives one), and a boss takes one hit every 1.6 s. Kiting cools his Rage; it comes when the crowd decides, not when you would choose, and it ends Spent. |
| **Personality** | Silent. A heavy stoop, head low, the stone grinding along behind him. Standing still, he lifts his head to the sky (he hasn't seen it for four hundred years). Unchained, he drops his horns and runs. Spent, he looks down at his hands. |
| **Unlock: defeat the Eternal Victor** | §6.7 "Defeat the final boss". `UL_Asterion` (`DefeatBoss eternal_victor 1`). Codex hint: *"Something has been chained under the Colosseum since its first games. Its jailer is the Eternal Victor."* At the Victor's fall, while Asterion is still locked, a second line follows "THE ETERNAL VICTOR FALLS!": **"BELOW THE SAND, A CHAIN SNAPS."** (a rattle and a far-off roar). The unlock's line: *"The Eternal Victor is dust, and the chain he kept under the stage for four hundred years has snapped. Asterion can be chosen, and his Labyrinth Chain joins the level-up pool."* A save that already beat the Victor gets him at the next main menu. |

**How he plays differently:**
- **From the Murder Machine (the other heavy):** the Machine plants his feet, chops the spot in front of him, shrugs off small hits (armour, no knockback) and wants to be hit (Wrath), then jumps for the Verdict. Asterion has no armour and is knocked about. He fights the **ring around him at reach** (the stone at 3.8–5.2 m, to both sides), wants to be **crowded, not hit**, goes **through** lines (Gore), and his payoff is **mobile**: 8 s of running and whirling, not one shockwave on the spot.
- **From the Net-Weaver (the other chain and reach):** the Weaver **gathers** a crowd into one spot, holds it (Root) and stabs it with narrow jabs: control and single-target damage. Asterion **scatters**: sweeps shove crowds sideways and Gore tosses them aside. He's wide and weak against one target, and he never roots.
- **From the Blood Knight (the other huge arc):** the Knight's greatsword hits its whole 220° at once, at 3.4 m, and its frenzy is a stat buff he triggers with a slide. Asterion's chain travels across its arc at 5.2 m with a crit zone at the end, and Unchained changes his weapon and his movement; he doesn't choose when it comes.

**Colours:** the chain bronze, the stone grey with a pale meander; the streak behind the stone and the bellow's rings are the player's white-cyan; Unchained's specks white-gold; his breath white. The Rage bar is bronze, white-gold while Unchained, grey while Spent. Nothing of his is orange-red.

**How it's built (R3):**
- **`Sweep`** in `WeaponSystem`: on impact, the chain's bearing steps from one end of the arc to the other over `sweepTime`, each frame hitting every enemy whose bearing it passed (within `range × area`, once per sweep). Hits in the last `tipLength × √area` always crit and push `tipKnockbackMul` harder. The push is the chain's tangent plus a little outward (`sweepOutward`), radial for bosses. Amount gives backswings at once, alternating direction. The chain is drawn as `Link` meshes plus the `Weight`, with a streak behind the stone, and the gladiator model's `Chain` part is hidden meanwhile (`WeaponDef.modelPart`). `WeaponSystem.WhirlSweeps` makes every sweep 360° (Unchained).
- **Traits:** `TR_Gore` (`Gore`: the horns, from the motor's velocity) and `TR_Rage` (`Rage`: the meter from the press and from `DamagedEvent`s, Unchained's StatMods, `KnockbackTakenMul`, the whirl, the bellow, Spent). The HUD draws Rage on the Blood bar.
- **The tease:** `BossDef.revealText` / `revealUnlock` / `revealDelay`, raised by `BossFight` during the fall while that unlock is locked.
- **Models** (the art part, `claude/zealous-mirzakhani-ed2dcc`): `BlenderSrc/characters/ch_asterion.py` (`CH_Asterion`, 1,148 tris; the `Chain` part is an empty under the root holding `ChainSeg0`–`3` and `ChainEnd`, the stone) and the chain weapon's script (`WP_LabyrinthChain`: `Link` 36 tris, `Weight` 152 tris).
- **Setup:** `AsterionSetup` (`GL_Asterion`, `MovementConfig_Asterion`, `PartAnim_Asterion`, `WP_LabyrinthChain` + `UP_W_LabyrinthChain`, `TR_Gore`, `TR_Rage`, `UL_Asterion`), called from `GladiatorSetup`.
- Numbers, fields, methods and tests per code part: `docs/tracks/R3-design.md`.

**Tests:** the sweep hits what it passes, in order, and not what's past its reach or behind it; the stone crits and the inner chain doesn't; the push is sideways; sweeps alternate, and a backswing follows without a wind-up; a Shield Thug facing him is hit in full and a boss's raised shield still blocks; the whirl hits all round once; Rage fills from the press (capped, elites ×4) and from hits (not ticks), and cools; a full meter Unchains him (bellow, stats, no knockback, the whirl, gore all round) for 8 s, then Spent for 2 s, then everything is back; calm Gore hits ahead, not beside, and not at a walk; a boss is gored but not moved; beating the Eternal Victor unlocks him, and the tease shows only while he's locked.
