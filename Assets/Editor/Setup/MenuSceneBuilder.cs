using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// The main menu scene (M3.5): a small diorama (the Nobody in the pit, Gorm looming in the gloom behind, braziers,
    /// a stretch of pit wall) behind the MainMenu UI, plus the slice's RunDef. The menu is the first scene in the build.
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string RunPath = "Assets/Data/Runs/RD_Slice.asset";

        [MenuItem("Gladiator/Setup/Build Main Menu")]
        public static string Build()
        {
            var run = EnsureRun();
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            var scene = SceneSetupUtil.NewEmptyScene();
            run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath); // the new scene unloaded unused assets
            nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            ArenaSceneCommon.AddLook(new Vector3(28f, 150f, 0f));

            var env = new GameObject("Diorama").transform;
            ArenaSceneCommon.PlaceDecor("Environment/SM_PitFloor", env, Vector3.zero, 0f);
            for (int i = -2; i <= 2; i++)
                ArenaSceneCommon.PlaceDecor("Environment/SM_PitWall_4m", env, new Vector3(i * 4.3f, 0f, -9f), 0f);
            ArenaSceneCommon.PlaceDecor("Environment/SM_Palisade_4m", env, new Vector3(-4.3f, 2.35f, -9.75f), 0f);
            ArenaSceneCommon.PlaceDecor("Environment/PR_Cage", env, new Vector3(-5.2f, 0f, -6.5f), 20f);
            ArenaSceneCommon.PlaceDecor("Environment/PR_ChainPost", env, new Vector3(3.6f, 0f, -2.5f), 150f);
            foreach (var x in new[] { -3.2f, 4.4f })
            {
                var b = ArenaSceneCommon.PlaceDecor("Environment/PR_Brazier", env, new Vector3(x, 0f, -4.2f), 0f);
                var l = new GameObject("FireLight").AddComponent<Light>();
                l.transform.SetParent(b.transform, false);
                l.transform.localPosition = new Vector3(0, 1.6f, 0);
                l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.range = 8f; l.intensity = 4f; l.shadows = LightShadows.None;
            }
            var charMat = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            var hero = ArenaSceneCommon.PlaceDecor("Characters/CH_Nobody", env, new Vector3(1.2f, 0f, 1.2f), 25f); // facing the camera
            LookSetup.AssignMaterial(hero, charMat);
            var gorm = ArenaSceneCommon.PlaceDecor("Enemies/EN_Gorm", env, new Vector3(3.4f, 0f, -5.8f), -20f); // looming, watching
            LookSetup.AssignMaterial(gorm, charMat);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camGo.transform.position = new Vector3(-1.4f, 1.9f, 6.4f);
            camGo.transform.LookAt(new Vector3(1.6f, 1.5f, -1.5f));

            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(CombatSetup.FontPath);
            var fontMat = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.FontOutlinePath);
            var menu = new GameObject("MainMenu").AddComponent<Gladiator.UI.MainMenu>();
            menu.font = font;
            menu.fontMaterial = fontMat;
            menu.run = run;
            menu.hero = hero.transform; // swapped for the picked gladiator
            menu.heroMaterial = charMat;
            foreach (var t in hero.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            menu.runs = ProvincialPitSetup.AllRuns(); // arena select (Phase 5.4)
            menu.meta = MetaSetup.Load();
            GladiatorSetup.CreateOrUpdate();
            menu.gladiators = GladiatorSetup.All(); // locked ones show their unlock's hint
            menu.locked = new[] // not built as data yet
            {
                new Gladiator.UI.MainMenu.Locked { name = "The Murder Machine", hint = "Gorm keeps something chained below the pit." },
            };
            var options = new GameObject("OptionsPanel").AddComponent<Gladiator.UI.OptionsPanel>();
            options.font = font; options.fontMaterial = fontMat;
            new GameObject("SfxPlayer").AddComponent<SfxPlayer>().bank = AssetDatabase.LoadAssetAtPath<SfxBank>(AudioSetup.BankPath);
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            SceneSetupUtil.SaveScene(scene, GameSession.MenuScene);
            MakeFirstInBuild(GameSession.MenuScene);
            return GameSession.MenuScene;
        }

        static RunDef EnsureRun()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Runs")) AssetDatabase.CreateFolder("Assets/Data", "Runs");
            var run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath);
            if (run == null)
            {
                run = PlayerSetup.EnsureAsset<RunDef>(RunPath);
                run.id = "slice";
                run.arenas = new[] { new RunDef.Arena { displayName = "THE SLAVE PIT", scenePath = ArenaBuilder.SlavePitScene } };
                EditorUtility.SetDirty(run);
                AssetDatabase.SaveAssets();
            }
            return run;
        }

        static void MakeFirstInBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
