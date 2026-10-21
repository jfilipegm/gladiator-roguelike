# Bosses

Each boss gets this spec before any code (CLAUDE.md §6.4): distinct silhouette, telegraphed unique attacks, arena
interaction, ≥ 2 phases, a memorable entrance, personality.

## Gorm, the Pit Master (Arena 1 — Slave Pit)

1. **Who:** the slaver who owns the pit and its champion; a huge, hunched man (~3.4 m, twice the Nobody) in a studded leather apron and an iron slaver's collar of keys, chain flail in the right hand, bullwhip in the left. Bald, beard, one golden earring. He thinks you're property.
2. **Silhouette:** wide hunched shoulders, a long arm ending in the spiked flail ball, the coiled whip in the other hand, a ring of keys at the belt. Nothing else in the pit is that tall or carries a ball on a chain.
3. **Entrance (timer hits 0:00):** the horde is dragged off (they die where they stand), the crowd roars, "THE PIT MASTER ENTERS!", the camera turns to the north gate, the doors swing open, Gorm strides in cracking the whip, stops, roars (shake, crowd spikes). ~4 s; the player keeps control. HP bar with his name appears.
4. **Flail Sweep (near):** spins the flail around himself — orange-red ring (radius 5.5 m) fills over 1.1 s, then the flail sweeps at knee height: jump over it or be outside the ring. Heavy damage + knockback.
5. **Whip Lash (mid-range):** a dotted orange-red lane (12 m) toward you fills over 0.7 s, then cracks along it: damage and Slow (2 s). Step sideways.
6. **Chain Slam (any range):** a circle (2.6 m) on your position, lagging slightly, fills over 1.0 s, then the ball smashes down. Keep moving.
7. **Call the Guards (every ~22 s):** blows a horn — "GORM CALLS HIS GUARDS!" — both gates open and 6–10 Shield Thugs and Convicts pour in. Adds the horde back as a pressure layer, never as the main fight.
8. **Phase 2 — "Burn it!" (at 55 % HP):** roars (invulnerable 1.5 s), throws a torch at the fence: a ring of fire runs along the whole wall foot (permanent burning ground, arena ~1.5 m smaller), attacks 30 % faster, Chain Slam leaves a burning patch, and a new **Charge**: a lane toward you fills over 0.9 s, then he barrels down it (hits once, knocks you aside; slams into the wall and is stunned 1 s if he misses).
9. **Arena interaction:** gates (guards), the fence (fire ring), mud slows him like everyone (3.0 → 2.1 m/s), fire pits don't hurt him (he's the owner).
10. **Defeat:** slow-mo, a big death pop, the crowd goes wild, "GORM FALLS!", then a pick of 3 Rare-or-better upgrades (the Pit Master's hoard) and the victory screen. First kill unlocks the reward from M3.6.

**Tunables (`BossDef BD_Gorm`):** HP, move speeds per phase, every attack's wind-up / damage / size / cooldown / weight, guard interval and counts, phase threshold, fire ring radius and dps, enrage speed multiplier.

## The Provincial Champion (Arena 2 — Provincial Pit)

1. **Who:** the town's undefeated hero, a murmillo who has never fought anyone from outside the province. Proud, loud, plays to the crowd. He thinks you're a fluke.
2. **Silhouette:** broad and armoured (~2.6 m), a **huge curved tower shield** (scutum) on the left arm that covers him from shin to chin, a fish-crested murmillo helm with a tall red crest, a short gladius. Nothing else carries a door-sized shield.
3. **Entrance (timer hits 0:00):** the horde is dragged off, the crowd chants, "THE CHAMPION OF THE PROVINCE!", the camera turns to the north gate, he walks in banging his gladius on the shield, raises it to the crowd (shake, crowd spike). ~4 s; the player keeps control.
4. **Shield Wall (passive):** his shield blocks 90 % of hits from the front (a 140° arc). The fight is about getting to his side or back, or making him turn: bleed and burn ticks ignore the shield, and a blast that lands beside or behind him isn't blocked (one in front of him is, like the Shield Thug's).
5. **Shield Bash (near):** a short orange-red cone (4 m, 100°) in front of him fills over 0.7 s, then he slams the shield forward: damage + big knockback. Get behind him or out of the cone.
6. **Gladius Flurry (near-mid):** three quick thrusts down a narrow lane (6 m) toward you, each with its own 0.35 s lane telegraph, re-aiming between them. Step sideways each time.
7. **Shield Charge (any range):** a wide lane (2.4 m) toward you fills over 1.0 s, then he charges 18 m behind his shield. He **smashes every barricade** in the lane and topples braziers (burning ground). If he hits a **pillar or the wall** he's **stunned 2 s with his shield down** (no block, takes +50 % damage). Lure him into the stone.
8. **Call the Guards (every ~25 s):** "TO ME, BROTHERS!": two gates open, 4–6 Shield Archers and Shield Thugs pour in.
9. **Phase 2 — "For the province!" (at 50 % HP):** roars (invulnerable 1.5 s), the crowd throws him a second gladius, and every spike trap fires at once on his roar. Attacks 25 % faster, the Charge comes twice in a row (re-aiming between), and the Flurry has five thrusts.
10. **Arena interaction and defeat:** barricades (smashed by charges), pillars and walls (stun him), braziers (burning ground), spike traps (phase-2 volley; they hurt him too, 1/4 damage), gates (guards). Defeat: slow-mo, "THE CHAMPION FALLS!", the crowd goes silent then wild, a pick of 3 Rare-or-better upgrades, victory screen.

**Built (Phase 5.4):** all ten points except the crowd throwing him a second gladius (BACKLOG). Body `EN_Champion` (13,000 HP, 540 tris), tuning `BD_Champion`. The boss bar reads "THE PROVINCIAL CHAMPION, UNDEFEATED".

**Tunables (`BossDef BD_Champion`):** HP, speeds, block arc and reduction, every attack's wind-up / damage / size / weight, charge stun and damage bonus, guard interval and counts, phase threshold, phase-2 speed-up, the flurry count per phase.

## The Beast Lord (Arena 3 — Stone Amphitheater)

1. **Who:** the master of the amphitheatre's beast pens, raised among his animals and more beast than man now. He feeds the losers to his boars. He thinks you're meat.
2. **Silhouette:** huge and hunched (~3.3 m), a **whole lion's pelt** worn as a cloak with the lion's head as his hood and its mane over his shoulders, a **long hooked goad-spear** held upright (taller than him), a coiled chain and a war horn at the belt. Nothing else wears a mane or carries a pole that long.
3. **Entrance (timer hits 0:00):** the horde is dragged off, every cage rattles at once, "THE BEAST LORD!", the camera turns to the great gate, he walks in dragging his goad and blows his horn (shake, crowd spike). ~4 s; the player keeps control.
4. **Goad Sweep (near):** a wide orange-red cone (5 m, 150°) fills over 0.8 s, then he sweeps the goad round: damage + knockback. Get behind him or out of reach.
5. **Hook Thrust (mid):** a lane (11 m) fills over 0.6 s, then he lunges the hooked goad down it: damage and Slow (2 s). Step sideways.
6. **Pounce (any range):** a circle (3.5 m) on your position, lagging a little, fills over 1.0 s; he leaps in an arc and lands there: damage + a knockback ring. He's slow to get up (0.8 s): the window to punish him. Keep moving.
7. **Call the Beasts (every ~20 s):** blows the horn, "THE PENS OPEN!": the nearest cage opens and 3–5 war boars charge out (plus two Pit Curs). The boars trample his own horde too.
8. **Phase 2 — "Feed them!" (at 50 % HP):** roars (invulnerable 1.5 s), **every cage opens at once** (a stampede from all four), he moves and attacks 25 % faster, and the Pounce comes twice in a row (re-aiming between).
9. **Arena interaction:** the cages (his beasts), the stage platform (he pounces onto it after you), the columns and statues (cover from a stampede: a boar stops dead on stone). Spike traps and barricades don't exist here.
10. **Defeat:** slow-mo, "THE BEAST LORD FALLS!", the beasts scatter (every enemy left is dragged off), the crowd goes wild, a pick of 3 Rare-or-better upgrades, victory screen.

**Tunables (`BossDef BD_BeastLord`):** HP, speeds, every attack's wind-up / damage / size / weight, pounce radius / flight time / recovery, beast-call interval and pack size, phase threshold, phase-2 speed-up and pounce count.

## The Gilded Patron (Arena 4 — Grand Arena)

1. **Who:** the richest man in the city. He owns the Grand Arena, half the fighters in it and most of the people watching. Fat, gilded and bored. He thinks you're an investment that stopped paying.
2. **Silhouette:** he lounges on a gilded **litter** under a red canopy, carried on the shoulders of four bearers: ~3.6 m tall and wider than anything else (the poles ~4.4 m long). Round, in gold and crimson robes, a laurel wreath, a purse in one fist and a goblet in the other. Nothing else has a roof.
3. **Entrance (timer hits 0:00):** the horde is dragged off, coins rain from the patron boxes, "THE GILDED PATRON!", the camera turns to the north gate, the bearers carry him in, he raises his goblet to the boxes (shake, crowd spike). ~4 s; the player keeps control.
4. **Coin Bombs (any range):** he hurls purses one after another (3; 5 in phase 2) at orange-red circles (2.4 m) on you and around you; each lands 1.0 s after it's thrown and bursts: damage, and it scatters **real gold** (grab it after the blast). Keep moving.
5. **Golden Volley (mid):** a fan of red-hot coins (7, 50°; 11, 70° in phase 2) toward you after a 0.8 s wind-up (the fan's lanes glow): step into a gap or out of the fan.
6. **Shove (near):** "Out of my way!" A cone (4.5 m, 120°) in front of the litter fills over 0.7 s, then the bearers barge forward: damage + big knockback. Don't stand in front of him.
7. **Hire the Guard (every ~22 s):** "GOLD TALKS!" He tosses purses to the boxes and 4–6 **Gilded Mercenaries** jump down from the two patron boxes nearest you. **Payroll:** while any mercenary lives, a gold thread runs from each to him and **he takes only 60 % of the damage** (`guardedDamageMul` 0.6 since C24; "GUARDED" on his bar). Kill the guard, then hit him.
8. **Phase 2 — "Everything is for sale!" (at 50 % HP):** roars (invulnerable 1.5 s), throws a fortune to the crowd: **every fire-jet row fires at once, and the blade sweepers spin for the rest of the fight**. Attacks 25 % faster, five bombs, the wide volley.
9. **Arena interaction:** the patron boxes (his guard), the gates (his entrance), the fire jets and the blades (phase 2; they hurt him too, a quarter, like the Champion's spikes), the dais and the statue (cover from the volley: a coin stops on stone).
10. **Defeat:** slow-mo, "THE PATRON IS BANKRUPT!", the litter crashes down and his fortune bursts out (a fountain of gold coins), the boxes go wild, a pick of 3 Rare-or-better upgrades, victory screen.

**Tunables (`BossDef BD_GildedPatron`):** HP, speeds, bomb count / radius / flight / damage / gold, volley count / arc / speed / damage, shove cone, guard interval / count, `guardedDamageMul`, phase threshold, phase-2 speed-up, defeat gold.

## The Emperor's Champion (Arena 5 — Imperial Arena)

1. **Who:** the Emperor's own gladiator, a hundred games and never beaten, a soldier before he was a fighter. Cold, exact, contemptuous; he fights for the imperial box, not for the crowd. He thinks you're the evening's entertainment before the real games.
2. **Silhouette:** upright and armoured (3.3 m to the helm, 4.6 m to the wingtips: the biggest humanoid in the game), an **eagle on the helmet** with its wings raised in a V (nothing else has wings), a **spear taller than he is** upright in the right hand, a curved purple **scutum** at his left side (not square in front like the Provincial Champion's), a **quiver of javelins fanning over his left shoulder**, a purple cloak; gold and crimson (A5). He reads as a soldier, not a brute: straight back, no hunch, no litter. *(Model: `EN_EmperorsChampion`, 1,304 tris, docs/tracks/R1-char-art.md.)*
3. **Entrance (timer hits 0:00):** the horde is dragged off, trumpets twice, "THE EMPEROR'S CHAMPION!", the camera turns east to the **triumphal gate** at the sphendone's apex, the doors swing open, he marches in along the axis (7.5 m), stops and raises his spear to the imperial box (shake, crowd spike). ~4 s; the player keeps control. Then he **strides** toward you (6.5 m/s while you're more than 28 m away): on a 190 m floor he mustn't spend a minute walking.
4. **Spear Thrust (mid):** a lane (10 m × 1.3 m) toward you fills, then he lunges the spear down it: damage, small push. Step sideways. *(kind `Whip`, no Slow)*
5. **Shield Bash (near):** a cone (4.5 m, 110°) in front of him fills, then he punches the scutum forward: damage + big knockback. Get beside or behind him. His scutum blocks **half** of what hits him from the front (100° arc); bleed and burn ticks ignore it. *(kind `Bash`; body `blockArc 100`, `blockReduction 0.5`)*
6. **Javelin Volley (mid-far):** he draws javelins from his back; a fan of lanes (5 javelins, 36°) glows toward you, then they fly fast and straight. Step into a gap or behind stone (the obelisk and the metae stop them). Thrown at your height, so the spina is no shelter. *(kind `Volley`, drawn as the Shield Archer's arrow)*
7. **Phase 2 — "PRAETORIANS, TO ME!" (at 60 % HP):** roars (invulnerable 1.5 s) and, on the roar, **two Praetorian formations** (a Centurion + 5 Praetorians each, docs/ARENAS.md §5) march in through the **two gates nearest you**; again every 26 s for the rest of the fight. Wind-ups and recovery 20 % faster, the volley widens (7, 50°), and a new **Shield Charge:** a wide lane (20 m × 2.6 m) fills, then he charges behind his scutum. If he hits the **obelisk, a meta or the wall** he's **stunned 1.8 s with his guard down** (takes +50 %). He smashes braziers in his path (burning ground).
8. **Phase 3 — "FOR THE EMPEROR!" (at 30 % HP):** roars (invulnerable 1.5 s): **the chariot races run back to back** for the rest of the fight (a race starts at once if none is running), he moves faster (4.6 m/s) and attacks 40 % faster, and the charge comes **twice in a row** (re-aimed). The formations keep coming. The chariots trample everything on the track, his guard and him included.
9. **Arena interaction:** the triumphal gate (his entrance), the side gates (his formations), the **chariots** (phase 3 back to back; a chariot hits him for 600, so lure him across the track), the obelisk, the metae and the walls (his charge stuns on them), braziers (toppled into burning ground), the obelisk and metae (cover from javelins), the spina (he follows you up).
10. **Defeat:** slow-mo, "THE EMPEROR'S CHAMPION FALLS!", the races halt (the chariots pull off), the Guard stands down (every enemy left is dragged off), the crowd goes wild, a pick of 3 Rare-or-better upgrades, victory screen.

**Bar:** "THE EMPEROR'S CHAMPION, FIRST SPEAR OF THE EMPIRE". **Body** `EN_EmperorsChampion`: 18,000 HP, radius 1.25 (char-art's measure: torso ±1.1, shield to −1.57), knockback 0, block 100° / 50 %. **Words:** guards "THE PRAETORIAN GUARD!", summary "The Emperor's Champion" / *"The Emperor's Champion turns to the imperial box. The thumb goes down."* / *"The Emperor's own champion lies in the sand, and the Emperor is on his feet."*

### Attacks per phase (starting numbers; damage is before the arena's ×1.45)
Effective wind-ups are the listed wind-up ÷ the phase's speed (1.0 / 1.2 / 1.4). He recovers 0.6 s after every attack (÷ the same speed); there are no per-attack cooldowns: `BossController.Weight` picks by range and the last attack is 0.3× as likely.

| Attack (kind) | Telegraph | Wind-up P1 / P2 / P3 (s) | Damage | Size | Knockback | Phases |
|---|---|---|---|---|---|---|
| Spear Thrust (`Whip`) | lane, fills from him | 0.65 / 0.54 / 0.46 | 22 | 10 m × 1.3 m | 3 (fixed) | 1, 2, 3 |
| Shield Bash (`Bash`) | cone, fills outward | 0.65 / 0.54 / 0.46 | 20 | 4.5 m, 110° | 12 | 1, 2, 3 |
| Javelin Volley (`Volley`) | one lane per javelin | 0.85 / 0.71 / 0.61 | 16 each | P1 5 × 36°; P2–P3 7 × 50°; 22 m/s, 30 m | — | 1, 2, 3 |
| Shield Charge (`Charge`) | wide lane | — / 0.75 / 0.64 | 26 | 20 m × 2.6 m, 17 m/s | 13 | 2 (once), 3 (twice) |
| Contact | — | — | 12 / 0.8 s | his radius | 4 | all |

| | P1 (100–60 %) | P2 (60–30 %) | P3 (30–0 %) |
|---|---|---|---|
| Move speed | 3.4 m/s (6.5 when > 28 m away) | 3.9 | 4.6 |
| Attack speed | ×1 | ×1.2 (`enrageSpeed`) | ×1.4 (`phase3Speed`) |
| Guards | none | 2 formations on the roar, then every 26 s, through the 2 gates nearest you | same |
| Charge stun | — | 1.8 s, takes ×1.5 | same |
| The races | every 60 s (the arena's clock) | same | back to back (2 s between races) |

### For the code worker: reused vs new
- **Reused as is:** attack kinds `Whip`, `Bash`, `Volley`, `Charge` (no new `BossAttack` value), the frontal shield (`blockArc` on the body), the charge stun on stone with `stunDamageTakenMul`, `ArenaBreakables` (braziers), `guards` / `guardCount` / `guardsEvery`, `scatterOnDefeat`, the entrance and the reward.
- **New in `BossDef`:** `phase3At` (0 = no phase 3: every older boss unchanged), `phase3Attacks`, `phase3Text`, `phase3Speed`, `phase3MoveSpeed`, `phase3ChargeRepeats` (0 = phase 2's); `guardsFromPhase` (1 = from the start, as today); `guardsNearestGates`; `volleyJavelins`; `closeInRange` / `closeInSpeed` (0 = off).
- **New in `BossController`:** a third phase (every `_phase == 2` check becomes `>= 2`; `Speed` returns `phase3Speed` in P3; the Enraging state carries the phase it leads to, and the phase-2 extras such as Gorm's fire ring only fire on the way to P2); the guard clock starts at `guardsFromPhase` with an immediate call on that roar; guards through the gates nearest the player when `guardsNearestGates`; javelins via `EnemyProjectiles.FireStraight(..., arrow: true)` aimed at the player's height; striding when far.
- **Not his code:** the races going back to back and halting are `ChariotRace`'s; it reads the existing `BossController.Active.Phase` and `.Defeated`, so the two parts don't depend on each other. Formations are `EnemyManager.Leash`'s (the guards are just `EN_Centurion`). Full field list: `docs/tracks/R1-design.md`.

**Tunables (`BossDef BD_EmperorsChampion`):** HP and block, speeds per phase and the close-in stride, every attack's wind-up / damage / size, the volley's count / arc / speed per phase, charge stun and damage bonus, phase-3 charge repeats, guard interval / count / first phase, phase thresholds (0.6 / 0.3), phase speeds (1.2 / 1.4).
