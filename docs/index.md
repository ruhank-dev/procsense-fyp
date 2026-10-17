# ProcSense documentation index — read this first

This is the map of the repository for every reader that arrives without context: a new teammate, a project assistant, or an agent that reaches the project through a tool server. Read it before anything else, the way a repository's agent-rules file is read before its code. It says **where everything is**, **which file is the source of truth for what**, and **which rules apply to every change**. Detail lives in the linked files; this page only routes.

_Last reviewed 12 October 2026 (UTC). Hand-written; `tools/plans/check_planning_sync.py` fails CI if a top-level folder or file of `docs/` is missing from section 3, so the map cannot silently go stale._

## 1. What ProcSense is, in five lines

- A VR training simulator (Meta Quest 2/3, Unity 6) for pre-operative preparation: the trainee takes an invented patient from arrival to the WHO Time Out in two scenarios, S1 (planned gallbladder removal) and S2 (emergency appendix removal). Educational only; not a medical device. All patients are invented.
- A deterministic C# engine (`src/ProcSense.Core`) decides every state change and every score. Content is data (`content/`), built by Python tools (`tools/`) and traced to sources (`docs/research/`).
- AI is planned in stages and is never the decider: rules decide, AI interprets and phrases, and nothing unchecked reaches a trainee ([ADR-0005](decisions/ADR-0005-ai-boundaries-and-staging.md)).
- The newest planning work (12 October 2026, **planning only, nothing implemented**) covers 69 AI features, dataset-driven live scenario generation, trainee personalisation, and a 24-phase roadmap: start at [architecture/README.md](architecture/README.md).
- Remote: https://github.com/ruhank-dev/procsense-fyp, branch `main`. Teammates push straight to `main` after `task ci` (no pull requests).

## 2. Start here: "I want to know…"

| Question | Open |
|---|---|
| What is built, and what is only planned? | [ai-roadmap-status.md](ai-roadmap-status.md) (AI), [plans/milestones.md](plans/milestones.md) (every step, ticked when approved) |
| What is outstanding, blocked or postponed? | [deferred-tasks.md](deferred-tasks.md) (register DT-001 onward) |
| Why was something decided? | [decisions/](decisions/) (ADR-0001 to ADR-0010) and the decision log in [context/ProcSense_Change_Report.md](context/ProcSense_Change_Report.md) section G |
| What do I do next, step by step? | [plans/README.md](plans/README.md), then the phase's `README.md` and `steps/` files |
| What does the AI part look like end to end? | [architecture/README.md](architecture/README.md), then [architecture/ai_scenario_generation_architecture.md](architecture/ai_scenario_generation_architecture.md) |
| What is the plan for one specific feature? | `plans/features/<ID>.md` (A1 to E65, A66, D67, D68, E69); list in [architecture/feature_registry.md](architecture/feature_registry.md); coverage in [architecture/feature_coverage_audit.md](architecture/feature_coverage_audit.md) |
| What are the live generation stages? | [architecture/live_generation_spec.md](architecture/live_generation_spec.md); diagrams in `architecture/flows/` (start with `00-live-pipeline.json`) |
| Which model, dataset or licence may I use? | [architecture/model_technology_register.md](architecture/model_technology_register.md), [architecture/dataset_feature_matrix.md](architecture/dataset_feature_matrix.md), [research/licence_provenance_register.md](research/licence_provenance_register.md) |
| Where does a clinical number come from? | [research/source_register.csv](research/source_register.csv) (one row per value, `SR-xxxx`); open medical conflicts in [research/issues.md](research/issues.md) |
| What is still waiting for a clinician? | [research/clinician_signoff.md](research/clinician_signoff.md) (research items; not a gate since 12 Oct 2026) |
| How do I run, test and push? | [../CONTRIBUTING.md](../CONTRIBUTING.md), [TEAM_GIT_WORKFLOW.md](TEAM_GIT_WORKFLOW.md), [CI.md](CI.md) |
| What did the owner ask, in order? | [previous_prompt.md](previous_prompt.md) (every owner prompt, appended, never edited) |

## 3. Map of `docs/`

| Path | What it holds | Authoritative for |
|---|---|---|
| [index.md](index.md) | This page | Where things are |
| [architecture/](architecture/README.md) | The AI and live-generation planning set: canonical `procsense_full_pipeline.json`, 69 feature plans, registry, live generation spec, dataset matrix, model register, roadmap, coverage audit, Synverk flows (`flows/`), Mermaid and Cytoscape exports, integration and testing plans, JSON guide | Anything about AI features, generation, personalisation, hardware, the roadmap phases PH-01 to PH-24 |
| [plans/](plans/README.md) | Tiers `p0/ p1/ p2/` → phases → step files (What, Why, How, Verification, Files, Dependencies, Common issues); `milestones.md` is generated; [working-model.md](plans/working-model.md), [phase-review.md](plans/phase-review.md) | What to build, in what order, with what acceptance |
| [decisions/](decisions/) | Architecture decision records ADR-0001 to ADR-0012 | Why a rule or design was chosen |
| [research/](research/README.md) | Proof of truth: source register, dataset register and cards (`datasets/`), licence and provenance register, research log, issues, clinician questions, review packs, glossary and teaching-script checks, equipment sheets (`equipment/`), behaviour and physiology notes (`behaviour/`, `physio-plots/`), quoted official sources (`official_quotes/`) | Every clinical value, dataset, licence and web source |
| [context/](context/) | The original Master Plan, Implementation Guide, Medical Walkthrough v5.1 and Data Source Inventory (format-only copies; bodies never edited), `CONVERSION_REPORT.md`, and the two living files `ProcSense_Change_Report.md` (decision log) and `ProcSense_Team_Board.md` | Original requirements and the history of decisions |
| [report/](report/ai-eval.md) | `ai-eval.md`, generated by `tools/eval/collect_ai.py` from the result files in `reports/ai/`: what each AI feature measured, on which model, with the limit of each measure | What the AI evaluations showed (never hand-typed) |
| [deferred-tasks.md](deferred-tasks.md) | Register of postponed, outstanding and blocked work (status: Pending, In progress, Completed, Blocked) | What is still open and what it waits for |
| [ai-roadmap-status.md](ai-roadmap-status.md) | Short audit of what is AI, what is rules, what is built; points to `architecture/` | The short answer on AI status |
| [CI.md](CI.md) | Every CI check, what it guards, the command to run it | What CI enforces |
| [USER_GUIDE.md](USER_GUIDE.md) | Short guide for trainees and teachers: starting, controls, the case flow, end states, debrief, practice and assessment | How to use the app |
| [ATTRIBUTIONS.md](ATTRIBUTIONS.md) | Credits and sources, generated from the registers (`task attributions`) | Who and what is credited |
| [TEAM_GIT_WORKFLOW.md](TEAM_GIT_WORKFLOW.md) | Branch, merge, `task ci`, direct push to `main` | Team git practice |
| [previous_prompt.md](previous_prompt.md) | Owner prompt history | What was asked, when |
| [team/](team/) | Ready-to-send messages for teammates | Nothing (convenience copies) |

## 4. Map of the code and content

| Path | What it holds | Notes |
|---|---|---|
| `src/ProcSense.Core` | Deterministic engine: session, subsystems, clock, physiology, events, scoring, difficulty, AI context and grounding validator | C# netstandard2.1, no Unity; determinism rules enforced by `tools/ci/repo_checks.py` |
| `src/ProcSense.Cli` | Headless runner: `run`, `replay`, `report`, `next-case`, `record-session`, `golden` | Plays a case without Unity |
| `tests/` | xUnit tests (`ProcSense.Core.Tests`), golden runs (`golden/`), shared vectors (`vectors/`), fixtures and sample scripts | |
| `content/` | The medical content package the engine reads (bands, tables, 53 case files, events, scoring, imaging library, AI lexicon and intents); `manifest.json` carries content hashes | Built by `tools/content/*.py`; every clinical value has a row in `research/source_register.csv` |
| `tools/` | Python tools managed with `uv`: content builders (each with `--check`), `composer/` (Stage B validator), `data/` (dataset fetch and extract), `golden/`, `physio/`, `ci/` (repo checks, `run_all.py`), `plans/` (planning validators and generators) | Run with `uv run --project tools <script>` |
| `server/` | `validation/` holds the Python grounding validator; the FastAPI backend and AI gateway are planned | See `architecture/architecture_integration_plan.md` |
| `unity/` | Unity 6 VR project (bridge, action emitters, views) | Not changed by the planning work |
| `.github/` | CI, release and main-guard workflows, Dependabot | |
| `Taskfile.yml` | `task setup`, `task ci`, `task check`, `task plans-check`, `task plans-index`, content and data tasks | |

## 5. Source of truth per topic (when two files disagree)

| Topic | The file that wins |
|---|---|
| AI features, generation stages, models, datasets, phases, rules | `architecture/procsense_full_pipeline.json` (everything else in `architecture/` is generated from it or must agree with it) |
| What steps exist and which are approved | The step files in `plans/`; `plans/milestones.md` is generated from them |
| Whether a task is open | `deferred-tasks.md` |
| A clinical value | `research/source_register.csv`, then the verified source it cites, never memory or the book alone |
| A dataset's licence | `research/dataset_register.csv` and `research/licence_provenance_register.md` |
| A decision and its reason | The ADR, else the Change Report decision log (previous decision, new decision, reason) |
| Simulation behaviour | `src/ProcSense.Core` and its tests (code is ground truth for what exists) |

## 6. Rules that apply to every change

These are the shared copies of the project rules. The owner's local rule files are not in the repository, so everything the team needs is here or in [CONTRIBUTING.md](../CONTRIBUTING.md).

1. **Planning-only tasks stay planning-only.** When a task is planning or documentation, write documents, registers, schemas and the small checkers that keep them consistent; do not implement features, train models, install or deploy services, or change Unity.
2. **Rules decide, AI phrases** ([ADR-0005](decisions/ADR-0005-ai-boundaries-and-staging.md), [ADR-0008](decisions/ADR-0008-live-generation-and-planning-policies.md)): state, critical scoring and action acceptance are deterministic; no AI output reaches a trainee unchecked; live sessions never retrain.
3. **Never claim something exists unless it was verified** in code, tests or files. Label planned work as planned; never mark planned work implemented. Unconfirmed items are recorded as such, not invented.
4. **No invented clinical facts or sources.** Every clinical value has a source row; research is logged with URL, access date, finding and certainty ([research/research_log.md](research/research_log.md)); licences are read and recorded, not assumed.
5. **Generated scenario content is labelled synthetic practice material**, never used for assessment, and never mixed up with real data. Invented patients only.
6. **No tool or co-author attribution** in commits or tracked files (enforced by `repo_checks.py`).
7. **Times are UTC**, shown as 12-hour with "UTC".
8. **Git:** commit only when the owner confirms; propose the commit message and the commands; no force-push, no history rewrite, no deleting others' work; teammates push directly to `main` after `task ci`.
9. **Use `uv` for Python.** Python scripts are run with `uv run --project tools …`.
10. **Keep the documents in sync.** After any decision or change, update every file that mentions it. The checklist for AI, generation, personalisation, dataset, model, hardware and roadmap changes is at the bottom of [architecture/README.md](architecture/README.md); for everything else update `deferred-tasks.md`, the plan step, `context/ProcSense_Change_Report.md` and `previous_prompt.md`. A passing validator is not proof that every document was updated.

## 7. Commands

| Command | Does |
|---|---|
| `task setup` | One-time clone setup, installs the pre-push hook |
| `task ci` (or `python tools/ci/run_all.py [--quick]`) | Everything CI runs; green locally means green on GitHub |
| `uv run --project tools tools/ci/repo_checks.py` | Project rules: determinism, hygiene, links, registers, prompt history, steps, attribution |
| `uv run --project tools tools/plans/validate_pipeline.py` | Validate the canonical pipeline JSON (plans, traceability, honesty) |
| `uv run --project tools tools/plans/pipeline_docs.py [--check]` | Regenerate (or check) the registry, feature plans, audit, spec, matrix, model register, roadmap and diagrams |
| `uv run --project tools tools/plans/check_planning_sync.py` | Check that plans, exports, step files, cited ids, registers and this index agree |
| `uv run --project tools tools/plans/milestones.py [--check]` | Rebuild (or check) `plans/milestones.md` |

## 8. Outside the repository

- **Task board:** a Synverk project named ProcSense (key PROC). On 12 October 2026 it was filled with one unassigned card per plan step (PROC-1 to PROC-263: Backlog 151, Ready 25, In progress 1, Human review 86), not the deferred-task items. Approved steps wait in Human review because only a person can move a card to Done. The repository, not the board, is the source of truth.
- **Local-only files** (git-ignored by the owner's decision): the owner's rule files and the session memory log. Anything the team must know is kept in tracked files such as this one.

## 9. Keeping this index current

Add a row to section 3 whenever a file or folder is added to `docs/`, and to section 4 whenever a top-level code folder is added. Update section 2 if a new kind of question appears. The sync checker enforces section 3.
