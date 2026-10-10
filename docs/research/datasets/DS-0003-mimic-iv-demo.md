# DS-0003 / DS-0004 MIMIC-IV demo and MIMIC-IV-ED demo: plausibility

**Source:** https://physionet.org/content/mimic-iv-demo/2.2/ and https://physionet.org/content/mimic-iv-ed-demo/2.2/ · **Licence:** ODbL 1.0 · **Checked:** 2026-10-10: both archives downloaded and read.

## Purpose
Check that the walkthrough's bands and our generated values are plausible for adult hospital patients. Nothing here becomes game content.

## Method (`tools/data/extract_mimic.py`)
Percentiles of all results of 14 tests in 100 demo patients, converted with the walkthrough's own unit factors; triage vitals of the 27 abdominal-complaint rows of 222 ED triage rows.

## What ships
`content/lab_plausibility.csv` and `content/ed_triage_abdominal.csv` hold percentiles only and are used by tests, not by the app. ODbL requires attribution and share-alike for derived databases, so no row-level data is kept.

## Limits
Hospital patients are sicker than the 'healthy reference' of a lab band, so these ranges are wider; the ED sample is tiny.
