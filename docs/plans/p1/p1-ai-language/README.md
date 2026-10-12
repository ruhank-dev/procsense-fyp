# p1-ai-language

**Tier:** P1 · **Builds on:** p1-backend, p0-scoring-debrief, p0-physiology-events-behaviour · **Feeds:** p1-voice-input, p1-scenario-composer-difficulty

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
| Model | Benchmark Qwen3-4B vs 8B (quantised) + a medical model | Pick now | 6 GB VRAM makes it empirical | Better small models appear |
| Authority | AI reads DTOs, outputs JSON-schema text/intents; validator gates all | AI writes state | Master Plan §27 | — |
| Fallback | Pre-rendered lines, templates, menu | None | Works with server off | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-AI-1 | Best model for 6 GB (quality, latency, grounding) | Benchmark on dialogue eval set | Latency ≤ 2.5 s fallback threshold + 0 leaks |
| EXP-AI-2 | Hidden-fact leak rate | 100 adversarial questions per case | 0 / 100 |
| EXP-AI-3 | Classifier quality | Held-out F1 on human-labelled set | Threshold fixed before testing |
| EXP-AI-4 | Debrief mutation rejection | Mutate facts/IDs/numbers | 100% rejected |

## Planning update (12 Oct 2026)
Model choices, latency targets and fallbacks for every step here are in `docs/architecture/model_technology_register.md` (MC-01 to MC-04, MC-12): Qwen3 4B served by Ollama with JSON-schema output as the default candidate (Apache-2.0), to be confirmed by the s02 benchmark on the 6 GB GPU (DT-112). The LLM also serves the live-generation narrative stage (LG-06) and the advisory quality judge (LG-11). Feature ids C24 to C30, C36, C37, D38, D46, D47, E53, E54, E55, E57 in `docs/architecture/feature_registry.md`. Planning only.

## Exit
Master Plan T-AI-01…13 pass; every AI call audited; full run works with server off.

## Who does what
Developer: all. Team: second annotator for labels, quality reading.

## Sources
Master Plan §26–41; Implementation Guide B-R1–B-R5, B-R8, B-R9, B-M5, B-M6, B-M10.
