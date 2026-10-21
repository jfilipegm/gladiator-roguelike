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
    /// R1 (docs/ARENAS.md §5, "the chariot races"): a race telegraphs the whole track with no chariot on it, then two
    /// chariots leave the stall half a lap apart, run counter-clockwise and go back in; a grounded player in the lane is
    /// hit once per pass and thrown out of it, a jump over a chariot or a step beside the lane is safe; a chariot kills
    /// ordinary enemies in its path (a shield doesn't save them), takes 200 off an elite and 600 off a boss, and leaves
    /// an enemy beside the lane alone; races come `chariotEvery` apart and tip an egg each; back to back rights the eggs
    /// and starts at once, then within the gap; Halt stops it. The Imperial scene doesn't exist yet (the layout part
    /// builds it), so the race runs on the Grand Arena's flat cross on a smaller track it fits: metae (±30, 0), radius
    /// 14, stalls at x = −60. Its hazards are parked and the Director is off: the test stages everything.
    /// </summary>
    public class ChariotRaceTests
    {
        const string Scene = "Assets/Scenes/Arenas/GrandArena.unity";
        const string ChariotModel = "Assets/Art/Models/Props/PR_Chariot.fbx";
        const float Z = -14f; // the test track's south straight (x −30 → 30; the run-in from x −60)

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        static EnemyDef Enemy(string asset) => Load<EnemyDef>("Assets/Data/Enemies/" + asset + ".asset");

        EnemyManager _em;
        PlayerHealth _health;
        PlayerMotor _motor;

        [TearDown]
        public void TearDown()
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GladiatorLoadout.NextOverride = null;
            GameSession.Clear();
        }

        IEnumerator Boot(bool god)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = god;
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            if (Director.Instance != null) Director.Instance.enabled = false;
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false;
            Player.Main.OverrideSource = new ScriptedInput();
            Player.Main.GetComponent<WeaponSystem>().RemoveAll();
            _em = EnemyManager.Instance;
            _em.KillAll();
            _health = Player.Main.GetComponent<PlayerHealth>();
            _health.invulnerableTime = 0f; // a second hit in one pass would land: the race must not deal it
            _motor = Player.Main.GetComponent<PlayerMotor>();
            if (FireJets.Instance != null) FireJets.Instance.enabled = false;
            if (BladeSweepers.Instance != null) BladeSweepers.Instance.enabled = false;
            Park(new Vector3(-8f, 0.1f, -24f));
            yield return new WaitForSeconds(0.2f);
        }

        void Park(Vector3 p) => _motor.Teleport(p);

        /// <summary>A race on the test track, its first scheduled race far away; chariots from PR_Chariot; an optional
        /// lap counter (a stand-in with `eggs` children Egg0…).</summary>
        static ChariotRace MakeRace(float speed = 22f, int laps = 2, float telegraph = 3f, int eggs = 0)
        {
            var go = new GameObject("ChariotRace (test)");
            go.SetActive(false); // Awake reads the fields: set them first
            var race = go.AddComponent<ChariotRace>();
            race.trackRadius = 14f;
            race.trackWestMeta = new Vector2(-30f, 0f);
            race.trackEastMeta = new Vector2(30f, 0f);
            race.trackWidth = 4f;
            race.trackStallX = -60f;
            race.chariotFirst = 1e6f;
            race.chariotEvery = 60f;
            race.chariotTelegraph = telegraph;
            race.chariotSpeed = speed;
            race.chariotCount = 2;
            race.chariotLaps = laps;
            var prefab = Load<GameObject>(ChariotModel);
            Assert.IsNotNull(prefab, ChariotModel);
            race.chariots = new[] { Object.Instantiate(prefab, go.transform).transform, Object.Instantiate(prefab, go.transform).transform };
            if (eggs > 0)
            {
                var counter = new GameObject("PR_LapCounter (test)").transform;
                counter.SetParent(go.transform, false);
                for (int i = 0; i < eggs; i++)
                {
                    var egg = new GameObject("Egg" + i).transform;
                    egg.SetParent(counter, false);
                    egg.localPosition = new Vector3(i * 0.9f, 3.8f, 0f);
                }
                race.lapCounter = counter;
            }
            go.SetActive(true);
            return race;
        }

        static Transform Part(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>Where on its path a chariot passes `p` on its first lap (the south straight).</summary>
        static float FirstPass(ChariotRace race, float x) => (-30f - race.trackStallX) + (x + 30f);

        /// <summary>Keeps the staged enemies rooted (SE_Root lasts 2 s) until `done` or `timeout` seconds.</summary>
        IEnumerator Hold(int[] slots, StatusDef root, System.Func<bool> done, float timeout)
        {
            float end = Time.time + timeout;
            while (Time.time < end && !done())
            {
                foreach (int e in slots) if (_em.IsAlive(e)) _em.ApplyStatus(e, root, 0f);
                yield return null;
            }
        }

        IEnumerator UntilPast(ChariotRace race, int c, float s, float timeout = 40f)
        {
            float end = Time.time + timeout;
            while (Time.time < end && race.ChariotDistance(c) < s) yield return null;
            Assert.GreaterOrEqual(race.ChariotDistance(c), s, "chariot " + c + " never got past s = " + s);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ARace_TelegraphsTheTrackWithNoChariotOnIt_ThenTwoLeaveHalfALapApart_RunCounterClockwise_AndGoBackIn()
        {
            yield return Boot(true);
            var race = MakeRace();
            yield return null;
            Assert.IsFalse(race.Running || race.Telegraphing);
            Assert.IsTrue(race.StartNow());
            Assert.IsTrue(race.Telegraphing);
            Assert.AreEqual(1, race.Races);
            Assert.IsFalse(race.StartNow(), "one race at a time");
            float tele0 = Time.time;
            var lane = new Vector3(-8f, 0f, Z);
            yield return new WaitForSeconds(race.chariotTelegraph * 0.5f);
            Assert.IsTrue(race.Telegraphing);
            for (int c = 0; c < race.Count; c++)
            {
                Assert.IsFalse(race.ChariotActive(c), "no chariot on the track while it telegraphs");
                Assert.IsFalse(race.chariots[c].gameObject.activeSelf, "chariot " + c + "'s model waits in its stall");
            }
            float danger = race.Danger(lane);
            Assert.IsFalse(float.IsInfinity(danger), "the lane is in danger: a chariot is coming");
            Assert.Greater(danger, race.chariotTelegraph * 0.5f);
            Assert.IsTrue(float.IsPositiveInfinity(race.Danger(new Vector3(-8f, 0f, -24f))), "off the lane: no danger");

            // the launches: chariot 0 when the telegraph ends, chariot 1 half a lap later
            float launch0 = -1f, launch1 = -1f, back0 = -1f;
            var wheelL = Part(race.chariots[0], "WheelL");
            Assert.IsNotNull(wheelL, "PR_Chariot has a WheelL");
            Quaternion wheel = Quaternion.identity;
            int heading = 0; // 1 = checked this frame, 2 = the wheel compared on the next
            float end = Time.time + race.chariotTelegraph + race.LaunchGap + race.RunSeconds + 6f;
            while (Time.time < end && (race.Running || race.Telegraphing))
            {
                yield return null;
                if (launch0 < 0f && race.ChariotActive(0)) launch0 = Time.time;
                if (launch1 < 0f && race.ChariotActive(1)) launch1 = Time.time;
                if (launch0 > 0f && back0 < 0f && !race.ChariotActive(0)) back0 = Time.time;
                if (heading == 1)
                {
                    heading = 2;
                    Assert.Greater(Quaternion.Angle(wheel, wheelL.rotation), 1f, "the wheels turn");
                }
                if (heading == 0 && launch0 > 0f && Time.time - launch0 > 1f)
                {
                    heading = 1;
                    wheel = wheelL.rotation;
                    Vector3 p = race.ChariotPosition(0);
                    Assert.AreEqual(Z, p.z, 0.05f, "it comes out along the south run-in");
                    Assert.Greater(race.ChariotForward(0).x, 0.99f, "heading east: counter-clockwise round the spina");
                    Assert.IsTrue(race.chariots[0].gameObject.activeSelf);
                    Assert.Less(Vector3.Distance(race.chariots[0].position, p), 0.01f, "the model rides the path");
                }
            }
            Assert.AreEqual(2, heading, "the heading and the wheels were checked");
            Assert.Greater(launch0, 0f, "chariot 0 launched");
            Assert.Greater(launch1, 0f, "chariot 1 launched");
            Assert.AreEqual(race.chariotTelegraph, launch0 - tele0, 0.2f, "the chariots wait for the whole telegraph");
            Assert.AreEqual(race.LaunchGap, launch1 - launch0, 0.15f, "half a lap apart");
            Assert.AreEqual(race.RunSeconds, back0 - launch0, 0.25f, "2 laps, then back into the north stall");
            Assert.IsFalse(race.Running || race.Telegraphing, "the race is over once the last is back");
            Assert.IsFalse(race.ChariotActive(0) || race.ChariotActive(1));
            Assert.IsFalse(race.chariots[1].gameObject.activeSelf);
            Assert.AreEqual(0, race.PlayerHits, "the player stayed off the track");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator AGroundedPlayerInTheLane_IsHitOncePerPass_ForItsDamage_AndThrownOutOfTheLane()
        {
            yield return Boot(true);
            var race = MakeRace();
            race.playerKnockback = 0f; // stand in the lane through the whole pass: still one hit
            race.playerKnockUp = 0f;
            var spot = new Vector3(-8f, 0.1f, Z);
            Park(spot);
            yield return null;
            float hp0 = _health.Hp;
            race.StartNow();
            float s = FirstPass(race, spot.x) + race.footprint;
            yield return UntilPast(race, 0, s);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, race.PlayerHits, "one hit from the first chariot's pass");
            float mul = RunState.Current != null ? RunState.Current.playerDamageTakenMul : 1f;
            Assert.AreEqual(race.playerDamage * mul, hp0 - _health.Hp, 0.5f, "28 × the arena's damage multiplier");
            yield return UntilPast(race, 1, s);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(2, race.PlayerHits, "and one from the second chariot's");

            // the second lap, with the knockback: thrown out of the lane
            race.playerKnockback = 13f;
            race.playerKnockUp = 5f;
            Park(spot);
            yield return UntilPast(race, 0, s + race.LapLength - race.footprint - 1f);
            float end = Time.time + 3f;
            while (Time.time < end && race.PlayerHits < 3) yield return null;
            Assert.AreEqual(3, race.PlayerHits, "the first chariot's second lap");
            yield return new WaitForSeconds(0.8f);
            Vector3 pp = Player.Main.transform.position;
            Assert.IsFalse(race.InLane(pp, 0.35f), "thrown out of the lane (offset " + race.LaneOffset(pp) + " m): raise playerKnockback");
            Assert.AreEqual(0, race.Jumped);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator AJumpOverAChariot_IsNotHit_NorIsAPlayerJustBesideTheLane()
        {
            yield return Boot(true);
            var race = MakeRace();
            var spot = new Vector3(-8f, 0f, Z);
            float ground = _em.Height.Sample(spot.x, spot.z);
            Park(new Vector3(spot.x, ground + race.jumpClear + 0.6f, spot.z));
            yield return null;
            race.StartNow();
            float s = FirstPass(race, spot.x);
            yield return UntilPast(race, 0, s - race.footprint - 8f);
            // over the lane while chariot 0 passes, feet held above jumpClear the whole time
            float end = Time.time + 3f;
            while (Time.time < end && race.ChariotDistance(0) < s + race.footprint + 2f)
            {
                if (_motor.IsGrounded) _motor.AddImpulse(Vector3.up * 10f);
                yield return null;
                Vector3 pp = Player.Main.transform.position;
                if (pp.y - ground < race.jumpClear + 0.2f) _motor.Teleport(new Vector3(spot.x, ground + race.jumpClear + 0.6f, spot.z));
            }
            yield return null;
            Assert.AreEqual(0, race.PlayerHits, "feet above 1.1 m: it runs under them");
            Assert.AreEqual(1, race.Jumped, "counted as a jump");

            // just beside the lane (0.4 m clear of its edge) while chariot 1 passes
            var beside = new Vector3(spot.x, 0.1f, Z - race.trackWidth * 0.5f - 0.35f - 0.4f);
            Assert.IsFalse(race.InLane(beside, 0.35f));
            Park(beside);
            yield return UntilPast(race, 1, s + race.footprint + 2f);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(0, race.PlayerHits, "beside the lane: safe");
            Assert.AreEqual(1, race.Jumped);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator AChariot_TramplesOrdinaryEnemies_ShieldOrNot_Takes200OffAnElite_600OffABoss_AndLeavesOneBesideTheLaneAlone()
        {
            yield return Boot(true);
            var race = MakeRace();
            // south of the lane: the Shield Thug (north of the centre line, pushed north) faces this way, into the push
            Park(new Vector3(-10f, 0.1f, -20f));
            var root = Load<StatusDef>("Assets/Data/Status/SE_Root.asset");
            var fast = Load<AffixDef>("Assets/Data/Enemies/Affixes/AF_Fast.asset");
            var gormDef = Load<EnemyDef>("Assets/Data/Enemies/EN_Gorm.asset");
            Assert.IsNotNull(fast);
            Assert.IsNotNull(gormDef);
            Assert.AreEqual(EnemyBehaviour.Scripted, gormDef.behaviour);
            int convict = _em.Spawn(Enemy("EN_Convict"), new Vector3(-24f, 0f, Z));
            int brute = _em.Spawn(Enemy("EN_Brute"), new Vector3(-21f, 0f, Z - 0.6f));
            int thug = _em.Spawn(Enemy("EN_ShieldThug"), new Vector3(-10f, 0f, Z + 0.5f));
            int elite = _em.Spawn(Enemy("EN_Brute"), new Vector3(-5f, 0f, Z), fast);
            int boss = _em.Spawn(gormDef, new Vector3(6f, 0f, Z));
            int beside = _em.Spawn(Enemy("EN_Convict"), new Vector3(-24f, 0f, Z - race.trackWidth * 0.5f - 1.2f));
            var held = new[] { convict, brute, thug, elite, beside };
            foreach (int e in held) Assert.GreaterOrEqual(e, 0);
            Assert.GreaterOrEqual(boss, 0);
            yield return Hold(held, root, () => false, 0.3f);
            if (!_em.IsBlocking(thug, Vector3.forward)) Debug.Log("[ChariotRaceTests] the Thug doesn't face into the push: the shield case isn't exercised this run");
            Assert.IsTrue(race.InLane(_em.GetPosition(thug), 0f) && race.InLane(_em.GetPosition(elite), 0f), "staged in the lane");
            Assert.IsFalse(race.InLane(_em.GetPosition(beside), _em.GetRadius(beside)), "staged beside the lane");
            float eliteHp = _em.GetHp(elite), bossHp = _em.GetHp(boss);
            Assert.Greater(eliteHp, race.enemyDamage * 1.5f, "this elite survives one pass");
            int eliteUid = _em.GetUid(elite);

            race.StartNow();
            // after the first chariot's pass over all of them, before the second's
            float past = FirstPass(race, 6f) + race.footprint + 1.5f;
            yield return Hold(held, root, () => race.ChariotDistance(0) >= past, 20f);
            Assert.GreaterOrEqual(race.ChariotDistance(0), past);
            Assert.Less(race.ChariotDistance(1), FirstPass(race, -24f) - race.footprint, "the second chariot hasn't come yet");
            Assert.IsFalse(_em.IsAlive(convict), "a convict in its path dies");
            Assert.IsFalse(_em.IsAlive(brute), "a Brute dies too: 200 × 2 for ordinary enemies");
            Assert.IsFalse(_em.IsAlive(thug), "a Shield Thug's shield doesn't stop four horses");
            Assert.IsTrue(_em.IsAlive(elite) && _em.GetUid(elite) == eliteUid, "the elite survives one pass");
            Assert.AreEqual(eliteHp - race.enemyDamage, _em.GetHp(elite), 0.5f, "an elite takes 200");
            Assert.AreEqual(bossHp - race.bossDamage, _em.GetHp(boss), 0.5f, "a boss takes 600, flat");
            Assert.AreEqual(1, race.BossHits);
            Assert.GreaterOrEqual(race.Kills, 3);
            Assert.AreEqual(race.Kills, race.Trampled);
            Assert.AreEqual(_em.GetMaxHp(beside), _em.GetHp(beside), 0.01f, "beside the lane: untouched");

            yield return Hold(held, root, () => !race.Running, 40f);
            Assert.IsFalse(race.Running);
            Assert.AreEqual(4, race.BossHits, "a boss on the south straight: 2 chariots × 2 laps");
            Assert.AreEqual(bossHp - 4f * race.bossDamage, _em.GetHp(boss), 0.5f);
            Assert.IsTrue(_em.IsAlive(beside));
            Assert.AreEqual(_em.GetMaxHp(beside), _em.GetHp(beside), 0.01f, "still untouched after four passes");
            Assert.AreEqual(0, race.PlayerHits);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator Races_StartChariotEveryApart_AndEachTipsAnEgg()
        {
            yield return Boot(true);
            // a short fast race (one lap at 60 m/s: ~6.5 s) so two fit in a few seconds
            var race = MakeRace(speed: 60f, laps: 1, telegraph: 1f, eggs: 3);
            float t0 = race.Clock;
            race.chariotFirst = t0 + 1f;
            race.chariotEvery = 8f;
            race.Configure(); // re-reads the schedule (and the eggs)
            Assert.AreEqual(3, race.EggCount);
            float end = Time.time + 20f;
            float first = -1f, second = -1f;
            while (Time.time < end && race.Races < 2)
            {
                yield return null;
                if (first < 0f && race.Races == 1) first = race.Clock;
            }
            second = race.Clock;
            Assert.AreEqual(2, race.Races);
            Assert.AreEqual(1f, first - t0, 0.1f, "the first race at chariotFirst");
            Assert.AreEqual(race.chariotEvery, second - first, 0.1f, "the next chariotEvery after the last one started");
            yield return new WaitForSeconds(race.eggTipSeconds + 0.2f);
            Assert.AreEqual(2, race.EggsDown, "one egg tipped per race");
            var egg0 = Part(race.lapCounter, "Egg0");
            var egg2 = Part(race.lapCounter, "Egg2");
            Assert.AreEqual(race.eggTipDegrees, Quaternion.Angle(Quaternion.identity, egg0.localRotation), 1f, "Egg0 is down");
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, egg2.localRotation), 0.5f, "Egg2 still stands");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator BackToBack_RightsTheEggs_StartsAtOnce_ThenWithinTheGap_AndHaltStopsTheRaces()
        {
            yield return Boot(true);
            var race = MakeRace(speed: 60f, laps: 1, telegraph: 1f, eggs: 7);
            race.StartNow();
            float end = Time.time + 15f;
            while (Time.time < end && (race.Running || race.Telegraphing)) yield return null;
            race.StartNow();
            while (Time.time < end && (race.Running || race.Telegraphing)) yield return null;
            Assert.AreEqual(2, race.Races);
            Assert.AreEqual(2, race.EggsDown);
            yield return new WaitForSeconds(race.backToBackGap + 0.5f);
            Assert.AreEqual(2, race.Races, "nothing scheduled: the next is a minute away");

            race.BackToBack = true;
            yield return null;
            yield return null;
            Assert.AreEqual(3, race.Races, "back to back with none running: one starts at once");
            Assert.AreEqual(1, race.EggsDown, "the eggs were righted, then one tipped");
            end = Time.time + 15f;
            while (Time.time < end && (race.Running || race.Telegraphing)) yield return null;
            float over = Time.time;
            while (Time.time < end && race.Races < 4) yield return null;
            Assert.AreEqual(4, race.Races);
            Assert.AreEqual(race.backToBackGap, Time.time - over, 0.25f, "the next telegraph backToBackGap after the last chariot is back");

            // halt mid-race: the chariots vanish and no race starts again
            while (Time.time < end && !race.ChariotActive(0)) yield return null;
            Assert.IsTrue(race.ChariotActive(0));
            race.Halt();
            Assert.IsTrue(race.Halted);
            Assert.IsFalse(race.Running || race.Telegraphing);
            Assert.IsFalse(race.ChariotActive(0) || race.ChariotActive(1));
            Assert.IsFalse(race.chariots[0].gameObject.activeSelf);
            Assert.IsTrue(float.IsPositiveInfinity(race.Danger(new Vector3(-8f, 0f, Z))));
            yield return new WaitForSeconds(race.backToBackGap + 1.5f);
            Assert.AreEqual(4, race.Races, "halted: back to back or not, nothing starts");
            race.BackToBack = false;
        }
    }
}
