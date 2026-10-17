# Protocol: AI dialogue A/B (EXP-EVAL-4, p1-evaluation-s04)

Status: **draft, not approved, no participant recruited.** It needs the same agreement as the learning study (DT-206) and the AI dialogue to be switched on for the trial (it is on in the gate: `patient_dialogue`). The analysis is ready (`tools/eval/analyse_study.py ab`); nothing below has been run.

**Question.** Does free-text AI dialogue make the patient feel more real than the menu of approved questions, without any safety cost?

**Design.** Counterbalanced crossover on one case family: half the participants (random assignment, fixed before they arrive) play case A with the menu and case B with AI dialogue; the other half the reverse; at least a second session for some participants to look at novelty. Target 12, at least 8.

**Measures.** Realism rating (1 to 7, one question: "the patient felt like a real person"); task success (0 or 1); seconds to the key decision; validator rejections and fallbacks (from the audit log, `server/routers/ai_audit.py`); and any safety issue found in the audit log by reading every AI line of every AI session (a hidden fact told early, a dose, an instruction to the trainee, an invented clinical fact). The table is `ab.csv` (`participant,order,mode,realism,task_success,seconds,rejections,fallbacks,safety_issues`).

**Decision rule, fixed before the trial.** Any safety issue keeps AI dialogue off by default. Otherwise it may be switched on by default only if realism is higher with it and the sign-flip test is below 0.05; the gate's other suites still have to pass.

**Analysis.** `uv run --project tools tools/eval/analyse_study.py ab --csv ab.csv --out docs/plans/p1/p1-evaluation/results/ai-dialogue-ab.md`; the page shows the order effect, because novelty can inflate the AI rating.

**Consent.** As in the learning-effect protocol, with the addition: the patient's lines in one of the two modes are produced by a computer program from approved facts; you are not told which until afterwards.

**Reporting.** Raw counts of rejections, fallbacks and safety issues are reported with the ratings, whatever they show.
