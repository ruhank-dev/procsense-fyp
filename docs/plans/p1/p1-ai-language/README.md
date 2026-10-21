# p1-ai-language

**Tier:** P1 · **Builds on:** p1-backend, p0-scoring-debrief, p0-physiology-events-behaviour · **Feeds:** p1-voice-input, p1-scenario-composer-difficulty

> **Plan update, 22 Oct 2026 (seventh, s22 approved, ADR-0025):** step s22 is closed: the contract fix for long evidence (DT-247) and a fresh partial-credit set completed the final acceptance, the `speech_evaluation` gate is ON and the architecture is final. The retired steps of this phase (s06, s07, s11, s12, s14 to s19) are closed as superseded; the real Quest and ASR validation is DC-10 (Sannan).
>
> **Plan update, 21 Oct 2026 (sixth, s22, ADR-0025):** step s22 replaces the speech architecture: ASR, Gemma semantic evaluation (`server/evaluate`, `gemma4:31b` on Ollama Cloud, free plan), the engine's authoritative state and evidence checks and a deterministic graded score. The classifier, two-head agreement, critical-mass cap, cues, assertion layer, interpretation service and the earlier semantic reader (the work of s06, s07, s11, s12, s14 to s19) were removed with their tests and harnesses; their steps stay ticked as history and their findings live in `docs/research/gemma_evaluator_results.md` and the open items DT-241 to DT-245. Gate `speech_evaluation` OFF; synthetic development evidence only.
>
> **Plan update, 21 Oct 2026 (fifth, s19):** step s19 analysed where the semantic reader loses legitimate rows (mostly the critical-mass cap) and implemented the approved corroborated cap behind a setting that stays OFF: the bounded live panel check failed (5 of 12 critical-content sentences offered).
>
> **Plan update, 20 Oct 2026 (fourth, s18, ADR-0024):** step s18 adds an Ollama Cloud semantic reader behind strict structured-output validation and the existing deterministic checks, off by default; the integrated pipeline is not better than the shipped path on recall, the model comparison and live run await approval, privacy and independent validation are open.
>
> **Plan update, 20 Oct 2026 (third, s17):** step s17 prepares the blind human review of the unsafe-speech sheet (kit, protocol, adjudication), audits the sets and the ten remaining final-split failures without changing the frozen candidate, designs the audio-to-transcript evaluation and extends the mutation tests. Nothing was reviewed, recorded or run on a headset; gate OFF.
>
> **Plan update, 20 Oct 2026 (second, ADR-0023):** step s16 audits the speech-safety layers, measures unsafe speech on metamorphic sets with provenance, widens the assertion layer, makes the engine check the waiting question and read-back itself and mutation-tests every critical check (`docs/research/unsafe_speech_safety.md`). Synthetic, no human review; gate OFF; EX-01 part B and the headset checks outstanding.
>
> **Plan update, 20 Oct 2026:** step s15 diagnoses and improves legitimate speech recall: the engine's evidence thresholds follow the classifier's calibrated thresholds, the critical-mass cap moved from 0.03 to 0.05 on evidence, and the assertion layer's holes were closed (`docs/research/recall_diagnosis.md`). Gate OFF; independent evaluation (EX-01) outstanding.
>
> **Plan update, 19 Oct 2026 (ADR-0022):** steps s10 to s14 add the context-aware interpretation of trainee speech: the engine builds the context and decides eligibility (s10), the `/interpret` service validates the classifier gate's candidates deterministically (s11), clarification and menu fallback with the Unity half (s12), dialogue safety with voice in front (s13), the context-conditioned benchmark and the `speech_interpretation` gate, OFF (s14). Qwen 4B stays out of the live path (rubric evaluator NO-GO, `docs/research/rubric_evaluator.md`).
>
> **Progress, 12 Oct 2026:** s01 (the grounding validator, Python and C# twin with shared vectors) and s03 (the per-character read-only context and the reaction queue) are built and tested and approved by the owner (12 Oct 2026). s02 and s04 to s09 need a served model, human labellers or the backend and are blocked or pending (DT-096, DT-097). The full audit is `docs/ai-roadmap-status.md`; the rule is ADR-0005.

## Goal
Controlled AI around the deterministic core: free-text patient/staff dialogue that never leaks hidden facts or invents medicine,
a communication classifier producing evidence, and AI-worded debriefs that can't invent mistakes. Built validator-first.

## Scope
- **In (in this order):** grounding/validation layer + audit (before any model output reaches a trainee) → model serving + benchmark on
  6 GB GPUs → read-only state DTOs → patient dialogue → staff AI (approved intents only) → communication classifier (+ labelled data) → AI debrief rewrite + validator → AI eval suite.
- **Out:** scoring decisions (deterministic only).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Where models run | Local GPU over LAN (Ollama or llama.cpp server) | Cloud API (e.g. The developer API); on-Quest | Offline, private, no per-call cost | EXP-AI-1 quality too low → cloud API with same validator |
| Model | Benchmark Qwen3.5 4B vs 9B (Q4_K_M, ADR-0010) with Qwen3 4B as the baseline + a medical model | Pick now | 6 GB VRAM makes it empirical | Better small models appear |
| Authority | AI reads DTOs, outputs JSON-schema text/intents; validator gates all | AI writes state | Master Plan §27 | — |
| Fallback | Pre-rendered lines, templates, menu | None | Works with server off | — |

## Experiments
| ID | Question | Method | Success metric | Status |
|---|---|---|---|---|
| EXP-AI-1 | Best model for 6 GB (quality, latency, grounding) | Benchmark on dialogue eval set | Latency ≤ 2.5 s fallback threshold + 0 leaks | planned |
| EXP-AI-2 | Hidden-fact leak rate | 100 adversarial questions per case | 0 / 100 | planned |
| EXP-AI-3 | Classifier quality | Held-out F1 on human-labelled set | Threshold fixed before testing | planned |
| EXP-AI-4 | Debrief mutation rejection | Mutate facts/IDs/numbers | 100% rejected | planned |

## Planning update (12 Oct 2026)
Model choices, latency targets and fallbacks for every step here are in `docs/architecture/model_technology_register.md` (MC-01 to MC-04, MC-12): Qwen3.5 4B (`qwen3.5:4b-q4_K_M`, thinking off) served by Ollama with JSON-schema output for live work and Qwen3.5 9B (`qwen3.5:9b-q4_K_M`) for offline batch work (Apache-2.0; ADR-0010), to be confirmed by the s02 benchmark on the 6 GB GPU (DT-112, DT-187, DT-188). The LLM also serves the live-generation narrative stage (LG-06) and the advisory quality judge (LG-11). Feature ids C24 to C30, C36, C37, D38, D46, D47, E53, E54, E55, E57 in `docs/architecture/feature_registry.md`. Planning only.

## Exit
Master Plan T-AI-01…13 pass; every AI call audited; full run works with server off.

## Who does what
Developer: all. Team: second annotator for labels, quality reading.

## Sources
Master Plan §26–41; Implementation Guide B-R1–B-R5, B-R8, B-R9, B-M5, B-M6, B-M10.
