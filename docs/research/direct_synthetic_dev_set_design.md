# Direct synthetic development set (500 items): coverage design (p1-ai-language-s21, 21 October 2026)

Status: **design approved by the owner on 21 Oct 2026; the set is generated (no cloud call).** It replaces the multi-model cloud factory, which was abandoned and removed from the repository.

**Result (21 Oct 2026).** Generated and validated: see `tests/synthetic_dev_v1/report.md`. 493 of 500 specifications accepted (7 dropped as boundaries the approved text does not decide, with reasons in `discards.jsonl`); credit-eligible items are 180 in the specifications (the 150 class-A items, the 24 two-performed multi-intent items and the 6 mixed multi-intent items, each of which carries one credit-eligible act) against the 174 planned, because the 6 mixed items were not counted in the plan; 176 of 493 after the drops. The multi-model factory mentioned in this document was abandoned and its code, tests, reports and registrations removed; the useful findings are DT-241 to DT-244 and `tests/synthetic_dev_v1/open_issues.md`.

**What this set is.** AI-written, AI-reviewed (the same assistant, not independent) *synthetic DEVELOPMENT data*. It is not human-labelled, not independent and not real-world validation. We may inspect it and fix ProcSense from its failures; performance on it is never reported as generalisation evidence. A fresh held-out evaluation set will be built later and frozen before scoring. Quest microphone speech → ASR → reader → gates → engine remains the real validation and is always reported separately.

## 1. How ground truth is derived before any wording
1. **Plan** (`specs.json`, produced by code from the approved rules, not by me reading sentences): objective (a taxonomy act or NONE), situation (one of the approved kinds), actor/recipient, step, window, state (pending patient question, pending read-back), plus the **derived gold**: `performs_now`, `credit_eligible` (ordinary act, performed now by the trainee, inside the window, needed state present), critical flag (never credited from speech), the contrast-set id and role. The derivation is `derive_gold` over `taxonomy.json` / `mapping.json` / ADR-0005/0022/0023; nothing is read from a sentence.
2. **Freeze**: the spec file gets a SHA-256 manifest committed to the repo before the utterance file exists.
3. **Write**: utterances are written per spec id in a separate file (`utterances.jsonl`: id, text, prior turn if the spec needs one, style, persona). The utterance file has **no label fields**; labels are joined from the frozen specs only.
4. **Review** (me, against the spec): `ok` or `discard(reason)`. A discarded utterance is logged and a new one is written for the same spec (max 3 attempts, then the spec is dropped and the drop recorded). Never relabelled, never edited into fitting.
5. **Deterministic validation** (existing tools): schema, spec hash match, exact and near-duplicate (skeleton, Jaccard, embedding), historical leakage (`Leaks` against the old benchmarks), distinct-n, self-BLEU, opening and skeleton concentration, stock-phrase cap, form-mark and modality-cue checks, window/state validity, checksums and provenance. Provenance field states: `generation: AI-assisted (an AI assistant), review: AI-assisted (same model), not independent`.

## 2. Coverage matrix (500 items)

### 2.1 By primary class
| class | n | what |
|---|---|---|
| A. Ordinary act, performed now, in window, state satisfied (credit-eligible) | 150 | direct, paraphrase, questions vs assertions, uncertainty on content, context references, patient-question and read-back context |
| B. Ordinary act performed now but NOT eligible | 45 | valid out-of-window (29), ANSWER_QUESTION with no pending question (8), CLOSED_LOOP with no pending read-back (8) |
| C. Ordinary act not performed | 140 | denied 28, intended later 28, hypothetical 28, reported 28, another actor 28 |
| D. Critical act genuinely stated (recorded, never credited from speech) | 55 | 11 per CONFIRM_IDENTITY, STATE_ALLERGY, CONSENT_SIGN, HANDOVER_CRITICAL_ELEMENT, SBAR_ELEMENT |
| E. Critical topic without claiming completion | 30 | 6 per critical act: denied, intended, hypothetical, reported, another actor, plus one plain discussion |
| F. NONE | 50 | small talk 10, thinking aloud 10, filler 8, off-topic 10, near-miss adversarial 12 (critical or clinical words in a non-act sentence, an instruction aimed at the reader) |
| G. Realistic multi-clause / multi-intent | 30 | 24 with two performed ordinary acts (credit per act), 6 with one performed and one non-performed act |

### 2.2 By objective (ordinary acts; critical acts in D and E)
| objective | A | B | C | G | total |
|---|---|---|---|---|---|
| ASK_TOPIC | 11 | 3 | 10 | 2 | 26 |
| CLARIFY | 11 | 2 | 10 | 2 | 25 |
| ANSWER_QUESTION | 10 | 10 | 10 | 2 | 32 |
| EXPLAIN_PROCEDURE | 11 | 2 | 10 | 3 | 26 |
| EXPLAIN_TEST | 11 | 2 | 10 | 2 | 25 |
| EXPLAIN_ACTION | 11 | 2 | 10 | 2 | 25 |
| CHECK_UNDERSTANDING | 11 | 2 | 10 | 2 | 25 |
| ACKNOWLEDGE_EMOTION | 11 | 2 | 10 | 3 | 26 |
| REASSURE | 11 | 2 | 10 | 2 | 25 |
| ACKNOWLEDGE_FACT | 10 | 2 | 10 | 2 | 24 |
| CLOSED_LOOP | 10 | 10 | 10 | 2 | 32 |
| ESCALATE_SPEECH | 11 | 2 | 10 | 2 | 25 |
| REPORT_FAULT | 10 | 2 | 10 | 2 | 24 |
| VOICE_ORDER | 11 | 2 | 10 | 2 | 25 |
| **ordinary subtotal** | 150 | 45 | 140 | 30 | **365** |
| 5 critical acts (D 11 + E 6 each) | | | | | 85 (17 each) |
| NONE | | | | | 50 |

### 2.3 Expected credit vs no credit
Credit-eligible items: A 150 plus the 24 two-performed G items = **174 (35%)**; no credit **326 (65%)**. The skew is deliberate: ProcSense's main risk is false credit, so most of the set tests the cases where credit must NOT be given (non-performed, critical, ineligible, NONE). Eligibility is "recognisable as the act" (what the rules allow), not "will clear a threshold".

### 2.4 Critical and safety-sensitive coverage
D 55 + E 30 = 85 critical-act items (17%), plus the 6 critical-word near-misses of F: **91 safety-sensitive items (18%)**. Every critical act has the same six-way set (genuine, denied, intended, hypothetical, reported, another actor). No ordinary-act item mentions a critical topic (that was the leakage failure of the pilots); an ordinary-act sentence that touches one is discarded, not kept.

### 2.5 Contrast sets
**33 sets, 198 items (40%)**, all from one underlying scenario with the same core content:
* 28 ordinary sets (2 per ordinary act): performed now (from A) + denied + intended later + hypothetical + reported + another actor (from C) = 6 members each, 168 items.
* 5 critical sets (1 per critical act): genuine (from D) + the five E members = 30 items.
* Members are written together from the same scenario in natural wording (not slot substitution); the markers differ ("I haven't…", "I'll … in a minute", "if I were to …", "Sam said she …", "the nurse will …"). Where a request to someone else is the contrast, only the rubric-determined cases are used (task / senior request = performed order or escalation, rule 4).

### 2.6 Style and persona diversity (cross-cutting tags; each item has one style and one persona)
Personas P1–P4 and P6 are used; **P5 (formal, by-the-book) and P7 (calm methodical) are reserved for the future held-out evaluation set**, as are four topic groups. Within the dev set, style tags: concise ~90, conversational ~90, formal/explicit ~45 (written by me in the formal register without using P5), informal ~60, hesitant/uncertain ~50, indirect/polite ~50, verbose ~40, understandable non-native English ~50 (simple wording, no comic English), self-correction ~30 (never lets a correction flip the meaning), pronoun/contextual reference ~35 (needs a prior turn). Rules: no fake ASR spelling errors; no deliberately stilted wording; hesitations sparse and varied.

### 2.7 Preventing template leakage and repetition (my own repetition is the main risk)
* Authoring in small batches (≈10 items) per objective with a different persona/topic seed per batch, topic seeds from the existing T1–T4 slices and equipment lists.
* Opening-word cap and skeleton cap as in `dedupe.py`; a stock-phrase cap (≤5%); self-BLEU and distinct-n reported; exact and near duplicates rejected; embedding similarity to the historical benchmarks ≥0.92 rejected.
* Within a contrast set, members must share the content but not copy one another beyond the marker words; checked by Jaccard ≥0.7 flag.
* Reviewed per batch against the running opener and skeleton table before the next batch.

### 2.8 Excluded (unresolved) and tracked separately
Not generated and never in authoritative labelled data until the owner decides: **DT-243** (a request to someone else to ask, explain, reassure or obtain consent), **DT-244** (claims that an act was already done earlier), **broken-off / incomplete speech** (no real-ASR evidence of how a cut-off looks; a labelled synthetic-truncation experiment may follow separately), speech by anyone other than the trainee. Also excluded: sentences whose spec I cannot make unambiguous after three attempts, and boundary sentences between EXPLAIN_TEST / EXPLAIN_ACTION / EXPLAIN_PROCEDURE or CLARIFY / ASK_TOPIC / CHECK_UNDERSTANDING unless they clearly match the positive examples of the approved taxonomy. Tracked in `docs/research/direct_set_open_issues.md` (to be created with the set): ambiguous objective distinctions, the performed-vs-hypothetical tail (a hypothetical clause that ends by doing the act), reported vs another actor, broken-off/ASR uncertainty, DT-243, DT-244.

## 3. Concerns to settle before I generate
1. **Circularity.** The same assistant writes and reviews; the spec is frozen first and labels never come from sentences, which removes label drift but not my blind spots. Every report must say "AI-generated and AI-reviewed, development data".
2. **Hedged speech ("I think I should explain…", "maybe I could ask…").** No approved rule says whether it is performed. I will include uncertainty only as hedging on *content* of a performed act ("it should take about an hour, I think") or as a clear intention/hypothetical; hedged-performance borderlines are excluded and logged.
3. **Multi-intent gold** is per act (one credit per behaviour); it is derived the same way, one gold record per act in the spec, but the engine/reader may output fewer acts, so G is reported as per-act recall.
4. **Requests vs claims of completion.** Requests (and tasks/escalations) are covered; claims of earlier completion are DT-244 and absent, so the "claim" side of that contrast is currently empty by design.
5. **No-credit majority (65%)** will make accuracy-style summaries look flattering; reports must give per-class and per-family numbers, and false-credit counts separately.
6. **Dev becomes contaminated** if we tune against it heavily; the held-out set must be built afterwards from new specs with P5/P7 and the reserved topic groups.
7. **Eligibility is a spec fact** (window, pending question, read-back); items in B rely on `mapping.json` windows and ADR state rules, so any later change to those rules invalidates the corresponding gold.
8. **Cost**: writing 500 items directly is in-session tokens only (no cloud calls); about 10–14 batches.

## 4. Next steps after approval
(a) Build the frozen spec file and manifest with code (including the contrast-set plan); (b) write utterances in batches with running diversity checks; (c) my review pass, discard log, deterministic validators, manifests; (d) report with the AI-generated/AI-reviewed label; (e) register the work in the plan files and CI, no push.
