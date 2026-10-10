"""Writes docs/research/dataset_register.csv and one dataset card per dataset in docs/research/datasets/ from the committed extraction results.

    uv run --project tools tools/data/dataset_cards.py            # rewrite
    uv run --project tools tools/data/dataset_cards.py --check    # fail if out of date (CI)

The register says, for every dataset, where it is, its licence, how it is accessed, what was actually opened and checked, and whether it is used.
`contents_checked` is "yes" only for what was opened on the date given. A dataset that was identified but not opened is listed as NOT VERIFIED, never as verified.
Numbers on the cards come from tools/data/derived/*.json and content/images/index.json.
"""
from __future__ import annotations

import csv
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DERIVED = Path(__file__).resolve().parent / "derived"
CARDS = ROOT / "docs" / "research" / "datasets"
REGISTER = ROOT / "docs" / "research" / "dataset_register.csv"
TODAY = "2026-10-10"
HEADER = ["id", "name", "purpose", "url", "licence", "access", "contents_checked", "synthetic", "status", "date_checked", "notes"]


def j(name: str) -> dict:
    return json.loads((DERIVED / name).read_text(encoding="utf-8"))


def build() -> tuple[list[list[str]], dict[str, str]]:
    vdb, ptb, mimic, ed = j("vitaldb_noise.json"), j("ptbxl_ecg_templates.json"), j("mimic_lab_percentiles.json"), j("mimic_ed_triage.json")
    images = json.loads((ROOT / "content" / "images" / "index.json").read_text(encoding="utf-8"))["images"]
    real = [i for i in images if not i["synthetic"]]
    synth = [i for i in images if i["synthetic"]]
    classes = sorted({i["source"]["class"] for i in real})
    label = json.loads((DERIVED / "openfda_label_safety.json").read_text(encoding="utf-8"))
    xs = json.loads((ROOT / "content" / "xsec" / "index.json").read_text(encoding="utf-8"))["images"]
    xs_real, xs_synth = [i for i in xs if not i["synthetic"]], [i for i in xs if i["synthetic"]]
    images_cxr = json.loads((ROOT / "content" / "cxr" / "index.json").read_text(encoding="utf-8"))["images"]
    rows = [
        ["DS-0001", "VitalDB", "Vital-sign noise (spread and AR(1) autocorrelation) for the monitor and LitePhysio", "https://vitaldb.net/dataset (open API https://api.vitaldb.net)", "CC BY 4.0",
         "Open API, no account", f"yes: case table 6,388 cases and track list read; {vdb['cases_selected']} planned ASA 1-2 general-surgery cases sampled", "false", "USED (aggregates only)", TODAY,
         "Intra-operative, so the values describe stable monitored patients, not an awake bay patient. Cite Lee et al., Sci Data 2022. Card: datasets/DS-0001-vitaldb.md"],
        ["DS-0002", "PTB-XL 1.0.3", "Lead II beat templates for sinus rhythm, sinus tachycardia and atrial fibrillation (monitor trace)", "https://physionet.org/content/ptb-xl/1.0.3/", "CC BY 4.0",
         "Anyone (PhysioNet open access)", f"yes: ECG strips drawn from real recordings too (content/ecg); metadata file and {sum(c['records_used'] for c in ptb['classes'].values())} recordings read; licence page read", "false", "USED (aggregates only)", TODAY,
         "Cite Wagner et al., Sci Data 2020. Also the source of the 28 real ECG rhythm strips (sinus rhythm, tachycardia, bradycardia, atrial fibrillation, heart block, SVT, ST elevation) and the base of 12 edited ones, with reports, in content/ecg (approved for training on the owner's statement). Card: datasets/DS-0002-ptbxl.md"],
        ["DS-0003", "MIMIC-IV Clinical Database Demo 2.2", "Plausibility check of lab bands and generated results (adult hospital patients)", "https://physionet.org/content/mimic-iv-demo/2.2/", "ODbL 1.0",
         "Anyone (PhysioNet open access)", f"yes: whole archive read (100 patients); percentiles for {len(mimic['labs'])} tests", "false", "USED (reference and tests only, not shipped)", TODAY,
         "Hospital patients, so wider than a healthy reference range. ODbL: attribution required, share-alike for derived databases (why nothing is shipped). Card: datasets/DS-0003-mimic-iv-demo.md"],
        ["DS-0004", "MIMIC-IV-ED Demo 2.2", "Triage vitals of ED patients with abdominal complaints (S2 arrival realism)", "https://physionet.org/content/mimic-iv-ed-demo/2.2/", "ODbL 1.0",
         "Anyone (PhysioNet open access)", f"yes: triage table read ({ed['triage_rows']} rows, {ed['abdominal_pain_rows']} abdominal)", "false", "USED (reference and tests only, not shipped)", TODAY,
         "Very small sample. Card: datasets/DS-0003-mimic-iv-demo.md"],
        ["DS-0005", "UIdataGB (Gallblader Diseases Dataset) v2", "Real gallbladder ultrasound images for the EMR viewer", "https://data.mendeley.com/datasets/r6h24d2d3y/2", "CC BY 4.0",
         "Open download, no account", f"yes: file list and licence read; {len({i['source']['archive'] for i in real})} class archives downloaded and checksum-verified; images looked at", "false",
         f"USED ({len(real)} images, approved for training on the owner's statement)", TODAY,
         f"Classes used: {', '.join(classes)}. No images for bile-duct findings or sludge. Labels were not clinician-checked by us. Attribution required. Card: datasets/DS-0005-uidatagb.md"],
        ["DS-0006", "NIH ChestX-ray14", "Real chest X-rays for the EMR viewer (consolidation, effusion, pneumothorax, cardiomegaly, oedema, atelectasis, no finding) and the base of the free-air film", "https://nihcc.app.box.com/v/ChestXray-NIHCC (copy used: https://huggingface.co/datasets/BahaaEldin0/NIH-Chest-Xray-14)",
         "NIH Clinical Center terms: no restrictions on use, attribution required (read in the Google Cloud and RSNA documentation; the NIH page itself is script-driven and could not be read; the Hugging Face copy states no licence)",
         "Open download; only one 450 MB shard of the 45 GB copy is used", f"yes: one shard read ({len([i for i in images_cxr])} pictures chosen by eye from contact sheets); labels only, no radiology reports", "false",
         f"USED ({len(images_cxr)} images, approved for training on the owner's statement)", TODAY,
         "Labels are text-mined and not clinician-checked, so reports state only what the label supports. Patient id, age and sex are not copied. Oedema rarely stands alone, so its pictures also carry the usual companion labels. Four shards (about 4,500 films) were read. The NIH Clinical Center news release (27 September 2017) says the dataset is freely accessible to the scientific community; the 'no restrictions, attribution required' wording comes from the Google Cloud and RSNA documentation; the Box page itself is script-driven and could not be read. Card: datasets/DS-0006-nih-chestxray14.md"],
        ["DS-0007", "openFDA Drug Label API", "Contraindication and boxed-warning wording for the medicine groups (never doses)", "https://open.fda.gov/apis/drug/label/", "CC0 1.0 (terms page read)",
         "Open API, no account", f"yes: {len(label['medicines'])} labels queried and read (tools/data/extract_openfda.py); sentences with doses dropped", "false", f"USED ({len(label['medicines'])} medicine groups, wording only)", TODAY,
         "US labels as stand-ins for the UK product of the same group; aspirin and adrenaline returned no usable label. Card: datasets/DS-0007-openfda.md"],
        ["DS-0008", "Regensburg Pediatric Appendicitis Dataset", "Reference for appendicitis score distributions", "https://archive.ics.uci.edu/dataset/938/regensburg+pediatric+appendicitis", "not confirmed (the UCI page says 'see linked dataset', Zenodo 7669442)",
         "Open download", "partly: page read", "false", "NOT USED", TODAY, "Children only: not suitable for the adult S2 cases. Licence still to confirm if it is ever used."],
        ["DS-0009", "PriMock57", "Medical conversation audio for the speech-recognition evaluation (P1)", "https://github.com/babylonhealth/primock57", "not checked", "not checked", "no", "false", "NOT NEEDED", TODAY, "Decision 10 Oct 2026: speech recognition uses an existing recogniser (for example Whisper or a cloud service), not a dataset we train on; a short authored phrase list recorded by the team is enough for the P0/P1 checks. Reconsider if accent robustness must be measured."],
        ["DS-0010", "Mozilla Common Voice (accented English)", "Accent robustness for speech recognition (P1)", "https://commonvoice.mozilla.org/en/datasets", "not read (script-driven page)", "not checked", "no", "false", "NOT NEEDED", TODAY,
         "Decision 10 Oct 2026: not needed; existing speech recognisers already cover accents. Optional later for measuring accent robustness."],
        ["DS-0011", "ProcSense synthetic ultrasound set", "Labelled stand-in images where no real image exists (ADR-0003): ultrasound, ECG patterns, free-air chest film", "content/images (built by tools/data/ultrasound.py)", "ProcSense generated",
         "In the repository", f"yes: {len(synth) + len(xs_synth)} images rendered and looked at; mark read back from the pixels by CI", "true", "APPROVED_FOR_TRAINING (owner-attested)", TODAY,
         "Ultrasound: stylised procedural illustrations. CT/MRI: findings painted into real AMOS slices. MRCP: drawn entirely in code (clearly diagrammatic). Never a trained generative model; every image carries the visible mark. Card: datasets/DS-0011-synthetic-ultrasound.md"],
        ["DS-0013", "Indiana University Chest X-ray Collection (NLM Open-i)", "Real chest X-rays with radiology reports (for EMR if a case needs one)", "https://openi.nlm.nih.gov/", "CC BY-NC-ND 4.0 (read from the licence field of the report files)",
         "Open download (images 1.3 GB, reports 1 MB)", "yes: the 1 MB report archive downloaded and read; images not downloaded", "false", "REJECTED (licence)", TODAY,
         "NoDerivatives forbids sharing resized or re-encoded copies and NonCommercial limits later use, so images cannot ship in the build. No P0 case shows a chest X-ray result (the S1 book case lists it as 'not ordered'). Reconsider only with written permission or a clinician-approved synthetic set."],
        ["DS-0014", "AMOS 2022 (abdominal CT and MRI)", "Real CT and MRI slices as the clean pictures, and as the base the synthetic findings are painted into (appendix level, gallbladder level)",
         "https://zenodo.org/records/7155725 (Ji et al., NeurIPS 2022, doi:10.5281/zenodo.7155725)", "CC BY 4.0 (read from the Zenodo record)", "Open download; single scans read out of the 24 GB archive with HTTP range requests",
         f"yes: archive index read, {len({i['source']['class'].split(' slice')[0] for i in xs_real}) + 4} scans downloaded and looked at; {len(xs_real)} real slices used", "false",
         f"USED ({len(xs_real)} real clean pictures; the base of {len([i for i in xs_synth if i['generator']['base']])} painted ones)", TODAY,
         "Patients have many conditions and the dataset gives no per-slice diagnosis, so slices were chosen by eye and the clinician confirms they are clean. MRI appears to be post-contrast T1 fat-suppressed (not stated by us as fact). Card: datasets/DS-0014-amos.md"],
        ["DS-0012", "ProcSense authored EMR distractor identities", "Search-the-right-patient task in the EMR", "content/emr_distractors.csv", "ProcSense authored", "In the repository", "yes", "true", "DRAFT",
         TODAY, "Invented names, dates and numbers; no clinical content. Card: datasets/DS-0012-emr-distractors.md"],
    ]
    cards = {
        "DS-0001-vitaldb.md": f"""# DS-0001 VitalDB: vital-sign noise

**Source:** {rows[0][3]} · **Licence:** CC BY 4.0 · **Cite:** {vdb['citation']}
**Checked:** {TODAY}: the open API was opened; the case table ({vdb['eligible_cases']} eligible cases after the selection) and the track list were read.

## Purpose
Make the monitor numbers move like a real monitored patient: how much each vital sign wanders around its own slow level, and how strongly one reading follows the previous one.

## Method (`tools/data/extract_vitaldb.py`, reproducible)
{vdb['selection']}; {vdb['cases_selected']} cases, chosen deterministically. For each vital sign: sigma = spread of the signal after removing its rolling median ({vdb['window_samples']} samples); phi = lag-1 autocorrelation (the AR(1) coefficient); plus percentiles of each patient's typical level. Blood pressure is an intermittent cuff reading, so one value per reading is used. {vdb['nibp_note']}.

## What ships
Only the aggregates in `content/physio_noise.csv` (one row per vital sign, with a register row each). No patient row, no signal.

## Results
{chr(10).join(f"- {k}: sigma {v['sigma_median']}, phi {v['phi_median']}, typical level {v['level_p5']} to {v['level_p95']} {v['unit']} ({v['cases']} cases)" for k, v in vdb['variables'].items())}

## Limits
{vdb['limitation']}. Clinician plot review (EXP-DATA-1) is still pending, so the register marks these rows as pending approval.
""",
        "DS-0002-ptbxl.md": f"""# DS-0002 PTB-XL: ECG beat templates

**Source:** {rows[1][3]} · **Licence:** CC BY 4.0 · **Cite:** {ptb['citation']} (doi {ptb['doi']})
**Checked:** {TODAY}: the licence page was read; the metadata file and the recordings listed below were downloaded.

## Purpose
Beat shapes for the monitor trace of three rhythms.

## Method (`tools/data/extract_ptbxl.py`, reproducible)
Human-validated recordings with exactly one rhythm label (no pacemaker), up to {ptb['per_class_records']} per rhythm, picked evenly by record id. Lead II at {ptb['sampling_hz']} Hz; R peaks found; beats aligned on the peak ({ptb['samples_before_r']} samples before, {ptb['samples_after_r']} after) and averaged; normalised to the R peak. Heart rate and beat-to-beat variation of the class are kept.

## What ships
One short averaged template per rhythm in `content/physio_ecg_templates.csv`. No recording.

## Results
{chr(10).join(f"- {k}: {c['records_used']} recordings, {c['beats_averaged']} beats, heart rate median {c['heart_rate_median']} (5th to 95th percentile {c['heart_rate_p5']} to {c['heart_rate_p95']}), beat-interval variation {c['rr_variation_cv_median']}" for k, c in ptb['classes'].items())}

## ECG strips
`tools/data/ecg_images.py` also draws real 10-second lead II strips, four per code, for sinus rhythm, sinus tachycardia, atrial fibrillation, sinus bradycardia, complete heart block, supraventricular tachycardia and ST elevation (inferior infarction pattern; the ST level is measured from the signal, at least 1 mm), with a report stating the label and the heart rate measured from the signal. Hyperkalaemia, hypokalaemia and fast atrial fibrillation have no labelled open recordings, so those strips are real ones edited in code (peaked T waves; flat T with a U wave; a real atrial fibrillation strip sped up to about 132 per minute), flagged synthetic and marked. They are in `content/ecg/` with attribution (CC BY 4.0), DRAFT until a clinician approves them.

## Limits
A single averaged beat has no P wave detail beyond what averaging keeps; atrial fibrillation is drawn from the same template with irregular intervals (the variation figure). Clinician review of the traces is pending.
""",
        "DS-0003-mimic-iv-demo.md": f"""# DS-0003 / DS-0004 MIMIC-IV demo and MIMIC-IV-ED demo: plausibility

**Source:** {rows[2][3]} and {rows[3][3]} · **Licence:** ODbL 1.0 · **Checked:** {TODAY}: both archives downloaded and read.

## Purpose
Check that the walkthrough's bands and our generated values are plausible for adult hospital patients. Nothing here becomes game content.

## Method (`tools/data/extract_mimic.py`)
Percentiles of all results of {len(mimic['labs'])} tests in {mimic['patients']} demo patients, converted with the walkthrough's own unit factors; triage vitals of the {ed['abdominal_pain_rows']} abdominal-complaint rows of {ed['triage_rows']} ED triage rows.

## What ships
`content/lab_plausibility.csv` and `content/ed_triage_abdominal.csv` hold percentiles only and are used by tests, not by the app. ODbL requires attribution and share-alike for derived databases, so no row-level data is kept.

## Limits
Hospital patients are sicker than the 'healthy reference' of a lab band, so these ranges are wider; the ED sample is tiny.
""",
        "DS-0005-uidatagb.md": f"""# DS-0005 UIdataGB: real gallbladder ultrasound images

**Source:** {rows[4][3]} · **Licence:** CC BY 4.0 · **Cite:** Turki A., Obaid A.M., Bellaaj H. et al., Gallbladder Diseases Dataset, Mendeley Data V2, doi:10.17632/r6h24d2d3y.2
**Checked:** {TODAY}: file list and licence read; the class archives used were downloaded (`tools/data/fetch_uidatagb.py`) and each matched its published SHA-256.

## Purpose
Real images next to the matching reports in the EMR viewer.

## Method
Classes are mapped to the book's finding codes only where the match is clear: Gallstones to US-GB-01; cholecystitis to US-GB-02; membranous and gangrenous cholecystitis and perforation to US-GB-05; carcinoma to US-GB-06. The dataset has no images for bile-duct findings (US-GB-03, US-GB-04) or sludge only (US-GB-07). A few images per class were picked from a contact sheet checked by eye for burned-in text; each is downscaled to 640 x 480 and re-encoded as JPEG, which removes EXIF and ICC data.

## What ships
{len(real)} images in `content/images/`, listed in `content/images/index.json` with their archive, member, licence and attribution; reports in `content/imaging_reports.json`.

## Limits
The dataset labels were not checked by a clinician for this project, and a class does not prove a measurement. Every image is shown only at the status recorded in `content/image_approvals.json` (approved for training on the owner's statement that the clinician approves everything; the clinician is not named). Attribution is required wherever an image is shown.
""",
        "DS-0006-nih-chestxray14.md": f"""# DS-0006 NIH ChestX-ray14: chest X-rays

**Source:** NIH Clinical Center, https://nihcc.app.box.com/v/ChestXray-NIHCC · **Cite:** Wang X., Peng Y., Lu L., Lu Z., Bagheri M., Summers R., ChestX-ray8, IEEE CVPR 2017, pp. 3462-3471.
**Licence:** NIH states no restrictions on use with attribution (link to the NIH site, cite the paper, acknowledge the NIH Clinical Center). This was read in the Google Cloud and RSNA documentation because the NIH page is script-driven; the Hugging Face copy used for download states no licence. **Re-confirm on the primary page before release.**
**Checked:** {TODAY}: one parquet shard (test-00000-of-00010, 450 MB, about 1,100 images) downloaded and read; contact sheets looked at.

## What we need from the 45 GB
Almost nothing. Each finding needs a few pictures, so one shard is enough. The 45 GB is only the full collection (112,120 images). The original download site offers `Data_Entry_2017_v2020.csv` (labels) and `images_001.tar.gz` to `images_012.tar.gz`; the Hugging Face copy splits the same images into 93 parquet shards.

## Method (`tools/data/cxr_images.py`)
Images with exactly one label and the PA view; seven finding codes (CXR-01 to CXR-07) of real films plus CXR-08 (free air under the diaphragm, painted into a real film, flagged synthetic and marked); four pictures each chosen by eye; resized to 640 x 640 and re-encoded from pixels (no metadata). Patient id, age and sex are not copied.

## What ships
{len(images_cxr)} pictures in `content/cxr/` with reports that state only what the label supports. The dataset has no radiology reports.

## Limits
Labels were text-mined from reports and are not clinician-checked (some pictures may not show the labelled finding clearly). The book has no chest X-ray report categories, so the codes are proposals. Approved for training on the owner's statement (see `content/image_approvals.json`).
""",
        "DS-0011-synthetic-ultrasound.md": f"""# DS-0011 Synthetic ultrasound set

**Generator:** `tools/data/ultrasound_synth.py` (stylised procedural illustration, deterministic from a seed) · **Decision:** ADR-0003

## Purpose
A labelled stand-in picture, with a matching report, for finding codes with no suitable real image: {', '.join(sorted({i['finding_code'] for i in synth}))}.

## Method
A fan-shaped field with speckle texture, a simple anatomy sketch and the finding drawn at the size the report states; the calipers in the picture show the number the report uses. The mark "SIMULATION: synthetic image, not a real scan" is drawn into every picture and CI re-reads it from the pixels.

## What ships
{len(synth)} images in `content/images/`, flagged `synthetic: true` with generator and seed.

## Limits
These are teaching illustrations, much simpler than a real scan; not a trained generative model; never used to train or test any diagnostic model. Approved for training on the owner's statement (see `content/image_approvals.json`).
""",
        "DS-0007-openfda.md": f"""# DS-0007 openFDA drug labels: safety wording

**Source:** https://open.fda.gov/apis/drug/label/ · **Licence:** CC0 1.0 · **Checked:** {TODAY}: {len(label['medicines'])} labels queried.

## Purpose
Reference wording for contraindications and boxed warnings of the medicine groups, for the safety checks and the debrief. Never doses.

## Method (`tools/data/extract_openfda.py`)
One representative generic per medicine group; the newest single-ingredient label; only the contraindications and boxed-warning text; every sentence with a dose, rate or strength (a number with mg, mcg, g, mL, units, mEq, %) is dropped, as are section cross-references. The label's set id and effective date are kept.

## What ships
`content/drug_label_safety.csv` ({len(label['medicines'])} rows, one register row each).

## Limits
US labels, used as stand-ins for the UK product of the same group; aspirin and adrenaline returned no usable label; brand names can appear inside quotes. The clinician confirms local wording.
""",
        "DS-0014-amos.md": f"""# DS-0014 AMOS 2022: abdominal CT and MRI

**Source:** https://zenodo.org/records/7155725 · **Licence:** CC BY 4.0 · **Cite:** Ji Y. et al., AMOS: A Large-Scale Abdominal Multi-Organ Benchmark for Versatile Medical Image Segmentation, NeurIPS 2022 Datasets and Benchmarks, doi:10.5281/zenodo.7155725.
**Checked:** {TODAY}: the archive index was read (600 CT and MRI scans with 15-organ labels); ten scans were downloaded one member at a time (`tools/data/fetch_amos.py`, about 15 MB per CT, 3 MB per MRI) and looked at.

## Purpose
CT and MRI results for the appendix level, the gallbladder level and the clean (no problem) result of each test. No open dataset with matching reports exists for these tests (docs/research/imaging_scenarios.md).

## Method (`tools/data/xsec.py`)
- **Clean pictures:** a real slice, cropped, window-levelled (CT soft-tissue W400 L40) and re-encoded without metadata; {len(xs_real)} used.
- **Problem pictures:** the same kind of real slice with the finding painted in (inflamed appendix with fat stranding, abscess with gas bubbles, gas-containing thickened gallbladder wall, enhancing appendix wall on MRI) at the size the report states, using the scan's own voxel size and noise. Flagged synthetic, base slice named, visible SIMULATION mark. Because the base is real, clean and problem pictures look alike.
- The gallbladder wall painting is placed from AMOS's own gallbladder label.

## What ships
{len(xs_real) + len(xs_synth)} pictures in `content/xsec/` with attribution, plus the 6 MRCP pictures drawn in code.

## Limits
The patients have many conditions; slices were chosen by eye and the clinician confirms they show no abnormality in the region. The MRI sequence is not stated by the dataset for each scan. MRCP is clearly diagrammatic (drawn in code), the least realistic part of the library.
""",
        "DS-0012-emr-distractors.md": """# DS-0012 Authored EMR distractor identities

Invented names, dates of birth and numbers (two deliberately one digit away from Mrs Ayesha Khan's) so that searching for the right patient in the EMR is a real task.
They map to no real person and carry no clinical history. Flagged synthetic; in `content/emr_distractors.csv`.
""",
    }
    cards["README.md"] = "# Dataset cards\n\nOne card per dataset used (purpose, method, what ships, limits). The register is `../dataset_register.csv`. Generated by `tools/data/dataset_cards.py`; do not edit.\n\n" + "\n".join(
        f"- [{name}]({name})" for name in sorted(cards))
    return rows, cards


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    rows, cards = build()
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(HEADER)
    w.writerows(rows)
    files = {REGISTER: buf.getvalue(), **{CARDS / n: t.replace("\r\n", "\n") for n, t in cards.items()}}
    if "--check" in argv:
        stale = [str(p.relative_to(ROOT)) for p, t in files.items() if not p.exists() or p.read_text(encoding="utf-8").replace("\r\n", "\n") != t]
        for s in stale:
            print(f"{s} is out of date: run `uv run --project tools tools/data/dataset_cards.py`")
        return 1 if stale else 0
    CARDS.mkdir(parents=True, exist_ok=True)
    for p, t in files.items():
        p.write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote the dataset register ({len(rows)} datasets) and {len(cards)} card files")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
