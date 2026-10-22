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
    /// Q14 (R7, the producer 2026-10-22): the chests, the altar, the crowd's gifts and the Arena Master's whims no longer
    /// repeat every run. GameSession seeds GameRandom per run; Interactables, CrowdGifts and ArenaMaster seed their own
    /// generators from it in Awake. Two runs started through GameSession.StartRun differ; two loads after the same
    /// GameRandom.Seed are identical (a seeded test still gets a fixed placement).
    /// </summary>
    public class RunVarietyTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity"; // random chest spots (no layout anchors)
        const float WhimClock = 200f; // elites and swarms are in the table by now: both whims can run

        /// <summary>What one run rolled: its starting chests and altar, three gifts, three whims.</summary>
        sealed class Rolls
        {
            public readonly List<Vector3> Props = new List<Vector3>();
            public readonly List<GiftKind> GiftKinds = new List<GiftKind>();
            public readonly List<Vector3> GiftSpots = new List<Vector3>();
            public readonly List<int> Whims = new List<int>(); // WhimKind, or -1 when the whim couldn't run
        }

        Rolls _recording;
        RunDef _runDef;

        void OnFavor(in FavorEvent e)
        {
            if (_recording == null) return;
            if (e.Kind == FavorEventKind.GiftThrown) { _recording.GiftKinds.Add(e.Gift); _recording.GiftSpots.Add(e.Position); }
            else if (e.Kind == FavorEventKind.WhimFired) _recording.Whims.Add((int)e.Whim);
        }

        [SetUp]
        public void SetUp()
        {
            RunModifiers.Clear();
            _runDef = ScriptableObject.CreateInstance<RunDef>();
            _runDef.id = "variety_test";
            _runDef.arenas = new[] { new RunDef.Arena { displayName = "Test Arena", scenePath = Scene } };
            GameEvents.OnFavor.Subscribe(OnFavor);
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.OnFavor.Unsubscribe(OnFavor);
            _recording = null;
            DevSettings.ResetAll();
            GameSession.Clear();
            if (_runDef != null) Object.Destroy(_runDef);
        }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>
        /// Starts a run with `start` (it loads the test arena), then records what it rolled: the interactables placed at
        /// the start (in slot order), three gifts thrown with the player at the arena's centre, three whims.
        /// </summary>
        IEnumerator RunAndRecord(System.Action start, Rolls into)
        {
            DevSettings.ResetAll();
            DevSettings.GodMode = true;
            DevSettings.Timeless = true;
            TimeController.GameSpeed = 1f;
            var before = RunState.Current;
            start(); // called after this frame's gameplay tick: nothing draws from GameRandom before the new scene's Awakes
            yield return Until(() => RunState.Current != null && !ReferenceEquals(RunState.Current, before) && Interactables.Instance != null, 15f);
            Assert.IsNotNull(RunState.Current, "the arena loaded");
            yield return null;
            var spawner = Object.FindAnyObjectByType<DebugEnemySpawner>();
            if (spawner != null) spawner.enabled = false;
            var director = Object.FindAnyObjectByType<Director>();
            Assert.IsNotNull(director, "the test arena has a Director");
            director.densityMultiplier = 0f; // quiet: it stays on, the whims need it
            var lvl = Object.FindAnyObjectByType<LevelUpController>();
            if (lvl != null) lvl.enabled = false; // a Laurel's XP must not pause the run
            var player = Player.Main;
            player.OverrideSource = new ScriptedInput();
            player.GetComponent<WeaponSystem>().RemoveAll();
            var em = EnemyManager.Instance;
            em.DespawnAll();

            var props = Interactables.Instance;
            yield return Until(() => { for (int i = 0; i < props.SlotCount; i++) if (props.IsReady(i)) return true; return false; }, 5f);
            yield return null;
            for (int i = 0; i < props.SlotCount; i++)
                if (props.IsReady(i)) into.Props.Add(props.PositionOf(i));

            // the gifts land around the player: the same spot for every run
            R2TestArena.PutPlayer(em.arenaCenter);
            player.GetComponent<PlayerHealth>().HealFull(); // the gift's odds lean on HP: the same for every run
            yield return null;
            yield return null;
            _recording = into;
            var gifts = CrowdGifts.Instance;
            Assert.IsNotNull(gifts, "the test arena has the crowd's gifts");
            for (int k = 0; k < 3; k++) Assert.GreaterOrEqual(gifts.Throw(), 0, "a gift thrown");

            // the whims (the Director quiet, the clock where both kinds can run)
            var run = RunState.Current;
            run.SetTimeRemaining(run.config.runDuration - WhimClock);
            director.enabled = true;
            var master = ArenaMaster.Instance;
            Assert.IsNotNull(master, "the test arena has the Arena Master");
            for (int k = 0; k < 3; k++)
            {
                int n = into.Whims.Count;
                if (!master.FireWhim() && into.Whims.Count == n) into.Whims.Add(-1);
            }
            _recording = null;
        }

        void StartThroughSession() => GameSession.StartRun(null, _runDef);

        void LoadSeeded(int seed)
        {
            TimeController.ResetForNewRun();
            GameRandom.Seed(seed);
            SceneManager.LoadScene(Scene, LoadSceneMode.Single);
        }

        static bool Same(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-4f;

        static bool SameList(List<Vector3> a, List<Vector3> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!Same(a[i], b[i])) return false;
            return true;
        }

        static string Show(Rolls r) =>
            "props " + string.Join(" ", r.Props) + " | gifts " + string.Join(" ", r.GiftKinds) + " at " + string.Join(" ", r.GiftSpots) +
            " | whims " + string.Join(" ", r.Whims);

        [UnityTest, Timeout(120000)]
        public IEnumerator TwoRuns_FromTheMenusPath_RollDifferentChestsGiftsAndWhims()
        {
            var a = new Rolls();
            var b = new Rolls();
            yield return RunAndRecord(StartThroughSession, a);
            yield return RunAndRecord(StartThroughSession, b);
            Debug.Log("[RunVarietyTests] run 1: " + Show(a) + "\nrun 2: " + Show(b));

            Assert.GreaterOrEqual(a.Props.Count, 3, "two starting chests and the altar");
            Assert.AreEqual(a.Props.Count, b.Props.Count);
            int same = 0;
            for (int i = 0; i < a.Props.Count; i++) if (Same(a.Props[i], b.Props[i])) same++;
            Assert.LessOrEqual(same, 1, "the chests and the altar move from run to run (one coincidence allowed)");

            Assert.AreEqual(3, a.GiftKinds.Count);
            Assert.AreEqual(3, b.GiftKinds.Count);
            Assert.AreEqual(3, a.Whims.Count);
            Assert.AreEqual(3, b.Whims.Count);
            bool giftsDiffer = !SameList(a.GiftSpots, b.GiftSpots);
            for (int i = 0; i < 3; i++) giftsDiffer |= a.GiftKinds[i] != b.GiftKinds[i];
            bool whimsDiffer = false;
            for (int i = 0; i < 3; i++) whimsDiffer |= a.Whims[i] != b.Whims[i];
            Assert.IsTrue(giftsDiffer || whimsDiffer, "the gifts or the whims come in a different order");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TwoLoads_AfterTheSameSeed_RollTheSame()
        {
            var a = new Rolls();
            var b = new Rolls();
            yield return RunAndRecord(() => LoadSeeded(1234), a);
            yield return RunAndRecord(() => LoadSeeded(1234), b);
            Debug.Log("[RunVarietyTests] seed 1234: " + Show(a) + "\nagain: " + Show(b));

            Assert.GreaterOrEqual(a.Props.Count, 3, "two starting chests and the altar");
            Assert.IsTrue(SameList(a.Props, b.Props), "the same chests and altar for the same seed");
            CollectionAssert.AreEqual(a.GiftKinds, b.GiftKinds, "the same gifts");
            Assert.IsTrue(SameList(a.GiftSpots, b.GiftSpots), "landing in the same spots");
            CollectionAssert.AreEqual(a.Whims, b.Whims, "the same whims");
        }
    }
}
