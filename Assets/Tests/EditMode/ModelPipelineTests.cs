using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>
    /// Regression guard for the Blender -> Unity pipeline (BlenderSrc/dev/roundtrip_box.py). Re-export those assets
    /// after touching gladiator_kit.export_fbx or ModelImportRules, then run these.
    /// </summary>
    public class ModelPipelineTests
    {
        const float Eps = 0.002f;

        static GameObject Spawn(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "missing " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return go;
        }

        static Bounds RendererBounds(GameObject root, string name)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.name == name) return r.bounds;
            Assert.Fail("no renderer " + name);
            return default;
        }

        static void AssertVec(Vector3 expected, Vector3 actual, string what)
        {
            Assert.Less((expected - actual).magnitude, Eps, $"{what}: expected {expected:F3} got {actual:F3}");
        }

        [Test]
        public void Box_ArrivesOneByTwoByOne_FrontPlusZ_LeftMinusX()
        {
            var go = Spawn("Assets/Art/Models/Dev/SM_RoundTripBox.fbx");
            try
            {
                var box = RendererBounds(go, "SM_RoundTripBox");
                AssertVec(new Vector3(1, 2, 1), box.size, "box size");
                AssertVec(new Vector3(0, 1, 0), box.center, "box centre (origin at base)");
                AssertVec(new Vector3(0, 1, 0.6f), RendererBounds(go, "Marker_Front").center, "front marker");
                AssertVec(new Vector3(-0.6f, 1, 0), RendererBounds(go, "Marker_Left").center, "left marker");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Rig_EmptiesAndRotatedJointsSurvive()
        {
            var go = Spawn("Assets/Art/Models/Dev/SM_RoundTripRig.fbx");
            try
            {
                AssertVec(new Vector3(0, 0.5f, 0), RendererBounds(go, "Body").center, "body");
                AssertVec(new Vector3(-0.125f, 1.0f, 0.2165f), RendererBounds(go, "Arm").center, "arm under rotated empty");
                foreach (var t in go.GetComponentsInChildren<Transform>())
                {
                    if (t.name == "Body" || t.name == "Arm")
                        Assert.Less(Quaternion.Angle(Quaternion.identity, t.localRotation), 0.01f, t.name + " should keep identity local rotation");
                    if (t.name == "Joint")
                        Assert.AreEqual(-30f, Mathf.DeltaAngle(0, t.localEulerAngles.y), 0.01f, "joint yaw");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
