using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gladiator.EditorTools
{
    /// <summary>Shared helpers for the scene builders in Assets/Editor/Setup (scenes are always rebuilt from code).</summary>
    public static class SceneSetupUtil
    {
        public static Scene NewEmptyScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        public static void SaveScene(Scene scene, string path, bool addToBuild = true)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            if (addToBuild) AddToBuildSettings(path);
        }

        public static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        public static GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Transform parent = null, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static Light DirectionalLight(string name, Vector3 euler, Color color, float intensity)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(euler);
            return l;
        }
    }
}
