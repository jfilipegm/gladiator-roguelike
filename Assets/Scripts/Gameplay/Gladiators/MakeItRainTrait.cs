using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>The Golden Idol's ability: a slide with enough gold pays it and sprays coins in a ring.</summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Make It Rain", fileName = "TR_MakeItRain")]
    public sealed class MakeItRainTrait : GladiatorTrait
    {
        public int goldCost = 40;
        public float cooldown = 2f;
        public int coins = 16;
        [Tooltip("Damage per coin before the player's Damage multiplier.")]
        public float damage = 20f;
        public float speed = 15f;
        public float range = 9f;
        public float radius = 0.45f;
        public int pierce = 1;
        public float knockback = 4f;
        public Mesh coinMesh;
        public float coinScale = 1.6f;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<MakeItRain>();
            if (c == null) c = player.AddComponent<MakeItRain>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of MakeItRainTrait.</summary>
    public sealed class MakeItRain : MonoBehaviour, ITickable
    {
        public static readonly int SourceId = GameInfo.StableId("make_it_rain");

        public MakeItRainTrait trait;
        PlayerStats _stats;
        PlayerHealth _health;
        EventHandler<SlideEvent> _onSlide;
        int _mesh = -2;

        public float CooldownLeft { get; private set; }
        public int Showers { get; private set; }
        public int GoldSpent { get; private set; }

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _onSlide = OnSlide;
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Player, 62);
            GameEvents.OnSlide.Subscribe(_onSlide);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnSlide.Unsubscribe(_onSlide);
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (CooldownLeft > 0f && dt > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
        }

        void OnSlide(in SlideEvent e) => Perform();

        /// <summary>Pays and sprays, if there's the gold and the cooldown is over. True if it happened.</summary>
        public bool Perform()
        {
            var run = RunState.Current;
            var pp = PlayerProjectiles.Instance;
            if (trait == null || CooldownLeft > 0f || run == null || pp == null || (_health != null && _health.IsDead)) return false;
            if (!run.SpendGold(trait.goldCost)) return false;
            CooldownLeft = trait.cooldown;
            Showers++;
            GoldSpent += trait.goldCost;
            if (_mesh == -2) _mesh = trait.coinMesh != null ? pp.RegisterMesh(trait.coinMesh) : -1;
            var shot = new ShotParams
            {
                Speed = trait.speed, Range = trait.range, Radius = trait.radius,
                Damage = trait.damage * (_stats != null ? _stats.Damage : 1f),
                CritChance = _stats != null ? _stats.CritChance : 0f, CritMultiplier = 2f,
                Knockback = trait.knockback, Scale = trait.coinScale, Pierce = trait.pierce,
                SourceId = SourceId, Mesh = _mesh,
            };
            Vector3 from = transform.position + Vector3.up * pp.flightHeight;
            float step = 360f / Mathf.Max(1, trait.coins);
            for (int k = 0; k < trait.coins; k++)
                pp.FireStraight(from, Quaternion.Euler(0f, k * step, 0f) * Vector3.forward, shot);
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(from, 18, 6f, 0.14f, new Color(1f, 0.84f, 0.3f), new Color(1f, 0.97f, 0.7f));
            SfxPlayer.Play(SfxId.Coin, transform.position);
            return true;
        }

        /// <summary>Makes it ready now (tests, admin).</summary>
        public void Recharge() => CooldownLeft = 0f;
    }
}
