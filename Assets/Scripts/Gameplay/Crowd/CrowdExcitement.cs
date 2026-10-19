using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// How worked up the crowd is, 0..1 (CLAUDE.md §6.9 slice version). Kills push it up (elites far more), a player
    /// close to death holds it high, and it settles back to a murmur. Published as the global shader value
    /// _CrowdExcitement (the crowd cheers); the announcer and crowd audio read Value in Phase 4.
    /// </summary>
    public sealed class CrowdExcitement : MonoBehaviour, ITickable
    {
        public static CrowdExcitement Instance { get; private set; }

        [Range(0f, 1f)] public float idle = 0.12f;
        [Tooltip("Added per kill (a kill streak of ~60 a few seconds apart maxes it out).")]
        public float perKill = 0.012f;
        public float perEliteKill = 0.25f;
        [Tooltip("Per second, toward idle.")]
        public float decay = 0.08f;
        [Tooltip("Floor while the player is below this fraction of max HP (the crowd smells blood).")]
        [Range(0f, 1f)] public float nearDeathHp = 0.25f;
        [Range(0f, 1f)] public float nearDeathFloor = 0.6f;

        static readonly int ExcitementId = Shader.PropertyToID("_CrowdExcitement");
        PlayerHealth _health;
        float _value;

        public float Value => _value;

        void Awake()
        {
            Instance = this;
            _value = idle;
            Shader.SetGlobalFloat(ExcitementId, _value);
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Feedback, 5);
            GameEvents.OnKill.Subscribe(OnKill);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            if (Instance == this) Instance = null;
        }

        void OnKill(in KillEvent e) => _value = Mathf.Min(1f, _value + (e.Elite ? perEliteKill : perKill));

        /// <summary>Boss entrance, boss kill, big moments: jump straight to at least `level`.</summary>
        public void Spike(float level) => _value = Mathf.Max(_value, Mathf.Clamp01(level));

        public void Tick(float dt, float unscaledDt)
        {
            if (_health == null && Player.Main != null) _health = Player.Main.GetComponent<PlayerHealth>();
            float floor = idle;
            if (_health != null && !_health.IsDead && _health.Hp01 < nearDeathHp) floor = nearDeathFloor;
            _value = _value > floor ? Mathf.Max(floor, _value - decay * dt) : Mathf.MoveTowards(_value, floor, dt);
            Shader.SetGlobalFloat(ExcitementId, _value);
        }
    }
}
