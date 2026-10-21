# Guide for annotators and the adjudicator (EX-01 phase two)

> **PAUSED (owner decision of 21 Oct 2026):** the manual author/annotator workflow below is on hold. The owner prefers directly written, AI-generated synthetic development data (`docs/research/direct_synthetic_dev_set_design.md`; the multi-model factory was abandoned) and to spend human effort on the final Meta Quest test. The tooling, packages and guides are kept unchanged for possible later use; nothing here was deleted.

You label what a speaker DOES with one spoken sentence, using only the approved definitions. You are not judging whether a clinician would be pleased, and you are not checking any program. You will not be told what any system thinks.

## What you have
A file of items: `item_id`, `scenario_step`, `scenario_context`, `prior_turns`, `speaker_role`, `utterance`. Work alone; do not discuss items with anyone, including the other annotator; do not look at any benchmark, prompt, model output or earlier label.

## What you read
* The approved actions and their definitions: `content/assessment/taxonomy.json` (also listed in `AUTHOR_BRIEF.md`).
* The annotation rubric `tests/classifier_synthetic_v2/rubric.md` and the scenario steps `content/assessment/mapping.json`.

## What you fill for every item
| column | value |
|---|---|
| `acts_performed` | every action of the taxonomy the speaker performs in this utterance, separated by `;`, or `NONE`. Critical actions (identity, allergy, consent, handover element, SBAR element) are labelled as they are; they are never credited from speech |
| `actor` | `trainee`, `other` or `unclear` (who does the act) |
| `modality` | `actual` (done now), `negated`, `intended` (later), `hypothetical`, `reported`, `incomplete` |
| `evidence` | the words of the utterance that show the act, copied exactly (empty if none) |
| `credit_eligible` | `yes` only if an ORDINARY (non-critical) act is performed now by the trainee; otherwise `no`. The check tool reports any row that breaks this rule; do not alter your reading to satisfy it, adjudication will decide |
| `ambiguous` | `yes` if a reasonable clinician could read it either way (say why in `note`) |
| `definition_insufficient` | `yes` if the approved definitions or scenario rules cannot establish ONE correct label (for example two actions fit equally and the definitions do not separate them). You MUST say why in `note`. This is a specification problem, not an error of yours and not a failure of any system |
| `note` | free text |

Rules of thumb: the scene (`scenario_step`, the earlier turns) may change what an utterance does; an utterance can perform an action, ask about it, report it, deny it or only imagine it; a request that someone else do something is not the speaker doing it; do not guess what a program would output; do not invent clinical requirements.

## Adjudicator
You receive only the items on which the two annotators differ, with the same columns. Fill the same columns. If the definitions cannot settle it, mark `ambiguous` or `definition_insufficient` with a note: the item is then excluded from the scored set, kept in the record, and sent for specification review. You never see any system output.

## Check your file
`uv run --project tools tools/bench/ex01_kit.py check-annotator --file YOURS.csv --package THEBLINDFILE.csv`. Return the file to the owner; do not edit it afterwards.
