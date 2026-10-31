using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Diagnostics
{
    /// <summary>One way a labelled sample can go to the laboratory: the tube station, or the porter (who first has to come, after the porter arrival time of the locations).</summary>
    public sealed record DispatchWay(string Id, string Label, double TransitMinutes, bool Porter);

    /// <summary>content/diagnostics/diagnostics.json (tools/content/build_diagnostics.py): how samples go to the laboratory, and the nurse's prompt about tubes left at the bedside.</summary>
    public sealed class DiagnosticsTables
    {
        public static readonly DiagnosticsTables Empty = new DiagnosticsTables(new List<DispatchWay>(), 0, false, "", 10);

        public DiagnosticsTables(IReadOnlyList<DispatchWay> dispatch, double promptAfterMinutes, bool promptBeginnerOnly, string promptText, double waitOverlaySeconds = 10)
        {
            Dispatch = dispatch; PromptAfterMinutes = promptAfterMinutes; PromptBeginnerOnly = promptBeginnerOnly; PromptText = promptText; WaitOverlaySeconds = waitOverlaySeconds;
        }

        public IReadOnlyList<DispatchWay> Dispatch { get; }

        /// <summary>Minutes a taken sample may wait at the bedside before the nurse mentions it (0: never).</summary>
        public double PromptAfterMinutes { get; }

        /// <summary>The prompt is the beginner safety net: practice mode at the beginner level only, never in assessment.</summary>
        public bool PromptBeginnerOnly { get; }
        public string PromptText { get; }

        /// <summary>Seconds the full waiting overlay stays on screen when the patient goes behind a door (p0-diagnostic-transitions-s06); then it shrinks to a corner note. Tuned with testers (EXP-DT-1).</summary>
        public double WaitOverlaySeconds { get; }

        public DispatchWay? Way(string id) => Dispatch.FirstOrDefault(d => d.Id == id);

        public static DiagnosticsTables Parse(string json, string source = "diagnostics")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var ways = new List<DispatchWay>();
            var raw = root.Arr("dispatch");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("dispatch"), i));
                var w = new DispatchWay(o.Str("id"), o.Str("label"), o.Num("transit_minutes"), o.Bool("porter"));
                if (w.TransitMinutes < 0) throw new ContentException(o.Sub("transit_minutes") + ": must not be negative");
                if (ways.Any(x => x.Id == w.Id)) throw new ContentException(o.Sub("id") + ": duplicate way '" + w.Id + "'");
                o.Str("ref");
                ways.Add(w);
                o.Finish();
            }
            var prompt = root.Obj("waiting_prompt");
            double after = prompt.Num("after_minutes");
            bool beginner = prompt.Bool("practice_beginner_only");
            string text = prompt.Str("text");
            prompt.Str("ref");
            prompt.Finish();
            var rule = root.Obj("send_rule");
            if (rule.Str("default") != "now" || rule.Str("later") != "later") throw new ContentException(root.Sub("send_rule") + ": the engine knows the values now and later only");
            rule.Str("ref");
            rule.Finish();
            var overlay = root.Obj("wait_overlay");
            double seconds = overlay.Num("seconds");
            if (seconds < 0) throw new ContentException(root.Sub("wait_overlay") + ".seconds: must not be negative");
            overlay.Str("note");
            overlay.Finish();
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new DiagnosticsTables(ways, after, beginner, text, seconds);
        }
    }
}
