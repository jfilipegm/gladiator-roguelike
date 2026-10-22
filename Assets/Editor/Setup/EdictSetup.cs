using System;
using System.Collections.Generic;
using System.IO;
using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEditor;
using UnityEngine;



namespace Gladiator.EditorTools
{
    /// <summary>
    /// R7 Edicts (docs/tracks/R7-design.md §2): the eleven RunModifierDefs (Assets/Data/Edicts/ED_*), the seven unlocks
    /// that open them (in MetaSetup.Dir: The Editor Takes Notice opens the feature and the three free edicts, four are sold
    /// in the Market, three challenges ask for wins under edicts or the Grand Arena), each edict's icon (the upgrade
    /// atlas's "ed_&lt;id&gt;" row), and MetaConfig.edicts / edictsUnlock. Creates or updates: every run writes the spec's
    /// numbers and texts, so running it twice changes nothing. Run "Gladiator/Setup/Upgrade Icons" first (the atlas
    /// import); this ends with Create Meta Data, which collects the new unlocks into MetaConfig.unlocks.
    /// </summary>
    public static class EdictSetup
    {
        public const string Dir = "Assets/Data/Edicts/";
        const string Ul = MetaSetup.Dir;

        [MenuItem("Gladiator/Setup/Meta/Create Edict Data")]
        public static MetaConfig CreateOrUpdate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Edicts")) AssetDatabase.CreateFolder("Assets/Data", "Edicts");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(IconSetup.AtlasPath);
            if (atlas == null) Debug.LogError("[EdictSetup] no icon atlas at " + IconSetup.AtlasPath + " (run Gladiator/Setup/Upgrade Icons)");
            var rows = IconRows();

            // ---------------------------------------------------------------- the unlocks
            var feature = Unlock("UL_Edicts", "edicts", "The Editor Takes Notice", UnlockCategory.Challenge,
                "The Editor of the games has noticed you. EDICTS open on the arena screen: accept his decrees and the fight " +
                "gets harder and the Renown richer. Iron Hides, The Hungry Horde and Blood Sport are yours to accept.",
                "Defeat Gorm. Someone in the stands is taking notes.",
                C(UnlockConditionKind.DefeatBoss, "gorm", 1f));
            var glassJaw = Market("UL_Edict_GlassJaw", "edict_glass_jaw", "Glass Jaw", "max HP ×0.6", 25);
            var swiftFoes = Market("UL_Edict_SwiftFoes", "edict_swift_foes", "Swift Foes", "enemies move +15 % faster", 20);
            var emptyPurses = Market("UL_Edict_EmptyPurses", "edict_empty_purses", "Empty Purses", "gold ×0.5, chests cost ×1.5", 15);
            var fickleCrowd = Market("UL_Edict_FickleCrowd", "edict_fickle_crowd", "A Fickle Crowd", "Favor at half speed, boredom ×2", 15);
            var editorsEye = Unlock("UL_EditorsEye", "editors_eye", "Under the Editor's Eye", UnlockCategory.Challenge,
                "The Editor liked what he saw, and wants more. New edicts: Meagre Choices and The Impatient Emperor.",
                "Win a run under 2 edicts.",
                C(UnlockConditionKind.WinWithEdicts, "", 2f));
            var dayOfChampions = Unlock("UL_DayOfChampions", "day_of_champions", "A Day of Champions", UnlockCategory.Challenge,
                "The Grand Arena's champions all want a turn at you. New edict: Champions' Day (elites twice as often).",
                "Reach the Grand Arena.",
                C(UnlockConditionKind.ReachArena, "", 3f));
            var emperorsPleasure = Unlock("UL_EmperorsPleasure", "emperors_pleasure", "The Emperor's Pleasure", UnlockCategory.Challenge,
                "The Emperor himself was watching, and he wants cruelty. New edict: No Mercy.",
                "Win a run under 4 edicts.",
                C(UnlockConditionKind.WinWithEdicts, "", 4f));

            // ---------------------------------------------------------------- the edicts (spec §2.3, in its order)
            var edicts = new[]
            {
                Edict("ED_IronHides", "iron_hides", "Iron Hides", "Enemies +50 % HP; bosses +25 %", 0.25f, null,
                    "The Editor has them fed, oiled and armoured: everything takes longer to fall.",
                    e => { e.enemyHpMul = 1.5f; e.bossHpMul = 1.25f; }),
                Edict("ED_HungryHorde", "hungry_horde", "The Hungry Horde", "+30 % enemies alive and per swarm", 0.25f, null,
                    "More gates open, more mouths to feed. The arena's cap on fighters still holds.",
                    e => { e.densityMul = 1.3f; e.swarmMul = 1.3f; }),
                Edict("ED_BloodSport", "blood_sport", "Blood Sport", "You take +40 % damage", 0.2f, null,
                    "The crowd paid to see blood. Yours will do.",
                    e => e.playerDamageTakenMul = 1.4f),
                Edict("ED_GlassJaw", "glass_jaw", "Glass Jaw", "Max HP ×0.6", 0.25f, glassJaw,
                    "One clean blow and you're on the sand. Don't take it.",
                    e => e.playerStats = new[] { new StatMod(StatId.MaxHp, ModOp.Multiply, 0.6f) }),
                Edict("ED_SwiftFoes", "swift_foes", "Swift Foes", "Enemies move +15 % faster", 0.2f, swiftFoes,
                    "Whipped out of the pens at a run.",
                    e => e.enemySpeedMul = 1.15f),
                Edict("ED_EmptyPurses", "empty_purses", "Empty Purses", "Gold ×0.5; chests cost ×1.5", 0.15f, emptyPurses,
                    "The Editor counts every coin twice and pays out half.",
                    e => { e.playerStats = new[] { new StatMod(StatId.GoldGain, ModOp.Multiply, 0.5f) }; e.chestPriceMul = 1.5f; }),
                Edict("ED_FickleCrowd", "fickle_crowd", "A Fickle Crowd", "Favor fills at half speed; boredom ×2", 0.15f, fickleCrowd,
                    "They've seen it all before. Impress them, and quickly: they tire twice as fast.",
                    e => { e.favorGainMul = 0.5f; e.boredomMul = 2f; }),
                Edict("ED_MeagreChoices", "meagre_choices", "Meagre Choices", "One card fewer per level-up; no rerolls", 0.2f, editorsEye,
                    "The armoury is half bare: two cards a level (three with Luck), and nothing swapped. Chests, the altar " +
                    "and the boss's reward are unchanged.",
                    e => { e.levelUpCardDelta = -1; e.playerStats = new[] { new StatMod(StatId.Rerolls, ModOp.Multiply, 0f) }; }),
                Edict("ED_ImpatientEmperor", "impatient_emperor", "The Impatient Emperor", "The horde comes a third sooner", 0.2f, editorsEye,
                    "The Emperor has a banquet to get to: swarms, elites and the full horde arrive early. The timer and the " +
                    "boss don't change.",
                    e => e.clockScale = 1.33f),
                Edict("ED_ChampionsDay", "champions_day", "Champions' Day", "Elites come twice as often", 0.2f, dayOfChampions,
                    "Every champion in the city wants a turn at you.",
                    e => e.eliteEveryMul = 0.5f),
                Edict("ED_NoMercy", "no_mercy", "No Mercy", "No healing of any kind", 0.3f, emperorsPleasure,
                    "No water-boy, no surgeon: regen, amphorae, lifesteal and heals on kill all stop.",
                    e => { e.healingScale = 0f; e.dailyEligible = false; }), // too harsh for a forced daily (spec §2.3)
            };
            foreach (var e in edicts)
            {
                if (atlas != null && rows.TryGetValue("ed_" + e.id, out int row)) { e.icon = atlas; e.iconRow = row; }
                else { e.icon = null; e.iconRow = -1; Debug.LogWarning("[EdictSetup] no icon for ed_" + e.id + " (PixelSrc/upgrade_icons.py)"); }
                EditorUtility.SetDirty(e);
            }

            var cfg = PlayerSetup.EnsureAsset<MetaConfig>(MetaSetup.ConfigPath);
            cfg.edicts = edicts;
            cfg.edictsUnlock = feature;
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            cfg = MetaSetup.CreateOrUpdate(); // collects the seven unlocks, saves
            float bonus = 0f;
            foreach (var e in edicts) bonus += e.renownBonus;
            Debug.Log($"[EdictSetup] {edicts.Length} edicts (all on: Renown ×{1f + bonus:0.00}), {cfg.unlocks.Length} unlocks in MetaConfig");
            return cfg;
        }

        static RunModifierDef Edict(string file, string id, string name, string effectLine, float renownBonus, UnlockDef unlock,
            string flavour, Action<RunModifierDef> effects)
        {
            var e = Ensure<RunModifierDef>(Dir + file + ".asset");
            e.id = id; e.displayName = name; e.description = flavour; e.effectLine = effectLine;
            e.renownBonus = renownBonus; e.unlock = unlock; e.dailyEligible = true;
            // Every effect back to "no change" before this edict's own, so a re-run never keeps an old number.
            e.playerStats = Array.Empty<StatMod>();
            e.playerDamageTakenMul = e.healingScale = 1f;
            e.enemyHpMul = e.bossHpMul = e.enemySpeedMul = 1f;
            e.densityMul = e.swarmMul = e.eliteEveryMul = e.clockScale = 1f;
            e.chestPriceMul = e.favorGainMul = e.boredomMul = 1f;
            e.levelUpCardDelta = 0;
            effects(e);
            return e;
        }

        static UnlockDef Unlock(string file, string id, string name, UnlockCategory category, string description, string hint,
            params UnlockCondition[] conditions)
        {
            var u = Ensure<UnlockDef>(Ul + file + ".asset");
            u.id = id; u.displayName = name; u.category = category; u.description = description; u.hint = hint;
            u.conditions = conditions;
            u.renownCost = 0;
            u.renownBounty = 0;
            u.addsToPool = Array.Empty<UpgradeDef>(); // the reward is the edicts that point at it (CodexPanel.Reward)
            EditorUtility.SetDirty(u);
            return u;
        }

        /// <summary>A Market edict: 150 Renown (spec §2.3), never granted by conditions.</summary>
        static UnlockDef Market(string file, string id, string edictName, string effect, int renownPercent)
        {
            var u = Unlock(file, id, "Edict: " + edictName, UnlockCategory.Market,
                $"A new edict for the arena screen: {edictName} ({effect}). Accept it before a fight for +{renownPercent} % Renown.",
                "For sale in the Market.");
            u.renownCost = 150;
            return u;
        }

        static UnlockCondition C(UnlockConditionKind k, string id, float amount) => new UnlockCondition { kind = k, id = id, amount = amount };

        static T Ensure<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        /// <summary>The atlas index (PixelSrc/upgrade_icons.py): icon id → row.</summary>
        static Dictionary<string, int> IconRows()
        {
            var rows = new Dictionary<string, int>();
            if (!File.Exists(IconSetup.IndexPath)) { Debug.LogError("[EdictSetup] no icon index at " + IconSetup.IndexPath); return rows; }
            foreach (var line in File.ReadAllLines(IconSetup.IndexPath))
            {
                var parts = line.Split(' ');
                if (parts.Length == 2 && int.TryParse(parts[1], out int row)) rows[parts[0]] = row;
            }
            return rows;
        }
    }
}
