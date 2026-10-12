# Architecture and integration plan

How the AI features and live scenario generation fit around the existing deterministic system. Planning only (12 October 2026): the planned parts below are not built. Canonical graph: `procsense_full_pipeline.json`. Decision record: ADR-0008.

## 1. The layers

| Layer | What it is | Language and place | State today |
|---|---|---|---|
| Core engine | Session, ISubsystem pipeline, SimClock (0.5 s steps), LitePhysio, events, discovery, scoring, evidence, debrief facts, replay | C# netstandard2.1, `src/ProcSense.Core` | Built and tested |
| Content | Case files, bands, critical results, imaging library, what-ifs, rubric, AI lexicon and intents, manifest | JSON and CSV in `content/`, built by `tools/content/*.py` with `--check` | Built and tested |
| Offline preparation | Dataset profiles, parameter packs, block library, retrieval index, media index, evaluation sets, prompts, model preparation | Python in `tools/data`, `tools/generation` (planned), `tools/composer` | Partly built (registers, extracts, Stage B) |
| Generation service | Live stages LG-01 to LG-14, the AI scenario plan composer, the scenario knowledge base, and the learner-profile steps LP-01 to LP-03 | Python in `server/generation` (planned), behind the FastAPI backend | Planned |
| AI gateway | The only path to a model: LLM, ASR, classifier, judge; logs prompt version and model digest; applies the grounding validator | Python in `server/` (planned `p1-backend-s06`); validator built in `server/validation` | Validator built; gateway planned |
| Model runtimes | Ollama (LLMs), faster-whisper (ASR), TTS | Local processes on the 6 GB GPU or CPU | Not installed |
| Front end | Unity VR app: bridge, action emitters, views | C# in `unity/Assets/_Project/Scripts` | Partly built; not changed by this plan |

## 2. Data flow for one generated practice session

1. Unity (or the CLI) sends a SessionRequest (LG-01). Without a backend, Unity takes the next package from the local pool instead.
2. The learner profile is loaded (LP-01) and turned into objectives (LP-03, LG-02). The generation service retrieves approved evidence and a knowledge-base subgraph (LG-03); the AI composer proposes a scenario plan and the solver completes and checks it (LG-04); a skeleton is built (LG-05).
3. The AI gateway asks the LLM for the narrative and profile wording (LG-06) and, in LG-04, for the scenario plan, always schema-constrained. On any failure the template narrative or the solver-only plan is used.
4. Numbers come from parameter packs (LG-07); images and ECG strips from the tagged library (LG-08).
5. Cross-modal rules, Stage B and a headless correct-path run check the case (LG-09, LG-10); quality and novelty are measured (LG-11, LG-13); defects are repaired within budget or the fallback is used (LG-12).
6. The package is written with provenance and a SHA-256 (LG-14) and loaded by Unity through the existing bridge and ContentGate (LG-15).
7. The Core simulates and scores exactly as for library cases (LG-16 to LG-18). Voice and free text go ASR → classifier → structured act; the evidence store's guards decide what can be credited.
8. The template debrief is produced first; the AI rewrite is shown only if the validator passes (LG-19). History is stored (LG-20) and the learner profile is updated from the session evidence (LP-02). Offline, the evaluation loop proposes changes for a reviewed release (LG-21).

## 3. Contracts (planned)

| Contract | Producer → consumer | Format | Notes |
|---|---|---|---|
| SessionRequest v1 | Unity/CLI → generation service | JSON schema `server/schemas/session_request.json` | Assessment mode refused |
| Objectives v1 | LG-02 → LG-03, LG-04 | JSON | Mirrors the C# rating rules; shared test vectors |
| ScenarioSkeleton | LG-05 → LG-06 to LG-08 | case-file JSON with typed slots | Validated against `case_file.schema.json` with slots allowed |
| NarrativeFill | LLM → LG-06 | JSON schema with enums of approved ids | Grounding validator |
| ScenarioPackage v1 | LG-14 → Unity, pool, history | JSON + SHA-256 | Review status GENERATED_PRACTICE_ONLY |
| DebriefFacts v1 | Core → debrief | `content/schema/debrief_facts.schema.json` (exists) | Extended with package provenance |
| API | backend | `POST /v1/scenarios:generate`, `GET /v1/scenarios/{id}`, `POST /v1/llm/generate`, `POST /v1/asr`, `POST /v1/classify`, `POST /v1/sessions` | All planned; versioned under `/v1` |

## 4. Integration rules

1. **The Core stays the authority.** It does not call the network or a model; it loads a validated package and runs it. Core rules (no wall clock, no `System.Random`, no Unity references) remain enforced by `tools/ci/repo_checks.py`.
2. **One path to a model.** Every model call goes through the AI gateway, which logs prompt version, model digest and latency, and applies the validator. Nothing else imports a model client.
3. **Offline-first.** The pool and the case library mean a session never waits on the backend or the GPU.
4. **Review status.** GENERATED_PRACTICE_ONLY extends the review-status gate of ADR-0006; it never loosens it: generated packages are never eligible for assessment.
5. **Feature switches.** Each AI feature has an entry in `ai_features.yaml` (planned, `p1-ai-language-s09`) with a threshold; below it the feature is off and its fallback runs.
6. **Hardware budget.** Two 6 GB GPUs (desktop GTX 1660 SUPER, laptop RTX 4050) plus 16 GB RAM on the desktop. One LLM is loaded at a time (4B class by default); ASR shares the GPU only if measured VRAM allows, otherwise it runs on CPU or the other machine. The expected topology is inference on the desktop and Unity on the laptop; single-machine mode uses small models and the pool. Measured in `p1-ai-infrastructure-s02` with and without Unity running; see `ai_scenario_generation_architecture.md`.
7. **Unity work** begins only when the owner approves implementation; this plan changes no Unity file.

## 5. Where each existing piece plugs in

| Existing piece | Plugs into |
|---|---|
| Stage B validator and mutation set (`tools/composer`) | LG-10, evaluation sets |
| Grounding validator (`server/validation`, C# twin) | LG-06, LG-19, every AI line |
| ContextBuilder and ReactionQueue | dialogue features (C24 to C30), multi-agent lab (E49) |
| Elo ratings and Selector | LG-02, fallback selection |
| EventDirector and what-if bindings | LG-16, branch and event variants |
| Discovery rules | LG-05 |
| Band sampling (EXP-DATA-2), derived aggregates | LG-07, parameter packs |
| Imaging library and image tools | LG-08, media index |
| Content manifest and source register | provenance in LG-14 |
| Session replay and goldens | LG-16, generated-case goldens |
