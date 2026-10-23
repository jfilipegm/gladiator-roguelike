using System;
using System.Collections.Generic;
using System.IO;
using Gladiator.Core;
using Gladiator.Gameplay;
using Gladiator.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gladiator.Tests
{
    /// <summary>
    /// T1 admin mode (docs/tracks/T1-playground.md, W-menu): the switch (always on in the Editor and development builds,
    /// `-admin` for one launch, the typed switch remembered in PlayerPrefs), the typed code (A-D-M-I-N, at most 3 s
    /// between keys), the menu's view (every gladiator and arena pickable while it's on, the save untouched, the locks back
    /// when it's off) and GameSession's Playground flag and arena list. A release build is faked (SimulateRelease) and the
    /// switch lives under a test key, so the player's own switch is never flipped.
    /// </summary>
    public class AdminModeTests
    {
        const string TestKey = "gladiator.admin.editmode-test";

        string _path;
        readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(TestKey);
            AdminMode.ResetTestHooks();
            AdminMode.PrefKeyOverride = TestKey;
            AdminMode.SimulateRelease = true;      // a release build...
            AdminMode.CommandLineOverride = false; // ...launched without -admin
            AdminMode.Reload();
            _path = Path.Combine(Application.temporaryCachePath, "admin_test_save.json");
            DeleteFiles();
            SaveSystem.PathOverride = _path;
            MetaProgress.Reload();
            GameSession.Clear();
            GameSession.SuppressLoad = true;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(TestKey);
            AdminMode.ResetTestHooks();
            GameSession.Clear();
            RunModifiers.Clear();
            SaveSystem.PathOverride = null;
            MetaProgress.Reload();
            DeleteFiles();
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        void DeleteFiles()
        {
            foreach (var f in new[] { _path, _path + ".corrupt", _path + ".tmp" }) if (File.Exists(f)) File.Delete(f);
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        UnlockDef Unlock(string id)
        {
            var u = Make<UnlockDef>();
            u.id = id; u.hint = "Earn it.";
            return u;
        }

        GladiatorDef Gladiator(string id, UnlockDef unlock)
        {
            var g = Make<GladiatorDef>();
            g.id = id; g.displayName = id; g.unlock = unlock;
            return g;
        }

        RunDef Run(string id, string scene, UnlockDef unlock)
        {
            var r = Make<RunDef>();
            r.id = id; r.unlock = unlock;
            r.arenas = new[] { new RunDef.Arena { displayName = id, scenePath = scene } };
            return r;
        }

        // ---------------------------------------------------------------- the switch

        [Test]
        public void ReleaseBuild_OffUntilToggled_ToggleFlipsIt_AndChangedFiresEachTime()
        {
            int changed = 0;
            Action count = () => changed++;
            AdminMode.Changed += count;
            try
            {
                Assert.IsFalse(AdminMode.On, "a release build without -admin or the switch");
                Assert.IsFalse(AdminMode.Forced);
                AdminMode.Toggle();
                Assert.IsTrue(AdminMode.On, "typed once: on");
                Assert.IsTrue(AdminMode.Switched);
                Assert.AreEqual(1, changed);
                Assert.AreEqual("ADMIN MODE ON", AdminMode.StatusText);
                AdminMode.Toggle();
                Assert.IsFalse(AdminMode.On, "typed again: off");
                Assert.AreEqual(2, changed);
                Assert.AreEqual("ADMIN MODE OFF", AdminMode.StatusText);
            }
            finally { AdminMode.Changed -= count; }
        }

        [Test]
        public void TheSwitch_IsRememberedInPlayerPrefs_AcrossLaunches_NotInTheSave()
        {
            AdminMode.Toggle();
            Assert.AreEqual(1, PlayerPrefs.GetInt(TestKey, 0), "saved under its key");
            AdminMode.Reload(); // a new launch reads it back
            Assert.IsTrue(AdminMode.On, "remembered");
            Assert.IsFalse(File.Exists(_path), "the save file isn't written");

            AdminMode.Toggle();
            Assert.AreEqual(0, PlayerPrefs.GetInt(TestKey, 1));
            AdminMode.Reload();
            Assert.IsFalse(AdminMode.On, "off is remembered too");
        }

        [Test]
        public void MinusAdmin_TurnsItOnForTheLaunch_WhateverTheSwitch()
        {
            Assert.IsFalse(AdminMode.FromCommandLine, "the test hook stands in for the command line");
            AdminMode.CommandLineOverride = null; // the real command line (the Editor isn't launched with -admin)
            AdminMode.Reload();
            Assert.AreEqual(CommandLine.Has(AdminMode.CommandLineFlag), AdminMode.FromCommandLine, "reads -admin from the command line");

            AdminMode.CommandLineOverride = true;
            Assert.IsTrue(AdminMode.On, "-admin");
            Assert.IsTrue(AdminMode.Forced);
            Assert.IsFalse(AdminMode.Switched, "the switch is untouched");
            Assert.AreEqual("ADMIN MODE ON (-admin)", AdminMode.StatusText);
            AdminMode.Toggle();
            AdminMode.Toggle();
            Assert.IsTrue(AdminMode.On, "typing can't turn off what -admin turned on");
            AdminMode.CommandLineOverride = false;
            Assert.IsFalse(AdminMode.On, "the next launch without -admin");
        }

        [Test]
        public void TheEditorAndDevBuilds_AlwaysHaveIt()
        {
            AdminMode.SimulateRelease = false;
            Assert.IsTrue(AdminMode.BuildAlwaysOn, "the Editor (Debug.isDebugBuild too)");
            Assert.IsTrue(AdminMode.On);
            Assert.AreEqual("ADMIN MODE ON (DEV BUILD)", AdminMode.StatusText);
            AdminMode.Toggle();
            Assert.IsTrue(AdminMode.Switched, "the switch still flips (for a release launch)");
            Assert.IsTrue(AdminMode.On);
            Assert.AreEqual("ADMIN MODE ON", AdminMode.StatusText);
            AdminMode.Toggle();
            Assert.IsTrue(AdminMode.On, "and stays on");
        }

        // ---------------------------------------------------------------- the typed code

        static bool Type(ref AdminCode code, string keys, float start, float gap)
        {
            bool done = false;
            for (int i = 0; i < keys.Length; i++) done = code.Feed(keys[i], start + i * gap);
            return done;
        }

        [Test]
        public void TheCode_IsADMIN_InOrder_AtMostThreeSecondsBetweenKeys()
        {
            var code = new AdminCode();
            Assert.IsFalse(code.Feed('A', 0f));
            Assert.AreEqual(1, code.Progress);
            Assert.IsFalse(Type(ref code, "DMI", 1f, 1f));
            Assert.AreEqual(4, code.Progress);
            Assert.IsTrue(code.Feed('N', 4f), "the fifth letter completes it");
            Assert.AreEqual(0, code.Progress, "and it starts over");

            code = new AdminCode();
            Assert.IsTrue(Type(ref code, "admin", 10f, 0.1f), "lower case counts");
            code = new AdminCode();
            Assert.IsTrue(Type(ref code, "ADMIN", 20f, AdminMode.CodeGap), "exactly 3 s apart is still in time");

            code = new AdminCode();
            Assert.IsFalse(Type(ref code, "AD", 30f, 1f));
            Assert.IsFalse(Type(ref code, "MIN", 31f + AdminMode.CodeGap + 0.5f, 0.2f), "a pause over 3 s starts over");
            Assert.AreEqual(0, code.Progress);

            code = new AdminCode();
            Assert.IsFalse(Type(ref code, "ADXMIN", 40f, 0.2f), "a wrong key starts over");
            Assert.IsFalse(Type(ref code, "ADM\0IN", 50f, 0.2f), "any other key too");
            code = new AdminCode();
            Assert.IsTrue(Type(ref code, "ADADMIN", 60f, 0.2f), "an A starts again at once");
            code = new AdminCode();
            Assert.IsTrue(Type(ref code, "AAADMIN", 70f, 0.2f));
        }

        // ---------------------------------------------------------------- the menu's view

        [Test]
        public void AdminOn_LockedGladiatorsAndArenas_ArePickable_TheSaveUntouched_AndOffTheyreLockedAgain()
        {
            // a save with some progress, on disk, to compare byte for byte
            var save = new SaveData { renown = 120 };
            save.unlocked.Add("something_else");
            Assert.IsTrue(SaveSystem.Save(save, _path));
            MetaProgress.Reload();
            string before = File.ReadAllText(_path);

            var goblinUnlock = Unlock("unlock_goblin");
            var provincialUnlock = Unlock("unlock_provincial");
            var nobody = Gladiator("nobody", null);
            var goblin = Gladiator("goblin", goblinUnlock);
            var pit = Run("slave_pit", "Assets/Scenes/SlavePit.unity", null);
            var provincial = Run("provincial", "Assets/Scenes/Provincial.unity", provincialUnlock);
            var roster = new[] { goblin, nobody };
            var placeholders = new[] { new MainMenu.Locked { name = "later", hint = "Not yet." } };

            // off: the save's rule
            Assert.IsTrue(MainMenu.IsPickable(nobody));
            Assert.IsFalse(MainMenu.IsPickable(goblin), "locked on this save");
            Assert.IsTrue(MainMenu.IsPickable(pit));
            Assert.IsFalse(MainMenu.IsPickable(provincial));
            var slots = MainMenu.PickSlots(roster, placeholders);
            Assert.AreEqual(3, slots.Length);
            Assert.AreSame(nobody, slots[0].Def, "earned first");
            Assert.IsTrue(slots[0].Open);
            Assert.AreSame(goblin, slots[1].Def);
            Assert.IsFalse(slots[1].Open, "the goblin's tile is locked");
            Assert.IsFalse(slots[2].Open);

            // on: everything that is data can be picked; nothing is earned
            AdminMode.Toggle();
            Assert.IsTrue(MainMenu.IsPickable(goblin), "admin mode: the goblin can be picked");
            Assert.IsTrue(MainMenu.IsPickable(provincial), "and the Provincial Pit");
            Assert.IsFalse(MainMenu.IsAvailable(goblin), "but he isn't earned (the Codex and the Daily Games read this)");
            Assert.IsFalse(MainMenu.IsAvailable(provincial));
            Assert.IsFalse(MainMenu.IsPickable((GladiatorDef)null));
            Assert.IsFalse(MainMenu.IsPickable((RunDef)null));
            slots = MainMenu.PickSlots(roster, placeholders);
            Assert.AreEqual(3, slots.Length);
            Assert.AreSame(goblin, slots[0].Def, "roster order");
            Assert.IsTrue(slots[0].Open);
            Assert.AreSame(nobody, slots[1].Def);
            Assert.IsTrue(slots[1].Open);
            Assert.IsNull(slots[2].Def);
            Assert.IsFalse(slots[2].Open, "a gladiator that isn't data yet stays locked");
            Assert.AreEqual("Not yet.", slots[2].Hint);

            Assert.IsFalse(MetaProgress.Data.IsUnlocked(goblinUnlock.id), "nothing unlocked");
            Assert.IsFalse(MetaProgress.Data.IsUnlocked(provincialUnlock.id));
            Assert.AreEqual(1, MetaProgress.Data.unlocked.Count);
            Assert.AreEqual(120, MetaProgress.Data.renown, "nothing bought");
            Assert.AreEqual(before, File.ReadAllText(_path), "the save file is untouched");

            // off again: the locks are back
            AdminMode.Toggle();
            Assert.IsFalse(MainMenu.IsPickable(goblin));
            Assert.IsFalse(MainMenu.IsPickable(provincial));
            slots = MainMenu.PickSlots(roster, placeholders);
            Assert.AreSame(nobody, slots[0].Def);
            Assert.IsFalse(slots[1].Open, "locked again");
            Assert.AreEqual(before, File.ReadAllText(_path));
        }

        // ---------------------------------------------------------------- GameSession (SuppressLoad: no scene changes)

        [Test]
        public void StartPlayground_SetsPlayground_RetryKeepsIt_StartRunStartDailyAndClearResetIt()
        {
            var nobody = Gladiator("nobody", null);
            var playground = Run("playground", "Assets/Scenes/Dev/Playground.unity", null);
            var pit = Run("slave_pit", "Assets/Scenes/SlavePit.unity", null);

            Assert.IsFalse(GameSession.Playground);
            GameSession.StartPlayground(nobody, playground);
            Assert.IsTrue(GameSession.Playground);
            Assert.AreSame(playground, GameSession.Run);
            Assert.AreSame(nobody, GameSession.Gladiator);
            Assert.AreEqual("Assets/Scenes/Dev/Playground.unity", GameSession.LastLoadPath);
            Assert.AreNotEqual(0u, GameSession.Seed);
            GameSession.Retry();
            Assert.IsTrue(GameSession.Playground, "Retry keeps it");

            GameSession.StartRun(nobody, pit);
            Assert.IsFalse(GameSession.Playground, "a normal run");

            GameSession.StartPlayground(nobody, playground);
            GameSession.StartDaily(new DailyPlan { Date = 20261023, Seed = 7u, Run = pit, Gladiator = nobody, Edicts = Array.Empty<RunModifierDef>() }, false);
            Assert.IsFalse(GameSession.Playground, "the daily");

            GameSession.StartPlayground(nobody, playground);
            GameSession.Clear();
            Assert.IsFalse(GameSession.Playground, "Clear");
        }

        [Test]
        public void KnownRuns_NeverNull_ClearEmptiesIt()
        {
            var pit = Run("slave_pit", "Assets/Scenes/SlavePit.unity", null);
            Assert.IsNotNull(GameSession.KnownRuns);
            Assert.AreEqual(0, GameSession.KnownRuns.Length, "no menu has been up");
            GameSession.KnownRuns = new[] { pit };
            Assert.AreEqual(1, GameSession.KnownRuns.Length);
            Assert.AreSame(pit, GameSession.KnownRuns[0]);
            GameSession.KnownRuns = null;
            Assert.IsNotNull(GameSession.KnownRuns, "null reads as empty");
            Assert.AreEqual(0, GameSession.KnownRuns.Length);
            GameSession.KnownRuns = new[] { pit };
            GameSession.Clear();
            Assert.AreEqual(0, GameSession.KnownRuns.Length, "Clear forgets it");
        }
    }
}
