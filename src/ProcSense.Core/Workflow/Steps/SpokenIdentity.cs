using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;

namespace ProcSense.Core.Workflow.Steps
{
    /// <summary>How well one thing the patient said (the name, the date of birth) fits her real identity.</summary>
    public enum Fit
    {
        Match,
        Mismatch,
        /// <summary>The patient did not say it (or only part of it): ask again, this is not a wrong answer.</summary>
        Missing,
        /// <summary>It could be right or wrong depending on how it is read (03/04/1984, a two-digit year). Never guessed.</summary>
        Ambiguous,
    }

    public sealed record SpokenCheck(Fit Name, Fit Date);

    /// <summary>
    /// Reads what the patient said about her name and date of birth, however it is phrased, and compares it with her real identity.
    /// shortcut: names are matched by exact words (no spelling tolerance) and the vocabulary is English; upgrade when speech recognition
    /// (p1-voice-input) or a second language (p2-ai-extensions) feeds this.
    /// </summary>
    public static class SpokenIdentity
    {
        private static readonly Regex IsoDate = new Regex(@"(?<!\d)(\d{4})-(\d{1,2})-(\d{1,2})(?!\d)");
        private static readonly Regex NumericDate = new Regex(@"(?<!\d)(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4}|\d{2})(?!\d)");
        private static readonly Regex Separator = new Regex(@"(?<!\d)[./\-]|[./\-](?!\d)");
        private static readonly Regex Words = new Regex("[a-z0-9]+");
        private static readonly Regex DayDigits = new Regex(@"^(\d{1,2})(st|nd|rd|th)?$");

        private static readonly string[] Units =
            { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        private static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
        private static readonly string[] Ordinals =
            { "", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth", "eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteenth", "nineteenth", "twentieth" };
        private static readonly string[] MonthNames =
            { "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december" };
        private static readonly HashSet<string> Filler = new HashSet<string>
        {
            "my", "name", "is", "i", "am", "im", "its", "it", "um", "uh", "er", "erm", "well", "yes", "yeah", "sure", "okay", "ok", "and", "date", "of", "birth", "dob",
            "born", "was", "on", "the", "in", "a", "an", "please", "thanks", "thank", "you", "hello", "hi", "mr", "mrs", "ms", "miss", "dr", "mister", "madam", "sir", "sept",
        };

        /// <summary>Lower case, accents removed, punctuation gone; "/", "-" and "." survive only between digits so numeric dates stay readable. Safe to apply twice.</summary>
        public static string Normalise(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
            string s = Regex.Replace(sb.ToString(), @"[^a-z0-9/.\-]+", " ");
            s = Separator.Replace(s, " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        public static SpokenCheck Compare(string spoken, Identity id)
        {
            DateTime.TryParseExact(id.Dob, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var born);
            string text = Normalise(spoken);
            Fit? numeric = NumericFit(ref text, born);
            var tokens = Words.Matches(text).Cast<Match>().Select(m => m.Value).ToList();
            var nameWords = Words.Matches(Normalise(id.FullName)).Cast<Match>().Select(m => m.Value).ToList();
            Fit date = numeric ?? WordDateFit(tokens, nameWords, born);
            return new SpokenCheck(NameFit(tokens, nameWords), date);
        }

        // ---- name ----

        private static Fit NameFit(List<string> tokens, List<string> nameWords)
        {
            // Words that are only there for the date or for politeness are not part of a name; a real name that looks like one (May, June) still counts.
            var notName = new HashSet<string>(Filler.Concat(MonthNames).Concat(Units).Concat(Tens).Concat(Ordinals).Concat(new[] { "thousand", "oh", "twentieth", "thirtieth", "hundred" }));
            notName.ExceptWith(nameWords);
            var said = tokens.Where(t => !notName.Contains(t) && (Month(t) == 0 || nameWords.Contains(t)) && !t.Any(char.IsDigit)).ToList();
            if (nameWords.All(said.Contains)) return Fit.Match;      // extra words (a middle name) are fine
            if (said.Count == 0) return Fit.Missing;                  // no name at all
            if (said.All(nameWords.Contains)) return Fit.Missing;     // only part of the name ("Ayesha")
            return Fit.Mismatch;                                      // a word that is not hers
        }

        // ---- numeric dates: 21/07/1990, 21-7-1990, 21.07.1990, 1990-07-21, 21/07/90 ----

        private static Fit? NumericFit(ref string text, DateTime born)
        {
            var iso = IsoDate.Match(text);
            if (iso.Success)
            {
                text = text.Remove(iso.Index, iso.Length);
                return Same(int.Parse(iso.Groups[3].Value, CultureInfo.InvariantCulture), int.Parse(iso.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(iso.Groups[1].Value, CultureInfo.InvariantCulture), born);
            }
            var m = NumericDate.Match(text);
            if (!m.Success) return null;
            text = text.Remove(m.Index, m.Length);
            int a = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), b = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            string y = m.Groups[3].Value;
            bool twoDigitYear = y.Length == 2;
            int year = int.Parse(y, CultureInfo.InvariantCulture);
            bool yearFits = twoDigitYear ? born.Year % 100 == year : born.Year == year;
            bool dayFirst = a == born.Day && b == born.Month, monthFirst = b == born.Day && a == born.Month;
            if (!yearFits || !(dayFirst || monthFirst)) return Fit.Mismatch;
            if (twoDigitYear) return Fit.Ambiguous;                                  // 84 could be 1984 or 2084
            bool bothReadings = a <= 12 && b <= 12 && a != b;                         // 03/04 is 3 April or 4 March
            return bothReadings ? Fit.Ambiguous : Fit.Match;
        }

        private static Fit Same(int day, int month, int year, DateTime born) =>
            day == born.Day && month == born.Month && year == born.Year ? Fit.Match : Fit.Mismatch;

        // ---- dates with a month word: "twenty-first of July 1990", "July 21, 1990", "21 jul nineteen ninety" ----

        private static Fit WordDateFit(List<string> tokens, List<string> nameWords, DateTime born)
        {
            // A month word standing next to her other name words is her name ("June Khan"), not the month she was born in.
            int monthAt = -1;
            for (int i = 0; i < tokens.Count && monthAt < 0; i++)
            {
                if (Month(tokens[i]) == 0) continue;
                bool nextToName = nameWords.Contains(tokens[i]) &&
                    ((i > 0 && nameWords.Contains(tokens[i - 1]) && tokens[i - 1] != tokens[i]) || (i + 1 < tokens.Count && nameWords.Contains(tokens[i + 1]) && tokens[i + 1] != tokens[i]));
                if (!nextToName) monthAt = i;
            }
            if (monthAt < 0) return Fit.Missing;
            int month = Month(tokens[monthAt]);
            var used = new HashSet<int> { monthAt };

            int? year = null;
            for (int i = 0; i < tokens.Count && year == null; i++)
                if (!used.Contains(i)) year = YearAt(tokens, i, used);

            int? day = null;
            for (int i = 0; i < tokens.Count && day == null; i++)
                if (!used.Contains(i)) day = DayAt(tokens, i, used);
            if (day == null) return Fit.Missing;

            if (year == null)
            {   // a leftover two-digit number is a two-digit year; anything else means the year was not said
                var yy = tokens.Where((t, i) => !used.Contains(i) && t.Length == 2 && t.All(char.IsDigit)).ToList();
                if (yy.Count == 0) return Fit.Missing;
                bool fits = day == born.Day && month == born.Month && int.Parse(yy[0], CultureInfo.InvariantCulture) == born.Year % 100;
                return fits ? Fit.Ambiguous : Fit.Mismatch;
            }
            return Same(day.Value, month, year.Value, born);
        }

        private static int Month(string t)
        {
            if (t == "sept") return 9;
            for (int i = 0; i < 12; i++)
                if (MonthNames[i] == t || (t.Length == 3 && MonthNames[i].StartsWith(t, StringComparison.Ordinal))) return i + 1;
            return 0;
        }

        // A two-digit cardinal at position i ("fourteen", "twenty one", "oh five"): value and words used, or null.
        private static (int value, int used)? Cardinal(List<string> t, int i)
        {
            if (i >= t.Count) return null;
            if (t[i] == "oh" && i + 1 < t.Count && Array.IndexOf(Units, t[i + 1]) is int o && o >= 1 && o <= 9) return (o, 2);
            int tens = Array.IndexOf(Tens, t[i]);
            if (tens >= 2)
            {
                int unit = i + 1 < t.Count ? Array.IndexOf(Units, t[i + 1]) : -1;
                return unit >= 1 && unit <= 9 ? (tens * 10 + unit, 2) : (tens * 10, 1);
            }
            int u = Array.IndexOf(Units, t[i]);
            return u >= 0 ? (u, 1) : ((int, int)?)null;
        }

        private static int? YearAt(List<string> t, int i, HashSet<int> used)
        {
            if (t[i].Length == 4 && t[i].All(char.IsDigit) && int.Parse(t[i], CultureInfo.InvariantCulture) is int y4 && y4 >= 1900 && y4 <= 2100) { used.Add(i); return y4; }
            if (t[i] == "two" && i + 1 < t.Count && t[i + 1] == "thousand")
            {
                int j = i + 2;
                if (j < t.Count && t[j] == "and") j++;
                var tail = Cardinal(t, j);
                int value = 2000 + (tail?.value ?? 0), end = j + (tail?.used ?? 0);
                for (int k = i; k < end; k++) used.Add(k);
                return value;
            }
            if (t[i] == "nineteen" || t[i] == "twenty")
            {
                var tail = Cardinal(t, i + 1);
                if (tail == null || (t[i + 1] != "oh" && tail.Value.value < 10)) return null; // "twenty one" is a day, not the year 2001
                for (int k = i; k <= i + tail.Value.used; k++) used.Add(k);
                return (t[i] == "nineteen" ? 1900 : 2000) + tail.Value.value;
            }
            return null;
        }

        private static int? DayAt(List<string> t, int i, HashSet<int> used)
        {
            var digits = DayDigits.Match(t[i]);
            if (digits.Success && int.Parse(digits.Groups[1].Value, CultureInfo.InvariantCulture) is int d && d >= 1 && d <= 31) { used.Add(i); return d; }
            int ord = Array.IndexOf(Ordinals, t[i]);
            if (ord >= 1) { used.Add(i); return ord; }
            if (t[i] == "thirtieth") { used.Add(i); return 30; }
            int tens = Array.IndexOf(Tens, t[i]);
            if (tens >= 2 && tens <= 3 && i + 1 < t.Count && Array.IndexOf(Ordinals, t[i + 1]) is int unit && unit >= 1 && unit <= 9)
            {
                used.Add(i); used.Add(i + 1);
                return tens * 10 + unit;                                             // "twenty first"
            }
            var card = Cardinal(t, i);
            if (card != null && card.Value.value >= 1 && card.Value.value <= 31)
            {
                for (int k = 0; k < card.Value.used; k++) used.Add(i + k);
                return card.Value.value;
            }
            return null;
        }
    }
}
