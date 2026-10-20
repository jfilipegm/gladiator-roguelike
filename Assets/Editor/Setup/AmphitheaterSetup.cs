using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Arena 3, the Stone Amphitheater (Phase 5.8, docs/ARENAS.md §3): its run (RD_StoneAmphitheater), its unlock
    /// (UL_StoneAmphitheater: beat the Provincial Champion once), its spawn table (ST_StoneAmphitheater: the Provincial
    /// Pit's eight, sooner and in bigger numbers, plus the Murmillo and the Net-Thrower), the War Boars its cages
    /// release, and the one-click scene build from AL_StoneAmphitheater. Assets are created once and are
    /// designer-owned afterwards (a missing new enemy is still appended to an existing table).
    /// </summary>
    public static class AmphitheaterSetup
    {
        public const string Scene = "Assets/Scenes/Arenas/StoneAmphitheater.unity";
        public const string RunPath = "Assets/Data/Runs/RD_StoneAmphitheater.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_StoneAmphitheater.asset";
        public const string TablePath = "Assets/Data/Arenas/ST_StoneAmphitheater.asset";
        public const string BoarPath = EnemyDir + "EN_WarBoar.asset";
        const string EnemyDir = "Assets/Data/Enemies/";

        [MenuItem("Gladiator/Arenas/Build Stone Amphitheater")]
        public static string Build()
        {
            var layout = AmphitheaterLayout.Ensure();
            EnsureEnemies();
            if (layout.boss == null) { layout.boss = BossSetup.EnsureBeastLord(); layout.bossGate = 0; EditorUtility.SetDirty(layout); } // the great gate
            var table = EnsureTable();
            if (layout.spawnTable == null) { layout.spawnTable = table; EditorUtility.SetDirty(layout); }
            var boar = AssetDatabase.LoadAssetAtPath<EnemyDef>(BoarPath);
            if (layout.cageBeast == null && boar != null) { layout.cageBeast = boar; EditorUtility.SetDirty(layout); }
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
                unlock.id = "stone_amphitheater";
                unlock.displayName = "The Stone Amphitheater";
                unlock.description = "The province's champion is dead and the city has heard. A real amphitheatre: thousands in the seats, columns of travertine, and cages under the stage wall where the beasts wait.";
                unlock.hint = "The provinces have a champion. The city wants whoever beats him.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "champion", amount = 1f } };
                EditorUtility.SetDirty(unlock);
                MetaSetup.CreateOrUpdate(); // into MetaConfig.unlocks
            }
            var run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath);
            if (run == null)
            {
                run = PlayerSetup.EnsureAsset<RunDef>(RunPath);
                run.id = "stone_amphitheater";
                run.description = "A city amphitheatre, the biggest floor yet: columns to break the line of sight, a raised stage, thousands watching, and beast cages that open on their own. Tougher enemies, more Renown (×2).";
                run.unlock = unlock;
                run.arenas = new[] { new RunDef.Arena { displayName = "THE STONE AMPHITHEATER", scenePath = Scene } };
                EditorUtility.SetDirty(run);
            }
            AssetDatabase.SaveAssets();
            return run;
        }

        /// <summary>The Murmillo, the Net-Thrower and the War Boar (docs/ARENAS.md §3 "New enemies"); numbers in
        /// DECISIONS "Phase 5.8".</summary>
        public static void EnsureEnemies()
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(CombatSetup.ConvictAnimPath);
            var heavy = CombatSetup.Profile("PartAnim_Murmillo", a => { a.strideLength = 1.15f; a.legSwing = 30f; a.armSwing = 14f; a.bobHeight = 0.06f; a.fullRunSpeed = 2.6f; });
            var boar = CombatSetup.Profile("PartAnim_Boar", a => { a.strideLength = 1.0f; a.legSwing = 38f; a.armSwing = 52f; a.bobHeight = 0.06f; a.fullRunSpeed = 4.2f; });
            CombatSetup.Enemy("EN_Murmillo", "Murmillo", "murmillo", heavy, d =>
            {
                d.behaviour = EnemyBehaviour.ShieldBearer;
                d.maxHp = 135f; d.moveSpeed = 2.6f; d.radius = 0.5f; d.contactDamage = 13f; d.xp = 7f;
                d.blockArc = 130f; d.blockReduction = 0.9f; d.knockbackTaken = 0.15f; d.turnRate = 150f; d.goldChance = 0.12f; d.prewarm = 24;
            });
            CombatSetup.Enemy("EN_NetThrower", "Net-Thrower", "netthrower", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Lobber;
                d.lobNet = true; d.netSlow = 0.1f; d.netTime = 1.2f;
                d.maxHp = 26f; d.moveSpeed = 3.2f; d.radius = 0.38f; d.contactDamage = 5f; d.xp = 4f;
                d.keepDistance = new Vector2(7f, 11f); d.windup = 0.9f; d.lobTime = 0.85f; d.attackRange = 13f; d.recover = 0.5f;
                d.attackRadius = 1.7f; d.attackDamage = 6f; d.attackCooldown = 4.5f; d.goldChance = 0.08f; d.prewarm = 12;
            });
            CombatSetup.Enemy("EN_WarBoar", "War Boar", "warboar", boar, d =>
            {
                d.behaviour = EnemyBehaviour.Dasher;
                d.flank = false; d.trampleMul = 3f;
                d.maxHp = 60f; d.moveSpeed = 4.2f; d.radius = 0.6f; d.acceleration = 16f; d.contactDamage = 10f; d.xp = 3f;
                d.windup = 0.7f; d.dashSpeed = 17f; d.dashLength = 14f; d.attackDamage = 16f; d.attackCooldown = 3f; d.recover = 1.0f;
                d.turnRate = 300f; d.speedJitter = 0.12f; d.knockbackTaken = 0.3f; d.goldChance = 0.05f; d.prewarm = 24;
            });
            AssetDatabase.SaveAssets();
        }

        static SpawnTableDef.Entry E(string asset, float from, float w0, float w1, int cap, bool swarm, bool elite) => new SpawnTableDef.Entry
        {
            enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(EnemyDir + asset + ".asset"),
            from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = swarm, elite = elite,
        };

        /// <summary>The arena's new enemies: murmillos from 1:10, net-throwers from 1:40.</summary>
        static SpawnTableDef.Entry[] NewEntries() => new[]
        {
            E("EN_Murmillo", 70f, 0.9f, 1.8f, 18, false, true),
            E("EN_NetThrower", 100f, 0.7f, 1.2f, 8, false, true),
        };

        public static SpawnTableDef EnsureTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(TablePath);
            if (t == null)
            {
                t = PlayerSetup.EnsureAsset<SpawnTableDef>(TablePath);
                // the Provincial Pit's eight, sooner and in bigger numbers (the floor is bigger)
                t.entries = new[]
                {
                    E("EN_Convict", 0f, 7f, 3.5f, 0, true, true),
                    E("EN_PitCur", 10f, 3f, 5f, 90, true, true),
                    E("EN_ShieldThug", 30f, 1.6f, 2.6f, 40, false, true),
                    E("EN_Slinger", 45f, 1.2f, 1.8f, 14, false, true),
                    E("EN_ShieldArcher", 60f, 1.0f, 1.6f, 12, false, true),
                    E("EN_FirePot", 80f, 0.8f, 1.3f, 8, false, true),
                    E("EN_Assassin", 90f, 0.8f, 1.4f, 14, false, true),
                    E("EN_Brute", 110f, 0.4f, 1.0f, 8, false, true),
                };
                var slave = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(CombatSetup.SlavePitTablePath);
                t.affixes = slave != null ? slave.affixes : new AffixDef[0];
            }
            // the new enemies, appended once they exist (keeps the designer's edits)
            var list = new List<SpawnTableDef.Entry>(t.entries);
            bool added = false;
            foreach (var n in NewEntries())
            {
                if (n.enemy == null) continue;
                bool has = false;
                foreach (var x in list) has |= x.enemy == n.enemy;
                if (!has) { list.Add(n); added = true; }
            }
            if (added) t.entries = list.ToArray();
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssetIfDirty(t);
            return t;
        }
    }
}
