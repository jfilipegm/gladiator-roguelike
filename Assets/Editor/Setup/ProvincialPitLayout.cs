using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the starting layout of Arena 2, the Provincial Pit (docs/ARENAS.md): a ~50 m sand arena ringed by a stone
    /// foundation wall with tiered wooden stands (~200 townsfolk), three stone gates 120° apart, six pillars, three
    /// raised platforms (stairs, stairs, a wooden ramp), a central dais, eight barricades (the Champion's charge wrecks
    /// them), four braziers (they topple into fire) and six spike grates (they hurt everyone). Designer-owned once it
    /// exists: only the "Regenerate" menu item overwrites it. ArenaBuilder turns it into the scene.
    /// </summary>
    public static class ProvincialPitLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_ProvincialPit.asset";
        const string Env = "Environment/";
        const float WallR = 25.5f;   // front face of the wall ring
        const int Segments = 36;     // 4.45 m apart; 4.6 m pieces overlap a little
        static readonly int[] GateSlots = { 0, 12, 24 }; // 5°, 125°, 245°: north, south-east, south-west

        [MenuItem("Gladiator/Arenas/Regenerate Provincial Pit Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        static Vector3 Polar(float deg, float r, float y = 0f)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
        }

        static float SlotDeg(int i) => (i + 0.5f) / Segments * 360f;

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            l.id = "provincial_pit";
            l.displayName = "The Provincial Pit";
            l.tier = 1;
            l.center = Vector3.zero;
            l.radius = 24.4f;
            l.playerSpawn = new Vector3(0f, 0.1f, -6.5f); // ahead of the south ramp, so it doesn't fill the opening shot
            l.playerYaw = 0f;
            l.maxAlive = 300;
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

            // wall ring (model +Z → inward), stands on the rim behind it, banners on the coping
            for (int i = 0; i < Segments; i++)
            {
                float deg = SlotDeg(i);
                float yaw = deg + 180f;
                if (System.Array.IndexOf(GateSlots, i) >= 0)
                {
                    gates.Add(new ArenaLayoutDef.Gate { position = Polar(deg, WallR + 0.6f), yaw = yaw });
                    continue;
                }
                Add("SM_ProvWall_4m", PieceRole.Solid, Polar(deg, WallR), yaw);
                bool besideGate = false;
                foreach (int g in GateSlots) if (Mathf.Abs(Mathf.DeltaAngle(SlotDeg(g), deg)) < 11f) besideGate = true;
                if (besideGate)
                {
                    Add("PR_Banner", PieceRole.Decor, Polar(deg, WallR + 0.35f, 2.58f), yaw);
                    continue;
                }
                const float standR = WallR + 1.15f;
                Add("SM_ProvStand_4m", PieceRole.Decor, Polar(deg, standR, 2.4f), yaw);
                for (int k = 0; k < 3; k++) // one row of townsfolk per tier
                    crowd.Add(new ArenaLayoutDef.CrowdZone
                    {
                        center = Polar(deg, standR + 0.4f + k * 0.8f, 2.4f + (k + 1) * 0.45f),
                        size = new Vector2(3.8f, 0.4f), yaw = yaw, count = 2 + (i + k) % 2,
                    });
            }

            // vertical variety: stone platforms with stairs (and one wooden ramp), facing the centre
            void Platform(float deg, string climb)
            {
                const float r = 17.5f;
                float yaw = deg + 180f;
                Add("SM_ProvPlatform", PieceRole.Walkable, Polar(deg, r), yaw);
                // the climb's origin is its low front edge and it rises toward its local -Z (outward): its top edge
                // meets the platform's inner edge (r - 2). Stairs are 2 m long, the ramp 4 m.
                if (climb == "SM_Stairs") Add("SM_Stairs", PieceRole.Walkable, Polar(deg, r - 4f), yaw);
                else Add("SM_Ramp", PieceRole.Walkable, Polar(deg, r - 6f), yaw);
            }
            Platform(65f, "SM_Stairs");
            Platform(305f, "SM_Stairs");
            Platform(185f, "SM_Ramp");
            Add("SM_ProvDais", PieceRole.Walkable, Vector3.zero, 0f);

            foreach (float deg in new[] { 35f, 95f, 155f, 215f, 275f, 335f })
                Add("SM_ProvPillar", PieceRole.Solid, Polar(deg, 12f), deg);

            l.pieces = pieces.ToArray();
            l.gates = gates.ToArray();
            l.crowd = crowd.ToArray();
            l.zones = new ArenaLayoutDef.Zone[0];

            // barricades: a chicane in front of each gate, and two beside the dais (x, y, z, yaw)
            var bars = new List<Vector4>();
            foreach (float g in new[] { SlotDeg(0), SlotDeg(12), SlotDeg(24) })
            {
                var a = Polar(g - 14f, 18.5f); bars.Add(new Vector4(a.x, 0f, a.z, g - 14f));       // along the tangent
                var b = Polar(g + 14f, 15.5f); bars.Add(new Vector4(b.x, 0f, b.z, g + 14f));
            }
            foreach (float deg in new[] { 95f, 275f })
            {
                var c = Polar(deg, 6.8f); bars.Add(new Vector4(c.x, 0f, c.z, deg + 90f));            // along the radius
            }
            l.barricades = bars.ToArray();
            l.braziers = new[] { Polar(65f, 18.6f, 1.2f), Polar(305f, 18.6f, 1.2f), Polar(SlotDeg(0) - 9f, 22.6f), Polar(SlotDeg(0) + 9f, 22.6f) };
            l.spikeTraps = new[]
            {
                Polar(SlotDeg(0), 8.5f), Polar(SlotDeg(12), 8.5f), Polar(SlotDeg(24), 8.5f), // in the gate lanes: the horde walks over them
                Polar(95f, 20.5f), Polar(215f, 20.5f), Polar(335f, 20.5f),
            };

            l.chestAnchors = new[]
            {
                Polar(20f, 5.5f), Polar(140f, 5.5f), Polar(260f, 5.5f), Polar(65f, 11f), Polar(185f, 11f), Polar(305f, 11f),
                Polar(35f, 20f), Polar(155f, 20f), Polar(275f, 20.5f), Polar(110f, 21f),
            };
            l.altarAnchors = new[] { Polar(65f, 16.8f, 1.2f), Polar(305f, 16.8f, 1.2f) }; // up on the platforms
            l.secretSpots = new Vector3[0];

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }
    }
}
