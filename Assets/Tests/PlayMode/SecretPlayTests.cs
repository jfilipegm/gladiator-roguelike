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
    /// by touch (coins, the flag at once, the Sling unlocks when the run ends) or dives away in time. The Bookmaker (W1):
    /// E in his ring opens his book (the run pauses; WALK AWAY costs nothing), a pick takes the stake and the HUD counts
    /// the wager down, a win pays (the flag only at tier II or higher), a loss keeps the stake, a card you can't pay is
    /// refused, tier III loses on a hit, tier IV sends one elite and pays on its kill, and after a cooldown he bets again.
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

        // ------------------------------------------------------------------ the Bookmaker's book (W1)

        /// <summary>The scene's book menu, or one made now (a Provincial Pit built before W1 has none).</summary>
        static WagerPanel Panel()
        {
            var p = WagerPanel.Instance != null ? WagerPanel.Instance : Object.FindAnyObjectByType<WagerPanel>();
            return p != null ? p : new GameObject("WagerPanel").AddComponent<WagerPanel>();
        }

        IEnumerator AtHisBooth(Bookmaker bm, int spot, int gold)
        {
            bm.Place(spot);
            if (gold > 0) _run.AddGold(gold);
            _motor.Teleport(Ground(bm.Position + new Vector3(0.3f, 0f, 0.3f)));
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator KillConvicts(int n)
        {
            var convict = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            for (int k = 0; k < n; k++) _em.Spawn(convict, Ground(_em.arenaCenter + new Vector3((k % 10) - 5f, 0f, (k / 10) - 3f)));
            _em.KillAll();
            yield return null;
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bookmaker_EInHisRingOpensHisBook_ThePausedMenu_AndWalkingAwayCostsNothing()
        {
            yield return Boot(ProvincialPit);
            var bm = Bookmaker.Instance;
            Assert.IsNotNull(bm, "the Provincial Pit has a bookmaker");
            var panel = Panel();
            yield return AtHisBooth(bm, 0, 100);
            yield return new WaitForSeconds(1.1f);
            Assert.AreEqual(Bookmaker.Phase.Waiting, bm.State, "standing in his ring alone opens nothing");
            Assert.IsTrue(bm.InReach, "in his ring");
            var hud = Object.FindAnyObjectByType<Hud>();
            StringAssert.Contains("THE BOOK", hud.BookmakerTagText, "the prompt over him");
            Assert.AreEqual("[E] Open the Bookmaker's book", hud.PromptText, "and the bottom prompt, like a chest's");

            _input.Next.InteractPressed = true;
            yield return null;
            yield return null;
            Assert.IsTrue(bm.BookOpen, "E in his ring opens his book");
            Assert.IsTrue(TimeController.IsPaused, "the run pauses while it's open");
            Assert.IsFalse(Player.Main.InputEnabled, "and the player can't move or fight");
            Assert.AreEqual(100, _run.Gold, "opening the book costs nothing");
            Assert.AreEqual(1 + (bm.LuckOpenedTier ? 1 : 0), bm.OfferCount, "no kills yet: tier I (and Luck's one more, if it rolled)");
            Assert.AreEqual("", hud.BookmakerTagText, "his tag hides under the book");
            Assert.IsTrue(panel.Shown, "the book's menu shows");
            Assert.AreEqual(WagerPanel.TitleWord, panel.TitleText);
            Assert.AreEqual(WagerPanel.SubWord, panel.SubtitleText);
            Assert.AreEqual(bm.OfferCount, panel.CardCount, "one card per wager offered");
            Assert.AreEqual("I", panel.CardNumeral(0));
            Assert.AreEqual("KILL " + bm.Offer(0).Target, panel.CardChallenge(0));
            Assert.AreEqual(Bookmaker.TargetFor(bm.tiers[0], bm.RecentKills(), bm.maxKills), bm.Offer(0).Target,
                "N from your pace (paused: the same 30 s); at least the floor");
            Assert.GreaterOrEqual(bm.Offer(0).Target, bm.tiers[0].minKills);
            Assert.AreEqual("IN 30 SECONDS", panel.CardTime(0));
            Assert.AreEqual("STAKE 5", panel.CardStake(0));
            Assert.AreEqual("WINS 15", panel.CardWins(0));
            Assert.AreEqual("Bet 5 gold", panel.CardPlateLabel(0));
            Assert.AreEqual(WagerPanel.MoreLine, panel.MoreText, "fewer than four: more kills, bigger wagers");
            Assert.AreEqual(UiState.Focused, panel.CardPlate(0).State, "the first card is focused, its BET plate glows");

            yield return new WaitForSecondsRealtime(0.3f); // the input grace
            panel.Hover(WagerPanel.WalkAwayId);
            yield return null;
            Assert.AreEqual(UiState.Focused, panel.WalkAwayPlate.State, "the mouse over WALK AWAY lights it");
            panel.Click(WagerPanel.WalkAwayId);
            Assert.AreEqual(Bookmaker.Phase.Waiting, bm.State, "WALK AWAY closes the book");
            Assert.IsFalse(TimeController.IsPaused, "the run goes on");
            Assert.IsTrue(Player.Main.InputEnabled);
            Assert.AreEqual(100, _run.Gold, "walking away costs nothing");
            Assert.IsFalse(RunObjective.Active);
            yield return null;
            Assert.IsFalse(panel.Shown, "the menu goes away");
            Assert.AreEqual(1, bm.BookOpens);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Bookmaker_APickTakesTheStake_TierIPaysButKeepsHisSecret_ThenHeWaits_AndBetsAgain()
        {
            yield return Boot(ProvincialPit);
            var bm = Bookmaker.Instance;
            var panel = Panel();
            bm.cooldownSeconds = 1f;
            yield return AtHisBooth(bm, 0, 100);
            Assert.IsTrue(bm.OpenBook());
            int asked = Bookmaker.TargetFor(bm.tiers[0], bm.RecentKills(), bm.maxKills);
            yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            panel.Click(WagerPanel.PlateIdBase + 0); // the BET plate, as the mouse does
            Assert.AreEqual(Bookmaker.Phase.Wager, bm.State, "a pick takes the bet");
            Assert.IsFalse(TimeController.IsPaused, "and the run goes on");
            Assert.IsTrue(Player.Main.InputEnabled);
            Assert.AreEqual(95, _run.Gold, "the stake: 5");
            Assert.AreEqual(0, bm.ActiveTier);
            Assert.AreEqual(asked, bm.Target, "the card's N");
            Assert.IsTrue(RunObjective.Active);
            Assert.AreEqual("WAGER I", RunObjective.Label);
            yield return null;
            var hud = Object.FindAnyObjectByType<Hud>();
            StringAssert.Contains("WAGER I", hud.ObjectiveText);
            StringAssert.Contains("/ " + bm.Target, hud.ObjectiveText);

            yield return KillConvicts(bm.Target);
            Assert.AreEqual(Bookmaker.Outcome.Won, bm.LastOutcome, "the pace was kept");
            Assert.AreEqual(95 + bm.tiers[0].payout, _run.Gold, "he pays up");
            Assert.IsFalse(MetaProgress.Data.HasFlag(bm.flag), "tier I is too easy to be his secret");
            Assert.IsFalse(RunObjective.Active, "the HUD line goes away");
            Assert.AreEqual(Bookmaker.Phase.Cooldown, bm.State, "he waits before the next wager");
            yield return null;
            StringAssert.Contains("COME BACK LATER", hud.BookmakerTagText, "over him while he waits");
            StringAssert.DoesNotContain("book", hud.PromptText, "no bottom prompt while he waits");
            _input.Next.InteractPressed = true;
            yield return null;
            yield return null;
            Assert.IsFalse(bm.BookOpen, "come back later: E only gets his line");
            Assert.AreEqual(Bookmaker.Phase.Cooldown, bm.State);

            yield return new WaitForSeconds(1.3f);
            Assert.AreEqual(Bookmaker.Phase.Waiting, bm.State, "after the cooldown he calls again");
            yield return null;
            StringAssert.Contains("THE BOOK", hud.BookmakerTagText);
            int recent = bm.RecentKills();
            Assert.Greater(recent, 0, "the kills just made count toward the pace");
            Assert.IsTrue(bm.OpenBook(), "a second wager in the same run");
            Assert.AreEqual(2, bm.BookOpens);
            Assert.AreEqual(Bookmaker.TargetFor(bm.tiers[0], recent, bm.maxKills), bm.Offer(0).Target, "N from your pace at this opening");
            int gold = _run.Gold;
            Assert.IsTrue(bm.Pick(0));
            Assert.AreEqual(2, bm.WagersPlaced);
            Assert.AreEqual(gold - bm.tiers[0].stake, _run.Gold, "the second stake");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bookmaker_NoGoldStillOpensTheBook_TheCardIsRefused_AndTheHouseKeepsALostStake()
        {
            yield return Boot(ProvincialPit);
            var bm = Bookmaker.Instance;
            var panel = Panel();
            yield return AtHisBooth(bm, 1, 0);
            Assert.AreEqual(0, _run.Gold);
            _input.Next.InteractPressed = true;
            yield return null;
            yield return null;
            Assert.IsTrue(bm.BookOpen, "with no gold he still opens his book: the cards teach the prices");
            Assert.IsFalse(bm.CanAfford(0));
            Assert.AreEqual(UiState.Disabled, panel.CardPlate(0).State, "a card you can't pay is disabled");
            Assert.AreEqual("Need 5 gold", panel.CardPlateLabel(0));
            yield return new WaitForSecondsRealtime(0.3f);
            panel.Click(0);
            Assert.IsTrue(bm.BookOpen, "refused: the book stays open");
            Assert.IsFalse(bm.Pick(0), "no gold, no wager");
            Assert.AreEqual(0, _run.Gold);
            bm.CloseBook();
            Assert.IsFalse(TimeController.IsPaused);

            _run.AddGold(50);
            bm.tiers[0].seconds = 0.5f;
            Assert.IsTrue(bm.OpenBook());
            Assert.IsTrue(bm.Pick(0));
            Assert.AreEqual(45, _run.Gold, "the stake");
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(Bookmaker.Outcome.Lost, bm.LastOutcome, "nothing killed in time");
            Assert.AreEqual(45, _run.Gold, "the house keeps the stake");
            Assert.IsFalse(MetaProgress.Data.HasFlag(bm.flag));
            Assert.IsFalse(RunObjective.Active);
            Assert.AreEqual(Bookmaker.Phase.Cooldown, bm.State);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bookmaker_TierIII_NoHits_OneHitLosesIt()
        {
            yield return Boot(ProvincialPit);
            var bm = Bookmaker.Instance;
            Panel();
            for (int i = 1; i < bm.tiers.Length; i++) bm.tiers[i].unlockKills = 0; // every tier open (no 300 kills to make)
            yield return AtHisBooth(bm, 0, 100);
            Assert.IsTrue(bm.OpenBook());
            Assert.AreEqual(4, bm.OfferCount, "every tier open");
            Assert.AreEqual(WagerKind.NoHit, bm.Offer(2).Kind);
            Assert.IsTrue(bm.Pick(2));
            Assert.AreEqual(80, _run.Gold, "the stake: 20");
            Assert.AreEqual("WAGER III · NO HITS", RunObjective.Label);
            Assert.GreaterOrEqual(bm.Target, bm.tiers[2].minKills);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(Bookmaker.Phase.Wager, bm.State, "on while you're unhit");
            Assert.IsTrue(Player.Main.GetComponent<PlayerHealth>().TakeDamage(new DamageInfo { Amount = 1f }), "a hit lands");
            yield return null;
            yield return null;
            Assert.AreEqual(Bookmaker.Outcome.Lost, bm.LastOutcome, "one hit and the house wins");
            Assert.AreEqual(80, _run.Gold, "the house keeps the stake");
            Assert.IsFalse(RunObjective.Active);
            Assert.IsFalse(MetaProgress.Data.HasFlag(bm.flag));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bookmaker_TierIV_SendsOneChampionInAtAGate_AndItsKillPaysAndSavesTheFlag()
        {
            yield return Boot(ProvincialPit);
            var bm = Bookmaker.Instance;
            var panel = Panel();
            for (int i = 1; i < bm.tiers.Length; i++) bm.tiers[i].unlockKills = 0;
            yield return AtHisBooth(bm, 0, 100);
            Assert.IsFalse(MetaProgress.Data.HasFlag(bm.flag));
            Assert.IsTrue(bm.OpenBook());
            yield return null;
            Assert.AreEqual(4, panel.CardCount, "four cards");
            Assert.AreEqual("", panel.MoreText, "all four: no hint");
            Assert.AreEqual("IV", panel.CardNumeral(3));
            Assert.AreEqual("STAKE 40", panel.CardStake(3));
            Assert.AreEqual("WINS 200", panel.CardWins(3));
            StringAssert.Contains("CHAMPION", panel.CardChallenge(3));
            Assert.AreEqual((int)FrameKind.CardGold, panel.CardFrame(3), "the biggest stake wears gold");
            Assert.AreEqual((int)FrameKind.CardStone, panel.CardFrame(0));
            int alive = _em.AliveCount;
            yield return new WaitForSecondsRealtime(0.3f);
            panel.Click(3); // the card itself
            Assert.AreEqual(Bookmaker.Phase.Wager, bm.State);
            Assert.AreEqual(60, _run.Gold, "the stake: 40");
            Assert.AreEqual(alive + 1, _em.AliveCount, "he sends one champion");
            int slot = bm.ChampionSlot;
            Assert.GreaterOrEqual(slot, 0);
            Assert.IsTrue(_em.IsElite(slot), "an elite, with an affix");
            Assert.AreEqual(Bookmaker.ChampionLabel, RunObjective.Label);
            Assert.AreEqual(1, bm.Target);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(Bookmaker.Phase.Wager, bm.State, "on while it lives");
            _em.KillAll();
            yield return null;
            yield return null;
            Assert.AreEqual(Bookmaker.Outcome.Won, bm.LastOutcome, "the champion fell");
            Assert.AreEqual(60 + 200, _run.Gold, "he pays the big one");
            Assert.IsTrue(MetaProgress.Data.HasFlag(bm.flag), "a win at tier II or higher saves the flag (Loaded Dice)");
            Assert.IsFalse(RunObjective.Active);
        }
    }
}
