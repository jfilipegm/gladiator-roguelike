using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;



namespace Gladiator.Tests
{
    /// <summary>
    /// R6 Skins, the data side (docs/tracks/R7-contract.md "R6-B"): which skins are owned (free, Market after Buy, champion
    /// after three wins), equipping (owned only, one per gladiator, Unequip = the default, unknown or locked ids ignored),
    /// the skin-id rule, SkinsOf's order, save v3 → v4, GameSession.Skin (kept by Retry, reset by Clear), and the data
    /// Create Skin Data writes from BlenderSrc/characters/skins.json (ignored until the table exists).
    /// </summary>
    public class SkinTests
    {
        const int Day = 20261022;

        string _path;
        MetaConfig _meta;
        GladiatorDef _nobody, _knight;
        UnlockDef _market, _champion;
        SkinDef _freedman, _unbowed, _plain, _ashen;
        RunDef _run;
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Application.temporaryCachePath, "skin_test_save.json");
            DeleteFiles();
            SaveSystem.PathOverride = _path;
            MetaProgress.Reload();
            GameSession.Clear();
            RunModifiers.Clear();
            _nobody = Gladiator("nobody", "The Nobody");
            _knight = Gladiator("blood_knight", "The Blood Knight");
            _market = Unlock("skin_nobody_freedman", UnlockCategory.Market);
            _market.renownCost = 250;
            _champion = Unlock("skin_nobody_unbowed", UnlockCategory.Challenge, C(UnlockConditionKind.WinWithGladiator, "nobody", 3f));
            _freedman = Skin("nobody_freedman", _nobody, _market);
            _unbowed = Skin("nobody_unbowed", _nobody, _champion);
            _plain = Skin("nobody_plain", _nobody, null);   // free
            _ashen = Skin("blood_knight_ashen_knight", _knight, null);
            _meta = Make<MetaConfig>();
            _meta.unlocks = new[] { _market, _champion };
            _meta.skins = new[] { _freedman, _unbowed, _plain, _ashen };
            _run = Make<RunDef>();
            _run.id = "slave_pit";
            _run.arenas = new[] { new RunDef.Arena { displayName = "Slave Pit", scenePath = "Assets/Scenes/slave_pit.unity" } };
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.Clear();
            RunModifiers.Clear();
            SaveSystem.PathOverride = null;
            MetaProgress.Reload();
            DeleteFiles();
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        void DeleteFiles()
        {
            foreach (var f in new[] { _path, _path + ".corrupt", _path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        GladiatorDef Gladiator(string id, string name)
        {
            var g = Make<GladiatorDef>();
            g.id = id; g.displayName = name;
            return g;
        }

        UnlockDef Unlock(string id, UnlockCategory category, params UnlockCondition[] conditions)
        {
            var u = Make<UnlockDef>();
            u.id = id; u.category = category; u.conditions = conditions;
            return u;
        }

        SkinDef Skin(string id, GladiatorDef g, UnlockDef unlock)
        {
            var s = Make<SkinDef>();
            s.id = id; s.displayName = id; s.gladiator = g; s.unlock = unlock;
            return s;
        }

        static UnlockCondition C(UnlockConditionKind k, string id, float amount) => new UnlockCondition { kind = k, id = id, amount = amount };

        static RunSummary Win(string gladiator) =>
            new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, Kills = 900, GoldEarned = 30, GladiatorId = gladiator };

        static RunSummary Death(string gladiator) =>
            new RunSummary { Outcome = RunOutcome.Died, Elapsed = 200f, Kills = 300, GoldEarned = 10, GladiatorId = gladiator };

        /// <summary>A save that owns the Market and the champion skins.</summary>
        SaveData Owning()
        {
            var d = new SaveData();
            d.unlocked.Add(_market.id);
            d.unlocked.Add(_champion.id);
            return d;
        }

        // ---------------------------------------------------------------- ownership

        [Test]
        public void IsOwned_Free_Always()
        {
            var d = new SaveData();
            Assert.IsTrue(MetaProgress.IsOwned(_plain, d), "no unlock = free");
            Assert.IsTrue(MetaProgress.IsOwned(_ashen, d));
            Assert.IsFalse(MetaProgress.IsOwned(null, d));
            Assert.IsFalse(MetaProgress.IsOwned(_plain, null));
        }

        [Test]
        public void IsOwned_Market_OnlyAfterBuy()
        {
            var d = new SaveData { renown = 100 };
            Assert.IsFalse(MetaProgress.IsOwned(_freedman, d));
            Assert.AreEqual(BuyResult.TooExpensive, MetaProgress.Buy(_market, d));
            Assert.IsFalse(MetaProgress.IsOwned(_freedman, d), "not enough Renown: nothing changes");
            Assert.AreEqual(100, d.renown);
            d.renown = 300;
            Assert.AreEqual(BuyResult.Bought, MetaProgress.Buy(_market, d));
            Assert.IsTrue(MetaProgress.IsOwned(_freedman, d));
            Assert.AreEqual(50, d.renown, "250 Renown spent");
            Assert.AreEqual(BuyResult.AlreadyOwned, MetaProgress.Buy(_market, d));
            // conditions never grant a Market skin
            var rich = new SaveData();
            for (int i = 0; i < 5; i++) MetaProgress.Apply(Win("nobody"), _meta, rich);
            Assert.IsFalse(MetaProgress.IsOwned(_freedman, rich));
        }

        [Test]
        public void IsOwned_Champion_AfterThreeWinsWithHim()
        {
            var d = new SaveData();
            Assert.IsFalse(MetaProgress.IsOwned(_unbowed, d));
            Assert.AreEqual(BuyResult.NotForSale, MetaProgress.Buy(_champion, new SaveData { renown = 9999 }), "earned, not bought");
            MetaProgress.Apply(Win("nobody"), _meta, d);
            MetaProgress.Apply(Win("nobody"), _meta, d);
            MetaProgress.Apply(Death("nobody"), _meta, d);
            MetaProgress.Apply(Win("blood_knight"), _meta, d);
            Assert.IsFalse(MetaProgress.IsOwned(_unbowed, d), "two wins, a death and someone else's win");
            MetaProgress.Progress(_champion.conditions[0], d, out float cur, out float tgt);
            Assert.AreEqual(2f, cur);
            Assert.AreEqual(3f, tgt);
            var r = MetaProgress.Apply(Win("nobody"), _meta, d);
            CollectionAssert.Contains(r.NewUnlocks, _champion, "the third win grants it");
            Assert.IsTrue(MetaProgress.IsOwned(_unbowed, d));
            Assert.IsTrue(MetaProgress.Equip(_unbowed, d));
        }

        // ---------------------------------------------------------------- equipping

        [Test]
        public void Equip_RefusesAnUnownedSkin()
        {
            var d = new SaveData();
            Assert.IsFalse(MetaProgress.Equip(_freedman, d), "Market, not bought");
            Assert.IsFalse(MetaProgress.Equip(_unbowed, d), "champion, not earned");
            Assert.IsFalse(MetaProgress.Equip(null, d));
            Assert.AreEqual(0, d.equippedSkins.Count, "nothing changes");
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, _meta, d));
            Assert.IsTrue(MetaProgress.Equip(_plain, d), "a free one equips");
            Assert.AreSame(_plain, MetaProgress.EquippedSkin(_nobody, _meta, d));
            Assert.IsFalse(MetaProgress.Equip(_freedman, d));
            Assert.AreSame(_plain, MetaProgress.EquippedSkin(_nobody, _meta, d), "a refused one doesn't replace it");
        }

        [Test]
        public void Equip_KeepsOnePerGladiator()
        {
            var d = Owning();
            Assert.IsTrue(MetaProgress.Equip(_freedman, d));
            Assert.AreSame(_freedman, MetaProgress.EquippedSkin(_nobody, _meta, d));
            Assert.IsTrue(MetaProgress.Equip(_unbowed, d));
            Assert.AreSame(_unbowed, MetaProgress.EquippedSkin(_nobody, _meta, d), "the new one replaces the old");
            CollectionAssert.AreEqual(new[] { "nobody_unbowed" }, d.equippedSkins);
            Assert.IsTrue(MetaProgress.Equip(_ashen, d));
            Assert.AreSame(_ashen, MetaProgress.EquippedSkin(_knight, _meta, d));
            Assert.AreSame(_unbowed, MetaProgress.EquippedSkin(_nobody, _meta, d), "another gladiator's doesn't touch his");
            Assert.AreEqual(2, d.equippedSkins.Count);
            Assert.IsTrue(MetaProgress.Equip(_ashen, d), "equipping it again is fine");
            Assert.AreEqual(2, d.equippedSkins.Count, "and adds nothing");
            Assert.IsTrue(MetaProgress.Equip(_plain, d));
            CollectionAssert.AreEquivalent(new[] { "nobody_plain", "blood_knight_ashen_knight" }, d.equippedSkins);
        }

        [Test]
        public void Equip_RefusesAnIdOffTheRule()
        {
            var odd = Skin("freedman", _nobody, null); // free, but not "nobody_…": Equip couldn't replace it later
            var d = new SaveData();
            Assert.IsFalse(MetaProgress.Equip(odd, d));
            Assert.AreEqual(0, d.equippedSkins.Count);
            var stray = Skin("nobody_lost", null, null);
            Assert.IsFalse(MetaProgress.Equip(stray, d), "no gladiator");
        }

        [Test]
        public void Unequip_ReturnsTheDefault()
        {
            var d = Owning();
            MetaProgress.Equip(_freedman, d);
            MetaProgress.Equip(_ashen, d);
            Assert.IsTrue(MetaProgress.Unequip(_nobody, d));
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, _meta, d), "the default look");
            Assert.AreSame(_ashen, MetaProgress.EquippedSkin(_knight, _meta, d), "only his");
            Assert.IsFalse(MetaProgress.Unequip(_nobody, d), "nothing left to take off");
            Assert.IsFalse(MetaProgress.Unequip(null, d));
            d.equippedSkins.Add("nobody_ghost"); // an id from a skin that no longer exists
            Assert.IsTrue(MetaProgress.Unequip(_nobody, d), "goes too (it's his by the id rule)");
            CollectionAssert.AreEqual(new[] { "blood_knight_ashen_knight" }, d.equippedSkins);
        }

        [Test]
        public void EquippedSkin_IgnoresUnknownAndLockedIds()
        {
            var d = new SaveData();
            d.equippedSkins.Add("nobody_ghost");
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, _meta, d), "no SkinDef has that id");
            d.equippedSkins.Clear();
            d.equippedSkins.Add("nobody_freedman"); // a hand-edited save: equipped but never bought
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, _meta, d), "locked");
            d.unlocked.Add(_market.id);
            Assert.AreSame(_freedman, MetaProgress.EquippedSkin(_nobody, _meta, d), "owned now");
            Assert.IsNull(MetaProgress.EquippedSkin(_knight, _meta, d), "the Nobody's skin isn't the knight's");
            Assert.IsNull(MetaProgress.EquippedSkin(null, _meta, d));
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, null, d));
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, _meta, new SaveData { equippedSkins = null }));
            var otherMeta = Make<MetaConfig>();
            otherMeta.skins = new[] { _unbowed };
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, otherMeta, d), "only skins MetaConfig knows");
        }

        [Test]
        public void SkinsOf_ListsHisInMetaOrder()
        {
            var into = new List<SkinDef> { _ashen };
            Assert.AreEqual(3, MetaProgress.SkinsOf(_nobody, _meta, into));
            CollectionAssert.AreEqual(new[] { _freedman, _unbowed, _plain }, into, "cleared first, then meta order, owned or not");
            Assert.AreEqual(1, MetaProgress.SkinsOf(_knight, _meta, into));
            CollectionAssert.AreEqual(new[] { _ashen }, into);
            Assert.AreEqual(0, MetaProgress.SkinsOf(null, _meta, into));
            Assert.AreEqual(0, into.Count);
            Assert.AreEqual(0, MetaProgress.SkinsOf(_nobody, null, into));
            Assert.AreEqual(0, MetaProgress.SkinsOf(_nobody, _meta, null));
        }

        [Test]
        public void IdBelongsTo_TheGladiatorPrefix()
        {
            Assert.IsTrue(SkinDef.IdBelongsTo("nobody_freedman", "nobody"));
            Assert.IsTrue(SkinDef.IdBelongsTo("blood_knight_ashen_knight", "blood_knight"));
            Assert.IsFalse(SkinDef.IdBelongsTo("nobody", "nobody"), "the gladiator's own id");
            Assert.IsFalse(SkinDef.IdBelongsTo("nobody_", "nobody"), "no name");
            Assert.IsFalse(SkinDef.IdBelongsTo("nobodyx_a", "nobody"));
            Assert.IsFalse(SkinDef.IdBelongsTo("pit_goblin_sewer_king", "nobody"));
            Assert.IsFalse(SkinDef.IdBelongsTo(null, "nobody"));
            Assert.IsFalse(SkinDef.IdBelongsTo("nobody_freedman", ""));
        }

        // ---------------------------------------------------------------- the file (MetaProgress's saving overloads)

        [Test]
        public void EquipAndUnequip_SaveToTheFile()
        {
            MetaProgress.Data.unlocked.Add(_market.id);
            Assert.IsFalse(MetaProgress.Equip(_unbowed), "not earned");
            Assert.IsFalse(File.Exists(_path), "a refused equip writes nothing");
            Assert.IsTrue(MetaProgress.Equip(_freedman));
            CollectionAssert.AreEqual(new[] { "nobody_freedman" }, SaveSystem.Load(_path).equippedSkins, "saved at once");
            MetaProgress.Reload(); // as if the game was restarted
            Assert.AreSame(_freedman, MetaProgress.EquippedSkin(_nobody, _meta));
            Assert.IsTrue(MetaProgress.IsOwned(_freedman));
            MetaProgress.Unequip(_nobody);
            MetaProgress.Reload();
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, _meta), "the default look, saved");
        }

        // ---------------------------------------------------------------- the save

        [Test]
        public void Save_V3File_MigratesToV4()
        {
            Assert.AreEqual(4, SaveData.CurrentVersion);
            File.WriteAllText(_path, "{\"version\":3,\"renown\":7,\"runs\":4,\"edictsChosen\":[\"iron_hides\"],\"dailyAttempt\":" + Day +
                                     ",\"dailiesPlayed\":2,\"dailies\":[{\"date\":" + Day + ",\"run\":\"slave_pit\",\"kills\":12}]," +
                                     "\"unlocked\":[\"skin_nobody_freedman\"]}");
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(4, d.version);
            Assert.AreEqual(7, d.renown);
            Assert.AreEqual(4, d.runs);
            CollectionAssert.AreEqual(new[] { "iron_hides" }, d.edictsChosen, "nothing lost");
            Assert.AreEqual(Day, d.dailyAttempt);
            Assert.AreEqual(2, d.dailiesPlayed);
            Assert.AreEqual(1, d.dailies.Count);
            Assert.AreEqual(12, d.dailies[0].kills);
            Assert.IsNotNull(d.equippedSkins, "Migrate fills the list");
            Assert.AreEqual(0, d.equippedSkins.Count);
            Assert.IsNull(MetaProgress.EquippedSkin(_nobody, _meta, d), "everyone wears the default");

            Assert.IsTrue(MetaProgress.Equip(_freedman, d), "bought in the old file");
            Assert.IsTrue(SaveSystem.Save(d, _path));
            StringAssert.Contains("\"equippedSkins\"", File.ReadAllText(_path));
            var back = SaveSystem.Load(_path);
            Assert.AreEqual(4, back.version, "written as v4");
            CollectionAssert.AreEqual(new[] { "nobody_freedman" }, back.equippedSkins);
            Assert.AreSame(_freedman, MetaProgress.EquippedSkin(_nobody, _meta, back));

            var bare = new SaveData { version = 3, equippedSkins = null };
            bare.Migrate();
            Assert.IsNotNull(bare.equippedSkins);
            Assert.AreEqual(4, bare.version);
            bare.Migrate();
            Assert.AreEqual(4, bare.version, "idempotent");
        }

        [Test]
        public void Save_V1File_RunsEveryStep()
        {
            File.WriteAllText(_path, "{\"version\":1,\"renown\":5,\"unlocked\":[\"gorms_flail\"]}");
            var d = SaveSystem.Load(_path);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.IsNotNull(d.edictsChosen, "the v2 step");
            Assert.IsNotNull(d.dailies, "the v3 step");
            Assert.IsNotNull(d.equippedSkins, "the v4 step");
            Assert.IsTrue(d.IsUnlocked("gorms_flail"));
        }

        // ---------------------------------------------------------------- GameSession.Skin (SuppressLoad: no scene changes)

        [Test]
        public void GameSessionSkin_KeptByRetry_ResetByClear()
        {
            GameSession.SuppressLoad = true;
            GameSession.Skin = _freedman; // the menu sets it at launch
            GameSession.StartRun(_nobody, _run, 0x1234u);
            Assert.AreSame(_freedman, GameSession.Skin, "StartRun keeps it");
            GameSession.Retry();
            Assert.AreSame(_freedman, GameSession.Skin, "Retry keeps it");
            GameSession.RetrySameSeed();
            Assert.AreSame(_freedman, GameSession.Skin, "SAME SEED keeps it");
            var plan = new DailyPlan { Date = Day, Seed = 5u, Run = _run, Gladiator = _nobody, Edicts = Array.Empty<RunModifierDef>() };
            GameSession.StartDaily(plan, true);
            GameSession.Retry();
            Assert.AreSame(_freedman, GameSession.Skin, "the daily and its practice keep it");
            GameSession.Clear();
            Assert.IsNull(GameSession.Skin, "Clear resets it");
        }

        // ---------------------------------------------------------------- the data (Gladiator/Setup/Gladiators/Create Skin Data)

        const string TablePath = "BlenderSrc/characters/skins.json";

        [Serializable]
        sealed class Table
        {
            public Row[] skins = Array.Empty<Row>();
        }

        [Serializable]
        sealed class Row
        {
            public string id, gladiator, @base, name, suffix;
            public string[] chips = Array.Empty<string>();
            public int price;
            public bool champion;
        }

        [Test]
        public void SkinData_MatchesTheTable()
        {
            if (!File.Exists(TablePath)) Assert.Ignore(TablePath + " isn't here yet (R6-art writes it)");
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(TablePath));
            Assert.IsNotNull(table);
            Assert.IsNotNull(table.skins);
            var cfg = AssetDatabase.LoadAssetAtPath<MetaConfig>("Assets/Data/Meta/MetaConfig.asset");
            Assert.IsNotNull(cfg, "MetaConfig");
            Assert.IsNotNull(cfg.skins);
            Assert.AreEqual(table.skins.Length, cfg.skins.Length, "every row has its SkinDef (run Gladiator/Setup/Gladiators/Create Skin Data; a missing FBX skips one)");
            var unlocks = new HashSet<UnlockDef>(cfg.unlocks);
            var palette = Palette(File.ReadAllText(PalettePath));
            Assert.Greater(palette.Count, 100, "palette.json's swatches");
            var perGladiator = new Dictionary<string, int>();
            int marketTotal = 0;
            for (int i = 0; i < table.skins.Length; i++)
            {
                var row = table.skins[i];
                var s = cfg.skins[i];
                Assert.IsNotNull(s, "skin " + i);
                Assert.AreEqual(row.id, s.id, "the table's order");
                Assert.IsTrue(SkinDef.IdBelongsTo(s.id, row.gladiator), s.id + ": '<gladiator id>_<name>'");
                Assert.IsNotNull(s.gladiator, s.id);
                Assert.AreEqual(row.gladiator, s.gladiator.id, s.id);
                Assert.AreEqual(row.name, s.displayName, s.id);
                string key = row.@base.StartsWith("CH_", StringComparison.Ordinal) ? row.@base.Substring(3) : row.@base;
                Assert.AreEqual("Assets/Data/Skins/SK_" + key + row.suffix + ".asset", AssetDatabase.GetAssetPath(s), s.id);
                Assert.IsNotNull(s.model, s.id + " model");
                // the A1 skin when there is one (ModelStyle: the A1 characters are the game's, the classic ones kept)
                string classicModel = "Assets/Art/Models/Characters/Skins/" + row.@base + row.suffix + ".fbx";
                string a1Model = "Assets/Art/Models/A1/Characters/Skins/" + row.@base + row.suffix + ".fbx";
                Assert.AreEqual(System.IO.File.Exists(a1Model) ? a1Model : classicModel, AssetDatabase.GetAssetPath(s.model), s.id);
                Assert.AreEqual(3, s.chips.Length, s.id + " chips");
                Assert.AreEqual(3, row.chips.Length, s.id + ": three chip swatches in the table");
                for (int c = 0; c < 3; c++)
                {
                    Assert.IsTrue(palette.TryGetValue(row.chips[c], out var sw), s.id + ": chip '" + row.chips[c] + "' is a palette swatch");
                    Assert.IsFalse(sw.family == "hazard" || sw.family == "enemy_accent" || row.chips[c].StartsWith("glow_", StringComparison.Ordinal),
                        s.id + ": '" + row.chips[c] + "' (orange-red and glows are never a skin's main colours, spec §4.2)");
                    Assert.Less(Vector4.Distance(sw.color, s.chips[c]), 2e-3f, s.id + " chip " + c + " is " + row.chips[c] + "'s colour");
                }
                Assert.IsNotNull(s.portrait, s.id + " portrait (Gladiator/Setup/UI/Bake Portraits)");
                string suffix = row.suffix.Replace("_", "").ToLowerInvariant();
                Assert.AreEqual("Assets/Art/UI/Portraits/PT_" + row.gladiator + "_" + suffix + ".png", AssetDatabase.GetAssetPath(s.portrait), s.id);
                AssertSameParts(s);

                var u = s.unlock;
                Assert.IsNotNull(u, s.id + ": every skin of spec §4.4 is earned or bought");
                Assert.IsTrue(unlocks.Contains(u), s.id + "'s unlock is in MetaConfig.unlocks");
                Assert.AreEqual(MetaSetupDir + "UL_Skin_" + key + "_" + row.suffix.TrimStart('_') + ".asset", AssetDatabase.GetAssetPath(u), s.id);
                Assert.IsFalse(string.IsNullOrEmpty(u.hint) || string.IsNullOrEmpty(u.description), s.id + " has its texts");
                if (row.price > 0)
                {
                    Assert.AreEqual(UnlockCategory.Market, u.category, s.id);
                    Assert.AreEqual(row.price, u.renownCost, s.id);
                    marketTotal += row.price;
                }
                else
                {
                    Assert.IsTrue(row.champion, s.id + ": a price or champion");
                    Assert.AreEqual(UnlockCategory.Challenge, u.category, s.id);
                    Assert.AreEqual(1, u.conditions.Length, s.id);
                    Assert.AreEqual(UnlockConditionKind.WinWithGladiator, u.conditions[0].kind, s.id);
                    Assert.AreEqual(row.gladiator, u.conditions[0].id, s.id);
                    Assert.AreEqual(3f, u.conditions[0].amount, 1e-5f, s.id);
                }
                perGladiator.TryGetValue(row.gladiator, out int n);
                perGladiator[row.gladiator] = n + 1;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:GladiatorDef", new[] { "Assets/Data/Gladiators" }))
            {
                var g = AssetDatabase.LoadAssetAtPath<GladiatorDef>(AssetDatabase.GUIDToAssetPath(guid));
                perGladiator.TryGetValue(g.id, out int n);
                Assert.AreEqual(2, n, g.id + ": two skins each (one Market, one champion)");
            }
            Assert.AreEqual(3200, marketTotal, "spec §4.4: the Market's skins cost 3,200 Renown in all");
        }

        const string MetaSetupDir = "Assets/Data/Meta/";
        const string PalettePath = "Assets/Art/Textures/Palette/palette.json";

        static readonly Regex RowRx = new Regex("\\{\\s*\"family\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"swatches\"\\s*:\\s*\\[(.*?)\\]\\s*\\}",
            RegexOptions.Singleline);
        static readonly Regex SwatchRx = new Regex("\\[\\s*\"([A-Za-z0-9_]+)\"\\s*,\\s*\"(#[0-9A-Fa-f]{6})\"");

        /// <summary>palette.json's swatches by name: (family, colour).</summary>
        static Dictionary<string, (string family, Color color)> Palette(string json)
        {
            var map = new Dictionary<string, (string, Color)>();
            foreach (Match row in RowRx.Matches(json))
                foreach (Match sw in SwatchRx.Matches(row.Groups[2].Value))
                    if (ColorUtility.TryParseHtmlString(sw.Groups[2].Value, out var c)) map[sw.Groups[1].Value] = (row.Groups[1].Value, c);
            return map;
        }

        /// <summary>The model inside the player prefab (the Nobody's: A1 or classic, whichever the game uses).</summary>
        static GameObject PlayerModel()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/P_Player.prefab");
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                if (t.name == "CH_Nobody" && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    return PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Characters/CH_Nobody.fbx");
        }

        /// <summary>A skin is a palette remap: the same part names and the same geometry (index counts) as its gladiator's
        /// own model (the Nobody's is the player prefab's CH_Nobody), so the animator drives it unchanged.</summary>
        static void AssertSameParts(SkinDef s)
        {
            var baseModel = s.gladiator.model != null ? s.gladiator.model
                : PlayerModel();
            Assert.IsNotNull(baseModel, s.id + ": the gladiator's model");
            CollectionAssert.AreEquivalent(Parts(baseModel), Parts(s.model), s.id + ": the same parts as " + baseModel.name);
            Assert.AreEqual(Indices(baseModel), Indices(s.model), s.id + ": the same geometry as " + baseModel.name);
        }

        static List<string> Parts(GameObject model)
        {
            var names = new List<string>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t != model.transform) names.Add(t.name);
            return names;
        }

        static long Indices(GameObject model)
        {
            long n = 0;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = f.sharedMesh;
                if (m == null) continue;
                for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i);
            }
            return n;
        }
    }
}
