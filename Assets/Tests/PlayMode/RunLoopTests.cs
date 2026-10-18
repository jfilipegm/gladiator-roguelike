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
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = false;
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
            int sawPickup = 0, pickedUp = 0;
            var log = new System.Text.StringBuilder();
            Gladiator.Core.EventHandler<PickupEvent> onPick = (in PickupEvent e) => { pickedUp++; log.Append($" [pick f{Time.frameCount} amt={e.Amount}]"); };
            Gladiator.Core.EventHandler<KillEvent> onKill = (in KillEvent e) => log.Append($" [kill f{Time.frameCount} xp={e.Xp}]");
            GameEvents.OnPickup.Subscribe(onPick);
            GameEvents.OnKill.Subscribe(onKill);
            float end = Time.time + 4f;
            int frames = 0;
            while (Time.time < end)
            {
                if (frames++ < 40) log.Append($" f{Time.frameCount}:a{pickups.ActiveCount}k{run.Kills}");
                if (pickups.ActiveCount > 0) sawPickup++;
                if (run.Kills > kills0 && pickups.ActiveCount == 0 && sawPickup > 0) break;
                yield return null;
            }
            GameEvents.OnPickup.Unsubscribe(onPick);
            GameEvents.OnKill.Unsubscribe(onKill);
            Debug.Log("[GemTest] sameInstance=" + ReferenceEquals(pickups, PickupSystem.Instance) + log);
            Assert.AreEqual(kills0 + 1, run.Kills, "the convict should die");
            Assert.Greater(sawPickup + pickedUp, 0, "a gem should have dropped (and/or been picked up)");
            Assert.AreEqual(0, pickups.ActiveCount, "the gem should be vacuumed up (drops within magnet range)");
            Assert.Greater(run.Xp + (run.Level - 1) * 1000f, xp0, "XP should increase");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LevelUp_Pauses_AppliesChoice_Resumes()
        {
            yield return Boot();
            var run = RunState.Current;
            var lvl = LevelUpController.Instance;
            var weapons = Player.Main.GetComponent<WeaponSystem>();
            run.AddXp(run.XpToNext);
            Assert.AreEqual(2, run.Level);
            float end = Time.realtimeSinceStartup + 2f;
            while (!lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(lvl.IsOpen, "level-up cards should open");
            Assert.AreEqual(0f, Time.timeScale, "game pauses while choosing");
            Assert.IsFalse(Player.Main.InputEnabled, "gameplay input is off while choosing");

            // pick whichever card is damage if offered, else the first
            int pick = 0;
            for (int i = 0; i < lvl.ChoiceCount; i++) if (lvl.Choices[i].Boon == StubBoon.Damage) pick = i;
            var boon = lvl.Choices[pick].Boon;
            float dmg0 = weapons.damageMultiplier;
            lvl.Choose(pick);
            yield return null;
            Assert.IsFalse(lvl.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "game resumes");
            Assert.IsTrue(Player.Main.InputEnabled);
            Assert.AreEqual(1, lvl.TimesChosen);
            if (boon == StubBoon.Damage) Assert.Greater(weapons.damageMultiplier, dmg0);
        }
    }
}
