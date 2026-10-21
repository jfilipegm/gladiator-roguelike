using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Beastmaster's passive (docs/GLADIATORS.md): when he's hit, his companions frenzy (faster bites and runs) for
    /// `seconds`. Status ticks don't count.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Feral Bond", fileName = "TR_FeralBond")]
    public sealed class FeralBondTrait : GladiatorTrait
    {
        public float seconds = 4f;
        [Tooltip("Bite-speed multiplier while frenzied.")]
        public float biteSpeed = 1.6f;
        [Tooltip("Run-speed multiplier while frenzied.")]
        public float runSpeed = 1.25f;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<FeralBond>();
            if (c == null) c = player.AddComponent<FeralBond>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of FeralBondTrait.</summary>
    public sealed class FeralBond : MonoBehaviour
    {
        public FeralBondTrait trait;
        EventHandler<DamagedEvent> _onDamaged;

        void Awake() => _onDamaged = OnDamaged;
        void OnEnable() => GameEvents.OnDamaged.Subscribe(_onDamaged);
        void OnDisable() => GameEvents.OnDamaged.Unsubscribe(_onDamaged);

        void OnDamaged(in DamagedEvent e)
        {
            if (trait == null || e.Status || e.Damage <= 0f) return;
            var pack = CompanionPack.Instance;
            if (pack != null && pack.Count > 0) pack.Frenzy(trait.seconds, trait.biteSpeed, trait.runSpeed);
        }
    }
}
