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
    /// R8 (docs/tracks/R7-design.md §3.4, "what reproducible means"): with the same run seed, the keyed events repeat however
    /// the run went. Each test loads an arena after GameRandom.BeginRun(S) (what GameSession does right before every load)
    /// and records what it rolled; loads again with S, this time making 300 Combat draws and killing 20 enemies between the
    /// events (crits, gold drops, death pops, enemy jitter: the streams that diverge with play), and expects every record
    /// equal; then loads with another seed and expects at least one record to differ.
    /// The test arena: the first level-up's offers, three swarms (type, side, gate), two elites (type, affix), four crowd
    /// gifts (kind, landing spot) and two whims. The Slave Pit: its chests and altar, the Golden Rat's time and hole, the
    /// rattling crate's spot.
    /// </summary>
    public class SeedReplayTests
    {
        const string TestArena = "Assets/Scenes/Dev/TestArena.unity";
        const string SlavePit = "Assets/Scenes/Arenas/SlavePit.unity";
        const uint SeedA = 0x7F3A91C2u, SeedB = 0x0BADF00Du;
        const float EventClock = 200f; // the test arena's swarm and elite types are in its table by now (RunVarietyTests)

        sealed class Record
        {
            public readonly List<string> Offers = new List<string>();
            public readonly List<int> SwarmEntries = new List<int>();
            public readonly List<float> SwarmAngles = new List<float>();
            public readonly List<int> SwarmGates = new List<int>();
            public readonly List<string> Elites = new List<string>(); // "enemy/affix", the whims' champions included
            public readonly List<GiftKind> Gifts = new List<GiftKind>();
            public readonly List<Vector3> GiftSpots = new List<Vector3>();
            public readonly List<int> Whims = new List<int>(); // WhimKind, or -1 when the whim couldn't run
            public readonly List<Vector3> Props = new List<Vector3>();
            public float RatAt;
            public int RatHole = -1, CrateSpot = -1;

            public override string ToString() =>
                "offers " + string.Join(",", Offers) + " | swarms " + string.Join(",", SwarmEntries) + " at " + string.Join(",", SwarmAngles) +
                " gates " + string.Join(",", SwarmGates) + " | elites " + string.Join(",", Elites) + " | gifts " + string.Join(",", Gifts) +
                " at " + string.Join(" ", GiftSpots) + " | whims " + string.Join(",", Whims) + " | props " + string.Join(" ", Props) +
                " | rat " + RatAt + "s hole " + RatHole + " | crate " + CrateSpot;
        }

        Record _rec;
        bool _noisy;

        void OnFavor(in FavorEvent e)
        {
            if (_rec == null) return;
            if (e.Kind == FavorEventKind.GiftThrown) { _rec.Gifts.Add(e.Gift); _rec.GiftSpots.Add(e.Position); }
            else if (e.Kind == FavorEventKind.WhimFired) _rec.Whims.Add((int)e.Whim);
        }

        void OnDirector(in DirectorEvent e)
        {
            if (_rec == null || e.Kind != DirectorEventKind.Elite) return;
            var em = EnemyManager.Instance;
            for (int i = 0; i < em.HighWater; i++)
            {
                if (!em.IsAlive(i) || em.GetAffix(i) == null || (em.GetPosition(i) - e.Position).sqrMagnitude > 1e-4f) continue;
                var def = em.GetDef(i);
                _rec.Elites.Add((def != null ? def.id : "?") + "/" + em.GetAffix(i).id);
                return;
            }
            _rec.Elites.Add("?");
        }

        static void RatAlwaysComes(Scene scene, LoadSceneMode mode)
        {
            // after the scene's Awakes, before its Starts: the rat rolls its chance anyway, so its time and hole are the seed's
            var rat = Object.FindAnyObjectByType<GoldenRat>();
            if (rat != null) rat.alwaysAppear = true;
        }

        [SetUp]
        public void SetUp()
        {
            RunModifiers.Clear();
            GameSession.Clear();
            SaveSandbox.Wipe(); // the same unlocks for every load: the card pool and the secrets depend on them
            GameEvents.OnFavor.Subscribe(OnFavor);
            GameEvents.OnDirector.Subscribe(OnDirector);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnFavor.Unsubscribe(OnFavor);
            GameEvents.OnDirector.Unsubscribe(OnDirector);
            SceneManager.sceneLoaded -= RatAlwaysComes;
            _rec = null;
            _noisy = false;
            GameLoop.TrackHeapPerSystem = false;
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GameSession.Clear();
            GameRandom.BeginRun(GameRandom.DefaultSeed);
        }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>BeginRun(seed), then the scene (with the noisy load, the Combat and Map streams move before it: no fork reads them).</summary>
        IEnumerator Load(string scene, uint seed)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            DevSettings.Timeless = true; // the run clock holds: the spawn table and the curves read the same time at every event
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GameRandom.BeginRun(seed);
            if (_noisy)
            {
                for (int i = 0; i < 300; i++) GameRandom.Chance(RngStream.Combat, 0.5f);
                for (int i = 0; i < 50; i++) GameRandom.NextUInt(RngStream.Map);
            }
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return Until(() => RunIntro.Instance == null || RunIntro.Instance.Done, 20f);
            Assert.IsNotNull(RunState.Current, "the arena loaded");
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
        }

        /// <summary>The noisy load only: 300 crits' worth of Combat draws, then 20 enemies spawned and killed (jitter, gold, pops).</summary>
        IEnumerator Noise()
        {
            if (!_noisy) yield break;
            for (int i = 0; i < 300; i++) GameRandom.Chance(RngStream.Combat, 0.5f);
            var em = EnemyManager.Instance;
            var director = Director.Instance;
            var fodder = director != null && director.table != null && director.table.entries.Length > 0 ? director.table.entries[0].enemy : null;
            if (fodder != null)
            {
                for (int i = 0; i < 20; i++) em.Spawn(fodder, em.EdgePoint(i * Mathf.PI * 2f / 20f, 4f));
                yield return null;
                em.KillAll();
            }
            yield return null;
            yield return null;
            em.DespawnAll(); // nothing left over: the next event sees the same head-count in every load
            yield return null;
        }

        /// <summary>The test arena's events, recorded into `rec`.</summary>
        IEnumerator RecordTestArena(uint seed, Record rec)
        {
            yield return Load(TestArena, seed);
            var director = Director.Instance;
            Assert.IsNotNull(director, "the test arena has a Director");
            yield return Until(() => director.Prewarmed, 20f);
            Assert.IsTrue(director.Prewarmed, "the pools filled");
            director.densityMultiplier = 0f; // quiet: no steady spawns, swarms of none (their type and side are still rolled)
            var em = EnemyManager.Instance;
            var player = Player.Main;
            player.OverrideSource = new ScriptedInput();
            player.GetComponent<WeaponSystem>().RemoveAll(); // no kills but the test's, and the same loan candidates for the gifts
            em.DespawnAll();
            if (Favor.Instance != null) Favor.Instance.gainMul = 0f; // the noise's kills never earn a gift
            if (ArenaMaster.Instance != null) ArenaMaster.Instance.boredomMul = 0f; // nor a whim on their own
            var run = RunState.Current;
            run.SetTimeRemaining(run.config.runDuration - EventClock);
            R2TestArena.PutPlayer(em.arenaCenter); // the gates and the gifts lean on where the player is: the same everywhere
            yield return null;
            yield return null;
            _rec = rec;

            // the first level-up: its cards, then skip (the build stays as it was: the gifts' loans depend on it)
            var lvl = LevelUpController.Instance;
            Assert.IsNotNull(lvl, "the test arena has the level-up flow");
            Assert.AreEqual(0, lvl.OpensOf(PickSource.LevelUp));
            run.AddXp(run.XpToNext - run.Xp + 0.01f);
            yield return Until(() => lvl.IsOpen, 5f);
            Assert.IsTrue(lvl.IsOpen, "the level-up cards opened");
            for (int i = 0; i < lvl.ChoiceCount; i++) rec.Offers.Add(lvl.Offers[i].Def != null ? lvl.Offers[i].Def.id : "-");
            Assert.AreEqual(1, lvl.OpensOf(PickSource.LevelUp), "the first level-up pick (key: LevelUp, 0)");
            lvl.Skip();
            lvl.enabled = false; // the noise's gems mustn't open more cards
            yield return null;
            yield return Noise();

            // three swarms
            for (int k = 0; k < 3; k++)
            {
                int before = director.SwarmsStarted;
                director.TriggerSwarmNow();
                yield return Until(() => director.SwarmsStarted > before, 5f);
                Assert.Greater(director.SwarmsStarted, before, "swarm " + k + " started");
                rec.SwarmEntries.Add(director.SwarmEntry);
                rec.SwarmAngles.Add(director.SwarmAngle);
                rec.SwarmGates.Add(director.SwarmGate);
                yield return Noise();
            }

            // two elites (OnDirector records their type and affix)
            for (int k = 0; k < 2; k++)
            {
                int n = rec.Elites.Count;
                director.TriggerEliteNow();
                yield return Until(() => rec.Elites.Count > n, 5f);
                Assert.Greater(rec.Elites.Count, n, "elite " + k + " came");
                em.DespawnAll();
                yield return Noise();
            }

            // four gifts (the player at full HP: the amphora's odds lean on it)
            var gifts = CrowdGifts.Instance;
            Assert.IsNotNull(gifts, "the test arena has the crowd's gifts");
            for (int k = 0; k < 4; k++)
            {
                R2TestArena.PutPlayer(em.arenaCenter);
                player.GetComponent<PlayerHealth>().HealFull();
                yield return null;
                Assert.GreaterOrEqual(gifts.Throw(null), 0, "gift " + k + " thrown");
                yield return Noise();
            }

            // two whims (a swarm through a gate, or a champion: an elite from the Director's elite forks)
            var master = ArenaMaster.Instance;
            Assert.IsNotNull(master, "the test arena has the Arena Master");
            for (int k = 0; k < 2; k++)
            {
                int n = rec.Whims.Count;
                if (!master.FireWhim() && rec.Whims.Count == n) rec.Whims.Add(-1);
                yield return null;
                em.DespawnAll(); // a champion leaves: the next whim sees no challenge running, in every load
                yield return null;
                yield return null;
                yield return Noise();
            }
            _rec = null;
        }

        /// <summary>The Slave Pit's map rolls, recorded into `rec`.</summary>
        IEnumerator RecordSlavePit(uint seed, Record rec)
        {
            yield return Load(SlavePit, seed);
            var props = Interactables.Instance;
            Assert.IsNotNull(props, "the Slave Pit has chests");
            yield return Until(() => { for (int i = 0; i < props.SlotCount; i++) if (props.IsReady(i)) return true; return false; }, 10f);
            yield return null;
            for (int i = 0; i < props.SlotCount && rec.Props.Count < 3; i++)
                if (props.IsReady(i)) rec.Props.Add(props.PositionOf(i));
            var rat = GoldenRat.Instance;
            Assert.IsNotNull(rat, "the Slave Pit has the Golden Rat");
            Assert.AreNotEqual(GoldenRat.Phase.NotThisRun, rat.State, "alwaysAppear set before its Start");
            rec.RatAt = rat.AppearAt;
            rec.RatHole = rat.Hole;
            if (RattlingCrate.Instance != null) rec.CrateSpot = RattlingCrate.Instance.SpotIndex;
            yield return Noise();
        }

        static bool Same(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-4f;

        static bool SameSpots(List<Vector3> a, List<Vector3> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!Same(a[i], b[i])) return false;
            return true;
        }

        static bool SameAngles(List<float> a, List<float> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (Mathf.Abs(a[i] - b[i]) > 1e-5f) return false;
            return true;
        }

        static void AssertSame(Record a, Record b)
        {
            CollectionAssert.AreEqual(a.Offers, b.Offers, "the first level-up's cards");
            CollectionAssert.AreEqual(a.SwarmEntries, b.SwarmEntries, "the swarms' types");
            Assert.IsTrue(SameAngles(a.SwarmAngles, b.SwarmAngles), "the swarms' sides");
            CollectionAssert.AreEqual(a.SwarmGates, b.SwarmGates, "the swarms' gates");
            CollectionAssert.AreEqual(a.Elites, b.Elites, "the elites' types and affixes");
            CollectionAssert.AreEqual(a.Gifts, b.Gifts, "the gifts' kinds");
            Assert.IsTrue(SameSpots(a.GiftSpots, b.GiftSpots), "the gifts' landing spots");
            CollectionAssert.AreEqual(a.Whims, b.Whims, "the whims");
            Assert.IsTrue(SameSpots(a.Props, b.Props), "the chests and the altar");
            Assert.AreEqual(a.RatAt, b.RatAt, 1e-4f, "when the rat comes");
            Assert.AreEqual(a.RatHole, b.RatHole, "the rat's hole");
            Assert.AreEqual(a.CrateSpot, b.CrateSpot, "the crate's spot");
        }

        static bool Differs(Record a, Record b)
        {
            bool d = !SameAngles(a.SwarmAngles, b.SwarmAngles) || !SameSpots(a.GiftSpots, b.GiftSpots) || !SameSpots(a.Props, b.Props);
            if (a.Offers.Count != b.Offers.Count || a.Elites.Count != b.Elites.Count || a.Gifts.Count != b.Gifts.Count) return true;
            for (int i = 0; i < a.Offers.Count; i++) d |= a.Offers[i] != b.Offers[i];
            for (int i = 0; i < a.Elites.Count; i++) d |= a.Elites[i] != b.Elites[i];
            for (int i = 0; i < a.Gifts.Count; i++) d |= a.Gifts[i] != b.Gifts[i];
            for (int i = 0; i < a.SwarmEntries.Count && i < b.SwarmEntries.Count; i++) d |= a.SwarmEntries[i] != b.SwarmEntries[i];
            for (int i = 0; i < a.Whims.Count && i < b.Whims.Count; i++) d |= a.Whims[i] != b.Whims[i];
            return d || Mathf.Abs(a.RatAt - b.RatAt) > 1e-4f || a.RatHole != b.RatHole || a.CrateSpot != b.CrateSpot;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TestArena_SameSeed_ReplaysCardsSwarmsElitesGiftsAndWhims()
        {
            var a = new Record();
            yield return RecordTestArena(SeedA, a);
            Debug.Log("[SeedReplayTests] " + GameRandom.SeedText(SeedA) + ": " + a);
            Assert.Greater(a.Offers.Count, 0, "cards were offered");
            Assert.AreEqual(3, a.SwarmEntries.Count);
            Assert.GreaterOrEqual(a.Elites.Count, 2);
            Assert.AreEqual(4, a.Gifts.Count);
            Assert.AreEqual(2, a.Whims.Count);

            _noisy = true;
            var b = new Record();
            yield return RecordTestArena(SeedA, b);
            Debug.Log("[SeedReplayTests] " + GameRandom.SeedText(SeedA) + " again, with crits and kills between: " + b);
            AssertSame(a, b);

            // zero GC per frame: 5 s of the horde on the noisy load's seed (logged per system; in the Editor the heap
            // counter moves with the Editor's own allocations, so the bot run in the player is the measurement)
            var director = Director.Instance;
            director.densityMultiplier = 1f;
            GameLoop.TrackHeapPerSystem = true;
            yield return new WaitForSeconds(5f);
            Debug.Log("[SeedReplayTests] 5 s, " + EnemyManager.Instance.AliveCount + " enemies; heap growth per system:\n" + GameLoop.HeapReport());
            GameLoop.TrackHeapPerSystem = false;
            Assert.Greater(EnemyManager.Instance.AliveCount, 0, "the horde ran");

            _noisy = false;
            var c = new Record();
            yield return RecordTestArena(SeedB, c);
            Debug.Log("[SeedReplayTests] " + GameRandom.SeedText(SeedB) + ": " + c);
            Assert.IsTrue(Differs(a, c), "another seed rolls another run");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SlavePit_SameSeed_SameChestsRatAndCrate()
        {
            SceneManager.sceneLoaded += RatAlwaysComes;
            var a = new Record();
            yield return RecordSlavePit(SeedA, a);
            Debug.Log("[SeedReplayTests] Slave Pit " + GameRandom.SeedText(SeedA) + ": " + a);
            Assert.GreaterOrEqual(a.Props.Count, 3, "two starting chests and the altar");
            Assert.GreaterOrEqual(a.RatHole, 0, "the rat has a hole");
            Assert.That(a.RatAt, Is.InRange(GoldenRat.Instance.window.x, GoldenRat.Instance.window.y));

            _noisy = true;
            var b = new Record();
            yield return RecordSlavePit(SeedA, b);
            Debug.Log("[SeedReplayTests] Slave Pit " + GameRandom.SeedText(SeedA) + " again: " + b);
            AssertSame(a, b);

            _noisy = false;
            var c = new Record();
            yield return RecordSlavePit(SeedB, c);
            Debug.Log("[SeedReplayTests] Slave Pit " + GameRandom.SeedText(SeedB) + ": " + c);
            Assert.IsTrue(Differs(a, c), "another seed, another map");
        }
    }
}
