using System;
using System.Collections.Generic;
using Gladiator.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gladiator.Tests
{
    /// <summary>
    /// U1-A the themed builders (docs/tracks/U1-contract.md "UiKit"): Text takes the role's font, material and spacing
    /// with no faux bold; plates are bronze (Primary) or marble (Secondary) with the label as the first TMP child and a
    /// key chip only when asked; SetState lights the ring only when Focused, dims when Disabled and allocates nothing;
    /// laurels flank the word outside its text bounds; FitScale; TruncatedLabels. Works with or without the theme asset.
    /// P3: the focus is the ansata's glow (`AnsataGlow`, the plate's size + 12) and the label's box spans the content rect;
    /// UiAudit's contrast maths, its surfaces (draw order, siblings, plain dims) and its padding and contrast checks.
    /// </summary>
    public class UiKitTests
    {
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
        Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            UiTheme.Reload();
            _canvas = UiFactory.Canvas("UiKitTestCanvas", null, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas.gameObject);
            UiTheme.Reload();
        }

        [TestCase(TextRole.Number, TextLook.Outline)]
        [TestCase(TextRole.Shout, TextLook.Outline)]
        [TestCase(TextRole.Title, TextLook.Engraved)]
        [TestCase(TextRole.Body, TextLook.Flat)]
        [TestCase(TextRole.Plain, TextLook.Flat)]
        public void Text_UsesTheRolesFontMaterialAndSpacing(TextRole role, TextLook look)
        {
            var theme = UiTheme.Current;
            var t = UiKit.Text("Label", _canvas.transform, "Sample 125!", role, 30f, Color.white, TextAlignmentOptions.Center, Mid, Mid,
                Vector2.zero, new Vector2(400f, 50f), look);
            Assert.AreSame(theme.FontFor(role), t.font);
            Assert.AreSame(theme.MaterialFor(role, look), t.fontSharedMaterial);
            Assert.AreEqual(theme.SpacingFor(role), t.characterSpacing, 1e-4f);
            Assert.AreEqual(FontStyles.Normal, t.fontStyle, "no faux bold or italic");
            Assert.IsFalse(t.raycastTarget);
            Assert.AreEqual(TextWrappingModes.NoWrap, t.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Truncate, t.overflowMode, "Truncate, never Ellipsis");
            Assert.IsTrue(UiKit.HasRole(t, role));
            Assert.IsFalse(UiKit.HasRole(t, role == TextRole.Body ? TextRole.Title : TextRole.Body));
        }

        [Test]
        public void Style_RestylesAnExistingLabel_KeepingItsWrapping()
        {
            var t = UiFactory.Text("Old", _canvas.transform, "old label", 24f, Color.white, TextAlignmentOptions.Center, Mid, Mid,
                Vector2.zero, new Vector2(300f, 80f), null, null);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Style(t, TextRole.Body, TextLook.Flat);
            Assert.AreSame(UiTheme.Current.FontFor(TextRole.Body), t.font);
            Assert.AreEqual(FontStyles.Normal, t.fontStyle, "UiFactory's Bold is dropped");
            Assert.AreEqual(TextWrappingModes.Normal, t.textWrappingMode, "wrapping is the caller's");
            Assert.AreEqual(TextOverflowModes.Truncate, t.overflowMode);
            Assert.IsTrue(UiKit.HasRole(t, TextRole.Body));
        }

        [Test]
        public void Plate_IsBronzeOrMarble_LabelFirst_KeyChipOnlyWithAKey()
        {
            var play = UiKit.Plate("Play", _canvas.transform, "PLAY", ButtonStyle.Primary, Mid, Mid, Vector2.zero, new Vector2(360f, 66f));
            var codex = UiKit.Plate("Codex", _canvas.transform, "CODEX", ButtonStyle.Secondary, Mid, Mid, Vector2.zero, new Vector2(360f, 66f));
            var reroll = UiKit.Plate("Reroll", _canvas.transform, "Reroll (2)", ButtonStyle.Primary, Mid, Mid, Vector2.zero, new Vector2(280f, 66f), 30f, "R");

            Assert.AreEqual((int)FrameKind.AnsataBronze, UiFrames.KindOf(play.Frame));
            Assert.AreEqual((int)FrameKind.AnsataMarble, UiFrames.KindOf(codex.Frame));
            Assert.AreEqual(ButtonStyle.Secondary, codex.Style);
            Assert.AreSame(play.Frame.rectTransform, play.Root);
            foreach (var p in new[] { play, codex, reroll })
            {
                Assert.AreSame(p.Label, p.Frame.GetComponentInChildren<TMP_Text>(), "the label is the plate's first TMP child");
                Assert.IsTrue(UiKit.HasRole(p.Label, TextRole.Title));
                Assert.IsNull(p.Button, "Plate has no Button");
                Assert.AreEqual((int)FrameKind.AnsataGlow, UiFrames.KindOf(p.Focus), "P3: the focus follows the ansata's shape");
                Assert.AreEqual(p.Root.sizeDelta + UiKit.GlowGrow, p.Focus.rectTransform.sizeDelta, "the glow: the plate's size + 12");
                Assert.AreEqual(Vector2.zero, p.Focus.rectTransform.anchoredPosition, "centred on the plate");
                Assert.AreEqual(0f, p.Focus.color.a, "the glow starts hidden");
                var content = UiKit.ContentRect(UiFrames.KindOf(p.Frame) == (int)FrameKind.AnsataBronze ? FrameKind.AnsataBronze : FrameKind.AnsataMarble, p.Root.sizeDelta);
                var box = p.Label.rectTransform;
                Assert.LessOrEqual(box.anchoredPosition.x + box.sizeDelta.x * 0.5f, content.xMax + 0.01f, "the label's box ends inside the content rect");
                Assert.GreaterOrEqual(box.anchoredPosition.x - box.sizeDelta.x * 0.5f, content.xMin - 0.01f, "and starts inside it");
                Assert.IsFalse(p.Frame.raycastTarget);
            }
            Assert.IsNull(play.Key);
            Assert.IsNull(play.KeyChip);
            Assert.IsNotNull(reroll.Key);
            Assert.AreEqual("R", reroll.Key.text);
            Assert.IsTrue(UiKit.HasRole(reroll.Key, TextRole.Number));
            Assert.AreEqual((int)FrameKind.KeyChip, UiFrames.KindOf(reroll.KeyChip));
            Assert.Less(reroll.KeyChip.rectTransform.anchoredPosition.x, reroll.Label.rectTransform.anchoredPosition.x, "the chip sits left of the label");
        }

        [Test]
        public void Button_TargetsTheFrame_AndUiFactoryLabelStillFindsTheLabel()
        {
            int clicks = 0;
            var p = UiKit.Button("Fight", _canvas.transform, "FIGHT!", ButtonStyle.Primary, Mid, Mid, Vector2.zero, new Vector2(300f, 78f), () => clicks++);
            Assert.IsNotNull(p.Button);
            Assert.AreSame(p.Frame, p.Button.targetGraphic);
            Assert.AreSame(p.Label, UiFactory.Label(p.Button));
            Assert.AreEqual(Navigation.Mode.None, p.Button.navigation.mode);
            Assert.IsTrue(p.Frame.raycastTarget);
            p.Button.onClick.Invoke();
            Assert.AreEqual(1, clicks);
        }

        [Test]
        public void SetState_RingOnlyWhenFocused_DimsWhenDisabled_AllocatesNothing()
        {
            var p = UiKit.Plate("Plate", _canvas.transform, "Banish (1)", ButtonStyle.Primary, Mid, Mid, Vector2.zero, new Vector2(280f, 66f), 30f, "B");
            p.SetState(UiState.Normal);
            float normal = p.Frame.color.r;
            Assert.AreEqual(0f, p.Focus.color.a);
            p.SetState(UiState.Focused);
            Assert.AreEqual(UiState.Focused, p.State);
            Assert.Greater(p.Focus.color.a, 0.5f, "Focused shows the glow");
            Assert.AreEqual(p.FocusColor.r, p.Focus.color.r, 1e-4f, "tinted by FocusColor");
            Assert.Greater(p.Frame.color.r, normal, "Focused brightens the face");
            p.SetState(UiState.Pressed);
            Assert.AreEqual(0f, p.Focus.color.a);
            Assert.Less(p.Frame.color.r, normal, "Pressed darkens the face");
            p.SetState(UiState.Disabled);
            Assert.AreEqual(0f, p.Focus.color.a);
            Assert.Less(p.Frame.color.a, 0.5f, "Disabled fades the face");
            Assert.Less(p.Label.color.a, 0.5f, "and the label");
            Assert.Less(p.Key.color.a, 0.5f, "and the key");
            p.SetState(UiState.Normal);
            Assert.AreEqual(1f, p.Label.color.a, 1e-4f);

            for (int i = 0; i < 8; i++) p.SetState((UiState)(i % 4)); // warm up (the rebuild queue's first adds)
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) p.SetState((UiState)(i % 4));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "SetState allocated");
        }

        static void LocalBox(RectTransform rt, Transform space, out float minX, out float maxX, out float minY, out float maxY)
        {
            var w = new Vector3[4];
            rt.GetWorldCorners(w);
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            foreach (var c in w)
            {
                var l = space.InverseTransformPoint(c);
                minX = Mathf.Min(minX, l.x); maxX = Mathf.Max(maxX, l.x);
                minY = Mathf.Min(minY, l.y); maxY = Mathf.Max(maxY, l.y);
            }
        }

        [Test]
        public void Laurels_FlankTheWord_OutsideItsTextBounds_RightOneMirrored()
        {
            var word = UiKit.Text("Victory", _canvas.transform, "VICTORY!", TextRole.Shout, 96f, Color.white, TextAlignmentOptions.Center, Mid, Mid,
                Vector2.zero, new Vector2(900f, 160f), TextLook.Outline);
            var laurels = UiKit.Laurels("Laurels", word);
            var left = (RectTransform)laurels.Find("Left");
            var right = (RectTransform)laurels.Find("Right");
            Assert.IsNotNull(left);
            Assert.IsNotNull(right);
            Assert.AreEqual((int)FrameKind.LaurelBranch, UiFrames.KindOf(left.GetComponent<Image>()));
            Assert.Greater(left.localScale.x, 0f);
            Assert.Less(right.localScale.x, 0f, "the right branch is mirrored");

            var b = word.textBounds;
            Assert.Greater(b.size.x, 0f, "the word laid out");
            LocalBox(left, word.transform, out _, out float lMaxX, out float lMinY, out float lMaxY);
            LocalBox(right, word.transform, out float rMinX, out _, out _, out _);
            Assert.LessOrEqual(lMaxX, b.min.x + 0.01f, "the left branch ends before the word");
            Assert.GreaterOrEqual(rMinX, b.max.x - 0.01f, "the right branch starts after it");
            Assert.AreEqual(b.center.y, (lMinY + lMaxY) * 0.5f, 0.5f, "vertically centred on the word");
            float k = Mathf.Max(0.75f, 96f / 48f);
            Assert.AreEqual(UiFrames.NativeSize(FrameKind.LaurelBranch).y * k, left.sizeDelta.y, 0.01f, "sized to the font");

            UiKit.Laurels("Laurels", word); // again: the same pair, re-placed
            int pairs = 0;
            foreach (Transform c in word.transform) if (c.name == "Laurels") pairs++;
            Assert.AreEqual(1, pairs);
        }

        [Test]
        public void FitScale_ShrinksToTheCanvas_NeverGrows()
        {
            Assert.AreEqual(0.985f, UiKit.FitScale(new Vector2(1640f, 980f), new Vector2(1663f, 1247f)), 0.001f);
            Assert.AreEqual(1f, UiKit.FitScale(new Vector2(1640f, 980f), new Vector2(1920f, 1080f)), 1e-5f);
            Assert.AreEqual(0.905f, UiKit.FitScale(new Vector2(1640f, 980f), new Vector2(2217f, 935f)), 0.001f);
        }

        [Test]
        public void TruncatedLabels_FindsACutLabel_IgnoresHiddenOnes()
        {
            const string longText = "THE PROVINCIAL CHAMPION OF THE SOUTHERN PITS";
            UiKit.Text("Cut", _canvas.transform, longText, TextRole.Title, 30f, Color.white, TextAlignmentOptions.Left, Mid, Mid,
                new Vector2(0f, 200f), new Vector2(90f, 40f));
            UiKit.Text("Fits", _canvas.transform, "OK", TextRole.Title, 30f, Color.white, TextAlignmentOptions.Left, Mid, Mid,
                new Vector2(0f, 100f), new Vector2(200f, 40f));
            var hiddenGroup = UiFactory.Rect("HiddenGroup", _canvas.transform, Mid, Mid, Vector2.zero, new Vector2(400f, 400f));
            hiddenGroup.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            UiKit.Text("CutButHidden", hiddenGroup, longText, TextRole.Title, 30f, Color.white, TextAlignmentOptions.Left, Mid, Mid,
                Vector2.zero, new Vector2(90f, 40f));
            UiKit.Text("CutButClear", _canvas.transform, longText, TextRole.Title, 30f, new Color(1f, 1f, 1f, 0f), TextAlignmentOptions.Left, Mid, Mid,
                new Vector2(0f, -100f), new Vector2(90f, 40f));
            UiKit.Text("CutButInactive", _canvas.transform, longText, TextRole.Title, 30f, Color.white, TextAlignmentOptions.Left, Mid, Mid,
                new Vector2(0f, -200f), new Vector2(90f, 40f)).gameObject.SetActive(false);

            var names = new List<string>();
            Assert.AreEqual(1, UiKit.TruncatedLabels(_canvas.transform, names));
            CollectionAssert.AreEqual(new[] { "Cut" }, names);
        }

        // ------------------------------------------------------------------ P3: UiAudit

        [Test]
        public void Contrast_IsWcag2_FromOneTo21()
        {
            Assert.AreEqual(21f, UiAudit.Contrast(Color.black, Color.white), 0.01f);
            Assert.AreEqual(21f, UiAudit.Contrast(Color.white, Color.black), 0.01f, "symmetric");
            Assert.AreEqual(1f, UiAudit.Contrast(Color.grey, Color.grey), 1e-4f);
            Assert.AreEqual(4.54f, UiAudit.Contrast(new Color32(0x76, 0x76, 0x76, 0xFF), Color.white), 0.02f, "#767676 on white: the classic 4.54:1");
            Assert.AreEqual(1f, UiAudit.Contrast(new Color(1f, 0f, 0f, 0.2f), Color.red), 1e-4f, "alpha is ignored");
        }

        [Test]
        public void SurfaceTones_SplitLightAndDark_AsTheContractSays()
        {
            var th = UiTheme.CreateDefault();
            // light faces take dark ink
            Assert.GreaterOrEqual(UiAudit.Contrast(th.bronzeInk, UiAudit.SurfaceTone(FrameKind.AnsataBronze)), UiAudit.MinContrast, "bronzeInk on the bronze ansata");
            Assert.GreaterOrEqual(UiAudit.Contrast(th.marbleInk, UiAudit.SurfaceTone(FrameKind.AnsataMarble)), UiAudit.MinContrast, "marbleInk on the marble ansata");
            Assert.GreaterOrEqual(UiAudit.Contrast(th.marbleInk, UiAudit.SurfaceTone(FrameKind.Marble)), UiAudit.MinContrast, "marbleInk on marble");
            // dark faces take light ink
            var dark = new[]
            {
                FrameKind.Bronze, FrameKind.Wax, FrameKind.Mosaic, FrameKind.Stone, FrameKind.CardStone, FrameKind.CardBronze,
                FrameKind.CardSilver, FrameKind.CardGold, FrameKind.Scutum, FrameKind.ScutumLocked, FrameKind.VexillumCrimson,
                FrameKind.VexillumPurple, FrameKind.Trough, FrameKind.Chip, FrameKind.KeyChip,
            };
            foreach (var k in dark)
            {
                Assert.Greater(UiAudit.SurfaceTone(k).a, 0f, k + " is a surface");
                Assert.GreaterOrEqual(UiAudit.Contrast(th.ink, UiAudit.SurfaceTone(k)), UiAudit.MinContrast, "ink on " + k);
                Assert.Less(UiAudit.Contrast(th.marbleInk, UiAudit.SurfaceTone(k)), UiAudit.MinContrast, "dark ink doesn't read on " + k);
            }
            // rims, glows, bands, icons and the clipeus' window are no surfaces
            foreach (var k in new[] { FrameKind.Focus, FrameKind.AnsataGlow, FrameKind.Halo, FrameKind.MeanderGold, FrameKind.MeanderDark,
                         FrameKind.Clipeus, FrameKind.LaurelWreath, FrameKind.SealNew, FrameKind.ArrowLeft, FrameKind.IconHeart })
                Assert.AreEqual(0f, UiAudit.SurfaceTone(k).a, k + " is no surface");
            Object.DestroyImmediate(th);
        }

        TextMeshProUGUI Label(Transform parent, string name, string text, Color color, Vector2 pos, Vector2 box,
            TextLook look = TextLook.Flat, TextAlignmentOptions align = TextAlignmentOptions.Center) =>
            UiKit.Text(name, parent, text, TextRole.Title, 24f, color, align, Mid, Mid, pos, box, look);

        [Test]
        public void TheSurface_IsTheLastDrawnUnderTheWords_SiblingsAndDimsCount()
        {
            var th = UiTheme.Current;
            var dim = UiFactory.Image("Dim", _canvas.transform, th.night, Mid, Mid, Vector2.zero, new Vector2(1600f, 900f), PixelIcons.White);
            var page = UiKit.Frame("Page", _canvas.transform, FrameKind.Marble, Mid, Mid, Vector2.zero, new Vector2(1000f, 600f), Color.white);
            var card = UiKit.Frame("Card", page.transform, FrameKind.Stone, Mid, Mid, new Vector2(-200f, 0f), new Vector2(400f, 160f), Color.white);
            var onCard = Label(card.transform, "OnCard", "IRON HIDES", th.ink, Vector2.zero, new Vector2(300f, 40f));
            var onPage = Label(page.transform, "OnPage", "THE EDITOR", th.marbleInk, new Vector2(200f, 200f), new Vector2(300f, 40f));
            var onDim = Label(_canvas.transform, "OnDim", "BACK", th.ink, new Vector2(0f, 400f), new Vector2(300f, 40f));
            // a plate drawn beside its label (a sibling before it) is its surface
            var plate = UiKit.Frame("Plate", _canvas.transform, FrameKind.Bronze, Mid, Mid, new Vector2(600f, -380f), new Vector2(300f, 90f), Color.white);
            var beside = Label(_canvas.transform, "Beside", "125", th.ink, new Vector2(600f, -380f), new Vector2(200f, 40f));
            // a frame drawn after the label covers nothing under it
            var over = Label(_canvas.transform, "Over", "LATE", th.ink, new Vector2(-600f, -380f), new Vector2(200f, 40f));
            UiKit.Frame("Later", _canvas.transform, FrameKind.Stone, Mid, Mid, new Vector2(-600f, -380f), new Vector2(300f, 90f), Color.white);
            Canvas.ForceUpdateCanvases();

            Assert.AreSame(card, UiAudit.SurfaceOf(onCard, out int k));
            Assert.AreEqual((int)FrameKind.Stone, k);
            Assert.AreSame(page, UiAudit.SurfaceOf(onPage, out k));
            Assert.AreSame(dim, UiAudit.SurfaceOf(onDim, out k), "a dim layer is a plain colour surface");
            Assert.AreEqual(-1, k);
            Assert.AreSame(plate, UiAudit.SurfaceOf(beside, out k), "siblings count");
            Assert.AreSame(dim, UiAudit.SurfaceOf(over, out _), "the stone drawn after the words isn't under them");
            dim.color = new Color(0f, 0f, 0f, 0.3f);
            Assert.IsNull(UiAudit.SurfaceOf(onDim, out _), "a see-through dim is no surface: the words are over the scene");
        }

        [Test]
        public void Padding_FlagsInkOutsideTheContentRect_NotInsideIt()
        {
            var th = UiTheme.Current;
            var size = new Vector2(400f, 120f);
            var tablet = UiKit.Frame("Tablet", _canvas.transform, FrameKind.Wax, Mid, Mid, Vector2.zero, size, Color.white);
            var content = UiKit.ContentRect(FrameKind.Wax, size);
            var fits = Label(tablet.transform, "Fits", "THE NOBODY", th.waxInk, Vector2.zero, new Vector2(content.width, 40f));
            Canvas.ForceUpdateCanvases();
            var why = new List<string>();
            Assert.AreEqual(0, UiAudit.PaddingViolations(_canvas.transform, why), string.Join("; ", why));

            // the producer's note 4: the line sits on the frame's bottom edge
            // 40 tall: a 24 pt Marcellus line is 30.1, so a 30-tall box truncates the whole line (no ink to audit)
            var low = Label(tablet.transform, "Low", "Starts with Rusty Gladius", th.waxInk, new Vector2(0f, -size.y * 0.5f + 6f), new Vector2(content.width, 40f));
            Canvas.ForceUpdateCanvases();
            why.Clear();
            Assert.AreEqual(1, UiAudit.PaddingViolations(_canvas.transform, why));
            StringAssert.StartsWith("Tablet/Low", why[0]);
            StringAssert.Contains("bottom", why[0]);
            UiAudit.Exempt(low);
            Assert.AreEqual(0, UiAudit.PaddingViolations(_canvas.transform, null), "exempt");
            Assert.IsTrue(UiAudit.TryInkBounds(fits, out var ink));
            Assert.Less(ink.height, fits.textBounds.size.y, "ink is the glyphs, not the font's ascent-to-descent line");
        }

        [Test]
        public void Contrast_FlagsDarkOnDark_AndBareWordsOverTheScene()
        {
            var th = UiTheme.Current;
            var wax = UiKit.Frame("Wax", _canvas.transform, FrameKind.Wax, Mid, Mid, Vector2.zero, new Vector2(600f, 300f), Color.white);
            Label(wax.transform, "Light", "LEDGER", th.waxInk, new Vector2(0f, 60f), new Vector2(300f, 40f));
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(0, UiAudit.ContrastViolations(_canvas.transform, null));

            // note 8: dark words on a dark tablet
            Label(wax.transform, "Dark", "Left / Right: tab", th.marbleInk, new Vector2(0f, -60f), new Vector2(400f, 40f));
            // over the scene: bare words fail, outlined ones pass
            Label(_canvas.transform, "Bare", "28 runs", th.ink, new Vector2(0f, 400f), new Vector2(300f, 40f));
            Label(_canvas.transform, "Outlined", "1 victory", th.ink, new Vector2(0f, -400f), new Vector2(300f, 40f), TextLook.Outline);
            // a rich-text colour that doesn't read counts too
            Label(wax.transform, "Rich", "RENOWN <color=#3D342B>x2</color>", th.waxInk, Vector2.zero, new Vector2(400f, 40f));
            Canvas.ForceUpdateCanvases();
            var why = new List<string>();
            Assert.AreEqual(3, UiAudit.ContrastViolations(_canvas.transform, why), string.Join("; ", why));
            Assert.IsTrue(why.Exists(w => w.StartsWith("Wax/Dark")), "dark on wax");
            Assert.IsTrue(why.Exists(w => w.StartsWith("Bare") || w.Contains("/Bare")), "bare over the scene");
            Assert.IsTrue(why.Exists(w => w.Contains("Rich")), "the rich-text colour");
        }
    }
}
