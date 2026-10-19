using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>The arena's fire pits (M3.2): permanent burning zones (HazardZones) — stand in one and it burns, jump it
    /// and it doesn't. Orange-red, like everything that hurts the player.</summary>
    public sealed class ArenaFirePits : MonoBehaviour
    {
        [Tooltip("x, y, z = centre, w = radius. Written by ArenaBuilder from the layout.")]
        public Vector4[] pits = System.Array.Empty<Vector4>();
        public float dps = 10f;

        void Start()
        {
            var zones = HazardZones.Instance;
            if (zones == null) return;
            foreach (var p in pits) zones.AddPermanent(new Vector3(p.x, p.y, p.z), p.w, dps);
        }
    }
}
