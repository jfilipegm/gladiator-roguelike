using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>
    /// R2 (docs/ARENAS.md §6 "the trapdoors" / "the lifts", docs/BOSSES.md "The Colosseum through the fight"): a drop
    /// telegraphs 2 s with the doors shut, opens 4 s and closes; an ordinary enemy on it (or walking in) dies, an elite
    /// takes 300 and lives, one beside it is untouched; a grounded player on it is hit once (22 × 1.55) and thrown off, a
    /// jumping one isn't; the schedule drops the nearest group at 0:40, then another group 14 s later; a lift wave raises
    /// the 3 lifts nearest the player but not within 12 m, with packs from the active entries, and a full cap stops a
    /// pack; a sinking lift throws the player off; a door left open keeps the horde out but not an arrow, and throws the
    /// player out, hurting at most once a second; the boss's rise lifts the stage's platform, his phase 2 opens all 30
    /// (dusk), phase 3 holds all 30 (last light), his guards raise the lifts nearest the player, his fall closes them all
    /// within 3 s; Halt stops drops and waves. Needs the Colosseum scene (the layout part) and AL_Colosseum's numbers
    /// (docs/tracks/R2-colosseum.md); the Director is off and the schedule halted unless a test needs it: each test stages
    /// what it checks.
    /// </summary>
    public class HypogeumTests
    {
        const string Scene = "Assets/Scenes/Arenas/Colosseum.unity";

        /// <summary>Clear deck south of the stage (the player spawn): no door, lift, stair or statue within 8 m.</summary>
        static readonly Vector3 Safe = new Vector3(0f, 0.1f, -26f);
        /// <summary>The east axis door at (32, 0), yaw 0: 8 m along X (28 → 36), 4 m across Z (−2 → 2), group 5.</summary>
        static readonly Vector3 AxisDoor = new Vector3(32f, 0f, 0f);

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static EnemyDef Enemy(string asset) => Load<EnemyDef>("Assets/Data/Enemies/" + asset + ".asset");

        EnemyManager _em;
        PlayerHealth _health;
        PlayerMotor _motor;
        Hypogeum _hyp;
        readonly List<float> _hitTimes = new List<float>();
        readonly List<float> _hitAmounts = new List<float>();

        void OnDamaged(in DamagedEvent e)
        {
            if (e.Status) return;
            _hitTimes.Add(Time.time);
            _hitAmounts.Add(e.Damage);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        IEnumerator Boot(bool halt = true)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true; // the player stands idle in the horde's way
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            var director = Object.FindAnyObjectByType<Director>();
            if (director != null) director.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // a level-up pauses the clock: Time.time waits would never end
            var master = Object.FindAnyObjectByType<ArenaMaster>();
            if (master != null) master.enabled = false; // no whims: a bored crowd would send a champion into the count
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().RemoveAll();
            _em = EnemyManager.Instance;
            _em.KillAll();
            _health = Player.Main.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f; // a second hit would land: the hypogeum must not deal it
            _motor = Player.Main.GetComponent<PlayerMotor>();
            _hyp = Hypogeum.Instance;
            Assert.IsNotNull(_hyp, "the Colosseum has its hypogeum");
            Assert.AreEqual(30, _hyp.Count, "30 trapdoors");
            Assert.AreEqual(8, _hyp.LiftCount, "8 lifts");
            if (halt) _hyp.Halt();
            _hitTimes.Clear();
            _hitAmounts.Clear();
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            Park(Safe);
            yield return new WaitForSeconds(0.2f);
        }

        void Park(Vector3 p) => _motor.Teleport(p);
        Vector3 PlayerPos => Player.Main.transform.position;
        float DamageMul => RunState.Current != null ? RunState.Current.playerDamageTakenMul : 1f;

        int Door(Vector3 at)
        {
            int d = _hyp.NearestDoor(at);
            Assert.GreaterOrEqual(d, 0);
            Assert.Less(Vector3.Distance(Flat(_hyp.DoorCenter(d)), Flat(at)), 0.05f, "a door at " + at);
            return d;
        }

        int Lift(float x, float z)
        {
            for (int l = 0; l < _hyp.LiftCount; l++)
                if (Vector3.Distance(Flat(_hyp.LiftCenter(l)), new Vector3(x, 0f, z)) < 0.05f) return l;
            Assert.Fail("no lift at (" + x + ", " + z + ")");
            return -1;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        int CountDoors(System.Func<int, bool> state)
        {
            int n = 0;
            for (int d = 0; d < _hyp.Count; d++) if (state(d)) n++;
            return n;
        }

        static IEnumerator WaitFor(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !done()) yield return null;
        }

        /// <summary>Keeps the staged enemies rooted (SE_Root lasts 2 s) for `seconds`.</summary>
        IEnumerator Hold(int[] slots, float seconds)
        {
            var root = Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
            Assert.IsNotNull(root);
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                foreach (int e in slots) if (_em.IsAlive(e)) _em.ApplyStatus(e, root, 0f);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ drops

        [UnityTest, Timeout(60000)]
        public IEnumerator ADrop_Telegraphs2sWithTheDoorsShut_Opens4s_AndIsClosedBy6Point7s()
        {
            yield return Boot();
            int drops = _hyp.Drops;
            float t0 = Time.time;
            _hyp.Drop(5);
            Assert.AreEqual(5, _hyp.LastGroup);
            Assert.AreEqual(drops + 1, _hyp.Drops);
            int group = 0;
            for (int d = 0; d < _hyp.Count; d++) if (_hyp.GroupOf(d) == 5) group++;
            Assert.AreEqual(3, group, "the east axis: 3 doors");

            yield return new WaitForSeconds(1.8f);
            for (int d = 0; d < _hyp.Count; d++)
            {
                bool mine = _hyp.GroupOf(d) == 5;
                Assert.AreEqual(mine, _hyp.IsTelegraphing(d), "door " + d + " telegraphs only if it's in group 5");
                Assert.IsFalse(_hyp.IsOpen(d), "nothing is open during the telegraph (door " + d + ")");
            }
            Assert.Less(_hyp.Danger(AxisDoor), 0.5f, "the bot sees it coming");
            Assert.IsTrue(float.IsPositiveInfinity(_hyp.Danger(Safe)), "nothing under the spawn");

            yield return WaitFor(() => Time.time - t0 >= 2.25f, 3f);
            Assert.AreEqual(3, CountDoors(d => _hyp.GroupOf(d) == 5 && _hyp.IsOpen(d)), "open at 2 s");
            Assert.AreEqual(0, CountDoors(d => _hyp.GroupOf(d) != 5 && !_hyp.IsClosed(d)), "the other groups stay shut");
            Assert.AreEqual(0f, _hyp.Danger(AxisDoor));

            yield return WaitFor(() => Time.time - t0 >= 5.8f, 5f);
            Assert.AreEqual(3, CountDoors(d => _hyp.GroupOf(d) == 5 && _hyp.IsOpen(d)), "still open at 5.8 s");

            yield return WaitFor(() => Time.time - t0 >= 6.7f, 3f);
            Assert.AreEqual(_hyp.Count, CountDoors(_hyp.IsClosed), "all shut again by 6.7 s");
            Assert.AreEqual(0, _hyp.PlayerHits);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AnOrdinaryEnemyOnAnOpeningDoorDies_AnEliteTakes300AndLives_AndOneBesideItIsUntouched()
        {
            yield return Boot();
            Park(new Vector3(24f, 0.1f, 12f));
            int d = Door(AxisDoor);
            var fast = Load<AffixDef>("Assets/Data/Enemies/Affixes/AF_Fast.asset");
            Assert.IsNotNull(fast);
            int convict = _em.Spawn(Enemy("EN_Convict"), AxisDoor);
            int elite = _em.Spawn(Enemy("EN_Brute"), AxisDoor + Vector3.right * 2.6f, fast);
            int beside = _em.Spawn(Enemy("EN_Convict"), AxisDoor + Vector3.forward * (2f + 1f)); // 1 m past the long edge
            var staged = new[] { convict, elite, beside };
            foreach (int e in staged) Assert.GreaterOrEqual(e, 0);
            yield return Hold(staged, 0.3f);
            Assert.IsTrue(_hyp.InDoor(d, _em.GetPosition(convict), 0f) && _hyp.InDoor(d, _em.GetPosition(elite), 0f), "staged on the door");
            Assert.IsFalse(_hyp.InDoor(d, _em.GetPosition(beside), 0.5f), "staged beside it");
            int convictUid = _em.GetUid(convict), eliteUid = _em.GetUid(elite), besideUid = _em.GetUid(beside);
            float eliteHp = _em.GetHp(elite), besideHp = _em.GetHp(beside);
            Assert.Greater(eliteHp, _hyp.eliteDamage + 50f, "this elite survives a fall");
            int fallen = _hyp.Fallen;

            _hyp.DropDoor(d);
            yield return Hold(staged, _hyp.dropTelegraph - 0.2f);
            Assert.IsTrue(_em.IsAlive(convict) && _em.GetUid(convict) == convictUid, "nothing falls during the telegraph");
            yield return Hold(staged, 0.6f);
            Assert.IsFalse(_em.IsAlive(convict) && _em.GetUid(convict) == convictUid, "the convict on the door fell");
            Assert.AreEqual(fallen + 1, _hyp.Fallen, "one swallowed");
            Assert.IsTrue(_em.IsAlive(elite) && _em.GetUid(elite) == eliteUid, "the elite claws its way out");
            Assert.AreEqual(eliteHp - _hyp.eliteDamage, _em.GetHp(elite), 1f, "300 off the elite");
            Assert.AreEqual(1, _hyp.ElitesHurt);
            yield return Hold(staged, 1.5f);
            Assert.AreEqual(eliteHp - _hyp.eliteDamage, _em.GetHp(elite), 1f, "once per opening");
            Assert.IsTrue(_em.IsAlive(beside) && _em.GetUid(beside) == besideUid, "beside the door: alive");
            Assert.AreEqual(besideHp, _em.GetHp(beside), "and untouched");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AnEnemyWalkingOntoAnOpenDoor_Falls()
        {
            yield return Boot();
            int d = Door(AxisDoor);
            Park(AxisDoor + new Vector3(0f, 0.1f, 10f)); // north of the door: the horde comes across it
            _hyp.DropDoor(d);
            yield return WaitFor(() => _hyp.IsOpen(d), 3f);
            Assert.IsTrue(_hyp.IsOpen(d));
            int fallen = _hyp.Fallen;
            int convict = _em.Spawn(Enemy("EN_Convict"), AxisDoor + Vector3.back * 6f); // 4 m south of the near edge
            Assert.GreaterOrEqual(convict, 0);
            int uid = _em.GetUid(convict);
            bool entered = false;
            float end = Time.time + 3.5f;
            while (Time.time < end && _em.IsAlive(convict) && _em.GetUid(convict) == uid)
            {
                entered |= _hyp.InDoor(d, _em.GetPosition(convict), 0.5f);
                yield return null;
            }
            Assert.IsTrue(_hyp.IsOpen(d), "still open when it reached the door");
            Assert.IsFalse(_em.IsAlive(convict) && _em.GetUid(convict) == uid, "it walked in and fell (entered: " + entered + ")");
            Assert.AreEqual(fallen + 1, _hyp.Fallen);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AGroundedPlayerOnAnOpeningDoor_IsHitOnce_AndThrownOff_AJumpingOneIsNot()
        {
            yield return Boot();
            int d = Door(AxisDoor);
            Park(AxisDoor + Vector3.up * 0.05f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(_hyp.InDoor(d, PlayerPos, 0f), "standing on the door (the invisible floor holds)");
            Assert.Less(PlayerPos.y, 0.2f, "on the floor over the hole");
            _hyp.DropDoor(d);
            yield return WaitFor(() => _hyp.IsOpen(d), 3f);
            float opened = Time.time;
            yield return WaitFor(() => !_hyp.InDoor(d, PlayerPos, 0f), 0.5f);
            Assert.IsFalse(_hyp.InDoor(d, PlayerPos, 0f), "thrown off within 0.5 s (at " + PlayerPos + ")");
            Assert.LessOrEqual(Time.time - opened, 0.55f);
            yield return WaitFor(() => _hyp.IsClosed(d), 6f);
            Assert.AreEqual(1, _hitTimes.Count, "hit once");
            Assert.AreEqual(_hyp.playerDamage * DamageMul, _hitAmounts[0], 0.2f, "22 × the arena's 1.55");
            Assert.AreEqual(1, _hyp.PlayerHits);
            Assert.AreEqual(0, _hyp.Jumped);

            // in the air as it opens (the jump), then the jump carries them across: safe
            Park(AxisDoor + Vector3.up * 0.05f);
            yield return new WaitForSeconds(0.3f);
            _hitTimes.Clear();
            float t0 = Time.time;
            _hyp.DropDoor(d);
            yield return WaitFor(() => Time.time - t0 >= _hyp.dropTelegraph - 0.15f, 3f);
            var air = AxisDoor + Vector3.up * 1.3f;
            float end = Time.time + 1f;
            while (Time.time < end && !_hyp.IsOpen(d))
            {
                if (PlayerPos.y < 0.9f) Park(air);
                yield return null;
            }
            yield return null; // a frame over the open door, feet up
            Assert.IsTrue(_hyp.IsOpen(d));
            Assert.AreEqual(1, _hyp.Jumped, "feet in the air as it opened");
            Park(AxisDoor + new Vector3(0f, 0.1f, 2f + 1.5f)); // landed past the far edge
            yield return WaitFor(() => _hyp.IsClosed(d), 6f);
            Assert.AreEqual(0, _hitTimes.Count, "the jumper isn't hit");
            Assert.AreEqual(1, _hyp.PlayerHits);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheSchedule_DropsTheNearestGroupAt0m40_ThenAnotherGroup14sLater()
        {
            yield return Boot(halt: false);
            var spot = new Vector3(25f, 0.1f, 2f); // nearest door: the east axis (group 5)
            Park(spot);
            Assert.AreEqual(_hyp.dropFirst, _hyp.NextDropAt, 0.01f, "the first drop is due at 0:40 of scene time");
            Assert.AreEqual(40f, _hyp.dropFirst, 0.01f);
            Assert.AreEqual(14f, _hyp.dropEvery, 0.01f);
            TimeController.GameSpeed = 4f;
            yield return WaitFor(() => _hyp.Drops >= 1, 60f);
            float first = _hyp.Clock;
            Assert.AreEqual(1, _hyp.Drops);
            Assert.AreEqual(40f, first, 0.3f, "at 0:40");
            Assert.AreEqual(5, _hyp.LastGroup, "the group nearest the player");
            if ((PlayerPos - spot).sqrMagnitude > 1f) Park(spot); // stay put (nothing should have moved them)
            yield return WaitFor(() => _hyp.Drops >= 2, 30f);
            TimeController.GameSpeed = 1f;
            Assert.AreEqual(2, _hyp.Drops);
            Assert.AreEqual(14f, _hyp.Clock - first, 0.3f, "14 s later");
            Assert.AreNotEqual(5, _hyp.LastGroup, "never the same group twice running");
            Assert.AreEqual(0, _hyp.LastGroup, "the nearest door of another group: the ring's NE quarter");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Halt_StopsDropsAndWaves()
        {
            yield return Boot(halt: false);
            _hyp.dropFirst = 1f;
            _hyp.liftFirst = 1.5f;
            _hyp.Configure();
            _hyp.Halt();
            Assert.IsTrue(_hyp.Halted);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, _hyp.Drops, "no drop after Halt");
            Assert.AreEqual(0, _hyp.LiftWaves, "no lift wave after Halt");
            Assert.AreEqual(_hyp.Count, CountDoors(_hyp.IsClosed));
        }

        // ------------------------------------------------------------------ lifts

        [UnityTest, Timeout(60000)]
        public IEnumerator ALiftWave_RaisesThe3NearestLiftsAtLeast12mAway_WithPacksFromTheActiveEntries_AndAFullCapStopsAPack()
        {
            yield return Boot();
            var run = RunState.Current;
            Assert.IsNotNull(run);
            run.SetTimeRemaining(run.config.runDuration - 300f); // 5:00: Boars, the Chimera and the dead; no more Curs
            Assert.AreEqual(3, _hyp.liftsPerWave);
            Assert.AreEqual(12f, _hyp.liftMinDistance, 0.01f);
            Assert.Greater(_hyp.liftPacks.Length, 0, "AL_Colosseum's lift packs");

            // (34, -14) is 6 m from L3 (34, -20): skipped; then L0 (34, 20) 34 m, L7 (80, -24) 47 m, L4 (80, 24) 60 m
            Park(new Vector3(34f, 0.1f, -14f));
            yield return new WaitForSeconds(0.2f);
            int l0 = Lift(34f, 20f), l3 = Lift(34f, -20f), l4 = Lift(80f, 24f), l7 = Lift(80f, -24f);
            int raised = _hyp.LiftsRaised;
            Assert.IsTrue(_hyp.StartWaveNow(), "a wave");
            Assert.AreEqual(1, _hyp.LiftWaves);
            Assert.AreEqual(raised + 3, _hyp.LiftsRaised, "three lifts");
            Assert.IsTrue(_hyp.LiftTelegraphing(l0) && _hyp.LiftTelegraphing(l7) && _hyp.LiftTelegraphing(l4), "the 3 nearest at ≥ 12 m");
            Assert.IsFalse(_hyp.LiftBusy(l3), "not the one 6 m away");
            yield return new WaitForSeconds(_hyp.liftTelegraph * 0.5f);
            Assert.AreEqual(0f, _hyp.LiftOffset(l0), "still up while it telegraphs");
            yield return new WaitForSeconds(_hyp.liftTelegraph * 0.5f + _hyp.liftSink * 0.8f);
            Assert.Less(_hyp.LiftOffset(l0), -1f, "sinking");
            yield return WaitFor(() => !_hyp.LiftBusy(l0) && !_hyp.LiftBusy(l4) && !_hyp.LiftBusy(l7), 4f);
            Assert.AreEqual(0f, _hyp.LiftOffset(l0), "back up");
            Assert.GreaterOrEqual(_hyp.Released, 3, "at least one each (a Chimera comes alone)");
            int fromPacks = 0, curs = 0;
            float t = run.Elapsed;
            foreach (var p in _hyp.liftPacks)
            {
                if (p.enemy == null) continue;
                bool active = t >= p.from && (p.to <= 0f || t <= p.to);
                if (active) fromPacks += _em.AliveOf(p.enemy);
                else curs += _em.AliveOf(p.enemy);
            }
            Assert.AreEqual(_hyp.Released, fromPacks, "everything that came up is from a pack active at 5:00");
            Assert.AreEqual(0, curs, "nothing from a pack out of its window (the Curs end at 4:00)");
            Assert.LessOrEqual(_em.AliveCount, _hyp.Released, "nothing else is alive");

            // a full arena: the next wave brings nothing
            var director = Object.FindAnyObjectByType<Director>();
            Assert.IsNotNull(director);
            director.maxAliveOverride = Mathf.Max(1, _em.AliveCount);
            int released = _hyp.Released;
            Assert.IsFalse(_hyp.StartWaveNow(), "no pack fits under the cap");
            for (int l = 0; l < _hyp.LiftCount; l++) Assert.IsFalse(_hyp.LiftBusy(l), "no lift moves (" + l + ")");
            yield return new WaitForSeconds(3.2f);
            Assert.AreEqual(released, _hyp.Released);
            Assert.AreEqual(1, _hyp.LiftWaves);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ALiftSinkingUnderThePlayer_HurtsThemOnceAndThrowsThemOff_AVisualRaiseSkipsTheirLift()
        {
            yield return Boot();
            int l = Lift(34f, -20f);
            Park(_hyp.LiftCenter(l) + Vector3.up * 0.05f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(_hyp.InLift(l, PlayerPos, 0f), "on the lift");
            Assert.IsFalse(_hyp.RaiseLift(l, null, 0), "a visual raise (the boss's guards) skips the lift under the player");
            Assert.IsFalse(_hyp.LiftBusy(l));
            Assert.IsTrue(_hyp.RaiseLift(l, Enemy("EN_Convict"), 2));
            yield return WaitFor(() => _hitTimes.Count > 0, _hyp.liftTelegraph + 0.5f);
            Assert.AreEqual(1, _hitTimes.Count, "hit as it starts to sink");
            Assert.AreEqual(_hyp.liftPlayerDamage * DamageMul, _hitAmounts[0], 0.2f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_hyp.InLift(l, PlayerPos, 0f), "thrown off it");
            yield return WaitFor(() => !_hyp.LiftBusy(l), 3f);
            Assert.AreEqual(1, _hitTimes.Count, "once");
            Assert.AreEqual(2, _hyp.Released, "its pack stepped off");
            Assert.AreEqual(2, _em.AliveOf(Enemy("EN_Convict")));
        }

        // ------------------------------------------------------------------ doors left open (the boss's phase 3)

        [UnityTest, Timeout(60000)]
        public IEnumerator ADoorLeftOpen_KeepsAChasingConvictOut_LetsAnArrowCross_AndThrowsThePlayerOutHurtingAtMostOnceASecond()
        {
            yield return Boot();
            int d = Door(AxisDoor);
            _hyp.OpenForGood();
            yield return WaitFor(() => CountDoors(_hyp.IsHeld) == _hyp.Count, 3f);
            Assert.AreEqual(_hyp.Count, CountDoors(_hyp.IsHeld), "every door held open");
            Assert.AreEqual(0f, _hyp.Danger(AxisDoor), "the bot treats it as a wall");

            // the horde walks round it: a convict chasing the player across it never sets foot in it
            Park(AxisDoor + new Vector3(0f, 0.1f, 9f));
            int convict = _em.Spawn(Enemy("EN_Convict"), AxisDoor + Vector3.back * 7f);
            Assert.GreaterOrEqual(convict, 0);
            int uid = _em.GetUid(convict);
            float end = Time.time + 4f;
            float closest = float.MaxValue;
            while (Time.time < end && _em.IsAlive(convict) && _em.GetUid(convict) == uid)
            {
                Vector3 p = _em.GetPosition(convict);
                Assert.IsFalse(_hyp.InDoor(d, p, 0f), "the convict stepped into the held door at " + p);
                closest = Mathf.Min(closest, Mathf.Abs(p.z));
                yield return null;
            }
            Assert.IsTrue(_em.IsAlive(convict) && _em.GetUid(convict) == uid, "it never fell");
            Assert.Less(closest, 4f, "it did come up to the edge (closest |z| " + closest + ")");
            Assert.AreEqual(0, _hyp.Fallen);

            // an arrow flies straight over it (walk-only boxes don't stop shots)
            Park(AxisDoor + new Vector3(3f, 0.1f, 9f));
            var shots = EnemyProjectiles.Instance;
            Assert.IsNotNull(shots);
            int arrow = shots.FireStraight(AxisDoor + new Vector3(-1f, 1.2f, -6f), AxisDoor + new Vector3(-1f, 1.2f, 10f), 20f, 5f, 0.2f, 14f, arrow: true);
            Assert.GreaterOrEqual(arrow, 0);
            float far = float.MinValue;
            end = Time.time + 1.2f;
            while (Time.time < end && shots.IsActive(arrow))
            {
                far = Mathf.Max(far, shots.GetPosition(arrow).z);
                yield return null;
            }
            Assert.Greater(far, 2.5f, "the arrow crossed the held door (got to z " + far + ")");

            // the player steps in again and again: thrown out every time, hurt at most once a second
            _hitTimes.Clear();
            int thrown = 0;
            end = Time.time + 3f;
            while (Time.time < end)
            {
                Park(AxisDoor + new Vector3(1f, 0.05f, 0.5f));
                float back = Time.time + 0.7f;
                yield return null;
                while (Time.time < back && _hyp.InDoor(d, PlayerPos, 0f)) yield return null;
                Assert.IsFalse(_hyp.InDoor(d, PlayerPos, 0f), "thrown out of the held door");
                thrown++;
                yield return new WaitForSeconds(0.1f);
            }
            Assert.GreaterOrEqual(thrown, 3);
            Assert.GreaterOrEqual(_hitTimes.Count, 2, "it hurts");
            Assert.LessOrEqual(_hitTimes.Count, 4, "at most once a second over 3 s");
            for (int i = 1; i < _hitTimes.Count; i++)
                Assert.GreaterOrEqual(_hitTimes[i] - _hitTimes[i - 1], _hyp.heldHitEvery - 0.05f, "hits at least a second apart");
            Assert.AreEqual(_hyp.heldPlayerDamage * DamageMul, _hitAmounts[0], 0.2f);
        }

        // ------------------------------------------------------------------ the boss

        [UnityTest, Timeout(120000)]
        public IEnumerator TheBoss_RisesOnTheStageLift_Phase2OpensAll_Phase3HoldsAll_HisGuardsRaiseLifts_HisFallClosesThemWithin3s()
        {
            yield return Boot();
            var fight = Object.FindAnyObjectByType<BossFight>();
            Assert.IsNotNull(fight, "the Colosseum has its boss fight");
            Park(Safe);
            fight.StartNow();
            yield return WaitFor(() => BossController.Active != null, 5f);
            var boss = BossController.Active;
            Assert.IsNotNull(boss, "the boss is in");
            float lowest = float.MaxValue, deepest = 0f;
            float end = Time.time + 15f;
            while (Time.time < end && (boss.CurrentState == BossController.State.Entering || boss.CurrentState == BossController.State.Roaring))
            {
                if (boss.CurrentState == BossController.State.Entering)
                {
                    lowest = Mathf.Min(lowest, boss.Position.y);
                    deepest = Mathf.Min(deepest, _hyp.CenterLiftOffset);
                }
                yield return null;
            }
            Assert.AreNotEqual(BossController.State.Entering, boss.CurrentState, "his entrance is over");
            Assert.AreNotEqual(BossController.State.Roaring, boss.CurrentState);
            if (lowest < _hyp.centerLiftSpot.y - 1f) // he rose from below the stage (the rise, not the gate)
            {
                Assert.Less(deepest, -1f, "the stage's platform sank to meet him (lowest he was: " + lowest + ")");
                Assert.AreEqual(0f, _hyp.CenterLiftOffset, 0.01f, "and came up with him");
            }
            Assert.AreEqual(0, _hyp.LightStage, "the day");
            var sun = _hyp.Sun;
            float day = sun != null ? sun.intensity : 0f;

            // phase 2: every door drops; dusk; his guards come up the lifts nearest the player
            int raised = _hyp.LiftsRaised;
            _em.ApplyDamage(boss.Slot, new DamageInfo { Amount = _em.GetMaxHp(boss.Slot) * 0.4f, Flags = DamageFlags.Area, Direction = Vector3.up });
            yield return WaitFor(() => boss.Phase >= 2, 5f);
            Assert.AreEqual(2, boss.Phase);
            yield return null;
            Assert.AreEqual(1, _hyp.LightStage, "dusk");
            yield return WaitFor(() => CountDoors(_hyp.IsOpen) == _hyp.Count, 4f);
            Assert.AreEqual(_hyp.Count, CountDoors(_hyp.IsOpen), "all 30 open together");
            Assert.AreEqual(0, CountDoors(_hyp.IsHeld), "but not for good");
            if (boss.GuardWaves > 0) Assert.Greater(_hyp.LiftsRaised, raised, "his guards came up the lifts");
            if (sun != null)
            {
                yield return new WaitForSeconds(_hyp.lightLerp);
                Assert.AreEqual(day * _hyp.duskIntensity, sun.intensity, 0.02f, "the sun at dusk");
            }
            yield return WaitFor(() => CountDoors(_hyp.IsClosed) == _hyp.Count, 6f);

            // phase 3: every door drops and stays open; last light
            yield return WaitFor(() => boss.CurrentState != BossController.State.Enraging, 3f);
            float to25 = _em.GetHp(boss.Slot) - _em.GetMaxHp(boss.Slot) * 0.25f;
            _em.ApplyDamage(boss.Slot, new DamageInfo { Amount = Mathf.Max(1f, to25), Flags = DamageFlags.Area, Direction = Vector3.up });
            yield return WaitFor(() => boss.Phase >= 3, 5f);
            Assert.AreEqual(3, boss.Phase);
            yield return null;
            Assert.AreEqual(2, _hyp.LightStage, "last light");
            yield return WaitFor(() => CountDoors(_hyp.IsHeld) == _hyp.Count, 4f);
            Assert.AreEqual(_hyp.Count, CountDoors(_hyp.IsHeld), "all 30 held open");
            var probe = AxisDoor;
            Assert.IsTrue(_em.Obstacles.PushOut(ref probe, 0.4f), "their walk-only boxes are on");
            int drops = _hyp.Drops;
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(drops, _hyp.Drops, "no more drops once the floor is open for good");

            // his fall (the false death included): every door closes, one after another, within 3 s
            end = Time.time + 30f;
            while (Time.time < end && !boss.Defeated)
            {
                if (boss.Alive) _em.ApplyDamage(boss.Slot, new DamageInfo { Amount = 1e6f, Flags = DamageFlags.Area, Direction = Vector3.up });
                yield return new WaitForSeconds(0.3f);
            }
            Assert.IsTrue(boss.Defeated, "he fell");
            float fell = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - fell < 3f && CountDoors(_hyp.IsClosed) < _hyp.Count) yield return null;
            Assert.AreEqual(_hyp.Count, CountDoors(_hyp.IsClosed), "every door shut within 3 s of his fall");
            Assert.AreEqual(0, _hyp.LightStage, "the day comes back");
            Assert.IsTrue(_hyp.Halted);
            probe = AxisDoor;
            Assert.IsFalse(_em.Obstacles.PushOut(ref probe, 0.4f), "the boxes are off");
        }
    }
}
