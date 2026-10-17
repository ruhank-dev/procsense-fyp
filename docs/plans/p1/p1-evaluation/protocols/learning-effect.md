# Protocol: learning-effect study (EXP-EVAL-3, p1-evaluation-s03)

Status: **draft, not approved, no participant recruited.** The supervisor or the university's ethics contact must agree it before anyone is invited (DT-206). The analysis is ready (`tools/eval/analyse_study.py learning`); nothing below has been run.

**Question.** After practising with ProcSense, do people do better on a short knowledge quiz and in a matched case, and how sure can we be?

**Design.** The same participants (target 12, at least 8) play two sessions at least one day apart: session 1 on case A, session 2 on a matched but different case B of the same family (for example VAR-S1-A then VAR-S1-C; the pairs are fixed before anyone plays). A short knowledge quiz is taken immediately before session 1 and immediately after session 2. No control group (P1 has none); the page therefore never claims causation.

**Procedure.** Consent (below) -> quiz before -> session 1 (practice mode) -> a day or more later -> session 2 (practice mode, no hints beyond those of the app) -> quiz after -> debrief of the study. Each participant has a code (`P01`), never a name, in every file.

**Measures.** Quiz score change (before session 1 to after session 2); number of critical items (from the report); correct end state (0 or 1); seconds to the key decision. The table is `learning.csv` (`participant,session,quiz_before,quiz_after,critical_items,correct_end_state,seconds_to_decision`).

**Quiz.** Questions come from the confirmed teaching points of `docs/research/teaching_script_check.csv`; `tools/eval/build_quiz.py` lists them as candidates in `content/eval/quiz_candidates.json`. A clinician reviews the wording and writes the wrong answers before any question is used (the questions are not mine to invent). Until then the quiz is blocked (DT-206).

**Analysis.** `uv run --project tools tools/eval/analyse_study.py learning --csv learning.csv --out docs/plans/p1/p1-evaluation/results/learning-effect.md`: paired differences, effect sizes (Hedges' g), 95% bootstrap intervals, sign-flip permutation tests, and the limitations paragraph.

**Consent (to be read aloud and signed or recorded).** You are asked to play two short practice sessions of a simulated hospital scene and answer a short quiz. The scenes use invented patients. We record your actions and answers under a code, not your name, to see whether practice helps. You can stop at any time and ask for your data to be deleted until the results are written up. Your data is kept on the project's own machines and is not used to judge you. Recordings of your voice, if any, follow `docs/research/labelling/consent_and_storage.md`.

**Stopping and safety.** A participant who is upset stops; the study uses no real patient data; nothing is reported that identifies a person.

**Reporting.** The page is written from the raw table with its limitations; a result that is small or uncertain is reported as such.
