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
| Slinger | 444 | thin, upright, green pointed hood framing the face (dark tip), sack mask over the lower face, magenta scarf, green tunic + leather belt, stone bag, light leather trousers, the sling hanging from the right hand with a glowing orange-red stone | the hood is one chamfered loft with the face opening cut out (the tip falls back); the mask is painted on the jaw (no extra tris); the bag moved to the front hip (the arm hid it); no pouch (the stone hangs from the strap) | a faint light line at the top of the hood opening in the front view |
| Brute (elite) | 711 | huge mass (~1.95 m: built at the shared joints with brute proportions, scaled x1.4), faceted belly, spiked iron pauldrons, small shaved head with ink topknot, yellow eyes under a heavy dark brow, dark jaw, long arms with huge iron-banded fists, rusty cuffs with broken chains, leather chest strap, magenta loincloth, sack shin wraps, bare dark feet | the variant-A body: wide barrel torso, thick hex6 limbs, head ~30 % of the height (the others ~37 %) | the head sits on the shoulders rather than sunk between them; the belly reads as a slab from the front |
