# DS-0001 VitalDB: vital-sign noise

**Source:** https://vitaldb.net/dataset (open API https://api.vitaldb.net); also https://physionet.org/content/vitaldb/1.0.0/ · **Licence:** CC BY 4.0 for the PhysioNet copy; CC BY-NC-SA 4.0 with a data use agreement for the vitaldb.net copy our extract read (re-derive from PhysioNet: DT-183) · **Cite:** Lee HC, Park Y, Yoon SB, et al. VitalDB, a high-fidelity multi-parameter vital signs database in surgical patients. Sci Data 2022.
**Checked:** 2026-10-10: the open API was opened; the case table (3131 eligible cases after the selection) and the track list were read.

## Purpose
Make the monitor numbers move like a real monitored patient: how much each vital sign wanders around its own slow level, and how strongly one reading follows the previous one.

## Method (`tools/data/extract_vitaldb.py`, reproducible)
planned (emop 0), ASA 1-2, age 18-70, General surgery, every k-th case by id; 40 cases, chosen deterministically. For each vital sign: sigma = spread of the signal after removing its rolling median (31 samples); phi = lag-1 autocorrelation (the AR(1) coefficient); plus percentiles of each patient's typical level. Blood pressure is an intermittent cuff reading, so one value per reading is used. blood pressure is an intermittent cuff reading: one value per reading, window of 7 readings.

## What ships
Only the aggregates in `content/physio_noise.csv` (one row per vital sign, with a register row each). No patient row, no signal.

## Results
- heart_rate: sigma 2.196, phi 0.786, typical level 55.55 to 90.0 /min (40 cases)
- spo2: sigma 0.454, phi 0.76, typical level 98.0 to 100.0 % (40 cases)
- nibp_systolic: sigma 11.062, phi 0.107, typical level 96.4 to 117.8 mmHg (25 cases)
- nibp_diastolic: sigma 7.178, phi 0.12, typical level 53.25 to 74.75 mmHg (26 cases)
- respiratory_rate: sigma 4.891, phi 0.746, typical level 11.95 to 18.7 /min (14 cases)
- temperature_c: sigma 0.057, phi 0.78, typical level 35.0 to 36.69 C (38 cases)

## Limits
intra-operative monitoring of anaesthetised patients; respiratory rate is the monitor/ventilator value and is the least reliable of the six. Clinician plot review (EXP-DATA-1) is still pending, so the register marks these rows as pending approval.
