# Imaging scenarios: every scan a P0 case could show

Written 10 October 2026 after the owner asked to cover all the scans a doctor might look at, with the clinician approving the list. **Update 10 October 2026 (later):** the owner asked for the whole library to be built (search online first; where nothing is found, generate a realistic labelled fake) and stated that the clinician approves everything. All the codes below are now built and approved for training on that statement (`content/image_approvals.json`, tied to each exact picture). Codes marked *proposed* are still my proposals from where the walkthrough names a scan, so the named clinician should confirm them before any assessment use.

Rules for every picture (ADR-0003): real licensed image first; otherwise a labelled synthetic one; a report that states only what the picture supports; DRAFT until a clinician approves it.

## What the walkthrough needs, by test, and what is built (10 October 2026, final)

Every result the book's cases, scenario cards and what-ifs can produce has a stored picture with a matching report; the engine picks the finding (ADR-0004), never edits a picture. `content/imaging_library.csv` lists every result with its pictures (60 results, 14 tests, 204 pictures for the picture tests, at least 4 per result, plus 9 written-only results for the pregnancy test, dipstick and blood sample appearance); `content/case_imaging.csv` says which problem each case gives; `content/whatif_imaging.csv` says which finding each what-if can produce for each cause.

| Test | Results (clean first) | Pictures | Source |
|---|---|---|---|
| Gallbladder ultrasound | US-GB-00 normal; 01 stones; 02 inflamed; 03 wide duct; 04 duct stone; 05 burst/gas; 06 mass; 07 sludge; 08 equivocal | 36 | real UIdataGB for 01, 02, 05, 06; drawn for the rest |
| Appendix ultrasound | US-AP-05 normal; 01 simple; 02 complicated; 03 other finding; 04 not seen | 20 | drawn in code |
| Pelvic ultrasound | US-PV-00 normal; 01 ectopic; 02 ovarian torsion | 12 | drawn in code |
| Kidney ultrasound | US-RN-00 normal; 01 hydronephrosis with stone | 8 | drawn in code |
| ECG | ECG-01 sinus; 02 tachycardia; 03 AF; 04 bradycardia; 05 heart block; 06 SVT; 07 ST elevation; 08 high potassium; 09 low potassium; 10 fast AF | 40 | real PTB-XL for 01 to 07; real strips edited in code for 08 to 10 |
| Chest X-ray | CXR-01 no finding; 02 consolidation; 03 effusion; 04 pneumothorax; 05 cardiomegaly; 06 oedema; 07 atelectasis; 08 free air | 32 | real NIH ChestX-ray14 for 01 to 07; painted into a real film for 08 |
| CT, right iliac fossa | CT-AP-00 clean; 01 appendicitis; 02 abscess; 03 caecal mass; 04 diverticulitis | 20 | real AMOS slices; findings painted in |
| CT, gallbladder level | CT-GB-00 clean; 01 emphysematous cholecystitis | 8 | real AMOS slices; findings painted in |
| CT, mid abdomen | CT-AB-00 clean; 01 bowel obstruction; 02 free air | 12 | real AMOS slices; findings painted in |
| MRI, appendix level | MR-AP-00 clean; 01 appendicitis | 8 | real AMOS slices; finding painted in |
| MRCP | MRCP-00 normal; 01 duct stone | 8 | drawn in code (diagrammatic) |
| Pregnancy test | PREG-00 negative; 01 positive | result only (no picture, owner decision) | written result |
| Urine dipstick | DIP-00 normal; 01 infection; 02 glucose and ketones; 03 blood | result only | written result |
| Blood sample appearance | TUBE-00 acceptable; 01 haemolysed; 02 lipaemic | result only | written result |

ERCP is a procedure and the book shows no picture result for it. The book's equipment list already decided that CT and MRCP are report-first, so a picture is a key image next to the report.

## Where real pictures could come from, and what I found

- **CT:** no open set that matches appendicitis or cholecystitis and allows reuse was found. The RSNA abdominal trauma set is Kaggle and non-commercial; the abdominal organ-segmentation sets show anatomy, not these diseases. Neither would give a *matching* report.
- **MRCP:** the open sets found (Zenodo, CC BY) are raw scanner data (HDF5) for image-reconstruction research, not viewable studies with reports.
- **MRI appendix:** nothing found.
- **ECG changes:** PTB-XL has ST-elevation-type labels (myocardial infarction classes) that could be added after the clinician names the finding; it has no potassium labels.

## Already decided (equipment list rows 22 to 24)
CT is **report-only / a door transition** (no CT room) and MRCP/ERCP are **report only**; ultrasound gives a report plus images in the EMR. So a CT, MRI or MRCP *picture* is an optional "key image" next to the report, never required for a case to be playable.

## What was built instead of waiting (owner's rule: search first, then generate)
Search result: no open dataset with matching reports for these tests (MRCP sets on Zenodo are raw scanner data; open CT sets cover anatomy or trauma; CHAOS is non-commercial share-alike; case-report figures are one-off rare cases). AMOS 2022 (CC BY 4.0) gives real CT and MRI slices, so: clean pictures are real slices; problem pictures are the same kind of real slice with the finding painted in (flagged synthetic, marked); MRCP is drawn in code. Library design: `docs/decisions/ADR-0004-imaging-library.md`.

## Options that were considered for the missing ones

1. **Clinician-supplied images** from teaching files they are allowed to share (best realism; the clinician owns the licence question).
2. **Labelled synthetic schematics** under ADR-0003 (a stylised drawing with the SIMULATION mark): acceptable for teaching a *decision* ("order MRCP, then read it"), far less realistic than a scan; clinician must accept that simplification.
3. **Text-only report** with the line "image not available in this build": the trainee still gets the result and the decision stays testable; no picture.

Until one is chosen, the engine treats an ordered CT, MRI or MRCP as returning the **report text only** from the case file once the clinician writes it; the picture is added later without changing any case.

Tracked as DT-061.
