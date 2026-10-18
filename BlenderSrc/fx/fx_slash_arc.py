"""FX_SlashArc: flat 180° ribbon in front of the origin (horizontal), for melee slash VFX (Gladiator/FxSlash).
UVs are NOT palette UVs: u runs along the arc from the asset's RIGHT (Blender -X) to its LEFT (+X), v runs from
the inner edge (0) to the outer edge (1). Outer radius 1 m — the game scales it to the weapon's range."""
import importlib
import math

import bmesh
import gladiator_kit as K

importlib.reload(K)
SEGMENTS = 28
R_IN, R_OUT = 0.32, 1.0

K.clear_scene()
bm = bmesh.new()
uv_layer = bm.loops.layers.uv.new("UVMap")
inner, outer = [], []
for s in range(SEGMENTS + 1):
    t = s / SEGMENTS
    a = t * math.pi
    d = (-math.cos(a), -math.sin(a))  # t=0 right (-X), t=0.5 front (-Y), t=1 left (+X)
    inner.append((bm.verts.new((d[0] * R_IN, d[1] * R_IN, 0.0)), t))
    outer.append((bm.verts.new((d[0] * R_OUT, d[1] * R_OUT, 0.0)), t))
for s in range(SEGMENTS):
    (i0, t0), (i1, t1) = inner[s], inner[s + 1]
    (o0, _), (o1, _) = outer[s], outer[s + 1]
    f = bm.faces.new((i0, i1, o1, o0))
    for loop, uv in zip(f.loops, ((t0, 0.0), (t1, 0.0), (t1, 1.0), (t0, 1.0))):
        loop[uv_layer].uv = uv
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
for f in bm.faces:
    if f.normal.z < 0:
        f.normal_flip()
obj = K.object_from_bmesh("FX_SlashArc", bm)
result = {"fbx": K.export_fbx(obj, "FX/FX_SlashArc"), "tris": K.tri_count([obj]), "blend": K.save_blend("fx_slash_arc")}
