using Gladiator.Core;
using Gladiator.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Arrows at the screen edge pointing at off-screen threats (concern C2: with a free third-person camera you can't
    /// see what's behind you). Orange-red pulsing = an enemy is winding up an attack; affix colour = an elite.
    /// Reads EnemyManager's threat list each frame. Pooled images that never toggle (no UI rebuild allocations).
    /// </summary>
    public sealed class ThreatIndicators : MonoBehaviour, ITickable
    {
        public int poolSize = 16;
        [Tooltip("Distance from the screen edge (canvas px at 1080p).")]
        public float edgeMargin = 56f;
        public float size = 46f;
        [Tooltip("Threats further than this from the player are ignored (m).")]
        public float maxDistance = 30f;
        [Tooltip("Inset of the 'on screen' test, as a viewport fraction.")]
        public float onScreenInset = 0.04f;

        Image[] _icons;
        RectTransform[] _rts;
        bool[] _visible;

        public int VisibleCount { get; private set; }

        void Awake()
        {
            var canvas = UiFactory.Canvas("ThreatCanvas", transform, UiSortOrder.WorldOverlay); // positions in screen px, sizes scale with the canvas
            _icons = new Image[poolSize];
            _rts = new RectTransform[poolSize];
            _visible = new bool[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var img = UiFactory.Image("Threat" + i, canvas.transform, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size * 0.8f), PixelIcons.Chevron);
                img.raycastTarget = false;
                _icons[i] = img;
                _rts[i] = img.rectTransform;
                _rts[i].localScale = Vector3.zero; // stays active forever: toggling UI objects allocates
            }
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 60);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            var em = EnemyManager.Instance;
            var cam = Camera.main;
            int used = 0;
            if (em != null && cam != null && Player.Main != null)
            {
                Vector3 player = Player.Main.transform.position;
                float w = Screen.width, h = Screen.height;
                float max2 = maxDistance * maxDistance;
                float pulse = 0.85f + 0.25f * Mathf.Sin(Time.unscaledTime * 18f);
                // wind-ups first: they're about to hurt
                for (int pass = 0; pass < 2 && used < poolSize; pass++)
                {
                    for (int k = 0; k < em.ThreatCount && used < poolSize; k++)
                    {
                        var t = em.GetThreat(k);
                        if (t.Windup != (pass == 0)) continue;
                        Vector3 d = t.Position - player;
                        if (d.x * d.x + d.z * d.z > max2) continue;
                        Vector3 vp = cam.WorldToViewportPoint(t.Position + Vector3.up);
                        bool behind = vp.z < 0f;
                        if (!behind && vp.x > onScreenInset && vp.x < 1f - onScreenInset && vp.y > onScreenInset && vp.y < 1f - onScreenInset) continue;
                        var dir = new Vector2((vp.x - 0.5f) * w, (vp.y - 0.5f) * h);
                        if (behind) dir = -dir;
                        if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                        float margin = edgeMargin * (h / 1080f);
                        float sx = Mathf.Abs(dir.x) > 1e-3f ? (w * 0.5f - margin) / Mathf.Abs(dir.x) : float.MaxValue;
                        float sy = Mathf.Abs(dir.y) > 1e-3f ? (h * 0.5f - margin) / Mathf.Abs(dir.y) : float.MaxValue;
                        Vector2 pos = new Vector2(w * 0.5f, h * 0.5f) + dir * Mathf.Min(sx, sy);
                        var rt = _rts[used];
                        rt.position = new Vector3(pos.x, pos.y, 0f);
                        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                        rt.localScale = Vector3.one * (t.Windup ? pulse * 1.15f : 0.85f);
                        var c = t.Color;
                        c.a = 1f;
                        if (_icons[used].color != c) _icons[used].color = c;
                        _visible[used] = true;
                        used++;
                    }
                }
            }
            VisibleCount = used;
            for (int i = used; i < poolSize; i++)
            {
                if (!_visible[i]) continue;
                _visible[i] = false;
                _rts[i].localScale = Vector3.zero;
            }
        }
    }
}
