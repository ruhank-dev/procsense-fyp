# Architecture and AI planning

Planning set of 12 October 2026 for every AI feature and for dataset-driven live scenario generation. **Planning only:** nothing marked Planned, Proposed, Blocked or Deferred is built; implementation starts only when the owner says so (INSTRUCTIONS.md section 8j).

The map of the whole repository is [../index.md](../index.md); this README covers the AI and generation planning set only.

## Which file is authoritative for what

| Topic | Authoritative file | Kind |
|---|---|---|
| Whole plan as a graph (stages, features, datasets, models, phases, rules) | [procsense_full_pipeline.json](procsense_full_pipeline.json) | canonical source |
| Feature inventory (69 features: A1 to E65 plus A66, D67, D68, E69) with status and evidence | [feature_registry.md](feature_registry.md) | generated |
| Complete implementation plan for every feature (separate capability plans, seven AI questions, schemas, failure handling, both-GPU hardware, tasks, acceptance) | `feature_plans/<ID>.md` (69 files) | generated |
| Synverk flow diagrams of the pipeline (10 linked files, each under 64 KB; schema `/api/flows/schema`) | `flows/*.json` (start with `00-live-pipeline.json`) | generated |
| Feature coverage audit (computed, not asserted) | [feature_coverage_audit.md](feature_coverage_audit.md) | generated |
| AI scenario generation and personalisation architecture (learning from evidence, AI composer, modalities, consistency, novelty, learner model, offline versus live) | [ai_scenario_generation_architecture.md](ai_scenario_generation_architecture.md) | hand-written |
| Live generation stages LG-01 to LG-21, validation, novelty | [live_generation_spec.md](live_generation_spec.md) | generated |
| Datasets and which features use them | [dataset_feature_matrix.md](dataset_feature_matrix.md) | generated |
| Models and technology choices | [model_technology_register.md](model_technology_register.md) | generated |
| Phases, critical path, feature to task map | [master_roadmap.md](master_roadmap.md) | generated |
| Layers, contracts, integration rules | [architecture_integration_plan.md](architecture_integration_plan.md) | hand-written |
| Tests and evaluation | [testing_evaluation_plan.md](testing_evaluation_plan.md) | hand-written |
| JSON format, validator, visualisation | [pipeline_json_guide.md](pipeline_json_guide.md) | hand-written |
| Licences and provenance | [../research/licence_provenance_register.md](../research/licence_provenance_register.md) | hand-written |
| Sources read online | [../research/research_log.md](../research/research_log.md) | hand-written |
| Decision record | [../decisions/ADR-0008-live-generation-and-planning-policies.md](../decisions/ADR-0008-live-generation-and-planning-policies.md) | hand-written |
| Short audit answers (what is AI, what is built) | [../ai-roadmap-status.md](../ai-roadmap-status.md) | hand-written summary that points here |
| Open and deferred work | [../deferred-tasks.md](../deferred-tasks.md) (DT-101 to DT-118) | register |
| Plan steps for new work | `docs/plans/p1/p1-dataset-analysis`, `p1-live-scenario-generation`, `p1-ai-infrastructure`, `p1-personalisation`, `docs/plans/p2/p2-ai-learning-analytics`, `p2-ai-advanced-simulation` (written to INSTRUCTIONS 8b: What, Why, How, Verification, Files, Dependencies, Common issues) | step files |

If two files disagree, the JSON wins for anything it covers; fix the other file.

## Keeping the planning set in sync (checklist for everyone, tracked in the repository)

Whenever a decision, requirement, plan or implementation changes anything about the AI features, live generation, personalisation, datasets, models, hardware or the roadmap, update **all** of these in the same change:

1. `procsense_full_pipeline.json` (the source), then run `uv run --project tools tools/plans/pipeline_docs.py` so the registry, feature plans, coverage audit, spec, matrix, model register, roadmap, Mermaid and Cytoscape files regenerate (never edit those by hand).
2. The plan step files under `docs/plans/` (What / Why / How / Verification / Files / Dependencies / Common issues) and `uv run --project tools tools/plans/milestones.py`.
3. `docs/deferred-tasks.md` (new rows, statuses, the change log), `docs/ai-roadmap-status.md`, the ADRs (`docs/decisions/`), `docs/research/research_log.md` and `licence_provenance_register.md` (sources, dates, licences).
4. The context documents: `docs/context/ProcSense_Change_Report.md` (decision log row: previous decision, new decision, reason) and `ProcSense_Team_Board.md`; add a project-addendum banner to a converted source document only if its meaning changed.
5. `docs/CI.md` and the CI wiring if a check was added; `docs/previous_prompt.md` for every owner prompt.
6. When something is implemented, change its status and add real evidence paths in the JSON, tick the plan step, and keep the statuses honest (never mark planned work as built).
7. Run, and keep green: `validate_pipeline.py`, `test_validate_pipeline.py`, `pipeline_docs.py --check`, `check_planning_sync.py`, `milestones.py --check`, `tools/ci/repo_checks.py`.

The owner's local project-rule files and the `.memory/` log are git-ignored by the owner's decision (see `.gitignore`), so this checklist and ADR-0008 are the shared copy of the policies that matter to the team: planning-only tasks stay planning-only, no clinician sign-off gate, rules decide and AI phrases (ADR-0005), practice-only generated content, licences recorded not ignored, and every feature has a full plan.
