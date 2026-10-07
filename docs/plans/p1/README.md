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

```
P0 ─┬─ p1-theatre-extension
    ├─ p1-case-library-extended ─ p1-scenario-composer-difficulty
    ├─ p1-backend ─┬─ p1-ai-language ──┘
    │              └─ p1-voice-input
    ├─ p1-pulse-physiology
    └─ p1-evaluation (alongside)
```
