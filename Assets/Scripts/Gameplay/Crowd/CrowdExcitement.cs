using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// How worked up the crowd is, 0..1 (CLAUDE.md §6.9 slice version). Kills push it up (elites far more), a player
    /// close to death holds it high, and it settles back to a murmur. Published as the global shader value
    /// _CrowdExcitement (the crowd cheers); the announcer and crowd audio read Value.
    /// Playtest pass: a multi-kill (many kills inside a second) swells it at once with a cheer, bigger for bigger
    /// bursts; Activity says whether the player is fighting (kills or hits lately), and when they stop, the crowd
    /// cools off faster and settles lower, which the audio plays as a slower, quieter murmur.
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
        [Tooltip("A spike at least this far above the current level gets a cheer.")]
        [Range(0f, 1f)] public float cheerJump = 0.2f;

        [Header("Multi-kills")]
        [Tooltip("Kills are counted over roughly this many seconds (a decaying tally).")]
        public float multiKillWindow = 0.8f;
        [Tooltip("Tally thresholds for a multi-kill swell, small to big.")]
        public float[] multiKillTiers = { 6f, 14f, 28f };
        [Tooltip("Excitement added at each tier.")]
        public float[] multiKillBoost = { 0.12f, 0.22f, 0.35f };
        [Tooltip("Seconds before the same tier can swell again.")]
        public float multiKillCooldown = 2.5f;

        [Header("Not fighting")]
        [Tooltip("Seconds without a kill or a hit before the crowd starts to cool off.")]
        public float calmAfter = 3.5f;
        [Tooltip("Seconds for Activity to fall from 1 to 0 once cooling.")]
        public float calmTime = 4f;
        [Tooltip("Where excitement settles when nobody's fighting (idle while fighting).")]
        [Range(0f, 1f)] public float calmFloor = 0.04f;
        [Tooltip("Decay multiplier while nobody's fighting.")]
        public float calmDecayMul = 2.5f;

        static readonly int ExcitementId = Shader.PropertyToID("_CrowdExcitement");
        PlayerHealth _health;
        float _value, _activity, _sinceCombat = 999f, _tally, _lastSwell = -999f, _clock;
        int _swellTier = -1;

        public float Value => _value;
        /// <summary>1 while the player is fighting, easing to 0 a few seconds after the last kill or hit.</summary>
        public float Activity => _activity;
        /// <summary>How many multi-kill swells have fired (tests, bot log).</summary>
        public int Swells { get; private set; }
        public float KillTally => _tally;

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
            GameEvents.OnHit.Subscribe(OnHit);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            GameEvents.OnHit.Unsubscribe(OnHit);
            if (Instance == this) Instance = null;
        }

        void OnKill(in KillEvent e)
        {
            _value = Mathf.Min(1f, _value + (e.Elite ? perEliteKill : perKill));
            _sinceCombat = 0f;
            _tally += 1f;
            CheckSwell();
        }

        void OnHit(in HitEvent e)
        {
            if ((e.Flags & DamageFlags.Status) == 0) _sinceCombat = 0f; // burn ticks on a kited horde don't count
        }

        /// <summary>A burst of kills: the crowd erupts, louder for bigger bursts.</summary>
        void CheckSwell()
        {
            if (multiKillTiers == null) return;
            if (_clock - _lastSwell > multiKillCooldown) _swellTier = -1;
            for (int t = multiKillTiers.Length - 1; t > _swellTier; t--)
            {
                if (_tally < multiKillTiers[t]) continue;
                float boost = multiKillBoost != null && t < multiKillBoost.Length ? multiKillBoost[t] : 0.15f;
                _value = Mathf.Min(1f, _value + boost);
                _swellTier = t;
                _lastSwell = _clock;
                Swells++;
                SfxPlayer.Play2D(SfxId.Cheer, Mathf.Lerp(0.55f, 1f, (t + 1f) / multiKillTiers.Length));
                return;
            }
        }

        /// <summary>Boss entrance, boss kill, big moments: jump straight to at least `level`.</summary>
        public void Spike(float level)
        {
            level = Mathf.Clamp01(level);
            if (level - _value >= cheerJump) SfxPlayer.Play2D(SfxId.Cheer, Mathf.Lerp(0.6f, 1f, level));
            _value = Mathf.Max(_value, level);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (_health == null && Player.Main != null) _health = Player.Main.GetComponent<PlayerHealth>();
            _clock += dt;
            _sinceCombat += dt;
            _tally = Mathf.Max(0f, _tally - _tally * dt / Mathf.Max(0.05f, multiKillWindow));
            bool fighting = _sinceCombat < calmAfter;
            _activity = fighting ? Mathf.MoveTowards(_activity, 1f, dt * 3f) : Mathf.MoveTowards(_activity, 0f, dt / Mathf.Max(0.1f, calmTime));
            float floor = Mathf.Lerp(calmFloor, idle, _activity);
            if (_health != null && !_health.IsDead && _health.Hp01 < nearDeathHp) floor = nearDeathFloor;
            float fall = decay * Mathf.Lerp(calmDecayMul, 1f, _activity);
            _value = _value > floor ? Mathf.Max(floor, _value - fall * dt) : Mathf.MoveTowards(_value, floor, dt);
            Shader.SetGlobalFloat(ExcitementId, _value);
        }
    }
}
