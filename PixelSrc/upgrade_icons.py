"""Upgrade icons: 16x16 pixel art for every upgrade card, drawn here as text so the set is regenerable and diffable
like the Blender scripts (no third-party art).

Each icon is written with fill colours only; the script adds the dark 8-bit outline, three "shine" frames (a light
band sweeping across), and four frames of a white aura mask around the silhouette that the game tints with the card's
rarity colour. Output: one point-filtered atlas, 20 px cells (the 16 px icon plus 2 px of aura on each side),
one row per icon, columns 0-3 icon frames, 4-7 aura frames, plus a text index (id -> row).

    python3 PixelSrc/upgrade_icons.py [--preview out.png]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
OUT_PNG = ROOT / "Assets/Art/Textures/UI/UpgradeIcons.png"
OUT_TXT = ROOT / "Assets/Art/Textures/UI/UpgradeIcons.txt"

ART, CELL, PAD = 16, 20, 2
ICON_FRAMES, AURA_FRAMES = 4, 4

PALETTE = {
    "k": (27, 22, 34), "w": (251, 251, 244), "l": (201, 206, 216), "s": (143, 152, 170), "d": (84, 91, 110),
    "r": (192, 97, 46), "R": (122, 58, 28), "o": (240, 154, 48), "y": (255, 216, 74), "Y": (184, 138, 42),
    "b": (154, 100, 54), "B": (90, 55, 28), "g": (106, 216, 96), "G": (47, 138, 60), "c": (102, 224, 255),
    "C": (47, 116, 216), "e": (232, 56, 60), "E": (140, 26, 38), "f": (240, 191, 144), "F": (182, 122, 82),
    "n": (226, 172, 98), "h": (255, 244, 168), "p": (168, 104, 224),
}
OUTLINE = PALETTE["k"]


def grid(rows):
    assert len(rows) == ART, f"{len(rows)} rows"
    for r in rows:
        assert len(r) == ART, f"row {r!r} is {len(r)} wide"
    return [list(r) for r in rows]


def blank():
    return [["."] * ART for _ in range(ART)]


def disc(g, cx, cy, r, ch, shade=None):
    """Filled circle; `shade(x, y)` may return another colour per pixel."""
    for y in range(ART):
        for x in range(ART):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                g[y][x] = shade(x, y) if shade else ch


def put(g, pts, ch):
    for x, y in pts:
        if 0 <= x < ART and 0 <= y < ART:
            g[y][x] = ch


# ---------- the icons (fill colours only; the outline is added) ----------
ICONS = {}

ICONS["weapon_gladius"] = grid([  # Rusty Gladius: a short blade, rust-eaten, brass guard
    "................",
    ".............ww.",
    "............lsr.",
    "...........lsR..",
    "..........lrr...",
    ".........lsr....",
    "........lsR.....",
    "....y..lrs......",
    "....Yylsr.......",
    ".....Yyy........",
    ".....bBYy.......",
    "....bB..Y.......",
    "...bB...........",
    "..yY............",
    "..YY............",
    "................",
])

ICONS["weapon_spear"] = grid([  # Pit Spear: long shaft, leaf blade
    "................",
    "..............w.",
    "............lws.",
    "...........lwsd.",
    "...........lsd..",
    "..........YY....",
    ".........bB.....",
    "........bB......",
    ".......bB.......",
    "......bB........",
    ".....bB.........",
    "....bB..........",
    "...bB...........",
    "..bB............",
    ".B..............",
    "................",
])

ICONS["weapon_javelins"] = grid([  # two crossed javelins
    "................",
    "..w..........w..",
    "..lw........wl..",
    "...ls......sl...",
    "....Y......Y....",
    ".....b....b.....",
    "......b..b......",
    ".......bb.......",
    ".......BB.......",
    "......b..b......",
    ".....b....b.....",
    "....b......b....",
    "...B........B...",
    "..B..........B..",
    "................",
    "................",
])

ICONS["weapon_crimson_greatsword"] = grid([  # a long blood-red blade, iron guard
    "................",
    ".............ww.",
    "............weE.",
    "...........weE..",
    "..........weE...",
    ".........weE....",
    "........weE.....",
    ".......weE......",
    "..d...weE.......",
    "...dsweE........",
    "....sds.........",
    "....BEsd........",
    "...BE...d.......",
    "..BE............",
    ".yY.............",
    "................",
])

ICONS["weapon_gorms_flail"] = grid([  # spiked ball, chain, handle
    "................",
    "..........l.....",
    ".......l.sdd.l..",
    "........sssdd...",
    ".......swssddd..",
    "......lssssdddl.",
    ".......ssddddd..",
    "........sdddd...",
    ".......l.ddd.l..",
    ".......d..l.....",
    "......s.........",
    ".....d..........",
    "....Yb..........",
    "...bB...........",
    "..bB............",
    "................",
])


def chakram():
    g = blank()
    cx = cy = 7.5

    def shade(x, y):
        d = (x - cx) - (y - cy)
        return "w" if d < -6 else "l" if d < -1 else "s" if d < 4 else "d"
    disc(g, cx, cy, 6.6, "s", shade)
    disc(g, cx, cy, 4.2, "Y", lambda x, y: "y" if x + y < 15 else "Y")
    disc(g, cx, cy, 2.6, ".")
    put(g, [(7, 0), (15, 7), (8, 15), (0, 8)], ".")
    put(g, [(13, 2), (2, 13)], "c")  # glints on the edge
    return g


ICONS["weapon_chakram"] = chakram()


def bomb():
    g = blank()
    cx, cy = 7.0, 9.5

    def shade(x, y):
        d = (x - cx) + (y - cy)
        return "s" if d < -3 else "d"
    disc(g, cx, cy, 4.8, "d", shade)
    put(g, [(5, 7), (5, 8), (6, 7)], "w")
    put(g, [(9, 5), (10, 5), (9, 4), (10, 4)], "Y")  # fuse cap
    put(g, [(11, 3), (12, 2)], "b")
    put(g, [(13, 1)], "h")
    put(g, [(14, 1), (13, 0), (12, 1), (13, 2), (14, 2)], "o")
    put(g, [(14, 0)], "y")
    return g


ICONS["weapon_bomb_satchel"] = bomb()

ICONS["blood_slide"] = grid([  # a boot skidding through blood
    "................",
    "................",
    "..........BBB...",
    "..........bbB...",
    "...www....bbB...",
    "..........bbB...",
    "....ww....bYY...",
    ".........bbbB...",
    "...e.....bbbbbb.",
    "..E..e...bbbbbb.",
    ".w.......BBBBBB.",
    ".eeeeeeeE.......",
    "..EeeeE.........",
    "....E...........",
    "................",
    "................",
])

ICONS["bread"] = grid([  # Bread and Water: a loaf and a jug
    "................",
    "..........rrr...",
    "..........rcr...",
    "...........r....",
    "..........rrr...",
    ".........rrrrr..",
    ".........rRrrr..",
    "...nnnn..rRrrr..",
    "..nnhnnn.rRrrR..",
    ".nnYnnYnnRRRRR..",
    ".nnnnnnnnn......",
    ".YnnnnnnnY......",
    "..YYYYYYY.......",
    "................",
    "................",
    "................",
])

ICONS["brute_force"] = grid([  # a clenched fist
    "................",
    "...w..w..w......",
    "................",
    "....ffffff......",
    "...fFfFfFff.....",
    "...ffffffff.....",
    "...fFFFFFFff....",
    "...ffffffffff...",
    "...fffffffFFf...",
    "....ffffffff....",
    ".....FffffF.....",
    ".....rrrrrr.....",
    ".....rRrRrr.....",
    ".....rrrrrr.....",
    "................",
    "................",
])

ICONS["coin_purse"] = grid([  # a fat purse spilling coins
    "................",
    "................",
    "......B..B......",
    ".......BB.......",
    "......YyyY......",
    ".....bbbbbb.....",
    "....bbbbbbbb....",
    "...bbbnbbbbbb...",
    "...bbnbbbbbBb...",
    "...bbbbbbbbBb...",
    "...bbbbbbbbBb...",
    "....bbbbbbBB....",
    "..y..BBBBBB..y..",
    ".yYy.......yYy..",
    "..y.........y...",
    "................",
])

ICONS["conductive_oil"] = grid([  # a flask of oil, sparking
    "................",
    "......bb....cw..",
    "......ll...cw...",
    "......ll..cwwc..",
    ".....llll...wc..",
    "....lYYYYl.wc...",
    "...lYooooYlc....",
    "...lohoooYl.....",
    "...loooooYl.....",
    "...lYoooYYl.....",
    "....lYYYYl......",
    ".....llll.......",
    "................",
    "................",
    "................",
    "................",
])

ICONS["fortune"] = grid([  # Fortune's Favour: a four-leaf clover
    "................",
    "....gg....gg....",
    "...gwgg..ggwg...",
    "...ggggGGgggg...",
    "....gggGGggg....",
    ".....ggGGgg.....",
    "....gggGGggg....",
    "...ggggGGgggg...",
    "...gGgg..ggGg...",
    "....gg....gg....",
    ".......G.....y..",
    "........G...yhy.",
    "........G....y..",
    ".........G......",
    "................",
    "................",
])

ICONS["greedy"] = grid([  # Greedy Hands: a magnet pulling coins in
    "................",
    ".....eeeeee.....",
    "....eeEEEEee....",
    "...eeE....Eee...",
    "...ee......ee...",
    "...ee......ee...",
    "...ee......ee...",
    "...ll......ll...",
    "...ww......ww...",
    "....w......w....",
    ".....y....y.....",
    "....yYy..yYy....",
    ".....y....y.....",
    "................",
    "................",
    "................",
])

ICONS["iron_hide"] = grid([  # a riveted iron breastplate
    "................",
    "................",
    "..lls....ssdd...",
    "..llssllssssdd..",
    "..lswssssssdd...",
    "...lssssssssd...",
    "...lsyslsyssd...",
    "...lssssssssd...",
    "...ldddddddd....",
    "...lssssssssd...",
    "....lssssssd....",
    ".....lssssd.....",
    "......dddd......",
    "................",
    "................",
    "................",
])

ICONS["kindling"] = grid([  # a fire catching on crossed sticks
    "........e.......",
    ".......eo.......",
    "......eoo..e....",
    ".....eooy.eo....",
    "....eooyyoo.....",
    "....eoyhyyoe....",
    "...eoyhhhyoe....",
    "...eoyhhhyoe....",
    "....eoyyyoe.....",
    ".....eooee......",
    "...bbB....Bbb...",
    ".....bbBBbb.....",
    "...Bbb....bbB...",
    "................",
    "................",
    "................",
])

ICONS["light_feet"] = grid([  # a winged sandal
    "................",
    "................",
    ".ww.............",
    ".wlww...........",
    "..wllww.........",
    "...wlllwbb......",
    "....wwlwbB......",
    "........bB......",
    "........bBy.....",
    ".ww....bbBbb....",
    "......bbbbbbbb..",
    ".www.BBBBBBBBB..",
    "................",
    "................",
    "................",
    "................",
])

ICONS["long_reach"] = grid([  # Long Reach (area): arrows pushing out from a ring
    "................",
    ".......ww.......",
    "......wwww......",
    ".......ll.......",
    "................",
    "......cccc......",
    "..w..c....c..w..",
    ".wwl.c.ww.c.lww.",
    ".wwl.c.ww.c.lww.",
    "..w..c....c..w..",
    "......cccc......",
    ".......ll.......",
    "......wwww......",
    ".......ww.......",
    "................",
    "................",
])

ICONS["lucky_cut"] = grid([  # Lucky Cut (crits): a dagger and a lucky star
    "................",
    "...........y....",
    "..........yhy...",
    "........yyhwhyy.",
    "..........yhy...",
    "...........y....",
    ".........w......",
    "........wl......",
    ".......wls......",
    "......wls.......",
    "...y.wls........",
    "....yY..........",
    "...bBy..........",
    "..bB............",
    ".yY.............",
    "................",
])

ICONS["open_wounds"] = grid([  # Open Wounds (bleed): drops of blood
    "................",
    "................",
    ".......e........",
    "......eee.......",
    "......eee.......",
    ".....eeeee......",
    "....eweeeee.....",
    "....eweeeeE.....",
    "....eeeeeeE.....",
    ".....eeeeE..e...",
    "..e...EEE..eee..",
    ".eee.......eEe..",
    ".eEe........E...",
    "..E.............",
    "................",
    "................",
])

ICONS["pyre"] = grid([  # Pyre (burn spreads on death): a burning skull
    "................",
    "....e...e...e...",
    "....eo.eo..eo...",
    "...eoo.eoo.eoo..",
    "...eoyoeoyooyoe.",
    "...eolllllllooe.",
    "...olwwwwwwlo...",
    "...lwkkwwkkwl...",
    "...lwkkwwkkwl...",
    "...lwwwkkwwwl...",
    "....lwwwwwwl....",
    ".....wkwkww.....",
    ".....lwlwll.....",
    "................",
    "................",
    "................",
])

ICONS["quick_hands"] = grid([  # Quick Hands (attack speed): an hourglass running fast
    "................",
    "...YYYYYYYYYY...",
    "....llllllll....",
    "....lnnnnnnl....",
    ".....lnnnnl.....",
    "......lnnl...ww.",
    ".......ll.......",
    ".......nn...www.",
    "......lnnl......",
    ".....l.nn.l..ww.",
    "....l..nn..l....",
    "....lnnnnnnl....",
    "....llllllll....",
    "...YYYYYYYYYY...",
    "................",
    "................",
])

ICONS["quick_study"] = grid([  # Quick Study (XP): an open book under a green gem
    "................",
    ".......gg.......",
    "......gwgG......",
    ".....gwggGG.....",
    "......gggG......",
    ".......GG.......",
    "................",
    "...wwww..wwww...",
    "..wlllwwwwlllw..",
    ".wwwwwwwwwwwwww.",
    ".wlllwwwwwwlllw.",
    ".wwwwwwwwwwwwww.",
    ".CCCCCCbbCCCCCC.",
    "..CCCCC..CCCCC..",
    "................",
    "................",
])

ICONS["second_wind"] = grid([  # Second Wind (regen): a heart, a plus, a breath of wind
    "................",
    "................",
    "................",
    "..ee...ee...g...",
    ".eeee.eeee.gwg..",
    ".ewweeeeeE..g...",
    ".ewEeeeeeE......",
    "..eeeeeeE.......",
    "...eeeeE...ccc..",
    "....eeE.......c.",
    ".....E......cc..",
    "................",
    "................",
    "................",
    "................",
    "................",
])

ICONS["serrated_edge"] = grid([  # Serrated Edge: a saw-toothed knife, dripping
    "................",
    "................",
    "................",
    "................",
    "....wwwwwwwww...",
    "....lllllllllw..",
    ".bbYsssssssssl..",
    ".BbYsssssssssd..",
    ".bbYsssssssssd..",
    "....dd..dd..dd..",
    "....d...d...d...",
    "........e.......",
    "................",
    "................",
    "................",
    "................",
])

ICONS["static_charge"] = grid([  # Static Charge: a lightning bolt
    "................",
    "........ccc.....",
    ".......ccw......",
    "......ccw.......",
    ".....ccw........",
    "....cwwwwwwc....",
    ".........cwc....",
    "........cwc.....",
    ".......cwc......",
    "......cwc.......",
    ".....cwc........",
    "....cwc.........",
    "...cc...........",
    "..c.............",
    "................",
    "................",
])

ICONS["thick_skin"] = grid([  # Thick Skin (max HP): a big heart
    "................",
    "................",
    "...eee....eee...",
    "..eweee..eeeee..",
    ".ewweeeeeeeeeeE.",
    ".eweeeeeeeeeeeE.",
    ".eeeeeeeeeeeeeE.",
    "..eeeeeeeeeeeE..",
    "...eeeeeeeeeE...",
    "....eeeeeeeE....",
    ".....eeeeeE.....",
    "......eeeE......",
    ".......EE.......",
    "................",
    "................",
    "................",
])



# ---------- Phase 5.5: evolutions (base icon + brighter metal / gold + one added part) and tag synergies ----------
def swap(g, table):
    return [[table.get(c, c) for c in row] for row in g]


def overlay(base, top):
    """`top` drawn over `base` wherever `top` isn't empty."""
    return [[t if t != "." else b for b, t in zip(rb, rt)] for rb, rt in zip(base, top)]


def mirrored(g):
    return [row[::-1] for row in g]


def champions_gladius():  # two clean blades crossed (the echo behind you), gold pommels
    clean = swap(ICONS["weapon_gladius"], {"r": "l", "R": "s", "b": "Y", "B": "Y"})
    return overlay(mirrored(clean), clean)


def impaler():  # the spear, longer tip, barbs and a gold collar
    g = [row[:] for row in ICONS["weapon_spear"]]
    put(g, [(15, 0)], "w")
    put(g, [(10, 3), (13, 5)], "l")
    put(g, [(9, 4), (14, 6)], "s")
    put(g, [(10, 5), (11, 5)], "y")
    return g


def thunderbolts():  # the javelins with charged heads and a bolt between them
    g = swap(ICONS["weapon_javelins"], {"l": "c", "s": "C"})
    put(g, [(8, 0), (7, 1), (8, 3), (7, 4)], "c")
    put(g, [(7, 2), (8, 2), (7, 3)], "w")
    return g


def twin_moons():  # two discs, one silver, one gold
    g = blank()
    disc(g, 5.0, 5.5, 4.6, "s", lambda x, y: "w" if x + y < 7 else "l" if x + y < 11 else "s")
    disc(g, 5.0, 5.5, 1.7, ".")
    disc(g, 10.5, 10.0, 4.6, "Y", lambda x, y: "h" if x + y < 18 else "y" if x + y < 22 else "Y")
    disc(g, 10.5, 10.0, 1.7, ".")
    put(g, [(13, 6), (6, 14)], "c")
    return g


ICONS["evo_champions_gladius"] = champions_gladius()
ICONS["evo_impaler"] = impaler()
ICONS["evo_thunderbolts"] = thunderbolts()
ICONS["evo_twin_moons"] = twin_moons()

ICONS["evo_fire_pot_barrage"] = grid([  # a clay fire-pot, flames over the rim
    "........o.......",
    ".....o.oyo..o...",
    ".....oyoyhoyo...",
    "......ohhhyo....",
    ".....YyyyyyyY...",
    "......BbbbbB....",
    ".....bbbbbbbB...",
    "....bnbbbbbbbB..",
    "...bnbbbbbbbbbB.",
    "...bnbbbbbbbbbB.",
    "...bbbbbbbbbbbB.",
    "....bbbbbbbbbB..",
    ".....bbbbbbbB...",
    "......BBBBBB....",
    "................",
    "................",
])

ICONS["evo_chain_of_ruin"] = grid([  # two spiked balls on one chain, gold-banded
    "................",
    "..........l.....",
    ".......l.sdd.l..",
    "........sssdd...",
    "..l....swyyydd..",
    ".sdd..lssssdddl.",
    "lsydl..ssddddd..",
    ".sdd....sdddd...",
    "..l..d.l.ddd.l..",
    ".....d....l.....",
    "......s.........",
    ".....d..........",
    "....Yb..........",
    "...bB...........",
    "..bB............",
    "................",
])


def sanguine_reaver():  # the greatsword with a blood crescent following it
    g = swap(ICONS["weapon_crimson_greatsword"], {"d": "Y", "s": "y"})
    put(g, [(12, 6), (14, 7), (15, 9), (15, 10), (14, 12)], "e")
    put(g, [(13, 6), (14, 8), (14, 9), (14, 10), (13, 12)], "e")
    put(g, [(13, 7), (13, 8), (13, 9), (13, 10), (13, 11)], "E")
    return g


ICONS["evo_sanguine_reaver"] = sanguine_reaver()

ICONS["synergy_wildfire"] = grid([  # three fires: it spreads
    "................",
    "........o.......",
    ".......oyo......",
    "..o....oyo...o..",
    ".oyo..oyhyo.oyo.",
    ".oyo..oyhyo.oyo.",
    "oyhyo.ohhho.oyho",
    "oyhyooyhhhyooyhy",
    ".ohhoohhwhhoohho",
    ".bBbbBbbbbbBbbB.",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
])

ICONS["synergy_bloodbath"] = grid([  # a big drop splashing
    "................",
    ".......e........",
    "......eee.......",
    "......eee.......",
    ".....eeeee......",
    ".....eweeeE.....",
    "....ewweeeeE....",
    "....eweeeeeE....",
    "....eeeeeeeE....",
    ".....eeeeeE.....",
    "..e...EEEE...e..",
    ".eE..........eE.",
    "....e..ee..e....",
    "...eE.eEEe.eE...",
    "................",
    "................",
])

ICONS["synergy_storm_front"] = grid([  # a storm cloud, bolts falling
    "................",
    ".....llll.......",
    "...llwwwwll.....",
    "..lwwwwwwwwll...",
    ".lwwwwwwwwwwwl..",
    ".lllllllllllll..",
    "..ssssssssssss..",
    "....c.....c.....",
    "...cw....cw.....",
    "..cwc...cwc.....",
    "...cw....cw.....",
    "...c....c.......",
    "..c....c........",
    "................",
    "................",
    "................",
])

ICONS["synergy_executioner"] = grid([  # an executioner's axe
    "................",
    "......lll.......",
    ".....lwwll......",
    "....lwwwlls.....",
    "....lwwllssd....",
    "....lwllsssdb...",
    ".....lssssdbB...",
    "......sssdbB....",
    "..........bB....",
    ".........bB.....",
    "........bB......",
    ".......bB.......",
    "......bB........",
    ".....bB.........",
    "....YY..........",
    "................",
])

ICONS["synergy_juggernaut"] = grid([  # a war hammer
    "................",
    "...ssssss.......",
    "..swwllssd......",
    "..sllssssd......",
    "..sssssddd.y....",
    "...ddddddyYb....",
    "..........bB....",
    ".........bB.....",
    "........bB......",
    ".......bB.......",
    "......bB........",
    ".....bB.........",
    "....bB..........",
    "...YY...........",
    "................",
    "................",
])


def bulwark():  # a round shield: wood, iron rim, gold boss
    g = blank()
    disc(g, 7.5, 7.5, 7.2, "s", lambda x, y: "l" if x + y < 9 else "s" if x + y < 21 else "d")
    disc(g, 7.5, 7.5, 5.8, "b", lambda x, y: "n" if x + y < 11 else "b" if x + y < 19 else "B")
    disc(g, 7.5, 7.5, 2.1, "y", lambda x, y: "h" if x + y < 15 else "Y")
    return g


ICONS["synergy_bulwark"] = bulwark()

# ---------- Phase 5.6: unlockable weapons, Boons and the MOVEMENT/GOLD/XP synergies ----------
ICONS["weapon_sling"] = grid([  # Sling (review iteration 2: read as a ladle): two cords in a V down to a pouch with its stone, a finger loop
    "................",
    ".nn.............",
    "n..n............",
    "n..n............",
    ".nnB............",
    "....B.......B...",
    ".....B.....B....",
    "......B...B.....",
    ".......B.B......",
    "......bbbbb.....",
    ".....bsllssb....",
    ".....bswlssb....",
    ".....bsssddb....",
    "......bsddb.....",
    ".......bbb......",
    "................",
])

ICONS["weapon_net"] = grid([  # Net: a round cord lattice, lead weights on the rim, a white knot
    "................",
    ".....d.nn.d.....",
    "....n.n..n.n....",
    "...n..n..n..n...",
    "..d.nnnnnnnn.d..",
    "..n.n..nn..n.n..",
    ".n..n..nn..n..n.",
    ".nnnnnnwwnnnnnn.",
    ".nnnnnnwwnnnnnn.",
    ".n..n..nn..n..n.",
    "..n.n..nn..n.n..",
    "..d.nnnnnnnn.d..",
    "...n..n..n..n...",
    "....n.n..n.n....",
    ".....d.nn.d.....",
    "................",
])

ICONS["weapon_gorms_whip"] = grid([  # Gorm's Whip: an iron-capped handle, the lash curling up to a white popper
    "................",
    ".......BBBB.....",
    ".....BB....BB...",
    "....B........B..",
    "...B..........w.",
    "...B.........ww.",
    "...B............",
    "....B...........",
    ".....BB.........",
    ".......B........",
    ".......bB.......",
    "......bbB.......",
    ".....bbB........",
    "....bbB.........",
    "...ddB..........",
    "...dd...........",
])


def cleaver():
    """Executioner's Cleaver (Phase 5.9): a slab of a blade with a hole in its far corner, a short wrapped handle."""
    g = blank()
    for y in range(3, 11):
        for x in range(5, 15):
            g[y][x] = "l"
    for x in range(5, 15):
        g[3][x] = "d"   # the spine
        g[10][x] = "w"  # the edge
    for y in range(4, 10):
        g[y][14] = "s"
    for y in (4, 5):
        for x in (11, 12):
            g[y][x] = "."  # the hole in the far corner (the outline pass rims it dark)
    put(g, [(6, 9), (7, 9), (9, 9)], "s")  # nicks along the edge
    put(g, [(4, 4), (4, 5)], "d")          # the ferrule
    put(g, [(0, 4), (1, 4), (2, 4), (3, 4)], "B")
    put(g, [(0, 5), (1, 5), (2, 5), (3, 5)], "b")
    return g


ICONS["weapon_executioners_cleaver"] = cleaver()

ICONS["loaded_dice"] = grid([  # Loaded Dice (iteration 2: read as toast): a die's top, front and side, five dark pips
    "................",
    "................",
    "....llllllllll..",
    "...lwwwwwwwwld..",
    "..llllllllllddd.",
    "..wwwwwwwwwwddd.",
    "..wkkwwwwkkwddd.",
    "..wkkwwwwkkwddd.",
    "..wwwwkkwwwwddd.",
    "..wwwwkkwwwwdd..",
    "..wkkwwwwkkwdd..",
    "..wkkwwwwkkwd...",
    "..wwwwwwwwwwd...",
    "...ddddddddd....",
    "................",
    "................",
])

ICONS["gilded_palm"] = grid([  # Gilded Palm: an open hand, gold dripping from the palm
    "................",
    ".....f.f.f......",
    ".....f.f.f.f....",
    ".....f.f.f.f....",
    ".....fffffff....",
    "..f..fyyyyfF....",
    "..ff.fyhyyfF....",
    "...ffyyyyyfF....",
    "....fyyyyyF.....",
    ".....fyyyF......",
    ".....FfffF......",
    "......y..y......",
    "......Y..y......",
    ".........Y......",
    "................",
    "................",
])

ICONS["winged_sandals"] = grid([  # Winged Sandals (iteration 2: read as a knife): a strapped sandal side-on, a feathered wing at the ankle
    "................",
    "................",
    "..w.............",
    ".wlw.ww.........",
    ".wlwwllw........",
    "..wlwlw.........",
    "...wwlw.bb......",
    "....wwwbnb......",
    "......bn.b......",
    ".....bnnnb......",
    "....bn.n.nbb....",
    "...bnnnnnnnnbb..",
    "..BBBBBBBBBBBBB.",
    "..BBBBBBBBBBBBB.",
    "................",
    "................",
])

ICONS["wax_tablet"] = grid([  # Wax Tablet: a wooden frame, yellow wax with scratched lines, a stylus
    "................",
    "...........ll...",
    "..bbbbbbbbls....",
    "..bYYYYYYls.b...",
    "..bYBBBBls.Yb...",
    "..bYYYYls.YYb...",
    "..bYBBBsBBYYb...",
    "..bYYYdYYYYYb...",
    "..bYBBBBBBYYb...",
    "..bYYYYYYYYYb...",
    "..bYBBBBYYYYb...",
    "..bYYYYYYYYYb...",
    "..bbbbbbbbbbb...",
    "...BBBBBBBBBB...",
    "................",
    "................",
])

ICONS["synergy_whirlwind"] = grid([  # Whirlwind (iteration 2: read as a target): a funnel of cyan and white wind bands
    "................",
    ".cccccccccccccc.",
    "..wwwwwwwwwwww..",
    "...cccccccccc...",
    "....wwwwwwww....",
    ".....cccccccc...",
    "......wwwwww....",
    ".......cccc.....",
    "......cccc......",
    ".......ww.......",
    "........cc......",
    ".......cc.......",
    "........c.......",
    "................",
    "................",
    "................",
])

ICONS["synergy_midas"] = grid([  # Midas: a golden hand over a pile of coins, sparkles
    "...h........h...",
    "........y.......",
    ".....y.y.y......",
    ".....y.y.y.y....",
    ".....yyyyyyy..h.",
    "..y..yyhyyyY....",
    "..yy.yyyyyyY....",
    "...yyyyyyyyY....",
    "....yyyyyyY.....",
    ".....YyyyY......",
    "................",
    "...yyy..yyy.....",
    "..yhyyYyhyyY....",
    "..yyyyYyyyyY....",
    "...YYY..YYY.....",
    "................",
])

ICONS["synergy_prodigy"] = grid([  # Prodigy: an open book, a star rising from it
    ".......y........",
    "......yhy.......",
    "....yyyhyyy.....",
    ".....yyhyy......",
    "......y.y.......",
    ".....y...y......",
    "................",
    "..wwwww.wwwww...",
    ".wlllllwlllllw..",
    ".wl.ll.wl.ll.w..",
    ".wlllllwlllllw..",
    ".wl.ll.wl.ll.w..",
    ".wlllllwlllllw..",
    ".CCCCCCCCCCCCC..",
    "..CCCCCC.CCCCC..",
    "................",
])

# ---------- frames ----------
def to_rgba(g):
    """Fill + 4-neighbour outline, as an RGBA pixel grid (None = transparent)."""
    px = [[PALETTE[c] + (255,) if c != "." else None for c in row] for row in g]
    out = [row[:] for row in px]
    for y in range(ART):
        for x in range(ART):
            if px[y][x] is not None:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < ART and 0 <= ny < ART and px[ny][nx] is not None and g[ny][nx] != "k":
                    out[y][x] = OUTLINE + (255,)
                    break
    return out


def shine(px, band):
    """A light band (x + y near `band`) sweeping across the fill; the outline stays dark."""
    out = [row[:] for row in px]
    for y in range(ART):
        for x in range(ART):
            p = px[y][x]
            if p is None or p[:3] == OUTLINE:
                continue
            d = abs((x + y) - band)
            if d <= 1.5:
                k = 0.7 if d < 0.75 else 0.4
                out[y][x] = tuple(int(c + (255 - c) * k) for c in p[:3]) + (255,)
    return out


def aura(px, frame, seed):
    """White mask around the silhouette in the 20 px cell: a soft two-pixel glow that flickers, and a few sparkles."""
    rng = random.Random(seed * 31 + frame)
    solid = [[False] * CELL for _ in range(CELL)]
    for y in range(ART):
        for x in range(ART):
            solid[y + PAD][x + PAD] = px[y][x] is not None
    dist = [[99] * CELL for _ in range(CELL)]
    for y in range(CELL):
        for x in range(CELL):
            if solid[y][x]:
                continue
            best = 99
            for yy in range(max(0, y - 3), min(CELL, y + 4)):
                for xx in range(max(0, x - 3), min(CELL, x + 4)):
                    if solid[yy][xx]:
                        best = min(best, max(abs(xx - x), abs(yy - y)))
            dist[y][x] = best
    img = [[0] * CELL for _ in range(CELL)]
    phase = frame / AURA_FRAMES
    for y in range(CELL):
        for x in range(CELL):
            d = dist[y][x]
            if d == 1:
                a = 210 if rng.random() > 0.12 else 120
            elif d == 2:
                wave = 0.5 + 0.5 * math.sin((x + y) * 0.7 + phase * 2 * math.pi)
                a = int(60 + 90 * wave) if rng.random() > 0.3 else 0
            else:
                a = 0
            img[y][x] = a
    # sparkles drift around the ring, a different spot every frame
    ring = [(x, y) for y in range(CELL) for x in range(CELL) if 2 <= dist[y][x] <= 3]
    for _ in range(3):
        if ring:
            x, y = ring[rng.randrange(len(ring))]
            img[y][x] = 255
    return img


def build():
    ids = list(ICONS.keys())
    sheet = Image.new("RGBA", (CELL * (ICON_FRAMES + AURA_FRAMES), CELL * len(ids)), (0, 0, 0, 0))
    for row, key in enumerate(ids):
        px = to_rgba(ICONS[key])
        frames = [px] + [shine(px, b) for b in (5, 15, 25)]
        for f, fr in enumerate(frames):
            for y in range(ART):
                for x in range(ART):
                    if fr[y][x] is not None:
                        sheet.putpixel((f * CELL + PAD + x, row * CELL + PAD + y), fr[y][x])
        for f in range(AURA_FRAMES):
            a = aura(px, f, row)
            for y in range(CELL):
                for x in range(CELL):
                    if a[y][x]:
                        sheet.putpixel(((ICON_FRAMES + f) * CELL + x, row * CELL + y), (255, 255, 255, a[y][x]))
    return sheet, ids


def main():
    sheet, ids = build()
    if "--preview" in sys.argv:
        out = Path(sys.argv[sys.argv.index("--preview") + 1])
        # icon frame 0 over its aura tinted blue, 6x, on a dark card colour, in a grid
        cols = 7
        rows = math.ceil(len(ids) / cols)
        scale = 6
        pv = Image.new("RGBA", (cols * CELL * scale, rows * CELL * scale), (20, 26, 36, 255))
        tints = [(210, 216, 230), (97, 230, 115), (82, 158, 255), (255, 189, 51)]
        for i, key in enumerate(ids):
            cell = Image.new("RGBA", (CELL, CELL), (20, 26, 36, 255))
            aura_img = sheet.crop((ICON_FRAMES * CELL, i * CELL, (ICON_FRAMES + 1) * CELL, (i + 1) * CELL))
            r, g, b, a = aura_img.split()
            tint = tints[i % 4]
            tinted = Image.merge("RGBA", (r.point(lambda v: v * tint[0] // 255), g.point(lambda v: v * tint[1] // 255),
                                          b.point(lambda v: v * tint[2] // 255), a))
            cell.alpha_composite(tinted)
            cell.alpha_composite(sheet.crop((0, i * CELL, CELL, (i + 1) * CELL)))
            pv.paste(cell.resize((CELL * scale, CELL * scale), Image.NEAREST), ((i % cols) * CELL * scale, (i // cols) * CELL * scale))
        pv.save(out)
        print("preview", out)
        return
    OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(OUT_PNG)
    OUT_TXT.write_text("".join(f"{key} {row}\n" for row, key in enumerate(ids)))
    print(f"{len(ids)} icons -> {OUT_PNG.relative_to(ROOT)} ({sheet.width}x{sheet.height})")


if __name__ == "__main__":
    main()
