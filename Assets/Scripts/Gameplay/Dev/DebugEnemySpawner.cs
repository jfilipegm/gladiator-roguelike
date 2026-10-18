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
        [Tooltip("If set, each spawn picks a random type from here instead of `enemy` (perf runs: -mix).")]
        public EnemyDef[] mix;
        [Tooltip("With `affixes`: chance that a spawn is an elite with a random affix.")]
        [Range(0f, 1f)] public float eliteChance;
        public AffixDef[] affixes;
        [Tooltip("With `mix`: max alive of any one type (0 = no cap). Kiting types pile up otherwise — the bot rarely reaches them.")]
        public int maxPerType;
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
            if (EnemyManager.Instance == null) return;
            if (enemy != null) EnemyManager.Instance.Prewarm(enemy, Mathf.Max(enemy.prewarm, targetAlive));
            if (mix != null) foreach (var d in mix) if (d != null) EnemyManager.Instance.Prewarm(d, d.prewarm);
        }

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            if (em == null || enemy == null) return;
            if (RunState.Current != null && RunState.Current.Ended) return; // the arena stays quiet after the run
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
                var def = enemy;
                if (mix != null && mix.Length > 0)
                    for (int roll = 0; roll < 4; roll++)
                    {
                        def = mix[GameRandom.Range(0, mix.Length)];
                        if (maxPerType <= 0 || em.AliveOf(def) < maxPerType) break;
                    }
                var affix = affixes != null && affixes.Length > 0 && GameRandom.Chance(eliteChance) ? affixes[GameRandom.Range(0, affixes.Length)] : null;
                return em.Spawn(def, p, affix);
            }
            return -1;
        }
    }
}
