# R6 art — the twenty skins (worker notes, 2026-10-22)

**Branch:** `claude/nostalgic-haibt-c57e14` (cut from `main` at 5c04395). Blender only through `Tools/bheadless`; no
Unity, no `.meta` files (the orchestrator imports). Spec: `docs/tracks/R7-design.md` §4, contract:
`docs/tracks/R7-contract.md` "Wave 3 — R6 Skins".

## Files
| File | What |
|---|---|
| `BlenderSrc/gladiator_kit.py` | `active_skin()` reads `GLADIATOR_SKIN`; `set_swatch` maps through it; `export_fbx` writes `Characters/Skins/<base><suffix>.fbx` with it set. Nothing else changed. |
| `BlenderSrc/characters/skins.json` | the one table of skins (Blender and `SkinSetup` read it) |
| `BlenderSrc/characters/build_skins.py` | the driver: export, lineup review, two checks |
| `Assets/Art/Textures/Palette/palette.json`, `PAL_Master.png`, `palette_index.md` | nine new swatches in free cells |
| `Assets/Art/Models/Characters/Skins/*.fbx` | the 20 skins (no `.meta`) |
| `Screenshots/blender/skins_<gladiator>.png` | the lineups (gitignored; copies in the shared notes folder as `r6_art_skins_<gladiator>.png`) |

## Rebuild and check
```
Tools/bheadless BlenderSrc/characters/build_skins.py skin=all                 # the 20 FBX, ~2 s
Tools/bheadless BlenderSrc/characters/build_skins.py skin=nobody_freedman       # one
Tools/bheadless BlenderSrc/characters/build_skins.py gladiator=nobody review=1  # + Screenshots/blender/skins_nobody.png
Tools/bheadless BlenderSrc/characters/build_skins.py check=skins                # each skin FBX vs its base FBX
Tools/bheadless BlenderSrc/characters/build_skins.py check=base                 # base scripts, no variable, vs committed FBX
```
Every run first validates the table (`validate()`): ids `<gladiator>_<name>`, one of `price > 0` / `champion`, three
chips from the palette, every remap swatch in the palette, **no target in the hazard / enemy_accent / pickup / debug
families or a red** (`rust`, `rust_light`, `red_banner(_d)`, `crimson(_d)`, `blood`), a glow only replacing a glow, and
a player-colour mark (`player_*`) only becoming another player colour. A broken row stops the run with the error list.

How the driver works: per skin it sets `GLADIATOR_SKIN` = `{"suffix", "remap" (the list made a dict), "keep"}` and runs
`BlenderSrc/characters/<script>` with `runpy`. The scripts `importlib.reload` the kit, so the driver wraps
`importlib.reload` for the run and patches the fresh kit each time: no `save_blend` (skins have no `.blend`, spec §6.27)
and no `review_render` (it would overwrite the base's `ch_*_review.png`). After the run it restores `importlib.reload`,
clears the variable and reloads the kit clean. **The base character scripts are untouched.**

## Kit change, and one addition to the contract: `keep`
- `set_swatch(obj, swatch, faces)` looks the swatch up through `active_skin()["remap"]`, read from the environment on
  every call (the contract's design). With no variable, `_skin_swatch` returns the swatch unchanged: same code path as
  before.
- **Added: `"keep"`**, a list of name prefixes. A part whose name, or any ancestor's name, starts with one keeps its base
  swatches. Reason: spec §4.2 rule 5 ("weapons aren't skinned") and the identification order (silhouette → **weapon**
  → head). A plain swatch remap would have reskinned held weapons that share a swatch with the armour (the Nobody's
  `rusty_iron` gladius and pauldron, Asterion's bronze chain and collar, his stone's `ash` and his hide). It's a
  top-level `"keep"` object in `skins.json` keyed by base model; **JsonUtility ignores it, so `SkinSetup` needn't read
  it.** Kept: `Gladius`, `Bomb`, `Greatsword`, `Cleaver`, `StormSpear`, `CenserStaff`, `Trident`, `HuntingSpear`,
  `Chain` (Asterion's `Chain` tree, the stone `ChainEnd*` and the `ChainWrap0/1` on his forearm, so they still match
  the swung `WP_LabyrinthChain`). The Golden Idol keeps nothing: his sceptre is regalia (his weapon is the coin), so it
  takes the skin's metal.
- `export_fbx(root, rel_path)` with a skin set writes `Assets/Art/Models/Characters/Skins/<basename(rel_path)><suffix>.fbx`.
- The FBX's root node keeps the base name (e.g. `CH_Nobody` inside `CH_Nobody_Freedman.fbx`); every part name is the
  base's, so `PartAnimator` and `PortraitBaker.PortraitHidden` find them unchanged.

## Palette: nine swatches, free cells only
| swatch | row (family) | col | hex |
|---|---|---|---|
| `ivory` | 0 neutral | 7 | #F2E6C4 |
| `silver` | 8 metal | 8 | #E1E6EC |
| `silver_d` | 8 metal | 9 | #A3ABB5 |
| `obsidian` | 8 metal | 10 | #27232F |
| `cloth_black` | 9 cloth_leather | 9 | #2C2B30 |
| `cloth_purple` | 9 cloth_leather | 10 | #64478F |
| `cloth_teal` | 9 cloth_leather | 11 | #2C8582 |
| `jade` | 14 misc | 5 | #4E9E7F |
| `jade_d` | 14 misc | 6 | #2D6A55 |

Appended to the end of each row, so no existing swatch moved. Checked by decoding the old and new `PAL_Master.png`:
exactly these nine 8×8 cells differ, and each was debug magenta before. No emission on any of them.

## The twenty skins
Order in each lineup sheet, left to right: default, Market skin, champion skin; each as front / 3-4 / from behind (the
game camera); top row on a pale floor (`marble`), middle on a dark one (`mud_dark`), bottom the 64 px silhouette (3/4).

| Gladiator | Skin (FBX suffix) | Unlock | Main remaps | Iter. |
|---|---|---|---|---|
| Nobody | **Freedman** `_Freedman` | 250 | tunic `white`, loincloth/wraps `cloth_white` (all linen), bronze shield and pauldron | 2 |
| Nobody | **Unbowed** `_Unbowed` | champion | tunic `charcoal`, leathers `cloth_black`, gold pauldron and shield rim | 1 |
| Pit Goblin | **Sewer King** `_SewerKing` | 250 | `slate` rags and satchel, `cloth_purple` flap and belt | 1 |
| Pit Goblin | **Ratcatcher** `_Ratcatcher` | champion | `ochre_d` rags and satchel, dark leathers, gold teeth | 1 |
| Blood Knight | **Ashen Knight** `_AshenKnight` | 250 | `ash` plate, `bone` trims, `charcoal` cape and horns, `slate` tabard | 1 |
| Blood Knight | **Night Knight** `_NightKnight` | champion | `obsidian` plate, `silver` trims and spikes, `purple_d` tabard, `cloth_black` cape | 1 |
| Murder Machine | **Headsman of Rome** `_HeadsmanOfRome` | 350 | `iron` barrel, `steel` plates, `cloth_purple` mantle, black hood (base) | 2 |
| Murder Machine | **Bronze Colossus** `_BronzeColossus` | champion | `bronze_l` barrel, `bronze` plates, `bronze_d` fists and boots, gold rivets | 1 |
| Thunder Champion | **Storm-Grey** `_StormGrey` | 350 | `silver` armour, `slate` tunic and cape (the white bolt stays) | 1 |
| Thunder Champion | **Olympian** `_Olympian` | champion | `gold` armour, `ivory` tunic and cape, cape bolt `player_white` → `player_cyan` | 1 |
| Golden Idol | **Silver Idol** `_SilverIdol` | 350 | `silver` body, `steel_l` disc, `cloth_blue` sash and bangles | 1 |
| Golden Idol | **Jade Idol** `_JadeIdol` | champion | `jade` body, `jade_d` legs and skirt, gold sash, the disc stays gold | 1 |
| Ashen Oracle | **Bone Oracle** `_BoneOracle` | 350 | `ivory` robes, `white` mask, `sand_d` stoles and hem | 2 |
| Ashen Oracle | **Violet Oracle** `_VioletOracle` | champion | `cloth_purple` robes, `purple_d` stoles and hem, `gold_d` belt and stole ends | 1 |
| Net-Weaver | **Deep Sea** `_DeepSea` | 350 | `cloth_blue` skirt, `cloth_teal` net and manica, `water` wraps | 1 |
| Net-Weaver | **Pearl Diver** `_PearlDiver` | champion | `ivory` skirt and wraps, `silver_d` net, `silver` galerus | 1 |
| Beastmaster | **Winter Hunter** `_WinterHunter` | 350 | `white` furs (mantle, cuffs, boots), paler wolf hood | 1 |
| Beastmaster | **Black Wolf** `_BlackWolf` | champion | `charcoal` furs, `cloth_black` wolf hood, `ink` ears | 1 |
| Asterion | **The White Bull** `_TheWhiteBull` | 350 | `white` hide, `ivory` snout, `stone_grey` mane, gold kilt | 1 |
| Asterion | **Bronze Bull** `_BronzeBull` | champion | `bronze_l` hide, `gold_l` snout and horns, `bronze_d` mane, iron collar | 1 |

Iteration-2 fixes: the Freedman's first pass (white tunic, lighter leathers) was hard to tell from the default, so his
leathers became linen. The Headsman's `purple_d` mantle disappeared, so it became `cloth_purple`. The first Bone Oracle
(`cloth_white` robes) read as the default's grey under light, so it became `ivory` with tan trim. Every skin passed
within 2 iterations, so nothing goes in ART_DEBT. Themes are the spec's §4.4, with small changes: the Ratcatcher has
gold teeth (all three teeth are one swatch), and the White Bull's kilt is gold instead of blue.

Readability notes from the lineups: every player-cyan mark (scarves, headbands, sigils, rune, fur tips, labyrinth) is
untouched and readable on both floors. The darkest skins (Unbowed, Night Knight, Black Wolf) are dark from behind on
`mud_dark`, as the default Beastmaster already is. Their cyan marks and light trims carry them. The palest (Freedman,
Bone Oracle, Winter Hunter, White Bull) keep skin, hair or dark trims against `marble`. Worth a look in game at Gate
time: Black Wolf on the Slave Pit's mud.

## Verified
- **Base scripts unchanged (with no variable):** `check=base` ran all ten base scripts through the new kit (their FBX,
  `.blend` and review sheet sent to a temp folder) and compared each fresh FBX with the committed one: **identical for
  all ten**. Same nodes and parents, per-object tri counts, vertex positions and every face's UV cell. Totals: Nobody
  38 nodes / 876 tris, Pit Goblin 50 / 904, Blood Knight 52 / 980, Murder Machine 40 / 808, Thunder Champion 39 / 788,
  Golden Idol 40 / 844, Ashen Oracle 35 / 572, Net-Weaver 52 / 872, Beastmaster 41 / 720, Asterion 66 / 1,148. The
  Nobody's cells, for example: bronze_d, cloth_white, ink, leather, leather_d, mud_dark, player_cyan, rusty_iron,
  skin_light, skin_mid, steel, wood, wood_dark, wood_light.
- **Skins = base + remap:** `check=skins` imported each of the 20 skin FBX and its base FBX: the same nodes, parents,
  tris and vertices, and every face's UV cell equals the base's cell through the remap, with kept weapon parts
  unchanged. **20/20 ok** (72 to 446 faces remapped per skin). A negative control on the Freedman shows the check bites.
  Comparing with no remap fails (14 objects). A remap missing `wood` fails ("Shield: wood → bronze"). Pretending the
  gladius isn't kept fails ("Blade: bronze_l / rusty_iron").
- **64 px silhouettes:** rendered for every default and skin; **0 px differ** from the default for all 20 (identical by
  construction, counted anyway).
- **One material:** re-importing all 20 skin FBX finds one distinct material, `M_Palette` (the preview material Unity
  ignores; the bases use the same one). No new material.
- Not verified here (orchestrator's steps): the Unity import (`ModelImportRules` matches every path under
  `Assets/Art/Models/`, so `Skins/` gets scale 1 and no materials), `SkinSetup`, portraits, in-game lineups.

## For the code parts (R6-B, R6-C)
- `skins.json` rows follow the contract exactly: `id, gladiator, script, base, name, suffix, description, chips[3],
  price, champion, remap[{from, to}]`. Extra top-level fields `_comment` and `keep` can be ignored.
- Model path: `Assets/Art/Models/Characters/Skins/<base><suffix>.fbx`, e.g.
  `CH_MurderMachine_HeadsmanOfRome.fbx`, `CH_Asterion_TheWhiteBull.fbx`.
- Portrait names by the contract's rule (`PT_<gladiator id>_<suffix lower, no underscore>`): e.g.
  `PT_asterion_thewhitebull`, `PT_thunder_champion_stormgrey`.
- Chips are swatch names from `palette.json` (the new ones included), so `SkinSetup` reads their hex there.
- Market total: 3 × 250 + 7 × 350 = **3,200 Renown**, as the spec says.
