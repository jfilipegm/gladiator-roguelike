using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Beastmaster's ability (docs/GLADIATORS.md): starting a slide with the horn ready blows it, and every companion
    /// pounces on the enemies nearest a point `ahead` m along the slide (within `range`): a leap that lands for
    /// `damageMul` bites and applies `status` (Stagger).
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Hunting Horn", fileName = "TR_HuntingHorn")]
    public sealed class HuntingHornTrait : GladiatorTrait
    {
        public float cooldown = 5f;
        [Tooltip("The pounce targets the enemies nearest this point, this far along the slide (m).")]
        public float ahead = 4f;
        public float range = 8f;
        [Tooltip("A pounce lands for this many bites.")]
        public float damageMul = 3f;
        public StatusDef status;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<HuntingHorn>();
            if (c == null) c = player.AddComponent<HuntingHorn>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of HuntingHornTrait.</summary>
    public sealed class HuntingHorn : MonoBehaviour, ITickable
    {
        public HuntingHornTrait trait;
        EventHandler<SlideEvent> _onSlide;
        PlayerHealth _health;

        public float CooldownLeft { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        public int Blows { get; private set; }

        void Awake()
        {
            _onSlide = OnSlide;
            _health = GetComponent<PlayerHealth>();
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Player, 58);
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

        void OnSlide(in SlideEvent e)
        {
            Vector3 dir = e.Velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            Blow(dir.normalized);
        }

        /// <summary>Blows the horn toward `dir` if it's ready and there's a pack. True if any companion leapt.</summary>
        public bool Blow(Vector3 dir)
        {
            if (trait == null || !Ready || (_health != null && _health.IsDead)) return false;
            var pack = CompanionPack.Instance;
            if (pack == null || pack.Count == 0) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            dir.Normalize();
            int leapt = pack.PounceAll(transform.position + dir * trait.ahead, trait.range, trait.damageMul, trait.status);
            if (leapt == 0) return false; // nothing to hunt: keep the horn
            CooldownLeft = trait.cooldown;
            Blows++;
            SfxPlayer.Play(SfxId.Horn, transform.position);
            return true;
        }

        /// <summary>Makes the horn ready now (tests, admin).</summary>
        public void Recharge() => CooldownLeft = 0f;
    }
}
