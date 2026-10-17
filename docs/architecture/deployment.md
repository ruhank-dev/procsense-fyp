# Deployment topology, remote option and caching (p1-ai-infrastructure-s03)

Written 17 October 2026 from what is built and measured. Hardware: desktop with a GTX 1660 SUPER (6 GB), laptop with an RTX 4050 (6 GB, not yet set up, DT-188). The decision on where the gateway and the models run is ADR-0011; the choice of models is ADR-0010.

## Modes

| Mode | Where things run | When | Config |
|---|---|---|---|
| Split | Models and the gateway on the desktop; Unity and the headset client on the laptop or the Quest; the two talk over the local network | Default plan: the desktop has the GPU the models need | `OLLAMA_URL` points at the desktop (`http://<desktop-lan-address>:11434`), the service runs next to it |
| Single machine | Everything on one machine; the 4B model for live work, the 9B only for offline batch runs | Demos, one-person development, or the laptop alone | `OLLAMA_URL=http://127.0.0.1:11434` |
| No model host | The hosted service (Render, Supabase) with no model reachable | The internet service for sessions, reports and the dashboard | `OLLAMA_URL=` (empty): every model call answers with the fallback signal |
| Remote batch (option) | A rented or university GPU runs offline batch jobs (composition, knowledge-base extraction, evaluation) | Only for jobs that carry **no personal data** and are not part of a session | the batch tools take `--url`; nothing in `server/` points at it |

A session never depends on the model host: every capability has a deterministic fallback (below), and the pool of pre-generated scenarios means a session can start with no generation at all.

## Fallback matrix

| Capability | Model work | Fallback when the model is down, slow, busy or wrong | Tested by |
|---|---|---|---|
| Fresh scenario | Plan proposal (9B, batch), patient wording (4B), critic (9B) | The deterministic skeleton (solver, packs, tagged pictures) with the authored wording; or a pooled package; or a library case | `tools/ci/test_llm.py` (down, wrong answers), `test_generation_package.py` (pool) |
| Patient words in generated cases | 4B rewrite of up to six lines | The authored lines stay | `test_llm.py` Narrative, InPipeline |
| Patient dialogue (free questions) | 4B phrases what the Core decided to disclose | Menu of questions and the authored lines | planned: p1-ai-language-s04 |
| Staff and environment lines | 4B | Pre-rendered voice lines of the intent | planned: p1-ai-language-s05 |
| Debrief wording | 4B rewrite of the template | The template debrief (always produced first) | planned: p1-ai-language-s08 |
| Difficulty explanation | None (rules) | Not needed | `ProcSense.Core` difficulty tests |
| Performance hypotheses | 9B, offline | None shown | `test_personalisation_policy.py` |
| Speech recognition | faster-whisper (desktop) | Menus and typed input | planned: p1-voice-input |
| Voice | Piper, rendered offline | The text is shown | `tools/voice` |

## Failure behaviour (tested)

* Host unreachable, timeout, missing model, bad JSON, empty answer: a reply with an error code, never an exception (`server/llm/client.py`; `test_llm.py`).
* Host busy: one model call at a time; a request waits up to `AI_QUEUE_WAIT_SECONDS` (5 s) and then gets the fallback signal; measured 4 callers x 5 calls: 16 answered, 4 fallback signals, queue wait p95 5.0 s (`tools/ai/load_gateway.py`, ADR-0011).
* Answer rejected by the schema or the validator: one retry with the reasons, then the fallback signal.
* LAN latency spikes: the timeouts above and the pool; a session is never held for a model.

## Caching

* Generated packages are cached as files with their SHA-256 (`server/generation/pool.py`): byte-identical for the same seed, checked against an index before use, refused when the content changed.
* Model answers are not cached across trainees. The package stores the model's accepted output, so replaying a package never calls a model.
* Pre-rendered voice lines are files (`build/voice`, `tools/voice`).

## Not decided here

The laptop as the live host (needs its own benchmark, DT-188); a cloud model host (DT-122); the Unity side of the switches (`docs/architecture/unity_ai_hooks.md`).
