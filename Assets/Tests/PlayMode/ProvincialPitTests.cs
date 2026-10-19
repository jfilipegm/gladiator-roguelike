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
    /// Phase 5.4 (docs/ARENAS.md): the Provincial Pit is locked until Gorm falls, then the menu asks which arena and a
    /// run there loads it with its own difficulty and records arena tier 1; spike grates telegraph, then hurt whoever
    /// stands on them (a jump clears them); barricades block the horde until something smashes them; a toppled brazier
    /// leaves burning ground.
    /// </summary>
    public class ProvincialPitTests
    {
        const string Pit = "Assets/Scenes/Arenas/ProvincialPit.unity";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static RunDef Provincial => Load<RunDef>("Assets/Data/Runs/RD_ProvincialPit.asset");
        static RunDef Slice => Load<RunDef>("Assets/Data/Runs/RD_Slice.asset");

        EnemyManager _em;
        EnemyDef _convict;
        PlayerHealth _health;
        PlayerMotor _motor;
        StatusDef _root;

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(target, null);

        static string ScreenOf(Gladiator.UI.MainMenu menu) =>
            menu.GetType().GetField("_screen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu).ToString();

        static IEnumerator LoadMenu()
        {
            var op = SceneManager.LoadSceneAsync(GameSession.MenuScene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
        }

        IEnumerator Boot(bool god)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = god;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Pit, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            _em = EnemyManager.Instance;
            _health = Player.Main.GetComponent<PlayerHealth>();
            _motor = Player.Main.GetComponent<PlayerMotor>();
            _convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            _root = Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
            yield return new WaitForSeconds(0.2f);
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        [UnityTest, Timeout(120000)]
        public IEnumerator LockedUntilGormFalls_ThenTheMenuAsks_AndARunThereIsTier1WithItsOwnDifficulty()
        {
            SaveSandbox.Wipe();
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Provincial), "locked on a fresh save");
            Assert.IsFalse(menu.AsksForArena, "one arena: FIGHT! goes straight in");
            Assert.IsFalse(menu.PickArena(Provincial));

            MetaProgress.CommitRun(new RunSummary { Outcome = RunOutcome.Survived, Elapsed = 480f, BossDefeated = "gorm", BossDefeatedHp01 = 0.6f, GladiatorId = "nobody" }, menu.meta);
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Provincial), "beating Gorm earns it");
            yield return LoadMenu();
            menu = Object.FindAnyObjectByType<Gladiator.UI.MainMenu>();
            Assert.IsTrue(menu.AsksForArena);
            Invoke(menu, "OpenSelect");
            yield return null;
            Invoke(menu, "StartRun");
            yield return null;
            Assert.AreEqual("Arena", ScreenOf(menu), "FIGHT! on the gladiator screen asks which arena");
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
            Assert.IsTrue(menu.PickArena(Provincial));
            Assert.AreSame(Provincial, menu.SelectedRun);
            Invoke(menu, "Launch");
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && SceneManager.GetActiveScene().name != "ProvincialPit") yield return null;
            yield return null;
            Assert.AreEqual("ProvincialPit", SceneManager.GetActiveScene().name);
            Assert.AreSame(Provincial, GameSession.Run);

            Assert.AreEqual(1, RunState.Current.arenaTier);
            Assert.AreEqual(1.25f, EnemyManager.Instance.hpMul, 1e-4f, "+25 % enemy HP");
            Assert.AreEqual(1.15f, RunState.Current.playerDamageTakenMul, 1e-4f, "+15 % damage taken");
            Assert.AreSame(Load<SpawnTableDef>("Assets/Data/Arenas/ST_ProvincialPit.asset"), Director.Instance.table);
            Assert.IsNotNull(SpikeTraps.Instance);
            Assert.AreEqual(6, SpikeTraps.Instance.traps.Length);
            Assert.IsNotNull(ArenaBreakables.Instance);
            Assert.AreEqual(8, ArenaBreakables.Instance.barricades.Length);
            Assert.AreEqual(4, ArenaBreakables.Instance.braziers.Length);

            var convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            int i = EnemyManager.Instance.Spawn(convict, Player.Main.transform.position + new Vector3(0f, 0f, 12f));
            int hp = Mathf.RoundToInt(EnemyManager.Instance.GetMaxHp(i));
            Assert.AreEqual(Mathf.RoundToInt(convict.maxHp * 1.25f), hp, "spawned with the arena's HP");

            RunState.Current.End(RunOutcome.Died);
            yield return null;
            Assert.AreEqual(1, MetaProgress.Data.deepestArena, "the save knows the player reached tier 1");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator SpikeTraps_Telegraph_ThenHurtEveryoneOnTheGrate_OncePerRise_AndAJumpClearsThem()
        {
            yield return Boot(false);
            var traps = SpikeTraps.Instance;
            Assert.IsNotNull(traps);

            // enemies: the horde dies on them (ordinary ones take double); the player stands well away
            _motor.Teleport(traps.traps[3] + Vector3.up * 0.1f);
            Vector3 g = traps.traps[2];
            int a = _em.Spawn(_convict, g + new Vector3(0.3f, 0f, 0f));
            int b = _em.Spawn(_convict, g + new Vector3(-0.3f, 0f, 0.2f));
            int far = _em.Spawn(_convict, g + new Vector3(3f, 0f, 0f));
            traps.Fire(2);
            Assert.IsTrue(traps.IsTelegraphing(2));
            int fired = traps.Fired, hitBefore = traps.EnemiesHit;
            float end = Time.time + 2f;
            while (Time.time < end && !traps.IsUp(2))
            {
                foreach (int k in new[] { a, b, far }) if (_em.IsAlive(k)) _em.ApplyStatus(k, _root, 0f);
                yield return null;
            }
            yield return null;
            Assert.IsTrue(traps.IsUp(2), "spikes up after the telegraph");
            Assert.Greater(traps.Fired, fired);
            Assert.GreaterOrEqual(traps.EnemiesHit - hitBefore, 2);
            Assert.IsFalse(_em.IsAlive(a), "an ordinary convict dies on the spikes");
            Assert.IsFalse(_em.IsAlive(b));
            Assert.IsTrue(_em.IsAlive(far), "off the grate: untouched");
            Assert.AreEqual(_em.GetMaxHp(far), _em.GetHp(far), 1e-3f);

            // the player: hurt once per rise, through the arena's damage multiplier
            Vector3 c = traps.traps[0];
            _motor.Teleport(c + Vector3.up * 0.1f);
            yield return new WaitForSeconds(0.3f);
            traps.Fire(0);
            float hp0 = _health.Hp;
            int hits = traps.PlayerHits;
            end = Time.time + 2f;
            while (Time.time < end && !traps.IsUp(0)) yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(hits + 1, traps.PlayerHits, "stood on it: hit");
            Assert.AreEqual(traps.playerDamage * 1.15f, hp0 - _health.Hp, 0.6f);
            float hp1 = _health.Hp;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(hits + 1, traps.PlayerHits, "once per rise");
            Assert.AreEqual(hp1, _health.Hp, 1e-3f);

            // a jump clears them: high over the grate the frame they rise
            c = traps.traps[1];
            _motor.Teleport(c + Vector3.up * 0.1f);
            traps.Fire(1);
            hits = traps.PlayerHits;
            end = Time.time + 2f;
            while (Time.time < end && traps.IsTelegraphing(1))
            {
                _motor.Teleport(c + Vector3.up * 1.6f); // held in the air above it
                yield return null;
            }
            Assert.IsTrue(traps.IsUp(1));
            yield return null;
            Assert.AreEqual(hits, traps.PlayerHits, "feet above the spikes: no hit");
            _motor.Teleport(c + new Vector3(4f, 0.1f, 0f)); // lands beside it
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Barricades_BlockTheHorde_UntilSmashed_AndAToppledBrazierBurns()
        {
            yield return Boot(true);
            var br = ArenaBreakables.Instance;
            var obs = _em.Obstacles;
            Assert.IsNotNull(br);
            _motor.Teleport(new Vector3(0f, 0.6f, 0f)); // on the dais, away from everything below

            Vector3 bar = br.barricades[0].position;
            Assert.IsTrue(obs.Overlaps(XZ(bar), 0.2f), "baked as an obstacle");
            int e = _em.Spawn(_convict, bar);
            for (int f = 0; f < 5; f++) { if (_em.IsAlive(e)) _em.ApplyStatus(e, _root, 0f); yield return null; }
            Assert.IsTrue(_em.IsAlive(e));
            Assert.Greater(Vector2.Distance(XZ(_em.GetPosition(e)), XZ(bar)), 0.5f, "pushed out of the barricade");

            int n = br.SmashAlong(bar - br.barricades[0].right * 3f, bar + br.barricades[0].right * 3f, 1f);
            Assert.GreaterOrEqual(n, 1);
            Assert.IsTrue(br.IsBroken(0));
            Assert.AreEqual(1, br.BarricadesBroken);
            Assert.IsFalse(br.barricades[0].gameObject.activeSelf, "gone, collider and all");
            Assert.IsFalse(obs.Overlaps(XZ(bar), 0.2f), "the horde walks through now");
            int e2 = _em.Spawn(_convict, bar);
            for (int f = 0; f < 5; f++) { if (_em.IsAlive(e2)) _em.ApplyStatus(e2, _root, 0f); yield return null; }
            Assert.Less(Vector2.Distance(XZ(_em.GetPosition(e2)), XZ(bar)), 0.3f, "nothing pushes it out any more");

            // a brazier by the north gate topples into burning ground
            Vector3 brz = br.braziers[2].position;
            int zones = HazardZones.Instance.Count;
            n = br.SmashAlong(brz + Vector3.left * 3f, brz + Vector3.right * 3f, 1f);
            Assert.GreaterOrEqual(n, 1);
            Assert.IsTrue(br.IsToppled(2));
            Assert.AreEqual(zones + 1, HazardZones.Instance.Count, "burning ground where it fell");
            Assert.IsFalse(obs.Overlaps(XZ(brz), 0.1f));
            Assert.IsFalse(br.IsToppled(0), "the others stand");
        }
    }
}
