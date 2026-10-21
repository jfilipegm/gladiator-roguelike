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
    /// Phase 5.8 (docs/ARENAS.md §3): the Stone Amphitheater is a D bigger than the Provincial Pit that keeps the horde
    /// in along its stage wall and its curve; it's locked until the Champion falls, then a run there is tier 2 with its
    /// own difficulty; a cage telegraphs, opens and releases war boars; a boar's charge tramples the horde in its lane
    /// and bowls the player over; the Murmillo blocks from the front; a net catches a standing player, not a jumping one.
    /// </summary>
    public class AmphitheaterTests
    {
        const string Scene = "Assets/Scenes/Arenas/StoneAmphitheater.unity";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static RunDef Amphitheater => Load<RunDef>("Assets/Data/Runs/RD_StoneAmphitheater.asset");
        static EnemyDef Enemy(string asset) => Load<EnemyDef>("Assets/Data/Enemies/" + asset + ".asset");

        EnemyManager _em;
        PlayerHealth _health;
        PlayerMotor _motor;
        int _damaged;

        void OnDamaged(in DamagedEvent e) => _damaged++;

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

        /// <summary>The arena with no Director, no level-ups and no automatic cages: the test stages everything.</summary>
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
            if (BeastCages.Instance != null) BeastCages.Instance.every = 0f;
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

        [UnityTest, Timeout(60000)]
        public IEnumerator TheArena_IsADBiggerThanTheProvincialPit_ThatKeepsTheHordeIn()
        {
            var amph = Load<ArenaLayoutDef>("Assets/Data/Arenas/AL_StoneAmphitheater.asset");
            var prov = Load<ArenaLayoutDef>("Assets/Data/Arenas/AL_ProvincialPit.asset");
            Assert.Greater(amph.outline.Length, 40, "a D: a straight stage wall and a curve, not a circle");
            Assert.Greater(amph.PlayArea(), prov.PlayArea() * 1.25f, "every arena is bigger than the one before it");
            Assert.AreEqual(2, amph.tier);
            yield return Boot(true);
            Assert.IsTrue(_em.HasOutline);
            // due east of the middle the curve has begun (it starts at z 20): x = 58.9 * sqrt(1 - (20 / 62.9)^2)
            Assert.AreEqual(58.9f * Mathf.Sqrt(1f - (20f / 62.9f) * (20f / 62.9f)), _em.EdgePoint(0f, 0f).x, 0.15f);
            // the straight east side north of it: 60 m, less the play inset
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(58.5f, 0f, 30f)), 1f);
            Assert.Greater(_em.EdgeDistance01(new Vector3(59.4f, 0f, 30f)), 1f, "the east side");
            // the stage wall is straight: (50, 42) is inside, (50, 44) behind it; the curve: (40, -40) is beyond it
            Assert.LessOrEqual(_em.EdgeDistance01(new Vector3(50f, 0f, 42f)), 1f);
            Assert.Greater(_em.EdgeDistance01(new Vector3(50f, 0f, 44f)), 1f, "behind the stage wall");
            Assert.Greater(_em.EdgeDistance01(new Vector3(40f, 0f, -40f)), 1f, "behind the seating bowl");
            var convict = Enemy("EN_Convict");
            Vector3[] outside = { new Vector3(10f, 0f, 46f), new Vector3(62f, 0f, 30f), new Vector3(40f, 0f, -40f), new Vector3(0f, 0f, -47f) };
            int[] slots = new int[outside.Length];
            for (int k = 0; k < outside.Length; k++) slots[k] = _em.Spawn(convict, outside[k]);
            yield return new WaitForSeconds(0.3f);
            for (int k = 0; k < slots.Length; k++)
                Assert.LessOrEqual(_em.EdgeDistance01(_em.GetPosition(slots[k])), 1.001f, "pulled back in from " + outside[k]);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LockedUntilTheChampionFalls_ThenTheMenuOffersIt_AndARunThereIsTier2WithItsOwnDifficulty()
        {
            SaveSandbox.Wipe();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Amphitheater), "locked on a fresh save");
            Assert.IsFalse(menu.PickArena(Amphitheater));

            MetaProgress.CommitRun(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "champion", BossDefeatedHp01 = 0.5f, GladiatorId = "nobody" }, menu.meta);
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Amphitheater), "beating the Champion earns it");
            yield return LoadMenu();
            menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsTrue(menu.AsksForArena);
            Invoke(menu, "OpenSelect");
            yield return null;
            Invoke(menu, "StartRun");
            yield return null;
            Assert.IsTrue(menu.PickArena(Amphitheater));
            Invoke(menu, "Launch");
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().name != "StoneAmphitheater") yield return null;
            yield return null;
            Assert.AreEqual("StoneAmphitheater", SceneManager.GetActiveScene().name);
            Assert.AreSame(Amphitheater, GameSession.Run);

            Assert.AreEqual(2, RunState.Current.arenaTier);
            Assert.AreEqual(1.5f, EnemyManager.Instance.hpMul, 1e-4f, "+50 % enemy HP");
            Assert.AreEqual(1.25f, RunState.Current.playerDamageTakenMul, 1e-4f, "+25 % damage taken");
            Assert.AreSame(Load<SpawnTableDef>("Assets/Data/Arenas/ST_StoneAmphitheater.asset"), Director.Instance.table);
            Assert.AreEqual(60f, Director.Instance.eliteEveryOverride, 1e-4f, "elites every 60 s here");
            Assert.IsNotNull(BeastCages.Instance);
            Assert.AreEqual(4, BeastCages.Instance.Count);
            Assert.AreSame(Enemy("EN_WarBoar"), BeastCages.Instance.beast);
            Assert.AreEqual(4, ArenaGates.Instance.Count, "the great gate and three tunnels");
            Assert.AreEqual("beastlord", Object.FindAnyObjectByType<BossFight>().boss.id);

            RunState.Current.End(RunOutcome.Died);
            yield return null;
            Assert.AreEqual(2, MetaProgress.Data.deepestArena, "the save knows the player reached tier 2");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ACage_Telegraphs_Opens_AndReleasesItsPack()
        {
            yield return Boot(true);
            var cages = BeastCages.Instance;
            Assert.IsTrue(cages.Open(1, 3));
            Assert.IsTrue(cages.IsTelegraphing(1), "the lane glows and the bars rattle first");
            Assert.IsFalse(cages.Open(1, 3), "a busy cage can't open again");
            yield return new WaitForSeconds(cages.telegraph * 0.5f);
            Assert.AreEqual(0, cages.Released, "nothing out during the telegraph");
            yield return new WaitForSeconds(cages.telegraph * 0.5f + cages.liftTime + 3f * cages.releaseSpacing + 0.3f);
            Assert.AreEqual(3, cages.Released);
            Assert.AreEqual(3, _em.AliveOf(Enemy("EN_WarBoar")));
            yield return new WaitForSeconds(cages.holdOpen + cages.liftTime + 0.3f);
            Assert.IsFalse(cages.IsBusy(1), "the bars drop again");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ABoarCharge_TramplesTheHordeInItsLane_AndBowlsThePlayerOver()
        {
            yield return Boot(true);
            var boar = Enemy("EN_WarBoar");
            var convict = Enemy("EN_Convict");
            Vector3 p = new Vector3(0f, 0f, -10f);
            _motor.Teleport(p + Vector3.up * 0.1f);
            yield return null;
            int b = _em.Spawn(boar, p + new Vector3(0f, 0f, 13f));
            for (int k = 0; k < 4; k++) _em.Spawn(convict, p + new Vector3(0f, 0f, 4.5f + k * 1.6f));
            float end = Time.time + 6f;
            while (Time.time < end && _em.Trampled < 4) yield return null;
            Assert.GreaterOrEqual(_em.Trampled, 4, "every convict in its lane was hit");
            Assert.AreEqual(0, _em.AliveOf(convict), "3x its hit kills them");
            Assert.IsTrue(_em.IsAlive(b), "it doesn't hurt itself");

            // the same charge at a standing player, nothing in the way
            _em.KillAll();
            yield return null;
            _damaged = 0;
            _motor.Teleport(p + Vector3.up * 0.1f);
            _em.Spawn(boar, p + new Vector3(0f, 0f, 12f));
            end = Time.time + 5f;
            while (Time.time < end && _damaged == 0) yield return null;
            Assert.GreaterOrEqual(_damaged, 1, "the charge hits a player who stands in its lane");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheMurmillo_BlocksFromTheFront_NotFromBehind()
        {
            yield return Boot(true);
            Vector3 p = Player.Main.transform.position;
            int m = _em.Spawn(Enemy("EN_Murmillo"), p + new Vector3(0f, 0f, 7f));
            yield return new WaitForSeconds(0.6f); // turns to face the player
            Vector3 toHim = _em.GetPosition(m) - Player.Main.transform.position;
            toHim.y = 0f;
            toHim.Normalize();
            float hp = _em.GetHp(m);
            _em.ApplyDamage(m, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = toHim }); // from the player's side
            Assert.AreEqual(hp - 10f, _em.GetHp(m), 0.5f, "the scutum takes 90 %");
            hp = _em.GetHp(m);
            _em.ApplyDamage(m, new DamageInfo { Amount = 100f, Flags = DamageFlags.Melee, Direction = -toHim }); // from behind him
            Assert.AreEqual(hp - 100f, _em.GetHp(m), 0.5f, "his back is open");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ANet_CatchesAStandingPlayer_NotAJumpingOne()
        {
            yield return Boot(true);
            var proj = EnemyProjectiles.Instance;
            Vector3 p = Player.Main.transform.position;
            Assert.AreEqual(1f, _motor.SlowMultiplier, 1e-4f);
            proj.LobNet(p + new Vector3(0f, 2f, 9f), p, 0.5f, 1.7f, 6f, 0.1f, 1.2f);
            yield return new WaitForSeconds(0.65f);
            Assert.AreEqual(1, proj.NetsCaught, "caught standing");
            Assert.AreEqual(0.1f, _motor.SlowMultiplier, 1e-4f, "moves at a crawl");
            yield return new WaitForSeconds(1.3f);
            Assert.AreEqual(1f, _motor.SlowMultiplier, 1e-4f, "free after 1.2 s");

            // jump as it comes down: it lands under the player
            p = Player.Main.transform.position;
            proj.LobNet(p + new Vector3(0f, 2f, 9f), p, 0.4f, 1.7f, 6f, 0.1f, 1.2f);
            yield return new WaitForSeconds(0.15f);
            _motor.AddImpulse(Vector3.up * 10f);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, proj.NetsCaught, "a net can't catch you in the air");
            Assert.AreEqual(1f, _motor.SlowMultiplier, 1e-4f);
        }
    }
}
