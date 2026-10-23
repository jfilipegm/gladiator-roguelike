"""stylized_kit — A1 art-direction helpers on top of gladiator_kit (docs/tracks/A1-art-direction.md).

The first-generation characters are built from boxes (gladiator_kit.box), so every part reads as a separate brick.
These helpers make the same rigid parts from shaped forms instead, at a similar triangle cost:
  * loft()    — elliptical rings stacked along Z and joined by quads: tapered limbs, a V-shaped torso, a rounded head.
  * shell()   — a curved plate (a slice of an elliptical tube) with thickness: pauldrons, greaves, armour lames.
  * strips()  — a ring of hanging cloth or leather strips with gaps and uneven hems: tunic skirts, pteruges.
  * band()    — a thin ring hugging a loft: belts, wraps, straps, headbands.
  * soften()  — smooth shading with hard edges above an angle, so curved forms shade softly and creases stay crisp.
  * merge_part() — joins every mesh under a rigid part into the part itself (one renderer per part in Unity),
                   leaving other parts and kept props (the held weapon, the shield) as separate children.
Conventions are gladiator_kit's: 1 unit = 1 m, Z up, the asset's front faces -Y, its left is +X, palette swatches.
gladiator_kit stays untouched: nothing here changes how the existing assets build.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import gladiator_kit as K


def _ring(bm, z, rx, ry, cx, cy, segments, phase):
    out = []
    for k in range(segments):
        a = phase + 2.0 * math.pi * k / segments
        out.append(bm.verts.new((cx + rx * math.cos(a), cy + ry * math.sin(a), z)))
    return out


def loft(name, rings, segments=8, location=(0, 0, 0), swatch="stone_grey", parent=None, cap_bottom=True,
         cap_top=True, smooth_angle=55.0, phase=None):
    """Rings (z, rx, ry[, cx, cy]) from bottom to top, joined by quads; the origin is the rings' (0, 0, 0), so put
    the joint there. phase: the first vertex's angle (default: half a segment, so a flat face looks forward)."""
    if phase is None:
        phase = math.pi / segments + math.pi / 2.0
    bm = bmesh.new()
    loops = []
    for r in rings:
        z, rx, ry = r[0], r[1], r[2]
        cx = r[3] if len(r) > 3 else 0.0
        cy = r[4] if len(r) > 4 else 0.0
        loops.append(_ring(bm, z, rx, ry, cx, cy, segments, phase))
    for a, b in zip(loops, loops[1:]):
        for k in range(segments):
            k2 = (k + 1) % segments
            bm.faces.new((a[k], a[k2], b[k2], b[k]))
    if cap_bottom:
        bm.faces.new(list(reversed(loops[0])))
    if cap_top:
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = K.object_from_bmesh(name, bm, location, parent, swatch)
    if smooth_angle:
        soften(obj, smooth_angle)
    return obj


def shell(name, radius=(0.2, 0.2), arc=(0.0, 180.0), z=(0.0, 0.2), taper=1.0, thickness=0.02, segments=5,
          location=(0, 0, 0), rotation=(0, 0, 0), swatch="rusty_iron", parent=None, smooth_angle=40.0,
          flare=0.0):
    """A curved plate: the slice arc[0]..arc[1] (degrees; 0 = +X, 90 = +Y, 270 = -Y = the front) of an elliptical
    tube of radius (rx, ry) from z[0] to z[1], its top `taper`-times the bottom's size, `flare` pushing the bottom
    edge outward (metres), with `thickness`."""
    bm = bmesh.new()
    rows = []
    for zi, zz in enumerate(z):
        s = 1.0 if zi == 0 else taper
        out_extra = flare if zi == 0 else 0.0
        row_o, row_i = [], []
        for k in range(segments + 1):
            a = math.radians(arc[0] + (arc[1] - arc[0]) * k / segments)
            ca, sa = math.cos(a), math.sin(a)
            rx, ry = radius[0] * s + out_extra, radius[1] * s + out_extra
            row_o.append(bm.verts.new((rx * ca, ry * sa, zz)))
            row_i.append(bm.verts.new(((rx - thickness) * ca, (ry - thickness) * sa, zz)))
        rows.append((row_o, row_i))
    (bo, bi), (to, ti) = rows
    for k in range(segments):
        bm.faces.new((bo[k], bo[k + 1], to[k + 1], to[k]))      # outside
        bm.faces.new((bi[k + 1], bi[k], ti[k], ti[k + 1]))      # inside
        bm.faces.new((bi[k], bi[k + 1], bo[k + 1], bo[k]))      # bottom edge
        bm.faces.new((to[k], to[k + 1], ti[k + 1], ti[k]))      # top edge
    bm.faces.new((bo[0], to[0], ti[0], bi[0]))                  # the two ends
    bm.faces.new((bi[-1], ti[-1], to[-1], bo[-1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = K.object_from_bmesh(name, bm, location, parent, swatch)
    obj.rotation_euler = rotation
    if smooth_angle:
        soften(obj, smooth_angle)
    return obj


def strips(name, count=10, radius_top=(0.18, 0.13), radius_bottom=(0.22, 0.16), z_top=0.0, length=0.2,
           gap=0.25, thickness=0.012, jitter=0.0, seed=1, location=(0, 0, 0), swatch="cloth_white",
           alt_swatch=None, parent=None, skip=(), inner=False, sides=True):
    """A ring of hanging strips (a skirt of pteruges or a torn tunic hem): `count` strips round the ellipse, each
    leaving `gap` (a fraction of its slot) empty, flaring from radius_top at z_top down to radius_bottom.
    jitter (metres): uneven hems, deterministic per seed. alt_swatch: every other strip. skip: slot indices left out."""
    import random
    rnd = random.Random(seed)
    bm = bmesh.new()
    faces_alt = []
    for k in range(count):
        if k in skip:
            continue
        a0 = 2 * math.pi * (k + gap * 0.5) / count + math.pi / 2
        a1 = 2 * math.pi * (k + 1 - gap * 0.5) / count + math.pi / 2
        z_bot = z_top - length - (rnd.uniform(-jitter, jitter) if jitter else 0.0)
        quad = []
        for (rx, ry), zz in ((radius_top, z_top), (radius_bottom, z_bot)):
            for a in (a0, a1):
                quad.append(Vector((rx * math.cos(a), ry * math.sin(a), zz)))
        # quad: top0, top1, bot0, bot1 -> a thin slab with `thickness` inward
        outer = [quad[0], quad[1], quad[3], quad[2]]
        inner_pts = []
        for v in outer:
            d = Vector((v.x, v.y, 0.0))
            d = d.normalized() if d.length > 1e-6 else Vector((0, 0, 0))
            inner_pts.append(v - d * thickness)
        vo = [bm.verts.new(v) for v in outer]
        vi = [bm.verts.new(v) for v in inner_pts]
        new = [bm.faces.new((vo[0], vo[3], vo[2], vo[1]))]
        if inner:  # the inside face only shows from under the skirt: off by default (8 tris a strip, not 12)
            new.append(bm.faces.new((vi[0], vi[1], vi[2], vi[3])))
        for i in ((1, 2, 3) if sides else (2,)):  # the two sides and the hem; the top is hidden under the belt
            j = (i + 1) % 4
            new.append(bm.faces.new((vo[i], vo[j], vi[j], vi[i])))
        if alt_swatch and k % 2 == 1:
            faces_alt.extend(new)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.faces.index_update()
    alt_ids = {f.index for f in faces_alt}
    obj = K.object_from_bmesh(name, bm, location, parent, swatch)
    if alt_swatch and alt_ids:
        K.set_swatch(obj, alt_swatch, faces=alt_ids)
    return obj


def band(name, z0, z1, rx, ry, segments=8, location=(0, 0, 0), swatch="leather_d", parent=None, cx=0.0, cy=0.0,
         taper=1.0, smooth_angle=55.0):
    """A ring band from z0 to z1 (open ends are capped with nothing: a band hugs a form)."""
    return loft(name, [(z0, rx, ry, cx, cy), (z1, rx * taper, ry * taper, cx, cy)], segments=segments,
                location=location, swatch=swatch, parent=parent, cap_bottom=False, cap_top=False,
                smooth_angle=smooth_angle)


def soften(obj, angle_deg=55.0):
    """Smooth shading, hard edges where faces meet at more than angle_deg (the creases stay crisp)."""
    me = obj.data
    for p in me.polygons:
        p.use_smooth = True
    try:
        me.set_sharp_from_angle(angle=math.radians(angle_deg))
    except AttributeError:  # older Blender: auto smooth
        me.use_auto_smooth = True
        me.auto_smooth_angle = math.radians(angle_deg)
    return obj


def rounded_box(name, size=(0.1, 0.1, 0.1), bevel=0.03, segments=1, smooth_angle=50.0, **kw):
    """gladiator_kit.box with chamfered edges and soft shading (44 tris; 2 segments cost ~100): hands, feet."""
    obj = K.box(name, size=size, bevel=bevel, segments=segments, **kw)
    if smooth_angle:
        soften(obj, smooth_angle)
    return obj


def _mesh_descendants(obj, stop):
    out = []
    for c in obj.children:
        if c.name.split(".")[0] in stop:
            continue
        if c.type == "MESH":
            out.append(c)
        out.extend(_mesh_descendants(c, stop))
    return out


def merge_part(part, stop=("Head", "ArmL", "ArmR", "LegL", "LegR", "Body", "Gladius", "Shield")):
    """Joins every mesh under `part` (not crossing into other parts or kept props named in `stop`) into `part`, so
    Unity gets one renderer per rigid part. The part keeps its name, origin (the joint) and place in the hierarchy;
    children in `stop` stay children."""
    bpy.context.view_layer.update()
    meshes = _mesh_descendants(part, stop)
    if not meshes:
        return part
    keep_children = [c for c in part.children if c.name.split(".")[0] in stop]
    saved = [(c, c.matrix_world.copy()) for c in keep_children]
    # empties between the part and its meshes (none in A1 builds, but be safe): reparent meshes to the part first
    for m in meshes:
        mw = m.matrix_world.copy()
        m.parent = part
        m.matrix_world = mw
    name = part.name
    joined = K.join([part] + meshes, name)
    bpy.context.view_layer.update()
    for c, mw in saved:
        c.parent = joined
        c.matrix_world = mw
    return joined


def tris(root):
    return K.tri_count([o for o in K.descendants(root) if o.type == "MESH"])


def renderer_count(root):
    return sum(1 for o in K.descendants(root) if o.type == "MESH")
