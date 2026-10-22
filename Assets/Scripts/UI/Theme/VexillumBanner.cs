using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    public enum BannerCloth : byte { Crimson, Purple }

    public enum BannerPhase : byte { Hidden, Dropping, Settling, Holding, Rolling }

    /// <summary>
    /// U1: the announcer's vexillum (docs/tracks/U1-design.md §7): a bronze crossbar with gold finials and a cloth that
    /// drops from behind it like a curtain, settles, holds and rolls back up. A plain class ticked by its owner (like
    /// UpgradeIconView): the HUD passes scaled dt, the end screen unscaled. It lives on its own nested Canvas (inherits
    /// sorting, raycasts off) so its motion re-batches only itself; the cloth moves by anchoredPosition.y, and its words
    /// and sprite change only when a banner starts.
    /// P3 (the producer's note 16): no rotation and no sideways sway any more. While the cloth drops a wave travels
    /// across it, A·env(t)·sin(2π(x/λ − speed·t)) with the theme's bannerWave fields (A a share of the cloth's height, λ
    /// of its width): ClothWave bends the cloth's mesh in columns, and the words ride the same function at their
    /// characters' x (their TMP vertices moved from a copy taken when the words changed, then UpdateVertexData: no
    /// ForceMeshUpdate per frame). env is 1 through the drop, then falls to a third every bannerWaveDecay seconds and is
    /// cut to 0 by 3 × decay after the drop (1.35 s into the 2 s hold with the defaults), so the cloth hangs still
    /// before it rolls up. Tick allocates nothing.
    ///
    /// Timeline: Dropping (bannerDrop, −104 % → +overshoot, ease-out) → Settling (bannerSettle, → −0.4 × overshoot → 0)
    /// → Holding (max(bannerHold, seconds)) → Rolling (bannerRoll, ease-in) → Hidden. Show while visible rolls the
    /// current one up in bannerQuickRoll (old words), then drops the new one.
    /// </summary>
    public sealed class VexillumBanner
    {
        public const float DefaultWidth = 760f;
        const float BehindBar = 1.04f; // Down01 = 0 hides the cloth this share of its height above the bar's centre line
        const float Ln3 = 1.0986123f;

        readonly Canvas _canvas;
        readonly CanvasGroup _group;
        readonly RectTransform _cloth;
        readonly Image _clothImage, _bar;
        readonly TextMeshProUGUI _words;
        readonly float _clothH, _clothW;
        readonly Sprite _crimsonSprite, _purpleSprite;
        readonly ClothWave _wave;

        BannerPhase _phase;
        float _t, _drop, _settle, _hold, _rollDur, _overshoot, _quickRoll, _roll;
        float _rollFrom, _down, _appliedY = float.NaN, _appliedAlpha = -1f;
        bool _held;
        // the wave (P3): its time since the drop began, phase (cycles), strength, the theme's shape, what was applied
        float _waveT, _wavePhase, _env, _waveAmp01, _waveLen01, _waveSpeed, _waveDecay, _appliedAmp;
        // the words' vertices as TMP made them (per mesh), so each tick's offsets start from the flat text
        Vector3[][] _wordBase = new Vector3[2][];
        int[] _wordCount = new int[2];

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
        /// <summary>Always 0 since P3 (no rotation: the cloth waves instead).</summary>
        public float SwayDegrees => 0f;
        /// <summary>P3: the wave's strength now (0..1, WaveEnvelope), its height and length in canvas units as applied.</summary>
        public float WaveEnvelope01 => _env;
        public float WaveAmplitude => _appliedAmp;
        public float WaveLength => _waveLen01 * _clothW;
        public float WavePhase => _wavePhase;
        /// <summary>The cloth's width (the wave's x runs 0..width from its left edge).</summary>
        public float ClothWidth => _clothW;
        public ClothWave Wave => _wave;
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
            _clothW = width;
            _wave = _clothImage.gameObject.AddComponent<ClothWave>();

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
            CaptureWords(); // sizes the copies for the longest words
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
            Apply();
            ApplyWave();
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
            _hold = Mathf.Max(theme.bannerHold, seconds);
            _roll = Mathf.Max(0f, theme.bannerRoll);
            _quickRoll = Mathf.Max(0f, theme.bannerQuickRoll);
            _overshoot = theme.bannerOvershoot;
            _waveAmp01 = Mathf.Max(0f, theme.bannerWaveAmplitude);
            _waveLen01 = Mathf.Max(0.01f, theme.bannerWaveLength);
            _waveSpeed = theme.bannerWaveSpeed;
            _waveDecay = Mathf.Max(0f, theme.bannerWaveDecay);
            _waveT = 0f;
            _wavePhase = 0f;

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
            Apply();
        }

        void StartRoll(bool quick)
        {
            _rollFrom = _down;
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
            _waveT += dt;
            _wavePhase += _waveSpeed * dt;
            if (_wavePhase > 1000f) _wavePhase -= 1000f; // whole cycles: the same wave, a small number
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
            ApplyWave();
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
                    break;
                case BannerPhase.Settling:
                    p = _settle > 0f ? Mathf.Clamp01(_t / _settle) : 1f;
                    float bounce = 1f - 0.4f * _overshoot;
                    _down = p < 0.5f
                        ? Mathf.Lerp(1f + _overshoot, bounce, Mathf.SmoothStep(0f, 1f, p * 2f))
                        : Mathf.Lerp(bounce, 1f, Mathf.SmoothStep(0f, 1f, p * 2f - 1f));
                    break;
                case BannerPhase.Holding:
                    _down = 1f;
                    break;
                case BannerPhase.Rolling:
                    p = _rollDur > 0f ? Mathf.Clamp01(_t / _rollDur) : 1f;
                    _down = _rollFrom * (1f - p * p); // ease-in
                    break;
                default:
                    _down = 0f;
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
            // the crossbar fades in over the drop's first quarter and out over the roll's last
            float a = _phase == BannerPhase.Hidden ? 0f : Mathf.Clamp01(_down * 4f);
            if (a != _appliedAlpha)
            {
                _appliedAlpha = a;
                _group.alpha = a;
            }
        }

        // ---------- the wave (P3) ----------

        /// <summary>
        /// The wave's strength (0..1) `sinceDrop` seconds after the drop began: 1 while the cloth drops; then it falls to a
        /// third every `decay` seconds and is cut smoothly to 0 between 2 and 3 × decay after the drop's end (0 at once with
        /// decay 0). 0 when hidden.
        /// </summary>
        public static float WaveEnvelope(BannerPhase phase, float sinceDrop, float drop, float decay)
        {
            if (phase == BannerPhase.Hidden) return 0f;
            if (phase == BannerPhase.Dropping) return 1f;
            float u = sinceDrop - drop;
            if (u <= 0f) return 1f;
            if (decay <= 0f || u >= 3f * decay) return 0f;
            float cut = u > 2f * decay ? 1f - Mathf.SmoothStep(0f, 1f, (u - 2f * decay) / decay) : 1f;
            return Mathf.Exp(-Ln3 * u / decay) * cut;
        }

        /// <summary>The wave's height in canvas units at `x` units from the cloth's left edge (the cloth and its words).</summary>
        public float WaveOffsetAt(float x) => ClothWave.Offset(x, _appliedAmp, WaveLength, _wavePhase);

        /// <summary>Bends the cloth and its words for this tick (only while there is a wave, and once more to flatten them).</summary>
        void ApplyWave()
        {
            _env = WaveEnvelope(_phase, _waveT, _drop, _waveDecay);
            float amp = _waveAmp01 * _clothH * _env;
            if (amp == 0f && _appliedAmp == 0f) return;
            _appliedAmp = amp;
            float length = WaveLength;
            _wave.Amplitude = amp;
            _wave.Length = length;
            _wave.Phase = _wavePhase;
            _clothImage.SetVerticesDirty();
            WaveWords(amp, length, _wavePhase);
        }

        /// <summary>The words ride the wave: each character's four vertices move by the offset at its centre's x.</summary>
        void WaveWords(float amp, float length, float phase)
        {
            if (_words.havePropertiesChanged)
            {
                _words.ForceMeshUpdate(); // only when the words changed (a new banner, YOU DIED's restyle), never per frame
                CaptureWords();
            }
            var info = _words.textInfo;
            if (info == null || info.meshInfo == null) return;
            float fromLeft = _words.rectTransform.anchoredPosition.x + _clothW * 0.5f; // the words' x 0 on the cloth
            int meshes = Mathf.Min(info.materialCount, Mathf.Min(info.meshInfo.Length, _wordBase.Length));
            for (int k = 0; k < meshes; k++)
            {
                var verts = info.meshInfo[k].vertices;
                var src = _wordBase[k];
                if (verts == null || src == null) continue;
                int n = Mathf.Min(_wordCount[k], Mathf.Min(verts.Length, src.Length));
                for (int i = 0; i + 3 < n; i += 4)
                {
                    float cx = (src[i].x + src[i + 2].x) * 0.5f;
                    float dy = amp == 0f ? 0f : ClothWave.Offset(fromLeft + cx, amp, length, phase);
                    for (int j = 0; j < 4; j++)
                    {
                        var v = src[i + j];
                        v.y += dy;
                        verts[i + j] = v;
                    }
                }
            }
            _words.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        /// <summary>Keeps a copy of the words' vertices as TMP laid them out (grows the copies only for longer words).</summary>
        void CaptureWords()
        {
            var info = _words.textInfo;
            if (info == null || info.meshInfo == null) return;
            int meshes = Mathf.Min(info.materialCount, info.meshInfo.Length);
            if (meshes > _wordBase.Length)
            {
                System.Array.Resize(ref _wordBase, meshes);
                System.Array.Resize(ref _wordCount, meshes);
            }
            for (int k = 0; k < _wordBase.Length; k++)
            {
                if (k >= meshes) { _wordCount[k] = 0; continue; }
                var verts = info.meshInfo[k].vertices;
                int n = verts != null ? Mathf.Min(info.meshInfo[k].vertexCount, verts.Length) : 0;
                if (verts != null && (_wordBase[k] == null || _wordBase[k].Length < verts.Length)) _wordBase[k] = new Vector3[verts.Length];
                if (n > 0) System.Array.Copy(verts, _wordBase[k], n);
                _wordCount[k] = n;
            }
        }
    }
}
