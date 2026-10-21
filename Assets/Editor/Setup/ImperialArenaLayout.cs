using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the layout of Arena 5, the Imperial Arena (docs/ARENAS.md §5): a hippodrome along X, straight sides at
    /// z = ±44 from the carceres (x = -95, the chariots' starting stalls) to x = 51, then the sphendone, a half circle of
    /// radius 44 round (51, 0) whose apex is flattened into a 21 m chord for the triumphal gate (the boss's). ≈ 15,300 m²
    /// of play, 1.31× the Grand Arena. Every long side and the sphendone carry stands (decor, kept solid by invisible
    /// stone; curved sections round the sphendone), four side gates cut the long sides, the pulvinar the north one. Down
    /// the middle the spina (a 1.2 m walkway with stairs, the obelisk, the lap counter, a meta at each end) and round it
    /// the chariot track (ChariotRace). Four terraces against the long walls. Designer-owned once it exists: only the
    /// "Regenerate" menu item overwrites it. Source art: BlenderSrc/environment/imperial_arena.py
    /// (docs/tracks/R1-env-art.md, whose dimensions these must match).
    /// </summary>
    public static class ImperialArenaLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_ImperialArena.asset";
        const string Env = "Environment/";
        /// <summary>The wall line: the carceres' x, the sphendone's centre x, the long sides' |z| (= its radius).</summary>
        public const float WestX = -95f, SphendoneX = 51f, HalfZ = 44f;
        /// <summary>Half the triumphal gate's width: the sphendone's apex is cut into a chord this far either side of
        /// z = 0, at x = 51 + √(44² − 10.5²) = 93.73, and the gate stands on it.</summary>
        public const float TriumphHalf = 10.5f;
        /// <summary>Wall-line edges (and curved stand sections) on each side of the sphendone, between a long side and
        /// the triumphal gate: 3.90 m chords, scale 0.975.</summary>
        public const int ArcSections = 15;
        const float PlayInset = 1.1f;    // enemies stay this far inside the wall face
        /// <summary>The spina: from x0 to x1 along z = 0, 6 m wide, walkable at 1.2 m, in 10 m segments.</summary>
        public const float SpinaX0 = -60f, SpinaX1 = 40f, SpinaHalfWidth = 3f, SpinaHeight = 1.2f, SpinaSegment = 10f;
        /// <summary>The chariot track: the turns' radius round the metae (the spina's ends), the lane's width, and the x
        /// inside the carceres' open bays where the chariots appear and vanish (env-art: wholly inside the tunnel).</summary>
        public const float TrackRadius = 14f, TrackWidth = 4f, StallX = -98f;
        /// <summary>Nothing may stand within the lane + this (m): no chest, altar, brazier, gate mouth or spawn.</summary>
        public const float TrackClear = 1.5f;
        public const float ObeliskX = -10f, LapCounterX = 20f, PulvinarX = -10f;
        /// <summary>Half the side gate's width (SM_ImpGate) and the pulvinar's (SM_Pulvinar): they cut the stand runs.</summary>
        public const float GateHalf = 5.5f, PulvinarHalf = 7f;
        public static readonly float[] SideGateX = { -50f, 30f };
        public static readonly float[] SpinaStairsX = { -45f, -20f, 5f, 30f };
        const float StairsZ = 5f;        // SM_SpinaStairs: origin 2 m out from the walkway's side, rising toward it
        const float CarceresBay = 4f;    // SM_Carceres: 22 bays along the west wall
        const int CarceresBays = 22;
        const float TerraceStairs = 4.5f; // SM_ImpTerrace: its stairs' origin in front of its centre
        const float StandWidth = 4f;     // SM_ImpStand_4m / SM_ImpStand_Curve (the chord)
        const float Podium = 3.2f, PodiumCap = 0.6f, TierRise = 0.45f, TierRun = 0.8f; // GrandArenaLayout's profile
        const int Tiers = 8;
        const float CrowdPerMetre = 0.75f; // per tier (the Grand Arena: 0.6)

        /// <summary>Edge indices of WallLine(): the north side, the apex chord (the triumphal gate), the south side and
        /// the west wall (the carceres); the curved chords lie between them.</summary>
        public const int NorthEdge = 0, ApexEdge = 1 + ArcSections, SouthEdge = 2 + 2 * ArcSections, WestEdge = 3 + 2 * ArcSections;

        [MenuItem("Gladiator/Arenas/Regenerate Imperial Arena Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        /// <summary>The wall line, clockwise from above: from (-95, 44) east to (51, 44), round the sphendone to the
        /// triumphal gate's chord at x = 93.73, down it, round to (51, -44), west to (-95, -44). 34 corners.</summary>
        public static Vector2[] WallLine()
        {
            var o = new List<Vector2> { new Vector2(WestX, HalfZ), new Vector2(SphendoneX, HalfZ) };
            float apex = Mathf.Asin(TriumphHalf / HalfZ);
            for (int k = 1; k <= ArcSections; k++) o.Add(Arc(90f * Mathf.Deg2Rad - (90f * Mathf.Deg2Rad - apex) * k / ArcSections));
            for (int k = 0; k <= ArcSections; k++) o.Add(Arc(-apex - (90f * Mathf.Deg2Rad - apex) * k / ArcSections));
            o.Add(new Vector2(WestX, -HalfZ));
            return o.ToArray();
        }

        static Vector2 Arc(float a) => new Vector2(SphendoneX + HalfZ * Mathf.Cos(a), HalfZ * Mathf.Sin(a));

        public static Vector2[] Outline() => GrandArenaLayout.Inset(WallLine(), PlayInset);

        /// <summary>Distance (XZ) from `p` to the track's centre line: the two straights at z = ∓14, run-in and
        /// run-out included (from the stalls), and the half circles round the metae.</summary>
        public static float TrackOffset(Vector2 p)
        {
            float x = Mathf.Clamp(p.x, StallX, SpinaX1);
            float best = Mathf.Min(Vector2.Distance(p, new Vector2(x, -TrackRadius)), Vector2.Distance(p, new Vector2(x, TrackRadius)));
            if (p.x >= SpinaX1) best = Mathf.Min(best, Mathf.Abs(Vector2.Distance(p, new Vector2(SpinaX1, 0f)) - TrackRadius));
            if (p.x <= SpinaX0) best = Mathf.Min(best, Mathf.Abs(Vector2.Distance(p, new Vector2(SpinaX0, 0f)) - TrackRadius));
            return best;
        }

        /// <summary>True if `p` (XZ) is on the lane grown by `pad` metres.</summary>
        public static bool OnTrack(Vector3 p, float pad) => TrackOffset(new Vector2(p.x, p.z)) <= TrackWidth * 0.5f + pad;

        static Vector3 V3(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);
        /// <summary>Yaw that turns a model's +Z toward `dir` (XZ).</summary>
        static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        static Vector2 Forward(float yaw) => new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            l.id = "imperial_arena";
            l.displayName = "The Imperial Arena";
            l.tier = 4;
            l.center = Vector3.zero;           // on the spina, between the obelisk and the lap counter: it sees every edge
            l.outline = Outline();
            l.radius = HalfZ - PlayInset;      // the biggest circle round the centre (the debug spawn ring)
            l.aspect = 1f;
            l.fog = new Vector2(80f, 330f);    // the triumphal gate is ~190 m from the carceres
            l.playerSpawn = new Vector3(-10f, 0.1f, -26f);
            l.playerYaw = 0f;                  // facing north: the track, the obelisk and the pulvinar beyond
            l.maxAlive = 400;
            l.densityMul = 1.25f;              // the biggest floor so far
            l.spawnMaxDistance = 55f;          // the horde comes from your end, not from 190 m away
            l.eliteEvery = 60f;
            l.sunEuler = new Vector3(45f, -35f, 0f);
            l.enemyHpMul = 2.15f;
            l.playerDamageTakenMul = 1.45f;
            l.gateModel = Env + "SM_ImpGate";
            l.secrets = ArenaSecrets.None;
            l.secretSpots = new Vector3[0];
            l.guardSpots = new Vector3[0];     // the Emperor's Champion calls his Guard through the gates nearest you

            // the chariot races (ChariotRace): counter-clockwise round the spina, out of and back into the carceres
            l.trackRadius = TrackRadius;
            l.trackWestMeta = new Vector2(SpinaX0, 0f);
            l.trackEastMeta = new Vector2(SpinaX1, 0f);
            l.trackWidth = TrackWidth;
            l.trackStallX = StallX;
            l.chariotFirst = 80f;
            l.chariotEvery = 60f;
            l.chariotTelegraph = 3f;
            l.chariotSpeed = 22f;
            l.chariotCount = 2;
            l.chariotLaps = 2;
            l.chariotModel = "Props/PR_Chariot";

            var pieces = new List<ArenaLayoutDef.Piece>();
            var gates = new List<ArenaLayoutDef.Gate>();
            var crowd = new List<ArenaLayoutDef.CrowdZone>();
            var stone = new List<Vector4>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });

            Add("SM_ImpFloor", PieceRole.Walkable, Vector3.zero, 0f); // the darker track lane and its run-ins are in it

            // ---------- the walls: every edge of the wall line is invisible stone (and a player-only wall behind it);
            // the long sides carry straight stand sections (scaled to fit each run between the gates and the pulvinar),
            // the sphendone curved ones (one per chord), the apex chord the triumphal gate, the west wall the carceres
            float carry = 0f;
            void Seats(Vector2 a, Vector2 b, Vector2 inward, float curve) // curve: the arc's radius (0 = straight)
            {
                for (int k = 0; k < Tiers; k++)
                {
                    float off = PodiumCap + TierRun * (k + 0.5f);
                    float len = (b - a).magnitude * (curve > 0f ? (curve + off) / curve : 1f); // a tier further back is longer round the curve
                    carry += len * CrowdPerMetre;
                    int n = Mathf.FloorToInt(carry);
                    carry -= n;
                    if (n <= 0) continue;
                    crowd.Add(new ArenaLayoutDef.CrowdZone
                    {
                        center = V3((a + b) * 0.5f - inward * off, Podium + TierRise * (k + 1)),
                        size = new Vector2(len * 0.9f, 0.4f), yaw = YawOf(inward), count = n,
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
                    Add("SM_ImpStand_4m", PieceRole.Decor, V3(a + dir * (w * (i + 0.5f))), YawOf(inward), new Vector3(w / StandWidth, 1f, 1f));
                Seats(a, b, inward, 0f);
            }
            // a long side from a to b (clockwise), stands everywhere but the cuts (x centre, half width)
            void Side(Vector2 a, Vector2 b, params Vector2[] cuts)
            {
                var sorted = new List<Vector2>(cuts);
                sorted.Sort((p, q) => Mathf.Abs(p.x - a.x).CompareTo(Mathf.Abs(q.x - a.x))); // in the run's order
                Vector2 dir = (b - a).normalized, from = a;
                foreach (var c in sorted)
                {
                    Vector2 mid = new Vector2(c.x, a.y);
                    Stands(from, mid - dir * c.y);
                    from = mid + dir * c.y;
                }
                Stands(from, b);
            }

            var line = WallLine();
            for (int i = 0; i < line.Length; i++)
            {
                Vector2 a = line[i], b = line[(i + 1) % line.Length];
                Vector2 dir = (b - a).normalized, inward = new Vector2(dir.y, -dir.x);
                float len = (b - a).magnitude;
                stone.Add(new Vector4((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, YawOf(inward), len + 0.3f));
                if (i == NorthEdge)
                    Side(a, b, new Vector2(SideGateX[0], GateHalf), new Vector2(PulvinarX, PulvinarHalf), new Vector2(SideGateX[1], GateHalf));
                else if (i == SouthEdge)
                    Side(a, b, new Vector2(SideGateX[0], GateHalf), new Vector2(SideGateX[1], GateHalf));
                else if (i == WestEdge)
                {
                    // the carceres: 22 stall bays facing east; the two on the track's straights (z = ±14) are open tunnels
                    for (int k = 0; k < CarceresBays; k++)
                    {
                        float z = -HalfZ + CarceresBay * (k + 0.5f);
                        bool open = Mathf.Abs(Mathf.Abs(z) - TrackRadius) < 0.5f;
                        Add(open ? "SM_CarceresOpen" : "SM_Carceres", PieceRole.Decor, new Vector3(WestX, 0f, z), 90f);
                    }
                    // towers close the carceres' corners (behind both wall lines)
                    Add("SM_ImpTower", PieceRole.Decor, new Vector3(WestX - 4f, 0f, HalfZ + 4f), 90f);
                    Add("SM_ImpTower", PieceRole.Decor, new Vector3(WestX - 4f, 0f, -HalfZ - 4f), 90f);
                }
                else if (i != ApexEdge)
                {
                    // the sphendone: one curved section per chord, at the chord's middle, facing the curve's centre
                    Add("SM_ImpStand_Curve", PieceRole.Decor, V3((a + b) * 0.5f), YawOf(inward), new Vector3(len / StandWidth, 1f, 1f));
                    Seats(a, b, inward, HalfZ);
                }
            }
            // the gates: [0] the triumphal gate on the apex chord (the boss's, the horde's too), then the side gates
            Vector2 apex = (line[ApexEdge] + line[ApexEdge + 1]) * 0.5f;
            gates.Add(new ArenaLayoutDef.Gate { position = V3(apex), yaw = -90f, model = Env + "SM_TriumphalGate" });
            foreach (float x in SideGateX) gates.Add(new ArenaLayoutDef.Gate { position = new Vector3(x, 0f, HalfZ), yaw = 180f });
            foreach (float x in SideGateX) gates.Add(new ArenaLayoutDef.Gate { position = new Vector3(x, 0f, -HalfZ), yaw = 0f });

            // the pulvinar: the imperial box in the north wall, its senators on the loge (4.2 m) and the Emperor at his throne
            Add("SM_Pulvinar", PieceRole.Decor, new Vector3(PulvinarX, 0f, HalfZ), 180f);
            crowd.Add(new ArenaLayoutDef.CrowdZone { center = new Vector3(PulvinarX, 4.2f, HalfZ + 2.1f), size = new Vector2(12f, 2.6f), yaw = 180f, count = 16 });
            crowd.Add(new ArenaLayoutDef.CrowdZone { center = new Vector3(PulvinarX, 4.2f, HalfZ + 3.85f), size = new Vector2(0.4f, 0.4f), yaw = 180f, count = 1 });
            l.solidWalls = stone.ToArray();
            l.solidWallHeight = 8.4f;
            // the player's boundary (P1): one closed loop on the stone's face (no gate has a ledge in reach)
            l.boundaryLoop = WallLine();
            l.boundary = System.Array.Empty<Vector4>();

            // ---------- the spina: ten walkway segments, a meta at each end (three gold cones: they block), the obelisk
            // (blocks movement, shots and sight), the lap counter, and stairs on both long sides
            for (int k = 0; k < Mathf.RoundToInt((SpinaX1 - SpinaX0) / SpinaSegment); k++)
                Add("SM_Spina_10m", PieceRole.Walkable, new Vector3(SpinaX0 + SpinaSegment * (k + 0.5f), 0f, 0f), 0f);
            Add("SM_SpinaEnd", PieceRole.Solid, new Vector3(SpinaX0, 0f, 0f), -90f); // the round side faces the turn
            Add("SM_SpinaEnd", PieceRole.Solid, new Vector3(SpinaX1, 0f, 0f), 90f);
            Add("SM_Obelisk", PieceRole.Solid, new Vector3(ObeliskX, SpinaHeight, 0f), 0f);
            Add("PR_LapCounter", PieceRole.Prop, new Vector3(LapCounterX, SpinaHeight, 0f), 0f); // ChariotRace finds it by name
            foreach (float x in SpinaStairsX)
            {
                Add("SM_SpinaStairs", PieceRole.Walkable, new Vector3(x, 0f, StairsZ), 0f);   // north side, rising south
                Add("SM_SpinaStairs", PieceRole.Walkable, new Vector3(x, 0f, -StairsZ), 180f); // south side, rising north
            }

            // ---------- four travertine terraces against the long walls (backs on the wall line), stairs on their track side
            var terraces = new[] { new Vector3(-75f, 180f, 41.5f), new Vector3(10f, 180f, 41.5f), new Vector3(-80f, 0f, -41.5f), new Vector3(0f, 0f, -41.5f) };
            foreach (var t in terraces) // (x, yaw, z)
            {
                var pos = new Vector2(t.x, t.z);
                Add("SM_ImpTerrace", PieceRole.Walkable, V3(pos), t.y);
                Add("SM_SpinaStairs", PieceRole.Walkable, V3(pos + Forward(t.y) * TerraceStairs), t.y);
            }

            l.pieces = pieces.ToArray();
            l.gates = gates.ToArray();
            l.bossGate = 0; // the triumphal gate
            l.crowd = crowd.ToArray();
            l.zones = new ArenaLayoutDef.Zone[0];
            l.barricades = new Vector4[0];
            l.spikeTraps = new Vector3[0];
            l.cages = new Vector4[0];
            l.fireJets = new Vector4[0];
            l.bladeSweepers = new Vector3[0];
            // braziers flank the obelisk on the spina, and the triumphal gate
            l.braziers = new[]
            {
                new Vector3(-16f, SpinaHeight, 2.2f), new Vector3(-16f, SpinaHeight, -2.2f), new Vector3(-4f, SpinaHeight, 2.2f), new Vector3(-4f, SpinaHeight, -2.2f),
                new Vector3(88f, 0f, 6f), new Vector3(88f, 0f, -6f),
            };
            // 14 chests, all off the track: the spina top, the inner strips, the outer fields, the sphendone, the west end
            l.chestAnchors = new[]
            {
                new Vector3(-35f, SpinaHeight, 0f), new Vector3(5f, SpinaHeight, 0f),
                new Vector3(-35f, 0f, 7.5f), new Vector3(15f, 0f, -7.5f),
                new Vector3(-70f, 0f, 28f), new Vector3(-20f, 0f, 30f), new Vector3(25f, 0f, 28f),
                new Vector3(-45f, 0f, -28f), new Vector3(0f, 0f, -30f), new Vector3(45f, 0f, -28f),
                new Vector3(72f, 0f, 22f), new Vector3(72f, 0f, -22f),
                new Vector3(-85f, 0f, 30f), new Vector3(-85f, 0f, -30f),
            };
            l.altarAnchors = new[] { new Vector3(-25f, SpinaHeight, 0f), new Vector3(66f, 0f, 0f), new Vector3(-82f, 0f, 0f) };

            CheckTrackClear(l);
            int people = 0;
            foreach (var c in l.crowd) people += c.count;
            Debug.Log($"[ImperialArenaLayout] play area {l.PlayArea():0} m² ({l.PlayArea() / 11674f:0.00}× the Grand Arena's 11,674), " +
                      $"{l.pieces.Length} pieces, {l.gates.Length} gates, crowd {people}");

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }

        /// <summary>Nothing stands on the track: logs every anchor, brazier, gate mouth or the player spawn within the
        /// lane (run-in and run-out included) + TrackClear.</summary>
        static void CheckTrackClear(ArenaLayoutDef l)
        {
            void Check(string what, Vector3 p)
            {
                if (OnTrack(p, TrackClear)) Debug.LogError($"[ImperialArenaLayout] {what} at {p} is on the chariot track (lane + {TrackClear} m)");
            }
            foreach (var p in l.chestAnchors) Check("chest", p);
            foreach (var p in l.altarAnchors) Check("altar", p);
            foreach (var p in l.braziers) Check("brazier", p);
            foreach (var g in l.gates) Check("gate mouth", g.position + V3(Forward(g.yaw)) * 3.3f);
            Check("player spawn", l.playerSpawn);
        }
    }
}
