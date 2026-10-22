// U1-STUB(a): stand-ins for U1-A's theme API (docs/tracks/U1-contract.md) so U1-B and U1-C compile alone.
// Both parts add this exact file; the orchestrator deletes it when it merges them (U1-A's files win).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gladiator.UI
{
    public enum TextRole : byte { Number, Shout, Title, Body, Plain }
    public enum TextLook : byte { Flat, Outline, Engraved }
    public enum ButtonStyle : byte { Primary, Secondary }
    public enum UiState : byte { Normal, Focused, Pressed, Disabled }
    public enum BannerCloth : byte { Crimson, Purple }
    public enum BannerPhase : byte { Hidden, Dropping, Settling, Holding, Rolling }
    public enum FrameKind : byte
    {
        AnsataBronze, AnsataMarble, Marble, Bronze, Wax, Mosaic, Stone, MeanderGold, MeanderDark,
        CardStone, CardBronze, CardSilver, CardGold, Halo, Focus,
        Scutum, ScutumLocked, ScutumSpine, Clipeus, ClipeusLocked,
        VexillumBar, VexillumCrimson, VexillumPurple,
        Trough, TroughFill, Knob, Chip, KeyChip, LaurelBranch, LaurelWreath,
    }

    [Serializable]
    public struct RoleFont
    {
        public TMP_FontAsset font;
        public Material flat, outline, engraved;
        public float spacing;
    }

    public sealed class UiTheme : ScriptableObject // U1-STUB(a)
    {
        public const string ResourcePath = "UiTheme";
        public const string Symbols = "";
        public RoleFont number, shout, title, body, plain;
        public Texture2D frameAtlas;
        public TextAsset frameIndex;
        public Color night, stone, stoneLight, line, cardInside, ink, dust, outlineInk,
            bronze, bronzeLight, bronzeDark, bronzeInk, gold, goldBright, crimson, crimsonLight, purple, purpleDark, purpleInk,
            marble, marbleShade, marbleInk, waxWood, wax, waxInk, danger, gain, loss, hp, hpDark, xp, favor, coin;
        public float bannerDrop = 0.32f, bannerSettle = 0.12f, bannerSway = 0.7f, bannerHold = 2f, bannerRoll = 0.28f,
            bannerQuickRoll = 0.15f, bannerSwayDegrees = 1.4f, bannerOvershoot = 0.05f;
        static UiTheme _current;
        public static UiTheme Current => _current != null ? _current : _current = CreateInstance<UiTheme>();
        public static void Reload() => _current = null;
        public RoleFont Role(TextRole r) => default;
        public TMP_FontAsset FontFor(TextRole r) => TMP_Settings.defaultFontAsset;
        public Material MaterialFor(TextRole r, TextLook look) => null;
        public static string Glyphs(TextRole r) => "";
    }

    public struct FrameEntry // U1-STUB(a)
    {
        public string Name;
        public RectInt Rect;
        public Vector4 Border, Padding;
        public Image.Type Type;
    }

    public static class UiFrames // U1-STUB(a)
    {
        public const float ArtScale = 3f;
        public static Sprite Get(FrameKind k) => PixelIcons.White;
        public static Image.Type TypeOf(FrameKind k) => Image.Type.Simple;
        public static Vector2 NativeSize(FrameKind k) => new Vector2(48f, 48f);
        public static Vector4 Padding(FrameKind k) => Vector4.zero;
        public static Image Apply(Image img, FrameKind k) { img.sprite = PixelIcons.White; return img; }
        public static int KindOf(Component image) => -1;
        public static int ParseIndex(string text, List<FrameEntry> into) => 0;
    }

    public sealed class UiPlate // U1-STUB(a)
    {
        public RectTransform Root;
        public Image Frame, Focus;
        public TextMeshProUGUI Label, Key;
        public Button Button;
        public ButtonStyle Style;
        public UiState State { get; private set; }
        public void SetState(UiState s) => State = s;
    }

    public static class UiKit // U1-STUB(a)
    {
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        public static TextMeshProUGUI Text(string name, Transform parent, string text, TextRole role, float size, Color color,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextLook look = TextLook.Flat)
            => UiFactory.Text(name, parent, text, size, color, align, anchor, pivot, pos, box, null, null);

        public static void Style(TMP_Text t, TextRole role, TextLook look) { }

        public static Image Frame(string name, Transform parent, FrameKind kind, Vector2 anchor, Vector2 pivot, Vector2 pos,
            Vector2 size, Color tint)
            => UiFactory.Image(name, parent, tint, anchor, pivot, pos, size, PixelIcons.White);

        public static UiPlate Plate(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, float fontSize = 30f, string key = null)
        {
            var frame = UiFactory.Image(name, parent, Color.white, anchor, pivot, pos, size, PixelIcons.White);
            var text = UiFactory.Text("Label", frame.transform, label, fontSize, Color.black, TextAlignmentOptions.Center, Mid, Mid,
                Vector2.zero, size, null, null);
            return new UiPlate { Root = frame.rectTransform, Frame = frame, Label = text, Style = style };
        }

        public static UiPlate Button(string name, Transform parent, string label, ButtonStyle style, Vector2 anchor, Vector2 pivot,
            Vector2 pos, Vector2 size, UnityAction onClick, float fontSize = 30f, string key = null)
        {
            var plate = Plate(name, parent, label, style, anchor, pivot, pos, size, fontSize, key);
            plate.Frame.raycastTarget = true;
            plate.Button = plate.Frame.gameObject.AddComponent<Button>();
            if (onClick != null) plate.Button.onClick.AddListener(onClick);
            return plate;
        }

        public static Image Bar(string name, Transform parent, Color fill, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
            => UiFactory.Bar(name, parent, Color.black, fill, anchor, pivot, pos, size);

        public static Image Meander(string name, Transform parent, bool gold, Vector2 anchor, Vector2 pivot, Vector2 pos, float width)
            => UiFactory.Image(name, parent, Color.white, anchor, pivot, pos, new Vector2(width, 15f), PixelIcons.White);

        public static RectTransform Laurels(string name, TMP_Text word, float gap = 12f)
            => UiFactory.Rect(name, word.transform, Mid, Mid, Vector2.zero, Vector2.zero);

        public static Image Rule(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float width)
            => UiFactory.Image(name, parent, Color.white, anchor, pivot, pos, new Vector2(width, 3f), PixelIcons.White);

        public static float FitScale(Vector2 size, Vector2 canvas, float margin = 24f) => 1f;
        public static bool HasRole(Component text, TextRole role) => false;
        public static int TruncatedLabels(Transform root, List<string> into) => 0;
    }

    public sealed class VexillumBanner // U1-STUB(a)
    {
        public VexillumBanner(Transform parent, Vector2 anchor, Vector2 pos, float width = 760f)
            => Root = UiFactory.Rect("Banner", parent, anchor, new Vector2(0.5f, 1f), pos, new Vector2(width, 150f));

        public RectTransform Root { get; }
        public BannerPhase Phase { get; private set; }
        public bool Visible => Phase != BannerPhase.Hidden;
        public BannerCloth Cloth { get; private set; }
        public string Text { get; private set; }
        public float Down01 => 0f;
        public float SwayDegrees => 0f;
        public bool Held { get; private set; }
        public void Show(string text, BannerCloth cloth, float seconds = 0f) { Text = text; Cloth = cloth; Phase = BannerPhase.Dropping; }
        public void Hold(string text, BannerCloth cloth) { Show(text, cloth); Held = true; }
        public void RollUp(bool quick = false) => Phase = BannerPhase.Hidden;
        public void Clear() { Phase = BannerPhase.Hidden; Held = false; }
        public void Tick(float dt) { }
        public static float TotalSeconds(float seconds = 0f) => 2.72f;
    }
}
