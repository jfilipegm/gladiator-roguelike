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
    /// Phase 5.12 (docs/GLADIATORS.md): the Ashen Oracle. He spawns with his model, stats, censer and traits; the
    /// censer's cursed fireball (note 33: his own, not the goblin's lobbed bomb) flies at an enemy, steers after it and
    /// bursts on it; the burst Curses what it hits and leaves a cursed patch that curses an enemy walking in; a fireball
    /// bursts on the first enemy in its way; a cursed enemy's death bursts
    /// and passes the curse on (Contagion); a hit releases the Smoke Veil (curse, slow, push) once per cooldown, and a
    /// status tick doesn't; the masked stranger in the Stone Amphitheater reads a player who stays in his ring (stepping
    /// out resets it), saves the flag and leaves; unread he fades; the flag unlocks the Oracle.
    /// </summary>
    public class AshenOracleTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string Amphitheater = "Assets/Scenes/Arenas/StoneAmphitheater.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static GladiatorDef Oracle => Load<GladiatorDef>("Assets/Data/Gladiators/GL_AshenOracle.asset");
        static EnemyDef Brute => Load<EnemyDef>("Assets/Data/Enemies/EN_Brute.asset");
        static StatusDef Curse => Load<StatusDef>("Assets/Data/Status/SE_Curse.asset");
        static StatusDef Root => Load<StatusDef>("Assets/Data/Status/SE_Root.asset");

        EnemyManager _em;
        WeaponSystem _weapons;
        PlayerHealth _health;

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
            _health = Player.Main.GetComponent<PlayerHealth>();
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
        public IEnumerator HeSpawns_WithHisModel_Stats_Censer_AndTraits()
        {
            yield return Boot(TestArena, Oracle);
            var p = Player.Main.transform;
            // his own model (A1: one mesh per part, so no piece names to look for), rigged
            Assert.IsNotNull(FindDeep(p, Oracle.model.name), "his model, " + Oracle.model.name);
            foreach (var part in new[] { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR" })
                Assert.IsNotNull(FindDeep(p, part), "his " + part);
            Assert.AreEqual(85f, _health.MaxHp, 0.01f);
            Assert.AreEqual(1, _weapons.Weapons.Count);
            Assert.AreEqual("ashen_censer", _weapons.Weapons[0].Def.id);
            Assert.AreEqual(WeaponPattern.Fireball, _weapons.Weapons[0].Def.pattern, "his own cursed fireball, not the goblin's lob");
            Assert.AreNotEqual(Load<WeaponDef>("Assets/Data/Weapons/WP_BombSatchel.asset").mesh, _weapons.Weapons[0].Def.mesh, "nor its bomb");
            Assert.IsNotNull(Player.Main.GetComponent<TraitEffects>(), "Contagion");
            Assert.IsNotNull(Player.Main.GetComponent<SmokeVeil>());
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Censer_CursesWhatItHits_AndLeavesACursedPatch()
        {
            yield return Boot(TestArena, Oracle);
            var fx = Player.Main.GetComponent<BoonEffects>();
            var pp = PlayerProjectiles.Instance;
            Vector3 p = Player.Main.transform.position;
            int a = _em.Spawn(Brute, p + new Vector3(6f, 0f, 0f));
            var root = Root;
            int blasts = pp.Blasts, bursts = pp.Fireballs, cast = _weapons.FireballsCast, flying = 0;
            float end = Time.time + 5f;
            while (Time.time < end && pp.Fireballs == bursts)
            {
                if (_em.IsAlive(a)) _em.ApplyStatus(a, root, 0f);
                if (_weapons.FireballsCast > cast) flying++; // frames it spent in the air
                yield return null;
            }
            Assert.Greater(pp.Fireballs, bursts, "the fireball burst");
            Assert.Greater(flying, 2, "after a flight: a fireball, not an instant hit");
            Assert.AreEqual(blasts, pp.Blasts, "and no bomb went off");
            Assert.Less(Vector3.Distance(new Vector3(pp.LastFireballBurst.x, 0f, pp.LastFireballBurst.z),
                new Vector3(_em.GetPosition(a).x, 0f, _em.GetPosition(a).z)), _em.GetRadius(a) + 0.8f, "on the brute");
            yield return null;
            Assert.IsTrue(_em.HasStatus(a, StatusKind.Curse), "what it hits is Cursed");
            Assert.GreaterOrEqual(_em.StatusStacks(a, StatusKind.Curse), 2, "two stacks a hit");
            Assert.Greater(fx.MarkCount, 0, "a cursed patch on the ground");

            _weapons.RemoveAll(); // no more orbs: only the patch can curse now
            int walker = _em.Spawn(Brute, _em.GetPosition(a) + new Vector3(0f, 0f, 0.6f));
            end = Time.time + 2f;
            while (Time.time < end && !_em.HasStatus(walker, StatusKind.Curse))
            {
                if (_em.IsAlive(walker)) _em.ApplyStatus(walker, root, 0f);
                yield return null;
            }
            Assert.IsTrue(_em.HasStatus(walker, StatusKind.Curse), "the patch curses whatever walks in");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Fireball_BurstsOnTheFirstEnemyInItsWay_AndCursesAllAroundIt()
        {
            yield return Boot(TestArena, Oracle);
            _em.DespawnAll();
            var pp = PlayerProjectiles.Instance;
            Vector3 p = Player.Main.transform.position;
            int front = _em.Spawn(Brute, p + new Vector3(0f, 0f, 4f)); // the nearest: its target
            int behind = _em.Spawn(Brute, p + new Vector3(0f, 0f, 9f)); // in line behind it, out of the burst
            int beside = _em.Spawn(Brute, p + new Vector3(1.6f, 0f, 4.6f)); // next to the target, inside the burst
            var all = new[] { front, behind, beside };
            int bursts = pp.Fireballs;
            float end = Time.time + 4f;
            while (Time.time < end && pp.Fireballs == bursts)
            {
                foreach (int i in all) if (_em.IsAlive(i)) _em.ApplyStatus(i, Root, 0f);
                yield return null;
            }
            Assert.AreEqual(bursts + 1, pp.Fireballs, "one fireball, one burst");
            Assert.Less(Mathf.Abs(pp.LastFireballBurst.z - _em.GetPosition(front).z), _em.GetRadius(front) + 0.8f, "it bursts on the first it touches");
            Assert.IsTrue(_em.HasStatus(front, StatusKind.Curse) && _em.HasStatus(beside, StatusKind.Curse), "the burst curses all around it");
            Assert.IsFalse(_em.HasStatus(behind, StatusKind.Curse), "it doesn't fly on through");
            Assert.AreEqual(_em.GetMaxHp(behind), _em.GetHp(behind), 0.01f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Contagion_ACursedDeathBursts_AndPassesTheCurseOn()
        {
            yield return Boot(TestArena, Oracle);
            _weapons.RemoveAll();
            Vector3 p = Player.Main.transform.position + new Vector3(0f, 0f, 9f);
            int carrier = _em.Spawn(Brute, p);
            int n1 = _em.Spawn(Brute, p + new Vector3(1.6f, 0f, 0f));
            int n2 = _em.Spawn(Brute, p + new Vector3(-1.6f, 0f, 0.4f));
            int far = _em.Spawn(Brute, p + new Vector3(0f, 0f, 8f));
            yield return null;
            var fx = Player.Main.GetComponent<BoonEffects>();
            int bursts = fx.BurstsFired;
            _em.ApplyStatus(carrier, Curse, 1f, 1);
            _em.ApplyDamage(carrier, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee, Point = p });
            yield return null;
            yield return null;
            Assert.Greater(fx.BurstsFired, bursts, "the cursed death bursts");
            Assert.IsTrue(_em.HasStatus(n1, StatusKind.Curse) && _em.HasStatus(n2, StatusKind.Curse), "and the curse passes on");
            Assert.Less(_em.GetHp(n1), _em.GetMaxHp(n1), "the burst hurts");
            Assert.IsFalse(_em.HasStatus(far, StatusKind.Curse), "out of the burst: clean");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SmokeVeil_AHitBurstsIntoSmoke_OncePerCooldown_NotOnStatusTicks()
        {
            yield return Boot(TestArena, Oracle);
            _weapons.RemoveAll();
            _health.invulnerableTime = 0f;
            var veil = Player.Main.GetComponent<SmokeVeil>();
            Vector3 p = Player.Main.transform.position;
            int near = _em.Spawn(Brute, p + new Vector3(2f, 0f, 0f));
            int edge = _em.Spawn(Brute, p + new Vector3(0f, 0f, 3.8f));
            int far = _em.Spawn(Brute, p + new Vector3(-9f, 0f, 0f));
            yield return null;
            _health.TakeDamage(new DamageInfo { Amount = 3f, Flags = DamageFlags.Status });
            Assert.AreEqual(0, veil.Veils, "a burn tick isn't a hit");
            _health.TakeDamage(new DamageInfo { Amount = 5f, Flags = DamageFlags.Melee });
            Assert.AreEqual(1, veil.Veils, "a hit: smoke");
            foreach (int i in new[] { near, edge })
            {
                Assert.IsTrue(_em.HasStatus(i, StatusKind.Curse), "Cursed");
                Assert.IsTrue(_em.HasStatus(i, StatusKind.Slow), "and Slowed");
            }
            Assert.IsFalse(_em.HasStatus(far, StatusKind.Curse), "out of reach");
            _health.TakeDamage(new DamageInfo { Amount = 5f, Flags = DamageFlags.Melee });
            Assert.AreEqual(1, veil.Veils, "once per cooldown");
            yield return new WaitForSeconds(veil.trait.cooldown + 0.1f);
            _health.TakeDamage(new DamageInfo { Amount = 5f, Flags = DamageFlags.Melee });
            Assert.AreEqual(2, veil.Veils, "ready again");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheMaskedStranger_ReadsAPlayerWhoStays_SavesTheFlag_AndUnlocksTheOracle()
        {
            SaveSandbox.Wipe();
            yield return Boot(Amphitheater, null);
            var s = AshenStranger.Instance;
            Assert.IsNotNull(s, "the amphitheatre has its stranger");
            Assert.IsFalse(Gladiator.UI.MainMenu.IsAvailable(Oracle), "locked on a fresh save");
            var motor = Player.Main.GetComponent<PlayerMotor>();
            s.Appear(0);
            Assert.AreEqual(AshenStranger.Phase.Here, s.State);
            motor.Teleport(s.Position + new Vector3(0.8f, 0.1f, 0f));
            yield return new WaitForSeconds(s.readSeconds * 0.5f);
            Assert.Greater(s.Read01, 0.3f, "the ring fills while you stand in it");
            motor.Teleport(s.Position + new Vector3(6f, 0.1f, 0f));
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0f, s.Read01, "step out and the reading starts over");
            motor.Teleport(s.Position + new Vector3(0.8f, 0.1f, 0f));
            yield return new WaitForSeconds(s.readSeconds + 0.4f);
            Assert.AreEqual(AshenStranger.Phase.Read, s.State, "read: gone in ash");
            Assert.IsTrue(MetaProgress.Data.HasFlag("ashen_stranger"), "the flag is saved at once");

            RunState.Current.End(RunOutcome.Died);
            yield return null;
            var rewards = RunEndController.Instance.Rewards;
            bool got = false;
            foreach (var u in rewards.NewUnlocks) got |= u.id == "ashen_oracle";
            Assert.IsTrue(got, "the Oracle is unlocked at the run's end");
            Assert.IsTrue(Gladiator.UI.MainMenu.IsAvailable(Oracle));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheMaskedStranger_Unread_FadesAway()
        {
            SaveSandbox.Wipe();
            yield return Boot(Amphitheater, null);
            var s = AshenStranger.Instance;
            s.stay = 1f; // the scene's instance, not an asset
            s.Appear(1);
            yield return new WaitForSeconds(1.3f);
            Assert.AreEqual(AshenStranger.Phase.Gone, s.State, "nobody came: he's gone");
            Assert.IsFalse(MetaProgress.Data.HasFlag("ashen_stranger"));
        }
    }
}
