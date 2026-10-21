using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.6 secrets (docs/UNLOCKS.md) in their arenas: the Golden Rat comes out of a hole, outruns a run, is caught
    /// by touch (coins, the flag at once, the Sling unlocks when the run ends) or dives away in time; the Bookmaker takes
    /// a stake in his ring when you press E (P1: like the chests, with a prompt over him), the HUD counts the wager down,
    /// a win pays and saves the flag, a loss keeps the stake, and no gold means no bet.
    /// </summary>
    public class SecretPlayTests
    {
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        const string ProvincialPit = "Assets/Scenes/Arenas/ProvincialPit.unity";

        EnemyManager _em;
        PlayerMotor _motor;
        RunState _run;
        ScriptedInput _input;

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            RunObjective.Clear();
        }

        IEnumerator Boot(string scene)
        {
            SaveSandbox.Wipe();
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = _input = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().enabled = false; // nothing dies by accident: kills are the test's
            _em = EnemyManager.Instance;
            _em.KillAll();
            _motor = Player.Main.GetComponent<PlayerMotor>();
            _run = RunState.Current;
            yield return new WaitForSeconds(0.2f);
        }

        Vector3 Ground(Vector3 p)
        {
            p.y = (_em.Height != null ? _em.Height.Sample(p.x, p.z) : 0f) + 0.1f;
            return p;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator GoldenRat_OutrunsARun_IsCaughtByTouch_AndTheSlingUnlocksAtTheEnd()
        {
            yield return Boot(SlavePit);
            var rat = GoldenRat.Instance;
            Assert.IsNotNull(rat, "the Slave Pit hides a golden rat");
            Assert.IsFalse(MetaProgress.Data.HasFlag(rat.flag));
            rat.Appear(0);
            Assert.AreEqual(GoldenRat.Phase.Running, rat.State);
            Vector3 toCenter = _em.arenaCenter - rat.Position;
            toCenter.y = 0f;
            _motor.Teleport(Ground(rat.Position + toCenter.normalized * 4f)); // between it and the middle: it bolts
            yield return null;
            float run = 0f, t = 0f;
            Vector3 last = rat.Position;
            while (t < 0.6f) { yield return null; t += Time.deltaTime; run += Vector3.Distance(last, rat.Position); last = rat.Position; }
            Assert.Greater(run / t, 7.5f, "faster than the player can run");

            int gold = PickupSystem.Instance.ActiveCount;
            _motor.Teleport(Ground(rat.Position));
            yield return null;
            yield return null;
            Assert.AreEqual(GoldenRat.Phase.Caught, rat.State, "caught by touch");
            Assert.Greater(PickupSystem.Instance.ActiveCount, gold, "it bursts into coins");
            Assert.IsTrue(MetaProgress.Data.HasFlag(rat.flag), "the discovery is saved at once");

            _run.End(RunOutcome.Died);
            yield return null;
            var rewards = MetaProgress.Last;
            Assert.IsNotNull(rewards, "the run was committed");
            Assert.IsTrue(rewards.NewUnlocks.Exists(u => u.id == "sling"), "the Sling unlocks");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator GoldenRat_DivesBackIntoAHole_WhenNotCaughtInTime()
        {
            yield return Boot(SlavePit);
            var rat = GoldenRat.Instance;
            rat.life = 0.5f;
            rat.Appear(1);
            _motor.Teleport(Ground(_em.arenaCenter));
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(GoldenRat.Phase.Escaped, rat.State);
            Assert.IsFalse(MetaProgress.Data.HasFlag(rat.flag), "nothing found");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bookmaker_TakesTheStake_TheHudCountsDown_AWinPaysAndSavesTheFlag()
        {
            yield return Boot(ProvincialPit);
            var bm = Bookmaker.Instance;
            Assert.IsNotNull(bm, "the Provincial Pit has a bookmaker");
            bm.Place(0);
            _run.AddGold(100);
            Assert.AreEqual(100, _run.Gold);
            _motor.Teleport(Ground(bm.Position + new Vector3(0.3f, 0f, 0.3f)));
            yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(Bookmaker.Phase.Waiting, bm.State, "P1: standing in his ring alone takes nothing");
            Assert.IsTrue(bm.InReach, "in his ring");
            var hud = Object.FindAnyObjectByType<Hud>();
            StringAssert.Contains("WAGER " + bm.stake + " GOLD", hud.BookmakerTagText, "the prompt over him");
            StringAssert.Contains("[E]", hud.PromptText, "and the bottom prompt, like a chest's");
            _input.Next.InteractPressed = true;
            yield return null;
            yield return null;
            Assert.AreEqual(Bookmaker.Phase.Wager, bm.State, "E in his ring takes the bet");
            Assert.AreEqual(100 - bm.stake, _run.Gold, "the stake");
            Assert.AreEqual(bm.minKills, bm.Target, "no kills lately: the floor");
            Assert.IsTrue(RunObjective.Active);
            yield return null;
            StringAssert.Contains("WAGER", hud.ObjectiveText);
            StringAssert.Contains("/ " + bm.Target, hud.ObjectiveText);

            var convict = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            for (int k = 0; k < bm.Target; k++) _em.Spawn(convict, Ground(_em.arenaCenter + new Vector3((k % 10) - 5f, 0f, (k / 10) - 3f)));
            _em.KillAll();
            yield return null;
            yield return null;
            Assert.AreEqual(Bookmaker.Phase.Paid, bm.State, "the pace was beaten");
            Assert.AreEqual(100 - bm.stake + bm.payout, _run.Gold, "he pays up");
            Assert.IsTrue(MetaProgress.Data.HasFlag(bm.flag));
            Assert.IsFalse(RunObjective.Active, "the HUD line goes away");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bookmaker_TheHouseKeepsTheStake_AndNoGoldMeansNoBet()
        {
            yield return Boot(ProvincialPit);
            var bm = Bookmaker.Instance;
            bm.Place(1);
            Assert.AreEqual(0, _run.Gold);
            _motor.Teleport(Ground(bm.Position + new Vector3(0.3f, 0f, 0.3f)));
            yield return new WaitForSeconds(0.4f);
            var hud = Object.FindAnyObjectByType<Hud>();
            StringAssert.Contains("NEED " + bm.stake + " GOLD", hud.BookmakerTagText, "P1: short of the stake, the prompt says so");
            _input.Next.InteractPressed = true;
            yield return null;
            yield return null;
            Assert.AreEqual(Bookmaker.Phase.Waiting, bm.State, "no gold, no wager (E does nothing but his line)");
            Assert.IsFalse(bm.TryBet(), "no gold, no wager");
            Assert.AreEqual(Bookmaker.Phase.Waiting, bm.State);

            _run.AddGold(50);
            bm.seconds = 0.5f;
            Assert.IsTrue(bm.TryBet());
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(Bookmaker.Phase.Lost, bm.State, "nothing killed in time");
            Assert.AreEqual(50 - bm.stake, _run.Gold, "the house keeps the stake");
            Assert.IsFalse(MetaProgress.Data.HasFlag(bm.flag));
            Assert.IsFalse(RunObjective.Active);
        }
    }
}
