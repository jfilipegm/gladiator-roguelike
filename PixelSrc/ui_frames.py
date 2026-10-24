"""UI frames (U1): the Roman frame art for every themed button, panel, card, tile, bar and the vexillum banner, drawn
here as code so the set is regenerable and diffable like the Blender scripts (no third-party art).

Spec: docs/tracks/U1-design.md §3-§4; names, sizes and the index format: docs/tracks/U1-contract.md ("UiFrames").
Every frame is point-filtered pixel art shown at 3 canvas units per art pixel (UiFrames.ArtScale). The script packs the
37 frames (one per FrameKind, in the enum's order; P3 added the focus glow, the NEW seal, the pilum arrows and the HUD
icons: docs/tracks/P3-contract.md) into one atlas with a 2 px transparent gutter, so the whole UI batches on one
texture, and writes the index the game reads at runtime:

    name x y w h bl bb br bt type pl pb pr pt

x y from the PNG's top-left; sizes, borders (left bottom right top) and paddings (content insets) in art pixels;
type sliced | tiled | simple.

    python3 PixelSrc/ui_frames.py              # writes Assets/Art/Textures/UI/UiFrames.png + UiFrames.txt
    python3 PixelSrc/ui_frames.py --preview    # also renders Screenshots/ui/ui_frames_preview.png

The preview slices and tiles each frame the way Unity's Image does (corners fixed, edges stretched or tiled), at 3
units per pixel, on the dark `night` tone and on an arena mid-tone, with the producer's fonts for the sample words.
Then `Gladiator/Setup/UI/All` in Unity imports the atlas and links it into the theme.
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT_PNG = ROOT / "Assets/Art/Textures/UI/UiFrames.png"
OUT_TXT = ROOT / "Assets/Art/Textures/UI/UiFrames.txt"
OUT_PREVIEW = ROOT / "Screenshots/ui/ui_frames_preview.png"
FONTS = ROOT / "Assets/Art/Fonts"

SCALE = 3      # canvas units per art pixel (UiFrames.ArtScale)
GUTTER = 2     # transparent pixels between atlas cells
ATLAS_W = 512  # atlas width; the height is the next power of two that fits (<= 512)

# ---------- colours: spec §3's tokens (UiTheme's defaults), then the shades derived from them -------------------------
TOKENS = {
    "night": "#191512", "stone": "#26201C", "stoneLight": "#2F2823", "line": "#4A3F36", "cardInside": "#1C1714",
    "ink": "#EFE6D6", "dust": "#B3A690", "outlineInk": "#1A0F08",
    "bronze": "#B07A3B", "bronzeLight": "#E0AE6C", "bronzeDark": "#5A3A1B", "bronzeInk": "#2A180A",
    "gold": "#E2B14A", "goldBright": "#FFCF4A", "crimson": "#7A1F25", "crimsonLight": "#9B2E33",
    "purple": "#4B2A5C", "purpleDark": "#2C1736", "purpleInk": "#F6D98C",
    "marble": "#ECE7DE", "marbleShade": "#DBD3C5", "marbleInk": "#3D342B",
    "waxWood": "#7D5734", "wax": "#251E19", "waxInk": "#D8C7A6",
    "danger": "#FF5433", "gain": "#7BE08A", "loss": "#F0806A", "hp": "#C8372D", "hpDark": "#8A2420",
    "xp": "#4FE3D1", "favor": "#FFC740", "coin": "#FFD64D",
}
SHADES = {
    # bronze plate, ansata and trough (the mockup's gradients, quantised)
    "bronzeEdge": "#3B2410", "bronzeHi": "#F4CC90", "bronzeFace1": "#E8B878", "bronzeFace2": "#CC9550",
    "bronzeFace3": "#B07A3B", "bronzeFace4": "#9A6831", "bronzeFace5": "#8A5A2B", "bronzeLo": "#6E4620",
    "bronzeRim": "#A8743A", "bronzeRimLo": "#7A5226", "bronzeLine": "#E0B070", "bronzeLineLo": "#B08850",
    "bronzePlate": "#3A2819", "bronzePlateEdge": "#2A1D12", "bronzeShadow": "#1B120A",
    "rivetHi": "#FFE6B4", "rivet": "#D9A560", "rivetLo": "#6E4A22",
    # marble
    "marbleEdge": "#5E554A", "marbleHi": "#FBF9F5", "marbleBevel": "#E4DED3", "marbleBevelLo": "#B5AA98",
    "marbleGroove": "#A99D89", "marbleLine": "#F7F4EE", "marbleVein": "#CBC1B0",
    "marbleFace1": "#F6F2EB", "marbleFace2": "#ECE7DE", "marbleFace3": "#DDD5C8", "marbleFace4": "#CFC6B6",
    "marbleLo": "#B9AF9F", "marbleAnsataEdge": "#4E4338",
    # wax tablet
    "woodEdge": "#24170D", "woodHi": "#A27A4E", "woodGrain": "#684627", "woodLo": "#5A3C21", "woodJoint": "#46301A",
    "waxShadow": "#140F0C",
    # mosaic
    "grout": "#120D0A", "field": "#211915", "ochre": "#C2A674", "ochreHi": "#DCC593", "ochreLo": "#A68B5B",
    "oxblood": "#7A2C25", "oxbloodHi": "#9A3F33", "oxbloodLo": "#5E211C",
    # dark stone
    "stoneEdge": "#120E0B", "stoneHi": "#3E352E", "stoneLo": "#1A1512", "stoneMid": "#2A231F",
    # card materials (the mockup's .c-common/.c-uncommon/.c-rare/.c-legendary)
    "cStoneEdge": "#24211E", "cStoneHi": "#8C867E", "cStone": "#6E6861", "cStoneLo": "#4F4943", "cStoneIn": "#46413B",
    "cBronzeEdge": "#24170D", "cBronzeHi": "#E6B678", "cBronze": "#C58D4A", "cBronzeLo": "#7C5226", "cBronzeIn": "#5E3D1C",
    "cSilverEdge": "#2A2D33", "cSilverHi": "#F4F6F9", "cSilver": "#C9CED6", "cSilverLo": "#8B929C", "cSilverIn": "#6C727C",
    "cGoldEdge": "#3B2508", "cGoldHi": "#FFF0BF", "cGold": "#F3CF73", "cGoldLo": "#A8741F", "cGoldIn": "#7E5514",
    "meanderInk": "#3B4250", "meanderSilver": "#CDD2D9", "meanderDarkGround": "#1E1612",
    # scutum and clipeus
    "shieldEdge": "#3B2508", "shieldRimHi": "#F6D78A", "shieldRim": "#E2B14A", "shieldRimLo": "#A8741F",
    "shield1": "#561619", "shield2": "#66191C", "shield3": "#781F22", "shield4": "#8A2A2C",
    "ironEdge": "#141518", "ironRimHi": "#9A9DA4", "ironRim": "#6E7178", "ironRimLo": "#4A4C52",
    "iron1": "#26282C", "iron2": "#2E3034", "iron3": "#383A3F", "iron4": "#45484E",
    # vexillum
    "poleHi": "#B07A3B", "pole1": "#8A5A2B", "pole2": "#6B4521", "pole3": "#4D3015", "poleEdge": "#24170D",
    "finialHi": "#FFF0BF", "finial": "#E2B14A", "finialLo": "#A8741F", "finialEdge": "#3B2508",
    "clothCEdge": "#3E0E12", "clothC1": "#9B2E33", "clothC2": "#8E282D", "clothC3": "#822329", "clothC4": "#7A1F25",
    "clothCFold": "#6E1B21", "clothPEdge": "#170A1D", "clothP1": "#4B2A5C", "clothP2": "#412450", "clothP3": "#371E45",
    "clothP4": "#2C1736", "clothPFold": "#26132F", "trimHi": "#FFE08A", "trim": "#E2B14A", "trimLo": "#9C6F1E",
    # laurel (gold, spec §1.3)
    "laurelHi": "#FFE7A0", "laurel": "#E2B14A", "laurelLo": "#A8741F", "laurelEdge": "#3B2508", "laurelStem": "#C99A35",
    # chips, troughs, knob
    "chipEdge": "#120A04", "chipHi": "#7A5226", "chipLo": "#3E2712", "keyHi": "#6B4524", "keyLo": "#170D05",
    "troughEdge": "#1A0F08", "channel": "#1A120C",
    # P3: the wreath's gilded laurel (gold where lit, olive in the mids and shadows) and its ribbon
    "wreathHi": "#FFE7A0", "wreathGold": "#E2B14A", "wreathOlive": "#8E9A3A", "wreathOliveLo": "#55622A",
    "wreathEdge": "#1F240E", "wreathStem": "#A8862E",
    "ribbonHi": "#E0574C", "ribbon": "#B42A2F", "ribbonLo": "#7A1A20", "ribbonEdge": "#3A0B0F",
    # P3: the NEW wax seal
    "sealEdge": "#3A0B0F", "sealRim": "#6E1B21", "sealLo": "#7E2228", "seal": "#9B2E33", "sealHi": "#C24A47",
    "sealShine": "#E58A7C", "sealInk": "#4A1015",
    # P3: the HUD icons (the heart in the hp reds, the aureus in gold, the skull's bone, the galea's steel and crest)
    "heartEdge": "#3A0A0C", "heartShine": "#FFC2B2", "heartHi": "#E85A48",
    "coinEdge": "#3B2508", "coinHi": "#FFF0BF", "coin1": "#F7D77E", "coin2": "#E8B94F", "coin3": "#C48E2C",
    "coinLo": "#94641A",
    "boneHi": "#FBF6EA", "bone": "#E6DCC6", "boneLo": "#B8A98A", "socket": "#1A0F08",
    "steelHi": "#E6EAF0", "steel": "#AEB4BE", "steelLo": "#737A86", "steelEdge": "#1C1E24",
    "crestHi": "#F06A55", "crest": "#C8372D", "crestLo": "#8A2420",
}
COLOURS = {**TOKENS, **SHADES}


def rgba(c, a=255):
    if c is None:
        return (0, 0, 0, 0)
    if isinstance(c, tuple):
        return c if len(c) == 4 else (*c, a)
    h = COLOURS.get(c, c).lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


class Art:
    """A small RGBA pixel canvas with the drawing helpers the frames need."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.p = self.im.load()

    def set(self, x, y, c, a=255):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.p[x, y] = rgba(c, a)

    def get(self, x, y):
        return self.p[x, y] if 0 <= x < self.w and 0 <= y < self.h else (0, 0, 0, 0)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def filled(self, x, y):
        return self.get(x, y)[3] > 0


def outline_inside(mask, w, h):
    """Pixels of `mask` with a 4-neighbour outside it (or on the image edge)."""
    out = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in mask:
                out.add((x, y))
                break
    return out


def rings(w, h, layers, centre, chamfer=0):
    """A bevelled rectangle: `layers` from the outside in, each (top/left colour, bottom/right colour), then `centre`.
    `chamfer` clears that many diagonal pixels at each corner (the outline follows)."""
    a = Art(w, h)
    for y in range(h):
        for x in range(w):
            r = min(x, y, w - 1 - x, h - 1 - y)
            if r < len(layers):
                tl, br = layers[r]
                top_left = (y == r and x < w - 1 - r) or (x == r and y < h - 1 - r)
                a.set(x, y, tl if top_left else br)
            else:
                a.set(x, y, centre)
    if chamfer:
        for cx, cy, sx, sy in ((0, 0, 1, 1), (w - 1, 0, -1, 1), (0, h - 1, 1, -1), (w - 1, h - 1, -1, -1)):
            for i in range(chamfer):
                for j in range(chamfer - i):
                    a.set(cx + sx * i, cy + sy * j, None)
            for i in range(chamfer + 1):  # re-outline the cut
                a.set(cx + sx * i, cy + sy * (chamfer - i), layers[0][0] if sy > 0 and sx > 0 else layers[0][1])
    return a


# ---------- the frames ------------------------------------------------------------------------------------------------

def ansata_mask(w=30, h=22):
    """The tabula ansata's silhouette (shared by both plates and the focus glow)."""
    mask = set()
    for y in range(h):
        for x in range(w):
            xl = min(x, w - 1 - x)
            if xl >= 7:
                mask.add((x, y))          # the plaque
            elif 1 + xl <= y <= 20 - xl:  # the handle: rows 1..20 at the tip, 7..14 where it meets the plaque
                mask.add((x, y))
    return mask


def ansata(marble=False):
    """Tabula ansata, 30x22: a plaque with a dovetail handle on each side (inside the 10 px side borders, so any height
    40-90 stretches them evenly); a light top row and a shadow row; the label is engraved by the caller."""
    w, h = 30, 22
    mask = ansata_mask(w, h)
    edge = outline_inside(mask, w, h)
    if marble:
        bands = ["marbleFace1", "marbleFace1", "marbleFace2", "marbleFace2", "marbleFace3", "marbleFace4"]
        hi, lo, ec = "marbleHi", "marbleLo", "marbleAnsataEdge"
    else:
        bands = ["bronzeFace1", "bronzeFace1", "bronzeFace2", "bronzeFace3", "bronzeFace4", "bronzeFace5"]
        hi, lo, ec = "bronzeHi", "bronzeLo", "bronzeEdge"
    a = Art(w, h)
    for (x, y) in mask:
        xl = min(x, w - 1 - x)
        band = min(len(bands) - 1, (y - 2) * len(bands) // (h - 4))
        if (x, y) in edge or xl == 7:  # the outline, and the plaque's sides drawn through the handles
            a.set(x, y, ec)
        elif (x, y - 1) in edge and xl > 7:
            a.set(x, y, hi)
        elif (x, y + 1) in edge:
            a.set(x, y, lo)
        elif xl < 7:  # the handles sit one band darker than the plaque's face
            a.set(x, y, bands[min(len(bands) - 1, band + 1)] if (x, y - 1) not in edge else bands[1])
        else:
            a.set(x, y, bands[band])
    return a


def marble():
    """Carved marble, 24x24 border 6: a bevelled off-white slab with a groove; veins in the corners; flat centre."""
    a = rings(24, 24, [("marbleEdge", "marbleEdge"), ("marbleHi", "marbleBevelLo"), ("marbleBevel", "marbleFace4"),
                       ("marbleGroove", "marbleGroove"), ("marbleLine", "marbleShade")], "marble", chamfer=1)
    # veins: short diagonal streaks crossing the bevel in the corners (corners never stretch)
    for (x, y) in ((1, 4), (2, 3), (3, 2), (19, 1), (20, 2), (21, 2), (2, 20), (3, 21), (21, 19), (20, 20), (22, 18)):
        a.set(x, y, "marbleVein")
    for (x, y) in ((5, 2), (4, 1)):
        a.set(x, y, "marbleShade")
    return a


def rivet(a, x, y):
    """A 3x3 dome rivet lit from the top-left, its top-left pixel at (x, y)."""
    for dx, dy, c in ((0, 0, "rivetHi"), (1, 0, "rivet"), (0, 1, "rivet"), (1, 1, "rivet"),
                      (2, 1, "rivetLo"), (1, 2, "rivetLo"), (2, 2, "rivetLo")):
        a.set(x + dx, y + dy, c)


def bronze():
    """Riveted bronze, 20x20 border 6: a bright bronze rim round a dark plate, a rivet in each corner."""
    a = rings(20, 20, [("bronzeShadow", "bronzeShadow"), ("bronzeFace2", "bronzeFace5"), ("bronzeRim", "bronzeRimLo"),
                       ("bronzeLine", "bronzeLineLo"), ("bronzePlateEdge", "bronzePlateEdge")], "bronzePlate")
    for (x, y) in ((3, 3), (14, 3), (3, 14), (14, 14)):
        rivet(a, x, y)
    return a


def wax():
    """Wax tablet, 24x24 border 7: a wooden frame (grain along each member, mitred corners) round dark wax."""
    a = rings(24, 24, [("woodEdge", "woodEdge"), ("woodHi", "woodLo"), ("waxWood", "waxWood"), ("woodGrain", "woodGrain"),
                       ("waxWood", "woodLo"), ("woodLo", "woodLo"), ("waxShadow", "waxShadow")], "wax")
    for i in range(1, 6):  # mitre joints
        a.set(i, i, "woodJoint")
        a.set(23 - i, i, "woodJoint")
        a.set(i, 23 - i, "woodJoint")
        a.set(23 - i, 23 - i, "woodJoint")
    for (x, y) in ((2, 6), (6, 2), (17, 2), (21, 6), (2, 17), (6, 21), (17, 21), (21, 17)):  # small pegs
        a.set(x, y, "woodHi")
    return a


def mosaic():
    """Mosaic, 25x25 borders l9 b8 r8 t9 (tiled): two rows of 3 px tesserae (ochre / oxblood checker) in dark grout
    round a dark field. The lattice has a 4 px period, so the 8 px edge pieces tile seamlessly."""
    w = h = 25
    a = Art(w, h)

    def lat(v):  # tessera index along an axis, or -1 for grout / outline
        return (v - 1) // 4 if 1 <= v <= 23 and (v - 1) % 4 != 3 else -1

    for y in range(h):
        for x in range(w):
            if 9 <= x <= 16 and 9 <= y <= 16:
                a.set(x, y, "field")
                continue
            i, j = lat(x), lat(y)
            if i < 0 or j < 0:
                a.set(x, y, "grout")
                continue
            ox, oy = (x - 1) % 4, (y - 1) % 4
            base = ("ochre", "ochreHi", "ochreLo") if (i + j) % 2 == 0 else ("oxblood", "oxbloodHi", "oxbloodLo")
            a.set(x, y, base[1] if (ox, oy) == (0, 0) else base[2] if (ox, oy) == (2, 2) else base[0])
    return a


def stone():
    """Dark stone plate, 12x12 border 4 (edict cards, locked entries, dark sub-panels); chipped corners."""
    return rings(12, 12, [("stoneEdge", "stoneEdge"), ("stoneHi", "stoneLo"), ("stoneLight", "stoneMid"),
                          ("stoneMid", "stoneMid")], "stone", chamfer=1)


MEANDER = [  # one period of the Greek key (a spiral hook on a baseline), 6x5
    ".#####",
    ".#.#.#",
    ".###.#",
    ".....#",
    "######",
]


def meander(gold=True):
    a = Art(6, 5)
    ink, ground = ("gold", "meanderDarkGround") if gold else ("meanderInk", "meanderSilver")
    for y, row in enumerate(MEANDER):
        for x, ch in enumerate(row):
            a.set(x, y, ink if ch == "#" else ground)
    return a


CARD = {  # the four card materials: edge, highlight, body, shadow, inner line
    "stone": ("cStoneEdge", "cStoneHi", "cStone", "cStoneLo", "cStoneIn"),
    "bronze": ("cBronzeEdge", "cBronzeHi", "cBronze", "cBronzeLo", "cBronzeIn"),
    "silver": ("cSilverEdge", "cSilverHi", "cSilver", "cSilverLo", "cSilverIn"),
    "gold": ("cGoldEdge", "cGoldHi", "cGold", "cGoldLo", "cGoldIn"),
}


def card(kind):
    """Card frame, 18x26, borders l3 b8 r3 t3, tiled, no centre: a 3 px rim (edge, highlight, body) and an 8 px foot.
    The same insets for every rarity; the material lives in the rim and the foot (stone chipped, bronze riveted, silver
    with the meander, gold with a bead row). Edge pieces repeat every 12 px (horizontal) and 15 px (vertical)."""
    w, h = 18, 26
    edge, hi, body, lo, inner = CARD[kind]
    a = Art(w, h)
    for y in range(h):
        for x in range(w):
            r = min(x, y, w - 1 - x)
            if y >= h - 8:  # the foot
                fy = y - (h - 8)  # 0..7
                if x == 0 or x == w - 1 or fy == 7:
                    a.set(x, y, edge)
                elif fy == 6:
                    a.set(x, y, lo)
                elif fy == 0 and 3 <= x <= w - 4:
                    a.set(x, y, inner)
                elif x == 1:
                    a.set(x, y, hi)
                else:
                    a.set(x, y, body if fy < 5 else lo)
            elif r == 0:
                a.set(x, y, edge)
            elif r == 1:
                a.set(x, y, hi if (y == 1 or x == 1) else lo)
            elif r == 2:
                a.set(x, y, body)
            # else: the inside stays clear (no centre)
    if kind == "stone":  # chips knocked out of the rim, and a crack in the foot
        for (x, y) in ((7, 0), (8, 0), (8, 1), (0, 9), (1, 9), (17, 6), (16, 13), (17, 13), (11, 25), (12, 25)):
            a.set(x, y, None)
        for (x, y) in ((7, 1), (9, 1), (8, 2), (1, 8), (1, 10), (2, 9), (16, 6), (15, 13), (16, 12), (11, 24), (12, 24)):
            a.set(x, y, edge)
        for (x, y) in ((5, 20), (6, 21), (6, 22), (13, 19), (14, 20)):
            a.set(x, y, lo)
    elif kind == "bronze":  # rivets along the rim and the foot
        for y in (6, 12):
            a.set(1, y, "rivetHi"); a.set(1, y + 1, lo)
            a.set(16, y, "rivetHi"); a.set(16, y + 1, lo)
        for x in (5, 11):
            a.set(x, 1, "rivetHi"); a.set(x + 1, 1, lo)
        for x in (4, 10, 16):
            rivet(a, x - 1, 20)
    elif kind == "silver":  # the meander in the foot, dark on silver
        for fy, row in enumerate(MEANDER):
            for x in range(3, w - 3):
                if row[(x - 3) % 6] == "#":
                    a.set(x, h - 7 + fy, "meanderInk")
    elif kind == "gold":  # a row of beads in the foot
        for x in range(3, w - 3, 3):
            a.set(x, 21, hi); a.set(x + 1, 21, body)
            a.set(x, 22, body); a.set(x + 1, 22, lo)
            a.set(x + 2, 21, lo); a.set(x + 2, 22, lo)
    return a


def halo():
    """Halo, 16x16 border 7: white stepped alpha rings round a solid core (the caller tints it the rarity colour)."""
    steps = [150, 122, 96, 72, 50, 32, 16, 0]
    a = Art(16, 16)
    for y in range(16):
        for x in range(16):
            dx = max(0.0, 7 - x - 0.5, x - 8 + 0.5) if not 7 <= x <= 8 else 0.0
            dy = max(0.0, 7 - y - 0.5, y - 8 + 0.5) if not 7 <= y <= 8 else 0.0
            d = math.hypot(dx, dy)
            s = steps[min(len(steps) - 1, int(d + 0.5))]
            if s:
                a.set(x, y, "#FFFFFF", s)
    return a


def focus():
    """Focus ring, 6x6 border 2, no centre: a 2 px white ring with soft corners (tint white / gold / crimson / danger)."""
    a = Art(6, 6)
    for y in range(6):
        for x in range(6):
            if min(x, y, 5 - x, 5 - y) < 2 and (x, y) not in ((0, 0), (5, 0), (0, 5), (5, 5)):
                a.set(x, y, "#FFFFFF")
    return a


def in_ellipse_corner(x, y, w, h, rx, ry):
    """Is pixel (x, y) inside a w x h rectangle whose corners are rx x ry ellipse quarters?"""
    px, py = x + 0.5, y + 0.5
    cx = rx if px < rx else w - rx if px > w - rx else px
    cy = ry if py < ry else h - ry if py > h - ry else py
    return ((px - cx) / rx) ** 2 + ((py - cy) / ry) ** 2 <= 1.0


def scutum(locked=False):
    """Scutum, 30x40 borders l9 b12 r9 t12: the legionary's curved shield (elliptical corners), a gold rim (dark iron
    when locked) and a body shaded darker towards the sides; the centre is flat so the clipeus' corners match it."""
    w, h = 30, 40
    if locked:
        edge, rhi, rim, rlo, body = "ironEdge", "ironRimHi", "ironRim", "ironRimLo", ("iron1", "iron2", "iron3", "iron4")
    else:
        edge, rhi, rim, rlo, body = "shieldEdge", "shieldRimHi", "shieldRim", "shieldRimLo", ("shield1", "shield2", "shield3", "shield4")
    mask = {(x, y) for y in range(h) for x in range(w) if in_ellipse_corner(x, y, w, h, 9, 12)}
    ring0 = outline_inside(mask, w, h)
    m1 = mask - ring0
    ring1 = outline_inside(m1, w, h)
    m2 = m1 - ring1
    ring2 = outline_inside(m2, w, h)
    a = Art(w, h)
    for (x, y) in mask:
        if (x, y) in ring0:
            a.set(x, y, edge)
        elif (x, y) in ring1:
            a.set(x, y, rhi if (x < w / 2 and y < h * 0.6) or y < 6 else rim if x < w / 2 or y < h / 2 else rlo)
        elif (x, y) in ring2:
            a.set(x, y, rim if x < w / 2 else rlo)
        else:
            dx = min(x, w - 1 - x)  # distance from the side: 3 = just inside the rim
            a.set(x, y, body[min(3, max(0, (dx - 3) // 2))])
    return a


def spine():
    """Scutum spine, 4x4 border 1: the gold bar above and below the clipeus (rounded by a light and a dark half)."""
    a = Art(4, 4)
    for y in range(4):
        for x in range(4):
            a.set(x, y, "shieldEdge" if x in (0, 3) or y in (0, 3) else "shieldRimHi" if x == 1 else "shieldRimLo")
    return a


def clipeus(locked=False):
    """Clipeus, 36x36 simple: the round portrait shield. A bronze ring (iron when locked) with studs, a transparent round
    window for the portrait behind it, and the corners in the scutum's centre colour, so no mask is needed."""
    w = h = 36
    c = 18.0
    if locked:
        corner, edge, hi, mid, lo, stud = "iron4", "ironEdge", "ironRimHi", "ironRim", "ironRimLo", "ironRimHi"
    else:
        corner, edge, hi, mid, lo, stud = "shield4", "bronzeShadow", "bronzeFace1", "bronze", "bronzeDark", "rivetHi"
    a = Art(w, h)
    for y in range(h):
        for x in range(w):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c)
            if d > 17.5:
                a.set(x, y, corner)
            elif d > 16.6:
                a.set(x, y, edge)
            elif d > 12.6:
                # light from the top-left: the ring's upper-left half bright, lower-right dark
                t = ((x + 0.5 - c) + (y + 0.5 - c)) / (d + 1e-6)
                a.set(x, y, hi if t < -0.55 else mid if t < 0.55 else lo)
            elif d > 11.7:
                a.set(x, y, edge)
            # inside: the window, clear
    for k in range(8):  # eight studs round the ring
        ang = k * math.pi / 4 + math.pi / 8
        sx, sy = int(c + 14.6 * math.cos(ang)), int(c + 14.6 * math.sin(ang))
        a.set(sx, sy, stud)
        a.set(sx + 1, sy + 1, edge)
    return a


def vex_bar():
    """Vexillum crossbar, 30x8 border l6 r6: a wooden pole (4 px) between two gold finials (in the side borders)."""
    w, h = 30, 8
    a = Art(w, h)
    for x in range(4, w - 4):
        a.set(x, 1, "poleEdge"); a.set(x, 2, "poleHi"); a.set(x, 3, "pole1"); a.set(x, 4, "pole2")
        a.set(x, 5, "pole3"); a.set(x, 6, "poleEdge")
    for side in (0, 1):
        for y in range(h):
            for x in range(5):
                d = math.hypot(x + 0.5 - 2.5, y + 0.5 - 4.0)
                if d <= 3.3:
                    xx = x if side == 0 else w - 1 - x
                    edge = d > 2.4
                    t = (x + 0.5 - 2.5) * (1 if side == 0 else -1) + (y + 0.5 - 4.0)
                    a.set(xx, y, "finialEdge" if edge else "finialHi" if t < -1.6 else "finial" if t < 1.0 else "finialLo")
        # the collar where the finial meets the pole
        cx = 5 if side == 0 else w - 6  # inside the 6 px side border, so it never stretches
        for y in range(1, 7):
            a.set(cx, y, "finialEdge" if y in (1, 6) else "finialLo")
    return a


CREASES = ((19, 6), (47, 10), (83, 5), (118, 8), (151, 6), (186, 11), (214, 5), (238, 8))  # x, length


def cloth(purple=False):
    """Vexillum cloth, 254x40 borders l4 b14 r4 t4: the standard's cloth (762x120 units at 3 per pixel), a vertical
    gradient with faint creases, gold trim under the bar and above the hem, and four swallowtail points with tassels in
    the 14 px bottom border."""
    w, h = 254, 40
    if purple:
        edge, bands, fold = "clothPEdge", ("clothP1", "clothP2", "clothP3", "clothP4"), "clothPFold"
    else:
        edge, bands, fold = "clothCEdge", ("clothC1", "clothC2", "clothC3", "clothC4"), "clothCFold"
    hem = 26  # first row of the hem (the 14 px bottom border): trim, then the points (rows 28-37), then the tassels
    tips = [w * (2 * k + 1) / 8 for k in range(4)]  # tooth tips at 12.5 / 37.5 / 62.5 / 87.5 %
    half = w / 8

    def inside(x, y):
        if not (0 <= x < w and 0 <= y < h - 2):
            return False
        if y < hem + 2:
            return True
        # below the trim: teeth narrowing to a point at row h-3
        depth = (y - (hem + 2)) / (h - 2 - (hem + 2))
        px = x + 0.5
        return any(abs(px - t) <= half * (1 - depth) + 0.5 for t in tips)

    mask = {(x, y) for y in range(h) for x in range(w) if inside(x, y)}
    ring = outline_inside(mask, w, h)
    a = Art(w, h)
    for (x, y) in mask:
        if (x, y) in ring and y > 0:
            a.set(x, y, edge)
            continue
        if y == 0:
            a.set(x, y, edge)  # tucked under the bar
        elif y in (2, 3):
            a.set(x, y, "trim" if y == 2 else "trimLo")
        elif y in (hem, hem + 1):
            a.set(x, y, "trimHi" if y == hem else "trim")
        else:
            band = bands[min(3, (y - 4) * 4 // (hem - 4))] if y < hem else bands[3]
            # faint creases hanging from the bar: 1 px, irregular lengths, one band darker (never the fold ink over text)
            crease = any(x == cx and 4 <= y <= 4 + ln for cx, ln in CREASES)
            a.set(x, y, bands[min(3, bands.index(band) + 1)] if crease and band != bands[3] else fold if crease else band)
    for t in tips:  # tassels hanging from each point
        tx = int(t)
        a.set(tx - 1, h - 3, "trim"); a.set(tx, h - 3, "trim")
        a.set(tx - 1, h - 2, "trimHi"); a.set(tx, h - 2, "trimLo")
        a.set(tx - 1, h - 1, "trimLo"); a.set(tx, h - 1, "trimLo")
    return a


def trough():
    """Trough, 12x8 border 2: a bronze rim (light top, dark bottom) round a dark channel; fills inset 2 px."""
    return rings(12, 8, [("troughEdge", "troughEdge"), ("bronzeFace2", "bronzeRimLo")], "channel")


def trough_fill():
    """TroughFill, 2x4 simple: a white top row and 85 % grey below (the caller tints it and sets Image.Type.Filled)."""
    a = Art(2, 4)
    for x in range(2):
        a.set(x, 0, "#FFFFFF")
        for y in range(1, 4):
            a.set(x, y, "#D9D9D9")
    return a


KNOB = [
    ".####.",
    "#hhmm#",
    "#hmml#",
    "#mmml#",
    "#mmll#",
    "#mmll#",
    "#mlll#",
    ".####.",
]


def knob():
    a = Art(6, 8)
    cols = {"#": "bronzeShadow", "h": "bronzeFace1", "m": "bronze", "l": "bronzeFace5"}
    for y, row in enumerate(KNOB):
        for x, ch in enumerate(row):
            if ch != ".":
                a.set(x, y, cols[ch])
    return a


def chip(key=False):
    """Chip / key chip, 8x8 border 3: a small dark-bronze plate, or the darker bronzeInk key plate."""
    if key:
        return rings(8, 8, [("chipEdge", "chipEdge"), ("keyHi", "keyLo"), ("bronzeInk", "bronzeInk")], "bronzeInk", chamfer=1)
    return rings(8, 8, [("chipEdge", "chipEdge"), ("chipHi", "chipLo"), ("bronzeDark", "bronzeDark")], "bronzeDark", chamfer=1)


# the mockup's laurel branch (an SVG <g id="branch">, 62x60): the stem, a quadratic curve, and nine leaves (ellipses
# cx, cy, rx, ry, rotation in degrees). The wreath is the branch plus its mirror about x = 60.
BRANCH_STEM = ((60, 56), (20, 50), (10, 12))
BRANCH_LEAVES = [
    (42.7, 48.0, 6.5, 2.6, 240), (40.5, 54.0, 6.5, 2.6, 160), (31.4, 41.3, 6.5, 2.6, 253.9), (27.8, 46.6, 6.5, 2.6, 173.9),
    (22.7, 32.3, 6.5, 2.6, 268.9), (17.9, 36.5, 6.5, 2.6, 188.9), (16.3, 21.0, 6.0, 2.4, 283.2), (10.6, 23.9, 6.0, 2.4, 203.2),
    (9.6, 10.9, 5.5, 2.2, 252.5),
]


GOLD_LAUREL = ((0.25, "laurelHi"), (-0.3, "laurel"), (-9.0, "laurelLo"), "laurelStem", "laurelEdge")


def laurel(w, h, mirrored_pair, box, leaf_scale=1.0, tones=GOLD_LAUREL):
    """Rasterise the branch (or the wreath) into w x h pixels with a 1 px dark outline. `box` = the SVG area mapped to
    the pixels inside the outline; leaves are lit from the top-left (a bright upper side, a dark lower side). `tones`:
    the leaf colours as (light above which, colour) from lit to shadow, then the stem's and the outline's colours."""
    *leaf_tones, stem_tone, edge_tone = tones
    leaves = list(BRANCH_LEAVES)
    stems = [BRANCH_STEM]
    if mirrored_pair:
        leaves += [(120 - cx, cy, rx, ry, 180 - rot) for (cx, cy, rx, ry, rot) in BRANCH_LEAVES]
        stems.append(tuple((120 - x, y) for (x, y) in BRANCH_STEM))
    bx0, by0, bx1, by1 = box
    sx, sy = (bx1 - bx0) / (w - 2), (by1 - by0) / (h - 2)
    stem_pts = []
    for (p0, p1, p2) in stems:
        for i in range(81):
            t = i / 80
            stem_pts.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]))
    n = 5
    cls = {}
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            leaf_hits, stem_hits, light = 0, 0, 0.0
            for j in range(n):
                for i in range(n):
                    vx = bx0 + (x - 1 + (i + 0.5) / n) * sx
                    vy = by0 + (y - 1 + (j + 0.5) / n) * sy
                    hit = False
                    for (cx, cy, rx, ry, rot) in leaves:
                        r = math.radians(rot)
                        dx, dy = vx - cx, vy - cy
                        u = dx * math.cos(r) + dy * math.sin(r)
                        v = -dx * math.sin(r) + dy * math.cos(r)
                        rxs, rys = rx * leaf_scale, ry * leaf_scale
                        if (u / rxs) ** 2 + (v / rys) ** 2 <= 1.0:
                            hit = True
                            # the leaf's side facing up-left is lit
                            nx, ny = -math.sin(r), math.cos(r)
                            side = 1 if v > 0 else -1
                            light += side * (nx * -0.6 + ny * -0.8) * min(1.0, abs(v) / rys + 0.3)
                            break
                    if hit:
                        leaf_hits += 1
                    elif min((vx - qx) ** 2 + (vy - qy) ** 2 for (qx, qy) in stem_pts) <= 1.3 ** 2:
                        stem_hits += 1
            total = n * n
            if leaf_hits >= total * 0.42:
                l = light / max(1, leaf_hits)
                cls[(x, y)] = next(c for (t, c) in leaf_tones if l > t or t == leaf_tones[-1][0])
            elif stem_hits + leaf_hits >= total * 0.36:
                cls[(x, y)] = stem_tone
    a = Art(w, h)
    for (x, y), c in cls.items():
        a.set(x, y, c)
    for y in range(h):  # a 1 px dark outline round the silhouette
        for x in range(w):
            if (x, y) not in cls and any((x + dx, y + dy) in cls for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                a.set(x, y, edge_tone)
    return a


def laurel_branch():
    """One laurel branch, 20x24 (the left one; UiKit mirrors it for the right): a stem curving like "(" from its base at
    the bottom-right (next to the word) up to the tip, with leaves in pairs pointing to the tip, lit from the top-left.
    Drawn from its own geometry (the mockup's branch clumps at this size)."""
    w, h = 20, 24
    B, C, T = (15.4, 22.4), (2.6, 18.0), (6.6, 2.4)  # base, control, tip (pixels)

    def bez(t):
        return ((1 - t) ** 2 * B[0] + 2 * (1 - t) * t * C[0] + t * t * T[0],
                (1 - t) ** 2 * B[1] + 2 * (1 - t) * t * C[1] + t * t * T[1])

    def tangent(t):
        dx = 2 * (1 - t) * (C[0] - B[0]) + 2 * t * (T[0] - C[0])
        dy = 2 * (1 - t) * (C[1] - B[1]) + 2 * t * (T[1] - C[1])
        n = math.hypot(dx, dy)
        return dx / n, dy / n

    leaves = []  # (cx, cy, ux, uy, half length, half width, outer?)
    for t, size in ((0.17, 1.0), (0.39, 0.97), (0.6, 0.92), (0.8, 0.84)):
        px_, py_ = bez(t)
        tx, ty = tangent(t)
        for side in (-1, 1):  # -1 = outer (left of the stem), 1 = inner
            nx, ny = -ty * side, tx * side
            ang = math.radians(52)
            ux, uy = tx * math.cos(ang) + nx * math.sin(ang), ty * math.cos(ang) + ny * math.sin(ang)
            hl, hw = 3.0 * size, 1.1 * size
            leaves.append((px_ + ux * hl * 1.05, py_ + uy * hl * 1.05, ux, uy, hl, hw, side < 0))
    tx, ty = tangent(1.0)
    leaves.append((T[0] + tx * 1.4, T[1] + ty * 1.4, tx, ty, 2.4, 1.05, True))  # the tip leaf
    stem = [bez(i / 60) for i in range(61)]
    n = 6
    cls = {}
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            votes = {}
            for j in range(n):
                for i in range(n):
                    vx, vy = x + (i + 0.5) / n, y + (j + 0.5) / n
                    hit = None
                    for li, (cx, cy, ux, uy, hl, hw, outer) in enumerate(leaves):
                        du, dv = (vx - cx) * ux + (vy - cy) * uy, -(vx - cx) * uy + (vy - cy) * ux
                        if (du / hl) ** 2 + (dv / hw) ** 2 <= 1.0:
                            lit = (-uy) * -0.6 + ux * -0.8  # the minor axis' "+" side, dotted with the light
                            up = (dv > 0) == (lit > 0)
                            hit = (li, ("laurelHi" if up else "laurel") if outer else ("laurel" if up else "laurelLo"))
                            break
                    if hit is None and min((vx - qx) ** 2 + (vy - qy) ** 2 for (qx, qy) in stem) <= 0.55 ** 2:
                        hit = (-1, "laurelStem")
                    if hit:
                        votes[hit] = votes.get(hit, 0) + 1
            if sum(votes.values()) >= n * n * 0.4:
                cls[(x, y)] = max(votes, key=votes.get)
    a = Art(w, h)
    for (x, y), (li, c) in cls.items():
        # where two leaves touch, the later one's edge pixel goes dark so each leaf keeps its own shape
        touching = any(cls.get((x + dx, y + dy), (li, c))[0] not in (li, -1) and cls[(x + dx, y + dy)][0] < li
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) and li >= 0
        a.set(x, y, "laurelLo" if touching and c != "laurelLo" else "laurelEdge" if touching else c)
    for y in range(h):
        for x in range(w):
            if (x, y) not in cls and any((x + dx, y + dy) in cls for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                a.set(x, y, "laurelEdge")
    return a


WREATH_TONES = ((0.3, "wreathHi"), (-0.02, "wreathGold"), (-0.35, "wreathOlive"), (-9.0, "wreathOliveLo"),
                "wreathStem", "wreathEdge")

RIBBON = [  # the bow tied where the two stems meet (12x7, its top-left at WREATH_BOW): two loops, the knot, two tails
    "oo........oo",
    "ohoo....ooro",
    "ohrroooorrlo",
    "orrlohrolrlo",
    ".oooorloooo.",
    "...oloolo...",
    "...oo..oo...",
]
WREATH_BOW = (24, 23)


def laurel_wreath():
    """Laurel wreath, 60x30 (P3, note 10): the U1 branch pair, now gilded laurel: gold where the leaves catch the light,
    olive green in the mids and shadows, and a small red ribbon tied where the stems meet."""
    a = laurel(60, 30, True, (3.0, 4.0, 117.0, 60.5), leaf_scale=1.12, tones=WREATH_TONES)
    cols = {"o": "ribbonEdge", "h": "ribbonHi", "r": "ribbon", "l": "ribbonLo", "k": "ribbonLo"}
    bx, by = WREATH_BOW
    for y, row in enumerate(RIBBON):
        for x, ch in enumerate(row):
            if ch != ".":
                a.set(bx + x, by + y, cols[ch])
    return a


def pixels(rows, cols):
    """Pixel art from strings: one character per pixel, '.' clear, every other character a key of `cols`."""
    a = Art(len(rows[0]), len(rows))
    for y, row in enumerate(rows):
        assert len(row) == a.w, (row, len(row))
        for x, ch in enumerate(row):
            if ch != ".":
                a.set(x, y, cols[ch])
    return a


def mirrored(art):
    out = Art(art.w, art.h)
    out.im = art.im.transpose(Image.FLIP_LEFT_RIGHT)
    out.p = out.im.load()
    return out


def ansata_glow():
    """AnsataGlow, 34x26 border l12 b2 r12 t2, sliced, no centre (P3, note 1): the focus rim that follows the plate's
    shape. ansata()'s silhouette placed 2 px in and dilated by 2 px; only the ring is drawn (the plate's own pixels stay
    clear): the inner row opaque, the outer row ~45 %, in white so UiPlate.FocusColor tints it. An Image of the plate's
    size + 12 units (2 art px x 3 per side) puts the ring exactly round the plate: the 2 px top and bottom rows are the
    fixed border and the 22 rows between stretch like the ansata's own; the 12 px sides hold the handles (10) + 2."""
    w, h = 34, 26
    mask = {(x + 2, y + 2) for (x, y) in ansata_mask()}
    a = Art(w, h)
    for y in range(h):
        for x in range(w):
            if (x, y) in mask:
                continue
            d = min(math.hypot(x - mx, y - my) for (mx, my) in mask)
            if d < 1.5:
                a.set(x, y, "#FFFFFF")
            elif d < 2.3:
                a.set(x, y, "#FFFFFF", 115)
    return a


SEAL_MASK = [  # 12x12: a round seal, symmetric (note 27, v0.27.0: the irregular wax read as "not a circle")
    "....####....",
    "..########..",
    ".##########.",
    ".##########.",
    "############",
    "############",
    "############",
    "############",
    ".##########.",
    ".##########.",
    "..########..",
    "....####....",
]
SEAL_N = [  # 4x6, pressed in the middle of the field (cols 4-7, rows 3-8)
    "G..G",
    "GG.G",
    "GG.G",
    "G.GG",
    "G.GG",
    "G..G",
]


def seal_new():
    """SealNew, 12x12 simple (P3, note 6; note 27): the NEW mark, a round crimson seal (a dark edge, a raised rim lit from
    the top-left, the pressed field) with a gold N (novum) in its exact centre."""
    mask = {(x, y) for y, row in enumerate(SEAL_MASK) for x, ch in enumerate(row) if ch == "#"}
    edge = outline_inside(mask, 12, 12)
    rim = outline_inside(mask - edge, 12, 12)
    a = Art(12, 12)
    for (x, y) in mask:
        if (x, y) in edge:
            a.set(x, y, "sealEdge")
        elif (x, y) in rim:
            a.set(x, y, "sealHi" if x + y < 11 else "sealRim")
        else:
            a.set(x, y, "seal")
    a.set(3, 2, "sealShine")
    # the N dead centre (cols 4-7, rows 3-8 of 12), no cast shadow: the shadow pulled it off centre (note 27)
    gold = {(4 + x, 3 + y) for y, row in enumerate(SEAL_N) for x, ch in enumerate(row) if ch == "G"}
    for (x, y) in gold:
        a.set(x, y, "goldBright")
    return a


PILUM = [  # pointing left, rows (top, bottom) per column: the barbed head (8 px at its base), the pilum's long thin
    (5, 6), (4, 7), (3, 8), (3, 8), (2, 9), (2, 9),  # iron shank, and the short socket where the wooden shaft sits
    (4, 7), (4, 7), (4, 7), (4, 7), (4, 7),
    (3, 8), (3, 8), (3, 8), (3, 8), (4, 7),
]


def pilum(left=True):
    """ArrowLeft / ArrowRight, 16x12 simple (P3, note 11): a bronze pilum tip as the pager arrow (mirror images). A dark
    outline; the ridge along the middle splits the lit upper facets from the dark lower ones; a dark ring at the
    socket's mouth."""
    mask = {(x, y) for x, (top, bottom) in enumerate(PILUM) for y in range(top, bottom + 1)}
    edge = outline_inside(mask, 16, 12)
    a = Art(16, 12)
    for (x, y) in mask:
        if (x, y) in edge or (x == 11 and 4 <= y <= 7):
            a.set(x, y, "bronzeEdge")
        elif y <= 5:
            a.set(x, y, "bronzeHi" if (x, y - 1) in edge else "bronzeFace1")
        else:
            a.set(x, y, "bronzeFace5" if (x, y + 1) in edge else "bronzeFace3")
    return a if left else mirrored(a)


HEART = [  # 16x16: a chunky heart in the hp reds, lit up-left; its box sits half a pixel low (the optical centre)
    "................",
    "................",
    "..oooo....oooo..",
    ".ohhRRo..oRRRRo.",
    "ohwhRRRooRRRRRDo",
    "ohhRRRRRRRRRRRDo",
    "ohRRRRRRRRRRRRDo",
    "ohRRRRRRRRRRRDDo",
    ".oRRRRRRRRRRRDo.",
    "..oRRRRRRRRRDo..",
    "...oRRRRRRRDo...",
    "....oRRRRRDo....",
    ".....oRRRDo.....",
    "......oDDo......",
    ".......oo.......",
    "................",
]


def icon_heart():
    """IconHeart, 16x16 simple (P3, note 13): the HUD's HP icon."""
    return pixels(HEART, {"o": "heartEdge", "w": "heartShine", "h": "heartHi", "R": "hp", "D": "hpDark"})


COIN_MASK = [  # 16x16: the round flan
    ".....######.....",
    "...##########...",
    "..############..",
    ".##############.",
    ".##############.",
    "################",
    "################",
    "################",
    "################",
    "################",
    "################",
    ".##############.",
    ".##############.",
    "..############..",
    "...##########...",
    ".....######.....",
]
AUREUS_HEAD = [  # 9x10 at (4, 3): a laureate head facing right, struck in relief: the laurel round the crown (leaves W
    "..hWhW...",  # lit, h shaded), the head and face (F), the eye and the jaw line (x), the wreath's ties at the nape (t);
    ".hWFFFh..",  # the head's top-left edges catch the light and a shadow falls down-right on the field
    "hWFFFFF..",
    "WFFFFFFF.",
    "FFFFFxFF.",
    "FFFFFFFFF",
    "tFFFFFF..",
    "t.FxFFFF.",
    "...FFFF..",
    "..FFFFFF.",
]


def icon_aureus():
    """IconAureus, 16x16 simple (P3, note 13): the HUD's gold and the price tags, a Roman aureus (not an arcade coin):
    a dark edge, a beaded rim, and a laureate head in relief on the field."""
    mask = {(x, y) for y, row in enumerate(COIN_MASK) for x, ch in enumerate(row) if ch == "#"}
    edge = outline_inside(mask, 16, 16)
    beads = outline_inside(mask - edge, 16, 16)
    a = Art(16, 16)
    for (x, y) in mask:
        a.set(x, y, "coinEdge" if (x, y) in edge else "coin2")
    for i, (x, y) in enumerate(sorted(beads, key=lambda p: math.atan2(p[1] + 0.5 - 8, p[0] + 0.5 - 8))):
        a.set(x, y, "coinHi" if i % 2 == 0 else "coin3")
    head = {}
    for y, row in enumerate(AUREUS_HEAD):
        for x, ch in enumerate(row):
            if ch != ".":
                head[(4 + x, 3 + y)] = ch
    for (x, y), ch in head.items():
        if ch == "F":
            lit = (x - 1, y) not in head or (x, y - 1) not in head
            a.set(x, y, "coinHi" if lit else "coin1")
        else:
            a.set(x, y, {"h": "coin3", "W": "coinHi", "x": "coinLo", "t": "coin3"}[ch])
    for (x, y) in mask - edge - beads:  # the relief's shadow on the field, down-right of the head
        if (x, y) not in head and any(head.get(n, "t") != "t" for n in ((x - 1, y), (x, y - 1))):
            a.set(x, y, "coinLo")
    return a


SKULL_GALEA = [  # 16x16, symmetric: a red crest fanning out from the galea's bowl (gold holder, gold brow band, steel
    "................",  # cheek guards) over a bone skull with dark sockets, a nose hole and a row of teeth
    "....oooooooo....",
    "...oHCcCCcCCo...",
    "...oCCcCCcCCo...",
    "....occCCcco....",
    "....oSSggSSo....",
    "...oSsSSSSSSo...",
    "..oGGGGGGGGGGo..",
    ".oSoBBBBBBBBoSo.",
    ".oSoBkkBBkkBoSo.",
    ".oSoBkkBBkkBoSo.",
    ".oSooBBkkBBooSo.",
    "..oSoBBBBBBoSo..",
    "..oSoTtTTtToSo..",
    "...oo.oooo.oo...",
    "................",
]


def icon_skull_galea():
    """IconSkullGalea, 16x16 simple (P3, note 13): the HUD's kills, a straight skull in a Roman galea with a red crest."""
    return pixels(SKULL_GALEA, {"o": "socket", "H": "crestHi", "C": "crest", "c": "crestLo", "g": "gold", "S": "steel",
                                "s": "steelHi", "G": "gold", "B": "bone", "k": "socket", "T": "bone", "t": "boneLo"})


# ---------- the catalogue: FrameKind order (append, never reorder) ----------------------------------------------------
# name, art, border (l, b, r, t), type, padding (l, b, r, t)
def catalogue():
    return [
        ("AnsataBronze", ansata(False), (10, 0, 10, 0), "sliced", (10, 2, 10, 2)),
        ("AnsataMarble", ansata(True), (10, 0, 10, 0), "sliced", (10, 2, 10, 2)),
        ("Marble", marble(), (6, 6, 6, 6), "sliced", (8, 8, 8, 8)),
        ("Bronze", bronze(), (6, 6, 6, 6), "sliced", (7, 7, 7, 7)),
        ("Wax", wax(), (7, 7, 7, 7), "sliced", (8, 8, 8, 8)),
        ("Mosaic", mosaic(), (9, 8, 8, 9), "tiled", (10, 10, 10, 10)),
        ("Stone", stone(), (4, 4, 4, 4), "sliced", (5, 5, 5, 5)),
        ("MeanderGold", meander(True), (0, 0, 0, 0), "tiled", (0, 0, 0, 0)),
        ("MeanderDark", meander(False), (0, 0, 0, 0), "tiled", (0, 0, 0, 0)),
        ("CardStone", card("stone"), (3, 8, 3, 3), "tiled", (3, 8, 3, 3)),
        ("CardBronze", card("bronze"), (3, 8, 3, 3), "tiled", (3, 8, 3, 3)),
        ("CardSilver", card("silver"), (3, 8, 3, 3), "tiled", (3, 8, 3, 3)),
        ("CardGold", card("gold"), (3, 8, 3, 3), "tiled", (3, 8, 3, 3)),
        ("Halo", halo(), (7, 7, 7, 7), "sliced", (0, 0, 0, 0)),
        ("Focus", focus(), (2, 2, 2, 2), "sliced", (0, 0, 0, 0)),
        ("Scutum", scutum(False), (9, 12, 9, 12), "sliced", (9, 12, 9, 12)),
        ("ScutumLocked", scutum(True), (9, 12, 9, 12), "sliced", (9, 12, 9, 12)),
        ("ScutumSpine", spine(), (1, 1, 1, 1), "sliced", (0, 0, 0, 0)),
        ("Clipeus", clipeus(False), (0, 0, 0, 0), "simple", (6, 6, 6, 6)),
        ("ClipeusLocked", clipeus(True), (0, 0, 0, 0), "simple", (6, 6, 6, 6)),
        ("VexillumBar", vex_bar(), (6, 0, 6, 0), "sliced", (6, 0, 6, 0)),
        ("VexillumCrimson", cloth(False), (4, 14, 4, 4), "sliced", (6, 14, 6, 5)),
        ("VexillumPurple", cloth(True), (4, 14, 4, 4), "sliced", (6, 14, 6, 5)),
        ("Trough", trough(), (2, 2, 2, 2), "sliced", (2, 2, 2, 2)),
        ("TroughFill", trough_fill(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("Knob", knob(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("Chip", chip(False), (3, 3, 3, 3), "sliced", (2, 2, 2, 2)),
        ("KeyChip", chip(True), (3, 3, 3, 3), "sliced", (2, 2, 2, 2)),
        ("LaurelBranch", laurel_branch(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("LaurelWreath", laurel_wreath(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        # P3 (docs/tracks/P3-contract.md "P3-art")
        ("AnsataGlow", ansata_glow(), (12, 2, 12, 2), "sliced", (0, 0, 0, 0)),
        ("SealNew", seal_new(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("ArrowLeft", pilum(True), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("ArrowRight", pilum(False), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("IconHeart", icon_heart(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("IconAureus", icon_aureus(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
        ("IconSkullGalea", icon_skull_galea(), (0, 0, 0, 0), "simple", (0, 0, 0, 0)),
    ]


def pack(frames):
    """Shelf-pack the frames (tallest first) with GUTTER px between cells and at the edges. Returns {name: (x, y)} and the
    atlas size."""
    order = sorted(range(len(frames)), key=lambda i: (-frames[i][1].h, -frames[i][1].w, i))
    pos, x, y, shelf = {}, GUTTER, GUTTER, 0
    for i in order:
        a = frames[i][1]
        if x + a.w + GUTTER > ATLAS_W:
            x, y, shelf = GUTTER, y + shelf + GUTTER, 0
        pos[frames[i][0]] = (x, y)
        x += a.w + GUTTER
        shelf = max(shelf, a.h)
    need = y + shelf + GUTTER
    h = 16
    while h < need:
        h *= 2
    assert h <= 512, f"atlas too tall ({need})"
    return pos, (ATLAS_W, h)


def build():
    frames = catalogue()
    pos, size = pack(frames)
    sheet = Image.new("RGBA", size, (0, 0, 0, 0))
    lines = ["# UI frame index (PixelSrc/ui_frames.py). One line per FrameKind, in the enum's order.",
             "# name x y w h bl bb br bt type pl pb pr pt  -- x y from the PNG's top-left; art pixels; borders and paddings"
             " left bottom right top"]
    for name, art, border, kind, pad in frames:
        x, y = pos[name]
        sheet.paste(art.im, (x, y))
        bl, bb, br, bt = border
        assert bl + br < art.w or (bl + br == 0), name
        assert bb + bt < art.h or (bb + bt == 0), name
        lines.append(f"{name} {x} {y} {art.w} {art.h} {bl} {bb} {br} {bt} {kind} {pad[0]} {pad[1]} {pad[2]} {pad[3]}")
    return sheet, frames, pos, "\n".join(lines) + "\n"


# ---------- preview: Unity's sliced / tiled image, rebuilt in PIL ------------------------------------------------------

def nine(art, border, kind, size, fill_center=True):
    """Draw `art` at `size` canvas units the way a UGUI Image of that type does (3 units per art pixel)."""
    W, H = int(round(size[0])), int(round(size[1]))
    src = art.im
    if kind == "simple":
        return src.resize((W, H), Image.NEAREST)
    bl, bb, br, bt = border
    w, h = art.w, art.h
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    L, R, T, B = bl * SCALE, br * SCALE, bt * SCALE, bb * SCALE
    cw, ch = w - bl - br, h - bt - bb  # the source's centre size in art px
    tw, th = W - L - R, H - T - B      # the target's centre size in units

    def piece(x0, y0, x1, y1):
        return src.crop((x0, y0, x1, y1))

    def place(img, box_w, box_h, at, tile_x, tile_y):
        if box_w <= 0 or box_h <= 0 or img.width == 0 or img.height == 0:
            return
        if kind == "tiled" and (tile_x or tile_y):
            big = img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST)
            if not tile_x:
                big = big.resize((box_w, big.height), Image.NEAREST)
            if not tile_y:
                big = big.resize((big.width, box_h), Image.NEAREST)
            tile = Image.new("RGBA", (box_w, box_h), (0, 0, 0, 0))
            for yy in range(0, box_h, big.height):
                for xx in range(0, box_w, big.width):
                    tile.paste(big, (xx, yy))
            out.alpha_composite(tile, at)
        else:
            out.alpha_composite(img.resize((box_w, box_h), Image.NEAREST), at)

    # corners
    place(piece(0, 0, bl, bt), L, T, (0, 0), False, False)
    place(piece(w - br, 0, w, bt), R, T, (W - R, 0), False, False)
    place(piece(0, h - bb, bl, h), L, B, (0, H - B), False, False)
    place(piece(w - br, h - bb, w, h), R, B, (W - R, H - B), False, False)
    # edges
    place(piece(bl, 0, w - br, bt), tw, T, (L, 0), True, False)
    place(piece(bl, h - bb, w - br, h), tw, B, (L, H - B), True, False)
    place(piece(0, bt, bl, h - bb), L, th, (0, T), False, True)
    place(piece(w - br, bt, w, h - bb), R, th, (W - R, T), False, True)
    if fill_center and cw > 0 and ch > 0:
        place(piece(bl, bt, w - br, h - bb), tw, th, (L, T), True, True)
    return out


def tint(img, colour):
    r, g, b, a = rgba(colour)
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            pr, pg, pb, pa = px[x, y]
            px[x, y] = (pr * r // 255, pg * g // 255, pb * b // 255, pa * a // 255)
    return img


def font(family, size):
    path = FONTS / family / f"{family}-Regular.ttf"
    try:
        return ImageFont.truetype(str(path), size)
    except OSError:
        return ImageFont.load_default()


def text(img, xy, s, family, size, colour, anchor="mm", stroke=0, stroke_col="outlineInk", drop=0, engraved=False):
    d = ImageDraw.Draw(img)
    f = font(family, size)
    x, y = xy
    if drop:
        d.text((x, y + drop), s, font=f, fill=rgba(stroke_col), anchor=anchor, stroke_width=stroke, stroke_fill=rgba(stroke_col))
    if engraved:
        d.text((x, y + 1), s, font=f, fill=(255, 255, 255, 128), anchor=anchor)
    d.text((x, y), s, font=f, fill=rgba(colour), anchor=anchor, stroke_width=stroke, stroke_fill=rgba(stroke_col))


NO_CENTRE = {"CardStone", "CardBronze", "CardSilver", "CardGold", "Focus", "AnsataGlow"}
SIZES = {  # two sizes per sliced / tiled frame, in canvas units
    "AnsataBronze": ((240, 66), (360, 90)), "AnsataMarble": ((240, 66), (300, 40)),
    "Marble": ((156, 120), (300, 180)), "Bronze": ((120, 90), (330, 150)), "Wax": ((150, 120), (330, 180)),
    "Mosaic": ((147, 147), (291, 171)), "Stone": ((90, 60), (240, 120)),
    "MeanderGold": ((180, 15), (360, 15)), "MeanderDark": ((180, 15), (360, 15)),
    "CardStone": ((120, 180), (180, 260)), "CardBronze": ((120, 180), (180, 260)),
    "CardSilver": ((120, 180), (180, 260)), "CardGold": ((120, 180), (180, 260)),
    "Halo": ((96, 96), (240, 160)), "Focus": ((60, 60), (210, 120)),
    "Scutum": ((90, 120), (144, 186)), "ScutumLocked": ((90, 120), (144, 186)), "ScutumSpine": ((12, 60), (18, 36)),
    "VexillumBar": ((300, 24), (820, 24)), "VexillumCrimson": ((700, 120), (920, 120)),
    "VexillumPurple": ((700, 120), (920, 120)), "Trough": ((200, 24), (400, 36)),
    "Chip": ((60, 30), (150, 36)), "KeyChip": ((36, 36), (48, 48)), "AnsataGlow": ((252, 78), (312, 92)),
}
TINTS = {"Halo": ("#529EFF", "#FFBD33"), "Focus": ("#FFFFFF", "gold"), "AnsataGlow": ("#FFFFFF", "gold")}


def preview_p3(art, bg):
    """P3's new kinds at their in-game sizes on one tone: the focus glow round both plates at 360x64, 300x80 and
    200x48 (the plate's size + 12, gold), the NEW seal on a Codex tab (36) and beside an entry (24), the pilum arrows
    (48x36, resting and hovered), the HUD row (icons at 32x32 beside their trough and numbers) and the wreath."""
    W = 1920
    P = Image.new("RGBA", (W, 400), rgba(bg))
    ink = "ink" if bg == "night" else "outlineInk"
    text(P, (20, 14), "P3", "MarcellusSC", 22, ink, anchor="la")

    def draw(name, size, at, colour=None):
        _, a, border, kind, _ = art[name]
        img = nine(a, border, kind, size, name not in NO_CENTRE)
        if colour:
            img = tint(img, colour)
        P.alpha_composite(img, (int(at[0]), int(at[1])))

    def plate(style, label, size, at, focused=True):
        draw(style, size, at)
        text(P, (at[0] + size[0] / 2, at[1] + size[1] / 2), label, "MarcellusSC", min(30, int(size[1] * 0.45)),
             "bronzeInk" if style == "AnsataBronze" else "marbleInk", engraved=True)
        if focused:
            draw("AnsataGlow", (size[0] + 12, size[1] + 12), (at[0] - 6, at[1] - 6), "gold")

    # the glow round both plates at the contract's three sizes
    x = 40
    for (w, h) in ((360, 64), (300, 80), (200, 48)):
        plate("AnsataBronze", "PLAY", (w, h), (x, 50))
        plate("AnsataMarble", "CODEX", (w, h), (x, 150))
        x += w + 40
    # a Codex-style tab with the seal on its top-right corner, over the last letter; an entry with the 24 px seal
    plate("AnsataMarble", "ARSENAL", (220, 60), (1040, 50), focused=False)
    draw("SealNew", (36, 36), (1040 + 220 - 64, 50 - 12))
    draw("Marble", (300, 60), (1040, 150))
    text(P, (1064, 180), "THE PIT CUR", "MarcellusSC", 24, "marbleInk", anchor="lm", engraved=True)
    draw("SealNew", (24, 24), (1064 + 172, 168))
    # the pager: resting arrows (darker) round a page count, then hovered (full colour)
    for i, (shade, label) in enumerate((("#D0D0D0", "1 / 3"), (None, "2 / 3"))):
        x0 = 1400 + i * 250
        draw("ArrowLeft", (48, 36), (x0, 62), shade)
        text(P, (x0 + 110, 80), label, "MarcellusSC", 24, ink)
        draw("ArrowRight", (48, 36), (x0 + 172, 62), shade)
    text(P, (1400 + 110, 120), "resting", "Marcellus", 18, ink)
    text(P, (1650 + 110, 120), "hovered", "Marcellus", 18, ink)
    # the HUD plate: icons at 2 units per art px (32x32), each centred on its trough or number
    draw("Bronze", (560, 130), (40, 250))
    draw("IconHeart", (32, 32), (64, 274))
    draw("Trough", (440, 36), (108, 272))
    P.alpha_composite(tint(nine(art["TroughFill"][1], (0, 0, 0, 0), "simple", (428, 24)), "hp"), (114, 278))
    text(P, (328, 290), "100 / 100", "LuckiestGuy", 26, "ink", stroke=2, drop=3)
    draw("IconAureus", (32, 32), (64, 326))
    text(P, (108, 342), "1,250", "LuckiestGuy", 30, "coin", anchor="lm", stroke=2, drop=3)
    draw("IconSkullGalea", (32, 32), (300, 326))
    text(P, (344, 342), "2,417", "LuckiestGuy", 30, "ink", anchor="lm", stroke=2, drop=3)
    # the icons alone at 32 and at 48 (the price tags' and the HUD's sizes), straight on the tone
    for i, name in enumerate(("IconHeart", "IconAureus", "IconSkullGalea")):
        draw(name, (32, 32), (660 + i * 60, 270))
        draw(name, (48, 48), (652 + i * 60, 320))
    # the redrawn wreath: on a scutum's top (a picked tile) and behind a legendary card's icon
    draw("Scutum", (144, 186), (900, 220))
    draw("LaurelWreath", (120, 60), (912, 196))
    ImageDraw.Draw(P).rectangle((1100, 230, 1400, 390), fill=rgba("cardInside"))
    draw("LaurelWreath", (180, 90), (1160, 250))
    ImageDraw.Draw(P).rectangle((1215, 270, 1284, 339), fill=rgba("#6F7B8C"), outline=rgba("#FFBD33"), width=3)
    return P


def preview(frames, path):
    W = 1920
    grids = []
    for bg in ("night", "#857563"):
        # flow layout: each frame at its two sizes (simple ones at native size and 2/3), name underneath
        items = []
        for name, art, border, kind, pad in frames:
            if kind == "simple":
                n = (art.w * SCALE, art.h * SCALE)
                sizes = (n, (n[0] * 2 // 3, n[1] * 2 // 3))
            else:
                sizes = SIZES.get(name, ((art.w * SCALE, art.h * SCALE),) * 2)
            for si, sz in enumerate(sizes):
                img = nine(art, border, kind, sz, name not in NO_CENTRE)
                if name in TINTS:
                    img = tint(img, TINTS[name][si])
                if name == "Trough":  # a fill at 70 %, inset by the padding
                    fill = nine(dict((f[0], f[1]) for f in frames)["TroughFill"], (0, 0, 0, 0), "simple",
                                (int((sz[0] - 2 * pad[0] * SCALE) * 0.7), sz[1] - 2 * pad[1] * SCALE))
                    img.alpha_composite(tint(fill, "hp"), (pad[0] * SCALE, pad[3] * SCALE))
                items.append((name if si == 0 else "", img))
        x, y, row_h, placed = 20, 20, 0, []
        for label, img in items:
            if x + img.width + 20 > W:
                x, y, row_h = 20, y + row_h + 44, 0
            placed.append((label, img, x, y))
            x += img.width + 24
            row_h = max(row_h, img.height)
        H = y + row_h + 60
        g = Image.new("RGBA", (W, H), rgba(bg))
        for label, img, x, y in placed:
            g.alpha_composite(img, (x, y))
            if label:
                text(g, (x, y + img.height + 6), label, "MarcellusSC", 18, "ink" if bg == "night" else "outlineInk", anchor="la")
        grids.append(g)

    # the specials, on the arena tone: ansata buttons at 66 and 78 tall, the banner, the four cards, laurels
    S = Image.new("RGBA", (W, 1800), rgba("#857563"))
    art = dict((f[0], f) for f in frames)

    def draw(name, size, at, colour=None):
        _, a, border, kind, _ = art[name]
        img = nine(a, border, kind, size, name not in NO_CENTRE)
        if colour:
            img = tint(img, colour)
        S.alpha_composite(img, (int(at[0]), int(at[1])))
        return img

    # buttons
    bx = 40
    for (label, style, key, h) in (("FIGHT!", "AnsataBronze", None, 78), ("Reroll (2)", "AnsataBronze", "R", 66),
                                   ("CODEX", "AnsataMarble", None, 66), ("MAIN MENU", "AnsataMarble", None, 78),
                                   ("BUY", "AnsataBronze", None, 40)):
        w = 280 if h != 40 else 200
        draw(style, (w, h), (bx, 40))
        ink = "bronzeInk" if style == "AnsataBronze" else "marbleInk"
        cx = bx + w / 2
        if key:
            draw("KeyChip", (36, 36), (bx + 40, 40 + (h - 36) / 2))
            text(S, (bx + 58, 40 + h / 2 + 2), key, "LuckiestGuy", 22, "ink")
            cx += 20
        text(S, (cx, 40 + h / 2), label, "MarcellusSC", 30 if h > 40 else 24, ink, engraved=True)
        bx += w + 40
    # the banner: crossbar and both cloths
    for i, (name, words, col) in enumerate((("VexillumCrimson", "SWARM INCOMING!", "goldBright"),
                                            ("VexillumPurple", "GORM, THE PIT MASTER", "purpleInk"))):
        x0 = 40 + i * 940
        draw(name, (760, 120), (x0 + 30, 172))
        draw("VexillumBar", (820, 24), (x0, 160))
        text(S, (x0 + 410, 172 + 54), words, "CaesarDressing", 56 if i == 0 else 48, col, stroke=3, drop=3)
    # the four card frames at 300x520 on the card inside, a band, a 100 px dummy icon and texts
    rar = (("CardStone", "COMMON", "#D1D9E6"), ("CardBronze", "UNCOMMON", "#61E673"),
           ("CardSilver", "RARE", "#529EFF"), ("CardGold", "LEGENDARY", "#FFBD33"))
    for i, (name, band, rc) in enumerate(rar):
        x0, y0 = 40 + i * 330, 360
        ImageDraw.Draw(S).rectangle((x0 + 6, y0 + 6, x0 + 294, y0 + 500), fill=rgba("cardInside"))
        ImageDraw.Draw(S).rectangle((x0 + 9, y0 + 9, x0 + 290, y0 + 48), fill=rgba(rc))
        text(S, (x0 + 150, y0 + 30), band, "MarcellusSC", 24, "#17120F")
        halo_img = tint(nine(art["Halo"][1], art["Halo"][2], "sliced", (160, 160)), rc)
        S.alpha_composite(halo_img, (x0 + 70, y0 + 52))
        if name == "CardGold":
            draw("LaurelWreath", (180, 90), (x0 + 60, y0 + 104))
        ImageDraw.Draw(S).rectangle((x0 + 100, y0 + 82, x0 + 199, y0 + 181), fill=rgba("#6F7B8C"), outline=rgba(rc), width=3)
        text(S, (x0 + 150, y0 + 200), "NEW WEAPON!", "MarcellusSC", 21, "gold")
        text(S, (x0 + 150, y0 + 240), "Crimson Greatsword"[:16] if i == 3 else "Javelins", "MarcellusSC", 32, "ink")
        for k in range(8):
            ImageDraw.Draw(S).rectangle((x0 + 70 + k * 20, y0 + 280, x0 + 83 + k * 20, y0 + 293),
                                        fill=rgba(rc if k == 0 else "#3A3F4A"))
        text(S, (x0 + 150, y0 + 330), "A slow, enormous cleave", "Marcellus", 24, "ink")
        text(S, (x0 + 150, y0 + 360), "around you.", "Marcellus", 24, "ink")
        text(S, (x0 + 150, y0 + 470), f"[{i + 1}]", "LuckiestGuy", 26, (239, 230, 214, 128))
        draw(name, (300, 520), (x0, y0))
        if i == 1:
            draw("Focus", (312, 532), (x0 - 6, y0 - 6), "#FFFFFF")
    # laurels: beside VICTORY! (Shout 132) and an unlock line (Title 30), sized as UiKit.Laurels does
    # (NativeSize x fontSize / 48, floor 0.75, `gap` 12 outside the word), and at native size beside a 48-unit word
    def laurels(cx, cy, words, family, size, colour, stroke=0):
        text(S, (cx, cy), words, family, size, colour, stroke=stroke, drop=stroke)
        tw = ImageDraw.Draw(S).textlength(words, font=font(family, size))
        k = max(0.75, size / 48)
        br = art["LaurelBranch"][1]
        left = br.im.resize((int(br.w * SCALE * k), int(br.h * SCALE * k)), Image.NEAREST)
        S.alpha_composite(left, (int(cx - tw / 2 - 12 - left.width), int(cy - left.height / 2)))
        S.alpha_composite(left.transpose(Image.FLIP_LEFT_RIGHT), (int(cx + tw / 2 + 12), int(cy - left.height / 2)))

    laurels(1640, 780, "WON", "MarcellusSC", 48, "gold")
    laurels(960, 1560, "VICTORY!", "CaesarDressing", 132, "goldBright", stroke=4)
    laurels(960, 1720, "NEW GLADIATOR: THE MURDER MACHINE", "MarcellusSC", 30, "gold")
    # the wreath alone at native size on a card inside
    ImageDraw.Draw(S).rectangle((1420, 380, 1860, 640), fill=rgba("cardInside"))
    draw("LaurelWreath", (180, 90), (1450, 420))
    draw("LaurelWreath", (360, 180), (1460, 450 + 0))
    # a bronze HUD plate with a trough and a marble page with a rule, a wax tablet and a mosaic plaque
    draw("Bronze", (440, 112), (40, 920))
    text(S, (70, 950), "HP", "MarcellusSC", 22, "bronzeLight", anchor="lm")
    tr = draw("Trough", (300, 30), (120, 935))
    fill = tint(nine(art["TroughFill"][1], (0, 0, 0, 0), "simple", (int(288 * 0.72), 18)), "hp")
    S.alpha_composite(fill, (126, 941))
    text(S, (270, 950), "86 / 120", "LuckiestGuy", 26, "ink", stroke=2, drop=3)
    text(S, (120, 1000), "1,250", "LuckiestGuy", 32, "coin", anchor="lm", stroke=2, drop=3)
    draw("Marble", (420, 220), (520, 920))
    text(S, (550, 960), "THE PIT CUR", "MarcellusSC", 34, "marbleInk", anchor="lm", engraved=True)
    ImageDraw.Draw(S).rectangle((550, 985, 900, 987), fill=rgba("crimson"))
    text(S, (550, 1020), "Crouches, then lunges at 13 m/s.", "Marcellus", 24, "marbleInk", anchor="lm")
    text(S, (550, 1052), "Jump or slide aside.", "Marcellus", 24, "marbleInk", anchor="lm")
    draw("Wax", (420, 220), (980, 920))
    for r, (k_, v_) in enumerate((("TIME", "8:00"), ("KILLS", "2,417"), ("RENOWN", "+6"))):
        text(S, (1020, 970 + r * 44), k_, "MarcellusSC", 22, "waxInk", anchor="lm")
        text(S, (1360, 970 + r * 44), v_, "LuckiestGuy", 30, "ink", anchor="rm")
    draw("Mosaic", (447, 219), (1440, 920))
    text(S, (1663, 1010), "GLADIATOR", "MarcellusSC", 64, "gold")
    draw("MeanderGold", (360, 15), (1484, 1060))
    # scuta with clipei and spines, a selected one with the wreath
    for i, locked in enumerate((False, False, True)):
        x0, y0 = 60 + i * 180, 1200
        draw("ScutumLocked" if locked else "Scutum", (144, 186), (x0, y0))
        draw("ScutumSpine", (12, 27), (x0 + 66, y0 + 18))
        draw("ScutumSpine", (12, 15), (x0 + 66, y0 + 141))
        ImageDraw.Draw(S).rectangle((x0 + 30, y0 + 51, x0 + 113, y0 + 134), fill=rgba("#A07850" if not locked else "#202020"))
        draw("ClipeusLocked" if locked else "Clipeus", (90, 90), (x0 + 27, y0 + 48))
        text(S, (x0 + 72, y0 + 170), "???" if locked else "ASTERION" if i == 0 else "NOBODY", "MarcellusSC", 20, "ink")
    for i, cname in enumerate(("Chip", "KeyChip")):
        draw(cname, (90, 36), (640 + i * 120, 1240))
    text(S, (685, 1258), "BLOOD", "MarcellusSC", 18, "ink")
    text(S, (805, 1259), "ESC", "LuckiestGuy", 22, "ink")
    draw("Trough", (300, 24), (640, 1300))
    S.alpha_composite(tint(nine(art["TroughFill"][1], (0, 0, 0, 0), "simple", (int(288 * 0.55), 12)), "xp"), (646, 1306))
    draw("Knob", (18, 24), (640 + 6 + int(288 * 0.55) - 9, 1300))
    draw("Stone", (300, 120), (1000, 1240))
    text(S, (1030, 1275), "EDICT OF IRON", "MarcellusSC", 26, "ink", anchor="lm")
    text(S, (1030, 1315), "Enemies +20 % HP", "Marcellus", 20, "dust", anchor="lm")

    p3 = [preview_p3(art, bg) for bg in ("night", "#857563")]
    total = Image.new("RGBA", (W, sum(g.height for g in grids + p3) + S.height), (0, 0, 0, 255))
    y = 0
    for g in grids + [S] + p3:
        total.paste(g, (0, y))
        y += g.height
    path.parent.mkdir(parents=True, exist_ok=True)
    total.convert("RGB").save(path)
    print("preview", path.relative_to(ROOT), f"{total.width}x{total.height}")


def main():
    sheet, frames, pos, index = build()
    OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(OUT_PNG)
    OUT_TXT.write_text(index)
    print(f"{len(frames)} frames -> {OUT_PNG.relative_to(ROOT)} ({sheet.width}x{sheet.height}), {OUT_TXT.relative_to(ROOT)}")
    if "--preview" in sys.argv:
        preview(frames, OUT_PREVIEW)


if __name__ == "__main__":
    main()
