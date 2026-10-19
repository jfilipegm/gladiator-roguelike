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
    /// M2.5: each weapon pattern hits what it should (spear line, javelin pierce and walls, chakram out and back) and
    /// weapon cards equip / level only their own weapon. Targets are stationary high-HP dummies on the flat runway in
    /// front of the TestArena spawn (0,0,-8) facing +Z; the pillar at (5,0,-3) is the wall.
    /// </summary>
    public class WeaponTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        static readonly Vector3 Pillar = new Vector3(5f, 0f, -3f);

        EnemyManager _em;
        Player _player;
        WeaponSystem _weapons;
        PlayerProjectiles _shots;
        EnemyDef _dummy;

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
            _weapons = _player.GetComponent<WeaponSystem>();
            _shots = PlayerProjectiles.Instance;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            spawner.enabled = false;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // kills must not pause the test on a level-up
            _player.OverrideSource = new ScriptedInput();
            _weapons.RemoveAll();
            _dummy = Object.Instantiate(spawner.enemy);
            _dummy.moveSpeed = 0f;   // stays where the test puts it
            _dummy.maxHp = 5000f;    // never dies, so every hit shows on its HP
            yield return new WaitForSeconds(0.3f);
        }

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            if (_dummy != null) Object.Destroy(_dummy);
        }

        static WeaponDef Load(string asset)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponDef>("Assets/Data/Weapons/" + asset + ".asset");
#else
            return null;
#endif
        }

        int Place(float x, float z) => _em.Spawn(_dummy, _player.transform.position + new Vector3(x, 0f, z));
        bool Hurt(int i) => _em.GetHp(i) < _em.GetMaxHp(i);

        IEnumerator WaitUntil(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !done()) yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Spear_HitsTheWholeLine_NotBesideOrBeyond()
        {
            yield return Boot();
            Assert.IsNotNull(_weapons.Equip(Load("WP_Spear")));
            int near = Place(0f, 2f);       // nearest: the thrust aims here (+Z)
            int line = Place(0f, 4f);       // same line, inside its 4.4 m reach
            int beside = Place(2.2f, 2.6f); // off the line
            int beyond = Place(0f, 6.2f);   // past the tip
            yield return WaitUntil(() => Hurt(near), 1.5f);
            yield return null;
            Assert.IsTrue(Hurt(near), "the aimed-at enemy is hit");
            Assert.IsTrue(Hurt(line), "everything along the line is hit");
            Assert.IsFalse(Hurt(beside), "an enemy beside the line is not hit");
            Assert.IsFalse(Hurt(beyond), "an enemy past the reach is not hit");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Javelin_PierceOne_HitsTwoOfThree()
        {
            yield return Boot();
            var w = _weapons.Equip(Load("WP_Javelins"));
            w.Local.Add(new StatMod { stat = StatId.Amount, op = ModOp.Flat, value = -1f }, 1f); // a single javelin
            Assert.AreEqual(1, _weapons.AmountOf(w));
            Assert.AreEqual(1, _weapons.PierceOf(w));
            int a = Place(0f, 3f), b = Place(0f, 5f), c = Place(0f, 7f);
            yield return WaitUntil(() => _shots.Count > 0, 1.5f);
            Assert.Greater(_shots.Count, 0, "a javelin was thrown");
            yield return WaitUntil(() => _shots.Count == 0, 1.5f);
            Assert.IsTrue(Hurt(a) && Hurt(b), "pierce 1: the first two in line are hit");
            Assert.IsFalse(Hurt(c), "the javelin is spent before the third");
            Assert.AreEqual(2, _shots.Hits);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Javelin_StoppedByAPillar()
        {
            yield return Boot();
            Vector3 from = new Vector3(Pillar.x, 0f, Pillar.z - 4f);
            from.y = _em.Height.Sample(from.x, from.z) + 0.9f;
            int before = _em.Spawn(_dummy, new Vector3(Pillar.x, 0f, Pillar.z - 2f));
            int after = _em.Spawn(_dummy, new Vector3(Pillar.x, 0f, Pillar.z + 2.5f));
            yield return null;
            var p = new ShotParams { Speed = 20f, Range = 16f, Radius = 0.2f, Damage = 10f, CritMultiplier = 2f, Pierce = -1, Mesh = -1, Scale = 1f };
            Assert.IsTrue(_shots.FireStraight(from, Vector3.forward, p));
            yield return WaitUntil(() => _shots.Count == 0, 2f);
            Assert.AreEqual(0, _shots.Count, "the javelin is gone");
            Assert.IsTrue(Hurt(before), "unlimited pierce: it passes through the enemy before the pillar");
            Assert.IsFalse(Hurt(after), "the pillar stops it");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Chakram_HitsOnTheWayOutAndBack_ThenReturns()
        {
            yield return Boot();
            Assert.IsNotNull(_weapons.Equip(Load("WP_Chakram")));
            int target = Place(0f, 3f);
            int far = Place(0f, 9.5f); // past its 7 m turn-around point
            yield return WaitUntil(() => _shots.Count > 0, 1.5f);
            Assert.Greater(_shots.Count, 0, "the chakram was thrown");
            yield return WaitUntil(() => _shots.Count == 0, 3f);
            Assert.AreEqual(0, _shots.Count, "it came back to the player");
            Assert.AreEqual(2, _shots.Hits, "one hit going out, one coming back");
            Assert.IsTrue(Hurt(target));
            Assert.IsFalse(Hurt(far), "it turns around before the far enemy");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WeaponCards_EquipNew_LevelOnlyTheirWeapon_FourSlots()
        {
            yield return Boot();
            var build = _player.GetComponent<PlayerBuild>();
            var gladius = Load("WP_Gladius");
            Assert.IsNotNull(_weapons.Equip(gladius));
            var spear = Load("WP_Spear");
            var spearCard = build.FindWeaponCard(spear);
            Assert.IsNotNull(spearCard, "every weapon has a card");

            build.Take(new CardOffer { Def = spearCard, Rarity = Rarity.Common, Level = 1 });
            var s = _weapons.Find(spear);
            Assert.IsNotNull(s, "a new weapon card equips the weapon");
            Assert.AreEqual(1, s.Level);
            int gladiusVersion = _weapons.Find(gladius).Local.Version, spearVersion = s.Local.Version;

            build.Take(new CardOffer { Def = spearCard, Rarity = Rarity.Rare, Level = 2 });
            Assert.AreEqual(2, s.Level);
            Assert.AreNotEqual(spearVersion, s.Local.Version, "the spear got its level-2 modifiers");
            Assert.AreEqual(gladiusVersion, _weapons.Find(gladius).Local.Version, "the gladius is untouched");
            Assert.AreEqual(2, build.State.LevelOf(spearCard));

            build.Take(new CardOffer { Def = build.FindWeaponCard(Load("WP_Javelins")), Rarity = Rarity.Common, Level = 1 });
            build.Take(new CardOffer { Def = build.FindWeaponCard(Load("WP_Chakram")), Rarity = Rarity.Common, Level = 1 });
            Assert.AreEqual(WeaponSystem.MaxSlots, _weapons.Weapons.Count);
            var extra = ScriptableObject.CreateInstance<WeaponDef>();
            try { Assert.IsNull(_weapons.Equip(extra), "no fifth weapon"); }
            finally { Object.Destroy(extra); }
        }

        /// <summary>Regression (found by the M3.5 perf run): an OnHit hook killing the same enemy re-entrantly (a spark,
        /// a burst) killed it twice, so the alive count drifted negative and the Director overfilled the pools.</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator ReentrantKill_FromAnOnHitHook_CountsOnce()
        {
            yield return Boot();
            yield return new WaitForSeconds(0.2f);
            int before = _em.AliveCount, kills = 0;
            int i = Place(0f, 6f);
            yield return null;
            Assert.AreEqual(before + 1, _em.AliveCount);
            bool inHook = false;
            EventHandler<HitEvent> hook = (in HitEvent e) =>
            {
                if (inHook || e.Index != i) return;
                inHook = true;
                _em.ApplyDamage(i, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Proc }); // kills it inside the event
                inHook = false;
            };
            EventHandler<KillEvent> counter = (in KillEvent e) => kills++;
            GameEvents.OnHit.Subscribe(hook);
            GameEvents.OnKill.Subscribe(counter);
            try { _em.ApplyDamage(i, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee }); }
            finally { GameEvents.OnHit.Unsubscribe(hook); GameEvents.OnKill.Unsubscribe(counter); }
            Assert.AreEqual(before, _em.AliveCount, "one enemy died once");
            Assert.AreEqual(1, kills, "one kill event");
        }
    }
}
