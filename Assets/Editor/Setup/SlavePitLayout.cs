using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Writes the starting layout of Arena 1, the Slave Pit (CLAUDE.md §6.2): a ~42 m dirt pit ringed by earthen walls
    /// with two gates, three raised terraces with ramps, mounds, mud patches that slow, three fire pits, cages and
    /// chain posts, rough stands with ~40 locals on the rim. The asset is designer-owned once it exists: this only
    /// overwrites it from the "Regenerate" menu item. ArenaBuilder turns it into the scene.
    /// </summary>
    public static class SlavePitLayout
    {
        public const string Path = "Assets/Data/Arenas/AL_SlavePit.asset";
        const string Env = "Environment/";
        const float WallR = 22f;   // front face of the wall ring
        const int Segments = 32;   // 4.3 m apart; 4.4 m pieces overlap a little
        static readonly int[] GateSlots = { 0, 19 }; // north, and south-west

        [MenuItem("Gladiator/Arenas/Regenerate Slave Pit Layout (overwrites)")]
        public static void Regenerate() => Write(true);

        public static ArenaLayoutDef Ensure() => Write(false);

        static ArenaLayoutDef Write(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(Path);
            if (existing != null && !overwrite) return existing;
            var l = existing != null ? existing : ScriptableObject.CreateInstance<ArenaLayoutDef>();
            l.id = "slave_pit";
            l.displayName = "The Slave Pit";
            l.center = Vector3.zero;
            l.radius = 20.4f;
            l.playerSpawn = new Vector3(0f, 0.1f, -8f);
            l.playerYaw = 0f;
            l.maxAlive = 300;
            l.sunEuler = new Vector3(36f, -40f, 0f);

            var pieces = new List<ArenaLayoutDef.Piece>();
            var gates = new List<ArenaLayoutDef.Gate>();
            void Add(string model, PieceRole role, Vector3 pos, float yaw, Vector3? scale = null) =>
                pieces.Add(new ArenaLayoutDef.Piece { model = Env + model, role = role, position = pos, yaw = yaw, scale = scale ?? Vector3.one });

            Add("SM_PitFloor", PieceRole.Walkable, Vector3.zero, 0f);
            Add("SM_PitRim", PieceRole.Decor, Vector3.zero, 0f);

            // wall ring: front faces the centre (model +Z → inward)
            for (int i = 0; i < Segments; i++)
            {
                float a = (i + 0.5f) / Segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float yaw = a * Mathf.Rad2Deg + 180f;
                if (System.Array.IndexOf(GateSlots, i) >= 0)
                {
                    gates.Add(new ArenaLayoutDef.Gate { position = dir * (WallR + 0.6f), yaw = yaw });
                    continue;
                }
                Add("SM_PitWall_4m", PieceRole.Solid, dir * WallR, yaw);
                if (i % 3 == 1) Add("SM_Palisade_4m", PieceRole.Decor, dir * (WallR + 0.75f) + Vector3.up * 2.35f, yaw);
            }

            // stands on the rim, facing in, away from the gates
            var crowd = new List<ArenaLayoutDef.CrowdZone>();
            foreach (float deg in new[] { 35f, 80f, 130f, 165f, 290f })
            {
                float a = deg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float yaw = deg + 180f;
                var at = dir * (WallR + 3.4f) + Vector3.up * 2.4f;
                Add("SM_Stand_4m", PieceRole.Decor, at, yaw);
                crowd.Add(new ArenaLayoutDef.CrowdZone { center = at + Vector3.up * 1.4f, size = new Vector2(3.4f, 1.5f), yaw = yaw, count = 5 });
            }
            // locals standing at the lip of the pit
            foreach (float deg in new[] { 58f, 245f, 320f })
            {
                float a = deg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                crowd.Add(new ArenaLayoutDef.CrowdZone { center = dir * (WallR + 1.9f) + Vector3.up * 2.4f, size = new Vector2(4.5f, 1.2f), yaw = deg + 180f, count = 5 });
            }

            // vertical variety: terraces with a ramp down toward the centre
            void Terrace(Vector3 at, float yaw)
            {
                Add("SM_Terrace_A", PieceRole.Walkable, at, yaw);
                var toCentre = (-at).normalized;
                var top = at + toCentre * 2.4f;              // the ramp's high end meets the terrace top edge
                var low = top + toCentre * 5f;               // SM_DirtRamp is 5 m long, rising toward its -Z
                Add("SM_DirtRamp", PieceRole.Walkable, low, Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg);
            }
            Terrace(new Vector3(-12.5f, 0f, 6f), 20f);
            Terrace(new Vector3(12.5f, 0f, -7f), -30f);
            Terrace(new Vector3(-4f, 0f, -15.5f), 75f);

            Add("SM_Mound_A", PieceRole.Walkable, new Vector3(-2.5f, 0f, 5f), 15f);
            Add("SM_Mound_B", PieceRole.Walkable, new Vector3(7.5f, 0f, 8.5f), 40f);
            Add("SM_Mound_A", PieceRole.Walkable, new Vector3(9f, 0f, -14.5f), 70f, new Vector3(1.2f, 0.7f, 1.2f));

            // props: cages and chain posts near the walls, crates by the gates
            Add("PR_Cage", PieceRole.Prop, new Vector3(-17.5f, 0f, -3f), 25f);
            Add("PR_Cage", PieceRole.Prop, new Vector3(17f, 0f, 4f), -15f);
            Add("PR_Cage", PieceRole.Prop, new Vector3(-8f, 0f, 17.5f), 60f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(5f, 0f, 17.5f), 120f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(-16.5f, 0f, -10f), 30f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(15f, 0f, 12f), 210f);
            Add("PR_ChainPost", PieceRole.Prop, new Vector3(1.5f, 0f, -19f), 0f);
            Add("PR_Crate", PieceRole.Prop, new Vector3(3.2f, 0f, 19.2f), 12f);
            Add("PR_Crate", PieceRole.Prop, new Vector3(-12.8f, 0f, -14.6f), -20f);
            Add("PR_Barrel", PieceRole.Prop, new Vector3(-11.7f, 0f, -15.8f), 0f);
            Add("PR_Brazier", PieceRole.Prop, new Vector3(-2.6f, 0f, 19.6f), 0f);
            Add("PR_Brazier", PieceRole.Prop, new Vector3(-14.8f, 0f, -11.2f), 0f);

            l.pieces = pieces.ToArray();
            l.gates = gates.ToArray();
            l.crowd = crowd.ToArray();
            l.zones = new[]
            {
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(3f, 0f, -2.5f), radius = 2.4f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(-8f, 0f, -6.5f), radius = 2f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(9.5f, 0f, 2.5f), radius = 1.8f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Mud, position = new Vector3(-3.5f, 0f, 12f), radius = 2.2f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Fire, position = new Vector3(-14f, 0f, 13f), radius = 0.9f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Fire, position = new Vector3(15f, 0f, -1.5f), radius = 0.9f },
                new ArenaLayoutDef.Zone { kind = ArenaLayoutDef.ZoneKind.Fire, position = new Vector3(5.5f, 0f, -12.5f), radius = 0.9f },
            };
            l.chestAnchors = new[]
            {
                new Vector3(-6.5f, 0f, -2f), new Vector3(6f, 0f, -6f), new Vector3(15.5f, 0f, 7f), new Vector3(-15f, 0f, 0f),
                new Vector3(0.5f, 0f, 9.5f), new Vector3(-9f, 0f, 13f), new Vector3(11f, 0f, 14.5f), new Vector3(13f, 0f, -14f),
                new Vector3(-9.5f, 0f, -12f), new Vector3(1f, 0f, -12f),
            };
            l.altarAnchors = new[] { new Vector3(-12.5f, 1.3f, 6f), new Vector3(12.5f, 1.3f, -7f) }; // up on a terrace
            // the rattling crate (Phase 5.2): beside the other crates and a cage, so it blends in until it rattles
            l.secretSpots = new[] { new Vector3(1.2f, 0f, 18.4f), new Vector3(-10f, 0f, -15.6f), new Vector3(16.4f, 0f, 6.6f) };

            if (existing == null) AssetDatabase.CreateAsset(l, Path);
            EditorUtility.SetDirty(l);
            AssetDatabase.SaveAssetIfDirty(l);
            return l;
        }
    }
}
