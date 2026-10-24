"""Blood smears (note 32): the shapes of the Blood Slide trail, drawn here as code so they're regenerable and diffable
like the Blender scripts and the UI art (no third-party art).

BloodTrailRenderer draws one quad per Bleed slide-trail mark, lying on the ground, stretched along the slide (twice the
distance slid since the previous mark, so each smear overlaps half of each neighbour and the marks run together into
one streak) and as wide as the bleeding area. Each quad shows one of the CELLS x CELLS smear shapes of this atlas,
mirrored across the slide or not (shape_of: the same hash as BloodTrailRenderer.ShapeOf). The texture is DATA, not
colour (imported linear, point filtered, no mipmaps); the shader (Assets/Art/Shaders/BloodSmear.shader) turns it into
blood:

    R  thickness: 0 empty, then 4 steps (1/4 thin rim .. 1 pooled spine). Thick blood is darker; thin edges dry first;
       near the end of the mark's life the blood crumbles away thin-first, pixel by pixel, down to the spine.
    G  wet glint: a couple of pixels on the puddles and the big drops, gone once the blood dries.
    B  noise: a random value per pixel, so splashing in, drying and crumbling go pixel by pixel, not in bands.
    A  coverage (255 inside the smear, 0 outside).

Cell layout: x runs across the slide, y along it, and the top of every cell (uv.y = 1) points FORWARD, the way the
player slid. Cell 0 is the bottom-left one (Unity's uv origin), then left to right, bottom to top. Every shape: a band
through the middle ~60 % (a dark spine, two body tones, a thin rim) with ragged edges, drag streaks and a few slits along
the slide, sometimes a puddle, droplets and specks flung beside it. Inside a trail each newer smear covers the older
one's front half, so the band is the same at the back end and the middle row (see smear()): the streak shows no seam or
rhythm at the marks. Its back end is a ragged cut (the tail of a trail), its front end tapers (the head).

    python3 PixelSrc/blood_smears.py              # writes Assets/Art/Textures/FX/BloodSmears.png
    python3 PixelSrc/blood_smears.py --preview    # also renders Screenshots/fx/blood_smears_preview.png

The preview lays a slide trail on the Slave Pit's dirt and on the arena sand at three moments (just made, drying,
crumbling) with the shader's rules and default colours (mirrored below), plus the atlas at 8x. Then
`Gladiator/Setup/Combat/Blood Trail` in Unity imports the texture, makes the material and puts the renderer on the
player prefab.
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
OUT_PNG = ROOT / "Assets/Art/Textures/FX/BloodSmears.png"
OUT_PREVIEW = ROOT / "Screenshots/fx/blood_smears_preview.png"

CELL = 32    # pixels per smear cell (about 6 cm per pixel across a 2 m wide smear, 5 cm along a 1.6 m long one)
CELLS = 2    # cells per side: 4 shapes in a 64 x 64 atlas (BloodSmear.shader's _Cells)

# One entry per cell: band half-width (px) and edge raggedness, side bulges, pools, drag streaks (a lighter line along
# the slide), slits (the sand showing through), droplets, specks, and the share of droplets stretched into spatter.
SHAPES = [
    dict(seed=3201, hw=9.5, ragged=1.4, bulges=1, pools=0, streaks=3, slits=1, drops=3, specks=3, spatter=0.3),  # plain drag
    dict(seed=3202, hw=9.5, ragged=1.2, bulges=1, pools=1, streaks=2, slits=0, drops=2, specks=2, spatter=0.2),  # pooled
    dict(seed=3203, hw=9.5, ragged=1.6, bulges=1, pools=0, streaks=4, slits=2, drops=4, specks=4, spatter=0.5),  # streaky
    dict(seed=3204, hw=9.5, ragged=1.8, bulges=2, pools=1, streaks=2, slits=1, drops=5, specks=3, spatter=0.8),  # splashy
]
HALF = CELL // 2   # inside a trail a newer smear's back end lands on the older one's middle row (spacing = half a cell)


def noise1d(rng, length, step, lo, hi):
    """Smooth 1D value noise over [-step, length + step] (cosine between random knots every `step` px)."""
    knots = [rng.uniform(lo, hi) for _ in range(int(length / step) + 4)]

    def f(x):
        p = (x + step) / step
        i = max(0, min(len(knots) - 2, int(math.floor(p))))
        t = min(1.0, max(0.0, p - i))
        t = (1 - math.cos(t * math.pi)) * 0.5
        return knots[i] * (1 - t) + knots[i + 1] * t
    return f


def profile(x, hw, swell=0.0):
    """The band's cross-section in the 4 thickness steps, `x` px off the centre line of a band `hw` px wide each side:
    a pooled spine and an inner body tone (their widths swell or pinch by `swell` px, which is 0 where neighbouring
    smears meet), then the outer body and a thin rim along the ragged edge. A bulging edge only moves the edge, never
    the spine, and every shape has the same section at the joins, so the streak runs on unseamed."""
    if x >= hw:
        return 0.0
    if hw - x < 1.5:
        return 0.25
    if x < 3.4 + swell:
        return 1.0
    if x < 5.6 + swell * 0.6:
        return 0.75
    return 0.5


def away_from_joins(rng, length):
    """A run of `length` rows that keeps clear of the rows where neighbouring smears meet (0, HALF, CELL)."""
    half = rng.choice((0, HALF))
    lo, hi = half + 2, half + HALF - 2 - length
    start = rng.uniform(lo, max(lo, hi))
    return int(start), int(start + length)


def smear(shape):
    """One cell: thickness (0 = empty), sheen and noise grids indexed [v][u] (v = 0 at the back end).

    In a trail the marks are half a smear apart and the newer smear is drawn on top, so its back half lies over the
    older one's front half: what shows is each smear's back half, from its back end to its middle row. So the band has
    the same centre line and cross-section at the back end and the middle row (any rhythm there would bead the streak
    every 0.8 m), its back end is a ragged cut at full width (a taper or a rim there would draw a seam across it), and
    the variety (wobble, bulges, streaks, slits, pools) lives between those rows. The front end tapers: only the head
    of the trail shows it."""
    rng = random.Random(shape["seed"])
    n = CELL
    th = [[0.0] * n for _ in range(n)]
    sh = [[0] * n for _ in range(n)]
    nz = [[rng.random() for _ in range(n)] for _ in range(n)]

    wobble = noise1d(rng, n, 6, -1.3, 1.3)
    edge_l = noise1d(rng, n, 3, -shape["ragged"], shape["ragged"])
    edge_r = noise1d(rng, n, 3, -shape["ragged"], shape["ragged"])
    v0, v1 = rng.uniform(1.5, 2.5), rng.uniform(28.5, 30.0)
    end_jit = noise1d(rng, n, 2, -1.2, 1.2)   # the ragged back cut wanders across the band

    swelling = noise1d(rng, n, 5, -1.4, 1.4)

    def at_joins(v):
        return math.sin(math.pi * v / HALF) ** 2   # 0 on the rows where smears meet, 1 between them

    def centre(v):
        return (n - 1) / 2 + wobble(v) * at_joins(v)

    bulges = []
    for _ in range(shape["bulges"]):
        a, b = away_from_joins(rng, 6)
        bulges.append((rng.choice((-1, 1)), (a + b) / 2, rng.uniform(1.5, 2.8), rng.uniform(1.2, 2.2)))

    def half_width(v, side):
        hw = shape["hw"] + (edge_l(v) if side < 0 else edge_r(v))
        for s, bv, ext, rad in bulges:
            if s == side:
                hw += ext * max(0.0, 1 - ((v - bv) / (rad * 2)) ** 2)
        return hw

    for v in range(1, n - 1):
        c = centre(v)
        for u in range(1, n - 1):
            side = -1 if u < c else 1
            ev = v + end_jit(u)
            if ev < v0:
                continue
            taper = min(1.0, max(0.0, (v1 - ev) / 5.0)) ** 0.5
            hw = half_width(v, side) * taper
            if hw < 0.8:
                continue
            k = max(taper, 0.2)                        # the front taper narrows the whole section
            th[v][u] = profile(abs(u - c) / k, hw / k, swelling(v) * at_joins(v))

    # pools: the spine widens into a thick, stretched puddle here and there (never at the joins)
    for _ in range(shape["pools"]):
        a, b = away_from_joins(rng, 8)
        pv = (a + b) / 2
        pu = centre(pv) + rng.choice((-1, 1)) * rng.uniform(1.0, 2.5)
        rx, ry = rng.uniform(2.6, 3.4), rng.uniform(3.0, 4.0)
        for v in range(n):
            for u in range(n):
                if th[v][u] > 0 and ((u - pu) / rx) ** 2 + ((v - pv) / ry) ** 2 <= 1.0:
                    th[v][u] = 1.0
        gu, gv = int(round(pu - rx * 0.4)), int(round(pv + ry * 0.3))
        for y in (gv, gv - 1):                         # the wet glint: two pixels up one side of the puddle
            if 0 < gu < n - 1 and 0 < y < n - 1 and th[y][gu] >= 1.0:
                sh[y][gu] = 1

    # drag streaks (a line two steps thinner) and slits (bare sand) along the slide, clear of the joins
    def line(count, slit):
        for _ in range(count):
            a, b = away_from_joins(rng, rng.uniform(4, 10))
            side = rng.choice((-1, 1))
            off = rng.uniform(0.45, 0.8) if slit else rng.uniform(0.1, 0.7)
            for v in range(max(1, a), min(n - 1, b)):
                u = int(round(centre(v) + side * off * half_width(v, side)))
                if 0 < u < n - 1 and th[v][u] > 0:
                    th[v][u] = 0.0 if slit else max(0.25, th[v][u] - 0.5)
                    sh[v][u] = 0

    line(shape["streaks"], slit=False)
    line(shape["slits"], slit=True)

    # droplets beside the band (some stretched along the slide: flung while sliding), then single-pixel specks
    def drop(cu, cv, rx, ry, t, glint):
        for v in range(1, n - 1):
            for u in range(1, n - 1):
                if ((u - cu) / rx) ** 2 + ((v - cv) / ry) ** 2 <= 1.0 and th[v][u] < t:
                    th[v][u] = t
        if glint:
            gu, gv = int(round(cu - rx * 0.4)), int(round(cv + ry * 0.4))
            if 0 < gu < n - 1 and 0 < gv < n - 1 and th[gv][gu] > 0:
                sh[gv][gu] = 1

    placed = tries = 0
    while placed < shape["drops"] and tries < 200:
        tries += 1
        side = rng.choice((-1, 1))
        r = rng.uniform(0.6, 1.9)
        stretch = 1.0 + (rng.uniform(0.3, 1.0) if rng.random() < shape["spatter"] else 0.0)
        cv = rng.uniform(3, n - 4)
        lo, hi = half_width(cv, side) + 1.5 + r, (n - 2) / 2 - r
        if hi <= lo:
            continue
        cu = centre(cv) + side * rng.uniform(lo, hi)
        if not (1 + r <= cu <= n - 2 - r):
            continue
        t = 1.0 if r > 1.5 else (0.75 if r > 1.0 else 0.5)
        drop(cu, cv, max(0.55, r), max(0.55, r * stretch), t, r > 1.3)
        placed += 1
    for _ in range(shape["specks"]):
        side = rng.choice((-1, 1))
        cv = int(rng.uniform(2, n - 3))
        u = int(round(centre(cv) + side * rng.uniform(half_width(cv, side) + 1.0, (n - 3) / 2)))
        if 0 < u < n - 1 and th[cv][u] == 0:
            th[cv][u] = rng.choice((0.25, 0.5))
    return th, sh, nz


def shape_of(serial):
    """Mark `serial` -> (cell, mirrored): BloodTrailRenderer.ShapeOf, the same integer hash (keep them in step)."""
    h = (serial * 0x9E3779B1) & 0xFFFFFFFF
    h ^= h >> 15
    h = (h * 0x2C1B3C6D) & 0xFFFFFFFF
    h ^= h >> 12
    h = (h * 0x297A2D39) & 0xFFFFFFFF
    h ^= h >> 15
    return h % (CELLS * CELLS), (h >> 8) & 1 == 1


def build_atlas():
    size = CELL * CELLS
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    cells = []
    for index, shape in enumerate(SHAPES):
        th, sh, nz = smear(shape)
        cells.append((th, sh, nz))
        cx, cy = index % CELLS, index // CELLS            # cy counted from the bottom (uv origin)
        for v in range(CELL):
            for u in range(CELL):
                if th[v][u] <= 0:
                    continue
                x = cx * CELL + u
                y = (CELLS - 1 - cy) * CELL + (CELL - 1 - v)   # PNG rows run top-down; v runs bottom-up
                px[x, y] = (int(round(th[v][u] * 255)), 255 if sh[v][u] else 0, int(nz[v][u] * 255), 255)
    return img, cells


# ---------- the preview: the shader's rules in Python (keep in step with BloodSmear.shader's defaults) ----------------
FRESH_THIN, FRESH_THICK = (0.55, 0.05, 0.09), (0.33, 0.02, 0.04)
SHEEN = (0.72, 0.18, 0.21)
DRY_THIN, DRY_THICK = (0.33, 0.12, 0.08), (0.17, 0.04, 0.04)
OPACITY, GROW_TIME = 1.0, 0.1
DRY_START, DRY_LENGTH, EDGE_LEAD, DRY_NOISE = 0.15, 0.5, 0.15, 0.15
SHEEN_END, FADE_START = 0.3, 0.6


def lerp3(a, b, t):
    return tuple(a[k] + (b[k] - a[k]) * t for k in range(3))


def shade(t, glint, noise, age, life):
    """The smear pixel's colour at `age` (0..1 of `life` seconds), or None where the shader discards it."""
    keep = t * 0.8 + noise * 0.2
    grow = min(1.0, age * life / GROW_TIME)
    crumble = min(1.0, max(0.0, (age - FADE_START) / (1 - FADE_START)))
    if keep < 1 - grow or keep <= crumble:
        return None
    t01 = min(1.0, max(0.0, (t - 0.25) / 0.75))
    dry = min(1.0, max(0.0, (age - DRY_START + (1 - t01) * EDGE_LEAD + (noise - 0.5) * DRY_NOISE) / DRY_LENGTH))
    dry = math.floor(dry * 3 + 0.5) / 3
    col = lerp3(lerp3(FRESH_THIN, FRESH_THICK, t01), lerp3(DRY_THIN, DRY_THICK, t01), dry)
    if glint and age < SHEEN_END * (0.6 + 0.4 * noise):
        col = SHEEN
    return col


def preview(cells, path):
    import numpy as np
    ppm = 40                                   # preview pixels per metre (a smear texel is ~2.5 px)
    width_m, strip_m = 16.0, 3.6
    floors = [(0x8E, 0x7A, 0x62), (0xD2, 0xAE, 0x78)]     # dirt_floor (Slave Pit), sand (arenas 3+)
    moments = [0.45, 1.3, 2.25]                # seconds after the slide started: just made, drying, crumbling
    life, spacing, radius = 2.5, 0.8, 1.0
    width, length, back = radius * 2.0, spacing * 2.0, 0.25

    # a slide: 11 m/s decaying by 5 m/s^2, steering a little; a mark every 0.8 m (BoonEffects.trailSpacing)
    marks = []
    x, z, heading, speed, t, slid = 1.2, strip_m * 0.3, 0.0, 11.0, 0.0, spacing
    dt = 1 / 120
    while speed > 3.5 and x < width_m - 1.0:
        heading = 0.22 * math.sin(t * 2.4) - 0.05
        dx, dz = math.cos(heading) * speed * dt, math.sin(heading) * speed * dt
        x, z, t = x + dx, z + dz, t + dt
        slid += math.hypot(dx, dz)
        speed -= 5 * dt
        if slid >= spacing:
            slid = 0.0
            marks.append((x, z, math.cos(heading), math.sin(heading), t, len(marks)))

    rows = []
    for floor in floors:
        for moment in moments:
            h, w = int(strip_m * ppm), int(width_m * ppm)
            rng = np.random.default_rng(7)
            base = np.array(floor, dtype=np.float32) / 255
            grain = (rng.random((h // 7 + 1, w // 7 + 1)) - 0.5) * 0.07   # the floor's pixel grain (_PixelNoise)
            grain = np.kron(grain, np.ones((7, 7)))[:h, :w]
            canvas = np.clip(base[None, None, :] * (1 + grain[..., None]), 0, 1)
            ys, xs = np.mgrid[0:h, 0:w]
            wx, wz = (xs + 0.5) / ppm, (h - ys - 0.5) / ppm
            for (mx, mz, fx, fz, born, serial) in marks:
                age = (moment - born) / life
                if age < 0 or age >= 1:
                    continue
                cell, mirror = shape_of(serial)
                th, sh, nz = cells[cell]
                cxm, czm = mx - fx * length * back, mz - fz * length * back
                rx, rz = fz, -fx                      # right of the slide direction
                lu = ((wx - cxm) * rx + (wz - czm) * rz) / width + 0.5
                lv = ((wx - cxm) * fx + (wz - czm) * fz) / length + 0.5
                inside = (lu >= 0) & (lu < 1) & (lv >= 0) & (lv < 1)
                if mirror:
                    lu = 1 - lu
                iu = np.clip((lu * CELL).astype(int), 0, CELL - 1)
                iv = np.clip((lv * CELL).astype(int), 0, CELL - 1)
                for yy, xx in zip(*np.nonzero(inside)):
                    u, v = iu[yy, xx], iv[yy, xx]
                    if th[v][u] <= 0:
                        continue
                    col = shade(th[v][u], sh[v][u], nz[v][u], age, life)
                    if col is None:
                        continue
                    canvas[yy, xx] = canvas[yy, xx] * (1 - OPACITY) + np.array(col) * OPACITY
            rows.append((canvas * 255).astype(np.uint8))
    sheet = np.concatenate([np.pad(r, ((0, 6), (0, 0), (0, 0)), constant_values=30) for r in rows], axis=0)
    out = Image.fromarray(sheet, "RGB")

    # the atlas at 8x: thickness as grey, the glint pixels white, on a dark ground
    atlas = Image.new("RGB", (CELL * CELLS * 8 + 8, out.height), (30, 30, 30))
    size = CELL * CELLS
    for index, (th, sh, nz) in enumerate(cells):
        cx, cy = index % CELLS, index // CELLS
        for v in range(CELL):
            for u in range(CELL):
                if th[v][u] <= 0:
                    continue
                col = (255, 255, 255) if sh[v][u] else tuple(int(c * 255) for c in shade(th[v][u], 0, 0.5, 0.12, life))
                x = (cx * CELL + u) * 8
                y = ((CELLS - 1 - cy) * CELL + (CELL - 1 - v)) * 8
                for k in range(8):
                    for j in range(8):
                        atlas.putpixel((x + j, y + k), col)
    sheet_img = Image.new("RGB", (out.width + atlas.width, max(out.height, size * 8)), (30, 30, 30))
    sheet_img.paste(out, (0, 0))
    sheet_img.paste(atlas, (out.width + 8, 0))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet_img.save(path)
    return path


def main():
    img, cells = build_atlas()
    OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT_PNG)
    print(f"wrote {OUT_PNG.relative_to(ROOT)} ({img.width}x{img.height}, {len(SHAPES)} smears)")
    if "--preview" in sys.argv:
        print(f"wrote {preview(cells, OUT_PREVIEW).relative_to(ROOT)}")


if __name__ == "__main__":
    main()
