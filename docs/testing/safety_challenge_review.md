# Independent review of the unsafe-speech challenge sets and the independent safety run (protocol, EX-01 part B)

Written 20 October 2026 (p1-ai-language-s16, ADR-0023). **Nothing in this document is a result.** No person has reviewed any row of `tests/safety_challenge`; every label there is the builder's, by construction, and PROVISIONAL. Nothing here approves a threshold: every number marked PENDING is a proposal for the owner (DT-229). Independent validation, clinical acceptance and Quest acceptance are NOT DONE.

## 1. What this protocol is for

`tests/safety_challenge` (2,079 rows, `dev` / `challenge` / `final`) is a synthetic, metamorphic set: each row is a seed sentence put through one transformation (negation, tense, attribution, conditional, quotation, self-correction, hedge, question, fragment, combination, out-of-window, duplicate, ...) with the outcome the construction implies. It shows that the implementation does not fail in the ways its author thought of. It cannot show that the implementation is safe on speech an independent person would produce or on labels an independent person would give. This protocol turns it into evidence in two ways:

* **Part A, label review** of the rows the builder could not settle (every `PENDING_HUMAN_REVIEW` row) and of a stratified sample of the rest, by people who did not write or generate them. It measures how often the by-construction labels are wrong, and removes or relabels the rows that are.
* **Part B, independent run**: new sentences written by people who have never seen the sets, the generator or the implementation's outputs, labelled by two annotators and an adjudicator (the procedure of `docs/testing/independent_labelling_protocol.md`, with the three groups extended as in section 4), scored once with a frozen configuration. This is the EX-01 run; Part A does not replace it.

## 2. Roles (all different people; none of them the builder, the owner of the generator, or the author of `taxonomy.json`)

| role | does | must not |
|---|---|---|
| Reviewer A, Reviewer B (two, working alone) | Fill `reviewer_a_label` / `reviewer_b_label` of `tests/safety_challenge/review_sheet.csv` | See the other's column, the `.jsonl` sets (they hold the expected labels), any system output, the template or seed names |
| Adjudicator (one; a clinician educator if possible) | Settles the rows on which A and B differ, marks the genuinely ambiguous ones `unclear` | Be A or B, or the builder |
| Authors for Part B (two or more) | Write fresh fictional dialogue per `independent_labelling_protocol.md` | See any repository set or this protocol's examples |

The owner records who filled each role in `docs/testing/results/`; the tools cannot verify it, and `independent: true` is set only from that record.

## 3. Part A: the review sheet

`uv run --project tools tools/bench/safety_review_sheet.py` writes `tests/safety_challenge/review_sheet.csv` (560 rows: all 267 `PENDING_HUMAN_REVIEW` rows, which are every ambiguous or clinically relevant row, plus a deterministic stratified sample, at least 3 per split and template and 10 percent, of the rest). Columns: `id, split, sentence, situation, act_in_question`, then the empty `reviewer_a_label, reviewer_b_label, adjudicated_label, notes`. The sheet holds **no prediction, no expected label and no template note**.

Labels (one of): `credit_allowed`, `no_credit`, `unclear`. The question a reviewer answers is the one in the approved sources, not a new rule: *in this situation (a step inside or outside the window of the act named; a patient question or read-back waiting or not), does the speaker PERFORM the named non-critical act now? If yes `credit_allowed`; if the sentence denies, imagines, plans, reports, quotes, retracts, asks about whether to do it, is cut off, hedges whether it happened, is said by someone else, names no act, or the act is critical, or the step is outside the act's window: `no_credit`; if a reasonable clinician could read it either way: `unclear` and say why.* Sources: `content/assessment/taxonomy.json` (definitions and positive and negative examples), `tests/classifier_synthetic_v2/rubric.md` (rule 10: a sentence saying the speaker has NOT yet done something performs nothing), `content/assessment/mapping.json` (windows). Reviewers add no clinical requirement.

Agreement: `uv run --project tools tools/bench/rubric_labelling_kit.py agree` style raw agreement and Cohen's kappa between A and B, per split and per template group, reported with the results. **Disagreement resolution:** the adjudicator decides rows where A and B differ; rows any of the three marks `unclear` are excluded from all pass or fail counts and reported with their number; the builder never settles a disagreement. **Effect on the sets:** a row whose adjudicated label differs from the construction label is relabelled in a NEW version of the set (`metamorph.py` is not edited to fit; the relabelling is a file of overrides with the reviewers' ids), the old numbers stay in the repository, and the change of label is counted: *the label-error rate of the construction* is a result in itself.

## 4. Part B: the independent run (EX-01 extension)

Composition required before the run (the protocol of `independent_labelling_protocol.md`, plus):

* at least 40 items in each group: performed; not performed; critical; and **at least 40 items per unsafe family the taxonomy lists as generated** that could realistically be spoken (negated, future, other person, quoted or reported, retracted, question or advice, hedged, fragment, combinations of two), with the situation given as in the sheet;
* at least 30 items per group in the wrong situation (outside the window; an answer with no patient question waiting; a read-back with none waiting);
* at least 20 natural-speech items per group with recogniser-style damage taken from a real recogniser run, not typed;
* authors and annotators as in `independent_labelling_protocol.md`; items already present in any repository set are removed; the merged set's SHA-256 is recorded **before** any system sees it.

**Frozen configuration** (recorded in the result, `configuration` block of `tools/bench/safety_eval.py`): the sha256 of every service file and engine file listed in it, the embedding model, the critical-mass cap, the rule-set version `INTERPRET_VERSION`, the commit. The set is scored **once** per frozen configuration; a second look makes it development data and the next independent set is needed.

Metrics (raw counts with denominators and Wilson 95 percent intervals, never only rates): unsafe credit (by category, and for combinations); critical false credit; out-of-window and no-context credit; duplicate credit; legitimate recall and credited precision; clarification, abstention and menu rates on legitimate items; credited-under-another-act; malformed output, timeouts and fallbacks (service failures are injected, not sampled); latency p50 and p95; the baseline `4a2ed00` and the candidate on the identical items; confusion matrices; representative errors; the label-error rate of the construction (Part A).

## 5. Acceptance criteria

Existing and PENDING the owner (DT-229, `content/ai/thresholds.json`, unchanged by this step): critical false credit = 0; out-of-window false credit = 0; non-assertive false-credit rate at most 0.01; credited precision at least 0.95; legitimate recall at least 0.50 and not below the classifier route; interpretation p95 at most 300 ms.

Proposed in this step, **PENDING the owner's approval, rationale given, not wired into any gate**:

| proposal | rationale |
|---|---|
| The non-assertive criterion is judged on the **upper bound** of the Wilson 95 percent interval, not the point rate | 0 failures in 100 items is compatible with a 3.7 percent rate; the point estimate hides the sample size |
| No unsafe-family category (negated, future, other person, quoted, retracted, question or advice, hedged, fragment, combination) may show more than 1 credit in 40 items, and the total across categories is reported | a pooled rate can hide one family that is not covered |
| Duplicate credit through a stale session context = 0 | the engine store is the last line and has no reason to fail |
| `asr_negation_lost` and every `unclear` row reported, never counted as passes | no text rule can refuse a negation the recogniser removed; hiding them would overstate safety |
| The label-error rate of the construction (Part A) is below 5 percent, otherwise the synthetic figures of this repository are labelled unreliable | the synthetic numbers are only as good as their labels |

## 6. What it can and cannot show

Can: failure rates of the frozen configuration on unfamiliar human-written speech with human labels, by family; label quality of the synthetic sets. Cannot: how real trainees speak in a headset, recognition accuracy of the real microphone chain (Sannan's DC-08 and DC-09, NOT RUN), clinical acceptance, or any rate for unseen speech from a finite set (zero failures in N items bounds the rate at about 3/N with 95 percent confidence, no more).

## 7. Operational procedure with the kit (`tools/bench/safety_review_kit.py`, tested by `tools/ci/test_safety_review_kit.py`)

Blindness is enforced by the files, not by trust:

* **What a reviewer receives:** only `reviewer_a.csv` or `reviewer_b.csv`, written by `package` from the sheet: `id, sentence, situation, act_in_question, label, note`. Each reviewer gets their own random order. The kit refuses to write or read a reviewer file whose header holds anything else, and its test fails if a file contains `credit_allowed`-style expected labels, review status, template, category, seed, split, a prediction or a model decision. Row ids carry a split letter (`d`, `c`, `f`) and no more.
* **What a reviewer must not receive or ask for:** the `.jsonl` sets (they hold the expected labels), `tests/safety_challenge/taxonomy.json` (it names the expected decision of each category), `reports/ai/safety/*` (predictions, reasons, engine verdicts), `docs/research/unsafe_speech_safety.md`, `docs/research/dt233_final_failures.md`, this repository's tests. Give the two reviewers `content/assessment/taxonomy.json` (approved definitions), rule 10 of `tests/classifier_synthetic_v2/rubric.md`, the mapping windows, and the question in section 3, copied into a handout; nothing else.
* **Steps:**
  1. Owner: `uv run --project tools tools/bench/safety_review_kit.py package --out <folder outside the repository>`; send each file to one reviewer by a channel that does not include the other.
  2. Reviewers label alone, no discussion, no search of the repository, no model. `check --file F --who a` lets a reviewer (or the owner) see missing rows, invalid labels and `unclear` without a note; it changes nothing.
  3. Owner: `agree --a A --b B --out <folder>` prints raw agreement and Cohen's kappa (and the label counts) and writes `adjudication.csv`: only the rows that differ, and rows either reviewer marked `unclear`, with the two labels and no key and no system output.
  4. Adjudicator (a third person; not a reviewer, not the builder, not the owner of the generator) fills `adjudicated_label` for each row. They see the two labels and the sentence and situation; they do not see the key. A row the adjudicator also marks `unclear` is EXCLUDED from every pass or fail count and counted in the report.
  5. Owner: `merge --a A --b B --adj ADJ --out <file>`. Only now is the construction label read. The kit refuses to merge while any difference lacks an adjudicated label, when the two reviewer files are the same, or when a reviewer file fails `check`. The output is an override file: the label-error rate of the by-construction labels by split and by template, the excluded rows, and the rows whose reviewed label differs from the construction. The generator and the sets are never edited.
  6. The owner records who filled each role, the dates and the files' sha256 in `docs/testing/results/EX-01-part-b.md`. Only that record can make a label "independently reviewed" (and the sets' `independent` flag stays false until Part B's authors and annotators also exist).
* **Ambiguity rule:** `unclear` is a legitimate answer and costs nothing. Rows that were `ambiguous_review` by construction (about 6 percent of the sheet) are expected to be `unclear` or to split the reviewers; they stay excluded and reported, never counted as passes. A disagreement is never resolved by the builder, by majority of two, or by looking at the system's decision.
* **What the review result is allowed to change:** the labels used to score (through the override file) and the reported label-error rate. It does not change the frozen candidate, the final-set hash or any past result.

## 8. Readiness status (20 October 2026)

| item | status |
|---|---|
| Sheet (560 rows), blind packaging, check, agreement, adjudication, merge and override file | **READY** (built and tested with toy data only; no real review result exists) |
| Reviewers A and B, adjudicator, authors for Part B | **BLOCKED**: people not named (owner) |
| Label review (Part A) | **NOT RUN** |
| Independent run (Part B) | **NOT RUN**, blocked on authors, annotators, adjudicator |
| Audio-to-transcript evaluation (Part C, `docs/testing/asr_safety_evaluation.md`) | **DESIGN ONLY**, not run, not validated |
| Acceptance additions of section 5 | **PENDING** the owner (DT-229) |
| Quest and headset checks (DC-08, DC-09; Sannan) | **NOT RUN** |
| Gate `speech_interpretation` | **OFF** |

## 9. Status
Prepared: the sets, taxonomy, generator, evaluator, blind review sheet and kit, mutation tests, this protocol. **Not done:** any human review, the independent run, owner approval of the proposals above, Quest testing. The `speech_interpretation` gate stays OFF.
