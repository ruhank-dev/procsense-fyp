# p0-simulation-workflow-s03 — Step 3: sensors and vital signs

Walking skeleton 1 · step 8 of 15

## What
Attach the SpO2 probe → SpO2 and pulse appear; attach the BP cuff and press measure → after the cuff cycle the BP appears;
thermometer reading; count RR; choose alertness (ACVPU); ask pain score. Values come from the case's baseline vitals
(no physiology over time yet). Nothing shows for a sensor that isn't attached.

## Why
Walkthrough Step 3 / Part 16: the monitor is blank until devices are attached; the trainee must gather each value.

## How to implement (Implementation Guide §09, L3-01, L3-02)
1. `Workflow/Steps/ObservationsSubsystem.cs`: verbs `attach_sensor(spo2|nibp_cuff)`, `detach_sensor`, `measure_nibp`, `measure_temperature`,
   `count_rr`, `assess_acvpu`, `ask_pain_score`.
2. `measure_nibp` starts a cuff cycle; BP appears after the cycle duration from `content/devices.json` (walkthrough Part E: ~30–40 s;
   conflict with guide's 20 s logged in issues.md) — a deterministic value inside the range via `SimRandom` stream "devices".
3. Observable: `vitals` contains only measured/attached values with sim time stamps.
4. Requires `identity_verified`? — No: walkthrough Step 3 doesn't gate observations on identity (only tests/medicines). Don't add a gate.
5. Tests: monitor blank before attach; SpO2 98 / HR 76 after attach; BP 124/78 appears only after the cycle; RR 14 after count; temperature 36.7.

## Verification
`task test` green; CLI run shows vitals appearing in order with times.

## Files changed
`src/ProcSense.Core/Workflow/Steps/ObservationsSubsystem.cs`, tests.

## Dependencies
p0-simulation-workflow-s01 (and s02 not required).

## Common issues
- Display bands/colours (normal/amber/red) and NEWS2 come later from clinical-logic — don't hard-code any threshold here.

## Sources
Implementation Guide §09 (L3-01, L3-02, T03-*); walkthrough Part 11 Step 3, Part 12 Scene 3, Part E (BP cuff, pulse oximeter); issues ISS-0001.
