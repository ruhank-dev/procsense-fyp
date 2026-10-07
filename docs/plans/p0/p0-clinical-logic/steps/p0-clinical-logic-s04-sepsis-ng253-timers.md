# p0-clinical-logic-s04 — Sepsis tiers, timers and lateness

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
NICE NG253 tiering (requires suspected infection; clinical concern can raise the tier; qSOFA never used alone), timers that start when criteria are first met (antibiotics / fluids / review / reassessment per tier from content), recorded antibiotic and fluid times, lateness for the debrief, and the 'say it out loud' recognition action.

## Why
S2 sepsis recognition and timing (Usman, Part 13 Patient D) is a core teaching moment.

## How to implement
1. `Clinical/Sepsis.cs`: `Tier(infection_suspected, news2, single3, concern)` → tier + timers from `sepsis_ng253_tiers.csv`.
2. Timers on the sim clock from 'criteria met'; antibiotic/fluid times come from the medicine workflow log.
3. Lateness per timer → DebriefFacts (p0-scoring-debrief-s04).
4. Verb `say_concern('sepsis')` → recognition item (walkthrough 4.10).
5. Fixture test T-SEP1: Usman timeline — tiers at each time point match the walkthrough (guide example: 14:45 NEWS2 5 + infection → moderate; 15:15 NEWS2 10 → high; verify against Part 13 before freezing).

## Verification
`dotnet test --filter FullyQualifiedName~Sepsis` → T-SEP1, T-SEP2 green; lateness computed as the walkthrough example.

## Files changed
`src/ProcSense.Core/Clinical/Sepsis.cs`, tests.

## Dependencies
p0-clinical-logic-s03; p0-medical-research-data-s03, s11 (Usman timeline).

## Common issues
WT-I13 (NG253 250 mL vs CG174 500 mL bolus) is display-only educational text — no logic may depend on it.

## Sources
B-R18 Steps 2–3, 5, T-SEP1; walkthrough 4.10, Part 13; inventory D100, D196, D207.
