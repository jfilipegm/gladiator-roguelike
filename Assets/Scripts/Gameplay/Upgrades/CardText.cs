using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>Card descriptions written from the data with the rarity's real numbers ("+17% Damage"), allocation-free.</summary>
    public static class CardText
    {
        static readonly string[] RarityNames = { "COMMON", "UNCOMMON", "RARE", "LEGENDARY" };

        public static string RarityName(Rarity r) => RarityNames[(int)r];

        public static void Describe(in CardOffer o, UpgradeConfig config, CharBuffer t)
        {
            t.Clear();
            var d = o.Def;
            if (d == null) return;
            if (d.kind == UpgradeKind.Consumable)
            {
                if (d.healFraction > 0f) t.Append("Heal ").Append(d.healFraction * 100f, 0).Append("% HP");
                if (d.gold > 0)
                {
                    if (t.Length > 0) t.Append('\n');
                    t.Append('+').Append(d.gold, 0).Append(" Gold");
                }
                return;
            }
            float scale = config.Magnitude(o.Rarity);
            for (int i = 0; i < d.perLevel.Length; i++)
            {
                if (i > 0) t.Append('\n');
                Mod(d.perLevel[i], scale, t);
            }
        }

        /// <summary>One modifier as a card line: "+17% Damage", "+28 Max HP", "+0.6 HP Regen/s", "x1.25 Area".</summary>
        public static void Mod(in StatMod m, float scale, CharBuffer t)
        {
            float v = m.Scaled(scale);
            if (m.op == ModOp.Multiply)
            {
                t.Append('x').Append(v, 2);
            }
            else
            {
                bool pct = m.op == ModOp.Percent || StatInfo.IsFraction(m.stat);
                float shown = pct ? v * 100f : v;
                t.Append(shown < 0f ? '-' : '+');
                shown = Mathf.Abs(shown);
                bool whole = StatInfo.IsInteger(m.stat) || shown >= 10f;
                t.Append(shown, whole ? 0 : 1);
                if (pct) t.Append('%');
            }
            t.Append(' ').Append(StatInfo.Name(m.stat));
        }
    }
}
