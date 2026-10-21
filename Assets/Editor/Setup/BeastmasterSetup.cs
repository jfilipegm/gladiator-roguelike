using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Phase 5.15 (docs/GLADIATORS.md "The Beastmaster"): the Beast Handler enemy (a Lunger with a whip who walks two
    /// Pit Curs on leashes; kill him and they're freed) in the Stone Amphitheater's and the Grand Arena's tables; the
    /// Beastmaster (GL_Beastmaster), the Wolf Pack (the Summon pattern, CH_Wolf) and its card, the Feral Bond and
    /// Hunting Horn traits, and UL_Beastmaster (KillEnemies beasthandler 25).
    /// </summary>
    public static class BeastmasterSetup
    {
        const string EnemyDir = "Assets/Data/Enemies/";
        public const string HandlerPath = EnemyDir + "EN_BeastHandler.asset";
        public const string CurPath = EnemyDir + "EN_PitCur.asset";
        public const string Dir = GladiatorSetup.Dir;
        public const string BeastmasterPath = Dir + "GL_Beastmaster.asset";
        public const string MovePath = "Assets/Data/Config/MovementConfig_Beastmaster.asset";
        public const string AnimPath = "Assets/Data/Config/PartAnim_Beastmaster.asset";
        public const string FeralBondPath = Dir + "TR_FeralBond.asset";
        public const string HornPath = Dir + "TR_HuntingHorn.asset";
        public const string WolfPackPath = UpgradeSetup.WeaponDir + "WP_WolfPack.asset";
        public const string WolfPackCardPath = UpgradeSetup.Dir + "UP_W_WolfPack.asset";
        public const string UnlockPath = MetaSetup.Dir + "UL_Beastmaster.asset";
        public const string Model = "Assets/Art/Models/Characters/CH_Beastmaster.fbx";
        public const string WolfModel = "Assets/Art/Models/Characters/CH_Wolf.fbx";
        public const string HandlerId = "beasthandler";

        [MenuItem("Gladiator/Setup/Gladiators/Create Beastmaster Data")]
        public static GladiatorDef CreateOrUpdate()
        {
            EnsureHandler();
            AddToTable(AmphitheaterSetup.EnsureTable(), 130f, 0.5f, 0.9f, 6);
            AddToTable(GrandArenaSetup.EnsureTable(), 90f, 0.5f, 0.9f, 8);
            var g = Beastmaster();
            AssetDatabase.SaveAssets();
            return g;
        }

        /// <summary>The Beast Handler: a whip-cracking Lunger (a short step and a long crack) who walks two Pit Curs on
        /// leashes. Slow: he's walking beasts. Kill him and the curs are freed.</summary>
        public static EnemyDef EnsureHandler()
        {
            var humanoid = AssetDatabase.LoadAssetAtPath<PartAnimProfile>(CombatSetup.ConvictAnimPath);
            CombatSetup.Enemy("EN_BeastHandler", "Beast Handler", HandlerId, humanoid, d =>
            {
                d.behaviour = EnemyBehaviour.Lunger; // braces, then the whip cracks
                d.maxHp = 110f; d.moveSpeed = 2.5f; d.radius = 0.5f; d.acceleration = 10f; d.contactDamage = 10f; d.xp = 8f;
                d.windup = 0.6f; d.attackRange = 3.5f; d.lungeSpeed = 6f; d.lungeTime = 0.2f; d.attackDamage = 14f;
                d.attackCooldown = 3.2f; d.recover = 0.6f; d.knockbackTaken = 0.5f; d.turnRate = 280f;
                d.goldChance = 0.25f; d.prewarm = 10;
                d.escortCount = 2; d.leashLength = 3.5f;
            });
            var h = AssetDatabase.LoadAssetAtPath<EnemyDef>(HandlerPath);
            if (h != null && h.escort == null)
            {
                h.escort = AssetDatabase.LoadAssetAtPath<EnemyDef>(CurPath);
                EditorUtility.SetDirty(h);
            }
            var cur = AssetDatabase.LoadAssetAtPath<EnemyDef>(CurPath);
            if (cur != null && cur.prewarm < 110) { cur.prewarm = 110; EditorUtility.SetDirty(cur); } // + two per handler
            return h;
        }

        static void AddToTable(SpawnTableDef t, float from, float w0, float w1, int cap)
        {
            var h = AssetDatabase.LoadAssetAtPath<EnemyDef>(HandlerPath);
            if (t == null || h == null) return;
            foreach (var e in t.entries) if (e.enemy == h) return;
            var list = new System.Collections.Generic.List<SpawnTableDef.Entry>(t.entries)
            {
                new SpawnTableDef.Entry { enemy = h, from = from, weightStart = w0, weightEnd = w1, maxAlive = cap, swarm = false, elite = true },
            };
            t.entries = list.ToArray();
            EditorUtility.SetDirty(t);
        }

        static GladiatorDef Beastmaster()
        {
            var stagger = AssetDatabase.LoadAssetAtPath<StatusDef>(GladiatorSetup.StaggerPath);
            var pack = WolfPack();
            var card = WeaponCard(pack);
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(UnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(UnlockPath);
                unlock.id = "beastmaster";
                unlock.displayName = "The Beastmaster";
                unlock.description = "Twenty-five handlers dead, fifty beasts running free, and a hunter from the forests has heard about you. The Beastmaster can be chosen, and his Wolf Pack joins the level-up pool.";
                unlock.hint = "Somewhere a man is waiting for someone to free the beasts. Kill their handlers.";
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.KillEnemies, id = HandlerId, amount = 25f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, MovePath, m =>
            {
                m.capsuleHeight = 1.65f;
                m.slideCapsuleHeight = 0.95f;
                m.capsuleRadius = 0.38f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, AnimPath, a =>
            {
                a.strideLength = 1.65f; // a loping, wide stride
                a.bobHeight = 0.06f;
                a.runLean = 12f;
            });
            var bond = Trait<FeralBondTrait>(FeralBondPath, "feral_bond", "Feral Bond",
                "When you're hit, your wolves frenzy for 4 s: they bite 60 % faster and run 25 % faster.");
            var horn = Trait<HuntingHornTrait>(HornPath, "hunting_horn", "Hunting Horn",
                "Start a slide with the horn ready (every 5 s): every wolf pounces on the enemies ahead of you, biting three times and Staggering them.");
            if (horn.status == null) { horn.status = stagger; EditorUtility.SetDirty(horn); }

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(BeastmasterPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(BeastmasterPath);
                g.id = "beastmaster";
                g.displayName = "The Beastmaster";
                g.description = "A hunter from the frontier forests who won't fight without his wolves. They hunt beside him, frenzy when he's hurt, and pounce when he blows his horn.";
                g.baseStats = new[] { new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 100f } };
                g.cameraTargetHeight = 1.25f;
                g.tradeoff = "The pack is his damage: two wolves at first, and they won't stray far from him.";
            }
            if (g.model == null) g.model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = pack;
            if (g.traits == null || g.traits.Length == 0 || System.Array.IndexOf(g.traits, null) >= 0) g.traits = new GladiatorTrait[] { bond, horn };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        static WeaponDef WolfPack()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(WolfPackPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(WolfPackPath);
                w.id = "wolf_pack"; w.displayName = "Wolf Pack";
                w.description = "Wolves fight beside you for the whole run: each hunts the nearest enemy near you and bites. More levels, more wolves.";
                w.pattern = WeaponPattern.Summon;
                // amount = wolves, cooldown = seconds between one wolf's bites, speed = run speed (m/s),
                // acquireRange = how far from you they hunt, range = the leash (past it they come back)
                w.damage = 9f; w.critChance = 0.06f; w.critMultiplier = 2f; w.knockback = 2f;
                w.cooldown = 0.8f; w.swingDuration = 0.2f; w.windup = 0.05f;
                w.range = 12f; w.width = 0.6f; w.acquireRange = 10f; w.amount = 2; w.speed = 8.5f; w.meshScale = 1.3f;
                w.levels = new[]
                {
                    L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.25f)), L(Pct(StatId.AttackSpeed, 0.2f)), L(Add(StatId.Amount, 1f)),
                    L(Pct(StatId.ProjectileSpeed, 0.2f), Pct(StatId.Area, 0.15f)), L(Pct(StatId.Damage, 0.3f)), L(Add(StatId.Amount, 1f)),
                };
            }
            if (w.summon == null) w.summon = AssetDatabase.LoadAssetAtPath<GameObject>(WolfModel);
            EditorUtility.SetDirty(w);
            return w;
        }

        static UpgradeDef WeaponCard(WeaponDef weapon)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(WolfPackCardPath);
            if (d != null) return d;
            d = PlayerSetup.EnsureAsset<UpgradeDef>(WolfPackCardPath);
            d.id = "weapon_" + weapon.id; d.displayName = weapon.displayName; d.glyph = "WP";
            d.kind = UpgradeKind.Weapon;
            d.weapon = weapon;
            d.maxLevel = weapon.MaxLevel;
            d.weight = 8f;
            EditorUtility.SetDirty(d);
            return d;
        }

        static WeaponDef.Level L(params StatMod[] mods) => new WeaponDef.Level { mods = mods };
        static StatMod Pct(StatId s, float v) => new StatMod(s, ModOp.Percent, v);
        static StatMod Add(StatId s, float v) => new StatMod(s, ModOp.Flat, v);

        static T Trait<T>(string path, string id, string name, string description) where T : GladiatorTrait
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            if (t != null) return t;
            t = PlayerSetup.EnsureAsset<T>(path);
            t.id = id; t.displayName = name; t.description = description;
            EditorUtility.SetDirty(t);
            return t;
        }

        static T Copy<T>(string from, string to, System.Action<T> tweak) where T : ScriptableObject
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(to);
            if (t != null) return t;
            if (!AssetDatabase.CopyAsset(from, to)) return PlayerSetup.EnsureAsset<T>(to);
            t = AssetDatabase.LoadAssetAtPath<T>(to);
            tweak(t);
            EditorUtility.SetDirty(t);
            return t;
        }
    }
}
