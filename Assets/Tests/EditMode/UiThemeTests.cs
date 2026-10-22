using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Gladiator.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gladiator.Tests
{
    /// <summary>
    /// U1-A the theme and the frames (docs/tracks/U1-contract.md "U1-A"): the theme is always there and its defaults are
    /// spec §3's; the index parser; the shipped index (one line per FrameKind, inside the PNG, no overlaps); and, once
    /// Gladiator/Setup/UI/All has made the theme asset, the cut sprites, the static role fonts with every required glyph
    /// (the failure lists the missing ones), their three looks on one atlas, and the licences in StreamingAssets.
    /// </summary>
    public class UiThemeTests
    {
        const string IndexPath = "Assets/Art/Textures/UI/UiFrames.txt";
        const string AtlasPath = "Assets/Art/Textures/UI/UiFrames.png";
        const string LicenceDir = "Assets/StreamingAssets/Licenses/Fonts";

        [TearDown]
        public void TearDown() => UiTheme.Reload();

        [Test]
        public void Current_IsNeverNull()
        {
            UiTheme.Reload();
            Assert.IsNotNull(UiTheme.Current);
            Assert.AreSame(UiTheme.Current, UiTheme.Current, "cached");
        }

        static void AssertHex(Color c, uint rgb, string token)
        {
            Color32 c32 = c;
            Assert.AreEqual(rgb.ToString("X6"), ((uint)(c32.r << 16 | c32.g << 8 | c32.b)).ToString("X6"), token);
            Assert.AreEqual(255, c32.a, token + " alpha");
        }

        [Test]
        public void Defaults_AreTheSpecsTokens_AndTmpsFont()
        {
            var t = UiTheme.CreateDefault();
            try
            {
                AssertHex(t.night, 0x191512, "night");
                AssertHex(t.cardInside, 0x1C1714, "cardInside");
                AssertHex(t.bronze, 0xB07A3B, "bronze");
                AssertHex(t.gold, 0xE2B14A, "gold");
                AssertHex(t.goldBright, 0xFFCF4A, "goldBright");
                AssertHex(t.crimson, 0x7A1F25, "crimson");
                AssertHex(t.purple, 0x4B2A5C, "purple");
                AssertHex(t.marble, 0xECE7DE, "marble");
                AssertHex(t.danger, 0xFF5433, "danger");
                foreach (TextRole r in Enum.GetValues(typeof(TextRole)))
                    Assert.AreSame(TMP_Settings.defaultFontAsset, t.FontFor(r), r.ToString());
                Assert.AreEqual(0.32f, t.bannerDrop, 1e-5f);
                Assert.AreEqual(2f, t.bannerHold, 1e-5f);
                Assert.AreEqual(4f, t.title.spacing, 1e-5f);
            }
            finally { Object.DestroyImmediate(t); }
        }

        [Test]
        public void MaterialFor_FallsBackToThePlainRole_ThenTheFlatPreset()
        {
            var t = UiTheme.CreateDefault();
            var flat = new Material(Shader.Find("UI/Default"));
            var outline = new Material(Shader.Find("UI/Default"));
            try
            {
                t.plain = new RoleFont { font = TMP_Settings.defaultFontAsset, flat = flat, outline = outline };
                Assert.AreSame(TMP_Settings.defaultFontAsset, t.FontFor(TextRole.Title), "no Title font: the plain one");
                Assert.AreSame(outline, t.MaterialFor(TextRole.Title, TextLook.Outline));
                Assert.AreSame(flat, t.MaterialFor(TextRole.Title, TextLook.Engraved), "no engraved preset: the flat one");
                Assert.AreEqual(0f, t.SpacingFor(TextRole.Title), "a borrowed font isn't tuned");
            }
            finally
            {
                Object.DestroyImmediate(t);
                Object.DestroyImmediate(flat);
                Object.DestroyImmediate(outline);
            }
        }

        [Test]
        public void ParseIndex_SkipsCommentsAndBlankLines_AndWarnsOnBadOnes()
        {
            const string sample =
                "# a comment\n\n" +
                "Marble 0 0 24 24 6 6 6 6 sliced 8 8 8 8\r\n" +
                "   Focus 30 2 6 6 2 2 2 2 sliced 0 0 0 0   \n" +
                "Broken 1 2 three 4 0 0 0 0 sliced 0 0 0 0\n" +
                "TooShort 1 2 3\n" +
                "BadType 0 0 4 4 0 0 0 0 stretched 0 0 0 0\n" +
                "MeanderGold 40 0 6 5 0 0 0 0 tiled 0 0 0 0";
            for (int i = 0; i < 3; i++) LogAssert.Expect(LogType.Warning, new Regex("bad index line"));
            var list = new List<FrameEntry>();
            Assert.AreEqual(3, UiFrames.ParseIndex(sample, list));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("Marble", list[0].Name);
            Assert.AreEqual(new RectInt(0, 0, 24, 24), list[0].Rect);
            Assert.AreEqual(new Vector4(6, 6, 6, 6), list[0].Border);
            Assert.AreEqual(new Vector4(8, 8, 8, 8), list[0].Padding);
            Assert.AreEqual(Image.Type.Sliced, list[0].Type);
            Assert.AreEqual(new RectInt(30, 2, 6, 6), list[1].Rect);
            Assert.AreEqual(Image.Type.Tiled, list[2].Type);
            Assert.AreEqual(0, UiFrames.ParseIndex("", list));
        }

        static Vector2Int PngSize(string path)
        {
            var b = File.ReadAllBytes(path); // IHDR: width and height, big-endian, at bytes 16..23
            return new Vector2Int(b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19], b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23]);
        }

        [Test]
        public void ShippedIndex_HasEveryKindOnce_InsideThePng_WithoutOverlaps()
        {
            var list = new List<FrameEntry>();
            int n = UiFrames.ParseIndex(File.ReadAllText(IndexPath), list);
            Assert.AreEqual(UiFrames.KindCount, n, "one line per FrameKind");
            var png = PngSize(AtlasPath);
            Assert.LessOrEqual(png.x, 512);
            Assert.LessOrEqual(png.y, 512);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                Assert.AreEqual(((FrameKind)i).ToString(), e.Name, "the index follows the enum's order");
                var r = e.Rect;
                Assert.IsTrue(r.x >= 0 && r.y >= 0 && r.xMax <= png.x && r.yMax <= png.y, e.Name + " lies inside the PNG");
                float bx = e.Border.x + e.Border.z, by = e.Border.y + e.Border.w;
                Assert.IsTrue(bx == 0f || bx < r.width, e.Name + ": left + right borders inside the rect");
                Assert.IsTrue(by == 0f || by < r.height, e.Name + ": bottom + top borders inside the rect");
                if (e.Type == Image.Type.Simple) Assert.AreEqual(Vector4.zero, e.Border, e.Name + ": simple frames have no border");
                for (int j = 0; j < i; j++)
                    Assert.IsFalse(r.Overlaps(list[j].Rect), e.Name + " overlaps " + list[j].Name);
            }
        }

        static UiTheme ThemeAssetOrIgnore()
        {
            var theme = Resources.Load<UiTheme>(UiTheme.ResourcePath);
            if (theme == null) Assert.Ignore("no UiTheme asset yet (run Gladiator/Setup/UI/All)");
            UiTheme.Reload();
            return theme;
        }

        [Test]
        public void Sprites_AreCutWithTheIndexBorders()
        {
            var theme = ThemeAssetOrIgnore();
            Assert.IsNotNull(theme.frameAtlas, "the theme links the atlas");
            Assert.IsNotNull(theme.frameIndex, "the theme links the index");
            var list = new List<FrameEntry>();
            UiFrames.ParseIndex(theme.frameIndex.text, list);
            foreach (var e in list)
            {
                var k = (FrameKind)Enum.Parse(typeof(FrameKind), e.Name);
                var s = UiFrames.Get(k);
                Assert.AreNotSame(PixelIcons.White, s, e.Name);
                Assert.AreSame(s, UiFrames.Get(k), e.Name + " is cached");
                Assert.AreEqual(e.Border, s.border, e.Name + " border");
                Assert.AreEqual(100f / UiFrames.ArtScale, s.pixelsPerUnit, 1e-3f, e.Name + " pixelsPerUnit");
                Assert.AreEqual(new Rect(e.Rect.x, theme.frameAtlas.height - e.Rect.y - e.Rect.height, e.Rect.width, e.Rect.height), s.rect,
                    e.Name + " rect (y flipped)");
                Assert.AreEqual(e.Type, UiFrames.TypeOf(k), e.Name + " type");
                Assert.AreEqual(new Vector2(e.Rect.width, e.Rect.height) * UiFrames.ArtScale, UiFrames.NativeSize(k), e.Name + " native size");
            }
        }

        static string Describe(uint[] missing)
        {
            if (missing == null || missing.Length == 0) return "(none)";
            var sb = new StringBuilder();
            foreach (var u in missing) sb.Append(char.ConvertFromUtf32((int)u)).Append($" (U+{u:X4}) ");
            return sb.ToString();
        }

        [TestCase(TextRole.Number)]
        [TestCase(TextRole.Shout)]
        [TestCase(TextRole.Title)]
        [TestCase(TextRole.Body)]
        public void RoleFont_IsStatic_FallsBackToSymbols_AndHasEveryGlyph(TextRole role)
        {
            var theme = ThemeAssetOrIgnore();
            var rf = theme.Role(role);
            Assert.IsNotNull(rf.font, role + " has no font (run Gladiator/Setup/UI/All)");
            Assert.AreEqual(AtlasPopulationMode.Static, rf.font.atlasPopulationMode, rf.font.name + " must be static (no glyph adds mid-run)");
            var fallbacks = rf.font.fallbackFontAssetTable;
            Assert.IsTrue(fallbacks != null && fallbacks.Count > 0 && fallbacks[0] != null, rf.font.name + " has no fallbacks");
            Assert.AreEqual("FN_Symbols SDF", fallbacks[0].name);
            bool all = rf.font.HasCharacters(UiTheme.Glyphs(role) + UiTheme.Symbols, out uint[] missing, true);
            Assert.IsTrue(all, $"{role} ({rf.font.name}) and its fallbacks lack: {Describe(missing)}");
            foreach (var m in new[] { rf.flat, rf.outline, rf.engraved })
            {
                Assert.IsNotNull(m, rf.font.name + " is missing a material preset");
                Assert.AreEqual(rf.font.atlasTexture, m.GetTexture(ShaderUtilities.ID_MainTex), m.name + " draws from the font's atlas"); // Unity equality: GetTexture can hand back a second wrapper of the same texture
            }
            Assert.AreSame(rf.font, theme.FontFor(role));
        }

        [Test]
        public void FontLicences_ShipInStreamingAssets()
        {
            ThemeAssetOrIgnore();
            foreach (var f in new[] { "LuckiestGuy-LICENSE.txt", "CaesarDressing-OFL.txt", "MarcellusSC-OFL.txt", "Marcellus-OFL.txt" })
                Assert.IsTrue(File.Exists(Path.Combine(LicenceDir, f)), f + " is not in " + LicenceDir);
        }
    }
}
