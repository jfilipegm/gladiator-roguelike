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
            if (d.kind == UpgradeKind.Weapon && d.weapon != null)
            {
                if (o.Level <= 1) { t.Append(d.weapon.description); return; }
                int step = o.Level - 2;
                if (d.weapon.levels == null || step >= d.weapon.levels.Length) return;
                var mods = d.weapon.levels[step].mods;
                for (int i = 0; mods != null && i < mods.Length; i++)
                {
                    if (i > 0) t.Append('\n');
                    Mod(mods[i], scale, t);
                }
                return;
            }
            for (int i = 0; i < d.perLevel.Length; i++)
            {
                if (i > 0) t.Append('\n');
                Mod(d.perLevel[i], scale, t);
            }
            for (int i = 0; d.effects != null && i < d.effects.Length; i++)
            {
                if (t.Length > 0) t.Append('\n');
                Effect(d.effects[i], scale, t);
            }
        }

        static string Name(StatusDef s) => s != null ? s.displayName : "?";
        static string Adj(StatusDef s) => s != null ? s.adjective : "?";

        /// <summary>One Boon effect as a card line, its headline number at this card's rarity.</summary>
        public static void Effect(in BoonEffect e, float scale, CharBuffer t)
        {
            switch (e.kind)
            {
                case EffectKind.StatusOnHit:
                    t.Append(e.chance * scale * 100f, 0).Append("% chance on hit to ").Append(Name(e.status));
                    break;
                case EffectKind.StatusOnCrit:
                    t.Append("Crits apply ").Append(Mathf.Max(1, e.stacks), 0).Append(' ').Append(Name(e.status))
                        .Append(" (").Append(e.power * scale, 1).Append("/s each)");
                    break;
                case EffectKind.BurstOnDeath:
                    t.Append(Adj(e.status)).Append(" enemies burst on death: ").Append(e.power * scale, 0)
                        .Append(" damage, spreading ").Append(Name(e.status));
                    break;
                case EffectKind.ChainSpark:
                    t.Append("Every ").Append(e.everyNth, 0).Append(" hits a spark jumps to ").Append(e.count, 0)
                        .Append(" enemies (").Append(e.power * scale, 0).Append(" damage, ").Append(Name(e.status)).Append(')');
                    if (e.status != null && e.status.damageTakenBonus > 0f)
                        t.Append('\n').Append(Adj(e.status)).Append(" enemies take +").Append(e.status.damageTakenBonus * 100f, 0).Append("% damage");
                    break;
                case EffectKind.SparkBonus:
                    t.Append("Sparks deal +").Append(e.bonus * scale * 100f, 0).Append("% to ").Append(Adj(e.status))
                        .Append(" enemies and apply ").Append(Name(e.status));
                    break;
                case EffectKind.DamagePerStack:
                    t.Append('+').Append(e.bonus * scale * 100f, 1).Append("% damage per ").Append(Name(e.status)).Append(" stack");
                    break;
                case EffectKind.DamageVsStatus:
                    t.Append('+').Append(e.bonus * scale * 100f, 0).Append("% damage to ").Append(Adj(e.status)).Append(" enemies");
                    break;
                case EffectKind.SlideTrail:
                    t.Append("Slides leave a trail that applies ").Append(Name(e.status)).Append(" (").Append(e.power * scale, 1).Append("/s)");
                    break;
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
