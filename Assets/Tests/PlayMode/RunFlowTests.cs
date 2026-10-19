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
