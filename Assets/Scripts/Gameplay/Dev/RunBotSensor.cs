using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>PlaytestBot's view of a run: enemies via EnemyManager; pickups and level-ups plug in as they land.</summary>
    public sealed class RunBotSensor : MonoBehaviour, IBotSensor
    {
        public DebugEnemySpawner stressSpawner;

        /// <summary>Optional hooks wired by other systems (pickups, level-up UI).</summary>
        public static System.Func<Vector3, float, (bool ok, Vector3 pos)> NearestPickup;
        public static System.Func<int> PendingCards;
        public static System.Action<int> ChooseCardHook;

        void OnEnable() => PlaytestBot.Sensor = this;
        void OnDisable() { if (ReferenceEquals(PlaytestBot.Sensor, this)) PlaytestBot.Sensor = null; }

        public bool TryGetNearestPickup(Vector3 from, float maxDistance, out Vector3 position)
        {
            position = default;
            if (NearestPickup == null) return false;
            var r = NearestPickup(from, maxDistance);
            position = r.pos;
            return r.ok;
        }

        public Vector3 ThreatVector(Vector3 from, float radius) =>
            EnemyManager.Instance != null ? EnemyManager.Instance.ThreatVector(from, radius) : Vector3.zero;

        public bool LevelUpPending(out int cardCount)
        {
            cardCount = PendingCards != null ? PendingCards() : 0;
            return cardCount > 0;
        }

        public void ChooseCard(int index) => ChooseCardHook?.Invoke(index);

        public int RequestStressSpawn(int count)
        {
            if (stressSpawner == null) return 0;
            // Grow the pool up front (allocation happens here, not during the measured run) and fill fast.
            if (EnemyManager.Instance != null && stressSpawner.enemy != null)
                EnemyManager.Instance.Prewarm(stressSpawner.enemy, count + 64);
            stressSpawner.targetAlive = Mathf.Max(stressSpawner.targetAlive, count);
            stressSpawner.spawnInterval = 0.01f;
            return count;
        }
    }
}
