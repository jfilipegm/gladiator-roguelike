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
    /// Phase 5.13 (docs/ARENAS.md §4): the Grand Arena is a cross bigger than the Stone Amphitheater that keeps the
    /// horde in at its arm ends, arm sides and patron boxes; it's locked until the Beast Lord falls, then a run there is
    /// tier 3 with its own difficulty and a denser horde (C18); a fire-jet row telegraphs, then burns the player (jumping
    /// or not) and the enemies in its strip; a blade sweeper hits a grounded player and mows the horde, and a jump clears
    /// it; the Gilded Mercenary steals gold and gives it back when he dies; the Sorcerer's rune hits a player who stays
    /// and misses one who leaves, and he blinks away when the player closes in.
    /// </summary>
    public class GrandArenaTests
    {
        const string Scene = "Assets/Scenes/Arenas/GrandArena.unity";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static RunDef Grand => Load<RunDef>("Assets/Data/Runs/RD_GrandArena.asset");
        static EnemyDef Enemy(string asset) => Load<EnemyDef>("Assets/Data/Enemies/" + asset + ".asset");

        EnemyManager _em;
        PlayerHealth _health;
        PlayerMotor _motor;
        int _damaged;

        void OnDamaged(in DamagedEvent e) { if (!e.Status) _damaged++; }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, null);

        static IEnumerator LoadMenu()
        {
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
        }

        /// <summary>The arena with no Director, no level-ups, the jets and blades parked: the test stages everything.</summary>
        IEnumerator Boot(bool god)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = god;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().RemoveAll();
            _em = EnemyManager.Instance;
            _em.KillAll();
            _health = Player.Main.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _motor = Player.Main.GetComponent<PlayerMotor>();
            _damaged = 0;
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>Parks every row and blade far in the future (Awake set their clocks; the test fires them itself).</summary>
        static void Park()
        {
            var jets = FireJets.Instance;
            for (int i = 0; jets != null && i < jets.Count; i++) jets.Fire(i); // reset, then push past the cycle below
            var t = typeof(FireJets).GetField("_t", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (jets != null) { var a = (float[])t.GetValue(jets); for (int i = 0; i < a.Length; i++) a[i] = -1e6f; }
            var blades = BladeSweepers.Instance;
            var b = typeof(BladeSweepers).GetField("_t", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (blades != null) { var a = (float[])b.GetValue(blades); for (int i = 0; i < a.Length; i++) a[i] = -1e6f; }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheArena_IsACrossBiggerThanTheAmphitheater_ThatKeepsTheHordeIn()
        {
            var grand = Load<ArenaLayoutDef>("Assets/Data/Arenas/AL_GrandArena.asset");
            var amph = Load<ArenaLayoutDef>("Assets/Data/Arenas/AL_StoneAmphitheater.asset");
            Assert.AreEqual(16, grand.outline.Length, "a cross with its four corners cut: sixteen corners");
            Assert.Greater(grand.PlayArea(), amph.PlayArea() * 1.25f, "every arena is bigger than the one before it");
            Assert.AreEqual(3, grand.tier);
            yield return Boot(true);
            Park();
            Assert.IsTrue(_em.HasOutline);
            // the arms reach 70 m (less the inset); between them the patron boxes cut the corner on the diagonal
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(0f, 0f, 68f)), 1f, "the north arm's end");
            Assert.Greater(_em.EdgeDistance01(new Vector3(0f, 0f, 69.5f)), 1f, "behind the north gate");
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(24f, 0f, 60f)), 1f, "inside the north arm");
            Assert.Greater(_em.EdgeDistance01(new Vector3(28f, 0f, 60f)), 1f, "behind the north arm's east side");
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(31f, 0f, 31f)), 1f, "in front of a patron box");
            Assert.Greater(_em.EdgeDistance01(new Vector3(34f, 0f, 34f)), 1f, "inside a patron box");
            var convict = Enemy("EN_Convict");
            Vector3[] outside = { new Vector3(0f, 0f, 72f), new Vector3(29f, 0f, 55f), new Vector3(36f, 0f, 36f), new Vector3(-72f, 0f, 5f), new Vector3(-20f, 0f, -72f) };
            int[] slots = new int[outside.Length];
            for (int k = 0; k < outside.Length; k++) slots[k] = _em.Spawn(convict, outside[k]);
            yield return new WaitForSeconds(0.3f);
            for (int k = 0; k < slots.Length; k++)
                Assert.LessOrEqual(_em.EdgeDistance01(_em.GetPosition(slots[k])), 1.001f, "pulled back in from " + outside[k]);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LockedUntilTheBeastLordFalls_ThenARunThereIsTier3_WithItsOwnDifficulty_AndADenserHorde()
        {
            SaveSandbox.Wipe();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Grand), "locked on a fresh save");
            Assert.IsFalse(menu.PickArena(Grand));

            MetaProgress.CommitRun(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "beastlord", BossDefeatedHp01 = 0.5f, GladiatorId = "nobody" }, menu.meta);
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Grand), "beating the Beast Lord earns it");
            yield return LoadMenu();
            menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Invoke(menu, "OpenSelect");
            yield return null;
            Invoke(menu, "StartRun");
            yield return null;
            Assert.IsTrue(menu.PickArena(Grand));
            Invoke(menu, "Launch");
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().name != "GrandArena") yield return null;
            yield return null;
            Assert.AreEqual("GrandArena", SceneManager.GetActiveScene().name);
            Assert.AreSame(Grand, GameSession.Run);

            Assert.AreEqual(3, RunState.Current.arenaTier);
            Assert.AreEqual(1.8f, EnemyManager.Instance.hpMul, 1e-4f, "+80 % enemy HP");
            Assert.AreEqual(1.35f, RunState.Current.playerDamageTakenMul, 1e-4f, "+35 % damage taken");
            var director = Director.Instance;
            Assert.AreSame(Load<SpawnTableDef>("Assets/Data/Arenas/ST_GrandArena.asset"), director.table);
            Assert.AreEqual(400, director.maxAliveOverride, "a bigger cap on the biggest floor");
            Assert.AreEqual(1.2f, director.arenaDensity, 1e-4f, "C18: the target curve ×1.2 here");
            Assert.AreEqual(Mathf.RoundToInt(director.config.targetAlive.Evaluate(director.Clock) * 1.2f), director.TargetAlive);
            Assert.AreEqual(4, ArenaGates.Instance.Count, "a gate at each arm's end");
            Assert.AreEqual(4, FireJets.Instance.Count, "a jet row across each arm");
            Assert.AreEqual(4, BladeSweepers.Instance.Count);
            Assert.AreEqual("patron", Object.FindAnyObjectByType<BossFight>().boss.id);

            RunState.Current.End(RunOutcome.Died);
            yield return null;
            Assert.AreEqual(3, MetaProgress.Data.deepestArena, "the save knows the player reached tier 3");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator AFireJetRow_Telegraphs_ThenBurnsThePlayerJumpingOrNot_AndTheHordeInItsStrip()
        {
            yield return Boot(true);
            Park();
            var jets = FireJets.Instance;
            const int north = 0;
            Vector3 c = jets.Center(north);
            var convict = Enemy("EN_Convict");
            int inStrip = _em.Spawn(convict, c + jets.Along(north) * 6f);
            int beside = _em.Spawn(convict, c + jets.Across(north) * 6f);
            var root = Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
            _em.ApplyStatus(inStrip, root, 0f);
            _em.ApplyStatus(beside, root, 0f);
            _motor.Teleport(c - jets.Along(north) * 8f + Vector3.up * 0.1f);
            yield return null;
            _damaged = 0;
            jets.Fire(north);
            Assert.IsTrue(jets.IsTelegraphing(north));
            yield return new WaitForSeconds(jets.telegraph * 0.6f);
            Assert.AreEqual(0, jets.Fired, "nothing burns during the telegraph");
            Assert.AreEqual(0, _damaged);
            _motor.AddImpulse(Vector3.up * 9f); // jump just before it roars: the wall of fire is too tall
            yield return new WaitForSeconds(jets.telegraph * 0.4f + 0.3f);
            Assert.AreEqual(1, jets.Fired);
            Assert.AreEqual(1, jets.PlayerHits, "a jump doesn't clear a wall of fire");
            Assert.GreaterOrEqual(jets.EnemiesHit, 1);
            Assert.IsTrue(!_em.IsAlive(inStrip) || _em.HasStatus(inStrip, StatusKind.Burn), "the convict in the strip burns");
            Assert.AreEqual(_em.GetMaxHp(beside), _em.GetHp(beside), 0.01f, "out of the strip: untouched");
            yield return new WaitForSeconds(jets.burn);
            Assert.IsFalse(jets.IsBurning(north));
            Assert.AreEqual(1, jets.PlayerHits, "one hit per burst");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ABladeSweeper_HitsAGroundedPlayer_MowsTheHorde_AndAJumpClearsIt()
        {
            yield return Boot(true);
            Park();
            var blades = BladeSweepers.Instance;
            Vector3 hub = blades.hubs[0];
            var convict = Enemy("EN_Convict");
            var root = Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
            int a = _em.Spawn(convict, hub + new Vector3(3.5f, 0f, 0f));
            int b = _em.Spawn(convict, hub + new Vector3(-3f, 0f, 1f));
            _em.ApplyStatus(a, root, 0f);
            _em.ApplyStatus(b, root, 0f);
            _motor.Teleport(hub + new Vector3(0f, 0.1f, -3.5f));
            yield return null;
            blades.Spin(0);
            Assert.IsTrue(blades.IsTelegraphing(0));
            yield return new WaitForSeconds(blades.telegraph * 0.8f);
            Assert.AreEqual(0, blades.PlayerHits, "it only creeps round while it warns");
            float end = Time.time + blades.telegraph + 3f;
            while (Time.time < end && (blades.PlayerHits == 0 || blades.EnemiesHit < 2)) yield return null;
            Assert.GreaterOrEqual(blades.PlayerHits, 1, "a grounded player in its circle is hit");
            Assert.GreaterOrEqual(blades.EnemiesHit, 2, "it mows both convicts");

            // jump over it: in the circle, feet held above the blade the whole time (starting in the air: a frame or two
            // on the floor before the first hop let a passing blade hit, about one run in three)
            _motor.Teleport(hub + new Vector3(0f, blades.jumpClear + 0.6f, -3.5f));
            yield return null;
            int hits = blades.PlayerHits;
            end = Time.time + 1.6f;
            while (Time.time < end)
            {
                if (_motor.IsGrounded) _motor.AddImpulse(Vector3.up * 10f); // always airborne within a frame or two
                yield return null;
                // hold the player above the blade the whole time (feet higher than jumpClear)
                Vector3 pp = Player.Main.transform.position;
                if (pp.y - hub.y < blades.jumpClear + 0.2f) _motor.Teleport(new Vector3(pp.x, hub.y + blades.jumpClear + 0.6f, pp.z));
            }
            Assert.AreEqual(hits, blades.PlayerHits, "feet above the blade: it passes under");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheMercenary_StealsGoldWithAHit_AndDropsItWhenHeDies()
        {
            yield return Boot(false);
            Park();
            _health.Heal(1000f);
            var run = RunState.Current;
            run.AddGold(100);
            Vector3 p = Player.Main.transform.position;
            int m = _em.Spawn(Enemy("EN_GildedMercenary"), p + new Vector3(0f, 0f, 3.5f));
            float end = Time.time + 8f;
            while (Time.time < end && _em.GoldStolen == 0) yield return null;
            Assert.Greater(_em.GoldStolen, 0, "his hit pockets gold");
            Assert.LessOrEqual(_em.GoldStolen, 12 * 3);
            Assert.AreEqual(100 - _em.GoldStolen, run.Gold, "the run lost exactly what he took");
            int carried = _em.CarriedGold(m);
            Assert.AreEqual(_em.GoldStolen, carried, "he carries it");
            int before = PickupSystem.Instance.ActiveCount;
            _em.ApplyDamage(m, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(m) });
            yield return null;
            Assert.AreEqual(carried, _em.GoldReturned, "it falls out of his purse");
            Assert.Greater(PickupSystem.Instance.ActiveCount, before, "as coins on the sand");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheSorcerersRune_HitsAPlayerWhoStays_MissesOneWhoLeaves_AndHeBlinksAway()
        {
            yield return Boot(true);
            Park();
            var proj = EnemyProjectiles.Instance;
            Vector3 p = Player.Main.transform.position;
            proj.Rune(p, 0.6f, 2.2f, 16f);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(1, proj.RunesErupted);
            Assert.AreEqual(1, proj.RuneHits, "stood in it");
            p = Player.Main.transform.position;
            proj.Rune(p, 0.6f, 2.2f, 16f);
            yield return null;
            _motor.Teleport(p + new Vector3(4f, 0.1f, 0f));
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(2, proj.RunesErupted);
            Assert.AreEqual(1, proj.RuneHits, "left the circle in time");

            // a sorcerer casts at range; step up to him and he's gone (open sand in the west arm)
            var sorc = Enemy("EN_Sorcerer");
            p = new Vector3(-40f, 0.1f, -10f);
            _motor.Teleport(p);
            yield return null;
            int s = _em.Spawn(sorc, p + new Vector3(0f, 0f, 12f));
            float end = Time.time + 8f;
            while (Time.time < end && proj.RunesErupted < 3) yield return null;
            Assert.GreaterOrEqual(proj.RunesErupted, 3, "he opened a rune under the player from range");
            Vector3 at = _em.GetPosition(s);
            _motor.Teleport(at + new Vector3(0f, 0.1f, -2.5f));
            end = Time.time + 3f;
            while (Time.time < end && _em.Blinks == 0) yield return null;
            Assert.AreEqual(1, _em.Blinks, "he blinked away");
            Vector3 d = _em.GetPosition(s) - Player.Main.transform.position;
            d.y = 0f;
            Assert.Greater(d.magnitude, 6f, "out of reach again");
        }
    }
}
