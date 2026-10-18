# Concerns

Design / scope / perf / readability problems spotted, each with a proposed fix. Status: OPEN · WATCH · RESOLVED.

## C1 — Slave Pit size vs. 300 enemies (OPEN)
A ~35 m pit has ~960 m² of floor. 300 enemies at ~0.45 m radius cover roughly 20 % of it, before mounds, mud and fences. With slide momentum and a third-person camera, that risks a "wall of bodies" where positioning (the player's main job) disappears.
**Proposal:** make the Slave Pit's *playable* floor ~42 m across, and give the Director a per-arena `maxAlive` (Slave Pit ≈ 200–250, rising in bigger arenas). Hold the 300-enemy perf budget in the stress test and the larger arenas. Pressure in the pit comes from enemy HP/speed instead of raw count. Decide after M2.3 with real numbers.

## C2 — Off-screen threats with a free-orbit camera (WATCH)
With no lock-on and auto-aim, the player will often look away from enemies closing in from behind.
**Proposal:** cheap screen-edge threat indicators (orange-red chevrons) for ranged wind-ups and elites only, plus a slightly high default camera pitch. Revisit at Gate 1 feedback.

## C3 — Camera squeeze against thin obstacles (WATCH)
When the player backs onto a pillar with the pillar between them and the camera, the only clear spot is right behind the player's head (≈0.45 m). That's handled: the player turns shadows-only below 0.75 m. It can still feel cramped. Arena 3 uses pillars as line-of-sight blockers, so watch it there.
**Proposal if it bothers at Gate 1:** fade small occluders (dithered transparency on a shared material) instead of pulling the camera in.

## C4 — Measuring performance (OPEN, tooling)
Editor Play-mode frame times swing between ~18 fps and ~180 fps depending on focus and bridge traffic, so they're useless for the §8 budget.
**Plan:** M2.1 adds a scripted standalone development build + `-stress` run that writes the CSV; gate reports quote those numbers only.
