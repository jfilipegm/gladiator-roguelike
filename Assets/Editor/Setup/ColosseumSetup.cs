using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Arena 6, the Colosseum (R2, docs/ARENAS.md §6): its run (RD_Colosseum), its unlock (UL_Colosseum: beat the
    /// Emperor's Champion once), its new enemies (the Skeletal Legionnaire, who reassembles once, and the Chimera, who
    /// breathes a cone of fire), its spawn table (ST_Colosseum: the ladder's walkers with the dead taking over as the run
    /// goes, no Slinger, Fire-Pot Thrower, Mercenary or Beast Handler), the lift packs (the beasts and the dead the
    /// hypogeum raises), its boss (the Eternal Victor) and the one-click scene build from AL_Colosseum. Assets are created
    /// once and are designer-owned afterwards (a missing new enemy is still appended to an existing table and to the lift
    /// packs; the Legionnaire's pile mesh is filled in if empty).
    /// </summary>
    public static class ColosseumSetup
    {
        public const string Scene = "Assets/Scenes/Arenas/Colosseum.unity";
        public const string RunPath = "Assets/Data/Runs/RD_Colosseum.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_Colosseum.asset";
        public const string TablePath = "Assets/Data/Arenas/ST_Colosseum.asset";
        const string EnemyDir = "Assets/Data/Enemies/";
        public const string LegionnairePath = EnemyDir + "EN_SkeletalLegionnaire.asset";
        public const string ChimeraPath = EnemyDir + "EN_Chimera.asset";
        public const string BonePileModel = "Assets/Art/Models/Props/PR_BonePile.fbx";
        const string BoarAnimPath = "Assets/Data/Config/PartAnim_Boar.asset";

        [MenuItem("Gladiator/Arenas/Build Colosseum")]
        public static string Build()
        {
            var layout = ColosseumLayout.Ensure();
            EnsureEnemies();
            var boss = BossSetup.EnsureEternalVictor(); // R2-STUB(boss): after the Legionnaire exists (his guards)
            if (layout.boss == null) { layout.boss = boss; layout.bossGate = 0; EditorUtility.SetDirty(layout); } // he rises from the stage; the Gate of Death is the fallback
            var table = EnsureTable();
            if (layout.spawnTable == null) { layout.spawnTable = table; EditorUtility.SetDirty(layout); }
            EnsureLiftPacks(layout);
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
                unlock.id = "colosseum";
                unlock.displayName = "The Colosseum";
                unlock.description = "The oldest arena of all, and the biggest. Its floor drops into the hypogeum, beasts and the dead rise on its lifts, and its first champion has never left the sand.";
                unlock.hint = "The Emperor's champion is dead. One arena is left, and it has been waiting for you.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "emperors_champion", amount = 1f } };
                EditorUtility.SetDirty(unlock);
                MetaSetup.CreateOrUpdate(); // into MetaConfig.unlocks
            }
            var run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath);
            if (run == null)
            {
                run = PlayerSetup.EnsureAsset<RunDef>(RunPath);
                run.id = "colosseum";
                run.description = "The last arena: the biggest floor of all, laid over the hypogeum. The floor drops open under the horde, beasts and the dead rise on the lifts, and the Eternal Victor waits below. Tougher enemies, more Renown (×3.5).";
                run.unlock = unlock;
                run.arenas = new[] { new RunDef.Arena { displayName = "THE COLOSSEUM", scenePath = Scene } };
                EditorUtility.SetDirty(run);
            }
            AssetDatabase.SaveAssets();
            return run;
        }

        /// <summary>The Skeletal Legionnaire and the Chimera (docs/ARENAS.md §6 "New enemies"; numbers:
        /// docs/tracks/R2-design.md "Layout and setup"). The Legionnaire collapses into a bone pile when it dies and stands
        /// up 2.5 s later with half its HP (once; Burn, a stomp or a fall stop it). The Chimera plants and breathes a 60°
        /// cone of fire 8 m long that burns the horde ×3 too; it turns slowly.</summary>
        public static void EnsureEnemies()
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(CombatSetup.ConvictAnimPath);
            var beast = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(BoarAnimPath); // the War Boar's quadruped gait
            CombatSetup.Enemy("EN_SkeletalLegionnaire", "Skeletal Legionnaire", "skeletal_legionnaire", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Chaser;
                d.maxHp = 34f; d.moveSpeed = 3.7f; d.speedJitter = 0.15f; d.radius = 0.36f; d.acceleration = 14f;
                d.contactDamage = 9f; d.contactInterval = 0.9f; d.knockbackTaken = 1f; d.turnRate = 540f;
                d.xp = 2.5f; d.goldChance = 0.05f; d.prewarm = 140; // a swarm type late in the run, and the boss's guards
                d.reviveCount = 1;          // R2-STUB(enemies)
                d.reviveDelay = 2.5f;       // R2-STUB(enemies)
                d.reviveHpMul = 0.5f;       // R2-STUB(enemies)
                d.stompRadius = 0.9f;       // R2-STUB(enemies)
                d.burnStopsRevive = true;   // R2-STUB(enemies)
            });
            CombatSetup.Enemy("EN_Chimera", "Chimera", "chimera", beast != null ? beast : humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Breather; // R2-STUB(enemies)
                d.maxHp = 420f; d.moveSpeed = 3.0f; d.speedJitter = 0.05f; d.radius = 0.95f; d.acceleration = 8f;
                d.contactDamage = 14f; d.contactInterval = 1.0f; d.knockbackTaken = 0.3f; d.turnRate = 160f; // flank it
                d.windup = 1.0f; d.attackRange = 7f; d.attackRadius = 8f; d.attackDamage = 24f; d.trampleMul = 3f;
                d.zoneDuration = 4f; d.zoneDps = 10f; d.attackCooldown = 4.5f; d.recover = 1.4f;
                d.breathAngle = 60f;        // R2-STUB(enemies)
                d.breathTime = 1.0f;        // R2-STUB(enemies)
                d.breathPatches = 3;        // R2-STUB(enemies)
                d.breathPatchRadius = 1.4f; // R2-STUB(enemies)
                d.breathKnockback = 4f;     // R2-STUB(enemies)
                d.xp = 30f; d.goldChance = 0.6f; d.prewarm = 6;
            });
            var legionnaire = AssetDatabase.LoadAssetAtPath<EnemyDef>(LegionnairePath);
            if (legionnaire != null && legionnaire.pileMesh == null) // R2-STUB(enemies)
            {
                legionnaire.pileMesh = FirstMesh(BonePileModel); // R2-STUB(enemies)
                if (legionnaire.pileMesh == null) Debug.LogWarning("[ColosseumSetup] " + BonePileModel + " missing: the Legionnaires' bone piles won't draw (export it, then run this again)"); // R2-STUB(enemies)
                EditorUtility.SetDirty(legionnaire);
            }
            AssetDatabase.SaveAssets();
        }

        static Mesh FirstMesh(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m) return m;
            return null;
        }

        static SpawnTableDef.Entry E(string asset, float from, float w0, float w1, int cap, bool swarm, bool elite) => new SpawnTableDef.Entry
        {
            enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(EnemyDir + asset + ".asset"),
            from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = swarm, elite = elite,
        };

        /// <summary>The arena's new enemies: the dead from 0:45 (0.8 → 6: a third of the steady horde at 8:00 and over half
        /// the swarms), the Chimera from 4:00 (two at most, never an elite).</summary>
        static SpawnTableDef.Entry[] NewEntries() => new[]
        {
            E("EN_SkeletalLegionnaire", 45f, 0.8f, 6.0f, 160, true, true),
            E("EN_Chimera", 240f, 0.08f, 0.15f, 2, false, false),
        };

        public static SpawnTableDef EnsureTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(TablePath);
            if (t == null)
            {
                t = PlayerSetup.EnsureAsset<SpawnTableDef>(TablePath);
                // the Imperial Arena's walkers less the Slinger, Fire-Pot Thrower and Mercenary (the Chimera owns fire, the
                // Archer owns range), the Centurion rarer; readability at 450
                var entries = new List<SpawnTableDef.Entry>();
                foreach (var e in new[]
                {
                    E("EN_Convict", 0f, 6f, 1.5f, 0, true, true),
                    E("EN_PitCur", 5f, 3f, 3f, 100, true, true),
                    E("EN_ShieldThug", 20f, 1.2f, 1.0f, 24, false, true),
                    E("EN_ShieldArcher", 30f, 1.0f, 1.2f, 12, false, true),
                    E("EN_SkeletalLegionnaire", 45f, 0.8f, 6.0f, 160, true, true),
                    E("EN_Assassin", 50f, 1.0f, 1.3f, 14, false, true),
                    E("EN_Murmillo", 50f, 0.6f, 0.8f, 10, false, true),
                    E("EN_NetThrower", 60f, 0.7f, 1.0f, 10, false, true),
                    E("EN_Centurion", 75f, 0.12f, 0.2f, 2, false, false), // + 5 Praetorians; an affix would break the march
                    E("EN_Brute", 75f, 0.6f, 1.0f, 10, false, true),
                    E("EN_Sorcerer", 90f, 0.6f, 1.0f, 10, false, true),
                    E("EN_Chimera", 240f, 0.08f, 0.15f, 2, false, false),
                })
                {
                    if (e.enemy != null) entries.Add(e);
                    else Debug.LogWarning("[ColosseumSetup] an enemy of ST_Colosseum is missing (run the earlier arenas' setups first): skipped");
                }
                t.entries = entries.ToArray();
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

        static ArenaLayoutDef.LiftPack Pack(string path, float from, float to, float weight, int min, int max, int cap, string call) => new ArenaLayoutDef.LiftPack // R2-STUB(hypogeum)
        {
            enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(path),
            from = from, to = to, weight = weight, count = new Vector2Int(min, max), maxAlive = cap, call = call,
        };

        /// <summary>What the lifts raise (docs/ARENAS.md §6 "Lift packs"; `to` 0 = the end of the run): Pit Curs early, War
        /// Boars from 2:30, a Chimera from 4:00, the dead from 4:30. Each lift in a wave picks its own (weighted, under the
        /// caps); the wave's call is the first lift's. Filled once; a missing enemy is appended once it exists.</summary>
        public static void EnsureLiftPacks(ArenaLayoutDef layout)
        {
            var want = new[]
            {
                Pack(EnemyDir + "EN_PitCur.asset", 90f, 240f, 3f, 4, 6, 100, "THE BEASTS RISE!"),
                Pack(AmphitheaterSetup.BoarPath, 150f, 0f, 2f, 2, 3, 12, "THE BEASTS RISE!"),
                Pack(ChimeraPath, 240f, 0f, 1f, 1, 1, 2, "A CHIMERA!"),
                Pack(LegionnairePath, 270f, 0f, 3f, 5, 7, 160, "THE DEAD RISE!"),
            };
            var list = new List<ArenaLayoutDef.LiftPack>(layout.liftPacks ?? new ArenaLayoutDef.LiftPack[0]); // R2-STUB(hypogeum)
            bool added = false;
            foreach (var p in want)
            {
                if (p.enemy == null) { Debug.LogWarning("[ColosseumSetup] a lift pack's enemy is missing (run the earlier arenas' setups first): skipped"); continue; }
                bool has = false;
                foreach (var x in list) has |= x.enemy == p.enemy;
                if (!has) { list.Add(p); added = true; }
            }
            if (!added) return;
            layout.liftPacks = list.ToArray(); // R2-STUB(hypogeum)
            EditorUtility.SetDirty(layout);
        }
    }
}
