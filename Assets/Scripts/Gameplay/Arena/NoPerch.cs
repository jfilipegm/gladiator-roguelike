using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// A top nobody may stand on (P1, the producer's playtest of v0.17.0: "he can hit the mobs but they cant hit the
    /// player"): a prop's box, or an invisible sleeve ArenaBuilder puts round a statue, a pillar or a shed, more than a
    /// metre above the ground enemies walk on. PlayerMotor doesn't count its collider as ground and slides the player
    /// off it, so an apex or an edge can't hold them either.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoPerch : MonoBehaviour
    {
        /// <summary>Which way (XZ, unit) to slide a player at `pos` off `col`: away from its middle, unless that leads out
        /// of the arena (a prop against the wall), then toward the arena's centre.</summary>
        public static Vector3 SlideDirection(Vector3 pos, Collider col) => SlideDirection(pos, col, ArenaBounds.Instance);

        /// <summary>The same against a given boundary (null: away from the middle, or toward the origin).</summary>
        public static Vector3 SlideDirection(Vector3 pos, Collider col, ArenaBounds bounds)
        {
            var b = col.bounds;
            Vector2 c = bounds != null ? bounds.center : Vector2.zero;
            var toCentre = new Vector3(c.x - pos.x, 0f, c.y - pos.z);
            var away = new Vector3(pos.x - b.center.x, 0f, pos.z - b.center.z);
            if (away.sqrMagnitude < 1e-4f) away = toCentre;
            if (away.sqrMagnitude < 1e-4f) away = Vector3.right;
            away.Normalize();
            if (bounds != null && toCentre.sqrMagnitude > 1e-4f)
            {
                float reach = Mathf.Max(b.extents.x, b.extents.z) + 0.6f; // off the top and a step beyond
                if (!bounds.Contains(pos.x + away.x * reach, pos.z + away.z * reach)) away = toCentre.normalized;
            }
            return away;
        }
    }
}
