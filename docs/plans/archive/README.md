# Plans archive and retired plans

Updated 27 Oct 2026. Two kinds of entry: documents moved out of the way because every decision in them has been taken, and plans that were **retired or superseded by a decision** and are deliberately **kept in place** (not deleted) because the reconciliation, the pipeline JSON, the Ruhan and Moiz inventory (129 steps) and the ADRs count or cite them. A retired plan is never ticked; the generated pages call it `Retired by decision` or `RESEARCH_NEGATIVE`.

## Archived documents
| Document | Why archived |
|---|---|
| [2026-10-07-phase-review.md](2026-10-07-phase-review.md) | A one-off phase review: all proposals were decided on 07 Oct 2026 (approved P1, P2, P3, P6; rejected P4, P5); the outcome is in `docs/plans/README.md` and the change report. |

## Retired or superseded plans (kept in place for traceability)
| Plan | Where | Retired by | Why it stays |
|---|---|---|---|
| Feature E60 (communication classifier) | `docs/plans/features/E60.md` | ADR-0025 (Gemma is the semantic evaluator; the sentence-embedding and TF-IDF classifier and both heads were removed) | The feature id is one of the 68 in `procsense_full_pipeline.json`; the page records what replaced it. |
| Steps p1-ai-language-s06 (classifier dataset), p1-ai-language-s07 (classifier model), p2-ai-extensions-s03 (small classifier) | `docs/plans/p1/p1-ai-language/steps/`, `docs/plans/p2/p2-ai-extensions/steps/` | ADR-0025 | Counted in the 282 steps and the 129-step inventory (as `retired`); cited by ADR-0012 and ADR-0016. |
| p2-ai-advanced-simulation-s04 (generative image research), p2-ai-advanced-simulation-s05 (LLM adaptation research) | `docs/plans/p2/p2-ai-advanced-simulation/steps/` | Research analysed on purpose; the trigger is not met (DT-212, DT-21x) | They reopen only when the stated trigger occurs; they are conditional work, not rejected work. |
| Spoken-order mapper stack (orders.kind, orders.map, the re-ranker, the agreement rule) | code removed from the model path; history in ADR-0018, ADR-0021 and `docs/research/order_mapper_comparison.md` | ADR-0028 | Only the offline grammar and its question guard remain from it. |

Not retired, although unticked: features and steps owned by others (Blender, Unity, VR), externally gated work and anything awaiting a test or acceptance. Their status is in `docs/plans/ruhan-moiz-inventory.md`, `docs/plans/execution-queue.md` and `docs/testing/SANNAN_EXTERNAL_TESTING.md`.
