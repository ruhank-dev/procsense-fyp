"""One register row per playable case file (p0-medical-research-data s10 to s12): the authored parts of each case.

A case file (content/case_files/<id>.json, built by tools/content/build_case_files.py from tools/content/cases/) is made of (a) what the walkthrough states for the case, which cites the
case's story row in case_seeds / its measurement rows, and (b) detail the walkthrough does not state, authored so the case is playable: history topics it does not mention ('none'),
examination findings that fit the story, previous results, expected decisions and debrief wording that restate the book's teaching. Every authored item is marked origin 'authored' and
cites THIS row. Authored values stay inside the verified bands (lab_bands, vitals_bands, news2_scoring), contain no medicine doses, and rest on the guideline claims in
docs/research/clinical_verification.md. The owner states that the clinician approves everything (10 October 2026); the clinician is not named.
"""
from framework import Table

cf = Table("case_files_authoring", 3700, ["case_id", "d_scope", "d_checks"], "Playable case file (authored parts)", "ProcSense case authoring (owner decision, 10 October 2026)", evidence="D", size=60)
URL = "docs/research/clinical_verification.md"
M = ("Authored detail for a playable case, consistent with the walkthrough's story for the case and checked by the case-file validator (NEWS2 recomputed from the verified table, lab flags against the "
     "verified bands, decision rules, end states and picture results that exist). Synthetic patient; no doses. Approved on the owner's statement; the named clinician should confirm before assessment use.")
SCOPE = ("history topics the book does not mention; examination findings that fit the story; previous results and result timings; expected actions, decision options and consequences; correct, incorrect, "
         "missed and recovery paths; debrief wording that restates the book's teaching")
CASES = ["VAR-S1-A", "VAR-S1-PB", "VAR-S1-E", "VAR-S1-B", "VAR-S1-C", "VAR-S1-D", "VAR-S1-G", "VAR-S1-H", "VAR-S1-J",
         "VAR-S2-PC", "VAR-S2-PD", "VAR-S2-B", "VAR-S2-D", "VAR-S2-F", "VAR-S2-G", "STK-S1-D", "STK-S2-B",
         "VAR-S1-I", "VAR-S1-K", "VAR-S1-L", "VAR-S1-M", "VAR-S2-E", "VAR-S2-H", "VAR-S2-I", "VAR-S2-J", "VAR-S2-K", "VAR-S2-L",
         "STK-S1-A", "STK-S1-B", "STK-S1-C", "STK-S2-A", "STK-S2-C", "STK-S2-D"] + [f"SC-{n}" for n in range(1, 21)]
for c in CASES:
    cf.add_derived(c, "ProcSense case authoring", URL, M, case_id=c, d_scope=SCOPE, d_checks="news2_recomputed;lab_flags;decision_rules;end_states;picture_results;no_doses")

TABLES = [cf]
OFFICIAL_FILES = {}
