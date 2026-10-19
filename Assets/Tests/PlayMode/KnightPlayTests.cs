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
    /// Phase 5.3 (docs/GLADIATORS.md): the Blood Knight spawns with his model, capsule, stats, greatsword and traits;
    /// kills heal him (capped per second, elites in full) while other healing is halved; Bloodlust raises damage as HP
    /// falls; a slide with a full Blood meter pays HP (never below 1, not a hit) for a frenzy; greatsword hits bleed in a
    /// wide arc; beating Gorm below 30 % HP unlocks him, beating Gorm healthy doesn't.
    /// </summary>
    public class KnightPlayTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Knight => Load<GladiatorDef>("Assets/Data/Gladiators/GL_BloodKnight.asset");

        EnemyManager _em;
        EnemyDef _convict;
        WeaponSystem _weapons;
        PlayerHealth _health;
        PlayerStats _stats;

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

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
            if (spawner != null) { _convict = spawner.enemy; spawner.enabled = false; }
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            _em = EnemyManager.Instance;
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            _health = Player.Main.GetComponent<PlayerHealth>();
            _stats = Player.Main.GetComponent<PlayerStats>();
            if (_convict == null) _convict = Load<EnemyDef>("Assets/Data/Enemies/EN_Convict.asset");
            yield return new WaitForSeconds(0.2f);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        void KillOne(Vector3 at, AffixDef affix = null)
        {
            int i = _em.Spawn(_convict, at, affix);
            _em.ApplyDamage(i, new DamageInfo { Amount = 99999f, Flags = DamageFlags.Melee, Point = at, Direction = Vector3.forward });
            Assert.IsFalse(_em.IsAlive(i));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Knight_SpawnsWithItsModel_Capsule_Stats_GreatswordAndTraits()
        {
            yield return Boot(TestArena, Knight);
            var p = Player.Main;
            Assert.AreSame(Knight, p.GetComponent<GladiatorLoadout>().Current);
            var visual = p.transform.Find("Visual");
            Assert.AreEqual(1, visual.childCount, "the Nobody's model is gone");
            Assert.AreEqual("CH_BloodKnight", visual.GetChild(0).name);
            Assert.IsNotNull(FindDeep(visual, "Head"), "same part names: the animator drives it");
            Assert.IsNotNull(FindDeep(visual, "ArmR"));
            Assert.AreEqual(1.65f, p.GetComponent<CharacterController>().height, 0.05f, "a knight-sized capsule");
            Assert.AreEqual(120f, _health.MaxHp, 0.01f);
            Assert.AreEqual(0.92f, _stats.Get(StatId.MoveSpeed), 0.001f);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual("crimson_greatsword", _weapons.Weapons[0].Def.id);
            Assert.IsNotNull(p.GetComponent<Bloodthirst>());
            Assert.IsNotNull(p.GetComponent<Bloodlust>());
            Assert.AreSame(p.GetComponent<BloodRite>(), BloodRite.Instance, "the HUD finds the meter");
            Assert.AreEqual(0.5f, _health.HealingMul, 1e-4f, "other healing is halved");
            var build = p.GetComponent<PlayerBuild>();
            var card = build.FindWeaponCard(_weapons.Weapons[0].Def);
            Assert.AreEqual(1, build.State.LevelOf(card), "the greatsword is owned from the start");
            Assert.IsTrue(UpgradeRoller.IsEligible(card, build.State, build.config), "and he can level it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bloodthirst_KillsHeal_CappedPerSecond_ElitesInFull_OtherHealingHalved()
        {
            yield return Boot(TestArena, Knight);
            _weapons.RemoveAll();
            var thirst = Player.Main.GetComponent<Bloodthirst>();
            var trait = thirst.trait;
            Vector3 far = Player.Main.transform.position + new Vector3(12f, 0f, 12f);
            _health.Spend(80f);
            Assert.AreEqual(40f, _health.Hp, 0.01f);
            _health.Heal(20f);
            Assert.AreEqual(50f, _health.Hp, 0.01f, "an amphora or a healing card heals half");

            yield return new WaitForSeconds(1.1f); // the kill-heal budget is full
            for (int k = 0; k < 20; k++) KillOne(far);
            Assert.AreEqual(50f + trait.maxHealPerSecond, _health.Hp, 0.3f, "20 kills in one frame heal only the per-second cap");
            float hp = _health.Hp;
            KillOne(far, Load<AffixDef>("Assets/Data/Enemies/Affixes/AF_Armored.asset"));
            Assert.AreEqual(hp + trait.healPerElite, _health.Hp, 0.01f, "an elite always heals in full");

            yield return new WaitForSeconds(1.1f);
            hp = _health.Hp;
            KillOne(far);
            Assert.AreEqual(hp + trait.healPerKill, _health.Hp, 0.01f, "the budget refills");
            Assert.Greater(thirst.Healed, trait.maxHealPerSecond + trait.healPerElite - 0.5f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Bloodlust_DamageRisesAsHpFalls()
        {
            yield return Boot(TestArena, Knight);
            _weapons.RemoveAll();
            var lust = Player.Main.GetComponent<Bloodlust>();
            lust.Refresh();
            Assert.AreEqual(0f, lust.Bonus, 1e-4f, "nothing at full HP");
            Assert.AreEqual(1f, _stats.Get(StatId.Damage), 1e-3f);

            _health.Spend(60f); // 50 %
            lust.Refresh();
            Assert.AreEqual(lust.trait.BonusAt(0.5f), lust.Bonus, 0.011f);
            Assert.AreEqual(1f + lust.Bonus, _stats.Get(StatId.Damage), 1e-3f);

            _health.Spend(36f); // 20 %
            lust.Refresh();
            Assert.AreEqual(lust.trait.maxDamageBonus, lust.Bonus, 1e-3f, "full at 20 % HP");
            _health.Spend(1000f); // 1 HP
            lust.Refresh();
            Assert.AreEqual(lust.trait.maxDamageBonus, lust.Bonus, 1e-3f, "and no more below");
            Assert.AreEqual(1f + lust.trait.maxDamageBonus, _stats.Get(StatId.Damage), 1e-3f);

            _health.HealFull();
            lust.Refresh();
            Assert.AreEqual(1f, _stats.Get(StatId.Damage), 1e-3f, "gone again at full HP");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BloodRite_ASlideWithAFullMeter_PaysHp_NeverKills_AndFrenzies()
        {
            yield return Boot(TestArena, Knight);
            _weapons.RemoveAll();
            Player.Main.GetComponent<Bloodlust>().enabled = false; // keep the damage maths about the Rite
            var thirst = Player.Main.GetComponent<Bloodthirst>();
            var rite = Player.Main.GetComponent<BloodRite>();
            rite.trait = Object.Instantiate(rite.trait);
            var trait = rite.trait;
            Vector3 far = Player.Main.transform.position + new Vector3(12f, 0f, 12f);
            Vector3 at = Player.Main.transform.position;

            for (int k = 0; k < 10; k++) KillOne(far);
            Assert.AreEqual(10f * trait.perKill, rite.Blood, 1e-3f, "kills fill the meter");
            _health.TakeDamage(new DamageInfo { Amount = 10f, Flags = DamageFlags.Status });
            Assert.AreEqual(10f * trait.perKill + 10f * trait.perHpLost, rite.Blood, 1e-3f, "and so do wounds");

            float hp = _health.Hp;
            GameEvents.OnSlide.Raise(new SlideEvent { Position = at, Velocity = Vector3.forward * 8f });
            Assert.AreEqual(0, rite.Rites, "a slide without a full meter is just a slide");
            Assert.AreEqual(hp, _health.Hp, 1e-3f);

            rite.Fill();
            Assert.IsTrue(rite.Full);
            float taken = _health.DamageTaken;
            GameEvents.OnSlide.Raise(new SlideEvent { Position = at, Velocity = Vector3.forward * 8f });
            Assert.AreEqual(1, rite.Rites, "the Rite");
            Assert.AreEqual(hp - _health.MaxHp * trait.hpCost, _health.Hp, 0.01f, "paid in HP");
            Assert.AreEqual(taken, _health.DamageTaken, 1e-3f, "paid, not taken as a hit");
            Assert.AreEqual(0f, rite.Blood, 1e-3f, "the meter empties");
            Assert.IsTrue(rite.Frenzied);
            Assert.AreEqual(trait.frenzyDamage, _stats.Get(StatId.Damage), 1e-3f);
            Assert.AreEqual(trait.frenzyAttackSpeed, _stats.Get(StatId.AttackSpeed), 1e-3f);
            Assert.AreEqual(trait.frenzyHealMul, thirst.HealMul, 1e-3f, "kills heal double");
            for (int k = 0; k < 5; k++) KillOne(far);
            Assert.AreEqual(0f, rite.Blood, 1e-3f, "the meter is still while frenzied");

            rite.Fill();
            GameEvents.OnSlide.Raise(new SlideEvent { Position = at, Velocity = Vector3.forward * 8f });
            Assert.AreEqual(1, rite.Rites, "no second Rite during the frenzy");

            float end = Time.time + trait.frenzySeconds + 1f;
            while (Time.time < end && rite.Frenzied) yield return null;
            Assert.IsFalse(rite.Frenzied, "it wears off");
            Assert.AreEqual(1f, _stats.Get(StatId.Damage), 1e-3f);
            Assert.AreEqual(1f, _stats.Get(StatId.AttackSpeed), 1e-3f);
            Assert.AreEqual(1f, thirst.HealMul, 1e-3f);

            _health.Spend(1000f);
            Assert.AreEqual(1f, _health.Hp, 1e-3f);
            rite.Fill();
            Assert.IsTrue(rite.Perform());
            Assert.AreEqual(1f, _health.Hp, 1e-3f, "the Rite never kills: it can't take the last HP");
            Assert.IsFalse(_health.IsDead);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Greatsword_CleavesAWideArc_AndEveryHitBleeds()
        {
            yield return Boot(TestArena, Knight);
            DevSettings.DamagePct = -95f; // ×0.05: they must live through the hit to show the bleed
            var root = Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
            Vector3 p = Player.Main.transform.position;
            int front = _em.Spawn(_convict, p + new Vector3(0f, 0f, 2.2f));
            int side = _em.Spawn(_convict, p + new Vector3(2.4f, 0f, 0.4f));   // ~80° off the aim: inside 220°
            int behind = _em.Spawn(_convict, p + new Vector3(0.6f, 0f, -2.6f)); // ~167° off: outside
            int swings = _weapons.Swings;
            float end = Time.time + 4f;
            while (Time.time < end && _em.StatusStacks(front, StatusKind.Bleed) == 0)
            {
                foreach (int i in new[] { front, side, behind }) if (_em.IsAlive(i)) _em.ApplyStatus(i, root, 0f);
                yield return null;
            }
            Assert.Greater(_weapons.Swings, swings, "it swung");
            Assert.GreaterOrEqual(_em.StatusStacks(front, StatusKind.Bleed), 1, "the target bleeds");
            Assert.GreaterOrEqual(_em.StatusStacks(side, StatusKind.Bleed), 1, "so does the one at his side: a huge arc");
            Assert.AreEqual(0, _em.StatusStacks(behind, StatusKind.Bleed), "but not the one behind him");
            Assert.AreEqual(_em.GetMaxHp(behind), _em.GetHp(behind), 0.01f, "untouched");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BeatingGormBleedingOut_UnlocksTheKnight_BeatingHimHealthyDoesNot()
        {
            SaveSandbox.Wipe();
            yield return Boot(SlavePit, null);
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Knight), "locked on a fresh save");
            RunState.Current.MarkBossDefeated("gorm", 0.55f);
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            Assert.IsNotNull(rewards);
            foreach (var u in rewards.NewUnlocks) Assert.AreNotEqual("blood_knight", u.id, "a healthy win isn't enough");
            Assert.IsTrue(MetaProgress.Data.TryBossKillHp("gorm", out int permille));
            Assert.AreEqual(550, permille, "the save keeps the HP he fell at");

            RunEndController.Instance.Retry();
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end && (RunState.Current == null || RunState.Current.Ended || SceneManager.GetActiveScene().name != "SlavePit")) yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            RunState.Current.MarkBossDefeated("gorm", 0.22f);
            RunState.Current.End(RunOutcome.Survived);
            yield return null;
            rewards = RunEndController.Instance.Rewards;
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "blood_knight";
            Assert.IsTrue(got, "beaten bleeding out: the knight is unlocked");
            Assert.IsTrue(SaveSystem.Load(SaveSandbox.TestPath).IsUnlocked("blood_knight"), "on disk");
            Assert.IsTrue(MetaProgress.Data.TryBossKillHp("gorm", out permille));
            Assert.AreEqual(220, permille, "the lowest is kept");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Knight));
        }
    }
}
