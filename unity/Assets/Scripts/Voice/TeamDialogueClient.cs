using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core.AIContext;
using ProcSense.Core.Logging;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    [Serializable] public class RoleReplyDto { public string role, text, intent, source; public string[] fact_ids, reasons; }
    [Serializable] public class TeamOutDto { public RoleReplyDto[] replies; public bool cloud; public string model; public int ms; }

    /// <summary>The answers of the patient and the staff to one trainee turn, in the order they speak, and where they came from.</summary>
    public sealed class TeamAnswer
    {
        public List<TeamReply> Replies = new List<TeamReply>();
        public bool FromService;
        public bool Cloud;
        public string Model = "";
        public int ServiceMs;
        public string Reason = "";
    }

    /// <summary>
    /// The Unity half of the three voices (plan p2-ai-advanced-simulation-s03, ADR-0026): builds the request for one trainee turn from the engine's own context (what the patient has earned or been asked, never a score or
    /// a hint), asks the dialogue service, and reads the patient's and the staff's replies. The engine decides what the patient may say: the facts the trainee has learned or earned this turn go to the model's prompt, the
    /// facts not yet released go to the service's grounding check only. Staff are asked only for an intent; their words come from the service's approved lines. It applies no action and never throws: a missing sign-in, no
    /// network, a refusal or an unreadable answer gives the patient's authored line (or "I am not sure") and no staff, so the trainee is never left without an answer. Nothing here awards credit.
    /// </summary>
    public static class TeamDialogueClient
    {
        public const string Route = "/dialogue/organisations/";
        public const string NotSure = "I am sorry, I am not sure about that.";
        public static readonly string[] StaffRoles = { "nurse", "anaesthesia" };

        /// <summary>The request body. <paramref name="askedTopic"/> is the history topic the engine released for this question (null for small talk and for a question the engine did not match to a topic).</summary>
        public static string RequestBody(SimulationHost host, string question, string askedTopic, IReadOnlyList<ContextTurn> turns, IEnumerable<string> staffRoles = null)
        {
            var session = host.Session;
            var built = ContextBuilder.Build(session, host.Content, "patient", askedTopic, turns ?? new List<ContextTurn>());
            var dto = built.Dto;
            var file = host.Content.CaseFiles.TryGetValue(session.Case.Id, out var f) ? f : null;
            string sex = file != null && !string.IsNullOrEmpty(file.Identity.Sex) && char.ToUpperInvariant(file.Identity.Sex[0]) == 'F' ? "F" : "M";
            string style = string.IsNullOrWhiteSpace(dto.BehaviourMode) ? "plain" : dto.BehaviourMode.ToLowerInvariant();
            var sb = new StringBuilder("{\"context\":{");
            Str(sb, "role", "patient"); sb.Append(','); Str(sb, "case", dto.Case); sb.Append(",\"facts\":[");
            bool first = true;
            foreach (var fact in built.Validation.Facts)
            {
                if (!first) sb.Append(',');
                first = false;
                bool usable = fact.DisclosableNow || fact.Disclosed;         // what the trainee has already learned the patient may say again; a fact not yet released stays out of the prompt
                sb.Append('{'); Str(sb, "id", fact.Id); sb.Append(','); Str(sb, "value", fact.Value);
                sb.Append(",\"disclosed\":").Append(fact.Disclosed ? "true" : "false").Append(",\"disclosable_now\":").Append(usable ? "true" : "false").Append('}');
            }
            sb.Append("],\"last_turns\":[");
            var kept = dto.LastTurns.Skip(Math.Max(0, dto.LastTurns.Count - 4)).ToList();
            for (int i = 0; i < kept.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{'); Str(sb, "who", kept[i].Speaker); sb.Append(','); Str(sb, "text", kept[i].Text); sb.Append('}');
            }
            sb.Append("],\"results\":[]},\"staff_roles\":[");
            sb.Append(string.Join(",", (staffRoles ?? StaffRoles).Select(r => "\"" + r + "\"")));
            sb.Append("],"); Str(sb, "question", question); sb.Append(','); Str(sb, "style", style);
            sb.Append(",\"age\":").Append(session.Case.Identity.Age.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(','); Str(sb, "sex", sex);
            sb.Append(",\"authored\":{");
            var asked = askedTopic == null || file == null ? null : file.History.FirstOrDefault(h => h.Topic == askedTopic && (h.Variant == null || h.Variant == session.Variant));
            if (asked != null && built.Validation.Facts.Any(x => x.Id == "h." + askedTopic && (x.DisclosableNow || x.Disclosed))) Str(sb, "h." + askedTopic, asked.Answer);
            sb.Append('}');
            if (askedTopic != null) { sb.Append(','); Str(sb, "asked_fact", "h." + askedTopic); }
            return sb.Append('}').ToString();
        }

        public static TeamAnswer Parse(string body)
        {
            TeamOutDto dto = null;
            try { dto = JsonUtility.FromJson<TeamOutDto>(body); } catch (ArgumentException) { }
            if (dto == null || dto.replies == null || dto.replies.Length == 0) return null;
            var answer = new TeamAnswer { FromService = true, Cloud = dto.cloud, Model = dto.model ?? "", ServiceMs = dto.ms };
            foreach (var r in dto.replies)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.text) || string.IsNullOrEmpty(r.role)) continue;
                answer.Replies.Add(new TeamReply(r.role, r.text) { Intent = r.intent ?? "", Source = r.source ?? "" });
            }
            return answer.Replies.Count == 0 ? null : answer;
        }

        /// <summary>The answer without the service: the patient's authored line for the topic the engine released, otherwise "I am not sure". No staff speak.</summary>
        public static TeamAnswer Offline(SimulationHost host, string askedTopic, string why)
        {
            string text = NotSure;
            if (host != null && host.Session != null && host.Content != null && askedTopic != null && host.Content.CaseFiles.TryGetValue(host.Session.Case.Id, out var file))
            {
                bool released = host.Session.Observable.DisclosedFacts.Any(d => d.Topic == askedTopic);
                var h = file.History.FirstOrDefault(x => x.Topic == askedTopic && (x.Variant == null || x.Variant == host.Session.Variant));
                if (released && h != null) text = h.Answer;
            }
            return new TeamAnswer { Replies = { new TeamReply("patient", text) { Intent = "answer_question", Source = "fallback" } }, Reason = why };
        }

        public static IEnumerator Ask(AuthService auth, SimulationHost host, string question, string askedTopic, IReadOnlyList<ContextTurn> turns, Action<TeamAnswer> done)
        {
            if (auth == null || !auth.IsSignedIn || host == null || host.Session == null || host.Content == null) { done(Offline(host, askedTopic, "NOT_SIGNED_IN")); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("POST", Route + auth.OrganisationId + "/team", RequestBody(host, question, askedTopic, turns), r => answer = r);
            var parsed = answer.Ok ? Parse(answer.Body) : null;
            done(parsed ?? Offline(host, askedTopic, answer.NetworkError ? "NO_NETWORK" : answer.Ok ? "UNREADABLE" : "HTTP_" + answer.Status));
        }

        private static void Str(StringBuilder sb, string key, string value) { JsonText.Quote(sb, key); sb.Append(':'); JsonText.Quote(sb, value); }
    }
}
