using System;
using System.Collections.Generic;
using System.Globalization;
using Gladiator.Core;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gladiator.UI
{
    /// <summary>
    /// The Daily Games screen (R8, docs/tracks/R7-design.md §3.7): the day's proclamation on the left, the ledger on the
    /// right; the menu's shared Chrome adds the header ("THE DAILY GAMES — 22 OCT 2026"), the key line and BACK / FIGHT!.
    /// The proclamation: the arena (thumbnail, name, tagline) beside the gladiator (portrait, name, select line); the
    /// day's edicts (icon, name, effect line, share), one block that opens the Edicts page read-only (OnEdicts); the
    /// Renown line "RENOWN ×1.5 · EDICTS ×1.45"; the attempt ("SCORED ATTEMPT" in gold, or "PRACTICE · scored: VICTORY
    /// 8:00, 1,904 kills, +412" in grey, the ×1.5 struck through); and the clock to the next daily. The ledger: the last
    /// seven scored dailies, newest first (date, who, where, outcome, time, kills, Renown), today's framed in gold.
    /// Built once (hidden rows wait for records); Show rewrites the words and pictures, Tick only the clock, once a second.
    /// Menu-only UI: it allocates freely. Truncate, never Ellipsis (the font has no U+2026).
    /// U1: the proclamation inside a `Mosaic` border, the arena and gladiator cards on `Bronze`, the edicts block on
    /// `Stone`; "SCORED ATTEMPT" in Title gold, "PRACTICE …" in Body dust; the clock in Number 28. The ledger on a `Wax`
    /// tablet with its values in Number; today's line ringed in gold, a won day's outcome between small laurels.
    /// </summary>
    public sealed class DailyPanel : IUiHoverTarget
    {
        /// <summary>The two blocks at 1:1 (the menu scales them down, never up, to fit between the header and the buttons).</summary>
        public static readonly Vector2 Size = new Vector2(1760f, 790f);
        /// <summary>The ledger's rows (the save keeps more: MetaConfig.dailyLedger).</summary>
        public const int LedgerShown = 7;
        const float LeftW = 900f, RightW = 840f, Pad = 30f;
        const float CardW = 412f, CardH = 250f, RowH = 84f, LedgerRowH = 88f, LedgerPitch = 96f, LedgerTop = 300f;

        static readonly Color Hurt = EdictsPanel.Hurt;
        static readonly Color Pale = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color PlateColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        static readonly Color ArenaFallback = new Color(0.45f, 0.32f, 0.22f, 1f);
        const string GoldHex = "#FFD84A", HurtHex = "#FF5433", StruckHex = "#FFFFFF66";

        static readonly string[] Months = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

        sealed class EdictRow
        {
            public RectTransform Root;
            public RawImage Icon;
            public TextMeshProUGUI Glyph, Name, Line, Share;
        }

        sealed class LedgerRow
        {
            public RectTransform Root, Laurels;
            public Image Frame;
            public TextMeshProUGUI Date, Year, Who, Where, Outcome, Detail, Renown;
        }

        CodexConfig _codex;
        Image _proclamationFrame, _ledgerFrame;
        RectTransform _box, _edictsBlock;
        Image _edictsRing;
        RawImage _thumb, _portrait;
        TextMeshProUGUI _arenaName, _arenaLine, _gladName, _gladLine, _edictsLabel, _noEdicts, _renown, _status, _statusSub, _next;
        TextMeshProUGUI _ledgerCounts, _ledgerEmpty;
        EdictRow[] _edictRows = Array.Empty<EdictRow>();
        LedgerRow[] _ledger = Array.Empty<LedgerRow>();
        RunDef[] _runs = Array.Empty<RunDef>();
        GladiatorDef[] _gladiators = Array.Empty<GladiatorDef>();
        DailyPlan _plan;
        int _date;
        long _clockSecond = long.MinValue;
        int _clockMinutes = int.MinValue;
        bool _hover;

        /// <summary>The font arguments are ignored (U1: the theme decides).</summary>
        public DailyPanel(TMP_FontAsset font, Material fontMat) { }

        /// <summary>The edicts block was clicked: the menu opens the Edicts page read-only on the day's edicts.</summary>
        public Action OnEdicts;

        /// <summary>FIGHT! would start today's scored attempt (false: practice, or nothing to fight).</summary>
        public bool Scored { get; private set; }
        /// <summary>The plan the screen shows.</summary>
        public DailyPlan Plan => _plan;
        /// <summary>The ledger lines shown (at most LedgerShown).</summary>
        public int LedgerRows { get; private set; }

        // ---- tests: what the screen shows
        public RectTransform Box => _box;
        public RectTransform EdictsRect => _edictsBlock;
        public string StatusText => _status != null ? _status.text : "";
        public string RenownText => _renown != null ? _renown.text : "";
        public string NextText => _next != null ? _next.text : "";
        public string ArenaText => _arenaName != null ? _arenaName.text : "";
        public string GladiatorText => _gladName != null ? _gladName.text : "";
        /// <summary>The edicts listed (rows shown).</summary>
        public int EdictCount { get; private set; }
        public string EdictName(int i) => i >= 0 && i < _edictRows.Length && _edictRows[i].Root.gameObject.activeSelf ? _edictRows[i].Name.text : "";
        /// <summary>Ledger line `i` (0 = newest) as one string: "22 OCT 2026 · The Nobody · The Slave Pit · VICTORY · 8:00 · 1,904 kills · +412".</summary>
        public string LedgerText(int i)
        {
            if (i < 0 || i >= LedgerRows) return "";
            var r = _ledger[i];
            return r.Date.text + " " + r.Year.text + " · " + r.Who.text + " · " + r.Where.text + " · " + r.Outcome.text + " · " + r.Detail.text + " · " + r.Renown.text;
        }
        public RectTransform LedgerRect(int i) => i >= 0 && i < _ledger.Length ? _ledger[i].Root : null;
        /// <summary>The ledger line `i` is today's (ringed in gold).</summary>
        public bool LedgerIsToday(int i) => i >= 0 && i < LedgerRows && _ledger[i].Frame.enabled;
        // ---- tests (U1): the look
        /// <summary>The proclamation's frame (`FrameKind.Mosaic`) and the ledger's (`FrameKind.Wax`).</summary>
        public int ProclamationFrame => UiFrames.KindOf(_proclamationFrame);
        public int LedgerFrame => UiFrames.KindOf(_ledgerFrame);
        /// <summary>The attempt line and the clock (their theme roles: Title for SCORED ATTEMPT, Body for practice; Number).</summary>
        public Component StatusLabel => _status;
        public Component ClockLabel => _next;
        /// <summary>Ledger line `i`'s outcome has the laurels (a won day).</summary>
        public bool LedgerHasLaurels(int i) => i >= 0 && i < LedgerRows && _ledger[i].Laurels != null;

        /// <summary>Tests (their assembly has no TextMeshPro): the first active text under `root` that's cut off after
        /// autosizing, as "Name: words", or null when everything fits.</summary>
        public static string FirstTruncated(Transform root)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>())
            {
                t.ForceMeshUpdate(true, true);
                if (t.isTextTruncated) return t.name + ": " + t.text;
            }
            return null;
        }

        // ---- words (pure; tests and the end screen use them)

        /// <summary>The Chrome's header: "THE DAILY GAMES — 22 OCT 2026".</summary>
        public static string Header(int date) => "THE DAILY GAMES — " + DateLong(date);

        /// <summary>"22 OCT 2026" from yyyymmdd (invariant: the same words in every locale).</summary>
        public static string DateLong(int date) => date <= 0 ? "" : DateShort(date) + " " + (date / 10000).ToString(CultureInfo.InvariantCulture);

        /// <summary>"22 OCT" from yyyymmdd.</summary>
        public static string DateShort(int date)
        {
            if (date <= 0) return "";
            int month = Mathf.Clamp(date / 100 % 100, 1, 12), day = date % 100;
            return day.ToString(CultureInfo.InvariantCulture) + " " + Months[month - 1];
        }

        /// <summary>"5 H 12 M", "12 M", or "LESS THAN A MINUTE".</summary>
        public static string Countdown(TimeSpan left)
        {
            int minutes = Mathf.Max(0, (int)Math.Floor(left.TotalMinutes));
            int h = minutes / 60, m = minutes % 60;
            if (h > 0) return h.ToString(CultureInfo.InvariantCulture) + " H " + m.ToString(CultureInfo.InvariantCulture) + " M";
            if (m > 0) return m.ToString(CultureInfo.InvariantCulture) + " M";
            return "LESS THAN A MINUTE";
        }

        /// <summary>"8:00".</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>"VICTORY" / "DIED" (a record's RunOutcome).</summary>
        public static string OutcomeWord(int outcome) => outcome == (int)RunOutcome.Survived ? "VICTORY" : outcome == (int)RunOutcome.Died ? "DIED" : "UNFINISHED";

        /// <summary>"scored: VICTORY 8:00, 1,904 kills, +412": a scored daily's result in one line.</summary>
        public static string ResultLine(in DailyRecord r) =>
            "scored: " + OutcomeWord(r.outcome) + " " + Clock(r.elapsed) + ", " + r.kills.ToString("N0", CultureInfo.InvariantCulture) + " kills, +" + r.renown.ToString(CultureInfo.InvariantCulture);

        /// <summary>The ledger's names: the arenas and gladiators the records' ids point at (unknown ids show as words).</summary>
        public void Names(IList<RunDef> runs, IList<GladiatorDef> gladiators)
        {
            _runs = Copy(runs);
            _gladiators = Copy(gladiators);
        }

        static T[] Copy<T>(IList<T> list) where T : class
        {
            if (list == null) return Array.Empty<T>();
            var a = new List<T>(list.Count);
            foreach (var x in list) if (x != null) a.Add(x);
            return a.ToArray();
        }

        // ------------------------------------------------------------------ build

        /// <summary>Builds the two blocks (empty) under `root`, which should cover the screen, centred between the header and
        /// the buttons.</summary>
        public void Build(Transform root, CodexConfig codex)
        {
            _codex = codex;
            var mid = new Vector2(0.5f, 0.5f);
            _box = UiFactory.Rect("Daily", root, mid, mid, new Vector2(0f, (MenuLayout.Bottom - MenuLayout.Top) * 0.5f), Size);
            BuildProclamation(UiFactory.Rect("Proclamation", _box, mid, mid, new Vector2(-Size.x * 0.5f + LeftW * 0.5f, 0f), new Vector2(LeftW, Size.y)));
            BuildLedger(UiFactory.Rect("Ledger", _box, mid, mid, new Vector2(Size.x * 0.5f - RightW * 0.5f, 0f), new Vector2(RightW, Size.y)));
        }

        static Image Panel(RectTransform r, Vector2 size, FrameKind kind)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var frame = UiKit.Frame("Frame", r, kind, mid, mid, Vector2.zero, size, Color.white);
            frame.raycastTarget = true;
            return frame;
        }

        void BuildProclamation(RectTransform p)
        {
            var th = UiTheme.Current;
            var size = new Vector2(LeftW, Size.y);
            _proclamationFrame = Panel(p, size, FrameKind.Mosaic);
            var mid = new Vector2(0.5f, 0.5f);
            float inner = LeftW - Pad * 2f;
            Text(p, "THE EDITOR PROCLAIMS", TextRole.Title, 24, th.gold, new Vector2(0f, 350f), new Vector2(inner, 32f), TextAlignmentOptions.Left);

            // the arena and the gladiator, side by side, each on a bronze plate
            float cx = (CardW + (inner - CardW * 2f)) * 0.5f;
            var arena = Card(p, "Arena", new Vector2(-cx, 210f));
            UiFactory.Image("Plate", arena, PlateColor, mid, mid, new Vector2(0f, 24f), new Vector2(296f, 170f), PixelIcons.White);
            _thumb = UiFactory.Rect("Thumbnail", arena, mid, mid, new Vector2(0f, 24f), new Vector2(288f, 162f)).gameObject.AddComponent<RawImage>();
            _thumb.raycastTarget = false;
            _arenaName = Text(arena, "", TextRole.Title, 30, th.ink, new Vector2(0f, -79f), new Vector2(CardW - 24f, 40f), TextAlignmentOptions.Center);
            AutoSize(_arenaName, 18f, 30f);
            _arenaLine = Text(arena, "", TextRole.Body, 20, th.dust, new Vector2(0f, -106f), new Vector2(CardW - 24f, 28f), TextAlignmentOptions.Center);
            AutoSize(_arenaLine, 18f, 20f);
            var glad = Card(p, "Gladiator", new Vector2(cx, 210f));
            UiFactory.Image("Plate", glad, PlateColor, mid, mid, new Vector2(0f, 24f), new Vector2(170f, 170f), PixelIcons.White);
            _portrait = UiFactory.Rect("Portrait", glad, mid, mid, new Vector2(0f, 24f), new Vector2(162f, 162f)).gameObject.AddComponent<RawImage>();
            _portrait.raycastTarget = false;
            _gladName = Text(glad, "", TextRole.Title, 30, th.ink, new Vector2(0f, -79f), new Vector2(CardW - 24f, 40f), TextAlignmentOptions.Center);
            AutoSize(_gladName, 18f, 30f);
            _gladLine = Text(glad, "", TextRole.Body, 20, th.dust, new Vector2(0f, -106f), new Vector2(CardW - 24f, 28f), TextAlignmentOptions.Center);
            AutoSize(_gladLine, 18f, 20f);

            // the day's edicts: one dark stone block, a click opens the Edicts page read-only
            float blockTop = 74f, blockH = 30f + 2f * RowH + 8f + 12f;
            var blockPos = new Vector2(0f, blockTop - blockH * 0.5f);
            var blockSize = new Vector2(inner, blockH);
            _edictsRing = UiKit.Frame("Ring", p, FrameKind.Focus, mid, mid, blockPos, blockSize + new Vector2(12f, 12f), Color.clear);
            var block = UiKit.Frame("Edicts", p, FrameKind.Stone, mid, mid, blockPos, blockSize, Color.white);
            block.raycastTarget = true;
            _edictsBlock = block.rectTransform;
            var button = block.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => OnEdicts?.Invoke());
            var hover = block.gameObject.AddComponent<UiHover>();
            hover.Target = this;
            hover.Id = 0;
            _edictsLabel = Text(_edictsBlock, "THE DAY'S EDICTS", TextRole.Title, 22, th.gold, new Vector2(0f, blockH * 0.5f - 18f), new Vector2(inner - 24f, 30f), TextAlignmentOptions.Left);
            AutoSize(_edictsLabel, 18f, 22f);
            BuildEdictRows(blockH, inner);
            _noEdicts = Text(_edictsBlock, "No edicts today: the Editor is in a mild mood.", TextRole.Body, 22, th.dust, new Vector2(0f, blockH * 0.5f - 30f - 6f - RowH * 0.5f), new Vector2(inner - 24f, 40f), TextAlignmentOptions.Center);

            // the Renown, the attempt and the clock
            _renown = Text(p, "", TextRole.Title, 30, th.gold, new Vector2(0f, -172f), new Vector2(inner, 44f), TextAlignmentOptions.Center);
            AutoSize(_renown, 22f, 30f);
            _status = Text(p, "", TextRole.Title, 30, th.gold, new Vector2(0f, -224f), new Vector2(inner, 44f), TextAlignmentOptions.Center);
            AutoSize(_status, 18f, 30f);
            _statusSub = Text(p, "", TextRole.Body, 20, th.dust, new Vector2(0f, -262f), new Vector2(inner, 30f), TextAlignmentOptions.Center);
            AutoSize(_statusSub, 18f, 20f);
            _next = Text(p, "", TextRole.Number, 28, th.ink, new Vector2(0f, -334f), new Vector2(inner, 36f), TextAlignmentOptions.Center, TextLook.Outline);
        }

        void BuildEdictRows(float blockH, float inner)
        {
            var th = UiTheme.Current;
            var mid = new Vector2(0.5f, 0.5f);
            _edictRows = new EdictRow[2];
            float rowW = inner - 24f;
            for (int i = 0; i < _edictRows.Length; i++)
            {
                float y = blockH * 0.5f - 30f - 6f - RowH * 0.5f - i * (RowH + 8f);
                var v = new EdictRow();
                v.Root = UiFactory.Rect("Edict" + i, _edictsBlock, mid, mid, new Vector2(0f, y), new Vector2(rowW, RowH));
                UiFactory.Image("Edge", v.Root, Hurt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, RowH - 8f), PixelIcons.White);
                float half = rowW * 0.5f;
                var iconAt = new Vector2(-half + 48f, 0f);
                UiFactory.Image("IconPlate", v.Root, PlateColor, mid, mid, iconAt, new Vector2(68f, 68f), PixelIcons.White);
                v.Icon = UiFactory.Rect("Icon", v.Root, mid, mid, iconAt, new Vector2(60f, 60f)).gameObject.AddComponent<RawImage>();
                v.Icon.raycastTarget = false;
                v.Glyph = Text(v.Root, "", TextRole.Title, 36, Hurt, iconAt, new Vector2(60f, 60f), TextAlignmentOptions.Center);
                float x0 = -half + 96f, x1 = half - 196f, cx = (x0 + x1) * 0.5f, w = x1 - x0;
                v.Name = Text(v.Root, "", TextRole.Title, 26, th.ink, new Vector2(cx, 20f), new Vector2(w, 34f), TextAlignmentOptions.Left);
                AutoSize(v.Name, 18f, 26f);
                v.Line = Text(v.Root, "", TextRole.Body, 20, th.ink, new Vector2(cx, -16f), new Vector2(w, 46f), TextAlignmentOptions.TopLeft);
                v.Line.textWrappingMode = TextWrappingModes.Normal;
                AutoSize(v.Line, 18f, 20f);
                v.Share = Text(v.Root, "", TextRole.Number, 22, th.gold, new Vector2(half - 98f, 0f), new Vector2(176f, 30f), TextAlignmentOptions.Right, TextLook.Outline);
                AutoSize(v.Share, 18f, 22f);
                _edictRows[i] = v;
            }
        }

        static RectTransform Card(RectTransform p, string name, Vector2 pos)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var r = UiFactory.Rect(name, p, mid, mid, pos, new Vector2(CardW, CardH));
            UiKit.Frame("Back", r, FrameKind.Bronze, mid, mid, Vector2.zero, new Vector2(CardW, CardH), Color.white);
            return r;
        }

        void BuildLedger(RectTransform p)
        {
            var th = UiTheme.Current;
            var size = new Vector2(RightW, Size.y);
            _ledgerFrame = Panel(p, size, FrameKind.Wax);
            var mid = new Vector2(0.5f, 0.5f);
            float inner = RightW - Pad * 2f, half = inner * 0.5f;
            Text(p, "THE LEDGER", TextRole.Title, 34, th.gold, new Vector2(0f, 346f), new Vector2(inner, 44f), TextAlignmentOptions.Left);
            _ledgerCounts = Text(p, "", TextRole.Title, 22, th.waxInk, new Vector2(0f, 344f), new Vector2(inner, 30f), TextAlignmentOptions.Right);
            AutoSize(_ledgerCounts, 18f, 22f);
            _ledger = new LedgerRow[LedgerShown];
            var band = th.night;
            band.a = 0.35f;
            for (int i = 0; i < LedgerShown; i++)
            {
                var v = new LedgerRow();
                float y = LedgerTop - LedgerRowH * 0.5f - i * LedgerPitch;
                v.Root = UiFactory.Rect("Row" + i, p, mid, mid, new Vector2(0f, y), new Vector2(inner, LedgerRowH));
                UiFactory.Image("Back", v.Root, band, mid, mid, Vector2.zero, new Vector2(inner, LedgerRowH), PixelIcons.White);
                v.Frame = UiKit.Frame("Frame", v.Root, FrameKind.Focus, mid, mid, Vector2.zero, new Vector2(inner, LedgerRowH), th.gold);
                v.Frame.enabled = false;
                float x = -half + 16f;
                v.Date = Text(v.Root, "", TextRole.Number, 26, th.waxInk, new Vector2(x + 50f, 14f), new Vector2(100f, 32f), TextAlignmentOptions.Left, TextLook.Outline);
                v.Year = Text(v.Root, "", TextRole.Body, 18, th.dust, new Vector2(x + 50f, -18f), new Vector2(100f, 26f), TextAlignmentOptions.Left);
                x += 112f;
                v.Who = Text(v.Root, "", TextRole.Title, 22, th.waxInk, new Vector2(x + 145f, 14f), new Vector2(290f, 32f), TextAlignmentOptions.Left);
                AutoSize(v.Who, 18f, 22f);
                v.Where = Text(v.Root, "", TextRole.Body, 20, th.dust, new Vector2(x + 145f, -18f), new Vector2(290f, 28f), TextAlignmentOptions.Left);
                AutoSize(v.Where, 18f, 20f);
                x += 298f;
                v.Outcome = Text(v.Root, "", TextRole.Title, 22, th.gold, new Vector2(x + 110f, 14f), new Vector2(220f, 32f), TextAlignmentOptions.Center);
                v.Detail = Text(v.Root, "", TextRole.Number, 20, th.waxInk, new Vector2(x + 110f, -18f), new Vector2(220f, 28f), TextAlignmentOptions.Center, TextLook.Outline);
                AutoSize(v.Detail, 18f, 20f);
                v.Renown = Text(v.Root, "", TextRole.Number, 30, th.goldBright, new Vector2(half - 14f - 54f, 0f), new Vector2(108f, 40f), TextAlignmentOptions.Right, TextLook.Outline);
                AutoSize(v.Renown, 18f, 30f);
                v.Root.gameObject.SetActive(false);
                _ledger[i] = v;
            }
            _ledgerEmpty = Text(p, "No dailies yet. Your first scored attempt writes the first line.", TextRole.Body, 24, th.dust, new Vector2(0f, LedgerTop - LedgerRowH * 0.5f), new Vector2(inner - 40f, 70f), TextAlignmentOptions.Center);
            _ledgerEmpty.textWrappingMode = TextWrappingModes.Normal;
        }

        // ------------------------------------------------------------------ show

        /// <summary>Shows `plan` (today's) against `save` (scored or practice, today's result, the ledger) at `utcNow`.</summary>
        public void Show(in DailyPlan plan, SaveData save, MetaConfig meta, DateTime utcNow)
        {
            if (_box == null) return;
            _plan = plan;
            _date = plan.Date > 0 ? plan.Date : DailyGames.DateKey(utcNow);
            save ??= MetaProgress.Data;
            bool valid = plan.IsValid;
            bool attempted = ReferenceEquals(save, MetaProgress.Data)
                ? MetaProgress.DailyAttempted(_date)
                : save.dailyAttempt >= _date;
            Scored = valid && !attempted;

            // the arena and the gladiator
            var run = plan.Run;
            ArenaLadder.SetThumbnail(_thumb, run, run != null, ArenaFallback);
            _arenaName.text = run != null ? (run.DisplayName ?? run.id).ToUpperInvariant() : "???";
            _arenaLine.text = run != null && !string.IsNullOrEmpty(run.tagline) ? run.tagline : "";
            var g = plan.Gladiator;
            RosterGrid.SetPortrait(_portrait, g, g != null, _codex);
            _gladName.text = g != null ? (g.displayName ?? g.id).ToUpperInvariant() : "???";
            _gladLine.text = g != null ? g.SelectLine ?? "" : "";

            // the edicts
            var edicts = plan.Edicts ?? Array.Empty<RunModifierDef>();
            EdictCount = 0;
            for (int i = 0; i < _edictRows.Length; i++)
            {
                var d = i < edicts.Length ? edicts[i] : null;
                _edictRows[i].Root.gameObject.SetActive(d != null);
                if (d == null) continue;
                DrawEdict(_edictRows[i], d);
                EdictCount++;
            }
            if (edicts.Length > _edictRows.Length) // more than the two the spec proclaims (MetaConfig.dailyEdicts raised): the page lists them all
                _edictsLabel.text = "THE DAY'S EDICTS  <color=#FFFFFF73>+" + (edicts.Length - _edictRows.Length) + " more: click to read them all</color>";
            else _edictsLabel.text = EdictCount > 0 ? "THE DAY'S EDICTS  <color=#FFFFFF73>click or E / Y to read them</color>" : "THE DAY'S EDICTS";
            _noEdicts.gameObject.SetActive(EdictCount == 0);
            _edictsBlock.GetComponent<Button>().interactable = EdictCount > 0;
            var th = UiTheme.Current;

            // the Renown: the daily's ×1.5 only pays on the scored attempt (struck through on practice)
            float daily = 1f + Mathf.Max(0f, meta != null ? meta.dailyRenownBonus : 0.5f);
            float edictMul = 1f + RunModifiers.Bonus(edicts);
            string dailyMul = EdictsPanel.MulText(daily);
            string s = Scored || !valid ? "RENOWN " + dailyMul : "RENOWN <color=" + StruckHex + "><s>" + dailyMul + "</s></color>";
            if (EdictCount > 0) s += " · EDICTS " + EdictsPanel.MulText(edictMul);
            _renown.text = s;

            // the attempt
            if (!valid)
            {
                UiKit.Style(_status, TextRole.Title, TextLook.Flat);
                _status.text = "THE EDITOR HAS NOTHING TO PROCLAIM";
                _status.color = th.dust;
                _statusSub.text = "No arena or gladiator to send out today.";
            }
            else if (Scored)
            {
                UiKit.Style(_status, TextRole.Title, TextLook.Flat);
                _status.text = "SCORED ATTEMPT";
                _status.color = th.gold;
                _statusSub.text = "Your first fight today counts: " + dailyMul + " Renown and a line in the ledger.";
            }
            else
            {
                UiKit.Style(_status, TextRole.Body, TextLook.Flat);
                _status.text = TodaysRecord(save, _date, out var rec) ? "PRACTICE · " + ResultLine(rec) : "PRACTICE · today's scored attempt was left unfinished";
                _status.color = th.dust;
                _statusSub.text = "Practice pays as a normal run: no daily bonus, no ledger line.";
            }

            DrawLedger(save);
            _clockSecond = long.MinValue;
            _clockMinutes = int.MinValue;
            Tick(utcNow);
            Refresh();
        }

        void DrawEdict(EdictRow v, RunModifierDef d)
        {
            bool hasIcon = d.icon != null && d.iconRow >= 0;
            v.Icon.enabled = hasIcon;
            if (hasIcon)
            {
                float cw = (float)UpgradeIconView.Cell / d.icon.width, ch = (float)UpgradeIconView.Cell / d.icon.height;
                v.Icon.texture = d.icon;
                v.Icon.uvRect = new Rect(0f, 1f - (d.iconRow + 1) * ch, cw, ch);
            }
            v.Glyph.text = hasIcon ? "" : EdictsPanel.Initial(d);
            v.Name.text = d.displayName;
            v.Line.text = !string.IsNullOrEmpty(d.effectLine) ? d.effectLine : d.description ?? "";
            v.Share.text = EdictsPanel.Share(d);
        }

        /// <summary>Today's scored result, if the attempt has finished.</summary>
        static bool TodaysRecord(SaveData save, int date, out DailyRecord rec)
        {
            rec = default;
            var list = save != null ? save.dailies : null;
            if (list == null) return false;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].date == date) { rec = list[i]; return true; }
            return false;
        }

        void DrawLedger(SaveData save)
        {
            var th = UiTheme.Current;
            var list = save.dailies;
            int n = list != null ? Mathf.Min(LedgerShown, list.Count) : 0;
            LedgerRows = n;
            for (int i = 0; i < _ledger.Length; i++)
            {
                var v = _ledger[i];
                bool on = i < n;
                v.Root.gameObject.SetActive(on);
                if (!on) continue;
                var r = list[list.Count - 1 - i]; // newest first
                v.Date.text = DateShort(r.date);
                v.Year.text = r.date > 0 ? (r.date / 10000).ToString(CultureInfo.InvariantCulture) : "";
                v.Who.text = GladiatorName(r.gladiator);
                v.Where.text = RunName(r.run);
                v.Outcome.text = OutcomeWord(r.outcome);
                // a death is Roman red's soft tone on the wax, not the orange-red of what hurts you (spec §1.2)
                v.Outcome.color = r.outcome == (int)RunOutcome.Died ? th.loss : r.outcome == (int)RunOutcome.Survived ? th.gold : th.dust;
                v.Detail.text = Clock(r.elapsed) + " · " + r.kills.ToString("N0", CultureInfo.InvariantCulture) + " kills";
                v.Renown.text = "+" + r.renown.ToString(CultureInfo.InvariantCulture);
                v.Frame.enabled = r.date == _date;
                // laurel means victory (spec §1.3): a won day's outcome between two small branches
                if (v.Laurels != null) { UnityEngine.Object.Destroy(v.Laurels.gameObject); v.Laurels = null; }
                if (r.outcome == (int)RunOutcome.Survived)
                {
                    v.Outcome.ForceMeshUpdate(true, true);
                    v.Laurels = UiKit.Laurels("Laurels", v.Outcome, 6f);
                }
            }
            _ledgerEmpty.gameObject.SetActive(n == 0);
            _ledgerCounts.text = "PLAYED " + save.dailiesPlayed + "  ·  WON " + save.dailiesWon;
        }

        string GladiatorName(string id)
        {
            foreach (var g in _gladiators) if (g.id == id) return g.displayName;
            return Words(id);
        }

        string RunName(string id)
        {
            foreach (var r in _runs) if (r.id == id) return r.DisplayName;
            return Words(id);
        }

        /// <summary>"slave_pit" → "Slave Pit" (an id no data knows any more).</summary>
        static string Words(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            var parts = id.Replace('-', '_').Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        /// <summary>The clock to the next daily, rewritten when its minute changes (checked once a second). True when a new
        /// UTC day has begun since Show: the menu then shows the new plan.</summary>
        public bool Tick(DateTime utcNow)
        {
            if (_box == null) return false;
            long second = utcNow.Ticks / TimeSpan.TicksPerSecond;
            if (second == _clockSecond) return false;
            _clockSecond = second;
            if (_date > 0 && DailyGames.DateKey(utcNow) != _date) return true;
            var left = DailyGames.UntilNext(utcNow);
            int minutes = Mathf.Max(0, (int)Math.Floor(left.TotalMinutes));
            if (minutes != _clockMinutes)
            {
                _clockMinutes = minutes;
                _next.text = "NEXT DAILY IN " + Countdown(left);
            }
            return false;
        }

        /// <summary>Scales both blocks (down, never up) about their centre.</summary>
        public void Fit(float scale)
        {
            if (_box != null) _box.localScale = new Vector3(scale, scale, 1f);
        }

        public void Hover(int id, bool on)
        {
            _hover = on;
            Refresh();
        }

        void Refresh()
        {
            if (_edictsRing != null) _edictsRing.color = _hover && EdictCount > 0 ? Pale : Color.clear;
        }

        static void AutoSize(TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        static TextMeshProUGUI Text(Transform parent, string text, TextRole role, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align,
            TextLook look = TextLook.Flat)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var t = UiKit.Text("Line", parent, text, role, size, color, align, mid, mid, pos, box, look);
            t.overflowMode = TextOverflowModes.Truncate; // never Ellipsis (docs/tracks/README.md)
            return t;
        }
    }
}
