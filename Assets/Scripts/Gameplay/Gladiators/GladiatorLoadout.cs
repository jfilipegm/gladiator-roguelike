using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Turns the one player prefab into the chosen gladiator (GameSession.Gladiator, else PlayerStats.gladiator) before
    /// anything else on the player wakes: swaps the rigid-part model under the visual root, the movement config (capsule
    /// size), the animation profile, the camera target height and the starting weapon, then installs the traits.
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class GladiatorLoadout : MonoBehaviour
    {
        public Transform visualRoot;
        public Transform cameraTarget;
        [Tooltip("Palette material for swapped models (they import without materials).")]
        public Material characterMaterial;

        /// <summary>Tests: the gladiator the next player spawns as (beats GameSession). Cleared after use.</summary>
        public static GladiatorDef NextOverride;

        public GladiatorDef Current { get; private set; }

        void Awake()
        {
            var stats = GetComponent<PlayerStats>();
            var def = NextOverride != null ? NextOverride : GameSession.Gladiator != null ? GameSession.Gladiator : stats != null ? stats.gladiator : null;
            NextOverride = null;
            if (stats != null && def != null) stats.gladiator = def;
            Apply(def);
        }

        void Apply(GladiatorDef def)
        {
            Current = def;
            if (def == null) return;
            if (def.model != null && visualRoot != null) SwapModel(def.model);
            var motor = GetComponent<PlayerMotor>();
            if (def.movement != null && motor != null) motor.config = def.movement;
            var animator = GetComponent<PlayerPartAnimator>();
            if (def.animProfile != null && animator != null) animator.profile = def.animProfile;
            if (cameraTarget != null)
            {
                var p = cameraTarget.localPosition;
                cameraTarget.localPosition = new Vector3(p.x, def.cameraTargetHeight, p.z);
            }
            var weapons = GetComponent<WeaponSystem>();
            if (def.startingWeapon != null && weapons != null) weapons.startingWeapons = new[] { def.startingWeapon };
            if (def.traits != null)
                foreach (var t in def.traits) if (t != null) t.Install(gameObject);
        }

        void SwapModel(GameObject prefab)
        {
            // detach first: Destroy is deferred, and the animator's rig looks parts up by name this frame
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                var old = visualRoot.GetChild(i).gameObject;
                old.SetActive(false);
                old.transform.SetParent(null, false);
                Destroy(old);
            }
            var model = Instantiate(prefab, visualRoot, false);
            model.name = prefab.name;
            int layer = gameObject.layer;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (characterMaterial != null) r.sharedMaterial = characterMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }
    }
}
