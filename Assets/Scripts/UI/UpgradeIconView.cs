using Gladiator.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// An upgrade's animated 8-bit icon over its rarity aura (playtest: cards and the HUD showed letters). The atlas
    /// (PixelSrc/upgrade_icons.py) has 20 px cells, one row per icon: columns 0-3 are the icon with a light band
    /// sweeping across it, columns 4-7 a white aura mask tinted here with the rarity colour. The icon bobs one art pixel,
    /// shines every few seconds, and the aura flickers. A plain class driven by its panel's tick; it only touches the
    /// RawImages when a frame changes (no per-frame canvas rebuilds, no allocation).
    /// </summary>
    public sealed class UpgradeIconView
    {
        public const int Cell = 20, IconFrames = 4, AuraFrames = 4;

        readonly RawImage _icon, _aura;
        readonly RectTransform _iconRt;
        readonly float _px;
        readonly bool _lively;
        Texture2D _tex;
        int _row = -1, _iconFrame = -1, _auraFrame = -1, _bob = -1;
        float _phase;

        public RectTransform Root { get; }
        /// <summary>Tests: is an icon showing, and which atlas row.</summary>
        public bool Showing => _tex != null && _icon.enabled;
        public int Row => _row;
        public int IconFrame => _iconFrame;
        public int AuraFrame => _auraFrame;

        /// <param name="size">Edge of the square (a 20 px cell scaled up; keep it a multiple of 20 for crisp pixels).</param>
        /// <param name="lively">Cards: bob, shine often, flicker the aura. HUD: a rare shine, a still aura.</param>
        public UpgradeIconView(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, bool lively)
        {
            _lively = lively;
            _px = size / Cell;
            Root = UiFactory.Rect(name, parent, anchor, anchor, pos, new Vector2(size, size));
            _aura = Raw("Aura", Root, size);
            _icon = Raw("Icon", Root, size);
            _iconRt = _icon.rectTransform;
            Clear();
        }

        static RawImage Raw(string name, Transform parent, float size)
        {
            var rt = UiFactory.Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Shows `def`'s icon with a `rarity` aura (false if it has none: the caller keeps its letters).
        /// `phase` (0..1) staggers the shine so a row of icons doesn't flash in step.</summary>
        public bool Set(UpgradeDef def, Color rarity, float phase = 0f) =>
            def != null ? Set(def.icon, def.iconRow, rarity, phase) : SetNone();

        /// <summary>Any atlas row (tag synergies use the same atlas, Phase 5.5).</summary>
        public bool Set(Texture2D atlas, int row, Color rarity, float phase = 0f)
        {
            if (atlas == null || row < 0) return SetNone();
            _phase = phase;
            if (_tex != atlas || _row != row)
            {
                _tex = atlas;
                _row = row;
                _icon.texture = _tex;
                _aura.texture = _tex;
                _iconFrame = _auraFrame = -1;
            }
            _icon.enabled = _aura.enabled = true;
            _aura.color = new Color(rarity.r, rarity.g, rarity.b, _lively ? 1f : 0.75f);
            Tick(Time.unscaledTime);
            return true;
        }

        bool SetNone() { Clear(); return false; }

        public void Clear()
        {
            _tex = null;
            _row = -1;
            _icon.enabled = _aura.enabled = false;
        }

        public void Tick(float time)
        {
            if (_tex == null) return;
            // shine: three quick frames, then a rest (cards every 2.4 s, the HUD every 7 s)
            float period = _lively ? 2.4f : 7f;
            float t = Mathf.Repeat(time / period + _phase, 1f) * period;
            int iconFrame = t < 0.27f ? 1 + Mathf.Min(2, (int)(t / 0.09f)) : 0;
            int auraFrame = _lively ? (int)(time * 6f) % AuraFrames : 0;
            int bob = _lively ? (int)(time * 2.2f + _phase * 2f) & 1 : 0;
            if (iconFrame != _iconFrame) { _iconFrame = iconFrame; _icon.uvRect = CellRect(iconFrame); }
            if (auraFrame != _auraFrame) { _auraFrame = auraFrame; _aura.uvRect = CellRect(IconFrames + auraFrame); }
            if (bob != _bob) { _bob = bob; _iconRt.anchoredPosition = new Vector2(0f, bob * _px); }
        }

        Rect CellRect(int column)
        {
            float w = Cell / (float)_tex.width, h = Cell / (float)_tex.height;
            return new Rect(column * w, 1f - (_row + 1) * h, w, h); // texture rows run bottom-up
        }
    }
}
