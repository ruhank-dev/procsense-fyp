# p0-scoring-debrief-s02 — Evidence mapper and decision checkpoints

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Every action becomes an `EvidenceRecord {id, source: deterministic | ai, act, topic, span?, confidence?, log_refs[], accepted, reason}`; a mapping table links behaviours to rules; critical items accept deterministic evidence only; each Part D 'Decision n' is a checkpoint (notice → means → check → do → tell); context check (act must be plausible for the current stage).

## Why
Every score must point to the exact log entries that justify it; AI evidence (P1) must never decide critical items.

## How to implement
1. `Scoring/EvidenceRecord.cs` + `EvidenceStore`.
2. `content/assessment/mapping.json`: behaviour → {acts | actions, rule_id} (the guide's 22 behaviours).
3. Critical guard: critical acts only from deterministic actions (T-EM1); low-confidence AI acts recorded, not credited (T-EM3) — P0 has no classifier yet, so the AI path is just a rejected-by-default branch (T-EM5).
4. Context mismatch → rejected with reason (T-EM4).
5. Checkpoints from stacked-case §9 decisions.

## Verification
`dotnet test --filter FullyQualifiedName~Evidence` → T-EM1…T-EM5 green; golden runs produce identical evidence sets.

## Files changed
`src/ProcSense.Core/Scoring/Evidence*.cs`, `content/assessment/mapping.json`, tests.

## Dependencies
p0-scoring-debrief-s01.

## Common issues
AI evidence may arrive late (P1) — the scorer accepts evidence until case end.

## Sources
Implementation Guide B-M3 Steps 1–4, 6, T-EM1–T-EM5; walkthrough Part D §9, Part C.
