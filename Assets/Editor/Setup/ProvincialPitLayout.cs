using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the layout of Arena 2, the Provincial Pit (docs/ARENAS.md). Rebuilt for v0.8.0 (producer: every arena
    /// bigger than the last, not every arena round): a provincial town's walled square, an elongated octagon whose wall
    /// face runs 100 × 70 m with 14 m cut corners (≈ 6,600 m², ~1.45× the Slave Pit). A stone foundation wall with
    /// tiered wooden stands on top (~400 townsfolk) and stone piers at the eight corners; four stone gates, one mid-side
    /// (north: the boss); six raised platforms (five stairs, one ramp), a central dais, a colonnade of eight pillars
    /// around it and one toward each end, twelve barricades (a chicane at each gate; the Champion's charge wrecks them),
    /// six braziers (they topple into fire) and ten spike grates (they hurt everyone). The play area is the wall line
    /// inset by PlayInset (ArenaLayoutDef.outline). Designer-owned once it exists: only the "Regenerate" menu item
    /// overwrites it. ArenaBuilder turns it into the scene. Source art: BlenderSrc/environment/provincial_pit.py.
    /// </summary>
    public static class ProvincialPitLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_ProvincialPit.asset";
        const string Env = "Environment/";
        // the wall's inner face (matches provincial_pit.py's floor and rim)
        public const float HX = 50f, HZ = 35f, Chamfer = 14f;
        const float WallWidth = 4.6f;   // the wall, stand and gate models' width
        const float WallHeight = 2.4f;  // the stands sit on the wall top
        const float PlayInset = 1.1f;   // enemies stay this far inside the wall face
        /// <summary>The player's boundary loop (P1): this far inside the wall line, just in front of the frontmost face a
        /// player could stand on (the walls' coping at 0.16, the gates' lintels at 0.28): the 2.4 m wall top, a perch out
        /// of the horde's reach, can't be reached.</summary>
        public const float LoopInset = 0.3f;
        static readonly int[] GateEdges = { 0, 2, 4, 6 }; // mid north, east, south, west: gate 0 (north) is the boss's

        [MenuItem("Gladiator/Arenas/Regenerate Provincial Pit Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        /// <summary>The wall face's corners in order around the arena (clockwise from above, north-west first).</summary>
        public static Vector2[] WallLine => new[]
        {
            new Vector2(-HX + Chamfer, HZ), new Vector2(HX - Chamfer, HZ), new Vector2(HX, HZ - Chamfer), new Vector2(HX, -HZ + Chamfer),
            new Vector2(HX - Chamfer, -HZ), new Vector2(-HX + Chamfer, -HZ), new Vector2(-HX, -HZ + Chamfer), new Vector2(-HX, HZ - Chamfer),
        };

        /// <summary>The Bookmaker's booths (Phase 5.6): at the foot of the wall on two cut corners and the west side, clear of
        /// chests, spikes and platforms.</summary>
        public static Vector3[] BookmakerSpots => new[] { new Vector3(-41.4f, 0f, 26.4f), new Vector3(41.4f, 0f, -26.4f), new Vector3(-47.7f, 0f, 12f) };

        /// <summary>Inward normal of the edge a → b (the outline runs clockwise from above).</summary>
        static Vector2 Inward(Vector2 a, Vector2 b) { var e = (b - a).normalized; return new Vector2(e.y, -e.x); }

        /// <summary>A convex outline moved `d` metres inward (each corner along its mitre).</summary>
        static Vector2[] Inset(Vector2[] poly, float d)
        {
            int n = poly.Length;
            var o = new Vector2[n];
            for (int k = 0; k < n; k++)
            {
                Vector2 n1 = Inward(poly[(k + n - 1) % n], poly[k]), n2 = Inward(poly[k], poly[(k + 1) % n]);
                o[k] = poly[k] + (n1 + n2) * (d / (1f + Vector2.Dot(n1, n2)));
            }
            return o;
        }

        static Vector3 V3(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);
        /// <summary>Yaw that turns a model's +Z toward `dir` (XZ).</summary>
        static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            var wall = WallLine;
            l.id = "provincial_pit";
            l.displayName = "The Provincial Pit";
            l.tier = 1;
            l.center = Vector3.zero;
            l.outline = Inset(wall, PlayInset);
            l.radius = HZ - PlayInset; // the biggest circle inside (the debug spawn ring)
            l.aspect = 1f;
            l.fog = new Vector2(50f, 240f);   // the far end is ~100 m away: keep it clear
            l.playerSpawn = new Vector3(0f, 0.1f, -7f);
            l.playerYaw = 0f;
            l.maxAlive = 300;
            l.spawnMaxDistance = 55f;          // the far end is up to 110 m off: the horde comes from the near side
            l.sunEuler = new Vector3(42f, 28f, 0f);
            l.enemyHpMul = 1.25f;
            l.playerDamageTakenMul = 1.15f;
            l.gateModel = Env + "SM_ProvGate";

            var pieces = new List<ArenaLayoutDef.Piece>();
            var gates = new List<ArenaLayoutDef.Gate>();
            var crowd = new List<ArenaLayoutDef.CrowdZone>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });

            Add("SM_ProvFloor", PieceRole.Walkable, Vector3.zero, 0f);
            Add("SM_ProvRim", PieceRole.Decor, Vector3.zero, 0f);

            // ---------- the wall: each edge split into equal sections (scaled to fit exactly: straight runs can't overlap
            // without z-fighting), a gate mid-edge on four of them; stands on top, banners beside the gates
            var gateMids = new List<Vector2>();
            int slot = 0;
            void Section(Vector2 centre, Vector2 n, float width)
            {
                float yaw = YawOf(n);
                var sx = new Vector3(width / WallWidth, 1f, 1f);
                Add("SM_ProvWall_4m", PieceRole.Solid, V3(centre), yaw, sx);
                bool besideGate = false;
                foreach (var g in gateMids) if ((g - centre).sqrMagnitude < 6f * 6f) besideGate = true;
                if (besideGate)
                {
                    Add("PR_Banner", PieceRole.Decor, V3(centre - n * 0.35f, WallHeight + 0.18f), yaw);
                    return;
                }
                const float standBack = 1.15f;
                Add("SM_ProvStand_4m", PieceRole.Decor, V3(centre - n * standBack, WallHeight), yaw, sx);
                for (int k = 0; k < 3; k++) // one row of townsfolk per tier
                    crowd.Add(new ArenaLayoutDef.CrowdZone
                    {
                        center = V3(centre - n * (standBack + 0.4f + k * 0.8f), WallHeight + (k + 1) * 0.45f),
                        size = new Vector2(3.8f * width / WallWidth, 0.4f), yaw = yaw, count = 2 + (slot + k) % 2,
                    });
                slot++;
            }
            void Run(Vector2 from, Vector2 dir, Vector2 n, float length)
            {
                int m = Mathf.Max(1, Mathf.RoundToInt(length / WallWidth));
                float w = length / m;
                for (int k = 0; k < m; k++) Section(from + dir * (w * (k + 0.5f)), n, w);
            }
            for (int e = 0; e < wall.Length; e++)
            {
                Vector2 a = wall[e], b = wall[(e + 1) % wall.Length];
                if (System.Array.IndexOf(GateEdges, e) >= 0) gateMids.Add((a + b) * 0.5f);
            }
            for (int e = 0; e < wall.Length; e++)
            {
                Vector2 a = wall[e], b = wall[(e + 1) % wall.Length];
                float len = (b - a).magnitude;
                Vector2 dir = (b - a) / len, n = Inward(a, b);
                if (System.Array.IndexOf(GateEdges, e) >= 0)
                {
                    float side = (len - WallWidth) * 0.5f;
                    Vector2 mid = (a + b) * 0.5f;
                    gates.Add(new ArenaLayoutDef.Gate { position = V3(mid - n * 0.6f), yaw = YawOf(n) });
                    // P1 ("the arenas must remain closed"): a dark passage ending in bars behind the door, not the town
                    Add("SM_ProvGateTunnel", PieceRole.Decor, V3(mid - n * 0.6f), YawOf(n));
                    Run(a, dir, n, side);
                    Run(mid + dir * (WallWidth * 0.5f), dir, n, side);
                }
                else Run(a, dir, n, len);
            }
            // the corners: a stone pier over each joint, and a narrow stand filling the gap the bend leaves behind it
            for (int k = 0; k < wall.Length; k++)
            {
                Vector2 n1 = Inward(wall[(k + wall.Length - 1) % wall.Length], wall[k]), n2 = Inward(wall[k], wall[(k + 1) % wall.Length]);
                Vector2 inward = (n1 + n2).normalized;
                float yaw = YawOf(inward);
                Add("SM_ProvPillar", PieceRole.Solid, V3(wall[k] - inward * 0.45f), yaw);
                Add("SM_ProvStand_4m", PieceRole.Decor, V3(wall[k] - inward * 1.25f, WallHeight), yaw, new Vector3(0.45f, 1f, 1f));
            }

            // ---------- vertical variety: stone platforms facing the floor, with stairs (or the ramp) on their +Z side
            void Platform(Vector2 pos, float yaw, string climb)
            {
                Add("SM_ProvPlatform", PieceRole.Walkable, V3(pos), yaw);
                // the climb's origin is its low front edge and it rises toward its local -Z, onto the platform's edge
                // (2 m from its centre): stairs are 2 m long, the ramp 4 m
                var f = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
                Add(climb, PieceRole.Walkable, V3(pos + f * (climb == "SM_Stairs" ? 4f : 6f)), yaw);
            }
            Platform(new Vector2(-26f, 24.5f), 180f, "SM_Stairs");
            Platform(new Vector2(26f, 24.5f), 180f, "SM_Stairs");
            Platform(new Vector2(-26f, -24.5f), 0f, "SM_Ramp");
            Platform(new Vector2(26f, -24.5f), 0f, "SM_Stairs");
            Platform(new Vector2(-37f, -11f), 90f, "SM_Stairs");
            Platform(new Vector2(37f, 11f), 270f, "SM_Stairs");
            Add("SM_ProvDais", PieceRole.Walkable, Vector3.zero, 0f);

            // pillars: a colonnade either side of the dais, and one toward each end (in the east/west gate lanes)
            foreach (float x in new[] { -18f, -6f, 6f, 18f })
                foreach (float z in new[] { -10f, 10f })
                    Add("SM_ProvPillar", PieceRole.Solid, new Vector3(x, 0f, z), 0f);
            Add("SM_ProvPillar", PieceRole.Solid, new Vector3(-31f, 0f, 0f), 90f);
            Add("SM_ProvPillar", PieceRole.Solid, new Vector3(31f, 0f, 0f), 90f);

            // the arena staff's clutter against the walls, never in a gate lane: weapon racks beside the gates, crates and
            // barrels along the long sides, sacks at the ends
            void Prop(string model, float x, float z, float yaw) => Add(model, PieceRole.Prop, new Vector3(x, 0f, z), yaw);
            Prop("PR_WeaponRack", -7.5f, 33.4f, 180f);
            Prop("PR_WeaponRack", 7.5f, -33.4f, 0f);
            Prop("PR_WeaponRack", 48.4f, -7.5f, 270f);
            Prop("PR_WeaponRack", -48.4f, 7.5f, 90f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Prop("PR_Crate", 19f * sx, 33.2f, 8f);
                Prop("PR_Barrel", 17.4f * sx, 33.5f, 0f);
                Prop("PR_Crate", -19f * sx, -33.2f, -6f);
                Prop("PR_Barrel", -20.6f * sx, -33.5f, 0f);
            }
            Prop("PR_Sacks", 48.3f, 14f, 90f);
            Prop("PR_Sacks", -48.3f, -14f, 270f);

            l.pieces = pieces.ToArray();
            l.gates = gates.ToArray();
            l.boundaryLoop = Inset(wall, LoopInset);
            l.boundary = System.Array.Empty<Vector4>();
            l.crowd = crowd.ToArray();
            l.zones = new ArenaLayoutDef.Zone[0];

            // barricades (x, y, z, yaw; long axis along the local X): a chicane in front of each gate, four on the floor
            l.barricades = new[]
            {
                new Vector4(-4f, 0f, 27.5f, 0f), new Vector4(4f, 0f, 24f, 0f),       // north gate
                new Vector4(4f, 0f, -27.5f, 0f), new Vector4(-4f, 0f, -24f, 0f),     // south gate
                new Vector4(42.5f, 0f, 4f, 90f), new Vector4(39f, 0f, -4f, 90f),     // east gate
                new Vector4(-42.5f, 0f, -4f, 90f), new Vector4(-39f, 0f, 4f, 90f),   // west gate
                new Vector4(-24f, 0f, 3f, 90f), new Vector4(24f, 0f, -3f, 90f), new Vector4(-11f, 0f, -19f, 0f), new Vector4(11f, 0f, 19f, 0f),
            };
            // braziers: on the four corner platforms (behind their middle) and either side of the north gate
            l.braziers = new[]
            {
                new Vector3(-26f, 1.2f, 25.6f), new Vector3(26f, 1.2f, 25.6f), new Vector3(-26f, 1.2f, -25.6f), new Vector3(26f, 1.2f, -25.6f),
                new Vector3(-5f, 0f, 32.6f), new Vector3(5f, 0f, 32.6f),
            };
            l.spikeTraps = new[]
            {
                new Vector3(0f, 0f, 20.5f), new Vector3(0f, 0f, -20.5f), new Vector3(35f, 0f, 0f), new Vector3(-35f, 0f, 0f), // the gate lanes: the horde walks over them
                new Vector3(-31f, 0f, 13f), new Vector3(31f, 0f, -13f), new Vector3(-14f, 0f, 27f), new Vector3(14f, 0f, -27f),
                new Vector3(-42f, 0f, -22f), new Vector3(42f, 0f, 22f),
            };

            l.chestAnchors = new[]
            {
                new Vector3(-8f, 0f, 4f), new Vector3(8f, 0f, -4f), new Vector3(0f, 0f, 15f), new Vector3(0f, 0f, -15f),
                new Vector3(-20f, 0f, -15f), new Vector3(20f, 0f, 15f), new Vector3(-34f, 0f, 22f), new Vector3(34f, 0f, -22f),
                new Vector3(-44f, 0f, 8f), new Vector3(44f, 0f, -8f), new Vector3(-13f, 0f, -31f), new Vector3(13f, 0f, 31f),
                new Vector3(-30f, 0f, -31f), new Vector3(30f, 0f, 31f),
            };
            l.altarAnchors = new[] { new Vector3(-26f, 1.2f, 23.8f), new Vector3(26f, 1.2f, 23.8f), new Vector3(36.3f, 1.2f, 11f) }; // up on platforms
            l.secretSpots = BookmakerSpots;
            l.secrets = ArenaSecrets.Bookmaker;

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }
    }
}
