# Language-model adaptation research: is its trigger met? (p2-ai-advanced-simulation-s05, E55 and E57)

Written 18 October 2026 from the committed evaluations in `reports/ai/`. No fine-tuning was done.

## The trigger

The step starts only when there is a **documented quality gap that prompting cannot close** for the dialogue (E55) or the model choice (E57).

## What the measurements say (updated 18 October 2026, second session)

The first version of this page listed gaps that were open that morning (debrief 62 percent within 8 s, interpreter below random picks, composer below the solver, three characters at 7.1 s). Each was closed by a prompt version, completion in code or a change of design, not by training (see `docs/research/model_benchmarks.md` and `docs/report/ai-eval.md`); every language-model feature of the gate now passes. The check is no longer hand-written: `uv run --project tools tools/ai/adaptation_trigger.py --check` reads the gate and `content/ai/adaptation_triggers.json` (remedies tried, training examples held, held-out set, compute window) and the result is `docs/research/adaptation_trigger_check.md`, which CI keeps current.

## Decision

- **Trigger: not met.** There is no failing quality suite in the gate for any language-model feature, and the other four conditions (remedies exhausted, 500 permitted examples that are not the model's own output, a held-out set, a compute window) do not hold either. Fine-tuning a 4B model on the 6 GB card would also need training data: the only data we hold are synthetic, model-written conversations, and tuning a model on its own output would not show whether it had improved.
- **What would reopen it:** a quality gap that survives those remedies (for example a patient dialogue whose rated realism stays low after prompt and context changes), real or clinician-approved dialogue examples to learn from, and a way to measure the gain that does not use the training data. The learning-effect and AI-dialogue A/B studies (p1-evaluation-s03, s04) are the evidence that would show such a gap.
- **Meanwhile:** the models stay as chosen in ADR-0010 and the prompt registry is the way a behaviour changes (a new immutable version, measured against the old one).
