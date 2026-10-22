# R6-C menu: the skin strip, the launch, the roster tiles, the Codex (worker notes)

Contract: `docs/tracks/R7-contract.md` → "Wave 3 — R6 Skins", part **R6-C menu**. Spec: `docs/tracks/R7-design.md` §4.
Branch `claude/hopeful-bell-2180cf`, cut from `main` @ ce9e7cc (R8 merged).

**Status:** `Tools/ccheck` errors=0, 0 warnings (all six assemblies), with the R6-STUB(b) stand-ins below.
Nothing was run in Unity (worker rule): the tests are written, not run; no screenshots.

## Files
| File | What |
|---|---|
| `Assets/Scripts/UI/SkinStrip.cs` (new) | The strip under the diorama: `SkinStrip(font, fontMat)`, `Build(root, anchor, pos, onPreview)`, `Show(g, meta, save)`, `Count`, `Index`, `Current`, `CurrentOwned`, `Cycle(dir)`, `Buy()`, plus `OnChanged(bool bought)` and test getters (`NameText`, `StatusText`, `BuyShown`, `BuyEnabled`, `PrevRect` / `NextRect` / `BuyRect`, `ChipCount`, `Chip(i)`, `Shown`, `Gladiator`). |
| `Assets/Scripts/UI/MainMenu.cs` | The strip on the select screen, Q / E · LB / RB · F / North, `ShowHero(g, skin)`, the launches set `GameSession.Skin`, `-skin <id>`, the key line, the tile portraits, the prune of unknown ids. Test API: `Skins`, `CycleSkin(int)`, `BuySkin()`, `HeroPrefab`, `HeroShown`, `SelectKeysText`. |
| `Assets/Scripts/UI/RosterGrid.cs` | `SetPortrait(int slot, Sprite s)` (earned tiles only; null puts back his own picture), tests: `PortraitOf(i)`, `FaceImage(i)`. |
| `Assets/Scripts/UI/CodexPanel.cs` | `Reward(u)` names a skin ("Reward: Skin — Freedman (The Nobody)"; several: "Skins — A (…), B (…)"); Market and Challenge cards of a skin's unlock show its baked portrait (`Entry.Picture`). |
| `Assets/Tests/PlayMode/SkinMenuTests.cs` (new) | 9 PlayMode tests (below). |
| R6-STUB(b) stand-ins | `Assets/Scripts/Gameplay/Stats/SkinDef.cs`, `MetaConfig.cs`, `SaveData.cs`, `MetaProgress.cs`, `RunDef.cs` — **one commit (e014c40), delete at merge** (below). |

No `.meta` files: Unity makes them for `SkinStrip.cs` and `SkinMenuTests.cs`.

## How it works
- **The strip** (680 × 104 canvas units, a dark plate, bottom-centre at 28 units up: between BACK and FIGHT!, under the
  diorama's feet). Row 1: `◀  ■■■ FREEDMAN · 2 / 3  ▶` (chevron buttons; the skin's three chips in a bone rim and the
  name as one centred group; the counter dimmed). Row 2: **EQUIPPED** (gold) for an owned skin; a **BUY · 250 RENOWN**
  button with an "F / Y" tag for an affordable Market skin, or a greyed **250 RENOWN · NEED 130 MORE**; for a champion
  skin its goal and progress, **Win 3 runs as the Nobody · 1 / 3** (the progress in gold); any other locked unlock: its
  hint (+ progress when it has one condition).
- **Slots:** 0 = the gladiator's own colours, shown as **CLASSIC**; then `MetaProgress.SkinsOf(g)` in MetaConfig order.
  `Show` opens on the equipped skin. `Cycle` wraps round the ends, previews on the diorama, and equips at once when
  owned (`Equip`, or `Unequip` for CLASSIC; nothing is written when the equipped skin doesn't change). `Buy` buys the
  Market skin on show (`MetaProgress.Buy` on its unlock) and equips it; CLASSIC and champion skins are `NotForSale`, a
  free one `AlreadyOwned`, short of Renown `TooExpensive` with nothing changed. Hidden when the gladiator has no skins
  (before SkinSetup has run the menu looks exactly as today).
- **The menu.** The strip follows the gladiator the diorama shows (`Selected`: a locked tile under the cursor leaves the
  last earned one standing, and his strip). `ShowHero(g, skin)` instantiates `skin.model` when the skin is his, else
  `g.model`, cached by prefab (each skin is built once) and every renderer gets `heroMaterial` (the one palette material).
  Opening the select screen re-reads the save (a skin bought in the Codex Market, Renown spent on a Market edict).
  After an equip the picked gladiator's tile switches portrait; after a purchase the corner's Renown, the Codex and its
  NEW count follow.
- **Input** (select screen only): Q / LB previous, E / RB next, F / gamepad North buy; the mouse uses the strip's arrows
  and button. The key line adds "Q / E: skin" once skins exist as data.
- **Launch.** `Launch` sets `GameSession.Skin` right before `GameSession.StartRun`: the gladiator's **equipped** skin,
  never the strip's preview of an unowned one. `FightDaily` does the same for the plan's gladiator before
  `StartDaily` (the day fixes the gladiator, not his colours); `-daily` too.
- **`-skin <id>`** (bot / perf runs): any skin, owned or not. Without `-gladiator` it also picks its wearer
  (`-bot -skin pit_goblin_sewer_king` runs the Goblin); with a `-gladiator` it doesn't fit, a warning and his equipped skin.
  An unknown id: a warning, then as without it.
- **Roster tiles:** each earned tile shows its gladiator's equipped skin's baked portrait (fallback: his own). The
  Codex's GLADIATORS tab stays as it is (skin portraits there are out of scope, spec §7).
- **Prune** (spec §5): `Awake` drops equipped ids that no `MetaConfig.skins` entry knows and saves; skipped while
  `meta.skins` is empty, so a build or checkout without skin data never wipes the choices.

## Decisions (one line each)
1. **The classic slot reads "CLASSIC".** A skin label, not the gladiator's name, so it's never mistaken for a skin.
2. **The counter sits inside the arrows ("◀ FREEDMAN · 2 / 3 ▶"), not after them.** The arrows frame the whole item, and
   one centred group reads at a glance.
3. **Chevron sprites, not ◀ ▶ glyphs.** LiberationSans has no U+25C0 / U+25B6 (a missing glyph warns and fails tests).
4. **Centred on the screen in the bottom band, not under the 3D hero's x.** The hero's screen x moves with the aspect
   ratio; the strip stays clear of BACK / FIGHT! at any aspect (≥ 978 free units at 5:4).
5. **The diorama keeps an unowned preview until you cycle away or leave; FIGHT! takes the equipped skin.** Spec §4.1;
   re-opening the screen shows the equipped one again.
6. **"NEED 130 MORE" on an unaffordable skin.** The select screen shows no Renown balance otherwise.
7. **"F / Y" beside the BUY button, not on the key line.** It only matters when something is for sale.
8. **`-skin` alone picks its wearer.** The orchestrator's "a bot run per gladiator with `-skin`" needs only one flag.
9. **The Codex's skin cards show the baked portrait** (dark for a champion skin until earned). A cosmetic sold without
   a picture is a blind buy; it costs one optional `Sprite` on the Codex entry.
10. **Buying in the Codex Market owns a skin but doesn't equip it.** Equipping is the strip's job (cycle to it).

## Tests (`Assets/Tests/PlayMode/SkinMenuTests.cs`, not run)
Menu scene, the sandbox save (`SaveSandbox`), `GameSession.SuppressLoad`. They find the Nobody's skins from the data
(`SkinsOf`: the Market one, and the challenge `WinWithGladiator nobody N`), not by id.
1. `TheStrip_SitsUnderTheDiorama_AndCyclingChangesTheModel` — CLASSIC · 1 / n, EQUIPPED, on screen, clear of BACK and
   FIGHT!, centred, bottom band; the arrows (clicked through the EventSystem) change `HeroPrefab`; one shared material;
   the chips; a preview equips nothing; wrap-round; no TMP warnings.
2. `AMarketSkin_ShowsItsPrice_AndBuyOwnsAndEquipsIt` — "BUY · 250 RENOWN", a click buys, Renown spent, equipped, saved,
   `AlreadyOwned` after, the tile follows.
3. `AMarketSkin_WithoutTheRenown_IsTooExpensive_AndNothingChanges`.
4. `AChampionSkin_CantBeBoughtOrEquipped_BeforeItsWins` — "Win 3 runs as the Nobody · 1 / 3", `NotForSale`, not
   equipped; with the wins the menu grants it and cycling equips it.
5. `TheEquippedSkin_SurvivesAMenuReload` (`MetaProgress.Reload` + reload) — the diorama and the strip open on it; CLASSIC
   unequips and saves.
6. `Fight_SetsGameSessionSkin_ToTheEquippedOne_NotThePreview` — and null for CLASSIC.
7. `TheDaily_FightsInTheEquippedSkin`.
8. `TheRosterTile_ShowsTheEquippedSkinsPortrait` — needs the baked portraits.
9. `TheCodex_NamesTheSkinAnUnlockGives` — "Reward: Skin — Freedman (The Nobody)", listed in the Market tab.

Before running them: `Gladiator/Setup/Gladiators/Create Skin Data` and `Gladiator/Setup/UI/Bake Portraits` (the asserts
say so if missing). Not covered by a test: the keyboard / gamepad keys themselves (they call `CycleSkin` / `BuySkin`)
and `-skin` (command line).

## Stubs: R6-STUB(b) (delete at merge)
Commit **e014c40** holds every stand-in, nothing else, all marked `R6-STUB(b)`: `SkinDef` (the contract's file),
`MetaConfig.skins`, `SaveData.equippedSkins`, `MetaProgress.IsOwned` ×2 / `EquippedSkin` ×2 / `Equip` / `Unequip` /
`SkinsOf`, `GameSession.Skin`. My real files call the contract's names and need no change. After R6-B is merged into
`phase5-skins`:
```
git merge --no-ff --no-commit origin/claude/hopeful-bell-2180cf
git checkout HEAD -- Assets/Scripts/Gameplay/Stats/SkinDef.cs Assets/Scripts/Gameplay/Meta/MetaConfig.cs Assets/Scripts/Gameplay/Meta/SaveData.cs Assets/Scripts/Gameplay/Meta/MetaProgress.cs Assets/Scripts/Gameplay/Run/RunDef.cs
grep -rn "R6-STUB(b)" Assets/   # expect nothing
Tools/ccheck && git commit
```
(`git checkout HEAD -- …` puts back R6-B's version of each file and resolves its conflict; if R6-B's `SkinDef` lives
elsewhere, `git rm` mine instead.)

## Needs from R6-B (what my code assumes; all in the contract)
- `MetaProgress.SkinsOf(g, meta, into)` lists **every** skin of `g` (owned or not) in MetaConfig order (the strip clears
  its own list first and ignores the return value).
- `MetaProgress.Equip(s)` and `Unequip(g)` save; `EquippedSkin` ignores unknown and unowned ids.
- `GameSession.StartRun` / `StartDaily` leave `Skin` alone (the menu sets it just before them); `Clear()` resets it.
- `SkinSetup`: the Nobody has one Market skin (price > 0) and one challenge `WinWithGladiator nobody 3` (the tests find
  them that way); a fresh `SaveData` has a non-null `equippedSkins` (the tests guard with `??=` anyway).

## Screenshots for the orchestrator
After Create Skin Data and Bake Portraits, the select screen at **1920×1080 and 1280×720**:
1. The Nobody on CLASSIC (the strip: "CLASSIC · 1 / 3", EQUIPPED).
2. His Market skin with enough Renown ("BUY · 250 RENOWN", F / Y), and without ("250 RENOWN · NEED … MORE").
3. His champion skin ("Win 3 runs as the Nobody · 1 / 3").
4. A skin equipped: the tile's portrait and the diorama in it.
5. The Codex MARKET tab with a skin card (its portrait).
Check: the strip doesn't hide the hero's feet (the plate's top is 132 units up; the feet stood near 140 in
`Screenshots/r3_select_asterion.png`), the chips read on the dark plate, and the name / status fit at 720p. Driving it:
`Tools/ueval 'var m = UnityEngine.Object.FindAnyObjectByType<Gladiator.UI.MainMenu>(); m.SendMessage("OpenSelect"); m.CycleSkin(1); return m.Skins.NameText;'`
(SendMessage reaches the private `OpenSelect`; Renown for the BUY shots: `MetaProgress.Data.renown = 300` before
opening the screen, in a throwaway save).
