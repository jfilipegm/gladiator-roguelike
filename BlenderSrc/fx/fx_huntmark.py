"""Note 37: FX_HuntMark, the Hunter's Mark sigil HuntersMark draws over every enemy the Beastmaster's Hunting Spear
has marked (instanced, the shared palette material, turned to face the camera).
A wolf's paw print standing upright: a broad pad and four toes in glowing player cyan, white claw points, on an ink
rim (a slightly bigger dark copy behind) so it reads against sand, sky and crowd alike. Player colours only: an
enemy marked for the pack must never look like a threat (orange-red). Front faces Blender -Y (Unity +Z), origin at
the bottom centre (it sits that far above the enemy's head). About 0.42 m wide, 0.4 m tall.
    Tools/bheadless BlenderSrc/fx/fx_huntmark.py [review=1]
"""
import importlib
import math

import gladiator_kit as K

importlib.reload(K)
OUT = {}

FRONT, RIM = 0.05, 0.03  # thickness of the cyan pieces and of the ink rim behind them
RIM_GROW = 0.028         # the rim's outline width (m)


def ellipse(cx, cz, rx, rz, tilt=0.0, n=8):
    t = math.radians(tilt)
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n + math.pi / n
        x, z = rx * math.cos(a), rz * math.sin(a)
        pts.append((cx + x * math.cos(t) - z * math.sin(t), cz + x * math.sin(t) + z * math.cos(t)))
    return pts


def grow(pts, d):
    """The outline pushed out by d from its centroid (good enough for these convex blobs)."""
    cx = sum(p[0] for p in pts) / len(pts)
    cz = sum(p[1] for p in pts) / len(pts)
    out = []
    for x, z in pts:
        vx, vz = x - cx, z - cz
        m = math.hypot(vx, vz) or 1.0
        out.append((x + vx / m * d, z + vz / m * d))
    return out


def piece(name, pts, swatch):
    """A cyan piece standing in the XZ plane (front toward -Y) and its ink rim just behind it."""
    front = K.extrude_poly(name, pts, FRONT, plane="XZ", location=(0, -FRONT / 2, 0), swatch=swatch)
    rim = K.extrude_poly(name + "_Rim", grow(pts, RIM_GROW), RIM, plane="XZ", location=(0, RIM / 2 + 0.002, 0),
                         swatch="ink")
    return [front, rim]


def huntmark(name):
    parts = []
    # the pad: a broad rounded triangle, three soft lobes along the bottom
    pad = [(-0.1, 0.035), (-0.05, 0.012), (0.0, 0.03), (0.05, 0.012), (0.1, 0.035), (0.135, 0.09), (0.11, 0.15),
           (0.05, 0.19), (-0.05, 0.19), (-0.11, 0.15), (-0.135, 0.09)]
    parts += piece(name, pad, "glow_cyan")
    # four toes in an arc over it, the outer two lower and leaning out
    toes = [(-0.155, 0.235, -24.0), (-0.058, 0.29, -6.0), (0.058, 0.29, 6.0), (0.155, 0.235, 24.0)]
    for i, (x, z, tilt) in enumerate(toes):
        parts += piece(name + "_Toe%d" % i, ellipse(x, z, 0.042, 0.058, tilt), "glow_cyan")
        # a claw point beyond each toe, pointing the way the toe leans
        t = math.radians(tilt)
        ux, uz = -math.sin(t), math.cos(t)          # the toe's "up"
        bx, bz = x + ux * 0.05, z + uz * 0.05       # claw base, at the toe's tip
        px, pz = -uz, ux                            # across the toe
        claw = [(bx + px * 0.018, bz + pz * 0.018), (bx + ux * 0.06, bz + uz * 0.06), (bx - px * 0.018, bz - pz * 0.018)]
        parts.append(K.extrude_poly(name + "_Claw%d" % i, claw, FRONT * 0.8, plane="XZ", location=(0, -FRONT / 2, 0),
                                    swatch="glow_white"))
    return K.join(parts, name)


K.clear_scene()
o = huntmark("FX_HuntMark")
if "ARGS" in globals() and ARGS.get("review") == "1":
    OUT["review"] = K.review_render(o, "fx_huntmark", size=300)
OUT["fbx"] = K.export_fbx(o, "FX/FX_HuntMark")
OUT["tris"] = K.tri_count(K.descendants(o))
lo, hi = K.world_bounds([o])
OUT["bounds"] = [tuple(round(x, 3) for x in lo), tuple(round(x, 3) for x in hi)]
OUT["blend"] = K.save_blend("fx_huntmark")
result = OUT
