using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Asterion's ability (docs/GLADIATORS.md): a Rage meter fills from the press of the crowd (every enemy within
    /// `pressRadius` adds `pressPerEnemy` a second, an elite or a boss `pressHeavy` times that, capped at
    /// `pressMaxPerSecond`: "CORNERED") and from hits taken (`perHit`, not status ticks); after `calmDelay` with nobody
    /// that close and no hit it cools. Full, he is Unchained at once (it isn't a button; `triggerOnSlide` makes it wait
    /// for a slide): a bellow throws back everything round him, and for `unchainedSeconds` the chain whirls 360°
    /// (WeaponSystem.WhirlSweeps), he's faster, nothing knocks him back, Gore works from every side, and he swells and
    /// drops his horns. Then he's Spent for `spentSeconds` (slower, head bowed, the meter can't fill), then calm again.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Rage", fileName = "TR_Rage")]
    public sealed class RageTrait : GladiatorTrait
    {
        [Header("Rage meter")]
        public float meterMax = 100f;
        [Tooltip("Metres from his centre (plus the enemy's radius) that count as the press. Not scaled by Area.")]
        public float pressRadius = 3.5f;
        [Tooltip("Rage per second for each enemy in the press.")]
        public float pressPerEnemy = 1f;
        [Tooltip("An elite or a Scripted enemy (a boss) counts as this many.")]
        [Min(1f)] public float pressHeavy = 4f;
        [Tooltip("The press's cap (Rage per second): \"CORNERED\" by the bar.")]
        public float pressMaxPerSecond = 5f;
        [Tooltip("Rage per hit taken (status ticks don't count).")]
        public float perHit = 3f;
        [Tooltip("Seconds with nobody in the press and no hit before the meter cools.")]
        public float calmDelay = 2.5f;
        public float coolPerSecond = 8f;
        [Tooltip("Q1's fallback: a full meter waits for the next slide instead of Unchaining him at once (the HUD says \"SLIDE!\").")]
        public bool triggerOnSlide;

        [Header("Unchained")]
        public float unchainedSeconds = 8f;
        [Tooltip("Attack-speed multiplier while Unchained.")]
        public float attackSpeed = 1.3f;
        [Tooltip("Move-speed multiplier while Unchained.")]
        public float moveSpeed = 1.2f;
        [Tooltip("He swells: PlayerPartAnimator.ScaleBoost.")]
        public float scale = 1.1f;
        [Tooltip("Horns down: forward body lean (deg), PlayerPartAnimator.ExtraLean.")]
        public float lean = 14f;

        [Header("The bellow (as he's Unchained)")]
        [Tooltip("× √Area.")]
        public float bellowRadius = 4f;
        [Tooltip("Damage before the player's Damage multiplier.")]
        public float bellowDamage = 8f;
        [Tooltip("Before the player's Knockback multiplier: away from him.")]
        public float bellowKnockback = 12f;

        [Header("Spent (after Unchained; 0 s = straight back to calm)")]
        public float spentSeconds = 2f;
        [Tooltip("Move-speed multiplier while Spent.")]
        public float spentMoveSpeed = 0.75f;
        [Tooltip("Head bowed (deg), PlayerPartAnimator.HeadDrop.")]
        public float spentHeadDrop = 30f;

        [Header("Look (never the orange-red that means \"this hurts you\")")]
        [Tooltip("Snorts of breath from this share of the meter.")]
        [Range(0f, 1f)] public float breathFrom = 0.5f;
        [Tooltip("Seconds between snorts at breathFrom (x) and at a full meter (y).")]
        public Vector2 breathEvery = new Vector2(0.9f, 0.4f);
        [Tooltip("Seconds between Unchained's white-gold specks.")]
        public float speckEvery = 0.12f;
        public Color bronze = new Color(0.80f, 0.56f, 0.30f);
        public Color hot = new Color(1f, 0.93f, 0.7f);
        public Color spent = new Color(0.45f, 0.45f, 0.48f);
        public Color speck = new Color(1f, 0.9f, 0.55f);
        public Color breath = new Color(0.95f, 0.95f, 0.95f);
        public string announce = "UNCHAINED!";

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<Rage>();
            if (c == null) c = player.AddComponent<Rage>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of RageTrait: the meter, Unchained and Spent. The HUD and Gore read Instance.</summary>
    public sealed class Rage : MonoBehaviour, ITickable
    {
        public enum Mode : byte { Calm, Unchained, Spent }

        public static Rage Instance { get; private set; }
        public static readonly int BellowSourceId = GameInfo.StableId("bellow");

        public RageTrait trait;

        PlayerHealth _health;
        PlayerStats _stats;
        PlayerMotor _motor;
        WeaponSystem _weapons;
        PlayerPartAnimator _animator;
        Transform _head;
        bool _headLooked;
        EventHandler<DamagedEvent> _onDamaged;
        EventHandler<SlideEvent> _onSlide;
        readonly int[] _hits = new int[512];
        StatMod _atk, _move, _tired;
        bool _unchainedMods, _spentMod, _knockHeld, _posed;
        float _savedKnock = 1f;
        float _quiet, _breath, _speck;

        public float Value { get; private set; }
        public float Fill01 => trait != null && trait.meterMax > 0f ? Mathf.Clamp01(Value / trait.meterMax) : 0f;
        public bool Full => trait != null && Value >= trait.meterMax;
        public Mode Current { get; private set; }
        public bool IsUnchained => Current == Mode.Unchained;
        public bool IsSpent => Current == Mode.Spent;
        /// <summary>Calm with a full meter, waiting for a slide (triggerOnSlide): the HUD's "SLIDE!".</summary>
        public bool WaitingForSlide => Current == Mode.Calm && Full && trait != null && trait.triggerOnSlide;
        public float UnchainedLeft { get; private set; }
        public float Unchained01 => trait != null && trait.unchainedSeconds > 0f ? Mathf.Clamp01(UnchainedLeft / trait.unchainedSeconds) : 0f;
        public float SpentLeft { get; private set; }
        /// <summary>The press this frame, weighted (an elite or a boss counts pressHeavy).</summary>
        public int Pressing { get; private set; }
        /// <summary>Rage per second from the press this frame (capped).</summary>
        public float PressRate { get; private set; }
        /// <summary>The press is at its cap: "CORNERED".</summary>
        public bool Cornered { get; private set; }

        /// <summary>Times Unchained this run (bot log, tests).</summary>
        public int Unchains { get; private set; }
        /// <summary>Enemies hit by bellows this run.</summary>
        public int BellowHits { get; private set; }
        /// <summary>The biggest weighted press this run (bot log: tunes pressRadius).</summary>
        public int PeakPress { get; private set; }
        /// <summary>Seconds spent Unchained this run.</summary>
        public float UnchainedTime { get; private set; }

        void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _stats = GetComponent<PlayerStats>();
            _motor = GetComponent<PlayerMotor>();
            _weapons = GetComponent<WeaponSystem>();
            _animator = GetComponent<PlayerPartAnimator>();
            _onDamaged = OnDamaged;
            _onSlide = OnSlide;
        }

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Player, 59);
            GameEvents.OnDamaged.Subscribe(_onDamaged);
            GameEvents.OnSlide.Subscribe(_onSlide);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnDamaged.Unsubscribe(_onDamaged);
            GameEvents.OnSlide.Unsubscribe(_onSlide);
            Halt();
        }

        bool Down => (_health != null && _health.IsDead) || RunState.CombatOver;

        void OnDamaged(in DamagedEvent e)
        {
            if (trait == null || e.Status || Current != Mode.Calm || Down) return;
            Value = Mathf.Min(trait.meterMax, Value + trait.perHit);
            _quiet = 0f;
        }

        void OnSlide(in SlideEvent e)
        {
            if (trait != null && trait.triggerOnSlide && Current == Mode.Calm && Full) Unchain();
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (trait == null) return;
            if (Down) { Halt(); return; }
            if (dt <= 0f) return;
            switch (Current)
            {
                case Mode.Calm: TickCalm(dt); break;
                case Mode.Unchained: TickUnchained(dt); break;
                case Mode.Spent: TickSpent(dt); break;
            }
        }

        void TickCalm(float dt)
        {
            CountPress();
            if (Pressing > 0)
            {
                Value = Mathf.Min(trait.meterMax, Value + PressRate * dt);
                _quiet = 0f;
            }
            else
            {
                _quiet += dt;
                if (_quiet >= trait.calmDelay && Value > 0f) Value = Mathf.Max(0f, Value - trait.coolPerSecond * dt);
            }
            Breathe(dt);
            if (Full && !trait.triggerOnSlide) Unchain();
        }

        void CountPress()
        {
            int pressing = 0, heavyCount = Mathf.Max(1, Mathf.RoundToInt(trait.pressHeavy));
            var em = EnemyManager.Instance;
            if (em != null)
            {
                int n = em.QueryAlive(transform.position, trait.pressRadius, _hits); // XZ, within pressRadius + the enemy's radius
                for (int k = 0; k < n; k++)
                {
                    int i = _hits[k];
                    var def = em.GetDef(i);
                    bool heavy = em.IsElite(i) || (def != null && def.behaviour == EnemyBehaviour.Scripted);
                    pressing += heavy ? heavyCount : 1;
                }
            }
            Pressing = pressing;
            if (pressing > PeakPress) PeakPress = pressing;
            float raw = pressing * trait.pressPerEnemy;
            PressRate = Mathf.Min(raw, trait.pressMaxPerSecond);
            Cornered = pressing > 0 && raw >= trait.pressMaxPerSecond;
        }

        /// <summary>Snorts of breath from his nostrils past `breathFrom`, faster as the meter fills.</summary>
        void Breathe(float dt)
        {
            float f = Fill01;
            if (f < trait.breathFrom) { _breath = 0f; return; }
            _breath -= dt;
            if (_breath > 0f) return;
            float u = trait.breathFrom < 1f ? (f - trait.breathFrom) / (1f - trait.breathFrom) : 1f;
            _breath = Mathf.Lerp(trait.breathEvery.x, trait.breathEvery.y, u);
            var pop = DeathPopFx.Instance;
            if (pop == null) return;
            var head = Head();
            Vector3 fwd = transform.forward;
            Vector3 at = head != null ? head.position + fwd * 0.3f : transform.position + Vector3.up * 1.4f + fwd * 0.5f;
            pop.Burst(at, 3, 1.2f, 0.07f, trait.breath, Color.white);
        }

        Transform Head()
        {
            if (_headLooked) return _head;
            _headLooked = true;
            var root = _animator != null && _animator.visualRoot != null ? _animator.visualRoot : transform;
            _head = Find(root, "Head");
            return _head;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int c = 0; c < t.childCount; c++)
            {
                var r = Find(t.GetChild(c), name);
                if (r != null) return r;
            }
            return null;
        }

        void TickUnchained(float dt)
        {
            UnchainedLeft -= dt;
            UnchainedTime += dt;
            if (UnchainedLeft <= 0f) { UnchainedLeft = 0f; EnterSpent(); return; }
            Value = trait.meterMax * Unchained01;
            _speck -= dt;
            if (_speck > 0f) return;
            _speck = trait.speckEvery;
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(transform.position + Vector3.up * 1f, 3, 2.2f, 0.08f, trait.speck, trait.hot);
        }

        void TickSpent(float dt)
        {
            SpentLeft -= dt;
            if (SpentLeft > 0f) return;
            SpentLeft = 0f;
            EnterCalm();
        }

        /// <summary>
        /// Unchains him now, whatever the meter (the tick calls it when it's full; tests and admin may call it any time
        /// while he's calm). True if it happened.
        /// </summary>
        public bool Unchain()
        {
            if (trait == null || Current != Mode.Calm || Down) return false;
            Current = Mode.Unchained;
            UnchainedLeft = Mathf.Max(0.01f, trait.unchainedSeconds);
            Value = trait.meterMax;
            Pressing = 0;
            PressRate = 0f;
            Cornered = false;
            _speck = 0f;
            Unchains++;
            if (_stats != null && !_unchainedMods)
            {
                var src = new StatSource(StatSourceKind.Trait, trait);
                _atk = new StatMod(StatId.AttackSpeed, ModOp.Multiply, trait.attackSpeed);
                _move = new StatMod(StatId.MoveSpeed, ModOp.Multiply, trait.moveSpeed);
                _stats.Block.Add(_atk, 1f, src);
                _stats.Block.Add(_move, 1f, src);
                _unchainedMods = true;
            }
            if (_motor != null && !_knockHeld)
            {
                _savedKnock = _motor.KnockbackTakenMul;
                _motor.KnockbackTakenMul = 0f;
                _knockHeld = true;
            }
            if (_weapons != null) _weapons.WhirlSweeps = true; // R3-STUB(chain)
            if (_animator != null)
            {
                _animator.ScaleBoost = trait.scale; // R3-STUB(chain)
                _animator.ExtraLean = trait.lean;   // R3-STUB(chain)
                _posed = true;
            }
            Bellow();
            GameEvents.OnAnnounce.Raise(new AnnounceEvent { Kind = AnnounceKind.Line, Text = trait.announce, Color = trait.hot, Seconds = 1.4f });
            return true;
        }

        /// <summary>Everything within the bellow's radius: damage, knocked away from him.</summary>
        void Bellow()
        {
            Vector3 at = transform.position;
            float area = _stats != null ? _stats.Area : 1f;
            float radius = trait.bellowRadius * Mathf.Sqrt(Mathf.Max(0f, area));
            float dmg = trait.bellowDamage * (_stats != null ? _stats.Damage : 1f);
            float knock = trait.bellowKnockback * (_stats != null ? _stats.Knockback : 1f);
            var em = EnemyManager.Instance;
            if (em != null)
            {
                int n = em.QueryAlive(at, radius, _hits);
                for (int k = 0; k < n; k++)
                {
                    int i = _hits[k];
                    Vector3 to = em.GetPosition(i) - at;
                    to.y = 0f;
                    float d = to.magnitude;
                    em.ApplyDamage(i, new DamageInfo
                    {
                        Amount = dmg, Flags = DamageFlags.Area, Point = em.GetPosition(i),
                        Direction = d > 1e-3f ? to / d : transform.forward, Knockback = knock, SourceId = BellowSourceId,
                    });
                    BellowHits++;
                }
            }
            var pp = PlayerProjectiles.Instance;
            if (pp != null)
            {
                pp.Shockwave(at, radius);
                pp.Shockwave(at, radius * 0.6f);
            }
            var pop = DeathPopFx.Instance;
            if (pop != null) pop.Burst(at + Vector3.up * 0.9f, 30, 8f, 0.18f, trait.speck, trait.hot);
            SfxPlayer.Play(SfxId.BossRoar, at);
            ScreenShake.Add(0.3f);
            if (CrowdExcitement.Instance != null) CrowdExcitement.Instance.Spike(0.4f);
        }

        void EnterSpent()
        {
            ReleaseUnchained();
            Value = 0f;
            if (trait.spentSeconds <= 0f) { EnterCalm(); return; }
            Current = Mode.Spent;
            SpentLeft = trait.spentSeconds;
            if (_stats != null && !_spentMod)
            {
                _tired = new StatMod(StatId.MoveSpeed, ModOp.Multiply, trait.spentMoveSpeed);
                _stats.Block.Add(_tired, 1f, new StatSource(StatSourceKind.Trait, trait));
                _spentMod = true;
            }
            if (_animator != null)
            {
                _animator.HeadDrop = trait.spentHeadDrop; // R3-STUB(chain)
                _posed = true;
            }
        }

        void EnterCalm()
        {
            ReleaseSpent();
            Current = Mode.Calm;
            SpentLeft = 0f;
            UnchainedLeft = 0f;
            _quiet = 0f;
            _breath = 0f;
        }

        void ReleaseUnchained()
        {
            if (_unchainedMods && _stats != null)
            {
                var src = new StatSource(StatSourceKind.Trait, trait);
                _stats.Block.Remove(_atk, 1f, src);
                _stats.Block.Remove(_move, 1f, src);
            }
            _unchainedMods = false;
            if (_knockHeld && _motor != null) _motor.KnockbackTakenMul = _savedKnock;
            _knockHeld = false;
            if (_weapons != null) _weapons.WhirlSweeps = false; // R3-STUB(chain)
            if (_animator != null && _posed)
            {
                _animator.ScaleBoost = 1f; // R3-STUB(chain)
                _animator.ExtraLean = 0f;  // R3-STUB(chain)
            }
        }

        void ReleaseSpent()
        {
            if (_spentMod && _stats != null) _stats.Block.Remove(_tired, 1f, new StatSource(StatSourceKind.Trait, trait));
            _spentMod = false;
            if (_animator != null && _posed)
            {
                _animator.HeadDrop = 0f; // R3-STUB(chain)
                _posed = false;
            }
        }

        /// <summary>
        /// Back to calm with every modifier, flag and pose undone and the meter empty (the player went down, the run
        /// ended, the component went away). Safe to call at any time.
        /// </summary>
        public void Halt()
        {
            if (Current == Mode.Calm && !_unchainedMods && !_spentMod && !_knockHeld && !_posed && Value <= 0f && Pressing == 0) return;
            ReleaseUnchained();
            ReleaseSpent();
            Current = Mode.Calm;
            Value = 0f;
            UnchainedLeft = SpentLeft = 0f;
            Pressing = 0;
            PressRate = 0f;
            Cornered = false;
            _quiet = _breath = _speck = 0f;
        }

        /// <summary>Fills the meter (tests, admin): the next tick Unchains him (or, with triggerOnSlide, the next slide).</summary>
        public void Fill() { if (trait != null && Current == Mode.Calm) Value = trait.meterMax; }

        /// <summary>Sets the meter while calm (tests, admin) and restarts the cooling clock.</summary>
        public void SetRage(float value)
        {
            if (trait == null || Current != Mode.Calm) return;
            Value = Mathf.Clamp(value, 0f, trait.meterMax);
            _quiet = 0f;
        }
    }
}
