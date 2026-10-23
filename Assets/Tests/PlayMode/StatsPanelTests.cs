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
    /// P1 part C in the test arena: the stats page opens with the pause and names its sources (admin buffs marked), the
    /// level-up cards sit where LevelUpLayout puts them beside the "your stats" column (U1 layout A), the selected card's preview is exactly the stats after the
    /// pick, a Vow wears its split frame and previews its price in red, and the Blood Altar's prompt shows the max-HP cut
    /// that using it takes.
    /// P3: the pause shows the same bronze column as the level-up (the seed in its second line), each stat row's sources
    /// in a tooltip on hover; the pick preview's rows never move between cards (no weapon lines).
    /// </summary>
    public class StatsPanelTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        RunState _run;
        LevelUpController _lvl;
        PlayerBuild _build;
        PlayerStats _stats;
        LevelUpPanel _cards;
        StatsPanel _page;
        ScriptedInput _input;

        IEnumerator Boot(bool levelUps)
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            _input = new ScriptedInput();
            Player.Main.OverrideSource = _input;
            Player.Main.GetComponent<WeaponSystem>().enabled = false;
            DevSettings.GodMode = true;
            EnemyManager.Instance.KillAll();
            _lvl = LevelUpController.Instance;
            if (!levelUps && _lvl != null) _lvl.enabled = false; // P1 rule: off unless the test is about the cards
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _run = RunState.Current;
            _build = Player.Main.GetComponent<PlayerBuild>();
            _stats = Player.Main.GetComponent<PlayerStats>();
            _cards = Object.FindAnyObjectByType<LevelUpPanel>();
            _page = StatsPanel.Instance;
            Assert.IsNotNull(_page, "LevelUpPanel makes the stats page");
        }

        [TearDown]
        public void TearDown()
        {
            if (Player.Main != null) Player.Main.SetPaused(false);
            DevSettings.ResetAll();
        }

        IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator OpenCards()
        {
            _run.AddXp(_run.XpToNext - _run.Xp + 0.01f);
            float end = Time.realtimeSinceStartup + 3f;
            while (!_lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_lvl.IsOpen, "level-up cards should open");
            yield return Frames(2); // the cards draw, then the page
        }

        UpgradeDef Find(string id)
        {
            foreach (var d in _build.catalog.upgrades) if (d != null && d.id == id) return d;
            return null;
        }

        /// <summary>Banish every upgrade `keep` refuses, so the cards that come up are the ones the test needs.</summary>
        void BanishAllBut(System.Func<UpgradeDef, bool> keep)
        {
            foreach (var d in _build.catalog.upgrades)
                if (d != null && !keep(d)) _build.State.Banish(d);
        }

        static bool StatCard(UpgradeDef d) => d.kind == UpgradeKind.Boon && d.perLevel != null && d.perLevel.Length > 0;

        [UnityTest, Timeout(60000)]
        public IEnumerator Page_OpensWithThePause_ClosesOnResume_AndDoesNotRewriteEveryFrame()
        {
            yield return Boot(false);
            Assert.AreEqual(StatsPanel.Mode.Hidden, _page.Current);
            Player.Main.SetPaused(true);
            yield return Frames(2);
            Assert.AreEqual(StatsPanel.Mode.Pause, _page.Current, "Esc's pause shows the page");
            string text = _page.Text;
            foreach (var s in StatText.Order) StringAssert.Contains(StatInfo.Name(s), text, "every stat is on the page");
            var t = new CharBuffer(32);
            StringAssert.Contains(StatText.Value(t, StatId.MaxHp, _stats.MaxHp).ToString(), text, "with its total");
            StringAssert.Contains("SEED " + GameRandom.SeedText(GameRandom.RunSeed), _page.SubText, "the seed under the gladiator's name");
            Assert.AreEqual((int)FrameKind.Bronze, _page.FrameShown, "P3: the level-up's bronze column, not a marble page");
            var size = ((RectTransform)_cards.Content.parent).rect.size;
            var fit = LevelUpLayout.For(size, LevelUpLayout.PauseCards);
            Assert.AreEqual(fit.StatsScale, _page.PickScale, 1e-4f, "a three-card level-up's fit");
            int rewrites = _page.Rewrites;
            yield return Frames(10);
            Assert.AreEqual(rewrites, _page.Rewrites, "nothing changed: the text isn't rewritten");
            Player.Main.SetPaused(false);
            yield return Frames(2);
            Assert.AreEqual(StatsPanel.Mode.Hidden, _page.Current, "resuming hides it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Page_NamesEachSource_AndMarksAdminBuffs()
        {
            yield return Boot(false);
            var skin = Find("thick_skin");
            Assert.IsNotNull(skin);
            _build.Take(new CardOffer { Def = skin, Rarity = Rarity.Common, Level = 1 });
            DevSettings.DamagePct = 50f;
            Player.Main.SetPaused(true);
            yield return Frames(2);
            // P3: the sources moved from the page into each stat row's tooltip
            _page.HoverRow(StatId.MaxHp, true);
            yield return Frames(2);
            Assert.IsTrue(_page.TooltipShown, "hovering Max HP shows its sources");
            string text = _page.TooltipText;
            StringAssert.Contains(skin.displayName, text, "the card");
            StringAssert.Contains("+20", text, "and what it adds");
            StringAssert.Contains("Base", text);
            _page.HoverRow(StatId.MaxHp, false);
            _page.HoverRow(StatId.Damage, true);
            yield return Frames(2);
            StringAssert.Contains("ADMIN (F1)", _page.TooltipText, "admin buffs are a marked share (Editor / dev builds)");
            var shares = new StatShare[24];
            float sum = 0f;
            int n = _stats.Breakdown(StatId.Damage, shares);
            for (int i = 0; i < n; i++) sum += shares[i].Amount;
            Assert.AreEqual(_stats.Get(StatId.Damage), sum, 1e-4f, "the page's shares add up to the total");
            DevSettings.DamagePct = 0f;
            yield return Frames(2);
            StringAssert.DoesNotContain("ADMIN (F1)", _page.TooltipText, "the tooltip follows the admin menu while paused");
            _page.HoverRow(StatId.Damage, false);
            _page.HoverTitle(true);
            yield return Frames(2);
            var g = _stats.gladiator;
            if (g != null && g.traits != null && g.traits.Length > 0 && g.traits[0] != null)
            {
                Assert.IsTrue(_page.TooltipShown, "hovering the name shows the gladiator's traits");
                StringAssert.Contains(g.traits[0].displayName, _page.TooltipText);
            }
            _page.HoverTitle(false);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator HoverPreview_IsTheStatsAfterThePick_BesideTheCards()
        {
            yield return Boot(true);
            BanishAllBut(StatCard);
            yield return OpenCards();
            Assert.AreEqual(StatsPanel.Mode.Pick, _page.Current, "the page sits beside the cards");
            var canvas = ((RectTransform)_cards.Content.parent).rect.size; // U1 layout A: centred unless the column needs room
            Assert.AreEqual(LevelUpLayout.For(canvas, _lvl.ChoiceCount).ContentX, _cards.Content.anchoredPosition.x, 1f,
                "the content sits where LevelUpLayout puts it");
            var pageCorners = new Vector3[4];
            var cardCorners = new Vector3[4];
            _page.Page.GetWorldCorners(pageCorners);
            _cards.CardRect(_lvl.ChoiceCount - 1).GetWorldCorners(cardCorners);
            Assert.Less(cardCorners[2].x, pageCorners[0].x, "the last card ends before the stats column starts");

            int sel = _lvl.ChoiceCount > 1 ? 1 : 0;
            _cards.Hover(sel); // the mouse; keys and the gamepad move the same selection
            yield return Frames(2);
            Assert.AreEqual(sel, _cards.Selected);
            var preview = _page.Preview;
            Assert.IsTrue(preview.Active);
            Assert.Greater(preview.ChangedCount, 0, _lvl.Offers[sel].Def.id + " changes a stat");
            StringAssert.Contains(_lvl.Offers[sel].Def.displayName, _page.Text, "the preview names the card");
            StringAssert.Contains("→", _page.Text);
            var after = (float[])preview.After.Clone();
            var picked = _lvl.Offers[sel];
            _lvl.Choose(sel);
            yield return null;
            for (int i = 0; i < StatInfo.Count; i++)
                Assert.AreEqual(after[i], _stats.Get((StatId)i), 1e-3f, picked.Def.id + " " + (StatId)i + ": the preview is what the pick did");
            yield return Frames(2);
            Assert.AreEqual(StatsPanel.Mode.Hidden, _page.Current, "the column leaves with the cards");
        }

        /// <summary>P3 (the producer's note 19): every card shows the same rows at the same places; only the numbers
        /// change. A new weapon's card adds no lines (its info is in the inventory's ghost).</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator PickPreview_HasFixedRows_NothingMovesBetweenCards()
        {
            yield return Boot(true);
            yield return OpenCards();
            int n = _lvl.ChoiceCount;
            Assert.GreaterOrEqual(n, 2, "two cards to move between");
            var rows = new Vector3[StatText.Order.Length];
            int lines = -1;
            for (int c = 0; c < n; c++)
            {
                _cards.Hover(c);
                yield return Frames(2);
                Assert.AreEqual(c, _cards.Selected);
                int count = _page.Text.Split('\n').Length;
                if (lines < 0) lines = count;
                Assert.AreEqual(lines, count, "card " + c + ": the same rows");
                StringAssert.DoesNotContain("attacks/s", _page.Text, "no weapon lines");
                for (int i = 0; i < rows.Length; i++)
                {
                    var p = _page.RowRect(StatText.Order[i]).position;
                    if (c == 0) rows[i] = p;
                    else Assert.AreEqual(rows[i], p, StatText.Order[i] + " stays where it was");
                }
            }
            _lvl.Choose(0);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator VowCard_WearsTheSplitFrame_AndPreviewsItsPriceInRed()
        {
            yield return Boot(true);
            UpgradeDef vow = null;
            foreach (var d in _build.catalog.upgrades) if (d != null && d.IsVow) { vow = d; break; }
            if (vow == null) Assert.Ignore("no Vows in the catalog: run Gladiator/Setup/Upgrades/Create Vows");
            BanishAllBut(d => d == vow); // the Vow, then filler cards
            yield return OpenCards();
            int at = -1;
            for (int i = 0; i < _lvl.ChoiceCount; i++) if (_lvl.Offers[i].Def == vow) at = i;
            Assert.GreaterOrEqual(at, 0, "the Vow is offered");
            for (int i = 0; i < _lvl.ChoiceCount; i++)
                Assert.AreEqual(i == at, _cards.CardIsVowFramed(i), "only the Vow wears the red/gold frame");
            StringAssert.Contains(CardText.DrawbackColor, _cards.CardDescription(at), "its card shows the price");
            _cards.Hover(at);
            yield return Frames(2);
            var p = _page.Preview;
            bool loss = false, gain = false;
            for (int i = 0; i < StatInfo.Count; i++)
            {
                if (!p.Changed((StatId)i)) continue;
                if (p.After[i] < p.Before[i]) loss = true;
                else gain = true;
            }
            Assert.IsTrue(gain && loss, "a Vow previews a gain and a loss");
            StringAssert.Contains(StatText.Loss, _page.Text, "the loss in red");
            var after = (float[])p.After.Clone();
            _lvl.Choose(at);
            yield return null;
            for (int i = 0; i < StatInfo.Count; i++)
                Assert.AreEqual(after[i], _stats.Get((StatId)i), 1e-3f, vow.id + " " + (StatId)i);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AltarPrompt_ShowsTheMaxHpCut_ThatUsingItTakes()
        {
            yield return Boot(true);
            var props = Interactables.Instance;
            var health = Player.Main.GetComponent<PlayerHealth>();
            int altar = props.PlaceAltarAt(Player.Main.transform.position + new Vector3(0f, 0f, 1f));
            Assert.GreaterOrEqual(altar, 0);
            float end = Time.realtimeSinceStartup + 2f;
            while (props.InReach != altar && Time.realtimeSinceStartup < end) yield return null;
            yield return Frames(2);
            Assert.IsTrue(_page.PromptShown, "walking up to the altar shows its price");
            StringAssert.Contains("Max HP", _page.PromptText);
            float expected = _page.PromptPreview.AfterOf(StatId.MaxHp);
            Assert.AreEqual(health.MaxHp * (1f - props.config.altarMaxHpCost), expected, 0.01f);
            var t = new CharBuffer(16);
            StringAssert.Contains(StatText.Value(t, StatId.MaxHp, expected).ToString(), _page.PromptText);
            Assert.IsTrue(props.TryUse(altar));
            Assert.AreEqual(expected, health.MaxHp, 0.01f, "the prompt's number is what the altar took");
            end = Time.realtimeSinceStartup + 2f;
            while (!_lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            yield return Frames(2);
            Assert.IsFalse(_page.PromptShown, "the prompt goes with the altar");
            Assert.AreEqual(StatsPanel.Mode.Pick, _page.Current, "its Rare+ pick has the stats column too");
            _lvl.Choose(0);
        }
    }
}
