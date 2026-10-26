# ADR-0029 — The retired classifier plans are removed from the plan set

**Date:** 27 October 2026 · **Status:** ACCEPTED (owner authorisation of 27 Oct 2026: "permanently remove retired, cancelled, rejected and superseded feature plans that we have decided not to implement … E60 and the three retired classifier steps, provided you verify their exact identities and dependencies first")

## Context
ADR-0025 (21 Oct 2026) replaced the communication-act classifier (sentence-embedding and TF-IDF heads, the two-head credit gate, the critical-mass cap) with the Gemma semantic evaluator against the simulator's state and evidence, and removed the classifier code, tests, configuration and heads. The plans that had described that classifier stayed in the plan set as `Retired by decision` / `RESEARCH_NEGATIVE` so that the reconciliation, the pipeline JSON and the Ruhan and Moiz inventory kept counting them. They no longer describe anything the project is building.

## What was removed (exact identities)
| Id | Title | Kind | Status before | Retired by |
|---|---|---|---|---|
| **E60** | P2 small trained classifier | feature plan (`docs/plans/features/E60.md`, PH-19, tier P2) | `Retired by decision` | ADR-0025 |
| **p1-ai-language-s06** | Classifier taxonomy and labelled dataset | step (`docs/plans/p1/p1-ai-language/steps/p1-ai-language-s06-classifier-dataset.md`) | `RESEARCH_NEGATIVE`, retired | ADR-0025 |
| **p1-ai-language-s07** | Communication classifier (EXP-AI-3) | step (`…/p1-ai-language-s07-classifier-model.md`) | `RESEARCH_NEGATIVE`, retired | ADR-0025 |
| **p2-ai-extensions-s03** | Distilled small classifier (EXP-P2AI-2) | step (`docs/plans/p2/p2-ai-extensions/steps/p2-ai-extensions-s03-small-classifier.md`) | `RESEARCH_NEGATIVE` | ADR-0025 |
The text of the four files, and the reconciliation entries with their measurements, stay in git history (`git show d095b156:<path>`); the reasoning is in ADR-0012, 0013, 0016, 0020 and 0025 and in the decision log of the change report.

## Not removed, on purpose
* **p2-ai-advanced-simulation-s04 and s05** (generative image research, LLM adaptation research) and the features **E64** and **E65**: analysed on purpose, they reopen when the trigger stated in their reconciliation entry occurs. They are conditional work, not rejected work.
* Everything awaiting a device, a person or an acceptance (Blender, Unity and VR steps, headset checks, EX-01, EX-03, EX-18): unfinished or externally gated, not retired.

## Dependencies found and how they were handled
* **C36 (communication acts: taxonomy and evidence mapping)** was realised only by step s06, because s06 made `content/assessment/taxonomy.json`, which is still used by the graded scoring. The deliverable is live; the step was a classifier-dataset step. C36 is now realised by **p1-ai-language-s22** (the Gemma-first speech evaluation of ADR-0025 that built the graded scoring on that taxonomy and `mapping.json`). `taxonomy.json` and `tests/classifier/` data stay (the data and the frozen splits are evidence and are hash-locked).
* The pipeline JSON: node E60 and its three edges removed; the deleted step ids removed from the `tasks`, `traced_to` and phase lists of LG-17, B23, C36, C37 and the edges that cited them. The generated pages (matrix, registry, coverage audit, roadmap, milestones, flows, exports) were regenerated.
* The external handoff: item **EX-19** (n2c2 2010 assertion data as a supplementary check of a classifier vocabulary), whose only step was s06, was removed with its page entry; **EX-01** and **EX-18** keep their other steps.
* Counts, all derived: feature plans 68 to **67** (44 Complete, 15 Engineering complete, 6 Engineering complete with the switch off, 2 research triggers not met; the `Retired by decision` row is now 0; C36 moved from Engineering complete to Complete because its only outside item, EX-19, belonged to the retired classifier); steps 282 to **279**; the Ruhan and Moiz inventory 129 to **126** (the class `retired` no longer exists; completed 78, engineering done 43, gated off 1, research trigger not met 2, waiting on a source, a person or a device 2, unfinished 0).

## Consequences
The plan set describes what is being built. An ADR (this one and ADR-0025) is the record that the classifier was tried, measured and retired; nothing in the active plans, queues or indexes points at the removed files. The reconciliation and inventory checks fail if a removed id comes back without an entry.
