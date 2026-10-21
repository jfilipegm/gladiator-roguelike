# R1 char-art — notes (Praetorian, Centurion, chariot, the Emperor's Champion)

Branch `claude/r1-char-art-05b28b`. Every model is built by its script through `Tools/bheadless` (no GUI Blender, no
Unity), exported through `K.export_fbx`, saved as `.blend` and reviewed (front, side, 3/4 and the 64 px silhouette). The
FBX files were re-imported in a headless Blender to check them: hierarchy, part names, no duplicate names, tri counts
match. **Unity hasn't imported them yet** (no `.meta` files: the orchestrator's Editor makes those).

## What was built

| Asset | Script → FBX | Tris (budget) | Iterations | Size |
|---|---|---|---|---|
| `EN_Praetorian` | `BlenderSrc/enemies/en_praetorian.py` → `Enemies/EN_Praetorian.fbx` | **288** (≤ 300) | 2 | 1.92 m to the plume, 2.04 m to the pilum tip |
| `EN_Centurion` | `BlenderSrc/enemies/en_centurion.py` → `Enemies/EN_Centurion.fbx` | **284** (≤ 300) | 2 | 1.85 m to the crest; crest 0.88 m wide |
| `PR_Chariot` | `BlenderSrc/props/pr_chariot.py` → `Props/PR_Chariot.fbx` | **692** (~700) | 2 | 4.44 m long, 2.34 m wide, 2.6 m to the plumes |
| `EN_EmperorsChampion` | `BlenderSrc/enemies/en_emperorschampion.py` → `Enemies/EN_EmperorsChampion.fbx` | **1,304** (≤ 2,500) | 2 | 3.29 m to the helm, 4.63 m to the wingtips, spear tip 4.61 m |

`.blend` files: `BlenderSrc/blend/<script name>.blend`. Reviews (gitignored, in this worktree):
`Screenshots/blender/<script name>_review.png`, and the lineup `Screenshots/blender/r1_lineup.png`.

### Looks
- **Praetorian:** travertine armour and greaves, purple skirt, a travertine helm with cheek guards and an upright purple
  plume, a curved purple scutum with a gold rim, spine and boss (taller and curved round him, unlike the Murmillo's flat
  blue door), a pilum held upright in the right hand. Magenta belt (the accent).
- **Centurion:** silver cuirass with a gold medal harness, purple skirt, gilded greaves, a crimson cloak down his back,
  a steel helm under a broad red crest running side to side (the only transverse crest in the game), a vine staff
  with a gold cap raised in his right hand, left fist on his hip. No shield. Magenta belt.
- **Chariot:** four charcoal horses abreast with crimson blankets, black manes and tails, orange-red (`hazard_red`)
  plumes; a crimson car with gold rails and side medallions and a high curved front; wheels crimson with a gold tyre and
  a gold spoke cross (so the spin shows); a charioteer in purple with a gold helm, reins and a raised whip.
- **The Emperor's Champion:** stocky chibi giant (head + helm ≈ 33 % of his height to the helm). Gold muscle cuirass,
  layered gold-crimson-gold pauldrons, gold vambraces, knee guards and greaves, a fringe of crimson and gold pteruges, a
  gladius at his right hip, a long purple cloak with a gold hem, a stern face under a gold helm (crimson band, visor,
  cheek guards), and on the helm a gold eagle with its wings raised in a V. Spear upright in the right fist (leaf
  blade, gold collar, crimson tassel). Curved purple scutum with gold rim, boss and wings, held at his **left side**
  (not square in front like the Provincial Champion's). A crimson quiver of five javelins on his back, fanning up past
  his left shoulder. Magenta belt.

### Silhouettes (lineup, `r1_lineup.png`: the Nobody, Murmillo, Praetorian, Centurion, Provincial Champion, Gilded Patron, Emperor's Champion, chariot)
- Praetorian vs Murmillo: a rounded tall slab with the pilum line and plume above it vs a flat door with a fin crest
  and a forward gladius. Distinct at 64 px, but the closest pair of the set (both are shield men by design).
- Centurion: the wide "mushroom" crest and the diagonal staff: unlike anything else.
- Chariot: long, low, legged: nothing else is that shape.
- The Emperor's Champion: the eagle's V, the spear and the javelin fan. The biggest humanoid in the game (the Patron's
  canopy is taller, 4.87 m, but it's a litter). If he should feel even bigger in play, `EnemyDef.scale` 1.1 works on
  this model as is.

## For the code (wave 2)

All positions below are **Unity prefab space** (x right, y up, z forward; the character's left is −X), origin on the
ground, at bind pose. Blender → Unity: front −Y → +Z, left +X → −X, Z up → Y up (`K.export_fbx`, verified M0.4).

### Praetorian (`EN_Praetorian`)
- Parts: `Body` (hip, y 0.48), `Head` (neck, y 1.06), `ArmL` (shoulder, (−0.38, 1.0, 0)), `ArmR` (shoulder,
  (0.38, 1.0, 0)), `LegL`/`LegR` (hips, (∓0.15, 0.48, 0)). Shared names, so `PartRig` drives them.
- The shield arm is built like the Murmillo's (ArmL bind swung 40° forward, the shield counter-rotated upright), so the
  ShieldBearer pose that keeps ArmL still works as it does for the Murmillo. `Shield` is a child of ArmL, centre
  (−0.06, 0.66, 0.36), front face at z ≈ 0.52, 1.12 m tall × 0.74 m wide.
- The pilum is upright in the right fist (part of ArmR, not a separate part); its tip at (0.38, 2.04, 0.03).
- **Suggested EnemyDef:** `radius` 0.5 (the Murmillo's), `blockArc` like the Murmillo's (the scutum covers the front).

### Centurion (`EN_Centurion`)
- Parts: `Body` (y 0.5), `Head` (y 1.08), `ArmL`/`ArmR` (shoulders (∓0.39, 1.02, 0)), `LegL`/`LegR` (y 0.5).
- `ArmL` bind: elbow out (fist on hip). `ArmR` bind: swung ~29° forward and out, the vine staff (part of ArmR) raised;
  its gold cap at (0.57, 1.44, 0.48). An attack that swings ArmR forward reads as "drive them on".
- **Suggested EnemyDef:** `radius` 0.42.

### Chariot (`PR_Chariot`)
- Origin on the ground at the centre of the footprint. Footprint: x ±1.17, z −2.22 (car's back) … +2.22 (noses).
  Height 2.6 m (plumes). Fits the 4 m lane with 0.83 m to spare each side.
- Parts (all direct children of the root unless noted):
  - `Car`: pivot on the ground under the axle, (0, 0, −1.68). Holds the car, pole, yoke and axle.
  - `Driver`: child of `Car`, pivot at his feet (0, 0.86, −1.42).
  - `WheelL` (−0.8, 0.54, −1.68), `WheelR` (0.8, 0.54, −1.68): pivot at the axle centre, radius **0.54 m**. Spin about
    the wheel's local X axis: degrees = distance travelled / (2π · 0.54) × 360. The wheels' bind rotation is the same
    as every rigid part's, so by `PartPose`'s convention (`Lean(+deg)` about local X tips a part's top forward) a
    **positive** angle about local X should roll it forward. **Sign unverified in Unity;** the frame-proof way is
    `wheel.Rotate(chariotTransform.right, +deg, Space.World)`. At 22 m/s that's ~39°/frame at 60 FPS; the spoke cross repeats every 90°, so below
    ~45 FPS it can look like it spins backward (cap the visual spin if it does).
  - `Horse0` … `Horse3`: Horse0 is the far left (x −0.92), then −0.3, 0.3, Horse3 far right (0.92). Pivot at the
    barrel's centre, (x, 1.15, 0.72): bob them on y, rock them about local X.
  - `Horse<i>LegsF` / `Horse<i>LegsB`: children of each horse, pivot at the barrel's underside, (x, 0.85, 1.24) front
    and (x, 0.85, 0.2) hind. Optional: swing them about local X in opposite phase for a gallop. Legs hang 0.85 m.
- The plumes are `hazard_red` (non-emissive): the chariot says "it hurts" through them and the track lane's telegraph.

### The Emperor's Champion (`EN_EmperorsChampion`)
- Shared parts: `Body` (hip, (0, 1.15, 0)), `Head` (neck, (0, 2.23, 0.05)), `ArmL` (left shoulder, (−0.8, 2.1, 0)),
  `ArmR` (right shoulder, (0.8, 2.1, 0)), `LegL`/`LegR` (hips, (∓0.36, 1.15, −0.02)).
  Bind poses: ArmR swung ~17° forward (spear upright in front of his right hip); ArmL ~14° forward and a little out
  (shield at his side).
- Extra parts, each separately drivable (nothing in code uses these names yet; none collide with the rig names):
  - `Spear`: child of ArmR, pivot at the grip in the fist, (0.83, 1.17, 0.31). At bind its world rotation is identity,
    so its local axes are character space: the **blade tip is local +Y 3.44 m** from the pivot (world (0.83, 4.61, 0.31)),
    the butt spike −1.18 m (touches the ground). For the thrust: either swing ArmR forward and add a forward tilt to
    the Spear (its bind is upright like the Head's, so `PartPose.Lean`'s convention applies: +X degrees on top of its
    bind tips the blade forward; about +90° levels it when ArmR is at bind), or leave the Spear rigid and accept that a
    forward arm swing tips the blade backward. Unverified in Unity.
  - `Shield`: child of ArmL, centre (−0.95, 1.16, 0.60), front face at z ≈ 0.86; 1.7 m tall × 1.2 m wide, curved.
    The shield bash = ArmL punched forward, as BossController does for the Provincial Champion.
  - `Javelins`: child of Body, pivot at the quiver's centre on his back (0, 1.65, −0.64). The five tips fan out above
    his left shoulder, from (−0.38, 3.43, −0.44) to (−1.30, 2.93, −0.44): a natural spawn point for the javelin fan
    (or the right fist, if he throws with the spear hand). The quiver never empties (it's a model, not ammo).
  - `Cloak`: child of Body, pivot at the shoulders (0, 2.15, −0.47); hangs to the ground. Can sway about local X.
  - `Eagle`: child of the helm (under Head), pivot at its feet on the helm top (0, 3.29, 0.01). `WingL` / `WingR`:
    its children, pivots at the wing roots (∓0.1, 3.51, 0.01); wingtips at (∓1.23, 4.63, 0). Flap them about local Z
    for the phase changes ("FOR THE EMPEROR!") if wanted.
- Chest centre (for hit sparks or a telegraph anchor): (0, 1.75, 0.47).
- **Suggested BossDef/EnemyDef:** `radius` 1.25 (torso and pauldrons reach ±1.1; the shield to −1.57 on his left).

## Decisions
- **The Praetorian holds a pilum upright, not a gladius forward:** the pilum line is what separates him from the
  Murmillo at 64 px, and a line of upright pila reads as a formation.
- **The Emperor's Champion's spear, shield, javelins, cloak and eagle are separate parts** (like Gorm's `Flail`), so
  the boss code can pose the spear for the thrust independently of the arm and flap the wings at phase changes.
- **His scutum is at his side, not in front:** the Provincial Champion's shield covers his whole front; this one leaves
  the gold body, the spear and the eagle visible, which is what makes him read "imperial" rather than "big shield".
- **A visible stern face, not a gold mask:** the Golden Idol already wears a golden mask.
- **Dark horses:** they contrast with the pale sand floor and leave the orange-red plumes and the gold-crimson car as
  the colours that read.
- The curved-scutum mesh function is copied in `en_praetorian.py` and `en_emperorschampion.py` (I don't own
  `gladiator_kit.py`). Candidate for the kit (`K.curved_shield`) if a third script needs it.

## Verified / unverified
- **Verified (Blender only):** tri counts (above), every review sheet looked at (front, side, 3/4, 64 px), the lineup,
  and the exported FBX re-imported in Blender: one root each, the parent chains above, no duplicate names.
- **Unverified:** the Unity import (scale, facing, the axis conversion on the new parts) — it uses the same export path
  as every existing model, so it should match; the wheel spin sign; how the Spear tilt looks in the thrust; anything
  about feel in play.

## Tools used (not committed)
The lineup was rendered by a throwaway script that `exec`s each model's `build()` (no export) and lays them out
side by side: a copy is in the shared notes folder for this session group (`r1_lineup.py`). Run it with
`Tools/bheadless <path>`; it writes `Screenshots/blender/r1_lineup.png`.

## Needs from others
None.
