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
    /// - GLADIATORS: the select screen's roster grid and wax tablet, read-only (R9; locked ones as black silhouettes
    ///   with their hint; up/down walks the roster there, since left/right switch tabs);
    /// - ARSENAL: weapons, evolutions, Boons, synergies (evolutions and synergies stay "???" until found; locked ones
    ///   say how they're earned);
    /// - BESTIARY: every enemy and boss (unseen = a silhouette);
    /// - CHALLENGES: goals with progress bars and rewards;
    /// - SECRETS: found ones explained, the rest a hint;
    /// - MARKET: Boons bought with Renown.
    /// Entries opened since the Codex was last viewed are NEW (the menu's button counts them); viewing a page marks
    /// them seen, saved on close. Pages instead of scrolling, for the gamepad: left/right = tab, up/down = page (in the
    /// Market: the item), Enter = buy. Menu-only UI: it allocates freely when a page is built.
    /// R7: an unlock that opens edicts names them as its reward ("Reward: Edict — Glass Jaw") and shows the first one's
    /// icon; RECORDS adds "Most edicts in a win". R8: RECORDS adds "Dailies played" and "Dailies won" (scored dailies).
    /// R6: a skin's unlock (MARKET or CHALLENGES) names it as its reward ("Reward: Skin — Freedman (The Nobody)") and
    /// shows its baked portrait.
    /// U1: the tabs are marble tabulae ansatae, the active one bronze with a gold meander under it; entries are carved
    /// `Marble` (names Title 28, text Body 22 in marble ink), still-locked ones dark `Stone` with their hint in dust;
    /// progress in bronze troughs; BUY a bronze plate; RECORDS on a `Wax` tablet with its values in Number; GLADIATORS is
    /// the scutum roster beside the wax tablet; &lt; &gt; and BACK marble. The whole page (1640 × 980) scales down with
    /// UiKit.FitScale (MainMenu.Refit).
    /// P3 (the producer's notes 4, 6-9 and 11): no meander under the active tab; the NEW mark is a crimson wax seal with a
    /// gold N (`SealNew`) on a tab's top-right corner over its last letter and on a new entry's corner (it was a gold
    /// square and the word NEW); the pager's arrows are bronze pilum tips with no plate; the bottom row's words are light
    /// and flat on the dark dim (they read dark when outlined); every entry's words sit inside its frame's content rect,
    /// so a page holds 6 narrow or 4 wide entries (it held 9 and 6); the Market's keyboard focus is a soft band and a
    /// pilum marker, not a ring.
    /// </summary>
    public sealed class CodexPanel
    {
        public enum Tab { Records, Gladiators, Arsenal, Bestiary, Challenges, Secrets, Market }
        static readonly string[] TabNames = { "RECORDS", "GLADIATORS", "ARSENAL", "BESTIARY", "CHALLENGES", "SECRETS", "MARKET" };
        const int TabCount = 7;
        /// <summary>The page's box at 1:1 (the menu scales it down, never up, to fit the canvas).</summary>
        public static readonly Vector2 Size = new Vector2(1640f, 980f);

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
            public Sprite Picture;       // a single sprite (R6: a skin's portrait)
            public bool New;
        }

        SaveData _save;
        CodexConfig _codex;
        MetaConfig _meta;
        GladiatorDef[] _gladiators;
        MainMenu.Locked[] _locked;
        Action _back;
        Transform _root;
        RectTransform _box, _content;
        TextMeshProUGUI _renown, _page;
        Button[] _tabButtons;
        UiPlate[] _tabPlates;
        Image[] _tabDots;
        Image _recordsFrame;
        readonly List<Image> _cardFrames = new List<Image>(); // the entries drawn on the page (tests)
        readonly List<Entry>[] _tabs = new List<Entry>[TabCount];
        Tab _tab;
        int _pageIndex, _focus;
        // R9: the GLADIATORS tab
        RosterGrid.Slot[] _slots = Array.Empty<RosterGrid.Slot>();
        RosterGrid _roster;
        GladiatorTablet _rosterTablet;
        RectTransform _rosterRoot;
        int _rosterFocus;
        readonly Dictionary<string, string> _records = new Dictionary<string, string>(); // RECORDS: label → value (tests)

        /// <summary>The font arguments are ignored (U1: the theme decides).</summary>
        public CodexPanel(TMP_FontAsset font, Material fontMat) { }

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
        /// <summary>Tests: the GLADIATORS tab's grid, its tablet and the picked tile.</summary>
        public RosterGrid Roster => _roster;
        public GladiatorTablet RosterTablet => _rosterTablet;
        public int RosterFocus => _rosterFocus;
        // ---- tests (U1): the look
        /// <summary>Tab `t`'s plate (AnsataBronze while active, AnsataMarble otherwise: UiFrames.KindOf(plate.Frame)).</summary>
        public UiPlate TabPlate(Tab t) => _tabPlates[(int)t];
        /// <summary>The RECORDS page's tablet (`FrameKind.Wax`; -1 off that tab).</summary>
        public int RecordsFrame => _tab == Tab.Records && _recordsFrame != null ? UiFrames.KindOf(_recordsFrame) : -1;
        /// <summary>The entries drawn on the current page and entry `k`'s frame (`Marble`, or `Stone` while it's dark).</summary>
        public int DrawnEntries => _cardFrames.Count;
        public int EntryFrame(int k) => k >= 0 && k < _cardFrames.Count ? UiFrames.KindOf(_cardFrames[k]) : -1;
        /// <summary>The page box the menu scales (MainMenu.Refit).</summary>
        public RectTransform Box => _box;
        /// <summary>Tests (P3): tab `t`'s NEW seal (`FrameKind.SealNew`) and whether it shows.</summary>
        public Component TabSeal(Tab t) => _tabDots[(int)t];
        public bool TabSealShown(Tab t) => _tabDots[(int)t].enabled;
        /// <summary>Tests (P3): the drawn entries that wear the NEW seal; the pager's arrows.</summary>
        public int SealedEntries { get; private set; }
        public RectTransform PrevArrow { get; private set; }
        public RectTransform NextArrow { get; private set; }

        /// <summary>Scales the page about the screen's centre (UiKit.FitScale: down, never up).</summary>
        public void Fit(float scale)
        {
            if (_box != null) _box.localScale = new Vector3(scale, scale, 1f);
        }
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
            var th = UiTheme.Current;
            _box = UiFactory.Rect("Box", root, mid, mid, Vector2.zero, Size);
            var box = _box.transform;

            Text(box, "CODEX", TextRole.Title, 60, th.gold, new Vector2(-560, 430), new Vector2(500, 80), TextAlignmentOptions.Left, TextLook.Outline);
            _renown = Text(box, "", TextRole.Number, 34, th.goldBright, new Vector2(560, 430), new Vector2(500, 50), TextAlignmentOptions.Right, TextLook.Outline);

            _tabButtons = new Button[TabCount];
            _tabPlates = new UiPlate[TabCount];
            _tabDots = new Image[TabCount];
            for (int i = 0; i < TabCount; i++)
            {
                int t = i;
                var pos = new Vector2(-690f + i * 230f, 345f);
                _tabPlates[i] = UiKit.Button("Tab" + TabNames[i], box, TabNames[i], ButtonStyle.Secondary, mid, mid, pos, new Vector2(218, 62), () => Select((Tab)t), 24f);
                _tabButtons[i] = _tabPlates[i].Button;
                // P3 (note 6): the NEW seal on the tab's top-right corner, over its last letter (no meander under the tab: note 9)
                var seal = UiKit.Seal("New", _tabPlates[i].Root);
                UiKit.PlaceSeal(seal, _tabPlates[i].Root, _tabPlates[i].Label);
                _tabDots[i] = seal;
            }

            _content = UiFactory.Rect("Content", box, mid, mid, new Vector2(0, -40), new Vector2(1600, 660));
            BuildRoster(box);
            UiKit.Button("Back", box, "BACK", ButtonStyle.Secondary, mid, mid, new Vector2(-640, -446), new Vector2(220, 66), () => Close(), 30f);
            // P3 (note 11): pilum-tip arrows, no plates
            PrevArrow = (RectTransform)UiKit.Arrow("Prev", box, false, mid, mid, new Vector2(-150, -446), new Vector2(96, 66), () => Turn(-1)).transform;
            NextArrow = (RectTransform)UiKit.Arrow("Next", box, true, mid, mid, new Vector2(150, -446), new Vector2(96, 66), () => Turn(1)).transform;
            // P3 (note 8): light and flat on the dark dim (an outline made the thin letters read dark)
            _page = Text(box, "", TextRole.Title, 26, th.ink, new Vector2(0, -446), new Vector2(200, 40), TextAlignmentOptions.Center, TextLook.Flat);
            Text(box, "Left / Right: tab    Up / Down: page or gladiator    Esc / B: back", TextRole.Body, 22, th.dust, new Vector2(530, -446), new Vector2(640, 32),
                TextAlignmentOptions.Right, TextLook.Flat);

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

        /// <summary>One entry per roster tile, in the grid's order (NEW marks and TryEntry follow the tiles).</summary>
        void CollectGladiators()
        {
            var list = Entries(Tab.Gladiators);
            foreach (var slot in _slots)
            {
                var g = slot.Def;
                if (g == null)
                {
                    list.Add(new Entry { Id = "", Title = "???", Kind = "GLADIATOR", Body = slot.Hint, Footer = "", Open = false, Progress = -1f, Portrait = "" });
                    continue;
                }
                bool open = slot.Open;
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
        }

        /// <summary>The GLADIATORS tab: the roster grid on the left, the wax tablet on the right, centred in the space
        /// between the tabs and the bottom row. Shown only on that tab.</summary>
        void BuildRoster(Transform root)
        {
            var mid = new Vector2(0.5f, 0.5f);
            _slots = RosterGrid.Slots(_gladiators, _locked);
            // between the tab row (bottom edge 314) and the bottom row (top edge -413), the picked tile's wreath (it rises
            // RosterGrid.WreathRoom over the grid) clear of the tabs
            const float top = 314f - RosterGrid.WreathRoom - 2f, bottom = -406f, height = top - bottom;
            float tabletScale = GridMath.Fit(MenuLayout.Tablet, new Vector2(MenuLayout.Tablet.x, height));
            float gridW = GridMath.Size(MenuLayout.Columns, MenuLayout.Columns, MenuLayout.Tile, MenuLayout.Gap).x;
            var area = new Vector2(gridW + 60f + MenuLayout.Tablet.x * tabletScale, height);
            _rosterRoot = UiFactory.Rect("Gladiators", root, mid, mid, new Vector2(0f, (top + bottom) * 0.5f), area);
            _roster = new RosterGrid(null);
            _roster.Build(_rosterRoot, new Vector2(0f, 1f), Vector2.zero, _slots, MenuLayout.Columns, _codex, FocusRoster);
            _roster.Fit(GridMath.Fit(GridMath.Size(_slots.Length, MenuLayout.Columns, MenuLayout.Tile, MenuLayout.Gap), new Vector2(gridW, area.y)));
            _rosterTablet = new GladiatorTablet(null);
            var corner = new Vector2(1f, 1f);
            _rosterTablet.Build(_rosterRoot, corner, corner, Vector2.zero, _codex);
            _rosterTablet.Fit(tabletScale);
            _rosterRoot.gameObject.SetActive(false);
            if (_slots.Length > 0) FocusRoster(0);
        }

        /// <summary>Picks roster tile `i` (a click, or up/down) and shows it on the tablet. Read-only: no FIGHT!.</summary>
        void FocusRoster(int i)
        {
            if (_roster == null || _roster.Count == 0) return;
            _rosterFocus = Mathf.Clamp(i, 0, _roster.Count - 1);
            _roster.SetPick(_rosterFocus);
            _rosterTablet.Show(_roster.Def(_rosterFocus), _roster.IsOpen(_rosterFocus), _save, _roster.Hint(_rosterFocus));
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
                if (u == null || SkinOfLockedGladiator(u)) continue;
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
                            Icon = RewardIcon(u, out int row), IconRow = row, Picture = SkinPicture(u),
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
                            Icon = RewardIcon(u, out int mrow), IconRow = mrow, Buy = u, Lit = true, Picture = SkinPicture(u),
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
            if (r.Length == 0) r = EdictReward(u);
            if (r.Length == 0) r = SkinReward(u);
            if (u.renownBounty > 0) r += (r.Length > 0 ? "  +" : "+") + u.renownBounty + " Renown";
            return r.Length > 0 ? "Reward: " + r : "";
        }

        Texture2D RewardIcon(UnlockDef u, out int row)
        {
            row = -1;
            if (u.addsToPool != null && u.addsToPool.Length > 0 && u.addsToPool[0] != null)
            {
                row = u.addsToPool[0].iconRow;
                return u.addsToPool[0].icon;
            }
            var edicts = EdictsOpenedBy(u); // R7: the first edict it opens
            if (edicts.Count == 0 || edicts[0].icon == null) return null;
            row = edicts[0].iconRow;
            return edicts[0].icon;
        }

        /// <summary>The edicts `u` opens, in MetaConfig.edicts order: those whose unlock it is, and for the Edicts' own
        /// unlock (The Editor Takes Notice) those that need nothing more.</summary>
        List<RunModifierDef> EdictsOpenedBy(UnlockDef u)
        {
            var list = new List<RunModifierDef>();
            if (_meta == null || _meta.edicts == null || u == null) return list;
            foreach (var e in _meta.edicts)
                if (e != null && (e.unlock == u || (e.unlock == null && _meta.edictsUnlock == u))) list.Add(e);
            return list;
        }

        /// <summary>"Edict — Glass Jaw", "Edicts — Iron Hides, The Hungry Horde, Blood Sport", or "".</summary>
        string EdictReward(UnlockDef u)
        {
            var edicts = EdictsOpenedBy(u);
            if (edicts.Count == 0) return "";
            var sb = new System.Text.StringBuilder(edicts.Count == 1 ? "Edict — " : "Edicts — ");
            for (int i = 0; i < edicts.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(edicts[i].displayName);
            }
            return sb.ToString();
        }

        /// <summary>"Skin — Freedman (The Nobody)", "Skins — A (The Nobody), B (The Nobody)", or "" (R6).</summary>
        string SkinReward(UnlockDef u)
        {
            if (_meta == null || _meta.skins == null || u == null) return "";
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var s in _meta.skins)
            {
                if (s == null || s.unlock != u) continue;
                if (n++ > 0) sb.Append(", ");
                sb.Append(s.displayName);
                if (s.gladiator != null) sb.Append(" (").Append(s.gladiator.displayName).Append(')');
            }
            return n == 0 ? "" : (n == 1 ? "Skin — " : "Skins — ") + sb;
        }

        /// <summary>The baked portrait of the first skin `u` gives, or null (R6).</summary>
        /// <summary>A skin's unlock stays out of the Codex until its gladiator is earned: its Market card and its challenge
        /// would show a secret gladiator's name and look, which the roster keeps as a silhouette.</summary>
        bool SkinOfLockedGladiator(UnlockDef u)
        {
            if (_meta == null || _meta.skins == null) return false;
            foreach (var s in _meta.skins)
                if (s != null && s.unlock == u && s.gladiator != null && s.gladiator.unlock != null && !_save.IsUnlocked(s.gladiator.unlock.id))
                    return true;
            return false;
        }

        Sprite SkinPicture(UnlockDef u)
        {
            if (_meta == null || _meta.skins == null || u == null) return null;
            foreach (var s in _meta.skins) if (s != null && s.unlock == u && s.portrait != null) return s.portrait;
            return null;
        }

        /// <summary>The reward line the Codex gives `u` (tests).</summary>
        public string RewardText(UnlockDef u) => u != null ? Reward(u) : "";

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
            Tab.Challenges or Tab.Secrets or Tab.Market => 4, // P3: 2 × 2 wide entries (their words inside the padding)
            Tab.Gladiators => 1024, // one grid, no pages
            _ => 6,                  // 3 × 2
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
            var th = UiTheme.Current;
            for (int i = _content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _cardFrames.Clear();
            _recordsFrame = null;
            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_tab;
                var plate = _tabPlates[i];
                if (plate.Style != (active ? ButtonStyle.Primary : ButtonStyle.Secondary))
                {
                    // the active tab is the page's bronze plate, the rest marble (re-skinned in place: same button, same label)
                    plate.Style = active ? ButtonStyle.Primary : ButtonStyle.Secondary;
                    if (plate.Frame != null) UiFrames.Apply(plate.Frame, active ? FrameKind.AnsataBronze : FrameKind.AnsataMarble);
                    if (plate.Label != null) plate.Label.color = active ? th.bronzeInk : th.marbleInk;
                }
                _tabDots[i].enabled = TabHasNew((Tab)i);
            }
            SealedEntries = 0;
            bool roster = _tab == Tab.Gladiators;
            _rosterRoot.gameObject.SetActive(roster);
            _page.text = _tab == Tab.Records || roster ? "" : "PAGE " + (_pageIndex + 1) + " / " + PageCount;
            if (_tab == Tab.Records) { DrawRecords(); return; }
            if (roster) { DrawRoster(); return; }

            var list = Entries(_tab);
            int per = PerPage(_tab), start = _pageIndex * per;
            bool wide = per == 4;
            int cols = wide ? 2 : 3;
            Vector2 size = wide ? new Vector2(780, 324) : new Vector2(516, 324); // two rows in the 660-tall content
            float top = 330f - size.y * 0.5f;
            for (int k = 0; k < per && start + k < list.Count; k++)
            {
                int i = start + k;
                var e = list[i];
                var pos = new Vector2((k % cols - (cols - 1) * 0.5f) * (size.x + 12f), top - (k / cols) * (size.y + 12f));
                DrawCard(e, pos, size, wide, _tab == Tab.Market && k == _focus);
                if (e.New) { e.New = false; list[i] = e; MetaProgress.MarkSeen(e.Id, _save); } // seen now: not NEW next time
            }
            if (list.Count == 0) Text(_content, "Nothing here yet.", TextRole.Body, 28, th.dust, Vector2.zero, new Vector2(800, 60), TextAlignmentOptions.Center, TextLook.Outline);
        }

        /// <summary>The roster's NEW marks for this visit (then they're seen), and the picked tile on the tablet.</summary>
        void DrawRoster()
        {
            var list = Entries(Tab.Gladiators);
            for (int i = 0; i < list.Count && i < _roster.Count; i++)
            {
                var e = list[i];
                _roster.SetNew(i, e.New);
                if (e.New) { e.New = false; list[i] = e; MetaProgress.MarkSeen(e.Id, _save); }
            }
            FocusRoster(_rosterFocus);
        }

        /// <summary>A card: the picture on the left (portrait or icon, black while locked), the text on the right. Carved
        /// marble while open or lit (the Market shows what you buy), dark stone while it's still a hint. P3: the words are
        /// laid from the frame's content rect (kind and title at its top, the footer at its bottom, the bar and BUY over the
        /// footer, the body between), a new entry wears the seal on its corner, the Market's focus is a band and a marker.</summary>
        void DrawCard(in Entry e, Vector2 pos, Vector2 size, bool wide, bool focused)
        {
            var th = UiTheme.Current;
            var mid = new Vector2(0.5f, 0.5f);
            bool lit = e.Open || e.Lit;
            var kind = lit ? FrameKind.Marble : FrameKind.Stone;
            var frame = UiKit.Frame("Card", _content, kind, mid, mid, pos, size, Color.white);
            _cardFrames.Add(frame);
            var card = frame.transform;
            if (focused) // under the words: a warm band on the marble, the pilum marker at the card's left
                UiKit.Row("Focus", card, mid, mid, Vector2.zero, size, new Color(th.gold.r, th.gold.g, th.gold.b, 0.22f)).Show(true);
            var cr = UiKit.ContentRect(kind, size);
            float pic = wide ? 150f : 100f, halfW = size.x * 0.5f; // an icon shows at 80 either way
            bool hasPic = DrawPicture(card, e, new Vector2(-halfW + 18f + pic * 0.5f, 0f), pic);
            float x0 = hasPic ? Mathf.Max(cr.xMin, -halfW + 32f + pic) : cr.xMin, x1 = cr.xMax;
            float w = x1 - x0, cx = (x0 + x1) * 0.5f;
            bool bar = e.Progress >= 0f;
            bool buy = e.Buy != null && !e.Open;
            bool footer = !string.IsNullOrEmpty(e.Footer);
            var ink = lit ? th.marbleInk : th.ink;
            var hint = lit ? th.marbleInk : th.dust;
            var look = lit ? TextLook.Engraved : TextLook.Flat;

            // from the top: the kind (Title 18), the title (Title 28), each placed by its ink (Midline)
            float kindY = cr.yMax - 7f, titleY = kindY - 7f - 6f - 10f;
            Text(card, "Kind", e.Kind, TextRole.Title, 18, lit ? th.bronzeDark : th.dust, new Vector2(cx, kindY), new Vector2(w, 24), TextAlignmentOptions.MidlineLeft, look);
            float sealW = e.New ? 32f : 0f;
            var title = Text(card, "Title", e.Title, TextRole.Title, 28, lit ? ink : th.dust, new Vector2(cx - sealW * 0.5f, titleY), new Vector2(w - sealW, 34), TextAlignmentOptions.MidlineLeft, look);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true; title.fontSizeMin = 18; title.fontSizeMax = 28;
            if (e.New) // the seal on the card's top-right corner, beside the title
            {
                var seal = UiKit.Seal("New", card, 24f);
                seal.rectTransform.anchoredPosition = new Vector2(halfW - 22f, size.y * 0.5f - 22f);
                seal.enabled = true;
                SealedEntries++;
            }

            // from the bottom: the footer (Body 18), then the bar and BUY over it
            float y = cr.yMin;
            if (footer)
            {
                Text(card, "Footer", e.Footer, TextRole.Body, 18, lit ? th.bronzeDark : th.dust, new Vector2(cx, y + 9f), new Vector2(w, 24), TextAlignmentOptions.MidlineLeft, look);
                y += 18f + 8f;
            }
            if (bar || buy)
            {
                float rowY = y + 24f; // the BUY plate is 48 tall; the bar 24
                if (bar)
                {
                    float barW = w - (buy ? 260f : 170f);
                    var fill = UiKit.Bar("Progress", card, e.Open ? th.gain : th.gold, mid, mid, new Vector2(x0 + barW * 0.5f, rowY), new Vector2(barW, 24));
                    fill.fillAmount = Mathf.Clamp01(e.Progress);
                    Text(card, "ProgressText", e.ProgressText ?? "", TextRole.Number, 20, lit ? th.marbleInk : th.ink, new Vector2(x0 + barW + 84f, rowY), new Vector2(160, 28),
                        TextAlignmentOptions.MidlineLeft, lit ? TextLook.Flat : TextLook.Outline);
                }
                if (buy)
                {
                    var u = e.Buy;
                    bool afford = _save.renown >= u.renownCost;
                    var plate = UiKit.Button("Buy", card, afford ? "BUY " + u.renownCost : "NEED " + (u.renownCost - _save.renown) + " MORE", ButtonStyle.Primary, mid, mid,
                        new Vector2(x1 - 120f, rowY), new Vector2(240, 48), () => Buy(u), 22f);
                    var label = UiFactory.Label(plate.Button);
                    label.enableAutoSizing = true; label.fontSizeMin = 18f; label.fontSizeMax = 22f;
                    plate.Button.interactable = afford;
                    plate.SetState(afford ? UiState.Normal : UiState.Disabled);
                }
                y = rowY + 24f + 8f;
            }

            // the body between: top-aligned under the title, autosized 22 → 18
            float bodyTop = titleY - 10f - 8f, bodyBottom = y;
            var body = Text(card, "Body", e.Body, TextRole.Body, 22, hint, new Vector2(cx, (bodyTop + bodyBottom) * 0.5f), new Vector2(w, bodyTop - bodyBottom), TextAlignmentOptions.TopLeft, look);
            body.enableAutoSizing = true; body.fontSizeMin = 18; body.fontSizeMax = 22;
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
            if (e.Picture != null) // R6: a skin's portrait (a champion skin's stays dark until earned)
            {
                var rt = UiFactory.Rect("Portrait", card, mid, mid, pos, new Vector2(edge, edge));
                var img = rt.gameObject.AddComponent<RawImage>();
                img.raycastTarget = false;
                RosterGrid.Show(img, e.Picture, e.Open || e.Lit ? Color.white : Silhouette);
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
                ("Dailies played", s.dailiesPlayed.ToString()), ("Dailies won", s.dailiesWon.ToString()),
                ("Most edicts in a win", s.bestEdictWin.ToString()),
            };
            // a wax tablet; two columns; the rows close up when there are more than eight of them (the page is 660 tall)
            _records.Clear();
            foreach (var row in rows) _records[row.Item1] = row.Item2;
            var th = UiTheme.Current;
            var mid = new Vector2(0.5f, 0.5f);
            _recordsFrame = UiKit.Frame("Records", _content, FrameKind.Wax, mid, mid, Vector2.zero, new Vector2(1600, 660), Color.white);
            int lines = (rows.Length + 1) / 2;
            float pitch = Mathf.Min(76f, 528f / Mathf.Max(1, lines - 1)), rowH = Mathf.Min(64f, pitch - 8f);
            float top = (lines - 1) * pitch * 0.5f;
            var band = th.night;
            band.a = 0.3f;
            for (int i = 0; i < rows.Length; i++)
            {
                float x = i % 2 == 0 ? -390f : 390f, y = top - (i / 2) * pitch;
                UiFactory.Image("Row", _content, band, mid, mid, new Vector2(x, y), new Vector2(740, rowH), PixelIcons.White).raycastTarget = false;
                Text(_content, "Label", rows[i].Item1, TextRole.Title, 26, th.waxInk, new Vector2(x - 120f, y), new Vector2(460, 36), TextAlignmentOptions.Left, TextLook.Flat);
                Text(_content, "Value", rows[i].Item2, TextRole.Number, 30, th.goldBright, new Vector2(x + 210f, y), new Vector2(280, 36), TextAlignmentOptions.Right, TextLook.Outline);
            }
        }

        /// <summary>Tests: the value the last RECORDS page drawn shows beside `label` ("" when there's no such row).</summary>
        public string RecordValue(string label) => _records.TryGetValue(label, out var v) ? v : "";

        static string Clock(float t) => Mathf.FloorToInt(t / 60f) + ":" + Mathf.FloorToInt(t % 60f).ToString("00");

        // ------------------------------------------------------------------ actions

        /// <summary>Buys a Market item, then rebuilds every tab (the Arsenal shows it unlocked).</summary>
        public BuyResult Buy(UnlockDef u)
        {
            var r = MetaProgress.Buy(u);
            SfxPlayer.Play2D(r == BuyResult.Bought ? SfxId.Coin : SfxId.Blocked);
            if (r == BuyResult.Bought) Refresh();
            return r;
        }

        /// <summary>Rebuilds every tab from the save, keeping the tab, page and focus (after a purchase made elsewhere:
        /// the Edicts page's Market edicts, R7).</summary>
        public void Refresh()
        {
            var tab = _tab;
            int page = _pageIndex, focus = _focus;
            Collect();
            _tab = tab; _pageIndex = Mathf.Min(page, PageCount - 1); _focus = focus;
            Draw();
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
            if (_tab == Tab.Gladiators)
            {
                if ((up || down) && _roster.Count > 0)
                {
                    SfxPlayer.Play2D(SfxId.Card);
                    FocusRoster(GridMath.StepWrap(_rosterFocus, _roster.Count, down ? 1 : -1));
                }
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

        static TextMeshProUGUI Text(Transform parent, string text, TextRole role, float size, Color color, Vector2 pos, Vector2 box,
            TextAlignmentOptions align, TextLook look) => Text(parent, "Line", text, role, size, color, pos, box, align, look);

        /// <summary>A themed label named `name` (a failing fit test names what was cut).</summary>
        static TextMeshProUGUI Text(Transform parent, string name, string text, TextRole role, float size, Color color, Vector2 pos, Vector2 box,
            TextAlignmentOptions align, TextLook look)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var t = UiKit.Text(name, parent, text, role, size, color, align, mid, mid, pos, box, look);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate; // never Ellipsis
            return t;
        }
    }
}
