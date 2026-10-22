# R8-A streams: notes (seeded streams and forks for every run-shaping roll)

Worker for part **R8-A streams** of `docs/tracks/R7-contract.md` (wave 2), branch `claude/focused-booth-810345` (from
`origin/phase5-daily` at 526843e). Code and tests only: no Unity, no Blender. `Tools/ccheck`: **errors=0, 0 warnings**
(all six assemblies; no R8-STUB names: this part calls nothing B or C adds). Tests **written, not run**.

## Files
| File | What |
|---|---|
| `Assets/Scripts/Gameplay/Combat/GameRandom.cs` (new) | `RngStream` (byte, 15 values, append only), `Rng` (struct xorshift32), `GameRandom` (streams, forks, legacy API, `Hash`, `KeyOf`, `SeedText`, `TryParseSeed`). **Needs a `.meta`:** Unity makes it on import; commit it with the merge. |
| `Assets/Scripts/Gameplay/Combat/DamageInfo.cs` | The old `GameRandom` class removed (moved). |
| `Run/LevelUpController.cs`, `Upgrades/UpgradeRoller.cs` | Rows 1–3: `_opens[4]`; `Open` reseeds Cards with `(source << 16) \| nth`; card count and both roller draws on Cards; `OpensOf(PickSource)`. |
| `Director/Director.cs`, `Arena/ArenaGates.cs` | Rows 4–7: `_steady` fork (Director, 0) in Awake; per swarm `Fork(Swarm, SwarmsStarted)` (type, gate, angle, spread); per elite `Fork(Elite, _eliteRolls++)` (type, affix, gate; timer elites and `SpawnEliteAt`); `Pick(…, ref Rng)`; ring angle, tries, depth and gate spread on Spawns; `SwarmEntry`, `SwarmAngle`, `SwarmGate` getters; `ArenaGates.Pick(…, ref Rng)` (the old overload draws from Spawns). |
| `Enemies/EnemyManager.cs`, `EnemyManager.Gilded.cs`, `EnemyProjectiles.cs`, `Pickups/PickupSystem.cs` | Rows 8–10: every draw in `Spawn` and the AI timers, the Gilded blink, the fire-pot scatter on Enemies; the gold chance and the Gilded coin burst on Loot. Projectile spin, pickup bob and pop stay legacy (Misc). |
| `Weapons/WeaponSystem.cs`, `Weapons/PlayerProjectiles.cs`, `Gladiators/VerdictTrait.cs`, `Upgrades/BoonEffects.cs` | Row 11: crits and Boon procs on Combat. |
| `Boss/BossController.cs` | Row 12: `Rng _rng = Fork(Boss, KeyOf(def.id))` in `SpawnBody` (both `Begin` and `BeginRise` go through it); attack choice, bomb scatter, beast pack, guard count through it. |
| `Arena/Hypogeum.cs`, `Arena/BeastCages.cs` | Row 13: `Fork(Hazards, KeyOf("hypogeum" / "beast_cages"))` in Awake. |
| `Gladiators/ScavengerTrait.cs` | Row 14: `Scavenger.SeedRolls()` = `Fork(Traits, KeyOf("scavenger"))`, called by `ScavengerTrait.Install`. |
| `Interactables/Interactables.cs`, `Crowd/CrowdGifts.cs`, `Crowd/ArenaMaster.cs` | Rows 15–17: R7's `uint _rng` + salt + `Rand01()` replaced by `Rng _rng` = `Fork(Map/Gifts/Whims, KeyOf("interactables"/"crowd_gifts"/"arena_master"))` in Awake. |
| `Interactables/GoldenRat.cs`, `AshenStranger.cs`, `RattlingCrate.cs`, `Bookmaker.cs` | Rows 18–21: `UnityEngine.Random` → `Fork(Map, KeyOf("golden_rat"/"ashen_stranger"/"rattling_crate"/"bookmaker"))` at the top of `Start`; the "not GameRandom" comments gone; `GoldenRat.Hole` added (`AppearAt` existed). |
| Tests | new `Assets/Tests/EditMode/RandomStreamTests.cs` (21 tests), `Assets/Tests/PlayMode/SeedReplayTests.cs` (2 tests). No older test edited (below). |

## How it works
- **`BeginRun(seed)`:** `RunSeed = seed`, `state[s] = Hash(seed, s, 0)` for all 16 states. **`Seed(int)`** (tests, and
  R7's `GameSession` until R8-B replaces it): `RunSeed = (uint)seed`, every state = seed (0 → `DefaultSeed`), so each
  stream replays the old single generator. **`Reseed(s, key)`:** `state[s] = Hash(RunSeed, s, key)`. **`Fork(s, key)`:**
  `new Rng(Hash(RunSeed, s | 0x100, key))`: depends only on the run seed and the key, never on any stream's state, so a
  component's rolls don't depend on Unity's `Start` order or on what was drawn before.
- **`Hash`** is murmur3_32 of the three words (little-endian, seed 0), 0 mapped to `DefaultSeed`. Checked against the
  published murmur3 vectors in Python; `RandomStreamTests.Hash_IsMurmur3_Pinned` pins three values, because changing it
  changes every seeded run and every daily.
- Launch (`SubsystemRegistration`) and the static initialiser (EditMode) both `BeginRun(DefaultSeed)`.
- **Key scheme:** cards `(PickSource << 16) | nth open of that source`; swarm n = `SwarmsStarted` before it starts (whims'
  swarms count); elite n = `_eliteRolls`, bumped on every roll (timer and champion), even when no elite type is active
  yet (that's clock-determined); the boss = its `id`; components = `KeyOf(name)` (FNV-1a, hashed once in Awake/Start).
- Zero GC: the states are a static `Rng[16]`, forks are structs in fields or locals, draws are plain arithmetic
  (`RandomStreamTests.Draws_Forks_AndReseeds_DontAllocate` asserts it over 10,000 rounds).

## Decisions (one-line reasons)
1. **The states are an `Rng[]`, not a `uint[]`, plus `ref Rng GameRandom.Stream(s)`.** Same memory; stream draws and fork
   draws share one implementation, and `ArenaGates`' old overload passes `ref GameRandom.Stream(Spawns)` to the new one
   instead of duplicating its loop.
2. **`Rng.NextUInt` maps a 0 state to `DefaultSeed`.** A `default(Rng)` field nobody seeded (a component added in a test
   without `Install`/`Awake`) would otherwise return 0 forever; one predictable branch, the formulas unchanged.
3. **The elite fork also picks the elite's gate** (the contract names type and affix). Free, and the nth elite then comes
   through the same gate for the same player position; its draws come after type and affix, so those are as specified.
4. **A swarm's gate is picked inside `BeginSwarm`, after its type** (`AnyGate = -2`), so the gate comes from the swarm's
   fork; the whim's `StartSwarmThrough(gate)` still passes its own gate.
5. **The rat and the stranger roll their chance even when `alwaysAppear`, and pre-roll their hole/spot at `Start`.**
   `alwaysAppear` then only forces the outcome, so a test sees the seed's own time and hole, and `Hole` is known from
   the start (the contract's test getter); "whether, when, where" are all fixed at run start, as spec §3.4 says.
6. **The Bookmaker and the stranger use a local fork** (all their draws are in `Start`); the rat and the crate keep a field
   (the wander seed and the rattles come later).
7. **`TryParseSeed` takes 1–8 hex digits, any case, an optional `0x`, dashes or spaces anywhere, surrounding whitespace;
   accepts 0.** R8-B decides what a seed of 0 means (`BeginRun(0)` is valid: every state comes from `Hash`).

## Tests (written, not run: the orchestrator runs them)
- **EditMode `RandomStreamTests`** (21): `Seed(12345)` → five `Next01(Cards)` and five `Value` equal a hand-rolled
  xorshift32 (first value pinned 0.8963085); `Seed(0)` = DefaultSeed; `BeginRun` gives 16 pairwise different first values;
  the same seed twice → the same 100 draws per stream (read in another order); another seed differs; 1,000 Combat draws
  don't move Cards; the legacy API is Misc; `Reseed` same key = same, other key/other source/other run seed = different;
  `Fork` by key differs, repeats after the same `BeginRun`, ignores stream draws, never equals a stream state, moves no
  stream; `Stream(s)` is a live ref; `default(Rng)` still rolls; `KeyOf` = `StableId` (pinned); `Hash` pinned, never 0 over
  11,648 inputs, a one-bit change flips 15.99 bits on average and every output bit with p 0.496–0.503 (asserted 15–17 and
  0.45–0.55; numbers from a Python replica of the test); `SeedText`/`TryParseSeed` round-trip and reject junk; `Range(int)`
  never returns its max (and reaches both ends); float ranges stay in range; no allocation.
- **PlayMode `SeedReplayTests`** (2). Each load: `GameRandom.BeginRun(S)` then `LoadSceneAsync`; the second load with `S`
  also makes 300 Combat + 50 Map draws before loading and, between events, 300 Combat draws and 20 enemies spawned and
  killed (`KillAll`: jitter, gold, pops), then a `DespawnAll`. To keep the two loads comparable the test pins what play
  would otherwise change: `DevSettings.Timeless` with the clock at 200 s, Director density 0 (swarms roll their type and
  side but spawn none), `Favor.gainMul = 0` and `ArenaMaster.boredomMul = 0` (the kills can't earn a gift or a whim),
  weapons removed, the player at the centre at full HP, everything despawned after each record.
  - `TestArena_SameSeed_ReplaysCardsSwarmsElitesGiftsAndWhims`: the first level-up's offer ids (then `Skip`), three
    `TriggerSwarmNow` (entry, angle, gate), two `TriggerEliteNow` (type and affix from `OnDirector`, the slot looked up by
    position), four `Throw(null)` (kind and landing spot), two `FireWhim` → all equal; then 5 s of the horde with
    `GameLoop.TrackHeapPerSystem`, **logged, not asserted** (the project's rule since R3: the Editor's heap counter moves
    with the Editor; the bot run in the player is the measurement); then `S'` → at least one record differs.
  - `SlavePit_SameSeed_SameChestsRatAndCrate`: `alwaysAppear` set on the rat in `sceneLoaded` (after Awake, before
    Start); the first three ready props (two chests and the altar), the rat's `AppearAt` and `Hole`, the crate's
    `SpotIndex` → equal on the second load; `S'` → at least one differs.
- **Older tests: none needed an edit.** `StatAndUpgradeTests`, `StatsAndVowTests`, `EvolutionTests` and EditMode
  `InteractableTests` seed and call `UpgradeRoller.Roll` directly: the roller only draws Cards, which `Seed(n)` starts at
  n, so their sequences are exactly the old ones. PlayMode `InteractablePlayTests.Boot` seeds 5150: the chests now come
  from a fork of 5150 (another fixed placement), and its only placement assertion holds for any seed (each spot is found
  by up to 40 tries for clear, flat ground away from the player). `RunVarietyTests` keeps working: its same-seed loads use
  `Seed(1234)` (same `RunSeed`, same forks), its two menu runs get different seeds from `GameSession`. Tests that start
  through `LevelUpController` (`LevelUpTests`, `InteractablePlayTests`) now see a reseeded Cards stream per pick; none of
  them asserts specific cards.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings, all six assemblies; murmur3 port matches the published vectors
  (Python); the avalanche and pinned numbers in the tests come from a Python replica.
- **Unverified (needs Unity):** every test above; that `sceneLoaded` runs before the rat's `Start` in an async load; that
  the test arena has swarm and elite types active at 200 s (RunVarietyTests relies on the same); the heap log; the bot runs.

## Needs from others / for the orchestrator
- **R8-B:** call `GameRandom.BeginRun(seed)` (not `Seed(int)`) before every load. Until then (R7's `GameSession`),
  `Seed(int)` starts all 16 streams at the same value, so e.g. the first crit roll and the first card roll are the same
  number: harmless, but streams only become independent with `BeginRun`. R7's `SeedRun` skips bot/stress runs, so until
  R8-B every bot run of a launch has `RunSeed = DefaultSeed` and the same chests, gifts, whims and secrets (before, R7's
  salted generators varied per run within a launch); `-runSeed` fixes that.
- **The `.meta` for `GameRandom.cs`** is generated on import: commit it.
- **CONCERNS** (spec §3.2): rows 15–17 repeated every run and the global generator every launch; closed by R8 once R8-B
  seeds every run.
- Nothing calls R8-B or R8-C names: no `R8-STUB` marks in this part.
