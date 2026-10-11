# ADR-0005: AI only interprets and phrases; rules decide; nothing unchecked reaches a trainee

**Status:** Accepted 12 October 2026 (written during the AI-roadmap audit; for the owner's approval) · **Related:** ADR-0001, `docs/ai-roadmap-status.md`

## Context
The plans use AI in three ways: to turn speech into structured acts (classifier, speech recognition), to voice lines the rules have already decided (patient and staff dialogue, debrief rewrite) and to propose new cases. A model can be wrong, can leak a hidden fact, can invent a drug or an event, and cannot be proven correct. The simulator teaches clinical practice, so a wrong line teaches the wrong thing.

## Decision
1. **Rules decide, AI phrases.** Correctness, disclosure, scoring, end states and every state change come from tested rules and tables. An AI suggests an intent id or a wording; it never sets state and never credits a critical item (identity, allergy, consent, handover critical elements) from speech.
2. **The model is never given what it must not say.** The per-character context holds only facts the trainee has learned or has earned this turn; hidden facts, scores, hints and expected answers are not in it. The validator holds the hidden facts only to catch a leak.
3. **Every AI output passes the grounding validator** (schema and enums, fact ids, off-card entities, ungrounded numbers, disclosure, dose statements, prescribing phrasing, and for a debrief: every fact covered, numbers and verdicts unchanged, no invented event). A failing line is replaced by a template or a pre-rendered line. Python and C# versions run the same vectors.
4. **A template always exists first.** The template debrief and the fixed staff lines work offline; an AI version replaces them only after validation.
5. **Adaptation is a rating and a lookup, not a model**, and is off in assessment mode. It chooses among approved cases only. A case drafted by AI is checked by Stage B and cannot be given an approved status by any tool; a person approves it.
6. **Staging.** P0 has no model. The deterministic foundation and the safety gate come first; served models come in P1 behind the gate; the only trained model is the P2 small classifier. A feature that fails its evaluation threshold is switched off by default.
7. **Honest labels.** Anything that is rules or a rating says so in its name, its docs and its tests; no rule is called a trained model.

## Consequences
The AI stages can be built in any order after the gate, none can bypass it, and a missing model leaves the product working. The cost is that a paraphrased leak is only prevented by the context design (rule 2), not detected by the validator; this is documented as a known limit.

## Revisit if
A model is trained and evaluated well enough to credit non-critical acts, or a regulator requires different handling of AI output.
