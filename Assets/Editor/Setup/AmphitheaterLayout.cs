using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the layout of Arena 3, the Stone Amphitheater (docs/ARENAS.md §3): a D, 120 m wide and 88 m deep
    /// (≈ 8,500 m² of play, ~1.35× the Provincial Pit). A half-ellipse seating bowl (south; decor, kept solid by
    /// invisible stone) with three tunnels faces a straight stage wall (north) holding the great gate (the boss's) and
    /// four beast cages. Two halves of a raised stage either side of the great gate, twelve portico columns round the
    /// middle (line-of-sight blockers), four statues, four platforms, ~1,700 spectators. The play area is the wall line
    /// inset by PlayInset (ArenaLayoutDef.outline). Designer-owned once it exists: only the "Regenerate" menu item
    /// overwrites it. ArenaBuilder turns it into the scene. Source art: BlenderSrc/environment/amphitheater.py, whose
    /// D numbers (AX, ZN, ZC, BZ, TUNNELS, TUNNEL_HALF and the seating profile) these must match.
    /// </summary>
    public static class AmphitheaterLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_StoneAmphitheater.asset";
        const string Env = "Environment/";
        // the D's wall line: half width, the stage wall line, where the curve starts, the curve's depth (apex z = ZC - BZ)
        public const float AX = 60f, ZN = 44f, ZC = 20f, BZ = 64f;
        static readonly float[] Tunnels = { Mathf.PI / 2f - 0.75f, Mathf.PI / 2f, Mathf.PI / 2f + 0.75f };
        const float TunnelHalf = 2.5f;
        // the seating bowl's profile: the podium, then eight tiers (rise, run) from the podium cap
        const float Podium = 3f, PodiumCap = 0.6f, TierRise = 0.45f, TierRun = 0.8f;
        const int Tiers = 8;
        const float StageWidth = 4.6f;   // the stage section, cage and great gate models' width
        const float PlayInset = 1.1f;    // enemies stay this far inside the wall face
        const int CurveSteps = 48;
        static readonly float[] CageX = { -44.44f, -26.67f, 26.67f, 44.44f };

        [MenuItem("Gladiator/Arenas/Regenerate Stone Amphitheater Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        /// <summary>The wall line's curve at parameter t (0 = east end, π/2 = the south apex, π = west end), `inset`
        /// metres inside it: (point, inward normal).</summary>
        public static (Vector2 p, Vector2 inward) Curve(float t, float inset = 0f)
        {
            var n = new Vector2(-Mathf.Cos(t) / AX, Mathf.Sin(t) / BZ).normalized;
            return (new Vector2((AX - inset) * Mathf.Cos(t), ZC - (BZ - inset) * Mathf.Sin(t)), n);
        }

        static float CurveSpeed(float t) => Mathf.Sqrt(AX * AX * Mathf.Sin(t) * Mathf.Sin(t) + BZ * BZ * Mathf.Cos(t) * Mathf.Cos(t));

        /// <summary>The play area: the D inset by PlayInset, clockwise from above (north-west corner first).</summary>
        public static Vector2[] Outline()
        {
            var o = new List<Vector2> { new Vector2(-AX + PlayInset, ZN - PlayInset), new Vector2(AX - PlayInset, ZN - PlayInset) };
            for (int i = 0; i <= CurveSteps; i++) o.Add(Curve(Mathf.PI * i / CurveSteps, PlayInset).p);
            return o.ToArray();
        }

        /// <summary>The seating bowl's foot as runs of (point, inward normal), split at the tunnels like
        /// amphitheater.py's cavea_path: east side (stage line down to the curve), the curve, west side.</summary>
        static List<List<(Vector2 p, Vector2 n)>> BowlRuns()
        {
            var runs = new List<List<(Vector2, Vector2)>>();
            var cur = new List<(Vector2, Vector2)>();
            for (int i = 0; i <= 6; i++) cur.Add((new Vector2(AX, ZN - (ZN - ZC) * i / 6f), Vector2.left));
            var ts = new List<float>();
            for (int i = 1; i < CurveSteps; i++) ts.Add(Mathf.PI * i / CurveSteps);
            var gaps = new List<Vector2>();
            foreach (float t in Tunnels) { float h = TunnelHalf / CurveSpeed(t); gaps.Add(new Vector2(t - h, t + h)); ts.Add(t - h); ts.Add(t + h); }
            ts.Sort();
            foreach (float t in ts)
            {
                bool inside = false, gapStart = false, gapEnd = false;
                foreach (var g in gaps) { inside |= t > g.x + 1e-5f && t < g.y - 1e-5f; gapStart |= Mathf.Abs(t - g.x) < 1e-5f; gapEnd |= Mathf.Abs(t - g.y) < 1e-5f; }
                if (inside) continue;
                if (gapEnd) cur = new List<(Vector2, Vector2)>();
                cur.Add(Curve(t));
                if (gapStart) runs.Add(cur);
            }
            for (int i = 0; i <= 6; i++) cur.Add((new Vector2(-AX, ZC + (ZN - ZC) * i / 6f), Vector2.right));
            runs.Add(cur);
            return runs;
        }

        static Vector3 V3(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);
        /// <summary>Yaw that turns a model's +Z toward `dir` (XZ).</summary>
        static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            l.id = "stone_amphitheater";
            l.displayName = "The Stone Amphitheater";
            l.tier = 2;
            l.center = Vector3.zero;
            l.outline = Outline();
            l.radius = ZN - PlayInset; // the biggest circle round the centre (the debug spawn ring)
            l.aspect = 1f;
            l.fog = new Vector2(60f, 280f);    // the far end is ~120 m away
            l.playerSpawn = new Vector3(0f, 0.1f, -12f);
            l.playerYaw = 0f;                  // facing the stage wall and the great gate
            l.maxAlive = 320;
            l.spawnMaxDistance = 55f;
            l.eliteEvery = 60f;                // "elites, mini-bosses": more of them here
            l.sunEuler = new Vector3(54f, -25f, 0f);  // high: lights the stage face and the seat tops
            l.enemyHpMul = 1.5f;
            l.playerDamageTakenMul = 1.25f;
            l.gateModel = Env + "SM_AmphiTunnel";
            l.secrets = ArenaSecrets.None;
            l.secretSpots = new Vector3[0];

            var pieces = new List<ArenaLayoutDef.Piece>();
            var gates = new List<ArenaLayoutDef.Gate>();
            var crowd = new List<ArenaLayoutDef.CrowdZone>();
            var stone = new List<Vector4>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });

            Add("SM_AmphiFloor", PieceRole.Walkable, Vector3.zero, 0f);
            Add("SM_AmphiCavea", PieceRole.Decor, Vector3.zero, 0f);
            Add("SM_AmphiRim", PieceRole.Decor, Vector3.zero, 0f);

            // ---------- the stage wall (north, facing south): the great gate mid-wall (gate 0, the boss's), four beast
            // cages, and stage sections filling the runs between them (scaled to fit exactly: no z-fighting)
            gates.Add(new ArenaLayoutDef.Gate { position = new Vector3(0f, 0f, ZN), yaw = 180f, model = Env + "SM_GreatGate" });
            var cages = new List<Vector4>();
            foreach (float x in CageX) cages.Add(new Vector4(x, 0f, ZN, 180f));
            l.cages = cages.ToArray();
            var specials = new List<float> { 0f };
            specials.AddRange(CageX);
            specials.Sort();
            float from = -AX;
            for (int k = 0; k <= specials.Count; k++)
            {
                float to = k < specials.Count ? specials[k] - StageWidth * 0.5f : AX;
                float len = to - from;
                int m = Mathf.Max(1, Mathf.RoundToInt(len / StageWidth));
                float w = len / m;
                for (int i = 0; i < m; i++)
                    Add("SM_AmphiStage_4m", PieceRole.Solid, new Vector3(from + w * (i + 0.5f), 0f, ZN), 180f, new Vector3(w / StageWidth, 1f, 1f));
                if (k < specials.Count) from = specials[k] + StageWidth * 0.5f;
            }
            // the stage (pulpitum) in two halves, x 3..20 either side, leaving the great gate a 6 m processional way;
            // stairs up its front and its outer ends (their origin is the low edge, they rise toward their local -Z)
            foreach (float s in new[] { -1f, 1f })
            {
                Add("SM_Pulpitum", PieceRole.Walkable, new Vector3(11.5f * s, 0f, 41.25f), 180f);
                Add("SM_Stairs", PieceRole.Walkable, new Vector3(11.5f * s, 0f, 36.5f), 180f);
                Add("SM_Stairs", PieceRole.Walkable, new Vector3(22f * s, 0f, 41.25f), s > 0f ? 90f : 270f);
            }

            // ---------- the seating bowl: tunnels (gates 1-3) at its three gaps, invisible stone along its foot, and
            // eight tiers of spectators along it (over the tunnels too)
            foreach (float t in Tunnels)
            {
                var (p, n) = Curve(t);
                gates.Add(new ArenaLayoutDef.Gate { position = V3(p), yaw = YawOf(n) });
            }
            void Seats(Vector2 a, Vector2 b, Vector2 na, Vector2 nb, ref int seed)
            {
                for (int k = 0; k < Tiers; k++)
                {
                    float off = PodiumCap + TierRun * (k + 0.5f);
                    Vector2 pa = a - na * off, pb = b - nb * off;
                    float len = (pb - pa).magnitude;
                    var inward = ((na + nb) * 0.5f).normalized;
                    crowd.Add(new ArenaLayoutDef.CrowdZone
                    {
                        center = V3((pa + pb) * 0.5f, Podium + TierRise * (k + 1)),
                        size = new Vector2(len * 0.9f, 0.4f), yaw = YawOf(inward),
                        count = Mathf.Max(1, Mathf.RoundToInt(len * 0.85f + ((seed++ % 3) - 1) * 0.4f)),
                    });
                }
            }
            int crowdSeed = 0;
            foreach (var run in BowlRuns())
                for (int i = 0; i + 1 < run.Count; i++)
                {
                    var (a, na) = run[i];
                    var (b, nb) = run[i + 1];
                    var inward = ((na + nb) * 0.5f).normalized;
                    stone.Add(new Vector4((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, YawOf(inward), (b - a).magnitude + 0.3f));
                    Seats(a, b, na, nb, ref crowdSeed);
                }
            foreach (float t in Tunnels) // the tunnel pieces carry the seats over their passage
            {
                var (p, n) = Curve(t);
                var along = new Vector2(n.y, -n.x) * TunnelHalf;
                Seats(p - along, p + along, n, n, ref crowdSeed);
            }
            l.solidWalls = stone.ToArray();
            l.solidWallHeight = 8.4f;
            // the stage wall is 7.5 m: an invisible wall behind it keeps air jumps off its top (player only)
            l.boundary = new[] { new Vector4(0f, ZN + 1.7f, 180f, 2f * AX + 4f) };

            // ---------- line-of-sight blockers: a portico ring of twelve columns round the middle, four statues
            for (int i = 0; i < 12; i++)
            {
                float a = (15f + 30f * i) * Mathf.Deg2Rad;
                Add("SM_AmphiColumn", PieceRole.Solid, new Vector3(30f * Mathf.Cos(a), 0f, 22f * Mathf.Sin(a)), 0f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Add("PR_Statue", PieceRole.Solid, new Vector3(35.5f * s, 0f, 33f), 180f);        // between the cages
                Add("PR_Statue", PieceRole.Solid, new Vector3(42f * s, 0f, -16f), YawOf(new Vector2(-42f * s, 16f)));
            }

            // ---------- vertical variety: stone platforms facing the floor (stairs on their +Z side, or the ramp)
            void Platform(Vector2 pos, float yaw, string climb)
            {
                Add("SM_ProvPlatform", PieceRole.Walkable, V3(pos), yaw);
                var f = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                Add(climb, PieceRole.Walkable, V3(pos + f * (climb == "SM_Stairs" ? 4f : 6f)), yaw);
            }
            Platform(new Vector2(-42f, 4f), 90f, "SM_Stairs");
            Platform(new Vector2(42f, 4f), 270f, "SM_Stairs");
            Platform(new Vector2(-24f, -30f), YawOf(new Vector2(24f, 30f)), "SM_Ramp");
            Platform(new Vector2(24f, -30f), YawOf(new Vector2(-24f, 30f)), "SM_Ramp");

            // weapon racks either side of the south tunnel
            Add("PR_WeaponRack", PieceRole.Prop, new Vector3(-5.5f, 0f, -40.4f), 0f);
            Add("PR_WeaponRack", PieceRole.Prop, new Vector3(5.5f, 0f, -40.4f), 0f);

            l.pieces = pieces.ToArray();
            l.gates = gates.ToArray();
            l.crowd = crowd.ToArray();
            l.zones = new ArenaLayoutDef.Zone[0];
            l.barricades = new Vector4[0];
            l.spikeTraps = new Vector3[0];
            // braziers on the stage, either side of each half
            l.braziers = new[] { new Vector3(-17f, 1.2f, 42.5f), new Vector3(-6f, 1.2f, 42.5f), new Vector3(6f, 1.2f, 42.5f), new Vector3(17f, 1.2f, 42.5f) };

            l.chestAnchors = new[]
            {
                new Vector3(0f, 0f, -18f), new Vector3(0f, 0f, 12f), new Vector3(-14f, 0f, 0f), new Vector3(14f, 0f, 0f),
                new Vector3(-36f, 0f, -20f), new Vector3(36f, 0f, -20f), new Vector3(-50f, 0f, 12f), new Vector3(50f, 0f, 12f),
                new Vector3(-30f, 0f, 30f), new Vector3(30f, 0f, 30f), new Vector3(-10f, 0f, -36f), new Vector3(10f, 0f, -36f),
                new Vector3(-52f, 0f, 32f), new Vector3(52f, 0f, 32f),
            };
            l.altarAnchors = new[] { new Vector3(-10f, 1.2f, 41f), new Vector3(10f, 1.2f, 41f), new Vector3(41.3f, 1.2f, 4f) }; // the stage, a platform

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }
    }
}
