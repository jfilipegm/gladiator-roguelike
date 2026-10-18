using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// Level-up card picker (M1.3 stub look): dimmed screen, "LEVEL UP!", N cards. Mouse click, keys 1-4, or
    /// gamepad (EventSystem navigation + South). Shown while LevelUpController.IsOpen (the game is paused).
    /// Card texts are pre-sized in Awake so TMP never grows its buffers when a card opens (that was ~27 KB per open).
    /// Show/hide is a plain SetActive: measured ~1.1 KB of UGUI internals per open+close, while toggling the
    /// Canvas/CanvasGroup instead cost ~7.7 KB (colour-tint tweens + canvas-hierarchy callbacks).
    /// </summary>
    public sealed class LevelUpPanel : MonoBehaviour, ITickable
    {
        public TMP_FontAsset font;
        public Material fontMaterial;

        GameObject _root;
        readonly Button[] _cards = new Button[4];
        readonly TextMeshProUGUI[] _titles = new TextMeshProUGUI[4];
        readonly TextMeshProUGUI[] _descs = new TextMeshProUGUI[4];
        bool _shown;
        float _openedAt;

        static readonly Color CardColor = new Color(0.13f, 0.16f, 0.2f, 0.97f);
        static readonly Color CardBorder = new Color(0.31f, 0.89f, 0.82f);

        void Awake()
        {
            var canvas = UiFactory.Canvas("LevelUpCanvas", transform, UiSortOrder.LevelUp).transform;
            _root = new GameObject("Panel", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            UiFactory.Stretch((RectTransform)_root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.03f, 0.06f, 0.6f);
            dim.raycastTarget = true;
            var mid = new Vector2(0.5f, 0.5f);
            UiFactory.Text("Title", _root.transform, "LEVEL UP!", 92, new Color(1f, 0.84f, 0.3f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, 330), new Vector2(1000, 120), font, fontMaterial);
            UiFactory.Text("Sub", _root.transform, "choose one", 34, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, 260), new Vector2(1000, 50), font, fontMaterial);

            for (int i = 0; i < _cards.Length; i++)
            {
                var border = UiFactory.Image("Card" + i, _root.transform, CardBorder, mid, mid, Vector2.zero, new Vector2(340, 420), PixelIcons.White);
                border.raycastTarget = true;
                var inner = UiFactory.Image("Inner", border.transform, CardColor, mid, mid, Vector2.zero, new Vector2(328, 408), PixelIcons.White);
                inner.raycastTarget = false;
                var btn = border.gameObject.AddComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(1f, 0.95f, 0.6f);
                colors.selectedColor = new Color(1f, 0.95f, 0.6f);
                colors.pressedColor = new Color(1f, 0.8f, 0.3f);
                btn.colors = colors;
                int idx = i;
                btn.onClick.AddListener(() => Pick(idx));
                _cards[i] = btn;
                _titles[i] = UiFactory.Text("Title", border.transform, "Title", 40, Color.white, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 120), new Vector2(310, 110), font, fontMaterial);
                _titles[i].textWrappingMode = TextWrappingModes.Normal;
                _descs[i] = UiFactory.Text("Desc", border.transform, "Desc", 30, new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, -20), new Vector2(300, 160), font, fontMaterial);
                _descs[i].textWrappingMode = TextWrappingModes.Normal;
                _descs[i].fontStyle = FontStyles.Normal;
                UiFactory.Text("Key", border.transform, "[" + (i + 1) + "]", 30, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, -170), new Vector2(200, 50), font, fontMaterial);
            }
            // Pre-size every card's TMP buffers with longer text than any card will show.
            for (int i = 0; i < _cards.Length; i++)
            {
                _titles[i].SetText("WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW");
                _descs[i].SetText("WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW");
                _titles[i].ForceMeshUpdate(true, true);
                _descs[i].ForceMeshUpdate(true, true);
            }
            _root.SetActive(false);
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 60);
        void OnDisable() => GameLoop.Unregister(this);

        void Pick(int i)
        {
            if (Time.unscaledTime - _openedAt < 0.25f) return; // ignore the click/press that was already in flight
            LevelUpController.Instance?.Choose(i);
        }

        public void Tick(float dt, float unscaledDt)
        {
            var c = LevelUpController.Instance;
            bool open = c != null && c.IsOpen;
            if (open != _shown)
            {
                _shown = open;
                _root.SetActive(open);
                if (open) Show(c);
                else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null) EventSystem.current.SetSelectedGameObject(null);
            }
            if (!open) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Pick(0);
                else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
                else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
                else if (kb.digit4Key.wasPressedThisFrame) Pick(3);
            }
        }

        void Show(LevelUpController c)
        {
            _openedAt = Time.unscaledTime;
            int n = c.ChoiceCount;
            float spacing = 380f;
            float x0 = -(n - 1) * spacing * 0.5f;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < n;
                if (_cards[i].gameObject.activeSelf != on) _cards[i].gameObject.SetActive(on);
                if (!on) continue;
                ((RectTransform)_cards[i].transform).anchoredPosition = new Vector2(x0 + i * spacing, -20f);
                _titles[i].text = c.Choices[i].Title;
                _descs[i].text = c.Choices[i].Description;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_cards[0].gameObject);
        }
    }
}
