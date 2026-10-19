# Favor — the crowd as a gameplay system (Phase 5, milestone 5.1)

CLAUDE.md §6.9: the crowd rewards a good show with gifts and punishes a dull one with the Arena Master's whims. The slice already has `CrowdExcitement`, which stays as the crowd's **mood**: a 0–1 value that decays in seconds and drives the crowd shader, the crowd loop and the cheers. **Favor** is new and slower: a meter that fills over a run and pays out.

## Favor meter
- **The bar:** 0 → 100. When it fills, the crowd throws a gift, the bar empties, and the next threshold is ×1.25 higher. So gifts come regularly but don't snowball.
- **On the HUD:** a thin gold bar with a laurel, under the XP bar, labelled FAVOR. It pulses when it's close to full.

| Source | Favor (start values, all in `FavorConfig`) |
|---|---|
| Announcer streak rung (40 / 80 / 140 / 220 kills in 4 s) | +8 / +12 / +16 / +20 |
| Elite kill | +6 |
| **Variety:** killing a type you haven't killed in the last 20 s | +1.5 per type |
| **Style:** a kill while sliding or airborne | +0.25 each, at most +3/s |
| **Close call:** below 15 % HP, then back above 40 % (the announcer's STILL STANDING!) | +15 |
| **No-hit stretch:** every 20 s without taking damage | +5, then +7, then +10 (the chain resets on a hit) |
| Gorm enters / Gorm reaches phase 2 | +10 / +10 |

The style row is my addition: movement is a power source (§5), so the crowd should love it.

## Gifts
- **The throw:** a gift arcs out of the stands over ~1.2 s and lands 3–6 m from the player, on open walkable ground. While it's in the air, a soft gold marker shows where it will land. It's gold rather than orange-red, because it's good news.
- **On the ground:** gifts are pooled pickups, at most 3 at once, and each lasts 25 s. The announcer says "A GIFT FROM THE CROWD!", the crowd cheers, and excitement spikes.

| Gift | Effect | Weight |
|---|---|---|
| **Healing Amphora** | Heals 30 % of max HP. | 1, or ×3 below 50 % HP |
| **Gold Shower** | 20 coins burst out in a ring. | 1 |
| **Weapon in the Sand** | A random pool weapon you don't own, at level 4, for 30 s. It doesn't take a slot and shows on the HUD with a countdown. | 0.8 |
| **Laurel** | XP worth 40 % of the current level. | 0.6 |

## Whims (when the show gets dull)
- **Boredom:** a hidden 0 → 100 meter.
  - **Rises:** while the last 10 s had fewer kills than 25 % of the Director's current target count, and no enemy is within 6 m.
  - **Falls:** fast while you're fighting.
- **Never:** in the first 90 s, during the boss, or within 60 s of the last whim.
- **At 100:** "THE CROWD GROWS RESTLESS!" sounds with a boo. Then one whim fires, chosen at random:
  - **Gates thrown open:** a swarm (the Director's current swarm size) pours in from the gate nearest the player.
  - **A champion's challenge:** an elite with a gold skull marker spawns 12 m away. Kill it within 20 s for +15 Favor and 15 gold. If you fail, the bounty is gone.
- **Purpose:** whims make passive kiting a choice with a price, not a strategy. They add pressure; they never take power away.

## Data and code
- **`FavorConfig`** (ScriptableObject): every number above. Gifts are a list of `{kind, weight, amount, duration}`; whims are a list too. Each `ArenaDef` points at its own `FavorConfig`, so Arena 2 can be greedier or kinder without code.
- **`Favor`** (Gameplay/Crowd): its own tick group.
  - It listens to `OnKill`, `OnDamaged`, `OnAnnounce`, `OnJump`, `OnSlide` and the boss events.
  - It raises a new `GameEvents.OnFavor` (gift thrown, whim fired).
- **`CrowdGifts`:** the pooled gift pickups, their landing markers and their effects.
- **`TemporaryWeapon`:** the "in the sand" weapon. It uses the existing `WeaponSystem`, just without taking a slot.
- **`ArenaMaster`:** boredom and the whims. It asks the `Director` for a gate swarm and spawns the elite.
- **New sounds** synthesised in `SfxSynth`: Boo, GiftThrow, GiftLand.
- **The PlaytestBot** treats gifts as pickups worth walking to.

## Tests
- **EditMode:**
  - Favor maths: each source, threshold growth, the no-hit chain resetting on a hit.
  - Gift weights, including the low-HP amphora bias.
  - The boredom rules: never during the boss, in the first 90 s, or inside the cooldown.
- **PlayMode:**
  - A full bar throws a gift that lands on walkable ground in the Slave Pit.
  - An amphora heals.
  - The temporary weapon hits, then disappears.
  - A player standing still in a corner with enemies kept away triggers a whim. Fighting doesn't.
  - Gifts and whims never allocate per frame.
- **Bot run:** gifts per run (target 4–7 in 8 minutes for the bot's build) and whims per run (0–2).

## Open for the playtest
- Are gifts frequent enough to feel like the crowd is watching?
- Does the boredom rule ever fire when you're actually fighting?
- Is the temporary weapon readable as temporary?
