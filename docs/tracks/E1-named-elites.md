# E1 — Named elites: mini-bosses you let out (note 40, draft for the producer's OK)

The producer on v0.28.0: "elites shouldn't be a normal mob with a ring under it, it should be a smaller boss … they
should spawn after some requirements are met like the user is at least level x and/or the user triggers stuff to make
them spawn (to get item rewards from them) … doors, cages, stuff like that that you would unlock … elites should have a
name, be a different character, bigger than every other mob except the real bosses, and have HP bars too."

## What an elite is now
- **A named character**, its own A1 model: bigger than any mob (about 1.6–2× a Convict), smaller than the bosses.
  - Examples: **Brann the Bonebreaker** (Slave Pit), **Vesta of the Nets** (Provincial Pit).
- **Its own HP bar** with its name: a smaller version of the boss bar, under the boss bar's place, while it lives.
- **One or two signature attacks, telegraphed.**
  - Built from the boss attack kinds the game already has (sweep, charge, pounce, volley, bomb, whip), so most of it
    is data (a `BossDef`-like `EliteDef`).
  - A lighter controller that allows more than one at a time.
- **A reward:** it drops gear (U2): one Uncommon-or-better weapon, item or satchel, shown as a glowing drop. Elites are
  how you farm gear between chests.

## How they come out
- **Map triggers:** each arena gets places that hold an elite, and **you choose when to let it out**.
  - Each arena's own features: a barred door in the Slave Pit's wall, a cage in the Stone Amphitheater (its beast
    cages already exist), a patron's box in the Grand Arena, a hypogeum lift in the Colosseum.
  - **Each has a level requirement** ("Lv 8"). Below it, it's locked and shows the level. At or above it, hold the
    interact key for about 1.5 s to break it open.
  - A short camera beat and the announcer names the elite.
- **The Arena Master can release one:** if you kite passively for too long, a whim (the Favor system's existing
  pressure tool) opens a door for you, so elites still show up for players who never open anything.
- **The old ring elites go away:** no more random mob with a coloured ring and a bigger HP pool on a 90 s timer. The
  affixes (Armored, Fast, Explosive…) can return later as elite variants or T2 tricks.

## Scope (first pass)
- **One named elite per arena: 6 elites, 6 models.**
  - A lineup for the producer to approve first, as with the cast.
  - Then two or three trigger spots per arena that share that arena's elite.
  - More elites per arena later, as data.
- Code:
  - `EliteDef` and `EliteController`, using the boss attack code;
  - elite spots in `ArenaLayoutDef`, an `EliteGate` interactable (lock, level, hold to open);
  - the elite HP bar in the HUD;
  - the gear drop (needs U2's items);
  - the whim hook;
  - the Director stops making ring elites.
- Tests: the gate's level lock, the release, the bar, the drop, two elites at once, the whim release.

## Order (orchestrator's recommendation)
1. **v0.29.0** (PR #37): notes 29–39a, as is.
2. **U2, items and satchels:** the gear the elites drop.
3. **E1, named elites:** the elite models start in Blender during U2, since they're independent art work.
4. **T2, new attacks by threat tier.**

## Open points for the producer
1. One elite per arena to start, or two?
2. Should the old ring elites go away completely (recommended), or stay as unnamed tougher mobs without the ring?
3. Opening a gate: hold a key (free, level-gated), or also cost gold like a chest?
