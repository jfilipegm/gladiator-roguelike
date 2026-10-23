# A1 — Character art direction (prototype: the Nobody)

The producer's brief (2026-10-23): evolve the characters into a polished, distinctive stylized 3D look, without
rebuilding the game. Keep the identity (low-poly, chunky, readable with 300 enemies), lose the "assembled from
rectangular bricks" look. Reference: a minimalist hooded figure (Journey-like), for its principles, not its design.
Branch `phase5-artdir`. **One prototype first; nothing else changes until the producer approves it.**

## 1. What the project does today (inspected)
| Area | Finding |
|---|---|
| Engine | Unity 6000.6.6f1, URP, linear; one custom shader, `Gladiator/PaletteLit` (wrap-lit main light, tinted shadows, hemisphere ambient, optional rim, emission from the palette's alpha, pixel noise, hit flash). Post: bloom, vignette, colour grade, fog. |
| Materials | Characters share **one** material, `M_PaletteLit_Char` (wrap 0.45, rim 0.28, pixel noise 0.03); the arena uses `M_PaletteLit_Env`. Every colour comes from one 128×128 point-filtered palette (118 of 256 swatches used; the alpha channel is emission). No per-object materials, no textures beyond the palette. |
| Models | Procedural: one Python script per character (`BlenderSrc/characters/*.py`, `BlenderSrc/enemies/*.py`) on the shared `gladiator_kit.py`, run through the Blender bridge or `Tools/bheadless`, exported as FBX to `Assets/Art/Models/` and imported by `ModelImportRules` (normals imported, no embedded materials). Skins are re-exports of the same script with a palette remap (`build_skins.py`, `SkinDef.model`). |
| Shapes | The kit's primitives are `box` (with optional taper/bevel), 4–8-sided `cylinder`/`cone`, `ico`, `wedge` and extruded outlines. **Every character is 20–40 boxes**, flat-shaded (`object_from_bmesh` forces flat normals; the FBX exports per-face smoothing). This is most of the "blocky, primitive" read: the geometry, not the shader. |
| Rig and animation | Rigid parts, no skinning: `Body` (hip), `Head` (neck), `ArmL/ArmR` (shoulders, weapon and shield as their children), `LegL/LegR` (hips). `PartRig` finds the six parts by name; `PlayerPartAnimator` and `EnemyManager` drive them procedurally (bob, run cycle, swings, recoil, squash). Hitboxes are the player's `CharacterController` and the enemies' radius in data — the meshes play no part in gameplay. |
| Rendering cost | Each sub-mesh is its own `MeshRenderer`: the Nobody is **36 renderers / 876 tris**, the Convict **20 renderers / 240 tris**. Enemies are pooled GameObjects (not instanced), so 300 Convicts are ~6,000 renderers. The SRP Batcher keeps that affordable today (148 fps at 300 enemies), but renderer count, not triangles, is the cost that grows with the crowd. |
| Budgets (CLAUDE.md §7.2) | player ≤ 1,200 tris; regular enemy ≤ 300; elite ≤ 800; boss ≤ 2,500. |
| Where the look comes from | **Geometry ~70 %** (boxes, flat normals, uniform limb widths, details stuck on as more boxes); **palette ~20 %** (flat swatches, many hues per character); **lighting/shader ~10 %** (already stylized: wrap light, cool shadows, rim on characters). |

## 2. The direction
The hooded reference works because of five principles, which the redesign translates into gladiators:
1. **One strong silhouette idea per character.** The Nobody: a big rounded head, broad shoulders over a narrow waist, a torn tunic skirt, one armoured side. Every other gladiator and enemy class gets its own single idea (the Murmillo's fish-crest helmet and big shield, the Brute's sunk head under a shoulder yoke, the Convict's hunch and chains).
2. **Shaped forms instead of boxes.** Tapered limbs with a deltoid, an elbow, a forearm bulge and a wrist; a V torso; a rounded head with a jaw and a crown; curved armour plates (shells) and hanging cloth strips; soft shading on round forms, crisp creases where plates and cloth meet.
3. **Appealing, stylized proportions.** Chibi stays (head ~33 %), but with chunky hands and feet, broad shoulders and a tight waist: cartoonish, not childish (heavy angled brows, a jaw, no big round eyes).
4. **Restrained, layered detail.** Detail clusters where the eye goes (face, shoulder, belt, weapon): a belt and buckle, sandal straps, one greave, a two-lame pauldron, a torn hem. No detail just to fill space.
5. **Cohesive colour.** Four families per character (skin, cloth, leather, metal) plus one identity accent (the player's cyan; an enemy's accent patch), instead of a hue per box.

Readability rules kept: the player's cyan reads from behind (headband tails, the focale's tail), enemies keep their accent patches, anything harmful stays orange-red, the 64 px silhouette test still applies.

## 3. The prototype: `CH_Nobody_V2`
- **Source:** `BlenderSrc/characters/proto/ch_nobody_v2.py` on a new helper module `BlenderSrc/stylized_kit.py` (`loft`, `shell`, `strips`, `band`, `soften`, `rounded_box`, `merge_part`). `gladiator_kit.py` and every existing asset are untouched.
- **Output:** `Assets/Art/Models/Prototype/CH_Nobody_V2.fbx` (+ `BlenderSrc/blend/ch_nobody_v2.blend`). Nothing in the game references it yet.
- **The same rig contract:** the same part names, joint pivots (hip 0.40, shoulders ±0.28 at Body 0.46, neck Body 0.55, hips ±0.11) and weapon grip (ArmR −0.385) as `CH_Nobody`, so animation, attack timing, hitboxes and collision are unchanged by construction.
- **Design:** the exomis (a worker's tunic over one shoulder), a torn hem over the hip joints, a balteus belt with a bronze buckle, a scrap two-lame pauldron and one greave on the shield side, caligae straps, a domed plank shield with one mismatched plank, the cyan headband with flying tails and a cyan focale (a legionary's neck scarf) whose tail runs down the back.
- **Cost:** **1,190 tris** (budget 1,200; the original 876, +36 %) in **8 renderers** (the original 36, −78 %): one mesh per rigid part plus the shield and the blade.
- **Iterations (3, the cap):** (1) 3,462 tris, lanky, the head too small, the back read as dark hair; (2) chibi proportions, the focale back, 1,566 tris, but 6-sided limbs shaded faceted; (3) limbs smoothed past the hexagon's 60°, hidden caps and small extras cut (ears, thumbs, knee cap, a second plank), the tufts that read as cat ears replaced by one swept back: 1,190 tris.
- **Screens:** `Screenshots/a1_blender_compare.jpg` (front, back, 3/4, review sheet with the 64 px silhouette) and `Screenshots/a1_ingame_compare.jpg` (in the Slave Pit with the game's material, light and post: a close 3/4, and the gameplay camera from behind). Both are captured with the original beside it.

## 4. Comparison and risks
| | Original | Prototype |
|---|---|---|
| Triangles | 876 | 1,190 (within the player's 1,200) |
| Renderers (draw items) | 36 | 8 |
| Materials / textures | 1 / the palette | 1 / the palette (no change) |
| Rig, pivots, animation, hitbox | — | identical contract |

- **Animation:** rigid parts can't bend, so the new limbs keep a slight built-in bend; the hem hangs from the Body and the legs swing under it (strips flare outward to limit poke-through); the pauldron rides the arm, so raised-arm swings can't clip it. To check in play: the hem against the legs during a sprint and a slide, and the headband tails during the head bob.
- **Performance:** for the player, +314 tris is negligible; the merge to one renderer per part is a net win. For enemies it matters most: merging the Convict's 20 renderers into ~7 cuts 300 Convicts from ~6,000 renderers to ~2,100.
- **Pipeline:** soft normals reach Unity through the FBX (verified in the capture); `merge_part` must leave the parts' names and pivots alone (it does), and skins still work because they re-run the same script with a palette remap.

## 5. Staged plan (after the producer approves the prototype)
1. **Preview in the real game (no code change):** a free `nobody_v2` skin pointing at the prototype FBX, so the select screen's skin strip flips between the original and the prototype, and a run can be played with it. Reversible: delete the skin asset.
2. **Adopt for the Nobody:** `ch_nobody.py` is rewritten on `stylized_kit` (the old script kept as `ch_nobody_v1.py`), the Nobody's skins re-exported.
3. **Shading pass (one material, no extra draw calls):** a small material map beside the palette (same 128×128 layout: specular strength, gloss, rim boost per swatch), read only by `M_PaletteLit_Char`, so metal glints, leather has a soft sheen and cloth stays matte; vertex-colour ambient occlusion in creases. Optional; judged on screenshots before it ships.
4. **Enemies, Tier 1 first** (Convict, Pit Cur, Shield Thug, Slinger, Brute), each with one silhouette idea, merged to one renderer per part, in ~300–400 tris (a proposed small budget raise from 300, as the merge more than pays for it). Then the 64 px lineup test against each other and the floor.
5. **The other gladiators, then elites and bosses**, one script each, the same rules; at most 3 iterations per asset (CLAUDE.md §2.9), leftovers in ART_DEBT.
6. **Measure** after each stage: the 300-enemy stress run (fps, gameplay ms, GC) against v0.26.0.

## 6. Files
- New: `BlenderSrc/stylized_kit.py`, `BlenderSrc/characters/proto/ch_nobody_v2.py`, `Assets/Art/Models/Prototype/CH_Nobody_V2.fbx`, `BlenderSrc/blend/ch_nobody_v2.blend`, this file.
- Stage 1 would add: `Assets/Data/Skins/` one `SkinDef` (via the skins setup script, not by hand) and its portrait.
- Later stages touch: the character and enemy scripts in `BlenderSrc/`, their FBX, optionally `PaletteLit.shader` + `M_PaletteLit_Char.mat` + a new material-map texture, `docs/ART_DEBT.md`, `docs/DECISIONS.md`.
