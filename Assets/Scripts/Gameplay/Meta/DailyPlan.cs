using System;
using System.Collections.Generic;
using System.Globalization;
using Gladiator.Core;

namespace Gladiator.Gameplay
{
    /// <summary>
    /// One day's Daily Games (R8, docs/tracks/R7-design.md §3.6): the date (UTC yyyymmdd), the run seed, the arena, the
    /// gladiator and the edicts. Everyone with the same unlocks gets the same plan on the same day.
    /// </summary>
    public struct DailyPlan
    {
        /// <summary>yyyymmdd, UTC (20261022).</summary>
        public int Date;
        public uint Seed;
        public RunDef Run;
        public GladiatorDef Gladiator;
        /// <summary>The day's edicts (handicaps, owned or not), in the order they were drawn. Never null in a plan from For.</summary>
        public RunModifierDef[] Edicts;

        public bool IsValid => Date > 0 && Run != null && Gladiator != null;
    }

    /// <summary>
    /// The daily's rules as pure functions: the UTC day, its seed (Hash(FNV-1a(salt), date, 0)) and its plan. Each pick
    /// has its own key off the seed (arena 1, gladiator 2, edicts 3), and every list is sorted by id first, so the plan
    /// never depends on the order the menu found things in. Allocates: menu only.
    /// </summary>
    public static class DailyGames
    {
        /// <summary>MetaConfig.dailySalt's default (bumped after a content change, so old dailies stop pretending to be comparable).</summary>
        public const string DefaultSalt = "gladiator-daily-1";

        static readonly string[] Months = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

        /// <summary>Today's date key, by the UTC clock (one daily for the whole world at a time).</summary>
        public static int Today() => DateKey(DateTime.UtcNow);

        /// <summary>yyyymmdd of `utc` (a local time is converted to UTC first).</summary>
        public static int DateKey(DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
            return utc.Year * 10000 + utc.Month * 100 + utc.Day;
        }

        /// <summary>The calendar day of a date key (false for 0 or a key that isn't a real day).</summary>
        public static bool TryDate(int date, out DateTime day)
        {
            day = default;
            if (date <= 0) return false;
            int y = date / 10000, m = date / 100 % 100, d = date % 100;
            if (y < 1 || y > 9999 || m < 1 || m > 12 || d < 1 || d > DateTime.DaysInMonth(y, m)) return false;
            day = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }

        /// <summary>"22 OCT 2026" (fixed English month names, whatever the machine's culture); "" for no date.</summary>
        public static string DateText(int date)
        {
            if (!TryDate(date, out var day)) return date > 0 ? date.ToString(CultureInfo.InvariantCulture) : "";
            return day.Day.ToString(CultureInfo.InvariantCulture) + " " + Months[day.Month - 1] + " " +
                   day.Year.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>How long until the next daily (the next UTC midnight).</summary>
        public static TimeSpan UntilNext(DateTime utcNow)
        {
            if (utcNow.Kind == DateTimeKind.Local) utcNow = utcNow.ToUniversalTime();
            return utcNow.Date.AddDays(1) - utcNow;
        }

        /// <summary>The day's run seed: Hash(FNV-1a(salt), date, 0). A new salt gives every day a new seed.</summary>
        public static uint SeedFor(int date, string salt) =>
            GameRandom.Hash((uint)GameInfo.StableId(salt ?? ""), (uint)date, 0u);

        /// <summary>
        /// The plan for `date`: an arena among `earnedRuns`, a gladiator among `earnedGladiators`, and meta.dailyEdicts
        /// distinct edicts among the dailyEligible ones in meta.edicts (owned or not). Nulls and repeats in the lists are
        /// ignored. No arena or no gladiator → an invalid plan (IsValid false).
        /// </summary>
        public static DailyPlan For(int date, MetaConfig meta, IList<RunDef> earnedRuns, IList<GladiatorDef> earnedGladiators)
        {
            string salt = meta != null && !string.IsNullOrEmpty(meta.dailySalt) ? meta.dailySalt : DefaultSalt;
            var plan = new DailyPlan { Date = date, Seed = SeedFor(date, salt), Edicts = Array.Empty<RunModifierDef>() };
            var runs = SortedById(earnedRuns, r => r.id);
            var gladiators = SortedById(earnedGladiators, g => g.id);
            if (date <= 0 || runs.Count == 0 || gladiators.Count == 0) return plan;

            plan.Run = runs[(int)(GameRandom.Hash(plan.Seed, 1u, 0u) % (uint)runs.Count)];
            plan.Gladiator = gladiators[(int)(GameRandom.Hash(plan.Seed, 2u, 0u) % (uint)gladiators.Count)];
            plan.Edicts = DrawEdicts(plan.Seed, meta);
            return plan;
        }

        /// <summary>meta.dailyEdicts distinct dailyEligible edicts: a partial Fisher–Yates over them sorted by id, step i
        /// drawing with Hash(seed, 3, i).</summary>
        static RunModifierDef[] DrawEdicts(uint seed, MetaConfig meta)
        {
            if (meta == null || meta.edicts == null || meta.dailyEdicts <= 0) return Array.Empty<RunModifierDef>();
            var eligible = new List<RunModifierDef>(meta.edicts.Length);
            foreach (var e in meta.edicts) if (e != null && e.dailyEligible) eligible.Add(e);
            eligible = SortedById(eligible, e => e.id);
            for (int i = eligible.Count - 1; i > 0; i--) // one per id: two defs with one id would be the same edict twice
                if (string.Equals(eligible[i].id, eligible[i - 1].id, StringComparison.Ordinal)) eligible.RemoveAt(i);
            int k = Math.Min(meta.dailyEdicts, eligible.Count);
            var picked = new RunModifierDef[k];
            for (int i = 0; i < k; i++)
            {
                int j = i + (int)(GameRandom.Hash(seed, 3u, (uint)i) % (uint)(eligible.Count - i));
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
                picked[i] = eligible[i];
            }
            return picked;
        }

        /// <summary>A copy without nulls or repeats, sorted by id (ordinal), ties by asset name: the input order never matters.</summary>
        static List<T> SortedById<T>(IList<T> source, Func<T, string> id) where T : UnityEngine.Object
        {
            var list = new List<T>(source != null ? source.Count : 0);
            if (source != null)
                foreach (var x in source)
                    if (x != null && !list.Contains(x)) list.Add(x);
            list.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(id(a), id(b));
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            return list;
        }
    }
}
