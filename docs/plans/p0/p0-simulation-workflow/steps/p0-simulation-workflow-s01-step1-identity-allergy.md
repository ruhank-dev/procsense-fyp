# p0-simulation-workflow-s01 — Step 1: identity and allergy check

Walking skeleton 1 · step 6 of 15

## What
Walkthrough Step 1 logic in Core: introduce yourself, ask the patient to **say** name + DOB, read the wristband, open the notes,
compare identifiers, confirm identity (or report a mismatch), ask about allergies **and what happened**, ask the reason for admission,
check the responsible adult (S1). Until identity is confirmed, blood tests / medicines / transfer actions are **rejected**.

## Why
Walkthrough Step 1: wrong-patient errors are among the most serious hospital mistakes; everything later depends on this step.

## How to implement (rules from Implementation Guide §07, table L1-01…L1-WT2)
1. `Workflow/Steps/IdentitySubsystem.cs` registers verbs: `introduce_self`, `ask_identity` (open question), `read_name_to_patient`
   (closed — the wrong way), `read_wristband`, `open_notes`, `confirm_identity`, `report_mismatch`, `ask_allergies`, `ask_allergy_reaction`,
   `ask_reason_for_admission`, `ask_responsible_adult`.
2. `confirm_identity` enabled only after identity asked + wristband read + notes opened; sets `identity_verified=true` only if every
   identifier present matches (walkthrough: at least two must match; any mismatch → stop). Mismatch → `ST1_MISMATCH`.
3. Unlock rule L1-02: verbs tagged `requires_identity` (blood tests, medicines, transfer — registered later) rejected with
   `IDENTITY_NOT_CONFIRMED` before verification; logged as CRITICAL evidence in assessment mode.
4. L1-03: `introduce_self` as first act → trust +1, anxiety −1 (Type D design values, placed in content `behaviour.json`).
5. L1-WT1: `read_name_to_patient` → patient agrees; flag `risk.wrong_blood` armed (consequence fires in a later phase).
6. Answers come from the case file (`identity`, `allergies`, `reason_answer`, `responsible_adult`); nothing typed in code.
7. Tests T01-1 (correct path), T01-2 (blood test before identity → rejected), T01-3 (mismatch variant — build a test fixture case
   derived from VAR-S1-A with a different wristband DOB, marked test-only), L1-WT1 flag.

## Verification
`task test` green; CLI run of a correct Step 1 action script shows `identity_verified: true` in the observable state; a script
that orders bloods first shows the rejection reason in the log.

## Files changed
`src/ProcSense.Core/Workflow/Steps/IdentitySubsystem.cs`, `content/behaviour.json` (+ register rows for its Type D values), tests, test fixtures.

## Dependencies
p0-core-engine-s04.

## Common issues
- A stub verb for "order blood test" is needed to prove the unlock rule — mark it clearly as a placeholder until clinical-logic adds the real one.
- Keep "what the patient says" as content text, not code strings.

## Sources
Implementation Guide §07 (L1-01…L1-WT2, T01-1…T01-5), B-R11 Step 9; walkthrough Part 11 Step 1, Part 12 Scene 1.
