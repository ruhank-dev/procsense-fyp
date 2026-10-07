# p0-clinical-logic-s05 — Clinical calculators

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Pure functions over `scores.json`: AIR (default), AAS, Alvarado, TG18 cholecystitis (diagnosis + grade), TG18 cholangitis, ASGE 2019 risk tier, ASA proposal check, STOP-BANG, plus supporting R ratio, MAP, shock index, AKI stage. Each returns {total, band, items[]}; none decides an outcome.

## Why
Steps 5 and 7 and the S2 diagnosis rely on validated scores; the decision engine consumes them as information.

## How to implement
1. `Clinical/Scores/ScoreCalculator.cs`: one table-driven evaluator for point-sum scores (AIR, AAS, Alvarado, STOP-BANG) — Ponytail: one generic function, not one class per score.
2. Small explicit functions only where logic isn't a sum: TG18 diagnosis/grade, TG18 cholangitis, ASGE tier, AKI stage.
3. Type C settings: AIR vs AAS default (institutional decision 13), ASGE vs ESGE.
4. Unit tests from walkthrough worked examples (guide list: Hamza AIR 7 / AAS 19; Rabia TG18 Grade I; Farzana ASGE high; Rukhsana AKI stage 1) — values verified against Part 7/13 in research before freezing.

## Verification
`dotnet test --filter FullyQualifiedName~Scores` → T-SCO-A, T-SCO-B and all worked examples green.

## Files changed
`src/ProcSense.Core/Clinical/Scores/*.cs`, tests.

## Dependencies
p0-medical-research-data-s07.

## Common issues
Scores inform, never decide (B-M20 config). Keep item wording from content for the UI.

## Sources
Implementation Guide B-M20 Steps 1–2, T-SCO-A/B; walkthrough Part 7; inventory D225–D233.
