# R1 design — notes (the Imperial Arena + the Emperor's Champion)

Branch `claude/r1-design-791b2a`. Docs only: no code, no Blender, no Unity.

**Written:** `docs/BOSSES.md` § "The Emperor's Champion" (the 10-line spec, per-phase attack tables, reused vs new);
`docs/ARENAS.md` § "5. The Imperial Arena" (shape and size check, spina, gates, the chariot race rules and numbers, the
Praetorian and the Centurion, formation rules, spawn table, difficulty, Renown, chests and altars, unlock, tests); this
file (decisions, open questions, needs, and the exact fields and methods for each code part).

**Aligned with char-art** (`docs/tracks/R1-char-art.md` on `claude/r1-char-art-05b28b`, read 2026-10-21): the boss is
3.3 m with radius 1.25; the chariot is 4.44 × 2.34 m with `Car`, `Driver`, `WheelL/R` (r 0.54), `Horse0–3`,
`Horse<i>LegsF/B`; Praetorian radius 0.5, Centurion radius 0.42. Env-art's notes weren't published yet: the layout
numbers below follow the contract and must be checked against `R1-env-art.md` when it lands.

## Decisions (one-line reasons)
Where I changed the contract or the PROGRESS design notes, it says **(changed)**.

1. **Races every 60 s from 1:20, not every ~45 s (changed).** Two chariots × 2 laps keep the track live ~37 s per
   race; at 45 s that's 82 % of the run (a wall, not an event); at 60 s it's 62 %, with 23 s of quiet. It also gives 7
   races before 8:00, one per egg on the lap counter.
2. **The whole lane fills orange-red only during the 3 s telegraph (changed: the notes lit it for the race).** A
   290 m orange-red loop for 37 s would paint a big share of the floor in the danger colour and drown the enemy
   telegraphs inside it (300 enemies). During the race only the rims stay lit (alpha 0.35) plus a bright 26 m tongue
   ahead of each chariot (where it'll be in 1.2 s): that's the part you actually need to read.
3. **The chariots come out of the carceres and go back in (new).** Spawning on the loop would pop them in. The
   run-in and run-out are the straights' own extensions (z = ∓14, x −97 → −60), so the path stays two straights and two
   half-circles, and the carceres finally do something.
4. **Counter-clockwise (left turns), the Roman way.** Arbitrary but fixed: tests and the telegraph tongue depend on it.
5. **A jump clears a chariot (feet above 1.1 m).** Movement is the skill (CLAUDE.md §5): a full jump stays above
   1.1 m for 0.37 s, a chariot takes 0.26 s to pass ((5.0 + 0.7) / 22), so it's a timed trick with ~0.1 s of slack;
   a double jump makes it easy. Stepping off the track stays the main counter. 1.1 m is also the formation push's and
   the contact damage's height, so "above 1.1 m" means one thing in this arena.
6. **The hit zone is the whole lane width, not the chariot's 2.34 m.** "The darker lane is where you get hit" is the
   readable rule; a 0.8 m safe strip inside the lane's edges would contradict the floor.
7. **Chariots hit the boss for 600 (changed from the other hazards' token ¼).** The chariots are this arena's identity
   and his phase 3: luring him across the track should be a real play (P3 is 5,400 HP: 9 hits), not a rounding error.
   Open for the playtest (Q5).
8. **Ordinary enemies take 200 × 2 = 400 from a chariot:** enough to kill every ordinary type here (the toughest, the
   Brute: 180 × 2.15 = 387) and a Centurion, so a pass visibly clears the lane; elites take 200 and mostly survive.
9. **The Praetorian blocks 75 % from 120°, not the Murmillo's 90 % / 130° (char-art suggested the Murmillo's).** They
   come five at a time and the player's weapons auto-aim at the nearest (the shields); five 90 % doors would make the
   formation a damage sponge.
10. **Fewer loose shields in the table** (Thugs 1.2–1.6, Murmillos 0.6–1.0, Mercenaries 0.5–0.9). With the boss's
    scutum and the formations, the old weights would make this the shield arena; at these weights the end-of-run
    shield share (Thug + Archer + Murmillo + 5 × Centurion) ≈ 5.25 of the weights vs the Grand Arena's 5.5.
11. **No Beast Handler here.** Beasts are arenas 3–4; the Beastmaster's unlock still comes from there.
12. **Killing the Centurion must pay off (changed: the notes only said "they fight on").** If the line just keeps
    fighting, the officer is not worth hunting. Broken line: Stagger 1 s + shields down 3 s, then ordinary
    ShieldBearers. Same lesson as the Beast Handler ("kill the leader"), different payoff.
13. **The line has a coordinated attack, the push (new).** Without one, a 2.6 m/s wall that only does contact damage is
    something the player just walks away from. The push reuses the Centurion's Lunger state machine (brace = his
    wind-up, shove = his lunge); the escorts ride their slots, so the whole line moves with him.
14. **The leader holds at 2.6 m while his line stands (new).** Otherwise the Lunger keeps walking and his line walks
    through the player between pushes; holding keeps the wall in front of you, pressing.
15. **The Centurion is never an elite.** A Fast or Armored affix would break the march (Fast) or make the "kill the
    officer" lesson a slog (Armored ×2.5 HP).
16. **Guards come through the gates nearest the player (new `guardsNearestGates`).** `CallGuards` today uses gates
    0, 1, 2… in list order; on a 190 m floor that marches a formation for 40 s before it matters.
17. **The boss strides when far (new `closeInRange` / `closeInSpeed`: 6.5 m/s beyond 28 m).** He enters at the east
    apex; the player can be 190 m away. A minute of walking is dead air. (Also useful for the Grand Arena's C24.)
18. **His scutum blocks 50 % from 100°, not the Provincial Champion's 90 % / 140°.** His shield is at his side
    (char-art), the formations are this arena's shield lesson, and an 18,000 HP three-phase boss behind a 90 % door
    would take minutes.
19. **No new `BossAttack` or `EnemyBehaviour` values.** Spear Thrust = `Whip` (whipSlow 1 = no slow), Shield Bash =
    `Bash`, Javelin Volley = `Volley` (`FireStraight(arrow: true)` already exists), Shield Charge = `Charge`; the
    Praetorian is a `ShieldBearer`, the Centurion a `Lunger`. Fewer serialized-enum risks, less code.
20. **Phase 3 reuses phase 2's pair values** (every `Vector2Int (p1, p2)` such as `volleyCount`), plus explicit
    `phase3*` fields where it differs. Turning the pairs into triples would break every existing `BossDef` asset.
21. **`ChariotRace` reads the boss, the boss doesn't call `ChariotRace`.** It goes back to back while
    `BossController.Active.Phase >= backToBackFromBossPhase` (3) and halts when `.Defeated`: both are existing public
    properties, so the chariot and boss parts compile without each other.
22. **Javelins are aimed at the player's height** (`at.y = player.y + 0.9`). The volley aims at the boss's floor
    level + 0.9 today; a straight shot dies when it dips below the ground under it, so the 1.2 m spina would be a
    javelin-proof 100 m platform.
23. **`enemyHpMul 2.15`, `playerDamageTakenMul 1.45`.** The ladder's steps so far: HP +0.25, +0.25, +0.3; damage
    +0.15, +0.10, +0.10. +0.35 / +0.10 keeps the curve smooth; the races and the formations are the new difficulty.
24. **400 alive, densityMul 1.25, spawnMaxDistance 55.** 1.31× the floor: a little more of the horde, still inside the
    §8 budget the Grand Arena measured at 400 (178 fps at ≥ 250, p95 7.8 ms); spawns from your end of a long floor.
25. **18,000 HP.** One arena per run, so player power at 8:00 is similar in every arena (the Beast Lord's 15,000 fell
    in ~48 s to the bot); three phases and a 50 % frontal block should land around 75–120 s. Measure it.
26. **Crowd ~2,200** (0.75 per metre per tier; the Grand Arena used 0.6): "huge; senators, commanders". Instanced, cheap.
27. **Four terraces in the outer fields (proposal, Q2).** Each outer field is 27 m × 146 m of flat sand; the spina is
    the only height otherwise. SM_SpinaStairs fits any 1.2 m platform, so one simple extra piece does it.

## Open questions (for the orchestrator; my recommendation first)
- **Q1. The run-in and run-out on the floor.** Ask env-art to draw them darker too: two 4 × 37 m strips at z = ±14
  from x = −95 to −60, continuing the loop's straights. Without them the run-ins are only shown by the 3 s telegraph.
- **Q2. Terraces.** (a) env-art adds `SM_ImpTerrace` (12 × 5 m, 1.2 m, A6), stairs = `SM_SpinaStairs` (recommended);
  (b) reuse `SM_GrandTerrace` (white marble: an ART_DEBT line); (c) none (the spina only).
- **Q3. The formations part needs `EnemyManager.cs`.** Steering, facing, the push hit and the shield check live there,
  not in `EnemyManager.Leash.cs`: about six one-line hooks (listed below). Add the file to the formations part
  (recommended), or the orchestrator lands the hooks.
- **Q4. `PlaytestBot.cs`.** Nobody owns it. Recommended: give the chariot part a one-line log
  (`| races N (trampled X, hit you Y, jumped J, boss B)`) and the formations part `formations / linesBroken`; bot
  avoidance of the tongue is optional (`ChariotRace.Danger(p)`). Without the log the race and Centurion numbers can't
  be tuned.
- **Q5. Chariots vs the boss (600 a hit).** A playtest call: keep it unless he melts in P3.
- **Q6. The lap counter's eggs.** If `PR_LapCounter` has `Egg1`–`Egg7` children, `ChariotRace` tips one per race and
  rights them all when the races go back to back. Nice to have; skip if the model is one mesh.
- **Q7. The carceres' chariot bays.** The bays at z = ±14 must be open (no door mesh) with an arch ≥ 2.8 m wide and
  ≥ 2.8 m tall (the chariot is 2.34 m wide, 2.6 m to the plumes).
- **Q8. The sphendone's stands.** 4 m straight sections on r = 44 open ~0.9 m gaps at the top tier (the back row is at
  r ≈ 54). A wedge piece (`SM_ImpStand_Arc`) or 2 m sections; the layout handles either.
- **Q9. What his first kill unlocks.** R2's contract: `UL_Colosseum`, `DefeatBoss emperors_champion`.

## Needs from others
- **env-art:** Q1, Q2, Q6, Q7, Q8; piece pivots and orientation for the spina, metae and stairs (the layout follows
  their notes); the triumphal gate with hinged `DoorL` / `DoorR` (contract).
- **char-art:** none (their notes already give every part and size I need).
- **orchestrator:** Q3, Q4; the run list in `ProvincialPitSetup.cs` (line 72, append `ImperialArenaSetup.EnsureRun()`)
  and `BuildScripts.cs` (lines 20 and 38, append `ImperialArenaSetup.Scene`): no part owns them.
- **Spotted in passing (not R1):** `docs/BOSSES.md` says the Patron's guard makes him take "60 % less damage", but
  `BossSetup` sets `guardedDamageMul = 0.6` (he takes 60 %), and its own comment says 40 %.

## Merge order
Formations, chariot races and the boss don't depend on each other. **Layout and setup goes last**: it sets the new
`EnemyDef` fields, the `ArenaLayoutDef` chariot fields and calls `BossSetup.EnsureEmperorsChampion()`. If it starts in
parallel, it codes against the names below and only compiles after the other three are merged. The boss's tests
need `EN_Centurion` (made by the layout part): run them after everything is in.

## For each code part

### Chariot races — `ChariotRace.cs` (new), `ArenaLayoutDef.cs` (chariot fields), `ArenaBuilder.cs` (the hook)
**`ArenaLayoutDef`** — a new header `"Chariot races (Phase 5.16)"`:
| Field | Type | Imperial value | Meaning |
|---|---|---|---|
| `trackRadius` | float | 14 | the turns' radius round the metae (0 = no races in this arena) |
| `trackWestMeta`, `trackEastMeta` | Vector2 (XZ) | (−60, 0), (40, 0) | the turns' centres; same z (the track runs along X) |
| `trackWidth` | float | 4 | the lane's width |
| `trackStallX` | float | −97 | where chariots appear and vanish (inside the carceres, at z = ∓trackRadius) |
| `chariotFirst` | float | 80 | seconds into the run of the first race's telegraph |
| `chariotEvery` | float | 60 | seconds from one race's start to the next |
| `chariotTelegraph` | float | 3 | the whole path fills orange-red for this long first |
| `chariotSpeed` | float | 22 | m/s |
| `chariotCount` | int | 2 | the n-th launches lap ÷ count later (opposite phases) |
| `chariotLaps` | int | 2 | the last lap leaves from the north straight instead of turning |
| `chariotModel` | string | `"Props/PR_Chariot"` | under `Assets/Art/Models/` |

**`ChariotRace : MonoBehaviour, ITickable`** (`Instance`; `TickGroup.Enemies`, order 32, after `FireJets`):
- Fields copied from the layout by `ArenaBuilder` (the table above), plus tuning with defaults (like `FireJets`):
  `footprint = 5f` (m along the track: the 4.44 m model + margin), `jumpClear = 1.1f`, `playerDamage = 28f`,
  `playerKnockback = 13f` (sideways out of the lane, + 5 up), `enemyDamage = 200f`, `ordinaryEnemyMul = 2f`,
  `bossDamage = 600f` (Scripted enemies: flat, no knockback), `enemyKnockback = 10f` (sideways + 4 up),
  `tongueSeconds = 1.2f`, `rimAlpha = 0.35f`, `backToBackGap = 2f`, `backToBackFromBossPhase = 3` (0 = never),
  `telegraphColor` (the shared orange-red), `chariots` (Transform[]: the model roots, made by `ArenaBuilder`),
  `lapCounter` (Transform, optional).
- Path math, public and pure (EditMode-testable, no allocation): `float LapLength` (2 × (east − west) + 2πr = 288);
  `float PathLength` (run-in + laps × lap − πr + run-out = 606); `Vector3 PointAt(float s, out Vector3 forward)` (s =
  metres from the stall along a chariot's path; y = 0, the caller samples the height); `float LaneOffset(Vector3 p)`
  (distance from the nearest point of the centre line, run-in and run-out included); `bool InLane(Vector3 p, float pad)`
  (`LaneOffset ≤ width / 2 + pad`).
- Schedule: Idle → Telegraph (`chariotTelegraph`) → Running (chariot n launches at n × lap ÷ count ÷ speed s) →
  Idle when the last one is back in its stall. Next start: `chariotFirst + k × chariotEvery`; back to back: the next
  telegraph `backToBackGap` after the last chariot leaves, and one starts at once if none is running. Back to back while
  `BackToBack` (settable, for tests) or `BossController.Active` is alive with `Phase >= backToBackFromBossPhase`.
  Halted (no new race; chariots on the track vanish in a dust burst) once `BossController.Active.Defeated`, or by
  `Halt()`. Uses scene game time (pauses stop it), like `FireJets`.
- Each frame per running chariot: advance `s` by speed × dt; place the root on `PointAt` (height-sampled), facing
  `forward`, leaning 8° into the turns; spin `WheelL` / `WheelR` (r 0.54; char-art's notes: sign unverified, the safe
  form is `Rotate(root.right, deg, Space.World)`; cap the visual spin below ~45 fps), bob `Horse0–3` on y and swing
  `Horse<i>LegsF/B` in opposite phase.
- Hits, once per chariot pass (a per-chariot serial bumped every half lap, and per-enemy last-serial arrays like
  `FireJets`):
  - **Player:** inside the footprint box round the chariot (|along| ≤ footprint / 2 + 0.35, |side from the centre
    line| ≤ width / 2 + 0.35) with feet less than `jumpClear` above the lane: `playerDamage` (`DamageFlags.Area`),
    knockback away from the centre line. Feet above it while inside the box count `Jumped`.
  - **Enemies:** `QueryAlive(chariot, footprint / 2 + width / 2 + MaxRadius)`, the same box with the enemy's radius:
    Scripted → `bossDamage`; elite → `enemyDamage`; else × `ordinaryEnemyMul`; knockback sideways + up.
- Telegraphs: during Telegraph, every straight (incl. run-in/out) as one `TelegraphRenderer.Lane` and each turn as
  ~12 chords, filling over the telegraph; during Running, the same rims at `rimAlpha` (draw only within ~70 m of the
  player if the 512-quad budget gets tight) and a full-strength tongue of `speed × tongueSeconds` ahead of each
  chariot (chords through the turns).
- Feedback: `SfxId.Horn` twice at the telegraph; `AnnounceKind.Line` "CHARIOTS!"; `SfxId.Slam` on a trample (throttled);
  `CrowdExcitement.Spike(0.5)` at the start, `Spike(0.3)` for a pass that tramples 8+.
- Public: `bool Running`, `bool Telegraphing`, `bool BackToBack { get; set; }`, `int Races`, `EnemiesHit`, `Kills`,
  `PlayerHits`, `BossHits`, `Jumped`; `void StartNow()` (tests: telegraph now), `void Halt()`;
  `bool ChariotActive(int c)`, `Vector3 ChariotPosition(int c)`; `float Danger(Vector3 p)` (seconds until a chariot
  reaches p in the lane; +∞ if none: for the bot).
- Optional (Q6): if `lapCounter` has `Egg1`–`Egg7`, tip one per race started; right them all when back to back starts.
- **`ArenaBuilder` hook:** when `layout.trackRadius > 0`: a `ChariotRace` object under the arena root, the fields
  copied, `chariotCount` × the `chariotModel` FBX under it (inactive, not static, the environment palette material),
  and the `PR_LapCounter` piece's transform if there is one.
- **Tests:** EditMode `ChariotTrackTests`: lap = 288 ± 0.5; path = 606 ± 1 for 2 laps; `PointAt(0)` = (−97, −14)
  heading +X; the east apex (54, 0) is on the path; `LaneOffset` 0 on the centre line, 2 at the lane's edge, > 2 on
  the spina. PlayMode `ChariotRaceTests` (in the Imperial scene once it exists): the telegraph shows no chariot; two
  launch ~6.5 s apart and leave after ~27.5 s each; a grounded player in the lane is hit once per pass, one beside it
  isn't, one jumping above 1.1 m isn't; an ordinary enemy in the path dies, an elite takes 200, one beside the lane is
  untouched; `BackToBack` starts the next race within 2 s; `Halt` stops it.

### Formations — `EnemyDef.cs`, `EnemyManager.Leash.cs` (+ hooks in `EnemyManager.cs`, Q3)
**`EnemyDef`** — new fields under a header `"Formation (the Centurion, Phase 5.16)"`, after the escort fields:
| Field | Type | Default | Centurion | Meaning |
|---|---|---|---|---|
| `formationSlots` | Vector2[] | empty | (±2.6, 1.8), (±1.3, 1.8), (0, 1.8) | escorts hold these slots in the leader's frame (x right, y forward, m) instead of a leash; empty = a leash (the Beast Handler) |
| `formationHold` | float | 0 | 2.6 | a leader with a formed line stops this far from the player (0 = walks on) |
| `pushPad` | float | 0.8 | 0.8 | the push lane is the line's width + this |
| `pushDepth` | float | 2.6 | 2.6 | the push lane's depth in front of the front rank |
| `pushKnockback` | float | 9 | 9 | along the leader's facing |
| `freeEscortsOnDeath` | bool | **true** | false | true: escorts vanish when he dies (the Beast Handler, unchanged); false: the line breaks and they fight on |
| `breakStatus` | StatusDef | null | `SE_Stagger` | applied to every escort still in the line when he dies |
| `breakShieldsDown` | float | 0 | 3 | their shields are down this long (s) |
`escortCount` is still the count; with slots it's `min(escortCount, formationSlots.Length)`. The push itself reuses
the leader's Lunger numbers: `windup` 0.7, `attackRange` 4.2, `lungeSpeed` 8, `lungeTime` 0.25, `attackDamage` 16,
`attackCooldown` 3.5, `recover` 0.7.

**`EnemyManager.Leash.cs`:**
- Per slot: `_slotIndex` (−1 = on a leash or free), `_shieldsDownT`; per leader: `_formed` (escorts still in his line,
  kept by spawn, an escort's death and the break; no per-frame scans).
- `public static Vector3 SlotWorld(Vector3 leader, float leaderYaw, Vector2 slot)` (EditMode-testable).
- `bool InFormation(int i)` (a living leader and a slot); `int FormedCount(int leader)`.
- `SpawnEscorts`: with slots, escort k spawns at `SlotWorld(leader, yaw, slots[k])` (clamped inside), `_slotIndex = k`;
  `Formations++`. (The leader's yaw already faces the player at spawn.)
- `Vector3 FormationSteer(int i, out float faceYaw)`: toward the slot (time constant ~0.35 s, capped at 1.6 × his
  speed); while the leader is in his Attack state (the push), the leader's velocity exactly; `faceYaw` = the leader's
  yaw. No rope drawn and no `LeashClamp` for formed escorts.
- `Vector3 LeaderHold(int i, Vector3 desired, float dist)`: zero while `FormedCount > 0`, he's in Move and
  `dist < formationHold`.
- `void DrawPush(int leader, float fill)`: during his Windup, a `Lane` from the front rank's centre along his facing,
  (slots' width + 2 × escort radius + `pushPad`) wide and `pushDepth` deep, in the telegraph colour.
- `bool PushHit(int i)`: during the leader's Attack, a formed escort touching the player (radius + 0.35 + 0.25, feet
  < 1.1 m) hits once per push (the leader's `_attackHit` flag): `attackDamage`, `pushKnockback` along his facing.
- `bool ShieldsDown(int i) => _shieldsDownT[i] > 0f` (ticked down with the rest).
- `FreeEscorts`: unchanged when `freeEscortsOnDeath`; else for each escort of this leader: drop the leash and the slot,
  `ApplyStatus(breakStatus)`, `_shieldsDownT = breakShieldsDown`; `LinesBroken++` once.
- Public counters: `Formations`, `LinesBroken` (bot log, tests). `EscortsFreed` stays the freed count only.

**Hooks in `EnemyManager.cs`** (Q3; each a line or two): after `Think`, a formed escort's desired velocity and facing
come from `FormationSteer`, a leader's through `LeaderHold`; skip `LeashClamp` for formed escorts; the Lunger's Windup
calls `DrawPush` when he has a formed line; the contact block calls `PushHit`; `IsBlocking` adds `&& !ShieldsDown(i)`;
`Kill` decrements the dead escort's leader's `_formed`.

**Tests:** EditMode `FormationTests.SlotWorld_RotatesWithTheLeader`. PlayMode `FormationTests`: a Centurion spawns
five Praetorians on his slots and they keep them (± 0.5 m) as he walks and turns 90°; a hit from the front is blocked
(75 %), one from behind isn't; the push draws its lane and hits a player inside it once, not one 4 m to the side;
killing him staggers the line with shields down (a frontal hit lands in full), and after 3 s they block again and are
still alive (`EscortsFreed` unchanged, `LinesBroken` 1); the Beast Handler still frees his curs.

### The boss — `BossDef.cs`, `BossController.cs`, `BossSetup.cs`
**`BossDef`** — new fields (defaults keep every existing boss as it is):
| Field | Type | Default | Emperor's Champion | Where |
|---|---|---|---|---|
| `phase3At` | float [0, 0.9] | 0 (none) | 0.3 | new header "Phase 3" |
| `phase3Attacks` | BossAttack[] | empty (= phase 2's) | Whip, Bash, Volley, Charge | " |
| `phase3Text` | string | "" | `"\"FOR THE EMPEROR!\""` | " |
| `phase3Speed` | float | 1.4 | 1.4 | " (wind-ups and recovery ÷ this) |
| `phase3MoveSpeed` | float | 0 (= enraged) | 4.6 | " |
| `phase3ChargeRepeats` | int | 0 (= `chargeRepeats.y`) | 2 | " |
| `guardsFromPhase` | int ≥ 1 | 1 | 2 | "Call the Guards": the clock runs from this phase; reaching it calls them at once |
| `guardsNearestGates` | bool | false | true | " : the `guardSpotsUsed` gates nearest the player, which open |
| `volleyJavelins` | bool | false | true | "Golden Volley": arrows (`FireStraight(arrow: true)`) aimed at the player's height |
| `closeInRange`, `closeInSpeed` | float | 0, 0 | 28, 6.5 | "Movement": strides while the player is farther than this |

**`BossController`:**
- `_phase` runs 1 → 2 → 3; the Enraging state remembers the phase it leads to. P2 starts as today (from P1 only);
  P3 when `_phase == 2 && def.phase3At > 0 && Hp01 <= def.phase3At` (not while entering, roaring or enraging), with
  `phase3Text` (`AnnounceKind.BossPhase`), the roar SFX, invulnerable `enrageRoar`. The phase-2 extras (fire ring,
  spike volley, cages, jets, blades) fire only on the way to P2. A hit from above 60 % to below 30 % goes P2, then P3.
- Every `_phase == 2` check becomes `>= 2` (`Speed`, `Choose`, the repeat counts, the enraged move speed, the slam's
  fire, `BombCount`, `VolleyCount`, `VolleyArc`). `Speed` = `phase3Speed` in P3. `Choose` uses `phase3Attacks` in P3
  (phase 2's if empty). The charge repeats in P3 = `phase3ChargeRepeats` if > 0. The public `Phase` returns 3 in P3
  (`ChariotRace` reads it).
- Chase: P3 speed `phase3MoveSpeed` (enraged if 0); when `closeInRange > 0` and the player is farther, `closeInSpeed`.
- Guards: `GuardsClock` only while `_phase >= def.guardsFromPhase`; when an Enraging into that phase ends, `CallGuards()`
  at once. With `guardsNearestGates`: rank the gates by distance to the player (a selection over ≤ 8 gates, no
  allocation), use the `clamp(guardSpotsUsed, 1, gates.Count)` nearest, guard k through gate k % use, open it 3 s.
- Volley with `volleyJavelins`: `FireStraight(Hand, at, volleySpeed, volleyDamage, 0.3f, volleyRange, arrow: true)`
  with `at.y = player.y + 0.9`. Pose: ArmR drawn back over the shoulder in the wind-up, flung forward on release
  (the quiver is the `Javelins` part on his back; spawning from `Hand` is fine).
- Spear Thrust pose (optional): char-art's `Spear` is a separate child of ArmR: swing ArmR forward and tilt `Spear`
  ~+90° about local X so the blade levels (sign unverified in Unity), instead of `Whip`'s left-arm pose.
- Optional: flap `WingL` / `WingR` at the phase roars.
- **`BossSetup`:** `EmperorsChampionEnemyPath = "Assets/Data/Enemies/EN_EmperorsChampion.asset"`,
  `EmperorsChampionPath = "Assets/Data/Bosses/BD_EmperorsChampion.asset"`, `EnsureEmperorsChampion()` (menu
  `Gladiator/Setup/Bosses/Create Emperor's Champion Data`), created once with:
  - body: id `emperors_champion`, "The Emperor's Champion", `Scripted`, maxHp 18000, radius 1.25, knockbackTaken 0,
    blockArc 100, blockReduction 0.5, contactDamage 0, moveSpeed 0, xp 0, goldChance 0, prewarm 1, model
    `Assets/Art/Models/Enemies/EN_EmperorsChampion.fbx`;
  - words: displayName "THE EMPEROR'S CHAMPION", title "FIRST SPEAR OF THE EMPIRE", enterText "THE EMPEROR'S
    CHAMPION!", fallText "THE EMPEROR'S CHAMPION FALLS!", guardsText "THE PRAETORIAN GUARD!", phase2Text
    "\"PRAETORIANS, TO ME!\"", phase3Text "\"FOR THE EMPEROR!\"", shortName "The Emperor's Champion", deathLine "The
    Emperor's Champion turns to the imperial box. The thumb goes down.", victoryLine "The Emperor's own champion lies
    in the sand, and the Emperor is on his feet.";
  - attacks: phase1 {Whip, Bash, Volley}, phase2 and phase3 {Whip, Bash, Volley, Charge};
  - movement: moveSpeed 3.4, enragedMoveSpeed 3.9, phase3MoveSpeed 4.6, turnRate 160, closeInRange 28, closeInSpeed
    6.5, contactDamage 12, contactInterval 0.8, recover 0.6;
  - Spear Thrust: whipLength 10, whipWidth 1.3, whipWindup 0.65, whipDamage 22, whipSlow 1, whipSlowSeconds 0;
  - Shield Bash: bashRange 4.5, bashAngle 110, bashWindup 0.65, bashDamage 20, bashKnockback 12;
  - Javelins: volleyCount (5, 7), volleyArc (36, 50), volleyWindup 0.85, volleySpeed 22, volleyDamage 16,
    volleyRange 30, volleyJavelins true;
  - Shield Charge: chargeWindup 0.9, chargeSpeed 17, chargeLength 20, chargeWidth 2.6, chargeDamage 26,
    chargeKnockback 13, chargeStun 1.8, stunDamageTakenMul 1.5, chargeRepeats (1, 1), phase3ChargeRepeats 2;
  - guards: { `EN_Centurion` loaded by its literal path `"Assets/Data/Enemies/EN_Centurion.asset"` (no compile
    dependency on the layout part; fill it in when it exists, like the Patron's mercenary) }, guardCount (2, 2),
    guardsEvery 26, guardsFromPhase 2, guardsNearestGates true, guardSpotsUsed 2;
  - phases: phase2At 0.6, enrageSpeed 1.2, enrageRoar 1.5, phase3At 0.3, phase3Speed 1.4; phase2FireRing,
    phase2SpikeVolley, phase2FireJets, phase2Blades all false; scatterOnDefeat true; rewardFloor Rare; defeatGold 0.
- **Tests:** PlayMode `EmperorsChampionTests`: he enters through gate 0 and strides when you're 60 m away (> 6 m/s);
  the thrust, bash and volley telegraph and hit (a javelin reaches a player standing on the spina); at 60 % two
  Centurions with their lines appear at the two gates nearest the player, and the charge appears and stuns him on the
  obelisk (takes ×1.5); at 30 % `Phase == 3`, wind-ups ÷ 1.4, two charges in a row, and `ChariotRace.BackToBack`
  behaviour starts (integration); at his fall the races halt and every enemy is dragged off. Regression (EditMode or
  PlayMode): a `BossDef` with `phase3At 0` never reaches phase 3, and Gorm still lights the fence on the way to P2.

### Layout and setup — `ImperialArenaLayout.cs`, `ImperialArenaSetup.cs` (new)
**`ImperialArenaLayout`** (like `GrandArenaLayout`; `Path = "Assets/Data/Arenas/AL_ImperialArena.asset"`; menu
`Gladiator/Arenas/Regenerate Imperial Arena Layout (overwrites)`; designer-owned once it exists):
- Constants: `WestX −95`, `SphendoneX 51`, `HalfZ 44` (= the sphendone's radius), `ArcSegments 24`, `PlayInset 1.1`,
  `SpinaX0 −60`, `SpinaX1 40`, `SpinaHalfWidth 3`, `SpinaHeight 1.2`, `TrackRadius 14`, `TrackWidth 4`, `StallX −97`,
  `ObeliskX −10`, `LapCounterX 20`, `PulvinarX −10`, `PulvinarWidth 14`, `CarceresBay 4` (22 bays), side gates at
  x = −50 and 30, spina stairs at x = −45, −20, 5, 30; the stand profile as `GrandArenaLayout`'s.
- `WallLine()`: clockwise from above, from (−95, 44) east to (51, 44), round the sphendone through (95, 0) to
  (51, −44) in 24 segments, west to (−95, −44). `Outline()` = `GrandArenaLayout.Inset(WallLine(), PlayInset)`
  (`PlayArea()` ≈ 15,315). `public static bool OnTrack(Vector3 p, float pad)` for the tests (or `ChariotRace`'s).
- Values: id `imperial_arena`, "The Imperial Arena", tier 4, centre (0, 0, 0), radius 42.9, aspect 1, fog (80, 330),
  playerSpawn (−10, 0.1, −26), playerYaw 0, maxAlive 400, densityMul 1.25, spawnMaxDistance 55, eliteEvery 60,
  sunEuler ~(45, −35, 0) (warm), enemyHpMul 2.15, playerDamageTakenMul 1.45, gateModel `Environment/SM_ImpGate`,
  secrets None, guardSpots empty; the chariot fields as in the chariot table above.
- Gates: [0] (95, 0) yaw −90, model `Environment/SM_TriumphalGate` (bossGate 0); (−50, 44) and (30, 44) yaw 180;
  (−50, −44) and (30, −44) yaw 0.
- Pieces: `SM_ImpFloor` (Walkable); 22 × `SM_Carceres` along x = −95 facing east (Decor + invisible stone; the bays at
  z = ±14 are the chariots'); `SM_ImpStand_4m` along the long walls (cut for the gates and the pulvinar, scaled to fit
  each run) and the sphendone (Q8); `SM_Pulvinar` at (−10, 44) yaw 180; spina: 10 × `SM_Spina_10m` (Walkable), two
  `SM_SpinaEnd` metae (Solid) at (−60, 0) and (40, 0), `SM_Obelisk` (Solid) at (−10, 1.2, 0), `PR_LapCounter` (Prop)
  at (20, 1.2, 0), 8 × `SM_SpinaStairs` (Walkable); terraces per Q2 at (−75, 41.5), (10, 41.5), (−80, −41.5),
  (0, −41.5) with `SM_SpinaStairs` on their track side. Orientation and pivots per `R1-env-art.md`.
- Crowd: per tier per run at 0.75 per metre (none over the carceres), 16 at the pulvinar. `solidWalls` and `boundary`
  along every wall-line edge (as the Grand Arena), `solidWallHeight 8.4`.
- Braziers: (−16, 1.2, ±2.2), (−4, 1.2, ±2.2), (88, 0, ±6). Chests (14) and altars (3): the lists in ARENAS.md §5.
  Zones, barricades, spike traps, cages, fire jets and blades: empty.
**`ImperialArenaSetup`** (like `GrandArenaSetup`): `Scene = "Assets/Scenes/Arenas/ImperialArena.unity"`,
`RunPath "Assets/Data/Runs/RD_ImperialArena.asset"`, `UnlockPath MetaSetup.Dir + "UL_ImperialArena.asset"`,
`TablePath "Assets/Data/Arenas/ST_ImperialArena.asset"`, `PraetorianPath`, `CenturionPath`; `Build()` (menu
`Gladiator/Arenas/Build Imperial Arena`): the layout, `EnsureEnemies()`, the boss (`BossSetup.EnsureEmperorsChampion()`,
bossGate 0), the table, `EnsureRun()`, `ArenaBuilder.Build`.
- `EnsureRun`: `UL_ImperialArena` (id `imperial_arena`, `DefeatBoss patron` × 1, hint *"The Patron is bankrupt. The
  Emperor has heard your name."*, description *"The Emperor has sent for you. His hippodrome of travertine and purple:
  chariots racing round the spina, the Praetorian Guard in formation, and the Emperor's own champion."*), then
  `MetaSetup.CreateOrUpdate()`; `RD_ImperialArena` (id `imperial_arena`, arena "THE IMPERIAL ARENA", description
  *"The Emperor's hippodrome, the biggest floor yet: chariots race round the spina and trample everything on the track,
  and the Praetorian Guard marches in formation. Tougher enemies, more Renown (×3)."*).
- `EnsureEnemies`: `EN_Praetorian` (ShieldBearer; maxHp 70, moveSpeed 3.0, radius 0.5, acceleration 14, contactDamage
  10, blockArc 120, blockReduction 0.75, knockbackTaken 0.25, turnRate 180, xp 4, goldChance 0.06, prewarm 40, anim
  `PartAnim_Murmillo`) and `EN_Centurion` (Lunger; maxHp 160, moveSpeed 2.6, speedJitter 0, radius 0.42,
  acceleration 10, contactDamage 10, windup 0.7, attackRange 4.2, lungeSpeed 8, lungeTime 0.25, attackDamage 16,
  attackCooldown 3.5, recover 0.7, knockbackTaken 0.3, turnRate 80, xp 12, goldChance 0.35, prewarm 8, the humanoid
  anim; escort `EN_Praetorian`, escortCount 5, the formation fields from the formations table, breakStatus
  `SE_Stagger`).
- `EnsureTable`: the ARENAS.md §5 table (Centurion `elite: false`), affixes from the Slave Pit's table.
- **Tests:** PlayMode `ImperialArenaTests`: locked until the Patron falls, then a run there is tier 4 with its
  multipliers; bigger than the Grand Arena and keeps the horde in (carceres, both sides, the curve, the triumphal
  gate); nothing on the track (+ 1.5 m); the spina is walkable at 1.2 m and climbed by the horde; a Centurion from the
  table comes with his line. Add the arena to `ArenaNavTests` if it lists arenas.

## Ideas for the BACKLOG (not R1)
Closing ranks when a Praetorian dies; a testudo formation (roofed: immune to lobs from above); the Emperor's thumb at
the boss's fall; the Champion entering on a chariot; a Codex challenge "leap 10 chariots in one run"; betting on the
race (the Bookmaker: Blues or Greens); phase ticks at 60 % / 30 % on the boss bar.

## Verified / unverified
- **Verified (arithmetic only):** the areas (wall line 15,889 m², play area 15,315 m² with the curve as 24 segments,
  1.31× the Grand Arena's 11,674), the lap (287.96 m), the race path (606 m, 27.5 s at 22 m/s, launch gap 6.5 s,
  ~37 s per race), the jump window (0.37 s above 1.1 m with jumpHeight 1.7, gravity 30, fall ×1.35, vs 0.26 s for a
  chariot to pass), and that every field named above as "existing" exists in today's code.
- **Unverified:** everything about feel and balance in play: the race cadence, the chariot damage, the formation's
  threat, the boss's HP and fight length, the Centurion's weight. These need the orchestrator's bot runs and the
  producer's playtest.
