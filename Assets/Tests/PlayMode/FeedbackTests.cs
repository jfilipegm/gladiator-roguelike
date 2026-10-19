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
    /// Phase 4: placeholder sound (pooled voices with repeat limits and distance fade), the crowd announcer's kill-streak
    /// ladder, and the options reaching the mixer, camera and screen shake. TestArena with the Director off.
    /// </summary>
    public class FeedbackTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GameOptions.Reload();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            GameOptions.Reload(); // the sandbox file was never written: back to defaults
            GameOptions.Apply();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Sfx_AHundredHitsInOneFrame_PlayOnce_FarAwayIsSilent()
        {
            yield return Boot();
            var sfx = SfxPlayer.Instance;
            Assert.IsNotNull(sfx, "every arena has the sound player");
            Assert.IsNotNull(sfx.bank, "with its bank");
            Assert.IsNotNull(sfx.bank.mixer, "and the mixer");
            yield return new WaitForSecondsRealtime(0.2f);
            Vector3 at = Player.Main.transform.position + Vector3.forward * 2f;
            int before = sfx.Played;
            for (int k = 0; k < 100; k++) GameEvents.OnHit.Raise(new HitEvent { Position = at, Damage = 1f, Flags = DamageFlags.Melee });
            Assert.AreEqual(before + 1, sfx.Played, "repeat limit: one hit sound per burst, not a hundred");
            yield return new WaitForSecondsRealtime(0.2f);
            before = sfx.Played;
            GameEvents.OnHit.Raise(new HitEvent { Position = at + Vector3.forward * 100f, Damage = 1f, Flags = DamageFlags.Melee });
            Assert.AreEqual(before, sfx.Played, "100 m away is out of earshot");
            GameEvents.OnHit.Raise(new HitEvent { Position = at, Damage = 1f, Flags = DamageFlags.Melee | DamageFlags.Status });
            Assert.AreEqual(before, sfx.Played, "status ticks are silent");
            SfxPlayer.Play2D(SfxId.LevelUp);
            Assert.AreEqual(before + 1, sfx.Played, "UI sounds ignore distance");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Announcer_KillStreak_ClimbsTheLadder_NeverRepeats()
        {
            yield return Boot();
            var announcer = Object.FindAnyObjectByType<CrowdAnnouncer>();
            Assert.IsNotNull(announcer);
            Assert.IsNotNull(announcer.config);
            var cfg = Object.Instantiate(announcer.config);
            cfg.minGap = 0.1f;
            announcer.config = cfg;
            string said = null;
            EventHandler<AnnounceEvent> listen = (in AnnounceEvent e) => said = e.Text;
            GameEvents.OnAnnounce.Subscribe(listen);
            try
            {
                for (int k = 0; k < cfg.streaks[0].kills; k++) GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero });
                yield return new WaitForSeconds(0.15f);
                Assert.AreEqual(cfg.streaks[0].text, said, "first rung");
                said = null;
                yield return new WaitForSeconds(0.3f);
                Assert.IsNull(said, "the same streak isn't announced twice");
                for (int k = 0; k < cfg.streaks[1].kills - cfg.streaks[0].kills; k++) GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero });
                yield return new WaitForSeconds(0.2f);
                Assert.AreEqual(cfg.streaks[1].text, said, "a bigger streak climbs a rung");
                if (CrowdExcitement.Instance != null) // the Slave Pit has a crowd; the test arena doesn't
                    Assert.Greater(CrowdExcitement.Instance.Value, cfg.streaks[1].crowd - 0.05f, "and the crowd answers");
            }
            finally
            {
                GameEvents.OnAnnounce.Unsubscribe(listen);
                Object.Destroy(cfg);
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Options_ReachTheMixer_Camera_AndShake()
        {
            yield return Boot();
            var o = GameOptions.Data;
            o.sfx = 0.5f;
            o.fov = 80f;
            o.screenShake = 0f;
            GameOptions.Apply();
            Assert.IsTrue(SfxPlayer.Instance.bank.mixer.GetFloat("SfxVolume", out float db));
            Assert.AreEqual(GameOptions.Db(0.5f), db, 0.01f, "effects volume → mixer");
            Assert.AreEqual(0f, ScreenShake.UserScale, "shake slider → screen shake");
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(80f, Camera.main.fieldOfView, 1f, "FOV option → camera at rest");
        }
    }
}
