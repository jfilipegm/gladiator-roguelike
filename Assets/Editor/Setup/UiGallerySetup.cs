using Gladiator.Gameplay;
using Gladiator.UI;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// U1: the UI gallery dev scene (Assets/Scenes/Dev/UiGallery.unity): a camera on a dim arena colour, an EventSystem
    /// and a UiGallery that builds every frame, role, look, plate, card and the banner on Start. Run
    /// Gladiator/Setup/UI/All first. Not added to the build.
    /// </summary>
    public static class UiGallerySetup
    {
        public const string ScenePath = "Assets/Scenes/Dev/UiGallery.unity";
        static readonly Color Backdrop = new Color32(0x5C, 0x4E, 0x3F, 0xFF); // dim arena sand

        [MenuItem("Gladiator/Setup/Dev/Build UI Gallery")]
        public static string Build()
        {
            if (AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemeSetup.ThemePath) == null)
                Debug.LogWarning("[UiGallerySetup] no UI theme yet: run Gladiator/Setup/UI/All (the gallery would show plain frames)");
            var scene = SceneSetupUtil.NewEmptyScene();

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backdrop;
            cam.orthographic = true;

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var gallery = new GameObject("UiGallery").AddComponent<UiGallery>();
            gallery.iconAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(IconSetup.AtlasPath);
            var config = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(UpgradeSetup.ConfigPath);
            if (config != null && config.rarityColors != null && config.rarityColors.Length >= 4)
                gallery.rarityColors = (Color[])config.rarityColors.Clone();

            SceneSetupUtil.SaveScene(scene, ScenePath, false);
            Debug.Log("[UiGallerySetup] built " + ScenePath + " (Play it; Tab / 1-4 switch pages)");
            return ScenePath;
        }
    }
}
