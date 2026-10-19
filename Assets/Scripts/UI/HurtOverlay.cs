using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Screen-edge feedback for the player's health (Phase 4 juice): an orange-red edge flash when hit (the "this hurts"
    /// colour), and a slow pulse while HP is low. One full-screen image with a procedural edge-only texture; the centre
    /// of the screen is never covered. Numbers in HitFeedbackConfig.
    /// </summary>
    public sealed class HurtOverlay : MonoBehaviour, ITickable
    {
        public HitFeedbackConfig feedback;

        Image _edge;
        float _flash;
        PlayerHealth _health;

        void Awake()
        {
            var canvas = UiFactory.Canvas("HurtCanvas", transform, UiSortOrder.Menus - 5).transform;
            _edge = UiFactory.Image("Edge", canvas, new Color(1f, 0.25f, 0.12f, 0f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, EdgeSprite());
            UiFactory.Stretch(_edge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _edge.raycastTarget = false;
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Feedback, 40);
            GameEvents.OnDamaged.Subscribe(OnDamaged);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
        }

        void OnDamaged(in DamagedEvent e)
        {
            if (feedback == null) return;
            float share = e.HealthMax > 0f ? Mathf.Clamp01(e.Damage / e.HealthMax * 4f) : 1f; // big hits flash harder
            _flash = Mathf.Max(_flash, feedback.hurtFlashAlpha * Mathf.Lerp(0.6f, 1f, share));
        }

        public void Tick(float dt, float unscaledDt)
        {
            if (feedback == null) return;
            _flash = Mathf.MoveTowards(_flash, 0f, unscaledDt * feedback.hurtFlashAlpha / Mathf.Max(0.05f, feedback.hurtFlashTime));
            if (_health == null && Player.Main != null) _health = Player.Main.GetComponent<PlayerHealth>();
            float low = 0f;
            if (_health != null && !_health.IsDead && _health.Hp01 < feedback.lowHpThreshold)
            {
                float depth = 1f - _health.Hp01 / Mathf.Max(0.01f, feedback.lowHpThreshold);
                low = feedback.lowHpPulseAlpha * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * feedback.lowHpPulseRate)) * Mathf.Lerp(0.5f, 1f, depth);
            }
            var c = _edge.color;
            float a = Mathf.Max(_flash, low);
            if (!Mathf.Approximately(c.a, a)) { c.a = a; _edge.color = c; }
        }

        /// <summary>White at the screen edges fading to clear well before the centre (alpha only).</summary>
        static Sprite EdgeSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) * 0.6f + new Vector2(u, v).magnitude * 0.4f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f, 1.15f, d));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
