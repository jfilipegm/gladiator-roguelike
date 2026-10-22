# R3 design — notes (Asterion, the Chained)

Branch `claude/elegant-herschel-00f6aa` (fast-forwarded to `origin/phase5-asterion` at 7d7884f). Docs only: no code,
no Blender, no Unity.

**Written:** `docs/GLADIATORS.md` § "Asterion, the Chained" (his card with numbers, how he differs from the Machine,
the Net-Weaver and the Knight, colours, how it's built, tests); `docs/tracks/R3-asterion.md` (the contract's Wave 2:
parts, owned files, done-when, merge order); this file (decisions, open questions, the art brief, and the fields,
methods and tests for each code part). A hand-over note for the art part is in the shared notes folder
(`r3-design.md`: the part names and pivots the code needs), also sent to the art session.

**Built on (read in this worktree):** `WeaponDef` / `WeaponSystem` (the swing timeline, `ArcHit`, `ThrustHit` and the
whip's `tipLength`, `SlamHit`, `TickOrbit`'s per-enemy arrays, `Strike`, `Hit`, `Hold`, `PickTargets`), `PlayerProjectiles`
(`Orbiter` with its streak, `MaxOrbiters 16`, `Shockwave`, `RegisterMesh`), `SlashFx` (a 180° fan: not used), the
traits (`VerdictTrait` / `Verdict`, `BloodRite`, `Stormcaller`, `ExecutionersPlate`, `CastNet`, `GladiatorTrait`,
`GladiatorLoadout`), `PlayerPartAnimator` / `PartAnimProfile` / `PartRig`, `PlayerMotor` (`KnockbackTakenMul`,
`HorizontalVelocity`, `SpeedMultiplier`), `EnemyManager` (`QueryAlive`, `ApplyDamage`, `IsBlocking`: the block test uses
the hit's push direction, knockback, contact damage, the hit-stop cooldown), `GameEvents`, `Hud.DrawBlood` (one meter
slot shared by the gladiators), `PlaytestBot`'s summary line, `GladiatorSetup` / `BeastmasterSetup` (paths, `Copy`,
`Trait`, `WeaponCard`, `All()`), `MetaSetup` (collects every `UnlockDef`), `IconSetup` + `PixelSrc/upgrade_icons.py`,
`BossDef` / `BossFight` (the fall stage, unscaled `_t`), the HUD announcer (a new line replaces the last), `LanistaTablet`
(3 traits at most, a generic mark), `RosterGrid` (16 tiles fit), the palette index, BOSSES.md's Eternal Victor, R2's
design notes and contract.

## Decisions (one-line reasons)
Nothing below changes CLAUDE.md: §6.7's row (Rage meter, chain sweeps, tragic, defeat the final boss) is built as written.

### Who he is
1. **The Eternal Victor's first opponent, chained under the stage for four hundred years.** It ties the last gladiator
   to the last boss (the Victor "sleeps among his dead in the hypogeum") and makes the unlock a liberation, not a trophy.
2. **Tragic through the Rage, not through text.** He's gentle when calm (head up to the sky when standing still), the
   crowd pressing in is what turns him into the monster, and every rampage ends with him Spent, looking at his hands.
3. **A pale ash-grey bull, not a black one.** His father was the white bull from the sea; a pale hide plus a charcoal
   mane, a blue loincloth and bronze reads on dark floors and pale ones, and nobody else in the roster is pale and
   horned.
4. **The weight is a block of the labyrinth's wall.** He carries a piece of his prison: the image of the character, and a
   stone on a chain is readable at 64 px.

### The weapon
5. **A new `Sweep` pattern: the chain travels across its arc (0.3 s), it doesn't land all at once.** That is what makes
   it a chain and not a big `MeleeArc`: enemies are hit one after another as it passes, and the crowd sways aside in a
   wave you can read with 300 on screen.
6. **240° at 5.2 m (56.6 m²): the widest melee arc in the game, with weaker hits (15).** The Greatsword covers 22 m² for 28, the
   Cleaver 18 m² for 34: the chain trades hit size for area, and his weakness against a single target is real.
7. **The stone always crits (its last 1.4 m), reusing the whip's `tipLength` rule.** One rule the player already knows
   ("fight at the whip's length") becomes "fight at the chain's length", and the crit numbers draw a ring in the crowd
   at the right distance. The stone kills a Convict (20 HP) in one hit; the inner chain doesn't.
8. **The push is sideways (the chain's tangent, plus 0.35 outward), 1.6× at the stone.** "Weight": the crowd is swept
   aside, not knocked straight back, so it opens lanes rather than a ring.
9. **Shields only stop the chain when they face the way it comes from (no special-case code).** The block test
   already uses the push direction; a sideways push misses the arc of a shield that faces him (a flail's old trick).
   Bosses get a radial push, so their shields block as before and their fights don't change.
10. **Sweeps alternate direction; Amount = backswings with no wind-up.** A heavy chain swings back and forth; the
    backswing covers the other side at once, so levels 4 and 8 feel like a figure of eight, not a repeat.
11. **Drawn as a chain of `Link` meshes plus the stone, never a fan.** A filled 240° fan at 5 m would cover the floor's
    orange-red telegraphs round him; one bronze line with a cyan-white streak behind the stone doesn't.
12. **The model's trailing chain is a part (`Chain`) that the weapon hides while it draws the swung one.** The select
    screen, the portrait and the Codex show the chain from the model; in play the swung chain is the hitbox you see.
    Any other gladiator who takes the chain from the pool sees it only while it swings, like the Spear.
13. **No evolution for the chain in R3** (BACKLOG, with the other post-5.9 weapons, none of which has one).

### Gore and Rage
14. **Gore (calm, ahead, while running) is his passive.** "The bull goes through you" is the most bull-like thing he can
    do, it makes movement a power source (§5), and no other gladiator hurts enemies by running into them.
15. **Gore tosses sideways out of his path and still lets contact damage land.** It opens a lane through a thin line;
    wading through a thick crowd still costs HP, so it isn't an invulnerable charge.
16. **Rage fills from the press of the crowd (enemies within 3.5 m), capped at 5/s, plus 3 per hit.** The Wrath fills
    from hits, the Blood from kills and hits, the Charge from hits dealt. Being crowded is new, and it fights the chain:
    the stone wants enemies at 3.8–5.2 m, the Rage wants them inside 3.5 m. That's a decision every few seconds, and it
    reads on screen without a ring (how close the crowd is).
17. **The cap (5/s) and the cooling keep Unchained at about a quarter to a third of a real fight.** Pressed at the cap:
    20 s to full; at the cap and hit every second: 12.5 s; a cycle of 8 s Unchained + 2 s Spent + 12.5–20 s filling =
    27–36 % uptime at most, and much less while kiting.
18. **Unchained is automatic (it isn't a button).** It's his story (the rage takes him), every other meter waits for a
    slide or a landing, and because Rage comes from the press it fires exactly where it's wanted: in the thick of it.
    A `triggerOnSlide` flag keeps the other option one checkbox away (Q1).
19. **Unchained changes the weapon and the movement, not just numbers.** The whirl (360°), gore from every side, +20 %
    speed and no knockback turn him into a bull loose in the crowd for 8 s. The bellow at the start (4 m) makes room,
    because he's surrounded by definition when it fires.
20. **Spent for 2 s afterwards (75 % speed, no fill).** The tragic beat and the cost of the rampage; at 5.3 m/s he still
    outruns a Convict (3.6), not a Pit Cur (5.6). One field (`spentSeconds 0`) removes it (Q2).
21. **The whirl is a flag on `WeaponSystem` that Rage sets.** Only `Sweep` weapons read it, so the chain in another
    gladiator's hands never whirls, and the weapon code never depends on the trait.
22. **No glowing material while Unchained.** A per-object emissive needs a new material (§4: few shared materials):
    he swells ×1.1, drops his horns, sheds white-gold specks, and the bar drains. Enough to read it.
23. **No new SFX** (R4/R5 redo audio): the sweep plays `Rattle` + `Swing`, Gore `Hit`, the bellow `BossRoar`, the tease
    `Rattle` + `BossRoar` at the stage.

### Numbers for the gladiator
24. **130 HP, 95 % speed, capsule radius 0.45 m.** Heavy (the Machine is 150 HP, 85 %, 0.46 m, with armour; the Knight
    120 HP, 92 %): Asterion has no armour, so a little less HP than the Machine and more speed, and the wide capsule is
    his "big target" weakness.
25. **No new status and no new enemy.** Gore and the chain use Damage and knockback; everything else is reuse.

### The unlock
26. **`DefeatBoss eternal_victor 1`**, §6.7's "Defeat the final boss", unchanged.
27. **A tease at the Victor's fall, while Asterion is locked: "BELOW THE SAND, A CHAIN SNAPS." (data on `BossDef`).**
    The liberation is shown in the moment it happens, for about 20 lines of code, and any future boss can tease its
    unlock the same way. The full visual reveal (the stage's lift raising him, chained) goes to the BACKLOG (Q6).

## Open questions (for the orchestrator; my recommendation first)
- **Q1. Unchained: automatic (recommended) or on the next slide with a full meter.** Automatic is his identity; if the
  playtest finds it fires at bad moments, set `TR_Rage.triggerOnSlide` (the HUD then says "SLIDE!").
- **Q2. Spent: keep it (recommended: 2 s, 75 % speed)** or `spentSeconds 0`.
- **Q3. The press radius (3.5 m) against the stone's zone (3.8–5.2 m).** Keep the tension (recommended). If players
  don't get it, raise `pressRadius` to 5.2 (the whole chain): Rage gets easier and the decision goes away.
- **Q4. Gore while calm: keep it (recommended)** vs only while Unchained (simpler, but then calm Asterion is just a
  weapon).
- **Q5. Shields: the sideways push slips past shields that face him; bosses' shields block (recommended)** vs a radial
  push for everyone (shields stop the chain like any other hit).
- **Q6. The tease line at the Victor's fall in R3 (recommended; the setup part, ~20 lines in `BossDef` / `BossFight`)**;
  the full visual reveal (the lift raising him chained, the chain snapping) is a BACKLOG item for later.
- **Q7. The model's `Chain` part:** one part, a child of the model root (not of `ArmR`), pivot at the right hand's rest
  position, lying on the sand behind him (recommended: it stays on the ground while he runs). The art part has the
  request and decides; whatever it ships, the setup part writes its name into `WP_LabyrinthChain.modelPart`.
- **Q8. The chain's card tag: `Heavy` (recommended)**, which adds a fourth HEAVY carrier; or no tag, like the Cleaver's
  card.
- **Q9. The Nobody's *Underdog* (BACKLOG: bonus when outnumbered) would overlap the press.** If it's ever built, make it
  about something else (recommended) or drop it.
- **Q10. HP 130 / speed 95 % / the Rage rates** are first guesses: the bot's line (Unchained count and seconds, peak
  press, damage taken) and the producer's playtest tune them. The bot avoids enemies, so its Rage is a lower bound;
  no "wade in" bot mode in R3 (BACKLOG).
- **Q11. Three code parts (chain → rage → setup, in that merge order; recommended)**, or fold setup into rage to save a
  session (it would then own six more files and wait for the chain anyway).

## Needs from others
- **art:** `CH_Asterion` with the six usual parts plus `Chain` (Q7); `WP_LabyrinthChain` with two meshes, `Link` (a run
  of 3 links, 0.6 m along +Z, origin at its start, ≤ 48 tris) and `Weight` (the stone, origin at its centre, ring bolt
  on its −Z face, ≤ 160 tris). The names in its notes win; the code parts read them from there.
- **orchestrator:** Q1–Q11; after setup merges, run `Gladiator/Setup/Gladiators/Create Gladiator Data` (it calls
  `AsterionSetup`), `Gladiator/Setup/Upgrade Icons`, `Bake Portraits`, `Bake Codex Portraits`, rebuild the menu scene
  (`menu.gladiators = GladiatorSetup.All()`); BACKLOG lines (below); GDD/UNLOCKS rows for him.

## Merge order
**chain** depends only on today's code. **rage** sets three names of the chain part's (`WeaponSystem.WhirlSweeps`,
`PlayerPartAnimator.ScaleBoost` / `ExtraLean` / `HeadDrop`) and the bot line reads its counters (`Sweeps`, `Whirls`,
`SweepHits`, `WeightHits`); it can be written at the same time against these names (ccheck shows only those until the
chain part merges; then it merges `phase5-asterion` and is clean). **setup** uses `WeaponPattern.Sweep`, the new
`WeaponDef` and `PartAnimProfile` fields and both traits' fields; it codes against the names and merges last. PlayMode
tests run after all three (the setup's after the art too).

## The art brief (for the art part; it owns the scripts and the final names)
- **CH_Asterion** (≤ 1,200 tris, rigid parts, export like `ch_beastmaster.py` / `ch_murdermachine.py`): `Body`, `Head`,
  `ArmL`, `ArmR`, `LegL`, `LegR` with joint pivots, plus **`Chain`** (Q7). 1.95 m to the horn tips, the head low and
  forward of the shoulder hump (1.6 m at the top of the head), **lyre horns ~1.0 m tip to tip** (`bone`, `charcoal` tips),
  hide `ash`, muzzle and chest `bone`, mane on the hump `charcoal`, eyes `ink` (sad, no snarl), long heavy arms (knuckles
  near the knees), short thick legs, hooves `ink`; loincloth `cloth_blue` with an `ochre` meander hem; collar `bronze`
  (rope-worn, a broken ring hanging), a broken shackle on the left wrist, the right wrist's shackle joined to the chain;
  a few pale scars; **a `player_cyan` meander brand across the shoulders** (the mark from the camera behind). The
  `Chain`: bronze links trailing 2–2.5 m behind him on the sand to **the stone** (~0.42 × 0.42 × 0.48 m, `stone_grey`
  with a `bone` meander, a `bronze` ring bolt).
- **Review:** 3 angles + 64 px; a lineup with every gladiator, above all the Murder Machine, the Beastmaster, the Blood
  Knight and the Brute; one shot on a pale floor and one on a dark one; the portrait's square must hold the horns.
- **WP_LabyrinthChain:** `Link` and `Weight` as in "Needs from others", `bronze` / `bronze_d` links.

## For each code part

### Chain — `WeaponDef.cs`, `WeaponSystem.cs`, `PlayerProjectiles.cs`, `PlayerPartAnimator.cs`, `PartAnimProfile.cs`
**`WeaponPattern.Sweep`** appended after `Lob` (assets serialize the index: Sweep = 10). Update the enum's and the
class's doc comments.

**`WeaponDef`** — a new header `"Sweep (the Labyrinth Chain, R3)"`:
| Field | Type | Default | Labyrinth Chain | Meaning |
|---|---|---|---|---|
| `sweepTime` | float | 0.3 | 0.3 | seconds the chain takes to cross `arcDegrees` |
| `whirlTimeMul` | float | 1.5 | 1.5 | a whirl (360°) takes `sweepTime` × this |
| `sweepOutward` | float | 0.35 | 0.35 | the push = the chain's tangent + outward × this, normalised |
| `tipKnockbackMul` | float | 1.6 | 1.6 | the stone's knockback × this (its hits crit through `tipLength`, shared with Thrust) |
| `linkMesh` | Mesh | null | `WP_LabyrinthChain`'s `Link` | laid end to end from the hand to `mesh` (the stone) |
| `linkLength` | float | 0.6 | 0.6 | the link mesh's length along +Z |
| `chainHeight` | Vector2 | (1.05, 0.55) | same | the chain's height above the feet at the hand and at the stone |
| `modelPart` | string | "" | `"Chain"` | a part of the gladiator's model this weapon swings: hidden while the weapon draws it |

Existing fields the Sweep uses: `range` (the reach, × Area), `arcDegrees` (the arc), `tipLength` (× √Area), `width` (the
chain's thickness: its half pads the bearing test), `acquireRange`, `amount` (sweeps per trigger), `mesh` (the stone),
`meshScale`, `windup`, `swingDuration`, `cooldown`, `knockback`, `onHitStatus`, `hitEffects` (all through `Strike`).
Update the tooltips of `range`, `amount` and `tipLength` to mention the Sweep.

**`WeaponInstance`** — new state, reset in `Init`: `float SweepT = -1` (seconds into the current sweep), `float SweepFrom,
SweepTo, SweepPrev` (bearings relative to `AimDir`, degrees), `int SweepDir = 1` (+1 = right to left, the first one;
flips after every sweep), `int SweepLeft` (backswings to come), `bool SweepWhirl`, `int SweepId` (serial), `int[] SweepSeen`
(per enemy slot, the last `SweepId` that hit it; allocated once at `em.capacity`, like Orbit's `NextHit`),
`int LinkMesh = -2`, `Transform Part` + `bool PartLooked`.

**`WeaponSystem`:**
- `public bool WhirlSweeps { get; set; }` (Rage sets it; only Sweep weapons read it).
- Counters: `public int Sweeps, Whirls, SweepHits, WeightHits { get; private set; }` (every sweep including backswings;
  whirls among them; enemies hit; stone hits).
- `readonly int[] _sweepHits = new int[512]` (a 7 m disc can hold more than 256 at 450 alive).
- **Timeline:** a Sweep weapon runs the usual acquire → wind-up → impact; `BurstLeft` stays 0 for it (Amount is handled
  by the sweep); it doesn't start a new swing while `SweepT >= 0` (the cooldown still counts down). `StartSwing`: aim at
  the nearest as today, `SfxId.Swing`.
- **`BeginSweep(w, em)`** (at impact, and for each backswing): `SweepWhirl = WhirlSweeps`; `half = whirl ? 180 :
  arcDegrees / 2`; `SweepFrom = +half × SweepDir`, `SweepTo = −half × SweepDir` (bearings: `Vector3.SignedAngle(aim, to,
  up)`, positive to the right), `SweepPrev = SweepFrom`, `SweepT = 0`, `SweepId++`; hide `Part`; `SfxId.Rattle`;
  `Sweeps++` (and `Whirls++`). At impact, `SweepLeft = AmountOf(w) − 1`.
- **`StepSweep(w, em, dt)`** (every frame while `SweepT >= 0`, before the cooldown check): `T = sweepTime ×
  (whirl ? whirlTimeMul : 1)`; `u = clamp01(SweepT / T)`; `cur = lerp(SweepFrom, SweepTo, u)`; hit every enemy the chain
  passed between `SweepPrev` and `cur` (below); draw the chain at `cur`; `SweepPrev = cur`. At `u >= 1`: if
  `SweepLeft > 0`, `SweepLeft--`, flip `SweepDir`, re-aim at the nearest within reach (keep the aim if none) and
  `BeginSweep` at once (no wind-up); else `SweepT = −1`, flip `SweepDir` (the next trigger comes back the other way),
  show `Part`.
- **Hits:** `reach = range × area`, `tip = tipLength × √area`; `QueryAlive(origin, reach + MaxRadius, _sweepHits)`; skip
  `SweepSeen[i] == SweepId`; `to` on XZ, `dist`, `r`; skip `dist > reach + r`; `b = SweepBearing(AimDir, to)`;
  `pad = asin(min(1, (r + width / 2) / max(dist, r + width / 2)))` in degrees; skip unless
  `SweepCovers(SweepPrev, cur, b, pad)`; then `SweepSeen[i] = SweepId`; `stone = tip > 0 && dist >= reach − tip`;
  push = `SweepPush(to / dist, sign(cur − SweepPrev), sweepOutward)`, or `to / dist` for a Scripted enemy;
  `info = Hit(w, em, i, push, DamageFlags.Melee, forceCrit: stone)`, `info.Knockback *= stone ? tipKnockbackMul : 1`;
  `Strike(w, em, i, info)`; `SweepHits++` (`WeightHits++`).
- **The animator** during any sweep (the first and the backswings): `AttackT = w0 + (w1 − w0) × u` with `w0 = windup /
  swingDuration`, `w1 = (windup + sweepTime) / swingDuration` (so the profile's curve plays the sweep part of the swing),
  `AttackMirror = SweepDir < 0`, `AttackDirection = AimDir`, `AttackSpin = whirl ? cur − SweepFrom : 0`. After the last
  sweep: `AttackSpin = 0`, `AttackMirror = false`.
- **Drawing (`DrawChain`)** at bearing `cur`: `dir = Quaternion.Euler(0, cur, 0) × AimDir`; the hand at `origin + up ×
  chainHeight.x + dir × 0.45`; the stone at `origin + dir × reach + up × chainHeight.y`; `n = ceil(|stone − hand| /
  linkLength)` links laid end to end (scaled along Z to fit exactly), each rolled 90° from the last,
  `PlayerProjectiles.Orbiter(linkMesh, pos, rot, 1, zero)`; the stone `Orbiter(mesh, stone, LookRotation(dir) × a tumble
  of 360°/s, meshScale × √area, tangent)` (its streak behind it). A null mesh draws nothing (tests).
- **The model part:** on the first sweep, find `modelPart` under `animator.visualRoot` by name once (`PartLooked`);
  hidden from `BeginSweep` until the last sweep ends.
- **`Hold`** (halted): `SweepT = −1`, `SweepLeft = 0`, show `Part`, the animator's spin and mirror off.
- **Pure statics (EditMode):** `public static float SweepBearing(Vector3 aim, Vector3 to)` (signed, XZ, −180..180);
  `public static bool SweepCovers(float a0, float a1, float bearing, float pad)` (true if `bearing`, `bearing − 360` or
  `bearing + 360` lies in [min − pad, max + pad]; either direction); `public static Vector3 SweepPush(Vector3 outward,
  float sign, float outwardMix)` (`normalize(Quaternion.Euler(0, 90 × sign, 0) × outward + outward × outwardMix)`: the way
  a point on a chain turning that way moves, plus a little outward).

**`PlayerProjectiles`:** `MaxOrbiters` 16 → 40 (a 7 m chain is 12 links + the stone, beside a flail's 8 and a loan).

**`PlayerPartAnimator`** (new properties; every default leaves the other gladiators as they are):
- `bool AttackMirror` — negates the swing's yaw (`swingYawStart` / `swingYawEnd`) and the twist's yaw part.
- `float AttackSpin` — degrees the visual root turns about Y after the rig is applied (a whirl); cache the root's bind
  rotation in `Awake` and restore it at 0.
- `float ScaleBoost = 1` — multiplies the root scale (eased by the pose blend). `float ExtraLean` — forward body lean
  added (deg). `float HeadDrop` — the head pitched down (deg). (Rage sets these three.)
- Idle head lift: after `idleHeadDelay` s standing still, the head lifts `idleHeadLift` deg (he looks at the sky).
- The body's twist toward the swing uses `profile.bodyTwistScale` instead of the constant 0.2.

**`PartAnimProfile`** — new fields: `bodyTwistScale` (0.2: today's constant), `idleHeadLift` (0), `idleHeadDelay` (1.2).

**Tests:** EditMode **`SweepMathTests`**: `SweepCovers(120, 100, 110, 0)` true, `(120, 100, 90, 0)` false, `(120, 100, 98, 3)`
true, `(−100, −80, −90, 0)` true (the other direction), `(170, 180, −178, 3)` true (the wrap), the whole whirl
`(180, −180, x, 0)` true for x in −180..180; `SweepPush` is unit length, perpendicular to `outward` when the mix is 0,
flips with the sign, and has the outward part when the mix is 0.35; `SweepBearing` is +90 to the right and −90 to the
left. PlayMode **`ChainSweepTests`** (the test arena; a `WeaponDef` made in the test with `ScriptableObject.CreateInstance`
and the chain's numbers, so they don't wait for setup; the Nobody with `RemoveAll` then `Equip`): an enemy 3 m away
inside the arc is hit once per sweep, one 6.5 m away and one straight behind aren't; two enemies at bearings +100 and
−100 are hit in sweep order, about 0.25 s apart; an enemy at 4.5 m is hit as a crit (`WeightHits` 1) and one at 2.5 m
isn't; the push is sideways (the hit enemy moves more along the tangent than outward); two triggers sweep opposite ways;
with Amount 2 the backswing follows within 0.1 s of the first sweep's end, with no wind-up; a Shield Thug facing the
player takes full damage (not `Blocked`); a Scripted test enemy with its shield raised, facing the player, is `Blocked`;
`WhirlSweeps = true` hits an enemy straight behind and every enemy round him once (`Whirls` 1), and `AttackSpin` is
back to 0 after; a child named `Chain` under the visual root is hidden during the sweep and shown after; a sweep in
progress stops when the player dies (no more hits); no GC allocation per frame across 5 s of sweeps with 200 enemies
(ProfilerRecorder "GC Allocated In Frame", after a warm-up).

### Rage — `GoreTrait.cs`, `RageTrait.cs` (new, `Assets/Scripts/Gameplay/Gladiators/`), `Hud.cs`, `PlaytestBot.cs`
**`GoreTrait : GladiatorTrait`** (menu `Gladiator/Traits/Gore`, file `TR_Gore`); installs **`Gore : MonoBehaviour,
ITickable`** (`Instance`; `TickGroup.Player`, order 60; `public static readonly int SourceId = GameInfo.StableId("gore")`):
| Field | Default | Meaning |
|---|---|---|
| `minSpeed` | 4 | horizontal speed (m/s) to gore while calm |
| `reach` | 0.5 | m past his capsule radius (+ the enemy's radius) |
| `halfAngle` | 60 | deg either side of his velocity |
| `ahead` | 0.35 | the query's centre, m ahead along the velocity |
| `damage` | 8 | × the player's Damage |
| `knockback` | 10 | × the player's Knockback, sideways out of his path |
| `forward` | 0.25 | the toss = sideways + forward × this, normalised |
| `interval` | 1 | s before the same enemy can be gored again |
| `unchainedMinSpeed`, `unchainedReach`, `unchainedHalfAngle`, `unchainedDamage`, `unchainedInterval` | 2.5, 0.8, 180, 14, 0.5 | while `Rage.IsUnchained` |
| `puff` | (0.95, 0.95, 0.92) | the puff at the horns |
- **Tick:** not when dead or `WeaponSystem.Halted`; `v = PlayerMotor.HorizontalVelocity`; the calm or Unchained numbers;
  `speed < minSpeed` → nothing; `heading = v / speed`; `QueryAlive(pos + heading × ahead, capsule + reach + MaxRadius +
  ahead, _hits)`; for each enemy within `capsule + reach + r` (XZ), within `halfAngle` of `heading`, not gored within
  `interval` (per-slot `float[] _next` and `int[] _uid`, allocated once at `em.capacity`): the sideways part of `to`
  (`to − heading × dot(to, heading)`; dead ahead, the side from the uid's low bit), push = `normalize(side + heading ×
  forward)`; `ApplyDamage(DamageFlags.Melee, damage × Damage, push, knockback × Knockback, SourceId)`; a Scripted enemy
  gets `to / dist` and no knockback; a 3-speck `puff` at the contact point; `SfxId.Hit` at most every 0.1 s.
- **Public:** `int Gored`, `int GoreKills` (KillEvents with `SourceId`), `bool Goring` (this frame).

**`RageTrait : GladiatorTrait`** (menu `Gladiator/Traits/Rage`, file `TR_Rage`); installs **`Rage : MonoBehaviour,
ITickable`** (`Instance`; `TickGroup.Player`, order 59; `BellowSourceId = GameInfo.StableId("bellow")`):
| Field | Default | Meaning |
|---|---|---|
| `meterMax` | 100 | |
| `pressRadius` | 3.5 | m from his centre (+ the enemy's radius); not scaled by Area |
| `pressPerEnemy` | 1 | Rage per second for each enemy in the press |
| `pressHeavy` | 4 | an elite or a Scripted enemy counts this many |
| `pressMaxPerSecond` | 5 | the cap ("CORNERED") |
| `perHit` | 3 | per hit taken (status ticks don't count) |
| `calmDelay`, `coolPerSecond` | 2.5, 8 | no press and no hit for this long → it cools |
| `triggerOnSlide` | false | Q1's fallback: a full meter waits for a slide |
| `unchainedSeconds` | 8 | |
| `attackSpeed`, `moveSpeed` | 1.3, 1.2 | StatMods (Multiply) while Unchained |
| `scale`, `lean` | 1.1, 14 | `ScaleBoost`, `ExtraLean` while Unchained |
| `bellowRadius`, `bellowDamage`, `bellowKnockback` | 4, 8, 12 | radius × √Area, damage × Damage, knockback × Knockback |
| `spentSeconds`, `spentMoveSpeed`, `spentHeadDrop` | 2, 0.75, 30 | |
| `breathFrom`, `breathEvery` | 0.5, (0.9, 0.4) | snorts from half full: seconds between them at half and at full |
| `speckEvery` | 0.12 | Unchained's white-gold specks |
| `bronze`, `hot`, `spent`, `speck`, `breath` | (0.80, 0.56, 0.30), (1, 0.93, 0.7), (0.45, 0.45, 0.48), (1, 0.9, 0.55), (0.95, 0.95, 0.95) | never orange-red |
| `announce` | "UNCHAINED!" | |
- **State:** `enum Mode : byte { Calm, Unchained, Spent }`; `float Value`, `Fill01`, `bool Full`, `Mode Current`,
  `IsUnchained`, `IsSpent`, `float UnchainedLeft`, `Unchained01` (left / seconds), `SpentLeft`, `int Pressing` (this
  frame, weighted), `float PressRate`, `bool Cornered` (the rate at the cap); counters `Unchains`, `BellowHits`,
  `PeakPress`, `float UnchainedTime`.
- **Calm** (each tick): count the press (`QueryAlive(pos, pressRadius + MaxRadius, _hits[512])`, XZ within `pressRadius +
  r`, weighted); `PressRate = min(Pressing × pressPerEnemy, pressMaxPerSecond)`; `Value += PressRate × dt`; on a
  `DamagedEvent` that isn't a status tick, `+perHit`; no press and no hit for `calmDelay` → `−coolPerSecond × dt`;
  snorts (a 3-speck `breath` burst at the `Head` part, found once under the player, plus 0.3 m forward) from
  `breathFrom`; full → `Unchain()` (or, with `triggerOnSlide`, on the next `SlideEvent`).
- **`Unchain()`:** Mode Unchained, `UnchainedLeft = unchainedSeconds`; add the two StatMods; remember
  `PlayerMotor.KnockbackTakenMul` and set it to 0; `WeaponSystem.WhirlSweeps = true`; animator `ScaleBoost = scale`,
  `ExtraLean = lean`; the bellow (everything within the radius: damage, knockback away, `DamageFlags.Area`,
  `BellowSourceId`; `PlayerProjectiles.Shockwave` at the radius and at 0.6 of it; a 30-speck `speck` burst); the announce
  (`AnnounceKind.Line`, `hot`, 1.4 s); `SfxId.BossRoar`; `ScreenShake.Add(0.3)`; `CrowdExcitement.Spike(0.4)`; `Unchains++`.
- **Unchained** (each tick): `UnchainedLeft −= dt`; `Value = meterMax × Unchained01`; specks every `speckEvery`;
  `UnchainedTime += dt`; at 0 → **Spent**: the two mods off, knockback back, whirl off, `ScaleBoost 1`, `ExtraLean 0`;
  the Spent StatMod (MoveSpeed × `spentMoveSpeed`), `HeadDrop = spentHeadDrop`, `Value 0`, `SpentLeft = spentSeconds`.
- **Spent** (each tick): no fill; at 0 → Calm, the mod off, `HeadDrop 0`, the calm clocks reset.
- **`Halt()`** (death, `RunState.CombatOver`, `OnDisable`): back to Calm with every mod, flag and pose reset.
- **Public for tests and admin:** `void Fill()`, `void SetRage(float)`, `bool Unchain()`, `void Halt()`.

**`Hud.cs`:** `DrawRage(Rage r, float unscaledDt)` on the Blood bar (in `DrawBlood`, beside the others: one gladiator
at a time). Colours `RageColor (0.80, 0.56, 0.30)`, `RageHot (1, 0.93, 0.7)`, `RageSpent (0.45, 0.45, 0.48)`. Fill: calm
`Fill01` (MoveTowards × 3), Unchained `Unchained01` (× 6), Spent 0. Colour: calm bronze, pulsing to hot (8 Hz) when
`Cornered` or above 85 %; Unchained pulsing (14 Hz); Spent grey. Label (constant strings, `_bloodKey` keys):
"CORNERED", "UNCHAINED", "SPENT", "SLIDE!" (`triggerOnSlide` and full), else empty.

**`PlaytestBot.cs`:** after the wolves: ` sweeps S (whirls W, hits H, stone T)` from `WeaponSystem` when `Sweeps > 0`;
` unchained U (Xs, bellow B) peakPress P` from `Rage`; ` gored G (kills K)` from `Gore`.

**Tests:** PlayMode **`RageTests`** (the test arena; a `GladiatorDef`, a Sweep `WeaponDef`, `GoreTrait` and `RageTrait` all
made in the test with these numbers and given through `GladiatorLoadout.NextOverride`; enemies held in place by a copy
of the Convict with move speed 0 and no contact damage): four enemies within 3 m add ≈ 8 Rage in 2 s; eight are capped
at 5/s (`Cornered`); one elite counts 4; a hit taken adds 3, a Burn tick on the player adds nothing; with nobody near
and no hit, after 2.5 s it cools at 8/s; full → Unchained at once: the bellow hits and pushes out an enemy 3 m away
and not one 6 m away, AttackSpeed ×1.3 and MoveSpeed ×1.2 in `PlayerStats`, `KnockbackTakenMul` 0, `WhirlSweeps`
true, "UNCHAINED!" raised; 8 s later Spent (MoveSpeed ×0.75; the press adds nothing), 2 s after that calm with every
stat back to its base; with `triggerOnSlide` a full meter waits for a slide. Gore: running at 7 m/s into a held enemy
ahead gores it (8 damage, moved more than 1 m sideways within 0.3 s), one 90° off his heading isn't, walking at 2 m/s
doesn't, the same one isn't gored twice within 1 s; Unchained, an enemy behind him is gored at 3 m/s (14 damage);
a Scripted test enemy is damaged and not moved; the player dying while Unchained puts every stat back.

### Setup — `AsterionSetup.cs` (new), `GladiatorSetup.cs`, `BossDef.cs`, `BossFight.cs`, `PixelSrc/upgrade_icons.py` and its atlas
**`AsterionSetup`** (`Assets/Editor/Setup/`, like `BeastmasterSetup`; menu `Gladiator/Setup/Gladiators/Create Asterion
Data`; every asset created once, then designer-owned, except the empty-reference fills):
- Paths: `Dir = GladiatorSetup.Dir`; `GL_Asterion`, `TR_Gore`, `TR_Rage` there; `Assets/Data/Config/MovementConfig_Asterion`
  and `PartAnim_Asterion`; `UpgradeSetup.WeaponDir + "WP_LabyrinthChain.asset"`, `UpgradeSetup.Dir +
  "UP_W_LabyrinthChain.asset"`; `MetaSetup.Dir + "UL_Asterion.asset"`; models `Assets/Art/Models/Characters/CH_Asterion.fbx`,
  `Assets/Art/Models/Weapons/WP_LabyrinthChain.fbx` (meshes `Link`, `Weight`).
- **`WP_LabyrinthChain`:** id `labyrinth_chain`, "Labyrinth Chain", *"A heavy chain with a block of the labyrinth's wall
  at its end, swept round you in a wide arc. Everything it passes is shoved aside; the stone always crits: fight at the
  chain's length."*, `Sweep`; damage 15, critChance 0.05, critMultiplier 2, knockback 7, cooldown 1.6, swingDuration 0.8,
  windup 0.32, range 5.2, arcDegrees 240, width 0.5, acquireRange 5.6, amount 1, tipLength 1.4, sweepTime 0.3,
  whirlTimeMul 1.5, sweepOutward 0.35, tipKnockbackMul 1.6, linkLength 0.6, chainHeight (1.05, 0.55), meshScale 1,
  modelPart `"Chain"` (or the art's name), mesh = `Weight`, linkMesh = `Link`; levels: `L(Pct Damage 0.2)`,
  `L(Pct Area 0.15)`, `L(Add Amount 1)`, `L(Pct AttackSpeed 0.15)`, `L(Pct Damage 0.25, Pct Knockback 0.2)`,
  `L(Pct Area 0.15)`, `L(Add Amount 1, Pct Damage 0.15)`.
- **`UP_W_LabyrinthChain`:** id `weapon_labyrinth_chain`, glyph "LC", `UpgradeKind.Weapon`, maxLevel 8, weight 8, tag
  `Heavy` (Q8).
- **`TR_Gore`** ("Gore": *"Run into enemies head-first and you gore them: they're hurt and tossed out of your way."*) and
  **`TR_Rage`** ("Rage": *"The crowd pressing round you fills your Rage, and so do hits. Full, you're Unchained for 8 s:
  the chain whirls all round, you're faster and gore everything you touch. Then you're Spent for 2 s."*) with the
  defaults above.
- **`MovementConfig_Asterion`** (a copy of the player's): capsuleHeight 1.85, slideCapsuleHeight 1.1, capsuleRadius 0.45.
- **`PartAnim_Asterion`** (a copy of the player's): strideLength 1.45, bobHeight 0.08, runLean 16, armSwing × 0.6,
  weaponArmSwingScale 0.25, swingYawStart 120, swingYawEnd −120, swingRaise 20, bodyTwistScale 0.5, idleHeadLift 18,
  swingCurve keys (0, 0), (0.4, 0.04), (0.78, 1), (1, 1) (the pull, then the sweep: windup 0.32 and sweep 0.3 of 0.8 s),
  poseSharpness 24.
- **`GL_Asterion`:** id `asterion`, "Asterion, the Chained", *"The labyrinth's last prisoner, chained under the Colosseum
  for four hundred years. His chain sweeps the sand round him, his horns go through a line, and when the crowd closes in,
  he is Unchained."*, baseStats MaxHp 130, MoveSpeed 0.95; cameraTargetHeight 1.5; tradeoff *"Huge and heavy: 95 % speed,
  no armour, and his Rage takes him when the crowd closes in, not when you choose."*; startingWeapon, traits
  { Gore, Rage }, unlock, model.
- **`UL_Asterion`:** id `asterion`, "Asterion, the Chained", description *"The Eternal Victor is dust, and the chain he
  kept under the stage for four hundred years has snapped. Asterion can be chosen, and his Labyrinth Chain joins the
  level-up pool."*, hint *"Something has been chained under the Colosseum since its first games. Its jailer is the
  Eternal Victor."*, conditions { `DefeatBoss eternal_victor 1` }, addsToPool { the card }.
- **The tease on the boss:** load `BossSetup.EternalVictorPath`; if its `revealText` is empty, set `revealText` "BELOW THE
  SAND, A CHAIN SNAPS.", `revealUnlock` = `UL_Asterion`, `revealDelay` 1.2, `revealHold` 1.8 (so `BossSetup.cs` isn't
  touched and the order of the setups doesn't matter).
- Then `UpgradeSetup.Refresh()`, `MetaSetup.CreateOrUpdate()`, `AssetDatabase.SaveAssets()`.

**`GladiatorSetup.cs`:** call `AsterionSetup.CreateOrUpdate()` right after `BeastmasterSetup.CreateOrUpdate()`; add
`GL_Asterion` last in `All()` (the select screen, the Codex and the portrait bakes follow).

**`BossDef.cs`** — a new header `"Reveal (R3)"` (defaults: no tease):
| Field | Type | Default | Eternal Victor | Meaning |
|---|---|---|---|---|
| `revealText` | string | "" | "BELOW THE SAND, A CHAIN SNAPS." | a line after the fall line; empty = none |
| `revealUnlock` | UnlockDef | null | `UL_Asterion` | the line shows only while this is locked (null = always) |
| `revealDelay` | float | 1.2 | 1.2 | seconds into the fall (unscaled, the stage's `_t`) |
| `revealHold` | float | 0 | 1.8 | seconds the fall is held longer when the line shows, so it's read before the reward pick |
| `revealColor` | Color | (0.86, 0.78, 0.62) | same | parchment, not orange-red |

**`BossFight.cs`:** in `Stage.Falling`, once `_t >= revealDelay` with a `revealText`, the unlock locked (or null) and
not shown yet: raise it (`AnnounceKind.Line`, `revealColor`, 2.6 s), `SfxId.Rattle` and `SfxId.BossRoar` at the arena
centre, `ScreenShake.Add(0.15)`, `RevealShown = true`; the fall then lasts `fallSeconds + revealHold`. `public bool
RevealShown` (tests).

**`PixelSrc/upgrade_icons.py`:** `ICONS["weapon_labyrinth_chain"]`, appended last so no other row moves: a bronze chain
from the top left to a grey stone block with a pale meander, bottom right; regenerate `Assets/Art/Textures/UI/UpgradeIcons.png`
and `.txt` (`python3 PixelSrc/upgrade_icons.py`); the orchestrator runs `Gladiator/Setup/Upgrade Icons`.

**Tests:** PlayMode **`AsterionTests`** (after the setup has run): `GL_Asterion` has id `asterion`, MaxHp 130, MoveSpeed
0.95, `CH_Asterion`, `WP_LabyrinthChain` (pattern `Sweep`, range 5.2, arc 240, tipLength 1.4), traits Gore and Rage,
`UL_Asterion` (`DefeatBoss eternal_victor 1`), and is last in `GladiatorSetup.All()`; a run as Asterion in the test arena
has his model (its `Chain` part found and shown between sweeps), the 0.45 m capsule, his stats, the `Rage` and `Gore`
components, and the chain sweeps at an enemy; a save that has beaten `eternal_victor` unlocks `asterion` and adds the
chain's card to the pool, one that hasn't doesn't (like `BeastmasterTests`' unlock test); `UP_W_LabyrinthChain` has an
icon and max level 8. The tease, with a test `BossDef` (CreateInstance, `revealText` set) in a test-arena boss fight:
shown once after `revealDelay` while its test unlock is locked (`RevealShown`), not shown when unlocked, and the fall
lasts `revealHold` longer only when shown; an existing boss with no `revealText` falls exactly as before.

## Numbers (arithmetic, for the playtest)
| | Chain (L1) | Chain (L8) | Whirl (Unchained, L1) | Greatsword | Cleaver |
|---|---|---|---|---|---|
| Area hit per trigger | 56.6 m² (240°, 5.2 m) | 99 m² (240°, 6.88 m) × 3 sweeps | 84.9 m² | 22.2 m² | 18.1 m² |
| Hit (inner / stone) | 15 / 30 | 25.9 / 51.8 | 15 / 30 | 28 | 34 |
| Per enemy per second (inner / stone) | 9.4 / 18.8 | 18.6 / 37.2 per sweep (up to 3 sweeps) | 12.2 / 24.4 (×1.3 speed) | 18.7 | 16.2 |
| Busy time vs cooldown | 0.62 s of 1.6 s | 1.22 s of 1.39 s | 0.77 s of 1.23 s | | |
- The stone's band is 26.4 m², 47 % of the arc. A Convict (20 HP) dies to one stone hit and survives one inner hit; a
  Pit Cur (12) dies to either.
- Speeds (run 7.5 m/s): calm 7.1, Unchained 8.6, Spent 5.3. Gore's 4 m/s is 56 % of his run.
- Rage: at the cap 20 s to full; at the cap and hit once a second 12.5 s; a typical press of 3 and a hit every 2 s
  (4.5/s) 22 s. Unchained 8 s + Spent 2 s → 27–36 % uptime at most when pressed all fight; kiting cools 8/s
  (100 → 0 in 12.5 s).
- At L8 a whirl trigger is 3 whirls (0.32 + 3 × 0.45 = 1.67 s) against a 1.07 s cooldown: the cooldown waits for the
  sweeps (the timeline rule above), so the cycle is 1.67 s.

## Ideas for the BACKLOG (not R3)
The full reveal at the Victor's fall (the stage's lift raising Asterion, kneeling and chained; the chain snaps; he looks
up at the player; the lift sinks); a Labyrinth Chain evolution (the whirl as its twist, with the evolutions of the other
post-5.9 weapons); the crowd's roar feeding his Rage (`CrowdExcitement` as a third source); the Spent beat with a pose
that looks at his hands; a "wade in" PlaytestBot mode for melee gladiators; Asterion seen chained through an open
trapdoor in the Colosseum before he's unlocked (R2's idea); challenges "Unchained 10 times in a run" and "beat Gorm
without the stone ever hitting".

## Verified / unverified
- **Verified (reading the code and arithmetic):** every existing field, method and property named above exists in
  today's code (read this session): `WeaponDef.tipLength`, `WeaponSystem.Hit` / `Strike` / `Hold` / `AmountOf` /
  `AreaOf`, `PlayerProjectiles.Orbiter` / `Shockwave` / `MaxOrbiters 16`, `EnemyManager.IsBlocking` (it tests the push
  direction: a push 70.7° off the outward line gives cos 0.33, under every shield's threshold, 0.42–0.57 for arcs of
  110–130°, while a shield facing the chain's approach gives 0.94 and blocks), `PlayerMotor.KnockbackTakenMul` /
  `HorizontalVelocity`, `PlayerPartAnimator`'s twist constant 0.2 and its ±80° clamp, `Hud.DrawBlood`'s shared bar,
  `BossFight`'s unscaled `_t` and `fallSeconds 1.8`, the announcer replacing a line, `LanistaTablet.MaxTraits 3`,
  `RosterGrid` fitting 16, `MetaSetup` finding every `UnlockDef`, `-gladiator <id>` in `MainMenu`; the areas, rates and
  speeds in the table above.
- **Unverified:** everything about feel in play (the sweep's speed and weight, whether the sideways push reads as a
  sweep at 300 enemies, Gore's toss strength, the Rage rates and Unchained's uptime, Spent's cost, the whirl's spin of the
  whole model); performance of a 512-entry query per frame during sweeps at 450 alive (expected well under the 4 ms
  budget: one disc query, as `ArcHit`); every Unity sign (bearings, the arm's mirrored yaw, the spin direction) and pose;
  the model's look (the art part). These need the orchestrator's bot runs and the producer's playtest.
