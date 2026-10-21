using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the layout of Arena 4, the Grand Arena (docs/ARENAS.md §4): a Greek cross of white marble, four arms 52 m
    /// wide reaching 70 m from the centre, the four inner corners cut on the diagonal by the patron boxes (≈ 11,600 m²
    /// of play, ~1.36× the Stone Amphitheater). Every wall run is a podium wall with eight tiers of seats behind it
    /// (decor, kept solid by invisible stone); a monumental gate ends each arm (north = the boss's); towers close the
    /// outer corners. A round dais with the Patron's statue in the plaza, two terraces in each arm, a fire-jet row
    /// across each arm, four blade sweepers round the dais. The play area is the wall line inset by PlayInset
    /// (ArenaLayoutDef.outline). Designer-owned once it exists: only the "Regenerate" menu item overwrites it. Source
    /// art: BlenderSrc/environment/grand_arena.py, whose L / H / C / GATE_HALF and stand profile these must match.
    /// </summary>
    public static class GrandArenaLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_GrandArena.asset";
        const string Env = "Environment/";
        /// <summary>The wall line: an arm's reach from the centre, an arm's half width, the patron boxes' cut.</summary>
        public const float L = 70f, H = 26f, C = 14f;
        /// <summary>Half the monumental gate's width (its piece covers the wall there).</summary>
        public const float GateHalf = 5.5f;
        const float StandWidth = 4f;     // SM_GrandStand_4m
        const float Podium = 3.2f, PodiumCap = 0.6f, TierRise = 0.45f, TierRun = 0.8f;
        const int Tiers = 8;
        const float PlayInset = 1.1f;    // enemies stay this far inside the wall face
        /// <summary>Where the fire-jet rows cross the arms (m from the centre), the blades' hubs (± on both axes).</summary>
        public const float JetRow = 52f, BladeHub = 15f;
        public const float DaisRadius = 7f, DaisHeight = 1.2f;

        [MenuItem("Gladiator/Arenas/Regenerate Grand Arena Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        /// <summary>(x, z) turned `k` quarter turns clockwise seen from above: the north arm's numbers become the east,
        /// south and west arms'.</summary>
        static Vector2 Q(Vector2 v, int k)
        {
            for (int i = 0; i < (k & 3); i++) v = new Vector2(v.y, -v.x);
            return v;
        }

        /// <summary>The wall line, clockwise from above, starting at the north arm's west corner: 16 corners.</summary>
        public static Vector2[] WallLine()
        {
            var o = new List<Vector2>();
            for (int k = 0; k < 4; k++)
            {
                o.Add(Q(new Vector2(-H, L), k));
                o.Add(Q(new Vector2(H, L), k));
                o.Add(Q(new Vector2(H, H + C), k));
                o.Add(Q(new Vector2(H + C, H), k)); // the patron box's diagonal ends here
            }
            return o.ToArray();
        }

        /// <summary>A clockwise polygon moved `d` metres inward (each edge parallel, corners re-met).</summary>
        public static Vector2[] Inset(Vector2[] poly, float d)
        {
            int n = poly.Length;
            var o = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[(i + n - 1) % n], b = poly[i], c = poly[(i + 1) % n];
                Vector2 d1 = (b - a).normalized, d2 = (c - b).normalized;
                Vector2 n1 = new Vector2(d1.y, -d1.x), n2 = new Vector2(d2.y, -d2.x); // clockwise: inward is on the right
                Vector2 p1 = a + n1 * d, p2 = b + n2 * d;
                float den = d1.x * d2.y - d1.y * d2.x;
                if (Mathf.Abs(den) < 1e-5f) { o[i] = b + n1 * d; continue; }
                float t = ((p2.x - p1.x) * d2.y - (p2.y - p1.y) * d2.x) / den;
                o[i] = p1 + d1 * t;
            }
            return o;
        }

        public static Vector2[] Outline() => Inset(WallLine(), PlayInset);

        static Vector3 V3(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);
        /// <summary>Yaw that turns a model's +Z toward `dir` (XZ).</summary>
        static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        /// <summary>Where the Patron's hired swords jump down: the foot of each patron box.</summary>
        public static Vector3[] GuardSpots()
        {
            var o = new Vector3[4];
            float s = H + C * 0.5f - 3f / Mathf.Sqrt(2f); // the box's centre, 3 m in toward the plaza
            for (int k = 0; k < 4; k++) o[k] = V3(Q(new Vector2(s, s), k));
            return o;
        }

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            l.id = "grand_arena";
            l.displayName = "The Grand Arena";
            l.tier = 3;
            l.center = Vector3.zero;
            l.outline = Outline();
            l.radius = H - PlayInset; // the biggest circle round the centre (the debug spawn ring)
            l.aspect = 1f;
            l.fog = new Vector2(70f, 300f);    // the far arm's gate is ~140 m away
            l.playerSpawn = new Vector3(0f, 0.1f, -16f);
            l.playerYaw = 0f;                  // facing the dais, the statue and the north gate behind it
            l.maxAlive = 400;
            l.densityMul = 1.2f;               // C18: the biggest floor gets more of the horde
            l.spawnMaxDistance = 50f;
            l.eliteEvery = 60f;
            l.sunEuler = new Vector3(50f, -38f, 0f);
            l.enemyHpMul = 1.8f;
            l.playerDamageTakenMul = 1.35f;
            l.gateModel = Env + "SM_GrandGate";
            l.secrets = ArenaSecrets.None;
            l.secretSpots = new Vector3[0];
            l.guardSpots = GuardSpots();

            var pieces = new List<ArenaLayoutDef.Piece>();
            var gates = new List<ArenaLayoutDef.Gate>();
            var crowd = new List<ArenaLayoutDef.CrowdZone>();
            var stone = new List<Vector4>();
            var bounds = new List<Vector4>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });

            Add("SM_GrandFloor", PieceRole.Walkable, Vector3.zero, 0f);

            // ---------- the walls: every edge of the wall line is invisible stone (and a player-only wall behind it);
            // the straight runs carry stand sections (scaled to fit exactly), the diagonals the patron boxes
            int seed = 0;
            void Seats(Vector2 a, Vector2 b, Vector2 inward)
            {
                for (int k = 0; k < Tiers; k++)
                {
                    float off = PodiumCap + TierRun * (k + 0.5f);
                    Vector2 pa = a - inward * off, pb = b - inward * off;
                    float len = (pb - pa).magnitude;
                    crowd.Add(new ArenaLayoutDef.CrowdZone
                    {
                        center = V3((pa + pb) * 0.5f, Podium + TierRise * (k + 1)),
                        size = new Vector2(len * 0.9f, 0.4f), yaw = YawOf(inward),
                        count = Mathf.Max(1, Mathf.RoundToInt(len * 0.6f + ((seed++ % 3) - 1) * 0.4f)),
                    });
                }
            }
            void Stands(Vector2 a, Vector2 b)
            {
                Vector2 dir = (b - a).normalized, inward = new Vector2(dir.y, -dir.x);
                float len = (b - a).magnitude;
                int m = Mathf.Max(1, Mathf.RoundToInt(len / StandWidth));
                float w = len / m;
                for (int i = 0; i < m; i++)
                    Add("SM_GrandStand_4m", PieceRole.Decor, V3(a + dir * (w * (i + 0.5f))), YawOf(inward), new Vector3(w / StandWidth, 1f, 1f));
                Seats(a, b, inward);
            }
            var line = WallLine();
            for (int i = 0; i < line.Length; i++)
            {
                Vector2 a = line[i], b = line[(i + 1) % line.Length];
                Vector2 dir = (b - a).normalized, inward = new Vector2(dir.y, -dir.x);
                float len = (b - a).magnitude;
                stone.Add(new Vector4((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, YawOf(inward), len + 0.3f));
                Vector2 back = (a + b) * 0.5f - inward * 1.2f;
                bounds.Add(new Vector4(back.x, back.y, YawOf(inward), len + 3f));
                int kind = i % 4; // 0: the arm's tip (the gate), 1: its east side, 2: the patron box, 3: the next arm's west side
                if (kind == 0)
                {
                    Vector2 mid = (a + b) * 0.5f;
                    gates.Add(new ArenaLayoutDef.Gate { position = V3(mid), yaw = YawOf(inward) });
                    Stands(a, mid - dir * GateHalf);
                    Stands(mid + dir * GateHalf, b);
                    // a tower closes each outer corner (behind the corner, where two stand runs meet)
                    Add("SM_GrandTower", PieceRole.Decor, V3(a - inward * 4f - dir * 4f), YawOf(inward));
                    Add("SM_GrandTower", PieceRole.Decor, V3(b - inward * 4f + dir * 4f), YawOf(inward));
                }
                else if (kind == 2) Add("SM_PatronBox", PieceRole.Decor, V3((a + b) * 0.5f), YawOf(inward), new Vector3(len / (C * Mathf.Sqrt(2f)), 1f, 1f));
                else Stands(a, b);
            }
            // the nobles in the four boxes
            for (int k = 0; k < 4; k++)
            {
                Vector2 c = Q(new Vector2(H + C * 0.5f, H + C * 0.5f), k), inward = Q(new Vector2(-1f, -1f), k).normalized;
                crowd.Add(new ArenaLayoutDef.CrowdZone { center = V3(c - inward * 2.2f, 4.2f), size = new Vector2(12f, 1.2f), yaw = YawOf(inward), count = 18 });
            }
            l.solidWalls = stone.ToArray();
            l.solidWallHeight = 8.4f;
            l.boundary = bounds.ToArray();

            // ---------- the plaza: the dais (stairs on four sides) under the Patron's statue
            Add("SM_GrandDais", PieceRole.Walkable, Vector3.zero, 0f);
            for (int k = 0; k < 4; k++)
            {
                Vector2 f = Q(new Vector2(0f, 1f), k);
                Add("SM_GrandStairs", PieceRole.Walkable, V3(f * (DaisRadius + 1.8f)), YawOf(f));
            }
            Add("PR_PatronStatue", PieceRole.Solid, new Vector3(0f, DaisHeight, 0f), 180f);

            // ---------- two marble terraces in each arm, against its walls (stairs on one, a ramp on the other)
            for (int k = 0; k < 4; k++)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 pos = Q(new Vector2(side * (H - 3f), 37f), k); // flush with the wall: no slot behind it
                    Vector2 f = Q(new Vector2(-side, 0f), k); // facing the arm's middle
                    float yaw = YawOf(f);
                    Add("SM_GrandTerrace", PieceRole.Walkable, V3(pos), yaw);
                    bool ramp = (k + (side > 0f ? 1 : 0)) % 2 == 0;
                    Add(ramp ? "SM_GrandRamp" : "SM_GrandStairs", PieceRole.Walkable, V3(pos + f * (3f + (ramp ? 4f : 2f))), yaw);
                }
            }

            // ---------- the hazards: a fire-jet row across each arm (N, E, S, W: they fire in turn), four blade hubs
            var jets = new List<Vector4>();
            for (int k = 0; k < 4; k++)
            {
                Vector2 c = Q(new Vector2(0f, JetRow), k);
                jets.Add(new Vector4(c.x, c.y, k * 90f, 2f * (H - PlayInset) - 0.4f));
            }
            l.fireJets = jets.ToArray();
            l.bladeSweepers = new[]
            {
                new Vector3(BladeHub, 0f, BladeHub), new Vector3(BladeHub, 0f, -BladeHub),
                new Vector3(-BladeHub, 0f, -BladeHub), new Vector3(-BladeHub, 0f, BladeHub),
            };

            l.pieces = pieces.ToArray();
            l.gates = gates.ToArray();
            l.bossGate = 0; // north
            l.crowd = crowd.ToArray();
            l.zones = new ArenaLayoutDef.Zone[0];
            l.barricades = new Vector4[0];
            l.spikeTraps = new Vector3[0];
            l.cages = new Vector4[0];
            // a brazier on every terrace, at its back corner
            var braziers = new List<Vector3>();
            for (int k = 0; k < 4; k++)
                foreach (float side in new[] { -1f, 1f })
                    braziers.Add(V3(Q(new Vector2(side * (H - 1.6f), 41f), k), 1.2f));
            l.braziers = braziers.ToArray();

            var chests = new List<Vector3>();
            foreach (var p in new[] { new Vector2(0f, -14f), new Vector2(0f, 14f), new Vector2(-14f, 0f), new Vector2(14f, 0f) }) chests.Add(V3(p));
            for (int k = 0; k < 4; k++)
            {
                chests.Add(V3(Q(new Vector2(-8f, 44f), k)));
                chests.Add(V3(Q(new Vector2(10f, 62f), k)));
            }
            l.chestAnchors = chests.ToArray();
            l.altarAnchors = new[] { V3(Q(new Vector2(-(H - 3f), 35f), 0), 1.2f), V3(Q(new Vector2(-(H - 3f), 35f), 2), 1.2f), new Vector3(0f, 0f, -22f) };

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }
    }
}
