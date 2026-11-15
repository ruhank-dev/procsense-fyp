using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Flow
{
    /// <summary>Where the case comes from. Only Authored is planned here; Generated (practice only, from the laptop) is resolved by the Unity launcher and falls back to Authored.</summary>
    public enum CaseSource { Authored, Generated }

    /// <summary>What the menu asks for (P3-SES-06): a session is started only from a request, never from a hard-coded case id. Seed null = draw one (logged in the result).</summary>
    public sealed record SessionRequest(SessionMode Mode, string Procedure, TrainingLevel Level, CaseSource Source = CaseSource.Authored, ulong? Seed = null, string? CaseId = null);

    /// <summary>The decision made from a request: the case, the seed and the options that the session is built with and that the log header records.</summary>
    public sealed record SessionPlan(string CaseId, ulong Seed, SessionMode Mode, SessionOptions Options, string Procedure, CaseSource Source, string? Relaxed)
    {
        /// <summary>The request fields of the plan. A session started with Options writes procedure (and source, when not authored) to its saved header (SessionFile.Write), and
        /// SessionFile.RequestOf reads them back, so a replay or a bug report rebuilds the same request and plan.</summary>
        public IReadOnlyDictionary<string, string> Header() => new Dictionary<string, string>
        {
            ["case"] = CaseId,
            ["seed"] = Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mode"] = Mode.ToString().ToLowerInvariant(),
            ["level"] = Options.Level.ToString().ToLowerInvariant(),
            ["procedure"] = Procedure,
            ["source"] = Source.ToString().ToLowerInvariant(),
        };
    }

    /// <summary>Turns a request into a plan (pure). The case is either the one asked for (retry, replay, a chosen case) or drawn by the ScenarioPicker; every refusal has a code.</summary>
    public static class SessionPlanner
    {
        public const string UnknownProcedure = "UNKNOWN_PROCEDURE", NoCaseAvailable = "NO_CASE_AVAILABLE", CaseNotAllowed = "CASE_NOT_ALLOWED", GeneratedNotInAssessment = "GENERATED_NOT_IN_ASSESSMENT";

        public sealed record Outcome(SessionPlan? Plan, string? Refusal);

        public static Outcome Plan(CaseCatalogue catalogue, SessionRequest request, IReadOnlyList<string>? recent, Func<ulong> drawSeed)
        {
            if (request.Procedure != CaseCatalogue.Cholecystectomy && request.Procedure != CaseCatalogue.Appendicectomy) return new Outcome(null, UnknownProcedure);
            if (request.Mode == SessionMode.Assessment && request.Source == CaseSource.Generated) return new Outcome(null, GeneratedNotInAssessment);
            ulong seed = request.Seed ?? drawSeed();
            var options = new SessionOptions(request.Mode == SessionMode.Practice ? request.Level : TrainingLevel.Advanced, Procedure: request.Procedure,
                                             Source: request.Source == CaseSource.Authored ? "" : request.Source.ToString().ToLowerInvariant());   // written to the log header (P3-SES-06); authored is the default and is left out

            if (request.CaseId != null)
            {
                var entry = catalogue.Find(request.CaseId);
                if (entry == null || entry.Procedure != request.Procedure || !Content.ContentGate.Allows(entry.Status, request.Mode)) return new Outcome(null, CaseNotAllowed);
                return new Outcome(new SessionPlan(entry.Id, seed, request.Mode, options, request.Procedure, request.Source, null), null);
            }
            var pick = ScenarioPicker.Pick(catalogue, request.Mode, request.Procedure, request.Level, seed, recent);
            if (pick == null) return new Outcome(null, NoCaseAvailable);
            return new Outcome(new SessionPlan(pick.CaseId, seed, request.Mode, options, request.Procedure, request.Source, pick.Relaxed), null);
        }

        /// <summary>"Retry": the same case and seed. "New scenario": a new seed and, when another case is allowed, another case.</summary>
        public static SessionRequest Retry(SessionPlan previous, TrainingLevel level) => new SessionRequest(previous.Mode, previous.Procedure, level, previous.Source, previous.Seed, previous.CaseId);
    }
}
