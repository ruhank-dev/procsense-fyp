# FYP report outline and demo material

Draft outline for the final-year report (p0-evaluation-performance-s09). Every number in the report comes from a recorded result file; none is typed in (INSTRUCTIONS section 4c). Where a result does not exist yet, the chapter says so.

| Chapter | What it says | Where the facts are |
|---|---|---|
| 1 Problem and aims | A VR pre-operative preparation trainer for two scenarios, S1 (planned gallbladder removal) and S2 (emergency appendix removal); educational only | [../index.md](../index.md), [../USER_GUIDE.md](../USER_GUIDE.md) |
| 2 Walkthrough-driven design | The medical walkthrough fixes the steps, the end states and the lessons; the engine implements them | [../context/ProcSense_Medical_Walkthrough_v5_1.md](../context/ProcSense_Medical_Walkthrough_v5_1.md) |
| 3 Architecture | Deterministic C# engine decides; Unity is a view and input layer; AI interprets and phrases and never decides (ADR-0005) | [../decisions/](../decisions/), [../architecture/README.md](../architecture/README.md) |
| 4 Proof of truth | Every clinical value has a source-register row; conflicts are logged; datasets and licences are registered | [../research/source_register.csv](../research/source_register.csv), [../research/README.md](../research/README.md) |
| 5 Implementation by phase | What each phase built, with the step files and the reconciliation | [../plans/task-reconciliation.md](../plans/task-reconciliation.md), [../plans/milestones.md](../plans/milestones.md) |
| 6 Experiments | The experiment tables of all phases, generated | [experiments.md](experiments.md) |
| 7 AI evaluation | What each AI feature measured, on which model, with the limit of each measure | [ai-eval.md](ai-eval.md) |
| 8 Evaluation with people | Golden suite, usability, clinical realism, performance: only what was recorded | [../testing/SANNAN_EXTERNAL_TESTING.md](../testing/SANNAN_EXTERNAL_TESTING.md) (what is still to be run) |
| 9 Limitations and future work | Synthetic and AI-judged data are not validation; headset, participant and clinician checks that are still open | [../deferred-tasks.md](../deferred-tasks.md) |

Figures: the architecture diagram is `../architecture/pipeline_overview.mmd`; the sample debrief comes from `task sim -- report`; performance charts and usability tables are made from the result files named in chapter 8 once they exist.

## Demo video script

Two runs, about three minutes each, recorded on the headset by the person doing the demo (the recording itself is listed as EX-DEMO in the external testing handoff).

1. VAR-S1-A happy path: arrival and identity check, allergy question, history, vital signs, consent, the WHO Sign In and Time Out, ready for incision, then the debrief.
2. VAR-S2-PD deterioration: the patient worsens while untreated, the trainee recognises it late, the debrief shows the lateness on the timeline.

Say aloud at the start of each run that all patients are invented and the simulator is for education only.
