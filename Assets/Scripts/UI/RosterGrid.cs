using System;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// The roster as a grid of tesserae (R9, the producer's playtest: "a square with their face"). A tessera gladiatoria
    /// was the bone token a gladiator got after his first fights: each tile is one, a cord hole in its head, the portrait
    /// set into it and the name carved below. Open: ivory. Locked: dark and cracked, the portrait a black silhouette,
    /// "???". Picked: a gold frame, a gold wreath hung on the cord and a small lift. Hovered: a pale frame.
    /// The gladiator select and the Codex's GLADIATORS tab both use it. Nothing slides: the grid is scaled to fit its
    /// column (MenuLayout). Built once; picks and hovers only recolour and move tiles (no allocation).
    /// Open tiles' buttons are named "Card" and locked ones "Locked" (a test clicks the earned gladiators' cards).
    /// </summary>
    public sealed class RosterGrid : IUiHoverTarget
    {
        public struct Slot
        {
            /// <summary>Null: a gladiator that isn't data yet (MainMenu.locked).</summary>
            public GladiatorDef Def;
            public bool Open;
            /// <summary>The unlock hint when there's no Def to read it from.</summary>
            public string Hint;
        }

        // the game's palette (Assets/Art/Textures/Palette/palette_index.md)
        public static readonly Color Bone = new Color32(0xE3, 0xD7, 0xBC, 0xFF);
        public static readonly Color BoneLocked = new Color32(0x4A, 0x45, 0x40, 0xFF);
        public static readonly Color Gold = new Color32(0xE2, 0xB3, 0x3C, 0xFF);
        public static readonly Color Pale = new Color32(0xF5, 0xF1, 0xE8, 0xC0);
        public static readonly Color Carved = new Color32(0x4A, 0x33, 0x24, 0xFF);
        public static readonly Color CarvedLocked = new Color(0.9f, 0.86f, 0.78f, 0.45f);
        static readonly Color Backdrop = new Color32(0xD2, 0xAE, 0x78, 0xFF);
        static readonly Color BackdropLocked = new Color32(0x5F, 0x5C, 0x58, 0xFF);
        static readonly Color WindowRim = new Color32(0x94, 0x65, 0x3F, 0xFF);
        static readonly Color WindowRimLocked = new Color32(0x1E, 0x1B, 0x19, 0xFF);
        /// <summary>A locked picture: the portrait tinted to ink.</summary>
        public static readonly Color Silhouette = new Color32(0x1E, 0x1B, 0x19, 0xFF);

        const float Face = 128f, Lift = 4f, FrameBand = 7f; // lift + band stay inside the 12-unit gap

        readonly TMP_FontAsset _font;
        Slot[] _slots = Array.Empty<Slot>();
        RectTransform[] _tiles = Array.Empty<RectTransform>(), _frames = Array.Empty<RectTransform>();
        Image[] _frameImages = Array.Empty<Image>(), _wreaths = Array.Empty<Image>(), _dots = Array.Empty<Image>();
        Button[] _buttons = Array.Empty<Button>();
        TextMeshProUGUI[] _names = Array.Empty<TextMeshProUGUI>();
        Vector2[] _home = Array.Empty<Vector2>();
        int _pick = -1, _hover = -1;
        Action<int> _onClick;

        public RosterGrid(TMP_FontAsset font) => _font = font;

        /// <summary>The roster in the screens' order: the earned gladiators, then the locked ones (both in roster order),
        /// then those that aren't data yet.</summary>
        public static Slot[] Slots(GladiatorDef[] gladiators, MainMenu.Locked[] locked)
        {
            gladiators ??= Array.Empty<GladiatorDef>();
            locked ??= Array.Empty<MainMenu.Locked>();
            int n = locked.Length;
            foreach (var g in gladiators) if (g != null) n++;
            var slots = new Slot[n];
            int k = 0;
            foreach (var g in gladiators)
                if (g != null && MainMenu.IsAvailable(g)) slots[k++] = new Slot { Def = g, Open = true };
            foreach (var g in gladiators)
                if (g != null && !MainMenu.IsAvailable(g)) slots[k++] = new Slot { Def = g, Open = false };
            foreach (var l in locked) slots[k++] = new Slot { Def = null, Open = false, Hint = l.hint };
            return slots;
        }

        public RectTransform Root { get; private set; }
        public int Count => _slots.Length;
        public int Columns { get; private set; } = MenuLayout.Columns;
        public int Pick => _pick;
        public int Hovered => _hover;
        public bool IsOpen(int i) => i >= 0 && i < _slots.Length && _slots[i].Open;
        public GladiatorDef Def(int i) => i >= 0 && i < _slots.Length ? _slots[i].Def : null;
        public string Hint(int i)
        {
            if (i < 0 || i >= _slots.Length) return "";
            var d = _slots[i].Def;
            return d != null && d.unlock != null ? d.unlock.hint : _slots[i].Hint ?? "";
        }
        /// <summary>Tests: a tile's rect (its pixels on screen: the menus use a screen-space overlay canvas).</summary>
        public RectTransform Tile(int i) => _tiles[i];
        public Button Button(int i) => _buttons[i];
        public bool HasWreath(int i) => i >= 0 && i < _wreaths.Length && _wreaths[i].enabled;
        public bool HasNew(int i) => i >= 0 && i < _dots.Length && _dots[i].enabled;
        /// <summary>The name carved on tile `i` ("???" while locked).</summary>
        public string NameOf(int i) => i >= 0 && i < _names.Length ? _names[i].text : null;

        /// <summary>Builds every tile once. The grid's top-left corner sits at `pos` from `anchor`.</summary>
        public void Build(Transform parent, Vector2 anchor, Vector2 pos, Slot[] slots, int columns, CodexConfig codex, Action<int> onClick)
        {
            _slots = slots ?? Array.Empty<Slot>();
            Columns = Mathf.Max(1, columns);
            _onClick = onClick;
            int n = _slots.Length;
            var full = GridMath.Size(Mathf.Max(n, Columns), Columns, MenuLayout.Tile, MenuLayout.Gap);
            var used = GridMath.Size(Mathf.Max(n, 1), Columns, MenuLayout.Tile, MenuLayout.Gap);
            Root = UiFactory.Rect("Roster", parent, anchor, new Vector2(0f, 1f), pos, new Vector2(full.x, used.y));
            _tiles = new RectTransform[n];
            _frames = new RectTransform[n];
            _frameImages = new Image[n];
            _wreaths = new Image[n];
            _dots = new Image[n];
            _buttons = new Button[n];
            _names = new TextMeshProUGUI[n];
            _home = new Vector2[n];
            for (int i = 0; i < n; i++) BuildTile(i, codex);
            Refresh();
        }

        void BuildTile(int i, CodexConfig codex)
        {
            var s = _slots[i];
            var tile = MenuLayout.Tile;
            var topLeft = new Vector2(0f, 1f);
            var mid = new Vector2(0.5f, 0.5f);
            var c = GridMath.Cell(i, Columns, tile, MenuLayout.Gap);
            _home[i] = c;

            // the frame behind: a band round the tessera while it's hovered or picked
            var frame = UiFactory.Image("Frame", Root, Color.clear, topLeft, mid, c, tile + new Vector2(FrameBand * 2f, FrameBand * 2f), PixelIcons.Plate);
            frame.type = Image.Type.Sliced;
            _frameImages[i] = frame;
            _frames[i] = frame.rectTransform;

            // the tessera; its Button takes the click (the pictures and words on it ignore the pointer)
            var bone = UiFactory.Image(s.Open ? "Card" : "Locked", Root, s.Open ? Bone : BoneLocked, topLeft, mid, c, tile, PixelIcons.Tessera);
            bone.type = Image.Type.Sliced;
            bone.raycastTarget = true;
            var button = bone.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            button.onClick.AddListener(() => _onClick?.Invoke(index));
            var hover = bone.gameObject.AddComponent<UiHover>();
            hover.Target = this;
            hover.Id = i;
            _buttons[i] = button;
            _tiles[i] = bone.rectTransform;

            var t = bone.transform;
            float top = tile.y * 0.5f;
            UiFactory.Image("Hole", t, Color.white, mid, mid, new Vector2(0f, top - 11f), new Vector2(15f, 15f), PixelIcons.Hole);
            float wy = top - 20f - (Face + 4f) * 0.5f;
            UiFactory.Image("Window", t, s.Open ? WindowRim : WindowRimLocked, mid, mid, new Vector2(0f, wy), new Vector2(Face + 4f, Face + 4f), PixelIcons.White);
            UiFactory.Image("Backdrop", t, s.Open ? Backdrop : BackdropLocked, mid, mid, new Vector2(0f, wy), new Vector2(Face, Face), PixelIcons.White);
            var face = UiFactory.Rect("Portrait", t, mid, mid, new Vector2(0f, wy), new Vector2(Face, Face)).gameObject.AddComponent<RawImage>();
            face.raycastTarget = false;
            SetPortrait(face, s.Def, s.Open, codex);
            if (!s.Open)
                UiFactory.Image("Cracks", t, Color.white, mid, mid, new Vector2(0f, wy + 2f), new Vector2(Face * 0.75f, Face), PixelIcons.Crack);

            var name = UiFactory.Text("Name", t, s.Open ? TileName(s.Def) : "???", 18f, s.Open ? Carved : CarvedLocked, TextAlignmentOptions.Center,
                mid, mid, new Vector2(0f, -top + 23f), new Vector2(tile.x - 16f, 28f), _font, null);
            name.enableAutoSizing = true;
            name.fontSizeMin = 11f;
            name.fontSizeMax = 18f;
            _names[i] = name;

            var wreath = UiFactory.Image("Wreath", t, Color.white, mid, mid, new Vector2(0f, top - 6f), new Vector2(48f, 36f), PixelIcons.Wreath);
            wreath.enabled = false;
            _wreaths[i] = wreath;
            var dot = UiFactory.Image("New", t, Gold, mid, mid, new Vector2(tile.x * 0.5f - 16f, top - 16f), new Vector2(14f, 14f), PixelIcons.White);
            dot.enabled = false;
            _dots[i] = dot;
        }

        /// <summary>"The Pit Goblin" → "PIT GOBLIN": carved capitals, without the article (the tile is narrow).</summary>
        public static string TileName(GladiatorDef g)
        {
            if (g == null) return "???";
            string n = g.displayName ?? g.id;
            if (n.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) n = n.Substring(4);
            return n.ToUpperInvariant();
        }

        /// <summary>
        /// The picture for `g`: its baked portrait (or silhouette while locked), else its cell in the Codex atlas (full
        /// body, until the baker has run), else a generic bust. Locked pictures are ink-black.
        /// </summary>
        public static void SetPortrait(RawImage img, GladiatorDef g, bool open, CodexConfig codex)
        {
            if (g != null && !open && g.portraitSilhouette != null) { Show(img, g.portraitSilhouette, Color.white); return; }
            if (g != null && g.portrait != null) { Show(img, g.portrait, open ? Color.white : Silhouette); return; }
            if (g != null && codex != null && codex.TryPortrait("gl:" + g.id, out var uv))
            {
                img.texture = codex.portraits;
                img.uvRect = uv;
                img.color = open ? Color.white : Silhouette;
                return;
            }
            Show(img, PixelIcons.Bust, open ? Carved : Silhouette);
        }

        /// <summary>A sprite's pixels on a RawImage (single-sprite textures: the whole texture).</summary>
        public static void Show(RawImage img, Sprite s, Color tint)
        {
            var tex = s.texture;
            var r = s.textureRect;
            img.texture = tex;
            img.uvRect = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            img.color = tint;
        }

        public void SetPick(int i)
        {
            _pick = _slots.Length == 0 ? -1 : Mathf.Clamp(i, 0, _slots.Length - 1);
            Refresh();
        }

        /// <summary>The Codex's NEW mark on a tile (shown for this visit).</summary>
        public void SetNew(int i, bool on)
        {
            if (i >= 0 && i < _dots.Length) _dots[i].enabled = on;
        }

        /// <summary>Scales the whole grid (MenuLayout.GridScale) about its top-left corner.</summary>
        public void Fit(float scale)
        {
            if (Root != null) Root.localScale = new Vector3(scale, scale, 1f);
        }

        public void Hover(int id, bool on)
        {
            if (on) _hover = id;
            else if (_hover == id) _hover = -1;
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                bool picked = i == _pick;
                _frameImages[i].color = picked ? Gold : i == _hover ? Pale : Color.clear;
                _wreaths[i].enabled = picked && _slots[i].Open;
                var at = _home[i] + (picked ? new Vector2(0f, Lift) : Vector2.zero);
                if (_tiles[i].anchoredPosition != at)
                {
                    _tiles[i].anchoredPosition = at;
                    _frames[i].anchoredPosition = at;
                }
            }
        }
    }
}
