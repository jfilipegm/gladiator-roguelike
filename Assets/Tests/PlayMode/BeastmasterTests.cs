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
    /// hunt and bite (the Wolf Pack's kills); a level adds a wolf; a wolf past the leash runs back; the Hunting Horn
    /// sends every wolf at a different enemy ahead (three bites, Staggered) and waits when there's nothing to hunt; a
    /// hit on him frenzies the pack; the Beast Handler walks two curs on leashes and killing him frees them; 25 handler
    /// kills unlock the Beastmaster.
    /// Note 37 (v0.28.0): he starts with the Hunting Spear too, whose hits mark (a sigil over the head); a marked enemy
    /// draws the pack; a wolf moves on after its bite window; the pack stays within the leash when he runs off.
    /// </summary>
    public class BeastmasterTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Beastmaster => Load<GladiatorDef>("Assets/Data/Gladiators/GL_Beastmaster.asset");
        static WeaponDef WolfPack => Load<WeaponDef>("Assets/Data/Weapons/WP_WolfPack.asset");
        static WeaponDef HuntingSpear => Load<WeaponDef>("Assets/Data/Weapons/WP_HuntingSpear.asset");
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

        /// <summary>Roots these enemies again (they'd walk into the spear's reach and spoil a pack-only setup).</summary>
        void Pin(params int[] slots)
        {
            foreach (int i in slots) if (_em.IsAlive(i)) _em.ApplyStatus(i, Root, 0f);
        }

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
            Assert.IsNotNull(Player.Main.GetComponent<HuntersMark>());
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
            Assert.Greater(pack.Recalls, 0, "past the leash: they drop everything and run back");
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
        public IEnumerator HeStartsWithThePackAndTheHuntingSpear_WhoseHitsMark()
        {
            yield return Boot(TestArena, Beastmaster);
            Assert.AreEqual(2, _weapons.Weapons.Count, "two weapons from the start");
            Assert.AreEqual(WolfPack, _weapons.Weapons[0].Def, "the pack");
            var spear = _weapons.Weapons[1].Def;
            Assert.AreEqual(HuntingSpear, spear, "and his own Hunting Spear");
            Assert.AreEqual(WeaponPattern.Thrust, spear.pattern);
            Assert.Greater(spear.MaxLevel, 1, "with levels like every weapon");
            var build = Player.Main.GetComponent<PlayerBuild>();
            var card = build.FindWeaponCard(spear);
            Assert.IsNotNull(card, "its card is in the catalog");
            Assert.AreEqual(1, build.State.LevelOf(card), "owned at level 1, so its level-ups come up");
            Assert.IsTrue(UpgradeRoller.IsEligible(card, build.State, build.config), "his to level");
            Assert.IsFalse(UpgradeRoller.IsEligible(card, new BuildState(), build.config), "never a new pick for anyone else");

            var marks = HuntersMark.Instance;
            Assert.IsNotNull(marks, "the Hunter's Mark");
            Vector3 p = Player.Main.transform.position;
            int e = _em.Spawn(Convict, p + new Vector3(0f, 0f, 2f));
            _em.ScaleMaxHp(e, 20f); // it must outlive the first jabs and bites
            int uid = _em.GetUid(e);
            float end = Time.time + 4f;
            while (Time.time < end && marks.Marks == 0) { Pin(e); yield return null; }
            Assert.Greater(marks.Marks, 0, "the spear jabs it");
            Assert.IsTrue(_em.IsAlive(e) && marks.IsMarked(e, uid), "and marks it");
            yield return null;
            Assert.Greater(marks.Drawn, 0, "a sigil over its head");
            _weapons.RemoveAll(); // the spear put away: the mark runs out
            yield return new WaitForSeconds(marks.trait.seconds + 0.3f);
            Assert.IsFalse(marks.IsMarked(e, uid), "for a few seconds");
            Assert.AreEqual(0, marks.Drawn, "and the sigil goes with it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AMarkedEnemy_DrawsThePack()
        {
            yield return Boot(TestArena, Beastmaster);
            var pack = CompanionPack.Instance;
            var marks = HuntersMark.Instance;
            Vector3 p = Player.Main.transform.position;
            // two near him and one farther off, all past the spear's reach (it would mark them itself)
            int a = _em.Spawn(Brute, p + new Vector3(5f, 0f, 0f));
            int b = _em.Spawn(Brute, p + new Vector3(-5f, 0f, 0f));
            int far = _em.Spawn(Brute, p + new Vector3(0f, 0f, 8f));
            int farUid = _em.GetUid(far);
            float end = Time.time + 3f;
            while (Time.time < end && pack.Bites < 1) { Pin(a, b, far); yield return null; }
            Assert.Greater(pack.Bites, 0, "they fight what's near him");
            Assert.AreEqual(0, pack.MarkedPicks, "nothing marked yet");

            float farHp = _em.GetHp(far);
            marks.Mark(far);
            Assert.IsTrue(marks.IsMarked(far, farUid));
            var went = new bool[pack.Count];
            end = Time.time + pack.retargetInterval + 0.6f;
            while (Time.time < end)
            {
                Pin(a, b, far);
                for (int k = 0; k < went.Length; k++) went[k] |= pack.TargetUidOf(k) == farUid;
                yield return null;
            }
            for (int k = 0; k < went.Length; k++) Assert.IsTrue(went[k], "wolf " + k + " runs to the marked one");
            Assert.GreaterOrEqual(pack.MarkedPicks, went.Length, "because it's marked");
            end = Time.time + 2f;
            while (Time.time < end && _em.GetHp(far) >= farHp) { Pin(a, b, far); yield return null; }
            Assert.Less(_em.GetHp(far), farHp, "and bites it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AWolf_MovesOnAfterItsBiteWindow_SoToughOnesCantHoldIt()
        {
            yield return Boot(TestArena, Beastmaster);
            var pack = CompanionPack.Instance;
            Vector3 p = Player.Main.transform.position;
            int a = _em.Spawn(Brute, p + new Vector3(5f, 0f, 1f));
            int b = _em.Spawn(Brute, p + new Vector3(-5f, 0f, 1f));
            _em.ScaleMaxHp(a, 50f); // far too tough to finish in a bite window
            _em.ScaleMaxHp(b, 50f);
            int ua = _em.GetUid(a), ub = _em.GetUid(b);
            int first = 0;
            float end = Time.time + 3f;
            while (Time.time < end && first == 0) { Pin(a, b); first = pack.TargetUidOf(0); yield return null; }
            Assert.IsTrue(first == ua || first == ub, "wolf 0 goes for one brute");
            bool moved = false;
            end = Time.time + pack.chaseSeconds + pack.holdSeconds;
            while (Time.time < end && !moved)
            {
                Pin(a, b);
                int now = pack.TargetUidOf(0);
                moved = now != 0 && now != first;
                yield return null;
            }
            Assert.IsTrue(moved, "after its bites it moves on to the other");
            Assert.Greater(pack.Switches, 0);
            Assert.Less(_em.GetHp(a), _em.GetMaxHp(a), "both are bitten");
            Assert.Less(_em.GetHp(b), _em.GetMaxHp(b));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WhenHeRunsOff_ThePackLeavesItsFight_AndStaysWithinTheLeash()
        {
            yield return Boot(TestArena, Beastmaster);
            var pack = CompanionPack.Instance;
            var motor = Player.Main.GetComponent<PlayerMotor>();
            Vector3 start = Player.Main.transform.position;
            int brute = _em.Spawn(Brute, start + new Vector3(0f, 0f, -6f)); // behind him, past the spear's reach
            _em.ScaleMaxHp(brute, 50f); // the kind of tough mob they used to get stuck on
            int uid = _em.GetUid(brute);
            float end = Time.time + 4f;
            while (Time.time < end && pack.Bites < 2) { Pin(brute); yield return null; }
            Assert.GreaterOrEqual(pack.Bites, 2, "they're on it");

            // he runs off the other way at a full run for 14 m
            float leash = pack.Leash, worst = 0f, ran = 0f;
            while (ran < 14f)
            {
                Pin(brute);
                ran = Mathf.Min(14f, ran + 7.5f * Time.deltaTime);
                Vector3 to = start + Vector3.forward * ran;
                if (_em.Height != null) to.y = _em.Height.Sample(to.x, to.z);
                motor.Teleport(to + Vector3.up * 0.1f);
                yield return null;
                Vector3 pp = Player.Main.transform.position;
                for (int k = 0; k < pack.Count; k++) worst = Mathf.Max(worst, Flat(pack.PositionOf(k) - pp).magnitude);
            }
            Assert.LessOrEqual(worst, leash + pack.leashSlack + 0.6f, "never much past the leash (" + worst.ToString("0.0") + " m)");
            yield return new WaitForSeconds(1.5f);
            Vector3 at = Player.Main.transform.position;
            for (int k = 0; k < pack.Count; k++)
            {
                Assert.AreNotEqual(uid, pack.TargetUidOf(k), "the brute far behind is left alone");
                Assert.Less(Flat(pack.PositionOf(k) - at).magnitude, 4f, "they're at his heel");
            }
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
