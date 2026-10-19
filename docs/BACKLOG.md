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
- **Renown purchases** — an `UnlockCondition` kind "costs N Renown" bought in the Codex (only some unlocks, CLAUDE.md §6.6); arena tiers and cosmetics gated by Renown.
- **Codex discovery** — seen-enemy / seen-boss entries, "NEW" markers on fresh unlocks, secrets found in the arenas (the rattling crate → Pit Goblin).
- **Unlock banner beat** — a dedicated "UNLOCKED!" moment (crowd roar, reveal of the weapon model) instead of one gold line on the end screen.
- **Per-gladiator records** (best time / wins per gladiator) once there's more than one.

## Art / audio
- **Upgrade icons** — cards and the HUD build strip show 2-letter glyphs (`UpgradeDef.glyph`); procedural pixel icons per Boon/weapon in the Phase 4 juice pass.
