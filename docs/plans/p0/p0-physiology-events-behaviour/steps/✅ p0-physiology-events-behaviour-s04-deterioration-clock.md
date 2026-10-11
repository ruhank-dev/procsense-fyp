# p0-physiology-events-behaviour-s04 — Deterioration clock and the Usman fixture

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Untreated S2 problems worsen on a clock: HR +5–10 and NEWS2 +1 about every 30 min untreated (Type D); the most dangerous untreated problem sets the speed (WT 14B.5). Validated against the Usman (VAR-S2-PD) timeline: 14:00 NEWS2 2 → 14:45 5 → 15:15 10 → 15:45 16 if untreated; correct actions at 14:45 improve BP, HR and lactate (guide values — verify against Part 13 in research).

## Why
S2 time pressure exactly as the walkthrough describes.

## How to implement
1. Clock parameters from `key_rules.csv` / `decision_rules.csv` (D203) — editable without code.
2. `Physiology/DeteriorationClock.cs`: speed from the highest-ranked untreated problem (problem registry, p0-clinical-logic-s14); treatment effects slow/stop it.
3. Fixture tests: T-P6 Usman untreated matches the timeline ±1 NEWS2 point; T-P7 treated at 14:45 → BP rises, lactate falls.
4. EXP-PHY-2 result recorded in the phase README.

## Verification
`dotnet test --filter FullyQualifiedName~Deterioration` → T-P6, T-P7 green.

## Files changed
`src/ProcSense.Core/Physiology/DeteriorationClock.cs`, tests, README experiment log.

## Dependencies
p0-clinical-logic-s14; p0-medical-research-data-s11 (Usman timeline data).

## Common issues
The clock is a design value — label it as such in debrief text.

## Sources
B-R13 Steps 8–9, T-P6/T-P7; walkthrough Part 13 (Patient D), Part 17, 14B.5; inventory D203.
