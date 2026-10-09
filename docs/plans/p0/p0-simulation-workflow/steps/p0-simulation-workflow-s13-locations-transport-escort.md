# p0-simulation-workflow-s13 — Locations, transport modes and escort rule

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Location graph (patient bay, corridor, OT, the B-door destinations), transport state machine REQUESTED → PREPARING → IN_TRANSIT → ARRIVED → DONE → RETURNING → BACK (+ DELAYED, CANCELLED), location gating ('Patient is in CT'), transfer safety check at PREPARING, accompany flag, escort rule by NEWS2, corridor event hook.

## Why
Diagnostic door transitions and transfer to theatre both run on this; transport choice is scored against patient stability.

## How to implement
1. `Location/LocationGraph.cs`: locations + adjacency + travel minutes from `content/locations.json` (Type D durations); shortest path cached.
2. `Location/TransportSystem.cs`: per-request state + timers; DELAYED via staff/environment events (porter unavailable → message, T-L5); CANCELLED returns to origin (T-L2).
3. Gating: every patient-requiring verb checks location → reject `PATIENT_ELSEWHERE` (T06-4).
4. Transport modes: walk / wheelchair / trolley + escort + monitoring. Wheelchair only for stable S1 patients (walkthrough Part E); unstable patient walking or by wheelchair → major item.
5. Escort rule at PREPARING from `decision_rules.csv` D202 (walkthrough Part 17: NEWS2 ≤4 porter + nurse; 5–6 nurse + monitor; ≥7 doctor + nurse + full monitoring + oxygen — institutional decision 11).
6. Accompany: trainee location follows the patient when accompanying.
7. Corridor event: unstable patient with inadequate escort → corridor deterioration event eligible (T-L7; exact trigger values from the walkthrough 14.9 via content, verified in research).

## Verification
`dotnet test --filter FullyQualifiedName~Transport` → T-L1…T-L7 green; same seed → same delays (T-L6).

## Files changed
`src/ProcSense.Core/Location/*.cs`, `content/locations.json`, tests.

## Dependencies
p0-clinical-logic-s03 (NEWS2); p0-medical-research-data-s05 (escort rule).

## Common issues
Patient stuck IN_TRANSIT → missing adjacency entry. Trainee left behind → accompany flag not sent. The Implementation Guide's 'SpO2 89%' corridor value must be checked against walkthrough 14.9 before use.

## Sources
Implementation Guide B-R16 Steps 1–7, T-L1–T-L7; walkthrough Part E (wheelchair), Part 17 (escort), 14.9; inventory D202.

## Carried-over requirements (added 09 October 2026)

- Add the **patient-location check** (`PATIENT_ELSEWHERE`, Implementation Guide test T-K4): any action aimed at the patient while she is in another location is rejected and logged. Until now the location is the constant `patient_bay` (DT-005).
- Track where the nurse is during a wristband task so she can be "outside" before she enters the bay (p0-simulation-workflow-s18).
