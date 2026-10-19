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
