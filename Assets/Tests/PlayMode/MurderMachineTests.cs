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
    /// Phase 5.9 (docs/GLADIATORS.md): the Murder Machine. He spawns with his model, stats, cleaver and traits; the
    /// cleaver crushes the circle it lands on (not what's behind him); his plate takes 4 off every hit (never below 1,
    /// not status ticks) and nothing knocks him back; the Wrath meter fills only from hits, and a landing with it full is
    /// the Verdict (hits and staggers the ring, empties the meter); beating Gorm unlocks him.
    /// </summary>
    public class MurderMachineTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Machine => Load<GladiatorDef>("Assets/Data/Gladiators/GL_MurderMachine.asset");
        static EnemyDef Brute => Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");
        static StatusDef Root => Load<StatusDef>("Assets/Data/Status/SE_Root.asset");

        EnemyManager _em;
        WeaponSystem _weapons;
        PlayerHealth _health;
        PlayerMotor _motor;
        ScriptedInput _input;

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
            _input = new ScriptedInput();
            Player.Main.OverrideSource = _input;
            _em = EnemyManager.Instance;
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            _health = Player.Main.GetComponent<PlayerHealth>();
            _motor = Player.Main.GetComponent<PlayerMotor>();
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

        float ArenaMul => RunState.Current != null ? RunState.Current.playerDamageTakenMul : 1f;

        [UnityTest, Timeout(60000)]
        public IEnumerator HeSpawns_WithHisModel_Stats_Cleaver_AndTraits()
        {
            yield return Boot(TestArena, Machine);
            var p = Player.Main.transform;
            // his own model (A1: one mesh per part, so no piece names to look for), rigged
            Assert.IsNotNull(FindDeep(p, Machine.model.name), "his model, " + Machine.model.name);
            foreach (var part in new[] { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR" })
                Assert.IsNotNull(FindDeep(p, part), "his " + part);
            Assert.AreEqual(150f, _health.MaxHp, 0.01f);
            Assert.AreEqual(0.85f, Player.Main.GetComponent<PlayerStats>().Get(StatId.MoveSpeed), 1e-3f);
            Assert.AreEqual(2.0f, Player.Main.GetComponent<CharacterController>().height, 0.01f, "the biggest capsule");
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual(WeaponPattern.GroundSlam, _weapons.Weapons[0].Def.pattern);
            Assert.IsNotNull(Player.Main.GetComponent<Verdict>());
            Assert.AreEqual(4f, _health.FlatReduction, 1e-3f, "the plate");
            Assert.AreEqual(0f, _motor.KnockbackTakenMul, 1e-3f, "nothing knocks him back");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Cleaver_CrushesTheCircleItLandsOn_NotWhatsBehindHim()
        {
            yield return Boot(TestArena, Machine);
            Vector3 p = Player.Main.transform.position;
            int target = _em.Spawn(Brute, p + new Vector3(2f, 0f, 0f));  // the nearest: the chop lands on it
            int beside = _em.Spawn(Brute, p + new Vector3(3.6f, 0f, 1f)); // 1.9 m from where it lands: inside
            int behind = _em.Spawn(Brute, p + new Vector3(-3f, 0f, 0f));  // behind him, past point blank
            var root = Root;
            int slams = _weapons.Slams;
            float end = Time.time + 5f;
            while (Time.time < end && _weapons.Slams == slams)
            {
                foreach (int i in new[] { target, beside, behind }) if (_em.IsAlive(i)) _em.ApplyStatus(i, root, 0f);
                yield return null;
            }
            Assert.Greater(_weapons.Slams, slams, "it came down");
            Assert.Less(_em.GetHp(target), _em.GetMaxHp(target), "the one it was aimed at is crushed");
            Assert.Less(_em.GetHp(beside), _em.GetMaxHp(beside), "and so is the one beside it: an area");
            Assert.AreEqual(_em.GetMaxHp(behind), _em.GetHp(behind), 0.01f, "the one behind him is untouched");
            Assert.Greater(PlayerProjectiles.Instance.Shockwaves, 0, "a ring runs out across the sand");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Plate_TakesFourOffEveryHit_NeverBelowOne_ButNotStatusTicks_AndHeCantBeKnockedBack()
        {
            yield return Boot(TestArena, Machine);
            _weapons.RemoveAll();
            _health.invulnerableTime = 0f;
            float mul = ArenaMul;
            float hp = _health.Hp;
            _health.TakeDamage(new DamageInfo { Amount = 10f, Flags = DamageFlags.Melee });
            Assert.AreEqual(10f * mul - 4f, hp - _health.Hp, 1e-3f, "a hit is 4 lower");
            hp = _health.Hp;
            _health.TakeDamage(new DamageInfo { Amount = 2f, Flags = DamageFlags.Melee });
            Assert.AreEqual(1f, hp - _health.Hp, 1e-3f, "never below 1");
            hp = _health.Hp;
            _health.TakeDamage(new DamageInfo { Amount = 5f, Flags = DamageFlags.Status });
            Assert.AreEqual(5f * mul, hp - _health.Hp, 1e-3f, "a burn tick isn't reduced");

            Vector3 before = _motor.transform.position;
            _motor.AddImpulse(new Vector3(18f, 6f, 0f)); // a boar's or a boss's knockback
            yield return new WaitForSeconds(0.5f);
            Vector3 moved = _motor.transform.position - before;
            moved.y = 0f;
            Assert.Less(moved.magnitude, 0.05f, "he doesn't budge");

            _motor.KnockbackTakenMul = 1f; // control: the same push moves anyone else
            before = _motor.transform.position;
            _motor.AddImpulse(new Vector3(18f, 6f, 0f));
            yield return new WaitForSeconds(0.5f);
            moved = _motor.transform.position - before;
            moved.y = 0f;
            Assert.Greater(moved.magnitude, 0.5f, "the push itself works");
        }

        IEnumerator JumpAndLand()
        {
            _input.Next = new PlayerInputFrame { JumpPressed = true, JumpHeld = true };
            float end = Time.time + 3f;
            while (Time.time < end && _motor.IsGrounded) yield return null;
            Assert.IsFalse(_motor.IsGrounded, "in the air");
            _input.Next = new PlayerInputFrame();
            while (Time.time < end && !_motor.IsGrounded) yield return null;
            Assert.IsTrue(_motor.IsGrounded, "landed");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Wrath_FillsOnlyFromHits_AndALandingWithItFull_IsTheVerdict()
        {
            yield return Boot(TestArena, Machine);
            _weapons.RemoveAll();
            _health.invulnerableTime = 0f;
            var v = Player.Main.GetComponent<Verdict>();
            Assert.AreEqual(0f, v.Wrath);
            _health.TakeDamage(new DamageInfo { Amount = 6f, Flags = DamageFlags.Melee });
            Assert.AreEqual(v.trait.perHit, v.Wrath, 1e-3f, "a hit fills it");
            _health.TakeDamage(new DamageInfo { Amount = 6f, Flags = DamageFlags.Status });
            Assert.AreEqual(v.trait.perHit, v.Wrath, 1e-3f, "a status tick doesn't");

            yield return JumpAndLand();
            Assert.AreEqual(0, v.Verdicts, "a landing with a part-full meter is just a landing");
            Assert.AreEqual(v.trait.perHit, v.Wrath, 1e-3f);

            Vector3 p = Player.Main.transform.position;
            int near = _em.Spawn(Brute, p + new Vector3(3f, 0f, 0f));
            int mid = _em.Spawn(Brute, p + new Vector3(0f, 0f, 5.5f));
            int far = _em.Spawn(Brute, p + new Vector3(0f, 0f, -12f));
            v.Fill();
            Assert.IsTrue(v.Full);
            yield return JumpAndLand();
            Assert.AreEqual(1, v.Verdicts, "VERDICT!");
            Assert.AreEqual(0f, v.Wrath, "the meter empties");
            Assert.Less(_em.GetHp(near), _em.GetMaxHp(near), "the ring is hit");
            Assert.Less(_em.GetHp(mid), _em.GetMaxHp(mid));
            Assert.IsTrue(_em.HasStatus(near, StatusKind.Root) && _em.HasStatus(mid, StatusKind.Root), "and staggered");
            Assert.AreEqual(_em.GetMaxHp(far), _em.GetHp(far), 0.01f, "outside it: untouched");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BeatingGorm_UnlocksHim()
        {
            SaveSandbox.Wipe();
            yield return Boot(SlavePit, null);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Machine), "locked on a fresh save");
            RunState.Current.MarkBossDefeated("gorm", 0.8f);
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards);
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "murder_machine";
            Assert.IsTrue(got, "Gorm is dead: his executioner wants new work");
            Assert.IsTrue(SaveSystem.Load(SaveSandbox.TestPath).IsUnlocked("murder_machine"), "on disk");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Machine));
        }
    }
}
