using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// P1 (the producer's v0.17.0 playtest, part B): on death every weapon, companion and player projectile stops at once
    /// and the run stops counting; the crowd gives one last reaction (a groan, a cheer) and fades to silence, and ducks
    /// while the game is paused; the dev tag is spelled out in a corner; the synergy chips fit their names; PAUSED and
    /// its hint sit left of the stats page.
    /// (The Bookmaker on E is in SecretPlayTests.)
    /// </summary>
    public class RunEndP1Tests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";

        Player _player;
        PlayerHealth _health;
        WeaponSystem _weapons;
        RunState _run;
        EnemyManager _em;

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, path);
            return a;
        }

        static WeaponDef Wp(string file) => Load<WeaponDef>("Assets/Data/Weapons/" + file + ".asset");
        static EnemyDef En(string file) => Load<EnemyDef>("Assets/Data/Enemies/" + file + ".asset");
        static UpgradeDef Up(string file) => Load<UpgradeDef>("Assets/Data/Upgrades/" + file + ".asset");

        /// <summary>Loads `scene` with the Director, the debug spawner and level-ups off, past the arena intro.</summary>
        IEnumerator Boot(string scene, bool god = true)
        {
            SaveSandbox.Wipe();
            DevSettings.ResetAll();
            DevSettings.GodMode = god; // nothing kills the player before the test does
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
            _player = Player.Main;
            _player.OverrideSource = new ScriptedInput();
            _health = _player.GetComponent<PlayerHealth>();
            _weapons = _player.GetComponent<WeaponSystem>();
            _run = RunState.Current;
            _em = EnemyManager.Instance;
            _em.KillAll();
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (Player.Main != null) Player.Main.SetPaused(false);
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
        }

        static IEnumerator WaitRealtime(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        Vector3 Ground(Vector3 p)
        {
            if (_em.Height != null) p.y = _em.Height.Sample(p.x, p.z);
            return p;
        }

        void Ring(EnemyDef def, int count, float radius)
        {
            Vector3 p = _player.transform.position;
            for (int k = 0; k < count; k++)
                _em.Spawn(def, Ground(p + Quaternion.Euler(0f, k * 360f / count, 0f) * Vector3.forward * radius));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Death_StopsWeaponsCompanionsAndProjectiles_AtOnce()
        {
            yield return Boot(TestArena);
            Assert.IsNotNull(_weapons.Equip(Wp("WP_Javelins")), "a volley weapon");
            Assert.IsNotNull(_weapons.Equip(Wp("WP_GormsFlail")), "an orbit weapon");
            Assert.IsNotNull(_weapons.Equip(Wp("WP_WolfPack")), "and the companions, beside the gladius");
            var brute = En("EN_Brute");
            Ring(brute, 6, 2.2f);
            Ring(brute, 6, 6f);
            var pp = PlayerProjectiles.Instance;
            CompanionPack pack = null;
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until)
            {
                pack = _player.GetComponent<CompanionPack>();
                if (_weapons.Hits > 0 && pp.Count > 0 && pack != null && pack.Count > 0) break;
                if (_em.AliveCount < 6) Ring(brute, 6, 4f);
                yield return null;
            }
            Assert.Greater(_weapons.Hits, 0, "the weapons fight while the player lives");
            Assert.Greater(pp.Count, 0, "javelins are in the air when the player falls");
            Assert.IsNotNull(pack, "the wolf pack");
            Assert.Greater(pack.Count, 0, "the wolves are out");

            _health.Kill();
            int swings = _weapons.Swings, hits = _weapons.Hits, shotHits = pp.Hits, bites = pack.Bites, wolves = pack.Count;
            int kills = _run.Kills;
            yield return null;
            Assert.IsTrue(RunState.CombatOver, "the player is down");
            Assert.IsTrue(_weapons.Halted, "the weapons know it");
            Assert.AreEqual(0, pp.Count, "what was in flight fizzles out at once");
            Ring(En("EN_Convict"), 6, 1.3f); // fresh ones right on the body: nothing of the player's may touch them

            yield return WaitRealtime(() => RunEndController.Instance.ScreenShown, 6f);
            Assert.IsTrue(RunEndController.Instance.ScreenShown, "the death beat played out");
            Assert.AreEqual(swings, _weapons.Swings, "no swing after death");
            Assert.AreEqual(hits, _weapons.Hits, "no weapon hit after death (the flail's balls included)");
            Assert.AreEqual(shotHits, pp.Hits, "no projectile hit after death");
            Assert.AreEqual(0, pp.Count, "and nothing new thrown");
            Assert.AreEqual(bites, pack.Bites, "no bite after death");
            Assert.AreEqual(wolves, pack.Count, "the wolves stay by the body");
            Assert.AreEqual(kills, _run.Kills, "the kill count froze when the player fell");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator OnceThePlayerIsDown_KillsGoldAndXpStopCounting()
        {
            yield return Boot(TestArena);
            _weapons.enabled = false;
            _run.AddGold(40);
            int kills0 = _run.Kills;
            for (int k = 0; k < 3; k++) GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero });
            Assert.AreEqual(kills0 + 3, _run.Kills, "alive: kills count");
            int kills = _run.Kills, gold = _run.Gold, level = _run.Level;
            float xp = _run.Xp, dealt = _run.DamageDealt;

            _health.Kill(); // End() follows in this frame's Late tick: the count is frozen from now on
            GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero });
            GameEvents.OnHit.Raise(new HitEvent { Position = Vector3.zero, Damage = 50f, Flags = DamageFlags.Melee });
            _run.AddGold(25);
            _run.AddXp(10000f);
            Assert.AreEqual(kills, _run.Kills, "dead: no more kills");
            Assert.AreEqual(gold, _run.Gold, "no more gold");
            Assert.AreEqual(level, _run.Level, "no level-up");
            Assert.AreEqual(xp, _run.Xp, 1e-4f, "no XP");
            Assert.AreEqual(dealt, _run.DamageDealt, 1e-4f, "no damage dealt");
            Assert.IsFalse(_run.SpendGold(10), "and nothing spent");

            yield return null;
            Assert.IsTrue(_run.Ended, "End() ran");
            Assert.AreEqual(RunOutcome.Died, _run.Outcome);
            for (int k = 0; k < 5; k++) GameEvents.OnKill.Raise(new KillEvent { Position = Vector3.zero });
            _run.AddGold(25);
            Assert.AreEqual(kills, _run.Kills, "after End(): still frozen");
            Assert.AreEqual(gold, _run.Gold);
            yield return WaitRealtime(() => RunEndController.Instance.ScreenShown, 5f);
            Assert.AreEqual(kills, _run.Kills, "the end screen shows the kills of the moment of death");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Crowd_GroansOnDeath_ThenFadesToSilence()
        {
            yield return Boot(SlavePit);
            var sfx = SfxPlayer.Instance;
            Assert.IsNotNull(sfx);
            Assert.IsTrue(sfx.CrowdPlaying, "the Slave Pit's crowd is on");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.Greater(sfx.CrowdMurmurVolume, 0.02f, "and audible");
            Assert.IsNull(sfx.CrowdLastWord);

            _health.Kill();
            yield return null;
            Assert.IsTrue(sfx.CrowdEnding, "the run ended");
            Assert.AreEqual(SfxId.Boo, sfx.CrowdLastWord, "the crowd's last word on a death: the boo, slowed into a groan");
            yield return WaitRealtime(() => !sfx.CrowdPlaying, sfx.endHold + sfx.endFade + 1.5f);
            Assert.IsFalse(sfx.CrowdPlaying, "the loops stop: no loop at the end screen");
            Assert.AreEqual(0f, sfx.CrowdMurmurVolume, 1e-4f, "the murmur reached silence");
            Assert.AreEqual(0f, sfx.CrowdRoarVolume, 1e-4f, "the roar too");

            yield return WaitRealtime(() => RunEndController.Instance.ScreenShown, 5f);
            Assert.IsTrue(RunEndController.Instance.ScreenShown);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(sfx.CrowdPlaying, "still silent on the end screen");
            Assert.AreEqual(0f, sfx.CrowdMurmurVolume, 1e-4f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Crowd_CheersAWin_ThenFadesToSilence()
        {
            yield return Boot(SlavePit);
            var sfx = SfxPlayer.Instance;
            Assert.IsTrue(sfx.CrowdPlaying);
            yield return new WaitForSecondsRealtime(0.3f);
            _run.End(RunOutcome.Survived);
            Assert.AreEqual(SfxId.Cheer, sfx.CrowdLastWord, "a cheer for a win");
            yield return WaitRealtime(() => !sfx.CrowdPlaying, sfx.endHold + sfx.endFade + 1.5f);
            Assert.IsFalse(sfx.CrowdPlaying, "then silence");
            Assert.AreEqual(0f, sfx.CrowdMurmurVolume, 1e-4f);
            Assert.AreEqual(0f, sfx.CrowdRoarVolume, 1e-4f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Crowd_DucksWhilePaused_AndComesBackOnResume()
        {
            yield return Boot(SlavePit);
            var sfx = SfxPlayer.Instance;
            yield return new WaitForSecondsRealtime(sfx.duckTime + 0.3f);
            Assert.AreEqual(1f, sfx.CrowdDuck, 1e-3f, "playing: full crowd");
            float live = sfx.CrowdMurmurVolume;
            Assert.Greater(live, 0.02f);

            TimeController.PushPause(); // what the level-up cards and the pause menu do
            yield return new WaitForSecondsRealtime(sfx.duckTime + 0.3f);
            Assert.AreEqual(sfx.pauseDuck, sfx.CrowdDuck, 1e-3f, "paused: the crowd drops back behind the menu");
            Assert.Less(sfx.CrowdMurmurVolume, live * 0.6f, "audibly quieter");
            Assert.IsTrue(sfx.CrowdPlaying, "ducked, not stopped");

            TimeController.PopPause();
            yield return new WaitForSecondsRealtime(sfx.duckTime + 0.3f);
            Assert.AreEqual(1f, sfx.CrowdDuck, 1e-3f, "and it comes back on resume");
            Assert.IsFalse(sfx.CrowdEnding);
        }

        /// <summary>The arena intro holds the game paused behind its title card; the crowd stays full there (only menus and
        /// cards duck it). FeedbackTests' crowd swell runs in that window too.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Crowd_IsNotDucked_UnderTheArenaIntro()
        {
            SaveSandbox.Wipe();
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(SlavePit, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            var intro = RunIntro.Instance;
            Assert.IsNotNull(intro, "the Slave Pit opens on its title card");
            if (intro.Current != RunIntro.Stage.Holding) Assert.Inconclusive("the intro was already past its hold on the first frame");
            Assert.IsTrue(TimeController.IsPaused, "the intro holds the game paused");
            var sfx = SfxPlayer.Instance;
            for (int k = 0; k < 10; k++) sfx.Tick(0f, 0.1f); // a second of real time, still on the title card
            Assert.AreEqual(1f, sfx.CrowdDuck, 1e-3f, "the crowd isn't ducked under the title card");
        }

        /// <summary>P3 (the producer's note 17): the pause is the level-up's frame: PAUSED (one line, in the title's place),
        /// three plates where the cards stand, the inventory left and the stats column right, as a three-card level-up
        /// lays them out; the old hint text is gone (the plates are the commands).</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator Paused_IsTheLevelUpsFrame_PlatesBetweenTheColumns()
        {
            yield return Boot(SlavePit);
            var hud = Object.FindAnyObjectByType<Hud>();
            var cards = Object.FindAnyObjectByType<LevelUpPanel>();
            Assert.IsNotNull(cards, "every arena has the level-up panel (it hosts the pause)");
            _player.SetPaused(true);
            yield return null;
            yield return null;
            var page = StatsPanel.Instance;
            Assert.IsNotNull(page, "every arena has the stats page");
            Assert.AreEqual(StatsPanel.Mode.Pause, page.Current, "the stats column is up beside the pause");
            Assert.IsTrue(cards.PauseShown);
            Assert.AreEqual(LevelUpPanel.PausedWord, cards.TitleText);
            Assert.AreEqual("", cards.SubtitleText, "no hint text: the plates are the commands");
            var pause = hud.PauseRect;
            Assert.IsNotNull(pause, "the HUD's hook finds the PAUSED title");
            Assert.AreEqual(1, hud.PauseLineCount, "PAUSED on one line");
            Assert.LessOrEqual(hud.PauseTextWidth, pause.rect.width, "PAUSED fits its box");
            var fit = cards.Fit;
            Assert.AreEqual(LevelUpLayout.PauseCards, fit.Cards, "a three-card level-up's fit");
            var pc = new Vector3[4];
            var sc = new Vector3[4];
            var ic = new Vector3[4];
            page.Page.GetWorldCorners(sc);
            cards.Inventory.Root.GetWorldCorners(ic);
            for (int i = 0; i < LevelUpPanel.PausePlates; i++)
            {
                cards.PausePlateRect(i).GetWorldCorners(pc);
                Debug.Log($"[P3] screen {Screen.width}x{Screen.height}: plate {i} x {pc[0].x:0}-{pc[2].x:0}; inventory to x {ic[2].x:0}, stats from x {sc[0].x:0}");
                Assert.Less(ic[2].x, pc[0].x, "plate " + i + " clears the inventory");
                Assert.Less(pc[2].x, sc[0].x, "plate " + i + " clears the stats column");
            }

            _player.SetPaused(false);
            yield return null;
            yield return null;
            Assert.IsFalse(cards.PauseShown, "resumed: the pause goes");
            Assert.AreEqual(StatsPanel.Mode.Hidden, page.Current);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AdminTag_IsSpelledOut_InTheBottomRightCorner()
        {
            yield return Boot(TestArena, god: false);
            var hud = Object.FindAnyObjectByType<Hud>();
            Assert.IsNotNull(hud);
            yield return null;
            Assert.AreEqual("", hud.AdminTagText, "no dev switch on: no tag");

            DevSettings.GodMode = true;
            AdminController.Instance.SetBuff(AdminController.Buff.Damage, 50f);
            yield return null;
            yield return null;
            string t = hud.AdminTagText;
            StringAssert.StartsWith("ADMIN:", t, "it says what it is");
            StringAssert.Contains("GOD MODE", t);
            StringAssert.Contains("BUFFS ON", t, "not a bare BUFFS");
            StringAssert.Contains("(F1)", t, "and where to change it");
            StringAssert.DoesNotContain("TIMELESS", t);
            var rt = hud.AdminTagRect;
            Assert.AreEqual(new Vector2(1f, 0f), rt.anchorMin, "in the bottom-right corner, away from the timer");
            Assert.AreEqual(new Vector2(1f, 0f), rt.anchorMax);
            Debug.Log("[P1] admin tag: " + t);

            AdminController.Instance.ResetBuffs();
            DevSettings.GodMode = false;
            yield return null;
            yield return null;
            Assert.AreEqual("", hud.AdminTagText, "switches off: the tag goes");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SynergyChips_NameAfterTheIcon_ChipAsWideAsItsName()
        {
            yield return Boot(TestArena);
            _weapons.enabled = false;
            var build = _player.GetComponent<PlayerBuild>();
            void Take(UpgradeDef d) => build.Take(new CardOffer { Def = d, Rarity = Rarity.Common, Level = build.State.LevelOf(d) + 1 });
            Take(Up("UP_LuckyCut"));
            Take(Up("UP_SerratedEdge")); // gladius + these = 3 CRIT: Executioner, the longest synergy name
            Take(Up("UP_Kindling"));
            Take(Up("UP_Pyre"));
            Take(Up("UP_ConductiveOil")); // 3 FIRE: Wildfire
            yield return null;
            yield return null;
            int n = build.State.SynergyCount;
            Assert.GreaterOrEqual(n, 2, "Executioner and Wildfire are on");
            var hud = Object.FindAnyObjectByType<Hud>();
            bool executioner = false;
            for (int j = 0; j < n; j++)
            {
                var chip = hud.ChipRect(j);
                var icon = hud.ChipIconRect(j);
                var name = hud.ChipNameRect(j);
                float need = hud.ChipNameWidth(j);
                string label = hud.SynergyChip(j);
                Debug.Log($"[P1] chip {j} '{label}': at {chip.anchoredPosition}, {chip.sizeDelta.x:0} wide, name needs {need:0}");
                Assert.LessOrEqual(icon.anchoredPosition.x + icon.sizeDelta.x, name.anchoredPosition.x, label + ": the name starts after the icon");
                Assert.GreaterOrEqual(name.sizeDelta.x + 0.5f, need, label + ": the name's box fits it");
                Assert.GreaterOrEqual(chip.sizeDelta.x + 0.5f, name.anchoredPosition.x + need, label + ": the chip fits its name");
                Assert.LessOrEqual(chip.sizeDelta.x, name.anchoredPosition.x + need + 16f, label + ": and is no wider than it needs");
                Assert.LessOrEqual(chip.anchoredPosition.y, hud.BoonRowBottom - 4f, label + ": a gap under the Boon row");
                var a = new Rect(chip.anchoredPosition.x, chip.anchoredPosition.y - chip.sizeDelta.y, chip.sizeDelta.x, chip.sizeDelta.y);
                for (int i = 0; i < j; i++)
                {
                    var o = hud.ChipRect(i);
                    var b = new Rect(o.anchoredPosition.x, o.anchoredPosition.y - o.sizeDelta.y, o.sizeDelta.x, o.sizeDelta.y);
                    Assert.IsFalse(a.Overlaps(b), $"chips {i} and {j} don't overlap");
                }
                if (label == "Executioner") executioner = true;
            }
            Assert.IsTrue(executioner, "the longest name is on a chip");
        }
    }
}
