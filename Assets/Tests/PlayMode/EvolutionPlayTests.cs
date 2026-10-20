using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// Phase 5.5 (docs/EVOLUTIONS.md) in a real run: a maxed weapon + its catalyst evolves from a chest in the same slot;
    /// the evolutions' twists work (Echo behind you, chain lightning, burning ground); synergies switch on at 3 tags and
    /// do their thing (Wildfire spreads, Executioner kills low enemies on a crit but never a boss).
    /// </summary>
    public class EvolutionPlayTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

        RunState _run;
        LevelUpController _lvl;
        PlayerBuild _build;
        WeaponSystem _weapons;
        BoonEffects _fx;
        EnemyManager _em;
        Vector3 _p;

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, path);
            return a;
        }

        static UpgradeDef Up(string file) => Load<UpgradeDef>("Assets/Data/Upgrades/" + file + ".asset");
        static WeaponDef Wp(string file) => Load<WeaponDef>("Assets/Data/Weapons/" + file + ".asset");
        static EnemyDef En(string file) => Load<EnemyDef>("Assets/Data/Enemies/" + file + ".asset");
        static StatusDef St(string file) => Load<StatusDef>("Assets/Data/Status/" + file + ".asset");

        /// <summary>Loads the test arena with a quiet Director. Level-ups stay off unless `cards` (a stray level-up would pause the test's waits).</summary>
        IEnumerator Boot(bool weapons, bool cards = false)
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            DevSettings.GodMode = true;
            _em = EnemyManager.Instance;
            _em.KillAll();
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            _weapons = Player.Main.GetComponent<WeaponSystem>();
            _weapons.enabled = weapons;
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _run = RunState.Current;
            _lvl = LevelUpController.Instance;
            _build = Player.Main.GetComponent<PlayerBuild>();
            _fx = Player.Main.GetComponent<BoonEffects>();
            _p = Player.Main.transform.position;
            _lvl.enabled = cards;
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
        }

        void Take(UpgradeDef d) => _build.Take(new CardOffer { Def = d, Rarity = Rarity.Common, Level = _build.State.LevelOf(d) + 1 });

        int SpawnAt(EnemyDef def, Vector3 offset)
        {
            var at = _p + offset;
            at.y = _em.Height != null ? _em.Height.Sample(at.x, at.z) : _p.y;
            int i = _em.Spawn(def, at);
            Assert.GreaterOrEqual(i, 0, "spawned");
            return i;
        }

        /// <summary>Only `weapon` fires (equipped fresh at level 1).</summary>
        void Arm(WeaponDef weapon)
        {
            _weapons.RemoveAll();
            Assert.IsNotNull(_weapons.Equip(weapon));
            _weapons.enabled = true;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AMaxedWeaponAndItsCatalyst_EvolveFromAChest_InTheSameSlot()
        {
            yield return Boot(weapons: false, cards: true);
            var gladiusCard = Up("UP_W_Gladius");
            var luckyCut = Up("UP_LuckyCut");
            var evo = Up("UP_E_Gladius");
            var gladius = gladiusCard.weapon;
            Assert.IsNotNull(_weapons.Find(gladius), "the Nobody starts with the gladius");
            while (_build.State.LevelOf(gladiusCard) < gladiusCard.maxLevel) Take(gladiusCard);
            Assert.AreEqual(8, _weapons.Find(gladius).Level);
            Assert.IsFalse(UpgradeRoller.IsEvolutionReady(evo, _build.State), "no catalyst yet");
            Take(luckyCut);
            Assert.IsTrue(UpgradeRoller.IsEvolutionReady(evo, _build.State));
            int weaponCount = _weapons.Weapons.Count;

            _lvl.QueueReward(PickSource.Chest, Rarity.Uncommon);
            float end = Time.realtimeSinceStartup + 3f;
            while (!_lvl.IsOpen && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_lvl.IsOpen, "the chest's cards open");
            Assert.AreEqual(evo, _lvl.Offers[0].Def, "the evolution is the chest's first card");
            Assert.AreEqual(Rarity.Legendary, _lvl.Offers[0].Rarity);
            _lvl.Choose(0);
            yield return null;

            var w = _weapons.Find(evo.weapon);
            Assert.IsNotNull(w, "the evolved weapon is equipped");
            Assert.IsNull(_weapons.Find(gladius), "the rusty gladius is gone");
            Assert.AreSame(w, _weapons.Weapons[0], "in the same slot");
            Assert.AreEqual(8, w.Level, "it keeps the level-8 numbers");
            Assert.Greater(w.Local[StatId.Damage], 1f, "its level modifiers came along");
            Assert.AreEqual(weaponCount, _weapons.Weapons.Count);
            Assert.AreEqual(1, _build.State.LevelOf(luckyCut), "the catalyst stays");
            Assert.AreEqual(1, _build.Evolutions);
            Assert.IsFalse(UpgradeRoller.IsEligible(gladiusCard, _build.State, _build.config), "the rusty gladius never comes back");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ChampionsGladius_StrikesAgainBehindYou()
        {
            yield return Boot(weapons: false);
            var brute = En("EN_Brute");
            // both rooted, the one in front clearly nearer: walking in, the one behind could become the nearest and draw the
            // auto-aimed swing itself (a flake in the full suite). Within reach behind: 2.8 m < range 2.6 + its radius 0.75.
            var root = St("SE_Root");
            // control: the rusty gladius in the same spot never touches the one behind
            int front = SpawnAt(brute, new Vector3(0f, 0f, 1.2f));
            int back = SpawnAt(brute, new Vector3(0f, 0f, -2.8f));
            _em.ApplyStatus(front, root, 0f);
            _em.ApplyStatus(back, root, 0f);
            Arm(Wp("WP_Gladius"));
            yield return new WaitForSeconds(1.2f);
            Assert.Less(_em.GetHp(front), _em.GetMaxHp(front), "control: the gladius cuts the one in front");
            Assert.AreEqual(_em.GetMaxHp(back), _em.GetHp(back), 1e-3f, "control: nothing reaches the one behind");
            _weapons.RemoveAll();
            _em.DespawnAll(); // no XP shower
            PickupSystem.Instance.ClearAll();
            yield return new WaitForSeconds(0.5f);

            front = SpawnAt(brute, new Vector3(0f, 0f, 1.2f));
            back = SpawnAt(brute, new Vector3(0f, 0f, -2.8f));
            _em.ApplyStatus(front, root, 0f);
            _em.ApplyStatus(back, root, 0f);
            Arm(Wp("WP_Evo_Gladius"));
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(_weapons.Echoes, 0, "the echo fired");
            Assert.Less(_em.GetHp(front), _em.GetMaxHp(front), "the one in front was cut");
            Assert.Less(_em.GetHp(back), _em.GetMaxHp(back), "and the one behind, by the echo");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Thunderbolts_ArcLightningFromEveryJavelinThatLands()
        {
            yield return Boot(weapons: false);
            var brute = En("EN_Brute");
            SpawnAt(brute, new Vector3(0f, 0f, 7f));
            for (int k = 0; k < 4; k++) SpawnAt(brute, new Vector3(Mathf.Cos(k * 1.57f) * 2.2f, 0f, 7f + Mathf.Sin(k * 1.57f) * 2.2f));
            int sparks = _fx.SparksFired;
            Arm(Wp("WP_Evo_Javelins"));
            yield return new WaitForSeconds(2.5f);
            Assert.Greater(_fx.SparksFired, sparks, "javelin hits sparked");
            int shocked = 0;
            for (int i = 0; i < _em.HighWater; i++) if (_em.IsAlive(i) && _em.HasStatus(i, StatusKind.Shock)) shocked++;
            Assert.GreaterOrEqual(shocked, 2, "the lightning reached the neighbours");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FirePotBarrage_LeavesTheSandBurning()
        {
            yield return Boot(weapons: false);
            var brute = En("EN_Brute");
            for (int k = 0; k < 5; k++) SpawnAt(brute, new Vector3(Mathf.Cos(k * 1.25f) * 1.5f, 0f, 6f + Mathf.Sin(k * 1.25f) * 1.5f));
            Arm(Wp("WP_Evo_BombSatchel"));
            float end = Time.realtimeSinceStartup + 4f;
            while (_fx.MarkCount == 0 && Time.realtimeSinceStartup < end) yield return null;
            Assert.Greater(_fx.MarkCount, 0, "a blast left a burning mark");
            yield return new WaitForSeconds(0.6f);
            int burning = 0;
            for (int i = 0; i < _em.HighWater; i++) if (_em.IsAlive(i) && _em.HasStatus(i, StatusKind.Burn)) burning++;
            Assert.Greater(burning, 0, "enemies in it burn");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ThreeFireUpgrades_SwitchOnWildfire_ThatSpreadsBurn()
        {
            yield return Boot(weapons: false);
            var wildfire = Load<TagSynergyDef>("Assets/Data/Synergies/SY_Wildfire.asset");
            Take(Up("UP_Kindling"));
            Take(Up("UP_Pyre"));
            Assert.IsFalse(_build.State.HasSynergy(wildfire), "2 of 3");
            Take(Up("UP_ConductiveOil"));
            Assert.IsTrue(_build.State.HasSynergy(wildfire), "3 FIRE upgrades: Wildfire is on");
            yield return null;

            var brute = En("EN_Brute");
            int lit = SpawnAt(brute, new Vector3(0f, 0f, 9f));
            for (int k = 0; k < 3; k++) SpawnAt(brute, new Vector3(Mathf.Cos(k * 2.1f) * 1.6f, 0f, 9f + Mathf.Sin(k * 2.1f) * 1.6f));
            _em.ApplyStatus(lit, St("SE_Burn"), 1f, 1);
            yield return new WaitForSeconds(2.4f);
            int burning = 0;
            for (int i = 0; i < _em.HighWater; i++) if (_em.IsAlive(i) && _em.HasStatus(i, StatusKind.Burn)) burning++;
            Assert.Greater(_fx.Spreads, 0, "Wildfire spread the fire");
            Assert.GreaterOrEqual(burning, 2, "more than the one we lit is burning");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Executioner_KillsALowEnemyOnACrit_NeverABoss()
        {
            yield return Boot(weapons: false);
            var executioner = Load<TagSynergyDef>("Assets/Data/Synergies/SY_Executioner.asset");
            Take(Up("UP_LuckyCut"));
            Take(Up("UP_SerratedEdge"));
            Assert.IsTrue(_build.State.HasSynergy(executioner), "gladius + Lucky Cut + Serrated Edge = 3 CRIT");
            yield return null;

            int low = SpawnAt(En("EN_Brute"), new Vector3(0f, 0f, 8f));
            int boss = SpawnAt(En("EN_Gorm"), new Vector3(6f, 0f, 8f));
            yield return null;
            foreach (int i in new[] { low, boss })
                _em.ApplyDamage(i, new DamageInfo { Amount = _em.GetMaxHp(i) * 0.85f, Flags = DamageFlags.Melee, Point = _em.GetPosition(i) });
            Assert.IsTrue(_em.IsAlive(low) && _em.IsAlive(boss), "both at ~15% HP");
            int uidLow = _em.GetUid(low), uidBoss = _em.GetUid(boss);
            foreach (int i in new[] { low, boss })
                _em.ApplyDamage(i, new DamageInfo { Amount = 1f, Flags = DamageFlags.Melee | DamageFlags.Crit, Point = _em.GetPosition(i) });
            Assert.IsFalse(_em.IsAlive(low) && _em.GetUid(low) == uidLow, "a crit executes the brute");
            Assert.IsTrue(_em.IsAlive(boss) && _em.GetUid(boss) == uidBoss, "the boss is never executed");
            Assert.AreEqual(1, _fx.Executions);
        }
    }
}
