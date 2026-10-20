using System;
using System.Collections.Generic;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// The Codex (Phase 5.6, docs/UNLOCKS.md; M3.6 was a three-column stub). Seven tabs built from data:
    /// - RECORDS: the save's numbers;
    /// - GLADIATORS: the roster (locked ones as black silhouettes with their hint);
    /// - ARSENAL: weapons, evolutions, Boons, synergies (evolutions and synergies stay "???" until found; locked ones
    ///   say how they're earned);
    /// - BESTIARY: every enemy and boss (unseen = a silhouette);
    /// - CHALLENGES: goals with progress bars and rewards;
    /// - SECRETS: found ones explained, the rest a hint;
    /// - MARKET: Boons bought with Renown.
    /// Entries opened since the Codex was last viewed are NEW (the menu's button counts them); viewing a page marks
    /// them seen, saved on close. Pages instead of scrolling, for the gamepad: left/right = tab, up/down = page (in the
    /// Market: the item), Enter = buy. Menu-only UI: it allocates freely when a page is built.
    /// </summary>
    public sealed class CodexPanel
    {
        public enum Tab { Records, Gladiators, Arsenal, Bestiary, Challenges, Secrets, Market }
        static readonly string[] TabNames = { "RECORDS", "GLADIATORS", "ARSENAL", "BESTIARY", "CHALLENGES", "SECRETS", "MARKET" };
        const int TabCount = 7;

        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.78f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.42f);
        static readonly Color CardColor = new Color(0.08f, 0.1f, 0.14f, 1f); // opaque: the NEW/focus border must not tint it
        static readonly Color Border = new Color(0.3f, 0.3f, 0.34f, 0.9f);
        static readonly Color Done = new Color(0.43f, 0.9f, 0.5f);
        static readonly Color HintGold = new Color(1f, 0.84f, 0.3f, 0.7f);
        static readonly Color Silhouette = new Color(0f, 0f, 0f, 0.9f);

        struct Entry
        {
            public string Id;            // Codex-seen id ("" = never NEW)
            public string Title, Kind, Body, Footer;
            public bool Open;            // shown in full; else dark picture / ???
            public bool Lit;             // picture and text in colour though not open (Market: see what you buy)
            public float Progress;       // < 0: no bar
            public string ProgressText;
            public Texture2D Icon;       // upgrade/synergy atlas
            public int IconRow;
            public string Portrait;      // portrait atlas id
            public UnlockDef Buy;        // Market
            public bool New;
        }

        readonly TMP_FontAsset _font;
        readonly Material _fontMat;
        SaveData _save;
        CodexConfig _codex;
        MetaConfig _meta;
        GladiatorDef[] _gladiators;
        MainMenu.Locked[] _locked;
        Action _back;
        Transform _root;
        RectTransform _content;
        TextMeshProUGUI _renown, _page;
        Button[] _tabButtons;
        Image[] _tabFrames, _tabDots;
        readonly List<Entry>[] _tabs = new List<Entry>[TabCount];
        Tab _tab;
        int _pageIndex, _focus;

        public CodexPanel(TMP_FontAsset font, Material fontMat)
        {
            _font = font;
            _fontMat = fontMat;
        }

        public Tab Current => _tab;
        public int Page => _pageIndex;
        public int PageCount => Mathf.Max(1, Mathf.CeilToInt(Entries(_tab).Count / (float)PerPage(_tab)));
        /// <summary>Entries open but never shown in the Codex (the menu's "CODEX • n NEW").</summary>
        public int NewCount { get; private set; }
        public int EntryCount(Tab t) => Entries(t).Count;
        /// <summary>Tests: an entry's title, open state and NEW flag on the current tab.</summary>
        public bool TryEntry(Tab t, int i, out string title, out bool open, out bool isNew)
        {
            var list = Entries(t);
            title = null; open = isNew = false;
            if (i < 0 || i >= list.Count) return false;
            title = list[i].Title; open = list[i].Open; isNew = list[i].New;
            return true;
        }
        public bool TabHasNew(Tab t) { foreach (var e in Entries(t)) if (e.New) return true; return false; }
        /// <summary>NEW entries not shown yet this visit.</summary>
        public int RemainingNew
        {
            get
            {
                int n = 0;
                for (int i = 0; i < TabCount; i++) foreach (var e in Entries((Tab)i)) if (e.New) n++;
                return n;
            }
        }

        List<Entry> Entries(Tab t) => _tabs[(int)t] ?? (_tabs[(int)t] = new List<Entry>());

        public void Build(Transform root, SaveData save, CodexConfig codex, MetaConfig meta, GladiatorDef[] gladiators, MainMenu.Locked[] locked, Action back)
        {
            _root = root;
            _save = save;
            _codex = codex;
            _meta = meta != null ? meta : codex != null ? codex.meta : null;
            _gladiators = gladiators ?? Array.Empty<GladiatorDef>();
            _locked = locked ?? Array.Empty<MainMenu.Locked>();
            _back = back;
            var mid = new Vector2(0.5f, 0.5f);

            Text(root, "CODEX", 64, Gold, new Vector2(-560, 430), new Vector2(500, 90), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            _renown = Text(root, "", 34, Gold, new Vector2(560, 430), new Vector2(500, 60), TextAlignmentOptions.Right);

            _tabButtons = new Button[TabCount];
            _tabFrames = new Image[TabCount];
            _tabDots = new Image[TabCount];
            for (int i = 0; i < TabCount; i++)
            {
                int t = i;
                var pos = new Vector2(-690f + i * 230f, 345f);
                _tabButtons[i] = UiFactory.Button("Tab" + TabNames[i], root, TabNames[i], mid, mid, pos, new Vector2(218, 62), _font, _fontMat, () => Select((Tab)t), 26f);
                var frame = UiFactory.Image("Frame", _tabButtons[i].transform, Gold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, -4), new Vector2(200, 6), PixelIcons.White);
                frame.raycastTarget = false;
                _tabFrames[i] = frame;
                var dot = UiFactory.Image("New", _tabButtons[i].transform, Gold, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12, -12), new Vector2(14, 14), PixelIcons.White);
                dot.raycastTarget = false;
                _tabDots[i] = dot;
            }

            _content = UiFactory.Rect("Content", root, mid, mid, new Vector2(0, -40), new Vector2(1600, 660));
            UiFactory.Button("Back", root, "BACK", mid, mid, new Vector2(-640, -440), new Vector2(220, 66), _font, _fontMat, () => Close(), 30f);
            UiFactory.Button("Prev", root, "<", mid, mid, new Vector2(-150, -440), new Vector2(70, 66), _font, _fontMat, () => Turn(-1), 34f);
            UiFactory.Button("Next", root, ">", mid, mid, new Vector2(150, -440), new Vector2(70, 66), _font, _fontMat, () => Turn(1), 34f);
            _page = Text(root, "", 26, Soft, new Vector2(0, -440), new Vector2(220, 50), TextAlignmentOptions.Center);
            var keys = Text(root, "Left / Right: tab    Up / Down: page    Esc / B: back", 22, Dim, new Vector2(560, -440), new Vector2(560, 40), TextAlignmentOptions.Right);
            keys.fontStyle = FontStyles.Normal;

            Collect();
            Select(Tab.Records);
        }

        // ------------------------------------------------------------------ data → entries

        void Collect()
        {
            for (int i = 0; i < TabCount; i++) Entries((Tab)i).Clear();
            CollectGladiators();
            CollectArsenal();
            CollectBestiary();
            CollectUnlocks();
            NewCount = 0;
            for (int i = 0; i < TabCount; i++)
            {
                var list = Entries((Tab)i);
                for (int k = 0; k < list.Count; k++)
                {
                    var e = list[k];
                    e.New = e.Open && !string.IsNullOrEmpty(e.Id) && !MetaProgress.Seen(e.Id, _save);
                    list[k] = e;
                    if (e.New) NewCount++;
                }
            }
            _renown.text = "RENOWN " + _save.renown;
        }

        void CollectGladiators()
        {
            var list = Entries(Tab.Gladiators);
            foreach (var g in _gladiators)
            {
                if (g == null) continue;
                bool open = MainMenu.IsAvailable(g);
                list.Add(new Entry
                {
                    Id = g.unlock != null ? "gl:" + g.id : "",
                    Title = open ? g.displayName : "???",
                    Kind = "GLADIATOR",
                    Body = open ? g.description : g.unlock != null ? g.unlock.hint : "???",
                    Footer = open ? "Wins: " + SaveData.Count(_save.gladiatorWins, g.id) : "",
                    Open = open, Progress = -1f, Portrait = "gl:" + g.id,
                });
            }
            foreach (var l in _locked)
                list.Add(new Entry { Id = "", Title = "???", Kind = "GLADIATOR", Body = l.hint, Footer = "", Open = false, Progress = -1f, Portrait = "" });
        }

        void CollectArsenal()
        {
            var list = Entries(Tab.Arsenal);
            var cat = _codex != null ? _codex.catalog : null;
            if (cat == null) return;
            var text = new CharBuffer(384);
            void Add(UpgradeDef d, string kind)
            {
                var holder = Holder(d);
                bool locked = holder != null && !_save.IsUnlocked(holder.id);
                bool secret = locked && holder.category == UnlockCategory.Secret;
                long taken = SaveData.Count(_save.upgradesTaken, d.id);
                string body;
                if (locked) body = holder.category == UnlockCategory.Market ? "In the Market: " + holder.renownCost + " Renown."
                    : secret ? "A secret. " + holder.hint : "Challenge: " + holder.hint;
                else
                {
                    if (_codex.upgrades != null) CardText.Describe(new CardOffer { Def = d, Rarity = Rarity.Common, Level = 1 }, _codex.upgrades, text);
                    else text.Clear().Append(d.weapon != null ? d.weapon.description : "");
                    body = Plain(text.ToString());
                }
                list.Add(new Entry
                {
                    Id = holder != null ? "up:" + d.id : "",
                    Title = secret ? "???" : d.displayName, Kind = kind, Body = body,
                    Footer = locked ? "" : taken > 0 ? "Taken in " + taken + (taken == 1 ? " run" : " runs") : "Not taken yet",
                    Open = !locked, Progress = -1f, Icon = d.icon, IconRow = d.iconRow,
                });
            }
            foreach (var d in cat.upgrades) if (d != null && d.kind == UpgradeKind.Weapon && !d.IsEvolution) Add(d, "WEAPON " + Tags(d.tags));
            foreach (var d in cat.upgrades)
            {
                if (d == null || !d.IsEvolution) continue;
                long made = SaveData.Count(_save.evolutions, d.id);
                bool found = made > 0;
                list.Add(new Entry
                {
                    Id = "ev:" + d.id,
                    Title = found ? d.displayName : "???",
                    Kind = "EVOLUTION",
                    Body = found ? d.weapon != null ? d.weapon.description : "" : "Max the " + (d.evolvesFrom != null ? d.evolvesFrom.displayName : "weapon") + " and own the right Boon.",
                    Footer = found ? d.evolvesFrom.displayName + " + " + d.catalyst.displayName + "  ·  made " + made + "×" : "",
                    Open = found, Progress = -1f, Icon = d.icon, IconRow = d.iconRow,
                });
            }
            foreach (var d in cat.upgrades) if (d != null && d.kind == UpgradeKind.Boon) Add(d, "BOON " + Tags(d.tags));
            if (cat.synergies != null)
                foreach (var s in cat.synergies)
                {
                    if (s == null) continue;
                    long runs = SaveData.Count(_save.synergies, s.id);
                    bool found = runs > 0;
                    list.Add(new Entry
                    {
                        Id = "sy:" + s.id,
                        Title = found ? s.displayName : "???",
                        Kind = "SYNERGY " + CardText.TagName(s.tag),
                        Body = found ? s.description : s.count + " different " + CardText.TagName(s.tag) + " upgrades in one run.",
                        Footer = found ? "Switched on in " + runs + (runs == 1 ? " run" : " runs") : "",
                        Open = found, Progress = -1f, Icon = s.icon, IconRow = s.iconRow,
                    });
                }
        }

        void CollectBestiary()
        {
            var list = Entries(Tab.Bestiary);
            if (_codex == null || _codex.bestiary == null) return;
            foreach (var b in _codex.bestiary)
            {
                if (b.enemy == null) continue;
                long kills = b.boss != null ? SaveData.Count(_save.bossKills, b.boss.id) : SaveData.Count(_save.enemyKills, b.enemy.id);
                // fought in its arena = seen (saves from before Phase 5.6 have no per-enemy kills)
                bool seen = kills > 0 || (_save.runs > 0 && _save.deepestArena >= b.tier);
                string name = b.boss != null ? b.boss.displayName + ", " + b.boss.title : b.enemy.displayName;
                list.Add(new Entry
                {
                    Id = "en:" + b.enemy.id,
                    Title = seen ? Title(name) : "???",
                    Kind = (b.boss != null ? "BOSS · " : "") + ArenaName(b.tier),
                    Body = seen ? b.blurb : "Not met yet.",
                    Footer = seen ? (b.boss != null ? "Defeated: " : "Killed: ") + kills.ToString("N0") : "",
                    Open = seen, Progress = -1f, Portrait = "en:" + b.enemy.id,
                });
            }
        }

        void CollectUnlocks()
        {
            if (_meta == null || _meta.unlocks == null) return;
            foreach (var u in _meta.unlocks)
            {
                if (u == null) continue;
                bool open = _save.IsUnlocked(u.id);
                var reward = Reward(u);
                switch (u.category)
                {
                    case UnlockCategory.Challenge:
                    {
                        string bar = null;
                        if (u.conditions != null && u.conditions.Length == 1)
                        {
                            MetaProgress.Progress(u.conditions[0], _save, out float cur, out float tgt);
                            bar = Num(open ? tgt : cur) + " / " + Num(tgt);
                        }
                        else if (u.conditions != null && u.conditions.Length > 1)
                        {
                            int met = 0;
                            foreach (var c in u.conditions) if (MetaProgress.Met(c, _save)) met++;
                            bar = (open ? u.conditions.Length : met) + " / " + u.conditions.Length;
                        }
                        Entries(Tab.Challenges).Add(new Entry
                        {
                            Id = "ul:" + u.id, Title = u.displayName, Kind = open ? "CHALLENGE · DONE" : "CHALLENGE",
                            Body = open ? u.description : u.hint, Footer = reward, Open = open,
                            Progress = MetaProgress.Progress01(u, _save), ProgressText = bar,
                            Icon = RewardIcon(u, out int row), IconRow = row,
                        });
                        break;
                    }
                    case UnlockCategory.Secret:
                        Entries(Tab.Secrets).Add(new Entry
                        {
                            Id = "ul:" + u.id, Title = open ? u.displayName : "???", Kind = open ? "SECRET · FOUND" : "SECRET",
                            Body = open ? u.description : u.hint, Footer = open ? reward : "", Open = open, Progress = -1f,
                            Portrait = "sc:" + u.id,
                        });
                        break;
                    case UnlockCategory.Market:
                        Entries(Tab.Market).Add(new Entry
                        {
                            Id = "ul:" + u.id, Title = u.displayName, Kind = open ? "MARKET · OWNED" : "MARKET · " + u.renownCost + " RENOWN",
                            Body = u.description, Footer = "", Open = open, Progress = -1f,
                            Icon = RewardIcon(u, out int mrow), IconRow = mrow, Buy = u, Lit = true,
                        });
                        break;
                }
            }
        }

        UnlockDef Holder(UpgradeDef d)
        {
            if (_meta == null || _meta.unlocks == null) return null;
            foreach (var u in _meta.unlocks)
                if (u != null && u.addsToPool != null && Array.IndexOf(u.addsToPool, d) >= 0) return u;
            return null;
        }

        string Reward(UnlockDef u)
        {
            string r = "";
            foreach (var g in _gladiators) if (g != null && g.unlock == u) r = g.displayName;
            if (r.Length == 0 && u.addsToPool != null && u.addsToPool.Length > 0 && u.addsToPool[0] != null) r = u.addsToPool[0].displayName;
            if (u.renownBounty > 0) r += (r.Length > 0 ? "  +" : "+") + u.renownBounty + " Renown";
            return r.Length > 0 ? "Reward: " + r : "";
        }

        static Texture2D RewardIcon(UnlockDef u, out int row)
        {
            row = -1;
            if (u.addsToPool == null || u.addsToPool.Length == 0 || u.addsToPool[0] == null) return null;
            row = u.addsToPool[0].iconRow;
            return u.addsToPool[0].icon;
        }

        static string Tags(UpgradeTag tags)
        {
            if (tags == UpgradeTag.None) return "";
            string s = "";
            foreach (UpgradeTag t in Enum.GetValues(typeof(UpgradeTag)))
                if (t != UpgradeTag.None && (tags & t) != 0) s += (s.Length > 0 ? " " : "· ") + CardText.TagName(t);
            return s;
        }

        static string ArenaName(int tier) => tier switch { 0 => "THE SLAVE PIT", 1 => "THE PROVINCIAL PIT", _ => "ARENA " + (tier + 1) };
        static string Num(float v) => v >= 1000f ? Mathf.RoundToInt(v).ToString("N0") : Mathf.RoundToInt(v).ToString();
        static string Title(string s) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

        /// <summary>Card text without the level-up panel's rich-text colour tags.</summary>
        static string Plain(string s)
        {
            int open = s.IndexOf('<');
            if (open < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            foreach (char c in s)
            {
                if (c == '<') tag = true;
                else if (c == '>') tag = false;
                else if (!tag) sb.Append(c);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ pages

        static int PerPage(Tab t) => t switch
        {
            Tab.Records => 100,
            Tab.Challenges or Tab.Secrets or Tab.Market => 6,
            _ => 9,
        };

        public void Select(Tab t)
        {
            _tab = t;
            _pageIndex = 0;
            _focus = 0;
            Draw();
        }

        public void Turn(int dir)
        {
            int n = PageCount;
            _pageIndex = (_pageIndex + dir + n) % n;
            Draw();
        }

        void Draw()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            for (int i = 0; i < TabCount; i++)
            {
                _tabFrames[i].color = i == (int)_tab ? Gold : Color.clear;
                _tabDots[i].enabled = TabHasNew((Tab)i);
            }
            _page.text = _tab == Tab.Records ? "" : "PAGE " + (_pageIndex + 1) + " / " + PageCount;
            if (_tab == Tab.Records) { DrawRecords(); return; }

            var list = Entries(_tab);
            int per = PerPage(_tab), start = _pageIndex * per;
            bool wide = per == 6;
            int cols = wide ? 2 : 3;
            Vector2 size = wide ? new Vector2(780, 206) : new Vector2(516, 206);
            for (int k = 0; k < per && start + k < list.Count; k++)
            {
                int i = start + k;
                var e = list[i];
                var pos = new Vector2((k % cols - (cols - 1) * 0.5f) * (size.x + 12f), 222f - (k / cols) * (size.y + 12f));
                DrawCard(e, pos, size, wide, _tab == Tab.Market && k == _focus);
                if (e.New) { e.New = false; list[i] = e; MetaProgress.MarkSeen(e.Id, _save); } // seen now: not NEW next time
            }
            if (list.Count == 0) Text(_content, "Nothing here yet.", 28, Dim, Vector2.zero, new Vector2(800, 60), TextAlignmentOptions.Center);
        }

        /// <summary>A card: the picture on the left (portrait or icon, black while locked), the text on the right.</summary>
        void DrawCard(in Entry e, Vector2 pos, Vector2 size, bool wide, bool focused)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var border = UiFactory.Image("Card", _content, focused ? Gold : e.New ? HintGold : Border, mid, mid, pos, size, PixelIcons.White);
            var card = UiFactory.Image("Inner", border.transform, CardColor, mid, mid, Vector2.zero, size - new Vector2(6, 6), PixelIcons.White).transform;
            float pic = wide ? 150f : 120f, halfW = size.x * 0.5f, halfH = size.y * 0.5f;
            bool hasPic = DrawPicture(card, e, new Vector2(-halfW + 14f + pic * 0.5f, 0f), pic);
            float x0 = hasPic ? -halfW + 28f + pic : -halfW + 16f, x1 = halfW - 14f;
            float w = x1 - x0, cx = (x0 + x1) * 0.5f;
            bool bar = e.Progress >= 0f;
            bool buy = e.Buy != null && !e.Open;
            bool footer = !string.IsNullOrEmpty(e.Footer);

            bool lit = e.Open || e.Lit;
            Text(card, e.Kind, 17, lit ? Gold : HintGold, new Vector2(cx, halfH - 22f), new Vector2(w, 24), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            var title = Text(card, e.Title, wide ? 30 : 26, lit ? Color.white : Dim, new Vector2(cx - (e.New ? 32f : 0f), halfH - 52f), new Vector2(w - (e.New ? 64f : 0f), 36), TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true; title.fontSizeMin = 18; title.fontSizeMax = wide ? 30 : 26;

            float bodyTop = halfH - 74f, bodyBottom = -halfH + (bar || buy ? 70f : footer ? 36f : 14f);
            var body = Text(card, e.Body, 19, lit ? Soft : HintGold, new Vector2(cx, (bodyTop + bodyBottom) * 0.5f), new Vector2(w, bodyTop - bodyBottom), TextAlignmentOptions.TopLeft);
            body.fontStyle = lit ? FontStyles.Normal : FontStyles.Italic;
            body.enableAutoSizing = true; body.fontSizeMin = 13; body.fontSizeMax = 19;

            if (bar)
            {
                var fill = UiFactory.Bar("Progress", card, new Color(0.03f, 0.04f, 0.06f, 0.9f), e.Open ? Done : Gold, mid, mid,
                    new Vector2(cx - 85f, -halfH + 50f), new Vector2(w - 170f, 16));
                fill.fillAmount = Mathf.Clamp01(e.Progress);
                Text(card, e.ProgressText ?? "", 18, Soft, new Vector2(x1 - 80f, -halfH + 50f), new Vector2(160, 26), TextAlignmentOptions.Right).fontStyle = FontStyles.Normal;
            }
            if (footer)
                Text(card, e.Footer, 17, e.Open ? Gold : HintGold, new Vector2(cx, -halfH + 20f), new Vector2(w, 24), TextAlignmentOptions.Left).fontStyle = FontStyles.Normal;
            if (e.New) Text(card, "NEW", 20, Gold, new Vector2(halfW - 44f, halfH - 22f), new Vector2(70, 26), TextAlignmentOptions.Right).fontStyle = FontStyles.Bold;
            if (buy)
            {
                var u = e.Buy;
                bool afford = _save.renown >= u.renownCost;
                var b = UiFactory.Button("Buy", card, afford ? "BUY " + u.renownCost : "NEED " + (u.renownCost - _save.renown) + " MORE", mid, mid,
                    new Vector2(halfW - 112f, -halfH + 36f), new Vector2(196, 48), _font, _fontMat, () => Buy(u), 22f);
                b.interactable = afford;
            }
        }

        bool DrawPicture(Transform card, in Entry e, Vector2 pos, float edge)
        {
            var mid = new Vector2(0.5f, 0.5f);
            if (!string.IsNullOrEmpty(e.Portrait) && _codex != null && _codex.TryPortrait(e.Portrait, out var uv))
            {
                var rt = UiFactory.Rect("Portrait", card, mid, mid, pos, new Vector2(edge, edge));
                var img = rt.gameObject.AddComponent<RawImage>();
                img.texture = _codex.portraits;
                img.uvRect = uv;
                img.color = e.Open || e.Lit ? Color.white : Silhouette;
                img.raycastTarget = false;
                return true;
            }
            if (e.Icon != null && e.IconRow >= 0)
            {
                float s = Mathf.Floor(edge * 0.8f / UpgradeIconView.Cell) * UpgradeIconView.Cell; // whole art pixels
                var rt = UiFactory.Rect("Icon", card, mid, mid, pos, new Vector2(s, s));
                var img = rt.gameObject.AddComponent<RawImage>();
                img.texture = e.Icon;
                float w = (float)UpgradeIconView.Cell / e.Icon.width, h = (float)UpgradeIconView.Cell / e.Icon.height;
                img.uvRect = new Rect(0f, 1f - (e.IconRow + 1) * h, w, h);
                img.color = e.Open || e.Lit ? Color.white : Silhouette;
                img.raycastTarget = false;
                return true;
            }
            if (!e.Open && _tab == Tab.Gladiators)
            {
                UiFactory.Image("Silhouette", card, Silhouette, mid, mid, pos, new Vector2(edge * 0.4f, edge * 0.8f), PixelIcons.White).raycastTarget = false;
                return true;
            }
            return false;
        }

        void DrawRecords()
        {
            var s = _save;
            int secrets = 0, secretsFound = 0, challenges = 0, challengesDone = 0;
            if (_meta != null && _meta.unlocks != null)
                foreach (var u in _meta.unlocks)
                {
                    if (u == null) continue;
                    if (u.category == UnlockCategory.Secret) { secrets++; if (s.IsUnlocked(u.id)) secretsFound++; }
                    if (u.category == UnlockCategory.Challenge) { challenges++; if (s.IsUnlocked(u.id)) challengesDone++; }
                }
            var rows = new (string, string)[]
            {
                ("Renown", s.renown.ToString("N0")), ("Renown earned", s.lifetimeRenown.ToString("N0")),
                ("Runs", s.runs.ToString()), ("Victories", s.victories.ToString()),
                ("Best time", Clock(s.bestTime)), ("Most kills in a run", s.bestRunKills.ToString("N0")),
                ("Kills", s.lifetimeKills.ToString("N0")), ("Gold earned", s.lifetimeGold.ToString("N0")),
                ("Gorm defeated", SaveData.Count(s.bossKills, "gorm") + "×"), ("Champion defeated", SaveData.Count(s.bossKills, "champion") + "×"),
                ("Evolutions made", SaveData.Total(s.evolutions).ToString()), ("Synergies found", s.synergies.Count.ToString()),
                ("Challenges done", challengesDone + " / " + challenges), ("Secrets found", secretsFound + " / " + secrets),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float x = i % 2 == 0 ? -400f : 400f, y = 250f - (i / 2) * 76f;
                var mid = new Vector2(0.5f, 0.5f);
                UiFactory.Image("Row", _content, new Color(0.08f, 0.1f, 0.14f, 0.96f), mid, mid, new Vector2(x, y), new Vector2(740, 64), PixelIcons.White).raycastTarget = false;
                Text(_content, rows[i].Item1, 28, Soft, new Vector2(x - 120f, y), new Vector2(460, 50), TextAlignmentOptions.Left).fontStyle = FontStyles.Normal;
                Text(_content, rows[i].Item2, 32, Color.white, new Vector2(x + 210f, y), new Vector2(280, 50), TextAlignmentOptions.Right).fontStyle = FontStyles.Bold;
            }
        }

        static string Clock(float t) => Mathf.FloorToInt(t / 60f) + ":" + Mathf.FloorToInt(t % 60f).ToString("00");

        // ------------------------------------------------------------------ actions

        /// <summary>Buys a Market item, then rebuilds every tab (the Arsenal shows it unlocked).</summary>
        public BuyResult Buy(UnlockDef u)
        {
            var r = MetaProgress.Buy(u);
            SfxPlayer.Play2D(r == BuyResult.Bought ? SfxId.Coin : SfxId.Blocked);
            if (r == BuyResult.Bought)
            {
                var tab = _tab;
                int page = _pageIndex, focus = _focus;
                Collect();
                _tab = tab; _pageIndex = Mathf.Min(page, PageCount - 1); _focus = focus;
                Draw();
            }
            return r;
        }

        public void Close()
        {
            MetaProgress.SaveSeen();
            _back?.Invoke();
        }

        /// <summary>Gamepad/keyboard: returns true when the Codex closes.</summary>
        public bool HandleInput(bool left, bool right, bool up, bool down, bool confirm, bool back)
        {
            if (back) { Close(); return true; }
            if (left || right)
            {
                SfxPlayer.Play2D(SfxId.Card);
                Select((Tab)(((int)_tab + (right ? 1 : TabCount - 1)) % TabCount));
                return false;
            }
            if (_tab == Tab.Market)
            {
                var list = Entries(Tab.Market);
                int onPage = Mathf.Min(PerPage(_tab), list.Count - _pageIndex * PerPage(_tab));
                if ((up || down) && onPage > 0) { _focus = (_focus + (down ? 1 : onPage - 1)) % onPage; Draw(); }
                if (confirm && onPage > 0)
                {
                    var e = list[_pageIndex * PerPage(_tab) + Mathf.Clamp(_focus, 0, onPage - 1)];
                    if (e.Buy != null && !e.Open) Buy(e.Buy);
                }
                return false;
            }
            if (up || down) { SfxPlayer.Play2D(SfxId.Card); Turn(down ? 1 : -1); }
            return false;
        }

        TextMeshProUGUI Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var t = UiFactory.Text("Line", parent, text, size, color, align, mid, mid, pos, box, _font, _fontMat);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }
    }
}
