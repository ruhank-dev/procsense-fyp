# Stage 2 diagnostics contract handoff

Inspected 11 October 2026 (Asia/Karachi). Unity owner: Sannan. This is a dependency checklist, not a new shared interface or clinical implementation.

## Reassessment — 25 October 2026 (Asia/Karachi)

The original missing-subsystem finding below is historical. The current `Subsystems.Default` registers `TransportSubsystem`, `InvestigationsSubsystem` and `ResultReviewSubsystem`. `ObservableState` now supplies `Location`, `TraineeLocation`, `Transport`, `Investigations` and `Phone`. Core discloses result values/report text only after accepted `view_result`; Unity must not read `Patient.Clinical`, `CaseFile.Results` or report finding/category fields to fill a screen.

Stage 2's first runtime batch uses these existing contracts:

| Request | Parameters | Observable outcome / rejection |
|---|---|---|
| `order_investigation` | `test` | `Investigations` test/instance/state; identity, pregnancy and duplicate-order gates stay in Core |
| `take_sample` | `test`, `tube`, `label` | Blood sampling starts processing without moving the patient; content label/quality rules decide rejection |
| `perform_bedside_test` | `test` | Existing bedside-test processing; no sample transport invented |
| `time_skip` | `minutes=1` | Logged clock advance through the normal pipeline; `stopped:<reason>` interrupts the wait |
| `view_result` | `test` | Accepted opening changes the order to VIEWED and discloses permitted results/report |
| `acknowledge_result` | `test` | Separate request; critical-call prerequisites can reject it |
| `answer_phone` | none | `Phone.State`/`Message`; answering does not confirm identity, read back or acknowledge |

All requests use target `patient`, channel `Vr`, and `SimulationHost.Apply`. Catalogue metadata (test definitions, tube and label choices) is exposed separately by the host; no patient results are added to that metadata. The UI follows the latest instance of the selected test, clears local state on session replacement, and debounces requests for 0.35 seconds. After integration with the content-driven tablet shell on 27 October, the 11 content tabs and More remain intact; Session is index 12 and Diagnostics is index 13. Callers use the stable session/diagnostics IDs instead of historical numeric indexes. The host catalogue accessors use its current Content package, including follow-up/generated-session paths.

The upstream content-driven Orders tab already supplies a trainee-selected transport/escort/monitoring form; this integration preserves it. Remaining Stage 2 integration: dedicated transport presentation and physical patient transport, specimen dispatch/label props, critical-call identity/read-back/action/note entry, image asset review/provenance binding, printed report delivery/filing and automatic folder/EMR documents. The old proposed `send_samples`/`file_document` and loop event names must still be checked against actual runtime support; do not invent them. Current `take_sample` starts processing immediately, so it does not demonstrate the planned physical dispatch gate. Runtime durations come from Core/content; the one-minute wait button is a trainee-requested clock increment, not a test turnaround rule. No custom ten-second wait overlay or fixture-to-production conversion is claimed.

## Original inspection — 11 October 2026

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

## Corridor endpoint presentation — 27 October 2026

DiagnosticDoorView reads only ObservableState plus public catalogue/location labels. Destinations are ct, ultrasound, xray and mri (MRI/MRCP share Core's mri location); the laboratory is explicitly sample-only and has no patient-transport location ID. The UI displays the exact published transport stage/mode/escort/monitoring/oxygen/Arrives/message fields. TransportSubsystem.Enter publishes Arrives only in PREPARING (preparation end plus travel) and IN_TRANSIT (arrival boundary). The panel labels it Expected arrival only when supplied; all other states omit that timing row. No interpolated progress, eligibility judgement, automatic order or animation-completion action is introduced.

The lab board lists at most the latest three observed blood orders and their public states, never Results or Report. Current Core supports explicit held samples: take_sample with send=later publishes SAMPLE_TAKEN until send_samples; the earlier blanket statement that every sample starts processing immediately is historical. This door batch does not add physical dispatch controls. The normal Diagnostics tablet retains its existing immediate-send sample flow.

UI-only wording is a Unity TextAsset (Assets/Data/diagnostic-door-presentation.json); no shared clinical content/schema changes. Existing Orders transport form is retained. Cancel/restart/return views follow cancel_transport and subsequent Core ticks, without adding a new cancellation UI in this batch. Physical patient modes, escort animation, NPC-only door passage and device acceptance remain the s03 dependency/acceptance work.

## M2 visible transport — 28 October 2026

TransportPresentation and PatientTransportView project the current Session's ObservableState only. Public transport State/To/Mode/Escort/Monitoring/Oxygen and public BehaviourMode select the mode, chosen crew/kit and cosmetic gait. Exact roles are porter, nurse and doctor; monitoring levels none/portable/full remain Core/content choices. No private NEWS2, dose, suitability calculation, clinical clock or outcome write is used by the view. request_transport remains the existing Orders action; cancel_transport is exposed on Diagnostics through its existing logged/debounced request adapter.

IN_TRANSIT displays outbound motion and holds outside until Core ARRIVED/DONE; RETURNING permits the return sequence; BACK/CANCELLED/session replacement clean up and restore saved bedside renderer flags. A Core arrival time jump skips an unfinished outbound animation; late scene/session snapshots show the current phase rather than inventing a past trip. The original patient/sensors stay active. Unknown modes/states, missing endpoint or unavailable/blocked routes cannot advance Core. Diagnostic client routes cover ct/ultrasound/xray/mri, with lab sample-only. OT remains later presentation scope.

Accompany reads Core TraineeLocation changes, not animation timing or inferred request flags, and uses safe corridor/bay movement only. Initial snapshots do not warp. Its own room/fade requests have cancellation on session replacement; it cannot cancel an unrelated movement. The trainee remains physically blocked by B-room walls while NPC geometry passes the opaque cosmetic threshold. Current actors are labelled M2 prototypes, not approved final humans/equipment or measured device acceptance.

## Nonblocking wait status — 28 October 2026

DiagnosticWaitOverlay consumes ObservableState.ClockTime/Location, Transport.State/To/Arrives, public investigation Label/State/Ready and Phone.State; human location labels come from the public location catalogue. It uses SimulationHost.Applied only to display the outcome of accepted time_skip actions (actual clock and stopped reason). No clock, private clinical state, expected-return calculation or action emission is added. RESULT_AVAILABLE drives a count, never automatic view_result/acknowledge_result/answer_phone. A ringing phone remains independently visible after result reading.

Current Core exposes an arrival estimate only for PREPARING/IN_TRANSIT, not an expected return. This feature branch base has no screen-wait-duration setting. The newly fetched origin/main (79662140) now supplies wait_overlay.seconds and a separate WaitOverlay implementation; reconcile these during integration, as recorded in execution-plan section53. Those are explicit dependencies; Unity has not invented values or edited shared content/contracts. Nurse result audio and synchronized physical report/EMR delivery remain later work. Session reset/host rebinding and component disable remove old action listeners and notifications.

Attention-stop feedback is shown alongside any ready-result/ringing-phone summary. A wait interruption must not cover the independent critical-call notice. The regression uses an explicitly test-only attention subsystem with the existing SC-8 investigation fixture; no case approval/content is changed.
