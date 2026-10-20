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
        public const string ChampionEnemyPath = "Assets/Data/Enemies/EN_Champion.asset";
        public const string ChampionPath = "Assets/Data/Bosses/BD_Champion.asset";
        public const string BeastLordEnemyPath = "Assets/Data/Enemies/EN_BeastLord.asset";
        public const string BeastLordPath = "Assets/Data/Bosses/BD_BeastLord.asset";

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

        /// <summary>
        /// The Provincial Champion (docs/BOSSES.md): a murmillo behind a tower shield (his body blocks 90 % from a 140°
        /// front), Shield Bash / Gladius Flurry / Shield Charge in both phases, stunned with his guard down when the
        /// charge meets stone, Shield Archers and Thugs as guards, and a phase-2 roar that fires every spike trap.
        /// </summary>
        [MenuItem("Gladiator/Setup/Bosses/Create Champion Data")]
        public static BossDef EnsureChampion()
        {
            var body = AssetDatabase.LoadAssetAtPath<EnemyDef>(ChampionEnemyPath);
            if (body == null)
            {
                body = PlayerSetup.EnsureAsset<EnemyDef>(ChampionEnemyPath);
                body.id = "champion";
                body.displayName = "The Provincial Champion";
                body.behaviour = EnemyBehaviour.Scripted;
                body.maxHp = 13000f;
                body.radius = 1.0f;
                body.knockbackTaken = 0f;
                body.blockArc = 140f;
                body.blockReduction = 0.9f;
                body.contactDamage = 0f;
                body.moveSpeed = 0f;
                body.xp = 0f;
                body.goldChance = 0f;
                body.prewarm = 1;
            }
            if (body.model == null)
                body.model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Enemies/EN_Champion.fbx");
            EditorUtility.SetDirty(body);

            if (!AssetDatabase.IsValidFolder("Assets/Data/Bosses")) AssetDatabase.CreateFolder("Assets/Data", "Bosses");
            var boss = AssetDatabase.LoadAssetAtPath<BossDef>(ChampionPath);
            if (boss == null)
            {
                boss = PlayerSetup.EnsureAsset<BossDef>(ChampionPath);
                boss.id = "champion";
                boss.displayName = "THE PROVINCIAL CHAMPION"; // the boss bar reads "NAME, TITLE"
                boss.title = "UNDEFEATED";
                boss.enterText = "THE CHAMPION OF THE PROVINCE!";
                boss.fallText = "THE CHAMPION FALLS!";
                boss.guardsText = "\"TO ME, BROTHERS!\"";
                boss.phase2Text = "\"FOR THE PROVINCE!\"";
                boss.phase1Attacks = new[] { BossAttack.Bash, BossAttack.Flurry, BossAttack.Charge };
                boss.phase2Attacks = new[] { BossAttack.Bash, BossAttack.Flurry, BossAttack.Charge };
                boss.moveSpeed = 3.2f; boss.enragedMoveSpeed = 3.8f; boss.turnRate = 110f; // slow to turn: get to his side
                boss.contactDamage = 12f; boss.recover = 0.6f;
                boss.bashRange = 4f; boss.bashAngle = 100f; boss.bashWindup = 0.7f; boss.bashDamage = 22f; boss.bashKnockback = 11f;
                boss.flurryLength = 6f; boss.flurryWidth = 1.1f; boss.flurryWindup = 0.35f; boss.flurryDamage = 12f;
                boss.flurryThrusts = new Vector2Int(3, 5);
                boss.chargeWindup = 1.0f; boss.chargeSpeed = 16f; boss.chargeLength = 18f; boss.chargeWidth = 2.4f;
                boss.chargeDamage = 26f; boss.chargeKnockback = 13f; boss.chargeStun = 2f; boss.stunDamageTakenMul = 1.5f;
                boss.chargeRepeats = new Vector2Int(1, 2);
                boss.guardsEvery = 25f;
                boss.guards = new[]
                {
                    AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_ShieldArcher.asset"),
                    AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_ShieldThug.asset"),
                };
                boss.guardCount = new Vector2Int(4, 6);
                boss.phase2At = 0.5f; boss.enrageSpeed = 1.25f; boss.enrageRoar = 1.5f;
                boss.phase2FireRing = false;
                boss.phase2SpikeVolley = true;
                boss.rewardFloor = Rarity.Rare;
            }
            if (boss.shortName == "Gorm") // made before the summary words existed: Gorm's defaults
            {
                boss.shortName = "The Champion";
                boss.deathLine = "The Champion salutes the stands over you.";
                boss.victoryLine = "The Province's champion is down. The city will hear your name.";
            }
            boss.enemy = body;
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            return boss;
        }

        /// <summary>
        /// The Beast Lord (docs/BOSSES.md): no shield, a goad. Goad Sweep (the cone), Hook Thrust (the lane, with Slow)
        /// and Pounce (a circle that trails you, then he leaps onto it); every 20 s his horn opens the cage nearest him
        /// (3-5 war boars) and two Pit Curs come through the tunnels; phase 2 opens every cage at once and he pounces
        /// twice in a row. At his fall the beasts scatter.
        /// </summary>
        [MenuItem("Gladiator/Setup/Bosses/Create Beast Lord Data")]
        public static BossDef EnsureBeastLord()
        {
            var body = AssetDatabase.LoadAssetAtPath<EnemyDef>(BeastLordEnemyPath);
            if (body == null)
            {
                body = PlayerSetup.EnsureAsset<EnemyDef>(BeastLordEnemyPath);
                body.id = "beastlord";
                body.displayName = "The Beast Lord";
                body.behaviour = EnemyBehaviour.Scripted;
                body.maxHp = 15000f;
                body.radius = 1.1f;
                body.knockbackTaken = 0f;
                body.blockArc = 0f;
                body.contactDamage = 0f;
                body.moveSpeed = 0f;
                body.xp = 0f;
                body.goldChance = 0f;
                body.prewarm = 1;
            }
            if (body.model == null)
                body.model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Enemies/EN_BeastLord.fbx");
            EditorUtility.SetDirty(body);

            if (!AssetDatabase.IsValidFolder("Assets/Data/Bosses")) AssetDatabase.CreateFolder("Assets/Data", "Bosses");
            var boss = AssetDatabase.LoadAssetAtPath<BossDef>(BeastLordPath);
            if (boss == null)
            {
                boss = PlayerSetup.EnsureAsset<BossDef>(BeastLordPath);
                boss.id = "beastlord";
                boss.displayName = "THE BEAST LORD"; // the boss bar reads "NAME, TITLE"
                boss.title = "MASTER OF THE PENS";
                boss.enterText = "THE BEAST LORD!";
                boss.fallText = "THE BEAST LORD FALLS!";
                boss.guardsText = "THE PENS OPEN!";
                boss.phase2Text = "\"FEED THEM!\"";
                boss.shortName = "The Beast Lord";
                boss.deathLine = "The Beast Lord throws you to his boars.";
                boss.victoryLine = "The Beast Lord is down and his beasts scatter. The whole city is on its feet.";
                boss.phase1Attacks = new[] { BossAttack.Bash, BossAttack.Whip, BossAttack.Pounce };
                boss.phase2Attacks = new[] { BossAttack.Bash, BossAttack.Whip, BossAttack.Pounce };
                boss.moveSpeed = 3.4f; boss.enragedMoveSpeed = 4.2f; boss.turnRate = 200f;
                boss.contactDamage = 12f; boss.recover = 0.6f;
                // Goad Sweep: a wide cone round his front
                boss.bashRange = 5f; boss.bashAngle = 150f; boss.bashWindup = 0.8f; boss.bashDamage = 24f; boss.bashKnockback = 11f;
                // Hook Thrust: a lane, and the hook drags (Slow)
                boss.whipLength = 11f; boss.whipWidth = 1.4f; boss.whipWindup = 0.6f; boss.whipDamage = 20f;
                boss.whipSlow = 0.5f; boss.whipSlowSeconds = 2f;
                boss.pounceRadius = 3.5f; boss.pounceWindup = 1.0f; boss.pounceTrack = 0.7f; boss.pounceFlight = 0.55f;
                boss.pounceHeight = 3f; boss.pounceRange = 24f; boss.pounceDamage = 28f; boss.pounceKnockback = 9f;
                boss.pounceRecover = 0.8f; boss.pounceRepeats = new Vector2Int(1, 2);
                boss.beastPack = new Vector2Int(3, 5);
                boss.phase2CagePack = 3;
                boss.scatterOnDefeat = true;
                boss.guardsEvery = 20f;
                boss.guards = new[] { AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_PitCur.asset") };
                boss.guardCount = new Vector2Int(2, 2);
                boss.phase2At = 0.5f; boss.enrageSpeed = 1.25f; boss.enrageRoar = 1.5f;
                boss.phase2FireRing = false;
                boss.phase2SpikeVolley = false;
                boss.rewardFloor = Rarity.Rare;
            }
            boss.enemy = body;
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            return boss;
        }
    }
}
