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
    /// R2 (docs/BOSSES.md "The Eternal Victor"): he rises from the stage's lift at the Colosseum's centre (invulnerable,
    /// no gate) and strides in when the player is far; the Reaper's Circle, the Victor's Lunges, the Leap and (phase 2)
    /// the Grasp of the Dead telegraph, then hit; the Falx Throw flies out and back down its lane, hits a player in it once
    /// each way, misses one beside it and turns back early on a statue; at 65 % his dead rise at the two lifts nearest the
    /// player; at 30 % wind-ups ÷ 1.4 and two leaps and two throws in a row; the first 0 HP leaves his remains (1,500 HP
    /// for 8 s): destroyed, he's gone and his dead are dragged off; left alone, he rises with 20 % and a burst, and the
    /// next 0 HP is final. The five older bosses still walk in through their gates and die at 0 HP.
    /// Positions are the contract's shared facts (docs/tracks/R2-colosseum.md): the stage r 10 (top 1.2) at the centre,
    /// its lift in the middle, statues at (±9.9, ±9.9), the axis doors at z = 0 and the ring doors on the ellipse 62 × 42,
    /// so the sand south of the stage (|x| ≤ 15, z -38 to -15) is clear deck; the lifts at (±34, ±20) and (±80, ±24).
    /// Needs the Colosseum scene (the layout part), the Hypogeum, EN_SkeletalLegionnaire and EN_EternalVictor.
    /// </summary>
    public class EternalVictorTests
    {
        const string Scene = "Assets/Scenes/Arenas/Colosseum.unity";
        const string LegionnairePath = "Assets/Data/Enemies/EN_SkeletalLegionnaire.asset";
        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

        /// <summary>Clear deck south of the stage: no door, lift, stair or statue within 10 m.</summary>
        static readonly Vector3 Field = new Vector3(0f, 0f, -26f);
        /// <summary>The falx's test lane: from here 20 m east along z = -28 (x -12 to 8), all deck.</summary>
        static readonly Vector3 LaneStart = new Vector3(-12f, 0f, -28f);
        /// <summary>The south-east statue (PR_VictorStatue, a Solid circle r 1.1) and a spot behind it.</summary>
        static readonly Vector3 Statue = new Vector3(9.9f, 0f, -9.9f);
        static readonly Vector3 BehindStatue = new Vector3(9.9f, 0f, -7f);
        /// <summary>South of the ring of doors, on the bridge line x = 0: 60 m from the stage.</summary>
        static readonly Vector3 FarSouth = new Vector3(0f, 0f, -60f);
        static readonly Vector3[] Lifts =
        {
            new Vector3(34f, 0f, 20f), new Vector3(-34f, 0f, 20f), new Vector3(-34f, 0f, -20f), new Vector3(34f, 0f, -20f),
            new Vector3(80f, 0f, 24f), new Vector3(-80f, 0f, 24f), new Vector3(-80f, 0f, -24f), new Vector3(80f, 0f, -24f),
        };

        Player _player;
        PlayerMotor _motor;
        PlayerHealth _health;
        EnemyManager _em;
        BossFight _fight;
        int _damaged;
        string _phaseText, _guardsText;

        void OnDamaged(in DamagedEvent e) { if (!e.Status) _damaged++; }

        void OnAnnounce(in AnnounceEvent e)
        {
            if (e.Kind == AnnounceKind.BossPhase) _phaseText = e.Text;
            if (e.Kind == AnnounceKind.BossGuards) _guardsText = e.Text;
        }

        BossController Boss => BossController.Active;

        [SetUp]
        public void SetUp()
        {
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnAnnounce.Subscribe(OnAnnounce);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnAnnounce.Unsubscribe(OnAnnounce);
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            DevSettings.ResetAll();
        }

        /// <summary>Loads `scene` with a god-mode player on the clear deck (no weapons, no level-ups), then starts the boss
        /// and returns as soon as he exists (still entering).</summary>
        IEnumerator BootAndStart(string scene)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true; // an idle player dies in seconds
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
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
            if (lvl != null) lvl.enabled = false; // a level-up pauses the clock: Time.time waits would never end
            _em = EnemyManager.Instance;
            _fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(_fight, scene + " has its boss fight");
            _damaged = 0;
            _phaseText = _guardsText = null;
            if (scene == Scene) _motor.Teleport(Field + Vector3.up * 0.1f);
            yield return new WaitForSeconds(0.3f);
            _fight.clearHorde = true; // the boss alone (the game keeps the horde: note 38)
            _fight.StartNow();
            yield return WaitFor(() => BossController.Active != null && BossController.Active.Alive, 3f);
            Assert.IsNotNull(Boss, "the boss is in");
        }

        /// <summary>BootAndStart in the Colosseum, then waits out his rise and roar.</summary>
        IEnumerator BootToFight()
        {
            yield return BootAndStart(Scene);
            Assert.AreEqual("eternal_victor", Boss.def.id);
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Entering && Boss.CurrentState != BossController.State.Roaring, 8f);
            Assert.AreEqual(BossController.State.Chase, Boss.CurrentState, "the fight is on");
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !done()) yield return null;
        }

        /// <summary>Puts him at `at` facing +X and the player `d` m in front of him; waits a frame.</summary>
        IEnumerator Face(Vector3 at, float d)
        {
            Boss.Warp(at, 90f);
            _motor.Teleport(at + Vector3.right * d + Vector3.up * 0.1f);
            yield return null;
        }

        /// <summary>One unblockable hit to just under phase `phase`'s threshold, then his whole roar.</summary>
        IEnumerator ToPhase(int phase)
        {
            float max = _em.GetMaxHp(Boss.Slot);
            float at = phase == 2 ? Boss.def.phase2At : Boss.def.phase3At;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetHp(Boss.Slot) - max * at + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState, "he roars into phase " + phase);
            yield return WaitFor(() => Boss.Phase == phase && Boss.CurrentState != BossController.State.Enraging, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(phase, Boss.Phase);
        }

        /// <summary>His dead, gone for good: a fall into the hypogeum leaves no bone pile ("hypogeum" is the falls' source
        /// id), so nothing reassembles behind the test.</summary>
        void ClearGuards()
        {
            var legionnaire = Load<EnemyDef>(LegionnairePath);
            if (legionnaire == null) return;
            var slots = new int[64];
            int fall = GameInfo.StableId("hypogeum");
            int n = _em.FindAlive(legionnaire, slots);
            for (int k = 0; k < n; k++) _em.Execute(slots[k], fall);
        }

        /// <summary>The falx's farthest XZ distance from him while it's out, and whether it came back (waits for it).</summary>
        IEnumerator FollowFalx(float[] farthest, float seconds)
        {
            yield return WaitFor(() => Boss.DiscFlying, 2f);
            Assert.IsTrue(Boss.DiscFlying, "the falx leaves his hands");
            float end = Time.time + seconds;
            while (Time.time < end && Boss.DiscFlying)
            {
                Assert.AreEqual(BossController.State.Strike, Boss.CurrentState, "he stands and watches it: no other attack");
                farthest[0] = Mathf.Max(farthest[0], Flat(Boss.DiscPosition - Boss.Position).magnitude);
                farthest[1] = Mathf.Max(farthest[1], Boss.DiscPosition.z);
                yield return null;
            }
            Assert.IsFalse(Boss.DiscFlying, "and comes back to his hand");
        }

        // ---------------------------------------------------------------- the rise

        [UnityTest, Timeout(90000)]
        public IEnumerator HeRisesFromTheStageLift_InvulnerableUntilHeStands_ThenStridesWhenYoureFar()
        {
            yield return BootAndStart(Scene);
            Assert.AreEqual("eternal_victor", Boss.def.id);
            Assert.IsTrue(Boss.def.entranceRise);
            Assert.AreEqual(BossController.State.Entering, Boss.CurrentState);
            Assert.IsTrue(Boss.Rising, "he rises instead of walking in");
            Assert.AreEqual(1, _em.AliveCount, "the horde was dragged off: only him");
            float top = _em.Height.Sample(0f, 0f);
            Assert.AreEqual(1.2f, top, 0.15f, "the stage's lift is walkable at the stage top");
            Assert.Less(Flat(Boss.Position).magnitude, 0.5f, "at the arena's centre, not a gate");
            Assert.Less(Boss.Position.y, top - 2.5f, "he starts below the deck");
            Assert.IsNotNull(FindDeep(_em.GetViewRoot(Boss.Slot), Boss.def.discPart), "EN_EternalVictor has its Falx");

            float hp = Boss.Hp01;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 500f, Flags = DamageFlags.Status });
            Assert.AreEqual(hp, Boss.Hp01, 1e-6f, "invulnerable while he rises");
            float t0 = Time.time, lastY = Boss.Position.y;
            while (Time.time < t0 + Boss.def.riseTime + 1f && Boss.CurrentState == BossController.State.Entering)
            {
                Assert.GreaterOrEqual(Boss.Position.y, lastY - 1e-4f, "always rising");
                Assert.Less(Flat(Boss.Position).magnitude, 0.5f, "straight up, no walking");
                lastY = Boss.Position.y;
                yield return null;
            }
            Assert.AreEqual(BossController.State.Roaring, Boss.CurrentState, "up: the salute");
            Assert.AreEqual(Boss.def.riseTime, Time.time - t0, 0.35f, "the rise lasts riseTime");
            Assert.AreEqual(top, Boss.Position.y, 0.05f, "standing on the stage");
            Assert.IsFalse(Boss.Rising);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 500f, Flags = DamageFlags.Status });
            Assert.AreEqual(hp, Boss.Hp01, 1e-6f, "invulnerable while he roars");
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Roaring, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(BossController.State.Chase, Boss.CurrentState);

            // 60 m away: he strides (closeInSpeed 7) and doesn't attack
            _motor.Teleport(FarSouth + Vector3.up * 0.1f);
            yield return null;
            Assert.Greater(Flat(_player.transform.position - Boss.Position).magnitude, Boss.def.closeInRange + 20f);
            Vector3 from = Boss.Position;
            int attacks = Boss.AttacksDone;
            float t1 = Time.time;
            yield return new WaitForSeconds(1f);
            float speed = Flat(Boss.Position - from).magnitude / (Time.time - t1);
            Assert.Greater(speed, 6f, "he strides while you're farther than closeInRange");
            Assert.AreEqual(BossController.State.Chase, Boss.CurrentState, "no attack at that range");
            Assert.AreEqual(attacks, Boss.AttacksDone);
        }

        // ---------------------------------------------------------------- phase 1 attacks

        [UnityTest, Timeout(90000)]
        public IEnumerator ReapersCircle_FillsItsRing_ThenCutsInsideIt_NotAJumpOrBeyondIt()
        {
            yield return BootToFight();
            yield return Face(Field, 3f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Sweep);
            Assert.AreEqual(BossController.State.Windup, Boss.CurrentState);
            Assert.AreEqual(Boss.def.sweepWindup, Boss.StateDuration, 1e-4f, "phase 1: the listed wind-up");
            yield return new WaitForSeconds(Boss.def.sweepWindup * 0.5f);
            Assert.AreEqual(0, _damaged, "the ring fills first");
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 2f);
            Assert.AreEqual(1, _damaged, "inside the ring: cut");

            // a jump clears it: feet 1.5 m up just before the strike
            yield return Face(Field, 3f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Sweep);
            float strikeAt = Time.time + Boss.StateDuration;
            while (Time.time < strikeAt - 0.06f) yield return null;
            _motor.Teleport(Boss.Position + Vector3.right * 3f + Vector3.up * 1.5f);
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 2f);
            Assert.AreEqual(0, _damaged, "jumped over the falx");

            // beyond the ring
            yield return Face(Field, Boss.def.sweepRadius + 1.5f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Sweep);
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 2f);
            Assert.AreEqual(BossController.State.Recover, Boss.CurrentState);
            Assert.AreEqual(0, _damaged, "outside the ring: missed");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator VictorsLunges_TwoThrustsInPhaseOne_EachTelegraphed()
        {
            yield return BootToFight();
            yield return Face(Field, 4f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Flurry);
            Assert.AreEqual(Boss.def.flurryWindup, Boss.StateDuration, 1e-4f);
            yield return new WaitForSeconds(Boss.def.flurryWindup * 0.5f);
            Assert.AreEqual(0, _damaged, "the lane fills first");
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 3f);
            Assert.AreEqual(Boss.def.flurryThrusts.x, _damaged, "two thrusts in phase 1, each re-aimed at you");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator LeapOfTheVictor_TrailsYou_LocksThenLandsOnYou_OnceInPhaseOne()
        {
            yield return BootToFight();
            yield return Face(Field, 10f);
            _damaged = 0;
            int pounces = Boss.Pounces;
            Boss.ForceAttack(BossAttack.Pounce);
            Assert.AreEqual(Boss.def.pounceWindup, Boss.StateDuration, 1e-4f);
            yield return new WaitForSeconds(Boss.def.pounceWindup * 0.5f);
            Assert.AreEqual(0, _damaged, "the circle trails you first");
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 4f);
            Assert.AreEqual(pounces + 1, Boss.Pounces, "one leap in phase 1");
            Assert.AreEqual(1, _damaged, "landed on you");
            Assert.Less(Flat(Boss.Position - _player.transform.position).magnitude, Boss.def.pounceRadius + 8f, "he came to you");
        }

        // ---------------------------------------------------------------- the falx

        [UnityTest, Timeout(90000)]
        public IEnumerator FalxThrow_FliesOutAndBack_HitsYouInItsLaneOnceEachWay_MissesYouBesideIt()
        {
            yield return BootToFight();
            Assert.IsNotNull(FindDeep(_em.GetViewRoot(Boss.Slot), Boss.def.discPart), "the falx is a part of its own");
            yield return Face(LaneStart, 8f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Disc);
            Assert.AreEqual(BossAttack.Disc, Boss.CurrentAttack);
            Assert.AreEqual(Boss.def.discWindup, Boss.StateDuration, 1e-4f, "phase 1: the listed wind-up");
            yield return new WaitForSeconds(Boss.def.discWindup * 0.5f);
            Assert.AreEqual(0, Boss.DiscsThrown, "the lane fills first");
            Assert.IsFalse(Boss.DiscFlying);
            var far = new float[2];
            yield return FollowFalx(far, 3f);
            Assert.AreEqual(1, Boss.DiscsThrown);
            Assert.AreEqual(0, Boss.DiscsStopped, "nothing in its way");
            Assert.Greater(far[0], Boss.def.discLength - 0.5f, "out the lane's whole length");
            Assert.AreEqual(2, Boss.DiscHits, "once out, once back");
            Assert.AreEqual(2, _damaged);
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Strike, 1f);
            Assert.AreNotEqual(BossController.State.Windup, Boss.CurrentState, "once in phase 1");

            // the lane is set as the wind-up starts: a player 2.5 m beside it isn't touched, either way
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Chase, 2f);
            yield return Face(LaneStart, 8f);
            _damaged = 0;
            int hits = Boss.DiscHits;
            Boss.ForceAttack(BossAttack.Disc);
            yield return null;
            _motor.Teleport(LaneStart + new Vector3(8f, 0.1f, 2.5f));
            yield return FollowFalx(new float[2], 3f);
            Assert.AreEqual(hits, Boss.DiscHits, "beside the lane: missed");
            Assert.AreEqual(0, _damaged);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator FalxThrow_TurnsBackEarlyOnAStatue_SoTheStatueIsCover()
        {
            yield return BootToFight();
            // south of the south-east statue, the player 19 m north of him, behind it: in reach of a 20 m throw
            Boss.Warp(new Vector3(Statue.x, 0f, -26f), 0f);
            _motor.Teleport(BehindStatue + Vector3.up * 0.1f);
            yield return null;
            Assert.IsTrue(_em.Obstacles.Overlaps(new Vector2(Statue.x, Statue.z), 0.4f), "the statue is stone (a Solid obstacle)");
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Disc);
            var far = new float[] { 0f, float.MinValue };
            yield return FollowFalx(far, 3f);
            Assert.AreEqual(1, Boss.DiscsStopped, "it hit the statue");
            Assert.Less(far[1], Statue.z, "and flew back before passing it");
            Assert.Less(far[0], Boss.def.discLength - 3f, "short of the lane's end");
            Assert.AreEqual(0, _damaged, "the player behind the statue is safe");
        }

        // ---------------------------------------------------------------- phase 2

        [UnityTest, Timeout(90000)]
        public IEnumerator PhaseTwo_RiseBrothers_HisDeadComeUpTheTwoLiftsNearestYou_AndTheGraspHoldsYou()
        {
            yield return BootToFight();
            var legionnaire = Load<EnemyDef>(LegionnairePath);
            Assert.IsNotNull(legionnaire, "EN_SkeletalLegionnaire exists (ColosseumSetup)");
            Assert.AreSame(legionnaire, Boss.def.guards[0], "his guards are his dead");
            Assert.AreEqual(0, Boss.GuardWaves, "none in phase 1");
            _motor.Teleport(Field + Vector3.up * 0.1f);
            yield return null;
            float max = _em.GetMaxHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = max * (1f - Boss.def.phase2At) + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState);
            Assert.AreEqual(Boss.def.phase2Text, _phaseText, "\"RISE, BROTHERS!\"");
            yield return WaitFor(() => Boss.GuardWaves > 0, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(2, Boss.Phase);
            Assert.AreEqual(1, Boss.GuardWaves, "his dead, as the roar ends");
            Assert.AreEqual(Boss.def.guardsText, _guardsText, "THE DEAD RISE!");
            int n = _em.AliveOf(legionnaire);
            Assert.That(n, Is.InRange(Boss.def.guardCount.x, Boss.def.guardCount.y), "6-8 Legionnaires");

            // the two lifts nearest the player at (0, -26) are (34, -20) and (-34, -20)
            var slots = new int[16];
            int found = _em.FindAlive(legionnaire, slots);
            bool east = false, west = false;
            for (int k = 0; k < found; k++)
            {
                Vector3 p = _em.GetPosition(slots[k]);
                float de = Flat(p - Lifts[3]).magnitude, dw = Flat(p - Lifts[2]).magnitude;
                Assert.Less(Mathf.Min(de, dw), 4f, "each comes up one of the two lifts nearest you");
                if (de < dw) east = true; else west = true;
            }
            Assert.IsTrue(east && west, "both lifts");
            ClearGuards();

            // the Grasp of the Dead: a wide lane of cracks, then bone hands: damage, and you're held (×0.1) for 1 s
            yield return Face(Field, 8f);
            _damaged = 0;
            Boss.ForceAttack(BossAttack.Whip);
            Assert.AreEqual(Boss.def.whipWindup / Boss.def.enrageSpeed, Boss.StateDuration, 1e-4f, "phase 2: wind-ups ÷ 1.2");
            yield return new WaitForSeconds(Boss.StateDuration * 0.5f);
            Assert.AreEqual(0, _damaged, "the cracks run first");
            yield return WaitFor(() => _damaged > 0, 2f);
            Assert.AreEqual(1, _damaged, "the hands burst up under you");
            Assert.AreEqual(1, Boss.Grasps);
            Assert.AreEqual(Boss.def.whipSlow, _motor.SlowMultiplier, 1e-3f, "held: speed ×0.1");
            yield return new WaitForSeconds(Boss.def.whipSlowSeconds + 0.3f);
            Assert.AreEqual(1f, _motor.SlowMultiplier, 1e-3f, "and let go after a second");
        }

        // ---------------------------------------------------------------- phase 3

        [UnityTest, Timeout(120000)]
        public IEnumerator PhaseThree_TheArenaIsMine_WindupsOver1Point4_TwoLeapsAndTwoThrowsInARow()
        {
            yield return BootToFight();
            yield return ToPhase(2);
            ClearGuards();
            yield return Face(Field, 8f);
            float max = _em.GetMaxHp(Boss.Slot);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = _em.GetHp(Boss.Slot) - max * Boss.def.phase3At + 10f, Flags = DamageFlags.Status });
            yield return null;
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState, "he roars again at 30 %");
            Assert.AreEqual(Boss.def.phase3Text, _phaseText, "\"THE ARENA IS MINE!\"");
            yield return WaitFor(() => Boss.CurrentState != BossController.State.Enraging, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(3, Boss.Phase);
            ClearGuards();

            yield return Face(Field, 3f);
            Boss.ForceAttack(BossAttack.Sweep);
            Assert.AreEqual(Boss.def.sweepWindup / Boss.def.phase3Speed, Boss.StateDuration, 1e-4f, "phase 3: wind-ups ÷ phase3Speed");

            // two throws, re-aimed between them
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Chase, 3f);
            yield return Face(LaneStart, 8f);
            int thrown = Boss.DiscsThrown;
            Boss.ForceAttack(BossAttack.Disc);
            Assert.AreEqual(Boss.def.discWindup / Boss.def.phase3Speed, Boss.StateDuration, 1e-4f);
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover || Boss.CurrentState == BossController.State.Chase, 8f);
            Assert.AreEqual(thrown + 2, Boss.DiscsThrown, "phase3DiscRepeats: two, not three");
            Assert.IsFalse(Boss.DiscFlying);

            // two leaps
            ClearGuards();
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Chase, 3f);
            yield return Face(Field, 10f);
            int pounces = Boss.Pounces;
            Boss.ForceAttack(BossAttack.Pounce);
            Assert.AreEqual(Boss.def.pounceWindup / Boss.def.phase3Speed, Boss.StateDuration, 1e-4f);
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Recover, 6f);
            Assert.AreEqual(pounces + 2, Boss.Pounces, "phase3PounceRepeats: two in a row");
        }

        // ---------------------------------------------------------------- eternal

        /// <summary>Phase 3, his dead cleared, the player `d` m east of him on the clear deck; then a lethal hit.</summary>
        IEnumerator ToRemains(float d)
        {
            yield return BootToFight();
            yield return ToPhase(2);
            ClearGuards();
            yield return ToPhase(3);
            ClearGuards();
            yield return Face(Field, d);
            yield return WaitFor(() => Boss.CurrentState == BossController.State.Chase || Boss.CurrentState == BossController.State.Recover, 3f);
            yield return Face(Field, d);
            _phaseText = null;
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1e7f, Flags = DamageFlags.Status });
            yield return null;
            yield return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheFalseDeath_HisRemainsHold1500_DestroyThemAndHesGone_HisDeadDraggedOff()
        {
            yield return ToRemains(6f);
            Assert.IsTrue(Boss.InRemains, "he doesn't die: he collapses");
            Assert.AreEqual(BossController.State.Remains, Boss.CurrentState);
            Assert.IsFalse(Boss.Defeated);
            Assert.IsTrue(Boss.Alive);
            Assert.AreEqual(1, Boss.FalseDeaths);
            Assert.AreEqual(Boss.def.remainsHp, _em.GetHp(Boss.Slot), 0.01f, "his remains: 1,500 HP");
            Assert.AreEqual(Boss.def.remainsText, _phaseText, "HE WILL NOT STAY DOWN!");
            Assert.AreEqual(BossFight.Stage.Fight, _fight.Current);
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 100f, Flags = DamageFlags.Status });
            Assert.AreEqual(Boss.def.remainsHp, _em.GetHp(Boss.Slot), 0.01f, "a moment's grace as he falls");
            Vector3 at = Boss.Position;
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(Boss.InRemains);
            Assert.Less(Flat(Boss.Position - at).magnitude, 0.01f, "a heap: he doesn't move");

            // destroy the remains in time: gone for good
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = Boss.def.remainsHp + 10f, Flags = DamageFlags.Status });
            yield return null;
            yield return null;
            Assert.IsTrue(Boss.Defeated, "the remains destroyed: he's gone");
            Assert.AreEqual(BossFight.Stage.Falling, _fight.Current);
            Assert.AreEqual(0, _em.AliveCount, "his dead crumble: every enemy is dragged off");
            Assert.AreEqual(0, Boss.RemainsRisen);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheFalseDeath_LeftAlone_HeRisesWith20Percent_TheBurstThrowsYouBack_TheNextZeroIsFinal()
        {
            yield return ToRemains(2.8f); // within the burst's 4 m, outside his touch
            Assert.IsTrue(Boss.InRemains);
            int waves = Boss.GuardWaves;
            _damaged = 0;
            TimeController.GameSpeed = 4f;
            yield return new WaitForSeconds(Boss.def.remainsTime - 0.5f); // game seconds
            Assert.IsTrue(Boss.InRemains, "still lying there");
            Assert.AreEqual(0, _damaged, "his remains are harmless until they rise");
            yield return WaitFor(() => Boss.RemainsRisen > 0, 2f);
            TimeController.GameSpeed = 1f;
            Assert.AreEqual(1, Boss.RemainsRisen, "the ring filled: he rises");
            Assert.IsFalse(Boss.InRemains);
            Assert.IsFalse(Boss.Defeated);
            Assert.AreEqual(Boss.def.riseHp01, Boss.Hp01, 0.01f, "with 20 % of his HP");
            Assert.AreEqual(Boss.def.riseText, _phaseText, "\"NOT YET.\"");
            Assert.AreEqual(BossController.State.Enraging, Boss.CurrentState, "a roar as he stands");
            Assert.AreEqual(waves, Boss.GuardWaves, "the guard clock waited while he lay there");
            Assert.AreEqual(1, Boss.RiseBurstHits, "the burst hit the player within 4 m");
            Assert.GreaterOrEqual(_damaged, 1);
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(Flat(_player.transform.position - Boss.Position).magnitude, 2.8f + 0.5f, "and threw them back");

            yield return WaitFor(() => Boss.CurrentState != BossController.State.Enraging, Boss.def.enrageRoar + 1f);
            Assert.AreEqual(3, Boss.Phase, "the fight goes on in phase 3");
            _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1e7f, Flags = DamageFlags.Status });
            yield return null;
            yield return null;
            Assert.IsTrue(Boss.Defeated, "the next 0 HP is final");
            Assert.AreEqual(1, Boss.FalseDeaths, "only once");
        }

        // ---------------------------------------------------------------- regression: the older bosses

        [UnityTest, Timeout(300000)]
        public IEnumerator TheOlderBosses_StillWalkInThroughTheirGates_AndDieAtZero()
        {
            string[] scenes =
            {
                "Assets/Scenes/Arenas/SlavePit.unity", "Assets/Scenes/Arenas/ProvincialPit.unity",
                "Assets/Scenes/Arenas/StoneAmphitheater.unity", "Assets/Scenes/Arenas/GrandArena.unity",
                "Assets/Scenes/Arenas/ImperialArena.unity",
            };
            string[] ids = { "gorm", "champion", "beastlord", "patron", "emperors_champion" };
            for (int s = 0; s < scenes.Length; s++)
            {
                yield return BootAndStart(scenes[s]);
                Assert.AreEqual(ids[s], Boss.def.id, scenes[s]);
                Assert.IsFalse(Boss.def.entranceRise || Boss.def.falseDeath, ids[s] + ": no rise, no false death");
                Assert.IsFalse(Boss.Rising, ids[s] + " walks in");
                var gates = ArenaGates.Instance;
                Assert.IsNotNull(gates, scenes[s] + " has gates");
                Vector3 gate = gates.Position(Mathf.Clamp(_fight.gate, 0, gates.Count - 1));
                Assert.Less(Flat(Boss.Position - gate).magnitude, 3f, ids[s] + " starts at his gate");
                yield return WaitFor(() => Boss.CurrentState != BossController.State.Entering && Boss.CurrentState != BossController.State.Roaring, 8f);
                Assert.Less(Flat(Boss.Position - gate).magnitude, 10f, ids[s] + " walked in through it");
                _em.ApplyDamage(Boss.Slot, new DamageInfo { Amount = 1e7f, Flags = DamageFlags.Status });
                yield return null;
                yield return null;
                Assert.IsTrue(Boss.Defeated, ids[s] + " dies at 0 HP");
                Assert.IsFalse(Boss.InRemains);
                Assert.AreEqual(0, Boss.FalseDeaths);
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
