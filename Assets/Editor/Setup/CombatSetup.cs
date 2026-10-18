using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Combat data + FX assets. Data assets are created once with starting values and then belong to the designer
    /// (never overwritten); materials are (re)configured every run.
    /// </summary>
    public static class CombatSetup
    {
        public const string FeedbackPath = "Assets/Data/Config/HitFeedbackConfig.asset";
        public const string ConvictPath = "Assets/Data/Enemies/EN_Convict.asset";
        public const string ConvictAnimPath = "Assets/Data/Config/PartAnim_Convict.asset";
        public const string GladiusPath = "Assets/Data/Weapons/WP_Gladius.asset";
        public const string SlashMat = LookSetup.MatDir + "/M_FxSlash.mat";
        public const string BlobMat = LookSetup.MatDir + "/M_BlobShadow.mat";
        public const string ParticleMat = LookSetup.MatDir + "/M_FxParticles.mat";
        public const string SlashMesh = "Assets/Art/Models/FX/FX_SlashArc.fbx";
        public const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const string FontOutlinePath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";
        public const string ProgressionPath = "Assets/Data/Config/ProgressionConfig.asset";

        [MenuItem("Gladiator/Setup/Combat/Create Or Update Combat Assets")]
        public static string CreateOrUpdate()
        {
            PlayerSetup.EnsureAsset<HitFeedbackConfig>(FeedbackPath);
            PlayerSetup.EnsureAsset<ProgressionConfig>(ProgressionPath);

            bool newAnim = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(ConvictAnimPath) == null;
            var anim = PlayerSetup.EnsureAsset<PartAnimProfile>(ConvictAnimPath);
            if (newAnim)
            {
                anim.strideLength = 1.0f; anim.legSwing = 38f; anim.armSwing = 20f; anim.bobHeight = 0.06f; anim.fullRunSpeed = 3.6f;
                EditorUtility.SetDirty(anim);
            }

            bool newConvict = AssetDatabase.LoadAssetAtPath<EnemyDef>(ConvictPath) == null;
            var convict = PlayerSetup.EnsureAsset<EnemyDef>(ConvictPath);
            if (newConvict)
            {
                convict.id = "convict";
                convict.displayName = "Starving Convict";
                convict.model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Enemies/EN_Convict.fbx");
                convict.anim = anim;
                convict.maxHp = 20f; convict.moveSpeed = 3.6f; convict.radius = 0.36f; convict.contactDamage = 8f; convict.xp = 1f;
                EditorUtility.SetDirty(convict);
            }

            bool newGladius = AssetDatabase.LoadAssetAtPath<WeaponDef>(GladiusPath) == null;
            var gladius = PlayerSetup.EnsureAsset<WeaponDef>(GladiusPath);
            if (newGladius)
            {
                gladius.id = "gladius";
                gladius.displayName = "Rusty Gladius";
                gladius.description = "Fast arcs at the nearest enemy.";
                gladius.pattern = WeaponPattern.MeleeArc;
                EditorUtility.SetDirty(gladius);
            }

            var slash = MakeMat(SlashMat, "Gladiator/FxSlash");
            var blob = MakeMat(BlobMat, "Gladiator/BlobShadow");
            blob.enableInstancing = true;
            var particles = MakeMat(ParticleMat, "Universal Render Pipeline/Particles/Unlit");
            particles.SetColor("_BaseColor", Color.white);
            particles.enableInstancing = true;
            EditorUtility.SetDirty(slash); EditorUtility.SetDirty(blob); EditorUtility.SetDirty(particles);
            AssetDatabase.SaveAssets();
            return "combat assets ok";
        }

        static Material MakeMat(string path, string shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }
    }
}
