using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ProcSense.VR.Auth;
using UnityEngine;

namespace ProcSense.VR.Sync
{
    [Serializable] internal class ResultRowDto { public string session_id, case_id, mode, status, result, end_state, received_at; public float process_score; }
    [Serializable] internal class ResultPageDto { public int total; public ResultRowDto[] items; }

    /// <summary>The person's latest sessions as the service reports them, ready to show: one text line per session.</summary>
    public sealed class ResultsView
    {
        public bool Ok;
        public string Error = "";
        public int Total;
        public readonly List<string> Lines = new List<string>();
        public bool AnyPending;
        public string Text => Ok ? (Lines.Count == 0 ? "No sessions on the service yet." : string.Join("\n", Lines)) : Error;
    }

    /// <summary>Fetches and words the person's latest sessions (p1-backend-s04 route `GET /organisations/{id}/sessions`). Only the person's own sessions come back: the service narrows a trainee to themselves.</summary>
    public static class ResultsClient
    {
        public const int DefaultCount = 5;

        public static IEnumerator Fetch(AuthService auth, int count, Action<ResultsView> done)
        {
            if (auth == null || !auth.IsSignedIn) { done(new ResultsView { Error = "Sign in to see your results on the service." }); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("GET", "/organisations/" + auth.OrganisationId + "/sessions?status=any&limit=" + count, null, r => answer = r);
            if (answer.NetworkError) { done(new ResultsView { Error = "Cannot reach the service right now." }); yield break; }
            if (!answer.Ok) { done(new ResultsView { Error = auth.IsSignedIn ? "The service could not show your results (" + answer.Status + ")." : "Sign in to see your results on the service." }); yield break; }
            done(Parse(answer.Body));
        }

        public static ResultsView Parse(string json)
        {
            ResultPageDto page = null;
            try { page = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<ResultPageDto>(json); } catch (ArgumentException) { }
            if (page == null || page.items == null) return new ResultsView { Error = "The service's answer was not understood." };
            var view = new ResultsView { Ok = true, Total = page.total };
            foreach (var item in page.items)
            {
                if (item.status == "pending") view.AnyPending = true;
                view.Lines.Add(Line(item));
            }
            return view;
        }

        private static string Line(ResultRowDto item)
        {
            string when = DateTime.TryParse(item.received_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t) ? t.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture) : "";
            string outcome;
            switch (item.status)
            {
                case "verified": outcome = Words(item.result) + ", score " + item.process_score.ToString("0.#", CultureInfo.InvariantCulture) + " (provisional)"; break;
                case "pending": outcome = "waiting to be checked"; break;
                case "rejected": outcome = "refused by the check"; break;
                case "unverifiable": outcome = "could not be checked"; break;
                default: outcome = item.status; break;
            }
            return when + "   " + item.case_id + "   " + outcome;
        }

        private static string Words(string result)
        {
            switch (result)
            {
                case "SUCCESS": return "Success";
                case "SUCCESS_WITH_DELAY": return "Success with delay";
                case "FAIL": return "Fail";
                case "IN_PROGRESS": return "Not finished";
                default: return string.IsNullOrEmpty(result) ? "No result" : result;
            }
        }
    }
}
