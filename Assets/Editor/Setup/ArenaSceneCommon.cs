using Gladiator.Core;
using Gladiator.Gameplay;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gladiator.EditorTools
{
    /// <summary>Plumbing every gameplay scene needs: loop, look, player + camera rig, and placement helpers.</summary>
    public static class ArenaSceneCommon
    {
        const string Models = "Assets/Art/Models/";

        public static void AddLook(Vector3 sunEuler)
        {
            var sun = SceneSetupUtil.DirectionalLight("Sun", sunEuler, new Color(1f, 0.94f, 0.82f), 1.3f);
            var look = new GameObject("EnvironmentLighting").AddComponent<EnvironmentLighting>();
            look.skybox = AssetDatabase.LoadAssetAtPath<Material>(LookSetup.SkyMat);
            look.sun = sun;
            look.Apply();
            var vol = new GameObject("PostVolume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookSetup.VolumeProfilePath);
        }

        public static GameLoop AddGameLoop() => new GameObject("[GameLoop]").AddComponent<GameLoop>();

        /// <summary>Player prefab instance + Main Camera (brain) + orbit vcam + PlayerCameraRig.</summary>
        public static (Player player, PlayerCameraRig rig) AddPlayerAndCamera(Vector3 spawn, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSetup.PrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0, yaw, 0));
            var player = go.GetComponent<Player>();
            var target = go.transform.Find("CameraTarget");

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;

            var vcamGo = new GameObject("PlayerVCam");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            vcam.LookAt = target;
            var lens = vcam.Lens;
            lens.FieldOfView = 62f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 300f;
            vcam.Lens = lens;
            vcamGo.AddComponent<CinemachineOrbitalFollow>();
            vcamGo.AddComponent<CinemachineRotationComposer>();
            vcamGo.AddComponent<CinemachineDeoccluder>();
            vcamGo.AddComponent<CinemachineDecollider>(); // after the deoccluder: extensions run in add order

            var rig = new GameObject("CameraRig").AddComponent<PlayerCameraRig>();
            rig.config = AssetDatabase.LoadAssetAtPath<CameraConfig>(PlayerSetup.CameraPath);
            rig.player = player;
            rig.motor = go.GetComponent<PlayerMotor>();
            rig.vcam = vcam;
            rig.lookTarget = target;
            return (player, rig);
        }

        /// <summary>Big static geometry: Environment layer, mesh colliders, static batching. The camera avoids it.</summary>
        public static GameObject PlaceSolid(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale = null)
        {
            var go = Place(model, parent, pos, yaw, scale, Layers.Environment);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return go;
        }

        /// <summary>Small props: Props layer (player collides, camera ignores), one fitted box collider.</summary>
        public static GameObject PlaceProp(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale = null)
        {
            var go = Place(model, parent, pos, yaw, scale, Layers.Props);
            var b = LocalBounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
            return go;
        }

        /// <summary>Visual only (no collider).</summary>
        public static GameObject PlaceDecor(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale = null) =>
            Place(model, parent, pos, yaw, scale, Layers.Props);

        static GameObject Place(string model, Transform parent, Vector3 pos, float yaw, Vector3? scale, int layer)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (prefab == null) { Debug.LogError("[ArenaSceneCommon] missing model " + model); return new GameObject("MISSING " + model); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            if (scale.HasValue) go.transform.localScale = scale.Value;
            LookSetup.AssignMaterial(go, AssetDatabase.LoadAssetAtPath<Material>(LookSetup.EnvMat));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            return go;
        }

        /// <summary>Invisible cylinder-ish wall so nothing leaves the arena (Environment layer: camera stays in too).</summary>
        public static void AddBoundaryRing(Transform parent, float radius, float height, int segments)
        {
            var ring = new GameObject("BoundaryRing").transform;
            ring.SetParent(parent, false);
            float seg = 2f * Mathf.PI * radius / segments * 1.05f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var w = new GameObject("Wall" + i);
                w.layer = Layers.Environment;
                w.transform.SetParent(ring, false);
                w.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (radius + 0.5f) + Vector3.up * height * 0.5f,
                    Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                var bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(seg, height, 1f);
                GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic);
            }
        }

        static Bounds LocalBounds(GameObject go)
        {
            var inv = go.transform.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                var mb = m.bounds;
                var toRoot = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toRoot.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}
