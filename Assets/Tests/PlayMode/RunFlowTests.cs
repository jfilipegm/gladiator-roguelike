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
    /// M3.5: main menu → gladiator select → arena (with its intro holding the clock) → end screen → retry / main menu.
    /// </summary>
    public class RunFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
        }

        static IEnumerator WaitScene(string name, float seconds = 10f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().name != name) yield return null;
            yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
        }

        static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, null);

        IEnumerator MenuToArena()
        {
            DevSettings.ResetAll();
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            Assert.IsNotNull(op, "main menu not in the build settings");
            while (!op.isDone) yield return null;
            yield return null;
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsNotNull(menu);
            Invoke(menu, "OpenSelect");
            yield return null;
            Invoke(menu, "StartRun");
            if (menu.AsksForArena) // an earlier test beat Gorm in the sandbox save: the arena screen asks
            {
                Assert.IsTrue(menu.PickArena(menu.run));
                Invoke(menu, "Launch");
            }
            yield return WaitScene("SlavePit");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Menu_Select_Fight_ArenaIntroHoldsTheClock()
        {
            yield return MenuToArena();
            Assert.IsNotNull(GameSession.Gladiator, "the picked gladiator travels with the run");
            Assert.AreSame(GameSession.Gladiator, Player.Main.GetComponent<PlayerStats>().gladiator);
            var intro = RunIntro.Instance;
            Assert.IsNotNull(intro);
            Assert.AreEqual(RunIntro.Stage.Holding, intro.Current);
            Assert.IsTrue(TimeController.IsPaused, "paused behind the arena's name");
            Assert.AreEqual(0f, RunState.Current.Elapsed, 1e-4f, "the run clock waits");
            float end = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < end && !intro.Done) yield return null;
            Assert.IsTrue(intro.Done);
            Assert.IsFalse(TimeController.IsPaused, "FIGHT!");
            Assert.IsTrue(Director.Instance.Prewarmed, "pools filled during the intro");
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(RunState.Current.Elapsed, 0.2f, "the clock runs");
        }

        /// <summary>M3.6: the one unlock end to end. A fresh save keeps Gorm's Flail out of the cards; beating Gorm
        /// records the run (Renown, boss kill) and unlocks it; the next run can offer it.</summary>
        [UnityTest, Timeout(90000)]
        public IEnumerator BeatingGorm_UnlocksTheFlail_ForTheNextRun()
        {
            SaveSandbox.Wipe();
            yield return MenuToArena();
            float end = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < end && !RunIntro.Instance.Done) yield return null;
            var build = Player.Main.GetComponent<PlayerBuild>();
            UpgradeDef card = null;
            foreach (var u in build.catalog.upgrades) if (u != null && u.id == "weapon_gorms_flail") card = u;
            Assert.IsNotNull(card, "the flail card is in the catalog");
            Assert.IsTrue(build.State.IsLocked(card), "locked on a fresh save");
            Assert.IsFalse(UpgradeRoller.IsEligible(card, build.State, build.config));

            RunState.Current.MarkBossDefeated("gorm");
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards, "the run was recorded");
            Assert.Greater(rewards.Renown, 0);
            Assert.AreEqual(2, rewards.NewUnlocks.Count, "the flail, and (Phase 5.4) the Provincial Pit");
            bool flail = false, pit = false;
            foreach (var u in rewards.NewUnlocks) { flail |= u.id == "gorms_flail"; pit |= u.id == "provincial_pit"; }
            Assert.IsTrue(flail, "Gorm's Flail joins the cards");
            Assert.IsTrue(pit, "word reaches the provinces");
            Assert.IsTrue(System.IO.File.Exists(SaveSandbox.TestPath), "saved to disk (the sandbox file)");
            var onDisk = SaveSystem.Load(SaveSandbox.TestPath);
            Assert.IsTrue(onDisk.IsUnlocked("gorms_flail"));
            Assert.AreEqual(1, SaveData.Count(onDisk.bossKills, "gorm"));
            Assert.AreEqual(rewards.Renown, onDisk.renown);

            RunEndController.Instance.Retry();
            yield return WaitScene("SlavePit");
            build = Player.Main.GetComponent<PlayerBuild>();
            Assert.IsFalse(build.State.IsLocked(card), "the next run can offer the flail");
            Assert.IsTrue(UpgradeRoller.IsEligible(card, build.State, build.config));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator EndScreen_Retry_ThenMainMenu()
        {
            yield return MenuToArena();
            var glad = GameSession.Gladiator;
            float end = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < end && !RunIntro.Instance.Done) yield return null;
            Player.Main.GetComponent<PlayerHealth>().TakeDamage(new DamageInfo { Amount = 100000f, Flags = DamageFlags.Melee });
            end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end && !RunState.Current.Ended) yield return null;
            Assert.IsTrue(RunState.Current.Ended, "dying ends the run");
            RunEndController.Instance.Retry();
            yield return WaitScene("SlavePit");
            Assert.AreSame(glad, GameSession.Gladiator, "retry keeps the gladiator");
            Assert.IsFalse(RunState.Current.Ended, "a fresh run");
            RunEndController.Instance.Quit();
            yield return WaitScene("MainMenu");
        }
    }
}
