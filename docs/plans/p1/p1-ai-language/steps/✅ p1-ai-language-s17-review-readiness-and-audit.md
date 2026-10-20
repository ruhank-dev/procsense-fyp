# p1-ai-language-s17 — Review readiness and audit of the unsafe-speech work: blind review kit, set audit, frozen-test protections, DT-233 mechanisms, audio evaluation design, wider mutation tests

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
A preparation and audit phase after ADR-0023 (owner request of 20 October 2026, prompt 177). **The frozen candidate `s16`, the critical-mass cap (0.05), every threshold and the final split are not changed, and no human review, headset check or independent run was performed.**

1. **Blind review kit** (`tools/bench/safety_review_kit.py`, `tools/ci/test_safety_review_kit.py`, 7 tests with toy data only): `package` writes one file per reviewer (id, sentence, situation, act in question, label, note; own order; no expected label, template, category, seed, split, prediction or decision, refused if present), `check`, `agree` (raw agreement, Cohen's kappa, adjudication sheet with only the two labels), `merge` (refuses without adjudication, the same file twice or an invalid file; excludes `unclear`; writes an override file with the label-error rate of the by-construction labels; never edits the sets). Protocol, adjudication and the readiness table: `docs/testing/safety_challenge_review.md`.
2. **Audit of the sets** (`tools/bench/safety_set_audit.py` → `docs/research/safety_set_audit.md`, `reports/ai/safety/set-audit.json`): provisional against reviewed labels (0 reviewed), category coverage by split, duplicates (39 plain rows that equal the first utterance of a session pair), leakage (no seed family or template crosses a split; wording frames and nearest-neighbour overlap), construction artefacts, templates whose label depends on the situation or is ambiguous by design.
3. **Frozen-test protections** (`tools/bench/safety_frozen.py`, `tests/safety_challenge/frozen.json`): sha256 of the sets, the final-split results and the candidate files; the check fails on any change and says a new configuration needs a fresh final set.
4. **DT-233 audit** (`docs/research/dt233_final_failures.md`): the ten failures reduce to four closed-list or window mechanisms, checked on probes built from development seeds; a preregistered development-only plan and acceptance rule for any later fix; nothing fixed.
5. **Audio-to-transcript evaluation design** (`docs/testing/asr_safety_evaluation.md`): separate from the text evaluation; DESIGN ONLY, not run, not validated (DT-235).
6. **Mutation coverage** (`tools/ci/mutation_safety.py`, `test_mutation_safety.py`): route mutants (feature flag, busy slot, failure is the menu, reason codes, refusal audited as failed) judged by the route tests on PostgreSQL; the store's checks judged END TO END by the engine unit tests through the session log; a new engine test for `NO_RUNNING_STEP`; two new route tests (busy slot, critical sentence audited by reason code). The Unity client is NOT mutation-run.
7. **Handoff**: EX-01 gets the kit commands; DC-08 and DC-09 get an expected-behaviour note and an optional transcript observation for Sannan (NOT RUN stays NOT RUN); DT-229, DT-233, DT-234 updated; DT-235 added.

## Why
The labels are builder-written, so no figure can be called validated until independent people have reviewed them; the reviewers must not be steered; the final split must stay usable as evidence of what it measured; spoken input breaks the one assumption the text rules cannot repair; and a mutation harness that skips the route fallbacks or the store's end-to-end behaviour leaves those guards with untested claims.

## How to implement
Already implemented; to repeat: `tools/.venv/Scripts/python.exe tools/bench/safety_set_audit.py`, `safety_frozen.py --check`, `tools/ci/test_safety_review_kit.py`, `tools/ci/mutation_safety.py --engine --write` (needs dotnet and the PostgreSQL container), `server/.venv/Scripts/python.exe -m pytest server/tests/test_interpret_api.py` with `TEST_DATABASE_URL`.

## Verification
`tools/ci/test_safety_review_kit.py` (7), `tools/ci/test_mutation_safety.py` (9; every mutant killed), `tools/bench/safety_frozen.py --check`, `tools/bench/safety_set_audit.py --check`, engine `InterpretationContextTests` (14), `server/tests/test_interpret_api.py` (9), `tools/ci/test_safety_hardening.py` (the sets and the frozen state).

## Files changed
`tools/bench/{safety_review_kit,safety_set_audit,safety_frozen}.py`, `tests/safety_challenge/frozen.json`, `tools/ci/{test_safety_review_kit,mutation_safety,test_mutation_safety,run_all}.py`, `tests/ProcSense.Core.Tests/InterpretationContextTests.cs`, `server/tests/test_interpret_api.py`, `docs/research/{safety_set_audit,dt233_final_failures}.md`, `docs/testing/{safety_challenge_review,asr_safety_evaluation,external_testing.json}`, `reports/ai/safety/{set-audit,mutation-service}.json`, plans and registers.

## Status (20 October 2026)
Built and tested (kit, audit, protections, mutation tests); preparation only. **NOT DONE: any human review, the independent run (EX-01 part B), the audio evaluation (part C, design only), a fix for DT-233 (needs a fresh final set), owner approval of the acceptance proposals (DT-229). Quest and headset checks: NOT RUN (Sannan, DC-08 and DC-09). The `speech_interpretation` gate stays OFF.**

## Dependencies
p1-ai-language-s16 (the sets, the evaluator, the mutation harness), s14 and s15.

## Common issues
Do not give reviewers the `.jsonl` sets, the taxonomy or any report: they hold the expected labels and the predictions. Do not edit `frozen.json` to make its check pass; a changed candidate is a new configuration. Route mutants need the PostgreSQL container; without it that test is skipped, loudly, and must not be counted as run.

<!-- external-testing:start (generated by tools/plans/external_testing.py) -->
## External acceptance

This step has acceptance that only a person, device or account outside the engineering desk can give. The exact procedure, pass rule and evidence are in the single source of truth:

* [DC-08](../../../../testing/SANNAN_EXTERNAL_TESTING.md#dc-08): Push-to-talk voice client, voice orders with read-back and live speech output (English only)
* [DC-09](../../../../testing/SANNAN_EXTERNAL_TESTING.md#dc-09): Spoken communication interpreted against the scenario state: evidence, clarification, menu fallback and no credit for critical or non-performed speech (English only)
* [EX-01](../../../../testing/SANNAN_EXTERNAL_TESTING.md#ex-01): Independent communication-act set: authors, second labeller, adjudicator, then the independent run

<!-- external-testing:end -->
