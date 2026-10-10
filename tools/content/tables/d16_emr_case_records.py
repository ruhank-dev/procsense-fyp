"""Previous records for the EMR, one set per P0 case, written from the case files (p0-medical-research-data s14): the GP / pre-assessment summary, previous results, the clinic ultrasound report
for planned patients, and the previous anaesthetic record where the history mentions one.

Every record restates what the case file already says (history, allergies, 'previous' results, the picture finding for the case) in document form and adds no clinical fact; dates are offsets
from the case's 'today' (10 October 2026). All records are synthetic. The case files must exist before this table is built (tools/content/build_case_files.py writes them; content-refresh runs both).
"""
import csv
import json
from pathlib import Path

from framework import Table

ROOT = Path(__file__).resolve().parents[3]
URL = "content/case_files"
M = ("Record written from the case file of the same patient (history, allergies, previous results, picture finding); restates it in document form and adds no clinical fact. Synthetic. "
     "Approved on the owner's statement (10 Oct 2026); the named clinician may confirm.")

rec = Table("emr_case_records", 3900, ["case_id", "doc_type", "title", "date_offset", "d_body", "synthetic"], "EMR record for a case", "ProcSense case files (content/case_files)", evidence="D", size=140)

with open(ROOT / "content" / "case_imaging.csv", encoding="utf-8", newline="") as f:
    IMAGING = [r for r in csv.DictReader(f)]
with open(ROOT / "content" / "ultrasound_findings.csv", encoding="utf-8", newline="") as f:
    US = {r["code"]: r for r in csv.DictReader(f)}

for p in sorted((ROOT / "content" / "case_files").glob("*.json")):
    c = json.loads(p.read_text(encoding="utf-8"))
    cid, hx = c["id"], {h["topic"]: h["answer"] for h in c["history"]}
    ident = c["identity"]
    allergy = "; ".join(f"{a['substance']}: {a['reaction']}" for a in c["allergies"]) or "none recorded"
    parts = [f"Patient: {ident['title']} {ident['full_name']}, {ident['age']}, MR {ident['mrn']}.", f"Past history: {hx.get('past_history', 'none recorded')}", f"Medicines: {hx.get('medicines', 'none recorded')}", f"Allergies: {allergy}."]
    if c["scenario"] == "S1":
        rec.add_derived(f"{cid}.preassessment", "ProcSense case files", URL, M, case_id=cid, doc_type="clinic_letter", title="Pre-assessment summary", date_offset="2 months before today", d_body=" ".join(parts), synthetic="yes")
    else:
        rec.add_derived(f"{cid}.summary", "ProcSense case files", URL, M, case_id=cid, doc_type="clinic_letter", title="Registration summary", date_offset="today (on arrival)", d_body=" ".join(parts), synthetic="yes")
    prev = [r for r in c["results"] if r.get("previous")]
    if prev:
        body = "; ".join(f"{r['test']} {r['previous']['value']} {r['unit']} ({r['previous']['when']})" for r in prev)
        rec.add_derived(f"{cid}.previous_results", "ProcSense case files", URL, M, case_id=cid, doc_type="lab_result", title="Previous results", date_offset="earlier (see each result)", d_body=body + ".", synthetic="yes")
    if c["scenario"] == "S1":      # every planned patient was booked after a clinic ultrasound showing simple gallstones; any change (duct stone, inflamed gallbladder) is today's finding
        f = US["US-GB-01"]
        rec.add_derived(f"{cid}.clinic_ultrasound", "ProcSense case files", URL, M, case_id=cid, doc_type="imaging_report", title="Clinic ultrasound report (before booking)", date_offset="before the booking",
                        d_body=f"Ultrasound gallbladder: {f['v_finding']}. Impression: {f['v_plain']}. (Finding code US-GB-01.)", synthetic="yes")
    if "previous_anaesthetic" in hx and not hx["previous_anaesthetic"].lower().startswith(("no previous", "none")):
        rec.add_derived(f"{cid}.previous_anaesthetic", "ProcSense case files", URL, M, case_id=cid, doc_type="anaesthetic_history", title="Previous anaesthetic record", date_offset="see the operation date", d_body=f"Previous anaesthetic: {hx['previous_anaesthetic']}", synthetic="yes")

TABLES = [rec]
OFFICIAL_FILES = {}
