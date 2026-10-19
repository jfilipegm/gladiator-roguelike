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
