using System.Collections;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>M1.3: kill → gem → magnet → XP; level-up pauses, applies a choice and resumes.</summary>
    public class RunLoopTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        IEnumerator Boot()
        {
            DevSettings.ResetAll(); // static test switches must not leak between tests
            Gladiator.Core.TimeController.GameSpeed = 1f;
            Gladiator.Core.TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false; // tests place their own enemies
            Player.Main.OverrideSource = new ScriptedInput();
            EnemyManager.Instance.KillAll();
            yield return new WaitForSeconds(0.3f); // death pops finish
            PickupSystem.Instance.ClearAll();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Kill_DropsGem_MagnetCollects_AddsXp()
        {
            yield return Boot();
            var run = RunState.Current;
            var pickups = PickupSystem.Instance;
            var em = EnemyManager.Instance;
            var def = Object.FindAnyObjectByType<DebugEnemySpawner>().enemy;
            float xp0 = run.Xp + (run.Level - 1) * 1000f;
            int kills0 = run.Kills;
            em.Spawn(def, Player.Main.transform.position + new Vector3(0f, 0f, 1.6f));
            int sawPickup = 0;
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                if (pickups.ActiveCount > 0) sawPickup++;
                if (run.Kills > kills0 && pickups.ActiveCount == 0 && sawPickup > 0) break;
                yield return null;
            }
            Assert.AreEqual(kills0 + 1, run.Kills, "the convict should die");
            Assert.Greater(sawPickup, 0, "a gem should drop and stay visible for its pop before the magnet takes it");
            Assert.AreEqual(0, pickups.ActiveCount, "the gem should be vacuumed up (drops within magnet range)");
            Assert.Greater(run.Xp + (run.Level - 1) * 1000f, xp0, "XP should increase");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LevelUp_Pauses_AppliesChoice_Resumes()
        {
            yield return Boot();
            var run = RunState.Current;
            var lvl = LevelUpController.Instance;
            run.AddXp(run.XpToNext);
            Assert.AreEqual(2, run.Level);
            float end = Time.realtimeSinceStartup + 2f;
            while (!lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(lvl.IsOpen, "level-up cards should open");
            Assert.AreEqual(0f, Time.timeScale, "game pauses while choosing");
            Assert.IsFalse(Player.Main.InputEnabled, "gameplay input is off while choosing");

            var build = Player.Main.GetComponent<PlayerBuild>();
            var picked = lvl.Offers[0].Def;
            int version0 = build.Stats.Block.Version;
            lvl.Choose(0);
            yield return null;
            Assert.IsFalse(lvl.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "game resumes");
            Assert.IsTrue(Player.Main.InputEnabled);
            Assert.AreEqual(1, lvl.TimesChosen);
            Assert.AreEqual(1, build.State.LevelOf(picked), "the picked upgrade is owned");
            if (picked.kind == UpgradeKind.Weapon)
                Assert.IsNotNull(Player.Main.GetComponent<WeaponSystem>().Find(picked.weapon), "a weapon card equips/levels its weapon");
            else if (picked.perLevel != null && picked.perLevel.Length > 0)
                Assert.Greater(build.Stats.Block.Version, version0, "and changed the player's stats");
            else
                Assert.Greater(picked.effects.Length, 0, "an effect-only Boon (BoonEffects reads it from the build)");
        }
    }
}
