using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Arena 4, the Grand Arena (Phase 5.13, docs/ARENAS.md §4): its run (RD_GrandArena), its unlock (UL_GrandArena:
    /// beat the Beast Lord once), its spawn table (ST_GrandArena: the Amphitheater's walkers, sooner and in bigger
    /// numbers, plus the Gilded Mercenary and the Sorcerer), its boss (the Gilded Patron) and the one-click scene build
    /// from AL_GrandArena. Assets are created once and are designer-owned afterwards (a missing new enemy is still
    /// appended to an existing table).
    /// </summary>
    public static class GrandArenaSetup
    {
        public const string Scene = "Assets/Scenes/Arenas/GrandArena.unity";
        public const string RunPath = "Assets/Data/Runs/RD_GrandArena.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_GrandArena.asset";
        public const string TablePath = "Assets/Data/Arenas/ST_GrandArena.asset";
        const string EnemyDir = "Assets/Data/Enemies/";
        public const string MercenaryPath = EnemyDir + "EN_GildedMercenary.asset";
        public const string SorcererPath = EnemyDir + "EN_Sorcerer.asset";

        [MenuItem("Gladiator/Arenas/Build Grand Arena")]
        public static string Build()
        {
            var layout = GrandArenaLayout.Ensure();
            EnsureEnemies();
            if (layout.boss == null) { layout.boss = BossSetup.EnsureGildedPatron(); layout.bossGate = 0; EditorUtility.SetDirty(layout); } // the north gate
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
                unlock.id = "grand_arena";
                unlock.displayName = "The Grand Arena";
                unlock.description = "The city's beasts are dead and the rich want to watch you. White marble in the shape of a cross, patron boxes hung with gold, fire in the floor and blades that sweep the plaza.";
                unlock.hint = "The city has seen you kill its beasts. Now the rich want to watch.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "beastlord", amount = 1f } };
                EditorUtility.SetDirty(unlock);
                MetaSetup.CreateOrUpdate(); // into MetaConfig.unlocks
            }
            var run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath);
            if (run == null)
            {
                run = PlayerSetup.EnsureAsset<RunDef>(RunPath);
                run.id = "grand_arena";
                run.description = "The rich men's arena, a marble cross, the biggest floor yet: fire jets cut across its arms, blades sweep its plaza, and the horde is bigger. Tougher enemies, more Renown (×2.5).";
                run.unlock = unlock;
                run.arenas = new[] { new RunDef.Arena { displayName = "THE GRAND ARENA", scenePath = Scene } };
                EditorUtility.SetDirty(run);
            }
            AssetDatabase.SaveAssets();
            return run;
        }

        /// <summary>The Gilded Mercenary and the Sorcerer (docs/ARENAS.md §4 "New enemies"); numbers in DECISIONS
        /// "Phase 5.13".</summary>
        public static void EnsureEnemies()
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(CombatSetup.ConvictAnimPath);
            var heavy = AssetDatabase.LoadAssetAtPath<PartAnimProfile>("Assets/Data/Config/PartAnim_Murmillo.asset");
            CombatSetup.Enemy("EN_GildedMercenary", "Gilded Mercenary", "mercenary", heavy != null ? heavy : humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Lunger; // braces, then a halberd thrust
                d.maxHp = 95f; d.moveSpeed = 3.0f; d.radius = 0.48f; d.acceleration = 12f; d.contactDamage = 10f; d.xp = 7f;
                d.windup = 0.7f; d.attackRange = 4f; d.lungeSpeed = 10f; d.lungeTime = 0.3f; d.attackDamage = 16f;
                d.attackCooldown = 3f; d.recover = 0.6f; d.knockbackTaken = 0.4f; d.turnRate = 300f;
                d.stealGold = 12; d.goldChance = 0.6f; d.prewarm = 30;
            });
            CombatSetup.Enemy("EN_Sorcerer", "Sorcerer", "sorcerer", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Caster;
                d.maxHp = 34f; d.moveSpeed = 3.2f; d.radius = 0.4f; d.contactDamage = 6f; d.xp = 6f;
                d.keepDistance = new Vector2(10f, 14f); d.windup = 0.8f; d.attackRange = 16f; d.recover = 0.5f;
                d.attackRadius = 2.2f; d.attackDamage = 16f; d.attackCooldown = 4.5f; d.runeDelay = 1.1f;
                d.blinkRange = 5f; d.blinkCooldown = 5f; d.blinkDistance = new Vector2(10f, 13f);
                d.goldChance = 0.1f; d.prewarm = 14;
            });
            AssetDatabase.SaveAssets();
        }

        static SpawnTableDef.Entry E(string asset, float from, float w0, float w1, int cap, bool swarm, bool elite) => new SpawnTableDef.Entry
        {
            enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(EnemyDir + asset + ".asset"),
            from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = swarm, elite = elite,
        };

        /// <summary>The arena's new enemies: mercenaries from 1:00, sorcerers from 1:30.</summary>
        static SpawnTableDef.Entry[] NewEntries() => new[]
        {
            E("EN_GildedMercenary", 60f, 0.8f, 1.6f, 20, false, true),
            E("EN_Sorcerer", 90f, 0.6f, 1.1f, 10, false, true),
        };

        public static SpawnTableDef EnsureTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(TablePath);
            if (t == null)
            {
                t = PlayerSetup.EnsureAsset<SpawnTableDef>(TablePath);
                // the Amphitheater's walkers (not the cage boars), sooner and in bigger numbers
                t.entries = new[]
                {
                    E("EN_Convict", 0f, 7f, 3f, 0, true, true),
                    E("EN_PitCur", 5f, 3f, 5f, 110, true, true),
                    E("EN_ShieldThug", 20f, 1.6f, 2.4f, 45, false, true),
                    E("EN_Slinger", 30f, 1.2f, 1.6f, 14, false, true),
                    E("EN_ShieldArcher", 40f, 1.0f, 1.5f, 12, false, true),
                    E("EN_FirePot", 60f, 0.8f, 1.2f, 8, false, true),
                    E("EN_Assassin", 60f, 0.9f, 1.4f, 14, false, true),
                    E("EN_Brute", 80f, 0.5f, 1.0f, 10, false, true),
                    E("EN_Murmillo", 50f, 0.9f, 1.6f, 18, false, true),
                    E("EN_NetThrower", 70f, 0.6f, 1.0f, 8, false, true),
                };
                var slave = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(CombatSetup.SlavePitTablePath);
                t.affixes = slave != null ? slave.affixes : new AffixDef[0];
            }
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
