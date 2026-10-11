# AI in ProcSense: what exists, what is missing and what is planned

Audit of 12 October 2026. It answers one question for every AI idea in the original plans: **is it built, missing, deferred or only planned, and is the thing that is built really AI or just rules?** Nothing here claims more than the code and tests show. The plans it was read from are `docs/plans/p1/`, `docs/plans/p2/`, the Master Plan (AI sections 81 to 88) and the Implementation Guide (B-R2, B-R5, B-R8 to B-R10, B-M3, B-M5, B-M6, B-M7).

## 1. The short answers

| Question | Answer |
|---|---|
| Does P0 contain any trained model or LLM? | **No.** P0 is deterministic rules and tables only. Nothing in P0 is called AI, and no rule is dressed up as a model. |
| Where does AI start? | **P1** (milestones M10 to M12): a locally served language model (Ollama on the laptop GPU) for patient and staff lines, a communication classifier and a debrief rewrite; speech recognition on the desktop GPU; a FastAPI backend. |
| Where are models *trained*? | **Only in P2** (`p2-ai-extensions-s03`): a small classifier trained on the human-labelled set. The P1 classifier, dialogue and debrief use a served model with prompts, **not** training. Speech recognition is an off-the-shelf model, not trained here. |
| Who decides what is correct? | **Always the rules.** An AI only (a) turns a trainee's words into a structured act for the evidence mapper, (b) voices a line the rules have already decided, or (c) rewrites the template debrief from facts. It never sets state, never credits a critical item and never reaches the trainee unchecked (ADR-0005). |
| What is built today? | The whole **deterministic foundation** that the AI stages stand on, plus the **safety gate** that makes AI output safe to show. See section 3. |
| What is blocked? | Everything that needs a model, a GPU, human labellers, consented recordings or the backend. See section 5. Nothing was faked to look finished. |

## 2. Every AI idea, one row each

Status words: **Built** (code and tests exist), **Built, rules only** (works, but it is rules or a rating, not a model, and is labelled so), **Planned** (a step file exists, nothing built), **Blocked** (cannot be built without something named in section 5).

| Idea from the plans | Plan step | Stage | Status | What exists | What is missing |
|---|---|---|---|---|---|
| Rules, scoring and evidence that AI evidence plugs into | `p0-scoring-debrief` s01 to s05 | P0 | **Built, rules only** | 22 assessed behaviours, a rubric, `Scorer`, `EvidenceStore` (critical guard, thresholds, context check), outcome and fail gate, `DebriefFacts`, template debrief, `report` command | Owner approval of the steps; clinician review of weights (Q8) |
| Analysis of trainee performance | same | P0 | **Built, rules only** | Per-behaviour credits and penalties, lateness, a timeline with log references, one JSON and one HTML report | Nothing in P0; richer analysis is the AI debrief (below) |
| Difficulty a trainee has (what they find hard) | `p0-case-library` s06 | P0 | **Built, rules only** | Elo-style rating per behaviour (K 16, 3 sessions before adapting) from the scorer's credits and penalties | A clinician's review of the dimension mapping (Q9) |
| Progressively harder, personalised scenarios (selection) | `p0-case-library` s06 | P0 | **Built, rules only** | 8 difficulty dimensions, case tags, `Selector` (approved pool only, practice only, fixed order in assessment), `next-case` and `record-session` commands, pseudonym history | It chooses among the 53 existing cases; making new ones is the composer |
| Beginner versus advanced behaviour | `p0-case-library` s06 | P0 | **Built** | `SessionOptions.Level`: the nurse challenges (beginner) or the consequence plays out (advanced); saved in the log header | none |
| Validation of any case, hand-made or AI-drafted | `p0-case-library` s04, s05 | P0 | **Built** | Stage B: 11 ordered checks, 12 contradiction types, HTML report per case, 60 of 60 deliberately broken cases rejected, an AI draft can never reach an approved status | Checks 9 and 10 need a golden run per case (17 P0 cases have one) |
| Unexpected events | `p0-physiology-events-behaviour` s08, s09 | P0 | **Built** | `EventDirector` (seeded draw in practice, fixed schedule in assessment), 7 approved events, what-if coverage and bindings | 7 what-ifs have no state to run on (DT-087); events are approved for training only (Q7) |
| **AI-generated clinical scenarios and problems** | `p1-scenario-composer-difficulty` s01 to s05 | P1 | **Planned** | Only the gate they must pass (Stage B) and the context they need (case tags, discovery rules) | Block library with requires/excludes (clinician-reviewed), the deterministic solver, the LLM proposal step, clinician approval of every composed case. The solver is **not AI** and comes first; the LLM only proposes block combinations and a narrative |
| **Grounding validator** (no AI line reaches a trainee unchecked) | `p1-ai-language` s01 | P1 | **Built** | `server/validation/validator.py` and its C# twin, 44 shared vectors, 100+ disclosure probes, every lexicon drug rejected when off-card, debrief mutation tests | The FastAPI server around it (`p1-backend-s01`) |
| **Read-only context for AI characters** and who speaks next | `p1-ai-language` s03 | P1 | **Built** | `ContextBuilder` per role, hidden facts never in what the model is sent, 900-token budget, `ReactionQueue` (priority, debounce, one spontaneous remark per 8 s) | The Unity assembly split (`AIClient` asmdef) is Unity work |
| LLM serving benchmark | `p1-ai-language` s02 | P1 | **Blocked** | none | The laptop GPU, Ollama, the candidate models and their licences (MedGemma terms); a benchmark run |
| **Patient dialogue** (free questions answered in the patient's voice) | `p1-ai-language` s04 | P1 | **Planned, blocked** | The rules that decide disclosure (history step, behaviour gate), the context, the validator, the approved quoted answers | A served model; the persona tags in the case files (DT-085); the adversarial question set |
| **Staff AI** (nurse, anaesthetist, porter, lab) | `p1-ai-language` s05 | P1 | **Planned, blocked** | Approved intents per role (`content/ai/intents.json`), the existing fixed lines | A served model; pre-rendered fallback lines per intent; voices |
| **Communication taxonomy and labelled set** | `p1-ai-language` s06 | P1 | **Blocked** | The 22 behaviours and their AI-reportable acts (`content/assessment/mapping.json`) are the start of the taxonomy | At least 300 utterances labelled by two people (kappa 0.6 or more), consented role-play recordings, a clinician's list of critical acts |
| **Communication classification** | `p1-ai-language` s07 | P1 | **Blocked** | The evidence mapper already records an AI act but never credits a critical act, a low-confidence act or a wrong-stage act | The labelled set, a served model, threshold calibration. Below macro-F1 0.75 it stays switched off |
| **Speech recognition** | `p1-voice-input` s01 | P1 | **Blocked** | none | The desktop GPU, faster-whisper, a vocabulary built from content, accented-speech tests |
| Voice orders and live speech output | `p1-voice-input` s03, s04 | P1 | **Planned, blocked** | The text verbs and the lines already exist | ASR, a voice client in Unity, a TTS voice |
| **AI debriefing** | `p1-ai-language` s08 | P1 | **Planned, blocked** | The template debrief (always shown first), `DebriefFacts`, the validator that rejects a dropped fact, a changed number, a flipped verdict or an invented event | A served model; 50-session mutation run |
| **Adaptive feedback** | `p0-scoring-debrief` s04 and `p1-ai-language` s08 | P0 / P1 | Template feedback **built, rules only**; AI wording **Planned** | The template sentences are chosen by item, never invented | The AI rewrite |
| AI evaluation suite and the switch-off gate | `p1-ai-language` s09, `p1-evaluation` | P1 | **Planned** | The pieces it will run (validator tests, mutation tests) | The harness, the weekly schedule, `ai_features.yaml` |
| Backend (sessions, upload, reports, audit, AI gateway) | `p1-backend` s01 to s06 | P1 | **Planned** | The report format the backend reuses (`debrief_facts.v1`) | FastAPI, storage, a login decision |
| On-device speech, retrieval, Urdu speech, medical LLM benchmark | `p2-ai-extensions` s01, s02, s04, s05 | P2 | **Planned** | none | P1 first |
| **A trained model** | `p2-ai-extensions` s03 | P2 | **Planned, blocked** | none | The human-labelled set from P1 s06; training code; the frozen test split |
| PULSE physiology engine | `p1-pulse-physiology` | P1 | **Planned** (not AI) | LitePhysio is the P0 engine | The spike and the parity experiment |

## 3. What this audit built (all with tests; none of it is a model)

| Piece | Where | Tests |
|---|---|---|
| Scoring, evidence mapper, outcome, debrief facts, report | `src/ProcSense.Core/Scoring/`, `content/scoring`, `content/assessment`, `content/debrief` | `ScoringTests`, `tools/ci/check_report.py` |
| Adaptive difficulty (rating and lookup) | `src/ProcSense.Core/Difficulty/`, `content/difficulty` | `DifficultyTests` |
| Stage B validator and mutation tests | `tools/composer/`, `content/contradictions.json` | `test_composer.py` (60 of 60 mutations rejected) |
| Grounding validator, Python and C# | `server/validation/`, `src/ProcSense.Core/AIContext/Validator.cs`, `content/ai/lexicon.json`, `tests/vectors/grounding_vectors.json` | `test_validator.py`, `GroundingValidatorTests` |
| Per-character context, reaction queue | `src/ProcSense.Core/AIContext/ContextBuilder.cs`, `content/ai/intents.json` | `ContextBuilderTests` |
| Event Director and what-if coverage | `src/ProcSense.Core/Events/`, `content/events` | `EventDirectorTests`, `WhatIfBindingTests` |
| Seventeen correct-path runs of the P0 cases | `tests/golden/*_correct.*`, `tools/golden/p0_scripts.py` | `P0CaseTests` |

## 4. How to read "AI" in this project

1. **Rules** decide correctness. They are written down, sourced and tested. Most of the engine is this.
2. **A rating or a lookup** (difficulty selection, case tags) is arithmetic over the scorer's output. It adapts, but it is not a model and is labelled "a rating and a lookup, not a trained model" wherever it appears.
3. **A served language model with a prompt** (P1 dialogue, staff lines, classifier, debrief rewrite, composer proposals) interprets or phrases. It is not trained here. Its output passes the grounding validator or it is replaced by a template or a pre-rendered line.
4. **A trained model** exists nowhere yet. The first one is the P2 small classifier, trained on a human-labelled set, with a frozen test split and the critical-act guard.

Known limits of the checks that are built:
* The disclosure check is lexical: it catches a hidden fact told in its own words or numbers, not a paraphrase. The primary protection is that a model is never given a hidden fact (the context does not contain it); the validator is the second line. A shared vector documents the limit.
* The lexicon lists are curated (SR-2870) and must grow when a model slips a synonym through.
* The classifier thresholds, the 0.8 default and every scoring weight are design values awaiting the clinician (Q8, Q9, Q13).

## 5. What blocks the rest (exact needs)

| Need | Unblocks | Who |
|---|---|---|
| The laptop with the 6 GB GPU, Ollama installed, the candidate models downloaded (MedGemma terms accepted) | `p1-ai-language` s02, s04, s05, s07, s08, composer s03 | Owner |
| The desktop with the GTX 1660 Super and faster-whisper | `p1-voice-input` s01 to s04 | Owner |
| Two people to label at least 300 utterances, consented role-play recordings, a clinician's list of critical acts | `p1-ai-language` s06, s07, and the P2 trained classifier | Owner, a clinician |
| A decision on the backend hosting and login | `p1-backend` s01 to s06 | Owner |
| A clinician to review the 21 block types and their requires/excludes | `p1-scenario-composer-difficulty` s01 to s05 | A clinician |
| The persona tags in the case files (each case edit resets its approval) | patient dialogue style (DT-085) | Case authors |

When any of these arrives, the next step is already planned and the gate it must pass already exists.

## 6. Order the stages unlock in

`P0 scoring and evidence` (built) → `grounding validator and context` (built) → `backend skeleton (s01)` → `LLM serving benchmark (s02)` → `dialogue and staff AI (s04, s05)` and `debrief rewrite (s08)` → `labelled set (s06)` → `classifier (s07)` → `evaluation harness and switch-off gate (s09)` → `P2 small classifier (the first trained model)`.

Decision record: `docs/decisions/ADR-0005-ai-boundaries-and-staging.md`. Open items: `docs/deferred-tasks.md` DT-096 to DT-100. Clinician questions: `docs/research/clinician_signoff.md` Q8, Q9, Q13.
