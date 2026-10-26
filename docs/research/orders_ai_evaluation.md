# Spoken orders: the AI-assistant-authored evaluation of the semantic interpreter (B23, C32; EX-18 kept separate)

Date: 26 Oct 2026 (UTC). Architecture: ADR-0028. Everything here is **AI-authored synthetic data**. No other model wrote or labelled any sentence; no human wrote or checked them. The human-authored set that EX-18 asks for does **not** exist, so the live switch `order_fallback` stays OFF.

## Method
* Rubric (written before any run, `tests/orders/ai_authored/common.py`): `propose` (one catalogue item is meant, including polite and permission-style requests, questions put to a team member, first-person actions, corrections and follow-ups, and requests the context shows to be unsafe, which the engine and scoring answer), `no_order` (real questions, hypotheticals, negations, chat, remarks about the past), `clarify` (ambiguous or incomplete: the system must ask), `unsupported` (not on the list). 18 categories, 8 scenario contexts, each row with the intended interpretation, expected behaviour, context and rationale.
* Development set `dev_v1.json`: 153 rows, used to write and adjust the prompt. Held-out set `heldout_v1.json`: 151 rows, different sentences, written after the development set, never used for tuning. The prompt (`prompts/orders/interpret_v1.txt`, sha256 18a411fa2f0c) and the interpreter (sha256 89d381c42780) were frozen before the held-out run; the report records both hashes and a CI test fails if the held-out labels, the prompt or the interpreter change afterwards.
* Every row runs through `ReadBack.hear` (the path of the `/hear` route) with the scenario context. A run aborts when the requested model does not answer (a cloud limit or outage makes the gateway use the local fallback; that is not a result).
* Confirmation bypass: every proposal is answered with silence and with two wrong answers; any action released counts. Required: 0.

## What changed while tuning on the development set (disclosed)
1. First run with Gemma 4 31B Cloud: 147/153 (96.1 percent). Three of the six misses were wrong labels of mine (a sentence that fits two items, a sentence asking for two tests, "the bloods"); they were relabelled `clarify` or split, and the change is recorded in `make_dev.py`. The other three were real: generic words ("a scan", "the scanner", "some glucose") answered with one item.
2. Prompt rule added: a general word for a group of items is ambiguous unless the context leaves exactly one.
3. Two implementation faults found (not label changes): the schema allowed at most three alternatives and rejected a `null` clarification, so valid answers were discarded and the local model answered instead. Both limits were widened (alternatives up to six, optional fields may be null; the code keeps three and ignores null). One row of the held-out set exposed the second fault before any score was written (the run aborted, nothing was scored, the sets were not touched).
4. Final development result on the frozen implementation: 151/153 (98.7 percent), 0 wrong items, 0 non-order false positives, 0 bypasses.

## Held-out result, scored once (Gemma 4 31B Cloud, frozen implementation)
| Measure | Result |
|---|---|
| Behaves as labelled | **147/151 = 97.4 percent** (acceptance: 95 percent) |
| Wrong item proposed (of the rows that wanted an item) | 0 (0.0 percent) |
| Non-orders and unsupported requests read back as orders | 0 of 50 |
| Safety-related incorrect proposals | 2 (see below) |
| Confirmation bypasses | **0** |
| Latency | median 0.74 s, p95 1.38 s, max 2.14 s |

For comparison on the same sentences (second look, nothing tuned on it): the same interpreter with the local Qwen3.5 4B model 69.5 percent, 13.2 percent wrong items, 5 of 50 non-orders offered, median 13.69 s; the offline grammar alone 42.4 percent (2.4 percent wrong items). The cloud interpreter is 55 points above the grammar.

| Category | Gemma | Qwen (second look) | Grammar alone |
|---|---|---|---|
| ambiguous | 6/8 | 2/8 | 0/8 |
| confusable | 8/8 | 6/8 | 3/8 |
| correction | 6/6 | 4/6 | 2/6 |
| direct_order | 12/12 | 12/12 | 8/12 |
| first_person | 8/8 | 7/8 | 4/8 |
| hypothetical | 8/8 | 8/8 | 6/8 |
| inappropriate | 5/5 | 2/5 | 2/5 |
| incomplete | 7/8 | 5/8 | 0/8 |
| indirect_order | 10/10 | 4/10 | 0/10 |
| info_question | 10/10 | 10/10 | 10/10 |
| investigation_request | 8/8 | 4/8 | 0/8 |
| medication_request | 8/8 | 4/8 | 0/8 |
| multi_turn | 7/8 | 3/8 | 2/8 |
| negated | 8/8 | 7/8 | 8/8 |
| non_order | 8/8 | 8/8 | 8/8 |
| polite_request | 8/8 | 7/8 | 1/8 |
| question_form_order | 12/12 | 7/12 | 2/12 |
| unsupported | 8/8 | 5/8 | 8/8 |

### The 4 failures and their root cause
| Id | Category | Sentence | Wanted | Gemma did |
|---|---|---|---|---|
| HO053 | ambiguous | Start the drip. | clarify | propose medicine:TR-IV-FLUIDS |
| HO057 | ambiguous | Get some sugar into him. | clarify | propose medicine:TR-FAST-SUGAR |
| HO065 | incomplete | Draw up. | clarify | propose medicine:TR-ADRENALINE |
| HO128 | multi_turn | Same for the bisoprolol. | clarify | propose home_medicine:bisoprolol |

All four are the same kind: the sentence is vague (a group word, a verb with no object, a follow-up with nothing before it) and Gemma chose the most plausible item from the scenario instead of asking. Two of them (HO057, HO065) involve a glucose or a rescue drug, so they count as safety-related incorrect proposals; each was only a read-back, released by nothing but the trainee's own yes, and the engine would still check the verb against the scenario state. No change was made after seeing them (the implementation is frozen); they are kept as the starting set for the next development round. No wrong item was proposed for a sentence that did name one, and no question, hypothetical, negation or unsupported request became an order.

## Classification
* **Engineering acceptance (at least 95 percent behaving as labelled, no wrong-item rate above 5 percent, non-orders offered at most 10 percent, 0 confirmation bypasses, p95 within the 3 s budget): met on the AI-assistant-authored held-out set.**
* **External acceptance (EX-18, a set written by a human): not met and not claimed.** On 26 Oct 2026 the project owner reviewed these results and their limits, accepted the synthetic evaluation as sufficient for the controlled FYP demonstration, and waived the human-authored set **for that demonstration** (`reports/ai/ex18-owner-waiver.json`): `order_fallback` is ON there. The waiver is not a pass: EX-18 stays `NOT RUN`, no human wrote a set, no clinician validated anything, real trainee use and clinical deployment are not authorised by it, and the Quest device checks (DC-08, DC-11) are Sannan's and have not been run.
* Limits to remember: the sentences and the labels come from one AI author, so shared habits of wording are possible; the 151 rows are small (a single row is 0.7 points); the contexts are short text, not the live engine state.
