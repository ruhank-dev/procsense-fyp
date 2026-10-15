namespace ProcSense.Core.Diagnostics
{
    // The diagnostics loop's events (p0-diagnostic-transitions-s01). They are published on the session's EventBus for the views (doors, the porter, the wait
    // overlay, the EMR) and are not part of the log: they are worked out from the investigation and transport state, so a replay publishes them again.
    // Each carries the order id (the n-th test ordered in the session) and the sim step it happened at. ResultReady itself stays in Workflow.Steps.

    /// <summary>A test was ordered.</summary>
    public sealed record InvestigationRequested(int OrderId, string Test, string Kind, long Step);

    /// <summary>The patient left the bay for the place where the test is done (a scan room).</summary>
    public sealed record PatientDeparted(int OrderId, string Test, string To, long Step);

    /// <summary>A sample left for the laboratory; the patient stays where they are. Via is how it went: the porter, or the tube station (p0-diagnostic-transitions-s04).</summary>
    public sealed record SampleSent(int OrderId, string Test, string Via, long Step);

    /// <summary>The nurse points out a sample still at the bedside (practice mode, beginner level; p0-diagnostic-transitions-s04).</summary>
    public sealed record SamplesWaiting(int OrderId, string Test, string Text, long Step);

    /// <summary>The patient is back where they left from after the test.</summary>
    public sealed record PatientReturned(int OrderId, string Test, long Step);

    /// <summary>The report has reached the trainee's side: with the patient (imaging), from the laboratory (a sample) or at the bedside (a bedside test).</summary>
    public sealed record ReportDelivered(int OrderId, string Test, string With, long Step);
}
