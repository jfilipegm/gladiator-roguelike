using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Note 35: the Thunder Champion's blink after-images. M_FxBlinkGhost (Gladiator/FxGhost: additive, GPU-instanced,
    /// one shared material) and its link in TR_LightningDash (`ghostMaterial`). Safe to run again: the material keeps
    /// its shader and instancing right (its numbers are the designer's after the first run); the trait's link is only
    /// filled when empty. The HUD's bolt needs nothing here (BlinkIconArt draws it in code).
    /// </summary>
    public static class BlinkFxSetup
    {
        public const string GhostMat = LookSetup.MatDir + "/M_FxBlinkGhost.mat";
        const string ShaderName = "Gladiator/FxGhost";

        [MenuItem("Gladiator/Setup/Gladiators/Blink FX (note 35)")]
        public static string CreateOrUpdate()
        {
            var sh = Shader.Find(ShaderName);
            if (sh == null)
            {
                Debug.LogError("[BlinkFxSetup] no shader " + ShaderName + " (Assets/Art/Shaders/FxGhost.shader): refresh and try again");
                return "no shader";
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(GhostMat);
            if (m == null)
            {
                m = new Material(sh) { name = "M_FxBlinkGhost" };
                m.SetFloat("_Rim", 2f);
                m.SetFloat("_Core", 0.3f);
                m.SetFloat("_Intensity", 1.6f);
                AssetDatabase.CreateAsset(m, GhostMat);
            }
            else if (m.shader != sh) m.shader = sh;
            m.enableInstancing = true; // BlinkGhosts draws with RenderMeshInstanced
            EditorUtility.SetDirty(m);

            var dash = AssetDatabase.LoadAssetAtPath<LightningDashTrait>(GladiatorSetup.DashPath);
            if (dash == null)
            {
                Debug.LogError("[BlinkFxSetup] no " + GladiatorSetup.DashPath + ": run Gladiator/Setup/Gladiators/Create Gladiator Data first");
                AssetDatabase.SaveAssets();
                return "no trait";
            }
            if (dash.ghostMaterial == null)
            {
                dash.ghostMaterial = m;
                EditorUtility.SetDirty(dash);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[BlinkFxSetup] " + GhostMat + " linked to " + GladiatorSetup.DashPath);
            return "blink fx ok";
        }
    }
}
