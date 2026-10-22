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
                Assert.AreEqual((int)FrameKind.Focus, UiFrames.KindOf(p.Focus));
                Assert.AreEqual(0f, p.Focus.color.a, "the ring starts hidden");
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
            Assert.Greater(p.Focus.color.a, 0.5f, "Focused shows the ring");
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
    }
}
