using System;
using System.Collections;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gladiator.Tests
{
    /// <summary>
    /// T1 (docs/tracks/T1-playground.md), the Playground: its Showroom has every gladiator, skin and enemy (bosses
    /// included) from the data on show under it, each with its name label, the shared character material and no
    /// collider; the bosses stand in the last row at their EnemyDef.scale; every model's ArmR moves within a second (the
    /// idle → run → attack → hit loop) and the loop allocates nothing; the run is timeless, god mode is on, the Director
    /// is off, nothing spawns, and the run is never recorded. Built by Gladiator/Setup/Dev/Build Playground.
    /// </summary>
    public class PlaygroundTests
    {
        const string Scene = "Assets/Scenes/Dev/Playground.unity";

        static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

        static List<T> All<T>(string folder) where T : Object
        {
            var paths = new List<string>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
                paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            var list = new List<T>();
            foreach (var p in paths) { var a = Load<T>(p); if (a != null) list.Add(a); }
            return list;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // leave the Playground before the next test sets its own switches: its Showroom puts god mode and timeless
            // back as it found them when it goes, which must not land on top of the next test's
            var empty = SceneManager.CreateScene("PlaygroundTestsEmpty" + Time.frameCount);
            SceneManager.SetActiveScene(empty);
            var playground = SceneManager.GetSceneByPath(Scene);
            if (playground.IsValid() && playground.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(playground);
                while (op != null && !op.isDone) yield return null;
            }
            DevSettings.ResetAll();
            TimeController.ResetForNewRun();
            GameSession.Clear();
        }

        static IEnumerator Boot()
        {
            DevSettings.ResetAll();
            TimeController.GameSpeed = 1f;
            TimeController.ResetForNewRun();
            GameSession.Clear();
            var op = SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            Assert.IsNotNull(op, Scene + " isn't in the build settings: run Gladiator/Setup/Dev/Build Playground");
            while (!op.isDone) yield return null;
            yield return null;
            while (RunIntro.Instance != null && !RunIntro.Instance.Done) yield return null;
            yield return null;
        }

        static Showroom Room()
        {
            var room = Object.FindAnyObjectByType<Showroom>();
            Assert.IsNotNull(room, "the Playground has a Showroom");
            Assert.Greater(room.Count, 0, "the Showroom built its rows");
            return room;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Showroom_HasEveryGladiatorSkinAndEnemy_EachWithItsLabel()
        {
            yield return Boot();
            var room = Room();
            var expected = new List<Object>();
            foreach (var g in All<GladiatorDef>("Assets/Data/Gladiators")) expected.Add(g);
            foreach (var s in All<SkinDef>("Assets/Data/Skins")) expected.Add(s);
            var catalog = Load<EnemyCatalog>("Assets/Data/EnemyCatalog.asset");
            Assert.IsNotNull(catalog, "the EnemyCatalog");
            foreach (var e in catalog.enemies) if (e != null) expected.Add(e);
            Assert.Greater(expected.Count, 30, "the data: gladiators, skins, enemies");

            foreach (var def in expected)
            {
                int i = room.IndexOf(def);
                Assert.GreaterOrEqual(i, 0, def.name + " isn't on show");
                var model = room.ModelAt(i);
                Assert.IsNotNull(model, def.name + "'s model");
                Assert.IsTrue(model.IsChildOf(room.transform), def.name + "'s model is under the Showroom");
                Assert.IsTrue(model.gameObject.activeInHierarchy, def.name + "'s model is shown");
                var renderers = model.GetComponentsInChildren<Renderer>();
                Assert.Greater(renderers.Length, 0, def.name + " has meshes");
                foreach (var r in renderers)
                    Assert.IsTrue(r.sharedMaterial == room.material || r.sharedMaterial == room.flashMaterial,
                        def.name + "/" + r.name + " wears the shared character material, not " + (r.sharedMaterial != null ? r.sharedMaterial.name : "none"));
                var label = room.LabelAt(i);
                Assert.IsNotNull(label, def.name + "'s label");
                Assert.IsTrue(label.IsChildOf(room.transform) && label.gameObject.activeInHierarchy, def.name + "'s label is shown under the Showroom");
                Assert.IsTrue(label.GetComponent<Renderer>() != null, def.name + "'s label draws"); // Unity's ==: the Editor's fake null
                Assert.Greater(label.position.y, model.position.y + 0.5f, def.name + "'s label is over its head");
                Assert.AreEqual(Showroom.NameOf(def), room.LabelText(i), def.name + "'s label says its name");
            }
            Assert.AreEqual(expected.Count, room.Count, "nothing else on show");
            Assert.AreEqual(0, room.GetComponentsInChildren<Collider>(true).Length, "no colliders: the player walks through them");

            // the bosses: the last row, at full size (their scale, give or take the hit's squash)
            int bosses = 0;
            foreach (var b in All<BossDef>("Assets/Data/Bosses"))
            {
                if (b.enemy == null) continue;
                int i = room.IndexOf(b.enemy);
                Assert.GreaterOrEqual(i, 0, b.name + "'s body is on show");
                Assert.AreEqual(Showroom.BossRow, room.RowOf(i), b.enemy.name + " stands with the bosses");
                Assert.AreEqual(b.enemy.scale, room.ModelAt(i).localScale.y, b.enemy.scale * 0.15f, b.enemy.name + " at full size");
                bosses++;
            }
            Assert.Greater(bosses, 0, "the bosses");
            foreach (var g in All<GladiatorDef>("Assets/Data/Gladiators"))
                Assert.AreEqual(Showroom.GladiatorRow, room.RowOf(room.IndexOf(g)), g.name + " stands in the gladiators' row");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Showroom_EveryModelAnimates_AndTheLoopAllocatesNothing()
        {
            yield return Boot();
            var room = Room();
            var arms = new List<Transform>();
            var owners = new List<int>();
            var start = new List<Quaternion>();
            for (int i = 0; i < room.Count; i++)
            {
                var arm = FindDeep(room.ModelAt(i), "ArmR");
                if (arm == null) continue;
                arms.Add(arm);
                owners.Add(i);
                start.Add(arm.localRotation);
            }
            Assert.Greater(arms.Count, 0, "models with an ArmR");
            var moved = new float[arms.Count];
            float t = 0f;
            while (t < 1.2f)
            {
                yield return null;
                t += Time.deltaTime;
                for (int k = 0; k < arms.Count; k++) moved[k] = Mathf.Max(moved[k], Quaternion.Angle(start[k], arms[k].localRotation));
            }
            int nobody = room.IndexOf(Load<GladiatorDef>("Assets/Data/Gladiators/GL_Nobody.asset"));
            int at = owners.IndexOf(nobody);
            Assert.GreaterOrEqual(at, 0, "the Nobody's model has an ArmR");
            Assert.Greater(moved[at], 2f, "the Nobody's sword arm moves");
            for (int k = 0; k < arms.Count; k++)
                Assert.Greater(moved[k], 0.5f, room.DefAt(owners[k]).name + "'s ArmR didn't move in a second (move " + room.MoveOf(owners[k]) + ")");
            Debug.Log($"[Playground] {arms.Count} of {room.Count} models have an ArmR; all of them moved");

            // the loop: every move of every model, then measured
            for (int k = 0; k < 400; k++) room.Tick(0.016f, 0.016f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 400; k++) room.Tick(0.016f, 0.016f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log($"[Playground] 400 Showroom ticks ({room.Count} models) allocated {allocated} B");
            Assert.AreEqual(0, allocated, "the Showroom's tick allocated");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheRun_IsTimeless_GodModeOn_DirectorOff_NothingSpawns_NotRecorded()
        {
            yield return Boot();
            Room();
            Assert.IsTrue(DevSettings.Timeless, "the run is timeless");
            Assert.IsTrue(DevSettings.GodMode, "god mode starts on");
            var director = Object.FindAnyObjectByType<Director>(FindObjectsInactive.Include);
            Assert.IsNotNull(director, "the Director is there (F1 can turn it on)");
            Assert.IsFalse(director.enabled, "the Director is off");
            Assert.IsNull(Director.Instance, "no Director running");
            var end = Object.FindAnyObjectByType<RunEndController>();
            Assert.IsNotNull(end, "the run's end");
            Assert.IsTrue(end.meta == null, "a Playground run is never recorded in the save"); // Unity's ==: an empty field is a fake null
            var run = RunState.Current;
            Assert.IsNotNull(run, "the run");
            Assert.IsTrue(Object.FindAnyObjectByType<BossFight>() == null, "no boss comes");
            float elapsed = run.Elapsed;
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(elapsed, run.Elapsed, "the run clock doesn't move");
            Assert.IsFalse(run.Ended, "the run goes on");
            Assert.AreEqual(0, EnemyManager.Instance.AliveCount, "nothing spawned on its own");

            // leaving puts the switches back as they were
            var room = Object.FindAnyObjectByType<Showroom>();
            room.enabled = false;
            Assert.IsFalse(DevSettings.Timeless, "timeless back off");
            Assert.IsFalse(DevSettings.GodMode, "god mode back off");
            room.enabled = true;
            Assert.IsTrue(DevSettings.Timeless && DevSettings.GodMode, "on again");
        }
    }
}
