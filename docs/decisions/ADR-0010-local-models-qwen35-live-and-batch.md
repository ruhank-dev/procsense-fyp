# ADR-0010 — Local models: Qwen3.5 4B for live work, Qwen3.5 9B for offline batch work, rules decide

**Date:** 16 October 2026 · **Status:** Accepted (16 October 2026, owner; the quality of the choice is not yet confirmed, see Revisit) · **Related:** ADR-0005, ADR-0008, p1-ai-language-s02, p1-ai-infrastructure-s01, s02, DT-187 to DT-192

## Context
Nothing in the repository calls a language model yet. The plan named Qwen3 (4B default, 8B possible) with unmeasured speed. The hardware is a GTX 1660 SUPER (6 GB VRAM, 16 GB RAM) and an RTX 4050 laptop (6 GB). Independent evidence (Artificial Analysis, reasoning mode) puts Qwen3.5 4B at 27 and 9B at 32 on its index against 18 for Qwen3-4B-2507, and the owner's own experience is that Qwen2.5 and Qwen3 were poor and Qwen3.5 better. Both Qwen3.5 sizes answer about 80 percent of factual-knowledge questions wrongly (same source), so no model may state clinical facts that are not in its context. On 16 October 2026 the models were installed on the desktop and measured with the machine idle (`tools/ai/benchmark.py`).

## Decision
1. **Live work** (patient and staff dialogue, voice-command fallback, classifier, post-session rewrites): `qwen3.5:4b-q4_K_M`, thinking off, context 4096, kept loaded, on the desktop GPU, called at `127.0.0.1`.
2. **Offline batch work** (scenario composition, trap cases, knowledge-base extraction, advisory judging, pre-written hint variants and report explanations): `qwen3.5:9b-q4_K_M` on the desktop GPU with CPU offload, thinking on only where a benchmark shows it helps; never run during a session; output waits for the existing validators and human approval.
3. **Deterministic code decides** state, scoring, orders, numbers, images, difficulty and its explanations (ADR-0005). The two features that need no model: voice orders and read-back, and difficulty explanations.
4. **Qwen3 4B stays only as the comparison baseline** and is removed when the comparisons are done. No hosted model is required; one is an optional alternative if the 9B is not good enough for composition (DT-192).

## Alternatives
Qwen3.5 2B and 0.8B (clearly weaker on the index; 2B stays as the fallback if the live latency target is missed); Qwen3.5 9B for everything (measured about 5 s per reply and 43 percent on the CPU, so it fails the 3 s target); 27B, 35B-A3B, 122B (17 GB or more, beyond 6 GB VRAM and 16 GB RAM; not practical); gpt-oss-20b (below Qwen3.5 4B on the index); Gemma 4 E4B (6.6 to 9.5 GB in Ollama, does not fit); Phi-4-mini, Granite 4, Ministral 3 (no usable independent numbers found); a hosted model (breaks offline operation for live use).

## Consequences
Measured on the idle desktop: 4B 100 percent on the GPU, about 59 tokens per second, p95 1.15 s; 9B 57 percent on the GPU, about 12 tokens per second, about 5 s. Thinking mode is costly: a 150-word planning answer used a whole 1800-token budget in 162 s, so batch calls need large budgets. The patient needed an approved "cannot recall" intent (DT-189). The validator reads text only, so made-up denials get through (DT-190). Python on this Windows machine must call `127.0.0.1`, not `localhost` (+2 s per request, DT-191). Open: the laptop, speech recognition and Unity running together, thinking-on runs, debrief, composition and judge tests (DT-188), and the blind human rating (DT-187).

## Revisit if
The blind rating or the debrief test shows the 4B is not good enough for dialogue or debriefs; the 4B misses p95 under 3 s with speech recognition and Unity loaded; the 9B composition tests show it cannot produce cases that pass Stage B at a useful rate; a better small model appears; or the laptop cannot host the live model.
