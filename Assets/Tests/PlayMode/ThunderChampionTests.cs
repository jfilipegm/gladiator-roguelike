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
    /// Phase 5.10 (docs/GLADIATORS.md): the Thunder Champion. He spawns with his model, stats, spear and traits; the
    /// Storm Spear's lightning strikes the nearest enemy and jumps on, but not past its jump range, and Shocks; Charge
    /// raises attack speed and drains after the grace time; the Lightning Dash blinks along the slide, hits the path and
    /// not beside it, recharges, and a wall stops it; beating the Beast Lord unlocks him.
    /// </summary>
    public class ThunderChampionTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string Amphitheater = "Assets/Scenes/Arenas/StoneAmphitheater.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Thunder => Load<GladiatorDef>("Assets/Data/Gladiators/GL_ThunderChampion.asset");
        static EnemyDef Brute => Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");
        static StatusDef Root => Load<StatusDef>("Assets/Data/Status/SE_Root.asset");

        EnemyManager _em;
        WeaponSystem _weapons;
        PlayerStats _stats;

        IEnumerator Boot(string scene, GladiatorDef as_)
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
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            _em = EnemyManager.Instance;
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            _stats = Player.Main.GetComponent<PlayerStats>();
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator HeSpawns_WithHisModel_Stats_Spear_AndTraits()
        {
            yield return Boot(TestArena, Thunder);
            var p = Player.Main.transform;
            // his own model (A1: one mesh per part, so no piece names to look for), rigged
            Assert.IsNotNull(FindDeep(p, Thunder.model.name), "his model, " + Thunder.model.name);
            foreach (var part in new[] { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR" })
                Assert.IsNotNull(FindDeep(p, part), "his " + part);
            Assert.AreEqual(90f, Player.Main.GetComponent<PlayerHealth>().MaxHp, 0.01f);
            Assert.AreEqual(1.05f, _stats.Get(StatId.MoveSpeed), 1e-3f);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual(WeaponPattern.Chain, _weapons.Weapons[0].Def.pattern);
            Assert.IsNotNull(Player.Main.GetComponent<Stormcaller>());
            Assert.IsNotNull(Player.Main.GetComponent<LightningDash>());
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator StormSpear_StrikesTheNearest_JumpsOn_NotPastItsRange_AndShocks()
        {
            yield return Boot(TestArena, Thunder);
            Vector3 p = Player.Main.transform.position;
            int a = _em.Spawn(Brute, p + new Vector3(4f, 0f, 0f));   // the nearest: struck first
            int b = _em.Spawn(Brute, p + new Vector3(8f, 0f, 0f));   // 4 m on: a jump
            int c = _em.Spawn(Brute, p + new Vector3(12f, 0f, 0f));  // 4 m on: a jump
            int far = _em.Spawn(Brute, p + new Vector3(22f, 0f, 0f)); // 10 m past c: out of jump range
            var root = Root;
            int strikes = _weapons.ChainStrikes;
            float end = Time.time + 4f;
            while (Time.time < end && _weapons.ChainStrikes < strikes + 3)
            {
                foreach (int i in new[] { a, b, c, far }) if (_em.IsAlive(i)) _em.ApplyStatus(i, root, 0f);
                yield return null;
            }
            Assert.GreaterOrEqual(_weapons.ChainStrikes, strikes + 3, "one bolt, three strikes");
            foreach (int i in new[] { a, b, c })
            {
                Assert.Less(_em.GetHp(i), _em.GetMaxHp(i), "struck");
                Assert.IsTrue(_em.HasStatus(i, StatusKind.Shock), "and Shocked");
            }
            Assert.AreEqual(_em.GetMaxHp(far), _em.GetHp(far), 0.01f, "too far to jump to");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Charge_RaisesAttackSpeed_AndDrainsAfterTheGrace()
        {
            yield return Boot(TestArena, Thunder);
            _weapons.RemoveAll();
            var s = Player.Main.GetComponent<Stormcaller>();
            float baseSpeed = _stats.Get(StatId.AttackSpeed);
            Assert.AreEqual(0f, s.Charge);
            int i = _em.Spawn(Brute, Player.Main.transform.position + new Vector3(0f, 0f, 6f));
            yield return null;
            _em.ApplyDamage(i, new DamageInfo { Amount = 1f, Flags = DamageFlags.Melee, Point = _em.GetPosition(i) });
            Assert.AreEqual(1f, s.Charge, 1e-3f, "a hit is a Charge");
            _em.ApplyDamage(i, new DamageInfo { Amount = 1f, Flags = DamageFlags.Status, Point = _em.GetPosition(i) });
            Assert.AreEqual(1f, s.Charge, 1e-3f, "a status tick isn't");

            s.SetCharge(20f);
            Assert.AreEqual(baseSpeed * (1f + 20f * s.trait.attackSpeedPerCharge), _stats.Get(StatId.AttackSpeed), 1e-3f, "+30 % attack speed");
            yield return new WaitForSeconds(s.trait.graceSeconds * 0.6f);
            Assert.AreEqual(20f, s.Charge, 1e-3f, "kept while the grace lasts");
            yield return new WaitForSeconds(s.trait.graceSeconds * 0.4f + 20f / s.trait.drainPerSecond + 0.3f);
            Assert.AreEqual(0f, s.Charge, 1e-3f, "drained");
            Assert.AreEqual(baseSpeed, _stats.Get(StatId.AttackSpeed), 1e-3f, "and the bonus is gone");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator LightningDash_BlinksAlongTheSlide_HitsThePath_NotBeside_AndRecharges()
        {
            yield return Boot(TestArena, Thunder);
            _weapons.RemoveAll();
            var dash = Player.Main.GetComponent<LightningDash>();
            Vector3 p = Player.Main.transform.position;
            int onPath = _em.Spawn(Brute, p + new Vector3(3.5f, 0f, 0f));
            int beside = _em.Spawn(Brute, p + new Vector3(3.5f, 0f, 3.2f));
            int past = _em.Spawn(Brute, p + new Vector3(11f, 0f, 0f));
            yield return null;
            Assert.IsTrue(dash.Ready);
            Assert.IsTrue(dash.Perform(Vector3.right));
            Vector3 moved = dash.LastTo - dash.LastFrom;
            moved.y = 0f;
            Assert.AreEqual(dash.trait.distance, moved.magnitude, 0.3f, "a blink along the slide");
            Assert.Less(_em.GetHp(onPath), _em.GetMaxHp(onPath), "the one on the path is struck");
            Assert.IsTrue(_em.HasStatus(onPath, StatusKind.Shock), "and Shocked");
            Assert.AreEqual(_em.GetMaxHp(beside), _em.GetHp(beside), 0.01f, "the one beside the path isn't");
            Assert.AreEqual(_em.GetMaxHp(past), _em.GetHp(past), 0.01f, "nor the one past its end");
            Assert.IsFalse(dash.Ready);
            Assert.IsFalse(dash.Perform(Vector3.right), "recharging");
            yield return new WaitForSeconds(dash.trait.cooldown + 0.1f);
            Assert.IsTrue(dash.Ready, "ready again");
            GameEvents.OnSlide.Raise(new SlideEvent { Position = Player.Main.transform.position, Velocity = Vector3.forward * 8f });
            Assert.AreEqual(2, dash.Dashes, "a slide is a dash");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AWall_StopsTheBlink()
        {
            yield return Boot(TestArena, Thunder);
            _weapons.RemoveAll();
            var dash = Player.Main.GetComponent<LightningDash>();
            Vector3 p = Player.Main.transform.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = p + new Vector3(2.5f, 1.5f, 0f);
            wall.transform.localScale = new Vector3(0.5f, 3f, 6f);
            Physics.SyncTransforms();
            yield return null;
            Assert.IsTrue(dash.Perform(Vector3.right));
            Vector3 moved = dash.LastTo - dash.LastFrom;
            moved.y = 0f;
            Assert.Less(moved.magnitude, 2.4f, "stopped at the wall");
            Object.Destroy(wall);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BeatingTheBeastLord_UnlocksHim()
        {
            SaveSandbox.Wipe();
            yield return Boot(Amphitheater, null);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Thunder), "locked on a fresh save");
            RunState.Current.MarkBossDefeated("beastlord", 0.5f);
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards);
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "thunder_champion";
            Assert.IsTrue(got, "Arena 3 complete: the storm god was watching");
            Assert.IsTrue(SaveSystem.Load(SaveSandbox.TestPath).IsUnlocked("thunder_champion"), "on disk");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Thunder));
        }
    }
}
