using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Feel-test spawner (the real Director arrives in M2.3): keeps `targetAlive` enemies alive by spawning on a ring
    /// around the arena edge, and serves PlaytestBot stress requests.
    /// </summary>
    public sealed class DebugEnemySpawner : MonoBehaviour, ITickable
    {
        public EnemyDef enemy;
        public int targetAlive = 25;
        public float spawnInterval = 0.15f;
        public float ringRadius = 17f;
        [Tooltip("Never spawn closer than this to the player.")]
        public float minPlayerDistance = 9f;

        float _timer;

        void OnEnable() => GameLoop.Register(this, TickGroup.Director);
        void OnDisable() => GameLoop.Unregister(this);

        void Start()
        {
            if (EnemyManager.Instance != null && enemy != null) EnemyManager.Instance.Prewarm(enemy, Mathf.Max(enemy.prewarm, targetAlive));
        }

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            if (em == null || enemy == null) return;
            _timer -= dt;
            while (_timer <= 0f && em.AliveCount < targetAlive)
            {
                SpawnOne(em);
                _timer += spawnInterval;
            }
            if (_timer < 0f) _timer = 0f;
        }

        public int SpawnBurst(int count)
        {
            var em = EnemyManager.Instance;
            int n = 0;
            for (int k = 0; k < count; k++) if (SpawnOne(em) >= 0) n++;
            return n;
        }

        int SpawnOne(EnemyManager em)
        {
            Vector3 player = Player.Main != null ? Player.Main.transform.position : Vector3.zero;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float a = GameRandom.Range(0f, Mathf.PI * 2f);
                float r = ringRadius * Mathf.Sqrt(GameRandom.Range(0.55f, 1f));
                var p = em.arenaCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if ((p - player).sqrMagnitude < minPlayerDistance * minPlayerDistance) continue;
                return em.Spawn(enemy, p);
            }
            return -1;
        }
    }
}
