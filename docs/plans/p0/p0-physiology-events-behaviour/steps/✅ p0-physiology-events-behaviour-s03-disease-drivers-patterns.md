# p0-physiology-events-behaviour-s03 — Disease drivers and the 9 pattern presets

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Disease drivers (e.g. S2 dehydration increases volume_deficit over time unless a fluids effect is active) and pattern presets for the walkthrough's 9 patterns in 4.11 — normal, pain, infection, dehydration, sepsis, bleeding, allergy, opioid, AF — as effect bundles applied by cases/events.

## Why
The walkthrough teaches recognition through patterns (e.g. infection, dehydration, sepsis, bleeding, allergy, opioid, AF): the same signs must appear consistently every time a case uses that pattern.

## How to implement
1. `content/physio/patterns.json`: 9 presets as effect bundles (direction from WT 4.11, magnitudes Type D).
2. `Physiology/Drivers.cs`: time-based drivers referenced by case files.
3. Tests T-P1: each effect/pattern changes variables in the walkthrough's direction within its timing band.

## Verification
`dotnet test --filter FullyQualifiedName~Patterns` → T-P1 green for all 9 presets.

## Files changed
`src/ProcSense.Core/Physiology/Drivers.cs`, `content/physio/patterns.json`, tests.

## Dependencies
p0-physiology-events-behaviour-s02.

## Common issues
Two drivers acting on the same variable can double-count — couplings and drivers must sum through the effect system, never write vitals directly.

## Sources
B-R13 Steps 5, 8, T-P1; walkthrough 4.11.
