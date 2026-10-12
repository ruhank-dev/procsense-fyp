# P1 — Extensions on top of the core simulator

Built after the P0 parts they depend on exist. Nothing here is optional scope — it is the next layer.

| Phase | Adds |
|---|---|
| p1-theatre-extension | Step 15 WHO Sign Out, intra-operative training events, recovery handover, their scenes + equipment (specimen container, recovery bay) |
| p1-case-library-extended | Remaining 10 variants (S1-I, K, L, M; S2-E, H, I, J, K, L) + 6 stacked cases (STK-S1-A/B/C, STK-S2-A/C/D) |
| p1-backend | Optional local server: session sync, reports, AI gateway, audit log |
| p1-ai-language | Grounding validator, LLM patient/staff dialogue, communication classifier, AI-worded debrief |
| p1-voice-input | Speech recognition, voice orders with read-back, live TTS |
| p1-scenario-composer-difficulty | Scenario Composer solver, AI Stage A case proposals, difficulty explanations |
| p1-pulse-physiology | Pulse Physiology Engine as a higher-fidelity option |
| p1-evaluation | AI eval suite, learning-effect study, P1 golden runs |
| p1-dataset-analysis | (added 12 Oct 2026, planning only) Profile every dataset and content table, build generator parameter packs, tag the image library, build evaluation sets |
| p1-live-scenario-generation | (added 12 Oct 2026, planning only) Fresh validated practice scenario per session: the 21-stage pipeline LG-01 to LG-21 with the AI scenario plan composer (`docs/architecture/live_generation_spec.md`) |
| p1-ai-infrastructure | (added 12 Oct 2026, planning only) Inference hosts, dual-GPU benchmark with Unity running, deployment topology, labelling programme, Unity AI hook specification |
| p1-personalisation | (added 12 Oct 2026, planning only) Session evidence, Bayesian learner model, targeting policy, LLM performance interpreter, why-this-case explanations |

```
P0 ─┬─ p1-theatre-extension
    ├─ p1-case-library-extended ─ p1-scenario-composer-difficulty
    ├─ p1-backend ─┬─ p1-ai-language ──┘
    │              └─ p1-voice-input
    ├─ p1-pulse-physiology
    └─ p1-evaluation (alongside)

p1-dataset-analysis ─┐
p1-scenario-composer-difficulty (blocks, solver) ─┴─ p1-live-scenario-generation (LLM parts plug in from p1-ai-language and p1-backend when ready)
```
