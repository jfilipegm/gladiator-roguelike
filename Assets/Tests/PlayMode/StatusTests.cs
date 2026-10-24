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
    /// M2.6: statuses on real enemies (damage over time, expiry, Shock vulnerability) and each Boon effect through the
    /// event bus: Kindling ignites, Pyre spreads fire from a burning death, Static Charge chains, Serrated Edge stacks
    /// Bleed on crits, Open Wounds scales with stacks, Blood Slide marks bleed. Boons are built in code from the real
    /// status assets; targets are stationary dummies on the runway in front of the spawn.
    /// </summary>
    public class StatusTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        EnemyManager _em;
        Player _player;
        PlayerBuild _build;
        BoonEffects _fx;
        EnemyDef _dummy;
        StatusDef _burn, _bleed, _shock;
        readonly System.Collections.Generic.List<Object> _made = new System.Collections.Generic.List<Object>();

        IEnumerator Boot()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            _em = EnemyManager.Instance;
            _player = Player.Main;
            _build = _player.GetComponent<PlayerBuild>();
            _fx = _player.GetComponent<BoonEffects>();
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll(); // the tests deal the hits themselves
            _dummy = Object.Instantiate(spawner.enemy);
            _dummy.moveSpeed = 0f;
            _dummy.maxHp = 1000f;
            _dummy.contactDamage = _dummy.attackDamage = 0f;
            _made.Add(_dummy);
            _burn = Load<StatusDef>("Assets/Data/Status/SE_Burn.asset");
            _bleed = Load<StatusDef>("Assets/Data/Status/SE_Bleed.asset");
            _shock = Load<StatusDef>("Assets/Data/Status/SE_Shock.asset");
            Assert.IsNotNull(_burn); Assert.IsNotNull(_bleed); Assert.IsNotNull(_shock);
            yield return new WaitForSeconds(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            foreach (var o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
        }

        static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }

        int Place(float x, float z) => _em.Spawn(_dummy, _player.transform.position + new Vector3(x, 0f, z));
        float Lost(int i) => _em.GetMaxHp(i) - _em.GetHp(i);

        void Give(params BoonEffect[] effects)
        {
            var d = ScriptableObject.CreateInstance<UpgradeDef>();
            d.id = d.name = "test_boon_" + _made.Count;
            d.effects = effects;
            _made.Add(d);
            _build.Take(new CardOffer { Def = d, Rarity = Rarity.Common, Level = 1 }); // potency 1
        }

        /// <summary>A direct player hit (raises OnHit/OnCrit like a weapon does).</summary>
        void Hit(int i, float amount, bool crit = false) =>
            _em.ApplyDamage(i, new DamageInfo { Amount = amount, Flags = DamageFlags.Melee | (crit ? DamageFlags.Crit : 0), Point = _em.GetPosition(i), Direction = Vector3.forward });

        [UnityTest, Timeout(60000)]
        public IEnumerator Burn_TicksDamage_ThenExpires()
        {
            yield return Boot();
            int a = Place(0f, 3f);
            _em.ApplyStatus(a, _burn, 10f);
            Assert.IsTrue(_em.HasStatus(a, StatusKind.Burn));
            yield return new WaitForSeconds(1.15f); // two 0.5 s ticks of 5
            Assert.AreEqual(10f, Lost(a), 0.01f, "10 dps for ~1 s");
            yield return new WaitForSeconds(_burn.duration);
            Assert.IsFalse(_em.HasStatus(a, StatusKind.Burn), "it wears off");
            float lost = Lost(a);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(lost, Lost(a), 0.01f, "no damage after it wore off");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Shock_MakesEnemiesTakeMoreDamage()
        {
            yield return Boot();
            int plain = Place(-1.5f, 3f), shocked = Place(1.5f, 3f);
            _em.ApplyStatus(shocked, _shock, 0f);
            Hit(plain, 100f);
            Hit(shocked, 100f);
            Assert.AreEqual(100f, Lost(plain), 0.01f);
            Assert.AreEqual(100f * (1f + _shock.damageTakenBonus), Lost(shocked), 0.01f);
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Kindling_DirectHitIgnites_ProcDamageDoesNot()
        {
            yield return Boot();
            Give(new BoonEffect { kind = EffectKind.StatusOnHit, status = _burn, chance = 1f, power = 0.5f });
            yield return null; // BoonEffects reads the build on its tick
            int a = Place(-1.5f, 3f), b = Place(1.5f, 3f);
            Hit(a, 20f);
            Assert.IsTrue(_em.HasStatus(a, StatusKind.Burn), "a direct hit ignites");
            Assert.AreEqual(10f, _em.StatusPower(a, StatusKind.Burn), 1e-3f, "50 % of the hit per second");
            _em.ApplyDamage(b, new DamageInfo { Amount = 20f, Flags = DamageFlags.Proc });
            Assert.IsFalse(_em.HasStatus(b, StatusKind.Burn), "effect damage never procs effects");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Pyre_BurningDeathSpreadsFire_NotToFarEnemies()
        {
            yield return Boot();
            Give(new BoonEffect { kind = EffectKind.BurstOnDeath, status = _burn, power = 8f, radius = 2.8f });
            yield return null;
            int victim = Place(0f, 3f), near = Place(1.6f, 3.5f), far = Place(0f, 9f);
            _em.ApplyStatus(victim, _burn, 6f);
            Hit(victim, 5000f); // dies burning
            Assert.IsFalse(_em.IsAlive(victim));
            yield return null; // the burst resolves on BoonEffects' tick
            yield return null;
            Assert.IsTrue(_em.HasStatus(near, StatusKind.Burn), "the neighbour catches fire");
            Assert.AreEqual(6f, _em.StatusPower(near, StatusKind.Burn), 1e-3f, "with the victim's burn");
            Assert.GreaterOrEqual(Lost(near), 8f, "and takes the burst");
            Assert.IsFalse(_em.HasStatus(far, StatusKind.Burn));
            Assert.AreEqual(0f, Lost(far), 1e-3f);
            Assert.AreEqual(1, _fx.BurstsFired);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator StaticCharge_ChainsToItsCount_AndShocks()
        {
            yield return Boot();
            Give(new BoonEffect { kind = EffectKind.ChainSpark, status = _shock, power = 7f, everyNth = 1, count = 2, radius = 3f });
            yield return null;
            int a = Place(0f, 3f), b = Place(0f, 5f), c = Place(0f, 7f), d = Place(0f, 9f);
            yield return null; // in the spatial grid (rebuilt each enemy tick) before the spark looks for them
            Hit(a, 1f);
            Assert.AreEqual(1, _fx.SparksFired);
            Assert.IsTrue(_em.HasStatus(a, StatusKind.Shock) && _em.HasStatus(b, StatusKind.Shock) && _em.HasStatus(c, StatusKind.Shock), "origin + 2 jumps");
            Assert.IsFalse(_em.HasStatus(d, StatusKind.Shock), "no third jump");
            Assert.AreEqual(7f * (1f + _shock.damageTakenBonus), Lost(b), 0.01f, "spark damage, after its own Shock lands");
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SerratedEdge_CritsStackBleed_OpenWoundsScalesWithStacks()
        {
            yield return Boot();
            Give(new BoonEffect { kind = EffectKind.StatusOnCrit, status = _bleed, power = 2.5f, stacks = 2 });
            Give(new BoonEffect { kind = EffectKind.DamagePerStack, status = _bleed, bonus = 0.04f });
            yield return null;
            int a = Place(0f, 3f), b = Place(2f, 3f);
            Hit(a, 1f, crit: false);
            Assert.AreEqual(0, _em.StatusStacks(a, StatusKind.Bleed), "plain hits don't bleed");
            for (int k = 0; k < 7; k++) Hit(a, 1f, crit: true);
            Assert.AreEqual(_bleed.maxStacks, _em.StatusStacks(a, StatusKind.Bleed), "2 per crit, capped");
            float before = Lost(a);
            Hit(a, 100f);
            Assert.AreEqual(100f * (1f + 0.04f * _bleed.maxStacks), Lost(a) - before, 0.01f, "+4 % per stack");
            Hit(b, 100f);
            Assert.AreEqual(100f, Lost(b), 0.01f, "no stacks, no bonus");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BloodSlide_MarksBleedEnemiesStandingInThem()
        {
            yield return Boot();
            Give(new BoonEffect { kind = EffectKind.SlideTrail, status = _bleed, power = 3f, stacks = 1, radius = 1f, duration = 2.5f });
            yield return null;
            int inside = Place(0f, 3f), outside = Place(3f, 3f);
            _fx.DebugMark(_em.GetPosition(inside));
            Assert.AreEqual(1, _fx.MarkCount);
            yield return new WaitForSeconds(0.45f);
            Assert.Greater(_em.StatusStacks(inside, StatusKind.Bleed), 0, "standing in a mark bleeds");
            Assert.AreEqual(0, _em.StatusStacks(outside, StatusKind.Bleed));
            yield return new WaitForSeconds(2.3f);
            Assert.AreEqual(0, _fx.MarkCount, "marks fade after their duration");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BloodSlide_TrailIsBloodSmears_NotRings_OtherTrailsKeepTheirRing()
        {
            yield return Boot();
            var blood = _player.GetComponent<BloodTrailRenderer>();
            Assert.IsNotNull(blood, "the player prefab carries the blood trail renderer (Gladiator/Setup/Combat/Blood Trail)");
            Assert.IsNotNull(blood.material, "with M_FxBloodTrail");
            Give(new BoonEffect { kind = EffectKind.SlideTrail, status = _bleed, power = 3f, stacks = 1, radius = 1f, duration = 2.5f });
            yield return null;
            Vector3 spot = _player.transform.position + new Vector3(0f, 0f, 3f);
            _fx.DebugMark(spot);
            yield return null;
            yield return null;
            Assert.AreEqual(1, _fx.SmearsDrawn, "the Bleed trail mark is drawn as a blood smear (note 32)");
            Assert.AreEqual(0, _fx.RingsDrawn, "and not as a telegraph ring");
            Assert.AreEqual(1, blood.DrawnLastFrame, "through the blood trail renderer");

            // Whirlwind's Slow trail isn't blood: it keeps its ring (in the Slow colour), beside the smears
            var slow = Load<StatusDef>("Assets/Data/Status/SE_Slow.asset");
            Assert.IsNotNull(slow);
            Give(new BoonEffect { kind = EffectKind.SlideTrail, status = slow, power = 0f, stacks = 1, radius = 1.2f, duration = 2f });
            yield return null;
            _fx.DebugMark(spot + new Vector3(0.8f, 0f, 0f)); // one mark per trail: Bleed + Slow
            yield return null;
            yield return null;
            Assert.AreEqual(3, _fx.MarkCount);
            Assert.AreEqual(2, _fx.SmearsDrawn, "both Bleed marks are smears");
            Assert.AreEqual(1, _fx.RingsDrawn, "the Slow mark is a ring");
            Assert.AreEqual(2, blood.DrawnLastFrame);
        }
    }
}
