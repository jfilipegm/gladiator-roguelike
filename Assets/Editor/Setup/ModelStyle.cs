using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// A1 (docs/tracks/A1-ingame-contract.md): the characters' art style. The A1 models live in
    /// Assets/Art/Models/A1/ at the same relative path and name as the classic ones (kept: Characters/CH_Nobody.fbx and
    /// A1/Characters/CH_Nobody.fbx), with the same rigid part names, so PartRig, the animators and the defs work with
    /// either. Setup scripts load models through <see cref="Path"/>; the two menu items swap every reference in the
    /// data, the prefabs and the scenes from one style to the other (fields, and model instances placed in prefabs and
    /// scenes, keeping their place, scale, layer, static flags and material).
    /// </summary>
    public static class ModelStyle
    {
        public enum Style { Classic, A1 }

        /// <summary>The style the setup scripts build with. Flip it together with running the matching menu item.</summary>
        public const Style Current = Style.A1;

        const string Root = "Assets/Art/Models/";
        const string A1Root = "Assets/Art/Models/A1/";
        static readonly string[] DataFolders = { "Assets/Data" };
        static readonly string[] PrefabFolders = { "Assets/Prefabs" };
        static readonly string[] SceneFolders = { "Assets/Scenes" };

        /// <summary>The model to build with: the A1 twin of a classic model path when the style is A1 and it exists.</summary>
        public static string Path(string classicPath)
        {
            if (Current != Style.A1 || classicPath == null || !classicPath.StartsWith(Root) || classicPath.StartsWith(A1Root))
                return classicPath;
            var a1 = A1Root + classicPath.Substring(Root.Length);
            return File.Exists(a1) ? a1 : classicPath;
        }

        public static GameObject Load(string classicPath) => AssetDatabase.LoadAssetAtPath<GameObject>(Path(classicPath));

        [MenuItem("Gladiator/Setup/Art/Use A1 Character Models")]
        public static void UseA1() => Debug.Log("[ModelStyle] " + Swap(true));

        [MenuItem("Gladiator/Setup/Art/Use Classic Character Models")]
        public static void UseClassic() => Debug.Log("[ModelStyle] " + Swap(false));

        /// <summary>Every A1 model with a classic twin: classic → A1 (or A1 → classic).</summary>
        public static Dictionary<GameObject, GameObject> Pairs(bool toA1)
        {
            var map = new Dictionary<GameObject, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { A1Root.TrimEnd('/') }))
            {
                var a1Path = AssetDatabase.GUIDToAssetPath(guid);
                var classicPath = Root + a1Path.Substring(A1Root.Length);
                var a1 = AssetDatabase.LoadAssetAtPath<GameObject>(a1Path);
                var classic = AssetDatabase.LoadAssetAtPath<GameObject>(classicPath);
                if (a1 == null || classic == null) continue;
                if (toA1) map[classic] = a1; else map[a1] = classic;
            }
            return map;
        }

        /// <summary>Swaps the references; returns a one-line summary.</summary>
        public static string Swap(bool toA1)
        {
            var map = Pairs(toA1);
            if (map.Count == 0) return "no A1 models with a classic twin under " + A1Root;
            int fields = 0, instances = 0;
            var touched = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", DataFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(o is ScriptableObject)) continue;
                    int n = RemapFields(o, map);
                    if (n > 0) { fields += n; EditorUtility.SetDirty(o); touched.Add(path); }
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int i = ReplaceInstances(root, map), f = RemapTree(root, map);
                    if (i + f > 0)
                    {
                        instances += i; fields += f; touched.Add(path);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", SceneFolders))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int i = 0, f = 0;
                    foreach (var go in scene.GetRootGameObjects()) i += ReplaceInstances(go, map);
                    foreach (var go in scene.GetRootGameObjects()) f += RemapTree(go, map);
                    if (i + f > 0)
                    {
                        instances += i; fields += f; touched.Add(path);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            AssetDatabase.SaveAssets();
            var sb = new StringBuilder();
            sb.Append(toA1 ? "to A1: " : "to classic: ").Append(map.Count).Append(" model pairs, ").Append(fields)
              .Append(" fields, ").Append(instances).Append(" instances in ").Append(touched.Count).Append(" assets");
            foreach (var t in touched) sb.Append("\n  ").Append(t);
            return sb.ToString();
        }

        static int RemapTree(GameObject root, Dictionary<GameObject, GameObject> map)
        {
            int n = 0;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform) continue;
                int k = RemapFields(c, map);
                if (k > 0) { n += k; EditorUtility.SetDirty(c); }
            }
            return n;
        }

        static int RemapFields(Object o, Dictionary<GameObject, GameObject> map)
        {
            var so = new SerializedObject(o);
            var it = so.GetIterator();
            int n = 0;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (it.objectReferenceValue is GameObject g && map.TryGetValue(g, out var to))
                {
                    it.objectReferenceValue = to;
                    n++;
                }
            }
            if (n > 0) so.ApplyModifiedPropertiesWithoutUndo();
            return n;
        }

        /// <summary>Model instances (outermost or nested) of a mapped model under root get the other model, keeping
        /// their place, scale, layer, static flags, shadow settings and material.</summary>
        static int ReplaceInstances(GameObject root, Dictionary<GameObject, GameObject> map)
        {
            var found = new List<(GameObject go, GameObject to)>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src != null && map.TryGetValue(src, out var to)) found.Add((go, to));
            }
            var settings = new PrefabReplacingSettings
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                prefabOverridesOptions = PrefabOverridesOptions.ClearAllNonDefaultOverrides,
                changeRootNameToAssetName = false,
                logInfo = false,
            };
            foreach (var (go, to) in found)
            {
                var r0 = go.GetComponentInChildren<MeshRenderer>(true);
                var mat = r0 != null ? r0.sharedMaterial : null;
                var shadows = r0 != null ? r0.shadowCastingMode : UnityEngine.Rendering.ShadowCastingMode.On;
                var receive = r0 == null || r0.receiveShadows;
                int layer = r0 != null ? r0.gameObject.layer : go.layer;
                var flags = GameObjectUtility.GetStaticEditorFlags(r0 != null ? r0.gameObject : go);
                var rootFlags = GameObjectUtility.GetStaticEditorFlags(go);
                var scale = go.transform.localScale;
                int rootLayer = go.layer;
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, to, settings, InteractionMode.AutomatedAction);
                go.transform.localScale = scale;
                go.layer = rootLayer;
                GameObjectUtility.SetStaticEditorFlags(go, rootFlags);
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t == go.transform) continue;
                    t.gameObject.layer = layer;
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                }
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mat != null) r.sharedMaterial = mat;
                    r.shadowCastingMode = shadows;
                    r.receiveShadows = receive;
                }
            }
            return found.Count;
        }
    }
}
