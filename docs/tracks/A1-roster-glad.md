# A1 roster — the gladiators in the simple style (worker R-glad)

Scripts: `BlenderSrc/characters/a1/a1_<tag>.py` on `simple_kit` plus the shared helpers in
`BlenderSrc/characters/a1/a1_glad_lib.py` (tube, cape, panel, blade, helm). Sheets: `Screenshots/a1/a1_<tag>.png`
(new design left in the front view, the original beside it; front / back / three-quarter). Run:
`Tools/bheadless BlenderSrc/characters/a1/a1_<tag>.py`. In a fresh worktree run `git lfs checkout BlenderSrc/blend/*.blend`
first (the originals are LFS pointers until checked out).

| Character | Tris | Kept | Changed | Doubts |
|---|---|---|---|---|
| Blood Knight | 980 | blackened plate, crimson tabard and belt, horned great-helm with the cyan T visor, spiked pauldrons with crimson rims, tattered crimson cape with the cyan sword sigil on the back, crimson-fullered greatsword held low and forward | one spike per pauldron (was two), the sigil is a full sword outline, gauntlets and greaves in the lighter iron | the greatsword is only a sliver in the front view (it points forward, as in the original) |
| Murder Machine | 894 | huge (hood tip ~2.2 m), rusty iron barrel with an iron chest plate and bright rivets, black hood-mantle with its point down the back, the tall pointed black hood with two cyan eye holes, bare tanned arms with iron pauldrons and vambraces, leather apron with a rust stain, iron greaves and boots, the cyan axe sigil on the back, the slab cleaver held low and forward | built at chibi scale with a brute's proportions then scaled x1.45 (`G.grow`); one-piece barrel torso instead of box + plates | the pauldrons read as flat roof plates from the back; the arms stand off the barrel (as in the original) |
| Pit Goblin | 1,174 | tiny (~1.05 m, the satchel to ~1.4), big green head (~40 %) pushed forward, huge swept ears with tan insides, yellow eyes, long nose, toothy grin, pot belly, sackcloth rag on a rope belt, long thin arms with sack wraps, bowed legs and big flat feet, the towering satchel (leather flap, stick, sword hilt and bomb poking out), cyan headband with knot and the cyan rag round the satchel, a bomb in the right hand | the satchel is one leaning loft (it clears the head instead of sitting on a hump) | close to the 1,200 guide (the three-ring bombs and the junk cost ~150) |
