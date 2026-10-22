# Backlog

Good ideas that are **not** in the current phase. Pull from here only when a phase calls for it.

## Tooling
- Optional: `unity mcp configure claude-code` so future Code sessions get native Unity MCP tools.

## Gameplay
- **Assassin (dash) enemy** — moved out of the slice for the Fire-Pot Thrower (DECISIONS M2.2); Arena 2–3 roster.
- **PlaytestBot dodging** — jump when inside a slam/landing telegraph or a fire patch, so mortal bot runs measure fairer difficulty. Not needed for perf runs (the bot is immortal there).
- **Enemies hurt by fire patches / explosions from the player's own fire upgrades** — natural Boon synergy (M2.6 candidate).
- **Affixes Cursed / Gilded / Berserk** — Gilded after M2.7 gives gold a purpose.
- **Card tooltips with the current total** ("Damage 134 % → 151 %") — needs a stat sheet; pause menu in M3.5.
- **Weighted offers that favour owned upgrades** (Vampire-Survivors style) — only if picks feel too random at Gate 2.
- **More effect kinds** — on-jump / on-land (Thunderfall shock ring), on-pickup, on-damaged (thorns), Curse and Root statuses with the Arena 2–3 enemies that use them.
- **Status icons over enemies** (tiny pips above the head) if the puffs + coloured numbers don't read at Gate 2.
- **Status effects on the player** (enemy Burn/Slow/Root) — fire patches are their own system today.

## Performance
- **Attribute the first level-up's one-time ~7.5 KB allocation** (UGUI/TMP first use while paused; a hidden pre-draw of the card panel didn't remove it). A Profiler capture that includes that exact frame (`-rawProfile` near the first level-up, analyser frame window) should name it.
- **Instanced enemy rendering** (C5) only if an arena needs 1,000+ on screen.

## Meta progression (after the slice)
- **Renown purchases, the rest** — Phase 5.6 sells Boons in the Codex Market; run modifiers and cosmetics bought with Renown are still to come.
- **More secrets** — Phase 5.6 adds the Golden Rat and the Bookmaker; next: one per new arena, the Ashen Oracle's encounter, a Codex 3D model viewer.
- **Unlock banner beat** — a dedicated "UNLOCKED!" moment (crowd roar, reveal of the weapon model) instead of one gold line on the end screen.
- **Per-gladiator records** (best time / wins per gladiator) once there's more than one.

## Gladiators (from Phase 5.2)
- **Menu diorama shows the picked gladiator** (the hero model behind the select screen is always the Nobody).
- **The escaping goblin from the crate wears the player's cyan marks** (same model as the playable goblin); a rag-only variant would read better.
- **Break the rattling crate with weapon hits too** (needs weapons to test props, not only enemies).
- **Goblin personality beats from the spec:** a cackle on level-up, ears flopping when it runs (separate ear parts), grabbing gems with both hands.
- ~~The Blood Knight's real unlock~~ — done in Phase 5.9 (`ReachArena 2`).
- **The Murder Machine's cleaver resting on his shoulder between chops** (needs a rest-pose offset for the weapon arm in `PartAnimProfile`); today it's held low in front.
- **Weapon-specific level text:** the Storm Spear's "+1 Pierce" levels mean "+1 jump"; `CardText` could name a stat per pattern.
- **Storm Spear and Executioner's Cleaver evolutions** (catalyst + chest), e.g. the chop leaves a fissure that Roots, the chain forks.
- **The Thunder Champion's crest flickering at high Charge** (spec "Personality"): an emission pulse on one part needs a per-part material, so it waits for a VFX pass.
- **Knight feedback to judge in the playtest:** a faint crimson pulse on kill heals (nothing yet: 300 kills a minute would be noise), a heartbeat under 20 % HP, the blade resting on the shoulder between swings.
- **The Champion's second gladius** (BOSSES.md point 9): the crowd throws it to him on his phase-2 roar; a prop thrown from the stands and a dual-wield pose. The gameplay change (faster attacks, five-thrust flurry) is in.
- **The Grand Arena's extras:** a secret (it's the only arena without one), the patrons' special events ("a patron pays for a gold shower", "the gates open on the hour"), a city skyline beyond the stands, the gold threads of the payroll drawn in the air rather than on the ground, its own SFX for the jets, the blades and the blink (they reuse the fire-pot, crit and spark sounds).
- **Weapon on-hit status for projectiles** (`WeaponDef.onHitStatus` only works for MeleeArc/Thrust): a net that roots needs it on thrown weapons.

## Art / audio
- **Upgrade icons** — cards and the HUD build strip show 2-letter glyphs (`UpgradeDef.glyph`); procedural pixel icons per Boon/weapon in the Phase 4 juice pass.
- **Ability-ready pip on the HUD** (Phase 5.14) — the slide abilities (Lightning Dash, Make It Rain, Cast Net) have cooldowns the player can't see; a small pip by the HP bar would help. None of them has one yet, so the Net-Weaver doesn't either.
- **A dedicated cast-net prop** (Phase 5.14) — the Cast Net draws the thrown Net's mesh scaled ×5; a denser 3 m net with more cords would read better on the sand.
- **More companions** (Phase 5.15) — the GDD's boar and hawk for the Beastmaster (a second `Summon` weapon each: a boar that charges, a hawk that dives), and a companion that can be hurt and revived if the pack ever feels too safe.
- **Freed beasts that bolt** (Phase 5.15) — a freed cur vanishes in a white burst; running off through a gate would sell the "freed" better.


## From the R7/R8/R6 spec (2026-10-22, `docs/tracks/R7-design.md` §7)
- Edict ranks (Heat levels); *Night Games* (fog); *The Purist* (one weapon slot); edicts as Favor whims.
- Typed seeds in the menu; online leaderboards or a shared daily ledger (needs a server); daily streaks.
- Weapon skins, accessories (crests, plumes, capes), titles and announcer callouts, victory poses, skin portraits in the Codex's Gladiators tab.

## From the U1 spec (2026-10-22, `docs/tracks/U1-design.md` §11)
- Frames that follow the arena's palette family ("frames that climb the ladder"); per-arena UI palettes.
- Device-aware button prompts (gamepad glyphs for Xbox / PlayStation / Steam Deck); today's keyboard letters stay.
- A CREDITS page in the game (the licences ship in the build and in `docs/CREDITS.md` now); a logo (Cinzel Decorative or drawn).
- UI sounds (R4 / R5 audio), animated transitions between menu screens, cursor art, localisation.
- Restyling the admin panel, threat chevrons, the hurt overlay, world-space telegraphs.
- A 2× laurel branch for big words (VICTORY! at Shout 132 draws the 20×24 branch at 2.75×, chunky), from U1-A's art review.
