# Annotation guide: communication-act benchmark

For the people who write, second-label and adjudicate the independent dev set (100 rows) and test set (200 rows). Read this before the sheets. The act definitions are the approved ones in `content/assessment/taxonomy.json`
and are not changed here; this guide only says how to break ties when one sentence fits two acts, so that two people reach the same label.

## What each person does

| Role | Does | Must not |
|---|---|---|
| **Author** (at least two per split; dev and test authors are different people) | Opens `cards_dev.csv` or `cards_test.csv`, takes the rows of their `author_slot`, and writes one sentence per card in their own words in `text`, then types their name in `author`. | Look at `tools/synth/gen_utterances.py` or `tests/classifier/*.jsonl`; write for both dev and test; copy a sentence they have seen anywhere; ask a language model for sentences. |
| **Second labeller** (one person, not an author of those rows) | Receives only `label_sheet_<split>.csv` (card id and text, **no act**), writes the act id in `label_2` and their name in `labeller`. The sheet holds every critical-act row plus a quarter of the others, mixed, so critical rows cannot be spotted. | See the author sheet or the intended acts before finishing. |
| **Adjudicator** (a third person) | For each row where `label_2` differs from `act`, reads the sentence with this guide, writes `adjudicated_label` and their name in `adjudicator`. | Be the author or the second labeller of that row. |

The final label of a row is `adjudicated_label` if there is one, else `act`. `dataset.py freeze` refuses to build the dataset until every rule above holds.

## Rows to write

20 labels (19 acts + NONE); 5 are critical (`CONFIRM_IDENTITY`, `STATE_ALLERGY`, `CONSENT_SIGN`, `HANDOVER_CRITICAL_ELEMENT`, `SBAR_ELEMENT`), and `mapping.json` agrees.

| | Dev | Test |
|---|---|---|
| each critical act | 5 | 15 |
| each of the 14 other acts | 5 | 8 |
| NONE | 5 | 13 |
| **Rows** | **100** | **200** |
| hard-case rows in each of negation, correction, incomplete, ambiguous | 8 | 16 |
| author slots | A, B | C, D |

The rest of the rows are `plain` or `paraphrase`. Each card names the act, the addressee (patient, colleague or anyone), a situation, a subject and an instruction for its bucket. It never gives wording.
The ASR-noise rows are **not authored**: they are made later by speaking the frozen sentences with the offline voice and transcribing them with the speech recogniser (`tools/bench/asr_roundtrip.py`), so the errors are a recogniser's own. They are scored as a separate group and never count towards the 100/200.

## One label per sentence: tie-break order

Judge the sentence on its own words (the previous turn is not available to the classifier in this first benchmark: `ANSWER_QUESTION` is defined by its content, and that is a known limitation).
Apply the first rule that fits.

1. **Critical acts to a patient win over everything else.** If one sentence fits several, the earlier in this list wins: `CONFIRM_IDENTITY`, `STATE_ALLERGY`, `CONSENT_SIGN`. An allergy question is `STATE_ALLERGY`, never `ASK_TOPIC`; a name or date-of-birth check is `CONFIRM_IDENTITY`.
2. **To a colleague, a statement of an allergy, fasting state, identity mismatch or deteriorating sign is `HANDOVER_CRITICAL_ELEMENT`.** The same content said to the patient is `STATE_ALLERGY` or `ACKNOWLEDGE_FACT`.
3. **A structured handover sentence to a colleague** (situation, background, assessment or recommendation) that is not rule 2 is `SBAR_ELEMENT`.
4. **Order versus escalation.** If the purpose is a test, a medicine, transport or a call for a task, it is `VOICE_ORDER`. If the purpose is to get a senior's or colleague's judgement, help or decision, it is `ESCALATE_SPEECH`.
5. **Repeating back.** Repeating an instruction or result to a colleague is `CLOSED_LOOP`; noting back what the patient said is `ACKNOWLEDGE_FACT`.
6. **Explaining.** Information about what the operation or anaesthetic is: `EXPLAIN_PROCEDURE`; what a test is for: `EXPLAIN_TEST`; what you are about to do to the patient now: `EXPLAIN_ACTION`.
   Comfort with no new information is `REASSURE`; naming the patient's feeling is `ACKNOWLEDGE_EMOTION`. If both are in one sentence, the first clause decides.
7. **Questions to the patient.** About a history topic (not allergy or identity): `ASK_TOPIC`. Asking the patient to say back what they understood: `CHECK_UNDERSTANDING`. Asking for something to be repeated or explained: `CLARIFY`.
8. **A statement that informs the patient and fits no rule above** is `ANSWER_QUESTION`.
9. **Reporting broken equipment or the environment:** `REPORT_FAULT`.
10. **Anything else, and speech that is unintelligible: `NONE`.**

### Hard-case rows

| Bucket | How to label |
|---|---|
| negation | Label the act the sentence performs ("I haven't asked about allergies yet" is not `STATE_ALLERGY`; it is `NONE` unless it performs another act). |
| correction | Label the act the sentence ends up performing after the correction. |
| incomplete | Label the act if the meaning is recoverable; if the author's own meaning cannot be recovered by a reasonable reader, `NONE`. |
| ambiguous | Label with the tie-break order above. These rows are scored separately and are left out of the headline numbers. |

## After the sheets are filled

1. Check no author wrote for both splits and that nobody saw the generator's templates.
2. `uv run --project tools tools/bench/dataset.py label-sheet dev` (and `test`) to make the blind sheets for the second labeller; they return them filled in the same folder.
3. The adjudicator fills `adjudicated_label` and `adjudicator` on the author sheets.
4. `uv run --project tools tools/bench/dataset.py freeze` validates everything and writes `tests/classifier_independent/{dev,test}.jsonl` and `manifest.json` (hashes, authors, kappa on the double-labelled rows). Commit the manifest **before** any model is run on the test split.
5. `uv run --project tools --with scikit-learn tools/bench/bench_classifier.py --data independent`.
