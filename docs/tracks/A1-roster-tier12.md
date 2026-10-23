# A1 roster — tier 1-2 enemies in the simple style (R-tier12)

Screenshots only: one script per enemy in `BlenderSrc/characters/a1/a1_<tag>.py`, the sheet (front / back / 3-4 beside
the original) in `Screenshots/a1/a1_<tag>.png`. Run: `Tools/bheadless BlenderSrc/characters/a1/a1_<tag>.py` (needs the
originals' blends: `git lfs pull --include "BlenderSrc/blend/en_*.blend"` in a fresh worktree). Nothing in the game changes.

**Shared lean body** (`a1_tier12_lib.py`): enemies are drawn hundreds at a time, so `body()` is simple_kit.humanoid's
variant-A body (same joints, oct8/hex6 sections, anchors) with fewer rings (two-ring legs and fists, a five-ring head,
no hidden caps) and a decal face (quad eyes and brows with a ledge, a three-triangle wedge nose): ~370 tris with hair
and a tunic, against the kit's ~640. Also rigid group transforms (hunch, arms swung forward, scale), quadruped lofts
along Y, hood openings. Enemy faction tells, everywhere: the magenta (or lime) accent patch, angry brows, yellow or
red eyes on the feral ones, darker / dirtier cloth than the gladiators, no player cyan.

| Enemy | Tris | Kept | Changed | Doubt |
|---|---|---|---|---|
| Convict | 437 | thin, hunched, grey starving skin, sack rags, magenta chest rag, yellow eyes + grimace, charcoal hair, wrist shackles with a broken link, arms reaching, bare feet, no weapon | loincloth -> a ragged sack tunic with a dirt hem | the tunic reads a little like a dress from the front |
| Pit Cur | 392 | low lean quadruped in mud-brown, dark spine ridge (hackles), dark pointed ears, pale snout + black nose + bone fangs, yellow eyes, magenta collar with iron spikes, tail up, light paws, lunge stance | chibi: a big chamfered head, short stocky legs; oct8 body lofted along Y with a pale belly; the collar is a frustum so it frames the neck from the front | the magenta patch is smaller than the original's chest block (a frame round the neck, seen under the chin) |
| Shield Thug | 444 | stocky, dark leather, iron pot helmet with spike + brim + nose guard, dark beard, iron-shod boots, the light wooden door-shield held square in front (iron rims, plank lines, magenta diamond boss), knobbed club up-forward in the right fist | the pot helmet is a chamfered frustum; the rims are painted bands; no belt (hidden behind the shield) | the club hides behind the arm and shield in the 3/4 view |
