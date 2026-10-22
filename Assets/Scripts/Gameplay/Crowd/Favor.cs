using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The crowd's Favor meter (docs/FAVOR.md, Phase 5.1). Slower than CrowdExcitement (the crowd's mood): it fills
    /// over the run from streaks, elite kills, variety, style kills, close calls, no-hit stretches and the boss's beats,
    /// and when it's full the crowd throws a gift (CrowdGifts) and the next threshold rises. Event-driven, central tick,
    /// no allocation.
    /// </summary>
    public sealed class Favor : MonoBehaviour, ITickable
    {
        public static Favor Instance { get; private set; }

        public FavorConfig config;
        [Tooltip("Favor gained × this (R7: the Fickle Crowd edict).")]
        public float gainMul = 1f;

        const int VarietySlots = 32;
        readonly int[] _typeIds = new int[VarietySlots];
        readonly float[] _typeLastKill = new float[VarietySlots];
        int _typeCount;

        float _clock, _noHitT, _styleBucketT, _styleThisSecond;
        int _noHitStep;
        PlayerMotor _motor;

        /// <summary>Favor in the meter now.</summary>
        public float Value { get; private set; }
        /// <summary>Favor needed for the next gift.</summary>
        public float Threshold => config != null ? config.ThresholdFor(GiftsEarned) : 100f;
        public float Fill01 => Mathf.Clamp01(Value / Mathf.Max(1f, Threshold));
        public int GiftsEarned { get; private set; }
        /// <summary>All Favor earned this run (stats, tests).</summary>
        public float TotalEarned { get; private set; }

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Feedback, 7);
            GameEvents.OnKill.Subscribe(OnKill);
            GameEvents.OnDamaged.Subscribe(OnDamaged);
            GameEvents.OnAnnounce.Subscribe(OnAnnounce);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(OnKill);
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
            GameEvents.OnAnnounce.Unsubscribe(OnAnnounce);
            if (Instance == this) Instance = null;
        }

        /// <summary>Adds Favor × gainMul (sources, whims, admin, tests); a full meter throws a gift. Ignored once the run is over.</summary>
        public void Add(float amount)
        {
            if (config == null || amount <= 0f) return;
            var run = RunState.Current;
            if (run != null && run.Ended) return;
            amount *= gainMul;
            if (amount <= 0f) return;
            Value += amount;
            TotalEarned += amount;
            while (Value >= Threshold)
            {
                Value -= Threshold;
                GiftsEarned++;
                var gifts = CrowdGifts.Instance;
                if (gifts != null) gifts.Throw();
                GameEvents.OnFavor.Raise(new FavorEvent { Kind = FavorEventKind.GiftEarned });
            }
        }

        void OnKill(in KillEvent e)
        {
            if (config == null) return;
            float gain = 0f;
            if (e.Elite) gain += config.eliteKill;

            // variety: a type not killed for a while
            int slot = -1;
            for (int i = 0; i < _typeCount; i++) if (_typeIds[i] == e.EnemyTypeId) { slot = i; break; }
            if (slot < 0 && _typeCount < VarietySlots) { slot = _typeCount++; _typeIds[slot] = e.EnemyTypeId; _typeLastKill[slot] = -999f; }
            if (slot >= 0)
            {
                if (_clock - _typeLastKill[slot] >= config.varietyWindow) gain += config.variety;
                _typeLastKill[slot] = _clock;
            }

            // style: kills while sliding or airborne, capped per second
            if (_motor == null && Player.Main != null) _motor = Player.Main.GetComponent<PlayerMotor>();
            if (_motor != null && (_motor.IsSliding || !_motor.IsGrounded) && _styleThisSecond < config.styleMaxPerSecond)
            {
                float s = Mathf.Min(config.styleKill, config.styleMaxPerSecond - _styleThisSecond);
                _styleThisSecond += s;
                gain += s;
            }
            Add(gain);
        }

        void OnDamaged(in DamagedEvent e)
        {
            _noHitT = 0f;
            _noHitStep = 0;
        }

        void OnAnnounce(in AnnounceEvent e)
        {
            if (config == null) return;
            switch (e.Kind)
            {
                case AnnounceKind.Streak: Add(config.StreakFavor(e.Tier)); break;
                case AnnounceKind.CloseCall: Add(config.closeCall); break;
                case AnnounceKind.BossEnters: Add(config.bossEnters); break;
                case AnnounceKind.BossPhase: Add(config.bossPhase); break;
            }
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (config == null || dt <= 0f) return;
            var run = RunState.Current;
            if (run != null && run.Ended) return;
            _clock += dt;
            _styleBucketT += dt;
            if (_styleBucketT >= 1f) { _styleBucketT -= 1f; _styleThisSecond = 0f; }
            _noHitT += dt;
            if (_noHitT >= config.noHitStep)
            {
                _noHitT -= config.noHitStep;
                Add(config.NoHitFavor(_noHitStep++));
            }
        }
    }
}
