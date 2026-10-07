# p0-simulation-workflow-s06 — Two-level flow: the 15 steps + S1/S2 20-stage maps

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Level 1 = the walkthrough's 15 steps (canonical IDs, Part 11). Level 2 = scenario master maps (Parts F/G, 20 stages each) that order the steps per scenario, run **parallel tracks** (theatre preparation alongside the patient; S2 optimisation alongside investigations) and let deterioration/emergency stages (18–19) interrupt at any time.

## Why
S2 is defined by overlap and a running clock; S1 prepares the theatre while the patient is assessed. A single linear path can't represent either.

## How to implement
1. Load `content/flow_S1_20_stages.csv` and `flow_S2_20_stages.csv` (10 fields per stage: medical detail, trainee action, what they see, normal pathway, abnormal branches, escalation/NPC, equipment response, if missed, source, validation) — content from p0-medical-research-data-s08.
2. `Workflow/FlowMap.cs`: a stage references step IDs; several stages can be ACTIVE (tracks); interrupts (stage 18 deterioration, 19 emergency) push onto a stack and pop back when resolved.
3. ObservableState exposes active stages per track (the tablet shows 'what's happening').
4. Tests: S1 theatre-prep track progresses while the patient is in Step 6–7 (T-CG9 equivalent for S1); S2 optimisation runs while results are pending (T-CG9); emergency interrupt returns to the previous stage.
5. EXP-WF-1: encode all 40 stages; count stages needing custom code (target 0) — record in the phase README.

## Verification
`dotnet test --filter FullyQualifiedName~FlowMap` green; EXP-WF-1 result recorded (0 custom-code stages).

## Files changed
`src/ProcSense.Core/Workflow/FlowMap.cs`, tests, phase README experiment log.

## Dependencies
p0-simulation-workflow-s05; p0-medical-research-data-s08 (flow CSVs).

## Common issues
Stage numbering differs between walkthrough parts (WT-I09) — steps are the canonical IDs, stages only order them.

## Sources
B-R12 Step 8, T-CG9; walkthrough Parts F, G; Change Report WT-I09.
