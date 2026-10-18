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
            if (Director.Instance != null) Director.Instance.enabled = false; // stress = a fixed count, not the run's pacing
            // Grow the pool up front (allocation happens here, not during the measured run) and fill fast.
            var em = EnemyManager.Instance;
            var mix = stressSpawner.mix;
            if (em != null && mix != null && mix.Length > 0)
            {
                // same cap the Director will enforce per type (M2.3), with headroom for corpses mid death-pop
                stressSpawner.maxPerType = Mathf.CeilToInt(count * 1.25f / mix.Length);
                foreach (var d in mix) if (d != null) em.Prewarm(d, stressSpawner.maxPerType + 16);
            }
            else if (em != null && stressSpawner.enemy != null)
                em.Prewarm(stressSpawner.enemy, count + 64);
            stressSpawner.targetAlive = Mathf.Max(stressSpawner.targetAlive, count);
            stressSpawner.spawnInterval = 0.01f;
            return count;
        }
    }
}
