# Language-model adaptation research: is its trigger met? (p2-ai-advanced-simulation-s05, E55 and E57)

Written 18 October 2026 from the committed evaluations in `reports/ai/`. No fine-tuning was done.

## The trigger

The step starts only when there is a **documented quality gap that prompting cannot close** for the dialogue (E55) or the model choice (E57).

## What the measurements say

| Feature | Measure | Result | Gap against its target | Closed by prompting or code yet? |
|---|---|---|---|---|
| Patient dialogue | leaks of withheld facts; p95 latency | 0 leaks; 2.3 s against 3 s | none | not needed |
| Debrief rewrite | accepted share; within 8 s | 69 percent accepted; 62 percent within 8 s (target 80) | latency, acceptance | latency: a shorter prompt and completing dropped facts from the template are being tried (no fine-tuning needed: the cost is retries and output length); acceptance: failures are a changed verdict (5), a dropped fact (4, now completed in code) and a mixed section (1) |
| Text critic | recall gain over the rules | rules now find 96.7 percent of the first set | the set, not the model | a harder held-out set (DT-217) |
| Performance interpreter | confirmed share against random picks | 67.5 against 70.0 percent | below random | a weakness-only rule and a prompt version 2 are built and not yet measured |
| Plan composer | coverage against the solver | 0.685 against 0.746 | below the solver | the model can only choose among the solver's top eight; a hybrid or a tighter shortlist is the first remedy |
| Three characters | all three p95 | 7.1 s against 4 s | latency | parallel requests to one loaded model, not a second model |

## Decision

- **Trigger: not met.** Every gap that remains has a cheaper remedy that has not been exhausted (prompt versions, completion in code, a validator rule, shorter output, parallel requests, a better evaluation set). Fine-tuning a 4B model on the 6 GB card would also need training data: the only data we hold are synthetic, model-written conversations, and tuning a model on its own output would not show whether it had improved.
- **What would reopen it:** a quality gap that survives those remedies (for example a patient dialogue whose rated realism stays low after prompt and context changes), real or clinician-approved dialogue examples to learn from, and a way to measure the gain that does not use the training data. The learning-effect and AI-dialogue A/B studies (p1-evaluation-s03, s04) are the evidence that would show such a gap.
- **Meanwhile:** the models stay as chosen in ADR-0010 and the prompt registry is the way a behaviour changes (a new immutable version, measured against the old one).
