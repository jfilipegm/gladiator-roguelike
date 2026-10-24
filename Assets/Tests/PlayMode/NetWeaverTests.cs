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
    /// Trident's lunge (note 34) skewers a big enemy on all three prongs and strikes three parallel lanes at range,
    /// missing one beside them and one beyond its reach; Caught adds 50 % against a rooted enemy; the Cast Net hauls a
    /// group to its landing point and roots it, not again inside the cooldown, and only slows a boss; a slide's net goes
    /// for the crowd, not the empty sand ahead (note 34), and lands ahead with nobody in reach; a lying net holds what it
    /// caught, snares what walks on, and lets go when it's gone; a won run holding the Spear unlocks him.
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
        public IEnumerator Lunge_CloseUp_SkewersABruteOnAllThreeProngs()
        {
            yield return Boot(TestArena, Weaver);
            Vector3 p = Player.Main.transform.position;
            int a = _em.Spawn(Brute, p + new Vector3(1.6f, 0f, 0f));
            var pp = PlayerProjectiles.Instance;
            int prongs = _weapons.Prongs, hits = _weapons.Hits, lunges = pp.Lunges;
            float end = Time.time + 4f;
            while (Time.time < end && _weapons.Prongs < prongs + 3)
            {
                if (_em.IsAlive(a)) _em.ApplyStatus(a, Root, 0f); // held still
                yield return null;
            }
            Assert.AreEqual(prongs + 3, _weapons.Prongs, "one lunge: three prongs at once");
            Assert.AreEqual(hits + 3, _weapons.Hits, "a brute is wide enough for all three");
            Assert.AreEqual(lunges + 1, pp.Lunges, "one trident lunging out of his hands, not three spears");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Lunge_StrikesThreeParallelLanes_AndMissesOneBesideThem_AndOneBeyond()
        {
            yield return Boot(TestArena, Weaver);
            Vector3 p = Player.Main.transform.position;
            var w = _weapons.Weapons[0];
            float area = _weapons.AreaOf(w), root = Mathf.Sqrt(area);
            float spacing = w.Def.prongSpacing * root, length = w.Def.range * area, half = w.Def.width * 0.5f * root;
            int centre = _em.Spawn(Convict, p + new Vector3(2f, 0f, 0f)); // the nearest: the aim, on the middle lane
            int left = _em.Spawn(Convict, p + new Vector3(3f, 0f, spacing)); // on an outer lane, further out
            int right = _em.Spawn(Convict, p + new Vector3(3.4f, 0f, -spacing));
            float r = _em.GetRadius(centre);
            int beside = _em.Spawn(Convict, p + new Vector3(2.6f, 0f, spacing + half + r + 0.6f)); // clear of every lane
            int beyond = _em.Spawn(Convict, p + new Vector3(length + r + 1f, 0f, 0f)); // past the middle prong's reach
            var all = new[] { centre, left, right, beside, beyond };
            int prongs = _weapons.Prongs;
            float end = Time.time + 4f;
            while (Time.time < end && _weapons.Prongs < prongs + 3)
            {
                foreach (int i in all) if (_em.IsAlive(i)) _em.ApplyStatus(i, Root, 0f);
                yield return null;
            }
            Assert.AreEqual(prongs + 3, _weapons.Prongs, "one lunge");
            foreach (int i in new[] { centre, left, right })
                Assert.IsTrue(!_em.IsAlive(i) || _em.GetHp(i) < _em.GetMaxHp(i), "each lane strikes what's on it");
            foreach (int i in new[] { beside, beyond })
            {
                Assert.IsTrue(_em.IsAlive(i), "off the lanes");
                Assert.AreEqual(_em.GetMaxHp(i), _em.GetHp(i), 0.01f, "untouched");
            }
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
            Assert.IsTrue(net.CastAt(land));
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
            Assert.IsTrue(net.CastAt(land));
            yield return new WaitForSeconds(net.trait.flightTime + 0.4f);
            Assert.IsFalse(_em.HasStatus(boss, StatusKind.Root), "a boss can't be held");
            Assert.IsTrue(_em.HasStatus(boss, StatusKind.Slow), "only slowed (and so Caught)");
            Assert.Less(Flat(_em.GetPosition(boss) - at).magnitude, 0.05f, "and not hauled");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CastNet_ASlidesNetGoesForTheCrowd_NotTheEmptySandAhead()
        {
            yield return Boot(TestArena, Weaver);
            _weapons.RemoveAll();
            _em.DespawnAll(); // nobody about
            var net = Player.Main.GetComponent<CastNet>();
            Vector3 p = Player.Main.transform.position;
            Assert.IsTrue(net.Perform(Vector3.right), "nobody in reach: it still goes");
            Assert.Less(Flat(net.LastLanding - (p + Vector3.right * net.trait.throwDistance)).magnitude, 0.05f, "throwDistance ahead along the slide");
            yield return new WaitForSeconds(net.trait.flightTime + 0.1f);

            // a crowd behind him and to his left; he slides away from it, to the right
            Vector3 crowd = p + new Vector3(-4f, 0f, 3f);
            var group = new[]
            {
                _em.Spawn(Convict, crowd), _em.Spawn(Convict, crowd + new Vector3(1f, 0f, 0f)), _em.Spawn(Convict, crowd + new Vector3(-1f, 0f, 0f)),
                _em.Spawn(Convict, crowd + new Vector3(0f, 0f, 1f)), _em.Spawn(Convict, crowd + new Vector3(0f, 0f, -1f)),
            };
            int stray = _em.Spawn(Convict, p + new Vector3(5f, 0f, -5f)); // a lone one the slide's way
            yield return null;
            foreach (int i in group) _em.ApplyStatus(i, Root, 0f); // the crowd stands where it is until the net lands
            Vector3 mean = Vector3.zero;
            foreach (int i in group) mean += _em.GetPosition(i);
            mean /= group.Length;
            net.Recharge();
            int hauled = net.Hauled;
            GameEvents.OnSlide.Raise(new SlideEvent { Position = Player.Main.transform.position, Velocity = Vector3.right * 8f });
            Assert.Less(Flat(net.LastLanding - mean).magnitude, 1f, "on the middle of the crowd");
            yield return new WaitForSeconds(net.trait.flightTime + 0.2f);
            Assert.GreaterOrEqual(net.Hauled - hauled, 4, "and it catches the crowd");
            Assert.IsFalse(_em.HasStatus(stray, StatusKind.Root), "not the lone one ahead");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CastNet_HoldsWhatItCaught_AndSnaresWhatWalksOn_UntilItIsGone()
        {
            yield return Boot(TestArena, Weaver);
            _weapons.RemoveAll();
            _em.DespawnAll();
            var net = Player.Main.GetComponent<CastNet>();
            Vector3 land = Player.Main.transform.position + Vector3.right * 6f;
            int held = _em.Spawn(Brute, land + new Vector3(1f, 0f, 0f));
            yield return null;
            Assert.IsTrue(net.CastAt(land));
            yield return new WaitForSeconds(net.trait.flightTime + 0.05f);
            Assert.IsTrue(net.Holding, "it lies on the sand");
            Assert.IsTrue(_em.HasStatus(held, StatusKind.Root), "caught when it landed");
            int walker = _em.Spawn(Brute, land + new Vector3(0f, 0f, -2f)); // steps onto the lying net
            Assert.IsFalse(_em.HasStatus(walker, StatusKind.Root));
            yield return new WaitForSeconds(net.trait.snareInterval + 0.1f);
            Assert.IsTrue(_em.HasStatus(walker, StatusKind.Root), "snared");
            Assert.GreaterOrEqual(net.Snared, 1);
            float rootTime = Root.duration;
            yield return new WaitForSeconds(rootTime + 0.1f); // past the first catch's own Root: the net still holds it
            Assert.IsTrue(net.Holding);
            Assert.IsTrue(_em.HasStatus(held, StatusKind.Root), "held while the net lies there");
            while (net.Holding) yield return null;
            yield return new WaitForSeconds(0.15f);
            Assert.IsFalse(_em.HasStatus(held, StatusKind.Root), "the net is gone: so is the hold");
            Assert.IsFalse(_em.HasStatus(walker, StatusKind.Root));
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
