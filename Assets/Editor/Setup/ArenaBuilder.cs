using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Builds an arena scene from its ArenaLayoutDef (CLAUDE.md §7.3, M3.2): pieces by role (walkable / solid / prop /
    /// decor), gates with animated doors and an invisible blocker, mud and fire zones, the instanced crowd, chest and
    /// altar anchors, the run systems, then bakes the height grid and obstacles. One menu item per arena.
    /// </summary>
    public static class ArenaBuilder
    {
        public const string SlavePitScene = "Assets/Scenes/Arenas/SlavePit.unity";
        public const string CrowdMat = LookSetup.MatDir + "/M_Crowd.mat";
        const string SpectatorModel = "Assets/Art/Models/Environment/CH_Spectator.fbx";
        const string MudModel = "Environment/PR_MudPatch";
        const string FireModel = "Environment/PR_FirePit";

        [MenuItem("Gladiator/Arenas/Build Slave Pit")]
        public static string BuildSlavePit()
        {
            var layout = SlavePitLayout.Ensure();
            if (layout.boss == null) { layout.boss = BossSetup.EnsureGorm(); EditorUtility.SetDirty(layout); AssetDatabase.SaveAssetIfDirty(layout); }
            return Build(layout, SlavePitScene);
        }

        public static string Build(ArenaLayoutDef layout, string scenePath)
        {
            LookSetup.CreateOrUpdateAll();
            PlayerSetup.BuildPrefab();
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Arenas")) AssetDatabase.CreateFolder("Assets/Scenes", "Arenas");
            string layoutPath = AssetDatabase.GetAssetPath(layout);
            var scene = SceneSetupUtil.NewEmptyScene();
            // the new scene unloads unused assets: assets the layout points at (its boss) die in memory, so reload it
            if (!string.IsNullOrEmpty(layoutPath)) layout = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(layoutPath);
            ArenaSceneCommon.AddLook(layout.sunEuler);
            ArenaSceneCommon.AddGameLoop();

            var env = new GameObject("Environment").transform;
            foreach (var p in layout.pieces) PlacePiece(p, env);

            var gateRoots = new List<Transform>();
            var gatesRoot = new GameObject("Gates").transform;
            gatesRoot.SetParent(env, false);
            foreach (var g in layout.gates) gateRoots.Add(PlaceGate(g, gatesRoot));

            var mud = new List<Vector3>();
            var fires = new List<Vector4>();
            var zonesRoot = new GameObject("Zones").transform;
            zonesRoot.SetParent(env, false);
            int z = 0;
            foreach (var zone in layout.zones)
            {
                float yaw = (z++ * 67f) % 360f;
                if (zone.kind == ArenaLayoutDef.ZoneKind.Mud)
                {
                    // lifted clear of the floor's ±5 cm height noise (otherwise the floor pokes through the decal)
                    ArenaSceneCommon.PlaceDecor(MudModel, zonesRoot, zone.position + Vector3.up * 0.06f, yaw, new Vector3(zone.radius, 1f, zone.radius));
                    mud.Add(new Vector3(zone.position.x, zone.position.z, zone.radius));
                }
                else
                {
                    ArenaSceneCommon.PlaceDecor(FireModel, zonesRoot, zone.position, yaw); // walk into it and it burns
                    fires.Add(new Vector4(zone.position.x, zone.position.y, zone.position.z, zone.radius));
                }
            }

            var (player, _) = ArenaSceneCommon.AddPlayerAndCamera(layout.playerSpawn, layout.playerYaw);
            var (enemies, _) = ArenaSceneCommon.AddCombatSystems(player, layout.radius, debugEnemies: 0);
            enemies.arenaCenter = layout.center;
            ArenaNavBaker.Bake(enemies, System.IO.Path.GetFileNameWithoutExtension(scenePath));
            ArenaSceneCommon.AddRunSystems(player);

            var arena = new GameObject("[Arena]").transform;
            var gates = new GameObject("ArenaGates").AddComponent<ArenaGates>();
            gates.transform.SetParent(arena, false);
            gates.gates = gateRoots.ToArray();
            gates.spawnDepth = new Vector2(2.4f, 4.2f); // past the door line, inside the play radius
            var mudZones = new GameObject("MudZones").AddComponent<MudZones>();
            mudZones.transform.SetParent(arena, false);
            mudZones.circles = mud.ToArray();
            var firePits = new GameObject("FirePits").AddComponent<ArenaFirePits>();
            firePits.transform.SetParent(arena, false);
            firePits.pits = fires.ToArray();

            var crowd = new GameObject("Crowd").AddComponent<CrowdRenderer>();
            crowd.transform.SetParent(arena, false);
            crowd.mesh = FirstMesh(SpectatorModel);
            crowd.material = EnsureCrowdMaterial();
            crowd.spots = CrowdSpots(layout);
            new GameObject("CrowdExcitement").AddComponent<CrowdExcitement>().transform.SetParent(arena, false);
            var intro = new GameObject("RunIntro").AddComponent<RunIntro>();
            intro.transform.SetParent(arena, false);
            intro.arenaName = layout.displayName.ToUpperInvariant();

            if (layout.boss != null)
            {
                var fight = new GameObject("BossFight").AddComponent<BossFight>();
                fight.transform.SetParent(arena, false);
                fight.boss = layout.boss;
                fight.gate = layout.bossGate;
                fight.controller = new GameObject("BossController").AddComponent<BossController>();
                fight.controller.transform.SetParent(fight.transform, false);
                fight.controller.def = layout.boss;
                var hazards = Object.FindAnyObjectByType<HazardZones>();
                if (hazards != null) hazards.capacity = Mathf.Max(hazards.capacity, 160); // + the phase-2 fire ring
            }

            var director = Object.FindAnyObjectByType<Director>();
            if (director != null) director.maxAliveOverride = layout.maxAlive;
            var props = Object.FindAnyObjectByType<Interactables>();
            if (props != null) { props.chestAnchors = layout.chestAnchors; props.altarAnchors = layout.altarAnchors; }

            var bot = new GameObject("[PlaytestBot]").AddComponent<PlaytestBot>();
            bot.arenaCenter = layout.center;
            bot.arenaRadius = layout.radius - 3f;
            bot.gameObject.SetActive(false); // enabled by -bot / smoke test

            SceneSetupUtil.SaveScene(scene, scenePath);
            return scenePath;
        }

        static void PlacePiece(in ArenaLayoutDef.Piece p, Transform parent)
        {
            Vector3? scale = p.scale == Vector3.one || p.scale == Vector3.zero ? (Vector3?)null : p.scale;
            GameObject go;
            switch (p.role)
            {
                case PieceRole.Walkable: go = ArenaSceneCommon.PlaceWalkable(p.model, parent, p.position, p.yaw, scale); break;
                case PieceRole.Solid: go = ArenaSceneCommon.PlaceSolid(p.model, parent, p.position, p.yaw, scale); break;
                case PieceRole.Prop: go = ArenaSceneCommon.PlaceProp(p.model, parent, p.position, p.yaw, scale); break;
                default: go = ArenaSceneCommon.PlaceDecor(p.model, parent, p.position, p.yaw, scale); break;
            }
            if (p.model.EndsWith("PR_Brazier"))
            {
                var l = new GameObject("FireLight").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = new Vector3(0, 1.6f, 0);
                l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.range = 6f; l.intensity = 3f; l.shadows = LightShadows.None;
            }
        }

        /// <summary>Gate frame as solid geometry; the doors are visual only (they swing) and an invisible wall behind
        /// them keeps the player in when they're open.</summary>
        static Transform PlaceGate(in ArenaLayoutDef.Gate g, Transform parent)
        {
            var go = ArenaSceneCommon.PlaceSolid("Environment/SM_PitGate", parent, g.position, g.yaw);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "DoorL" && t.name != "DoorR") continue;
                foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var d in t.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(d.gameObject, 0);
            }
            var block = new GameObject("GateBlocker");
            block.layer = Layers.Environment;
            block.transform.SetParent(go.transform, false);
            block.transform.localPosition = new Vector3(0f, 2f, 0.3f); // on the door line: closed doors look and act solid
            block.AddComponent<BoxCollider>().size = new Vector3(4.4f, 4f, 0.3f);
            return go.transform;
        }

        static Mesh FirstMesh(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m) return m;
            Debug.LogError("[ArenaBuilder] no mesh in " + path);
            return null;
        }

        static Material EnsureCrowdMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(CrowdMat);
            var sh = Shader.Find("Gladiator/Crowd");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, CrowdMat); }
            else if (m.shader != sh) m.shader = sh;
            var palette = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            if (palette != null) m.SetTexture("_BaseMap", palette.GetTexture("_BaseMap"));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Deterministic spots inside each crowd zone, facing the arena centre with a little jitter.</summary>
        static Vector4[] CrowdSpots(ArenaLayoutDef layout)
        {
            var spots = new List<Vector4>();
            var rng = new System.Random(1234);
            foreach (var zone in layout.crowd)
            {
                var rot = Quaternion.Euler(0f, zone.yaw, 0f);
                int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(zone.count * zone.size.x / Mathf.Max(0.1f, zone.size.y))));
                int rows = Mathf.Max(1, Mathf.CeilToInt(zone.count / (float)cols));
                for (int k = 0; k < zone.count; k++)
                {
                    int cx = k % cols, cz = k / cols;
                    float x = ((cx + 0.5f) / cols - 0.5f) * zone.size.x + (float)(rng.NextDouble() - 0.5) * 0.35f;
                    float zz = ((cz + 0.5f) / rows - 0.5f) * zone.size.y + (float)(rng.NextDouble() - 0.5) * 0.25f;
                    var p = zone.center + rot * new Vector3(x, 0f, zz);
                    var toCentre = layout.center - p;
                    float yaw = Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg + (float)(rng.NextDouble() - 0.5) * 30f;
                    spots.Add(new Vector4(p.x, p.y, p.z, yaw));
                }
            }
            return spots.ToArray();
        }
    }
}
