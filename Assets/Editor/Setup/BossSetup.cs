using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Gorm, the Pit Master (docs/BOSSES.md): his body (EnemyDef, behaviour Scripted) and his tuning (BossDef), created
    /// once with the spec's starting numbers; designer-owned after that (only the model link is repaired).
    /// </summary>
    public static class BossSetup
    {
        public const string GormEnemyPath = "Assets/Data/Enemies/EN_Gorm.asset";
        public const string GormPath = "Assets/Data/Bosses/BD_Gorm.asset";

        [MenuItem("Gladiator/Setup/Bosses/Create Boss Data")]
        public static BossDef EnsureGorm()
        {
            var body = AssetDatabase.LoadAssetAtPath<EnemyDef>(GormEnemyPath);
            if (body == null)
            {
                body = PlayerSetup.EnsureAsset<EnemyDef>(GormEnemyPath);
                body.id = "gorm";
                body.displayName = "Gorm, the Pit Master";
                body.behaviour = EnemyBehaviour.Scripted;
                body.maxHp = 9000f;
                body.radius = 1.05f;
                body.knockbackTaken = 0f;
                body.contactDamage = 0f; // BossController handles touching him
                body.moveSpeed = 0f;
                body.xp = 0f;
                body.goldChance = 0f;
                body.prewarm = 1;
            }
            if (body.model == null)
                body.model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Enemies/EN_Gorm.fbx");
            EditorUtility.SetDirty(body);

            if (!AssetDatabase.IsValidFolder("Assets/Data/Bosses")) AssetDatabase.CreateFolder("Assets/Data", "Bosses");
            var boss = AssetDatabase.LoadAssetAtPath<BossDef>(GormPath);
            if (boss == null)
            {
                boss = PlayerSetup.EnsureAsset<BossDef>(GormPath);
                boss.id = "gorm";
                boss.displayName = "GORM";
                boss.title = "THE PIT MASTER";
                boss.guards = new[]
                {
                    AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_ShieldThug.asset"),
                    AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset"),
                    AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset"),
                };
            }
            boss.enemy = body;
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            return boss;
        }
    }
}
