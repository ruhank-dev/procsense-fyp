# Protocol: P0 usability study (EXP-EVAL-1, p0-evaluation-performance-s05)

Status: **draft, not approved, no participant recruited.** The supervisor or the university's ethics contact must agree it before anyone is invited. The analysis is ready and tested on simulated rows (`tools/eval/analyse_usability.py study`, `tools/ci/test_usability_analysis.py`); nothing below has been run with people.

**Question.** Can the target users (medical students and junior doctors) work through a P0 case in the headset, and how usable and how demanding do they find it?

**Participants.** Target 8 to 12, recruited through the supervisor: medical students in their clinical years and foundation doctors. Not chosen for VR experience. Each has a code (`P01`), never a name, in every file. Gamers who are not medical are not the target users (the plan says so) and are not recruited.

**Session (about 45 minutes).**

| Part | Minutes | What happens |
|---|---|---|
| Briefing and consent | 5 | The consent text below; questions answered |
| Controls practice | 5 | The Settings tab (panel size, subtitles, hand, seated) and the practice room; no case content |
| S1 case | 15 | VAR-S1-A in practice mode, from the bay to the decision |
| S2 case | 10 | VAR-S2-B in practice mode |
| Questionnaires | 5 | SUS (10 items, 1 to 5) and raw NASA-TLX (6 subscales, 0 to 100) on paper |
| Short interview | 5 | "What was hardest? What would you change? Did anything feel unrealistic?" |

The researcher does not coach during a case; where the participant is stuck for more than a minute the researcher notes it and gives the scripted prompt "What would you do on a real ward?" once.

**Measures.** Task success per step (from the session log and the observer), time per task, errors (rejected actions in the log), the end state reached, SUS, raw NASA-TLX, interview notes. The table is `study.csv`; its columns are in the header of `tools/eval/analyse_usability.py` and the empty template is `protocols/usability-study-template.csv`.

**Tasks scored.** `step1_identity` (identity checked and confirmed), `observations` (sensors on, readings taken), `order_test` (a test ordered and the sample sent), `read_results` (a result opened and interpreted), `sign_in` (WHO Sign In led to the end), `decision` (the case decision made).

**Analysis.** `uv run --project tools tools/eval/analyse_usability.py study --csv study.csv --out docs/plans/p0/p0-evaluation-performance/results/usability-study.md`: medians, interquartile ranges and ranges only, task success counts, the end states reached, and the limitations. No significance testing: the sample is too small and there is no comparison group.

**Consent (to be read aloud and signed or recorded).** You are asked to play two short practice cases of a simulated hospital in a VR headset and answer two short questionnaires. The patients are invented. We record your actions in the simulator and your answers under a code, not your name, to find out what is hard to use. You can stop at any time, for any reason, including if you feel unwell in the headset, and ask for your data to be deleted until the results are written up. Your data is kept on the project's own machines, is not shared, and is not used to judge you or your course.

**Stopping and safety.** Anyone who feels motion sickness stops at once and sits down; seated play is offered from the start. No real patient data is used. Nothing reported identifies a person.

**Reporting.** The page is written from the raw table by the script, with its limitations. A small or uncertain result is reported as such.
