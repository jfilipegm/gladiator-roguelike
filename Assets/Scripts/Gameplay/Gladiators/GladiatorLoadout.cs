using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Turns the one player prefab into the chosen gladiator (GameSession.Gladiator, else PlayerStats.gladiator) before
    /// anything else on the player wakes: swaps the rigid-part model under the visual root, the movement config (capsule
    /// size), the animation profile, the camera target height and the starting weapon, then installs the traits. R6: the
    /// model is the skin's (GameSession.Skin) when that skin is the gladiator's own; it has the same part names, so the
    /// animator drives it unchanged, and every renderer gets the one shared characterMaterial either way.
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
        /// <summary>Tests: the skin the next player spawns in (beats GameSession.Skin). Cleared after use.</summary>
        public static SkinDef NextSkinOverride;

        public GladiatorDef Current { get; private set; }
        /// <summary>The skin the player wears (null = the gladiator's default look).</summary>
        public SkinDef CurrentSkin { get; private set; }

        void Awake()
        {
            var stats = GetComponent<PlayerStats>();
            var def = NextOverride != null ? NextOverride : GameSession.Gladiator != null ? GameSession.Gladiator : stats != null ? stats.gladiator : null;
            var skin = NextSkinOverride != null ? NextSkinOverride : GameSession.Skin;
            NextOverride = null;
            NextSkinOverride = null;
            if (stats != null && def != null) stats.gladiator = def;
            Apply(def, skin);
        }

        void Apply(GladiatorDef def, SkinDef skin)
        {
            Current = def;
            CurrentSkin = null;
            if (def == null) return;
            if (skin != null)
            {
                if (skin.gladiator == def && skin.model != null)
                {
                    CurrentSkin = skin;
                    Debug.Log("[Loadout] " + def.id + " in skin " + skin.id);
                }
                else Debug.Log("[Loadout] skin " + skin.id + " isn't " + def.id + "'s (or has no model): the default look");
            }
            var model = CurrentSkin != null ? CurrentSkin.model : def.model;
            if (model != null && visualRoot != null) SwapModel(model);
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
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (characterMaterial != null) SetMaterial(r, characterMaterial);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        /// <summary>Every slot of `r` to `mat` (models import without materials; one slot each, but a stray second slot
        /// must not keep the default material).</summary>
        static void SetMaterial(Renderer r, Material mat)
        {
            var slots = r.sharedMaterials;
            if (slots.Length <= 1)
            {
                r.sharedMaterial = mat;
                return;
            }
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            r.sharedMaterials = slots;
        }
    }
}
