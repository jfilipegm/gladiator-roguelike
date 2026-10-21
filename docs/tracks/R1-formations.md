# R1 formations — notes (the Centurion's Praetorian line)

Branch `claude/angry-greider-31e4f8` (cut from `main`, fast-forwarded to `origin/phase5-imperial-arena` at 25ed4b0).
Code only, no Unity: `Tools/ccheck` errors=0; **no test has been run** (the orchestrator runs them).

**Owns:** `EnemyDef.cs`, `EnemyManager.Leash.cs`, `EnemyManager.cs` (the hooks), `Assets/Tests/EditMode/FormationSlotTests.cs`,
`Assets/Tests/PlayMode/FormationTests.cs`, this file. Built on Phase 5.15's escorts (the Beast Handler), which behave
exactly as before (default `freeEscortsOnDeath = true`, no slots).

## What it does (docs/ARENAS.md §5)
- **Slots:** an `EnemyDef` with `escort` + `formationSlots` spawns `min(escortCount, slots, 32)` escorts, escort k on
  slot k in the leader's frame (x right, y forward), clamped inside the arena, facing his front. Spawning one is a
  formation (`FormationsSpawned`).
- **Holding the line:** each tick a formed escort's velocity is its slot's own motion (the march and the wheel, from the
  slot's position last tick, followed up to `slotFollowMax` 3 × its move speed) plus closing on the slot (time constant
  `slotTime` 0.35 s, at most `slotCatchUp` 1.6 × its move speed). It faces the line's front (its own turn rate), not you:
  flanking works. No rope, no leash clamp. Velocity is set directly (no acceleration), like a lunge.
- **The leader** (any behaviour) with a line faces you in Move / Recover at his turn rate (the Centurion's 80°/s: the
  line wheels slowly) and slows over the last metre to stop `formationHold` (2.6 m) short of you: the middle shield sits
  ~0.8 m from you, inside contact range (the shields press you).
- **The push** (a `Lunger` leader with a line): his wind-up (`windup` 0.7 s, when you're within `attackRange` 4.2 m)
  locks his facing; a lane fills in front of the shields: from the middle of the live shields' front, along that facing,
  (live slots' width + 2 × escort radius + `pushPad`) wide = 7.0 m and `pushDepth` 2.6 m deep. He lunges along the
  locked facing (`lungeSpeed` 8 × `lungeTime` 0.25 = 2 m) and every formed escort moves with him rigidly. A shield
  touching you (escort radius + 0.35 + 0.25 = 1.1 m, feet within 1.1 m) hits once per push (his attack flag; his own
  staff shares it): `attackDamage` 16, `Melee`, thrown `pushKnockback` 9 m/s along the facing + 3 m/s up, `SfxId.Slam`.
  The wind-up is a threat (edge arrow, bot). Counters: step out of the lane, back, jump, or get behind.
- **A dead shield** leaves a gap (no closing ranks); the lane narrows to the live shields. With his whole line dead he's
  a plain Lunger again (faces you in the wind-up, lunges at you, no hold, no lane).
- **His death** with `freeEscortsOnDeath` off breaks the line: every escort still with him (line or leash) loses its
  leader and slot, gets `breakStatus` (SE_Stagger: Root, 1 s) and its shield down for `breakShieldsDown` (3 s: no block,
  `ArmL` swung back and out), then is an ordinary enemy of its kind (a ShieldBearer faces and chases you).
  `LinesBroken` +1 if any of them was in a line. With it on (the default) they're freed as before (`EscortsFreed`).

## API (for the layout, boss and bot parts)
**`EnemyDef`**, header "Formation (the Centurion, Phase 5.16)", after the escort fields:
| Field | Type | Default | Centurion (ARENAS.md §5) |
|---|---|---|---|
| `formationSlots` | Vector2[] | empty (a leash) | (−2.6, 1.8), (−1.3, 1.8), (0, 1.8), (1.3, 1.8), (2.6, 1.8) |
| `formationHold` | float | 0 (walks on) | 2.6 |
| `pushPad` | float | 0.8 | 0.8 |
| `pushDepth` | float | 2.6 | 2.6 |
| `pushKnockback` | float | 9 | 9 |
| `freeEscortsOnDeath` | bool | **true** | false |
| `breakStatus` | StatusDef | null | `SE_Stagger` (`GladiatorSetup.StaggerPath`) |
| `breakShieldsDown` | float | 0 | 3 |

The push itself uses the leader's Lunger numbers (windup 0.7, attackRange 4.2, lungeSpeed 8, lungeTime 0.25,
attackDamage 16, attackCooldown 3.5, recover 0.7), as R1-design.md lists for `EN_Centurion`.

**`EnemyManager`** (public):
- `int FormationsSpawned`, `int LinesBroken` (bot log: the contract's names; R1-design.md called the first `Formations`).
  `LinesBroken` ignores `KillAll` (the run's end); `EscortsFreed` is unchanged and still counts escorts freed by it.
- `static Vector3 SlotWorld(Vector3 leader, float leaderYaw, Vector2 slot)`.
- `bool InFormation(int i)`, `int FormedCount(int leader)`, `int FormationSlotOf(int i)`, `Vector3 SlotPosition(int i)`,
  `bool ShieldsDown(int i)`.
- `bool PushLane(int leader, out Vector3 from, out Vector3 dir, out float width, out float depth)`: true while a leader
  braces with a line (the bot can step out of it).
- Tuning on the manager (inspector, "Formations (Phase 5.16)"): `slotTime` 0.35, `slotCatchUp` 1.6, `slotFollowMax` 3.

**Hooks in `EnemyManager.cs`:** after `Think`, `FormationSteer` (escort in a line) / `LeaderHold` (leader with a line);
direct velocity for a line; no `LeashClamp` for a line; `PushHit` before contact damage; a line's facing = the leader's;
a bracing leader keeps `_aim`; the Lunger's wind-up locks `_aim` to his facing and calls `DrawPush`; `IsBlocking` checks
`ShieldsDown`; `Kill` calls `LeaveLine`; the shields-down timer ticks with the flash; `KillAll` sets `_killingAll`.
Per-enemy arrays allocated with the rest (`_slotIndex`, `_formedMask`, `_shieldsDownT`, `_slotPrev`); no allocation, no
LINQ, no per-frame scans (the line is a bit mask per leader).

## Decisions (one-line reasons)
1. **The slot's own motion is followed, the catch-up is capped (1.6×), not the total.** Capping the total at 1.6 × 3.0
   = 4.8 m/s bends the line ~2 m when it wheels on the march: the outer shield must move ~7 m/s (2.6 walk + 80°/s × 3.2 m).
   Following the slot keeps it rigid; a teleport or a big shove of the leader is still capped (3 ×).
2. **Formed escorts skip acceleration** (velocity = desired, like a lunge): the slot term is already smooth, and the
   Praetorian's 14 m/s² lagged ~0.7 m at the start of each wheel.
3. **The push goes along his facing, locked when the brace begins** (not at you at release like a cur's lunge): the
   lane is the truth. At 80°/s he'd otherwise track a sidestep by up to 56° during the 0.7 s brace.
4. **A leader with a line faces you while he walks or recovers.** Holding still, a Lunger faces its velocity (zero): the
   line would never turn to a player strafing in front of it.
5. **The hold ramps over the last metre** instead of cutting to zero at 2.6 m: his 10 m/s² braking overshot by 0.34 m.
6. **The line is a bit mask** (≤ 32 slots): count, gaps and the lane's width from the live shields in O(slots).
7. **The break applies to every escort still with him**, line or leash (`freeEscortsOnDeath` off); `LinesBroken` counts
   only lines.
8. **The push hit adds 3 m/s up** (a short hop, as the War Boar's 3.5): a flat 9 m/s on the ground reads as a slide;
   `SfxId.Slam` at half volume for the shields' thud. Both are tunables, not in the design.
9. **The cap uses the def's `moveSpeed`, not the jittered speed:** the whole line catches up alike.

## Tests (written, not run)
**EditMode `FormationSlotTests`** (`Assets/Tests/EditMode/FormationSlotTests.cs`; a different class name from the
PlayMode one, as `InteractableTests` / `InteractablePlayTests`):
- `SlotWorld_RotatesWithTheLeader`: (2.6, 1.8) at yaw 0 / 90 / 180 / −90 lands at (2.6, 1.8) / (1.8, −2.6) /
  (−2.6, −1.8) / (−1.8, 2.6) from him, at his height; 270 = −90; equals `Quaternion.Euler(0, yaw, 0)` × the slot for a
  sweep of yaws, distance preserved; slot (0, 0) is the leader.
- `ANewEnemy_KeepsItsEscortsOnALeash_AndFreesThemWhenItDies`: a fresh `EnemyDef`'s defaults (no slots, hold 0, frees on
  death, no break status, pad 0.8 / depth 2.6 / knockback 9); the real `EN_BeastHandler` asset reads
  `freeEscortsOnDeath` true and no slots.

**PlayMode `FormationTests`** (`Assets/Tests/PlayMode/FormationTests.cs`, the TestArena; stand-in Praetorian and
Centurion made in the test with ARENAS.md §5's numbers, speed jitter 0, the char-art FBX models if imported (else the
Murmillo's / Convict's); every obstacle switched off and the formation placed on cliff-free ground found by scanning the
height grid, so the TestArena's pillars don't bend the line):
- `HeSpawnsWithFiveOnTheirSlots_AndTheyKeepThemAsHeMarchesAndWheels`: 6 bodies, `FormationsSpawned` +1,
  `FormedCount` 5, one escort per slot, each on its slot (< 0.05 m) facing his front, the middle one 1.8 m ahead toward
  you; marching 1.5 s (> 2.5 m toward you) the worst slot error ≤ 0.5 m; you jump 8 m to his right: he wheels 90°
  (within 3°) with the worst error ≤ 0.5 m during the wheel and the 0.5 s after; the escorts face his front (< 5°).
  Logs the worst errors.
- `TheLine_BlocksFromItsFront_NotFromBehind_NorFromTheFlank`: every shield blocks a hit from the line's front, none
  from behind; 20 from the front costs 5 (75 %), from behind 20; you 7 m to his right: 0.25 s later a hit from you isn't
  blocked and the escort still faces the line's front (< 10° from his yaw).
- `ThePush_TelegraphsItsLane_HitsYouInItOnce_AndThrowsYouBack`: he braces within 4.2 m; `PushLane` 7.0 m wide, 2.6 m
  deep, along his facing, starting at the shields' front, with you in it; a threat is listed; no hit during the brace;
  after the shove exactly one 16 hit and you moved > 0.5 m along the push.
- `ThePush_MissesYouFourMetresToTheSide_ItsFacingLockedAtTheBrace`: you teleport 4 m off the lane's centre line during
  the brace: he turns < 0.5° through the brace and shove, and no 16 hit lands.
- `KillingHim_BreaksTheLine_StaggeredWithShieldsDown_ThenTheyFightOn`: all 5 alive, `EscortsFreed` unchanged,
  `LinesBroken` +1; each has no leader, Root (Stagger), shields down, doesn't block from its front (10 lands as 10) and
  stands still; 3.2 s later each is alive, shield up (blocks), no Root, faces you (dot > 0.9) and walks at you
  (> 1 m/s); a second formation then `KillAll`: `FormationsSpawned` +1, `LinesBroken` unchanged.
- `HeHoldsShortOfYou_ADeadShieldLeavesAGap_AndWithNoLineHeWalksOn`: after 4.5 s he stands 2.3–3.0 m from you, nearly
  still; killing the left end leaves `FormedCount` 4 with slots 1–4 still held and `LinesBroken` unchanged; his next
  brace's lane is 5.7 m wide (slots 1–4), centred 0.65 m right; with every shield dead `FormedCount` 0 and 2.5 s later
  he's within 1.5 m of you; his death then breaks no line.
- `TheBeastHandler_StillFreesHisCurs_AndIsNoFormation`: the real `EN_BeastHandler`: no formation counted, two curs on
  his leash and not in a line; his death frees both (`EscortsFreed` +2, `AliveOf(cur)` 0), `LinesBroken` unchanged.
  (`BeastmasterTests.TheHandler_WalksTwoCursOnLeashes_AndKillingHimFreesThem` covers the rest and must still pass.)

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings (all six assemblies). The arithmetic above (lane 7.0 m; the shove's
  reach 1.8 + 2 + 0.5 + 0.6 = 4.9 m = 2.3 + `pushDepth`; the outer shield's wheel speed ~7 m/s).
- **Unverified (needs the Editor):** every test above (none has run); that the Beast Handler, the Pit Cur's lunge and
  the shield tests (`BeastmasterTests`, `EnemyRosterTests`) still pass — the Lunger's code path is unchanged without a
  line, but please run them; the shield-down pose's sign (`ArmL` −30° swing, −40° roll); the feel of the push, the hold
  and the wheel; the line against pillars, the spina's 1.2 m edge and its stairs (escorts can't step up 1.2 m, so a slot
  on the spina jams against it until the leader moves); performance (per formed escort one sin/cos and a few
  multiplies per tick; expected negligible, not measured).

## Needs from others
- **Layout and setup:** `EN_Centurion` with the formation column above (+ `escort EN_Praetorian`, `escortCount 5`);
  the bot log can print `enemies.FormationsSpawned` and `enemies.LinesBroken` (and optionally dodge
  `PushLane(i, …)`). `EN_Praetorian` prewarm 40 covers 3 table Centurions + the boss's 2 (25 shields).
- **Boss:** nothing; `EnemyManager.Spawn(EN_Centurion, …)` brings the line.
- **Orchestrator:** run `FormationSlotTests` (EditMode), `FormationTests`, `BeastmasterTests`, `EnemyRosterTests`
  (PlayMode). The two new `.cs` files have no `.meta` yet (Unity writes them on import).
- **Spotted (not R1):** `EscortsFreed` also counts curs "freed" by `KillAll` at the run's end; `LinesBroken` doesn't.
