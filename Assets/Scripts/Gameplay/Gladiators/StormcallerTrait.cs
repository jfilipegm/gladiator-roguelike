using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Thunder Champion's passive (docs/GLADIATORS.md): every enemy his hits land on adds a Charge (status ticks
    /// don't), each worth `attackSpeedPerCharge` attack speed up to `maxCharge`; after `graceSeconds` without a hit the
    /// Charge drains at `drainPerSecond`.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Stormcaller", fileName = "TR_Stormcaller")]
    public sealed class StormcallerTrait : GladiatorTrait
    {
        public float attackSpeedPerCharge = 0.015f;
        public int maxCharge = 40;
        [Tooltip("Seconds without a hit before the Charge starts to drain.")]
        public float graceSeconds = 2.5f;
        public float drainPerSecond = 20f;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<Stormcaller>();
            if (c == null) c = player.AddComponent<Stormcaller>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of StormcallerTrait: the Charge and its attack-speed StatMod. The HUD reads Instance.</summary>
    public sealed class Stormcaller : MonoBehaviour, ITickable
    {
        public static Stormcaller Instance { get; private set; }

        public StormcallerTrait trait;

        PlayerStats _stats;
        EventHandler<HitEvent> _onHit;
        StatMod _applied;
        bool _has;
        int _appliedCharge;
        float _sinceHit = 99f;

        public float Charge { get; private set; }
        public int ChargeInt => Mathf.FloorToInt(Charge);
        public float Charge01 => trait != null && trait.maxCharge > 0 ? Mathf.Clamp01(Charge / trait.maxCharge) : 0f;
        public float AttackSpeedBonus => trait == null ? 0f : _appliedCharge * trait.attackSpeedPerCharge;
        /// <summary>Highest Charge reached this run (bot log).</summary>
        public int PeakCharge { get; private set; }

        void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _onHit = OnHit;
        }

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Player, 58);
            GameEvents.OnHit.Subscribe(_onHit);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnHit.Unsubscribe(_onHit);
            Apply(0);
        }

        void OnHit(in HitEvent e)
        {
            if (trait == null || (e.Flags & DamageFlags.Status) != 0) return;
            Charge = Mathf.Min(trait.maxCharge, Charge + 1f);
            _sinceHit = 0f;
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null || dt <= 0f) return;
            _sinceHit += dt;
            if (_sinceHit > trait.graceSeconds && Charge > 0f) Charge = Mathf.Max(0f, Charge - trait.drainPerSecond * dt);
            Refresh();
        }

        /// <summary>Applies the current Charge to the stats now (the tick does it every frame; tests).</summary>
        public void Refresh()
        {
            int c = ChargeInt;
            if (c > PeakCharge) PeakCharge = c;
            Apply(c);
        }

        /// <summary>Sets the Charge (tests, admin).</summary>
        public void SetCharge(float charge) { if (trait != null) { Charge = Mathf.Clamp(charge, 0f, trait.maxCharge); _sinceHit = 0f; Refresh(); } }

        void Apply(int charge)
        {
            if (_stats == null || (charge == _appliedCharge && _has == charge > 0)) return;
            if (_has) _stats.Block.Remove(_applied);
            _has = charge > 0 && trait != null;
            if (_has)
            {
                _applied = new StatMod(StatId.AttackSpeed, ModOp.Multiply, 1f + charge * trait.attackSpeedPerCharge);
                _stats.Block.Add(_applied);
            }
            _appliedCharge = _has ? charge : 0;
        }
    }
}
