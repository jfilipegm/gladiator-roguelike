# R2 char-art — notes (the Eternal Victor, the Skeletal Legionnaire, the Chimera, the bone pile)

Branch `claude/wizardly-lamarr-39e37a` (fast-forwarded from `phase5-colosseum`). Every model is built by its script
through `Tools/bheadless` (no GUI Blender, no MCP, no Unity), exported through `K.export_fbx`, saved as `.blend` and
reviewed (front, side, 3/4 and the 64 px silhouette). The FBX files were re-imported in a clean headless Blender to
check them: one root each, the part names and parents below, no duplicate names, tri counts match. **Unity hasn't
imported them yet** (no `.meta` files: the orchestrator's Editor makes those).

## What was built

| Asset | Script → FBX | Tris (budget) | Iterations | Size |
|---|---|---|---|---|
| `EN_EternalVictor` | `BlenderSrc/enemies/en_eternalvictor.py` → `Enemies/EN_EternalVictor.fbx` | **1,418** (≤ 2,500) | 3 | **3.60 m** to the crest top (helm top 2.99); falx blade to 1.9 m on his left at bind |
| `EN_SkeletalLegionnaire` | `BlenderSrc/enemies/en_skeletallegionnaire.py` → `Enemies/EN_SkeletalLegionnaire.fbx` | **288** (≤ 300) | 2 | **1.73 m** to the crest stump; gladius tip 0.74 m ahead of him |
| `EN_Chimera` | `BlenderSrc/enemies/en_chimera.py` → `Enemies/EN_Chimera.fbx` | **720** (≤ 800) | 3 | 1.56 m at the chest, 1.68 m at the mane's hump, **2.40 m** to the snake's head; 2.93 m nose to tail (2.3 m nose to rump), 1.36 m wide at the mane |
| `PR_BonePile` | `BlenderSrc/props/pr_bonepile.py` → `Props/PR_BonePile.fbx` | **72** (≤ 80) | 2 | 0.84 × 0.91 m, **0.36 m** high; one mesh |

`.blend` files: `BlenderSrc/blend/<script name>.blend`. Reviews (gitignored, in this worktree):
`Screenshots/blender/<script name>_review.png`; lineups `Screenshots/blender/r2_lineup.png` (Nobody, Convict,
Legionnaire, Praetorian, Murmillo, Pit Cur, War Boar, Chimera, bone pile, Gorm, Beast Lord, Emperor's Champion,
Victor), `r2_bosses.png` (every boss), `r2_beasts.png` (Pit Cur, Wolf, War Boar, Chimera, Brute), each with fitted 64 px
silhouettes (3/4 and front) of every model.

### Looks
- **The Eternal Victor:** tall and gaunt. Bare bone thighs and arms, knee knobs, tall flared bronze greaves; a bronze
  belt, a magenta sash with a hanging end, a short crimson kilt with bronze straps; a **wasp waist where only the bone
  spine and two lower ribs show** (the gaunt and the undead read, and his dead's motif); a bronze muscled cuirass. A
  round thraex helm: an octagonal bowl, a broad brim, darkness behind a three-bar grille, a bone jaw under the visor, a
  gold laurel (a band and two rows of leaves meeting at the front, two ribbon ties at the back) and the **griffin crest
  curling forward into a hooked beak far in front of the visor** (with a crimson fringe down its back). One great
  shoulder guard on his **left** (a galerus: a flared plate standing up beside the helm over three layered plates) and a
  three-ring bronze manica on his right forearm. A cloak of eight tattered crimson ribbons of different lengths down his
  back. The **falx**: a bronze haft wrapped in leather, bronze pommel and collar, and a crescent blade (steel, its inner
  cutting edge `steel_l`) held low across his body, right fist at the pommel, left fist lower on the haft; the crescent
  rises beside his left leg to shoulder height. Read: the hook on top, the crescent at his side, the thinnest body.
- **The Skeletal Legionnaire:** a big skull (a wide cranium over a narrower face, black sockets and a nose hole, the jaw
  hanging open) under a dented rusty dome helmet knocked askew, its crest a broken bronze stump; rusted plates on the
  chest with a bronze band, rusted shoulder guards; **below them nothing but a bare spine** down to the magenta sash and a
  rotted crimson rag; stick bone arms and legs; a notched gladius held forward in the right fist. Read: the only stick
  figure in the game (gaps at the waist and between the limbs) with a big round head.
- **The Chimera:** a dark lion (deep chest, haunch, thick legs, charcoal paws), a charcoal mane collar with ten spikes
  round the face, the jaws open on a `fire_mid` throat, bone fangs, yellow eyes; from the middle of its back a goat's neck
  rises (to the beast's **left**) with a pale goat head, an ash beard and two curling bone horns; its tail is a slate and
  iron snake arching up over its rump (to its **right**) with its own head, jaw and yellow eyes facing forward. A magenta
  harness strap round the barrel. Read: the spiky ruff with a horned head and a snake arc rising behind it, three heads
  from the front, the side and 3/4.
- **The bone pile:** crossed long bones (3-sided prisms), a pelvis, the skull tipped over on top with two dark sockets
  and its rusted helmet slipped back.

### Silhouettes (`r2_lineup.png`, `r2_bosses.png`, `r2_beasts.png`)
- **The Victor vs every boss:** every other boss is a wide mass (Gorm's block, the Beast Lord's spiky mane and goad, the
  Champion's shield, the Patron's litter, the Emperor's Champion's eagle V, javelin fan and scutum). The Victor is the
  only thin one, with a hook above and a crescent at his side: unmistakable at 64 px in 3/4 and from the front.
- **The Legionnaire vs the humanoids:** the only figure with see-through gaps (waist, between the stick limbs); the
  Assassin is thin too but solid and dark, with a pointed hood.
- **The Chimera vs the beasts:** the Pit Cur, Wolf and War Boar are low single-headed shapes; the Chimera is twice as
  tall with two heads standing up behind the spiky ruff. Closest motif: the Beast Lord's spiky lion hood (both lions),
  but he is a biped in rust and ochre and the Chimera a charcoal quadruped.
- **The bone pile** is a flat cross of bones with a lump on top; on `sand_d` it reads bone-white.

## For the code (wave 2)

All positions are **Unity prefab space** (x right, y up, z forward; the character's left is −X), origin on the ground,
at bind pose. Blender → Unity: front −Y → +Z, left +X → −X, Z up → Y up (`K.export_fbx` + `ModelImportRules`).

### The Eternal Victor (`EN_EternalVictor`) — boss part
- **Every named part has an identity bind rotation** except `Falx` (the octagonal helm was turned in its mesh data, not
  by its object), so `PartPose` offsets act in plain character space.
- Shared parts: `Body` (hip, (0, 1.38, 0)), `Head` (neck, (0, 2.27, 0.02)), `ArmL` (left shoulder, (−0.45, 2.14, 0)),
  `ArmR` (right shoulder, (0.45, 2.14, 0)), `LegL`/`LegR` (hips, (∓0.19, 1.38, 0)).
- **Both shoulder pivots lie on one X line** (y 2.14, z 0): give `ArmL` and `ArmR` the **same Swing** (about X) and the
  left fist stays on the haft (both arms turn rigidly about the same axis). Two-handed poses: Chase = bind; the lunges =
  both arms Swing about +40…+70 together; Grasp wind-up = both arms swung up high, strike = both back down with Body
  Lean about +35 (values to tune, signs unverified); Reaper's Circle = Body yaw. Twisting one arm alone (about Y/Z) lets the left fist leave the haft: fine for a
  quick wind-up, visible if held.
- Extra parts:
  - **`Falx`**: child of ArmR, **pivot at the grip in the right fist, (0.30, 1.46, 0.44)**. The left fist is on the haft at
    (−0.26, 1.20, 0.50). **Built flat in its own frame:** in the Falx's local space the haft runs along **local +Z**
    (pommel at z −0.2, the blade's root at z 0.98) and the crescent lies in the **local XZ plane**, curving toward local
    −X, 0.12 m thick (local y ±0.06). So for the throw, **`falx.rotation = Quaternion.Euler(0, yaw, 0)` (world) lays it flat**
    with the haft pointing along `yaw`; spin = add to `yaw`. Local bounds x [−1.33, 0.25], z [−0.20, 1.83]; **centre
    (−0.54, 0, 0.81)**. Spinning about the pivot (the grip) sweeps a ~1.9 m radius; to spin it about its middle instead,
    set `falx.position = flightPoint − falx.rotation * new Vector3(-0.54f, 0f, 0.81f)`. On the catch, restore the stored
    bind `localPosition`/`localRotation` (its bind rotation is not identity: the held pose). At bind the blade spans
    world x −1.90…−0.48 (his left), y 0.48…2.11, z −0.10…0.75.
  - `Crest`: child of Head, pivot at its base on the helm top, (0, 2.95, −0.04); the beak hangs in front of the visor to
    z ≈ 0.65. Can nod about local X at the roars.
  - `Laurel`: child of Head, pivot at the wreath's centre, (0, 2.73, 0.02).
  - `Cloak`: child of Body, pivot at the shoulders (the ribbons' yoke), (0, 2.18, −0.23); the ribbons end 0.13–0.8 m
    above the ground behind him. Can sway about local X.
- The darkness behind the visor (`ink`) is at (0, 2.48, 0.32): a natural anchor if eyes should ever light up (not in the
  contract, not built). Chest centre (0, 1.94, 0.21).
- **`Hand`** (`BossController`, radius 1.0 → `pos + up × 1.6 + aim × 0.6`): the grip sits at 1.46 m at bind, so the
  throw starting at 1.6 m is consistent.
- **The rise and the remains:** the legs are straight rigid bones, so the kneel = both legs Swing forward + `RootY` down
  (as the other bosses' crouches); the remains' heap = Body Lean +70, Head Lean +40, arms out (Swing −30, a Z roll). All
  parts allow these; signs unverified in Unity (as R1's).
- **Suggested EnemyDef:** `radius` **1.0** (as the contract): the cuirass reaches ±0.37, the elbows ±0.70, the galerus
  −0.79, the ribbons ±0.6; the falx is not body. Height 3.60 m (contract "~3.5"; iteration 3 enlarged the crest by 12 %). He is
  the tallest body but the thinnest, so in the lineup he looks smaller than the bulky bosses by design; if he reads
  small in play, `EnemyDef.scale` 1.1 works on this model as is.

### The Skeletal Legionnaire (`EN_SkeletalLegionnaire`) — enemies and layout parts
- Built like the Convict, **with the Convict's kind of bind rotations** (the humanoid poses were tuned on those): `Body`
  is the magenta sash at the hip, (0, 0.58, −0.02), hunched 9° forward about X; `Head` (neck, (0, 1.19, 0.09)), pushed a
  further 7° forward; `ArmL`/`ArmR` (shoulders, (∓0.25, 1.07, 0.06)), swung forward 20° / 32° about X and 7° out about Z;
  `LegL`/`LegR` (hips, (∓0.125, 0.58, 0)), identity.
- The gladius is part of ArmR (not a separate part): hilt at (0.28, 0.62, 0.26), tip at z 0.74.
- **Suggested EnemyDef:** `radius` **0.36** (as the contract; the arms reach ±0.34), `pileMesh` = the `PR_BonePile` mesh.

### The Chimera (`EN_Chimera`) — enemies and layout parts
- **Every named part has an identity bind rotation.** Quadruped like the War Boar: `Body` (belly, (0, 0.68, −0.02)),
  `ArmL`/`ArmR` (front legs under Body, (∓0.25, 0.82, 0.56)), `LegL`/`LegR` (hind legs under the root,
  (∓0.235, 0.82, −0.52)), **`Head`** = the lion's head under Body, **pivot at the neck (0, 1.32, 0.80)** (so the Breather's
  `Head` lean −25 / +20 nods the whole head, mane and jaws included).
- Extra parts (no code drives them yet; they ride on Body): **`GoatHead`** under Body, pivot at the goat's neck root on
  the back, (−0.16, 1.34, 0.14), its skull at (−0.27, 2.04, 0.52); **`Tail`** under Body, the snake, pivot at its base on
  the rump, (0.04, 1.12, −0.74); **`TailHead`** under Tail, pivot at the snake's neck, (0.25, 2.12, −1.06). Optional
  juice: sway `Tail` about local Z, bob `GoatHead` about local X, and snap `TailHead` forward with the breath.
- **The breath's origin:** the open jaws, throat centre (0, 1.22, 1.30), the nose at z 1.52: spawn the fire at about
  (0, 1.2, 1.55).
- **Suggested EnemyDef:** `radius` **0.95** (as the contract; the nose 1.52 ahead of the centre, the rump 0.77 behind, ±0.68 wide at
  the mane).

### The bone pile (`PR_BonePile`) — enemies part
- **One mesh**, no children, 72 tris, the shared palette UVs. Origin at the base centre; nothing below y 0; extent x
  −0.45…0.39, z −0.47…0.44, height 0.36. The skull faces +Z (it doesn't matter: piles get a yaw). Load it like the other
  instanced props (`FirstMesh`, e.g. `PR_HotCoin`): the import rule bakes the export's 180° yaw into its vertices.
- Its footprint (r ≈ 0.45) fits well inside `stompRadius` 0.9 and the bone ring (r 0.9).

## Decisions
- **The Victor's parts all have identity bind rotations** (the Emperor's Champion's arms don't): poses in plain character
  space, and the two-handed falx works with equal swings because both shoulders sit on one X line.
- **The falx is modelled flat in its own frame and turned into the held pose by its bind rotation:** the throw is then a
  pure yaw (no tilt to undo), and the code gets its centre for a spin about the middle. The pivot stays at the grip as
  the contract says.
- **The blade's plane is rolled 30° back about the haft** (iteration 3): held square to the front, the crescent sat in
  front of his legs from 3/4 and vanished at 64 px; rolled, it rises beside him, clear of the body, in every view.
- **The great shoulder guard is on his left, turned to face front-left** (iteration 1's fin stood edge-on: a stick from
  the front), with the manica on the right forearm: an asymmetric champion, not a mirrored statue.
- **The bare spine at the Victor's waist** echoes the Legionnaires' (his dead) and is what makes a 3.6 m figure read gaunt.
- **No glowing eyes in the visor:** the contract asks for darkness; the anchor is noted above if wanted later.
- **The Chimera's goat head is on its left and the snake on its right,** the goat moved forward and the snake back
  (iteration 3), so the three heads separate in every view, at 64 px too.
- **Chimera eyes `hazard_yellow` (non-emissive)** like the Convict and the Pit Cur; the throat `fire_mid` (emissive) as the
  contract asks: the only orange on it says where the fire comes from.
- **The bone pile's bones are 3-sided prisms and the sockets two flat quads:** that is what fits a recognisable skull,
  helmet and bones in 80 tris. Its helmet is smaller than the skull (iteration 1's covered it like a roof).
- **Each model's helper functions are local to its script** (`place`, `seg`, `wbox`): I don't own `gladiator_kit.py`.
  `seg` (a box between two world points) and `place` (set a world transform under a parent) now exist in five scripts;
  candidates for the kit.

## Verified / unverified
- **Verified (Blender only):** tri counts (above); every review sheet looked at (front, side, 3/4, 64 px) and the three
  lineups with fitted 64 px silhouettes; the exported FBX files re-imported in a clean Blender: one root each, the
  parent chains above (`Falx` under ArmR, `Crest`/`Laurel` under Head, `Cloak` under Body, `GoatHead`/`Tail` under Body,
  `TailHead` under Tail), no duplicate names; the named parts' bind rotations (identity except `Falx` and the
  Legionnaire's Convict-style Body/Head/Arms).
- **Unverified:** the Unity import (same export path and import rule as every existing model, so it should match); the
  falx's flat frame and centre in Unity (derived from the axis mapping, not observed); the pose signs; how the falx
  throw, the kneel and the remains look; the bone pile through `RenderMeshInstanced`; anything about feel or
  readability in play (at 450 enemies, under dusk and last light).

## Tools used (not committed)
The lineups were rendered by a throwaway script that `exec`s each model's `build()` (no export), lays them out side by
side on a `sand_d` background and renders fitted 64 px silhouettes of each, wrapping them into rows: `r2_lineup.py` in
the shared notes folder for this session group (R1's `r1_lineup.py`, generalised: `models=` and `out=` arguments). Run it
with `Tools/bheadless <path>`.

## Needs from others
None. For **env-art** (FYI, their `PR_VictorStatue` is "a bronze Victor with the falx raised"): the read is the forward
hook of the crest, the crescent falx, the galerus on his left shoulder and the gaunt body; a hand-over note with the key
shapes is in the shared notes folder (`r2-char-art.md`).
