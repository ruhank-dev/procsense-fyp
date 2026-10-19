# Independent human-labelled set for the rubric evaluator (protocol)

Written 19 October 2026. Nothing in this document is data: no sentence or label is supplied here. Everything the repository holds today (`tests/action_bench_v1`, the classifier challenge sets, the synthetic v2 sets) is **synthetic and written by one author family**, so none of it can show how the classifier or the rubric evaluator behaves on a person's speech (EX-01, EX-18).

## What the set is for

To measure, on sentences nobody on the build side has seen, whether a semantic reader (the rubric evaluator, the current gate, or anything later) names what a trainee's utterance does, refuses what it does not do, and never credits critical content. It is evidence for the debrief only; speech-based scoring stays OFF until the owner decides after reading these results.

## Three roles that must be different people

| Role | Does | Must not |
|---|---|---|
| **Author** (2 or more people) | Writes new fictional dialogue from the author instructions (`uv run --project tools tools/bench/second_author_kit.py sheet`, which shows no existing sentence) | Have seen any sentence of a repository set, the prompts, the model's answers or the labels of anyone else |
| **Annotator** (2 people, working alone) | Labels every item with `annotation_template.csv` (below) from the approved definitions only: `content/assessment/taxonomy.json` and the annotation rubric `tests/classifier_synthetic_v2/rubric.md` | Have written the item, see the other annotator's file, or see any model output |
| **Adjudicator** (1 person, a clinician educator if possible) | Resolves the items on which the two annotators differ and marks the genuinely ambiguous ones | Be the builder of the evaluator |

The owner records in `docs/testing/results/` who filled each role. The tools cannot verify this; `independent: true` is only set from that record.

## What to write (fictional only, no real patient or staff)

Each item is one scenario step (a number from `content/assessment/mapping.json`), a two-line fictional set-up, up to four earlier turns, and **one target utterance** as speech recognition would hand it over (lower case or sentence case, with the occasional mis-heard word). Aim for at least 150 items, written so that the three groups below each hold at least 40:

1. **Performed** (the trainee does the act now): plain and unfamiliar wordings of each ordinary act, with and without speech-recognition damage, and at least 30 items whose act can only be recognised **from the earlier turns** (an answer to the patient's question, a read-back of a colleague's order, noting back a fact the patient just gave, asking to repeat something just said).
2. **Not performed** (should earn nothing): denied, planned for later, imagined, reported as somebody else's act, said by the patient or a colleague, broken off.
3. **Critical content** (identity, allergy, consent, fasting state, deterioration, structured handover): said by the trainee, including wordings that sound like an ordinary act. These can be recorded as semantically correct; they are never credited from speech.

No example is given here on purpose: examples written by the builder would be copied.

## What each annotator records (one row per item)

`tools/bench/rubric_labelling_kit.py template --out DIR` writes the header. Columns:

| Column | Meaning |
|---|---|
| `item_id`, `scenario_step`, `scenario_context`, `prior_turns`, `speaker_role`, `utterance` | The item exactly as the author wrote it (filled before annotation; annotators do not change them) |
| `acts_performed` | Every act from `taxonomy.json` the **speaker performs in this utterance**, separated by `;`, or `NONE`. More than one is allowed when the utterance really does two things |
| `actor` | `trainee`, `other` (somebody else speaks or does it) or `unclear` |
| `modality` | `actual` (done now), `negated`, `intended` (later), `hypothetical`, `reported`, `incomplete` |
| `evidence` | The words of the utterance that show the act (copied exactly; empty if none) |
| `credit_eligible` | `yes` only if an **ordinary** act is performed now by the trainee, otherwise `no` (a critical act is always `no`). The kit reports any row where this differs from the rule, for adjudication |
| `ambiguous` | `yes` if a reasonable clinician could read it either way; then say why in `note` |
| `note` | Free text |

## Procedure

1. Authors write the items; the owner assembles one file of items and removes author names.
2. `rubric_labelling_kit.py check` validates each annotator's file: columns, allowed values, evidence inside the utterance, duplicate ids, any utterance already in a repository set, and the credit rule.
3. Annotators label independently, without discussing.
4. `rubric_labelling_kit.py agree --a A.csv --b B.csv` prints raw agreement and Cohen's kappa for each label field and lists the items on which they differ. Report these numbers with the results: a low kappa means the rubric, not the model, is the problem.
5. The adjudicator labels only the differing items (in a third file with the same columns), and may mark them `ambiguous`.
6. `rubric_labelling_kit.py merge` writes the scored set. Items either annotator or the adjudicator flagged ambiguous are listed separately and **excluded from the scores** (their count is reported); items still awaiting adjudication are not scored.
7. The merged set is frozen (its SHA-256 recorded) **before** any model sees it, then each system is scored once, on the same rows, with its configuration already frozen on the development data. A second look at the set makes it development data.

## What it can and cannot show

Can: precision, recall and F1 per objective on unfamiliar human-written dialogue; false support of denied, planned, imagined, reported and other-speaker items; critical-content leakage; whether the earlier turns help. Cannot: how real trainees speak (the authors are not trainees), recognition accuracy of the real microphone chain, anything about the headset. A set of 150 items gives wide intervals (a rate of 0 in 40 items is only "below about 7 percent" with 95 percent confidence); report the counts with their denominators.
