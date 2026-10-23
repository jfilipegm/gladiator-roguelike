using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Every boss's body (EnemyDef, behaviour Scripted) and tuning (BossDef) (docs/BOSSES.md): Gorm, the Provincial
    /// Champion, the Beast Lord, the Gilded Patron, the Emperor's Champion and the Eternal Victor, each created once with
    /// the spec's starting numbers; designer-owned after that (only the model and guard links are repaired).
    /// </summary>
    public static class BossSetup
    {
        public const string GormEnemyPath = "Assets/Data/Enemies/EN_Gorm.asset";
        public const string GormPath = "Assets/Data/Bosses/BD_Gorm.asset";
        public const string ChampionEnemyPath = "Assets/Data/Enemies/EN_Champion.asset";
        public const string ChampionPath = "Assets/Data/Bosses/BD_Champion.asset";
        public const string BeastLordEnemyPath = "Assets/Data/Enemies/EN_BeastLord.asset";
        public const string BeastLordPath = "Assets/Data/Bosses/BD_BeastLord.asset";
        public const string PatronEnemyPath = "Assets/Data/Enemies/EN_GildedPatron.asset";
        public const string PatronPath = "Assets/Data/Bosses/BD_GildedPatron.asset";
        public const string EmperorsChampionEnemyPath = "Assets/Data/Enemies/EN_EmperorsChampion.asset";
        public const string EmperorsChampionPath = "Assets/Data/Bosses/BD_EmperorsChampion.asset";
        public const string EternalVictorEnemyPath = "Assets/Data/Enemies/EN_EternalVictor.asset";
        public const string EternalVictorPath = "Assets/Data/Bosses/BD_EternalVictor.asset";
        /// <summary>His guard (made by ImperialArenaSetup): loaded by path so this file doesn't depend on it.</summary>
        const string CenturionPath = "Assets/Data/Enemies/EN_Centurion.asset";
        /// <summary>The Eternal Victor's dead (made by ColosseumSetup): loaded by path for the same reason.</summary>
        const string LegionnairePath = "Assets/Data/Enemies/EN_SkeletalLegionnaire.asset";

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
                body.model = ModelStyle.Load("Assets/Art/Models/Enemies/EN_Gorm.fbx");
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
                body.model = ModelStyle.Load("Assets/Art/Models/Enemies/EN_Champion.fbx");
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
                body.model = ModelStyle.Load("Assets/Art/Models/Enemies/EN_BeastLord.fbx");
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
            /// <summary>
        /// The Gilded Patron (docs/BOSSES.md): carried on a litter, no shield. Coin Bombs (purses that burst into gold),
        /// Golden Volley (a fan of red-hot coins) and Shove (the cone); every 22 s he hires 4-6 Gilded Mercenaries who
        /// jump down from the patron boxes nearest you, and while any of them lives he takes 40 % of the damage
        /// (payroll). Phase 2 fires every jet and spins every blade, five bombs, the wide volley. His fortune is paid
        /// out when he falls.
        /// </summary>
        [MenuItem("Gladiator/Setup/Bosses/Create Gilded Patron Data")]
        public static BossDef EnsureGildedPatron()
        {
            var body = AssetDatabase.LoadAssetAtPath<EnemyDef>(PatronEnemyPath);
            if (body == null)
            {
                body = PlayerSetup.EnsureAsset<EnemyDef>(PatronEnemyPath);
                body.id = "patron";
                body.displayName = "The Gilded Patron";
                body.behaviour = EnemyBehaviour.Scripted;
                body.maxHp = 15000f;
                body.radius = 1.6f;
                body.knockbackTaken = 0f;
                body.blockArc = 0f;
                body.contactDamage = 0f;
                body.moveSpeed = 0f;
                body.xp = 0f;
                body.goldChance = 0f;
                body.prewarm = 1;
            }
            if (body.model == null)
                body.model = ModelStyle.Load("Assets/Art/Models/Enemies/EN_GildedPatron.fbx");
            EditorUtility.SetDirty(body);

            if (!AssetDatabase.IsValidFolder("Assets/Data/Bosses")) AssetDatabase.CreateFolder("Assets/Data", "Bosses");
            var merc = AssetDatabase.LoadAssetAtPath<EnemyDef>(GrandArenaSetup.MercenaryPath);
            var boss = AssetDatabase.LoadAssetAtPath<BossDef>(PatronPath);
            if (boss == null)
            {
                boss = PlayerSetup.EnsureAsset<BossDef>(PatronPath);
                boss.id = "patron";
                boss.displayName = "THE GILDED PATRON"; // the boss bar reads "NAME, TITLE"
                boss.title = "OWNER OF THE GRAND ARENA";
                boss.enterText = "THE GILDED PATRON!";
                boss.fallText = "THE PATRON IS BANKRUPT!";
                boss.guardsText = "GOLD TALKS!";
                boss.phase2Text = "\"EVERYTHING IS FOR SALE!\"";
                boss.shortName = "The Gilded Patron";
                boss.deathLine = "The Gilded Patron writes you off as a bad investment.";
                boss.victoryLine = "The Patron is bankrupt and his gold is in the sand. The boxes chant your name.";
                boss.phase1Attacks = new[] { BossAttack.CoinBombs, BossAttack.Volley, BossAttack.Bash };
                boss.phase2Attacks = new[] { BossAttack.CoinBombs, BossAttack.Volley, BossAttack.Bash };
                boss.moveSpeed = 2.6f; boss.enragedMoveSpeed = 3.2f; boss.turnRate = 120f; // a litter turns slowly
                boss.contactDamage = 10f; boss.recover = 0.7f;
                // Shove: the bearers barge forward in a cone
                boss.bashRange = 4.5f; boss.bashAngle = 120f; boss.bashWindup = 0.7f; boss.bashDamage = 20f; boss.bashKnockback = 12f;
                boss.bombCount = new Vector2Int(3, 5); boss.bombWindup = 0.6f; boss.bombInterval = 0.35f; boss.bombFlight = 1.0f;
                boss.bombRadius = 2.4f; boss.bombDamage = 22f; boss.bombCoins = 3; boss.bombSpread = 3.5f;
                boss.volleyCount = new Vector2Int(7, 11); boss.volleyArc = new Vector2(50f, 70f); boss.volleyWindup = 0.8f;
                boss.volleySpeed = 15f; boss.volleyDamage = 12f; boss.volleyRange = 24f;
                boss.guardsEvery = 22f;
                boss.guardCount = new Vector2Int(4, 6);
                boss.guardSpotsUsed = 2;
                boss.guardedDamageMul = 0.6f;
                boss.defeatGold = 300;
                boss.phase2At = 0.5f; boss.enrageSpeed = 1.25f; boss.enrageRoar = 1.5f;
                boss.phase2FireRing = false;
                boss.phase2SpikeVolley = false;
                boss.phase2FireJets = true;
                boss.phase2Blades = true;
                boss.rewardFloor = Rarity.Rare;
            }
            if (merc != null && (boss.guards == null || boss.guards.Length == 0)) boss.guards = new[] { merc };
            if (merc != null && boss.guardedBy == null) boss.guardedBy = merc;
            boss.enemy = body;
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            return boss;
        }

        /// <summary>
        /// The Emperor's Champion (docs/BOSSES.md, Arena 5): spear, scutum at his side (blocks 50 % from a 100° front)
        /// and javelins. Spear Thrust (the lane), Shield Bash (the cone) and a Javelin Volley thrown at the player's height;
        /// he strides in when the player is far. Phase 2 (60 %): "PRAETORIANS, TO ME!", two Centurions with their lines
        /// through the two gates nearest the player, again every 26 s, and the Shield Charge (stunned on stone, takes
        /// ×1.5). Phase 3 (30 %): "FOR THE EMPEROR!", faster, the charge twice in a row, and the chariot races run back
        /// to back (ChariotRace reads his phase). At his fall the Guard stands down (every enemy is dragged off).
        /// </summary>
        [MenuItem("Gladiator/Setup/Bosses/Create Emperor's Champion Data")]
        public static BossDef EnsureEmperorsChampion()
        {
            var body = AssetDatabase.LoadAssetAtPath<EnemyDef>(EmperorsChampionEnemyPath);
            if (body == null)
            {
                body = PlayerSetup.EnsureAsset<EnemyDef>(EmperorsChampionEnemyPath);
                body.id = "emperors_champion";
                body.displayName = "The Emperor's Champion";
                body.behaviour = EnemyBehaviour.Scripted;
                body.maxHp = 18000f;
                body.radius = 1.25f;
                body.knockbackTaken = 0f;
                body.blockArc = 100f; // the scutum at his side: half of what hits him from the front
                body.blockReduction = 0.5f;
                body.contactDamage = 0f; // BossController handles touching him
                body.moveSpeed = 0f;
                body.xp = 0f;
                body.goldChance = 0f;
                body.prewarm = 1;
            }
            if (body.model == null)
                body.model = ModelStyle.Load("Assets/Art/Models/Enemies/EN_EmperorsChampion.fbx");
            EditorUtility.SetDirty(body);

            if (!AssetDatabase.IsValidFolder("Assets/Data/Bosses")) AssetDatabase.CreateFolder("Assets/Data", "Bosses");
            var centurion = AssetDatabase.LoadAssetAtPath<EnemyDef>(CenturionPath);
            var boss = AssetDatabase.LoadAssetAtPath<BossDef>(EmperorsChampionPath);
            if (boss == null)
            {
                boss = PlayerSetup.EnsureAsset<BossDef>(EmperorsChampionPath);
                boss.id = "emperors_champion";
                boss.displayName = "THE EMPEROR'S CHAMPION"; // the boss bar reads "NAME, TITLE"
                boss.title = "FIRST SPEAR OF THE EMPIRE";
                boss.enterText = "THE EMPEROR'S CHAMPION!";
                boss.fallText = "THE EMPEROR'S CHAMPION FALLS!";
                boss.guardsText = "THE PRAETORIAN GUARD!";
                boss.phase2Text = "\"PRAETORIANS, TO ME!\"";
                boss.phase3Text = "\"FOR THE EMPEROR!\"";
                boss.shortName = "Emperor's Champion"; // "The Emperor's Champion" overran the summary row
                boss.deathLine = "The Emperor's Champion turns to the imperial box. The thumb goes down.";
                boss.victoryLine = "The Emperor's own champion lies in the sand, and the Emperor is on his feet.";
                boss.phase1Attacks = new[] { BossAttack.Whip, BossAttack.Bash, BossAttack.Volley };
                boss.phase2Attacks = new[] { BossAttack.Whip, BossAttack.Bash, BossAttack.Volley, BossAttack.Charge };
                boss.phase3Attacks = new[] { BossAttack.Whip, BossAttack.Bash, BossAttack.Volley, BossAttack.Charge };
                boss.moveSpeed = 3.4f; boss.enragedMoveSpeed = 3.9f; boss.phase3MoveSpeed = 4.6f; boss.turnRate = 160f;
                boss.closeInRange = 28f; boss.closeInSpeed = 9.5f; // a 190 m floor: he strides in, faster than a 7.5 m/s runner (6.5 never caught a kiting bot: 0 attacks in 39 s)
                boss.contactDamage = 12f; boss.contactInterval = 0.8f; boss.recover = 0.6f;
                // Spear Thrust: a lane, a lunge down it, no slow
                boss.whipLength = 10f; boss.whipWidth = 1.3f; boss.whipWindup = 0.65f; boss.whipDamage = 22f;
                boss.whipSlow = 1f; boss.whipSlowSeconds = 0f;
                // Shield Bash: the scutum punched forward
                boss.bashRange = 4.5f; boss.bashAngle = 110f; boss.bashWindup = 0.65f; boss.bashDamage = 20f; boss.bashKnockback = 12f;
                // Javelin Volley: thrown at your height (the spina is no shelter); the obelisk and the metae stop them
                boss.volleyCount = new Vector2Int(5, 7); boss.volleyArc = new Vector2(36f, 50f); boss.volleyWindup = 0.85f;
                boss.volleySpeed = 22f; boss.volleyDamage = 16f; boss.volleyRange = 30f; boss.volleyJavelins = true;
                // Shield Charge: stunned on stone with his guard down
                boss.chargeWindup = 0.9f; boss.chargeSpeed = 17f; boss.chargeLength = 20f; boss.chargeWidth = 2.6f;
                boss.chargeDamage = 26f; boss.chargeKnockback = 13f; boss.chargeStun = 1.8f; boss.stunDamageTakenMul = 1.5f;
                boss.chargeRepeats = new Vector2Int(1, 1); boss.phase3ChargeRepeats = 2;
                // the Praetorian Guard: a Centurion and his line per gate, from phase 2
                boss.guardCount = new Vector2Int(2, 2);
                boss.guardsEvery = 26f;
                boss.guardsFromPhase = 2;
                boss.guardsNearestGates = true;
                boss.guardSpotsUsed = 2;
                boss.phase2At = 0.6f; boss.enrageSpeed = 1.2f; boss.enrageRoar = 1.5f;
                boss.phase3At = 0.3f; boss.phase3Speed = 1.4f;
                boss.phase2FireRing = false;
                boss.phase2SpikeVolley = false;
                boss.phase2FireJets = false;
                boss.phase2Blades = false;
                boss.scatterOnDefeat = true; // the Guard stands down
                boss.rewardFloor = Rarity.Rare;
                boss.defeatGold = 0;
            }
            if (centurion != null && (boss.guards == null || boss.guards.Length == 0)) boss.guards = new[] { centurion };
            boss.enemy = body;
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            return boss;
        }

        /// <summary>
        /// The Eternal Victor (docs/BOSSES.md, Arena 6): the Colosseum's first champion, a gaunt bone-limbed thraex with a
        /// two-handed falx and no shield. He rises from the stage's lift (no gate). Reaper's Circle (the ring), Victor's
        /// Lunges (the lanes), the Falx Throw (a lane: out and back, stopped by stone) and the Leap (a circle on you).
        /// Phase 2 (65 %): "RISE, BROTHERS!", Skeletal Legionnaires at the two lifts nearest you (the arena's guard spots)
        /// every 24 s, and the Grasp of the Dead (a lane that holds you). Phase 3 (30 %): "THE ARENA IS MINE!", faster,
        /// two leaps and two throws in a row. The first 0 HP leaves his remains (1,500 HP for 8 s); destroy them or he
        /// rises with 20 %. At his fall his dead crumble (every enemy is dragged off). The Hypogeum does the arena's part.
        /// </summary>
        [MenuItem("Gladiator/Setup/Bosses/Create Eternal Victor Data")]
        public static BossDef EnsureEternalVictor()
        {
            var body = AssetDatabase.LoadAssetAtPath<EnemyDef>(EternalVictorEnemyPath);
            if (body == null)
            {
                body = PlayerSetup.EnsureAsset<EnemyDef>(EternalVictorEnemyPath);
                body.id = "eternal_victor";
                body.displayName = "The Eternal Victor";
                body.behaviour = EnemyBehaviour.Scripted;
                body.maxHp = 22000f;
                body.radius = 1.0f; // the falx isn't body
                body.knockbackTaken = 0f;
                body.blockArc = 0f; // no shield: the first boss since Gorm with no damage reduction
                body.contactDamage = 0f; // BossController handles touching him
                body.moveSpeed = 0f;
                body.xp = 0f;
                body.goldChance = 0f;
                body.prewarm = 1;
            }
            if (body.model == null)
                body.model = ModelStyle.Load("Assets/Art/Models/Enemies/EN_EternalVictor.fbx");
            EditorUtility.SetDirty(body);

            if (!AssetDatabase.IsValidFolder("Assets/Data/Bosses")) AssetDatabase.CreateFolder("Assets/Data", "Bosses");
            var legionnaire = AssetDatabase.LoadAssetAtPath<EnemyDef>(LegionnairePath);
            var boss = AssetDatabase.LoadAssetAtPath<BossDef>(EternalVictorPath);
            if (boss == null)
            {
                boss = PlayerSetup.EnsureAsset<BossDef>(EternalVictorPath);
                boss.id = "eternal_victor";
                boss.displayName = "THE ETERNAL VICTOR"; // the boss bar reads "NAME, TITLE"
                boss.title = "FIRST CHAMPION OF THE COLOSSEUM";
                boss.enterText = "THE ETERNAL VICTOR!";
                boss.fallText = "THE ETERNAL VICTOR FALLS!";
                boss.guardsText = "THE DEAD RISE!";
                boss.phase2Text = "\"RISE, BROTHERS!\"";
                boss.phase3Text = "\"THE ARENA IS MINE!\"";
                boss.remainsText = "HE WILL NOT STAY DOWN!";
                boss.riseText = "\"NOT YET.\"";
                boss.shortName = "Eternal Victor"; // the summary row's label: "The Eternal Victor" is as long as the row that overran
                // the summary's subtitle (1,400 px at 36 px) holds ~80 characters: the spec's lines (82 and 103) are trimmed
                boss.deathLine = "The Eternal Victor salutes the crowd and carves your name into the podium.";
                boss.victoryLine = "The first champion lies still at last. Forty thousand chant a new name: yours.";
                boss.phase1Attacks = new[] { BossAttack.Sweep, BossAttack.Flurry, BossAttack.Disc, BossAttack.Pounce };
                boss.phase2Attacks = new[] { BossAttack.Sweep, BossAttack.Flurry, BossAttack.Whip, BossAttack.Disc, BossAttack.Pounce };
                boss.phase3Attacks = new[] { BossAttack.Sweep, BossAttack.Flurry, BossAttack.Whip, BossAttack.Disc, BossAttack.Pounce };
                boss.moveSpeed = 3.6f; boss.enragedMoveSpeed = 4.0f; boss.phase3MoveSpeed = 4.6f; boss.turnRate = 200f;
                boss.closeInRange = 30f; boss.closeInSpeed = 7f; // a 196 m floor: he strides in
                boss.contactDamage = 12f; boss.contactInterval = 0.8f; boss.recover = 0.55f;
                // Reaper's Circle: the falx out at knee height in a full turn; jump it
                boss.sweepRadius = 5.5f; boss.sweepWindup = 1.0f; boss.sweepDamage = 26f; boss.sweepKnockback = 9f; boss.sweepJumpClear = 0.7f;
                // Victor's Lunges: two thrusts, three from phase 2
                boss.flurryLength = 7f; boss.flurryWidth = 1.2f; boss.flurryWindup = 0.4f; boss.flurryDamage = 14f;
                boss.flurryThrusts = new Vector2Int(2, 3);
                // Grasp of the Dead: the falx into the deck, bone hands along the lane; held (×0.1) for 1 s
                boss.whipLength = 16f; boss.whipWidth = 2.2f; boss.whipWindup = 0.8f; boss.whipDamage = 18f;
                boss.whipSlow = 0.1f; boss.whipSlowSeconds = 1.0f; boss.whipGrasp = true;
                // the Falx Throw: out along the lane and back, once each way; it can't be jumped and stops on stone
                boss.discLength = 20f; boss.discWidth = 1.6f; boss.discWindup = 0.8f; boss.discSpeed = 26f;
                boss.discDamage = 20f; boss.discKnockback = 6f; boss.discSpin = 1080f;
                boss.discRepeats = new Vector2Int(1, 1); boss.phase3DiscRepeats = 2; boss.discPart = "Falx";
                // Leap of the Victor: onto the stage or a hill after you; twice in a row in phase 3
                boss.pounceRadius = 3.5f; boss.pounceWindup = 1.0f; boss.pounceTrack = 0.7f; boss.pounceFlight = 0.55f;
                boss.pounceHeight = 3.5f; boss.pounceRange = 26f; boss.pounceDamage = 26f; boss.pounceKnockback = 9f;
                boss.pounceRecover = 0.7f; boss.pounceRepeats = new Vector2Int(1, 1); boss.phase3PounceRepeats = 2;
                // his dead, up the lifts (the arena's guard spots) nearest you, from phase 2
                boss.guardCount = new Vector2Int(6, 8);
                boss.guardsEvery = 24f;
                boss.guardsFromPhase = 2;
                boss.guardSpotsUsed = 2;
                boss.guardsNearestGates = false;
                boss.guardThreadColor = new Color(0.89f, 0.84f, 0.74f, 0.9f); // bone, not gold
                boss.phase2At = 0.65f; boss.enrageSpeed = 1.2f; boss.enrageRoar = 1.5f;
                boss.phase3At = 0.3f; boss.phase3Speed = 1.4f;
                boss.phase2FireRing = false;
                boss.phase2SpikeVolley = false;
                boss.phase2FireJets = false;
                boss.phase2Blades = false;
                boss.phase2CagePack = 0;
                boss.beastPack = Vector2Int.zero;
                boss.scatterOnDefeat = true; // his dead crumble
                boss.rewardFloor = Rarity.Rare;
                boss.defeatGold = 0;
                // he rises from the stage's lift (no gate)
                boss.entranceRise = true; boss.riseDepth = 3f; boss.riseTime = 2.6f;
                // Eternal: the first 0 HP leaves his remains
                boss.falseDeath = true;
                boss.remainsHp = 1500f; boss.remainsTime = 8f; boss.riseHp01 = 0.2f;
                boss.riseBurstRadius = 4f; boss.riseBurstDamage = 20f; boss.riseBurstKnockback = 10f; boss.riseBurstWarn = 1f;
                boss.remainsColor = new Color(0.89f, 0.84f, 0.74f, 0.85f);
            }
            if (legionnaire != null && (boss.guards == null || boss.guards.Length == 0)) boss.guards = new[] { legionnaire };
            boss.enemy = body;
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            return boss;
        }
    }
}
