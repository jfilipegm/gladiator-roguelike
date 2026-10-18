using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// M1.1 feel-test arena: flat runway, 30°/20° slide hill, platform reachable by stairs or ramp, a stone-wall
    /// corner and pillars for camera occlusion, props, mound bumps, fence ring + invisible boundary.
    /// Layout (Unity, +Z forward from spawn): spawn (0,0,-8) facing +Z.
    /// </summary>
    public static class TestArenaScene
    {
        public const string ScenePath = "Assets/Scenes/Dev/TestArena.unity";
        public static readonly Vector3 Spawn = new Vector3(0f, 0.1f, -8f);
        /// <summary>Hill: steep face descends toward +Z; top at y≈3.4 between z=-1..1 (local), placed at x=12.</summary>
        public static readonly Vector3 HillPos = new Vector3(12f, 0f, 0f);

        [MenuItem("Gladiator/Setup/Dev/Build Test Arena")]
        public static string Build()
        {
            LookSetup.CreateOrUpdateAll();
            PlayerSetup.BuildPrefab();
            var scene = SceneSetupUtil.NewEmptyScene();
            ArenaSceneCommon.AddLook(new Vector3(42, -38, 0));
            ArenaSceneCommon.AddGameLoop();

            var env = new GameObject("Environment").transform;
            ArenaSceneCommon.PlaceWalkable("Environment/SM_PitFloor", env, Vector3.zero, 0);

            // Slide hill (steep 30° face toward +Z; climb the 20° back ramp from -Z).
            ArenaSceneCommon.PlaceWalkable("Environment/SM_TestHill", env, HillPos, 0);

            // Platform (deck 1.2 m, x -12..-8, z 2..6) + stairs from the north + ramp from the east.
            ArenaSceneCommon.PlaceWalkable("Environment/SM_Platform", env, new Vector3(-10, 0, 4), 0);
            ArenaSceneCommon.PlaceWalkable("Environment/SM_Stairs", env, new Vector3(-10, 0, 8), 0);
            ArenaSceneCommon.PlaceWalkable("Environment/SM_Ramp", env, new Vector3(-4, 0, 4), 90);

            // Camera-occlusion corner + pillars.
            ArenaSceneCommon.PlaceSolid("Environment/SM_Wall_Stone_4m", env, new Vector3(-3, 0, -14), 0);
            ArenaSceneCommon.PlaceSolid("Environment/SM_Wall_Stone_4m", env, new Vector3(-5.45f, 0, -11.55f), 90);
            ArenaSceneCommon.PlaceSolid("Environment/SM_Wall_Stone_4m", env, new Vector3(4, 0, -16), 15);
            foreach (var p in new[] { new Vector3(5, 0, -3), new Vector3(7, 0, -9), new Vector3(-2, 0, 12), new Vector3(2, 0, 15) })
                ArenaSceneCommon.PlaceSolid("Environment/SM_Pillar", env, p, 0);

            // Bumps and props.
            ArenaSceneCommon.PlaceWalkable("Environment/SM_Mound_A", env, new Vector3(-8, 0, -4), 30);
            ArenaSceneCommon.PlaceWalkable("Environment/SM_Mound_A", env, new Vector3(3, 0, 6), 80, new Vector3(1.2f, 0.6f, 1.2f));
            ArenaSceneCommon.PlaceProp("Environment/PR_Crate", env, new Vector3(4, 0, 2), 15);
            ArenaSceneCommon.PlaceProp("Environment/PR_Crate", env, new Vector3(4.9f, 0, 2.6f), -10);
            ArenaSceneCommon.PlaceProp("Environment/PR_Barrel", env, new Vector3(5.6f, 0, 1.2f), 0);
            ArenaSceneCommon.PlaceSolid("Environment/SM_Rock_A", env, new Vector3(-14, 0, 8), 10, Vector3.one * 1.6f);
            foreach (var p in new[] { new Vector3(-4f, 0, -2f), new Vector3(8f, 0, 10f) })
            {
                var b = ArenaSceneCommon.PlaceProp("Environment/PR_Brazier", env, p, 0);
                var l = new GameObject("FireLight").AddComponent<Light>();
                l.transform.SetParent(b.transform, false);
                l.transform.localPosition = new Vector3(0, 1.6f, 0);
                l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.range = 6f; l.intensity = 3f; l.shadows = LightShadows.None;
            }

            // Fence ring (visual + props collider) and the invisible boundary.
            const float r = 21f;
            for (int i = 0; i < 44; i++)
            {
                float a = (i + 0.5f) / 44f * Mathf.PI * 2f;
                ArenaSceneCommon.PlaceDecor("Environment/SM_Fence_3m", env, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r, a * Mathf.Rad2Deg + 90f);
            }
            ArenaSceneCommon.AddBoundaryRing(env, r, 4f, 48);

            var (player, rig) = ArenaSceneCommon.AddPlayerAndCamera(Spawn, 0f);
            ArenaSceneCommon.AddCombatSystems(player, r - 1f, debugEnemies: 25);
            var bot = new GameObject("[PlaytestBot]").AddComponent<PlaytestBot>();
            bot.arenaRadius = 18f;
            bot.gameObject.SetActive(false); // enabled by -bot / smoke test

            SceneSetupUtil.SaveScene(scene, ScenePath);
            return ScenePath;
        }
    }
}
