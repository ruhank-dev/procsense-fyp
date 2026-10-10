using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;
using ProcSense.Core.State;

namespace ProcSense.Core.Clinical
{
    /// <summary>
    /// What came of a sample: ok (the results to read), rejected (the lab could not use it: no results) or mislabelled (the results belong to someone else: nothing to trust). Valid is false for the last two,
    /// so a re-take is treated as a first sample and not as a repeat.
    /// </summary>
    public sealed record GeneratedResults(IReadOnlyList<ResultRecord> Records, string Outcome, string Note, bool Valid);

    /// <summary>
    /// Makes the results a test returns. They come only from the case card (value, unit, range, flag, comment, the quality the case itself states); where the case states none for a planned patient's liver tests a
    /// normal panel is supplied and labelled generated; nothing else is ever invented. How the sample was taken then changes it: unlabelled is rejected, pre-labelled is another patient's result, a drip-arm sample is
    /// diluted, a gold-tube test taken into an EDTA tube has a falsely high potassium and any other wrong tube is rejected. The same inputs always give the same results.
    /// </summary>
    public sealed class ResultGenerator
    {
        private static readonly Regex Between = new Regex(@"^\s*(-?\d+(?:\.\d+)?)\s*-\s*(-?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);
        private static readonly Regex Below = new Regex(@"^\s*below\s+(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly InvestigationTables _t;

        public ResultGenerator(InvestigationTables tables) { _t = tables; }

        /// <summary>
        /// The case's results for a test: the first set, or the repeat set when this is a second valid sample and the case states one (otherwise the same results again, because nothing has changed).
        /// </summary>
        public static List<CaseFileResult> CaseResults(CaseFile? file, InvestigationDef def, int validCompletedBefore)
        {
            if (file == null) return new List<CaseFileResult>();
            var mine = file.Results.Where(r => def.Analytes.Contains(InvestigationTables.BaseName(r.Test))).ToList();
            var first = mine.Where(r => !InvestigationTables.IsRepeat(r.Test)).ToList();
            var repeat = mine.Where(r => InvestigationTables.IsRepeat(r.Test)).ToList();
            return validCompletedBefore >= 1 && repeat.Count > 0 ? repeat : first;
        }

        public GeneratedResults Generate(CaseFile? file, InvestigationDef def, int validCompletedBefore, IReadOnlyDictionary<string, string> sample, string scenario)
        {
            var records = CaseResults(file, def, validCompletedBefore).Select(r => new ResultRecord(r.Id, InvestigationTables.BaseName(r.Test) + (InvestigationTables.IsRepeat(r.Test) ? " (repeat)" : ""), r.Value, r.Unit, r.NormalRange, r.Flag, r.Comment, r.Quality ?? "", "case", r.Previous?.Value ?? "")).ToList();
            string note = "";
            if (records.Count == 0)
            {
                if (file != null && _t.Normals.TryGetValue(def.Id, out var set) && set.Scenario == scenario)
                    records = set.Results.Where(n => n.Sex == file.Identity.Sex).Select(n => new ResultRecord(n.Id, n.Analyte, n.Value, n.Unit, n.Range, "normal", "Normal (supplied: the case states no result for this test).", "", "generated")).ToList();
                else note = "The case states no result for this test.";
            }

            if (def.Kind == "blood")
            {
                sample.TryGetValue("label", out var label);
                label = string.IsNullOrEmpty(label) ? "bedside" : label;
                sample.TryGetValue("tube", out var tube);
                bool wrongTube = !string.IsNullOrEmpty(tube) && tube != def.Tube;
                bool edtaPotassium = wrongTube && tube == "edta_purple" && records.Any(r => InvestigationTables.BaseName(r.Analyte) == "Potassium");

                if (label == "unlabelled")
                    return new GeneratedResults(new List<ResultRecord>(), "rejected", "The lab rejected the sample: it was not labelled. Re-take it with bedside labelling and report the incident.", false);
                if (wrongTube && !edtaPotassium)
                    return new GeneratedResults(new List<ResultRecord>(), "rejected", "The lab rejected the sample: it was taken into the wrong tube. Re-take it in the " + def.Tube.Replace('_', ' ') + " tube.", false);
                if (label == "prelabelled")
                    return new GeneratedResults(records.Select(r => new ResultRecord(r.Id, r.Analyte, "", "", "", "unknown", "These results do not match this patient: the tube was labelled before the sample was taken. Re-take it with bedside labelling and report the incident.", "mislabelled", r.Origin)).ToList(),
                        "mislabelled", "The results belong to someone else.", false);

                if (sample.TryGetValue("arm", out var arm) && arm == "drip_arm")
                    records = records.Select(r => Change(r, v => v * _t.DripDilution, "drip_arm", "Diluted: the sample came from the arm with the drip. Repeat from the other arm.")).ToList();
                if (edtaPotassium)
                    records = records.Select(r => InvestigationTables.BaseName(r.Analyte) == "Potassium" ? Change(r, v => v + _t.EdtaPotassiumRise, "wrong_tube_edta", "The sample was taken into an EDTA tube: the potassium looks falsely high. Repeat in the right tube.") : r).ToList();
            }
            return new GeneratedResults(records, "ok", note, true);
        }

        // A numeric result changed by an artefact: new value to the same number of decimals, flag worked out again from the range, quality and a comment added.
        private static ResultRecord Change(ResultRecord r, Func<double, double> effect, string quality, string comment)
        {
            if (!double.TryParse(r.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return r;
            int decimals = r.Value.Contains('.') ? r.Value.Split('.')[1].Length : 0;
            string value = Math.Round(effect(v), decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);
            double changed = double.Parse(value, CultureInfo.InvariantCulture);
            return r with { Value = value, Flag = FlagFor(changed, r.Range, r.Flag), Comment = r.Comment.Length > 0 ? r.Comment + " " + comment : comment, Quality = quality, Origin = "artefact" };
        }

        /// <summary>Normal, low or high against a range written "a-b" or "below x"; any other wording keeps the flag the case gave.</summary>
        public static string FlagFor(double value, string range, string original)
        {
            var m = Between.Match(range);
            if (m.Success)
            {
                double lo = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), hi = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                return value < lo ? "low" : value > hi ? "high" : "normal";
            }
            var b = Below.Match(range);
            if (b.Success) return value < double.Parse(b.Groups[1].Value, CultureInfo.InvariantCulture) ? "normal" : "high";
            return original;
        }
    }
}
