# DS-0007 openFDA drug labels: safety wording

**Source:** https://open.fda.gov/apis/drug/label/ · **Licence:** CC0 1.0 · **Checked:** 2026-10-10: 22 labels queried.

## Purpose
Reference wording for contraindications and boxed warnings of the medicine groups, for the safety checks and the debrief. Never doses.

## Method (`tools/data/extract_openfda.py`)
One representative generic per medicine group; the newest single-ingredient label; only the contraindications and boxed-warning text; every sentence with a dose, rate or strength (a number with mg, mcg, g, mL, units, mEq, %) is dropped, as are section cross-references. The label's set id and effective date are kept.

## What ships
`content/drug_label_safety.csv` (22 rows, one register row each).

## Limits
US labels, used as stand-ins for the UK product of the same group; aspirin and adrenaline returned no usable label; brand names can appear inside quotes. The clinician confirms local wording.
