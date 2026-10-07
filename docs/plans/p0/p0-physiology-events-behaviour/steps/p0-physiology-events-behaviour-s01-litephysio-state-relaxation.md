# p0-physiology-events-behaviour-s01 — LitePhysio: state, baselines, relaxation

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
The physiology engine behind `IPhysiologyEngine { Initialise(baseline, rng); Apply(effect); Advance(dt); Read() }`: variables HR, SBP, DBP, MAP, RR, SpO2, Temp, EtCO2, glucose, volume_deficit, pain, nausea, sedation, allergy_level; baselines per case; each tick every variable relaxes toward its target (baseline + active effects).

## Why
Vitals must drift and respond to treatment; values frozen at baseline teach nothing.

## How to implement
1. `Physiology/IPhysiologyEngine.cs`, `LitePhysio.cs`, `PhysioSnapshot`.
2. Relaxation: `x += (target − x)·(dt/tau)` per variable; tau defaults (guide: HR 60 s, BP 90 s, SpO2 30 s) in `content/physio/model.json` (Type D, register rows).
3. Clamp to bounds: Simulation guard bounds (not clinical) from the guide: HR 30–200, SBP 50–220, RR 4–45, SpO2 60–100, Temp 34–41 — stored in content, Type D.
4. `PhysioEffect {id, targets{HR:+8, SBP:-10…}, profile{onset_s, peak_s, plateau_s, offset_s}, source}` — contributions summed with their time profile.
5. Observations subsystem (p0-simulation-workflow-s03) switches from case baselines to `Read()`.
6. Tests: no effects → stays at baseline (± noise once s02 lands); target change converges smoothly; same seed → identical trace (T-P5).

## Verification
`dotnet test --filter FullyQualifiedName~LitePhysio` green; T-P3 (stacked effects clamped) and T-P5 pass; VAR-S1-A vitals unchanged over 1 h sim with no events.

## Files changed
`src/ProcSense.Core/Physiology/*.cs`, `content/physio/model.json`, register rows, tests.

## Dependencies
p0-core-engine-s04.

## Common issues
Oscillation → tau too small vs the 0.5 s step: increase tau or sub-step (guide troubleshooting).

## Sources
Implementation Guide B-R13 Steps 1–2, T-P3/T-P5; walkthrough Part 4.
