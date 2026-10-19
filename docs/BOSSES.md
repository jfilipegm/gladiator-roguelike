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
