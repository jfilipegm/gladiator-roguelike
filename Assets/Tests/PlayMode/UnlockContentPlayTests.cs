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
    /// Phase 5.6 (docs/UNLOCKS.md) rewards in a real run: the Sling aims at the farthest enemy, the Net pins what it lands
    /// on, Gorm's Whip cracks a line whose tip always crits and spreads extra cracks over the next-nearest enemies; the new
    /// Boons switch on Whirlwind (its slow trail beside Blood Slide's bleed), Midas and Prodigy.
    /// </summary>
    public class UnlockContentPlayTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";

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
        static TagSynergyDef Sy(string file) => Load<TagSynergyDef>("Assets/Data/Synergies/" + file + ".asset");

        /// <summary>The test arena with a quiet Director, no level-up cards (a stray one would pause the waits), weapons off.</summary>
        IEnumerator Boot()
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
            _weapons.enabled = false;
            yield return new WaitForSeconds(0.3f);
            PickupSystem.Instance.ClearAll();
            _build = Player.Main.GetComponent<PlayerBuild>();
            _fx = Player.Main.GetComponent<BoonEffects>();
            _p = Player.Main.transform.position;
            LevelUpController.Instance.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
        }

        void Take(UpgradeDef d) => _build.Take(new CardOffer { Def = d, Rarity = Rarity.Common, Level = _build.State.LevelOf(d) + 1 });

        int SpawnAt(EnemyDef def, Vector3 offset, bool rooted = true)
        {
            var at = _p + offset;
            at.y = _em.Height != null ? _em.Height.Sample(at.x, at.z) : _p.y;
            int i = _em.Spawn(def, at);
            Assert.GreaterOrEqual(i, 0, "spawned");
            if (rooted) _em.ApplyStatus(i, St("SE_Root"), 0f); // pinned: the test checks where the weapon aims, not who walks in
            return i;
        }

        void Arm(WeaponDef weapon)
        {
            _weapons.RemoveAll();
            Assert.IsNotNull(_weapons.Equip(weapon));
            _weapons.enabled = true;
        }

        bool Hurt(int i) => _em.GetHp(i) < _em.GetMaxHp(i) - 1e-3f;

        [UnityTest, Timeout(60000)]
        public IEnumerator Sling_HitsTheFarthestEnemyInReach()
        {
            yield return Boot();
            var brute = En("EN_Brute");
            int near = SpawnAt(brute, new Vector3(3f, 0f, 0f));
            int far = SpawnAt(brute, new Vector3(0f, 0f, 12f));
            int outOfReach = SpawnAt(brute, new Vector3(0f, 0f, -18f));
            Arm(Wp("WP_Sling"));
            yield return new WaitForSeconds(1f); // one shot (cooldown 1.3 s)
            Assert.IsTrue(Hurt(far), "the stone flew to the farthest one in reach");
            Assert.IsFalse(Hurt(near), "not the nearest");
            Assert.IsFalse(Hurt(outOfReach), "nothing beyond its reach");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Net_PinsEverythingItLandsOn()
        {
            yield return Boot();
            var convict = En("EN_Convict");
            int[] pack =
            {
                SpawnAt(convict, new Vector3(0f, 0f, 5f), false), SpawnAt(convict, new Vector3(0.8f, 0f, 5.4f), false),
                SpawnAt(convict, new Vector3(-0.8f, 0f, 5.4f), false), SpawnAt(convict, new Vector3(0f, 0f, 6.1f), false),
            };
            foreach (int i in pack) Assert.IsFalse(_em.HasStatus(i, StatusKind.Root));
            Arm(Wp("WP_Net"));
            yield return new WaitForSeconds(1.2f);
            int pinned = 0;
            foreach (int i in pack) if (_em.IsAlive(i) && _em.HasStatus(i, StatusKind.Root)) pinned++;
            Assert.GreaterOrEqual(pinned, 3, "one wide net pins most of the pack");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator GormsWhip_CracksALine_TheTipAlwaysCrits_AndBleeds()
        {
            yield return Boot();
            var brute = En("EN_Brute");
            int mid = SpawnAt(brute, new Vector3(0f, 0f, 2.5f));
            int tip = SpawnAt(brute, new Vector3(0f, 0f, 5.2f)); // within the last 1.5 m of its 6 m
            int tipCrits = 0;
            EventHandler<HitEvent> count = (in HitEvent e) =>
            {
                if (e.Index == tip && e.Crit) tipCrits++;
            };
            GameEvents.OnHit.Subscribe(count);
            Arm(Wp("WP_GormsWhip"));
            yield return new WaitForSeconds(0.8f); // one crack (cooldown 0.9 s)
            GameEvents.OnHit.Unsubscribe(count);
            Assert.IsTrue(Hurt(mid) && Hurt(tip), "the crack goes through the whole line");
            Assert.Greater(_weapons.TipHits, 0);
            Assert.Greater(tipCrits, 0, "the tip always crits");
            Assert.IsTrue(_em.HasStatus(tip, StatusKind.Bleed), "every crack bleeds");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ExtraWhipCracks_GoForTheNextNearest()
        {
            yield return Boot();
            var card = Up("UP_W_GormsWhip");
            _weapons.RemoveAll();
            for (int k = 0; k < 3; k++) Take(card); // level 3: a second crack
            var w = _weapons.Find(card.weapon);
            Assert.IsNotNull(w);
            Assert.AreEqual(3, w.Level);
            Assert.AreEqual(2, _weapons.AmountOf(w));
            var brute = En("EN_Brute");
            int a = SpawnAt(brute, new Vector3(2f, 0f, 0f));
            int b = SpawnAt(brute, new Vector3(0f, 0f, 3f)); // a quarter turn away: one line can't reach both
            _weapons.enabled = true;
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(Hurt(a), "the first crack, at the nearest");
            Assert.IsTrue(Hurt(b), "the second, at the next-nearest");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TheNewBoons_SwitchOnWhirlwindMidasAndProdigy()
        {
            yield return Boot();
            var stats = Player.Main.GetComponent<PlayerStats>().Block;
            Take(Up("UP_LightFeet"));
            Take(Up("UP_BloodSlide"));
            Assert.IsFalse(_build.State.HasSynergy(Sy("SY_Whirlwind")));
            Take(Up("UP_WingedSandals"));
            yield return null;
            Assert.IsTrue(_build.State.HasSynergy(Sy("SY_Whirlwind")), "three MOVEMENT upgrades");
            Assert.AreEqual(1f, stats.Get(StatId.ExtraJumps), "Winged Sandals: a jump in mid-air");

            int convict = SpawnAt(En("EN_Convict"), new Vector3(2.5f, 0f, 0f));
            _fx.DebugMark(_em.GetPosition(convict));
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(_em.HasStatus(convict, StatusKind.Bleed), "Blood Slide's trail");
            Assert.IsTrue(_em.HasStatus(convict, StatusKind.Slow), "and Whirlwind's, side by side");

            float rerolls = stats.Get(StatId.Rerolls);
            Take(Up("UP_Fortune"));
            Take(Up("UP_LoadedDice"));
            Take(Up("UP_GildedPalm"));
            Take(Up("UP_Greedy"));
            Take(Up("UP_QuickStudy"));
            Take(Up("UP_WaxTablet"));
            yield return null;
            Assert.IsTrue(_build.State.HasSynergy(Sy("SY_Midas")), "three GOLD upgrades");
            Assert.IsTrue(_build.State.HasSynergy(Sy("SY_Prodigy")), "three XP upgrades");
            Assert.AreEqual(rerolls + 2f, stats.Get(StatId.Rerolls), 1e-3f, "Wax Tablet's reroll and Prodigy's");
        }
    }
}
