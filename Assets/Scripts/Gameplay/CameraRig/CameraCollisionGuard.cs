using Unity.Cinemachine;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Last line of defence against near-plane clipping (CLAUDE.md M1.1 "no camera clipping through walls"):
    /// right after the CinemachineBrain positions the camera each frame, push a small sphere around it out of any
    /// Environment collider (Physics.ComputePenetration works against non-convex mesh colliders because the probe is
    /// a primitive). Cinemachine's deoccluder/decollider do the smooth work; this only corrects the rare squeeze.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraCollisionGuard : MonoBehaviour
    {
        [Tooltip("Keep at least this much clearance (m); > near clip plane.")]
        public float clearance = 0.16f;
        public int iterations = 3;

        readonly Collider[] _hits = new Collider[8];
        SphereCollider _probe;

        void Awake()
        {
            var go = new GameObject("CameraProbe");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = 2; // Ignore Raycast
            _probe = go.AddComponent<SphereCollider>();
            _probe.isTrigger = true;
            _probe.radius = clearance;
        }

        void OnEnable() => CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        void OnDisable() => CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        void OnDestroy() { if (_probe != null) Destroy(_probe.gameObject); }

        void OnCameraUpdated(CinemachineBrain brain)
        {
            if (brain == null || brain.gameObject != gameObject) return;
            var t = transform;
            Vector3 pos = t.position;
            _probe.radius = clearance;
            for (int it = 0; it < iterations; it++)
            {
                int n = Physics.OverlapSphereNonAlloc(pos, clearance, _hits, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
                if (n == 0) break;
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    var c = _hits[i];
                    if (Physics.ComputePenetration(_probe, pos, Quaternion.identity, c, c.transform.position, c.transform.rotation, out var dir, out var dist))
                    {
                        pos += dir * (dist + 0.005f);
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            if (pos != t.position) t.position = pos;
        }
    }
}
