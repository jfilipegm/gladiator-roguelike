# Notes 29–36: the producer's playtest of v0.28.0 (2026-10-24)

Branch `phase5-notes28` → v0.29.0 (the quick fixes). The upgrade-system change (notes 31 + the answers below) is its own
item after this one: `docs/tracks/U2-items-satchels.md`.

| # | Note (the producer) | Plan | Owner |
|---|---|---|---|
| 29 | Remove "BOSS" and the boss's name under it: the boss's name in BOSS's lettering, the same size; his HP bar shows his total HP. | `Hud.ShowBossName`: the timer label (Luckiest Guy, 64) reads BOSS, then his `displayName`; the second line is gone; the bar prints "hp / max". | orchestrator (done, d221e79) |
| 30 | The Armory Chest looks like a pirate chest. | A lineup of 4 Roman chests for the producer to pick (`BlenderSrc/props/chest_variants.py`); the pick goes to `Assets/Art/Models/A1/Props/PR_ArmoryChest.fbx`. | W-chest + orchestrator: the producer took all four, one per arena (ChestSetup: Slave Pit B, Provincial C, Amphitheater A, Grand D, Imperial C, Colosseum A; `ArenaLayoutDef.chestModel`); ArenaChestTests 1/1, checked in Play |
| 31 | Rework the upgrade sources: chests give weapons and items (a new category), level-ups give upgrades only, bosses drop Rare/Epic/Legendary weapons or items; add Epic. | Its own item: `U2-items-satchels.md`. | orchestrator (spec) |
| 32 | Blood Slide is just pink circles: make it a blood trail. | The slide trail drawn as blood on the sand (smears along the slide, drying dark, fading), not telegraph rings. | W-trail: done, tested (BloodTrailTests 4/4, StatusTests 8/8), checked in Play |
| 33 | The Ashen Oracle's attack looks like the Pit Goblin's (the same lob): a cursed purple fireball instead. | `WP_AshenCenser` → a purple cursed fireball, its own look. | W-weapons: done, tested (AshenOracleTests 7/7), checked in Play |
| 34 | The Net-Weaver's trident looks no different from the javelin; the net throw doesn't really work. | The trident gets its own look and attack; the net throw is fixed so it clearly casts, lands and holds. | W-weapons: done, tested (NetWeaverTests 9/9), checked in Play; his held trident still shows during the lunge (needs a re-export with a separate Trident part) |
| 35 | The Thunder Champion's blink is sudden (looked like a bug): an icon showing it's ready, and a smoother blink. | A ready icon in the HUD; the blink with a wind-up/trail/arrival so it reads as a move. | W-blink: done, tested (ThunderBlinkTests 4/4), checked in Play |
| 36 | The NEW seal stays after the word. | No change. | — |
| 37 | The Beastmaster: the dogs get stuck fighting a couple of mobs while he has no weapon; rethink it. | He also starts with a **Hunting Spear** (a quick thrust that marks what it hits; the pack goes for marked enemies first); the pack is leashed to him, picks enemies near **him**, bites and moves on, follows when he runs. | W-beast: done, tested (BeastmasterTests 10/10), checked in Play |
| 38 | "The boss fights are too easy: when the boss spawns don't remove the mobs nor their spawning; a tiny bit slower spawn." | The horde stays and keeps coming during the fight: `Director.BossPhase` (steady spawns ×0.75, alive ×0.85, no swarms or elites: the boss brings his own); `BossFight.clearHorde` keeps the old entrance for the boss tests only. | orchestrator: done, tested (BossTests +1) |
| 39 | "You can get super strong with some builds: the mobs should get stronger with the player, more HP, more damage, more speed, even unlock more attacks." | **(a, v0.29.0)** `Threat`: by the player's level, enemy HP +6 %/level past 5, damage to the player +2 %/level past 10, enemy speed +0.6 %/level past 10 (cap +15 %); "THE HORDE GROWS STRONGER!" at levels 12, 22, 32. **(b, its own item)** new enemy attacks unlocked at those tiers: `docs/tracks/T2-threat-attacks.md`. | orchestrator: (a) done, tested (ThreatTests 3/3); (b) spec |
| 40 | "Elites shouldn't be a normal mob with a ring under it: a smaller boss, named, a different character, bigger than every mob but the bosses, with an HP bar; spawned by requirements (level) and/or by things you unlock on the map (doors, cages), for item rewards." | Its own item: `docs/tracks/E1-named-elites.md` (after U2, whose items they drop). | orchestrator (spec) |

**The producer's answers on note 31 (2026-10-24):**
1. Items stack (a second copy strengthens the first).
2. Slots are earned: a **Satchel** item, in every rarity; each rarity adds a number of new slots to weapons, boons or items.
3. Mobs drop gold more often (so the first chest comes early), instead of a cheap first chest.
4. The NEW seal stays after the word.
