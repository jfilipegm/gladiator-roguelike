# T2 — The horde learns new attacks as you grow (note 39b, draft for the producer's OK)

The producer on v0.28.0: "the mobs should get stronger with the player, more HP, more damage, more speed, **even unlock
more attacks** to make it harder." v0.29.0 does the numbers (`Threat`). This item does the attacks, at the same tiers
the announcer already calls ("THE HORDE GROWS STRONGER!" at levels 12, 22, 32).

## The idea
Each tier teaches the horde one new trick per enemy family, every one telegraphed in the orange-red language so a good
player can still read and dodge it. Data, not code per enemy: a short list of shared behaviours, switched on per
`EnemyDef` with the tier it unlocks at.

| Tier (level) | Melee (Convict, Brute, Murmillo…) | Fast (Cur, Assassin…) | Ranged (Slinger, Javelineer…) | Elites |
|---|---|---|---|---|
| I (12) | **Lunge:** a short telegraphed leap at you from ~3 m | **Pack call:** a howl that speeds up nearby curs for 2 s | **Aimed shot:** they lead your movement | one more affix pool entry (Fast) |
| II (22) | **Shield bash / slam:** Brutes and Murmillos gain a small ground slam | **Feint dash:** a dash that stops short, then strikes | **Fan:** 3 projectiles in a cone | elites come with **two** affixes |
| III (32) | **Last stand:** at low HP a frenzy (faster, stronger), one at a time per pack | **Ambush:** some arrive from behind through the gates | **Rain:** a volley that lands on circles under you | an elite every 60 s instead of 90 |

## Constraints
- Zero GC, central tick, the spatial hash: the new behaviours live in `EnemyManager` partials like the existing ones
  (`EnemyManager.Breath.cs`, `.Leash.cs`).
- Readability with 300 enemies: at most a capped number of telegraphs at once (e.g. 12 lunges at a time); everything
  telegraphed ≥ 0.4 s.
- Bot runs before/after to keep a full run survivable for a good build: the target is "you have to keep moving", not
  "you can't win".

## Open points for the producer
1. Does the table feel right, or would you rather pick tricks per enemy?
2. Should a tier also be visible on the HUD (a small "THREAT II" plate)?
