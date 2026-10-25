using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProcSense.Core.AIContext;
using ProcSense.Core.Logging;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    [Serializable] public class ChosenActionDto { public string id, verb, key, value; }
    [Serializable] public class SpokenActionDto { public ChosenActionDto[] actions; public string addressee, source, model; public string[] reasons; public bool cloud; public int ms; }

    /// <summary>What the interpreter made of one spoken sentence: the tablet actions to start (possibly none) and who was spoken to.</summary>
    public sealed class SpokenActionResult
    {
        public List<string> ActionIds = new List<string>();
        public string Addressee = "nobody";
        public bool FromService;
        public bool Cloud;
        public string Model = "";
        public string Reason = "";
        /// <summary>The cloud model reads the words with measured precision and its actions are started at once. A local model only proposes (it started a wrong action in 10 of 100 sentences in the measurement): the trainee confirms.</summary>
        public bool NeedsConfirmation => FromService && !Cloud && ActionIds.Count > 0;
    }

    /// <summary>
    /// The Unity half of "speak instead of tap" (DT-251, ADR-0027): sends the transcript, the ids of the tablet actions that are enabled now and the recent turns to the interpreter and reads which actions the trainee asked
    /// for. It starts nothing itself. Never throws: no sign-in, no network, a refusal or an unreadable answer is "no action", and the tablet remains.
    /// </summary>
    public static class SpokenActionClient
    {
        public const string Route = "/speech/organisations/";

        public static string RequestBody(string utterance, IEnumerable<string> available, IReadOnlyList<ContextTurn> turns)
        {
            var sb = new StringBuilder("{\"utterance\":");
            JsonText.Quote(sb, utterance);
            sb.Append(",\"available\":[").Append(string.Join(",", available.Select(a => { var q = new StringBuilder(); JsonText.Quote(q, a); return q.ToString(); }))).Append("],\"turns\":[");
            var kept = (turns ?? new List<ContextTurn>()).Skip(Math.Max(0, (turns?.Count ?? 0) - 4)).ToList();
            for (int i = 0; i < kept.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"who\":"); JsonText.Quote(sb, kept[i].Speaker); sb.Append(",\"text\":"); JsonText.Quote(sb, kept[i].Text); sb.Append('}');
            }
            return sb.Append("]}").ToString();
        }

        public static SpokenActionResult Parse(string body)
        {
            SpokenActionDto dto = null;
            try { dto = JsonUtility.FromJson<SpokenActionDto>(body); } catch (ArgumentException) { }
            if (dto == null || dto.actions == null) return new SpokenActionResult { Reason = "UNREADABLE" };
            var r = new SpokenActionResult { FromService = dto.source == "model", Cloud = dto.cloud, Model = dto.model ?? "", Addressee = string.IsNullOrEmpty(dto.addressee) ? "nobody" : dto.addressee, Reason = dto.reasons != null && dto.reasons.Length > 0 ? dto.reasons[0] : "" };
            foreach (var a in dto.actions) if (a != null && !string.IsNullOrEmpty(a.id) && !r.ActionIds.Contains(a.id)) r.ActionIds.Add(a.id);
            if (r.ActionIds.Count > 2) r.ActionIds = r.ActionIds.Take(2).ToList();
            if (!r.FromService) r.ActionIds.Clear();                                    // only an answer the model gave starts anything
            return r;
        }

        public static IEnumerator Interpret(AuthService auth, string utterance, IEnumerable<string> available, IReadOnlyList<ContextTurn> turns, Action<SpokenActionResult> done)
        {
            if (auth == null || !auth.IsSignedIn || string.IsNullOrWhiteSpace(utterance)) { done(new SpokenActionResult { Reason = "NOT_SIGNED_IN" }); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("POST", Route + auth.OrganisationId + "/action", RequestBody(utterance, available, turns), r => answer = r);
            done(answer.Ok ? Parse(answer.Body) : new SpokenActionResult { Reason = answer.NetworkError ? "NO_NETWORK" : "HTTP_" + answer.Status });
        }
    }
}
