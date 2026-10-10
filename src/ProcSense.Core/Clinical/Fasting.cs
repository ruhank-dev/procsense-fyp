using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Clinical
{
    /// <summary>The fasting hours for each kind of intake, loaded from content/fasting/fasting.json (built from the verified fasting table).</summary>
    public sealed class FastingRules
    {
        public FastingRules(IReadOnlyDictionary<string, double> hours, double unknownKindHours) { Hours = hours; UnknownKindHours = unknownKindHours; }

        public static readonly FastingRules Empty = new FastingRules(new Dictionary<string, double>(), 0);

        /// <summary>Hours of fasting needed before anaesthesia, by kind: clear_liquids, milk, light_meal, heavy_meal.</summary>
        public IReadOnlyDictionary<string, double> Hours { get; }

        /// <summary>What is asked when the kind is not known: the longest fast in the table.</summary>
        public double UnknownKindHours { get; }

        public static FastingRules Parse(string json, string source = "fasting")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var classes = root.Obj("classes");
            var hours = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var kind in new[] { "clear_liquids", "milk", "light_meal", "heavy_meal" })
            {
                var c = classes.Obj(kind);
                double h = c.Num("hours");
                if (h <= 0) throw new ContentException(c.Sub("hours") + ": must be more than 0");
                c.Str("ref"); c.Finish();
                hours[kind] = h;
            }
            classes.Finish();
            double unknown = root.Num("unknown_kind_hours");
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            if (unknown != hours.Values.Max()) throw new ContentException(root.Sub("unknown_kind_hours") + ": must be the longest fast in the table");
            return new FastingRules(hours, unknown);
        }
    }

    /// <summary>What the patient last took: the kind (none, unknown, clear_liquids, milk, light_meal, heavy_meal) and hours before now (null = not stated).</summary>
    public sealed record Intake(string Kind, double? HoursBeforeNow);

    /// <summary>One of the two checks (food or drink) with the hours it needed and the hours it will have had at anaesthesia.</summary>
    public sealed record FastingItem(string Which, string Kind, double RequiredHours, double? HoursAtAnaesthesia, string Status);

    /// <summary>
    /// Status is unknown (not both asked yet), meets, breach or uncertain (a kind or time that is not known and the longest fast not yet reached). <see cref="HoursUntilSafe"/> is how long from
    /// now until the rule is met (0 when it is). <see cref="Plan"/> is "rsi" when an emergency patient's stomach is not empty: a full-stomach anaesthetic plan, never a cancellation.
    /// </summary>
    public sealed record FastingResult(string Status, IReadOnlyList<FastingItem> Items, double HoursUntilSafe, string Plan);

    /// <summary>
    /// Rule R-FAST-01: the fast is judged at the time of anaesthesia, not when the trainee asks. Each of the last meal and the last drink needs its own minimum number of hours before anaesthesia
    /// (the exact hours are inclusive: exactly 2 hours after clear liquids meets the rule). Gum and regular tablets with a sip of water do not break the fast (they are never counted).
    /// </summary>
    public static class Fasting
    {
        public static FastingResult Assess(FastingRules rules, Intake? food, Intake? drink, double hoursToAnaesthesia, bool emergency)
        {
            if (hoursToAnaesthesia < 0) throw new ArgumentException("anaesthesia cannot be before now");
            if (food == null || drink == null)
                return new FastingResult("unknown", new List<FastingItem>(), 0, "");
            var items = new List<FastingItem> { Item(rules, "food", food, hoursToAnaesthesia), Item(rules, "drink", drink, hoursToAnaesthesia) };
            string status = items.Any(i => i.Status == "breach") ? "breach" : items.Any(i => i.Status == "uncertain") ? "uncertain" : "meets";
            double until = 0;
            foreach (var i in items)
                if (i.Status != "meets" && i.HoursAtAnaesthesia.HasValue) until = Math.Max(until, i.RequiredHours - (i.HoursAtAnaesthesia.Value - hoursToAnaesthesia));
            return new FastingResult(status, items, Math.Round(until, 4), emergency && status != "meets" ? "rsi" : "");
        }

        private static FastingItem Item(FastingRules rules, string which, Intake intake, double toAnaesthesia)
        {
            double? at = intake.HoursBeforeNow.HasValue ? intake.HoursBeforeNow.Value + toAnaesthesia : (double?)null;
            if (intake.Kind == "none") return new FastingItem(which, "none", 0, null, "meets");
            if (intake.Kind == "unknown" || !rules.Hours.ContainsKey(intake.Kind))
            {
                // Not known what it was: the longest fast is asked for. Reaching it settles it; falling short cannot be called a breach.
                if (at.HasValue && at.Value >= rules.UnknownKindHours) return new FastingItem(which, "unknown", rules.UnknownKindHours, at, "meets");
                return new FastingItem(which, "unknown", rules.UnknownKindHours, at, "uncertain");
            }
            double need = rules.Hours[intake.Kind];
            if (!at.HasValue) return new FastingItem(which, intake.Kind, need, null, "uncertain");
            return new FastingItem(which, intake.Kind, need, at, at.Value >= need ? "meets" : "breach");
        }
    }
}
