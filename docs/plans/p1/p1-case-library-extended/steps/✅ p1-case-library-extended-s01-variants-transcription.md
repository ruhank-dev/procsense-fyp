# p1-case-library-extended-s01 — Transcribe the 10 remaining variants

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Case files for VAR-S1-I (penicillin anaphylaxis 2015), -K (Hb 98, MCV 72), -L (new AF HR 128), -M (BMI 41, STOP-BANG 6, ASA 3) and VAR-S2-E (weight loss, CT first), -H (pregnant 20 weeks: maternity tool, US then MRI, left tilt), -I (16-year-old: minor consent per law), -J (dysuria, don't stop at UTI), -K (childhood penicillin rash), -L (capacitous refusal; antibiotics-only option).

## Why
The ten remaining variants complete the walkthrough's variant set (Parts 12–13), each teaching a specific point (e.g. anaemia, new AF, obesity/airway, penicillin allergy, pregnancy, minor consent, capacitous refusal).

## How to implement
1. Same process as P0 case transcription (walkthrough Parts 12–13; inventory D459–D474 as checklist).
2. Verbatim check 100%; register rows.
3. Variant-specific rules recorded as content (e.g. maternity early-warning placeholder, minor consent per institution).

## Verification
Validator + Stage B green; verbatim 100% per case.

## Files changed
`content/cases/VAR-S1-{I,K,L,M}.json`, `content/cases/VAR-S2-{E,H,I,J,K,L}.json`, register rows, verbatim-check output.

## Dependencies
p0-case-library-s01.

## Common issues
Two different 'Mr Imran' patients and other ID collisions (WT-I01) — check namespaced IDs.

## Sources
Walkthrough Parts 12–13; inventory D459–D474; Change Report WT-I01.

## Notes (16 Oct 2026)
- **Already built before this step was started:** the ten case files (`content/case_files/VAR-S1-{I,K,L,M}.json`, `VAR-S2-{E,H,I,J,K,L}.json`) were written by Ruhan Kamran on 10 Oct 2026 with the other 43 (`tools/content/cases/p1v.py`, built by `tools/content/build_case_files.py`, commit 2f12adb), and are APPROVED_FOR_TRAINING on the owner's attestation. The project keeps cases as case files, not as `content/cases/` cards (only VAR-S1-A has a card), so the step's file list is met by `content/case_files/`. Checked on 16 Oct 2026 and found already in place: the book's story of each case is checked word for word against its page (`content/case_seeds.csv`, rows D459 to D474 of the inventory); every case cites its source-register rows (`sources`: story and authoring); Stage B passes all 53 case files and rejects all 60 mutated ones (CI); `case_file_checks.py` fails on a repeated name or record number across cases, which covers WT-I01 (the two Imrans are Imran Siddiqui, STK-S1-A, and Imran Sheikh, VAR-S1-D); the variant rules are content: the maternity early-warning placeholder (`content/physio/pregnancy.json`, `maternity_score`; VAR-S2-H is `pregnant`), the minor's consent (`content/consent/consent.json`, `minor_age` 18 and `minor_no_senior`) and capacity and refusal (the same file).
- **Built (branch myt1x54/p1-case-library-extended-s01):** the missing piece, "verbatim 100% per case": `tools/content/variant_facts.py` ties each of the 47 facts the book's stories give the ten cases (names, ages, every number, the allergy reactions, the dipstick, the pregnancy) in the book's own words to the case-file field that holds it, and writes `docs/research/p1_variant_verbatim.md`; in CI with `--check`, and 4 tests in `tools/ci/test_variant_facts.py` (a changed value and a phrase that is not the book's are caught). Result: **47 of 47, 100% for every case.**
- **Found:** VAR-S2-L gives the APPAC result as "about two in five ... over five years" where the book says "39.1% recurrence over 5 years"; the report lists it as reworded (the consent content and VAR-S2-PC give the exact figure). Changing the case's text would drop its approval until it is attested again, so it is left for the owner to decide.
- **Decisions that are the project team's, not the book's:** the check counts a fact as the book's only if its words are in the verbatim-checked story; a plain-words rewording is allowed but always listed; the age of a minor is a rule for all cases (consent content), not a field of one case.
- **Approved by Abdul Moiz (owner), 16 Oct 2026.** The VAR-S2-L wording stays as it is (no change requested), listed as reworded in the report.
- **Not done here:** golden runs and VR play of these cases (p1-case-library-extended-s04); engine additions they need (s03, EXP-CASE-3).
