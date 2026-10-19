using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Blood Knight's ability (docs/GLADIATORS.md): a Blood meter fills from kills and damage taken; a slide with a
    /// full meter performs the Rite: pay a share of max HP (never below 1 HP, not a hit), empty the meter, and frenzy
    /// for a while (damage and attack-speed multipliers, Bloodthirst heals multiplied). The meter is still while frenzied.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Blood Rite", fileName = "TR_BloodRite")]
    public sealed class BloodRiteTrait : GladiatorTrait
    {
        [Header("Blood meter")]
        public float meterMax = 100f;
        public float perKill = 0.6f;
        public float perElite = 8f;
        [Tooltip("Blood per HP lost to hits.")]
        public float perHpLost = 0.5f;

        [Header("The Rite (a slide with a full meter)")]
        [Tooltip("Share of max HP paid (never below 1 HP).")]
        [Range(0f, 0.9f)] public float hpCost = 0.12f;
        public float frenzySeconds = 8f;
        [Tooltip("Damage multiplier while frenzied.")]
        public float frenzyDamage = 1.4f;
        [Tooltip("Attack-speed multiplier while frenzied.")]
        public float frenzyAttackSpeed = 1.25f;
        [Tooltip("Bloodthirst's kill heals (and their cap) are multiplied by this while frenzied.")]
        public float frenzyHealMul = 2f;

        [Header("Look")]
        [Tooltip("The Bleed crimson-pink, never the orange-red that means \"this hurts you\".")]
        public Color blood = new Color(0.9f, 0.16f, 0.36f);
        public Color core = new Color(1f, 0.9f, 0.92f);
        public string announce = "BLOOD RITE!";

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<BloodRite>();
            if (c == null) c = player.AddComponent<BloodRite>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of BloodRiteTrait: the meter, the Rite and the frenzy. The HUD reads Instance.</summary>
    public sealed class BloodRite : MonoBehaviour, ITickable
    {
        public static BloodRite Instance { get; private set; }

        public BloodRiteTrait trait;
        [Tooltip("Seconds between the frenzy's crimson specks.")]
        public float speckInterval = 0.2f;

        PlayerHealth _health;
        PlayerStats _stats;
        Bloodthirst _thirst;
        EventHandler<KillEvent> _onKill;
        EventHandler<DamagedEvent> _onDamaged;
        EventHandler<SlideEvent> _onSlide;
        StatMod _dmg, _spd;
        bool _frenzyMods;
        float _speck;

        public float Blood { get; private set; }
        public float Fill01 => trait != null && trait.meterMax > 0f ? Mathf.Clamp01(Blood / trait.meterMax) : 0f;
        public bool Full => trait != null && Blood >= trait.meterMax;
        public float FrenzyLeft { get; private set; }
        public bool Frenzied => FrenzyLeft > 0f;
        public int Rites { get; private set; }
        public float HpPaid { get; private set; }

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _stats = GetComponent<PlayerStats>();
            _onKill = OnKill;
            _onDamaged = OnDamaged;
            _onSlide = OnSlide;
        }

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Player, 57);
            GameEvents.OnKill.Subscribe(_onKill);
            GameEvents.OnDamaged.Subscribe(_onDamaged);
            GameEvents.OnSlide.Subscribe(_onSlide);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnKill.Unsubscribe(_onKill);
            GameEvents.OnDamaged.Unsubscribe(_onDamaged);
            GameEvents.OnSlide.Unsubscribe(_onSlide);
            EndFrenzy();
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (!Frenzied || dt <= 0f) return;
            FrenzyLeft -= dt;
            if (FrenzyLeft <= 0f) { EndFrenzy(); return; }
            _speck -= dt;
            if (_speck > 0f) return;
            _speck = speckInterval;
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(transform.position + Vector3.up * 0.9f, 3, 1.6f, 0.09f, trait.blood, trait.core);
        }

        void OnKill(in KillEvent e)
        {
            if (trait != null && !Frenzied) Add(e.Elite ? trait.perElite : trait.perKill);
        }

        void OnDamaged(in DamagedEvent e)
        {
            if (trait != null && !Frenzied) Add(e.Damage * trait.perHpLost);
        }

        void OnSlide(in SlideEvent e)
        {
            if (Full && !Frenzied) Perform();
        }

        void Add(float blood)
        {
            if (blood > 0f && _health != null && !_health.IsDead) Blood = Mathf.Min(trait.meterMax, Blood + blood);
        }

        /// <summary>Fills the meter (tests, admin).</summary>
        public void Fill() { if (trait != null) Blood = trait.meterMax; }

        /// <summary>The Rite, if the meter is full and no frenzy is running. True if it happened.</summary>
        public bool Perform()
        {
            if (!Full || Frenzied || _health == null || _health.IsDead) return false;
            HpPaid += _health.Spend(_health.MaxHp * trait.hpCost);
            Blood = 0f;
            Rites++;
            FrenzyLeft = trait.frenzySeconds;
            _speck = 0f;
            if (_stats != null)
            {
                _dmg = new StatMod(StatId.Damage, ModOp.Multiply, trait.frenzyDamage);
                _spd = new StatMod(StatId.AttackSpeed, ModOp.Multiply, trait.frenzyAttackSpeed);
                _stats.Block.Add(_dmg);
                _stats.Block.Add(_spd);
                _frenzyMods = true;
            }
            if (_thirst == null) _thirst = GetComponent<Bloodthirst>();
            if (_thirst != null) _thirst.HealMul = trait.frenzyHealMul;

            Vector3 at = transform.position + Vector3.up * 0.9f;
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at, 36, 7f, 0.2f, trait.blood, trait.core);
            SfxPlayer.Play(SfxId.BloodRite, at);
            ScreenShake.Add(0.35f);
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Line, Text = trait.announce, Color = trait.blood, Seconds = 1.6f });
            return true;
        }

        void EndFrenzy()
        {
            if (FrenzyLeft <= 0f && !_frenzyMods) return;
            FrenzyLeft = 0f;
            if (_frenzyMods && _stats != null)
            {
                _stats.Block.Remove(_dmg);
                _stats.Block.Remove(_spd);
            }
            _frenzyMods = false;
            if (_thirst != null) _thirst.HealMul = 1f;
        }
    }
}
