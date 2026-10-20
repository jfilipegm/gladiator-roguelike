using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Gladiator data (docs/GLADIATORS.md). Phase 5.2: the Pit Goblin (GL_PitGoblin) with its capsule, animation, the
    /// Bomb Satchel and its card, the Scavenger and Trap Slide traits, the Root status, and UL_PitGoblin (the rattling
    /// crate flag). Phase 5.3: the Blood Knight (GL_BloodKnight), the Crimson Greatsword and its card, the Bloodthirst,
    /// Bloodlust and Blood Rite traits, and UL_BloodKnight (beat Gorm below 30 % HP). Every asset is created once and
    /// is designer-owned afterwards; only missing links are filled in.
    /// </summary>
    public static class GladiatorSetup
    {
        public const string Dir = "Assets/Data/Gladiators/";
        public const string GoblinPath = Dir + "GL_PitGoblin.asset";
        public const string GoblinMovePath = "Assets/Data/Config/MovementConfig_Goblin.asset";
        public const string GoblinAnimPath = "Assets/Data/Config/PartAnim_Goblin.asset";
        public const string ScavengerPath = Dir + "TR_Scavenger.asset";
        public const string TrapSlidePath = Dir + "TR_TrapSlide.asset";
        public const string SatchelPath = UpgradeSetup.WeaponDir + "WP_BombSatchel.asset";
        public const string SatchelCardPath = UpgradeSetup.Dir + "UP_W_BombSatchel.asset";
        public const string GoblinUnlockPath = MetaSetup.Dir + "UL_PitGoblin.asset";
        public const string RootPath = "Assets/Data/Status/SE_Root.asset";
        public const string BleedPath = "Assets/Data/Status/SE_Bleed.asset";
        public const string GoblinModel = "Assets/Art/Models/Characters/CH_PitGoblin.fbx";
        public const string CrateModel = "Assets/Art/Models/Environment/PR_Crate.fbx";
        public const string BombMesh = "Assets/Art/Models/Weapons/WP_Bomb.fbx";
        public const string SnareMesh = "Assets/Art/Models/Props/PR_Snare.fbx";
        public const string Flag = "rattling_crate";

        public const string KnightPath = Dir + "GL_BloodKnight.asset";
        public const string KnightMovePath = "Assets/Data/Config/MovementConfig_Knight.asset";
        public const string KnightAnimPath = "Assets/Data/Config/PartAnim_Knight.asset";
        public const string BloodthirstPath = Dir + "TR_Bloodthirst.asset";
        public const string BloodlustPath = Dir + "TR_Bloodlust.asset";
        public const string BloodRitePath = Dir + "TR_BloodRite.asset";
        public const string GreatswordPath = UpgradeSetup.WeaponDir + "WP_CrimsonGreatsword.asset";
        public const string GreatswordCardPath = UpgradeSetup.Dir + "UP_W_CrimsonGreatsword.asset";
        public const string KnightUnlockPath = MetaSetup.Dir + "UL_BloodKnight.asset";
        public const string KnightModel = "Assets/Art/Models/Characters/CH_BloodKnight.fbx";

        static WeaponDef.Level L(params StatMod[] mods) => new WeaponDef.Level { mods = mods };
        static StatMod Pct(StatId s, float v) => new StatMod(s, ModOp.Percent, v);
        static StatMod Add(StatId s, float v) => new StatMod(s, ModOp.Flat, v);

        [MenuItem("Gladiator/Setup/Gladiators/Create Gladiator Data")]
        public static GladiatorDef CreateOrUpdate()
        {
            UpgradeSetup.CreateOrUpdate();
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            if (nobody != null)
            {
                if (nobody.startingWeapon == null) nobody.startingWeapon = AssetDatabase.LoadAssetAtPath<WeaponDef>(CombatSetup.GladiusPath);
                if (string.IsNullOrEmpty(nobody.tradeoff)) nobody.tradeoff = "Steady: no strengths, no weaknesses.";
                EditorUtility.SetDirty(nobody);
            }

            var root = Status(RootPath, "root", "Root", "Rooted", StatusKind.Root, 2f, 0f, new Color(0.72f, 0.95f, 1f));
            var satchel = Satchel();
            var card = SatchelCard(satchel);
            var unlock = GoblinUnlock(card);
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, GoblinMovePath, m =>
            {
                m.capsuleHeight = 1.0f;
                m.slideCapsuleHeight = 0.6f;
                m.capsuleRadius = 0.28f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, GoblinAnimPath, a =>
            {
                a.strideLength = 1.0f;   // short legs: a quicker scurry
                a.bobHeight = 0.05f;
                a.runLean = 18f;         // hunched
            });
            var scavenger = Trait<ScavengerTrait>(ScavengerPath, "scavenger", "Scavenger",
                "Every 25 s, pulls an improvised weapon out of the sack: a weapon you don't hold, lent for 15 s.");
            var trap = Trait<TrapSlideTrait>(TrapSlidePath, "trap_slide", "Trap Slide",
                "Sliding drops a snare (3 at most). The first enemy to step in is held for 2 s and bleeds.");
            if (trap.root == null) trap.root = root;
            if (trap.bleed == null) trap.bleed = AssetDatabase.LoadAssetAtPath<StatusDef>(BleedPath);
            if (trap.mesh == null) trap.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SnareMesh);
            if (trap.material == null) trap.material = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.CharMat);
            EditorUtility.SetDirty(trap);

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(GoblinPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(GoblinPath);
                g.id = "pit_goblin";
                g.displayName = "The Pit Goblin";
                g.description = "Lived under the pit floor. Lobs bombs, leaves snares, pulls junk out of its sack.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 70f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 1.12f },
                    new GladiatorDef.BaseStat { stat = StatId.Damage, value = 0.9f },
                    new GladiatorDef.BaseStat { stat = StatId.PickupRange, value = 1.25f },
                };
                g.cameraTargetHeight = 0.85f;
                g.tradeoff = "Fast and greedy, but fragile: 70 HP, -10 % damage.";
            }
            if (g.model == null) g.model = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = satchel;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { scavenger, trap };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            BloodKnight();
            UpgradeSetup.Refresh();
            MetaSetup.CreateOrUpdate();
            IconSetup.Apply(); // the goblin's and the knight's weapon cards get their icons too
            AssetDatabase.SaveAssets();
            return g;
        }

        /// <summary>Every gladiator, the Nobody first (select screen and Codex order).</summary>
        public static GladiatorDef[] All()
        {
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            var goblin = AssetDatabase.LoadAssetAtPath<GladiatorDef>(GoblinPath);
            var knight = AssetDatabase.LoadAssetAtPath<GladiatorDef>(KnightPath);
            var list = new System.Collections.Generic.List<GladiatorDef>();
            if (nobody != null) list.Add(nobody);
            if (goblin != null) list.Add(goblin);
            if (knight != null) list.Add(knight);
            return list.ToArray();
        }

        /// <summary>Phase 5.3: the Blood Knight and everything only he brings.</summary>
        static GladiatorDef BloodKnight()
        {
            var bleed = AssetDatabase.LoadAssetAtPath<StatusDef>(BleedPath);
            var sword = Greatsword(bleed);
            var card = WeaponCard(GreatswordCardPath, sword, "GS");
            var unlock = AssetDatabase.LoadAssetAtPath<UnlockDef>(KnightUnlockPath);
            if (unlock == null)
            {
                unlock = PlayerSetup.EnsureAsset<UnlockDef>(KnightUnlockPath);
                unlock.id = "blood_knight";
                unlock.displayName = "The Blood Knight";
                unlock.description = "You won bleeding, and something in black plate took notice. The Blood Knight can be chosen, and his Crimson Greatsword joins the level-up pool.";
                unlock.hint = "Gorm must fall to someone already bleeding out (below 30 % HP).";
                // interim: §6.7 says "Reach Arena 3"; switch to ReachArena 2 once that arena exists (DECISIONS 5.3)
                unlock.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.DefeatBossBelowHp, id = "gorm", amount = 0.3f } };
                unlock.addsToPool = new[] { card };
                EditorUtility.SetDirty(unlock);
            }
            var move = Copy<MovementConfig>(PlayerSetup.MovementPath, KnightMovePath, m =>
            {
                m.capsuleHeight = 1.65f;
                m.slideCapsuleHeight = 0.95f;
                m.capsuleRadius = 0.36f;
            });
            var anim = Copy<PartAnimProfile>(PlayerSetup.AnimPath, KnightAnimPath, a =>
            {
                a.strideLength = 1.7f;   // long, heavy strides
                a.bobHeight = 0.05f;
                a.runLean = 8f;
                a.weaponArmSwingScale = 0.25f; // the blade stays on the shoulder while running
                a.swingYawStart = 110f;  // a wider, heavier cleave
                a.swingYawEnd = -130f;
                a.swingRaise = 95f;
            });
            var thirst = Trait<BloodthirstTrait>(BloodthirstPath, "bloodthirst", "Bloodthirst",
                "Kills heal you (1 HP, elites 8, at most 6 HP/s). Every other healing is halved.");
            var lust = Trait<BloodlustTrait>(BloodlustPath, "bloodlust", "Bloodlust",
                "Up to +60 % damage as your HP falls (full at 20 % HP or less).");
            var rite = Trait<BloodRiteTrait>(BloodRitePath, "blood_rite", "Blood Rite",
                "Kills and wounds fill the Blood meter. Slide with it full: pay 12 % of max HP for 8 s of frenzy (+40 % damage, +25 % attack speed, double kill heals).");

            var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(KnightPath);
            if (g == null)
            {
                g = PlayerSetup.EnsureAsset<GladiatorDef>(KnightPath);
                g.id = "blood_knight";
                g.displayName = "The Blood Knight";
                g.description = "A fallen knight who fights for the blood. Kills heal him; the closer to death, the harder he hits.";
                g.baseStats = new[]
                {
                    new GladiatorDef.BaseStat { stat = StatId.MaxHp, value = 120f },
                    new GladiatorDef.BaseStat { stat = StatId.MoveSpeed, value = 0.92f },
                };
                g.cameraTargetHeight = 1.35f;
                g.tradeoff = "Lives on kills: slow, and other healing is halved.";
            }
            if (g.model == null) g.model = AssetDatabase.LoadAssetAtPath<GameObject>(KnightModel);
            if (g.movement == null) g.movement = move;
            if (g.animProfile == null) g.animProfile = anim;
            if (g.startingWeapon == null) g.startingWeapon = sword;
            if (g.traits == null || g.traits.Length == 0) g.traits = new GladiatorTrait[] { thirst, lust, rite };
            if (g.unlock == null) g.unlock = unlock;
            EditorUtility.SetDirty(g);
            return g;
        }

        static WeaponDef Greatsword(StatusDef bleed)
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(GreatswordPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(GreatswordPath);
                w.id = "crimson_greatsword"; w.displayName = "Crimson Greatsword";
                w.description = "A slow, enormous cleave around you. Every hit makes them bleed.";
                w.pattern = WeaponPattern.MeleeArc;
                w.damage = 28f; w.critChance = 0.08f; w.critMultiplier = 2f; w.knockback = 9f;
                w.cooldown = 1.5f; w.swingDuration = 0.42f; w.windup = 0.2f;
                w.range = 3.4f; w.arcDegrees = 220f; w.width = 0.6f; w.acquireRange = 3.6f; w.amount = 1;
                w.onHitStatusPower = 0.1f; w.onHitStacks = 1;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Pct(StatId.Area, 0.15f)), L(Pct(StatId.AttackSpeed, 0.15f)), L(Add(StatId.Amount, 1f)),
                    L(Add(StatId.CritChance, 0.08f)), L(Pct(StatId.Damage, 0.25f), Pct(StatId.Area, 0.1f)), L(Pct(StatId.AttackSpeed, 0.2f), Pct(StatId.Knockback, 0.2f)),
                };
            }
            if (w.onHitStatus == null) w.onHitStatus = bleed;
            EditorUtility.SetDirty(w);
            return w;
        }

        static UpgradeDef WeaponCard(string path, WeaponDef weapon, string glyph)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(path);
            if (d != null) return d;
            d = PlayerSetup.EnsureAsset<UpgradeDef>(path);
            d.id = "weapon_" + weapon.id; d.displayName = weapon.displayName; d.glyph = glyph;
            d.kind = UpgradeKind.Weapon;
            d.weapon = weapon;
            d.maxLevel = weapon.MaxLevel;
            d.weight = 8f;
            EditorUtility.SetDirty(d);
            return d;
        }

        static StatusDef Status(string path, string id, string name, string adjective, StatusKind kind, float duration, float speed, Color color)
        {
            var s = AssetDatabase.LoadAssetAtPath<StatusDef>(path);
            if (s != null) return s;
            s = PlayerSetup.EnsureAsset<StatusDef>(path);
            s.id = id; s.displayName = name; s.adjective = adjective; s.kind = kind;
            s.stacking = StatusStacking.Refresh; s.duration = duration; s.maxStacks = 1;
            s.speedMultiplier = speed; s.color = color;
            EditorUtility.SetDirty(s);
            return s;
        }

        static WeaponDef Satchel()
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(SatchelPath);
            if (w == null)
            {
                w = PlayerSetup.EnsureAsset<WeaponDef>(SatchelPath);
                w.id = "bomb_satchel"; w.displayName = "Bomb Satchel";
                w.description = "Lobs a bomb into the thickest crowd nearby. It lands, fizzes, and blows everything back.";
                w.pattern = WeaponPattern.Lob;
                // width = blast radius, acquireRange = throw range, flight/fuse shrink with Projectile Speed
                w.damage = 14f; w.critChance = 0.06f; w.critMultiplier = 2f; w.knockback = 7f;
                w.cooldown = 1.6f; w.swingDuration = 0.28f; w.windup = 0.12f;
                w.range = 9f; w.width = 2.2f; w.acquireRange = 9f; w.amount = 1;
                w.flightTime = 0.6f; w.fuse = 0.4f; w.arcHeight = 2.2f; w.meshScale = 1.1f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.Area, 0.15f)), L(Pct(StatId.ProjectileSpeed, 0.25f)),
                    L(Pct(StatId.AttackSpeed, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.Damage, 0.25f), Pct(StatId.Area, 0.1f)),
                };
            }
            if (w.mesh == null) w.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BombMesh);
            EditorUtility.SetDirty(w);
            return w;
        }

        static UpgradeDef SatchelCard(WeaponDef satchel)
        {
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(SatchelCardPath);
            if (d != null) return d;
            d = PlayerSetup.EnsureAsset<UpgradeDef>(SatchelCardPath);
            d.id = "weapon_" + satchel.id; d.displayName = satchel.displayName; d.glyph = "BM";
            d.kind = UpgradeKind.Weapon;
            d.weapon = satchel;
            d.maxLevel = satchel.MaxLevel;
            d.weight = 8f;
            EditorUtility.SetDirty(d);
            return d;
        }

        static UnlockDef GoblinUnlock(UpgradeDef satchelCard)
        {
            var u = AssetDatabase.LoadAssetAtPath<UnlockDef>(GoblinUnlockPath);
            if (u != null) return u;
            u = PlayerSetup.EnsureAsset<UnlockDef>(GoblinUnlockPath);
            u.id = "pit_goblin";
            u.displayName = "The Pit Goblin";
            u.description = "The thing in the crate wants a turn. The Pit Goblin can be chosen, and its Bomb Satchel joins the level-up pool.";
            u.hint = "Something rattles in the Slave Pit...";
            u.conditions = new[] { new UnlockCondition { kind = UnlockConditionKind.Flag, id = Flag, amount = 1f } };
            u.addsToPool = new[] { satchelCard };
            EditorUtility.SetDirty(u);
            return u;
        }

        static T Trait<T>(string path, string id, string name, string description) where T : GladiatorTrait
        {
            var t = AssetDatabase.LoadAssetAtPath<T>(path);
            if (t != null) return t;
            t = PlayerSetup.EnsureAsset<T>(path);
            t.id = id; t.displayName = name; t.description = description;
            EditorUtility.SetDirty(t);
            return t;
        }

        /// <summary>A copy of `from` at `to` the first time (then `tweak`), else the existing asset.</summary>
        static T Copy<T>(string from, string to, System.Action<T> tweak) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(to);
            if (existing != null) return existing;
            if (!AssetDatabase.CopyAsset(from, to)) return PlayerSetup.EnsureAsset<T>(to);
            var t = AssetDatabase.LoadAssetAtPath<T>(to);
            tweak(t);
            EditorUtility.SetDirty(t);
            return t;
        }
    }
}
