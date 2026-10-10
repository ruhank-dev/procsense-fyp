"""D10 (p0-medical-research-data s15): the aggregates taken from the open datasets, as content tables with register rows.

The extraction scripts in tools/data/ do the downloading (network, slow, run by hand); their small results are committed in tools/data/derived/ and this module
turns them into tables, so CI and teammates never download anything. Every row is DERIVED: it names its dataset and method, and clinician approval stays
'pending' until the plots have been reviewed (EXP-DATA-1). Nothing in these tables is a patient row.
"""
import json
from pathlib import Path

from framework import Table

DERIVED = Path(__file__).resolve().parents[2] / "data" / "derived"


def load(name: str) -> dict:
    return json.loads((DERIVED / name).read_text(encoding="utf-8"))


vdb = load("vitaldb_noise.json")
noise = Table("physio_noise", 2600, ["variable", "unit", "d_sigma", "d_phi", "d_level_p5", "d_level_p50", "d_level_p95", "d_cases", "d_samples"],
              "Vital-sign noise (VitalDB)", "VitalDB open API, aggregates only", size=20)
for name, v in vdb["variables"].items():
    if v["cases"] == 0:
        continue
    low = " Least reliable of the six: the monitor or ventilator value, not a bedside count." if name == "respiratory_rate" else ""
    noise.add_derived(name, "VitalDB (Lee et al., Sci Data 2022)", "https://vitaldb.net/dataset",
                      f"{vdb['selection']} ({vdb['cases_selected']} of {vdb['eligible_cases']} eligible cases). sigma = spread around the patient's own slow level, phi = lag-1 autocorrelation (AR(1)); "
                      f"intra-operative monitoring, not an awake bay patient.{low}",
                      variable=name, unit=v["unit"], d_sigma=v["sigma_median"], d_phi=v["phi_median"], d_level_p5=v["level_p5"], d_level_p50=v["level_p50"],
                      d_level_p95=v["level_p95"], d_cases=v["cases"], d_samples=v["samples"])

ptb = load("ptbxl_ecg_templates.json")
ecg = Table("physio_ecg_templates", 2650, ["rhythm", "d_hr_median", "d_hr_p5", "d_hr_p95", "d_rr_cv", "d_records", "d_beats", "d_template"], "ECG beat template (PTB-XL)",
            "PTB-XL 1.0.3, lead II at 100 Hz, aggregates only", size=10)
for name, c in ptb["classes"].items():
    ecg.add_derived(name, "PTB-XL (Wagner et al., Sci Data 2020)", "https://physionet.org/content/ptb-xl/1.0.3/",
                    f"Average of {c['beats_averaged']} R-aligned beats from {c['records_used']} human-validated recordings; normalised to the R peak; "
                    f"{ptb['samples_before_r']} samples before and {ptb['samples_after_r']} after the peak at {ptb['sampling_hz']} Hz.",
                    rhythm=name, d_hr_median=c["heart_rate_median"], d_hr_p5=c["heart_rate_p5"], d_hr_p95=c["heart_rate_p95"], d_rr_cv=c["rr_variation_cv_median"],
                    d_records=c["records_used"], d_beats=c["beats_averaged"], d_template=" ".join(str(x) for x in c["template_r_normalised"]))

mimic = load("mimic_lab_percentiles.json")
plaus = Table("lab_plausibility", 2670, ["analyte", "unit", "d_n", "d_p5", "d_p25", "d_p50", "d_p75", "d_p95"], "Lab plausibility (MIMIC-IV demo)",
              "MIMIC-IV Clinical Database Demo 2.2, hospital patients, percentiles only", size=20)
for name, v in mimic["labs"].items():
    plaus.add_derived(name, "MIMIC-IV Clinical Database Demo 2.2 (ODbL 1.0)", "https://physionet.org/content/mimic-iv-demo/2.2/",
                      "Percentiles of all results of this test in 100 hospital patients. For plausibility checks only (these are sick patients, so the range is wider than a healthy "
                      "reference range); not shipped in the app. Converted with the walkthrough's own unit factors.",
                      analyte=name, unit=v["unit"], d_n=v["n"], d_p5=v["p5"], d_p25=v["p25"], d_p50=v["p50"], d_p75=v["p75"], d_p95=v["p95"])

ed = load("mimic_ed_triage.json")
triage = Table("ed_triage_abdominal", 2700, ["variable", "d_n", "d_p5", "d_p25", "d_p50", "d_p75", "d_p95"], "ED triage vitals, abdominal complaints (MIMIC-IV-ED demo)",
               "MIMIC-IV-ED Demo 2.2, triage table, percentiles only", size=10)
for name, v in ed["variables"].items():
    triage.add_derived(name, "MIMIC-IV-ED Demo 2.2 (ODbL 1.0)", "https://physionet.org/content/mimic-iv-ed-demo/2.2/",
                       f"Triage values where the chief complaint contains 'abd' ({ed['abdominal_pain_rows']} of {ed['triage_rows']} triage rows). Temperature converted from F to C. "
                       "Plausibility for the S2 arrival state only; a very small sample.",
                       variable=name, d_n=v["n"], d_p5=v["p5"], d_p25=v["p25"], d_p50=v["p50"], d_p75=v["p75"], d_p95=v["p95"])

TABLES = [noise, ecg, plaus, triage]
OFFICIAL_FILES = {}
