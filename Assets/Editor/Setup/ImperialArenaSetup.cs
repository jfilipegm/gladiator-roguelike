using System.Collections.Generic;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Arena 5, the Imperial Arena (Phase 5.16, docs/ARENAS.md §5): its run (RD_ImperialArena), its unlock
    /// (UL_ImperialArena: beat the Gilded Patron once), its new enemies (the Praetorian and the Centurion, who marches
    /// behind a line of five), its spawn table (ST_ImperialArena: the Grand Arena's walkers a little sooner, fewer loose
    /// shields, no Beast Handler, plus the Centurion), its boss (the Emperor's Champion) and the one-click scene build from
    /// AL_ImperialArena. Assets are created once and are designer-owned afterwards (a missing new enemy is still appended
    /// to an existing table; the Centurion's escort and break status are filled in if empty).
    /// </summary>
    public static class ImperialArenaSetup
    {
        public const string Scene = "Assets/Scenes/Arenas/ImperialArena.unity";
        public const string RunPath = "Assets/Data/Runs/RD_ImperialArena.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_ImperialArena.asset";
        public const string TablePath = "Assets/Data/Arenas/ST_ImperialArena.asset";
        const string EnemyDir = "Assets/Data/Enemies/";
        public const string PraetorianPath = EnemyDir + "EN_Praetorian.asset";
        public const string CenturionPath = EnemyDir + "EN_Centurion.asset";

        [MenuItem("Gladiator/Arenas/Build Imperial Arena")]
        public static string Build()
        {
            var layout = ImperialArenaLayout.Ensure();
            EnsureEnemies();
            var boss = BossSetup.EnsureEmperorsChampion(); // after the Centurion exists: it fills in his Guard
            if (layout.boss == null) { layout.boss = boss; layout.bossGate = 0; EditorUtility.SetDirty(layout); } // the triumphal gate
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
                unlock.id = "imperial_arena";
                unlock.displayName = "The Imperial Arena";
                unlock.description = "The Emperor has sent for you. His hippodrome of travertine and purple: chariots racing round the spina, the Praetorian Guard in formation, and the Emperor's own champion.";
                unlock.hint = "The Patron is bankrupt. The Emperor has heard your name.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBoss, id = "patron", amount = 1f } };
                EditorUtility.SetDirty(unlock);
                MetaSetup.CreateOrUpdate(); // into MetaConfig.unlocks
            }
            var run = AssetDatabase.LoadAssetAtPath<RunDef>(RunPath);
            if (run == null)
            {
                run = PlayerSetup.EnsureAsset<RunDef>(RunPath);
                run.id = "imperial_arena";
                run.description = "The Emperor's hippodrome, the biggest floor yet: chariots race round the spina and trample everything on the track, and the Praetorian Guard marches in formation. Tougher enemies, more Renown (×3).";
                run.unlock = unlock;
                run.arenas = new[] { new RunDef.Arena { displayName = "THE IMPERIAL ARENA", scenePath = Scene } };
                EditorUtility.SetDirty(run);
            }
            AssetDatabase.SaveAssets();
            return run;
        }

        /// <summary>The Praetorian and the Centurion (docs/ARENAS.md §5 "New enemies"). The Praetorian only ever comes in
        /// a Centurion's line: five on the slots 1.8 m ahead of him; kill him and the line breaks (Staggered, shields
        /// down 3 s), then they fight on as ordinary shield bearers.</summary>
        public static void EnsureEnemies()
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(CombatSetup.ConvictAnimPath);
            var heavy = AssetDatabase.LoadAssetAtPath<PartAnimProfile>("Assets/Data/Config/PartAnim_Murmillo.asset");
            CombatSetup.Enemy("EN_Praetorian", "Praetorian", "praetorian", heavy != null ? heavy : humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.ShieldBearer; // the tall scutum: 75 % off from a 120° front (they come five at a time)
                d.maxHp = 70f; d.moveSpeed = 3.0f; d.radius = 0.5f; d.acceleration = 14f; d.contactDamage = 10f; d.xp = 4f;
                d.blockArc = 120f; d.blockReduction = 0.75f; d.knockbackTaken = 0.25f; d.turnRate = 180f;
                d.goldChance = 0.06f; d.prewarm = 80; // broken lines fight on, so shields pile up: a bot run grew the pool from 40 twenty times
            });
            CombatSetup.Enemy("EN_Centurion", "Centurion", "centurion", humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Lunger; // his wind-up and lunge are the line's push
                d.maxHp = 160f; d.moveSpeed = 2.6f; d.speedJitter = 0f; d.radius = 0.42f; d.acceleration = 10f; d.contactDamage = 10f; d.xp = 12f;
                d.windup = 0.7f; d.attackRange = 4.2f; d.lungeSpeed = 8f; d.lungeTime = 0.25f; d.attackDamage = 16f;
                d.attackCooldown = 3.5f; d.recover = 0.7f; d.knockbackTaken = 0.3f; d.turnRate = 80f; // the line wheels slowly
                d.goldChance = 0.35f; d.prewarm = 8;
                d.escortCount = 5;
                d.formationSlots = new[] { new Vector2(-2.6f, 1.8f), new Vector2(-1.3f, 1.8f), new Vector2(0f, 1.8f), new Vector2(1.3f, 1.8f), new Vector2(2.6f, 1.8f) };
                d.formationHold = 2.6f; d.pushPad = 0.8f; d.pushDepth = 2.6f; d.pushKnockback = 9f;
                d.freeEscortsOnDeath = false; d.breakShieldsDown = 3f;
            });
            var c = AssetDatabase.LoadAssetAtPath<EnemyDef>(CenturionPath);
            if (c != null)
            {
                if (c.escort == null) { c.escort = AssetDatabase.LoadAssetAtPath<EnemyDef>(PraetorianPath); EditorUtility.SetDirty(c); }
                if (c.breakStatus == null && !c.freeEscortsOnDeath)
                {
                    c.breakStatus = AssetDatabase.LoadAssetAtPath<StatusDef>(GladiatorSetup.StaggerPath);
                    if (c.breakStatus == null) Debug.LogWarning("[ImperialArenaSetup] " + GladiatorSetup.StaggerPath + " missing: the Centurion's broken line won't stagger (run the gladiator setup, then this again)");
                    EditorUtility.SetDirty(c);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static SpawnTableDef.Entry E(string asset, float from, float w0, float w1, int cap, bool swarm, bool elite) => new SpawnTableDef.Entry
        {
            enemy = AssetDatabase.LoadAssetAtPath<EnemyDef>(EnemyDir + asset + ".asset"),
            from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = swarm, elite = elite,
        };

        /// <summary>The arena's new enemy: the Centurion (and his line) from 1:00, never an elite.</summary>
        static SpawnTableDef.Entry[] NewEntries() => new[]
        {
            E("EN_Centurion", 60f, 0.15f, 0.25f, 3, false, false),
        };

        public static SpawnTableDef EnsureTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpawnTableDef>(TablePath);
            if (t == null)
            {
                t = PlayerSetup.EnsureAsset<SpawnTableDef>(TablePath);
                // the Grand Arena's walkers (not the Beast Handler: beasts belong to arenas 3-4), a little sooner, with
                // fewer loose shields (the formations are this arena's shields)
                var entries = new List<SpawnTableDef.Entry>();
                foreach (var e in new[]
                {
                    E("EN_Convict", 0f, 6f, 2.5f, 0, true, true),
                    E("EN_PitCur", 5f, 3f, 4.5f, 110, true, true),
                    E("EN_ShieldThug", 20f, 1.2f, 1.6f, 30, false, true),
                    E("EN_Slinger", 25f, 1.2f, 1.6f, 14, false, true),
                    E("EN_ShieldArcher", 35f, 1.0f, 1.4f, 12, false, true),
                    E("EN_Murmillo", 45f, 0.6f, 1.0f, 12, false, true),
                    E("EN_FirePot", 50f, 0.8f, 1.2f, 8, false, true),
                    E("EN_Assassin", 50f, 1.0f, 1.5f, 16, false, true),
                    E("EN_NetThrower", 60f, 0.7f, 1.1f, 10, false, true),
                    E("EN_GildedMercenary", 60f, 0.5f, 0.9f, 12, false, true),
                    E("EN_Brute", 70f, 0.6f, 1.1f, 10, false, true),
                    E("EN_Sorcerer", 75f, 0.7f, 1.2f, 12, false, true),
                })
                {
                    if (e.enemy != null) entries.Add(e);
                    else Debug.LogWarning("[ImperialArenaSetup] an enemy of ST_ImperialArena is missing (run the earlier arenas' setups first): skipped");
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
    }
}
