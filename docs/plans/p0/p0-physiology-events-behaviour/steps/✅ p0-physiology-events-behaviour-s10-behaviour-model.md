# p0-physiology-events-behaviour-s10 — Patient behaviour model

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Deterministic behaviour: 13 attributes (anxiety, pain expression, trust, understanding, willingness, cooperation…) + persona constants from the case; transition table on acts/events/ticks with diminishing returns and bounds; modes CALM / ANXIOUS / DISTRESSED / IN_PAIN / CONFUSED / DROWSY / NAUSEATED / UNCOOPERATIVE (first-match with hysteresis); disclosure gating by trust/willingness; physiology coupling; capacity flag (false with confusion, severe pain, hypoxia, hypoglycaemia, hyponatraemia, sepsis — walkthrough Part 10).

## Why
Communication matters: introductions, jargon, reassurance and summarising change what the patient says and does.

## How to implement
1. `Behaviour/BehaviouralState.cs`, `TransitionEngine.cs`, `ModeResolver.cs`.
2. `content/behaviour/transitions.json` [{trigger, when, deltas, diminishing, bounds}] and `modes.json`; magnitudes Type D (guide worked example: reassure anxiety 7 → 6 → 5.3 → 4.8 with ×0.7).
3. Hysteresis ±0.5 on mode thresholds (T-BM4).
4. Disclosure gate reads per-fact thresholds; anxiety/pain emit EFF-ANXIETY to physiology; confusion read from physiology.
5. Capacity flag consumed by consent (p0-simulation-workflow-s11).
6. Plots via `tools/behaviour/plot.py` for the review pack.

## Verification
`dotnet test --filter FullyQualifiedName~Behaviour` → T-BM1…T-BM5 green.

## Files changed
`src/ProcSense.Core/Behaviour/*.cs`, `content/behaviour/*.json`, `tools/behaviour/plot.py`, tests.

## Dependencies
p0-simulation-workflow-s01; p0-physiology-events-behaviour-s02.

## Common issues
Patient never calms → diminishing factor too low. Mode flicker → raise hysteresis.

## Sources
Implementation Guide B-R6 Steps 1–7, T-BM1–T-BM5; walkthrough Part 10, Step 4, 4.6; inventory D49, D75.
