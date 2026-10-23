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
| sorcerer | 810 | the deep purple robe flaring to a gold hem, the gold stole, the magenta sash, wide sleeves with gold cuffs and pale hands, the tall pointed purple hood leaning back over a face in shadow with two orange-red eyes, the staff with a gold claw and a glowing orange-red orb | the hood is one loft whose front is pulled back so the dark head shows through (an open hood with a brow, no separate face plate); legs dropped (the robe reaches the ankles) | over the guide (kit body) |
| skeletal_legionnaire | 728 | the big bone skull (black sockets, nose hole, teeth, the jaw hanging open), the dented rusty helmet knocked askew with its broken crest stump, rusted chest plates with a bronze band and a shoulder plate, the bare spine over a gap at the waist, the magenta sash on the pelvis, the tattered crimson rag, stick-thin bone limbs, the notched gladius held forward, a slight hunch | bony hands and feet are plain blocks (no kit fists or feet); a short rusty neck guard added to the helmet | the tilted bowl helmet reads a bit like a cap; the gladius is edge-on from the front (it shows in 3/4) |
