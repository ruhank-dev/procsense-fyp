using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Sync
{
    [Serializable] internal class RewriteSectionDto { public string title, text; public string[] fact_ids; }
    [Serializable] internal class RewriteAnswerDto { public RewriteSectionDto[] template; public RewriteSectionDto[] ai; public string used; public string[] reasons; }

    /// <summary>The recommendation lines the results panel shows: the service's validated AI wording (Reworded), or the engine's own deterministic lines with the reason the wording was not used.</summary>
    public sealed class RewordedRecommendations
    {
        public bool Reworded;
        public string Reason = "";
        public IReadOnlyList<string> Lines = Array.Empty<string>();
    }

    /// <summary>
    /// Optional AI wording of the results (P3-RES-07) through the service route `POST /debrief/organisations/{id}/rewrite` with {"report": the engine's debrief facts}. Only the wording of the
    /// recommendation lines can change: the score, grade, points, result and every other number on the panel come from the engine's ResultSummary and are never sent back or replaced. The service
    /// returns the AI sections only when its strict grounding validator passed (`used` = "ai"); anything else (feature off, model busy or refused, not signed in, offline, an answer not understood)
    /// keeps the engine's deterministic lines, so the panel is complete without the service.
    /// </summary>
    public static class DebriefRewriteClient
    {
        public const string Label = "Reworded";
        public const string LabelLine = "[Reworded] AI wording of the same facts; the score, grade and points are the engine's and unchanged.";

        /// <summary>The identity used for the request; replaced by tests.</summary>
        public static Func<AuthService> Auth { get; set; } = () => AuthService.Current;

        public static string PathFor(string organisationId) => "/debrief/organisations/" + organisationId + "/rewrite";
        public static string BodyFor(string reportJson) => "{\"report\":" + reportJson + "}";

        public static RewordedRecommendations Deterministic(IReadOnlyList<string> lines, string reason) =>
            new RewordedRecommendations { Reworded = false, Reason = reason ?? "", Lines = lines ?? Array.Empty<string>() };

        public static IEnumerator Fetch(AuthService auth, string reportJson, IReadOnlyList<string> deterministic, Action<RewordedRecommendations> done)
        {
            if (auth == null || !auth.IsSignedIn || string.IsNullOrEmpty(auth.OrganisationId)) { done(Deterministic(deterministic, "NOT_SIGNED_IN")); yield break; }
            if (string.IsNullOrWhiteSpace(reportJson)) { done(Deterministic(deterministic, "NO_REPORT")); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("POST", PathFor(auth.OrganisationId), BodyFor(reportJson), r => answer = r);
            if (answer.NetworkError) { done(Deterministic(deterministic, "OFFLINE")); yield break; }
            if (!answer.Ok) { done(Deterministic(deterministic, "HTTP_" + answer.Status)); yield break; }
            done(Parse(answer.Body, deterministic));
        }

        public static RewordedRecommendations Parse(string json, IReadOnlyList<string> deterministic)
        {
            RewriteAnswerDto dto = null;
            try { dto = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<RewriteAnswerDto>(json); } catch (ArgumentException) { }
            if (dto == null || string.IsNullOrEmpty(dto.used)) return Deterministic(deterministic, "NOT_UNDERSTOOD");
            if (dto.used != "ai" || dto.ai == null || dto.ai.Length == 0)
                return Deterministic(deterministic, dto.reasons != null && dto.reasons.Length > 0 ? dto.reasons[0] : "TEMPLATE");
            var lines = dto.ai.Where(s => s != null && !string.IsNullOrWhiteSpace(s.text))
                .Select(s => string.IsNullOrWhiteSpace(s.title) ? s.text.Trim() : s.title.Trim() + ": " + s.text.Trim()).ToList();
            if (lines.Count == 0) return Deterministic(deterministic, "EMPTY");
            return new RewordedRecommendations { Reworded = true, Lines = lines };
        }
    }
}
