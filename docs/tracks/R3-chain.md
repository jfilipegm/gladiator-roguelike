# R3 chain — notes (Asterion, the Chained: the `Sweep` pattern)

**Branch:** `claude/vigorous-haslett-c72b33` (fast-forwarded to `origin/phase5-asterion` at 2823019). Code and tests
only; no Unity, no Blender. Built to `docs/tracks/R3-design.md` § "For each code part" → "Chain" and decisions 5–13.

## Files
| File | What changed |
|---|---|
| `Assets/Scripts/Gameplay/Weapons/WeaponDef.cs` | `WeaponPattern.Sweep` appended after `Lob` (index 10); header "Sweep (the Labyrinth Chain, R3)": `sweepTime` 0.3, `whirlTimeMul` 1.5, `sweepOutward` 0.35, `tipKnockbackMul` 1.6, `linkMesh`, `linkLength` 0.6, `chainHeight` (1.05, 0.55), `weightOffset` 0.33, `modelPart` ""; tooltips of `range`, `amount`, `tipLength` and the doc comments mention the Sweep. |
| `Assets/Scripts/Gameplay/Weapons/WeaponSystem.cs` | `WeaponInstance` sweep state (reset in `Init`); `WhirlSweeps`; counters `Sweeps`, `Whirls`, `SweepHits`, `WeightHits`; `_sweepHits[512]`; the timeline hooks; `Animate`, `BeginSweep`, `StepSweep`, `StopSweep`, `SweepStrikes`, `DrawChain`, the model part (`HidePart` / `ShowPart` / `FindPart`); the three pure statics `SweepBearing`, `SweepCovers`, `SweepPush`; two inspector fields `chainHandOffset` 0.45 and `stoneTumble` 360. |
| `Assets/Scripts/Gameplay/Weapons/PlayerProjectiles.cs` | `MaxOrbiters` 16 → 40; an `Orbiter` overload with a per-axis scale (the links are stretched along Z to fit exactly); the float overload calls it, so the flail is drawn as before. |
| `Assets/Scripts/Gameplay/Animation/PlayerPartAnimator.cs` | `AttackMirror`, `AttackSpin`, `ScaleBoost` (1), `ExtraLean`, `HeadDrop`; the idle head lift; the twist uses `profile.bodyTwistScale`. |
| `Assets/Scripts/Gameplay/Animation/PartAnimProfile.cs` | `bodyTwistScale` 0.2 (today's constant), `idleHeadLift` 0, `idleHeadDelay` 1.2. |
| `Assets/Tests/EditMode/SweepMathTests.cs` | new (8 tests) |
| `Assets/Tests/PlayMode/ChainSweepTests.cs` | new (11 tests) |

## How it works
- **Timeline:** a Sweep weapon runs the usual acquire → wind-up → impact (`StartSwing` plays `Swing`). `BurstLeft` stays 0
  (it isn't in the melee list) and it has no echo. The impact sets `SweepLeft = Amount − 1` and calls `BeginSweep`.
  `StepSweep` runs every frame while `SweepT >= 0`, before the swing block; no new swing starts while `SweepT >= 0`, and
  the cooldown counts down meanwhile (at L8 the cooldown waits for the sweeps, as the design's numbers say).
- **`BeginSweep`:** `SweepWhirl = WhirlSweeps`; half = 180 or `arcDegrees / 2`; from `+half × SweepDir` to
  `−half × SweepDir`; `SweepId++`; hides the model part; `Rattle`; `Sweeps++` (`Whirls++`).
- **`StepSweep`:** `u = SweepT / (sweepTime × whirlTimeMul?)`; `cur = lerp(from, to, u)`; hits what the chain passed in
  `[SweepPrev, cur]`; draws the chain at `cur`. At `u >= 1`: `SweepDir` flips; a backswing re-aims at the nearest within
  `acquireRange × Area` (keeps the aim if none) and begins at once; otherwise `SweepT = −1` and the model part is shown.
- **Hits (`SweepStrikes`):** exactly the design's rule (reach = range × Area, tip = tipLength × √Area, the pad
  `asin((r + width/2) / max(dist, r + width/2))`, `SweepSeen[slot] == SweepId` skips, `stone = dist >= reach − tip` →
  `forceCrit` and knockback × `tipKnockbackMul`, push `SweepPush(to/dist, sign(cur − prev), sweepOutward)`, radial for a
  Scripted enemy), then `Strike` (on-hit status, Boon procs). An enemy at the player's exact centre uses the chain's
  bearing.
- **Drawing (`DrawChain`):** hand at `origin + up × chainHeight.x + dir × 0.45`, stone at `origin + dir × reach + up ×
  chainHeight.y`; `ceil((|stone − hand| − weightOffset) / linkLength)` links laid end to end, each rolled 90° from the
  last and stretched along Z so the last ends `weightOffset` short of the stone's centre; the stone at `meshScale × √Area`,
  tumbling about the chain's line, with the orbit streak along the tangent. A null mesh draws nothing.
- **Animator (`Animate`):** the first weapon that's swinging or sweeping drives it. During a sweep `AttackT = w0 + (w1 −
  w0) × u` (w0 = windup / swingDuration, w1 = (windup + sweepTime) / swingDuration), `AttackSpin = cur − SweepFrom` for a
  whirl; otherwise `AttackT = SwingT`, spin 0. Mirror and spin are written every frame (false / 0 for every other
  weapon and when nothing swings), so they can't be left on.
- **Halted (`Hold`):** `StopSweep` (no more hits, no backswings, the part shown), the animator's mirror and spin off.

## Decisions (one-line reasons)
1. **The mirror is set at the swing's start and at each sweep, and kept until the swing ends** (the design said "after
   the last sweep: mirror false"). With Amount 1, every other trigger sweeps left to right; the swing runs on 0.18 s
   after the sweep (0.62 of 0.8 s), and turning the mirror off then would snap the arm from +120° to −120°. The wind-up
   is mirrored too, so the pull goes to the side the sweep starts from. When nothing swings the mirror is off.
2. **The backswing re-aims within `acquireRange × Area`** (5.6 m), like `StartSwing` ("within reach" in the design).
3. **The stone faces along the chain's line (hand → stone, ~6° down), not the flat bearing,** so its ring bolt meets the
   last link; it tumbles about that line (360°/s, `stoneTumble`) so the bolt stays on the chain.
4. **The last frame of the last sweep isn't drawn:** the model's `Chain` part comes back that frame, so both aren't
   shown at once.
5. **A per-axis `Orbiter` overload in `PlayerProjectiles`** (my file): "stretched along Z to fit exactly" needs a
   non-uniform scale; the float overload forwards to it, so existing callers draw exactly as before.
6. **The query is `QueryAlive(origin, reach)`, not `reach + MaxRadius`:** `QueryAlive` already includes each enemy's
   radius, and the filter is `dist <= reach + r`, so the result is the same with a smaller query.
7. **`RemoveAll`, a loan running out and `EndTemporary` show the model part** (a part hidden mid-sweep would otherwise
   stay hidden); `Init` shows it too if a slot is reused mid-sweep. `Evolve` re-registers the link mesh.
8. **Idle head lift only when standing still and not attacking, and a trait's `HeadDrop` cancels it** (Spent wins over
   the sky).
9. **`ScaleBoost` is eased by the pose blend into a separate value** multiplied onto the squash/stretch (the pose's own
   `RootScale` is overwritten by the unsmoothed squash each frame).

## Tests (written; the orchestrator runs them)
- **EditMode `SweepMathTests`:** Sweep is index 10 (Lob still 9); WeaponDef's and PartAnimProfile's new defaults;
  `SweepCovers` (the design's cases, plus "already past", a zero-length frame, both wrap directions, past the pad); a whole
  whirl covers every bearing both ways; `SweepPush` unit, perpendicular with no mix, flat, flips with the sign, points
  right when turning right, the outward part `0.35/√(1 + 0.35²)` with the mix; `SweepBearing` ±90, height ignored, ±180,
  relative to the aim, and agrees with `Quaternion.Euler(0, b, 0)` (the drawing).
- **PlayMode `ChainSweepTests`** (TestArena, the Nobody alone with a chain `WeaponDef` made in the test with the setup's
  numbers and the art's meshes when imported, rooted 1000-HP dummies, no Director, `LevelUpController` off, obstacles
  off, god mode): hits once a sweep, not at 6.5 m or behind; ±100° hit in sweep order ~0.25 s apart and the next trigger
  comes back; the stone (4.5 m) crits and shoves ≥ 1.3× more than the inner chain (2.5 m), `WeightHits` 1; the push is
  sideways (tangent > 1.5 × outward, outward > 0); Amount 2: one swing, the backswing begins `sweepTime` ± 0.1 s after
  the first sweep, order right, left, left, right; a Shield Thug facing the player (verified it would block a radial
  push) takes it unblocked in full; a Scripted dummy with its shield raised facing the player blocks (×0.25); a whirl hits
  8 enemies round him once each in ~0.45 s, the model spins > 180° and `AttackSpin` is 0 after; a `Chain` child under
  the visual root is shown through the wind-up, hidden during the sweep and shown after; killing the player mid-sweep
  stops it (no more hits, part shown, spin 0; real-time waits past the death beat); 5 s of sweeps (Amount 3, cooldown
  0.4, whirls toggled every 0.7 s) among 200 enemies: zero heap growth in `WeaponSystem`, `PlayerProjectiles` and
  `PlayerPartAnimator` (GameLoop's per-system tracking, after a 2 s warm-up).
- **GC measure:** the design asked for ProfilerRecorder "GC Allocated In Frame", but in the Editor that counter includes
  the Editor's and the test runner's allocations (`FrameStatsRecorder` says so), so it would never read 0 there. The
  test uses `GameLoop.TrackHeapPerSystem` / `HeapReport()` instead (the heap's used size around each system's tick,
  as RosterTests measures the menu), and prints the whole report on failure. Hit events are raised inside the weapons'
  tick, so their subscribers (damage numbers, audio, crowd) count toward `WeaponSystem`: if it fails, read the report
  before blaming the sweep. The test unsubscribes its own hit log first for that reason.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings, all six assemblies. The names the rage and setup parts code against
  match their spellings and types (`r3-setup.md` in the shared notes: `linkMesh` Mesh, `chainHeight` Vector2,
  `modelPart` string, `Sweeps` int; `WhirlSweeps` bool; `ScaleBoost` / `ExtraLean` / `HeadDrop` float). Existing weapons:
  the only shared paths changed are `Tick`'s animator hand-off (identical for them: mirror false, spin 0), `Hold`
  (`StopSweep` is a no-op for them), `RemoveAll`/`EndTemporary` (show a null part), the twist constant (now the profile's
  0.2 default) and the orbiter scale (same matrix for a uniform scale).
- **Unverified (needs the Editor):** every test above (none has run); the signs in play (the arm's mirrored yaw against
  the chain, the spin direction against the bearing, the twist with `bodyTwistScale` 0.5); the chain's look (the link
  roll, the stone's facing and tumble, the streak); the frame-timing tolerances (±0.06 s on 0.25 s, ±0.08 s on 0.45 s,
  0.1 s on the backswing) at the Editor's frame rate; the GC test's other subscribers. Feel: everything.

## Needs from others
- **None for the code.** The orchestrator: run `SweepMathTests` and `ChainSweepTests` (the meshes are loaded from
  `Assets/Art/Models/Weapons/WP_LabyrinthChain.fbx` when imported; without them the tests run with no chain drawn).
- **Follow-ups (not R3-chain):** two Sweep weapons at once (Asterion's chain plus a loaned chain) share the one `Chain`
  part, so the first to finish shows it while the other still swings. Only possible if the crowd's loan could be the
  chain; harmless otherwise.
