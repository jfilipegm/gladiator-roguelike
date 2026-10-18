# Concerns

Design / scope / perf / readability problems spotted, each with a proposed fix. Status: OPEN · WATCH · RESOLVED.

## C1 — Slave Pit size vs. 300 enemies (OPEN)
A ~35 m pit has ~960 m² of floor. 300 enemies at ~0.45 m radius cover roughly 20 % of it, before mounds, mud and fences. With slide momentum and a third-person camera, that risks a "wall of bodies" where positioning (the player's main job) disappears.
**Proposal:** make the Slave Pit's *playable* floor ~42 m across, and give the Director a per-arena `maxAlive` (Slave Pit ≈ 200–250, rising in bigger arenas). Hold the 300-enemy perf budget in the stress test and the larger arenas. Pressure in the pit comes from enemy HP/speed instead of raw count. Decide after M2.3 with real numbers.

## C2 — Off-screen threats with a free-orbit camera (MITIGATED in M2.2)
With no lock-on and auto-aim, the player will often look away from enemies closing in from behind.
**Done:** `ThreatIndicators` — up to 16 screen-edge chevrons: pulsing orange-red for any wind-up (Slinger, Fire-Pot, Brute), affix colour for elites. Not shown for plain melee swarmers on purpose (noise). **Still to judge in playtest:** whether 16 is too many in a 300-enemy swarm, and whether the default camera pitch should be higher.

## C3 — Camera squeeze against thin obstacles (WATCH)
When the player backs onto a pillar with the pillar between them and the camera, the only clear spot is right behind the player's head (≈0.45 m). That's handled: the player turns shadows-only below 0.75 m. It can still feel cramped. Arena 3 uses pillars as line-of-sight blockers, so watch it there.
**Proposal if it bothers at Gate 1:** fade small occluders (dithered transparency on a shared material) instead of pulling the camera in.

## C4 — Measuring performance (RESOLVED in M1.2)
Editor Play-mode frame times swing between ~18 fps and ~180 fps depending on focus and bridge traffic, so they're useless for the §8 budget.
**Done:** `BuildScripts.BuildLinuxDev` produces a standalone development build; `-bot -profile` / `-stress -stressCount N` runs write the CSV + summary. Gate reports quote those numbers only. Caveat: they're from this PC (not a "mid-range" reference machine).

## C5 — Enemy rendering cost at 1,000 enemies (WATCH)
Each enemy is a pooled GameObject with ~6 rigid-part MeshRenderers. At 1,000 enemies frame time is bimodal: ~6 ms when the crowd is partly off-screen, ~15–16 ms when the camera faces all of it (draw submission, not gameplay; gameplay is 3.5 ms). 500 enemies stays at ~5 ms / p95 8 ms, so the §8 budget (300 → 500 peak at 60 fps) holds with a wide margin on this PC.
**Proposal if an arena really needs 1,000+ on screen (Colosseum):** draw enemies with `Graphics.RenderMeshInstanced` per (enemy type × part), with part matrices computed in the central tick (the SoA state already holds everything); keep GameObjects only for elites/bosses. Decide in the Phase 4 perf pass with real arena numbers.

## C6 — Kiting enemies pile up (NEW in M2.2)
Slingers and Fire-Pot Throwers keep 6–11 m away, so a melee build rarely reaches them. In a 60 s mixed stress run they accumulated until their pools ran dry. In real play that becomes a wall of ranged fire nobody can clear.
**Proposal (M2.3 Director):** a per-type max-alive in the arena's spawn table (ranged/area types low, e.g. ≤ 8–12 each in Arena 1), and ranged enemies slowly advancing when they've been alive too long. Weapons with reach (Spear, Javelins in M2.5) are the player-side answer.

