"""Slave Pit kit (M3.1): palette family A1 (mud, rust, browns). Arena 1 is a ~42 m dirt pit dug into the ground:
earthen walls held by planks, a crude palisade, wooden gates the enemies come through, rough scaffold stands for the
locals, raised earth terraces with ramps for vertical play, fire pits (orange-red: they hurt), cages and chain posts.

ARGS: part=structures|terrain|props. Every piece goes to Assets/Art/Models/Environment/. Origins at the base; wall-like
pieces face -Y (their front, toward the pit centre, arrives as Unity +Z). Seeded randomness: re-runs are identical.
"""
import importlib
import math
import random

import bmesh
import bpy
import gladiator_kit as K
from mathutils import Vector

importlib.reload(K)
PART = ARGS.get("part", "structures")  # noqa: F821  (ARGS injected by Tools/blender_bridge.py)
OUT = {}


def jitter(obj, amount, seed, keep_bottom=True):
    rng = random.Random(seed)
    for v in obj.data.vertices:
        if keep_bottom and v.co.z < 0.01:
            continue
        v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * amount
    return obj


def finish(objs):
    bpy.context.view_layer.update()  # bake parts' rotations into joins
    return objs


# ------------------------------------------------------------------------------------------------ structures

def pit_wall(name, width=4.4, height=2.4, depth=1.2):
    """Earth bank held by vertical planks; front (-Y) face at y=0, origin at the base of that face."""
    earth = K.box(name, size=(width, depth, height), pivot=(0, -1, -1), taper=(1.0, 0.75), swatch="mud")
    jitter(earth, 0.05, 11)
    K.set_swatch_where(earth, "mud_dark", lambda c, n: n.y < -0.5)
    K.set_swatch_where(earth, "grass_dry_d", lambda c, n: n.z > 0.8)
    parts = [earth]
    rng = random.Random(3)
    n = 6
    for i in range(n):
        x = -width / 2 + (i + 0.5) * width / n
        h = height * rng.uniform(0.82, 0.97)
        p = K.box("Plank%d" % i, size=(width / n - 0.05, 0.08, h), location=(x, -0.05, 0), pivot=(0, 0, -1),
                  swatch="wood_dark" if i % 2 else "wood")
        p.rotation_euler = (math.radians(rng.uniform(-4, 2)), math.radians(rng.uniform(-2, 2)), 0)
        parts.append(p)
    for x in (-width / 2 + 0.15, width / 2 - 0.15):
        parts.append(K.box("Post", size=(0.22, 0.22, height + 0.25), location=(x, -0.12, 0), pivot=(0, 0, -1),
                           taper=(0.85, 0.85), swatch="wood_dark"))
    parts.append(K.box("Beam", size=(width, 0.14, 0.16), location=(0, -0.13, height * 0.62), pivot=(0, 0, 0),
                       swatch="wood"))
    finish(parts)
    return K.join(parts, name)


def palisade(name, width=4.4):
    """A row of sharpened logs lashed with rope; stands on the wall's top edge (origin at its base centre)."""
    rng = random.Random(5)
    parts = []
    n = 7
    for i in range(n):
        x = -width / 2 + (i + 0.5) * width / n
        h = rng.uniform(1.3, 1.75)
        log = K.cylinder("Log%d" % i, radius=0.16, depth=h, verts=6, location=(x, 0, 0), pivot=(0, 0, -1),
                         swatch="wood" if i % 2 else "wood_light")
        log.rotation_euler = (math.radians(rng.uniform(-5, 5)), math.radians(rng.uniform(-4, 4)), 0)
        tip = K.cone("Tip%d" % i, radius=0.16, depth=0.35, verts=6, location=(x, 0, h), pivot=(0, 0, -1),
                     swatch="wood_light")
        tip.rotation_euler = log.rotation_euler
        parts += [log, tip]
    parts.append(K.cylinder("Lash", radius=0.03, depth=width, verts=4, location=(0, -0.17, 0.85), pivot=(0, 0, 0),
                            axis="X", swatch="rope"))
    finish(parts)
    return K.join(parts, name)


def gate(name, opening=3.2, height=3.3, depth=1.2):
    """Wooden double gate in a heavy frame. DoorL / DoorR are children hinged at the posts (open by yawing)."""
    root = K.empty(name)
    frame = [
        K.box(name + "_Frame", size=(0.42, depth + 0.1, height), location=(-opening / 2 - 0.21, 0, 0),
              pivot=(0, 0, -1), swatch="wood_dark", parent=root),
        K.box("PostR", size=(0.42, depth + 0.1, height), location=(opening / 2 + 0.21, 0, 0), pivot=(0, 0, -1),
              swatch="wood_dark"),
        K.box("Lintel", size=(opening + 1.2, depth + 0.2, 0.42), location=(0, 0, height), pivot=(0, 0, -1),
              swatch="wood"),
        K.box("Skull", size=(0.34, 0.12, 0.3), location=(0, -(depth / 2 + 0.11), height + 0.06), pivot=(0, 0, -1),
              swatch="bone"),
    ]
    for x in (-opening / 2 - 0.21, opening / 2 + 0.21):
        frame.append(K.cone("Spike", radius=0.2, depth=0.5, verts=4, location=(x, 0, height + 0.42), pivot=(0, 0, -1),
                            swatch="wood_light"))
    finish(frame)
    K.join(frame, name + "_Frame")
    leaf_w = opening / 2 - 0.02
    for side, sx in (("L", -1), ("R", 1)):
        hinge = (sx * opening / 2, -depth / 2 + 0.12, 0)
        planks = []
        first = None
        for i in range(4):
            x = hinge[0] - sx * (i + 0.5) * leaf_w / 4
            p = K.box("Door%s%d" % (side, i), size=(leaf_w / 4 - 0.03, 0.1, height - 0.15 - (i % 2) * 0.12),
                      location=(x, hinge[1], 0.05), pivot=(0, 0, -1), swatch="wood" if i % 2 else "wood_dark")
            planks.append(p)
        for z in (0.5, height - 0.8):
            planks.append(K.box("Band", size=(leaf_w, 0.13, 0.16), location=(hinge[0] - sx * leaf_w / 2, hinge[1], z),
                                pivot=(0, 0, -1), swatch="rusty_iron"))
        # the joined door's origin must sit on the hinge: start the join with an empty-ish hinge block
        h = K.box("Door" + side, size=(0.08, 0.12, height - 0.2), location=hinge, pivot=(-sx, 0, -1),
                  swatch="rusty_iron", parent=root)
        finish(planks)
        K.join([h] + planks, "Door" + side)
    return root


def stand(name, width=4.0, depth=2.2, height=1.4):
    """Rough scaffold the locals stand on, behind the wall rim. Deck at `height`, railing at the front (-Y)."""
    deck = K.box(name, size=(width, depth, 0.16), location=(0, 0, height - 0.16), pivot=(0, 0, -1), bevel=0.02,
                 swatch="wood")
    parts = [deck]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(K.box("Leg", size=(0.18, 0.18, height - 0.16), location=(sx * (width / 2 - 0.15),
                               sy * (depth / 2 - 0.15), 0), pivot=(0, 0, -1), swatch="wood_dark"))
    for sx in (-1, 0, 1):
        parts.append(K.box("RailPost", size=(0.12, 0.12, 0.9), location=(sx * (width / 2 - 0.1), -depth / 2 + 0.08, height),
                           pivot=(0, 0, -1), swatch="wood_dark"))
    parts.append(K.box("Rail", size=(width, 0.08, 0.1), location=(0, -depth / 2 + 0.08, height + 0.8), pivot=(0, 0, 0),
                       swatch="wood_light"))
    brace = K.box("Brace", size=(width * 1.05, 0.08, 0.12), location=(0, depth / 2 - 0.15, height * 0.45),
                  pivot=(0, 0, 0), swatch="wood_dark")
    brace.rotation_euler = (0, math.radians(18), 0)
    parts.append(brace)
    parts.append(K.box("Banner", size=(0.7, 0.04, 0.9), location=(width * 0.28, -depth / 2 + 0.03, height - 0.95),
                       pivot=(0, 0, -1), swatch="cloth_red"))
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ terrain

def terrace(name, size=(6.0, 4.5), height=1.3, seed=21):
    """Raised earth plateau with steep sides (jump up, or take a ramp). Irregular octagon top, origin at the base."""
    rng = random.Random(seed)
    bm = bmesh.new()
    n = 8
    top, bot = [], []
    for i in range(n):
        a = (i + 0.5) / n * 2 * math.pi
        rx, ry = size[0] / 2 * rng.uniform(0.9, 1.05), size[1] / 2 * rng.uniform(0.9, 1.05)
        x, y = math.cos(a) * rx, math.sin(a) * ry
        top.append(bm.verts.new((x, y, height)))
        bot.append(bm.verts.new((x * 1.22, y * 1.22, 0.0)))  # ~65 deg sides
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    obj = K.object_from_bmesh(name, bm, swatch="dirt")
    K.set_swatch_where(obj, "dirt_light", lambda c, nn: nn.z > 0.9)
    K.set_swatch_where(obj, "mud_dark", lambda c, nn: nn.z < 0.6 and rng.random() < 0.5)
    return obj


def dirt_ramp(name, width=3.0, length=5.0, height=1.3):
    """Earth ramp: low end at -Y (origin there), rises to `height` at +Y. Walkable (≈15 degrees)."""
    obj = K.wedge(name, size=(width, length, height), pivot=(0, -1, -1), swatch="dirt")
    # wedge's high edge is +Y already; flare the base so it reads as packed earth, not a plank
    for v in obj.data.vertices:
        if v.co.z < 0.01:
            v.co.x *= 1.15
    K.set_swatch_where(obj, "dirt_light", lambda c, nn: nn.z > 0.8)
    return obj


def mound(name, radius, height, seed):
    rng = random.Random(seed)
    obj = K.ico(name, radius=radius, subdiv=2, scale=(1, 1, height / radius), pivot_z=-0.15, swatch="dirt")
    for v in obj.data.vertices:
        if v.co.z < 0:
            v.co.z = 0.0
        else:
            v.co.x += rng.uniform(-0.08, 0.08) * radius
            v.co.y += rng.uniform(-0.08, 0.08) * radius
    K.set_swatch_where(obj, "grass_dry_d", lambda c, n: n.z > 0.85 and rng.random() < 0.3)
    K.set_swatch_where(obj, "mud", lambda c, n: n.z < 0.45)
    return obj


def pit_rim(name, inner=22.3, outer=50.0, height=2.4, segments=40, seed=13):
    """The ground around the pit at wall-top height (stands and locals stand on it). Annulus, origin at the pit centre
    on the pit floor (so it places at 0,0,0); patchy dry grass and dirt, sinking slightly toward the horizon."""
    rng = random.Random(seed)
    radii = (inner, inner + 3.5, inner + 9.0, inner + 17.0, outer)
    bm = bmesh.new()
    rings = []
    for k, r in enumerate(radii):
        ring = []
        for i in range(segments):
            a = i / segments * 2 * math.pi
            rr = r + (rng.uniform(-0.6, 0.6) if 0 < k < len(radii) - 1 else 0.0)
            z = height + (rng.uniform(-0.12, 0.18) if k > 0 else 0.0) - 0.04 * max(0.0, r - inner - 10.0)
            ring.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)))
        rings.append(ring)
    for k in range(len(radii) - 1):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((rings[k][i], rings[k + 1][i], rings[k + 1][j], rings[k][j]))  # counter-clockwise from above: faces up
    # low contrast: it frames the pit, it must never compete with what's in it
    obj = K.object_from_bmesh(name, bm, swatch="dirt_floor")
    K.set_swatch_where(obj, "dirt_floor_d", lambda c, nn: rng.random() < 0.3)
    K.set_swatch_where(obj, "grass_dry_d", lambda c, nn: c.length > inner + 8.0 and rng.random() < 0.3)
    return obj


def mud_patch(name, radius=1.0, seed=9):
    """Flat dark-mud decal (scaled per placement); the slowing zone's visual. 2 cm above the floor."""
    rng = random.Random(seed)
    bm = bmesh.new()
    n = 11
    ring = []
    centre = bm.verts.new((0, 0, 0.025))
    for i in range(n):
        a = i / n * 2 * math.pi
        r = radius * rng.uniform(0.78, 1.08)
        ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, 0.02)))
    for i in range(n):
        bm.faces.new((centre, ring[i], ring[(i + 1) % n]))
    obj = K.object_from_bmesh(name, bm, swatch="mud")  # wet brown, not a black hole
    return obj


# ------------------------------------------------------------------------------------------------ props

def fire_pit(name):
    """Stone ring with burning logs. It hurts: flames stay orange-red (the danger colour)."""
    rng = random.Random(4)
    parts = [K.cylinder(name, radius=0.75, depth=0.05, verts=8, pivot=(0, 0, -1), swatch="charcoal")]
    for i in range(8):
        a = i / 8 * 2 * math.pi
        s = K.ico("Stone%d" % i, radius=0.2, subdiv=1, location=(math.cos(a) * 0.82, math.sin(a) * 0.82, 0),
                  scale=(1.2, 1.0, 0.75), pivot_z=-0.6, swatch="stone_grey" if i % 2 else "slate")
        parts.append(s)
    for i in range(3):
        a = i / 3 * 2 * math.pi + 0.4
        log = K.cylinder("Log%d" % i, radius=0.08, depth=0.9, verts=5, location=(0, 0, 0.12), pivot=(0, 0, 0),
                         axis="X", swatch="wood_dark")
        log.rotation_euler = (0, math.radians(15), a)
        parts.append(log)
    parts.append(K.cone("FlameOuter", radius=0.52, depth=0.75, verts=6, location=(0, 0, 0.1), pivot=(0, 0, -1),
                        swatch="fire_mid"))
    tongue = K.cone("FlameTongue", radius=0.26, depth=0.6, verts=5, location=(0.22, 0.1, 0.25), pivot=(0, 0, -1),
                    swatch="fire_mid")
    tongue.rotation_euler = (math.radians(-8), math.radians(14), 0)
    parts.append(tongue)
    parts.append(K.cone("FlameInner", radius=0.3, depth=0.95, verts=5, location=(-0.04, -0.06, 0.1), pivot=(0, 0, -1),
                        swatch="fire_core"))
    finish(parts)
    return K.join(parts, name)


def cage(name, w=1.6, h=2.0):
    parts = [K.box(name, size=(w, w, 0.12), pivot=(0, 0, -1), swatch="wood_dark")]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(K.box("Corner", size=(0.14, 0.14, h), location=(sx * (w / 2 - 0.07), sy * (w / 2 - 0.07), 0),
                               pivot=(0, 0, -1), swatch="wood_dark"))
    for side in range(4):
        for k in range(3):
            t = (k + 1) / 4 * w - w / 2
            x, y = (t, -w / 2 + 0.06) if side == 0 else (t, w / 2 - 0.06) if side == 1 else \
                (-w / 2 + 0.06, t) if side == 2 else (w / 2 - 0.06, t)
            parts.append(K.box("Bar", size=(0.06, 0.06, h - 0.2), location=(x, y, 0.1), pivot=(0, 0, -1),
                               swatch="rusty_iron"))
    parts.append(K.box("Roof", size=(w + 0.2, w + 0.2, 0.14), location=(0, 0, h), pivot=(0, 0, -1), swatch="wood"))
    parts.append(K.box("Lock", size=(0.16, 0.08, 0.2), location=(0, -w / 2 - 0.02, h * 0.5), pivot=(0, 0, -1),
                       swatch="iron_d"))
    finish(parts)
    return K.join(parts, name)


def chain_post(name):
    parts = [K.cylinder(name, radius=0.14, depth=1.5, verts=6, pivot=(0, 0, -1), taper=0.8, swatch="wood_dark")]
    parts.append(K.cylinder("Ring", radius=0.17, depth=0.08, verts=6, location=(0, 0, 1.1), pivot=(0, 0, 0),
                            swatch="rusty_iron"))
    for i in range(6):  # chain links trailing on the ground
        link = K.box("Link%d" % i, size=(0.16, 0.05, 0.05), location=(0.22 + i * 0.15, -0.05 * i, 0.03 if i else 0.6),
                     pivot=(0, 0, 0), swatch="iron")
        link.rotation_euler = (math.radians(90 * (i % 2)), 0, math.radians(-15))
        parts.append(link)
    parts.append(K.box("Shackle", size=(0.2, 0.2, 0.06), location=(1.15, -0.32, 0.03), pivot=(0, 0, 0), swatch="iron_d"))
    finish(parts)
    return K.join(parts, name)


def spectator(name):
    """Crowd member, ~1.6 m, cheering (arms up). One mesh; the crowd shader tints the 'cloth_white' tunic per instance."""
    parts = [K.box(name, size=(0.42, 0.26, 0.62), location=(0, 0, 0.62), pivot=(0, 0, -1), taper=(1.15, 1.1),
                   swatch="cloth_white")]
    parts.append(K.box("Legs", size=(0.36, 0.22, 0.62), pivot=(0, 0, -1), taper=(1.0, 1.0), swatch="leather_d"))
    parts.append(K.box("Head", size=(0.34, 0.32, 0.36), location=(0, 0, 1.24), pivot=(0, 0, -1), swatch="skin_mid"))
    parts.append(K.box("Hair", size=(0.36, 0.34, 0.1), location=(0, 0.01, 1.53), pivot=(0, 0, -1), swatch="mud_dark"))
    for sx in (-1, 1):
        arm = K.box("Arm", size=(0.11, 0.11, 0.55), location=(sx * 0.24, 0, 1.1), pivot=(0, 0, -1), swatch="skin_mid")
        arm.rotation_euler = (0, math.radians(28 * sx), 0)  # +Y rotation tips the top toward +X: arms spread out
        parts.append(arm)
    finish(parts)
    return K.join(parts, name)


# ------------------------------------------------------------------------------------------------ build

K.clear_scene()
if PART == "structures":
    items = {
        "SM_PitWall_4m": pit_wall("SM_PitWall_4m"),
        "SM_Palisade_4m": palisade("SM_Palisade_4m"),
        "SM_PitGate": gate("SM_PitGate"),
        "SM_Stand_4m": stand("SM_Stand_4m"),
    }
elif PART == "terrain":
    items = {
        "SM_Terrace_A": terrace("SM_Terrace_A"),
        "SM_DirtRamp": dirt_ramp("SM_DirtRamp"),
        "SM_Mound_B": mound("SM_Mound_B", 3.4, 0.85, 31),
        "PR_MudPatch": mud_patch("PR_MudPatch"),
        "SM_PitRim": pit_rim("SM_PitRim"),
    }
else:
    items = {
        "PR_FirePit": fire_pit("PR_FirePit"),
        "PR_Cage": cage("PR_Cage"),
        "PR_ChainPost": chain_post("PR_ChainPost"),
        "CH_Spectator": spectator("CH_Spectator"),
    }
for n, o in items.items():
    OUT[n] = K.export_fbx(o, "Environment/" + n)
    OUT[n + "_tris"] = K.tri_count(K.descendants(o))
OUT["blend"] = K.save_blend("slave_pit_" + PART)
result = OUT
