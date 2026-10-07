# p0-simulation-workflow-s02 — Step 2: history questions with hidden facts

Walking skeleton 1 · step 7 of 15

## What
`ask(topic)` reveals the case's answer for that topic; the patient volunteers only the main complaint; everything else stays hidden
until asked. Each revealed answer adds a line to the **clerking note** (visible in the observable state). Last food and last drink are
recorded as exact times (no fasting rule evaluated yet).

## Why
Walkthrough Step 2 / Part 16 Stage 2: most diagnoses and dangers come from questions; facts not asked surface later as consequences.

## How to implement (Implementation Guide §08, L2-01, L2-04, L2-05)
1. `Workflow/Steps/HistorySubsystem.cs`: verb `ask` with param `topic` (enum from content: complaint, red_flags, past_history, medicines,
   herbal, allergies, previous_anaesthetic, family_anaesthetic, smoking, alcohol, last_food, last_drink, pregnancy); `summarise`.
2. Requires `identity_verified` (L2-01 condition).
3. Reveal → `Clinical.RevealedFacts` + clerking line; asking twice doesn't duplicate.
4. `summarise` after ≥ 5 topics → trust +1, understanding +1 (L2-05; Type D values in `behaviour.json`).
5. Pregnancy topic marked "sensitive" (asked privately option) — content flag only for now.
6. Tests: un-asked topics never appear in observable; asking reveals exact walkthrough text; last food/drink times stored as clock times (21:00, 06:30).

## Verification
`task test` green; CLI run shows the clerking note growing line by line with the walkthrough wording.

## Files changed
`src/ProcSense.Core/Workflow/Steps/HistorySubsystem.cs`, `content/history_topics.json`, tests.

## Dependencies
p0-simulation-workflow-s01.

## Common issues
- Fasting decision ("2-hour rule applies at time of anaesthesia", walkthrough Part 12) belongs to clinical-logic later — store times only now.

## Sources
Implementation Guide §08 (L2-01, L2-04, L2-05, T02-*); walkthrough Part 11 Step 2, Part 12 Scene 2, Part 16 Stage 2.
