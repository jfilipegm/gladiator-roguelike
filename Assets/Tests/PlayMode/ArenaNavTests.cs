using System.Collections;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gladiator.Tests
{
    /// <summary>M2.1: the Editor-baked arena nav data is used at runtime and matches a fresh bake of the scene (the test
    /// arena; R2: the Colosseum, whose closed trapdoors and lifts bake as floor through their invisible floors).</summary>
    public class ArenaNavTests
    {
        const string Scene = "Assets/Scenes/Dev/TestArena.unity";
        const string ColosseumScene = "Assets/Scenes/Arenas/Colosseum.unity";

        [UnityTest, Timeout(60000)]
        public IEnumerator BakedNavData_IsUsed_AndMatchesRuntimeBake() => Check(Scene);

        [UnityTest, Timeout(180000)]
        public IEnumerator Colosseum_BakedNavData_IsUsed_AndMatchesRuntimeBake() => Check(ColosseumScene);

        static IEnumerator Check(string scene)
        {
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            var em = EnemyManager.Instance;
            Assert.IsNotNull(em.navData, "the arena build should bake and assign ArenaNavData");
            Assert.IsTrue(em.navData.Matches(em.boundsMin, em.boundsMax, em.heightCellSize), "baked data is stale (bounds/cell changed) — rebuild the arena");

            EnemyManager.BakeScene(em.boundsMin, em.boundsMax, em.heightCellSize, out var fresh, out var freshObstacles);
            var baked = em.navData.ToHeightGrid();
            Assert.AreEqual(fresh.W, baked.W);
            Assert.AreEqual(fresh.H, baked.H);
            float maxDiff = 0f;
            for (int i = 0; i < fresh.Heights.Length; i++) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(fresh.Heights[i] - baked.Heights[i]));
            Assert.Less(maxDiff, 0.01f, "baked heights differ from the scene (rebuild the arena)");
            Assert.AreEqual(freshObstacles.Count, em.navData.obstacleCount, "obstacle count differs from the scene (rebuild the arena)");
        }
    }
}
