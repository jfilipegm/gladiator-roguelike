using Gladiator.Core;
using UnityEngine;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// P1 stats page: how each stat reads (units, order, sections) and the page's text, written into a CharBuffer with
    /// TMP rich-text tags. Plain static code in Gameplay so EditMode tests can check it; StatsPanel (UI) only draws it.
    /// Allocation-free.
    /// </summary>
    public static class StatText
    {
        public enum Unit : byte { Whole, Decimal, Percent, Count }

        /// <summary>The page's order: three sections, every StatId once.</summary>
        public static readonly StatId[] Order =
        {
            StatId.MaxHp, StatId.HpRegen, StatId.Armor, StatId.MoveSpeed,
            StatId.Damage, StatId.AttackSpeed, StatId.CritChance, StatId.CritDamage, StatId.Area, StatId.Amount,
            StatId.ProjectileSpeed, StatId.Duration, StatId.Knockback, StatId.Pierce,
            StatId.PickupRange, StatId.XpGain, StatId.GoldGain, StatId.Luck, StatId.ExtraJumps, StatId.Rerolls, StatId.Banishes,
        };
        /// <summary>Order index where each section starts, and its title.</summary>
        public static readonly int[] SectionStart = { 0, 4, 14 };
        public static readonly string[] SectionTitle = { "DEFENSE", "OFFENSE", "UTILITY" };

        // colours (rich text): gains green, losses red, sources by kind
        public const string Gain = "#7CF08A", Loss = "#FF6A55", Dim = "#8E98A8", Head = "#FFD84A", ValueCol = "#FFFFFF",
            CardCol = "#E6ECF5", SynergyCol = "#8CEBFF", TraitCol = "#FFC94A", TempCol = "#FFA040", AltarCol = "#FF6688",
            AdminCol = "#FF66E0", VowCol = "#FF8A6A", EdictCol = "#FF5A3C"; // edicts hurt you: orange-red

        public static Unit UnitOf(StatId s) => s switch
        {
            StatId.MaxHp => Unit.Whole,
            StatId.HpRegen => Unit.Decimal,
            StatId.Amount or StatId.Pierce or StatId.ExtraJumps or StatId.Rerolls or StatId.Banishes => Unit.Count,
            _ => Unit.Percent, // multipliers (1 = 100 %) and fractions (0.06 = 6 %)
        };

        /// <summary>Bonus stats read with a sign: crit damage adds to each weapon's own crit multiplier, projectiles and pierce to its own.</summary>
        public static bool Signed(StatId s) => s is StatId.CritDamage or StatId.Amount or StatId.Pierce;

        /// <summary>Every stat is better higher (green when it rises, red when it falls).</summary>
        public static bool HigherIsBetter(StatId s) => true;

        /// <summary>Two values differ enough to show (half the shown precision).</summary>
        public static bool Differs(StatId s, float a, float b) => Mathf.Abs(a - b) > UnitOf(s) switch
        {
            Unit.Whole => 0.5f, Unit.Count => 0.5f, Unit.Decimal => 0.05f, _ => 0.0005f,
        };

        /// <summary>A stat's value as the page shows it: "138", "0.4", "117%", "6%", "+10%", "+1".</summary>
        public static CharBuffer Value(CharBuffer t, StatId s, float v)
        {
            var u = UnitOf(s);
            if (Signed(s) && v >= 0f) t.Append('+');
            switch (u)
            {
                case Unit.Whole: return t.Append(Mathf.Round(v), 0);
                case Unit.Count: return t.Append(Mathf.Floor(v + 1e-4f), 0);
                case Unit.Decimal: return t.Append(v, 1);
                default:
                    float pct = v * 100f;
                    return t.Append(pct, Mathf.Abs(pct) >= 10f ? 0 : 1).Append('%');
            }
        }

        /// <summary>A share of a stat in its units, always signed: "+17%", "−18", "+0.4", "+1".</summary>
        public static CharBuffer Delta(CharBuffer t, StatId s, float d)
        {
            var u = UnitOf(s);
            t.Append(d < 0f ? '−' : '+');
            float a = Mathf.Abs(d);
            switch (u)
            {
                case Unit.Whole: return t.Append(a, a >= 10f ? 0 : 1);
                case Unit.Count: return t.Append(a, 0);
                case Unit.Decimal: return t.Append(a, a >= 10f ? 0 : 1);
                default:
                    float pct = a * 100f;
                    return t.Append(pct, pct >= 10f ? 0 : 1).Append('%');
            }
        }

        /// <summary>"old → new", the new value green for a gain and red for a loss.</summary>
        public static CharBuffer Change(CharBuffer t, StatId s, float before, float after)
        {
            Value(t, s, before).Append(" → ");
            bool better = HigherIsBetter(s) ? after > before : after < before;
            t.Append("<color=").Append(better ? Gain : Loss).Append('>');
            return Value(t, s, after).Append("</color>");
        }

        /// <summary>A source's name on the page (`gladiator`: for sources that name them; the base reads "Base").</summary>
        public static string SourceName(in StatSource src, GladiatorDef gladiator)
        {
            switch (src.Kind)
            {
                case StatSourceKind.Base: return "Base"; // the gladiator's own number (the page's header names them)
                case StatSourceKind.Card:
                    var card = src.Ref as UpgradeDef;
                    return card != null ? card.displayName : "Card";
                case StatSourceKind.Synergy:
                    var syn = src.Ref as TagSynergyDef;
                    return syn != null ? syn.displayName : "Synergy";
                case StatSourceKind.Trait:
                    var trait = src.Ref as GladiatorTrait;
                    return trait != null ? trait.displayName : "Trait";
                case StatSourceKind.Temporary: return src.Ref != null ? src.Ref.name : "Temporary";
                case StatSourceKind.Altar: return "Blood Altar";
                case StatSourceKind.Admin: return "ADMIN (F1)";
                case StatSourceKind.Edict:
                    var edict = src.Ref as RunModifierDef;
                    return "Edict: " + (edict != null ? edict.displayName : "?");
                default: return "Traits & effects";
            }
        }

        public static string SourceColor(in StatSource src)
        {
            switch (src.Kind)
            {
                case StatSourceKind.Base: return Dim;
                case StatSourceKind.Card: return src.Ref is UpgradeDef d && d.IsVow ? VowCol : CardCol;
                case StatSourceKind.Synergy: return SynergyCol;
                case StatSourceKind.Trait: return TraitCol;
                case StatSourceKind.Altar: return AltarCol;
                case StatSourceKind.Admin: return AdminCol;
                case StatSourceKind.Edict: return EdictCol;
                default: return TempCol; // traits' live bonuses, frenzies, the altar until it says so
            }
        }

        /// <summary>
        /// One stat's line for the page: name, total (and "→ new" from `preview`), then, if `sources`, a smaller line of
        /// where it comes from ("Base 100 · Thick Skin ×2 +40 · Blood Altar −21"). Stats with nothing but their base
        /// get no source line. `shares` is scratch (≥ 16). `valuePos`: the value column ("62%"), or null for one line.
        /// </summary>
        public static void StatLine(CharBuffer t, PlayerStats stats, StatId s, StatPreview preview, bool sources, StatShare[] shares,
            string valuePos = "62%", int sourcePercent = 80)
        {
            bool previewing = preview != null && preview.Active;
            bool changed = previewing && preview.Changed(s);
            t.Append("<color=").Append(changed ? ValueCol : previewing ? Dim : CardCol).Append('>').Append(StatInfo.Name(s)).Append("</color>");
            if (valuePos != null) t.Append("<pos=").Append(valuePos).Append('>');
            else t.Append("  ");
            if (changed) Change(t, s, preview.Before[(int)s], preview.After[(int)s]);
            else
            {
                t.Append("<color=").Append(previewing ? Dim : ValueCol).Append('>');
                Value(t, s, stats.Get(s)).Append("</color>");
            }
            if (!sources) return;
            int n = stats.Breakdown(s, shares);
            bool any = false;
            for (int i = 1; i < n; i++) if (Differs(s, shares[i].Amount, 0f)) { any = true; break; }
            if (!any) return;
            t.Append("\n<size=").Append(sourcePercent, 0).Append("%><indent=3%>");
            bool first = true;
            for (int i = 0; i < n; i++)
            {
                var sh = shares[i];
                if (i > 0 && !Differs(s, sh.Amount, 0f)) continue;
                if (sh.Source.Kind == StatSourceKind.Admin && !AdminVisible) continue;
                if (!first) t.Append("<color=").Append(Dim).Append("> · </color>");
                first = false;
                t.Append("<color=").Append(SourceColor(sh.Source)).Append('>').Append(SourceName(sh.Source, stats.gladiator));
                if (sh.Source.Kind == StatSourceKind.Card && sh.Count > 1) t.Append(" ×").Append(sh.Count, 0);
                t.Append(' ');
                if (i == 0) Value(t, s, sh.Amount);
                else Delta(t, s, sh.Amount);
                t.Append("</color>");
            }
            t.Append("</indent></size>");
        }

        /// <summary>Admin buffs show on the page only where F1 works (Editor and development builds).</summary>
        public static bool AdminVisible => Application.isEditor || Debug.isDebugBuild;
    }
}
