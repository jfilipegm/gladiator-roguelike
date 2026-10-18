using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Marks geometry ground enemies walk ON (floor, hills, ramps, stairs, mounds, decks): it feeds the HeightGrid
    /// and is never treated as an obstacle. Everything else that's solid and taller than knee height is an obstacle.
    /// </summary>
    public sealed class WalkableSurface : MonoBehaviour { }
}
