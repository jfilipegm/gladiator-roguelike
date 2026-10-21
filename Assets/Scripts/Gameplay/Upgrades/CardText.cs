using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// Card descriptions written from the data with the rarity's real numbers ("+17% Damage"), allocation-free. A Vow's
    /// drawbacks follow its upside in red, at the gentler drawback magnitude.
    /// </summary>
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
                if (d.evolvesFrom != null) { t.Append("Evolves ").Append(d.evolvesFrom.displayName).Append('\n').Append(d.weapon.description); return; }
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
            if (d.IsVow && d.drawbacks != null) // P1: the price, in the hurts-you colour
            {
                float price = config.DrawbackMagnitude(o.Rarity);
                for (int i = 0; i < d.drawbacks.Length; i++)
                {
                    Line(t, DrawbackColor);
                    Mod(d.drawbacks[i], price, t);
                    t.Append("</color>");
                }
            }
            for (int i = 0; d.effects != null && i < d.effects.Length; i++)
            {
                if (t.Length > 0) t.Append('\n');
                Effect(d.effects[i], scale, t);
            }
        }

        const string EvoColor = "#FFC94A", TagColor = "#8CEBFF";
        /// <summary>A Vow's drawback lines (orange-red: anything that hurts the player, CLAUDE.md §5).</summary>
        public const string DrawbackColor = "#FF6A55";

        static CharBuffer Line(CharBuffer t, string color)
        {
            if (t.Length > 0) t.Append('\n');
            return t.Append("<color=").Append(color).Append('>');
        }

        public static string TagName(UpgradeTag tag) => tag switch
        {
            UpgradeTag.Fire => "FIRE", UpgradeTag.Lightning => "LIGHTNING", UpgradeTag.Blood => "BLOOD", UpgradeTag.Curse => "CURSE",
            UpgradeTag.Summon => "SUMMON", UpgradeTag.Heavy => "HEAVY", UpgradeTag.Crit => "CRIT", UpgradeTag.Gold => "GOLD",
            UpgradeTag.Movement => "MOVEMENT", UpgradeTag.Defense => "DEFENSE", UpgradeTag.Xp => "XP", _ => "TAG",
        };

        /// <summary>
        /// Phase 5.5 hints under a card's description (rich text, allocation-free): a weapon's evolution pairing, what a
        /// catalyst Boon evolves, and how far a new upgrade takes each tag toward its synergy ("FIRE 2/3: Wildfire").
        /// </summary>
        public static void Hints(in CardOffer o, BuildState build, UpgradeCatalog catalog, CharBuffer t)
        {
            var d = o.Def;
            if (d == null || build == null || catalog == null || d.kind == UpgradeKind.Consumable) return;
            var ups = catalog.upgrades;
            for (int i = 0; ups != null && i < ups.Length; i++)
            {
                var e = ups[i];
                if (e == null || e.evolvesFrom == null || e.catalyst == null || build.LevelOf(e) > 0) continue;
                if (d.kind == UpgradeKind.Weapon && e.evolvesFrom == d)
                    Line(t, EvoColor).Append("Lv ").Append(d.maxLevel, 0).Append(" + ").Append(e.catalyst.displayName).Append(" = evolution</color>");
                else if (d == e.catalyst && o.IsNew && build.LevelOf(e.evolvesFrom) > 0)
                {
                    Line(t, EvoColor).Append("Evolves your ").Append(e.evolvesFrom.displayName);
                    if (build.LevelOf(e.evolvesFrom) >= e.evolvesFrom.maxLevel) t.Append(": open a chest!</color>");
                    else t.Append(" at Lv ").Append(e.evolvesFrom.maxLevel, 0).Append("</color>");
                }
            }
            // a new upgrade (or an evolution's new tags) moves its tags toward their synergies
            UpgradeTag gain = d.evolvesFrom != null ? d.tags & ~d.evolvesFrom.tags : o.IsNew ? d.tags : UpgradeTag.None;
            var syns = catalog.synergies;
            for (int i = 0; gain != UpgradeTag.None && syns != null && i < syns.Length; i++)
            {
                var s = syns[i];
                if (s == null || (gain & s.tag) == 0 || build.HasSynergy(s)) continue;
                int after = build.TagCount(s.tag) + 1;
                if (after >= s.count) Line(t, TagColor).Append(TagName(s.tag)).Append(' ').Append(s.count, 0).Append('/').Append(s.count, 0).Append(": ").Append(s.displayName).Append("!</color>");
                else Line(t, TagColor).Append(TagName(s.tag)).Append(' ').Append(after, 0).Append('/').Append(s.count, 0).Append(": ").Append(s.displayName).Append("</color>");
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
                case EffectKind.StatusSpread:
                    t.Append("Every ").Append(e.duration, 1).Append(" s ").Append(Adj(e.status)).Append(" enemies spread it to a neighbour");
                    break;
                case EffectKind.ExecuteOnCrit:
                    t.Append("Crits kill enemies below ").Append(e.bonus * 100f, 0).Append("% HP (not bosses)");
                    break;
                case EffectKind.GroundOnHit:
                    t.Append("Hits leave ground that applies ").Append(Name(e.status)).Append(" (").Append(e.power * scale, 1).Append("/s)");
                    break;
            }
        }

        /// <summary>One modifier as a card line: "+17% Damage", "+28 Max HP", "+0.6 HP Regen/s", "-20% Max HP" (×0.8).</summary>
        public static void Mod(in StatMod m, float scale, CharBuffer t)
        {
            float v = m.Scaled(scale);
            if (m.op == ModOp.Multiply) // as the change it makes: ×0.8 reads "-20%", ×1.25 "+25%"
            {
                float pct = (v - 1f) * 100f;
                t.Append(pct < 0f ? '-' : '+');
                pct = Mathf.Abs(pct);
                t.Append(pct, pct >= 10f ? 0 : 1).Append('%');
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
