# Stage 2 diagnostics contract handoff

Inspected 11 October 2026 (Asia/Karachi). Unity owner: Sannan. This is a dependency checklist, not a new shared interface or clinical implementation.

## Verified current contract

- `src/ProcSense.Core/Workflow/Subsystems.cs` registers readiness, identity, history, observations and case graph only.
- `ObservableState` provides location, disclosed identity/history/vitals, goals/decisions and rejection/hint text. It exposes no investigation orders, transport status or released reports.
- `Session.TimeSkip` and attention-stop support exist, but a Unity diagnostics UI cannot invent the duration, eligibility, completion event or result from those capabilities alone.
- Case-file result data exists in content models. Its presence does not authorize showing it before runtime disclosure.

## Required owner handoff

| Capability | Needed by Unity | Dependency |
|---|---|---|
| Order request | Supported verb/target/parameters, stable order ID, accepted/rejected outcome and reasons | Investigations/content (B-M13; p0-clinical-logic-s06) |
| Patient or sample route | Authoritative route type, destination, eligibility/blockers, cancellation and return state | Transport/Core (p0-simulation-workflow-s13) |
| Wait | Authoritative turnaround, permitted time-skip action and attention-stop reason/resume behavior | Core diagnostics loop / existing time controls |
| Released report | Order/patient/episode IDs, release state, permitted report fields and review/provenance gate | Investigation results/content |
| Critical communication | Separate acknowledgement action/state; reading a report must not acknowledge a call | Core/content |
| Lifecycle | Reset/session identity, duplicate-event IDs/order, reload snapshot and stale update handling | Core diagnostics loop |

The original diagnostics s01 plan proposes `InvestigationRequested`, `PatientDeparted`, `SampleSent`, `ResultReady`, `PatientReturned` and `ReportDelivered`. These are planning names, **not verified implemented event APIs**. Owners must supply actual contracts and headless normal/rejected/interrupted/deterioration fixtures before Unity binds them. Do not build guessed adapters or clinical thresholds into Unity.

## Available presentation preview

During Play, use **ProcSense > Development > Diagnostics Preview (Play Mode)**. The clearly labeled synthetic panel previews patient/sample flow, processing, an injected attention stop and report visibility. Its controls inject fixture states only. It never calls `Session.Apply`, `Tick` or `TimeSkip`, reads case-file reports, moves the patient, or updates records. Sample mode keeps the displayed patient at bedside. Reports stay hidden until the fixture ready state. Reset/mode switch clears the report and interruption.

The component is compiled only under `UNITY_EDITOR`, opened only through an Editor menu, and created with `DontSaveInEditor | DontSaveInBuild`, with explicit Play-exit/reload cleanup; it is not part of shipped scenes. No runtime adapter or real diagnostics-loop acceptance is claimed. Physical transport, sample handling, records synchronization and critical-call acknowledgement remain pending their contracts and later Stage 2 steps.
