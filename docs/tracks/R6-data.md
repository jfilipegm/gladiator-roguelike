# R6-B — Skins: data + loadout (worker notes, 2026-10-22)

Contract: `docs/tracks/R7-contract.md` "Wave 3 — R6 Skins" / "R6-B data + loadout". Spec: `docs/tracks/R7-design.md` §4–5.
Branch `claude/xenodochial-benz-352dbc`, cut from `main` @ ce9e7cc (R8 merged, save v3).

## Files
| File | What |
|---|---|
| `Assets/Scripts/Gameplay/Stats/SkinDef.cs` (new) | The ScriptableObject (menu `Gladiator/Skin`, `SK_`): the contract's fields, plus `static IdBelongsTo(skinId, gladiatorId)` (the id rule). |
| `Assets/Scripts/Gameplay/Meta/SaveData.cs` | `List<string> equippedSkins`; `Migrate` fills it; `CurrentVersion = 4`. |
| `Assets/Scripts/Gameplay/Meta/MetaConfig.cs` | `[Header("Skins (R6)")] SkinDef[] skins`. |
| `Assets/Scripts/Gameplay/Meta/MetaProgress.cs` | `IsOwned`, `EquippedSkin`, `Equip`, `Unequip`, `SkinsOf` (each with a pure `SaveData` overload). |
| `Assets/Scripts/Gameplay/Run/RunDef.cs` | `GameSession.Skin` (get/set; reset by `Clear`, untouched by `StartRun` / `StartDaily` / `Retry` / `RetrySameSeed`). |
| `Assets/Scripts/Gameplay/Gladiators/GladiatorLoadout.cs` | `NextSkinOverride`, `CurrentSkin`, the model swap; every `Renderer` (was `MeshRenderer`) gets `characterMaterial` in every slot. |
| `Assets/Editor/Setup/PortraitBaker.cs` | Bakes each `MetaConfig.skins` entry; `SkinPortraitPath(SkinDef)`; new menu `Gladiator/Setup/UI/Bake Skin Portraits` (skins only, opens no scene). |
| `Assets/Editor/Setup/SkinSetup.cs` (new) | `Gladiator/Setup/Gladiators/Create Skin Data`: `skins.json` → SkinDefs, unlocks, `MetaConfig.skins`. |
| `Assets/Tests/EditMode/SkinTests.cs` (new) | 15 tests (below). |
| `Assets/Tests/PlayMode/SkinLoadoutTests.cs` (new) | 6 tests (below). |

No `.meta` files (the orchestrator's Unity makes them). No assets.

## How it works
- **Ownership:** `IsOwned(s, d)` = `s.unlock == null || d.IsUnlocked(s.unlock.id)`. Market skins are bought with the existing
  `MetaProgress.Buy(skin.unlock)`; champion skins are granted by the existing `GrantEarned` (run end) / `CheckUnlocks` (menu).
- **The equipped skin:** `SaveData.equippedSkins` holds skin ids, at most one per gladiator. `EquippedSkin(g, meta, d)` walks
  `meta.skins` in order and returns the first that is `g`'s (`s.gladiator == g`), owned, and listed; else null (the default).
  An unknown id (a skin removed from the table) or a no-longer-owned one is just ignored there; nothing prunes the list.
- **The id rule:** a skin id is `<gladiator id>_<name>` (contract). `Equip(s)` removes the gladiator's other ids **by that
  prefix** (`SkinDef.IdBelongsTo`), because `Equip(SkinDef)` / `Unequip(GladiatorDef)` get no `MetaConfig` to look the
  gladiator's skins up in. So `Equip` refuses (false) a skin whose id breaks the rule (the saving overload logs a warning);
  `SkinSetup` refuses such rows, and also an id another gladiator's id could prefix (none today).
- **Equip / Unequip:** `Equip(s)` = owned + rule → replace the gladiator's id, save only when the list changed. `Unequip(g)` =
  remove the gladiator's ids (stale unknown ones too), save only if one was removed. `SkinsOf(g, meta, into)` clears `into`,
  fills `g`'s skins in `meta.skins` order (owned or not), returns the count.
- **Loadout:** `GladiatorLoadout.Awake` takes `NextSkinOverride ?? GameSession.Skin` (and clears the override). The skin is
  worn when `skin.gladiator == def && skin.model != null` (`CurrentSkin`), else ignored with a `[Loadout]` log line; the
  model is the skin's, else `def.model` (the Nobody's `def.model` is null: his skin still swaps in, his default keeps the
  prefab's CH_Nobody). Same swap as before (detach + destroy the old model, instantiate, layer, shadows), and every slot of
  every `Renderer` gets `characterMaterial`: one material, as today. Awake only: no per-frame cost, no GC in play.
- **Portraits:** `Bake Portraits` now also renders each `MetaConfig.skins` entry through the same `Rig.Render(model, mat,
  true)` as the gladiators (head and shoulders, `PortraitHidden` parts left out) to `PT_<gladiator id>_<suffix>.png`, sets
  `SkinDef.portrait`. The suffix comes off the model's name (`CH_Nobody_Freedman` → `freedman`), else the asset's
  (`SK_Nobody_Freedman`), else the id after the gladiator's. No silhouette for skins (a skin tile only shows once its
  gladiator is earned). `Bake Skin Portraits` does only that (no scene opened, so unsaved scenes don't block it).
- **SkinSetup:** reads `BlenderSrc/characters/skins.json` with `JsonUtility` (wrapper `SkinTable { SkinRow[] skins }`; the
  JSON key `base` is the C# field `@base`) and `palette.json`'s swatches with a regex (its swatches are mixed arrays
  `["name", "#hex", emission]`, which JsonUtility can't read). Per row: checks (id/base/suffix present, unique id, gladiator
  by id from `GladiatorSetup.All()`, the id rule) → model `Assets/Art/Models/Characters/Skins/<base><suffix>.fbx` (missing:
  error + skip) → `Assets/Data/Skins/SK_<Base without CH_><Suffix>.asset` (id, name, description, gladiator, model, three
  chips from the palette; unknown swatch = grey + error; a hazard / enemy_accent / glow chip = warning) → its unlock
  `Assets/Data/Meta/UL_Skin_<Base>_<Suffix>.asset`, id `skin_<skin id>`, title `Skin: <name>`:
  - `price > 0` → Market, `renownCost = price`, hint "For sale in the Market.", description "A new look for the Nobody:
    Freedman. <description> Wear it from the select screen."
  - `champion` → Challenge, `WinWithGladiator <gladiator> 3`, hint "Win 3 runs as the Nobody.", description "3 wins as the
    Nobody earn a new look: Unbowed. <description> Wear it from the select screen."
  - neither → free (`unlock = null`; warns if an old `UL_Skin_*` for it is still on disk).
  No bounty, no `addsToPool` (the reward is the skin that points at the unlock: R6-C's `CodexPanel.Reward`). A portrait
  already baked is linked. Then `cfg.skins` in the table's order, `SaveAssets`, `MetaSetup.CreateOrUpdate()` (collects the
  unlocks). Creates or updates: twice = no change. Never deletes anything.

## Decisions (one-line reasons)
1. **Equip/Unequip find a gladiator's ids by the `<gladiator id>_` prefix** — the contract's `Equip(SkinDef)` and
   `Unequip(GladiatorDef)` take no `MetaConfig`, and the contract already fixes that id format.
2. **`Equip` refuses an id off the rule** instead of accepting it — accepting would let a gladiator end up with two equipped
   ids that later equips can't replace (a silent wrong skin); a refusal is visible.
3. **Skin matching is by reference (`skin.gladiator == def`)**, as the contract says — no id fallback, so test-made
   GladiatorDefs with the default id "nobody" can't be mistaken for the Nobody.
4. **The loadout sets every `Renderer`, every material slot** (was: `MeshRenderer`, slot 0) — "every renderer gets
   characterMaterial" literally; the FBX import has one slot per part, so nothing changes for today's models.
5. **Unknown equipped ids are ignored, not pruned** — `EquippedSkin` skips them, and the next `Equip` / `Unequip` of that
   gladiator removes them (prefix); a prune pass needs the menu's `MetaConfig` and buys nothing visible.
6. **`SkinsOf` clears `into` first** — the strip refills one list per gladiator; appending would mix gladiators.
7. **The portrait suffix is read off the model's file name** — `SkinDef` doesn't carry the table's suffix and the contract
   fixes its fields; the FBX is `<base><suffix>.fbx` by contract.
8. **A separate `Bake Skin Portraits` menu** — the full bake opens every arena scene and refuses with unsaved scenes; after
   a skin re-export only the skins need baking.
9. **Saving overloads save only on a change** — the strip calls `Equip` on every cycle step; re-equipping the equipped skin
   shouldn't write the file.

## Tests (written, not run: no Unity here)
**EditMode `SkinTests`** (15): `IsOwned_Free_Always`; `IsOwned_Market_OnlyAfterBuy` (TooExpensive changes nothing; Bought
spends 250; conditions never grant it); `IsOwned_Champion_AfterThreeWinsWithHim` (two wins + a death + another gladiator's
win = not yet, Progress 2/3; the third win grants it); `Equip_RefusesAnUnownedSkin`; `Equip_KeepsOnePerGladiator`;
`Equip_RefusesAnIdOffTheRule`; `Unequip_ReturnsTheDefault` (also removes a stale id); `EquippedSkin_IgnoresUnknownAndLockedIds`;
`SkinsOf_ListsHisInMetaOrder`; `IdBelongsTo_TheGladiatorPrefix`; `EquipAndUnequip_SaveToTheFile` (temp save, reload);
`Save_V3File_MigratesToV4` (a v3 JSON with R7/R8 fields keeps them, gets the list, writes v4; `Migrate` idempotent);
`Save_V1File_RunsEveryStep`; `GameSessionSkin_KeptByRetry_ResetByClear` (StartRun, Retry, RetrySameSeed, a daily and its
retry keep it; Clear resets; `SuppressLoad`); `SkinData_MatchesTheTable` (**after** Create Skin Data and Bake Portraits:
every `skins.json` row has its SkinDef in table order at the contract's path, its model at the contract's FBX path with the
**same part names and index count as the gladiator's own model** (palette-only remap, spec §4.2 rule 1), three chips equal
to the named palette swatches and none hazard / enemy_accent / glow, its portrait at `PT_<gladiator>_<suffix>.png`, its
unlock at `UL_Skin_*` in `MetaConfig.unlocks` (Market at its price, or `WinWithGladiator <gladiator> 3`), two skins per
`GL_*` gladiator, Market total 3,200; **ignored** while `skins.json` doesn't exist).

**PlayMode `SkinLoadoutTests`** (6, the test arena; the skin's model is a copy of `CH_Nobody.fbx` under an inactive
DontDestroyOnLoad holder, renamed `CH_Nobody_TestSkin`, with a marker child and a stray `Sprites/Default` material):
`Skin_ThePlayerSpawnsInItsModel`; `Skin_TheAnimatorFindsAndDrivesEveryPart` (the six parts by name; `PlayerPartAnimator._rig`'s
six part fields, by reflection, are inside the skin's model; 0.8 s of running swings LegL > 5°);
`Skin_EveryRendererWearsTheOneSharedMaterial` (every renderer, every slot = `characterMaterial`, one distinct material, the
stray one gone); `Skin_OfAnotherGladiator_IsIgnored` (a knight skin on the Nobody, the Nobody's on the knight, a skin with no
model); `NoSkin_TheGladiatorsOwnModel` (knight: CH_BloodKnight; Nobody: CH_Nobody); `GameSessionSkin_DressesThePlayer` (and
the test override beats it).

## For the orchestrator
1. **Merge order** (contract): art → B → C.
2. **Apply the DailyTests patch** (see "Needs from others") or `DailyTests.Save_V2File_MigratesToV3` /
   `Save_V1File_RunsEveryStep` fail on version 4.
3. In Unity: let it import the palette and the twenty FBX (check `ModelImportRules`: scale 1, no materials) →
   `Gladiator/Setup/Gladiators/Create Skin Data` (console: `[SkinSetup] 20 skins (10 Market, 10 champion, 0 free), 0
   skipped; … unlocks`; any error names the row) → `Gladiator/Setup/UI/Bake Portraits` (or `Bake Skin Portraits`: `[PortraitBaker]
   20 skin portraits`). No scene rebuild (MetaConfig is already referenced by the menu and every arena).
4. Tests: `SkinTests` (its `SkinData_MatchesTheTable` checks steps 3's output), full EditMode; `SkinLoadoutTests`, then R6-C's
   `SkinMenuTests`, full PlayMode.
5. Bot runs with `-skin <id>` per gladiator (R6-C reads it): look for `[Loadout] <gladiator> in skin <id>` and no
   `[Loadout] skin … isn't …` line.

## Needs from others
- **Orchestrator — `Assets/Tests/EditMode/DailyTests.cs`** (R8-B's file, not mine): five asserts hard-code save version 3.
  Ready patch (applies cleanly on this branch): `git apply
  /home/jfilipegm/.config/Claude/side-session-notes/local_b89aff3c-4e3a-44a8-960d-dc6389ed4b89/R6-B-dailytests-v4.patch`
  (3 → `SaveData.CurrentVersion`, and the first assert becomes `GreaterOrEqual(…, 3)` as EdictMetaTests already does).
- **R6-C — the id rule:** a test-made `SkinDef` must use an id `<gladiator id>_<name>` (e.g. `nobody_test`) or `Equip`
  refuses it. API summary for R6-C: `R6-B-api.md` in the shared notes folder.
- **R6-art — `skins.json`:** read with `JsonUtility`; ids must follow the rule; `base` + `suffix` must name the exported FBX;
  `chips` are three palette swatch names; `price` > 0 or `"champion": true`. Their table (branch
  `claude/nostalgic-haibt-c57e14` @ 3103519) already meets all of it (see below); its extra top-level `"keep"` is ignored
  by JsonUtility. Nothing needed.

## Verified / unverified
- **Verified:** `Tools/ccheck` errors=0, 0 warnings (all six assemblies, SkinDef.cs included: Gameplay compiles 138 files =
  137 tracked + 1). The palette regex reads all 109 swatches of today's `palette.json` (checked with the same pattern in
  Python). The DailyTests patch passes `git apply --check`. R6-art's `skins.json` + `palette.json` (3103519), checked in
  Python against SkinSetup's rules and `SkinData_MatchesTheTable`'s expectations: 20 rows, unique ids, every id
  `<gladiator>_…` for exactly its gladiator, every `<base><suffix>.fbx` in their tree, 60 chips all palette swatches (none
  hazard / enemy_accent / glow), 10 Market + 10 champion, 2 per gladiator, Market total 3,200; paths
  `SK_Nobody_Freedman` … `SK_Asterion_TheWhiteBull`, portraits `PT_nobody_freedman.png` … `PT_asterion_thewhitebull.png`.
- **Unverified (needs Unity):** every test above; `JsonUtility` mapping the JSON key `base` to the field `@base` (expected:
  the field's name is `base`); Create Skin Data and the portrait bake on the real assets (they're on R6-art's branch).
