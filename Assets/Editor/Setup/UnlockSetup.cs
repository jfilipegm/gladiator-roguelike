using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// Phase 5.6 data (docs/UNLOCKS.md): the unlockable weapons (Sling, Net, Gorm's Whip) and Boons (Loaded Dice, Gilded
    /// Palm, Winged Sandals, Wax Tablet), the MOVEMENT/GOLD/XP synergies they complete, and the unlocks that hold them
    /// back: secrets, challenges (some only pay Renown) and Market items. Like UpgradeSetup, each asset is created once
    /// with its starting numbers and is designer-owned afterwards; existing unlocks only get a category if they have none
    /// set yet (the Pit Goblin's crate is a Secret).
    /// </summary>
    public static class UnlockSetup
    {
        const string Up = UpgradeSetup.Dir, Wp = UpgradeSetup.WeaponDir, Sy = UpgradeSetup.SynergyDir, St = UpgradeSetup.StatusDir;
        const string Ul = MetaSetup.Dir;

        [MenuItem("Gladiator/Setup/Meta/Create Phase 5.6 Unlocks")]
        public static void CreateOrUpdate()
        {
            var root = Status("SE_Root");
            var bleed = Status("SE_Bleed");
            var slow = Status("SE_Slow");

            // ---------------------------------------------------------------- weapons
            var sling = Weapon("WP_Sling", "sling", "Sling", "Hurls a stone at the farthest enemy in reach. Big crits.", WeaponPattern.Projectile, w =>
            {
                w.damage = 22f; w.critChance = 0.15f; w.critMultiplier = 2.5f; w.knockback = 5f; w.cooldown = 1.3f;
                w.swingDuration = 0.25f; w.windup = 0.12f; w.range = 22f; w.width = 0.3f; w.acquireRange = 15f; w.amount = 1;
                w.speed = 30f; w.pierce = 0; w.targetFarthest = true; w.mesh = Mesh("WP_SlingStone"); w.meshScale = 1.6f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.CritChance, 0.1f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.CritDamage, 0.5f)), L(Add(StatId.Amount, 1f), Add(StatId.Pierce, 1f)),
                };
            });
            var net = Weapon("WP_Net", "net", "Net", "A weighted net, thrown slow and wide. Everything it lands on is pinned for a moment.", WeaponPattern.Projectile, w =>
            {
                w.damage = 5f; w.critChance = 0.05f; w.knockback = 0f; w.cooldown = 2.4f; w.swingDuration = 0.3f; w.windup = 0.12f;
                w.range = 9f; w.width = 1.2f; w.acquireRange = 9f; w.amount = 1; w.speed = 9f; w.pierce = -1;
                w.mesh = Mesh("WP_Net"); w.meshScale = 1f;
                w.hitEffects = new[] { new BoonEffect { kind = EffectKind.StatusOnHit, status = root, chance = 1f, power = 0f, stacks = 1 } };
                w.levels = new[]
                {
                    L(Pct(StatId.Area, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.15f)), L(Pct(StatId.Damage, 0.3f)),
                    L(Pct(StatId.Area, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.AttackSpeed, 0.2f)),
                };
            });
            var whip = Weapon("WP_GormsWhip", "gorms_whip", "Gorm's Whip", "Cracks a long thin line through everything. The tip always crits: fight at whip's length.", WeaponPattern.Thrust, w =>
            {
                w.damage = 13f; w.critChance = 0.05f; w.knockback = 3f; w.cooldown = 0.9f; w.swingDuration = 0.22f; w.windup = 0.1f;
                w.range = 6f; w.width = 0.45f; w.acquireRange = 6.4f; w.amount = 1; w.tipLength = 1.5f; w.spreadStrikes = true;
                w.onHitStatus = bleed; w.onHitStatusPower = 0.1f; w.onHitStacks = 1;
                w.mesh = Mesh("WP_Whip"); w.meshScale = 1.2f;
                w.levels = new[]
                {
                    L(Pct(StatId.Damage, 0.2f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.Area, 0.15f)), L(Pct(StatId.AttackSpeed, 0.15f)),
                    L(Pct(StatId.Damage, 0.25f)), L(Add(StatId.Amount, 1f)), L(Pct(StatId.Area, 0.15f), Pct(StatId.Damage, 0.2f)),
                };
            });
            var slingCard = WeaponCard("UP_W_Sling", sling, "SL", UpgradeTag.Crit);
            var netCard = WeaponCard("UP_W_Net", net, "NT", UpgradeTag.None);
            var whipCard = WeaponCard("UP_W_GormsWhip", whip, "WH", UpgradeTag.Blood);

            // ---------------------------------------------------------------- Boons
            var dice = Boon("UP_LoadedDice", "loaded_dice", "Loaded Dice", "LD", UpgradeTag.Gold, 5, M(StatId.Luck, ModOp.Flat, 0.06f), M(StatId.GoldGain, ModOp.Percent, 0.10f));
            var palm = Boon("UP_GildedPalm", "gilded_palm", "Gilded Palm", "GP", UpgradeTag.Gold, 5, M(StatId.GoldGain, ModOp.Percent, 0.20f), M(StatId.PickupRange, ModOp.Percent, 0.10f));
            var sandals = Boon("UP_WingedSandals", "winged_sandals", "Winged Sandals", "WS", UpgradeTag.Movement, 2, M(StatId.ExtraJumps, ModOp.Flat, 1f));
            var tablet = Boon("UP_WaxTablet", "wax_tablet", "Wax Tablet", "WT", UpgradeTag.Xp, 5, M(StatId.XpGain, ModOp.Percent, 0.08f), M(StatId.Rerolls, ModOp.Flat, 1f));

            // ---------------------------------------------------------------- synergies (the new Boons are each tag's third carrier)
            Synergy("SY_Whirlwind", "whirlwind", "Whirlwind", "MOVEMENT SYNERGY: WHIRLWIND!", UpgradeTag.Movement,
                "+10% speed. Your slides leave a trail that slows.",
                new[] { M(StatId.MoveSpeed, ModOp.Percent, 0.10f) },
                new[] { new BoonEffect { kind = EffectKind.SlideTrail, status = slow, power = 0f, radius = 1.2f, duration = 2f, stacks = 1 } });
            Synergy("SY_Midas", "midas", "Midas", "GOLD SYNERGY: MIDAS!", UpgradeTag.Gold,
                "+25% gold, +25% pickup range, +10% luck.",
                new[] { M(StatId.GoldGain, ModOp.Percent, 0.25f), M(StatId.PickupRange, ModOp.Percent, 0.25f), M(StatId.Luck, ModOp.Flat, 0.10f) }, null);
            Synergy("SY_Prodigy", "prodigy", "Prodigy", "XP SYNERGY: PRODIGY!", UpgradeTag.Xp,
                "+15% XP, +1 reroll, +1 banish.",
                new[] { M(StatId.XpGain, ModOp.Percent, 0.15f), M(StatId.Rerolls, ModOp.Flat, 1f), M(StatId.Banishes, ModOp.Flat, 1f) }, null);

            // ---------------------------------------------------------------- unlocks
            Unlock("UL_Sling", "sling", "The Sling", UnlockCategory.Secret,
                "The rat-catcher's sling is yours: a stone at the farthest enemy in reach, and big crits. Now in the level-up pool.",
                "Something small and shiny runs the Slave Pit's walls. Faster than you can run…",
                new[] { C(UnlockConditionKind.Flag, "golden_rat") }, slingCard);
            Unlock("UL_LoadedDice", "loaded_dice", "Loaded Dice", UnlockCategory.Secret,
                "You beat the Bookmaker at his own game, and took his dice. Loaded Dice (GOLD: +Luck, +gold) joins the level-up pool.",
                "Someone in the Provincial Pit takes bets. Find him, and beat the odds.",
                new[] { C(UnlockConditionKind.Flag, "bookmaker_paid") }, dice);
            Unlock("UL_GormsWhip", "gorms_whip", "Gorm's Whip", UnlockCategory.Challenge,
                "The Pit Master's whip: a long crack through everything in a line, and the tip always crits. Now in the level-up pool.",
                "Defeat Gorm 3 times.",
                new[] { C(UnlockConditionKind.DefeatBoss, "gorm", 3f) }, whipCard);
            Unlock("UL_Net", "net", "The Net", UnlockCategory.Challenge,
                "A retiarius's net: thrown slow and wide, it pins everything it lands on. Now in the level-up pool.",
                "Defeat the Provincial Champion.",
                new[] { C(UnlockConditionKind.DefeatBoss, "champion", 1f) }, netCard);
            Market("UL_WingedSandals", "winged_sandals", "Winged Sandals", 300,
                "Winged Sandals (MOVEMENT): jump again in mid-air, twice at level 2. Joins the level-up pool.", sandals);
            Market("UL_WaxTablet", "wax_tablet", "Wax Tablet", 300,
                "Wax Tablet (XP): more XP and a reroll per level. Joins the level-up pool.", tablet);
            Market("UL_GildedPalm", "gilded_palm", "Gilded Palm", 300,
                "Gilded Palm (GOLD): more gold and a longer reach for it. Joins the level-up pool.", palm);
            Bounty("UL_ChFirstEvolution", "ch_first_evolution", "First Evolution", 100,
                "Evolve a weapon: a level-8 weapon plus its partner Boon, then a chest.",
                new[] { C(UnlockConditionKind.Evolve, "", 1f) });
            Bounty("UL_ChSynergist", "ch_synergist", "Synergist", 150,
                "Switch on 3 different synergies (over any number of runs).",
                new[] { C(UnlockConditionKind.SynergiesFound, "", 3f) });
            Bounty("UL_ChButcher", "ch_butcher", "Butcher of the Pit", 100,
                "Kill 5,000 enemies in a single run.",
                new[] { C(UnlockConditionKind.RunKills, "", 5000f) });
            Bounty("UL_ChEveryGladiator", "ch_every_gladiator", "Every Gladiator Wins", 200,
                "Win a run with the Nobody, the Pit Goblin and the Blood Knight.",
                new[] { C(UnlockConditionKind.WinWithGladiator, "nobody", 1f), C(UnlockConditionKind.WinWithGladiator, "pit_goblin", 1f),
                        C(UnlockConditionKind.WinWithGladiator, "blood_knight", 1f) });

            Secrets();

            var goblin = AssetDatabase.LoadAssetAtPath<UnlockDef>(Ul + "UL_PitGoblin.asset");
            if (goblin != null && goblin.category != UnlockCategory.Secret) { goblin.category = UnlockCategory.Secret; EditorUtility.SetDirty(goblin); }

            AssetDatabase.SaveAssets();
            UpgradeSetup.Refresh();
            MetaSetup.CreateOrUpdate(); // lists every UnlockDef
        }

        /// <summary>The arenas' new secrets (the layouts' regenerators set the same): the golden rat in the Slave Pit's nooks,
        /// the bookmaker's booths in the Provincial Pit. Rebuild the arena scenes afterwards.</summary>
        static void Secrets()
        {
            var slave = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(SlavePitLayout.Path);
            if (slave != null && (slave.secrets & ArenaSecrets.GoldenRat) == 0)
            {
                slave.secrets |= ArenaSecrets.GoldenRat;
                EditorUtility.SetDirty(slave);
            }
            var prov = AssetDatabase.LoadAssetAtPath<ArenaLayoutDef>(ProvincialPitLayout.Path);
            if (prov != null && (prov.secrets & ArenaSecrets.Bookmaker) == 0)
            {
                if (prov.secretSpots == null || prov.secretSpots.Length == 0) prov.secretSpots = ProvincialPitLayout.BookmakerSpots;
                prov.secrets = ArenaSecrets.Bookmaker; // no rattling crate here
                EditorUtility.SetDirty(prov);
            }
        }

        static StatusDef Status(string file) => AssetDatabase.LoadAssetAtPath<StatusDef>(St + file + ".asset");
        static Mesh Mesh(string model) => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Models/Weapons/" + model + ".fbx");
        static WeaponDef.Level L(params StatMod[] mods) => new WeaponDef.Level { mods = mods };
        static StatMod Pct(StatId s, float v) => new StatMod(s, ModOp.Percent, v);
        static StatMod Add(StatId s, float v) => new StatMod(s, ModOp.Flat, v);
        static StatMod M(StatId s, ModOp op, float v) => new StatMod(s, op, v);
        static UnlockCondition C(UnlockConditionKind k, string id, float amount = 1f) => new UnlockCondition { kind = k, id = id, amount = amount };

        static WeaponDef Weapon(string file, string id, string name, string description, WeaponPattern pattern, System.Action<WeaponDef> init)
        {
            string path = Wp + file + ".asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(path);
            if (w != null) return w;
            w = ScriptableObject.CreateInstance<WeaponDef>();
            w.id = id; w.displayName = name; w.description = description; w.pattern = pattern;
            init(w);
            AssetDatabase.CreateAsset(w, path);
            return w;
        }

        static UpgradeDef WeaponCard(string file, WeaponDef weapon, string glyph, UpgradeTag tags)
        {
            string path = Up + file + ".asset";
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(path);
            if (d != null || weapon == null) return d;
            d = ScriptableObject.CreateInstance<UpgradeDef>();
            d.id = "weapon_" + weapon.id; d.displayName = weapon.displayName; d.glyph = glyph; d.tags = tags;
            d.kind = UpgradeKind.Weapon;
            d.weapon = weapon;
            d.maxLevel = weapon.MaxLevel;
            d.weight = 8f;
            AssetDatabase.CreateAsset(d, path);
            return d;
        }

        static UpgradeDef Boon(string file, string id, string name, string glyph, UpgradeTag tags, int maxLevel, params StatMod[] mods)
        {
            string path = Up + file + ".asset";
            var d = AssetDatabase.LoadAssetAtPath<UpgradeDef>(path);
            if (d != null) return d;
            d = ScriptableObject.CreateInstance<UpgradeDef>();
            d.id = id; d.displayName = name; d.glyph = glyph; d.tags = tags;
            d.kind = UpgradeKind.Boon;
            d.maxLevel = maxLevel;
            d.perLevel = mods;
            AssetDatabase.CreateAsset(d, path);
            return d;
        }

        static void Synergy(string file, string id, string name, string announce, UpgradeTag tag, string description, StatMod[] mods, BoonEffect[] effects)
        {
            string path = Sy + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TagSynergyDef>(path) != null) return;
            var s = ScriptableObject.CreateInstance<TagSynergyDef>();
            s.id = id; s.displayName = name; s.announce = announce; s.description = description; s.tag = tag; s.count = 3;
            s.mods = mods ?? System.Array.Empty<StatMod>();
            s.effects = effects ?? System.Array.Empty<BoonEffect>();
            AssetDatabase.CreateAsset(s, path);
        }

        static UnlockDef Unlock(string file, string id, string name, UnlockCategory category, string description, string hint,
            UnlockCondition[] conditions, UpgradeDef reward)
        {
            string path = Ul + file + ".asset";
            var u = AssetDatabase.LoadAssetAtPath<UnlockDef>(path);
            if (u != null) return u;
            u = ScriptableObject.CreateInstance<UnlockDef>();
            u.id = id; u.displayName = name; u.category = category; u.description = description; u.hint = hint;
            u.conditions = conditions;
            u.addsToPool = reward != null ? new[] { reward } : System.Array.Empty<UpgradeDef>();
            if (reward == null) Debug.LogWarning("[UnlockSetup] " + file + " has no reward card");
            AssetDatabase.CreateAsset(u, path);
            return u;
        }

        static void Market(string file, string id, string name, int cost, string description, UpgradeDef reward)
        {
            var u = Unlock(file, id, name, UnlockCategory.Market, description, "For sale in the Market.", System.Array.Empty<UnlockCondition>(), reward);
            if (u.renownCost != 0) return;
            u.renownCost = cost;
            EditorUtility.SetDirty(u);
        }

        static void Bounty(string file, string id, string name, int bounty, string goal, UnlockCondition[] conditions)
        {
            string path = Ul + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UnlockDef>(path) != null) return;
            var u = ScriptableObject.CreateInstance<UnlockDef>();
            u.id = id; u.displayName = name; u.category = UnlockCategory.Challenge;
            u.description = goal + " Paid " + bounty + " Renown.";
            u.hint = goal;
            u.conditions = conditions;
            u.renownBounty = bounty;
            AssetDatabase.CreateAsset(u, path);
        }
    }
}
