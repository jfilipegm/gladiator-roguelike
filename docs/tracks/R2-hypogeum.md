# R2 hypogeum — notes

Branch `claude/r2-hypogeum-8f1829` (from `phase5-colosseum` at 2c22c20). Code only, no Unity: `Tools/ccheck` errors=0
(all six assemblies, no warnings). Rules and numbers: `docs/ARENAS.md` §6, `docs/BOSSES.md` "The Eternal Victor" ("The
Colosseum through the fight"), `docs/tracks/R2-colosseum.md` (the contract), `docs/tracks/R2-design.md` § "Hypogeum".
No stubs: everything compiles against today's `BossController` (no `R2-STUB` marks needed).

## What I built
| File | What |
|---|---|
| `Assets/Scripts/Gameplay/Arena/Hypogeum.cs` (new) | The floor that opens: the drop schedule, the 30 doors (telegraph, open, close, held), falls, throwing the player out, the 8 lifts and their packs, the stage's lift following the boss's rise, the boss's phases, the light. `ITickable`, `TickGroup.Enemies` order 33 (after `ChariotRace`'s 32). |
| `Assets/Scripts/Gameplay/Arena/ArenaLayoutDef.cs` | `swarmMul` (default 1) under "Play area"; header "Hypogeum (R2)": `trapdoors`, `trapdoorSize`, `trapdoorModel`, `lifts`, `liftSize`, `liftModel`, `hasCenterLift`, `centerLift`, `centerLiftScale`, `dropFirst`, `dropEvery`, `dropTelegraph`, `dropOpen`, `liftFirst`, `liftEvery`, `liftsPerWave`, `liftMinDistance`, `liftPacks`; the nested `struct LiftPack { EnemyDef enemy; float from, to, weight; Vector2Int count; int maxAlive; string call; }`. Arrays default empty (no hypogeum); the clocks default to the Colosseum's numbers. |
| `Assets/Scripts/Core/Spatial/StaticObstacles.cs` | Walk-only boxes: `public int AddBox(Vector2 centre, float yawDeg, Vector2 half, bool active, bool walkOnly)` (grows the arrays at load), `public bool WalkOnly(int i)`. `PushOut` pushes out of them (when on); `Overlaps` skips them; `ToArrays` leaves them out; `FindBox` skips them (a barricade's lookup must never find a trapdoor). |
| `Assets/Scripts/Gameplay/Director/Director.cs` | `public float swarmMultiplier = 1f` (≤ 0 reads as 1), applied in `BeginSwarm` and in `ComputePoolTargets`' swarm term; `public int AliveCap` (the private `HardCap`, read by the lift packs). The R1 escort prewarm (`_escortIndex`) is untouched. |
| `Assets/Editor/Setup/ArenaBuilder.cs` | `PlaceHypogeum` **before the nav bake** (so the height grid samples y 0 over every hole) and `AddHypogeum` under `[Arena]` after the chariot races; `director.swarmMultiplier = layout.swarmMul`. |
| `Assets/Tests/EditMode/HypogeumMathTests.cs` (new) | `InRect`, `OutOfRect`, `PickGroup` (the contract's 30 doors), no allocation, walk-only boxes, the swarm multiplier and `AliveCap`. |
| `Assets/Tests/PlayMode/HypogeumTests.cs` (new) | The Colosseum scene: drops, falls, the player, the schedule, Halt, lift waves and caps, a sinking lift, held doors, the boss's phases and fall. |

## The builder hook
When the layout has trapdoors, lifts or a centre lift: an `Environment/Hypogeum` holder with
- each trapdoor's model (`trapdoorModel`, decor) at (x, 0, z), yaw from the layout, named `Trapdoor<d>`; `LeafF` / `LeafB`
  lose their static flags (static batching would freeze them); the frame stays batched;
- each lift's model at its spot, yaw 0, named `Lift<l>`; `Platform` non-static;
- the centre lift at `centerLift`, scaled `centerLiftScale`, named `CenterLift`;
- under each, a **`TrapFloor`**: a `BoxCollider` (8 × 0.2 × 4 for a door, 4 × 0.2 × 4 for a lift, 5 × 0.2 × 5 world for
  the centre lift), its top at the model's origin, Environment layer, `WalkableSurface` (feeds the height grid, never an
  obstacle). The player walks on it over an open door; the nav bake samples the floor there (and 1.2 over the stage's
  hole, where the boss's rise lands).
- A missing model still gets an (empty) stand-in at the right spot, so the floor box lands over its hole.
- Then `[Arena]/Hypogeum` with every field copied (`liftSpots` = `layout.lifts`, `centerLiftSpot` = `layout.centerLift`,
  the doors/lifts/centre transforms, the clocks, `liftPacks`).

## How it works
- **Doors** (`Closed → Telegraph → Open → Closing → Closed`, plus `Held`): the telegraph is one
  `TelegraphRenderer.Lane` per door (from centre − axisX × 4 along axisX, 8 × 4, filling over `dropTelegraph` 2 s) while
  the leaves bump up (rattle, ≤ 3°, only within 60 m) and a little dust puffs; then the leaves swing down 100° in 0.35 s
  (accelerating), three dust bursts, the hole's rim stays lit (four 0.15 m lanes at alpha × 0.35, only within 60 m of the
  player); after `dropOpen` 4 s they close in 0.6 s (`SfxId.Slam` near the player). `SfxId.Rattle` once per group drop.
  Leaves: `LeafF` turns by −angle about the door's world X axis, `LeafB` by +angle (both drop their free edge at z = 0
  into the shaft); transforms are written only when the angle changes.
- **Falls** (every frame, every Open or Held door): `QueryAlive(centre, 4.47)`, the enemy's centre inside the rect and
  its feet within `feetClear` of the floor, once per opening (per-slot serial + uid, like `FireJets`). Scripted (a boss) →
  `ApplyDamage(500, Area, up, no knockback)`, counted only if his HP dropped (a roaring boss is tried again next frame);
  an elite → 300 and a 4 m/s push across toward the nearer long edge (sent from above when its shield would block it);
  otherwise `Execute(i, Hypogeum.SourceId)` (= `GameInfo.StableId("hypogeum")`, the enemies part's `FallSourceId`).
  A group whose doors swallow 10 → `CrowdExcitement.Spike(0.4)` once.
- **The player** (never falls: the `TrapFloor` holds): centre inside the rect and feet within `feetClear` 0.35. Open: once
  per opening, 22 (`Area`, × the arena's 1.55 in `PlayerHealth`) and an impulse 12 across toward the nearer long edge
  + 5 up (the push happens even if the hit is absorbed by invulnerability). Held: a push every 0.35 s, 12 at most every
  1 s. Feet above `feetClear` inside the rect at the moment a door opens → `Jumped++`.
- **Lifts** (`Idle → Telegraph 1.5 → Sinking 0.5 → Rising 0.9 → Idle`): the square fills orange-red; the `Platform` goes
  to −3 and back (eased up); at the top the pack steps off in a ring of r 1.2 (heights sampled by `Spawn`), a dust burst.
  A grounded player on it as it starts sinking: 10 and pushed off toward the nearest edge. A visual raise (the boss's
  guards) skips the telegraph, the spawn, and a lift the player stands on.
- **Waves** (scene game time): at `liftFirst + k × liftEvery`, the `liftsPerWave` idle lifts nearest the player at
  ≥ `liftMinDistance`; each picks a pack weighted among those with `from ≤ run time ≤ to` (`to` 0 = the end),
  `AliveOf + (on their way) + count.y ≤ maxAlive`, and the arena under its cap (`Director.AliveCap`; alive + on their way
  + count.x); no pack → that lift stays down. The count is re-clamped to both caps when the platform arrives. The wave's
  first pack's `call` (`AnnounceKind.Line`, orange-red, 1.8 s), `SfxId.Horn`, `CrowdExcitement.Spike(0.5)`.
- **Drops** (scene game time): at `dropFirst + k × dropEvery`, `PickGroup(trapdoors, player, LastGroup)`.
- **The boss** (`BossController.Active`; no call from him): the first tick that sees him stops the run schedule and
  empties lifts already on their way (the horde was dragged off). While `Entering`: every closed door rattles; the
  stage's `Platform` moves (7.5 m/s) toward clamp(`Position.y − 1.2`, −3.75, 0) while he's inside its 5 × 5 square and
  below its top (so it sinks to meet him, then rides up with him; a gate entrance never moves it); a player on it is
  pushed off every 0.35 s (no damage). Fighting: a drop near the player every 12 s (phase 1) / 8 s (phase 2). `Phase`
  reaches 2 → `OpenAll()` and dusk; reaches 3 → `OpenForGood()` (held: boxes on) and last light, no more drops.
  `GuardWaves` goes up → a visual raise at the `def.guardSpotsUsed` lifts nearest the player (the same spots
  `CallGuards` picks: the layout's `guardSpots` are the lifts). `Defeated` → `CloseAll()` (door k after k × 0.07 s,
  boxes off), the day back, halt; from then on it ticks on **unscaled** time (the fall's slow motion and the reward or
  summary pause would otherwise freeze the doors half shut).
- **Light**: the first tick caches `RenderSettings.sun` (else the first directional light), its colour and intensity;
  `SetLight` lerps (smoothstep, `lightLerp` 3 s) to dusk (1, 0.62, 0.42) × 0.85, last light (0.95, 0.48, 0.36) × 0.72,
  or back to the day.
- **Walk-only boxes**: added on the first tick that has `EnemyManager.Obstacles`, half (4.05, 2.05), off; on at `Held`,
  off at close.
- **Prewarm**: after the Director's prewarm (or at once if it's off), each pack's enemy is filled to
  min(maxAlive, count.y × liftsPerWave × 2) + 4 views, 8 per frame, during the intro: the War Boar isn't in the
  Colosseum's table, so without this its pool would grow mid-run (R1's lesson).
- **No allocation per frame**: arrays sized in `Configure` / on the first tick; strings are the layout's constants.

## Public API (for the bot, the enemies part, tests)
`Instance`, `SourceId`; `Count`, `LiftCount`, `GroupCount`; `IsTelegraphing(d)`, `IsOpen(d)` (Open or Held), `IsHeld(d)`,
`IsClosing(d)`, `IsClosed(d)`, `GroupOf(d)`, `LastGroup`; `DoorCenter(d)`, `DoorAlong(d)`, `DoorAcross(d)`,
`InDoor(d, p, pad)`, `DoorAt(p, pad)` (−1 if none), `OutOfDoor(d, p)`; `LiftCenter(l)`, `LiftBusy(l)`,
`LiftTelegraphing(l)`, `LiftOffset(l)`, `InLift(l, p, pad)`, `CenterLiftOffset`; `Drop(group)`, `DropDoor(d)`,
`OpenAll()`, `OpenForGood()`, `CloseAll()`, `RaiseLift(l, def, count)` (bool; def null = visual), `StartWaveNow()`
(bool), `Halt()`, `Halted`; `NearestDoor(p)`, `NearestLift(p, minDistance)`, `Danger(p)` (s until a door or lift under
p, padded 0.35, opens or moves; 0 if open/held or moving; +∞ if none); `Clock`, `NextDropAt`, `NextWaveAt`, `LightStage`,
`Sun`; counters `Drops`, `Fallen`, `ElitesHurt`, `BossHits`, `PlayerHits`, `Jumped`, `LiftWaves`, `LiftsRaised`,
`Released`; statics `InRect`, `OutOfRect`, `PickGroup`; `Configure()` (re-read the fields; tests).

## Decisions (one-line reasons)
1. **Field names `liftSpots` / `centerLiftSpot` on `Hypogeum`** (the design named both the positions and the transforms
   `lifts` / `centerLift`): one field can't be both; the transforms keep the design's names, the layout keeps its own.
2. **`Halt()` also stops the boss fight's periodic drops** (his phase events still open the floor and raise lifts):
   "no more scheduled drops" should mean it everywhere, and the boss tests need a quiet floor.
3. **After his fall it ticks on unscaled time**: 30 × 0.07 + 0.6 = 2.7 s of closing would take 8 s of real time in the
   0.25 slow motion, and the reward/summary pause would leave doors half shut.
4. **The stage's platform moves at 7.5 m/s toward its target instead of snapping**: `BeginRise` starts him 3 m down at
   once, so a snap would teleport the deck; this way it sinks to meet him (0.4 s), then rides up with him.
5. **It follows him only inside the 5 × 5 square**: with the gate fallback (`entranceRise` off) his y = 0 < 1.2 would
   otherwise sink the stage's deck.
6. **The pack cap counts the lifts already on their way, and the count is re-clamped on arrival**: three lifts of one
   wave could otherwise each pick the last Chimera slot, and the horde changes in the 2.9 s a lift takes.
7. **`Director.AliveCap`** (a public read of `HardCap`): the packs must respect the arena's 450 and the cap is private;
   it's my file.
8. **Pack prewarm after the Director's**: two prewarms at once would double the per-frame hitch of the intro.
9. **`FindBox` skips walk-only boxes**: `ArenaBreakables` finds its barricade's box by "nearest within 1 m"; a trapdoor
   must never be that box.
10. **A boss is hit only with his feet down** (`feetClear`): a leap across an open door shouldn't cost him 500 mid-air;
    the landing does if he lands in it.
11. **Rims, dust and the rattle only within 60 m** (`drawHeldWithin`, for Open doors too, not just Held): phase 2 opens
    all 30 at once and 120 rim quads plus 30 telegraphs would eat a third of the 512 decals; past 60 m they don't read.
12. **The rattle only bumps the leaves up** (never below the deck): "something underneath is pushing".
13. **An elite killed by its 300 counts in `Fallen`** too: the bot's "fallen" means "swallowed by the floor".
14. **The player is pushed out even if the hit is absorbed** (invulnerability): the rule is "never stand in a hole".
15. **Lifts on their way when the boss arrives come up empty**: the horde was just dragged off and the Director is off.
16. **The wave's call is orange-red** like "CHARIOTS!": it announces danger.

## Numbers (defaults on `Hypogeum`; the clocks come from the layout)
Doors: telegraph 2, open 4, close 0.6, leaves 100° in 0.35 s, rattle 3° at 9 Hz. Hits: player 22 (+ push 12 / 5 up),
held 12 per ≥ 1 s (push every 0.35 s), elite 300 (push 4), boss 500, feet clear 0.35. Lifts: telegraph 1.5, sink 0.5,
rise 0.9, depth 3, player 10, ring 1.2. Boss: drops every 12 / 8 s, all open at phase 2, held at phase 3, close stagger
0.07. Look: rim alpha 0.35, width 0.15, draw within 60 m. Light: dusk (1, 0.62, 0.42) × 0.85, last light
(0.95, 0.48, 0.36) × 0.72, 3 s. Feedback: wave spike 0.5, 10 swallowed → spike 0.4.

## Tests (written, not run: no Unity here)
- **EditMode `HypogeumMathTests`**: `InRect` at yaw 0, 76.4 and −23.5 (centre, 3.9 along, 1.9 across, a corner inside;
  4.1 along, 2.1 across outside; the pad); `OutOfRect` across toward the nearer long edge, unit, flat, a tall rectangle
  crossed along X; `PickGroup` on the contract's 30 doors (nearest group; another when it was last; none; one group);
  no GC allocation (`Is.Not.AllocatingGCMemory`); a walk-only box (off: no push; on: pushed out the shortest way; shots
  pass; FindBox skips it; not baked; a solid `AddBox` blocks shots and is baked); the swarm multiplier (+½ swarm ×1.1 on
  the swarm type's pool, a non-swarm type unchanged, 0 reads as 1) and `AliveCap` (450).
- **PlayMode `HypogeumTests`** (needs `Assets/Scenes/Arenas/Colosseum.unity` and `AL_Colosseum` from the layout part;
  the boss test needs the boss part, the false death the enemies part — run after all four are in): `Drop(5)` telegraphs
  2 s (only group 5, nothing open, `Danger` < 0.5), opens at 2 s, still open at 5.8, all shut by 6.7; a Convict on a
  door falls, an elite Brute (Fast) loses 300 once and lives, a Convict 1 m beside it is untouched; a Convict walking in
  while it's open falls; a grounded player is hit once (22 × 1.55) and is off within 0.5 s, one held in the air as it
  opens isn't hit (`Jumped` 1); the schedule (first drop at 40 ± 0.3 s of scene time, group 5 from (25, 2), the next
  14 s later, group 0); `Halt()` stops drops and waves; at 5:00 from (34, −14) a wave raises L0, L7, L4 (not L3, 6 m away),
  everything that comes up is from a pack active at 5:00 (no Curs), and a full arena cap stops the next wave; a lift
  sinking under the player hits once (10 × 1.55) and throws them off, and a visual raise skips their lift; held doors
  (`OpenForGood`): a chasing Convict never steps in, an arrow crosses, a player stepping in again and again is thrown
  out each time and hurt at most once a second; the boss: the stage's platform sinks to meet his rise and comes back up
  (checked only if he started below the stage), phase 2 opens all 30 together (dusk, the sun at × 0.85 after 3 s, his
  guards raise lifts), phase 3 holds all 30 (last light, boxes on, no more drops), his fall closes them all within 3 s
  of real time (the day back, halted, boxes off).

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings (Core, Gameplay, UI, Editor, both test assemblies); the
  `PickGroup` expectations in the tests (computed with a throwaway script from the contract's table).
- **Unverified (needs the orchestrator's Unity):** every test; the leaves' swing sign (−angle for `LeafF`, +angle for
  `LeafB` about the door's world X: depends on env-art's pivots and axes); that the `TrapFloor` boxes and the floor's
  holes line up; how far the 12 m/s push throws the player (the test wants them off within 0.5 s); the decal budget at
  phase 2 with the horde's telegraphs; enemies at a held door's long side (Q1: push-out may park one at the middle of
  the edge; the test only checks it stays out); performance with 30 held doors (30 `QueryAlive` per frame).

## Needs from others
- **Layout and setup:** fill the hypogeum fields and `swarmMul` in `AL_Colosseum` (`lifts` = `guardSpots` = the 8
  lifts, `hasCenterLift`, the packs with `call` strings). The bot: `Hypogeum.Instance.Danger(p) < 2.5` → steer along
  `OutOfDoor(DoorAt(p, 0.35), p)` (or off the lift), treat `IsHeld` doors as walls; the log line reads `Drops`, `Fallen`,
  `PlayerHits`, `Jumped`, `BossHits`, `LiftWaves`, `LiftsRaised`, `Released`.
- **Enemies:** `FallSourceId` must equal `Hypogeum.SourceId` (both `GameInfo.StableId("hypogeum")`). A pile on an open
  door can use `Hypogeum.Instance.DoorAt(pos, 0f)` + `IsOpen(d)`.
- **Boss:** nothing to call. `BeginRise` must set `Active`, keep `CurrentState == Entering` through the rise and keep
  `Position` inside the stage lift's 5 × 5 square, below 1.2 until he's up (the platform follows that).
- **Env-art:** `LeafF` / `LeafB` pivots at local z = ±2 on the long edges, `Platform` pivot at the deck's top centre (as
  the contract says): the code turns them about the door's world X and moves the platform in world y.
- **Orchestrator:** Unity `.meta` files for `Hypogeum.cs` and the two tests; rebuild the Colosseum after the layout part
  so the `TrapFloor` boxes are in the nav bake.

## Open questions (my recommendation first)
- **The guards' visual raise:** the boss spawns his guards on the lifts at once, then the platform sinks and rises under
  them (the design's order). Alternative: snap the platform to −3 and only rise (they "come up" with it). Keep the
  design's until it's seen in play.
- **Q1 (enemies at a held door's edge):** unchanged from the design: walk-only boxes; revisit only if it looks bad.
