# p0-simulation-workflow-s08 — Step 5: risk and anaesthetic assessment

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
The trainee proposes ASA grade, completes VTE risk, computes scores (S2), and requests anaesthetic review; the anaesthetist NPC is always involved, corrects a wrong ASA as a teaching moment, and agrees the anaesthetic plan ('standard GA' / 'RSI').

## Why
Walkthrough Step 5: a validated risk tool must be used; the anaesthetist decides fitness — the trainee proposes.

## How to implement
1. Verbs `assess_risk(asa)`, `assess_vte(items)`, `compute_score(score)` (calculators from p0-clinical-logic-s05), `request_anaesthetic_review`, `decide_ready(option)`.
2. L5-01: |proposed − expected| within tolerance → credit; else anaesthetist correction line (not a failure).
3. L5-02 / E5-B: VTE not done → major item at readiness.
4. L5-03: score with missing components → prompt to order the missing test (T05-3).
5. L5-04 / E5-A: `decide_ready(proceed)` with an unresolved fasting breach → CRITICAL + readiness blocked until the anaesthetist agrees.
6. Anaesthetist NPC replies from an approved intent table (`content/npc/anaesthetist_intents.json`); anxiety +1 if a delay isn't explained to the patient.

## Verification
`dotnet test --filter FullyQualifiedName~Risk` → T05-1…T05-3 green; VAR-S1-A → ASA 1 accepted, 'standard general anaesthetic' (walkthrough Scene 5).

## Files changed
`src/ProcSense.Core/Workflow/Steps/RiskSubsystem.cs`, `content/npc/anaesthetist_intents.json`, tests.

## Dependencies
p0-clinical-logic-s05 (calculators), p0-clinical-logic-s15 (fasting), p0-simulation-workflow-s07.

## Common issues
Expected ASA per case comes from the walkthrough (B-type expected grades) — store in the case file, never compute it.

## Sources
Implementation Guide §11 (L5-01…L5-04, E5-A/B, T05-1…T05-3); walkthrough Step 5, Part 7, Part 12 Scene 5.
