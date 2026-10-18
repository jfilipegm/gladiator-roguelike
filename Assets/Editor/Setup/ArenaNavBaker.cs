using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Editor bake step for enemy navigation: one raycast pass over the open arena → Assets/Data/Arenas/&lt;Scene&gt;_Nav.asset,
    /// assigned to the scene's EnemyManager. Runs automatically when an arena is (re)built.
    /// </summary>
    public static class ArenaNavBaker
    {
        public static ArenaNavData Bake(EnemyManager em, string sceneName)
        {
            Physics.SyncTransforms();
            EnemyManager.BakeScene(em.boundsMin, em.boundsMax, em.heightCellSize, out var grid, out var obstacles);
            string path = "Assets/Data/Arenas/" + sceneName + "_Nav.asset";
            var data = AssetDatabase.LoadAssetAtPath<ArenaNavData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ArenaNavData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.Store(em.boundsMin, em.boundsMax, em.heightCellSize, grid, obstacles);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            em.navData = data;
            EditorUtility.SetDirty(em);
            Debug.Log($"[ArenaNavBaker] {path}: {grid.W}x{grid.H} heights, {obstacles.Count} obstacles");
            return data;
        }

        [MenuItem("Gladiator/Setup/Bake Arena Nav (open scene)")]
        static void BakeOpenScene()
        {
            var em = Object.FindAnyObjectByType<EnemyManager>();
            if (em == null) { Debug.LogError("[ArenaNavBaker] no EnemyManager in the open scene"); return; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Bake(em, scene.name);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
