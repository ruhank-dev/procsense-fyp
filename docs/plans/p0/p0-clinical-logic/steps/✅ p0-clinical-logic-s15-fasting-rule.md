# p0-clinical-logic-s15 — Fasting assessment at time of anaesthesia

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Fasting status from the recorded last-food and last-drink clock times compared with the **planned time of anaesthesia** using `fasting_rules.csv` (walkthrough Part 9: clear 2 h / light meal & milk 6 h / heavy meal 8 h; GLP-1 and SGLT2 policies institutional); status unknown / meets / breach / uncertain; breach → decision point DP-S1-FAST (inform anaesthetist & delay); S2 full stomach → RSI plan, not cancellation.

## Why
Walkthrough Part 12 'good catch' teaching: the 2-hour rule applies at anaesthesia time, not when you ask.

## How to implement
1. `Clinical/Fasting.cs` (rule R-FAST-01 in content); evaluated when both last food and last drink are known (L2-02).
2. Status chip in ObservableState; breach opens DP-S1-FAST (B-R12 worked example).
3. Tests: VAR-S1-A water 06:30, surgery 09:00 → meets rule at anaesthesia time; VAR-S1-G toast + milky tea 07:00, surgery 09:00 → not safe until 13:00 (walkthrough/inventory D456); VAR-S2-F biryani 2 h ago → RSI, not cancelled (D468).

## Verification
`dotnet test --filter FullyQualifiedName~Fasting` green for the three fixtures.

## Files changed
`src/ProcSense.Core/Clinical/Fasting.cs`, tests.

## Dependencies
p0-medical-research-data-s05; p0-simulation-workflow-s02.

## Common issues
Clear 2 h vs 1 h is an institutional choice (decision 4) — content only.

## Sources
Walkthrough Part 9, Part 12 Scene 2; §08 L2-02; B-R12 worked example; inventory D187, D209, D456, D468.
