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
    /// T1 (docs/tracks/T1-playground.md §4): the admin panel's new tools. FREEZE holds every enemy still (no move, turn,
    /// wind-up, shot or touch) while hits still flash and kill and drop XP, and lets go when it's switched off; FIGHT A
    /// BOSS starts the chosen menu run as the gladiator and skin you have on, and that arena brings its boss in ~3 s
    /// after FIGHT!, the real fight (frozen too, if FREEZE is on). No menu list: nothing to fight.
    /// </summary>
    public class AdminToolsTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static RunDef Slice => Load<RunDef>("Assets/Data/Runs/RD_Slice.asset");

        Player _player;
        PlayerHealth _health;
        AdminController _admin;
        EnemyManager _em;
        int _damaged, _kills;

        void OnDamaged(in DamagedEvent e) => _damaged++;
        void OnKill(in KillEvent e) => _kills++;

        IEnumerator Boot(GladiatorDef gladiator = null, SkinDef skin = null)
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = gladiator;
            GladiatorLoadout.NextSkinOverride = skin;
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            _player = Player.Main;
            _health = _player.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _admin = AdminController.Instance;
            _em = EnemyManager.Instance;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll(); // only the test hits the enemies
            Object.FindAnyObjectByType<DebugEnemySpawner>().enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false; // the test places its own enemies
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // no cards pausing the game
            _em.KillAll();
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _damaged = _kills = 0;
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnKill.Unsubscribe(OnKill);
            AdminController.PendingBossFight = false;
            GladiatorLoadout.NextOverride = null;
            GladiatorLoadout.NextSkinOverride = null;
            GameSession.Clear();
            GameSession.KnownRuns = null;
            TimeController.ResetForNewRun();
            DevSettings.ResetAll();
        }

        static IEnumerator WaitRealtime(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        int SpawnAt(EnemyDef def, Vector3 offset)
        {
            _em.Prewarm(def, 4);
            int i = _em.Spawn(def, _player.transform.position + offset);
            Assert.GreaterOrEqual(i, 0, def.id + " should spawn");
            return i;
        }

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [UnityTest, Timeout(60000)]
        public IEnumerator Freeze_EnemiesHoldStill_DontHurt_StillTakeHitsAndDie_ThenMoveAgain()
        {
            yield return Boot();
            var convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            var slinger = Load<EnemyDef>("Assets/Data/Enemies/EN_Slinger.asset");
            var brute = Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");
            Assert.IsNotNull(convict); Assert.IsNotNull(slinger); Assert.IsNotNull(brute);

            _admin.ToggleFreeze();
            Assert.IsTrue(DevSettings.FreezeEnemies, "the toggle switches FREEZE on");
            // touching convicts, one walking in from 6 m, a slinger in its band, a brute in slam range
            var slots = new[]
            {
                SpawnAt(convict, new Vector3(0.6f, 0f, 0f)), SpawnAt(convict, new Vector3(-0.6f, 0f, 0f)),
                SpawnAt(convict, new Vector3(0f, 0f, 0.6f)), SpawnAt(convict, new Vector3(0f, 0f, 6f)),
                SpawnAt(slinger, new Vector3(-8f, 0f, 0f)), SpawnAt(brute, new Vector3(0f, 0f, -2f)),
            };
            int walker = slots[3];
            var pos = new Vector3[slots.Length];
            var yaw = new float[slots.Length];
            for (int k = 0; k < slots.Length; k++) { pos[k] = _em.GetPosition(slots[k]); yaw[k] = _em.GetYaw(slots[k]); }

            yield return new WaitForSeconds(2f);
            for (int k = 0; k < slots.Length; k++)
            {
                int i = slots[k];
                Assert.IsTrue(_em.IsAlive(i), _em.GetDef(i).id + " is still alive");
                Assert.Less(Vector3.Distance(pos[k], _em.GetPosition(i)), 1e-3f, _em.GetDef(i).id + " didn't move");
                Assert.AreEqual(yaw[k], _em.GetYaw(i), 1e-3f, _em.GetDef(i).id + " didn't turn");
                Assert.AreEqual(0, _em.GetAiState(i), _em.GetDef(i).id + " didn't wind up or attack");
            }
            Assert.AreEqual(0, _damaged, "frozen enemies don't touch, slam or shoot the player");
            if (EnemyProjectiles.Instance != null) Assert.AreEqual(0, EnemyProjectiles.Instance.Count, "no shots in the air");

            // a hit still lands: HP drops and it flashes, but the knockback doesn't move it
            int target = slots[0];
            float hp = _em.GetHp(target);
            _em.ApplyDamage(target, new DamageInfo { Amount = 1f, Flags = DamageFlags.Melee, Point = _em.GetPosition(target), Direction = Vector3.right, Knockback = 20f });
            Assert.Less(_em.GetHp(target), hp, "a frozen enemy takes the hit");
            var body = _em.GetViewRoot(target).GetComponentInChildren<MeshRenderer>();
            if (_em.feedback != null) Assert.AreEqual(_em.flashMaterial, body.sharedMaterial, "it flashes");
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector3.Distance(pos[0], _em.GetPosition(target)), 1e-3f, "knockback doesn't move a frozen enemy");
            Assert.AreEqual(_em.bodyMaterial, body.sharedMaterial, "the flash wears off while frozen");

            // and a big one kills it, with its XP
            int gems = PickupSystem.Instance.ActiveCount;
            _em.ApplyDamage(target, new DamageInfo { Amount = 100000f, Flags = DamageFlags.Melee, Point = _em.GetPosition(target), Direction = Vector3.right });
            Assert.IsFalse(_em.IsAlive(target), "a frozen enemy still dies");
            Assert.AreEqual(1, _kills, "its kill counts");
            Assert.Greater(PickupSystem.Instance.ActiveCount, gems, "and it drops its XP");

            // FREEZE off: they come for you again
            _admin.ToggleFreeze();
            Assert.IsFalse(DevSettings.FreezeEnemies);
            Vector3 walkerFrom = _em.GetPosition(walker);
            float before = FlatDistance(walkerFrom, _player.transform.position);
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(FlatDistance(walkerFrom, _em.GetPosition(walker)), 1f, "unfrozen, the convict walks again");
            Assert.Less(FlatDistance(_em.GetPosition(walker), _player.transform.position), before, "toward the player");
            Assert.Greater(_damaged, 0, "and the touching ones hurt again");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FightABoss_StartsTheMenuRun_AsYouAre_AndNothingWithoutAList()
        {
            var knight = Load<GladiatorDef>("Assets/Data/Gladiators/GL_BloodKnight.asset");
            var skin = Load<SkinDef>("Assets/Data/Skins/SK_BloodKnight_NightKnight.asset");
            yield return Boot(knight, skin);
            var loadout = _player.GetComponent<GladiatorLoadout>();
            Assert.AreEqual(knight, loadout.Current, "the test arena runs as the Blood Knight");

            GameSession.KnownRuns = null; // an arena started straight from the Editor: no menu ran
            Assert.AreEqual(0, AdminController.BossRunCount);
            Assert.IsFalse(AdminController.CanFightBoss(0));
            Assert.IsFalse(_admin.FightBoss(0), "no list: nothing to fight");
            Assert.IsFalse(AdminController.PendingBossFight);
            GameSession.KnownRuns = new RunDef[0];
            Assert.IsFalse(_admin.FightBoss(0), "an empty list: nothing to fight");

            var slice = Slice;
            Assert.IsNotNull(slice, "RD_Slice exists");
            Assert.IsNotNull(slice.Boss, "the Slave Pit has a boss");
            GameSession.KnownRuns = new[] { slice };
            GameSession.SuppressLoad = true;
            DevSettings.GodMode = true;
            _admin.SetOpen(true);
            Assert.IsTrue(AdminController.CanFightBoss(0));
            Assert.IsTrue(_admin.FightBoss(0));
            Assert.IsFalse(_admin.IsOpen, "the panel closes");
            Assert.IsTrue(AdminController.PendingBossFight, "the arena it loads will jump to its boss");
            Assert.AreEqual(slice, GameSession.Run);
            Assert.AreEqual(slice.arenas[0].scenePath, GameSession.LastLoadPath, "the Slave Pit would load");
            Assert.AreEqual(knight, GameSession.Gladiator, "as the gladiator you are");
            Assert.AreEqual(loadout.CurrentSkin, GameSession.Skin, "in the skin you wear");
            Assert.IsTrue(DevSettings.GodMode, "god mode stays as it was");

            // an arena without a boss fight still spends the flag (and leaves its clock alone)
            GameSession.SuppressLoad = false;
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            Assert.IsFalse(AdminController.PendingBossFight, "the flag is spent on the next arena's Start");
            Assert.Less(RunState.Current.Elapsed, 1f, "no boss here: the clock isn't touched");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FightABoss_TheArenaBringsItsBoss_AboutThreeSecondsAfterFight_FreezeHoldsHim()
        {
            var knight = Load<GladiatorDef>("Assets/Data/Gladiators/GL_BloodKnight.asset");
            var skin = Load<SkinDef>("Assets/Data/Skins/SK_BloodKnight_NightKnight.asset");
            yield return Boot(knight, skin);
            var skinBefore = _player.GetComponent<GladiatorLoadout>().CurrentSkin;
            var slice = Slice;
            GameSession.KnownRuns = new[] { slice };
            DevSettings.GodMode = true;
            DevSettings.Timeless = true; // the jump switches it off: the clock must run out for him
            string path = slice.arenas[0].scenePath;

            Assert.IsTrue(_admin.FightBoss(0));
            yield return WaitRealtime(() => SceneManager.GetActiveScene().path == path && RunState.Current != null && Player.Main != null, 30f);
            Assert.AreEqual(path, SceneManager.GetActiveScene().path, "the Slave Pit loads");
            yield return null; // its Starts have run
            Assert.IsFalse(AdminController.PendingBossFight, "the arena took the flag");
            Assert.IsFalse(DevSettings.Timeless);
            Assert.IsTrue(DevSettings.GodMode, "god mode survives the load");
            var run = RunState.Current;
            Assert.AreEqual(AdminController.BossJumpSeconds, run.TimeRemaining, 0.05f, "the clock waits at 0:03 behind the intro");
            var loadout = Player.Main.GetComponent<GladiatorLoadout>();
            Assert.AreEqual(knight, loadout.Current, "the same gladiator");
            Assert.AreEqual(skinBefore, loadout.CurrentSkin, "in the same skin");
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().RemoveAll(); // no kills, no level-up cards holding the clock

            var fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(fight, "the Slave Pit has its boss fight");
            yield return WaitRealtime(() => RunIntro.Instance == null || RunIntro.Instance.Current != RunIntro.Stage.Holding, 20f);
            float fightAt = Time.time;
            yield return WaitRealtime(() => fight.Current == BossFight.Stage.Fight, 15f);
            Assert.AreEqual(BossFight.Stage.Fight, fight.Current, "the boss fight begins");
            float took = Time.time - fightAt;
            Assert.That(took, Is.InRange(2f, 5f), "about 3 s after FIGHT!");
            Assert.IsTrue(run.TimerDone);
            Assert.IsFalse(run.Ended, "the run waits for him");
            Assert.AreEqual(slice.Boss, fight.boss);

            // he walks in, roars, then FREEZE holds him too (and he still takes hits)
            float end = Time.time + 10f;
            while (Time.time < end && (BossController.Active == null || BossController.Active.CurrentState == BossController.State.Entering
                                       || BossController.Active.CurrentState == BossController.State.Roaring)) yield return null;
            var boss = BossController.Active;
            Assert.IsNotNull(boss);
            Assert.IsTrue(boss.Alive);
            AdminController.Instance.ToggleFreeze();
            Vector3 at = boss.Position;
            var state = boss.CurrentState;
            yield return new WaitForSeconds(1.5f);
            Assert.Less(Vector3.Distance(at, boss.Position), 1e-3f, "a frozen boss holds still");
            Assert.AreEqual(state, boss.CurrentState, "and his brain waits");
            float hp = boss.Hp01;
            EnemyManager.Instance.ApplyDamage(boss.Slot, new DamageInfo { Amount = 50f, Flags = DamageFlags.Melee });
            Assert.Less(boss.Hp01, hp, "a frozen boss still takes hits");
        }
    }
}
