# A1 roster — tier 3 enemies and bosses in the simple style (worker R-tier3boss)

Review sheets only (`Screenshots/a1/a1_<tag>.png`: the new design left from the front, the original right; front /
back / three-quarter). Nothing in the game changes. Scripts: `BlenderSrc/characters/a1/a1_<tag>.py`; shared helpers
(scutum, helmets, rods and points, capes, bake/scale/hunch) in `BlenderSrc/characters/a1/a1_tier3boss_lib.py`.
Bosses are built at their real size (the kit body scaled ×1.6–2.3 with wider/thicker proportions) and reviewed with
`review(height=, gap=)`.

**Triangles, a general note:** the kit's base body (`simple_kit.humanoid`) is ~620–660 tris on its own (feet 44 each,
fists 44 each, head 92, torso 80), so a humanoid in this style can't reach the 450 guide for regular enemies without
changing the kit itself (a lean variant with 2-ring feet and fists and a 4-ring head would save ~150). I only cut
hidden geometry (`helmet_head`: the head's top under a helmet, -32). The numbers below are honest.

| Character | Tris | Kept | Changed | Doubts |
|---|---|---|---|---|
| praetorian | 894 | travertine segmented armour (a darker chest band, shoulder plates), purple skirt, magenta belt, travertine helm with cheek and neck guards, the tall upright purple plume, the curved purple scutum (gold rim, spine, diamond boss) held square in front from shin to eyes, the pilum upright with iron shank and point above the plume | chibi kit body; the shield arm reaches forward to the grip (no floating shield); the shield sits left of centre so the pilum side and the belt show | 2× the 450 guide (see the note); the plume reads thin from the front (as in the original) |
| gilded_mercenary | 940 | steel cuirass with a gold trim, crimson skirt and cloak, the fat gold purse on the magenta belt, gilded helm with cheek and neck guards and the tall crimson crest front to back, gold pauldrons and greaves, leather gloves and boots, the halberd taller than him (axe broadside to the front, beak, spike) | chibi kit body, broader (TW 0.225); the axe head is a flat outline with darker edges instead of a tapered box | over the guide (kit body); the cloak's two flat panels read two-tone from the back |
