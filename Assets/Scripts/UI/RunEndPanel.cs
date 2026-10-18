using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// End-of-run screen (slice placeholder for M3.5): "YOU DIED" (orange-red) or "SURVIVED!" (gold), the run's
    /// stats, Retry (button / R / Enter / gamepad South) and Quit. Shown while RunEndController.ScreenShown.
    /// </summary>
    public sealed class RunEndPanel : MonoBehaviour, ITickable
    {
        public TMP_FontAsset font;
        public Material fontMaterial;

        static readonly Color Hurt = new Color(1f, 0.33f, 0.2f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);

        GameObject _root;
        TextMeshProUGUI _title, _subtitle, _note, _time, _level, _kills, _gold, _damage;
        bool _shown;
        float _shownAt;

        void Awake()
        {
            var canvas = UiFactory.Canvas("RunEndCanvas", transform, UiSortOrder.Menus).transform;
            _root = canvas.gameObject;
            var dim = UiFactory.Image("Dim", canvas, new Color(0.02f, 0.02f, 0.04f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, PixelIcons.White);
            UiFactory.Stretch(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true;
            var mid = new Vector2(0.5f, 0.5f);
            _title = UiFactory.Text("Title", canvas, "YOU DIED", 120, Hurt, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 300), new Vector2(1400, 150), font, fontMaterial);
            _subtitle = UiFactory.Text("Subtitle", canvas, "", 36, Soft, TextAlignmentOptions.Center, mid, mid, new Vector2(0, 205), new Vector2(1400, 50), font, fontMaterial);
            _subtitle.fontStyle = FontStyles.Normal;

            var card = UiFactory.Image("Stats", canvas, new Color(0.08f, 0.1f, 0.14f, 0.95f), mid, mid, new Vector2(0, 10), new Vector2(620, 300), PixelIcons.White);
            _time = StatRow(card.transform, "Time survived", 0);
            _level = StatRow(card.transform, "Level", 1);
            _kills = StatRow(card.transform, "Kills", 2);
            _gold = StatRow(card.transform, "Gold", 3);
            _damage = StatRow(card.transform, "Damage dealt", 4);

            UiFactory.Button("Retry", canvas, "Retry  [R]", mid, mid, new Vector2(-140, -220), new Vector2(250, 70), font, fontMaterial, OnRetry, 32f);
            UiFactory.Button("Quit", canvas, "Quit", mid, mid, new Vector2(140, -220), new Vector2(250, 70), font, fontMaterial, OnQuit, 32f);
            _note = UiFactory.Text("Note", canvas, "", 22, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center, mid, mid, new Vector2(0, -300), new Vector2(1400, 40), font, fontMaterial);
            _note.fontStyle = FontStyles.Normal;
            _root.SetActive(false);
        }

        TextMeshProUGUI StatRow(Transform card, string label, int row)
        {
            float y = 112f - row * 54f;
            var mid = new Vector2(0.5f, 0.5f);
            var l = UiFactory.Text(label, card, label, 32, Soft, TextAlignmentOptions.Left, mid, mid, new Vector2(-110, y), new Vector2(340, 50), font, fontMaterial);
            l.fontStyle = FontStyles.Normal;
            return UiFactory.Text(label + "Value", card, "0", 34, Color.white, TextAlignmentOptions.Right, mid, mid, new Vector2(170, y), new Vector2(220, 50), font, fontMaterial);
        }

        void OnEnable() => GameLoop.Register(this, TickGroup.Late, 65);
        void OnDisable() => GameLoop.Unregister(this);

        public void Tick(float dt, float unscaledDt)
        {
            var c = RunEndController.Instance;
            bool show = c != null && c.ScreenShown;
            if (show != _shown)
            {
                _shown = show;
                _root.SetActive(show);
                if (show) Fill(c);
            }
            if (!show) return;
            // ignore keys for a moment so a held button doesn't instantly retry
            if (Time.unscaledTime - _shownAt < 0.4f) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
                OnRetry();
        }

        void Fill(RunEndController c)
        {
            _shownAt = Time.unscaledTime;
            var run = c.run;
            bool died = c.Outcome == RunOutcome.Died;
            _title.text = died ? "YOU DIED" : "SURVIVED!";
            _title.color = died ? Hurt : Gold;
            _subtitle.text = died ? "The crowd wanted more." : "The pit falls silent. Somewhere above, the Pit Master stands up.";
            _note.text = died ? "Admin menu: F1 (God mode, buffs, spawns)" : "Slice note: Gorm, the boss, will appear at 0:00 once he's built; this screen moves to after the fight.";
            float t = run != null ? run.Elapsed : 0f;
            _time.SetText("{0}:{1:00}", Mathf.Floor(t / 60f), Mathf.Floor(t % 60f));
            _level.SetText("{0}", run != null ? run.Level : 0);
            _kills.SetText("{0}", run != null ? run.Kills : 0);
            _gold.SetText("{0}", run != null ? run.Gold : 0);
            _damage.SetText("{0}", run != null ? Mathf.Round(run.DamageDealt) : 0);
        }

        void OnRetry() { if (_shown) RunEndController.Instance?.Retry(); }
        void OnQuit() { if (_shown) RunEndController.Instance?.Quit(); }
    }
}
