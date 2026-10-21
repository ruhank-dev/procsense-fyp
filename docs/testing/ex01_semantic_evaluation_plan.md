# EX-01 phase two: independent evaluation of the semantic reader (plan, updated 21 October 2026)

> **PAUSED (owner decision of 21 Oct 2026):** the manual author/annotator workflow below is on hold. The owner prefers directly written, AI-generated synthetic development data (`docs/research/direct_synthetic_dev_set_design.md`; the multi-model factory was abandoned) and to spend human effort on the final Meta Quest test. The tooling, packages and guides are kept unchanged for possible later use; nothing here was deleted.

**Status: tooling, instructions and author packages are ready; NO sentence has been written and NO label exists.** Nothing in this document or in the kit is data. The first run against the set has not been made and will not be made until the set and the evaluation configuration are frozen, the owner has read the dataset and reviewer summary and the owner approves the single scored run.

## Owner decisions recorded (21 October 2026)
* **Size: 300 items**, balanced and risk-weighted across the families (section 4); the safety-sensitive and historically ambiguous families carry extra weight; the set is not built around individual dev, test or test2 failures. Existing benchmark sentences are never shown to, copied or mechanically paraphrased by the authors; the repository leak check (exact and near-copy) stays in force.
* **ASR: real-ASR coverage (family F17) is 0 in this evaluation** and no spelling mistake or synthetic ASR corruption is allowed anywhere (see "ASR separation").
* **Reporting:** the engineering targets are kept and every figure is reported as a point estimate AND a 95 percent Wilson interval (see "Acceptance reporting").
* **People:** none assigned; the independence design is the owner's choice after `docs/testing/ex01_role_designs.md`.

## 1. Why a new set
Everything the repository holds (`tests/action_bench_v1`, the challenge sets, the synthetic v2 sets, the safety panel) is synthetic and was written by one author family; dev, test and test2 have also shaped the architecture, the prompts, the guards and the verifier design, so more tuning against them cannot give convincing evidence.

## 2. Development findings that stay on record (development evidence, not validation)
* Current valid configuration: policy `hard`, both switches OFF. Dev recall after the engine-state context: **95/140** (estimated from a 24-row live re-check), precision 95/96; the shipped path alone also credits 95/140.
* The verifier-gated lift (**114/140**) is hypothetical only. The corroborated cap (**113/140** on replay) is research-only and **failed** the bounded live safety panel (5 of 12 critical-content sentences offered).
* The isolated verifier detected 34/34 critical sentences and cleared 35/42 legitimate ones on its non-held-out experiment (its prompt was written after the panel failures were known); it stays an experimental component and lifts no cap.
* Zero observed unsafe offers on synthetic sentences is a count, not proof of safety.

## 3. ASR separation (explicit)
EX-01 phase two measures **semantic interpretation from clean written text, independently of speech-recognition quality**. Every item is natural written text; family F17 (real speech-recognition wording) has quota 0 and the kit REFUSES any F17 item and any non-empty `asr_source`; deliberate typos, swapped letters or glued words are forbidden. How the system behaves on real recognised speech is a **separate later phase** that needs actual recorded speech and its transcripts (audio and Quest work, DC-08, EX-18, `docs/testing/asr_safety_evaluation.md`). A result here says nothing about recognition errors, and a good result here must not be quoted as a speech result.

## 4. What the authors write (300 items, `tools/bench/ex01_design.json`)
| family | items | | family | items |
|---|---|---|---|---|
| F01 ordinary paraphrase | 24 | | F10 acknowledging emotion against reassuring | 14 |
| F02 indirect natural speech | 22 | | F11 explaining an action against the procedure | 14 |
| F03 question against assertion | 16 | | F12 answers that need the patient's question | 16 |
| F04 request against claim of completion (safety) | 18 | | F13 read-backs that need the colleague's order | 16 |
| F05 hypothetical (safety) | 16 | | F14 critical-topic discussion (safety, extra weight) | 32 |
| F06 reported speech (safety) | 16 | | F15 adversarial or misleading wording (safety) | 24 |
| F07 negation (safety) | 16 | | F16 valid speech outside the behaviour window | 14 |
| F08 actor and recipient (safety) | 16 | | F17 real ASR wording | **0** |
| F09 escalation, recommendation and handover wording (safety, historically ambiguous, extra weight) | 26 | | **total** | **300** |
The safety-sensitive families hold 164 of the 300. Use only the approved definitions (`content/assessment/taxonomy.json`) and the scenario rules (`content/assessment/mapping.json`); invent no clinical requirement. Where the definitions cannot settle one label, the annotator marks `definition_insufficient`: the item goes to `specification_issues.csv` for clinical or specification review and is **not** a classifier failure.

## 5. Acceptance reporting (point estimate AND interval, two different claims)
Targets (unchanged): legitimate recall >= 90 percent, credited precision >= 95 percent, 0 observed critical false credits, 0 observed adversarial false credits, schema compliance >= 99.5 percent, critical actions never credited from speech alone.
* The report states for each target (1) whether the **observed point estimate** meets the engineering target and (2) the **95 percent Wilson interval** and what uncertainty remains. Whether the interval's lower bound also clears the target is shown as a separate, differently named fact (`interval_lower_bound_at_or_above_target`): it is a stricter statistical claim and is NOT what "meets the target" means here.
* A zero count (critical or adversarial false credits, critical acts credited) is reported as an observed count with a denominator and an upper bound (exact two-sided 95 percent, for example 0 of 300 still allows about 1.2 percent), never as proof of zero underlying risk.
* Arithmetic for planning (Wilson 95 percent): 126/140 (90 percent) has a lower bound of 0.839; 133/140 (95 percent) 0.900; with 125 or more in-window positives a system must score about 95 percent for the interval's lower bound to reach 0.90. The 300-item set will hold roughly 120 to 150 in-window legitimate positives (the rest are non-performed, critical or out-of-window items), so expect a recall interval about plus or minus 5 to 6 points wide.

## 6. Workflow (nothing is exposed before the freeze)
authoring -> blind annotation A/B -> disagreement identification -> adjudication -> definition-insufficient flagging/removal -> coverage verification -> freeze -> owner review -> ONE independent scored run.
Commands (`uv run --project tools tools/bench/ex01_kit.py ...`, run by the owner or a builder, never by an author or annotator):
1. `author-packages --authors N --out DIR` writes one folder per author with the brief, personal instructions with the author's quota, and a pre-structured `author_template.csv` (id, author code, family; no sentence). The committed copy for two authors is `docs/testing/ex01_author_packages/`.
2. Authors fill their file alone; `combine --files ... --out authors.csv` joins them; `check-authors --file authors.csv` checks columns, quotas, steps, turns, flags, F17 and ASR rules, duplicates and the leak check (exact and near-copy; the repository sentence is never shown).
3. `package --authors authors.csv --out DIR` writes the two blind annotator files (opaque ids, own order, scene and sentence only) and the owner-only `id_map.json`.
4. Annotators label ALL items alone (`docs/testing/ex01_annotator_guide.md`); `check-annotator` validates each file.
5. `agree` identifies the disagreements and the items either annotator flagged; the adjudicator labels only those.
6. `merge` writes the scored set, the excluded items (ambiguous, definition insufficient, awaiting), every disagreement and the specification issues; nothing is forced or invented.
7. `summary --dir DIR --roles roles_record.json` writes the **dataset and reviewer summary** for the owner (items; counts by family and objective; legitimate, critical, adversarial and out-of-window composition; disagreement number and rate; adjudication outcomes; definition-insufficient cases; leak-check result; roles; the independence checklist). **STOP HERE: the owner reviews it.**
8. Only after the owner's explicit approval: `freeze --dir DIR --roles roles_record.json` (refuses unless roles, coverage and adjudication are complete) and `verify`; then `ex01_eval.py config-freeze --model M` and ONE scored run `ex01_eval.py run --live M --max-calls N --max-tokens T` (free plan). A repeat is `--development` and marked not independent. Guards are not redesigned from the first run's results.

## 7. What the run reports (`reports/ai/ex01-semantic-eval.json`)
Four layers on the same items: (1) reader understanding before any safety gate; (2) final credited output after every gate (shipped checks, critical-mass cap with policy hard, thresholds, window, the engine's evidence store); (3) the shipped-path baseline; (4) the experimental verifier as a classifier of critical content, lifting nothing. Each: recall, precision, confusion by objective, critical and adversarial false credits, abstentions, menus, clarification opportunities, schema compliance (first attempt and final), latency, tokens, results by family and the component behind each miss, with Wilson intervals; an `engineering_targets` block with the point-estimate test and the interval kept apart; and an attribution that splits every in-window legitimate item into understood and credited, **understood but rejected downstream** (with the component), not understood by the reader, or credited although not understood. Semantically valid speech outside the behaviour window is reported separately and must NOT be credited.

## 8. What it can and cannot show
Can: how the reader and the gates behave on wording written by people who did not build them, by family, with honest intervals. Cannot: how trainees speak (the authors are not trainees), anything about speech recognition, the microphone chain or the headset, or clinical validity. Safety remains a design property enforced by the deterministic engine; an observed zero is never proof.

## 9. Open decisions for the owner
1. The independence design and the real people (`docs/testing/ex01_role_designs.md`): strict five (S) or four (A, needs the per-item role tooling), and whether the owner is declared a builder.
2. Whether a clinician educator can adjudicate.
3. After the dataset and reviewer summary: approval of the freeze and of the single scored run (free plan, a call and token ceiling).
