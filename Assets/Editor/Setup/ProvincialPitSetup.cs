using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Arena 2, the Provincial Pit (Phase 5.4, docs/ARENAS.md): its run (RD_ProvincialPit), its unlock (UL_ProvincialPit:
    /// beat Gorm once), its spawn table (ST_ProvincialPit: the Slave Pit's six, tougher), and the one-click scene build
    /// from AL_ProvincialPit. Assets are created once and are designer-owned afterwards.
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
                unlock.description = "Word of the Pit Master's fall reached the provinces. A real arena wants you: stone, stands, three gates, and a champion.";
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
                run.description = "A real arena: sand, stone and stands full of townsfolk. Three gates, barricades, spike traps that hurt everyone. Tougher enemies, more Renown (×1.5).";
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
            return new[] { AssetDatabase.LoadAssetAtPath<RunDef>(MenuSceneBuilder.RunPath), AssetDatabase.LoadAssetAtPath<RunDef>(RunPath) };
        }

        public static SpawnTableDef EnsureTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(TablePath);
            if (t != null) return t;
            t = PlayerSetup.EnsureAsset<SpawnTableDef>(TablePath);
            SpawnTableDef.Entry E(string asset, float from, float w0, float w1, int cap, bool swarm, bool elite) => new SpawnTableDef.Entry
            {
                enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(EnemyDir + asset + ".asset"),
                from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = swarm, elite = elite,
            };
            // the horde arrives sooner and mixed: shields and curs from the first minute, ranged and brutes earlier
            t.entries = new[]
            {
                E("EN_Convict", 0f, 8f, 4f, 0, true, true),
                E("EN_PitCur", 15f, 3f, 5f, 80, true, true),
                E("EN_ShieldThug", 40f, 1.6f, 3f, 40, false, true),
                E("EN_Slinger", 60f, 1.2f, 1.8f, 12, false, true),
                E("EN_FirePot", 100f, 0.7f, 1.2f, 7, false, true),
                E("EN_Brute", 140f, 0.35f, 0.9f, 6, false, true),
            };
            var slave = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(CombatSetup.SlavePitTablePath);
            t.affixes = slave != null ? slave.affixes : new AffixDef[0];
            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssetIfDirty(t);
            return t;
        }
    }
}
