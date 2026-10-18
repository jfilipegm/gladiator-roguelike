using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gladiator.Tests
{
    /// <summary>Guards the single-source palette (palette.json -> PAL_Master.png).</summary>
    public class PaletteTests
    {
        const string Dir = "Assets/Art/Textures/Palette/";

        static string Json => File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), Dir + "palette.json"));

        [Test]
        public void SwatchNames_AreUnique_AndHexValid()
        {
            var names = new HashSet<string>();
            foreach (Match m in Regex.Matches(Json, "\\[\"([a-z0-9_]+)\",\\s*\"(#[0-9A-Fa-f]{6})\""))
                Assert.IsTrue(names.Add(m.Groups[1].Value), "duplicate swatch " + m.Groups[1].Value);
            Assert.Greater(names.Count, 50);
        }

        [Test]
        public void Rows_FitTheGrid()
        {
            var rows = Regex.Matches(Json, "\"family\"");
            Assert.LessOrEqual(rows.Count, 16, "more than 16 palette rows");
            foreach (var row in Json.Split(new[] { "\"family\"" }, System.StringSplitOptions.None))
                Assert.LessOrEqual(Regex.Matches(row, "\\[\"[a-z0-9_]+\",\\s*\"#").Count, 16, "row wider than 16 swatches");
        }

        [Test]
        public void Texture_IsPointFiltered_128()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "PAL_Master.png");
            Assert.IsNotNull(tex);
            Assert.AreEqual(128, tex.width);
            Assert.AreEqual(FilterMode.Point, tex.filterMode);
            Assert.AreEqual(1, tex.mipmapCount);
        }
    }
}
