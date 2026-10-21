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
    /// Phase 5.13 (docs/BOSSES.md): the Gilded Patron. He's carried in alone through the north gate on his litter; his
    /// coin bombs land on and around the player, hurt one who stays and scatter real gold; the Golden Volley fans out
    /// red-hot coins; his hired swords jump down at the two patron boxes nearest the player and, while any lives, he
    /// takes 40 % of the damage; phase 2 fires every jet row and spins every blade for good; his fortune is paid out
    /// when he falls.
    /// </summary>
    public class GildedPatronTests
    {
        const string Scene = "Assets/Scenes/Arenas/GrandArena.unity";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static EnemyDef Merc => Load<EnemyDef>("Assets/Data/Enemies/EN_GildedMercenary.asset");

        Player _player;
        PlayerMotor _motor;
        PlayerHealth _health;
        EnemyManager _em;
        BossFight _fight;
        int _damaged;

        void OnDamaged(in DamagedEvent e) { if (!e.Status) _damaged++; }

        BossController Boss => BossController.Active;

        IEnumerator BootToBoss()
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            _player = Player.Main;
            _motor = _player.GetComponent<PlayerMotor>();
            _health = _player.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f;
            _player.OverrideSource = new ScriptedInput();
            _player.GetComponent<WeaponSystem>().RemoveAll();
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            _em = EnemyManager.Instance;
            _fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(_fight, "the Grand Arena has its boss fight");
            _damaged = 0;
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            yield return new WaitForSeconds(0.3f);
            _fight.StartNow();
            float end = Time.time + 10f;
            while (Time.time < end && (BossController.Active == null || BossController.Active.CurrentState == BossController.State.Entering
                                       || BossController.Active.CurrentState == BossController.State.Roaring)) yield return null;
            Assert.AreEqual("patron", Boss.def.id);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            TimeController.ResetForNewRun();
            DevSettings.ResetAll();
        }

        static readonly Vector3 Open = new Vector3(0f, 0f, -38f); // the south arm's sand, between its terraces

        /// <summary>Puts him at `at` facing +X and the player `d` m in front of him; waits a frame.</summary>
        IEnumerator Face(Vector3 at, float d)
        {
            Boss.Warp(at, 90f);
            _motor.Teleport(at + Vector3.right * d + Vector3.up * 0.1f);
            yield return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HesCarriedIn_AloneThroughTheNorthGate_OnHisLitter()
        {
            yield return BootToBoss();
            Assert.AreEqual(1, _em.AliveCount, "the horde was dragged off: only him");
            Assert.Greater(Boss.Position.z, 40f, "in through the north arm's gate");
            Assert.AreEqual(1f, Boss.Hp01, 1e-4f);
            Assert.IsFalse(Boss.Guarded, "nobody hired yet");
            var root = _em.GetViewRoot(Boss.Slot);
            int legs = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("BLeg")) legs++;
            Assert.AreEqual(8, legs, "four bearers, two legs each");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CoinBombs_LandOnAndAroundThePlayer_HurtOneWhoStays_AndScatterGold()
        {
            yield return BootToBoss();
            yield return Face(Open, 9f);
            var proj = EnemyProjectiles.Instance;
            var pickups = PickupSystem.Instance;
            int coinsBefore = pickups.ActiveCount;
            int earned = RunState.Current.GoldEarned;
            _damaged = 0;
            Boss.ForceAttack(BossAttack.CoinBombs);
            float end = Time.time + 6f;
            while (Time.time < end && proj.BombsBurst < 3) yield return null;
            Assert.AreEqual(3, Boss.BombsThrown, "three purses in phase 1");
            Assert.AreEqual(3, proj.BombsBurst);
            Assert.GreaterOrEqual(proj.BombHits, 1, "the first lands right on a player who stays");
            Assert.GreaterOrEqual(_damaged, 1);
            yield return new WaitForSeconds(0.6f); // the magnet pulls in the coins that landed by the player
            int onSand = pickups.ActiveCount - coinsBefore;
            int collected = (RunState.Current.GoldEarned - earned) / Mathf.Max(1, proj.bombCoinValue);
            Assert.GreaterOrEqual(onSand + collected, 3 * Boss.def.bombCoins - 1, "each burst scatters gold (on the sand or already in the purse)");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheGoldenVolley_FansOutRedHotCoins_AtThePlayer()
        {
            yield return BootToBoss();
            yield return Face(Open, 8f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Volley);
            yield return new WaitForSeconds(Boss.def.volleyWindup * 0.5f);
            Assert.AreEqual(0, Boss.CoinsFired, "the fan's lanes glow first");
            float end = Time.time + 3f;
            while (Time.time < end && _damaged == 0) yield return null;
            Assert.AreEqual(Boss.def.volleyCount.x, Boss.CoinsFired, "seven coins in phase 1");
            Assert.GreaterOrEqual(_damaged, 1, "the middle coin finds a player who stands still");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HisHiredSwords_JumpDownAtTheNearestBoxes_AndGuardHim_UntilTheyDie()
        {
            yield return BootToBoss();
            yield return Face(Open, 8f);
            Boss.ForceGuards();
            int hired = _em.AliveOf(Merc);
            Assert.That(hired, Is.InRange(4, 6), "4-6 mercenaries");
            // they appear by the two patron boxes nearest the player (the southern ones), not at the gates
            var spots = Boss.guardSpots;
            Vector3 pp = Player.Main.transform.position;
            int found = 0;
            var slots = new int[12];
            int n = _em.FindAlive(Merc, slots);
            for (int k = 0; k < n; k++)
            {
                Vector3 m = _em.GetPosition(slots[k]);
                float best = float.MaxValue;
                foreach (var s in spots) best = Mathf.Min(best, Vector2.Distance(new Vector2(m.x, m.z), new Vector2(s.x, s.z)));
                Assert.Less(best, 3.5f, "by a box");
                Assert.Less(m.z, 0f, "the boxes on the player's side");
                found++;
            }
            Assert.AreEqual(hired, found);
            yield return null;
            Assert.IsTrue(Boss.Guarded, "paid for: guarded");
            float hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1000f, Flags = DamageFlags.Melee, Point = Boss.Position });
            Assert.AreEqual(hp - 1000f * Boss.def.guardedDamageMul, _em.GetHp(Boss.Slot), 1f, "he takes guardedDamageMul (60 %) while his guard lives");

            for (int k = 0; k < n; k++) _em.ApplyDamage(slots[k], new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Melee });
            yield return null;
            yield return null;
            Assert.IsFalse(Boss.Guarded, "the guard is dead");
            hp = _em.GetHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1000f, Flags = DamageFlags.Melee, Point = Boss.Position });
            Assert.AreEqual(hp - 1000f, _em.GetHp(Boss.Slot), 1f, "full damage again");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator PhaseTwo_FiresEveryJetRow_AndSpinsEveryBladeForGood()
        {
            yield return BootToBoss();
            yield return Face(Open, 10f);
            var jets = FireJets.Instance;
            var blades = BladeSweepers.Instance;
            float max = _em.GetMaxHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = max * 0.52f, Flags = DamageFlags.Melee, Point = Boss.Position });
            float end = Time.time + 3f;
            while (Time.time < end && Boss.CurrentState != BossController.State.Enraging) yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState, "\"Everything is for sale!\"");
            for (int i = 0; i < jets.Count; i++) Assert.IsTrue(jets.IsTelegraphing(i) || jets.IsBurning(i), "every row fires on his roar");
            Assert.IsTrue(blades.alwaysSpin);
            yield return new WaitForSeconds(blades.telegraph + 1f);
            for (int i = 0; i < blades.Count; i++) Assert.IsTrue(blades.IsSpinning(i), "every blade whirls");
            yield return new WaitForSeconds(blades.spin + 1f);
            for (int i = 0; i < blades.Count; i++) Assert.IsTrue(blades.IsSpinning(i), "and keeps whirling");
            Assert.AreEqual(2, Boss.Phase);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HisFortune_IsPaidOut_WhenHeFalls()
        {
            yield return BootToBoss();
            yield return Face(Open, 10f);
            var run = RunState.Current;
            int gold = run.Gold;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1e7f, Flags = DamageFlags.Melee, Point = Boss.Position });
            float end = Time.time + 3f;
            while (Time.time < end && !Boss.Defeated) yield return null;
            Assert.IsTrue(Boss.Defeated);
            Assert.AreEqual(gold + Boss.def.defeatGold, run.Gold, "THE PATRON IS BANKRUPT: his gold is yours");
            yield return null;
            Assert.AreEqual(BossFight.Stage.Falling, _fight.Current);
        }
    }
}
