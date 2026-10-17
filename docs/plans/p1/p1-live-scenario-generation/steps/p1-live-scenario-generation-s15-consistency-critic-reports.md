# p1-live-scenario-generation-s15 — Generated reports and the cross-modal text consistency critic

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Write investigation reports from approved templates with numbers locked to the generated values (LLM paraphrase optional, numbers verified), and run an LLM critic that extracts claims from all generated text (narrative, reports, dialogue seeds) and compares them with the structured case; any unresolved conflict rejects the package.

## Why
Contradictions hidden in free text are the ones rules miss.

## How to implement
1. Report templating with number lock.
2. `server/generation/critic.py`: claim extraction prompt, comparison, defect output.
3. Seeded set of 200 text inconsistencies.

## Status (17 October 2026)

- **Built:** `server/generation/reports.py` (a laboratory line from each result row, an imaging report from the picture; `check` allows only the numbers the case or the picture holds) and `server/generation/critic.py` (the model reads the structured facts and the patient's lines and flags conflicts; a flag is kept only for a real line id and a fact copied from the list; unresolved flags become defects and go to repair; the critic only adds defects). `tools/generation/inconsistency_set.py` builds the seeded set (214 inconsistencies of seven kinds from the approved cases, SD-001) and the rule-based baseline.
- **Measured** (`tools/ai/eval_critic.py`, 9B): recall of the baseline 66.8 percent, of the critic alone 68.7 percent, of both 81.8 percent; false alarms on 26 clean cases: critic 3.8 percent, baseline 19.2 percent; 0 questions the critic could not answer.
- **Limit:** the contradictions are blunt and synthetic; the criterion (union recall 20 points above the rules, false alarms under 10 percent) is judged in `docs/report/ai-eval.md`. Paraphrase of reports by a model was not built: the numbers are locked in the template lines.

## Verification
Critic recall at least 20 points above rules plus lexical validator, false alarms under 10 percent.

## Files changed
`server/generation/reports.py`, `server/generation/critic.py`

## Dependencies
p1-live-scenario-generation-s07, p1-ai-infrastructure-s02

## Common issues
Critic false alarms block good cases: tune on the seeded set; unresolved flags go to repair, not silent pass.

## Sources
Features B18, B22.
