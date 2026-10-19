using System;
using Gladiator.Gameplay;
using TMPro;
using UnityEngine;

namespace Gladiator.UI
{
    /// <summary>
    /// The Codex (M3.6 stub, opened from the main menu): your records, every unlock (earned ones in full, locked ones as
    /// "???" and a cryptic hint), and the gladiators (locked ones as silhouettes with a hint). Built once from the save
    /// when the menu loads; the menu scene reloads after every run, so it's always current.
    /// </summary>
    public sealed class CodexPanel
    {
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Soft = new Color(1f, 1f, 1f, 0.75f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color Card = new Color(0.08f, 0.1f, 0.14f, 0.94f);

        readonly TMP_FontAsset _font;
        readonly Material _fontMat;

        public CodexPanel(TMP_FontAsset font, Material fontMat)
        {
            _font = font;
            _fontMat = fontMat;
        }

        public void Build(Transform root, SaveData save, MetaConfig meta, GladiatorDef[] gladiators, MainMenu.Locked[] locked, Action back)
        {
            var mid = new Vector2(0.5f, 0.5f);
            Text(root, "CODEX", 64, Gold, new Vector2(0, 400), new Vector2(1400, 90)).fontStyle = FontStyles.Bold;

            // records
            var rec = Column(root, -560f, "RECORDS");
            int y = 0;
            Row(rec, "Renown", save.renown.ToString(), y++);
            Row(rec, "Runs", save.runs.ToString(), y++);
            Row(rec, "Victories", save.victories.ToString(), y++);
            Row(rec, "Best time", Clock(save.bestTime), y++);
            Row(rec, "Kills", save.lifetimeKills.ToString(), y++);
            Row(rec, "Gold earned", save.lifetimeGold.ToString(), y++);
            Row(rec, "Gorm defeated", SaveData.Count(save.bossKills, "gorm") + "×", y++);

            // unlocks
            var ul = Column(root, 0f, "UNLOCKS");
            float uy = 170f;
            if (meta != null)
                foreach (var u in meta.unlocks)
                {
                    if (u == null || UnlocksGladiator(u, gladiators)) continue; // those live in the gladiators column
                    bool open = save.IsUnlocked(u.id);
                    var name = Text(ul, open ? u.displayName.ToUpperInvariant() : "???", 34, open ? Gold : Dim, new Vector2(0, uy), new Vector2(480, 46));
                    name.fontStyle = FontStyles.Bold;
                    var body = Text(ul, open ? u.description : u.hint, 24, open ? Soft : new Color(1f, 0.84f, 0.3f, 0.65f), new Vector2(0, uy - 72), new Vector2(480, 100));
                    body.fontStyle = open ? FontStyles.Normal : FontStyles.Italic;
                    uy -= 190f;
                }
            Text(ul, "More secrets wait in the arenas ahead.", 22, Dim, new Vector2(0, -250), new Vector2(480, 40)).fontStyle = FontStyles.Italic;

            // gladiators
            var gl = Column(root, 560f, "GLADIATORS");
            float gy = 170f;
            foreach (var g in gladiators)
            {
                if (g == null) continue;
                if (!MainMenu.IsAvailable(g)) { LockedGladiator(gl, ref gy, g.unlock != null ? g.unlock.hint : "???"); continue; }
                Text(gl, g.displayName.ToUpperInvariant(), 30, Gold, new Vector2(0, gy), new Vector2(480, 44)).fontStyle = FontStyles.Bold;
                Text(gl, "Unlocked", 22, Soft, new Vector2(0, gy - 36), new Vector2(480, 32)).fontStyle = FontStyles.Normal;
                gy -= 96f;
            }
            foreach (var l in locked) LockedGladiator(gl, ref gy, l.hint);

            UiFactory.Button("Back", root, "BACK", mid, mid, new Vector2(0, -420), new Vector2(240, 76), _font, _fontMat, () => back(), 34f);
        }

        void LockedGladiator(Transform gl, ref float gy, string hint)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var sil = UiFactory.Image("Silhouette", gl, new Color(0f, 0f, 0f, 0.85f), mid, mid, new Vector2(-212, gy - 16), new Vector2(36, 66), PixelIcons.White);
            sil.raycastTarget = false;
            Text(gl, "???", 30, Dim, new Vector2(30, gy), new Vector2(400, 44));
            Text(gl, hint, 21, new Color(1f, 0.84f, 0.3f, 0.65f), new Vector2(30, gy - 40), new Vector2(400, 56)).fontStyle = FontStyles.Italic;
            gy -= 112f;
        }

        static bool UnlocksGladiator(UnlockDef u, GladiatorDef[] gladiators)
        {
            foreach (var g in gladiators) if (g != null && g.unlock == u) return true;
            return false;
        }

        static string Clock(float t) => Mathf.FloorToInt(t / 60f) + ":" + Mathf.FloorToInt(t % 60f).ToString("00");

        Transform Column(Transform root, float x, string header)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var border = UiFactory.Image("Column", root, new Color(0.3f, 0.3f, 0.34f, 0.9f), mid, mid, new Vector2(x, -10), new Vector2(520, 640), PixelIcons.White);
            var inner = UiFactory.Image("Inner", border.transform, Card, mid, mid, Vector2.zero, new Vector2(510, 630), PixelIcons.White).transform;
            Text(inner, header, 34, Gold, new Vector2(0, 270), new Vector2(480, 50)).fontStyle = FontStyles.Bold;
            return inner;
        }

        void Row(Transform col, string label, string value, int row)
        {
            float y = 190f - row * 58f;
            var mid = new Vector2(0.5f, 0.5f);
            var l = UiFactory.Text(label, col, label, 28, Soft, TextAlignmentOptions.Left, mid, mid, new Vector2(-70, y), new Vector2(320, 46), _font, _fontMat);
            l.fontStyle = FontStyles.Normal;
            UiFactory.Text(label + "Value", col, value, 30, Color.white, TextAlignmentOptions.Right, mid, mid, new Vector2(140, y), new Vector2(180, 46), _font, _fontMat);
        }

        TextMeshProUGUI Text(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var t = UiFactory.Text("Line", parent, text, size, color, TextAlignmentOptions.Center, mid, mid, pos, box, _font, _fontMat);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
    }
}
