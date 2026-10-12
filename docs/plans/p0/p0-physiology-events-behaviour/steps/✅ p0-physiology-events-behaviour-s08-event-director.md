# p0-physiology-events-behaviour-s08 — Event Director

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Runtime for approved events: load library (status per mode), candidate evaluation on actions and every 10 s sim (eligible ∧ prerequisites ∧ trigger ∧ not on cooldown ∧ concurrency limit 1 clinical + 1 non-clinical), weighting (base × patient × action × timing × difficulty modifiers, seeded draw on 'events' stream), fixed assessment schedule, firing (physio effects, state patches, cues, communication intents, response timers) and resolution (handled within window vs missed consequences).

## Why
Unexpected events are part of training but must be safe, approved and reproducible.

## How to implement
1. `Events/EventDef` per guide interface {event_id, category, trigger, eligibility, prerequisites, base_probability, severity, cues[], physio_effects[], expected_responses[{action, within_s}], acceptable[], unacceptable[], consequences{handled, missed, wrong}, communication_intents[], difficulty_tier, cooldown_s, sources[], status}.
2. `Events/EventLibrary.cs`: load + reject status below the mode minimum (T-E6); pre-compile rules once.
3. `Events/EventDirector.cs`: Evaluate from Apply (reason=action) and Tick every 20 steps; concurrency + global cooldown 90 s sim in practice; warm-up (no events first 3 min sim).
4. Modifiers in `content/events/modifiers.json`; log candidates + weights at debug level.
5. Assessment: `scenario.assessment_schedule [{event_id, at_min | when}]` — probabilities ignored (T-E5).
6. Resolution checks expected responses on each Apply; window expiry applies missed consequences.

## Verification
`dotnet test --filter FullyQualifiedName~EventDirector` → T-E1…T-E7, T-E9 green.

## Files changed
`src/ProcSense.Core/Events/*.cs`, `content/events/*.json`, tests.

## Dependencies
p0-medical-research-data-s13; p0-physiology-events-behaviour-s05.

## Common issues
Event never fires → trigger keys missing in state; fires too often → check cooldown/modifiers in the debug log.

## Sources
Implementation Guide B-R14 Steps 1–6, T-E1–T-E9; walkthrough Part 14, Part C.
