using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Flow;
using ProcSense.VR.Auth;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Flow
{
    /// <summary>One section of the results card: a heading, its lines, and whether it is a critical one (drawn first and in the danger colour).</summary>
    public sealed class ResultSection
    {
        public string Heading = "";
        public readonly List<string> Lines = new List<string>();
        public bool Critical;
    }

    /// <summary>
    /// The words of the results card (P3-RES-04), from the engine's <see cref="ResultSummary"/> only, so the card can never disagree with the scorer: the verdict, the score and grade when the case
    /// has a correct-path total, then the sections with the critical items first, the missed goals (training only: the examination shows no goal list), hints, time, recommendations, and the case
    /// and seed. After a retry of the same case and seed, a comparison with the previous attempt's saved summary (P3-TRN-07).
    /// </summary>
    public static class ResultsCardText
    {
        public const string VerifiedText = "Verified by the server";

        public static string Verdict(ResultSummary s)
        {
            if (s.Abandoned) return "NOT COMPLETED";
            if (s.Grade != null) return s.Grade.Passed ? "PASSED" : "NOT PASSED";
            switch (s.Result)
            {
                case "SUCCESS": return "SUCCESS";
                case "SUCCESS_WITH_DELAY": return "SUCCESS WITH DELAY";
                case "FAIL": return "FAIL";
                default: return string.IsNullOrEmpty(s.Result) ? "NO RESULT" : s.Result.Replace('_', ' ');
            }
        }

        /// <summary>The verdict's colour: done green for a pass or success, hint amber for not completed or a delay, danger red otherwise.</summary>
        public static Color VerdictColour(ResultSummary s)
        {
            var t = UiTheme.Current;
            if (s.Abandoned) return t.Hint;
            bool good = s.Grade != null ? s.Grade.Passed : s.Result == "SUCCESS";
            if (good) return t.Done;
            return s.Grade == null && s.Result == "SUCCESS_WITH_DELAY" ? t.Hint : t.Danger;
        }

        public static string Minutes(double m) => m.ToString("0.#", CultureInfo.InvariantCulture) + " min";

        public static List<ResultSection> Sections(ResultSummary s)
        {
            var list = new List<ResultSection>();
            if (s.CriticalCount > 0)
            {
                var c = new ResultSection { Heading = s.CriticalCount == 1 ? "1 critical item" : s.CriticalCount + " critical items", Critical = true };
                list.Add(c);
            }
            if (s.MissedGoals.Count > 0 && s.Mode != "assessment")
            {
                var m = new ResultSection { Heading = "Missed" };
                m.Lines.Add(string.Join(", ", s.MissedGoals));
                list.Add(m);
            }
            var r = new ResultSection { Heading = "Recommendations" };
            r.Lines.AddRange(s.Recommendations);
            if (r.Lines.Count > 0) list.Add(r);
            var f = new ResultSection { Heading = "Session" };
            f.Lines.Add("Points +" + s.CreditPoints + " / -" + s.PenaltyPoints);
            f.Lines.Add("Hints " + s.HintsUsed + (s.HintsCharged > 0 ? " (charged)" : ""));
            f.Lines.Add("Time " + Minutes(s.TotalMinutes));
            list.Add(f);
            return list;
        }

        public static string CaseLine(ResultSummary s) => "Case " + s.CaseId + " · seed " + s.Seed.ToString(CultureInfo.InvariantCulture);

        /// <summary>True when <paramref name="previous"/> is an earlier attempt at the same case and seed (a retry), so the two can be compared.</summary>
        public static bool Comparable(ResultSummary previous, ResultSummary now) =>
            previous != null && now != null && !ReferenceEquals(previous, now) && previous.CaseId == now.CaseId && previous.Seed == now.Seed;

        /// <summary>P3-TRN-07: the previous attempt beside this one: verdict, time and missed steps, from the two saved summaries only.</summary>
        public static string Comparison(ResultSummary previous, ResultSummary now)
        {
            if (!Comparable(previous, now)) return "";
            return "Previous attempt: " + Verdict(previous) + ", " + Minutes(previous.TotalMinutes) + ", " + Missed(previous) + "\n"
                 + "This attempt: " + Verdict(now) + ", " + Minutes(now.TotalMinutes) + ", " + Missed(now);
        }

        private static string Missed(ResultSummary s) => s.MissedGoals.Count == 1 ? "1 missed step" : s.MissedGoals.Count + " missed steps";
    }

    /// <summary>
    /// P3-RES-08: whether the service's replay of this session matched. Reads the person's latest sessions (the same route as the results list) and looks for this session's row: "verified"
    /// gives the badge; pending, rejected, unverifiable, offline or not signed in give nothing (the panel never claims a check that did not happen).
    /// </summary>
    public static class ServerVerification
    {
        [System.Serializable] private class Row { public string session_id, status; }
        [System.Serializable] private class Page { public Row[] items; }

        /// <summary>This session's status in the service's answer, or "" when it is not there.</summary>
        public static string StatusOf(string json, string sessionId)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(sessionId)) return "";
            Page page = null;
            try { page = JsonUtility.FromJson<Page>(json); } catch (System.ArgumentException) { }
            var row = page?.items?.FirstOrDefault(r => r != null && r.session_id == sessionId);
            return row?.status ?? "";
        }

        public static IEnumerator Check(AuthService auth, string sessionId, System.Action<string> done)
        {
            if (auth == null || !auth.IsSignedIn || string.IsNullOrEmpty(sessionId)) { done(""); yield break; }
            HttpResult answer = default;
            yield return auth.Authorized("GET", "/organisations/" + auth.OrganisationId + "/sessions?status=any&limit=10", null, r => answer = r);
            done(answer.Ok ? StatusOf(answer.Body, sessionId) : "");
        }
    }

    /// <summary>
    /// The results card (P3-RES-04) inside the game-flow panel: verdict badge, score ring with the grade, the sections with critical items first, the case and seed, the server verification badge
    /// (P3-RES-08) and, after a retry, the comparison with the previous attempt (P3-TRN-07). Built from the component kit; redrawn whenever the summary changes. The buttons stay the panel's.
    /// </summary>
    public sealed class ResultsCard : MonoBehaviour
    {
        public const float PollSeconds = 5f, PollFor = 120f;

        public ResultSummary Shown { get; private set; }
        public ResultSummary Previous { get; private set; }
        public string VerdictText { get; private set; } = "";
        public string ScoreText { get; private set; } = "";
        public bool Verified { get; private set; }
        public string ComparisonText { get; private set; } = "";
        public IReadOnlyList<ResultSection> SectionsShown => _sections;
        /// <summary>Where the session id of the finished session comes from (the host by default; tests replace it).</summary>
        public System.Func<string> SessionId = () => ProcSense.VR.Bridge.SimulationHost.Instance != null ? ProcSense.VR.Bridge.SimulationHost.Instance.SessionId : "";
        public System.Func<AuthService> Auth = () => ProcSense.VR.Sync.DebriefRewriteClient.Auth?.Invoke();

        private RectTransform _root;
        private List<ResultSection> _sections = new List<ResultSection>();
        private ResultSummary _retriedFrom;
        private Coroutine _poll;

        public static ResultsCard Create(Transform canvas, Vector2 topLeft, Vector2 size)
        {
            var go = new GameObject("Results card", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rt.sizeDelta = size;
            var card = go.AddComponent<ResultsCard>();
            card._root = rt;
            go.SetActive(false);
            return card;
        }

        /// <summary>The player pressed Retry on this summary: the next result of the same case and seed is compared with it.</summary>
        public void RetriedFrom(ResultSummary s) => _retriedFrom = s;

        public void Hide() { if (_poll != null) StopCoroutine(_poll); _poll = null; gameObject.SetActive(false); }

        public void Show(ResultSummary s)
        {
            gameObject.SetActive(true);
            if (ReferenceEquals(s, Shown)) return;
            Previous = ResultsCardText.Comparable(_retriedFrom, s) ? _retriedFrom : null;
            _retriedFrom = null;
            Shown = s;
            Verified = false;
            Draw();
            if (_poll != null) StopCoroutine(_poll);
            _poll = Application.isPlaying && isActiveAndEnabled ? StartCoroutine(Poll(s)) : null;
        }

        /// <summary>Takes the service's answer for the session shown (the poll calls it; tests call it directly).</summary>
        public void Report(ResultSummary forSummary, string status)
        {
            if (!ReferenceEquals(forSummary, Shown)) return;
            bool v = status == "verified";
            if (v == Verified) return;
            Verified = v;
            Draw();
        }

        private IEnumerator Poll(ResultSummary s)
        {
            string id = SessionId?.Invoke();
            float until = Time.realtimeSinceStartup + PollFor;
            while (ReferenceEquals(s, Shown) && !Verified && Time.realtimeSinceStartup < until)
            {
                string status = "";
                yield return ServerVerification.Check(Auth?.Invoke(), id, r => status = r);
                Report(s, status);
                if (status == "rejected" || status == "unverifiable" || status == "") yield break;   // offline, not signed in, or a final answer: nothing more to wait for
                yield return new WaitForSecondsRealtime(PollSeconds);
            }
        }

        private void Draw()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) DestroyImmediate(_root.GetChild(i).gameObject);
            var s = Shown;
            if (s == null) return;
            var t = UiTheme.Current;
            float w = _root.sizeDelta.x;

            // left column: the score ring with the grade, or the verdict alone when the case has no correct-path total
            const float ring = 150;
            var colour = ResultsCardText.VerdictColour(s);
            Kit.Ring(_root, new Vector2(0, 0), ring, s.Grade != null ? s.Grade.Score100 / 100f : 0f, colour, 12);
            ScoreText = s.Grade != null ? s.Grade.Score100.ToString(CultureInfo.InvariantCulture) : "–";
            var score = Kit.Text(_root, "Score", ScoreText, Vector2.zero, new Vector2(ring, ring * .7f), TextStyle.Title, null, TextAlignmentOptions.Center);
            score.verticalAlignment = VerticalAlignmentOptions.Bottom;
            Kit.Text(_root, "Grade", s.Grade != null ? "Grade " + s.Grade.Letter : "", new Vector2(0, ring * .7f), new Vector2(ring, 32), TextStyle.Caption, t.TextSecondary, TextAlignmentOptions.Center);
            VerdictText = ResultsCardText.Verdict(s);
            Kit.Badge(_root, VerdictText, new Vector2(0, ring + 12), colour);
            if (Verified) Kit.Badge(_root, ResultsCardText.VerifiedText, new Vector2(0, ring + 60), t.Action);

            // right column: the sections, critical first
            float x = ring + 32, y = 0, col = w - x;
            _sections = ResultsCardText.Sections(s);
            foreach (var sec in _sections)
            {
                Kit.Text(_root, "Heading " + sec.Heading, sec.Heading, new Vector2(x, y), new Vector2(col, 30), TextStyle.Body, sec.Critical ? t.DangerText : t.TextPrimary).fontWeight = FontWeight.Bold;
                y += 30;
                if (sec.Lines.Count > 0)
                {
                    string body = sec.Heading == "Session" ? string.Join("   ·   ", sec.Lines) : string.Join("\n", sec.Lines.Select(l => "• " + l));
                    var text = Kit.Text(_root, "Lines " + sec.Heading, body, new Vector2(x, y), new Vector2(col, 26 * sec.Lines.Count + 4), TextStyle.Caption, t.TextSecondary);
                    text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = text.fontSize;
                    y += sec.Heading == "Session" ? 30 : 26 * sec.Lines.Count + 8;
                }
            }
            ComparisonText = ResultsCardText.Comparison(Previous, s);
            if (ComparisonText.Length > 0) { Kit.Text(_root, "Comparison", ComparisonText, new Vector2(x, y + 4), new Vector2(col, 52), TextStyle.Caption, t.ActionText); y += 56; }
            Kit.Text(_root, "Case", ResultsCardText.CaseLine(s), new Vector2(0, _root.sizeDelta.y - 24), new Vector2(w, 24), TextStyle.Caption, t.TextSecondary);
        }
    }
}
