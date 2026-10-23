using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// A1 (docs/tracks/A1-ingame-contract.md): every A1 model (Assets/Art/Models/A1/...) stands in for the classic model
    /// of the same path and name, so it must have the same animated parts and about the same size, and the game's data
    /// must point at it (Gladiator/Setup/Art/Use A1 Character Models swaps the references).
    /// </summary>
    public class A1ModelTests
    {
        const string Root = "Assets/Art/Models/";
        const string A1Root = "Assets/Art/Models/A1/";
        static readonly string[] Parts = { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR" };

        static IEnumerable<(string a1, string classic)> Twins()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Models/A1" }))
            {
                var a1 = AssetDatabase.GUIDToAssetPath(guid);
                var classic = Root + a1.Substring(A1Root.Length);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(classic) != null) yield return (a1, classic);
            }
        }

        static HashSet<string> PartsOf(GameObject model)
        {
            var set = new HashSet<string>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(Parts, t.name) >= 0) set.Add(t.name);
            return set;
        }

        static float Height(GameObject prefab)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                bool any = false;
                var b = new Bounds();
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                return any ? b.size.y : 0f;
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EachA1Model_HasItsClassicTwinsParts_Size_AndFewerRenderers()
        {
            int n = 0;
            foreach (var (a1Path, classicPath) in Twins())
            {
                var a1 = AssetDatabase.LoadAssetAtPath<GameObject>(a1Path);
                var classic = AssetDatabase.LoadAssetAtPath<GameObject>(classicPath);
                var pa = PartsOf(a1);
                var pc = PartsOf(classic);
                Assert.IsTrue(pa.SetEquals(pc), $"{a1Path}: parts [{string.Join(",", pa)}] vs classic [{string.Join(",", pc)}]");
                foreach (var t in a1.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Head" || t.name == "ArmL" || t.name == "ArmR")
                        Assert.AreEqual("Body", t.parent.name, $"{a1Path}: {t.name} must hang from Body");
                    if (System.Array.IndexOf(Parts, t.name) >= 0)
                        Assert.Less(Quaternion.Angle(t.localRotation, Quaternion.identity), 0.5f, $"{a1Path}: {t.name} rest rotation");
                }
                float ha = Height(a1), hc = Height(classic);
                Assert.That(ha / hc, Is.InRange(0.85f, 1.15f), $"{a1Path}: height {ha:F2} vs classic {hc:F2}");
                int ra = a1.GetComponentsInChildren<MeshRenderer>(true).Length;
                int rc = classic.GetComponentsInChildren<MeshRenderer>(true).Length;
                Assert.LessOrEqual(ra, rc, $"{a1Path}: {ra} renderers vs classic {rc}");
                n++;
            }
            Assert.Greater(n, 0, "no A1 models yet");
        }

        [Test]
        public void TheGamesData_PointsAtTheA1Models()
        {
            var a1Of = new Dictionary<GameObject, string>();
            foreach (var (a1Path, classicPath) in Twins())
                a1Of[AssetDatabase.LoadAssetAtPath<GameObject>(classicPath)] = a1Path;
            var stale = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                var it = so.GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is GameObject g
                        && a1Of.TryGetValue(g, out var a1))
                        stale.Add($"{path} {it.propertyPath} -> classic (A1: {a1})");
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/P_Player.prefab");
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && src != null && a1Of.ContainsKey(src))
                    stale.Add("P_Player " + t.name + " -> classic");
            }
            Assert.IsEmpty(stale, "run Gladiator/Setup/Art/Use A1 Character Models:\n" + string.Join("\n", stale));
        }
    }
}
