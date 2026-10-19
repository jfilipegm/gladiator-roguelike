# Gladiator Roguelike — living GDD (≤ 3 pages)

*Working title. Distilled from CLAUDE.md §5–6. Open questions at the bottom are resolved here as we go.*

## Pitch
A 3D horde-survival roguelike in the Megabonk mould. An unknown pit fighter climbs a gladiatorial hierarchy through ever-grander, ever-stranger arenas, unlocking very different fantasy gladiators. The tone is Roman gladiator culture + dark fantasy + myth + absurd characters. It starts grounded and gets steadily weirder.

## Moment to moment
- Third-person free-orbit camera, no lock-on. **Movement is the skill**: sprint, jump, slide (momentum, faster downhill), air control.
- Weapons **fire automatically** (nearest enemy or facing). The player positions, dodges, collects and chooses.
- Kills drop XP gems (magnet radius). Level-up pauses → pick 1 of 3 cards (4 with Luck): new weapon, weapon upgrade, or Boon. Limited rerolls/banishes.
- Pressure ramps with the run timer: swarm spikes, an elite every ~90 s, a **boss when the timer ends** (slice: 8 min, data-driven).
- Map interactables: Armory Chests (gold, price rises), Shrines (risk/reward). Slice: chests cost 15 × 1.4^opened gold and give a pick of 3 Uncommon+ cards; the Blood Altar takes 15 % max HP for good for a pick of 3 Rare+ cards, once. Interact = E / gamepad Y.
- Juice: damage numbers (crit styling), hit flash, death pop, gem vacuum, level-up burst, streak announcer, restrained shake (slider), hit-stop only on crit/elite/boss.

## Readability rules (non-negotiable)
- **Orange-red = hurts the player** (hazards, enemy projectiles, telegraphs). Player VFX are cool white/cyan/gold; XP is green/cyan; gold is yellow.
- Floor and background stay low-contrast; set dressing never breaks enemy silhouettes. Enemies carry a bright accent patch; elites are bigger and glow in their affix colour.
- Must still read at **300 enemies on screen**.

## Look
Chunky low-poly, flat-shaded, saturated; tiny point-filtered palette texture (one material per palette family, ≤ 8 in a scene). Chibi proportions (head ≈ 35–40 % of height), oversized weapons. Rigid-part procedural animation (no skeletons). Light fog, mild bloom/vignette/grade, short-range real shadows + blob shadows for enemies.

## Arenas (each changes rules, not just paint)
1. **Slave Pit** — tight irregular dirt pit, mud (slow), mounds, small fires, sparse locals. Boss **Gorm, the Pit Master** (chain flail + whip, summons guards, burns the fence in phase 2). *← slice*
2. Provincial Pit — barricades, braziers, spike traps, 3 gates. Boss: Provincial Champion.
3. Stone Amphitheater — pillars block line of sight, elites, assassins. Boss: Beast Lord.
4. Grand Arena — timed gates, fire jets, blades. Boss: Gilded Patron.
5. Imperial Arena — formations, imperial events. Boss: Emperor's Champion.
6. Colosseum — hypogeum trapdoors, factions, myth beasts. Boss: Eternal Victor.
Every arena needs **vertical variety** (mounds, ramps, stairs, platforms, pit edges) so jump/slide matter in a bounded space.

## Enemies
Tier 1 (slice): Starving Convict (swarmer), Pit Cur (fast), Shield Thug (frontal block), Slinger (ranged), Brute (big, slow), + one assassin/area-denial type. Elite = any enemy + one affix (slice: 3 of Armored, Fast, Explosive, Cursed, Gilded, Berserk).

## Build systems
- 4 weapon slots (lv 1–8), 6 Boon slots (lv 1–5). Rarity Common/Uncommon/Rare/Legendary, weighted by Luck.
- Weapons are **patterns** composed from data: MeleeArc, Thrust, MultiHit, GroundSlam, Projectile, Returning, Orbit, Chain, Summon.
- Synergy through **tags + status effects + events** (Burn, Shock, Bleed, Curse, Slow, Root; OnHit/OnKill/OnCrit/OnDamaged/OnLevelUp/OnJump/OnSlide/OnPickup). Example: "Burning enemies take +X % lightning damage".
  - Slice statuses: **Burn** (gold, refreshes, damage over time), **Bleed** (crimson, stacks to 10), **Shock** (cyan, +20 % damage taken), **Slow** (data only until an enemy or Boon uses it). Curse and Root come with the enemies that need them.
  - Slice synergy webs (M2.6): **Fire** — Kindling (chance on hit to Burn) → Pyre (burning enemies burst and spread fire). **Storm** — Static Charge (every 6th hit chains a Shocking spark) → Conductive Oil (sparks +50 % to burning enemies and ignite them). **Blood** — Serrated Edge (crits Bleed) / Blood Slide (slides leave a bleeding trail) → Open Wounds (+4 % damage per Bleed stack). Synergy Boons are only offered once a source is owned.
- Stats: flat → additive % → multiplicative layers.

## Meta
Renown per run gates arena tiers and cosmetics. Unlocks are **content, never flat stats**: gladiators, weapons entering the pool, arenas, modifiers, cosmetics. Conditions are pluggable (defeat boss, reach arena, survive T, kills with weapon, win with gladiator, lifetime gold, secret discovery, Renown purchase). A Codex shows locked gladiators as silhouettes with cryptic hints.
**Slice (built):** versioned save, Renown per run (time, kills, boss, victory, arena depth), Codex screen (records, unlocks, gladiator silhouettes), and one unlock: beating Gorm adds **Gorm's Flail** (orbiting spiked balls) to the level-up pool.

## Gladiators
Starter **The Nobody** (rusty gladius + scrap shield; *Underdog*: bonus damage/XP when outnumbered). After the slice, in order: Pit Goblin, Blood Knight, then the rest (Murder Machine, Beastmaster, Thunder Champion, Ashen Oracle, Golden Idol, Net-Weaver, Asterion).

## Crowd & Favor
Slice (built): `CrowdExcitement` 0–1 from kills, elite kills, near-death and boss beats → crowd shader, crowd loop volume/pitch, cheers on spikes; the announcer calls kill streaks (KILLING SPREE! → MASSACRE! → BLOODBATH! → UNSTOPPABLE!), close calls and the crowd's roar. **Favor (built, Phase 5.1, `docs/FAVOR.md`):** a slower meter fed by announced streaks, elite kills, variety, style kills (sliding/airborne), close calls, no-hit stretches and the boss's beats; full → the crowd throws a gift (healing amphora, gold shower, laurel of XP, a weapon in the sand for 30 s) and the next threshold is ×1.25. A dull show (few kills for the horde's size, nobody near, for a while) bores the crowd: the Arena Master opens the gate nearest the player or sends a champion with a 20 s bounty.

## Open questions
| # | Question | Status |
|---|---|---|
| Q1 | Run structure: one arena per run, or a chain of arenas? | Open — `RunDef` is a list of `ArenaDef`s, so both are data. Decide after Gate 2. |
| Q2 | Slave Pit floor size vs. enemy count (see CONCERNS C1) | Built at 42 m with a 300 cap (perf holds at 241 fps). Judge readability at Gate 2; the cap is `AL_SlavePit.maxAlive`. |
| Q3 | Sprint: separate button, or always-run + slide as the speed tool? | Kept: always-run, slide/jump give the speed (Gate 1 asked for no feel changes). |
