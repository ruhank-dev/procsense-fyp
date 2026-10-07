# p0-simulation-workflow-s15 — Step 12: transfer and SBAR handover

Milestone: **M3 Walking skeleton 3 — OT Sign In / Time Out**

## What
Transfer to theatre (requires signed ward checklist) with the escort rule, then an SBAR handover to the anaesthetist built from known facts; completeness and accuracy against required elements; critical omissions flagged; anaesthetist prompts for missing items in practice mode.

## Why
Walkthrough Step 12: structured handover prevents omissions; S2 handover has mandatory items.

## How to implement
1. L12-01: `request_transport(OT)` blocked unless the ward checklist is signed.
2. L12-02: `handover(section, items[])` — S/B/A/R sections filled from the 'Known' facts menu (P0); free speech is P1.
3. Required elements per scenario from `content/handover_required.json` — S2 must include aspiration risk, antibiotic name + time, fluids, latest NEWS2, lactate, kidney status, pregnancy status, scan result (walkthrough).
4. Allergy or fasting uncertainty omitted → critical (E12-A/B); practice: anaesthetist asks a follow-up.
5. Corridor deterioration ignored during transfer → arrest branch (via transport s13 + events).

## Verification
`dotnet test --filter FullyQualifiedName~Handover` → T12-1…T12-3 green; VAR-S1-A SBAR from walkthrough Scene 10 scores complete.

## Files changed
`src/ProcSense.Core/Workflow/Steps/HandoverSubsystem.cs`, `content/handover_required.json`, tests.

## Dependencies
p0-simulation-workflow-s12, s13.

## Common issues
Handover accuracy: a stated fact that contradicts state is worse than an omission — score it separately.

## Sources
Implementation Guide §18 (L12-01/02, E12-A/B, T12-1…T12-3); walkthrough Step 12, Part 12 Scene 10.
