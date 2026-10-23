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
    /// Phase 5.14 (docs/GLADIATORS.md): the Net-Weaver. He spawns with his model, stats, trident and traits; the
    /// Trident's flurry hits one enemy three times up close and spreads across three at range, missing one outside the
    /// fan; Caught adds 50 % against a rooted enemy; the Cast Net hauls a group to its landing point and roots it, not
    /// again inside the cooldown, and only slows a boss; a won run holding the Spear unlocks him.
    /// </summary>
    public class NetWeaverTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Weaver => Load<GladiatorDef>("Assets/Data/Gladiators/GL_NetWeaver.asset");
        static EnemyDef Brute => Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");
        static EnemyDef Convict => Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
        static EnemyDef Gorm => Load<EnemyDef>("Assets/Data/Enemies/EN_Gorm.asset");
        static StatusDef Root => Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
        static StatusDef Slow => Load<StatusDef>("Assets/Data/Status/SE_Slow.asset");
        static WeaponDef Spear => Load<WeaponDef>("Assets/Data/Weapons/WP_Spear.asset");

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

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        [UnityTest, Timeout(60000)]
        public IEnumerator HeSpawns_WithHisModel_Stats_Trident_AndTraits()
        {
            yield return Boot(TestArena, Weaver);
            var p = Player.Main.transform;
            // his own model (A1: one mesh per part, so no piece names to look for), rigged
            Assert.IsNotNull(FindDeep(p, Weaver.model.name), "his model, " + Weaver.model.name);
            foreach (var part in new[] { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR" })
                Assert.IsNotNull(FindDeep(p, part), "his " + part);
            Assert.AreEqual(95f, Player.Main.GetComponent<PlayerHealth>().MaxHp, 0.01f);
            Assert.AreEqual(1.05f, _stats.Get(StatId.MoveSpeed), 1e-3f);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual(WeaponPattern.MultiHit, _weapons.Weapons[0].Def.pattern);
            Assert.IsNotNull(Player.Main.GetComponent<CastNet>());
            Assert.AreEqual(0.5f, _em.BonusVsStatus[Root.Slot], 1e-4f, "Caught: +50 % against the rooted");
            Assert.AreEqual(0.25f, _em.BonusVsStatus[Slow.Slot], 1e-4f, "and +25 % against the slowed");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Flurry_CloseUp_HitsOneEnemyThreeTimes()
        {
            yield return Boot(TestArena, Weaver);
            Vector3 p = Player.Main.transform.position;
            int a = _em.Spawn(Brute, p + new Vector3(1.6f, 0f, 0f));
            int prongs = _weapons.Prongs, hits = _weapons.Hits;
            float end = Time.time + 4f;
            while (Time.time < end && _weapons.Prongs < prongs + 3)
            {
                if (_em.IsAlive(a)) _em.ApplyStatus(a, Root, 0f); // held still
                yield return null;
            }
            Assert.AreEqual(prongs + 3, _weapons.Prongs, "one flurry: three jabs");
            Assert.AreEqual(hits + 3, _weapons.Hits, "all three land on the one in front of him");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Flurry_AtRange_SpreadsAcrossTheFan_AndMissesOneOutsideIt()
        {
            yield return Boot(TestArena, Weaver);
            Vector3 p = Player.Main.transform.position;
            float half = _weapons.Weapons[0].Def.arcDegrees * 0.5f;
            int centre = _em.Spawn(Convict, p + new Vector3(3.7f, 0f, 0f)); // the nearest: the aim
            int left = _em.Spawn(Convict, p + Quaternion.Euler(0f, -half, 0f) * Vector3.right * 4f);
            int right = _em.Spawn(Convict, p + Quaternion.Euler(0f, half, 0f) * Vector3.right * 4f);
            int outside = _em.Spawn(Convict, p + Quaternion.Euler(0f, 55f, 0f) * Vector3.right * 4f);
            var all = new[] { centre, left, right, outside };
            int prongs = _weapons.Prongs;
            float end = Time.time + 4f;
            while (Time.time < end && _weapons.Prongs < prongs + 3)
            {
                foreach (int i in all) if (_em.IsAlive(i)) _em.ApplyStatus(i, Root, 0f);
                yield return null;
            }
            Assert.AreEqual(prongs + 3, _weapons.Prongs);
            foreach (int i in new[] { centre, left, right })
                Assert.IsTrue(!_em.IsAlive(i) || _em.GetHp(i) < _em.GetMaxHp(i), "each prong finds its own enemy");
            Assert.IsTrue(_em.IsAlive(outside), "outside the fan");
            Assert.AreEqual(_em.GetMaxHp(outside), _em.GetHp(outside), 0.01f, "untouched");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Caught_AddsHalfAgainstTheRooted()
        {
            yield return Boot(TestArena, Weaver);
            _weapons.RemoveAll();
            Vector3 p = Player.Main.transform.position;
            int free = _em.Spawn(Brute, p + new Vector3(0f, 0f, 8f));
            int held = _em.Spawn(Brute, p + new Vector3(0f, 0f, -8f));
            yield return null;
            _em.ApplyStatus(held, Root, 0f);
            float f0 = _em.GetHp(free), h0 = _em.GetHp(held);
            _em.ApplyDamage(free, new DamageInfo { Amount = 20f, Flags = DamageFlags.Melee, Point = _em.GetPosition(free) });
            _em.ApplyDamage(held, new DamageInfo { Amount = 20f, Flags = DamageFlags.Melee, Point = _em.GetPosition(held) });
            Assert.AreEqual(20f, f0 - _em.GetHp(free), 0.01f);
            Assert.AreEqual(30f, h0 - _em.GetHp(held), 0.01f, "caught: +50 %");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CastNet_HaulsAGroupTogether_AndRootsIt_NotAgainInsideTheCooldown()
        {
            yield return Boot(TestArena, Weaver);
            _weapons.RemoveAll();
            var net = Player.Main.GetComponent<CastNet>();
            Vector3 p = Player.Main.transform.position;
            Vector3 land = p + Vector3.right * net.trait.throwDistance;
            var caught = new[]
            {
                _em.Spawn(Convict, land + new Vector3(2.6f, 0f, 0f)),
                _em.Spawn(Convict, land + new Vector3(0f, 0f, 2.6f)),
                _em.Spawn(Convict, land + new Vector3(0f, 0f, -2.6f)),
            };
            int away = _em.Spawn(Convict, land + new Vector3(0f, 0f, 7f));
            yield return null;
            Assert.IsTrue(net.Ready);
            Assert.IsTrue(net.Perform(Vector3.right));
            Assert.IsTrue(net.InFlight, "it flies first");
            Assert.IsFalse(net.Perform(Vector3.right), "one net at a time: recharging");
            yield return new WaitForSeconds(net.trait.flightTime + 0.6f);
            Assert.AreEqual(3, net.Hauled, "three caught");
            foreach (int i in caught)
            {
                Assert.IsTrue(_em.HasStatus(i, StatusKind.Root), "rooted");
                Assert.Less(Flat(_em.GetPosition(i) - net.LastLanding).magnitude, 1.7f, "hauled toward the middle");
            }
            Assert.IsFalse(_em.HasStatus(away, StatusKind.Root), "the one outside the net is free");
            yield return new WaitForSeconds(net.trait.cooldown);
            Assert.IsTrue(net.Ready, "ready again");
            GameEvents.OnSlide.Raise(new SlideEvent { Position = Player.Main.transform.position, Velocity = Vector3.forward * 8f });
            Assert.AreEqual(2, net.Casts, "a slide casts it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CastNet_OnlySlowsABoss()
        {
            yield return Boot(TestArena, Weaver);
            _weapons.RemoveAll();
            var net = Player.Main.GetComponent<CastNet>();
            Vector3 land = Player.Main.transform.position + Vector3.right * net.trait.throwDistance;
            int boss = _em.Spawn(Gorm, land + new Vector3(1.5f, 0f, 0f)); // a scripted body: nothing drives it here
            yield return null;
            Vector3 at = _em.GetPosition(boss);
            Assert.IsTrue(net.Perform(Vector3.right));
            yield return new WaitForSeconds(net.trait.flightTime + 0.4f);
            Assert.IsFalse(_em.HasStatus(boss, StatusKind.Root), "a boss can't be held");
            Assert.IsTrue(_em.HasStatus(boss, StatusKind.Slow), "only slowed (and so Caught)");
            Assert.Less(Flat(_em.GetPosition(boss) - at).magnitude, 0.05f, "and not hauled");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WinningARunWithTheSpear_UnlocksHim()
        {
            SaveSandbox.Wipe();
            yield return Boot(SlavePit, null);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Weaver), "locked on a fresh save");
            _weapons.Equip(Spear);
            RunState.Current.MarkBossDefeated("gorm", 0.5f);
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards);
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "net_weaver";
            Assert.IsTrue(got, "a win with a spear in hand: the fisherman took note");
            Assert.IsTrue(SaveSystem.Load(SaveSandbox.TestPath).IsUnlocked("net_weaver"), "on disk");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Weaver));
        }
    }
}
