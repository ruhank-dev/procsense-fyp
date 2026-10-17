# p1-dataset-analysis-s03 — Waveform profiles: ECG and monitor signals

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Profile PTB-XL classes needed by cases (sinus, tachycardia, AF, hyperkalaemia-like changes): rate, PR, QRS, QT distributions, noise; VitalDB artefact rates and episode slopes (hypotension, desaturation).

## Why
ECG variants and monitor behaviour must stay inside what real recordings show.

## How to implement
1. `tools/data/profile_waveforms.py`; aggregates only.
2. Class coverage table against case needs; list classes needing parametric variation (OQ-05).
3. Try NeuroKit2 `ecg_simulate` parameters that match each class's rate and noise (design note only; generator built in p1-live-scenario-generation-s06).

## Status (17 October 2026)

- **Built:** `tools/data/profile_waveforms.py` (`--check` in CI) writes `tools/data/derived/profiles/waveforms.json` and `docs/research/datasets/profiles/waveforms.md`: the 10 ECG findings of the imaging library against PTB-XL (how many labelled records, and for up to 40 human-validated 100 Hz records per class the heart rate, beat-to-beat variation, a coarse QRS width and the noise share); the monitor behaviour quoted from the VitalDB aggregates; the design note for the generator.
- **Verification result:** every ECG finding has measured PTB-XL records or a stated plan. Hyperkalaemia and hypokalaemia have no PTB-XL label and are planned as parametric patterns after their cardiology reference is read (OQ-05); complete heart block (14 records) and SVT (37) are weak.
- **Not built / limits:** PR, QT, ST level and T-wave height are not measured (a delineator on a 100 Hz recording is not reliable; PTB-XL+ or the 500 Hz records would be needed); the QRS width has a 10 ms resolution; the peak finder misses beats in atrial fibrillation (the rate p5 of 31 per minute is an artefact, said in the report); VitalDB episode slopes need the full waveform downloads and stay with the physiology drivers; NeuroKit2 `ecg_simulate` was not tried (it is not installed, and it cannot make electrolyte changes anyway).

## Verification
Coverage table complete; every needed rhythm has a source (real strip or parametric plan).

## Files changed
`tools/data/profile_waveforms.py`, `docs/research/datasets/profiles/waveforms.md`

## Dependencies
p1-dataset-analysis-s01

## Common issues
PTB-XL labels are statement-level; check by eye for the strips used.

## Sources
DS-0001, DS-0002.
