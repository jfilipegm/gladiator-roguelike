using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// What PlaytestBot can perceive and do. Implemented by the run/session systems as they come online
    /// (pickups: M1.3, enemies: M1.2/M2.1, level-up: M1.3/M2.4, stress spawning: M2.1).
    /// Every member must be allocation-free.
    /// </summary>
    public interface IBotSensor
    {
        bool TryGetNearestPickup(Vector3 from, float maxDistance, out Vector3 position);
        /// <summary>Sum of (away-from-enemy / distance) vectors within radius, on the XZ plane.</summary>
        Vector3 ThreatVector(Vector3 from, float radius);
        bool LevelUpPending(out int cardCount);
        void ChooseCard(int index);
        /// <summary>Spawn up to `count` enemies for a stress test. Returns how many were requested.</summary>
        int RequestStressSpawn(int count);
    }
}
