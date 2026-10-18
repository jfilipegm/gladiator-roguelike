using UnityEngine;
using UnityEngine.Rendering;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// When the camera is squeezed right up to the player (backed against a pillar), render the player as
    /// shadows-only so the screen isn't filled with the inside of their head. Runs after Cinemachine has placed the camera.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    public sealed class CameraProximityFade : MonoBehaviour
    {
        public Transform target;
        public float hideDistance = 0.75f;
        Renderer[] _renderers;
        bool _hidden;

        void Awake() => _renderers = GetComponentsInChildren<Renderer>(true);

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || target == null) return;
            bool hide = (cam.transform.position - target.position).sqrMagnitude < hideDistance * hideDistance;
            if (hide == _hidden) return;
            _hidden = hide;
            var mode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].shadowCastingMode = mode;
        }
    }
}
