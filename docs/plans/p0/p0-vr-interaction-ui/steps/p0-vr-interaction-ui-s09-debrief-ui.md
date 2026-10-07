# p0-vr-interaction-ui-s09 — Debrief screen and timeline

Milestone: **M7 S1 complete in VR**

## What
Debrief panel readable in ~2 minutes: end state, critical items first, category bars, items with evidence links (tap → log time, what happened, rule, source), timeline with guideline windows and lateness (e.g. sepsis antibiotic window), unnecessary tests with 'cost', next-case recommendation (practice), AI narrative tab hidden until P1.

## Why
The debrief is where learning happens: trainees must see quickly what went wrong, why it matters, and the exact moment it happened — with sources, so the feedback is trusted.

## How to implement
1. `Scripts/UI/Debrief/DebriefView.cs` reading the ScoreReport/DebriefFacts JSON.
2. Timeline markers link to log sequence numbers (T-DU1).
3. Collapse minor items by default (B-S5 troubleshooting).
4. Evidence drawer: tap an item → log time, what the trainee did/said, rule ID, source (register ID → citation).
5. Template/AI toggle present but AI tab hidden until P1 (T-DU2 offline path).

## Verification
T-DU1–T-DU4 green; testers read the debrief in ≤ 2 minutes and can name their top mistake (EXP-SC-2).

## Files changed
`unity/Assets/_Project/Scripts/UI/Debrief/{DebriefView,TimelineView,EvidenceDrawer}.cs`, debrief prefab.

## Dependencies
p0-scoring-debrief-s04.

## Common issues
Too long → collapse minor items by default; an item with no evidence link means the scorer didn't attach log references (fix in scoring, not UI).

## Sources
B-S5 Steps 1–2, 4–5, T-DU1–T-DU4.
