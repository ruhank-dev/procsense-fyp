# ADR-0004: Imaging library, the engine picks the finding and never edits a picture

**Status:** Accepted 10 October 2026 (owner decision) · **Related:** ADR-0003

## Context
Tests that return a picture (ultrasound, ECG, chest X-ray, CT, MRI, MRCP) must give a picture and a report that agree with the case and with each other, replay identically, and cost no clinician time per session.

## Decision
- Store a **library**: for every test, the **clean (no problem) result** and **every problem result**, each with at least two pictures and a matching report (`content/imaging_library.csv`, generated).
- A case lists only the problem results it is built around (`content/case_imaging.csv`); any test it does not list returns that test's clean result. A scenario event may change the finding code during a session; that is the only way a result changes.
- The engine (and the AI that picks scenarios) chooses a **finding code** only. A picture with that code is picked with the session seed, avoiding the one just shown; only pictures at the status the mode allows are eligible. The AI never edits or generates a picture at run time, and may only vary report wording within the approved finding.
- Pictures are reused across cases and sessions. New pictures are added to the library offline, checked (`validate_images.py`) and approved before they can be picked.
- Real pictures are preferred; synthetic ones are labelled (ADR-0003). Clean and problem pictures of one test should look alike, so painted-into-real is preferred over drawn-in-code where a real base exists.

## Results without pictures
The pregnancy test, the urine dipstick and the look of a blood sample are **results only** (owner decision, 10 October 2026): the engine picks a finding code as for any test and shows its written result from `content/bedside_results.csv`; there is no picture to pick and nothing to approve. (An earlier version drew pictures of them in code; they were removed.)

## Coverage rule
Every finding code must be produced by some case (`case_imaging`), some what-if (`whatif_imaging`) or be a test's clean default, every what-if that names the ECG or imaging must map to at least one picture result, and every result keeps at least four pictures. CI checks all three. The written-only results are checked for text and for being produced by a case or what-if.

## Consequences
Adding a scenario never needs new code, only a finding code with pictures. The library is checked in CI (every test has a clean result, problems, and at least two pictures each; case rows are valid). Known weak spot: MRCP is drawn in code and looks diagrammatic; a real MRCP source or a better generator would replace it.

## Revisit if
A licensed real source appears for CT/MRCP problem results, or live generation of pictures is wanted.
