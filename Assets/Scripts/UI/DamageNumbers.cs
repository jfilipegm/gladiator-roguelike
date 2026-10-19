using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;

namespace Gladiator.UI
{
    /// <summary>
    /// Pooled floating damage numbers on one overlay canvas (CLAUDE.md §5). Listens to OnHit (white, crit = big gold)
    /// and OnDamaged (orange-red: the player got hurt). Allocation-free: TMP SetText with numeric args.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour, ITickable
    {
        public HitFeedbackConfig feedback;
        public TMP_FontAsset font;
        public Material fontMaterial;
        public int poolSize = 96;
        public float baseFontSize = 34f;

        struct Entry { public RectTransform Rt; public TextMeshProUGUI Text; public Vector3 World; public float Age, Life, Scale; public bool Busy; public Vector3 Drift; }
        Entry[] _pool;
        int _next;
        Canvas _canvas;
        RectTransform _canvasRt;

        void Awake()
        {
            var go = new GameObject("DamageNumbersCanvas", typeof(Canvas));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = UiSortOrder.WorldOverlay;
            _canvasRt = (RectTransform)go.transform;
            _pool = new Entry[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var t = new GameObject("Num" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
                t.transform.SetParent(go.transform, false);
                var txt = t.GetComponent<TextMeshProUGUI>();
                if (font != null) txt.font = font;
                if (fontMaterial != null) txt.fontSharedMaterial = fontMaterial;
                txt.fontSize = baseFontSize;
                txt.fontStyle = FontStyles.Bold;
                txt.alignment = TextAlignmentOptions.Center;
                txt.raycastTarget = false;
                txt.textWrappingMode = TextWrappingModes.NoWrap;
                var rt = (RectTransform)t.transform;
                rt.sizeDelta = new Vector2(200, 60);
                // Pre-size TMP's internal buffers with the longest string we'll show, so SetText never grows them mid-run.
                txt.SetText("88888!");
                txt.ForceMeshUpdate(true, true);
                rt.localScale = Vector3.zero;
                txt.alpha = 0f;
                // Stays active forever: toggling UI objects re-registers them with the canvas (allocates).
                _pool[i] = new Entry { Rt = rt, Text = txt };
            }
        }

        void OnEnable()
        {
            GameLoop.Register(this, TickGroup.Feedback, 50);
            GameEvents.OnHit.Subscribe(OnHit);
            GameEvents.OnDamaged.Subscribe(OnDamaged);
        }

        void OnDisable()
        {
            GameLoop.Unregister(this);
            GameEvents.OnHit.Unsubscribe(OnHit);
            GameEvents.OnDamaged.Unsubscribe(OnDamaged);
        }

        void OnHit(in HitEvent e)
        {
            var c = feedback;
            if (e.Status != StatusKind.None)
            {
                var em = EnemyManager.Instance;
                var def = em != null ? em.StatusDefOf(e.Status) : null;
                Spawn(e.Position, e.Damage, def != null ? def.color : c.numberColor, c.statusNumberScale, false);
                return;
            }
            if (e.Blocked) Spawn(e.Position, e.Damage, c.blockedNumberColor, c.blockedNumberScale, false);
            else Spawn(e.Position, e.Damage, e.Crit ? c.critNumberColor : c.numberColor, e.Crit ? c.critNumberScale : c.numberScale, e.Crit);
        }

        void OnDamaged(in DamagedEvent e) => Spawn(e.Position, e.Damage, feedback.playerDamageColor, feedback.critNumberScale, false);

        void Spawn(Vector3 world, float value, Color color, float scale, bool crit)
        {
            ref var en = ref _pool[_next];
            _next = (_next + 1) % _pool.Length;
            if (en.Busy) DevCounters.DamageNumbers--; // recycled the oldest
            en.Busy = true;
            en.World = world;
            en.Age = 0f;
            en.Life = feedback.numberLifetime * (crit ? 1.25f : 1f);
            en.Scale = scale;
            en.Drift = new Vector3(GameRandom.Range(-0.6f, 0.6f), 0f, GameRandom.Range(-0.6f, 0.6f));
            en.Text.color = color;
            if (crit) en.Text.SetText("{0:0}!", Mathf.Round(value));
            else en.Text.SetText("{0:0}", Mathf.Round(value));
            DevCounters.DamageNumbers++;
        }

        public void Tick(float dt, float unscaledDt)
        {
            var cam = Camera.main;
            if (cam == null) return;
            for (int i = 0; i < _pool.Length; i++)
            {
                ref var en = ref _pool[i];
                if (!en.Busy) continue;
                en.Age += unscaledDt;
                if (en.Age >= en.Life)
                {
                    en.Busy = false;
                    en.Rt.localScale = Vector3.zero;
                    en.Text.alpha = 0f;
                    DevCounters.DamageNumbers--;
                    continue;
                }
                float t = en.Age / en.Life;
                Vector3 w = en.World + Vector3.up * (feedback.numberRise * en.Age) + en.Drift * en.Age;
                Vector3 sp = cam.WorldToScreenPoint(w);
                if (sp.z <= 0f) { en.Rt.localScale = Vector3.zero; continue; }
                en.Rt.position = new Vector3(sp.x, sp.y, 0f);
                // pop in, hold, shrink out
                float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : (t > 0.7f ? Mathf.Lerp(1f, 0.4f, (t - 0.7f) / 0.3f) : 1f);
                float distScale = Mathf.Clamp(12f / Mathf.Max(1f, sp.z), 0.55f, 1.3f);
                en.Rt.localScale = Vector3.one * (en.Scale * pop * distScale);
                var col = en.Text.color;
                col.a = t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f;
                en.Text.color = col;
            }
        }
    }
}
