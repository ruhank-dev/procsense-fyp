# p0-simulation-workflow-s10 — Step 8: deciding, treating, optimising, escalating

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
The trainee states the decision outcome (SAFE / INVESTIGATE / OPTIMISE / REVIEW / EMERGENCY), escalates with SBAR to the right role, names concerns ('I think this is sepsis'), and requests treatments through the medicine workflow; postpone/redirect actions lead to end states. The question 'is it safe to continue?' is re-asked whenever information changes.

## Why
Walkthrough Step 8's master decision is the central skill the simulator rewards.

## How to implement
1. Verbs `declare_outcome(outcome, reason)`, `escalate(role, sbar{s,b,a,r})`, `say_concern(topic)`, `postpone(reason)`, `redirect(service)`.
2. Compare declared vs expected outcome from the decision engine (p0-clinical-logic-s13) → score item with evidence.
3. Treatment requests go only through `request_medication` / `request_fluids` / `request_oxygen` (p0-clinical-logic-s11) — **no trainee administer verb** (walkthrough Part 8.1; the guide's `administer(t)` wording in L8-01 is implemented as the nurse NPC's action).
4. L8-02 / E8-A: allergen requested → nurse challenge (beginner) or hypersensitivity event (advanced, seeded) — WT 14.4.
5. E8-B: S2 dehydration untreated → deterioration clock speeds up (physiology).
6. Escalation completeness: SBAR elements present vs required for the recipient (minor item if incomplete — L7-04).

## Verification
`dotnet test --filter FullyQualifiedName~Decision` → T08-1…T08-4 green; VAR-S1-B2 fixture (BP repeat ≥ 180/110 per walkthrough) → declare OPTIMISE + postpone → POSTPONED_APPROPRIATELY.

## Files changed
`src/ProcSense.Core/Workflow/Steps/DecisionSubsystem.cs`, tests.

## Dependencies
p0-clinical-logic-s11, s13; p0-physiology-events-behaviour-s04.

## Common issues
Thresholds tell the trainee when to think and whom to involve — never auto-cancel (walkthrough decision principle).

## Sources
Implementation Guide §14 (L8-01…L8-04, E8-A/B, T08-1…T08-4), B-R11 Step 9 (no administer verb); walkthrough Part 3, Step 8, 14.4, 14B.
