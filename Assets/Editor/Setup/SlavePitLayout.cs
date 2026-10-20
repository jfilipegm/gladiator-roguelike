using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the layout of Arena 1, the Slave Pit. Playtest rebuild (producer: "twice as wide", "a rough enclosure,
    /// not a wooden bowl", "people actually work and train there", "it shouldn't float"): an 84 × 70 m oval stockade on
    /// open ground. The boundary is 48 sections around the oval: log palisade runs with three log gates, patched
    /// fences, dirt embankments with wattle on top, and plank platforms of onlookers. Invisible walls behind them keep
    /// the player in whatever their air jumps. Inside: a drill yard of training posts and straw dummies, a sparring
    /// ring, a work corner (trough, barrow, hay, sacks), a woodpile and a tool shed, cages by the south wall, four earth
    /// terraces with ramps, mounds, mud, fire pits. Outside, never reachable: grassland rising into hills, woods,
    /// the slaves' huts, the slavers' longhouses and tents, watchtowers, carts on the roads from the gates.
    /// The asset is designer-owned once it exists: this only overwrites it from the "Regenerate" menu item.
    /// ArenaBuilder turns it into the scene. Source art: BlenderSrc/environment/slave_pit_yard.py.
    /// </summary>
    public static class SlavePitLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_SlavePit.asset";
        const string Env = "Environment/";
        const float RX = 42f, RZ = 35f;           // the boundary's inner line (matches slave_pit_yard.py)
        const int Sections = 48;                  // 7.5 degrees apart
        const float SectionWidth = 5f;            // the models' width
        static readonly int[] GateSections = { 0, 17, 31 }; // north, south-east, south-west

        enum Kind { Palisade, Fence, Berm, Platform, Gate }

        static Kind KindOf(int i)
        {
            if (System.Array.IndexOf(GateSections, i) >= 0) return Kind.Gate;
            if (i >= 3 && i <= 6 || i >= 21 && i <= 23 || i >= 38 && i <= 41) return Kind.Platform;
            if (i >= 7 && i <= 11 || i >= 24 && i <= 28 || i >= 42 && i <= 45) return Kind.Berm;
            if (i >= 12 && i <= 14 || i == 20 || i >= 34 && i <= 37) return Kind.Fence;
            return Kind.Palisade;
        }

        [MenuItem("Gladiator/Arenas/Regenerate Slave Pit Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        // ---------- the oval ----------
        /// <summary>Point on the boundary line at `bearing` (degrees, clockwise from +Z).</summary>
        static Vector3 Edge(float bearing)
        {
            float a = bearing * Mathf.Deg2Rad;
            return new Vector3(RX * Mathf.Sin(a), 0f, RZ * Mathf.Cos(a));
        }

        /// <summary>Outward normal of the oval at `bearing`.</summary>
        static Vector3 Out(float bearing)
        {
            float a = bearing * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) / RX, 0f, Mathf.Cos(a) / RZ).normalized;
        }

        static float InwardYaw(float bearing) { var n = Out(bearing); return Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg; }

        /// <summary>`d` metres inside the boundary line (negative: outside).</summary>
        static Vector3 Inside(float bearing, float d) => Edge(bearing) - Out(bearing) * d;

        /// <summary>Length of the boundary covered by one section at `bearing`.</summary>
        static float SectionLength(float bearing)
        {
            float a = bearing * Mathf.Deg2Rad;
            return Mathf.Sqrt(Mathf.Pow(RX * Mathf.Cos(a), 2f) + Mathf.Pow(RZ * Mathf.Sin(a), 2f)) * (2f * Mathf.PI / Sections);
        }

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            l.id = "slave_pit";
            l.displayName = "The Slave Pit";
            l.center = Vector3.zero;
            l.radius = RX - 1f;                    // enemies stay a metre inside the boundary line
            l.aspect = (RZ - 1f) / (RX - 1f);
            l.fog = new Vector2(45f, 230f);        // the far side is 70-80 m away: keep it clear; the hills fade
            l.playerSpawn = new Vector3(0f, 0.1f, -12f);
            l.playerYaw = 0f;
            l.maxAlive = 300;
            l.sunEuler = new Vector3(36f, -40f, 0f);

            var pieces = new List<ArenaLayoutDef.Piece>();
            var gates = new List<ArenaLayoutDef.Gate>();
            var boundary = new List<Vector4>();
            var crowd = new List<ArenaLayoutDef.CrowdZone>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });

            Add("SM_PitYard", PieceRole.Walkable, Vector3.zero, 0f);
            Add("SM_PitSurrounds", PieceRole.Decor, Vector3.zero, 0f);

            // ---------- the boundary ----------
            for (int i = 0; i < Sections; i++)
            {
                float bearing = i * 360f / Sections;
                var n = Out(bearing);
                float yaw = InwardYaw(bearing);
                float len = SectionLength(bearing);
                var widen = new Vector3(len / SectionWidth * 1.06f, 1f, 1f); // neighbours overlap a little: no gaps
                var kind = KindOf(i);
                float wall;  // where the invisible wall stands, outward from the line
                switch (kind)
                {
                    case Kind.Gate:
                        gates.Add(new ArenaLayoutDef.Gate { position = Edge(bearing) + n * 0.3f, yaw = yaw });
                        wall = 1.3f;
                        break;
                    case Kind.Palisade:
                        Add("SM_YardPalisade", PieceRole.Solid, Edge(bearing) + n * 0.3f, yaw, widen);
                        wall = 1.0f;
                        break;
                    case Kind.Fence:
                        Add("SM_YardFence", PieceRole.Solid, Edge(bearing) + n * 0.8f, yaw, widen);
                        wall = 1.1f;
                        // locals leaning on the fence from outside
                        crowd.Add(new ArenaLayoutDef.CrowdZone { center = Edge(bearing) + n * 1.9f + Vector3.up * 0.25f, size = new Vector2(3.6f, 0.8f), yaw = yaw, count = 3 });
                        break;
                    case Kind.Berm:
                        Add("SM_YardBerm", PieceRole.Walkable, Edge(bearing), yaw, widen);
                        wall = 2.3f; // just behind the wattle on the crest
                        if (i % 2 == 0)
                            crowd.Add(new ArenaLayoutDef.CrowdZone { center = Edge(bearing) + n * 2.6f + Vector3.up * 1.95f, size = new Vector2(3.6f, 0.6f), yaw = yaw, count = 3 });
                        break;
                    default:
                        Add(i % 2 == 0 ? "SM_YardPlatform" : "SM_YardPlatformB", PieceRole.Solid, Edge(bearing) + n * 0.4f, yaw, widen);
                        wall = 0.6f;
                        crowd.Add(new ArenaLayoutDef.CrowdZone { center = Edge(bearing) + n * 1.95f + Vector3.up * 2.4f, size = new Vector2(4.2f, 2.0f), yaw = yaw, count = 6 });
                        break;
                }
                var w = Edge(bearing) + n * wall;
                boundary.Add(new Vector4(w.x, w.z, yaw, len * 1.25f));
            }

            // ---------- the yard ----------
            // vertical variety: earth terraces with a ramp down toward the middle
            void Terrace(Vector3 at, float yaw)
            {
                Add("SM_Terrace_A", PieceRole.Walkable, at, yaw);
                var toCentre = (-at).normalized;
                var top = at + toCentre * 2.4f;               // the ramp's high end meets the terrace top edge
                var low = top + toCentre * 5f;                // SM_DirtRamp is 5 m long, rising toward its -Z
                Add("SM_DirtRamp", PieceRole.Walkable, low, Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg);
            }
            Terrace(new Vector3(-22f, 0f, 16f), 20f);
            Terrace(new Vector3(12f, 0f, 25f), 150f);
            Terrace(new Vector3(20f, 0f, -18f), -30f);
            Terrace(new Vector3(-30f, 0f, -14f), 40f);
            Add("SM_Mound_A", PieceRole.Walkable, new Vector3(-8f, 0f, 12f), 15f);
            Add("SM_Mound_B", PieceRole.Walkable, new Vector3(9f, 0f, -6f), 40f);
            Add("SM_Mound_A", PieceRole.Walkable, new Vector3(-12f, 0f, 2f), 70f, new Vector3(1.2f, 0.7f, 1.2f));
            Add("SM_Mound_B", PieceRole.Walkable, new Vector3(4f, 0f, -28f), 120f, new Vector3(0.9f, 0.8f, 0.9f));

            // the drill yard (north-east): posts to hack at, dummies, the practice rack, a bench
            Add("PR_SparringRing", PieceRole.Decor, new Vector3(0f, 0f, 4f), 0f);
            Add("PR_LogBench", PieceRole.Prop, new Vector3(5.5f, 0f, 9.5f), 30f);
            Add("PR_TrainingPost", PieceRole.Prop, new Vector3(13.5f, 0f, 11f), 10f);
            Add("PR_TrainingPost", PieceRole.Prop, new Vector3(17f, 0f, 10f), -15f);
            Add("PR_TrainingPost", PieceRole.Prop, new Vector3(20.5f, 0f, 12f), 25f);
            Add("PR_StrawDummy", PieceRole.Prop, new Vector3(15f, 0f, 16f), 200f);
            Add("PR_StrawDummy", PieceRole.Prop, new Vector3(19.5f, 0f, 17f), 220f);
            Add("PR_WeaponRack", PieceRole.Prop, new Vector3(23.5f, 0f, 19.5f), 220f);
            Add("PR_LogBench", PieceRole.Prop, new Vector3(11f, 0f, 16.5f), 70f);
            // the west drill circle
            Add("PR_TrainingPost", PieceRole.Prop, new Vector3(-20f, 0f, -8f), 0f);
            Add("PR_StrawDummy", PieceRole.Prop, new Vector3(-23.5f, 0f, -5.5f), 120f);
            Add("PR_StrawDummy", PieceRole.Prop, new Vector3(-17f, 0f, -11f), 300f);
            // the work corner (east): water, barrow, hay, sacks
            Add("PR_Trough", PieceRole.Prop, new Vector3(31f, 0f, 3f), 80f);
            Add("PR_Wheelbarrow", PieceRole.Prop, new Vector3(27.5f, 0f, -2f), 30f);
            Add("PR_HayBales", PieceRole.Prop, new Vector3(33f, 0f, -4.5f), 100f);
            Add("PR_Sacks", PieceRole.Prop, new Vector3(30f, 0f, 8f), 10f);
            Add("PR_Barrel", PieceRole.Prop, new Vector3(33.5f, 0f, 6.5f), 0f);
            // woodpile and the tool shed (west)
            Add("PR_ChoppingBlock", PieceRole.Prop, new Vector3(-30f, 0f, 6f), 20f);
            Add("PR_Shed", PieceRole.Solid, Inside(266f, 1.9f), InwardYaw(266f));
            Add("PR_Wheelbarrow", PieceRole.Prop, new Vector3(-33f, 0f, 1f), 140f);
            // where the slaves are kept (south wall): cages and chain posts
            Add("PR_Cage", PieceRole.Prop, Inside(162f, 3.6f), InwardYaw(162f) + 15f);
            Add("PR_Cage", PieceRole.Prop, Inside(177f, 3.4f), InwardYaw(177f) - 10f);
            Add("PR_Cage", PieceRole.Prop, Inside(196f, 3.6f), InwardYaw(196f) + 5f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(6f, 0f, -24f), 120f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(-7f, 0f, -23f), 30f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(-28f, 0f, -20f), 210f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(26f, 0f, 22f), 0f);
            // crates and barrels by the gates
            Add("PR_Crate", PieceRole.Prop, Inside(10f, 3.2f), 12f);
            Add("PR_Crate", PieceRole.Prop, Inside(121f, 3.2f), -20f);
            Add("PR_Barrel", PieceRole.Prop, Inside(116f, 2.8f), 0f);
            Add("PR_Crate", PieceRole.Prop, Inside(240f, 3.2f), 35f);
            Add("PR_Barrel", PieceRole.Prop, Inside(245f, 2.6f), 0f);
            Add("PR_Sacks", PieceRole.Prop, Inside(354f, 3.0f), 60f);
            // braziers by the platforms and gates
            foreach (float b in new[] { 32f, 100f, 207f, 300f })
                Add("PR_Brazier", PieceRole.Prop, Inside(b, 3.2f), 0f);

            // ---------- outside (decor: no colliders, never reachable) ----------
            using (var ground = new GroundProbe(Env + "SM_PitSurrounds"))
            {
                void Outside(string model, float bearing, float dist, float yaw, float scale = 1f)
                {
                    var p = Edge(bearing) + Out(bearing) * dist;
                    p.y = ground.Height(p.x, p.z) - 0.05f;
                    Add(model, PieceRole.Decor, p, yaw, Vector3.one * scale);
                }
                // the slaves' huts (south-west), the slavers' longhouses by the north road, tents (south-east)
                foreach (var (b, d) in new[] { (205f, 16f), (215f, 24f), (196f, 28f), (222f, 15f), (210f, 34f), (190f, 18f) })
                    Outside("SM_Hut", b, d, b * 1.7f, 1f);
                Outside("SM_Longhouse", 22f, 20f, 200f);
                Outside("SM_Longhouse", 338f, 22f, 160f);
                foreach (var (b, d) in new[] { (145f, 14f), (152f, 20f), (160f, 13f), (138f, 22f), (168f, 19f) })
                    Outside("SM_Tent", b, d, b * 2.3f);
                Outside("SM_Watchtower", 8f, 6f, 180f);
                Outside("SM_Watchtower", 120f, 7f, 300f);
                Outside("SM_Watchtower", 245f, 7f, 60f);
                Outside("SM_Cart", 4f, 30f, 175f);
                Outside("SM_Cart", 235f, 26f, 40f);
                Outside("SM_Cart", 130f, 32f, 300f);
                // woods and rocks, thinning toward the road from each gate
                var rng = new System.Random(77);
                float R() => (float)rng.NextDouble();
                int placed = 0;
                for (int attempt = 0; attempt < 600 && placed < 150; attempt++)
                {
                    float b = R() * 360f, d = 9f + 115f * R() * R();
                    bool road = false;
                    foreach (int g in GateSections)
                        if (Mathf.Abs(Mathf.DeltaAngle(b, g * 360f / Sections)) < (d < 40f ? 9f : 5f)) road = true;
                    if (road) continue;
                    bool camp = (b > 180f && b < 230f && d < 40f) || (b > 130f && b < 175f && d < 26f) || ((b < 30f || b > 330f) && d < 28f);
                    if (camp) continue;
                    float k = R();
                    string model = k < 0.5f ? "SM_TreePine" : k < 0.82f ? "SM_TreeOak" : k < 0.93f ? "SM_Bush" : "SM_Rock";
                    float s = model == "SM_Rock" ? 0.8f + 1.4f * R() : 0.8f + 0.5f * R();
                    Outside(model, b, d, R() * 360f, s);
                    placed++;
                }
            }

            l.pieces = pieces.ToArray();
            l.gateModel = "Environment/SM_YardGate";
            l.gates = gates.ToArray();
            l.bossGate = 0;
            l.boundary = boundary.ToArray();
            l.crowd = crowd.ToArray();
            l.zones = new[]
            {
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(5f, 0f, -3f), radius = 2.6f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(-10f, 0f, -14f), radius = 2.2f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(14f, 0f, 4f), radius = 2f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(-6f, 0f, 18f), radius = 2.4f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(-26f, 0f, 4f), radius = 2f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(8f, 0f, -21f), radius = 2.2f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Fire, position = new Vector3(-15f, 0f, 9f), radius = 0.9f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Fire, position = new Vector3(17f, 0f, -6f), radius = 0.9f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Fire, position = new Vector3(-4f, 0f, -20f), radius = 0.9f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Fire, position = new Vector3(24f, 0f, 9f), radius = 0.9f },
            };
            l.chestAnchors = new[]
            {
                new Vector3(-8f, 0f, -4f), new Vector3(8f, 0f, -9f), new Vector3(20f, 0f, 6f), new Vector3(-18f, 0f, 2f),
                new Vector3(2f, 0f, 14f), new Vector3(-12f, 0f, 24f), new Vector3(10f, 0f, 28f), new Vector3(28f, 0f, -14f),
                new Vector3(-27f, 0f, -6f), new Vector3(-2f, 0f, -28f), new Vector3(34f, 0f, -1f), new Vector3(-34f, 0f, 9f),
                new Vector3(16f, 0f, -26f), new Vector3(-20f, 0f, -24f), new Vector3(21f, 0f, 22f),
            };
            l.altarAnchors = new[] { new Vector3(-22f, 1.3f, 16f), new Vector3(20f, 1.3f, -18f) }; // up on a terrace
            // the rattling crate (Phase 5.2): beside the crates, the cages and the sacks, where one more box blends in
            l.secretSpots = new[] { Inside(14f, 2.8f), Inside(186f, 3.2f), new Vector3(29f, 0f, 10f) };
            l.secrets = ArenaSecrets.RattlingCrate | ArenaSecrets.GoldenRat; // the rat's holes are the same nooks
            l.barricades = System.Array.Empty<Vector4>();
            l.braziers = System.Array.Empty<Vector3>();
            l.spikeTraps = System.Array.Empty<Vector3>();

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }

        /// <summary>Heights of a decor ground mesh (the surrounds have no collider in the scene): a temporary copy
        /// with a mesh collider, probed straight down.</summary>
        sealed class GroundProbe : System.IDisposable
        {
            readonly GameObject _go;
            readonly List<MeshCollider> _cols = new List<MeshCollider>();

            public GroundProbe(string model)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/" + model + ".fbx");
                _go = Object.Instantiate(prefab);
                _go.hideFlags = HideFlags.HideAndDontSave;
                foreach (var mf in _go.GetComponentsInChildren<MeshFilter>())
                {
                    var c = mf.gameObject.AddComponent<MeshCollider>();
                    c.sharedMesh = mf.sharedMesh;
                    _cols.Add(c);
                }
                Physics.SyncTransforms();
            }

            public float Height(float x, float z)
            {
                var ray = new Ray(new Vector3(x, 200f, z), Vector3.down);
                foreach (var c in _cols)
                    if (c.Raycast(ray, out var hit, 400f)) return hit.point.y;
                return 0f;
            }

            public void Dispose() => Object.DestroyImmediate(_go);
        }
    }
}
