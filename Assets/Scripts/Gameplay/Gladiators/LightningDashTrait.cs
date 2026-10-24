using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// The Thunder Champion's ability (docs/GLADIATORS.md): starting a slide with the dash ready is a blink up to
    /// `distance` along the slide (CharacterController.Move, so walls stop it); everything within `pathRadius` of the
    /// path takes `damage` × the player's Damage and is Shocked. Then the slide goes on; the dash recharges.
    /// Note 35 (the producer: "sudden, I at first thought it was a bug"): the blink reads as a move without getting any
    /// slower. No wind-up (the slide key blinks this frame, as before); instead a spark burst where he leaves, two jagged
    /// strands of lightning along the path, after-images of his silhouette that appear from the start to the landing and
    /// fade from the start first (BlinkGhosts), a pale shell and ring where he lands, and the camera gliding onto him
    /// (PlayerCameraRig.Glide) instead of cutting. The HUD shows the dash's charge (BlinkReadyIcon).
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/Traits/Lightning Dash", fileName = "TR_LightningDash")]
    public sealed class LightningDashTrait : GladiatorTrait
    {
        public float distance = 7f;
        public float cooldown = 3f;
        public float pathRadius = 1.2f;
        [Tooltip("Damage before the player's Damage multiplier.")]
        public float damage = 25f;
        public float knockback = 3f;
        public StatusDef shock;

        [Header("Look (note 35)")]
        [Tooltip("Seconds the camera glides from where he was onto where he lands (ease-out); 0 = the old cut.")]
        public float cameraGlide = 0.22f;
        [Tooltip("After-images of his silhouette along the path (Gladiator/FxGhost: M_FxBlinkGhost, set by Gladiator/Setup/Gladiators/Blink FX). None = no after-images.")]
        public Material ghostMaterial;
        [Tooltip("Metres between after-images (a 7 m blink leaves 5).")]
        public float ghostSpacing = 1.4f;
        [Tooltip("Seconds the after-image nearest the landing lasts; the one at the start fades in half that.")]
        public float ghostLife = 0.3f;
        [Tooltip("Seconds over which the after-images light up from the start to the landing (the zip).")]
        public float ghostSweep = 0.05f;
        [Tooltip("After-image colour at the start and near the landing (additive: player colours, cyan to white).")]
        public Color ghostColorStart = new Color(0.2f, 0.62f, 1f), ghostColorEnd = new Color(0.78f, 0.96f, 1f);
        [Tooltip("Seconds of the pale shell over his landing spot (0 = none) and how much it swells (0.2 = 20 %).")]
        public float landingFlash = 0.14f;
        public float landingFlashGrow = 0.2f;
        public Color landingFlashColor = new Color(0.92f, 0.98f, 1f);
        [Tooltip("The lightning along the path: two jagged strands, a joint about every this many metres.")]
        public float streakSegment = 1.6f;
        [Tooltip("How far the strands' joints stray from the straight line (m).")]
        public float streakJitter = 0.4f;
        [Tooltip("The pale ring that runs out where he lands (m; 0 = none).")]
        public float landingRing = 1.8f;
        [Tooltip("Sparks where he leaves and where he lands.")]
        public int departureSparks = 8, landingSparks = 14;

        public override void Install(GameObject player)
        {
            var c = player.GetComponent<LightningDash>();
            if (c == null) c = player.AddComponent<LightningDash>();
            c.trait = this;
        }
    }

    /// <summary>Runtime half of LightningDashTrait.</summary>
    public sealed class LightningDash : MonoBehaviour, ITickable
    {
        public static LightningDash Instance { get; private set; }
        public static readonly int SourceId = GameInfo.StableId("lightning_dash");

        public LightningDashTrait trait;

        CharacterController _cc;
        PlayerStats _stats;
        PlayerHealth _health;
        EventHandler<SlideEvent> _onSlide;
        readonly int[] _hits = new int[256];
        BlinkGhosts _ghosts;

        static readonly Color DepartureA = new Color(0.45f, 0.8f, 1f), LandingA = new Color(0.75f, 0.95f, 1f);

        public float CooldownLeft { get; private set; }
        public bool Ready => CooldownLeft <= 0f;
        /// <summary>The dash's charge, 0 just after a blink to 1 when it's ready (the HUD's icon).</summary>
        public float Charge01 => trait == null || trait.cooldown <= 0f ? 1f : Mathf.Clamp01(1f - CooldownLeft / trait.cooldown);
        public int Dashes { get; private set; }
        public int DashHits { get; private set; }
        /// <summary>Where the last blink started and ended (tests).</summary>
        public Vector3 LastFrom { get; private set; }
        public Vector3 LastTo { get; private set; }
        /// <summary>After-images on screen now, and the model parts each is drawn from (tests).</summary>
        public int AfterImages => _ghosts != null ? _ghosts.Alive : 0;
        public int AfterImageParts => _ghosts != null ? _ghosts.Parts : 0;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _stats = GetComponent<PlayerStats>();
            _health = GetComponent<PlayerHealth>();
            _onSlide = OnSlide;
        }

        // the trait is set after AddComponent, so the after-images are made here (their parts gathered once, at spawn)
        void Start() => MakeGhosts();

        void OnEnable()
        {
            Instance = this;
            GameLoop.Register(this, TickGroup.Player, 59);
            GameEvents.OnSlide.Subscribe(_onSlide);
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            GameLoop.Unregister(this);
            GameEvents.OnSlide.Unsubscribe(_onSlide);
            if (_ghosts != null) _ghosts.Clear();
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (CooldownLeft > 0f && dt > 0f) CooldownLeft = Mathf.Max(0f, CooldownLeft - dt);
            if (_ghosts != null) _ghosts.Tick(dt);
        }

        void MakeGhosts()
        {
            if (_ghosts != null || trait == null || trait.ghostMaterial == null) return;
            var anim = GetComponent<PlayerPartAnimator>();
            var model = anim != null && anim.visualRoot != null ? anim.visualRoot : transform;
            _ghosts = new BlinkGhosts(trait.ghostMaterial, model);
        }

        void OnSlide(in SlideEvent e)
        {
            Vector3 dir = e.Velocity;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
            Perform(dir.normalized);
        }

        /// <summary>The blink along `dir`, if the dash is ready. True if it happened.</summary>
        public bool Perform(Vector3 dir)
        {
            if (trait == null || !Ready || _cc == null || (_health != null && _health.IsDead)) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return false;
            dir.Normalize();
            CooldownLeft = trait.cooldown;
            Dashes++;
            Vector3 from = transform.position;
            _cc.Move(dir * trait.distance); // collides: walls and stone stop the blink
            Vector3 to = transform.position;
            LastFrom = from;
            LastTo = to;

            Vector3 path = to - from;
            path.y = 0f;
            float len = path.magnitude;
            Vector3 along = len > 1e-3f ? path / len : dir;
            float dmg = trait.damage * (_stats != null ? _stats.Damage : 1f);
            var em = EnemyManager.Instance;
            if (em != null)
            {
                Vector3 mid = (from + to) * 0.5f;
                int n = em.QueryAlive(mid, len * 0.5f + trait.pathRadius + em.MaxRadius, _hits);
                for (int k = 0; k < n; k++)
                {
                    int i = _hits[k];
                    Vector3 rel = em.GetPosition(i) - from;
                    rel.y = 0f;
                    float t = Mathf.Clamp(Vector3.Dot(rel, along), 0f, len);
                    Vector3 side = rel - along * t;
                    float reach = trait.pathRadius + em.GetRadius(i);
                    if (side.sqrMagnitude > reach * reach) continue;
                    int uid = em.GetUid(i);
                    Vector3 push = side.sqrMagnitude > 1e-4f ? side.normalized : along;
                    em.ApplyDamage(i, new DamageInfo
                    {
                        Amount = dmg, Flags = DamageFlags.Area, Point = em.GetPosition(i), Direction = push,
                        Knockback = trait.knockback, SourceId = SourceId,
                    });
                    DashHits++;
                    if (trait.shock != null && em.IsAlive(i) && em.GetUid(i) == uid) em.ApplyStatus(i, trait.shock, 0f);
                }
            }

            Look(from, to, along, len);
            return true;
        }

        /// <summary>Note 35: what the blink looks like (nothing here changes where he goes or what he hits).</summary>
        void Look(Vector3 from, Vector3 to, Vector3 along, float len)
        {
            var t = trait;
            var rig = PlayerCameraRig.Main;
            if (rig != null && t.cameraGlide > 0f) rig.Glide(from - to, t.cameraGlide); // the camera starts where he was
            if (_ghosts == null) MakeGhosts();
            if (_ghosts != null) _ghosts.Spawn(from, to, t);

            var pp = PlayerProjectiles.Instance;
            if (pp != null)
            {
                Strand(pp, from, to, along, len, 0.95f, t.streakJitter, 0);
                Strand(pp, from, to, along, len, 0.55f, t.streakJitter * 1.4f, 1);
                if (t.landingRing > 0f) pp.Shockwave(to, t.landingRing);
            }
            var pop = DeathPopFx.Instance;
            if (pop != null)
            {
                if (t.departureSparks > 0) pop.Burst(from + Vector3.up * 0.9f, t.departureSparks, 3f, 0.1f, DepartureA, Color.white);
                if (t.landingSparks > 0) pop.Burst(to + Vector3.up * 0.9f, t.landingSparks, 5f, 0.12f, LandingA, Color.white);
            }
            SfxPlayer.Play(SfxId.Spark, to);
            ScreenShake.Add(0.08f);
        }

        /// <summary>
        /// One jagged strand of lightning from `from` to `to` at `height`: a joint about every streakSegment metres, each
        /// pushed sideways and up by a fixed hash of the blink's number (no game RNG drawn), so it never reads as a beam.
        /// </summary>
        void Strand(PlayerProjectiles pp, Vector3 from, Vector3 to, Vector3 along, float len, float height, float jitter, int strand)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(0.3f, trait.streakSegment)), 1, 6);
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Vector3 up = Vector3.up * height;
            Vector3 prev = from + up;
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, (float)i / n) + up;
                if (i < n) p += side * (jitter * Hash(Dashes, strand, i)) + Vector3.up * (jitter * 0.5f * Hash(Dashes, strand, i + 31));
                pp.Bolt(prev, p);
                prev = p;
            }
        }

        /// <summary>A repeatable -1..1 from three integers.</summary>
        static float Hash(int a, int b, int c)
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0xffffu) / 32767.5f - 1f;
        }

        /// <summary>Makes the dash ready now (tests, admin).</summary>
        public void Recharge() => CooldownLeft = 0f;
    }
}
