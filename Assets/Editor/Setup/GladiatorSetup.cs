using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Phase 5.2 gladiator data (docs/GLADIATORS.md): the Pit Goblin (GL_PitGoblin) with its capsule, animation, the
    /// Bomb Satchel and its card, the Scavenger and Trap Slide traits, the Root status, and UL_PitGoblin (the rattling
    /// crate flag). Every asset is created once and is designer-owned afterwards; only missing links are filled in.
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
                g.description = "Lived under the pit floor. Lobs bombs into crowds, leaves snares where it slides, and pulls junk weapons out of its sack.";
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
            UpgradeSetup.Refresh();
            MetaSetup.CreateOrUpdate();
            AssetDatabase.SaveAssets();
            return g;
        }

        /// <summary>Every gladiator, the Nobody first (select screen and Codex order).</summary>
        public static GladiatorDef[] All()
        {
            var nobody = AssetDatabase.LoadAssetAtPath<GladiatorDef>(UpgradeSetup.NobodyPath);
            var goblin = AssetDatabase.LoadAssetAtPath<GladiatorDef>(GoblinPath);
            var list = new System.Collections.Generic.List<GladiatorDef>();
            if (nobody != null) list.Add(nobody);
            if (goblin != null) list.Add(goblin);
            return list.ToArray();
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
            u.hint = "Something rattles in the Slave Pit…";
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
