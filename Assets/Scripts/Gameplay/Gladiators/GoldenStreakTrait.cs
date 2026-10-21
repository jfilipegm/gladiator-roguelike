using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>The Golden Idol's second passive: the announcer's kill streaks multiply gold for a while.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Golden Streak", fileName = "TR_GoldenStreak")]
    public sealed class GoldenStreakTrait : GladiatorTrait
    {
        [Tooltip("Gold multiplier per streak tier (KILLING SPREE!, MASSACRE!, BLOODBATH!, UNSTOPPABLE!...).")]
        public float[] goldByTier = { 1.5f, 2f, 2.5f, 3f };
        public float seconds = 8f;

        public float MultiplierFor(int tier) => goldByTier == null || goldByTier.Length == 0 ? 1f : goldByTier[Mathf.Clamp(tier, 0, goldByTier.Length - 1)];

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<GoldenStreak>();
            if (c == null) c = player.AddComponent<GoldenStreak>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of GoldenStreakTrait: a GoldGain StatMod while a streak's bonus lasts.</summary>
    public sealed class GoldenStreak : MonoBehaviour, ITickable
    {
        public GoldenStreakTrait trait;
        PlayerStats _stats;
        EventHandler<AnnounceEvent> _onAnnounce;
        StatMod _applied;
        bool _has;

        public float Multiplier { get; private set; } = 1f;
        public float Left { get; private set; }
        public int Streaks { get; private set; }

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _onAnnounce = OnAnnounce;
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Player, 61);
            GameEvents.OnAnnounce.Subscribe(_onAnnounce);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnAnnounce.Unsubscribe(_onAnnounce);
            Apply(1f);
        }

        void OnAnnounce(in AnnounceEvent e)
        {
            if (trait == null || e.Kind != AnnounceKind.Streak) return;
            Streaks++;
            Left = trait.seconds;
            Apply(Mathf.Max(Multiplier, trait.MultiplierFor(e.Tier)));
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (Left <= 0f || dt <= 0f) return;
            Left -= dt;
            if (Left <= 0f) Apply(1f);
        }

        void Apply(float mul)
        {
            if (_stats == null || (mul == Multiplier && _has == mul > 1f)) return;
            if (_has) _stats.Block.Remove(_applied);
            _has = mul > 1f;
            _applied = new StatMod(StatId.GoldGain, ModOp.Multiply, mul);
            if (_has) _stats.Block.Add(_applied);
            Multiplier = mul;
        }
    }
}
