The producer's playtest list, fixed before more Phase 5 work. Stacked on #6 (`phase5-arena2`): merge #6 first. Release notes: the "Fixes from your playtest" section of `docs/releases/v0.5.0.md` (v0.5.0 hasn't shipped, so one release can carry both). Decisions: DECISIONS "Playtest fixes"; new concern C15.

## The list, and what changed
1. **"The main sound sounds like a choo-choo train."**
   - The old crowd loop was low-passed noise pulsed by two fixed sine rhythms in a 4 s loop; its energy sat at about 6 kHz (hiss) and pulsed once a second.
   - New `CrowdSynth`:
     - dozens of formant-filtered voices talking in phrases, each on its own timing;
     - claps, whistles and a small reverb, in stereo;
     - a 16 s murmur loop and a 12 s roar loop, plus new cheer and boo sounds.
   - Measured: no fixed beat, energy centred at about 1 kHz.
2. **"Pit Goblin unlocked without the crate, and I can't use him."** Two bugs:
   - My Editor play sessions wrote to the same save file as the built game (on Linux they share `persistentDataPath`), and one of them set the crate flag. Editor sessions now use `save_editor.json`. The producer's save was cleaned, with a backup next to it.
   - The gladiator and arena cards ignored the mouse (`UiFactory.Image` turns raycasts off), so only the keyboard could pick another gladiator.
   - The menu diorama now shows the picked gladiator.
3. **Animated 8-bit upgrade icons with a rarity aura.**
   - `PixelSrc/upgrade_icons.py` draws all 27 icons as 16×16 text art into one point-filtered atlas: an outline, three shine frames and four aura-mask frames.
   - `IconSetup` points each `UpgradeDef` at its row.
   - Cards show the icon bobbing and shining over an aura tinted grey, green, blue or gold by rarity.
4. **Icons instead of letters in the HUD slots.** The weapon and Boon strip and the crowd's loan slot use the same view: a rare shine and a still aura.
5. **The Slave Pit: twice as wide, a rough enclosure, people work and train there.**
   - The pit is now an 84 × 70 m oval stockade on open ground, built from 48 boundary sections:
     - log palisade with 3 log gates;
     - patched fences;
     - dirt embankments with wattle on top;
     - onlooker platforms.
   - The yard has a drill yard, a sparring ring, a work corner, a woodpile, a shed and cages.
   - Invisible walls that only the player collides with keep you in, whatever your air jumps.
   - The arena's shape is now data (`ArenaLayoutDef.aspect`): every system that used a radius uses the ellipse. That covers enemies, the spawn ring, pickups, chests, Favor gifts and whims, the boss, the admin spawner and Gorm's fire ring.
6. **Not floating in the air:**
   - rolling land into fogged hills;
   - ~150 trees, bushes and rocks;
   - huts, longhouses, tents, watchtowers, and carts on the roads.
   - It is all decor without colliders, about 38k static tris for the whole arena.
7. **Crowd ambience that reacts:**
   - A multi-kill (6, 14 or 28 kills inside about 0.8 s) swells the crowd with a cheer.
   - After 3.5 s without a kill or hit, it cools off below its fighting idle, quieter and slower (pitch 0.88).

## Verification
VERIFICATION

## Not done / for the playtest
- How any of it sounds or feels is the producer's call. I can't listen: the crowd was checked by measurement, and a 24 s preview MP3 was sent.
- C15: the same spawn curve in about 3.5× the floor area may feel thin early on.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
