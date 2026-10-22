using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    public enum BannerCloth : byte { Crimson, Purple }

    public enum BannerPhase : byte { Hidden, Dropping, Settling, Holding, Rolling }

    /// <summary>
    /// U1: the announcer's vexillum (docs/tracks/U1-design.md §7): a bronze crossbar with gold finials and a cloth that
    /// drops from behind it like a curtain, settles, sways once, holds and rolls back up. A plain class ticked by its
    /// owner (like UpgradeIconView): the HUD passes scaled dt, the end screen unscaled. It lives on its own nested Canvas
    /// (inherits sorting, raycasts off) so its motion re-batches only itself; the cloth moves by anchoredPosition.y and
    /// localRotation only, and its words and sprite change only when a banner starts. Tick allocates nothing.
    ///
    /// Timeline: Dropping (bannerDrop, −104 % → +overshoot, ease-out) → Settling (bannerSettle, → −0.4 × overshoot → 0)
    /// → Holding (max(bannerHold, seconds); the sway plays at its start: +deg, −0.64 deg, +0.29 deg, 0 over bannerSway)
    /// → Rolling (bannerRoll, ease-in) → Hidden. Show while visible rolls the current one up in bannerQuickRoll (old
    /// words), then drops the new one.
    /// </summary>
    public sealed class VexillumBanner
    {
        public const float DefaultWidth = 760f;
        const float BehindBar = 1.04f; // Down01 = 0 hides the cloth this share of its height above the bar's centre line
        static readonly float[] SwayKeys = { 0f, 1f, -0.64f, 0.29f, 0f };

        readonly Canvas _canvas;
        readonly CanvasGroup _group;
        readonly RectTransform _cloth;
        readonly Image _clothImage, _bar;
        readonly TextMeshProUGUI _words;
        readonly float _clothH;
        readonly Sprite _crimsonSprite, _purpleSprite;

        BannerPhase _phase;
        float _t, _drop, _settle, _sway, _hold, _rollDur, _swayDeg, _overshoot, _quickRoll, _roll;
        float _rollFrom, _rollAngle, _down, _angle, _appliedY = float.NaN, _appliedAngle = float.NaN, _appliedAlpha = -1f;
        bool _held;

        bool _hasNext, _nextHeld;
        string _nextText;
        BannerCloth _nextCloth;
        float _nextSeconds;

        /// <summary>The words' colour on each cloth (goldBright / purpleInk by default; the end screen may change them).</summary>
        public Color CrimsonInk, PurpleInk;

        public RectTransform Root { get; }
        public BannerPhase Phase => _phase;
        public bool Visible => _phase != BannerPhase.Hidden;
        public BannerCloth Cloth { get; private set; }
        public string Text { get; private set; }
        /// <summary>0 = fully behind the bar (−104 %), 1 = resting (1 + overshoot at the drop's end).</summary>
        public float Down01 => _down;
        public float SwayDegrees => _angle;
        public bool Held => _held;
        /// <summary>A banner waits for the current one's quick roll-up.</summary>
        public bool Replacing => _hasNext;
        /// <summary>The words (Shout, Outline, autosized 36–56): restyle them for a bigger banner (YOU DIED).</summary>
        public TextMeshProUGUI Words => _words;
        public Image ClothImage => _clothImage;
        public Image Bar => _bar;

        public VexillumBanner(Transform parent, Vector2 anchor, Vector2 pos, float width = DefaultWidth)
            : this(parent, anchor, pos, width, 0f) { }

        /// <param name="height">The cloth's height (0 = its native 120): taller for bigger words.</param>
        public VexillumBanner(Transform parent, Vector2 anchor, Vector2 pos, float width, float height)
        {
            var theme = UiTheme.Current;
            CrimsonInk = theme.goldBright;
            PurpleInk = theme.purpleInk;
            var top = new Vector2(0.5f, 1f);
            var mid = new Vector2(0.5f, 0.5f);
            _clothH = height > 0f ? height : UiFrames.NativeSize(FrameKind.VexillumCrimson).y;
            float barH = UiFrames.NativeSize(FrameKind.VexillumBar).y;

            Root = UiFactory.Rect("Banner", parent, anchor, top, pos, new Vector2(width + 60f, barH * 0.5f + _clothH * 1.3f));
            _canvas = Root.gameObject.AddComponent<Canvas>(); // nested: inherits the parent's sorting
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // the slot: a mask whose top is the bar's centre line, wide and deep enough for the overshoot and the sway
            var slot = UiFactory.Rect("Slot", Root, top, top, new Vector2(0f, -barH * 0.5f), new Vector2(width + 80f, _clothH * 1.3f));
            slot.gameObject.AddComponent<RectMask2D>();

            _crimsonSprite = UiFrames.Get(FrameKind.VexillumCrimson);
            _purpleSprite = UiFrames.Get(FrameKind.VexillumPurple);
            _clothImage = UiKit.Frame("Cloth", slot, FrameKind.VexillumCrimson, top, top, Vector2.zero, new Vector2(width, _clothH), Color.white);
            _cloth = _clothImage.rectTransform;

            var pad = UiFrames.Padding(FrameKind.VexillumCrimson);
            var box = new Vector2(width - pad.x - pad.z, _clothH - pad.y - pad.w);
            _words = UiKit.Text("Words", _cloth, "", TextRole.Shout, 56f, CrimsonInk, TextAlignmentOptions.Center, top, top,
                new Vector2((pad.x - pad.z) * 0.5f, -pad.w), box, TextLook.Outline);
            _words.enableAutoSizing = true;
            _words.fontSizeMin = 36f;
            _words.fontSizeMax = 56f;
            // pre-size TMP's buffers for the longest line a banner shows, so no Show grows them in a run
            _words.text = "GORM, THE PIT MASTER RAGES! THE CROWD DEMANDS BLOOD!";
            _words.ForceMeshUpdate();
            _words.text = "";

            _bar = UiKit.Frame("Bar", Root, FrameKind.VexillumBar, top, top, Vector2.zero, new Vector2(width + 60f, barH), Color.white);

            Text = "";
            Cloth = BannerCloth.Crimson;
            _phase = BannerPhase.Hidden;
            _down = 0f;
            Apply();
        }

        /// <summary>Total length of one banner from drop to hidden, with the theme's timings.</summary>
        public static float TotalSeconds(float seconds = 0f)
        {
            var t = UiTheme.Current;
            return t.bannerDrop + t.bannerSettle + Mathf.Max(t.bannerHold, seconds) + t.bannerRoll;
        }

        /// <summary>Drops `text` on the cloth for max(bannerHold, seconds). While one is visible, it rolls up quickly
        /// (old words) and this one drops after. Pass constant strings (no allocation in a run).</summary>
        public void Show(string text, BannerCloth cloth, float seconds = 0f) => Begin(text, cloth, seconds, false);

        /// <summary>Drops, sways and stays until RollUp or Clear (the end screen).</summary>
        public void Hold(string text, BannerCloth cloth) => Begin(text, cloth, 0f, true);

        public void RollUp(bool quick = false)
        {
            if (_phase == BannerPhase.Hidden) return;
            _held = false;
            StartRoll(quick);
        }

        /// <summary>Hidden at once (nothing pending).</summary>
        public void Clear()
        {
            _phase = BannerPhase.Hidden;
            _held = false;
            _hasNext = false;
            _nextText = null;
            _t = 0f;
            _down = 0f;
            _angle = 0f;
            Apply();
        }

        void Begin(string text, BannerCloth cloth, float seconds, bool held)
        {
            if (_phase == BannerPhase.Hidden)
            {
                Drop(text, cloth, seconds, held);
                return;
            }
            _hasNext = true;
            _nextText = text;
            _nextCloth = cloth;
            _nextSeconds = seconds;
            _nextHeld = held;
            _held = false;
            StartRoll(true);
        }

        void Drop(string text, BannerCloth cloth, float seconds, bool held)
        {
            var theme = UiTheme.Current;
            _drop = Mathf.Max(0f, theme.bannerDrop);
            _settle = Mathf.Max(0f, theme.bannerSettle);
            _sway = Mathf.Max(0f, theme.bannerSway);
            _hold = Mathf.Max(theme.bannerHold, seconds);
            _roll = Mathf.Max(0f, theme.bannerRoll);
            _quickRoll = Mathf.Max(0f, theme.bannerQuickRoll);
            _swayDeg = theme.bannerSwayDegrees;
            _overshoot = theme.bannerOvershoot;

            if (cloth != Cloth || _clothImage.sprite != (cloth == BannerCloth.Purple ? _purpleSprite : _crimsonSprite))
                _clothImage.sprite = cloth == BannerCloth.Purple ? _purpleSprite : _crimsonSprite;
            Cloth = cloth;
            var ink = cloth == BannerCloth.Purple ? PurpleInk : CrimsonInk;
            if (_words.color != ink) _words.color = ink;
            if (!ReferenceEquals(Text, text))
            {
                Text = text ?? "";
                _words.text = Text;
            }
            _held = held;
            _phase = BannerPhase.Dropping;
            _t = 0f;
            _down = 0f;
            _angle = 0f;
            Apply();
        }

        void StartRoll(bool quick)
        {
            _rollFrom = _down;
            _rollAngle = _angle;
            _rollDur = quick ? _quickRoll : _roll;
            _phase = BannerPhase.Rolling;
            _t = 0f;
        }

        float PhaseLength()
        {
            switch (_phase)
            {
                case BannerPhase.Dropping: return _drop;
                case BannerPhase.Settling: return _settle;
                case BannerPhase.Holding: return _hold;
                case BannerPhase.Rolling: return _rollDur;
                default: return 0f;
            }
        }

        public void Tick(float dt)
        {
            if (_phase == BannerPhase.Hidden) return;
            _t += dt;
            for (int guard = 0; guard < 8; guard++)
            {
                if (_phase == BannerPhase.Hidden || (_phase == BannerPhase.Holding && _held)) break;
                float len = PhaseLength();
                if (_t < len) break;
                _t -= len;
                switch (_phase)
                {
                    case BannerPhase.Dropping: _phase = BannerPhase.Settling; break;
                    case BannerPhase.Settling: _phase = BannerPhase.Holding; break;
                    case BannerPhase.Holding:
                        _down = 1f;
                        _angle = 0f;
                        StartRoll(false);
                        break;
                    case BannerPhase.Rolling:
                        if (_hasNext)
                        {
                            _hasNext = false;
                            float carry = _t;
                            Drop(_nextText, _nextCloth, _nextSeconds, _nextHeld);
                            _nextText = null;
                            _t = carry;
                        }
                        else
                        {
                            _phase = BannerPhase.Hidden;
                            _t = 0f;
                        }
                        break;
                }
            }
            Evaluate();
            Apply();
        }

        void Evaluate()
        {
            float p;
            switch (_phase)
            {
                case BannerPhase.Dropping:
                    p = _drop > 0f ? Mathf.Clamp01(_t / _drop) : 1f;
                    float e = 1f - (1f - p) * (1f - p) * (1f - p); // ease-out
                    _down = (1f + _overshoot) * e;
                    _angle = 0f;
                    break;
                case BannerPhase.Settling:
                    p = _settle > 0f ? Mathf.Clamp01(_t / _settle) : 1f;
                    float bounce = 1f - 0.4f * _overshoot;
                    _down = p < 0.5f
                        ? Mathf.Lerp(1f + _overshoot, bounce, Mathf.SmoothStep(0f, 1f, p * 2f))
                        : Mathf.Lerp(bounce, 1f, Mathf.SmoothStep(0f, 1f, p * 2f - 1f));
                    _angle = 0f;
                    break;
                case BannerPhase.Holding:
                    _down = 1f;
                    if (_t < _sway && _sway > 0f)
                    {
                        float s = _t / _sway * (SwayKeys.Length - 1);
                        int i = Mathf.Min(SwayKeys.Length - 2, (int)s);
                        _angle = _swayDeg * Mathf.Lerp(SwayKeys[i], SwayKeys[i + 1], Mathf.SmoothStep(0f, 1f, s - i));
                    }
                    else _angle = 0f;
                    break;
                case BannerPhase.Rolling:
                    p = _rollDur > 0f ? Mathf.Clamp01(_t / _rollDur) : 1f;
                    _down = _rollFrom * (1f - p * p); // ease-in
                    _angle = _rollAngle * (1f - p);
                    break;
                default:
                    _down = 0f;
                    _angle = 0f;
                    break;
            }
        }

        void Apply()
        {
            float y = (1f - _down) * BehindBar * _clothH;
            if (y != _appliedY)
            {
                _appliedY = y;
                _cloth.anchoredPosition = new Vector2(0f, y);
            }
            if (_angle != _appliedAngle)
            {
                _appliedAngle = _angle;
                _cloth.localRotation = Quaternion.Euler(0f, 0f, _angle);
            }
            // the crossbar fades in over the drop's first quarter and out over the roll's last
            float a = _phase == BannerPhase.Hidden ? 0f : Mathf.Clamp01(_down * 4f);
            if (a != _appliedAlpha)
            {
                _appliedAlpha = a;
                _group.alpha = a;
            }
        }
    }
}
