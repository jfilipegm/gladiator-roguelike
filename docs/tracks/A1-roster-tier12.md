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
| Fire-Pot | 442 | hunched under the load, soot-black rags, white rag over nose + mouth, ash cap, yellow eyes, magenta sash, slate trousers, a wooden board on the back with clay pots, a burning clay pot held out in the right hand (glowing top, orange flame) | the variant-A body (lean); mask and cap painted on the head (no extra tris); two pots on the back instead of three; the hand pot darker clay so the hazard-orange flame pops | the wrapped head reads like a bandaged mummy (it is "face wrapped in bandages"); the hand pot is small under its flame |
| Assassin | 408 | lean and all charcoal, pointed hood with an ink tip, dark slate face with a white eye slit, leather sash, two steel daggers low in reverse grip, the long magenta scarf streaming down the back (collar + two-piece tail) | the hooded head is one chamfered loft (head and hood in one) with a painted face panel; the tail hangs lower (it read as a pole when flying out) | the hood is a big black egg: reads "hooded" but heavy on top |
| Murmillo | 454 | broad and bare-chested, brimmed steel helm with a dark grille (two bars) and the tall red fish crest, white manica on the sword arm, bronze greave on the left shin, white loincloth under the magenta belt, leather boots, the tall blue scutum (bronze rims, spine, boss) square in front from knee to chin, a gladius pointing forward | the helm is one chamfered loft standing in for the head; the scutum a two-facet bent plate (reads curved) with painted rims and spine; the manica is the arm painted white | 4 tris over the 450 guide; the magenta belt hides behind the scutum from the front (as in the original) |
| Net-Thrower | 455 | lean retiarius, bare head with dark hair (the only bare head), the tall steel galerus on the left shoulder, a rope net bundle over the right shoulder, a weighted net balled in the right fist, a long trident upright in the left hand (three steel prongs), blue loincloth under the magenta belt, sandals | the galerus is turned to face front-outward (edge-on it read as a detached sliver); the nets are faceted rope lumps with a darker weave; no leg wrap (triangles) | 5 tris over the guide; the balled net reads like a faceted stone |
| Shield Archer | 447 | blue tunic, iron cap with a dark brim, the tall straw wicker pavise with wooden bands standing from the ground to his chin with the magenta boss, the bow carried flat in the right hand along the walk direction with a glowing orange-red arrowhead | the pavise gets a pointed top (tells it from the Shield Thug's square door at a glance); the bow is one bent plate + string + arrow; hair painted at the back of the head | the iron cap reads a little like a bucket hat, close to the Thug's pot helmet (the Thug keeps the spike, nose guard and beard) |
| Beast Handler | 455 | burly and bald, heavy dark brow, stubble, bare-chested under a leather apron, spiked iron collar, magenta belt + armband, a coiled whip in the right fist with the lash rising behind the shoulder, two iron chain leashes hanging from the left fist, trousers and boots | the stubble is painted on the jaw; the coil is a small hex disc with a dark hole decal; the apron darker leather (the light one melted into the skin); the chains are plain iron bars | 5 tris over the guide; the lash reads as a stiff stick above the shoulder, not a slung whip |
| War Boar (elite) | 388 | low, wide, heavy boar with a shoulder hump, near-black hide, a bristling mud-brown mane, a big wedge head held low with a pale snout disc + nostrils, long bone tusks curving up, small red eyes, the magenta war harness round the barrel, ink hooves, short tail | chibi: a big chamfered head; oct8 sections lofted along Y with a slate belly, scaled x1.15 to the original's bulk; the mane a saw-tooth crest; the harness a frustum that frames the barrel from front and back, two iron studs on it (new) | far under the elite guide (it doesn't need more); the studs are new |
| Centurion (elite) | 539 | silver cuirass with a 3 x 2 grid of gold medals, purple skirt, magenta belt, long crimson cloak down the back, steel helm with silver cheek guards under the broad red crest running side to side, the vine staff (gold cap) raised up and out in the right hand, the left fist out at the hip in a dark glove, gilded greaves, leather boots, no shield | the variant-A body (lean) x1.1 for an officer's presence; the cloak a curved back plate; the crest fans out from a narrow foot on the helm | the crest is one flat red fan: it reads (and is the silhouette) but looks a little like a red umbrella |

Helpers worth knowing for the other workers: `simple_kit.cone` is a frustum with a tiny top (twice the triangles of a
pyramid); `a1_tier12_lib.spike()` / `octa()` are the cheap true pyramid / octahedron. `simple_kit.box` pivots at the
centre by default (pass `pivot=(0, 0, -1)` to grow up from `loc`). A ring (open band) seen end-on is invisible: a
collar/harness that must read from the front needs a front ring smaller than the next (a frustum).

## In-game models (export=1)
`Tools/bheadless BlenderSrc/characters/a1/a1_<tag>.py export=1` writes `Assets/Art/Models/A1/Enemies/<root>.fbx` with the
original's root and its six parts (Body, Head, ArmL, ArmR under Body; LegL, LegR under the root; the beasts' front
legs are ArmL/ArmR), one mesh per part with its origin at the joint, and a pose sheet
`Screenshots/a1/ingame/a1_<root>_poses.png` (rest, run, swing, hit). Running without `export=1` still makes the review
sheet as before (same tris). The lean body's joints are `a1_export.joints(p)`, moved with the same transforms as the
pieces (`a1_tier12_lib.move_joints`: the Convict's and the Fire-Pot's hunch; the Brute's and Centurion's x1.4/x1.1),
the beasts' joints written per script. Size: `fit_height` measures the exact height (vertices, not bound boxes) and
scales pieces + joints to the original's height when they're more than 8 % off. The chibi heads made most of them
taller than the originals, so 10 of 13 are scaled down (x0.76-0.90). Every pose sheet checked: every piece follows
its part (shields, weapons, nets, the whip and chains with their arm; loincloths, cloak, harness, pauldrons, back
frame with the body; helmets, hoods, crests with the head; hooves, paws, greaves, wraps with their leg). Each model is
6 renderers, and its tris are the same as its review sheet's. The exporter (`a1_export.py`) needed no change.

| Model | Tris | Renderers | Height (original) | Scale | Doubts |
|---|---|---|---|---|---|
| EN_Convict | 437 | 6 | 1.295 (1.295) | x0.836 | the hunch is baked into the rest pose (as in the original), so the run cycle swings hunched arms |
| EN_PitCur | 392 | 6 | 0.762 (0.762) | x0.903 | the shared pose sheet tips the whole dog nose-down for "hit" (a humanoid pose); fine for a check |
| EN_ShieldThug | 444 | 6 | 1.500 (1.500) | x0.896 | the door-shield swings with ArmL in the run (as the original's; the game damps the shield arm) |
| EN_Slinger | 444 | 6 | 1.437 (1.437) | x0.839 | none |
| EN_Brute | 711 | 6 | 1.786 (1.786) | x0.890 (of the x1.4 build) | pauldrons on Body (as the original), so a raised arm passes under them |
| EN_FirePot | 442 | 6 | 1.163 (1.163) | x0.761 | the most shrunk: he reads small next to the Convict (as the original did) |
| EN_Assassin | 408 | 6 | 1.430 (1.430) | x0.850 | none |
| EN_Murmillo | 454 | 6 | 1.820 (1.720) | x1 (+6 %, inside the rule) | none |
| EN_NetThrower | 455 | 6 | 1.955 (2.001) | x0.85, forced | its height is the trident: the body is matched to the original's and the trident lengthened in the game model only (top 2.02 instead of 1.62 before the scale), so the review sheet keeps the old trident |
| EN_ShieldArcher | 447 | 6 | 1.230 (1.230) | x0.770 | the pavise swings with ArmL in the run (as the original's) |
| EN_BeastHandler | 455 | 6 | 1.300 (1.300) | x0.839 | none |
| EN_WarBoar | 388 | 6 | 1.108 (1.020) | x0.86, forced (+9 %) | the tall mane makes the height: a plain fit left the barrel 17 % shorter than the original's; x0.86 splits it (height +9 %, length 1.62 vs 1.79, -9 %) |
| EN_Centurion | 539 | 6 | 1.850 (1.850) | x0.885 (of the x1.1 build) | none |
