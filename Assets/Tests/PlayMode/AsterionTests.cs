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
    /// R3 (docs/GLADIATORS.md "Asterion, the Chained"), the setup part: his data as AsterionSetup writes it (130 HP, 95 %
    /// speed, a 0.45 m capsule, the Labyrinth Chain and its HEAVY card with its icon, Gore and Rage, UL_Asterion and the
    /// tease on the Eternal Victor), last in the roster; a run as him has his model (its trailing Chain hidden only while
    /// the chain swings), capsule, stats and traits, and the chain sweeps at an enemy; beating the Eternal Victor unlocks
    /// him and puts the chain in the pool; the tease line comes during a boss's fall only while its unlock is locked and
    /// holds the fall longer, and the older bosses fall exactly as before. Runs after the setup (Create Gladiator Data,
    /// Upgrade Icons, Bake Portraits, the menu rebuild).
    /// </summary>
    public class AsterionTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        const string VictorPath = "Assets/Data/Bosses/BD_EternalVictor.asset";
        const string TeaseText = "BELOW THE SAND, A CHAIN SNAPS.";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Asterion => Load<GladiatorDef>("Assets/Data/Gladiators/GL_Asterion.asset");
        static WeaponDef Chain => Load<WeaponDef>("Assets/Data/Weapons/WP_LabyrinthChain.asset");
        static UpgradeDef ChainCard => Load<UpgradeDef>("Assets/Data/Upgrades/UP_W_LabyrinthChain.asset");
        static UnlockDef Unlock => Load<UnlockDef>("Assets/Data/Meta/UL_Asterion.asset");
        static MetaConfig Meta => Load<MetaConfig>("Assets/Data/Meta/MetaConfig.asset");
        static EnemyDef Brute => Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");
        static StatusDef Root => Load<StatusDef>("Assets/Data/Status/SE_Root.asset");

        EnemyManager _em;
        WeaponSystem _weapons;
        BossFight _fight;
        int _teases;
        string _teaseText;
        readonly System.Collections.Generic.List<Object> _made = new System.Collections.Generic.List<Object>();

        IEnumerator Boot(string scene, GladiatorDef as_, bool levelUps = false)
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
            if (lvl != null && !levelUps) lvl.enabled = false; // the boss's hoard needs it
            Player.Main.OverrideSource = new ScriptedInput();
            _em = EnemyManager.Instance;
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnAnnounce.Unsubscribe(OnAnnounce);
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
            foreach (var o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            SaveSandbox.Wipe();
        }

        void OnAnnounce(in AnnounceEvent e)
        {
            if (e.Kind != AnnounceKind.Line || _fight == null || _fight.boss == null || e.Text != _fight.boss.revealText) return;
            _teases++;
            _teaseText = e.Text;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        // ---------------------------------------------------------------- his data

        [Test]
        public void HisData_IsTheCard()
        {
            var g = Asterion;
            Assert.IsNotNull(g, "GL_Asterion (run Create Gladiator Data)");
            Assert.AreEqual("asterion", g.id);
            Assert.AreEqual("Asterion, the Chained", g.displayName);
            Assert.AreEqual(130f, g.BaseValue(StatId.MaxHp, 0f), 1e-3f);
            Assert.AreEqual(0.95f, g.BaseValue(StatId.MoveSpeed, 0f), 1e-3f);
            Assert.IsNotNull(g.model);
            Assert.AreEqual("CH_Asterion", g.model.name);
            Assert.IsNotNull(FindDeep(g.model.transform, "Chain"), "the trailing chain is a part of his model");
            Assert.IsNotNull(g.movement);
            Assert.AreEqual(0.45f, g.movement.capsuleRadius, 1e-3f, "the widest capsule after the Machine's");
            Assert.IsNotNull(g.animProfile);
            Assert.AreNotEqual(Load<PartAnimProfile>("Assets/Data/Config/PartAnim_Chibi.asset"), g.animProfile, "his own stoop and sweep");
            Assert.AreEqual(120f, g.animProfile.swingYawStart, 1e-3f);
            Assert.AreEqual(-120f, g.animProfile.swingYawEnd, 1e-3f);

            var w = g.startingWeapon;
            Assert.AreSame(Chain, w, "the Labyrinth Chain");
            Assert.AreEqual("labyrinth_chain", w.id);
            Assert.AreEqual(WeaponPattern.Sweep, w.pattern);
            Assert.AreEqual(5.2f, w.range, 1e-3f);
            Assert.AreEqual(240f, w.arcDegrees, 1e-3f);
            Assert.AreEqual(1.4f, w.tipLength, 1e-3f, "the stone");
            Assert.AreEqual(15f, w.damage, 1e-3f);
            Assert.AreEqual(1.6f, w.cooldown, 1e-3f);
            Assert.AreEqual(8, w.MaxLevel);
            Assert.IsNotNull(w.mesh, "the stone: WP_LabyrinthChain's Weight");
            Assert.AreEqual("Weight", w.mesh.name);
            Assert.IsNotNull(w.linkMesh, "the links: WP_LabyrinthChain's Link");
            Assert.AreEqual("Link", w.linkMesh.name);
            Assert.AreEqual("Chain", w.modelPart, "it swings his model's chain");

            Assert.IsNotNull(g.traits);
            Assert.AreEqual(2, g.traits.Length);
            Assert.IsInstanceOf<GoreTrait>(g.traits[0]);
            Assert.IsInstanceOf<RageTrait>(g.traits[1]);

            Assert.AreSame(Unlock, g.unlock);
            var all = AllGladiators();
            Assert.AreSame(g, all[all.Length - 1], "last in the roster (select screen, Codex, bakes)");
            Assert.IsNotNull(g.portrait, "baked from his model (Bake Portraits)");
            Assert.IsNotNull(g.portraitSilhouette);
        }

        [Test]
        public void TheChainsCard_IsHeavy_WithAnIcon()
        {
            var c = ChainCard;
            Assert.IsNotNull(c);
            Assert.AreEqual("weapon_labyrinth_chain", c.id);
            Assert.AreEqual("LC", c.glyph);
            Assert.AreEqual(UpgradeKind.Weapon, c.kind);
            Assert.AreSame(Chain, c.weapon);
            Assert.AreEqual(8, c.maxLevel);
            Assert.AreEqual(UpgradeTag.Heavy, c.tags, "Q8");
            Assert.IsNotNull(c.icon, "its row in the atlas (Upgrade Icons)");
            Assert.GreaterOrEqual(c.iconRow, 0);
            Assert.Contains(c, Load<UpgradeCatalog>("Assets/Data/UpgradeCatalog.asset").upgrades, "in the catalog");
        }

        [Test]
        public void TheUnlock_IsTheEternalVictor_AndHoldsTheChainBack()
        {
            var u = Unlock;
            Assert.IsNotNull(u);
            Assert.AreEqual("asterion", u.id);
            Assert.AreEqual(1, u.conditions.Length);
            Assert.AreEqual(UnlockConditionKind.DefeatBoss, u.conditions[0].kind);
            Assert.AreEqual("eternal_victor", u.conditions[0].id);
            Assert.AreEqual(1f, u.conditions[0].amount, 1e-3f);
            Assert.AreEqual(1, u.addsToPool.Length);
            Assert.AreSame(ChainCard, u.addsToPool[0], "the chain joins the pool");
            Assert.Contains(u, Meta.unlocks, "MetaConfig collects it");
            Assert.IsFalse(string.IsNullOrEmpty(u.hint), "the Codex's riddle");

            var fresh = new SaveData();
            Assert.IsFalse(MetaProgress.AllMet(u, fresh), "a fresh save");
            Assert.IsTrue(MetaProgress.IsLocked(ChainCard, Meta, fresh), "no chain in the cards");
            SaveData.Add(fresh.bossKills, "gorm", 3);
            SaveData.Add(fresh.bossKills, "emperors_champion", 1);
            Assert.IsFalse(MetaProgress.AllMet(u, fresh), "any other boss: still chained");
            SaveData.Add(fresh.bossKills, "eternal_victor", 1);
            Assert.IsTrue(MetaProgress.AllMet(u, fresh), "his jailer is dust");
            fresh.unlocked.Add(u.id);
            Assert.IsFalse(MetaProgress.IsLocked(ChainCard, Meta, fresh), "the chain in the cards");
        }

        [Test]
        public void TheTease_IsOnTheEternalVictor_AndNoOtherBoss()
        {
            var victor = Load<BossDef>(VictorPath);
            Assert.IsNotNull(victor);
            Assert.AreEqual(TeaseText, victor.revealText);
            Assert.AreSame(Unlock, victor.revealUnlock, "only while Asterion is locked");
            Assert.AreEqual(1.2f, victor.revealDelay, 1e-3f);
            Assert.AreEqual(1.8f, victor.revealHold, 1e-3f);
            var c = victor.revealColor;
            Assert.IsFalse(c.r > 0.8f && c.g < 0.5f, "not orange-red: it doesn't hurt");
            int others = 0;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:BossDef"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path == VictorPath) continue;
                var b = Load<BossDef>(path);
                Assert.IsTrue(string.IsNullOrEmpty(b.revealText), b.name + ": no tease, so it falls as before");
                others++;
            }
            Assert.GreaterOrEqual(others, 5, "Gorm, the Champion, the Beast Lord, the Patron, the Emperor's Champion");
        }

        [Test]
        public void ThePortraitBakes_HideTheChain()
        {
            var t = System.Type.GetType("Gladiator.EditorTools.PortraitBaker, Gladiator.Editor");
            Assert.IsNotNull(t, "PortraitBaker (Gladiator.Editor)");
            var hidden = (string[])t.GetField("PortraitHidden").GetValue(null);
            Assert.Contains("Chain", hidden);
            // the bakes hide parts by these names hanging off the model (the FBX root or the body root under it); the Ashen
            // Oracle's censer chain is deeper and stays
            int parts = 0;
            foreach (var name in hidden) if (TopPart(Asterion.model.transform, name) != null) parts++;
            Assert.AreEqual(1, parts, "his model's chain is the part they hide");
            foreach (var g in AllGladiators())
                if (g != Asterion && g.model != null)
                    foreach (var name in hidden)
                        Assert.IsTrue(TopPart(g.model.transform, name) == null, g.id + " has no such part at its root: it bakes as before");
        }

        /// <summary>A child of `root`, or of one of its children, named `name` (the parts PortraitBaker.HideParts sees).</summary>
        static Transform TopPart(Transform root, string name)
        {
            var t = root.Find(name);
            for (int i = 0; t == null && i < root.childCount; i++) t = root.GetChild(i).Find(name);
            return t;
        }

        static GladiatorDef[] AllGladiators()
        {
            var t = System.Type.GetType("Gladiator.EditorTools.GladiatorSetup, Gladiator.Editor");
            Assert.IsNotNull(t, "GladiatorSetup (Gladiator.Editor)");
            return (GladiatorDef[])t.GetMethod("All").Invoke(null, null);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheSelectScreen_EndsWithHim()
        {
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsNotNull(menu);
            Assert.AreSame(Asterion, menu.gladiators[menu.gladiators.Length - 1], "the menu was rebuilt with him last");
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Asterion), "locked on a fresh save");
        }

        // ---------------------------------------------------------------- a run as him

        [UnityTest, Timeout(60000)]
        public IEnumerator ARunAsHim_HasHisModel_Capsule_StatsAndTraits_AndTheChainSweeps()
        {
            yield return Boot(TestArena, Asterion);
            var p = Player.Main;
            Assert.AreSame(Asterion, p.GetComponent<GladiatorLoadout>().Current);
            var visual = p.transform.Find("Visual");
            Assert.AreEqual(1, visual.childCount, "the Nobody's model is gone");
            Assert.AreEqual("CH_Asterion", visual.GetChild(0).name);
            Assert.IsNotNull(FindDeep(visual, "Head"), "same part names: the animator drives it");
            Assert.IsNotNull(FindDeep(visual, "ArmR"));
            var chain = FindDeep(visual, "Chain");
            Assert.IsNotNull(chain, "the chain dragging behind him");
            Assert.IsTrue(chain.gameObject.activeSelf, "shown before any sweep");
            Assert.AreEqual(0.45f, p.GetComponent<CharacterController>().radius, 0.01f, "a big target");
            Assert.AreEqual(1.85f, p.GetComponent<CharacterController>().height, 0.05f);
            Assert.AreEqual(130f, p.GetComponent<PlayerHealth>().MaxHp, 0.01f);
            Assert.AreEqual(0.95f, p.GetComponent<PlayerStats>().Get(StatId.MoveSpeed), 1e-3f);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreSame(Chain, _weapons.Weapons[0].Def);
            Assert.IsNotNull(p.GetComponent<Gore>(), "Gore");
            Assert.IsNotNull(p.GetComponent<Rage>(), "Rage");
            var build = p.GetComponent<PlayerBuild>();
            var card = build.FindWeaponCard(_weapons.Weapons[0].Def);
            Assert.AreSame(ChainCard, card);
            Assert.AreEqual(1, build.State.LevelOf(card), "the chain is owned from the start");

            // a brute held at the stone's distance, ahead of him: the chain comes round and hits it
            int b = _em.Spawn(Brute, p.transform.position + p.transform.forward * 4.5f);
            yield return null;
            float hp = _em.GetHp(b);
            bool sawHidden = false;
            float end = Time.time + 4f;
            while (Time.time < end && (_weapons.Sweeps == 0 || _em.GetHp(b) >= hp))
            {
                if (_em.IsAlive(b)) _em.ApplyStatus(b, Root, 0f);
                if (_weapons.Sweeps > 0 && !chain.gameObject.activeSelf) sawHidden = true;
                yield return null;
            }
            Assert.GreaterOrEqual(_weapons.Sweeps, 1, "the chain swept");
            Assert.Less(_em.GetHp(b), hp, "and hit the brute");
            end = Time.time + 0.6f; // the sweep is 0.3 s; the next one is a cooldown away
            while (Time.time < end)
            {
                if (!chain.gameObject.activeSelf) sawHidden = true;
                yield return null;
            }
            Assert.IsTrue(sawHidden, "the model's chain is hidden while the swung one is drawn");
            Assert.IsTrue(chain.gameObject.activeSelf, "and back between sweeps");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BeatingTheEternalVictor_UnlocksHim_AndTheChainJoinsThePool()
        {
            SaveSandbox.Wipe();
            yield return Boot(SlavePit, null);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Asterion), "locked on a fresh save");
            var build = Player.Main.GetComponent<PlayerBuild>();
            UpgradeDef card = null;
            foreach (var u in build.catalog.upgrades) if (u != null && u.id == "weapon_labyrinth_chain") card = u;
            Assert.IsNotNull(card, "the chain's card is in the catalog");
            Assert.IsTrue(build.State.IsLocked(card), "held back until he's free");
            RunState.Current.MarkBossDefeated("eternal_victor", 0.5f);
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards);
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "asterion";
            Assert.IsTrue(got, "the jailer is dust: the chain snaps");
            Assert.IsTrue(SaveSystem.Load(SaveSandbox.TestPath).IsUnlocked("asterion"), "on disk");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Asterion));
            Assert.IsFalse(MetaProgress.IsLocked(card, Meta, MetaProgress.Data), "the chain joins the level-up pool");
        }

        // ---------------------------------------------------------------- the tease at a boss's fall

        /// <summary>The Slave Pit's boss fight with a copy of Gorm's BossDef (tweaked), straight to his fall. Returns
        /// when the fight is in Stage.Falling; `_fallStart` is the real time it got there.</summary>
        IEnumerator BootToFall(System.Action<BossDef> tweak)
        {
            yield return Boot(SlavePit, null, levelUps: true);
            _weapons.RemoveAll(); // no kills, no XP, no level-up cards mid-test
            _fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(_fight, "the Slave Pit has its boss fight");
            if (tweak != null)
            {
                var def = Object.Instantiate(_fight.boss);
                _made.Add(def);
                tweak(def);
                _fight.boss = def;
            }
            _teases = 0;
            _teaseText = null;
            GameEvents.OnAnnounce.Subscribe(OnAnnounce);
            _fight.StartNow();
            float end = Time.time + 8f;
            while (Time.time < end && (BossController.Active == null || BossController.Active.CurrentState == BossController.State.Entering
                                       || BossController.Active.CurrentState == BossController.State.Roaring)) yield return null;
            var boss = BossController.Active;
            Assert.IsNotNull(boss);
            _em.ApplyDamage(boss.Slot, new DamageInfo { Amount = _em.GetMaxHp(boss.Slot) * 2f, Flags = DamageFlags.Melee });
            float until = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < until && _fight.Current != BossFight.Stage.Falling) yield return null;
            Assert.AreEqual(BossFight.Stage.Falling, _fight.Current, "he falls");
            _fallStart = Time.realtimeSinceStartup;
        }

        float _fallStart, _shownAt, _fallEnd;

        /// <summary>Watches the fall to its end (real time): when the reveal showed and when the hoard came.</summary>
        IEnumerator WatchFall()
        {
            _shownAt = -1f;
            float until = _fallStart + 8f;
            while (Time.realtimeSinceStartup < until && _fight.Current == BossFight.Stage.Falling)
            {
                if (_shownAt < 0f && _fight.RevealShown) _shownAt = Time.realtimeSinceStartup;
                yield return null;
            }
            _fallEnd = Time.realtimeSinceStartup;
            Assert.AreNotEqual(BossFight.Stage.Falling, _fight.Current, "the fall ends");
        }

        UnlockDef TestUnlock(bool earned)
        {
            var u = ScriptableObject.CreateInstance<UnlockDef>();
            u.id = "r3_test_tease";
            _made.Add(u);
            var data = MetaProgress.Data;
            data.unlocked.Remove(u.id);
            if (earned) data.unlocked.Add(u.id);
            return u;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheTease_ComesOnceDuringTheFall_WhileItsUnlockIsLocked_AndHoldsTheFall()
        {
            UnlockDef unlock = null;
            yield return BootToFall(d =>
            {
                unlock = TestUnlock(false);
                d.revealText = TeaseText;
                d.revealUnlock = unlock;
                d.revealDelay = 1.2f;
                d.revealHold = 1.8f;
            });
            Assert.IsFalse(_fight.RevealShown, "not at once: the fall line first");
            yield return WatchFall();
            Assert.IsTrue(_fight.RevealShown, "BELOW THE SAND, A CHAIN SNAPS.");
            Assert.AreEqual(1, _teases, "once");
            Assert.AreEqual(TeaseText, _teaseText);
            float delay = _fight.boss.revealDelay, hold = _fight.boss.revealHold;
            Assert.GreaterOrEqual(_shownAt - _fallStart, delay - 0.05f, "after revealDelay (real seconds)");
            Assert.Less(_shownAt - _fallStart, delay + 0.5f);
            Assert.GreaterOrEqual(_fallEnd - _fallStart, _fight.fallSeconds + hold - 0.1f, "the fall is held so it's read");
            Assert.Less(_fallEnd - _fallStart, _fight.fallSeconds + hold + 0.8f);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheTease_StaysQuiet_OnceItsUnlockIsEarned()
        {
            yield return BootToFall(d =>
            {
                d.revealText = TeaseText;
                d.revealUnlock = TestUnlock(true);
                d.revealDelay = 1.2f;
                d.revealHold = 1.8f;
            });
            yield return WatchFall();
            Assert.IsFalse(_fight.RevealShown, "he's already free");
            Assert.AreEqual(0, _teases);
            Assert.GreaterOrEqual(_fallEnd - _fallStart, _fight.fallSeconds - 0.1f);
            Assert.Less(_fallEnd - _fallStart, _fight.fallSeconds + 0.8f, "no hold");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AnOlderBoss_FallsExactlyAsBefore()
        {
            yield return BootToFall(null); // Gorm as he is: no reveal line
            Assert.IsTrue(string.IsNullOrEmpty(_fight.boss.revealText));
            yield return WatchFall();
            Assert.IsFalse(_fight.RevealShown);
            Assert.AreEqual(0, _teases);
            Assert.GreaterOrEqual(_fallEnd - _fallStart, _fight.fallSeconds - 0.1f);
            Assert.Less(_fallEnd - _fallStart, _fight.fallSeconds + 0.8f, "fallSeconds, no longer");
            Assert.AreEqual(BossFight.Stage.Reward, _fight.Current, "then the hoard, as ever");
        }
    }
}
