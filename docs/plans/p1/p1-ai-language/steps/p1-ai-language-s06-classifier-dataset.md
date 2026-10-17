# p1-ai-language-s06 — Classifier taxonomy and labelled dataset

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Communication taxonomy (act id, definition, critical flag, positive/negative examples; critical acts reviewed by the medical professional) and a labelled set of ≥ 300 utterances from LLM generation + consented role-play recordings; two labellers (kappa ≥ 0.6); stratified train/dev/test split by speaker/session; dataset card.

## Why
The classifier's accuracy can only be trusted against utterances labelled by people, with critical acts defined by a clinician; a clean, documented dataset is also what makes the P2 small classifier possible.

## How to implement
1. `content/assessment/taxonomy.yaml`; `tools/synth/gen_utterances.py`.
2. Labelling in a simple sheet; compute kappa; resolve disputes.
3. Dataset card per B-M4.
4. Role-play scripts per step (3 sessions each) recorded with consent; transcripts added to the set.

## Status (17 October 2026)

- **Built:** `content/assessment/taxonomy.json` (20 acts: the 15 non-critical acts of the behaviour mapping, the 4 critical acts of `critical_acts` and SBAR_ELEMENT, now critical too, and NONE, each with a definition and examples); `tools/synth/gen_utterances.py` (480 template utterances, the old smoke set, SD-006); and, for the real set, the scenario cards for 100 dev and 200 test rows, the annotation guide with single-label tie-break rules, the blind sheets and the freeze rules (`docs/research/classifier_benchmark`, `tools/bench/dataset.py`).
- **Synthetic set:** `tests/classifier_synthetic` (320 train, 100 dev, 200 test rows in eight simulated writing styles, written and labelled by the model, NOT independent) lets the pipeline and the classifiers be exercised.
- **Blocked on people:** two authors per split, a second labeller and an adjudicator (DT-210).

## Verification
Card complete; kappa computed and recorded; splits frozen with hashes.

## Files changed
`content/assessment/taxonomy.yaml`, `tools/synth/gen_utterances.py`, `data/classifier/{train,dev,test}.jsonl` (dev-only), `data/classifier/card.yaml`, kappa report.

## Dependencies
p1-ai-language-s01.

## Common issues
Same speaker in train and test inflates scores — split by speaker/session (T-TX3).

## Sources
Implementation Guide B-M6 Steps 1–4, T-TX1–T-TX3; inventory D58–D60.
