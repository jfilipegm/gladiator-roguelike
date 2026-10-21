using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.11 (docs/GLADIATORS.md): the Golden Idol. He spawns with his model, stats, Coin Toss and traits; Midas
    /// Body turns gold carried into damage (capped); a streak announce multiplies GoldGain and it wears off; a slide with
    /// 40+ gold pays it and sprays 16 coins, with less it does nothing; the Coin Toss throws coins; 10,000 lifetime gold
    /// unlocks him.
    /// </summary>
    public class GoldenIdolTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Idol => Load<GladiatorDef>("Assets/Data/Gladiators/GL_GoldenIdol.asset");
        static EnemyDef Brute => Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");

        EnemyManager _em;
        WeaponSystem _weapons;
        PlayerStats _stats;

        IEnumerator Boot(string scene, GladiatorDef as_)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = as_;
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            _em = EnemyManager.Instance;
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            _stats = Player.Main.GetComponent<PlayerStats>();
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator HeSpawns_WithHisModel_Stats_CoinToss_AndTraits()
        {
            yield return Boot(TestArena, Idol);
            var p = Player.Main.transform;
            Assert.IsNotNull(FindDeep(p, "SunCrown"), "the sun-crown");
            Assert.IsNotNull(FindDeep(p, "Sceptre"));
            Assert.AreEqual(100f, Player.Main.GetComponent<PlayerHealth>().MaxHp, 0.01f);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual("coin_toss", _weapons.Weapons[0].Def.id);
            Assert.IsNotNull(Player.Main.GetComponent<MidasBody>());
            Assert.IsNotNull(Player.Main.GetComponent<GoldenStreak>());
            Assert.IsNotNull(Player.Main.GetComponent<MakeItRain>());
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MidasBody_GoldCarriedIsDamage_UpToTheCap()
        {
            yield return Boot(TestArena, Idol);
            _weapons.RemoveAll();
            var m = Player.Main.GetComponent<MidasBody>();
            var run = RunState.Current;
            run.SpendGold(run.Gold);
            m.Refresh();
            float baseDamage = _stats.Get(StatId.Damage);
            Assert.AreEqual(0f, m.Bonus);
            run.AddGold(250);
            m.Refresh();
            Assert.AreEqual(0.25f, m.Bonus, 1e-4f, "250 gold: +25 %");
            Assert.AreEqual(baseDamage * 1.25f, _stats.Get(StatId.Damage), 1e-3f);
            run.AddGold(1750);
            m.Refresh();
            Assert.AreEqual(1f, m.Bonus, 1e-4f, "2,000 gold: capped at +100 %");
            run.SpendGold(1990);
            m.Refresh();
            Assert.AreEqual(0.01f, m.Bonus, 1e-4f, "spent: the power goes with it");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator GoldenStreak_AStreakMultipliesGold_ThenWearsOff()
        {
            yield return Boot(TestArena, Idol);
            var s = Player.Main.GetComponent<GoldenStreak>();
            float baseGain = _stats.Get(StatId.GoldGain);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Line, Text = "WHAT A FIGHT!" });
            Assert.AreEqual(baseGain, _stats.Get(StatId.GoldGain), 1e-4f, "only streaks pay");
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Streak, Tier = 1, Text = "MASSACRE!" });
            Assert.AreEqual(baseGain * s.trait.MultiplierFor(1), _stats.Get(StatId.GoldGain), 1e-4f, "MASSACRE!: ×2 gold");
            yield return new WaitForSeconds(s.trait.seconds + 0.3f);
            Assert.AreEqual(baseGain, _stats.Get(StatId.GoldGain), 1e-4f, "worn off");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MakeItRain_ASlideWithGold_PaysAndSpraysCoins_WithoutGoldNothing()
        {
            yield return Boot(TestArena, Idol);
            _weapons.RemoveAll();
            var rain = Player.Main.GetComponent<MakeItRain>();
            var run = RunState.Current;
            run.SpendGold(run.Gold);
            run.AddGold(30);
            Assert.IsFalse(rain.Perform(), "30 gold isn't enough");
            Assert.AreEqual(30, run.Gold, "and nothing is spent");

            int brute = _em.Spawn(Brute, Player.Main.transform.position + new Vector3(3f, 0f, 0f));
            yield return null;
            run.AddGold(70);
            int before = PlayerProjectiles.Instance.Count;
            Assert.IsTrue(rain.Perform());
            Assert.AreEqual(100 - rain.trait.goldCost, run.Gold, "paid");
            Assert.AreEqual(before + rain.trait.coins, PlayerProjectiles.Instance.Count, "16 coins in the air");
            yield return new WaitForSeconds(0.4f);
            Assert.Less(_em.GetHp(brute), _em.GetMaxHp(brute), "and they hurt");

            Assert.IsFalse(rain.Perform(), "cooling down");
            rain.Recharge();
            GameEvents.OnSlide.Raise(new SlideEvent { Position = Player.Main.transform.position, Velocity = Vector3.forward * 8f });
            Assert.AreEqual(2, rain.Showers, "a slide makes it rain");
            Assert.AreEqual(100 - 2 * rain.trait.goldCost, run.Gold);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CoinToss_ThrowsCoinsAtTheNearest()
        {
            yield return Boot(TestArena, Idol);
            Vector3 p = Player.Main.transform.position;
            int a = _em.Spawn(Brute, p + new Vector3(6f, 0f, 0f));
            int b = _em.Spawn(Brute, p + new Vector3(0f, 0f, 7f));
            float end = Time.time + 4f;
            while (Time.time < end && (_em.GetHp(a) >= _em.GetMaxHp(a) || _em.GetHp(b) >= _em.GetMaxHp(b))) yield return null;
            Assert.Greater(_weapons.Swings, 0, "it threw");
            Assert.Less(_em.GetHp(a), _em.GetMaxHp(a), "a coin for each of the nearest");
            Assert.Less(_em.GetHp(b), _em.GetMaxHp(b));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TenThousandLifetimeGold_UnlocksHim()
        {
            SaveSandbox.Wipe();
            yield return Boot(SlavePit, null);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Idol), "locked on a fresh save");
            MetaProgress.Data.lifetimeGold = 9990;
            RunState.Current.AddGold(25);
            RunState.Current.End(RunOutcome.Died);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards);
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "golden_idol";
            Assert.IsTrue(got, "10,000 gold over all runs");
            Assert.IsTrue(SaveSystem.Load(SaveSandbox.TestPath).IsUnlocked("golden_idol"), "on disk");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Idol));
        }
    }
}
