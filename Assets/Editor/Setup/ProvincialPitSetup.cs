using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Arena 2, the Provincial Pit (Phase 5.4, docs/ARENAS.md): its run (RD_ProvincialPit), its unlock (UL_ProvincialPit:
    /// beat Gorm once), its two new enemies (EN_ShieldArcher, EN_Assassin), its spawn table (ST_ProvincialPit: the Slave
    /// Pit's six, tougher, plus the new two), and the one-click scene build from AL_ProvincialPit. Assets are created
    /// once and are designer-owned afterwards (a missing new enemy is still appended to an existing table).
    /// </summary>
    public static class ProvincialPitSetup
    {
        public const string Scene = "Assets/Scenes/Arenas/ProvincialPit.unity";
        public const string RunPath = "Assets/Data/Runs/RD_ProvincialPit.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_ProvincialPit.asset";
        public const string TablePath = "Assets/Data/Arenas/ST_ProvincialPit.asset";
        const string EnemyDir = "Assets/Data/Enemies/";

        [MenuItem("Gladiator/Arenas/Build Provincial Pit")]
        public static string Build()
        {
            var layout = ProvincialPitLayout.Ensure();
            EnsureEnemies();
            if (layout.boss == null) { layout.boss = BossSetup.EnsureChampion(); layout.bossGate = 0; EditorUtility.SetDirty(layout); } // north gate
            var table = EnsureTable();
            if (layout.spawnTable == null) { layout.spawnTable = table; EditorUtility.SetDirty(layout); }
            AssetDatabase.SaveAssetIfDirty(layout);
            EnsureRun();
            return ArenaBuilder.Build(layout, Scene);
        }

        public static RunDef EnsureRun()
        {
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(UnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(UnlockPath);
                unlock.id = "provincial_pit";
                unlock.displayName = "The Provincial Pit";
                unlock.description = "Word of the Pit Master's fall reached the provinces. A real arena wants you: a walled square, packed stands, four gates, and a champion.";
                unlock.hint = "Beat the Pit Master and the provinces will want you.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "gorm", amount = 1f } };
                EditorUtility.SetDirty(unlock);
                MetaSetup.CreateOrUpdate(); // into MetaConfig.unlocks
            }
            var run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath);
            if (run == null)
            {
                run = PlayerSetup.EnsureAsset<RunDef>(RunPath);
                run.id = "provincial_pit";
                run.description = "A real arena, and a big one: sand, stone and stands full of townsfolk. Four gates, barricades, spike traps that hurt everyone. Tougher enemies, more Renown (×1.5).";
                run.unlock = unlock;
                run.arenas = new[] { new RunDef.Arena { displayName = "THE PROVINCIAL PIT", scenePath = Scene } };
                EditorUtility.SetDirty(run);
            }
            var slice = AssetDatabase.LoadAssetAtPath<RunDef>(MenuSceneBuilder.RunPath);
            if (slice != null && string.IsNullOrEmpty(slice.description))
            {
                slice.description = "A dirt pit under the city. Mud, fire pits, two gates, and the Pit Master who owns it all.";
                EditorUtility.SetDirty(slice);
            }
            AssetDatabase.SaveAssets();
            return run;
        }

        /// <summary>Every run the menu offers, in ladder order.</summary>
        public static RunDef[] AllRuns()
        {
            EnsureRun();
            return new[] { AssetDatabase.LoadAssetAtPath<RunDef>(MenuSceneBuilder.RunPath), AssetDatabase.LoadAssetAtPath<RunDef>(RunPath), AmphitheaterSetup.EnsureRun(), GrandArenaSetup.EnsureRun(), ImperialArenaSetup.EnsureRun(), ColosseumSetup.EnsureRun() };
        }

        /// <summary>The Shield Archer and the Assassin (docs/ARENAS.md "New enemies"); numbers in DECISIONS "Phase 5.4".</summary>
        public static void EnsureEnemies()
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(CombatSetup.ConvictAnimPath);
            var lean = CombatSetup.Profile("PartAnim_Assassin", a => { a.strideLength = 1.0f; a.legSwing = 46f; a.armSwing = 24f; a.bobHeight = 0.04f; a.fullRunSpeed = 4.8f; });
            CombatSetup.Enemy("EN_ShieldArcher", "Shield Archer", "shieldarcher", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.ShieldedRanged;
                d.maxHp = 30f; d.moveSpeed = 2.8f; d.radius = 0.42f; d.contactDamage = 6f; d.xp = 4f;
                d.keepDistance = new Vector2(8f, 13f); d.windup = 0.9f; d.aimLockTime = 0.3f; d.attackCooldown = 3f; d.attackRange = 16f;
                d.recover = 0.7f; d.projectileSpeed = 24f; d.attackDamage = 12f; d.projectileRadius = 0.2f;
                d.blockArc = 110f; d.blockReduction = 0.85f; d.knockbackTaken = 0.5f; d.turnRate = 160f; d.goldChance = 0.1f; d.prewarm = 16;
            });
            CombatSetup.Enemy("EN_Assassin", "Assassin", "assassin", lean, d =>
            {
                d.behaviour = EnemyBehaviour.Dasher;
                d.maxHp = 24f; d.moveSpeed = 4.6f; d.radius = 0.34f; d.acceleration = 22f; d.contactDamage = 6f; d.xp = 4f;
                d.windup = 0.6f; d.dashSpeed = 18f; d.dashLength = 7f; d.attackDamage = 14f; d.attackCooldown = 3.5f; d.recover = 1.2f;
                d.flankRadius = 5.5f; d.flankTimeout = 4f; d.turnRate = 720f; d.speedJitter = 0.1f; d.goldChance = 0.08f; d.prewarm = 24;
            });
            AssetDatabase.SaveAssets();
        }

        static SpawnTableDef.Entry E(string asset, float from, float w0, float w1, int cap, bool swarm, bool elite) => new SpawnTableDef.Entry
        {
            enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(EnemyDir + asset + ".asset"),
            from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = swarm, elite = elite,
        };

        /// <summary>The arena's new enemies: archers from 1:30, assassins from 2:00, both capped low.</summary>
        static SpawnTableDef.Entry[] NewEntries() => new[]
        {
            E("EN_ShieldArcher", 90f, 1.0f, 1.6f, 10, false, true),
            E("EN_Assassin", 120f, 0.8f, 1.4f, 12, false, true),
        };

        public static SpawnTableDef EnsureTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(TablePath);
            if (t != null)
            {
                // a table made before the new enemies existed: append the missing ones, keep the designer's edits
                var list = new System.Collections.Generic.List<SpawnTableDef.Entry>(t.entries);
                bool added = false;
                foreach (var n in NewEntries())
                {
                    bool has = false;
                    foreach (var x in list) has |= x.enemy == n.enemy;
                    if (!has && n.enemy != null) { list.Add(n); added = true; }
                }
                if (added) { t.entries = list.ToArray(); EditorUtility.SetDirty(t); AssetDatabase.SaveAssetIfDirty(t); }
                return t;
            }
            t = PlayerSetup.EnsureAsset<SpawnTableDef>(TablePath);
            // the horde arrives sooner and mixed: shields and curs from the first minute, ranged and brutes earlier
            t.entries = new[]
            {
                E("EN_Convict", 0f, 8f, 4f, 0, true, true),
                E("EN_PitCur", 15f, 3f, 5f, 80, true, true),
                E("EN_ShieldThug", 40f, 1.6f, 3f, 40, false, true),
                E("EN_Slinger", 60f, 1.2f, 1.8f, 12, false, true),
                E("EN_FirePot", 100f, 0.7f, 1.2f, 7, false, true),
                E("EN_Brute", 140f, 0.35f, 0.9f, 6, false, true),
                NewEntries()[0],
                NewEntries()[1],
            };
            var slave = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(CombatSetup.SlavePitTablePath);
            t.affixes = slave != null ? slave.affixes : new AffixDef[0];
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssetIfDirty(t);
            return t;
        }
    }
}
