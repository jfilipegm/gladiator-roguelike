using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    public static class DevScenes
    {
        public const string BotSandboxPath = "Assets/Scenes/Dev/BotSandbox.unity";

        /// <summary>M0.3: minimal scene to verify PlaytestBot + FrameStatsRecorder end-to-end.</summary>
        [MenuItem("Gladiator/Setup/Dev/Build Bot Sandbox")]
        public static string BuildBotSandbox()
        {
            var scene = SceneSetupUtil.NewEmptyScene();
            SceneSetupUtil.DirectionalLight("Sun", new Vector3(50, -30, 0), Color.white, 1.2f);

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 22, -22);
            cam.transform.rotation = Quaternion.Euler(45, 0, 0);

            new GameObject("[GameLoop]").AddComponent<GameLoop>();

            var env = new GameObject("Environment").transform;
            SceneSetupUtil.Primitive(PrimitiveType.Plane, "Ground", Vector3.zero, new Vector3(4, 1, 4), env);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                SceneSetupUtil.Primitive(PrimitiveType.Cube, "Block" + i, new Vector3(Mathf.Cos(a) * 9, 0.75f, Mathf.Sin(a) * 9), new Vector3(2, 1.5f, 2), env);
            }

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 1.1f, 0);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.8f, 0);
            player.AddComponent<DevKinematicMover>();
            var body = SceneSetupUtil.Primitive(PrimitiveType.Capsule, "Body", new Vector3(0, 0.8f, 0), new Vector3(0.7f, 0.8f, 0.7f), player.transform, keepCollider: false);
            body.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            var bot = new GameObject("[PlaytestBot]").AddComponent<PlaytestBot>();
            bot.arenaRadius = 16f;
            new GameObject("[FrameStats]").AddComponent<FrameStatsRecorder>().label = "BotSandbox";

            SceneSetupUtil.SaveScene(scene, BotSandboxPath);
            return BotSandboxPath;
        }
    }
}
