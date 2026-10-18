using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gladiator.EditorTools
{
    /// <summary>M0.4 look-dev scene: the target style on real (procedural) assets. Rebuilt entirely from code.</summary>
    public static class LookDevScene
    {
        public const string ScenePath = "Assets/Scenes/Dev/LookDev.unity";
        const string Models = "Assets/Art/Models/";

        [MenuItem("Gladiator/Setup/Dev/Build Look-Dev Scene")]
        public static string Build()
        {
            LookSetup.CreateOrUpdateAll();
            var env = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat);
            var ch = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            var flash = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.FlashMat);

            var scene = SceneSetupUtil.NewEmptyScene();

            // --- light, sky, fog, post
            var sun = SceneSetupUtil.DirectionalLight("Sun", new Vector3(42, -38, 0), new Color(1f, 0.94f, 0.82f), 1.3f);
            sun.shadowStrength = 1f;
            var look = new GameObject("EnvironmentLighting").AddComponent<EnvironmentLighting>();
            look.skybox = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.SkyMat);
            look.sun = sun;
            look.Apply();
            var vol = new GameObject("PostVolume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookSetup.VolumeProfilePath);

            // --- environment
            var envRoot = new GameObject("Environment").transform;
            var floor = Place("Environment/SM_PitFloor", envRoot, Vector3.zero, 0, env, isStatic: true);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponentInChildren<MeshFilter>().sharedMesh;

            const float fenceRadius = 21f;
            int segments = 44;
            for (int i = 0; i < segments; i++)
            {
                if (i == 0 || i == 1) continue; // gate gap (north)
                float a = (i + 0.5f) / segments * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * fenceRadius;
                Place("Environment/SM_Fence_3m", envRoot, pos, a * Mathf.Rad2Deg + 90f, env, true);
            }
            Place("Environment/SM_Mound_A", envRoot, new Vector3(-6, 0, 9), 20, env, true);
            Place("Environment/SM_Mound_A", envRoot, new Vector3(10, 0, 12), 70, env, true).transform.localScale = new Vector3(1.3f, 0.8f, 1.3f);
            Place("Environment/SM_Rock_A", envRoot, new Vector3(7, 0, 9), 10, env, true);
            Place("Environment/SM_Rock_A", envRoot, new Vector3(-9, 0, -3), 140, env, true).transform.localScale = Vector3.one * 1.5f;
            Place("Environment/PR_Crate", envRoot, new Vector3(5.5f, 0, 3), 15, env, true);
            Place("Environment/PR_Crate", envRoot, new Vector3(6.4f, 0, 3.6f), -10, env, true).transform.localScale = Vector3.one * 0.8f;
            Place("Environment/PR_Crate", envRoot, new Vector3(5.9f, 0.9f, 3.2f), 40, env, true).transform.localScale = Vector3.one * 0.7f;
            Place("Environment/PR_Barrel", envRoot, new Vector3(6.8f, 0, 1.6f), 0, env, true);
            Place("Environment/PR_Barrel", envRoot, new Vector3(-4.8f, 0, 6.2f), 0, env, true);
            Place("Environment/SM_Ramp", envRoot, new Vector3(-9, 0, 7), 180, env, true);
            Place("Environment/SM_Platform", envRoot, new Vector3(-9, 0, 11), 0, env, true);
            Place("Environment/SM_Stairs", envRoot, new Vector3(9, 0, -6), 90, env, true);
            foreach (var p in new[] { new Vector3(-3.5f, 0, 4f), new Vector3(3.8f, 0, 9.5f) })
            {
                var b = Place("Environment/PR_Brazier", envRoot, p, 0, env, true);
                var lightGo = new GameObject("FireLight");
                lightGo.transform.SetParent(b.transform, false);
                lightGo.transform.localPosition = new Vector3(0, 1.6f, 0);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.6f, 0.25f);
                l.range = 6f;
                l.intensity = 3f;
                l.shadows = LightShadows.None;
            }

            // --- characters
            var chars = new GameObject("Characters").transform;
            Place("Characters/CH_Nobody", chars, new Vector3(0, 0, 0), 0, ch, false);
            Place("Enemies/EN_Convict", chars, new Vector3(2.2f, 0, 4.2f), 200, ch, false);
            Place("Enemies/EN_Convict", chars, new Vector3(-1.8f, 0, 5.0f), 165, ch, false);
            Place("Enemies/EN_Convict", chars, new Vector3(0.6f, 0, 6.6f), 185, flash, false).name = "EN_Convict (flash preview)";

            // --- cameras: gameplay (behind the player) and showcase (front 3/4)
            var gameCam = MakeCamera("Main Camera", new Vector3(0, 3.6f, -6.4f), new Vector3(0, 1.3f, 3.5f), 60f);
            gameCam.tag = "MainCamera";
            gameCam.AddComponent<AudioListener>();
            var show = MakeCamera("ShowcaseCam", new Vector3(3.6f, 2.2f, 4.8f), new Vector3(0.2f, 1.0f, 1.5f), 45f);
            show.GetComponent<Camera>().enabled = false;

            SceneSetupUtil.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        static GameObject MakeCamera(string name, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            return go;
        }

        static GameObject Place(string model, Transform parent, Vector3 pos, float yaw, Material mat, bool isStatic)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (prefab == null) { Debug.LogError("[LookDev] missing model " + model); return new GameObject("MISSING " + model); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            LookSetup.AssignMaterial(go, mat);
            if (isStatic)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }
}
