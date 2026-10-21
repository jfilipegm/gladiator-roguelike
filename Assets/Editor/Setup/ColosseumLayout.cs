using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the layout of Arena 6, the Colosseum (docs/ARENAS.md §6, the numbers: docs/tracks/R2-colosseum.md): the
    /// great oval, an ellipse of 98 × 67 m sampled every 3.75°, each great gate's 22 m span cut by its chord at
    /// x = ±96.67 (≈ 20,000 m² of play, 1.31× the Imperial Arena). A 22 m bowl of six cavea runs between the six gates
    /// (decor, kept solid by invisible stone), the imperial box and the editor's box on the podium at the apexes. The
    /// floor is a deck over the hypogeum: 30 trapdoors in six groups (a broken ring of 24, three either side of the stage
    /// on the axis) and 8 lifts, placed by ArenaBuilder's hypogeum hook (Hypogeum), not as pieces. In the middle the
    /// Stage of Victors (r 10, 1.2 m, four stairs, the Victor's lift in its centre) and four statues of him; four
    /// scenery hills (2.2 m) in the outer band. Designer-owned once it exists: only the "Regenerate" menu item overwrites
    /// it. Source art: BlenderSrc/environment/colosseum.py (docs/tracks/R2-env-art.md, whose numbers these must match).
    /// </summary>
    public static class ColosseumLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_Colosseum.asset";
        const string Env = "Environment/";
        /// <summary>The wall line: the ellipse's semi-axes (x, z) and its sampling step (deg).</summary>
        public const float A = 98f, B = 67f, WallStep = 3.75f;
        /// <summary>Half a great gate's span: the ellipse is cut by a chord there, at x = ±ChordX.</summary>
        public const float GreatGateHalf = 11f;
        public static readonly float ChordX = A * Mathf.Sqrt(1f - GreatGateHalf * GreatGateHalf / (B * B)); // 96.67
        public const float PlayInset = 1.1f; // enemies stay this far inside the wall face (GrandArenaLayout.Inset)
        /// <summary>The trapdoor ring's centre line (the doors' centres), the doors (along their local X, across), the lifts' side.</summary>
        public const float RingA = 62f, RingB = 42f;
        public static readonly Vector2 DoorSize = new Vector2(8f, 4f);
        public const float LiftSize = 4f;
        /// <summary>The Victor's lift in the stage's centre: SM_ColLift scaled to 5 × 5 m.</summary>
        public const float CenterLiftScale = 1.25f;
        public const float StageRadius = 10f, StageHeight = 1.2f, HillHeight = 2.2f;
        /// <summary>SM_ColStairs: origin at the low front edge's centre, rising toward its local -Z, this far out from the centre.</summary>
        const float StairsOut = 12f;
        const float StairsWidth = 3f, StairsDepth = 2f;
        /// <summary>The statues of the Victor: on the diagonals at r 14, a circle footprint.</summary>
        const float StatueAt = 9.9f, StatueRadius = 1.1f;
        /// <summary>The hills (SM_ColMons_A / _B): 14 m along their local X, 10 m along Z, centres at (±28, ±50.5).</summary>
        const float HillX = 28f, HillZ = 50.5f, HillLength = 14f, HillDepth = 10f;
        /// <summary>The side gates' parameter on the ellipse (deg), counter-clockwise from the east.</summary>
        static readonly float[] SideGateT = { 60f, 120f, 240f, 300f };
        /// <summary>Nothing may stand within this of a trapdoor or a lift (m): no chest, altar, spawn, stair, statue, hill
        /// footprint or gate mouth.</summary>
        public const float HoleClear = 1f;

        // ---------- the stand profile (u outward from the wall line, y up): where the crowd stands
        /// <summary>The podium's top, the senators' terrace (u 0.3-2.4) on it.</summary>
        public const float PodiumHeight = 4f, TerraceU0 = 0.3f, TerraceU1 = 2.4f;
        /// <summary>The three maeniana: each 5 tiers (rise 0.5, run 0.8) from its first tier's u and the walkway height below it.</summary>
        public static readonly float[] MaenianumU = { 2.4f, 7.4f, 12.4f };
        public static readonly float[] MaenianumY = { 4.0f, 7.7f, 11.4f };
        public const int TiersPerMaenianum = 5;
        public const float TierRise = 0.5f, TierRun = 0.8f;
        /// <summary>Spectators per metre of terrace, and per metre per tier in maenianum I, II, III (thinning to the top).</summary>
        public const float TerraceDensity = 0.5f;
        public static readonly float[] MaenianumDensity = { 0.5f, 0.45f, 0.35f };
        /// <summary>SM_ColPulvinar: its half width along the wall (an estimate: R2-design's crowd count used 15.6 m), and the
        /// seats behind it cut below this height (its canopy reaches 9 m; maenianum III starts at 11.9).</summary>
        public const float BoxHalf = 7.8f, BoxCutBelow = 11f;
        /// <summary>The boxes' guests: 14 senators either side of the throne (local (0, 4.0, -0.8)), and one on the throne.</summary>
        const int BoxSenators = 14;
        const float ThroneBack = 0.8f;

        // ---------- the hypogeum (Hypogeum; numbers: the contract)
        /// <summary>The 30 trapdoors (x, z, yaw of the long side = local X, group): 0 ring NE, 1 ring NW, 2 ring SW, 3 ring
        /// SE (6 each, on the ellipse 62 × 42 with 5.74 m bridges), 4 axis W, 5 axis E (3 each). SM_ColFloor's holes are
        /// cut at exactly these.</summary>
        public static readonly Vector4[] Trapdoors =
        {
            new Vector4(61.18f, 6.80f, 76.4f, 0), new Vector4(55.23f, 19.08f, 53.0f, 0), new Vector4(45.40f, 28.61f, 36.1f, 0),
            new Vector4(33.46f, 35.36f, 23.5f, 0), new Vector4(20.43f, 39.65f, 13.3f, 0), new Vector4(6.86f, 41.74f, 4.3f, 0),
            new Vector4(-6.86f, 41.74f, -4.3f, 1), new Vector4(-20.43f, 39.65f, -13.3f, 1), new Vector4(-33.46f, 35.36f, -23.5f, 1),
            new Vector4(-45.40f, 28.61f, -36.1f, 1), new Vector4(-55.23f, 19.08f, -53.0f, 1), new Vector4(-61.18f, 6.80f, -76.4f, 1),
            new Vector4(-61.18f, -6.80f, 76.4f, 2), new Vector4(-55.23f, -19.08f, 53.0f, 2), new Vector4(-45.40f, -28.61f, 36.1f, 2),
            new Vector4(-33.46f, -35.36f, 23.5f, 2), new Vector4(-20.43f, -39.65f, 13.3f, 2), new Vector4(-6.86f, -41.74f, 4.3f, 2),
            new Vector4(6.86f, -41.74f, -4.3f, 3), new Vector4(20.43f, -39.65f, -13.3f, 3), new Vector4(33.46f, -35.36f, -23.5f, 3),
            new Vector4(45.40f, -28.61f, -36.1f, 3), new Vector4(55.23f, -19.08f, -53.0f, 3), new Vector4(61.18f, -6.80f, -76.4f, 3),
            new Vector4(-20f, 0f, 0f, 4), new Vector4(-32f, 0f, 0f, 4), new Vector4(-44f, 0f, 0f, 4),
            new Vector4(20f, 0f, 0f, 5), new Vector4(32f, 0f, 0f, 5), new Vector4(44f, 0f, 0f, 5),
        };

        /// <summary>The 8 lifts L0-L7 (4 × 4 m, yaw 0, top at the floor): four in the inner arena, four in the outer band.
        /// Also the boss's guard spots.</summary>
        public static readonly Vector3[] Lifts =
        {
            new Vector3(34f, 0f, 20f), new Vector3(-34f, 0f, 20f), new Vector3(-34f, 0f, -20f), new Vector3(34f, 0f, -20f),
            new Vector3(80f, 0f, 24f), new Vector3(-80f, 0f, 24f), new Vector3(-80f, 0f, -24f), new Vector3(80f, 0f, -24f),
        };

        /// <summary>The Victor's lift: in the stage's centre, its top at the stage's.</summary>
        public static readonly Vector3 CenterLift = new Vector3(0f, StageHeight, 0f);

        [MenuItem("Gladiator/Arenas/Regenerate Colosseum Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        /// <summary>The ellipse's point at parameter `t` (radians).</summary>
        public static Vector2 Ellipse(float t) => new Vector2(A * Mathf.Cos(t), B * Mathf.Sin(t));

        /// <summary>The ellipse's outward unit normal at a point on it.</summary>
        public static Vector2 Outward(Vector2 p) => new Vector2(p.x / (A * A), p.y / (B * B)).normalized;

        /// <summary>The wall line, clockwise from above (as GrandArenaLayout.Inset needs), starting with the Gate of
        /// Death's chord: (96.67, 11) → (96.67, -11), the ellipse's samples every 3.75° down the south side, the Gate of
        /// Life's chord (-96.67, -11) → (-96.67, 11), the samples back along the north side. 90 corners: the 96 samples
        /// less the 5 inside each great gate's span, plus the chords' 4 ends. Edge 0 is the Gate of Death's chord,
        /// edge WestChordEdge the Gate of Life's.</summary>
        public static Vector2[] WallLine()
        {
            var o = new List<Vector2> { new Vector2(ChordX, GreatGateHalf), new Vector2(ChordX, -GreatGateHalf) };
            bool west = false;
            int n = Mathf.RoundToInt(360f / WallStep);
            for (int k = 1; k < n; k++)
            {
                Vector2 p = Ellipse(-WallStep * k * Mathf.Deg2Rad);
                if (Mathf.Abs(p.y) < GreatGateHalf)
                {
                    if (p.x < 0f && !west) { o.Add(new Vector2(-ChordX, -GreatGateHalf)); o.Add(new Vector2(-ChordX, GreatGateHalf)); west = true; }
                    continue; // inside a great gate's span: its chord replaces the curve
                }
                o.Add(p);
            }
            return o.ToArray();
        }

        public const int EastChordEdge = 0, WestChordEdge = 45;

        public static Vector2[] Outline() => GrandArenaLayout.Inset(WallLine(), PlayInset);

        /// <summary>Distance (XZ) from `p` to trapdoor `d`'s 8 × 4 rectangle (0 inside it).</summary>
        public static float DoorDistance(int d, Vector2 p)
        {
            var t = Trapdoors[d];
            float yaw = t.z * Mathf.Deg2Rad;
            Vector2 rel = p - new Vector2(t.x, t.y);
            float lx = rel.x * Mathf.Cos(yaw) - rel.y * Mathf.Sin(yaw); // along local X = (cos, -sin)
            float lz = rel.x * Mathf.Sin(yaw) + rel.y * Mathf.Cos(yaw); // along local Z = (sin, cos)
            float ox = Mathf.Max(0f, Mathf.Abs(lx) - DoorSize.x * 0.5f), oz = Mathf.Max(0f, Mathf.Abs(lz) - DoorSize.y * 0.5f);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        /// <summary>Distance (XZ) from `p` to the nearest trapdoor (0 on one).</summary>
        public static float TrapdoorClearance(Vector2 p)
        {
            float best = float.MaxValue;
            for (int d = 0; d < Trapdoors.Length; d++) best = Mathf.Min(best, DoorDistance(d, p));
            return best;
        }

        /// <summary>Distance (XZ) from `p` to the nearest lift's square, the Victor's 5 × 5 m one included (0 on one).</summary>
        public static float LiftClearance(Vector2 p)
        {
            float best = SquareDistance(p, new Vector2(CenterLift.x, CenterLift.z), LiftSize * CenterLiftScale * 0.5f);
            foreach (var l in Lifts) best = Mathf.Min(best, SquareDistance(p, new Vector2(l.x, l.z), LiftSize * 0.5f));
            return best;
        }

        static float SquareDistance(Vector2 p, Vector2 c, float half)
        {
            float ox = Mathf.Max(0f, Mathf.Abs(p.x - c.x) - half), oz = Mathf.Max(0f, Mathf.Abs(p.y - c.y) - half);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        /// <summary>True if `p` (XZ) is on a trapdoor grown by `pad` metres.</summary>
        public static bool OnTrapdoor(Vector3 p, float pad) => TrapdoorClearance(new Vector2(p.x, p.z)) <= pad;

        /// <summary>True if `p` (XZ) is on a lift (the Victor's included) grown by `pad` metres.</summary>
        public static bool OnLift(Vector3 p, float pad) => LiftClearance(new Vector2(p.x, p.z)) <= pad;

        static Vector3 V3(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);
        /// <summary>Yaw that turns a model's +Z toward `dir` (XZ).</summary>
        static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        static Vector2 Forward(float yaw) => new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
        static Vector2 Right(float yaw) => new Vector2(Mathf.Cos(yaw * Mathf.Deg2Rad), -Mathf.Sin(yaw * Mathf.Deg2Rad));

        /// <summary>The rows of the bowl where the crowd stands: (u, y, the zone's depth, spectators per metre).</summary>
        static List<Vector4> CrowdRows()
        {
            var rows = new List<Vector4> { new Vector4((TerraceU0 + TerraceU1) * 0.5f, PodiumHeight, 1.4f, TerraceDensity) };
            for (int m = 0; m < MaenianumU.Length; m++)
                for (int k = 1; k <= TiersPerMaenianum; k++)
                    rows.Add(new Vector4(MaenianumU[m] + TierRun * (k - 0.5f), MaenianumY[m] + TierRise * k, 0.4f, MaenianumDensity[m]));
            return rows;
        }

        /// <summary>Cuts the part of a crowd row's segment that lies behind a box (|x| < BoxHalf). False if nothing is left.
        /// A segment is one wall-line edge long (≤ 6.4 m): it never spans the whole box.</summary>
        static bool ClipBehindBox(ref Vector2 a, ref Vector2 b)
        {
            bool ina = Mathf.Abs(a.x) < BoxHalf, inb = Mathf.Abs(b.x) < BoxHalf;
            if (ina && inb) return false;
            if (!ina && !inb) return true;
            Vector2 inside = ina ? a : b, outside = ina ? b : a;
            float edge = Mathf.Sign(outside.x) * BoxHalf;
            Vector2 cut = Vector2.Lerp(inside, outside, (edge - inside.x) / (outside.x - inside.x));
            if (ina) a = cut; else b = cut;
            return true;
        }

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            l.id = "colosseum";
            l.displayName = "The Colosseum";
            l.tier = 5;
            l.center = Vector3.zero;           // the stage: it sees every edge (the radial edge table works from here)
            l.outline = Outline();
            l.radius = A - PlayInset;          // 96.9 along X (R2-design); with an outline the edge table is the outline's
            l.aspect = 0.68f;                  // ≈ 65.9 / 96.9
            l.fog = new Vector2(90f, 380f);    // the far gate is ~190 m away
            l.playerSpawn = new Vector3(0f, 0.1f, -26f);
            l.playerYaw = 0f;                  // facing north: the stage, its statues and the imperial box beyond
            l.maxAlive = 450;                  // "huge waves", inside §8's ~500 at peak
            l.densityMul = 1.3f;
            l.spawnMaxDistance = 55f;          // the horde comes from your end, not from 190 m away
            l.eliteEvery = 60f;
            l.sunEuler = new Vector3(32f, -40f, 0f); // a low, warm late-afternoon sun; Hypogeum lerps it in the boss fight
            l.enemyHpMul = 2.55f;
            l.playerDamageTakenMul = 1.55f;
            l.gateModel = Env + "SM_ColGate";
            l.secrets = ArenaSecrets.None;
            l.secretSpots = new Vector3[0];
            l.guardSpots = (Vector3[])Lifts.Clone(); // the Eternal Victor's dead rise on the lifts nearest you

            // the hypogeum (ArenaBuilder's hook places the trapdoors and lifts and adds Hypogeum): the floor drops open
            // from 0:40 every 14 s (2 s telegraph, 4 s open), lift waves from 1:30 every 45 s; the packs: ColosseumSetup
            l.trapdoors = (Vector4[])Trapdoors.Clone();
            l.trapdoorSize = DoorSize;
            l.trapdoorModel = Env + "SM_ColTrapdoor";
            l.lifts = (Vector3[])Lifts.Clone();
            l.liftSize = LiftSize;
            l.liftModel = Env + "SM_ColLift";
            l.hasCenterLift = true;
            l.centerLift = CenterLift;
            l.centerLiftScale = CenterLiftScale;
            l.dropFirst = 40f;
            l.dropEvery = 14f;
            l.dropTelegraph = 2f;
            l.dropOpen = 4f;
            l.liftFirst = 90f;
            l.liftEvery = 45f;
            l.liftsPerWave = 3;
            l.liftMinDistance = 12f;
            l.swarmMul = 1.5f; // the Director's swarms × this

            var pieces = new List<ArenaLayoutDef.Piece>();
            var gates = new List<ArenaLayoutDef.Gate>();
            var crowd = new List<ArenaLayoutDef.CrowdZone>();
            var stone = new List<Vector4>();
            var bounds = new List<Vector4>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });

            Add("SM_ColFloor", PieceRole.Walkable, Vector3.zero, 0f); // the deck, with a hole under every trapdoor and lift
            // the bowl: six runs between the gates, each one mesh in world coordinates (they pass behind the boxes)
            for (int k = 0; k < 6; k++) Add("SM_ColCavea_" + k, PieceRole.Decor, Vector3.zero, 0f);

            // ---------- the walls: every edge of the wall line is invisible stone (and a player-only wall behind it);
            // the crowd stands on the terrace and the 15 tiers along every edge but the great gates' chords (the side
            // gates carry seats), less the rows behind the two boxes
            var rows = CrowdRows();
            float carry = 0f;
            var line = WallLine();
            for (int i = 0; i < line.Length; i++)
            {
                Vector2 a = line[i], b = line[(i + 1) % line.Length];
                Vector2 dir = (b - a).normalized, inward = new Vector2(dir.y, -dir.x);
                float len = (b - a).magnitude;
                stone.Add(new Vector4((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, YawOf(inward), len + 0.3f));
                Vector2 back = (a + b) * 0.5f - inward * 1.2f;
                bounds.Add(new Vector4(back.x, back.y, YawOf(inward), len + 3f));
                if (i == EastChordEdge || i == WestChordEdge) continue; // the great gates rise above the bowl: no seats
                Vector2 na = Outward(a), nb = Outward(b); // every corner lies on the ellipse
                foreach (var r in rows)
                {
                    Vector2 pa = a + na * r.x, pb = b + nb * r.x;
                    if (r.y < BoxCutBelow && !ClipBehindBox(ref pa, ref pb)) continue;
                    float rowLen = (pb - pa).magnitude;
                    carry += rowLen * r.w;
                    int n = Mathf.FloorToInt(carry);
                    carry -= n;
                    if (n <= 0) continue;
                    crowd.Add(new ArenaLayoutDef.CrowdZone
                    {
                        center = V3((pa + pb) * 0.5f, r.y),
                        size = new Vector2(rowLen * 0.9f, r.z), yaw = YawOf(-(na + nb)), count = n,
                    });
                }
            }
            l.solidWalls = stone.ToArray();
            l.solidWallHeight = 8.4f;
            l.boundary = bounds.ToArray(); // ArenaBuilder's player walls are 30 m tall (to 27 m): the bowl is 22 m

            // ---------- the gates, counter-clockwise from the east: G0 the Gate of Death and G3 the Gate of Life on their
            // chords (the great arches), the side gates on the ellipse at t = 60, 120, 240, 300°, all facing in
            gates.Add(new ArenaLayoutDef.Gate { position = new Vector3(ChordX, 0f, 0f), yaw = -90f, model = Env + "SM_ColGreatGate" });
            for (int k = 0; k < SideGateT.Length; k++)
            {
                if (k == 2) gates.Add(new ArenaLayoutDef.Gate { position = new Vector3(-ChordX, 0f, 0f), yaw = 90f, model = Env + "SM_ColGreatGate" });
                Vector2 p = Ellipse(SideGateT[k] * Mathf.Deg2Rad);
                gates.Add(new ArenaLayoutDef.Gate { position = V3(p), yaw = YawOf(-Outward(p)) });
            }

            // ---------- the boxes on the podium at the apexes: the Emperor's (north) and the editor's (south), each with
            // 14 senators either side of the throne and one on it (the Emperor attends)
            foreach (float s in new[] { 1f, -1f })
            {
                float yaw = s > 0f ? 180f : 0f;
                var pos = new Vector3(0f, 0f, B * s);
                Add("SM_ColPulvinar", PieceRole.Decor, pos, yaw);
                Vector2 f = Forward(yaw), rt = Right(yaw);
                Vector2 throne = new Vector2(pos.x, pos.z) - f * ThroneBack;
                foreach (float side in new[] { -1f, 1f })
                    crowd.Add(new ArenaLayoutDef.CrowdZone { center = V3(throne + rt * (3.3f * side) - f * 0.6f, PodiumHeight), size = new Vector2(4.4f, 1.4f), yaw = yaw, count = BoxSenators / 2 });
                crowd.Add(new ArenaLayoutDef.CrowdZone { center = V3(throne, PodiumHeight), size = new Vector2(0.4f, 0.4f), yaw = yaw, count = 1 });
            }

            // ---------- the Stage of Victors: walkable at 1.2 m, stairs on each axis rising inward, the Victor's lift in
            // its centre (the hypogeum hook), four statues of him on the diagonals (Solid: sight and his thrown falx)
            Add("SM_ColStage", PieceRole.Walkable, Vector3.zero, 0f);
            for (int k = 0; k < 4; k++)
            {
                float yaw = k < 3 ? k * 90f : -90f; // (0, 12) 0, (12, 0) 90, (0, -12) 180, (-12, 0) -90
                Add("SM_ColStairs", PieceRole.Walkable, V3(Forward(yaw) * StairsOut), yaw); // rises toward its local -Z: the stage
            }
            foreach (var d in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
                Add("PR_VictorStatue", PieceRole.Solid, V3(d * StatueAt), YawOf(d)); // facing outward

            // ---------- the hills in the outer band (2.2 m, slopes ≤ 20°), their rock faces toward the near wall
            Add("SM_ColMons_A", PieceRole.Walkable, new Vector3(HillX, 0f, HillZ), 180f);
            Add("SM_ColMons_A", PieceRole.Walkable, new Vector3(HillX, 0f, -HillZ), 0f);
            Add("SM_ColMons_B", PieceRole.Walkable, new Vector3(-HillX, 0f, HillZ), 180f);
            Add("SM_ColMons_B", PieceRole.Walkable, new Vector3(-HillX, 0f, -HillZ), 0f);

            l.pieces = pieces.ToArray();
            l.gates = gates.ToArray();
            l.bossGate = 0; // the Gate of Death: the fallback (the Eternal Victor rises from the stage)
            l.crowd = crowd.ToArray();
            l.zones = new ArenaLayoutDef.Zone[0];
            l.barricades = new Vector4[0];
            l.braziers = new Vector3[0];       // no fire in the static art: the telegraphs are the floor's only orange-red
            l.spikeTraps = new Vector3[0];
            l.cages = new Vector4[0];
            l.fireJets = new Vector4[0];
            l.bladeSweepers = new Vector3[0];
            l.trackRadius = 0f;                // no chariots

            // 18 chests, all clear of the trapdoors, lifts, stairs and gate mouths (two on hill tops); 3 altars (the stage
            // top, two hill tops)
            l.chestAnchors = new[]
            {
                new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, -36f), new Vector3(22f, 0f, 10f), new Vector3(-22f, 0f, -10f),
                new Vector3(48f, 0f, 12f), new Vector3(-48f, 0f, -12f), new Vector3(48f, 0f, -14f), new Vector3(-48f, 0f, 14f),
                new Vector3(66f, 0f, 30f), new Vector3(-66f, 0f, 30f), new Vector3(66f, 0f, -30f), new Vector3(-66f, 0f, -30f),
                new Vector3(84f, 0f, 8f), new Vector3(-84f, 0f, -8f), new Vector3(8f, 0f, 56f), new Vector3(-8f, 0f, -57f),
                new Vector3(HillX, HillHeight, HillZ), new Vector3(-HillX, HillHeight, -HillZ),
            };
            l.altarAnchors = new[] { new Vector3(0f, StageHeight, 6.5f), new Vector3(-HillX, HillHeight, HillZ), new Vector3(HillX, HillHeight, -HillZ) };

            CheckHolesClear(l);
            int people = 0;
            foreach (var c in l.crowd) people += c.count;
            Debug.Log($"[ColosseumLayout] play area {l.PlayArea():0} m² ({l.PlayArea() / 15302f:0.00}× the Imperial Arena's 15,302), " +
                      $"{l.outline.Length} corners, {l.pieces.Length} pieces, {l.gates.Length} gates, {Trapdoors.Length} trapdoors, {Lifts.Length} lifts, " +
                      $"crowd {people} in {l.crowd.Length} zones");

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }

        /// <summary>Nothing stands on the floor that opens: logs every chest, altar, gate mouth, stair, statue or hill
        /// footprint and the player spawn within HoleClear of a trapdoor or a lift (the stage's own lift is under the stage:
        /// only the anchors are checked against it).</summary>
        static void CheckHolesClear(ArenaLayoutDef l)
        {
            void Point(string what, Vector2 p, bool againstCentre = true)
            {
                if (TrapdoorClearance(p) <= HoleClear) Debug.LogError($"[ColosseumLayout] {what} at {p} is within {HoleClear} m of a trapdoor");
                float lift = float.MaxValue;
                foreach (var q in Lifts) lift = Mathf.Min(lift, SquareDistance(p, new Vector2(q.x, q.z), LiftSize * 0.5f));
                if (againstCentre) lift = Mathf.Min(lift, SquareDistance(p, Vector2.zero, LiftSize * CenterLiftScale * 0.5f));
                if (lift <= HoleClear) Debug.LogError($"[ColosseumLayout] {what} at {p} is within {HoleClear} m of a lift");
            }
            // a footprint: its centre and points every 0.25 m round its outline (the holes are bigger than that step)
            void Rect(string what, Vector2 c, float yaw, float sx, float sz)
            {
                Point(what, c, false);
                Vector2 ax = Right(yaw), az = Forward(yaw);
                int nx = Mathf.CeilToInt(sx / 0.25f), nz = Mathf.CeilToInt(sz / 0.25f);
                for (int i = 0; i <= nx; i++)
                    foreach (float e in new[] { -0.5f, 0.5f })
                        Point(what, c + ax * (sx * (i / (float)nx - 0.5f)) + az * (sz * e), false);
                for (int i = 0; i <= nz; i++)
                    foreach (float e in new[] { -0.5f, 0.5f })
                        Point(what, c + az * (sz * (i / (float)nz - 0.5f)) + ax * (sx * e), false);
            }
            foreach (var p in l.chestAnchors) Point("chest", new Vector2(p.x, p.z));
            foreach (var p in l.altarAnchors) Point("altar", new Vector2(p.x, p.z));
            Point("player spawn", new Vector2(l.playerSpawn.x, l.playerSpawn.z));
            foreach (var g in l.gates)
                for (float depth = 2.4f; depth <= 4.21f; depth += 0.9f) // ArenaGates' spawn depth and spread
                    foreach (float side in new[] { -1.3f, 0f, 1.3f })
                        Point("gate mouth", new Vector2(g.position.x, g.position.z) + Forward(g.yaw) * depth + Right(g.yaw) * side);
            foreach (var p in l.pieces)
            {
                var c = new Vector2(p.position.x, p.position.z);
                if (p.model.EndsWith("SM_ColStairs")) Rect("stairs", c + Forward(p.yaw) * (-StairsDepth * 0.5f), p.yaw, StairsWidth, StairsDepth);
                else if (p.model.EndsWith("SM_ColMons_A") || p.model.EndsWith("SM_ColMons_B")) Rect("hill", c, p.yaw, HillLength, HillDepth);
                else if (p.model.EndsWith("PR_VictorStatue"))
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * Mathf.PI / 8f;
                        Point("statue", c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * StatueRadius, false);
                    }
            }
            // and no trapdoor's centre inside a hill (a door could hide wholly inside a 14 × 10 footprint)
            foreach (var p in l.pieces)
                if (p.model.EndsWith("SM_ColMons_A") || p.model.EndsWith("SM_ColMons_B"))
                    foreach (var t in Trapdoors)
                    {
                        Vector2 rel = new Vector2(t.x - p.position.x, t.y - p.position.z);
                        if (Mathf.Abs(Vector2.Dot(rel, Right(p.yaw))) < HillLength * 0.5f && Mathf.Abs(Vector2.Dot(rel, Forward(p.yaw))) < HillDepth * 0.5f)
                            Debug.LogError($"[ColosseumLayout] trapdoor at ({t.x}, {t.y}) is under a hill");
                    }
        }
    }
}
