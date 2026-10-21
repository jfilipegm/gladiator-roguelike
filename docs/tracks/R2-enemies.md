# R2 enemies — notes (the Skeletal Legionnaire's bone piles, the Chimera's breath, the boss's HP floor)

Branch `claude/r2-enemies-74db63` (fast-forward of `origin/phase5-colosseum` at 2c22c20: already up to date).
Code only, no Unity: `Tools/ccheck` errors=0, 0 warnings; **no test has been run** (the orchestrator runs them).

**Owns:** `EnemyDef.cs`; `EnemyManager.cs` (the hooks); `EnemyManager.Undead.cs`, `EnemyManager.Breath.cs` (new);
`Assets/Tests/EditMode/BreathConeTests.cs`, `Assets/Tests/PlayMode/UndeadTests.cs`, `Assets/Tests/PlayMode/ChimeraTests.cs`
(new); this file. `EnemyManager.Leash.cs` untouched. No assets, no `.meta` files (Unity makes them on import).

## What it does (docs/ARENAS.md §6, docs/tracks/R2-design.md § "Enemies")

### The dead: bone piles (`EnemyManager.Undead.cs`)
- **Collapse.** In `Kill`, an enemy with a rise left (`def.reviveCount > TimesRevived(i)`) collapses instead of dying,
  unless (a) it dies from a fall (`info.SourceId == FallSourceId`: `PilesDenied++`), (b) it's burning and
  `burnStopsRevive` (`PilesBurned++`), (c) 48 piles are down already (`PilesOverflow++`), or (d) `KillAll` (the run's
  end). A collapse frees the slot exactly like a death (`AliveCount`, `AliveOf(def)` down; gold carried, a formation slot
  or escorts handled as for a death) but raises **no `OnKill`** (no XP, no gold, no streak, no kill count), no kill pop,
  shake, hit-stop or Explosive blast: a bone-coloured burst and a soft `Splinter` (0.45) instead. `PilesMade++`.
- **The pile** (a struct in a 48-entry list, not an enemy slot: weapons don't aim at it, the Director doesn't count it):
  position, yaw, def, affix, scale (affix × def), `T = reviveDelay`, the uid, `Revived = TimesRevived + 1`.
  - Drawn with `Graphics.RenderMeshInstanced` per distinct `def.pileMesh` (the enemies' `bodyMaterial`, the palette
    material with instancing on; no shadows), a 0.15 s ease-out scale-in, a shake and a 15 % vertical gather in its last
    0.5 s. Also drawn on paused frames (a level-up), so piles don't blink out under the cards.
  - A bone-coloured disc of radius `stompRadius × scale` fills over `reviveDelay` (`TelegraphRenderer.Circle`, colour
    (0.89, 0.84, 0.74, 0.8)). It shows both "when it rises" and "where to step".
- **Stomp.** The player's XZ within `stompRadius × scale` and feet within ±`stompMaxHeight` (0.5 m) of the pile: it
  scatters. That's the kill: `OnKill` at the pile, `SourceId = StompSourceId`, `Xp = def.xp × affix.xpMultiplier`,
  `GoldChance = def.goldChance`, `Elite` for an elite pile; a bone burst and `Splinter`. `PilesStomped++`.
- **Rise.** At `T ≤ 0`: `Spawn(def, pos, affix)` (no escorts: `_rising`), then max HP and HP × `reviveHpMul`,
  `TimesRevived = Revived`, a bone burst. `PilesRisen++`. It faces the player (Spawn). Elites keep their affix (Q7).
  If Spawn has no free slot (never at the arena caps) it retries next frame.
- **Falls.** `public int DropPiles(Vector2 centre, float yawDeg, Vector2 size)`: piles inside the rectangle (local X =
  (cos, −sin), the contract's convention) fall: each is a kill credited to `FallSourceId`, `PilesDenied++`.
  **Nobody calls it yet** (see Needs).
- `DespawnAll` and `KillAll` clear the piles.
- **The kill event with no enemy behind it** (stomp, a pile's fall) uses `Index` = a free slot with its statuses cleared
  (`EventSlot()`): `BoonEffects.OnKill` reads `HasStatus(e.Index, …)` (bursts on death), so -1 would throw and a reused
  slot could trigger someone else's burst.

### The Breather (`EnemyManager.Breath.cs`, `EnemyBehaviour.Breather` appended after `Caster`)
- **Move:** straight at you. **Trigger:** cooldown ready, `dist < attackRange`, `|dy| < 1`, **and you're within
  `breathAngle / 2 + breathFacingSlack` (30 + 10°) of its facing** (see Decisions). It plants (velocity zeroed).
- **Wind-up** (`windup × affix time`): `_aim = dirTo`, locked; `TelegraphRenderer.Sector(p, aim, attackRadius × scale,
  breathAngle, fill)` in the telegraph orange-red; a wind-up threat (edge arrows, bot). It turns into its aim at its
  turn rate (the facing rule in `Tick` now treats a Breather's wind-up like a Dasher's).
- **Breath** (`breathTime`): the sector at full, wind-up threat still on, fire bursts at ¼, ½, ¾ of the cone every
  `breathFxEvery` (0.15 s), `SfxId.FirePot` as it starts.
  - **The player**, once per breath (`_attackHit`): `InCone(p, aim, len, angle, player, playerRadius)` and feet within
    ±`breathJumpClear` (1.2 m) → `attackDamage`, `DamageFlags.Area`, impulse `aim × breathKnockback + up × breathKnockUp`
    (2.5). `BreathHits++` (only when `TakeDamage` lands; i-frames retry later in the same breath, like the lunge).
  - **Other enemies**, once per breath (the War Boar's trample serials `_charge` / `_trampled`): `QueryAlive(p, len)`,
    body circle in the cone, height within 1.5 m, not itself, **not its own def**, not `Scripted` →
    `attackDamage × trampleMul`, `Area | Proc`, `SourceId = BreathSourceId`, knockback `breathKnockback`.
    `BreathEnemyHits++`.
- **At its end:** `breathPatches` `HazardZones.Add` at (k + 1) / (n + 1) of the cone's length (clamped inside the arena,
  ground height), radius `breathPatchRadius`, `zoneDuration`, `zoneDps` (they hurt the player only, as every
  `HazardZones` patch); cooldown `attackCooldown × affix time` starts; **Recover** (`recover`, still: the window).
- **Pose** (front legs = `ArmL`/`ArmR`, the War Boar's rig): wind-up head drawn back (−25 × k, with the wind-up shake),
  body low, front legs braced; breath head thrust forward (+20); recovery head hanging (+12 × k, easing out).
- `public static bool InCone(Vector3 origin, Vector3 aim, float length, float angleDeg, Vector3 p, float pad)`: XZ only;
  a pad widens the reach by `pad` and the angle by `asin(pad / d)` (exact for a circle touching the wedge); a circle
  touching the mouth counts.

### For the boss (`EnemyManager.cs`)
- `public void SetHpFloor(int i, float hp)` (0 = off; Spawn resets it), `public float GetHpFloor(int i)`.
  `ApplyDamage` clamps HP up to the floor after subtracting (so hits, status ticks, blasts, traps never kill through
  it). `Execute` with a floor: the hit event shows `hp − floor` (none if 0), HP drops to the floor, returns **false**.
- `public void SetHp(int i, float hp)`: clamped to [0.01, max HP], no events, alive slots only. It doesn't raise max HP.

### Source ids (static, `GameInfo.StableId`)
`EnemyManager.FallSourceId` ("hypogeum"), `StompSourceId` ("stomp"), `BreathSourceId` ("breath"), next to the existing
`TrampleSourceId`.

### Counters (public, for the bot log)
`PilesMade`, `PilesRisen`, `PilesStomped`, `PilesBurned`, `PilesDenied`, `PilesOverflow`, `PileCount`; `Breaths`,
`BreathHits`, `BreathEnemyHits`. Accessors: `GetPilePosition(k)`, `GetPileTime(k)`, `GetPileDef(k)`,
`TimesRevived(i)`, `GetHpFloor(i)`. The design's log line `piles P (risen, stomped, burned) breaths (hit you)` maps to
`PilesMade (PilesRisen, PilesStomped, PilesBurned) Breaths (BreathHits)`.

### Tunables (on the `EnemyManager` component; defaults apply to every scene without a rebuild)
`pileRingColor`, `boneColorA/B`, `pileScaleIn` 0.15, `pileShakeTime` 0.5, `stompMaxHeight` 0.5; `breathFacingSlack` 10,
`breathJumpClear` 1.2, `breathKnockUp` 2.5, `breathFxEvery` 0.15, `breathColorA/B`. On the `EnemyDef`: the fields below.

| `EnemyDef` field | Default | Legionnaire | Chimera |
|---|---|---|---|
| `reviveCount` | 0 | 1 | — |
| `reviveDelay` | 2.5 | 2.5 | — |
| `reviveHpMul` | 0.5 | 0.5 | — |
| `stompRadius` | 0.9 | 0.9 | — |
| `burnStopsRevive` | true | true | — |
| `pileMesh` | null | `PR_BonePile`'s mesh | — |
| `breathAngle` | 60 | — | 60 |
| `breathTime` | 1 | — | 1 |
| `breathPatches` | 3 | — | 3 |
| `breathPatchRadius` | 1.4 | — | 1.4 |
| `breathKnockback` | 4 | — | 4 |

## Decisions (one-line reasons)
1. **The pile's disc uses `Style.Telegraph` (a filling disc), not `Style.Ring`.** The shader's ring style only pulses
   (a fill above 0 just speeds the pulse), so it can't show the design's "fills as it reassembles"; the colour (bone,
   never orange-red) still says "it doesn't hurt". Check its readability with 48 piles at playtest.
2. **The Chimera only starts a breath with you inside its front 80° (half-angle 30 + 10).** With `_aim = dirTo` and no
   facing gate, "it turns slowly: flank it" (ARENAS.md) would do nothing: it would aim at you from any angle. Now beside
   or behind it is safe until it turns (160°/s).
3. **It plants (velocity zeroed) when the wind-up starts**, so the cone doesn't slide 0.5 m while the beast
   decelerates at 8 m/s².
4. **A collapse plays a soft `Splinter`, the stomp a full one plus the usual kill sound** (no new SFX): the collapse must
   not sound like a kill, and the doors already own `Rattle`.
5. **A pile that falls through a door is a kill credited to the fall**, like an enemy that falls (the Hypogeum's
   `Execute(i, FallSourceId)` is a kill too).
6. **`PilesOverflow` is its own counter** (the design folded nothing for the 48 cap): the bot log can tell whether the
   cap ever bites.
7. **A pile standing up brings no escorts** (`_rising`): no R2 enemy has escorts, but a future revenant leader would
   otherwise spawn a second line on every rise.
8. **The breath skips enemies more than 1.5 m above or below the beast** (the stage's 1.2 m edge): the cone is a ground
   decal.
9. **Not built (optional in the design):** `EnemyDef.riseOnSpawn` (the dead climbing out of the sand at a ring spawn).
   No tail sway for the Chimera (`PartRig` drives six parts; `GoatHead`/`Tail`/`TailHead` ride on `Body`).

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0 and 0 warnings on all six assemblies (compile only). New runtime code allocates
  nothing per frame (only field initialisers, `Awake` arrays and one `RenderParams`; checked by reading).
- **Unverified (needs the Editor):** every test; the pile mesh's look and scale (PR_BonePile isn't exported in this
  branch: the tests load it if it exists, else the piles work unseen); the bone disc's readability; the Chimera's pose
  on the real `EN_Chimera` rig (tests fall back to the War Boar's model); performance with 48 piles and 450 enemies.

## Tests (written, not run)
- **EditMode `BreathConeTests`:** `InCone` on the axis up to its length, 29° in / 31° out, past the length (with and
  without a body's pad), behind and beside, the pad's angular reach (33° in, 36° and 40° out at 5 m), any aim
  (flattened, not unit), the defaults (nothing reassembles, breath numbers), `Breather` = 10 after `Caster` = 9, the
  source ids distinct and `FallSourceId == StableId("hypogeum")`.
- **PlayMode `UndeadTests`** (test arena; stand-in Legionnaire made in the test with ARENAS.md §6's numbers):
  collapse (no OnKill, no gem, `AliveOf` 0) → stands up 2.5 s (± 0.15) later where it fell with half max HP → the second
  death is the kill with 2.5 XP and no pile; burning → no pile, the first death is the kill; `Execute(FallSourceId)` →
  no pile; `DropPiles` takes only the pile on the door (a kill) and the other one stands up; walking over a pile
  scatters it (a kill at the pile, `StompSourceId`, 2.5 XP) and nothing rises; an Armored elite stands up with its
  affix and half its elite max HP, the second death an elite kill; 60 killed at once → 48 piles, 12 overflow kills,
  the 48 stand up from the prewarmed pool.
- **PlayMode `ChimeraTests`** (stand-in Chimera, rooted with moveSpeed 0): wind-up 1 s with a wind-up threat and no
  damage → breath 1 s → the player hit once, `BreathHits` +1, three patches → recover → move; a player moved 40°
  off the locked aim, then (next breath) 9 m down it, isn't hit; a dummy in the cone loses exactly 72 for the whole
  breath, one 55° off, another Chimera (turned away so it never breathes) and a Scripted boss in the cone lose nothing;
  `SetHpFloor` keeps a Scripted slot at 100 through a 5,000 hit, `Execute` (false) and burn ticks, `SetHp` sets and
  clamps it (never kills), the floor off it dies, Spawn resets the floor.
- **Not testable here:** "2 Chimeras alive" (Q9) is the spawn table's and the lift pack's `maxAlive` (the layout
  part's data); the bot's log line (the layout part's `PlaytestBot`).

## Needs from others
- **Hypogeum part:** (1) an elite's fall damage (`eliteDamage` 300 through `ApplyDamage`) should carry
  `SourceId = EnemyManager.FallSourceId`, so an elite Legionnaire that dies of the fall doesn't reassemble (the design
  gave only `Execute` the id). (2) When a door is Open or Held, call `EnemyManager.Instance.DropPiles(new
  Vector2(x, z), yaw, trapdoorSize)` (per door, while open; it's cheap: ≤ 48 piles), so a pile on an opening door falls
  instead of standing up in the shaft. Optional: the orchestrator can land it at integration.
- **Layout and setup part:** (1) `pileMesh` = the mesh in `Props/PR_BonePile.fbx` (scale 1, origin at the base centre).
  (2) Pool headroom: risen Legionnaires come back on top of the table's cap (alive can exceed 160 by up to 48 while the
  Director refills during the 2.5 s), so give the Legionnaire's pool `cap + 48` (≈ 210) or check `PoolGrowths` in the
  first bot run. (3) The bot log names above.
- **Boss part:** `SetHpFloor` / `SetHp` / `GetHpFloor` exist with the design's signatures; delete any R2 stub of them at
  integration. `SetHp` doesn't change max HP (the HP bar shows remains as a fraction of 22,000).
- **Orchestrator / BACKLOG ideas:** the breath could also apply Burn to the horde (so a Chimera lured across the dead
  keeps them down: a combo the player can set up); a Codex line for the piles; a "stomp 100 piles" challenge (already in
  the design's BACKLOG list).
