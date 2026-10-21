using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.Core.Logging;
using ProcSense.Core.Speech;

namespace ProcSense.Core.Scoring
{
    /// <summary>
    /// Offline measurement of what the engine does with judged speech (p1-ai-language-s22, ADR-0025): each item ({"id","utterance_id","steps":[...],"patient_question_pending","readback_pending","verified":["action",...],"acts":[{"act","assertion","actor","form",
    /// "relevance","correctness","completeness","span","claims_physical"}]}) is run through a fresh <see cref="EvidenceStore"/> with the engine's own rules (state gate, performed-now, window, graded credit, critical objectives never evidence) and the communication scorer.
    /// It is the same code a session uses, with the simulator state given as plain data; it changes nothing and decides nothing. The "verified" list stands for actions the simulator accepted: only those can make a critical objective's physical part true.
    /// </summary>
    public static class SpeechScoreCheck
    {
        public static string Run(string itemsJson, ScoreTables tables)
        {
            var items = (List<object?>)MiniJson.Parse(itemsJson)!;
            var outItems = new List<string>();
            foreach (var raw in items)
            {
                var d = new JsonObj(raw, "item");
                string id = d.Str("id"), utt = d.Has("utterance_id") ? d.Str("utterance_id") : id;
                var steps = d.Arr("steps").Select(x => (int)Convert.ToInt64(x, CultureInfo.InvariantCulture)).ToList();
                bool pq = d.Has("patient_question_pending") && d.Bool("patient_question_pending"), rb = d.Has("readback_pending") && d.Bool("readback_pending");
                var store = new EvidenceStore(tables);
                if (d.Has("verified")) foreach (var v in d.Arr("verified")) store.AddDeterministic((string)v!, new[] { 0 });
                var verdicts = new List<string>();
                foreach (var ar in d.Arr("acts"))
                {
                    var a = new JsonObj(ar, "act");
                    string act = a.Str("act"), span = a.Has("span") ? a.Str("span") : "";
                    var j = new SpeechJudgement(a.Str("assertion"), a.Str("actor"), a.Str("form"), (int)a.Int("relevance"), (int)a.Int("correctness"), (int)a.Int("completeness"), a.Has("claims_physical") && a.Bool("claims_physical"));
                    var refusal = SpeechStateGate.Check(act, pq, rb);
                    var r = refusal != null ? new EvidenceRecord(0, "ai", "", act, "", span, 0, new[] { 0 }, false, refusal, j) : store.SubmitUtterance(utt, act, span, j, steps, new[] { 0 });
                    verdicts.Add("{\"act\":" + Q(act) + ",\"accepted\":" + B(r.Accepted) + ",\"reason\":" + Q(r.Reason) + ",\"behaviour\":" + Q(r.Behaviour) + ",\"grade\":" + N(r.Grade ?? 0) + "}");
                }
                var comm = CommunicationScorer.Collect(store, tables, new Dictionary<string, double>());
                var ci = comm.Select(c => "{\"behaviour\":" + Q(c.Behaviour) + ",\"act\":" + Q(c.Act) + ",\"critical\":" + B(c.Critical) + ",\"grade\":" + N(c.Grade) + ",\"weight\":" + N(c.Weight) + ",\"credit\":" + N(c.Credit) + ",\"claims_physical\":" + B(c.ClaimsPhysical) + ",\"physical_verified\":" + B(c.PhysicalVerified) + "}");
                outItems.Add("{\"id\":" + Q(id) + ",\"acts\":[" + string.Join(",", verdicts) + "],\"communication\":[" + string.Join(",", ci) + "],\"communication_total\":" + N(comm.Sum(c => c.Credit)) + "}");
            }
            return "[" + string.Join(",\n", outItems) + "]\n";
        }

        private static string Q(string s) { var sb = new StringBuilder(); JsonText.Quote(sb, s); return sb.ToString(); }
        private static string B(bool b) => b ? "true" : "false";
        private static string N(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
