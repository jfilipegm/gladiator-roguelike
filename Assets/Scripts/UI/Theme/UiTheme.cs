using System;
using TMPro;
using UnityEngine;

namespace Gladiator.UI
{
    /// <summary>The five type roles (docs/tracks/U1-design.md §2.1). Append, never reorder.</summary>
    public enum TextRole : byte { Number, Shout, Title, Body, Plain }

    /// <summary>The three TMP material looks (spec §2.3): flat, outlined over the 3D scene, engraved into a face.</summary>
    public enum TextLook : byte { Flat, Outline, Engraved }

    /// <summary>A role's font and its three material presets (all sharing the font's atlas).</summary>
    [Serializable]
    public struct RoleFont
    {
        public TMP_FontAsset font;
        public Material flat, outline, engraved;
        [Tooltip("TMP character spacing (em/100): Title +4, rarity band +12 (set by the caller), Number +2, Shout +1.")]
        public float spacing;
    }

    /// <summary>
    /// U1: the Roman UI look in one asset: the role fonts and their material presets, the colour tokens, the frame
    /// atlas + index (PixelSrc/ui_frames.py) and the banner timings. Read at runtime from Resources
    /// (`Assets/Resources/UiTheme.asset`, made and re-linked by `Gladiator/Setup/UI/All`), so retuning it restyles every
    /// screen without touching a scene. With no asset, `Current` is an instance with the spec's defaults and TMP's font.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladiator/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        public const string ResourcePath = "UiTheme";

        /// <summary>The typographic symbols the game writes ("→" in stat previews, "×" in multipliers, …), baked into every
        /// theme font that has them and into the FN_Symbols fallback. The contract's ∓ ◀ ▶ are left out: no shipped font
        /// draws them (LiberationSans lacks them too) and no UI string uses them; use an icon for arrows.</summary>
        public const string Symbols = "×·—–…→←↑↓’“”•−°²÷±≈√Σ■";

        [Header("Fonts")]
        public RoleFont number = new RoleFont { spacing = 2f };
        public RoleFont shout = new RoleFont { spacing = 1f };
        public RoleFont title = new RoleFont { spacing = 4f };
        public RoleFont body;
        public RoleFont plain;

        [Header("Frames")]
        public Texture2D frameAtlas;
        public TextAsset frameIndex;

        // spec §3's tokens (the mockup's values)
        [Header("Colours")]
        public Color night = new Color32(0x19, 0x15, 0x12, 0xFF);
        public Color stone = new Color32(0x26, 0x20, 0x1C, 0xFF);
        public Color stoneLight = new Color32(0x2F, 0x28, 0x23, 0xFF);
        public Color line = new Color32(0x4A, 0x3F, 0x36, 0xFF);
        public Color cardInside = new Color32(0x1C, 0x17, 0x14, 0xFF);
        public Color ink = new Color32(0xEF, 0xE6, 0xD6, 0xFF);
        public Color dust = new Color32(0xB3, 0xA6, 0x90, 0xFF);
        public Color outlineInk = new Color32(0x1A, 0x0F, 0x08, 0xFF);
        public Color bronze = new Color32(0xB0, 0x7A, 0x3B, 0xFF);
        public Color bronzeLight = new Color32(0xE0, 0xAE, 0x6C, 0xFF);
        public Color bronzeDark = new Color32(0x5A, 0x3A, 0x1B, 0xFF);
        public Color bronzeInk = new Color32(0x2A, 0x18, 0x0A, 0xFF);
        public Color gold = new Color32(0xE2, 0xB1, 0x4A, 0xFF);
        public Color goldBright = new Color32(0xFF, 0xCF, 0x4A, 0xFF);
        public Color crimson = new Color32(0x7A, 0x1F, 0x25, 0xFF);
        public Color crimsonLight = new Color32(0x9B, 0x2E, 0x33, 0xFF);
        public Color purple = new Color32(0x4B, 0x2A, 0x5C, 0xFF);
        public Color purpleDark = new Color32(0x2C, 0x17, 0x36, 0xFF);
        public Color purpleInk = new Color32(0xF6, 0xD9, 0x8C, 0xFF);
        public Color marble = new Color32(0xEC, 0xE7, 0xDE, 0xFF);
        public Color marbleShade = new Color32(0xDB, 0xD3, 0xC5, 0xFF);
        public Color marbleInk = new Color32(0x3D, 0x34, 0x2B, 0xFF);
        public Color waxWood = new Color32(0x7D, 0x57, 0x34, 0xFF);
        public Color wax = new Color32(0x25, 0x1E, 0x19, 0xFF);
        public Color waxInk = new Color32(0xD8, 0xC7, 0xA6, 0xFF);
        [Tooltip("Reserved for what hurts the player (CLAUDE.md §5): hazards, telegraphs, damage taken, edicts switched on.")]
        public Color danger = new Color32(0xFF, 0x54, 0x33, 0xFF);
        public Color gain = new Color32(0x7B, 0xE0, 0x8A, 0xFF);
        public Color loss = new Color32(0xF0, 0x80, 0x6A, 0xFF);
        public Color hp = new Color32(0xC8, 0x37, 0x2D, 0xFF);
        public Color hpDark = new Color32(0x8A, 0x24, 0x20, 0xFF);
        public Color xp = new Color32(0x4F, 0xE3, 0xD1, 0xFF);
        public Color favor = new Color32(0xFF, 0xC7, 0x40, 0xFF);
        public Color coin = new Color32(0xFF, 0xD6, 0x4D, 0xFF);

        [Header("Banner (seconds, degrees, share of the cloth's height)")]
        public float bannerDrop = 0.32f;
        public float bannerSettle = 0.12f;
        public float bannerSway = 0.7f;
        public float bannerHold = 2f;
        public float bannerRoll = 0.28f;
        public float bannerQuickRoll = 0.15f;
        public float bannerSwayDegrees = 1.4f;
        public float bannerOvershoot = 0.05f;
        [Tooltip("P3: the cloth's wave while it drops and settles (it replaced the sway): height as a share of the cloth's height.")]
        public float bannerWaveAmplitude = 0.07f;
        [Tooltip("P3: one wave's length as a share of the cloth's width.")]
        public float bannerWaveLength = 0.55f;
        [Tooltip("P3: how many waves a second travel across the cloth.")]
        public float bannerWaveSpeed = 1.8f;
        [Tooltip("P3: seconds for the wave to fade to a third after the drop ends (0 = it stops with the drop).")]
        public float bannerWaveDecay = 0.45f;

        static UiTheme _current;

        /// <summary>The theme in Resources, loaded once; with none, an instance with the spec's defaults. Cached with
        /// Unity's null check (not ??=), like PixelIcons: a scene load can unload it, and the statics outlive a play
        /// session when domain reload is off.</summary>
        public static UiTheme Current
        {
            get
            {
                if (_current != null) return _current;
                _current = Resources.Load<UiTheme>(ResourcePath);
                if (_current == null) _current = CreateDefault();
                return _current;
            }
        }

        /// <summary>A theme with the spec's defaults and no fonts or frames (what `Current` is without the asset).</summary>
        public static UiTheme CreateDefault()
        {
            var t = CreateInstance<UiTheme>();
            t.name = "UiTheme (defaults)";
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return t;
        }

        /// <summary>Drops the cached theme and the frame sprites cut from it (setup scripts, tests).</summary>
        public static void Reload()
        {
            _current = null;
            UiFrames.Reload();
        }

        public RoleFont Role(TextRole r)
        {
            switch (r)
            {
                case TextRole.Number: return number;
                case TextRole.Shout: return shout;
                case TextRole.Title: return title;
                case TextRole.Body: return body;
                default: return plain;
            }
        }

        /// <summary>The role's font, else the plain font, else TMP's default font asset.</summary>
        public TMP_FontAsset FontFor(TextRole r)
        {
            var f = Role(r).font;
            if (f != null) return f;
            if (plain.font != null) return plain.font;
            return TMP_Settings.defaultFontAsset;
        }

        /// <summary>The look's preset, else the role's flat material, else the font's own material. A role without a font
        /// uses the plain role's materials (a material must match the font it draws).</summary>
        public Material MaterialFor(TextRole r, TextLook look)
        {
            var rf = Role(r);
            if (rf.font == null) rf = plain;
            if (rf.font != null)
            {
                var m = look == TextLook.Outline ? rf.outline : look == TextLook.Engraved ? rf.engraved : rf.flat;
                if (m != null) return m;
                if (rf.flat != null) return rf.flat;
                return rf.font.material;
            }
            var def = TMP_Settings.defaultFontAsset;
            return def != null ? def.material : null;
        }

        /// <summary>The role's character spacing (0 when the role has no font of its own: the fallback isn't tuned).</summary>
        public float SpacingFor(TextRole r)
        {
            var rf = Role(r);
            return rf.font != null ? rf.spacing : 0f;
        }

        const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        const string Digits = "0123456789";
        static string _ascii;

        /// <summary>The glyphs a role's static atlas must hold (spec §2.3): Number = digits, letters, space and
        /// <c>+-%:/.,!?[]()'</c>; Shout = letters, digits, space and <c>!?.,'-:</c>; Title and Body = printable ASCII;
        /// Plain = none (LiberationSans stays dynamic).</summary>
        public static string Glyphs(TextRole r)
        {
            switch (r)
            {
                case TextRole.Number: return Digits + Letters + " +-%:/.,!?[]()'";
                case TextRole.Shout: return Letters + Digits + " !?.,'-:";
                case TextRole.Title:
                case TextRole.Body:
                    if (_ascii == null)
                    {
                        var c = new char[127 - 32];
                        for (int i = 0; i < c.Length; i++) c[i] = (char)(32 + i);
                        _ascii = new string(c);
                    }
                    return _ascii;
                default: return "";
            }
        }
    }
}
