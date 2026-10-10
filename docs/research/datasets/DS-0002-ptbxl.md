# DS-0002 PTB-XL: ECG beat templates

**Source:** https://physionet.org/content/ptb-xl/1.0.3/ · **Licence:** CC BY 4.0 · **Cite:** Wagner P, et al. PTB-XL, a large publicly available electrocardiography dataset. Sci Data 2020. (doi 10.13026/kfzx-aw45)
**Checked:** 2026-10-10: the licence page was read; the metadata file and the recordings listed below were downloaded.

## Purpose
Beat shapes for the monitor trace of three rhythms.

## Method (`tools/data/extract_ptbxl.py`, reproducible)
Human-validated recordings with exactly one rhythm label (no pacemaker), up to 25 per rhythm, picked evenly by record id. Lead II at 100 Hz; R peaks found; beats aligned on the peak (25 samples before, 50 after) and averaged; normalised to the R peak. Heart rate and beat-to-beat variation of the class are kept.

## What ships
One short averaged template per rhythm in `content/physio_ecg_templates.csv`. No recording.

## Results
- sinus_rhythm: 25 recordings, 274 beats, heart rate median 71.1 (5th to 95th percentile 56.0 to 85.3), beat-interval variation 0.019
- sinus_tachycardia: 23 recordings, 386 beats, heart rate median 111.2 (5th to 95th percentile 97.2 to 121.6), beat-interval variation 0.014
- atrial_fibrillation: 25 recordings, 305 beats, heart rate median 78.8 (5th to 95th percentile 58.5 to 106.7), beat-interval variation 0.216

## ECG strips
`tools/data/ecg_images.py` also draws real 10-second lead II strips, four per code, for sinus rhythm, sinus tachycardia, atrial fibrillation, sinus bradycardia, complete heart block, supraventricular tachycardia and ST elevation (inferior infarction pattern; the ST level is measured from the signal, at least 1 mm), with a report stating the label and the heart rate measured from the signal. Hyperkalaemia, hypokalaemia and fast atrial fibrillation have no labelled open recordings, so those strips are real ones edited in code (peaked T waves; flat T with a U wave; a real atrial fibrillation strip sped up to about 132 per minute), flagged synthetic and marked. They are in `content/ecg/` with attribution (CC BY 4.0), DRAFT until a clinician approves them.

## Limits
A single averaged beat has no P wave detail beyond what averaging keeps; atrial fibrillation is drawn from the same template with irregular intervals (the variation figure). Clinician review of the traces is pending.
