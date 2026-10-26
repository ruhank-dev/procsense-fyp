# Testing and evaluation plan (AI features and live generation)

Specification only (12 October 2026): these tests are planned with the features they test; none of the planned tests below is written yet. Tests that exist today are marked **exists**. Every planned test lands in CI with its feature (`docs/CI.md`), and every AI feature has a threshold below which it is switched off (E55).

## 1. Data quality

| Test | What it proves | Data | Pass mark | Where |
|---|---|---|---|---|
| Band compliance over 10,000 seeds (**exists** as EXP-DATA-2) | Generated labs stay in their band | `content/lab_bands.csv` | 100 percent | `tools/content/exp_data2.py` |
| Profile reproduction | Profiles match the existing derived percentiles | MIMIC-IV demo aggregates | p5 and p95 equal to 3 significant figures | p1-dataset-analysis-s02 |
| Pack sampling | 10,000 draws from a pack reproduce its profile | packs | KS statistic under the pack's tolerance; correlations within 0.1 | p1-dataset-analysis-s05 |
| Validation categories | Each of normal, abnormal, borderline, missing, artefact, conflicting, impossible and educational-anomaly rules has one passing and one failing fixture | fixtures | all fixtures behave | p1-live-scenario-generation-s05, s07 |
| Media tags | Every picture has finding tags, a quality grade and a licence row | imaging library | 100 percent | p1-dataset-analysis-s04 |
| Licence filter | No asset or dataset with REJECTED or NOT USED is ever selected | register | zero | p1-live-scenario-generation-s06 |

## 2. Scenario generation

| Test | What it proves | Pass mark | Where |
|---|---|---|---|
| Stage B mutation set (**exists**, 60 of 60) | Broken cases are rejected | 100 percent rejected | `tools/composer/test_composer.py` |
| Solver property test | No output violates requires or excludes | zero over 10,000 seeds | p1-scenario-composer-difficulty-s02 |
| Cross-modal mutations | Each VR-CM rule catches its break | 100 percent | p1-live-scenario-generation-s07 |
| Headless correct path | Every generated package reaches its declared end state | 100 percent | p1-live-scenario-generation-s07 |
| End-to-end batch | 1,000 seeded generations: zero defective deliveries, fallback rate reported | zero defects | p1-live-scenario-generation-s10 |
| Reproducibility | Same inputs give a byte-identical package | 100 percent | p1-live-scenario-generation-s09, s10 |
| Novelty simulation | 1,000 synthetic trainees: novelty rate | at least 95 percent where the pool allows | p1-live-scenario-generation-s09 |
| Quality gate calibration | Gold poor rejected, gold good passed | 100 percent and 90 percent | p1-live-scenario-generation-s08 |
| Judge agreement | LLM judge versus human ratings | Spearman 0.6 before it may steer repair | p1-live-scenario-generation-s08 |
| Latency | p95 generation time on the target GPU | under 45 s (pool covers the rest) | p1-live-scenario-generation-s10 |

## 3. AI interactions

| Test | What it proves | Pass mark | Where |
|---|---|---|---|
| Grounding vectors (**exists**, 44 shared Python/C#) | Validator behaves the same in both languages | all pass | `server/validation/test_validator.py`, `GroundingValidatorTests` |
| Disclosure probes (**exists**, 100+) | No hidden fact leaks | zero leaks | `server/validation` |
| Structured-output validity | Schema-valid on first try | 95 percent | p1-ai-language-s02 |
| Dialogue intent compliance | Every staff reply maps to an approved intent | 100 percent of sampled replies | p1-ai-language-s05 |
| Persona consistency | Persona stable across turns | 90 percent agreement | p1-ai-language-s04 |
| Classifier (retired by ADR-0025; its plans were removed by ADR-0029) | Macro-F1 on the frozen human-labelled split | 0.75 or switched off | none |
| ASR | WER on the medical vocabulary set and accented speech | under 15 percent | p1-voice-input-s01 |
| Voice orders | No order acted on without a confirmed read-back | 100 percent | p1-voice-input-s03 |
| Red team | Escape rate per attack class | zero for critical classes | p2-ai-advanced-simulation-s01 |

## 4. Scoring and debrief

| Test | What it proves | Pass mark | Where |
|---|---|---|---|
| Scoring tests and report check (**exist**) | Same log gives the same score and report | all pass | `ScoringTests`, `tools/ci/check_report.py` |
| Critical-act guard (**exists**) | AI never credits a critical, low-confidence or wrong-stage act | all pass | evidence store tests |
| Debrief mutation run | AI rewrite cannot drop a fact, change a number, flip a verdict or invent an event | zero escapes over 50 sessions | p1-ai-language-s08 |
| Generated-case scoring parity | Generated packages score with unchanged rules | identical rubric behaviour on paired cases | p1-live-scenario-generation-s11 |
| Analytics reproduction | Patterns, calibration and reasoning features reproduce from logs | exact | p2-ai-learning-analytics |

## 4b. AI composer, personalisation and hardware (second planning pass)

| Test | What it proves | Pass mark | Where |
|---|---|---|---|
| Composer versus solver-only baseline | The AI composer adds coherence and objective coverage, not just variety | Paired rater study on 40 cases, p under 0.05, equal Stage B pass rate | p1-live-scenario-generation-s13 |
| Knowledge-base extraction | Extracted relations are right | Precision at least 0.8 on 100 hand-labelled relations; recall above keyword rules | p1-dataset-analysis-s06 |
| Learned value generation versus independent sampling | Relationships between results are kept | Correlation error at least 50 percent lower; zero VR-CM violations | p1-live-scenario-generation-s14 |
| Text consistency critic | Free-text contradictions are caught | Recall 20 points above rules plus validator; false alarms under 10 percent | p1-live-scenario-generation-s15 |
| Learner model versus Elo | Better prediction of next-session performance; one slip never becomes a demonstrated weakness | Lower log-loss on simulated and pilot data | p1-personalisation-s02 |
| Targeting policy | Bandit adopted only when it beats rules | Off-policy estimate and simulated learning gain above rules | p1-personalisation-s03, s05 |
| Interpreter hypotheses | AI interpretations are useful, not taken as truth | Later confirmation rate above random picks | p1-personalisation-s04 |
| Dual-GPU benchmark | Every AI capability fits a measured deployment | Report for each model on GTX 1660 SUPER and RTX 4050, with and without Unity | p1-ai-infrastructure-s02 |
| Revived generative research | Only proven synthetic assets or adapters are used | Finding agreement at least 90 percent, rater accuracy under 70 percent; adapters beat base | p2-ai-advanced-simulation-s04, s05 |
| Every AI capability (105 capabilities, 65 AI) | Each has a baseline comparison and acceptance criterion | See `docs/plans/features/<ID>.md` and the coverage audit | per feature |

## 5. Whole system

| Test | What it proves | Pass mark | Where |
|---|---|---|---|
| Replay of every session (**exists** for library cases) | Determinism | log hash equal | goldens |
| Offline mode | A session runs with no backend and no GPU | starts from the pool in under 2 s | p1-live-scenario-generation-s10 |
| Switch-off gate | Turning any AI feature off leaves a working session | all fallbacks work | p1-ai-language-s09 |
| Learning effect | Trainees on generated practice versus library practice | study design in p1-evaluation-s03 | p1-evaluation-s03 |
| Planning documents agree with the JSON (**exists**) | Plan files, exports, step files, milestones and cited ids match; no stale wording | `check_planning_sync.py` passes | `tools/plans/` |
| Pipeline JSON (**exists**) | Planning graph is consistent and every feature has a complete plan | `validate_pipeline.py` passes (including VR-G-PLANS), 16 of 16 validator tests | `tools/plans/` |
