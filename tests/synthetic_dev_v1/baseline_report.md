# synthetic_dev_v1 baseline of the shipped pipeline (21 October 2026)

DEVELOPMENT evaluation of the system exactly as shipped (hybrid-head classifier, assertion layer, critical cues, critical-mass cap, validation against the frozen scenario context, then the engine's own EvidenceStore through the CLI). Language-model reader OFF (the default). Nothing tuned or changed; `cap_policy` hard. Data: 493 items, AI-written and AI-reviewed, so these numbers are not generalisation evidence. Reproduce: `server/.venv/Scripts/python.exe tools/synth_dev/baseline_eval.py --write` (needs local Ollama with all-minilm and a built `src/ProcSense.Cli`). Per-item results: `baseline_results.jsonl`; all tables: `baseline_summary.json`.

| measure | result |
|---|---|
| credit-eligible items / no-credit items | 176 / 317 |
| legitimate recall (credited by the engine) | 84/176 = 47.7 % (offered = credited: the engine refused nothing) |
| credited = offered precision | 84/99 = 84.8 % |
| false credits on no-credit items | 13 (intended-later 7, another actor 3, reported 1, hypothetical 1, state-absent answer read as a different act 1) |
| wrong act credited on an eligible item | 2 |
| critical items (91): false offers / false credits | 0 / 0 (85 to the menu, 6 abstained: 4 no eligible objective, 2 no act) |
| non-critical items diverted to the menu | 32 (9 of them eligible; 10 by a critical cue, 22 by the head's critical reading) |
| no-credit items not offered | 304/317 = 95.9 % (abstain 195, menu 108, clarify 1) |
| out-of-window (29): offered / credited | 0 / 0 |
| state-absent (15): offered / credited | 1 / 1 |
| contrast sets all correct | 16/33 (5 of 5 critical, 11 of 28 ordinary) |
| multi-intent (30 items, 54 credit acts) | 9 items with a correct credit; 9/54 acts (one offer per utterance at most) |
| structure | 0 exceptions, 0 malformed outputs; no model output to violate a schema (reader OFF) |
| latency | median 17 ms, p95 21 ms, max 1.36 s (first call), 0 time-outs |

Failures (105 = 92 eligible items not credited + 13 false credits) by responsible layer: critical-mass cap 39, objective confusion 24 (head read another act), critical-topic handling 9 (eligible items sent to the menu), semantic interpretation 9 (3 read as no act, 6 clarifications), assertion/modality 7 (performed read as not performed) plus 13 false credits (12 non-performed read as performed), gate threshold 4. The engine layer added no refusal.
