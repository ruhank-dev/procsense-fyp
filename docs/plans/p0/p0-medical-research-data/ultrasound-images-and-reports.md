# Feature plan: ultrasound images with matching reports

**Phase:** p0-medical-research-data (steps s14 to s17, stage D11 of [data-first-order.md](../data-first-order.md)) · **Decision:** [ADR-0003](../../../decisions/ADR-0003-synthetic-medical-images.md) · **Status:** planned 10 Oct 2026, nothing built or approved yet

## Goal
When the trainee orders an ultrasound, the result comes back as a **report and a matching image** in the EMR viewer (and the printed report in the folder),
for every case that has an ultrasound. The report and the image are always about the same finding.

## What the walkthrough defines (the master source)
Findings come from the book, not from the images.

| Scenario | Finding code | Report category (Walkthrough Part 6.3 / 6.4) |
|---|---|---|
| S1 gallbladder | US-GB-01 | Simple gallstones: wall 3 mm or less, no fluid, bile duct 6 mm or less |
| | US-GB-02 | Inflamed gallbladder (acute cholecystitis): wall over 4 mm, fluid around it, Murphy's sign, enlarged, stone in the neck |
| | US-GB-03 | Possible blockage of the duct: bile duct wider than 6 mm |
| | US-GB-04 | Stone seen in the bile duct (high risk) |
| | US-GB-05 | Severe complicated inflammation: gas in the wall, pus pocket, perforation |
| | US-GB-06 | Possible gallbladder cancer: polyp 10 mm or larger, mass, calcified wall |
| | US-GB-07 | Sludge only, no stones (diagnosis less certain) |
| S2 appendix | US-AP-01 | Positive, simple: appendix over 6 mm, will not compress, inflamed fat, maybe an appendicolith |
| | US-AP-02 | Positive, complicated: gas or hole outside the appendix, abscess, inflammatory lump, fluid |
| | US-AP-03 | Negative, something else found (for example ovarian cyst, kidney stone, inflamed bowel) |
| | US-AP-04 | Non-diagnostic / equivocal: appendix not seen, borderline size. **Not the same as negative.** |
| | US-AP-05 | Normal appendix, nothing else |

Each case's ultrasound result is one of these codes (VAR-S1-A's clinic ultrasound is US-GB-01). The code, not the picture, drives what the Core engine does next.

## Pipeline (in this order)
1. **Find real images first.** Candidates from the inventory: UIdataGB gallbladder ultrasound (Mendeley Data, D40) for S1. For S2 the only open dataset known is the Regensburg paediatric set (D42), which is **children only** and is not used for adult cases, so S2 is expected to need the fallback.
2. **Check each dataset before use** (the dataset protocol): open the page, read the licence and what the images really are (classes, counts, population), record it in `docs/research/dataset_register.csv`. A dataset whose licence does not allow this use is not used.
3. **Map dataset classes to finding codes.** Only classes that clearly match a code are used. An unclear match is not used. The mapping is written down per class with the reason.
4. **Select and prepare** a small number of images per code: strip metadata, remove any burned-in patient text, downscale to the size the viewer needs (keeps the APK small), keep attribution.
5. **Generate the matching report** from the finding code: the sentences come from the book's category (and the numbers it names, for example "wall 2 mm; duct 4 mm"); AI may only vary the wording, never the finding or a number. The report records its finding code and the image id.
6. **Fallback: synthetic image** only for a code with no suitable real image (expected: S2, and possibly some S1 codes). Method and limits below.
7. **Validate** (automatic): every image has a report with the same finding code; every report states only what its finding code allows (the validator checks the numbers against the band for that code); no image is used without a licence or generator record; synthetic images carry the visible mark; no embedded metadata.
8. **Clinician review**: a review pack lists every image next to its report, source or generator, and the questions to answer. Status moves `DRAFT` to `REVIEWED` to `APPROVED_FOR_TRAINING`. Unapproved images fall back to the text report.
9. **Integrate**: `content/images/index.json` (image, finding code, case(s), source, licence, synthetic flag, status) and `content/imaging_reports.json`; the EMR image viewer and the printed report read them by id (p0-clinical-logic s09, p0-patient-records-emr, p0-diagnostic-transitions).

## Synthetic images: what is honest to promise
- A **real image dataset is always preferred.** Synthetic images are a stand-in for teaching, not a replacement for a real scan.
- I can build **stylised, procedural** ultrasound-style pictures in code (speckle texture, an anatomy sketch with the finding drawn in: wall thickness, stone with shadow, dilated duct, fluid, an appendix cross-section). Each is clearly an illustration, deterministic from a seed, and checkable against its finding code (the measurements drawn are the measurements in the report).
- **Photo-realistic synthetic ultrasound** needs a trained generative image model (and usually a GPU). None is bundled. If the owner wants that level of realism, it is a separate decision with a licence check on the model, and it still goes through the same review. Until then, the stylised version is used and labelled as such.
- Every synthetic image has the visible mark "SIMULATION: synthetic image, not a real scan", `synthetic: true`, generator, version and seed.
- Synthetic images are never used to train or test any diagnostic AI.

## Tests
- Every finding code has at least one report; every image has exactly one finding code and a report with the same code.
- The numbers in a report fall inside the band of its finding code (for example US-GB-03 reports a duct wider than 6 mm).
- Every image in the index has a licence or generator record and an approval state; a synthetic one has the mark (checked on the pixels).
- `APPROVED_FOR_TRAINING` is required to show an image in a session; otherwise the viewer gets the text report (test with a `DRAFT` image).
- Regeneration is deterministic: the same seed gives the same file hash.
- The review pack lists every image and report pair, and CI fails if one is missing from it.

## Risks and how they are handled
| Risk | Handling |
|---|---|
| A dataset class is mislabelled (labels were created by text mining or non-experts) | Only clearly matching classes; clinician approves each image |
| A real dataset's licence forbids this use or needs registration | Not used; recorded in the register; fallback applies |
| A synthetic image is mistaken for a real scan | Visible mark, flag, card, viewer label |
| Report and image disagree | Both come from the same code; the validator checks it |
| An image carries hidden patient data | Metadata stripped, burned-in text checked, no raw rows |
| The viewer shows an unapproved image | Status gate, tested |

## Decisions the owner may want to make later
- Whether stylised synthetic images are acceptable for the demo, or a licensed generative model should be evaluated.
- Which clinician approves the images, and when (D13 review pack).

## Files this feature adds
`content/images/index.json`, `content/imaging_reports.json`, `content/images/<code>/…`, `tools/data/ultrasound_*.py`, dataset and generator cards in `docs/research/datasets/`, rows in `docs/research/dataset_register.csv` and `docs/research/source_register.csv`.

## Dependencies
D9 (which finding each case needs), p0-clinical-logic s09 (imaging categories), p0-medical-research-data s16 (image datasets). Feeds p0-patient-records-emr and p0-diagnostic-transitions.
