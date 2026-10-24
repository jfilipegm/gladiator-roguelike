using System.Text;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Note 30 (the producer on v0.28.0: the Armory Chest "shouldn't look like a pirate chest"; on the lineup: "all of
    /// these are perfect, we can maybe add a different one for each arena"). The four Roman chests
    /// (BlenderSrc/props/chest_variants.py → A1/Props/PR_ArmoryChest_&lt;V&gt;.fbx) spread over the ladder by each
    /// arena's palette, so neighbours always differ. Writes ArenaLayoutDef.chestModel (a rebuilt arena keeps its chest)
    /// and sets the Interactables of each built scene, so no arena needs a full rebuild.
    /// </summary>
    public static class ChestSetup
    {
        public const string ModelDir = "Assets/Art/Models/A1/Props/PR_ArmoryChest_";

        /// <summary>Layout name, scene path, design: A legion strongbox, B weapon crate, C shield chest, D marble cista.</summary>
        public static readonly (string layout, string scene, char variant)[] Arenas =
        {
            ("SlavePit", "Assets/Scenes/Arenas/SlavePit.unity", 'B'),                   // the crude crate in the mud pit
            ("ProvincialPit", "Assets/Scenes/Arenas/ProvincialPit.unity", 'C'),         // wood and a red shield
            ("StoneAmphitheater", "Assets/Scenes/Arenas/StoneAmphitheater.unity", 'A'), // dark iron on sandstone
            ("GrandArena", "Assets/Scenes/Arenas/GrandArena.unity", 'D'),               // marble among the patrons' marble
            ("ImperialArena", "Assets/Scenes/Arenas/ImperialArena.unity", 'C'),         // crimson and gold
            ("Colosseum", "Assets/Scenes/Arenas/Colosseum.unity", 'A'),                 // the imperial eagle
            ("Playground", "Assets/Scenes/Dev/Playground.unity", 'A'),
        };

        public static GameObject Model(char variant) => AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + variant + ".fbx");

        [MenuItem("Gladiator/Setup/Arenas/Assign Armory Chests")]
        public static string Assign()
        {
            var log = new StringBuilder("[ChestSetup]");
            string back = EditorSceneManager.GetActiveScene().path;
            foreach (var (layoutName, scenePath, variant) in Arenas)
            {
                var model = Model(variant);
                if (model == null) { log.Append(" missing ").Append(ModelDir).Append(variant).Append(".fbx;"); continue; }
                var layout = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>("Assets/Data/Arenas/AL_" + layoutName + ".asset");
                if (layout != null && layout.chestModel != model)
                {
                    layout.chestModel = model;
                    EditorUtility.SetDirty(layout);
                }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { log.Append(' ').Append(layoutName).Append(": no scene;"); continue; }
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var props = Object.FindAnyObjectByType<Interactables>();
                if (props == null) { log.Append(' ').Append(layoutName).Append(": no Interactables;"); continue; }
                if (props.chestModel != model)
                {
                    props.chestModel = model;
                    EditorUtility.SetDirty(props);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                log.Append(' ').Append(layoutName).Append('=').Append(variant);
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log(log.ToString());
            return log.ToString();
        }
    }
}
