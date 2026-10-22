using System.Collections.Generic;
using System.IO;
using Gladiator.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Gladiator.EditorTools
{
    /// <summary>
    /// U1: the Roman UI theme's assets (docs/tracks/U1-contract.md "U1-A"). Menus under Gladiator/Setup/UI/, each
    /// idempotent and logging what it did:
    /// <list type="bullet">
    /// <item>Import UI Frames: the atlas from PixelSrc/ui_frames.py as point-filtered, uncompressed, no mipmaps (as the
    /// upgrade icons). Sprites are cut from it at runtime (UiFrames), so it stays a Default texture.</item>
    /// <item>Create Font Assets: one static SDF atlas per family (Luckiest Guy, Caesar Dressing, Marcellus SC,
    /// Marcellus) holding its role's glyphs and the symbols it has, FN_Symbols from LiberationSans for the rest, three
    /// material presets each (Flat, Outline, Engraved), and the licences copied into StreamingAssets. An existing font
    /// asset is kept (Rebuild Font Assets regenerates them all).</item>
    /// <item>Create UI Theme: Assets/Resources/UiTheme.asset if missing; every run re-links the fonts, presets, atlas
    /// and index by path but never touches colours or banner timings (they belong to the designer).</item>
    /// <item>All: the three in order.</item>
    /// </list>
    /// Regenerate the frame art with `python3 PixelSrc/ui_frames.py`, then run All.
    /// </summary>
    public static class UiThemeSetup
    {
        public const string AtlasPath = "Assets/Art/Textures/UI/UiFrames.png";
        public const string IndexPath = "Assets/Art/Textures/UI/UiFrames.txt";
        public const string FontsDir = "Assets/Art/Fonts";
        public const string TmpDir = "Assets/Art/Fonts/TMP";
        public const string ThemePath = "Assets/Resources/UiTheme.asset";
        public const string LicenceDir = "Assets/StreamingAssets/Licenses/Fonts";
        public const string SymbolsName = "FN_Symbols SDF";
        public const string SymbolsSource = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        public const string SymbolsLicence = "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt";

        // the static atlases (spec §2.3)
        const int SamplingSize = 72, AtlasPadding = 9, AtlasSize = 1024;

        // the material looks: tune them in the gallery, then here
        const float OutlineWidth = 0.2f;          // dark ink rim (SDF units)
        const float OutlineWidthThin = 0.32f;     // Marcellus / Marcellus SC: thin strokes need a wider rim to read over sky and sand
        const float OutlineFaceDilate = 0.1f;     // thickens the face so the rim doesn't eat thin strokes
        const float OutlineDropAlpha = 0.9f;      // the drop shadow under outlined text
        const float OutlineDropOffsetY = -0.6f;
        const float OutlineDropDilate = 0.2f;     // the drop has the outlined silhouette
        const float EngravedLightAlpha = 0.5f;    // the light line under text cut into marble / bronze
        const float EngravedOffsetY = -0.35f;

        /// <summary>A theme font family: its folder and TTF name, the licence file beside it, and the role it serves.</summary>
        public struct Family
        {
            public string Name, Licence;
            public TextRole Role;
            public string Ttf => FontsDir + "/" + Name + "/" + Name + "-Regular.ttf";
            public string LicencePath => FontsDir + "/" + Name + "/" + Licence;
            public string Asset => TmpDir + "/FN_" + Name + " SDF.asset";
            public string Preset(TextLook look) => TmpDir + "/FN_" + Name + " SDF - " + look + ".mat";
        }

        public static readonly Family[] Families =
        {
            new Family { Name = "LuckiestGuy", Licence = "LICENSE.txt", Role = TextRole.Number },
            new Family { Name = "CaesarDressing", Licence = "OFL.txt", Role = TextRole.Shout },
            new Family { Name = "MarcellusSC", Licence = "OFL.txt", Role = TextRole.Title },
            new Family { Name = "Marcellus", Licence = "OFL.txt", Role = TextRole.Body },
        };

        static string SymbolsAsset => TmpDir + "/" + SymbolsName + ".asset";

        [MenuItem("Gladiator/Setup/UI/All")]
        public static void All()
        {
            ImportFrames();
            CreateFontAssets();
            CreateTheme();
            Debug.Log("[UiThemeSetup] All done: frames imported, font assets ready, theme linked");
        }

        [MenuItem("Gladiator/Setup/UI/Import UI Frames")]
        public static bool ImportFrames()
        {
            var imp = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (imp == null)
            {
                Debug.LogError("[UiThemeSetup] no frame atlas at " + AtlasPath + " (run python3 PixelSrc/ui_frames.py)");
                return false;
            }
            imp.textureType = TextureImporterType.Default;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.sRGBTexture = true;
            imp.isReadable = false;
            imp.SaveAndReimport();
            AssetDatabase.ImportAsset(IndexPath);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            var index = AssetDatabase.LoadAssetAtPath<TextAsset>(IndexPath);
            var lines = new List<FrameEntry>();
            int n = index != null ? UiFrames.ParseIndex(index.text, lines) : 0;
            Debug.Log($"[UiThemeSetup] frame atlas imported ({(tex != null ? tex.width + "x" + tex.height : "?")}, point, uncompressed), index {n} lines");
            return tex != null && index != null;
        }

        [MenuItem("Gladiator/Setup/UI/Create Font Assets")]
        public static void CreateFontAssets() => MakeFonts(false);

        [MenuItem("Gladiator/Setup/UI/Rebuild Font Assets")]
        public static void RebuildFontAssets() => MakeFonts(true);

        static void MakeFonts(bool force)
        {
            Directory.CreateDirectory(TmpDir);
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CombatSetup.FontPath);
            var symbols = MakeFont(SymbolsSource, SymbolsAsset, UiTheme.Symbols, force, null);
            var fallbacks = new List<TMP_FontAsset>();
            if (symbols != null) fallbacks.Add(symbols); // fallbacks draw with the primary's look (TMP copies it)
            if (liberation != null) fallbacks.Add(liberation);
            CopyLicence(SymbolsLicence, "LiberationSans-OFL.txt");

            int made = 0;
            foreach (var f in Families)
            {
                var fa = MakeFont(f.Ttf, f.Asset, UiTheme.Glyphs(f.Role) + UiTheme.Symbols, force, fallbacks);
                if (fa == null) continue;
                MakePresets(fa, TmpDir + "/FN_" + f.Name + " SDF", f.Role == TextRole.Title || f.Role == TextRole.Body);
                CopyLicence(f.LicencePath, f.Name + "-" + f.Licence);
                made++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UiThemeSetup] font assets: {made} / {Families.Length} families ready in {TmpDir}, licences in {LicenceDir}");
        }

        /// <summary>A static SDF font asset with `chars` (the ones the font has) at `assetPath`. Keeps an existing one
        /// unless `force`. `fallbacks` (null = leave as is) becomes its fallback table.</summary>
        static TMP_FontAsset MakeFont(string ttfPath, string assetPath, string chars, bool force, List<TMP_FontAsset> fallbacks)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null && !force)
            {
                if (fallbacks != null)
                {
                    existing.fallbackFontAssetTable = new List<TMP_FontAsset>(fallbacks);
                    EditorUtility.SetDirty(existing);
                }
                Debug.Log("[UiThemeSetup] kept " + assetPath + " (Rebuild Font Assets regenerates it)");
                return existing;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError("[UiThemeSetup] missing font " + ttfPath + ": skipped (add the family's TTF and licence)");
                return null;
            }
            var fa = TMP_FontAsset.CreateFontAsset(font, SamplingSize, AtlasPadding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
                AtlasPopulationMode.Dynamic);
            if (fa == null)
            {
                Debug.LogError("[UiThemeSetup] TMP could not load " + ttfPath + " (Include Font Data must be on)");
                return null;
            }
            var name = Path.GetFileNameWithoutExtension(assetPath);
            fa.name = name;
            fa.TryAddCharacters(chars, out string missing, true);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                var tex = fa.atlasTextures[i];
                if (tex == null) continue;
                tex.name = name + " Atlas" + (i > 0 ? " " + i : "");
                AssetDatabase.AddObjectToAsset(tex, fa);
            }
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasPopulationMode = AtlasPopulationMode.Static; // no glyph is ever added mid-run (zero GC)
            if (fallbacks != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>(fallbacks);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            if (fa.atlasTextures.Length > 1)
                Debug.LogWarning($"[UiThemeSetup] {name} needed {fa.atlasTextures.Length} atlases: lower the sampling size");
            Debug.Log($"[UiThemeSetup] made {assetPath}: {fa.characterTable.Count} glyphs, static" +
                      (string.IsNullOrEmpty(missing) ? "" : $"; not in the font (left to the fallbacks): {missing}"));
            return fa;
        }

        /// <summary>The three looks as material assets sharing the font's atlas: `<base> - Flat/Outline/Engraved.mat`.
        /// Existing materials are updated in place (their GUIDs stay).</summary>
        static void MakePresets(TMP_FontAsset fa, string basePath, bool thin)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            Color ink = theme != null ? theme.outlineInk : (Color)new Color32(0x1A, 0x0F, 0x08, 0xFF);
            foreach (TextLook look in System.Enum.GetValues(typeof(TextLook)))
            {
                string path = basePath + " - " + look + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = mat == null;
                if (isNew) mat = new Material(fa.material);
                else
                {
                    mat.shader = fa.material.shader;
                    mat.CopyPropertiesFromMaterial(fa.material);
                }
                mat.name = Path.GetFileNameWithoutExtension(path);
                mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
                mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
                mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
                if (look == TextLook.Outline)
                {
                    mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                    mat.SetFloat(ShaderUtilities.ID_FaceDilate, OutlineFaceDilate);
                    mat.SetFloat(ShaderUtilities.ID_OutlineWidth, thin ? OutlineWidthThin : OutlineWidth);
                    mat.SetColor(ShaderUtilities.ID_OutlineColor, ink);
                    mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(ink.r, ink.g, ink.b, OutlineDropAlpha));
                    mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, OutlineDropOffsetY);
                    mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, OutlineDropDilate);
                }
                else if (look == TextLook.Engraved)
                {
                    mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(1f, 1f, 1f, EngravedLightAlpha));
                    mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, EngravedOffsetY);
                }
                ShaderUtilities.UpdateShaderRatios(mat);
                if (isNew) AssetDatabase.CreateAsset(mat, path);
                else EditorUtility.SetDirty(mat);
            }
        }

        static void CopyLicence(string source, string target)
        {
            if (!File.Exists(source))
            {
                Debug.LogError("[UiThemeSetup] missing licence " + source + ": a font may not ship without its licence");
                return;
            }
            Directory.CreateDirectory(LicenceDir);
            File.Copy(source, LicenceDir + "/" + target, true);
        }

        [MenuItem("Gladiator/Setup/UI/Create UI Theme")]
        public static UiTheme CreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            bool created = theme == null;
            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                theme = ScriptableObject.CreateInstance<UiTheme>(); // the spec's colours and timings
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            // re-link fonts, presets and frames by path every run; colours, spacing and timings stay the designer's
            foreach (var f in Families)
            {
                var rf = theme.Role(f.Role);
                rf.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f.Asset);
                rf.flat = AssetDatabase.LoadAssetAtPath<Material>(f.Preset(TextLook.Flat));
                rf.outline = AssetDatabase.LoadAssetAtPath<Material>(f.Preset(TextLook.Outline));
                rf.engraved = AssetDatabase.LoadAssetAtPath<Material>(f.Preset(TextLook.Engraved));
                if (rf.font == null) Debug.LogWarning("[UiThemeSetup] no font asset for " + f.Role + " (run Create Font Assets)");
                SetRole(theme, f.Role, rf);
            }
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CombatSetup.FontPath);
            var plain = theme.plain;
            plain.font = liberation;
            plain.flat = liberation != null ? liberation.material : null;
            plain.outline = AssetDatabase.LoadAssetAtPath<Material>(CombatSetup.FontOutlinePath);
            plain.engraved = null;
            theme.plain = plain;
            theme.frameAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            theme.frameIndex = AssetDatabase.LoadAssetAtPath<TextAsset>(IndexPath);
            if (theme.frameAtlas == null) Debug.LogWarning("[UiThemeSetup] no frame atlas (run Import UI Frames)");
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            UiTheme.Reload();
            Debug.Log($"[UiThemeSetup] {(created ? "created" : "re-linked")} {ThemePath}: fonts, presets, frame atlas and index");
            return theme;
        }

        static void SetRole(UiTheme t, TextRole r, RoleFont rf)
        {
            switch (r)
            {
                case TextRole.Number: t.number = rf; break;
                case TextRole.Shout: t.shout = rf; break;
                case TextRole.Title: t.title = rf; break;
                case TextRole.Body: t.body = rf; break;
                default: t.plain = rf; break;
            }
        }
    }
}
