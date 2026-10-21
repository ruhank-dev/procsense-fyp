# EX-01 phase two: who can fill which role (strict design and FYP-scale alternatives), 21 October 2026

> **PAUSED (owner decision of 21 Oct 2026):** the manual author/annotator workflow below is on hold. The owner prefers directly written, AI-generated synthetic development data (`docs/research/direct_synthetic_dev_set_design.md`; the multi-model factory was abandoned) and to spend human effort on the final Meta Quest test. The tooling, packages and guides are kept unchanged for possible later use; nothing here was deleted.

**Nothing has been weakened and no names have been assigned.** The kit today enforces the strict five-person design. This page is the analysis the owner asked for before giving the real people and roles. The owner has seen model predictions, prompts, guard decisions and the failures throughout this project; under the builder rule the owner therefore belongs in `builder_names` and should not be an author, annotator or adjudicator unless the owner decides otherwise on the record.

## What independence has to protect (item by item)
For every item i:
* R1 the **annotators of i did not write i** (so their reading cannot be shaped by what the author meant);
* R2 the **two annotators of i work alone** and are two different people;
* R3 the **adjudicator of i is neither the author nor an annotator of i**;
* R4 **nobody in any role is a builder** of the evaluator, prompts, guards or verifier, and nobody saw a model prediction, head probability, guard decision, existing benchmark label or intended ProcSense output;
* R5 the **labels are fixed before any system sees the set** (freeze, SHA-256), disagreements are recorded and not forced, and an item the definitions cannot settle is flagged, not decided by taste.
Role independence (a person holds exactly one role for the whole set) is the simplest way to guarantee R1 to R3; item-level independence (a person may hold different roles on different items if R1 to R3 hold for every item) is enough in principle but needs the tooling to enforce it item by item and gives up some protections listed below.

## Design S: strict five people (what the kit enforces today)
| person | role | items |
|---|---|---|
| P1, P2 | authors | 150 each |
| P3, P4 | annotators | all 300, alone |
| P5 | adjudicator | the items on which P3 and P4 differ |
Independence: R1 to R5 hold at role level. Authors never label, so no author's idea of "what is obvious" enters any label. Both annotators see every item, so agreement and kappa are measured on the whole set and neither annotator's habit is confined to a subset. The adjudicator has seen no item before the disagreements. Cost: five people; about 3 hours of writing per author, 3 to 4 hours of labelling per annotator, about 1 to 2 hours of adjudication (the disagreement rate is unknown, 10 to 25 percent of 300 is plausible).

## Design A: four people, cross-annotation (the smallest design I consider defensible at FYP scale)
| person | role | items |
|---|---|---|
| P1 | author of items 1 to 150; annotator of P2's items only | |
| P2 | author of items 151 to 300; annotator of P1's items only | |
| P3 | annotator of all 300 | |
| P4 | adjudicator of every disagreement | |
Every item still has two blind annotators who did not write it and an adjudicator who is neither; R1 to R5 hold item by item.
What is given up, honestly:
1. P1 and P2 know the design (the family list and the idea of the set) and each has written half the set; they could guess the family of the other's sentence, and their way of reading definitions may be correlated because they wrote to the same brief. They must not discuss items.
2. Agreement and kappa are computed on two different pairs ((P2, P3) on P1's items, (P1, P3) on P2's), so each pair sees half the set; one reviewer (P3) labels everything, so P3's habits weigh on every item.
3. P1 and P2 label after writing their own items; the experience of authoring may sharpen or bias their reading of definitions.
4. The tool must issue each author a file that excludes their own items and check R1 to R3 per item. This is NOT implemented yet (the roles record, the packaging and the freeze check are role-level). I will implement it, with tests, once the owner chooses this design.

## Design B: three people (not recommended, listed so the trade-off is explicit)
P1 and P2 write 150 each and cross-annotate; P3 annotates all 300; **there is no independent adjudicator** (the only people left are the item's author or its own annotators, which breaks R3). The only honest handling is to keep every disagreement as **contested**, exclude contested items from the scored set and report their number, families and both labels. That removes exactly the borderline and safety-sensitive items, so measured recall and precision are biased upward; the report would have to add a sensitivity analysis (scoring contested items under each annotator's label, best case and worst case). Not defensible for the safety claims; usable only as a rehearsal.

## Rejected
* Two people who write and label each other's items, with no second annotator and no adjudicator: each item has one blind label, so disagreement cannot even be detected.
* Any design where the author labels or adjudicates their own item, or where a builder labels anything.

## Recommendation
Use **Design S** if five people are available. Otherwise use **Design A** and treat it as weaker on points 1 to 3 above, say so in the report, and keep the same freeze, summary and single-run rules. Do not use Design B for any claim. The 300-item size is the same in all designs.

## What I need from the owner
The real names and roles (or the choice of design and the people); whether the owner is declared a builder; whether a clinician educator can adjudicate. The roles record template is `docs/testing/ex01_templates/roles_record.template.json`; the owner signs it.
