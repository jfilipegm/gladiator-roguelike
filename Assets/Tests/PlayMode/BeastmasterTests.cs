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
    /// Phase 5.15 (docs/GLADIATORS.md): the Beastmaster. He spawns with his model, stats and two wolves; the wolves
    /// hunt the nearest enemy and bite it (the Wolf Pack's kills); a level adds a wolf; a wolf past the leash runs back;
    /// the Hunting Horn sends every wolf at a different enemy ahead (three bites, Staggered) and waits when there's
    /// nothing to hunt; a hit on him frenzies the pack; the Beast Handler walks two curs on leashes and killing him
    /// frees them; 25 handler kills unlock the Beastmaster.
    /// </summary>
    public class BeastmasterTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Beastmaster => Load<GladiatorDef>("Assets/Data/Gladiators/GL_Beastmaster.asset");
        static EnemyDef Brute => Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");
        static EnemyDef Convict => Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
        static EnemyDef Handler => Load<EnemyDef>("Assets/Data/Enemies/EN_BeastHandler.asset");
        static EnemyDef Cur => Load<EnemyDef>("Assets/Data/Enemies/EN_PitCur.asset");
        static StatusDef Root => Load<StatusDef>("Assets/Data/Status/SE_Root.asset");

        EnemyManager _em;
        WeaponSystem _weapons;

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
        public IEnumerator HeSpawns_WithTwoWolves_ThatHuntTheNearest_AndBite()
        {
            yield return Boot(TestArena, Beastmaster);
            var p = Player.Main.transform;
            // his own model (A1: one mesh per part, so no piece names to look for), rigged
            Assert.IsNotNull(FindDeep(p, Beastmaster.model.name), "his model, " + Beastmaster.model.name);
            foreach (var part in new[] { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR" })
                Assert.IsNotNull(FindDeep(p, part), "his " + part);
            Assert.AreEqual(100f, Player.Main.GetComponent<PlayerHealth>().MaxHp, 0.01f);
            Assert.AreEqual(WeaponPattern.Summon, _weapons.Weapons[0].Def.pattern);
            Assert.IsNotNull(Player.Main.GetComponent<FeralBond>());
            Assert.IsNotNull(Player.Main.GetComponent<HuntingHorn>());
            var pack = CompanionPack.Instance;
            Assert.IsNotNull(pack, "the pack comes with the weapon");
            Assert.AreEqual(2, pack.Count, "two wolves");
            int weak = _em.Spawn(Convict, p.position + new Vector3(6f, 0f, 0f));
            int tough = _em.Spawn(Brute, p.position + new Vector3(-6f, 0f, 2f));
            var run = RunState.Current;
            int kills = run.KillsBy(_weapons.Weapons[0].Def.RuntimeId);
            float end = Time.time + 6f;
            while (Time.time < end && (pack.Bites < 4 || _em.IsAlive(weak)))
            {
                if (_em.IsAlive(weak)) _em.ApplyStatus(weak, Root, 0f);
                if (_em.IsAlive(tough)) _em.ApplyStatus(tough, Root, 0f);
                yield return null;
            }
            Assert.GreaterOrEqual(pack.Bites, 4, "they bite, again and again");
            Assert.IsFalse(_em.IsAlive(weak), "the convict is torn down");
            Assert.Less(_em.GetHp(tough), _em.GetMaxHp(tough), "and the brute bitten");
            Assert.Greater(run.KillsBy(_weapons.Weapons[0].Def.RuntimeId), kills, "the Wolf Pack's kill");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ALevel_AddsAWolf_AndAWolfPastTheLeash_RunsBack()
        {
            yield return Boot(TestArena, Beastmaster);
            var pack = CompanionPack.Instance;
            _weapons.LevelUp(_weapons.Weapons[0].Def, 1f);
            yield return null;
            yield return null;
            Assert.AreEqual(3, pack.Count, "level 2: a third wolf");
            // nothing to hunt: they heel. Move him far away at once: they come running.
            var motor = Player.Main.GetComponent<PlayerMotor>();
            Vector3 from = Player.Main.transform.position;
            motor.Teleport(from + new Vector3(0f, 0.1f, 18f));
            yield return null;
            float leash = _weapons.Weapons[0].Def.range;
            Assert.Greater(Flat(pack.PositionOf(0) - Player.Main.transform.position).magnitude, leash, "left behind");
            yield return new WaitForSeconds(3f);
            for (int k = 0; k < pack.Count; k++)
                Assert.Less(Flat(pack.PositionOf(k) - Player.Main.transform.position).magnitude, 4f, "back at his heel");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheHorn_SendsEveryWolfAtADifferentEnemyAhead_AndWaitsWhenTheresNothingToHunt()
        {
            yield return Boot(TestArena, Beastmaster);
            var pack = CompanionPack.Instance;
            var horn = Player.Main.GetComponent<HuntingHorn>();
            yield return null;
            Assert.IsFalse(horn.Blow(Vector3.right), "nothing to hunt: the horn waits");
            Assert.IsTrue(horn.Ready, "and stays ready");

            Vector3 p = Player.Main.transform.position;
            int a = _em.Spawn(Brute, p + new Vector3(4f, 0f, 1.2f));
            int b = _em.Spawn(Brute, p + new Vector3(4.5f, 0f, -1.2f));
            yield return null;
            float ha = _em.GetHp(a), hb = _em.GetHp(b);
            Assert.IsTrue(horn.Blow(Vector3.right));
            Assert.AreEqual(2, pack.Pounces, "both wolves leap");
            Assert.IsTrue(pack.IsPouncing(0) && pack.IsPouncing(1));
            Assert.AreNotEqual(pack.TargetOf(0), pack.TargetOf(1), "at different enemies");
            Assert.IsFalse(horn.Ready, "the horn recharges");
            yield return new WaitForSeconds(pack.pounceTime + 0.1f);
            float bite = _weapons.Weapons[0].Def.damage;
            Assert.GreaterOrEqual(ha - _em.GetHp(a), bite * horn.trait.damageMul * 0.99f, "three bites in one");
            Assert.GreaterOrEqual(hb - _em.GetHp(b), bite * horn.trait.damageMul * 0.99f);
            Assert.IsTrue(_em.HasStatus(a, StatusKind.Root) && _em.HasStatus(b, StatusKind.Root), "Staggered");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AHitOnHim_FrenziesThePack()
        {
            yield return Boot(TestArena, Beastmaster);
            var pack = CompanionPack.Instance;
            Assert.IsFalse(pack.Frenzied);
            Player.Main.GetComponent<PlayerHealth>().TakeDamage(new DamageInfo { Amount = 5f, Flags = DamageFlags.Melee, Point = Player.Main.transform.position });
            Assert.IsTrue(pack.Frenzied, "hurt him and the wolves go wild");
            Assert.AreEqual(1, pack.Frenzies);
            yield return new WaitForSeconds(Player.Main.GetComponent<FeralBond>().trait.seconds + 0.2f);
            Assert.IsFalse(pack.Frenzied, "for a while");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheHandler_WalksTwoCursOnLeashes_AndKillingHimFreesThem()
        {
            yield return Boot(TestArena, null);
            _weapons.RemoveAll();
            Vector3 p = Player.Main.transform.position;
            int before = _em.AliveCount;
            int h = _em.Spawn(Handler, p + new Vector3(0f, 0f, 14f));
            Assert.AreEqual(before + 3, _em.AliveCount, "the handler and his two curs");
            Assert.AreEqual(2, _em.AliveOf(Cur));
            var curs = new int[8];
            int n = _em.FindAlive(Cur, curs);
            Assert.AreEqual(2, n);
            for (int k = 0; k < n; k++) Assert.AreEqual(h, _em.LeaderOf(curs[k]), "on his leash");
            yield return new WaitForSeconds(2f); // the curs want the player; the leash holds them to his pace
            float leash = Handler.leashLength;
            for (int k = 0; k < n; k++)
                Assert.LessOrEqual(Flat(_em.GetPosition(curs[k]) - _em.GetPosition(h)).magnitude, leash + 0.6f, "within the leash");
            int kills = RunState.Current.Kills, freed = _em.EscortsFreed;
            _em.ApplyDamage(h, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(h) });
            Assert.AreEqual(0, _em.AliveOf(Cur), "freed: they're gone");
            Assert.AreEqual(freed + 2, _em.EscortsFreed);
            yield return null;
            Assert.AreEqual(kills + 1, RunState.Current.Kills, "only the handler counts as a kill");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TwentyFiveHandlersDead_UnlockHim()
        {
            SaveSandbox.Wipe();
            yield return Boot(SlavePit, null);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Beastmaster), "locked on a fresh save");
            SaveData.Add(MetaProgress.Data.enemyKills, Handler.id, 24); // 24 from earlier runs
            int h = _em.Spawn(Handler, Player.Main.transform.position + new Vector3(0f, 0f, 9f));
            _em.ApplyDamage(h, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = _em.GetPosition(h) });
            yield return null;
            RunState.Current.End(RunOutcome.Died);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards);
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "beastmaster";
            Assert.IsTrue(got, "the 25th handler: the beasts are free");
            Assert.IsTrue(SaveSystem.Load(SaveSandbox.TestPath).IsUnlocked("beastmaster"), "on disk");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Beastmaster));
            SaveSandbox.Wipe();
        }
    }
}
