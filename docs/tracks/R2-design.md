# R2 design — notes (the Colosseum + the Eternal Victor)

Branch `claude/r2-design-d494b0` (cut from `phase5-imperial-arena` at e699589). Docs only: no code, no Blender, no Unity.

**Written:** `docs/ARENAS.md` § "6. The Colosseum (R2, planned)" (shape and size check, layout, the trapdoors and the
lifts with every number, gates, stands, the two new enemies, the spawn table, the lift packs, difficulty, pressure,
Renown, chests and altars, light, tests); `docs/BOSSES.md` § "The Eternal Victor" (the 10-line spec, attacks and phases
with numbers, the Colosseum through the fight, reused vs new); `docs/tracks/R2-colosseum.md` (the **draft** contract:
shared facts, every model with its budget, ids, the parts and the files each owns); this file (decisions, open
questions, and the fields, methods and tests for each code part).

**Built on (read in this worktree):** `ArenaLayoutDef`, `ArenaBuilder` (gates, solid walls, the boss fight),
`SpikeTraps` / `FireJets` / `BladeSweepers` / `BeastCages` / `ChariotRace` (the hazard pattern: central tick, scene
time, per-enemy serials, hurt both sides, a quarter or a flat amount to the boss), `ArenaBreakables` (obstacles switched
off at runtime), `StaticObstacles` (circles + boxes, linear `PushOut`, `Overlaps` used by projectiles, gifts and
placement), `HeightGrid` (baked, writable but not needed), `BossDef` / `BossController` (three phases, guard spots,
`guardsFromPhase`, the public `Phase`, `Defeated`, `CurrentState`, `Position`, `GuardWaves`), `BossFight` (the gate
entrance, the Director switched off for the fight), `EnemyDef` / `EnemyManager` (the behaviours, `Kill`, `Execute`,
`QueryAlive`, statuses with `HasStatus(Burn)`, the War Boar's trample serials), `Director` (swarm size ×
`densityMultiplier` only), `TelegraphRenderer` (`Circle`, `Lane`, `Sector`, 512 quads), `MovementConfig` (jump 1.7 m,
gravity 30, fall ×1.35, run 7.5 m/s), the palette index, R1's contract and notes.

**Geometry script:** every coordinate, area and clearance below was computed by a throwaway script, saved in the shared
notes folder as `r2_colosseum_geo.py` (run `python3 r2_colosseum_geo.py`; env-art and layout can use it to check
their numbers).

## Decisions (one-line reasons)
Where I changed CLAUDE.md's Colosseum (§6.2 row 6, §6.3 Tier 4) it says **(changed)**.

### The arena
1. **The great oval, 98 × 67 (196 × 134 m), play area 20,005 m², 1.31× the Imperial Arena (changed: §6.2 says ~140 m,
   stale since the Gate 2 rebuild).** The ladder's last two steps were ×1.36 and ×1.31; 17,500 would be ×1.14, the
   smallest step of the climb for the biggest arena.
2. **An oval, though the rule invites other shapes.** The rule is "not *only* circles and ovals" and every other arena
   since the first is something else; the ladder ends on the shape everyone knows, mirroring the oval pit it started
   in. The new geometry is the floor's: the trapdoor pattern, and a third phase that leaves a broken ring of chasms.
3. **The floor's pattern is the real hypogeum's: a central axis and a curved passage.** A broken ring of 24 doors
   (centre line 62 × 42) and 3 doors either side of the stage on the axis. Wherever you stand, a group is near you,
   and the boss's phase 3 turns the same pattern into chasms.
4. **Doors 8 × 4 m.** 4 m across is just under a running jump (0.63 s of air at 7.5 m/s = 4.7 m) and easy with a
   slide-jump (7.2 m): movement is the counter. 8 m along keeps the door count at 30.
5. **Drops every 14 s, the group with the door nearest you, never the same group twice running; 2 s telegraph, 4 s
   open.** On a 196 m floor a drop 100 m away is wasted; "nearest, never twice" stays relevant and predictable.
   ~31 drops a run.
6. **Falling kills ordinary enemies outright; elites take 300; the boss 500.** A pit is a pit (the readable rule);
   elites survive so they don't vanish without a fight; 500 makes luring the boss worth it (R1's chariots: 600).
7. **The horde doesn't avoid a temporary pit.** The 4 s horde-eater is the arena's tactic: lead them across.
8. **The player is thrown out, never falls in (changed: a real hypogeum would swallow you).** An invisible floor stays
   over every hole; a grounded player in an open door is hit once and knocked over the nearer long edge (R1's chariot
   rule). No CharacterController on moving colliders, no teleport out of a shaft, no camera in a shaft.
9. **Doors left open in phase 3 are walk-only boxes in `StaticObstacles`.** Enemies and the boss already push out of
   obstacles (no change to their steering); `Overlaps` (projectiles, gifts) skips them, so shots and the falx fly over
   chasms. Added once at load, switched on per door.
10. **Lifts raise packs in the middle of the floor (the "huge waves" from below).** On a 20,000 m² oval the edge
    stops being the only front. The 3 lifts nearest you, but none within 12 m, so nothing rises under you.
11. **Lift packs are a small table in the layout, not the spawn table.** Their own window, weight, count, cap and the
    announcer's call: the beasts and the dead each get a "who's coming" beat.
12. **Cap 450, density ×1.3, swarms ×1.5 (new `swarmMul`).** "Huge waves" inside §8 (~500 at peak); the Grand Arena held
    178 fps at ≥ 250 alive with a 400 cap (p95 7.8 ms). The open doors thin the horde. CONCERNS C5's instanced enemies
    (for 1,000+) aren't needed.
13. **`enemyHpMul 2.55`, `playerDamageTakenMul 1.55`.** The ladder's steps: HP +0.25, +0.25, +0.3, +0.35 → +0.4;
    damage +0.15, +0.10, +0.10, +0.10 → +0.10.
14. **Elites every 60 s,** as the last three arenas; the lifts add the heavies.
15. **The stage (r 10, 1.2 m) and four hills (2.2 m) are the vertical variety.** The stage is the duel's centre and the
    boss's lift; the hills are the hypogeum's scenery for a beast hunt, with slide slopes ≤ 20°.
16. **Stands 22 m tall, as six meshes along the curve.** "Multi-tier, huge arches": podium, three maeniana, a
    colonnade of arches; generated like the Amphitheater's cavea (no seams on the bend), split at the gates for culling.
17. **A new imperial box (`SM_ColPulvinar`), not R1's `SM_Pulvinar`.** R1's is an 8.4 m stand-run section and would
    leave a hole in a 22 m bowl; the new one sits on the podium in front of a continuous bowl. The Emperor attends.
18. **Crowd ~3,700** (1.7× the Imperial's ~2,200), thinning toward the top tiers (0.5 → 0.35 per metre per tier).
    "The loudest" at about 220k instanced tris; measure and thin maenianum III first.
19. **Late-afternoon light on a `sand_d` floor.** A mood no other arena has, and bone-white enemies read against a
    darker floor than the Imperial's pale sand.
20. **No new SFX** (R4/R5 will redo audio in code): the doors use `Rattle` then `Gate` / `Slam`, the waves `Horn`, a
    stomp `Splinter`.

### The enemies
21. **Two new enemies, not the seven of Tier 4 (changed).** The Skeletal Legionnaire (supernatural, the boss's own, one
    new rule: it reassembles) and the Chimera (the mythical beast, one new attack: the cone). Each adds one readable rule
    to an existing behaviour; the rest of the variety is reuse (Curs and Boars up the lifts, Centurions at the gates).
22. **No Minotaur (changed).** Asterion (R3) is the Minotaur gladiator; a common Minotaur would spend his silhouette.
23. **No harpies (changed; BACKLOG).** Flying enemies need a hit height for every weapon pattern, sit between the camera
    and the floor's telegraphs over a 450-strong horde, and fight the camera.
24. **No giants (changed).** A 5 m regular enemy would dwarf the final boss (3.5 m) and hide the horde behind it.
25. **"Multiple factions" = three ways in and three looks, not AI factions fighting each other.** The living through the
    gates, the beasts up the lifts, the dead from the ring and the lifts. The beasts hurt the others with existing or
    one-line code (the Boar's trample, the Chimera's fire ×3); nothing targets another faction.
26. **The dead take over the table as the run goes (0.8 → 6).** The arena changes during the run too: at 8:00 a third of
    the steady horde and over half of the swarms are dead men, and their champion is next.
27. **Reassembly is the Legionnaire's one rule; Burn, a stomp or a fall stops it.** Three counters the player already
    has (fire builds, movement, the arena). The first death isn't a kill (no OnKill, no XP), so kill counters, streaks,
    challenges and XP aren't doubled; the stomp or the second death is the kill.
28. **Piles are a list in `EnemyManager`, not dormant enemy slots.** A dormant slot would be auto-aimed at by weapons and
    counted by the Director; a 48-entry list drawn instanced (≤ 80 tris each) isn't. Past 48, they stay down.
29. **The pile's ring is bone-coloured, not orange-red.** It doesn't hurt you; orange-red stays "it hurts".
30. **The Chimera breathes a cone and burns the horde ×3.** The cone is the one telegraph shape no regular enemy has
    (`TelegraphRenderer.Sector` exists); burning the horde makes "lure it" a play, like the War Boar's charge.
31. **The Chimera: 2 alive, never an elite, slow to turn (160°/s).** It's elite-class already; an affix on a 1,071 HP
    beast would be a slog; slow turning makes flanking the counter.
32. **Fewer types in the table (12) than the Imperial Arena's (13); no Slinger, Fire-Pot, Mercenary, Beast Handler.**
    Readability at 450: the Chimera owns fire, the Archer owns range, the lifts own beasts.

### The boss
33. **He rises from the stage's lift, not through a gate (changed: every other boss walks in).** He's been underneath
    all along; the rattling doors and the lift are the "big entrance"; `bossGate` stays as the fallback.
34. **A gaunt thraex with a falx, not a disc, wings or a shield.** The Golden Idol owns the disc behind the head, the
    Emperor's Champion the wings; the hook-crest and the crescent blade read as one curve, and a scythe-like blade on a
    bone-limbed champion says "the dead's champion".
35. **No shield.** Two of the last three bosses blocked from the front; this fight is movement, the floor and the false
    death.
36. **One new attack, the falx throw (`BossAttack.Disc`); the rest is reuse.** Sweep, Flurry, Pounce and Whip (held =
    `whipSlow 0.1`) exist; the throw makes his signature part the weapon (like the Champion's javelins) and opens a
    window while his hands are empty. It can't be jumped (a spinning blade at chest height) and stops on stone.
37. **The false death (once: remains 1,500 HP for 8 s, else he rises with 20 %).** "Eternal" made literal, and the
    Legionnaires' rule, taught all run, applied to their champion. A chance to end it early, not a punishment: failing
    costs ~17 s more.
38. **Thresholds 65 % / 30 %, 22,000 HP.** Phase 1 long enough to learn the falx; phase 3 short and frantic on a broken
    floor; with the remains ≈ 100–140 s at the bot's measured damage (the Beast Lord's 15,000 fell in ~48 s).
39. **Phase 2 drops every door once; phase 3 leaves them all open.** P2's roar is the "everything opens" beat; P3 changes
    the floor's shape for good: a broken ring of chasms round the inner arena with 24 bridges, six on the axis.
40. **The arena's changes are `Hypogeum`'s, read from `BossController` (R1's decoupling, decision 21 there).** The guard
    spots are the lifts, so the existing guard code spawns his dead there; the hypogeum only animates the 2 lifts
    nearest the player when `GuardWaves` goes up. The boss never calls the hypogeum.
41. **Dusk, then last light, never below 0.72 of the day.** The Colosseum transforms in light too; telegraphs are
    emissive and enemy accents must still read.
42. **No flood (naumachia) for phase 3 (considered, rejected).** An opaque water plane over the floor hides every floor
    telegraph; a transparent one costs a new material and still dims them.
43. **No collapsing stands** (§12: environment destruction is out of scope): doors, lifts and light only.
44. **The guards are Legionnaires (6–8 at the 2 lifts nearest you, every 24 s from phase 2).** His own dead, rising
    where the beasts rose during the run.

## Open questions (for the orchestrator; my recommendation first)
- **Q1. Enemies at a chasm's edge in phase 3.** Seek + push-out has a dead spot at the middle of a door's long side
  when the player is straight across. (a) Keep the walk-only boxes: few guards, the boss leaps, the player moves
  (**recommended**); (b) if it looks bad in play, phase 3's doors drop back to back instead of staying open; (c) a flow
  field (§4 allows one only when a layout truly needs it).
- **Q2. The false death.** Keep it (**recommended**); if the boss part runs long, cut it (three phases remain).
- **Q3. `Director.cs`** (the swarm multiplier, ~3 lines). Give it to the hypogeum part (**recommended**: it owns
  `ArenaLayoutDef` and the builder hook that copies it) or the orchestrator lands it.
- **Q4. The invisible floor over the holes** (**recommended**) vs real falling (a teleport out of the shaft).
- **Q5. The crowd's hush at his rise.** `CrowdExcitement` can't silence the crowd today. Skip it in R2
  (**recommended**), or let the boss part own `CrowdExcitement.cs` for a `Hush(seconds)`.
- **Q6. The Codex.** Its bestiary stops at tier 2 (no Grand or Imperial entries). A BACKLOG line for the orchestrator
  (**recommended**), not R2's.
- **Q7. Elite Legionnaires reassemble with their affix** (**recommended**: one rule for all) vs elites stay down.
- **Q8. The lift cadence (45 s) and the packs** are first guesses: the bot's `lifts` counters tune them.
- **Q9. Chimeras: 2 alive** (**recommended**) or 3.
- **Q10. The rise vs the Gate of Death.** Rise (**recommended**); the gate is one field away if the rise looks wrong.

## Needs from others
- **env-art:** the floor's holes exactly as the contract's tables; `LeafF` / `LeafB` and `Platform` pivots; gate hinges
  like `SM_ImpGate`'s; the stand profile's numbers (the layout's crowd zones use them).
- **char-art:** `Falx` as a separate part with its pivot at the grip; the Chimera's part names; the 64 px lineup.
- **orchestrator:** Q1–Q10; create `phase5-colosseum` after R1 merges; BACKLOG lines (harpies, the Codex's tiers 3–5,
  the ideas below).

## Merge order
Hypogeum, enemies and boss don't depend on each other except **two names**: the boss calls
`EnemyManager.SetHpFloor` / `SetHp` (the enemies part's). If the enemies part isn't merged, the boss part adds them in a
marked stub file (`Assets/Scripts/Gameplay/Boss/R2Stubs.cs`, a `partial class EnemyManager`) that the orchestrator
deletes at integration, as R1 did. **Layout and setup goes last**: it sets the new fields and calls
`BossSetup.EnsureEternalVictor()`. Every PlayMode test that needs the Colosseum scene runs after all four are in.

## For each code part

### Hypogeum — `Hypogeum.cs` (new), `ArenaLayoutDef.cs`, `ArenaBuilder.cs`, `StaticObstacles.cs`, `Director.cs`
**`ArenaLayoutDef`** — a new header `"Hypogeum (R2)"`, plus `swarmMul` under "Play area":
| Field | Type | Colosseum value | Meaning |
|---|---|---|---|
| `trapdoors` | Vector4[] | the 30 (x, z, yaw, group) of the contract | door centres at y 0; yaw of the long side (local X); group 0–5 |
| `trapdoorSize` | Vector2 | (8, 4) | along local X, across local Z |
| `trapdoorModel` | string | `"Environment/SM_ColTrapdoor"` | needs `LeafF` / `LeafB` |
| `lifts` | Vector3[] | the 8 of the contract | centres (y = floor); also the boss's guard spots (the layout sets `guardSpots` to the same) |
| `liftSize` | float | 4 | square side |
| `liftModel` | string | `"Environment/SM_ColLift"` | needs `Platform` |
| `hasCenterLift`, `centerLift`, `centerLiftScale` | bool, Vector3, float | true, (0, 1.2, 0), 1.25 | the boss's lift in the stage |
| `dropFirst`, `dropEvery` | float | 40, 14 | seconds into the run of the first drop, then start to start |
| `dropTelegraph`, `dropOpen` | float | 2, 4 | |
| `liftFirst`, `liftEvery` | float | 90, 45 | the lift waves |
| `liftsPerWave`, `liftMinDistance` | int, float | 3, 12 | the nearest lifts at least this far from the player |
| `liftPacks` | `LiftPack[]` | ARENAS.md §6's table | `struct LiftPack { EnemyDef enemy; float from, to; float weight; Vector2Int count; int maxAlive; string call; }` (`to` 0 = the end; `maxAlive` 0 = no cap; `call` a constant string) |
| `swarmMul` | float | 1.5 | the Director's swarm sizes × this (default 1) |

**`Hypogeum : MonoBehaviour, ITickable`** (`Instance`; `TickGroup.Enemies`, order 33, after `ChariotRace`'s 32):
- **Fields** copied by `ArenaBuilder` (the table above, `doors` Transform[], `lifts` Transform[], `centerLift`
  Transform), plus tuning with defaults like `FireJets`: `closeTime 0.6`, `leafAngle 100`, `leafSwing 0.35`,
  `rattleDeg 3`, `playerDamage 22`, `playerKnockback 12` (+5 up), `feetClear 0.35` (grounded = feet within this of the
  floor), `heldPlayerDamage 12`, `heldHitEvery 1`, `heldPushEvery 0.35`, `eliteDamage 300`, `bossDamage 500`,
  `liftTelegraph 1.5`, `liftSink 0.5`, `liftRise 0.9`, `liftDepth 3`, `liftPlayerDamage 10`, `bossDropEvery (12, 8)`
  (phase 1, phase 2), `openAllOnPhase 2`, `holdOnPhase 3`, `rimAlpha 0.35`, `rimWidth 0.15`, `drawHeldWithin 60`,
  `duskColor (1.0, 0.62, 0.42)`, `duskIntensity 0.85`, `lastLightColor (0.95, 0.48, 0.36)`, `lastLightIntensity 0.72`,
  `lightLerp 3`, `telegraphColor` (the shared orange-red).
- **Doors:** states `Closed → Telegraph (dropTelegraph: rect fills, leaves rattle ±rattleDeg, Rattle SFX once per group)
  → Open (dropOpen: leaves swing down leafAngle over leafSwing about local X, a dust burst at 3 points, Gate/Slam once per
  group; the rim at rimAlpha) → Closing (closeTime) → Closed`, and `Held` (open for good, its walk-only box on).
  The telegraph is one `TelegraphRenderer.Lane` per door (from centre − axisX × 4 along axisX, 8 long, 4 wide); the rim
  four thin lanes (held doors only within `drawHeldWithin` of the player: 30 × 4 quads would eat a quarter of the 512).
- **Hits while Open or Held** (every frame, per door):
  - **Player:** grounded and the player's centre inside the rect (no pad). Open: once per opening, `playerDamage`
    (`DamageFlags.Area`) and an impulse across the door toward the nearer long edge (`OutOfRect`). Held: an impulse
    every `heldPushEvery`, `heldPlayerDamage` at most every `heldHitEvery`. Feet above `feetClear` as a door opens:
    `Jumped++`.
  - **Enemies:** `QueryAlive(centre, 4.5 + MaxRadius)`, centre inside the rect, once per opening (a per-enemy serial
    like `FireJets`): Scripted → `ApplyDamage(bossDamage, Area)`, no knockback; elite → `eliteDamage` with a small
    knockback across; otherwise `Execute(i, SourceId)` with `SourceId = GameInfo.StableId("hypogeum")` (`Fallen++`).
- **Lifts:** `Idle → Telegraph (liftTelegraph: the square fills) → Sinking (liftSink: Platform y 0 → −liftDepth) →
  Rising (liftRise) → Idle`; at the top the pack spawns on the platform (a ring of r 1.2, heights sampled) with a dust
  burst. A grounded player on a lift as it starts sinking: `liftPlayerDamage` and an impulse off it. A visual raise
  (the boss's guards) skips the telegraph and the spawn, and skips a lift the player stands on.
- **Run schedule** (scene game time, pauses stop it; no boss active): drops at `dropFirst + k × dropEvery`: `PickGroup`
  (the group of the door nearest the player, never `LastGroup` when another exists). Lift waves at
  `liftFirst + k × liftEvery`: the `liftsPerWave` idle lifts nearest the player at ≥ `liftMinDistance`; each picks a
  pack (active at the run time, weight > 0, `AliveOf(enemy) + count.y ≤ maxAlive`, the arena under its cap; weighted);
  no pack → that lift stays down. The first lift's `call` (`AnnounceKind.Line`), `SfxId.Horn`,
  `CrowdExcitement.Spike(0.5)`. A drop that kills 10+ → `Spike(0.4)`.
- **The boss** (reads `BossController.Active`; no compile dependency on the boss part's new fields):
  - `CurrentState == Entering`: the run schedule stops; every door rattles; while `Position.y < centerLift.y − 0.05`,
    the centre lift's `Platform` y = clamp(`Position.y − centerLift.y`, −liftDepth × centerLiftScale, 0).
  - Fighting: drops every `bossDropEvery.x` (phase 1) / `.y` (phase 2) near the player; no lift waves.
  - `Phase` becomes `openAllOnPhase`: every closed door telegraphs at once (`OpenAll`); the sun lerps to dusk.
  - `Phase` becomes `holdOnPhase`: every door telegraphs, then `Held` (`OpenForGood`); drops stop; last light.
  - `GuardWaves` goes up: a visual raise at the `guardSpotsUsed` lifts nearest the player.
  - `Defeated`: every door closes, door k after k × 0.07 s, the boxes off; the sun back to the day; halt.
- **Light:** on the first tick, cache `RenderSettings.sun` (else the first directional `Light`), its colour and
  intensity; lerp over `lightLerp`.
- **Walk-only boxes:** on the first tick (`EnemyManager.Obstacles` exists by then), `AddBox(centre, yaw, (4.05, 2.05),
  active: false, walkOnly: true)` per door; switched on at `Held`, off at close.
- **Public:** `int Count`, `int LiftCount`, `bool IsTelegraphing(int d)`, `bool IsOpen(int d)` (Open or Held),
  `bool IsHeld(int d)`, `int GroupOf(int d)`, `int LastGroup`, `void Drop(int group)`, `void DropDoor(int d)`,
  `void OpenAll()`, `void OpenForGood()`, `void CloseAll()`, `void RaiseLift(int lift, EnemyDef def, int count)`
  (def null = visual), `void StartWaveNow()`, `void Halt()`, `int NearestDoor(Vector3 p)`,
  `int NearestLift(Vector3 p, float minDistance)`, `float Danger(Vector3 p)` (seconds until a door or lift under p
  opens or moves; 0 if open; +∞ if none: for the bot); counters `Drops`, `Fallen`, `ElitesHurt`, `BossHits`,
  `PlayerHits`, `Jumped`, `LiftWaves`, `LiftsRaised`, `Released`.
- **Pure statics (EditMode):** `static bool InRect(Vector2 centre, float yawDeg, Vector2 size, Vector3 p, float pad)`,
  `static Vector3 OutOfRect(Vector2 centre, float yawDeg, Vector2 size, Vector3 p)` (the unit push across, toward the
  nearer long edge), `static int PickGroup(Vector4[] doors, Vector3 player, int lastGroup)` (no allocation).
- **`StaticObstacles`:** `public int AddBox(Vector2 centre, float yawDeg, Vector2 half, bool active, bool walkOnly)`
  (grows the arrays at load; axes from the yaw: Ax = (cos, −sin), Az = (sin, cos)); a per-box walk-only flag;
  `Overlaps` skips walk-only boxes, `PushOut` doesn't; `ToArrays` leaves them out (a bake never writes them).
- **`Director`:** `public float swarmMultiplier = 1f`, applied in `BeginSwarm` and the prewarm estimate.
- **`ArenaBuilder` hook** when the layout has trapdoors, lifts or a centre lift: a `Hypogeum` object under the arena
  root, the fields copied; each trapdoor's model at (x, 0, z) with its yaw (not static: the leaves move; no collider),
  plus a child `TrapFloor` box collider (8 × 0.2 × 4, top at y 0) on the Environment layer, marked walkable the way
  Walkable pieces are, so the player walks over the hole and the nav bake samples y 0; each lift the same with a
  4 × 0.2 × 4 box; the centre lift at `centerLift`, scaled, with its box at 1.2; the Director's `swarmMultiplier` from
  `layout.swarmMul`.
- **Tests:** EditMode `HypogeumMathTests`: `InRect` at yaw 0 and 76.4 (the centre; 3.9 m along and 1.9 m across inside;
  4.1 along or 2.1 across outside); `OutOfRect` points across toward the nearer long edge; `PickGroup` returns the
  nearest door's group, and another group when that one was last; `AddBox` walk-only: `PushOut` moves a circle out when
  active, not when inactive, `Overlaps` ignores it, `ToArrays` leaves it out. PlayMode `HypogeumTests` (the Colosseum
  scene): `Drop(5)` telegraphs 2 s (none open), opens 4 s, closed by 6.7 s; a Convict on a door dies (`Fallen` 1), one
  walking in while it's open dies, an elite loses 300 and lives, one 1 m beside it is untouched; a grounded player on a
  door is hit once (22 × 1.55) and is off it within 0.5 s, a jumping one isn't (`Jumped` 1); the schedule (first drop
  at 40 s, the next 14 s later and another group); `StartWaveNow()` at 5:00 raises the 3 nearest lifts ≥ 12 m away and
  spawns packs from the active entries, a full cap stops a pack; with a boss: phase 2 opens all 30, phase 3 holds all 30,
  a Convict chasing the player across a held door stays out of it, an arrow crosses it, a player stepping in is thrown
  out and hit at most once a second, `Defeated` closes them all within 3 s; `Halt()` stops drops and waves.

### Enemies — `EnemyDef.cs`, `EnemyManager.cs` (hooks), `EnemyManager.Undead.cs`, `EnemyManager.Breath.cs` (new)
**`EnemyBehaviour.Breather`** (appended after `Caster`): "Closes to `attackRange`, plants, a cone of fire
(`attackRadius` long, `breathAngle` wide) fills over `windup`, then breathes for `breathTime`: the player once, every
other enemy in it × `trampleMul`, fire patches after; then recovers (the Chimera)."

**`EnemyDef`** — two new headers:
| Field | Type | Default | Legionnaire | Chimera | Meaning |
|---|---|---|---|---|---|
| `reviveCount` | int | 0 | 1 | — | times it reassembles |
| `reviveDelay` | float | 2.5 | 2.5 | — | seconds as a pile |
| `reviveHpMul` | float | 0.5 | 0.5 | — | its max HP when it stands up |
| `stompRadius` | float | 0.9 | 0.9 | — | the player's feet within this of a pile (and < 0.5 m above it) scatter it |
| `burnStopsRevive` | bool | true | true | — | dying while burning: no pile |
| `pileMesh` | Mesh | null | `PR_BonePile`'s | — | drawn instanced while it's a pile |
| `breathAngle` | float | 60 | — | 60 | the cone's width (deg) |
| `breathTime` | float | 1 | — | 1.0 | seconds of fire |
| `breathPatches` | int | 3 | — | 3 | fire patches along the cone afterwards (radius below, `zoneDuration` / `zoneDps`) |
| `breathPatchRadius` | float | 1.4 | — | 1.4 | |
| `breathKnockback` | float | 4 | — | 4 | along the cone |

The Chimera also uses `windup` 1.0, `attackRange` 7, `attackRadius` 8 (the cone's length), `attackDamage` 24,
`trampleMul` 3, `zoneDuration` 4, `zoneDps` 10, `attackCooldown` 4.5, `recover` 1.4.

**`EnemyManager.cs` hooks:**
- In `Kill`, before `OnKill`: if `def.reviveCount > _revived[i]`, not (`burnStopsRevive` and `HasStatus(i, Burn)`),
  `info.SourceId != FallSourceId` and a pile is free → `Collapse(i, info)` and return: the slot is freed as a death (a
  bone-coloured pop instead of the usual), `AliveCount` and the per-def count go down, but **no `OnKill`, no XP, no
  gold, no blast**; a pile is added (position, yaw, def, affix, `reviveDelay`, `_revived[i] + 1`). Otherwise as today
  (a burning death that could have collapsed: `PilesBurned++`; a fall: `PilesDenied++`).
- Per slot `byte[] _revived` (0 at `Spawn`).
- `case EnemyBehaviour.Breather` in `Think` → `ThinkBreather(...)`; its pose in `Animate`.
- **For the boss:** `float[] _hpFloor` (0 at `Spawn`); `public void SetHpFloor(int i, float hp)` (0 = off); in
  `ApplyDamage`, after subtracting, clamp `_hp[i]` up to the floor before the kill check; `Execute` with a floor sets HP
  to the floor and returns false. `public void SetHp(int i, float hp)` (clamped to (0, maxHp], no events).
- `public static readonly int FallSourceId = GameInfo.StableId("hypogeum")`, `StompSourceId = StableId("stomp")`,
  `BreathSourceId = StableId("breath")`.

**`EnemyManager.Undead.cs`:**
- `struct Pile { Vector3 Pos; float Yaw; EnemyDef Def; AffixDef Affix; float T; byte Revived; }`, `MaxPiles = 48`,
  `Pile[] _piles`, `int PileCount`.
- Each tick: `T -= dt`; a bone-coloured ring (`TelegraphRenderer.Circle(pos, stompRadius, 1 − T / delay,
  (0.89, 0.84, 0.74, 0.8), Style.Ring, 0.05)`); the meshes with `Graphics.RenderMeshInstanced` per `pileMesh` (the shared
  palette material, a 0.15 s scale-in, a shake in the last 0.5 s); **stomp** when the player's XZ distance ≤
  `stompRadius` and feet < 0.5 m above it: `OnKill` at the pile (`SourceId = StompSourceId`, its XP × the affix's,
  its gold chance), a bone burst, `SfxId.Splinter`, `PilesStomped++`; **rise** at `T ≤ 0`: `Spawn(def, pos, affix)`,
  then max HP and HP × `reviveHpMul`, `_revived[s] = Revived`, a bone burst, `PilesRisen++`.
- `DespawnAll` / `KillAll` clear the piles. Counters `PilesMade`, `PilesRisen`, `PilesStomped`, `PilesBurned`,
  `PilesDenied`.
- Optional (only if time allows): `EnemyDef.riseOnSpawn`, the dead climbing out of the sand at a ring spawn (the view
  starts 1.4 m down and rises over 0.6 s, no contact meanwhile, a sand burst).

**`EnemyManager.Breath.cs`:**
- `ThinkBreather`: Move (straight at the player) → when `dist < attackRange`, `|dy| < 1`, the cooldown ready: Windup
  (`_aim = dirTo`, locked; `tele.Sector(p, aim, attackRadius, breathAngle, fill, telegraphColor)`;
  `AddWindupThreat`) → Attack (`breathTime`: the sector at full strength, fire bursts at 3 points every 0.15 s; the
  player once per breath if `InCone(p, aim, attackRadius, breathAngle, player, playerRadius)` and `|dy| < 1.2`:
  `attackDamage` (Area) + impulse along the aim; every other enemy in the cone once per breath (the trample serials):
  `attackDamage × trampleMul`, `Area | Proc`, `SourceId = BreathSourceId`, skipping itself, its own type and Scripted) →
  at its end `breathPatches` `HazardZones.Add` at (k + 1) / (n + 1) of the length, radius `breathPatchRadius`,
  `zoneDuration`, `zoneDps` → Recover (`recover`, still) → Move. It faces `_aim` from the wind-up to the end of the
  breath.
- Pose: wind-up, the head drawn back (`Head` lean −25 × k), the body low, the front legs braced; breath, the head
  thrust forward (+20).
- `public static bool InCone(Vector3 origin, Vector3 aim, float length, float angleDeg, Vector3 p, float pad)`.

**Tests:** EditMode `BreathConeTests` (`InCone`: on the axis, at 29° inside and 31° outside a 60° cone, past the
length, behind). PlayMode, in the test arena with defs made by the test (`ScriptableObject.CreateInstance`, so they
don't wait for the layout part): `UndeadTests`: a Legionnaire killed collapses (no `OnKill`, no gem, `PilesMade` 1,
`AliveOf` 0), stands up 2.5 s later at the spot with half max HP, and its second death is an `OnKill` with its XP and no
pile; a burning one doesn't collapse; one executed with `FallSourceId` doesn't; a player walking over a pile scatters it
(`OnKill` at the pile, `PilesStomped` 1, nothing rises); never more than 48 piles. `ChimeraTests`: the cone shows for
its wind-up; the breath hits a player inside once, not one 40° off its aim or 9 m away; another enemy in the cone takes
×3; three fire patches; `SetHpFloor` keeps a Scripted slot alive at the floor, `SetHp` sets it, `Execute` respects it.

### The boss — `BossDef.cs`, `BossController.cs`, `BossFight.cs`, `BossSetup.cs`
**`BossDef`** — new fields (every default keeps the existing bosses as they are):
| Field | Type | Default | Eternal Victor | Header |
|---|---|---|---|---|
| `discLength`, `discWidth` | float | 18, 1.6 | 20, 1.6 | "Falx Throw (the Eternal Victor)" |
| `discWindup`, `discSpeed` | float | 0.8, 24 | 0.8, 26 | " |
| `discDamage`, `discKnockback` | float | 18, 6 | 20, 6 | " |
| `discSpin` | float | 1080 | 1080 | " (deg/s) |
| `discRepeats` | Vector2Int | (1, 1) | (1, 1) | " (throws in a row, P1 / P2) |
| `phase3DiscRepeats` | int | 0 (= P2's) | 2 | "Phase 3" |
| `phase3PounceRepeats` | int | 0 (= `pounceRepeats.y`) | 2 | "Phase 3" |
| `discPart` | string | `"Falx"` | `"Falx"` | the model part that flies |
| `whipGrasp` | bool | false | true | "Whip Lash": bone hands burst along the lane instead of the crack; the falx driven into the deck |
| `entranceRise`, `riseDepth`, `riseTime` | bool, float, float | false, 3, 2.6 | true, 3, 2.6 | "Entrance" |
| `falseDeath` | bool | false | true | "Eternal (false death)" |
| `remainsHp`, `remainsTime`, `riseHp01` | float | 1500, 8, 0.2 | same | " |
| `remainsText`, `riseText` | string | "", "" | "HE WILL NOT STAY DOWN!", "\"NOT YET.\"" | " |
| `riseBurstRadius`, `riseBurstDamage`, `riseBurstKnockback`, `riseBurstWarn` | float | 4, 20, 10, 1 | same | " |
| `remainsColor` | Color | bone (0.89, 0.84, 0.74, 0.85) | same | " |

**`BossController`:**
- **`BossAttack.Disc`** appended after `Volley` (serialized enum). `Weight`: `d > 4 && d < discLength × 0.9 → 2.4`.
  `StartAttack` wind-up `discWindup`. Wind-up: `tele.Lane(_pos, _aim, discLength, discWidth, u, col)`; the right arm
  drawn back high, the body twisted. **Strike:** find `discPart` under the view root once (like `Spear`), keep its bind
  pose; it flies in world space from `Hand` along `_aim` at `discSpeed`, spinning `discSpin` about the vertical, at
  hand height, until it has gone `discLength` or `_em.Obstacles.Overlaps(xz, 0.4)` (stone; walk-only pits don't count);
  then back toward his current `Hand`; caught within 0.6 m → the bind pose back. The player within `discWidth / 2 +
  playerRadius` of it (XZ) and less than 2 m above its height: `HitPlayer(discDamage, along the flight, discKnockback)`,
  once out and once back. He stands and faces the falx (no other attack) until it's caught. Repeats like the charge's:
  `discRepeats` per phase, `phase3DiscRepeats` in P3, re-aimed between. Counters `DiscsThrown`, `DiscHits`.
- **Pounce repeats** in P3: `phase3PounceRepeats` when > 0.
- **Grasp** (`whipGrasp`): `StrikeBegin`'s Whip branch bursts bone-coloured dust at six points along the lane (and
  `SfxId.Slam`) instead of the crack; the existing slow applies (`whipSlow 0.1` for 1 s = held). Pose with a `Falx`:
  both arms raised in the wind-up, driven down with the body leaning ~35° on the strike.
- **`BeginRise(Vector3 at)`**: like `Begin`, but he starts at `ground − riseDepth` under `at` and Entering lasts
  `riseTime`: `_pos.y` eases up to the ground, a kneeling pose that straightens, no walking; then the existing roar
  (the falx raised: the salute). Invulnerable, no contact, as today's entrance.
- **The false death:** `State.Remains` appended after `Dead`. When phase 3 starts (the end of its roar) and
  `falseDeath`: `_em.SetHpFloor(_slot, 1)`. When his HP reaches the floor (not busy): `SetHpFloor(_slot, 0)`,
  `_em.SetHp(_slot, remainsHp)`, invulnerable 0.4 s, `Enter(State.Remains, remainsTime)`, `remainsText`
  (`AnnounceKind.BossPhase`), a bone burst, `CrowdExcitement.Spike(1)`, `FalseDeaths++`. In Remains: a collapsed pose
  (a heap: body folded forward, head down, arms out), no movement, no contact, the guard clock paused; a
  `remainsColor` ring (r 2.2) fills over `remainsTime`; in its last `riseBurstWarn` an orange-red circle of
  `riseBurstRadius` fills. Killed in Remains → `Die()` as today (the top-of-tick `Alive` check). The time runs out →
  `SetHp(_slot, maxHp × riseHp01)`, the burst (a grounded player within `riseBurstRadius + playerRadius`:
  `riseBurstDamage`, knockback away), `riseText`, a roar like `BeginEnrage(3)` but without its phase text or extras,
  then Chase. Public `bool InRemains`, `int FalseDeaths`.
- Poses for a two-handed falx (signs unverified in Unity, as R1's): Chase, the falx low across the body; Reaper's
  Circle, wind-up twisted back, strike a full turn of the body with the falx out; the lunges, both arms thrust.
- **`BossFight.Begin`:** when `boss.entranceRise`: no gate opens; `controller.BeginRise(arena centre)` (the
  `EnemyManager.arenaCenter`, y from the height grid: the stage top, 1.2); otherwise as today.
- **`BossSetup`:** `EternalVictorEnemyPath = "Assets/Data/Enemies/EN_EternalVictor.asset"`,
  `EternalVictorPath = "Assets/Data/Bosses/BD_EternalVictor.asset"`, `EnsureEternalVictor()` (menu
  `Gladiator/Setup/Bosses/Create Eternal Victor Data`), created once with:
  - body: id `eternal_victor`, "The Eternal Victor", `Scripted`, maxHp 22000, radius 1.0, knockbackTaken 0, blockArc 0,
    contactDamage 0, moveSpeed 0, xp 0, goldChance 0, prewarm 1, model `Assets/Art/Models/Enemies/EN_EternalVictor.fbx`;
  - words: displayName "THE ETERNAL VICTOR", title "FIRST CHAMPION OF THE COLOSSEUM", enterText "THE ETERNAL VICTOR!",
    fallText "THE ETERNAL VICTOR FALLS!", guardsText "THE DEAD RISE!", phase2Text "\"RISE, BROTHERS!\"", phase3Text
    "\"THE ARENA IS MINE!\"", remainsText, riseText, shortName "The Eternal Victor", deathLine "The Eternal Victor salutes
    the crowd, then carves your name into the podium wall.", victoryLine "The first champion of the Colosseum lies still
    at last. Forty thousand voices chant a new name: yours.";
  - attacks: phase1 {Sweep, Flurry, Disc, Pounce}; phase2 and phase3 {Sweep, Flurry, Whip, Disc, Pounce};
  - movement: moveSpeed 3.6, enragedMoveSpeed 4.0, phase3MoveSpeed 4.6, turnRate 200, closeInRange 30, closeInSpeed 7,
    contactDamage 12, contactInterval 0.8, recover 0.55;
  - Reaper's Circle: sweepRadius 5.5, sweepWindup 1.0, sweepDamage 26, sweepKnockback 9, sweepJumpClear 0.7;
  - lunges: flurryLength 7, flurryWidth 1.2, flurryWindup 0.4, flurryDamage 14, flurryThrusts (2, 3);
  - grasp: whipLength 16, whipWidth 2.2, whipWindup 0.8, whipDamage 18, whipSlow 0.1, whipSlowSeconds 1.0, whipGrasp;
  - falx: the table above;
  - leap: pounceRadius 3.5, pounceWindup 1.0, pounceTrack 0.7, pounceFlight 0.55, pounceHeight 3.5, pounceRange 26,
    pounceDamage 26, pounceKnockback 9, pounceRecover 0.7, pounceRepeats (1, 1), phase3PounceRepeats 2;
  - guards: { `EN_SkeletalLegionnaire` by its literal path (no compile dependency on the layout part) }, guardCount
    (6, 8), guardsEvery 24, guardsFromPhase 2, guardSpotsUsed 2, guardsNearestGates false, guardThreadColor bone;
  - phases: phase2At 0.65, enrageSpeed 1.2, enrageRoar 1.5, phase3At 0.3, phase3Speed 1.4, phase3Attacks as phase 2;
    phase2FireRing, phase2SpikeVolley, phase2FireJets, phase2Blades false; phase2CagePack 0; beastPack 0;
    scatterOnDefeat true; rewardFloor Rare; defeatGold 0; entranceRise true; falseDeath true.
- **Tests:** PlayMode `EternalVictorTests` (the Colosseum scene): he rises at the centre (starts ≥ 2.5 m below the stage
  top, ends on it, invulnerable meanwhile) and strides when you're 60 m away (> 6 m/s); the circle, lunges, grasp
  (held: speed ×0.1) and leap telegraph and hit; the falx flies out and back, hits a player in its lane once each way,
  misses one beside it, turns back early on a statue; at 65 % `Phase` 2 and Legionnaires appear at the 2 lifts nearest
  the player; at 30 % `Phase` 3, two leaps and two throws in a row, wind-ups ÷ 1.4; at 0 HP in P3: `InRemains`, not
  `Defeated`, HP = 1,500; killing the remains → `Defeated`; letting 8 s pass → he rises with 20 % and the burst hits a
  player within 4 m; the next 0 HP → `Defeated`; at his fall every enemy is dragged off. EditMode `BossPhaseDataTests`
  additions: `BossAttack.Volley` is still 9 and `Disc` 10, `State.Dead` unchanged; a `BossDef`'s new fields default to
  off (`entranceRise`, `falseDeath` false; repeats 0); Gorm, the Champion, the Beast Lord, the Patron and the Emperor's
  Champion still walk in through their gates and die at 0 HP.

### Layout and setup — `ColosseumLayout.cs`, `ColosseumSetup.cs` (new), `PlaytestBot.cs`, the run and scene lists
**`ColosseumLayout`** (like `GrandArenaLayout` / `AmphitheaterLayout`; `Path = "Assets/Data/Arenas/AL_Colosseum.asset"`;
menu `Gladiator/Arenas/Regenerate Colosseum Layout (overwrites)`; designer-owned once it exists):
- Constants: `A 98`, `B 67`, `WallStep 3.75°`, `GreatGateHalf 11`, `PlayInset 1.1`, `RingA 62`, `RingB 42`,
  `DoorSize (8, 4)`, `LiftSize 4`, `StageRadius 10`, `StageHeight 1.2`, `HillHeight 2.2`, the stand profile (the
  contract's numbers), the crowd densities.
- `WallLine()` (counter-clockwise from (98, 0), the great gates' chords at x = ±96.67), `Outline()` =
  `GrandArenaLayout.Inset(WallLine(), PlayInset)` (`PlayArea()` ≈ 20,005); `public static bool OnTrapdoor(Vector3 p,
  float pad)` for the tests.
- Values: id `colosseum`, "The Colosseum", tier 5, centre (0, 0, 0), radius 96.9, aspect 0.68, fog (90, 380),
  playerSpawn (0, 0.1, −26), playerYaw 0, maxAlive 450, densityMul 1.3, swarmMul 1.5, spawnMaxDistance 55,
  eliteEvery 60, sunEuler (32, −40, 0), enemyHpMul 2.55, playerDamageTakenMul 1.55, gateModel `Environment/SM_ColGate`,
  secrets None, `guardSpots` = the 8 lifts, boss `BD_EternalVictor`, bossGate 0; the hypogeum fields as in the hypogeum
  table (the 30 doors with their groups, the 8 lifts, the centre lift, the clocks, the packs).
- Gates: the contract's six (G0 and G3 with model `Environment/SM_ColGreatGate`).
- Pieces: `SM_ColFloor` (Walkable, origin); `SM_ColCavea_0`–`_5` (Decor, origin); `SM_ColPulvinar` ×2 (Decor);
  `SM_ColStage` (Walkable, origin); 4 × `SM_ColStairs` (Walkable); 4 × `PR_VictorStatue` (Solid); `SM_ColMons_A` ×2 and
  `SM_ColMons_B` ×2 (Walkable). The trapdoors and lifts are not pieces: the hypogeum hook places them.
- Crowd zones per run and tier along the curve (the Amphitheater's `Seats` approach, the contract's profile and
  densities), over the side gates too; 14 per box. `solidWalls` and `boundary` along every wall-line edge
  (`solidWallHeight 8.4`, the boundary ≥ 30 m).
- Chests (18) and altars (3): ARENAS.md §6's lists.
**`ColosseumSetup`** (like `GrandArenaSetup`): `Scene = "Assets/Scenes/Arenas/Colosseum.unity"`,
`RunPath "Assets/Data/Runs/RD_Colosseum.asset"`, `UnlockPath MetaSetup.Dir + "UL_Colosseum.asset"`,
`TablePath "Assets/Data/Arenas/ST_Colosseum.asset"`, `LegionnairePath`, `ChimeraPath`; `Build()` (menu
`Gladiator/Arenas/Build Colosseum`): the layout, `EnsureEnemies()`, `BossSetup.EnsureEternalVictor()`, the table,
`EnsureRun()`, `ArenaBuilder.Build`.
- `EnsureRun`: `UL_Colosseum` (id `colosseum`, `DefeatBoss emperors_champion` × 1, hint *"The Emperor's champion is dead.
  One arena is left, and it has been waiting for you."*, description *"The oldest arena of all, and the biggest. Its
  floor drops into the hypogeum, beasts and the dead rise on its lifts, and its first champion has never left the
  sand."*), then `MetaSetup.CreateOrUpdate()`; `RD_Colosseum` (id `colosseum`, arena "THE COLOSSEUM", description *"The
  last arena: the biggest floor of all, laid over the hypogeum. The floor drops open under the horde, beasts and the dead
  rise on the lifts, and the Eternal Victor waits below. Tougher enemies, more Renown (×3.5)."*).
- `EnsureEnemies`: `EN_SkeletalLegionnaire` (Chaser; maxHp 34, moveSpeed 3.7, speedJitter 0.15, radius 0.36,
  acceleration 14, contactDamage 9, contactInterval 0.9, knockbackTaken 1, turnRate 540, xp 2.5, goldChance 0.05,
  prewarm 140, the Convict's anim; reviveCount 1, reviveDelay 2.5, reviveHpMul 0.5, stompRadius 0.9, burnStopsRevive,
  pileMesh = the mesh in `Props/PR_BonePile.fbx`) and `EN_Chimera` (Breather; maxHp 420, moveSpeed 3.0, speedJitter
  0.05, radius 0.95, acceleration 8, contactDamage 14, contactInterval 1.0, knockbackTaken 0.3, turnRate 160, windup
  1.0, attackRange 7, attackRadius 8, attackDamage 24, trampleMul 3, breathAngle 60, breathTime 1.0, breathPatches 3,
  breathPatchRadius 1.4, zoneDuration 4, zoneDps 10, breathKnockback 4, attackCooldown 4.5, recover 1.4, xp 30,
  goldChance 0.6, prewarm 6, the War Boar's anim).
- `EnsureTable`: ARENAS.md §6's table (Centurion and Chimera `elite: false`), affixes from the Slave Pit's table. The
  lift packs: Pit Cur, `EN_WarBoar` (`AmphitheaterSetup.BoarPath`), Chimera, Legionnaire, with ARENAS.md §6's numbers.
- **`PlaytestBot`:** `-arena colosseum`; one log line `| hypogeum drops N (fallen F, hit you H, jumped J, boss B) lifts
  W (raised L, released R) piles P (risen, stomped, burned) breaths (hit you)`; the bot steers off a door or lift whose
  `Hypogeum.Danger(p)` < 2.5 s (toward `OutOfRect`) and treats held doors as walls.
- **Run and scene lists:** append `ColosseumSetup.EnsureRun()` to the run list in `ProvincialPitSetup.cs` and
  `ColosseumSetup.Scene` to `BuildScripts.cs`'s scene lists (both owned by this part in R2).
- **Tests:** PlayMode `ColosseumTests`: locked until the Emperor's Champion falls, then a run there is tier 5 (Renown
  ×3.5) with ×2.55 HP, ×1.55 damage, cap 450, density ×1.3 and swarms ×1.5; `PlayArea()` ≈ 20,005 (> 15,315, ≥ 17,500);
  the horde stays in on 24 bearings and at all six gates; nothing (chest, altar, spawn, stair, statue, hill footprint,
  gate mouth, lift) within 1 m of a trapdoor; the stage is walkable at 1.2 m and the hills at 2.2 m and the horde climbs
  them; `guardSpots` are the 8 lifts; a Legionnaire and a Chimera come from the table. Add `colosseum` to
  `ArenaNavTests`.

## Ideas for the BACKLOG (not R2)
Harpy swarms (with a hit-height rule for every weapon pattern); Asterion chained in the hypogeum, seen through an open
trapdoor by the Gate of Death, his chain breaking at the Victor's fall (R3's reveal); the crowd's hush before the rise
(`CrowdExcitement.Hush`); **the Emperor's thumb at the false death, decided by your Favor** (full: thumbs down, he stays
down); the podium's wall of names showing the player's own past deaths from the save; a naumachia event once
telegraphs have a water-safe pass; Codex bestiary entries for tiers 3–5; challenges "stomp 100 piles in a run" and
"beat the Victor without falling once".

## Verified / unverified
- **Verified (arithmetic, the geometry script):** the wall line (20,576 m² with 96 samples and the two chords; the
  ellipse 20,628), the play area (20,005 m², 1.306× the Imperial's 15,315, 1.14× the GDD's 17,500; 19,967 with 48
  samples), the perimeter (522 m), the ring (329.8 m, 5.74 m bridges), every door's centre and yaw (the long side along
  the ring's tangent, checked on R00 and R05), the closest two doors 4.0 m apart, doors 4.8 % of the floor; clearances
  from the doors: lifts ≥ 8.8 m, hills ≥ 3.8 m, statues ≥ 9.9 m, chests ≥ 4.9 m, altars ≥ 10 m, the spawn 14 m; chests
  ≥ 12 m from gate mouths; the cavea's run angles (±9.45°, ±3.77°); the jump (0.63 s of air: 4.7 m at a run, 7.2 m
  from a slide); the crowd estimate (~3,786 before the boxes' cut). Every existing field, method and property named
  above exists in today's code (read this session).
- **Unverified:** everything about feel and balance in play (the drop cadence and how much of the horde it eats, the
  lift waves, the reassembly load at 450, the Chimera's threat, the boss's HP and fight length, the false death's
  fairness); performance at 450 alive with a ~3,700 crowd and 39 animated doors and lifts; how enemies behave at a
  phase-3 chasm's edge (Q1); every Unity sign and pose. These need the orchestrator's bot runs and the producer's
  playtest.
