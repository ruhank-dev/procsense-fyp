"""Drug label safety wording from the openFDA drug label API (CC0), one representative medicine per medicine group (p0-medical-research-data s06 / s16, deferred task DT-060).

Only contraindications and boxed-warning wording, with every sentence containing a dose, rate or strength dropped by tools/data/extract_openfda.py; so the 'never import a dose' rule holds.
These rows are reference wording for the safety-check content and the debrief; they are US labels used as stand-ins for the UK product of the same group, so the clinician confirms the local wording.
"""
import json
from pathlib import Path

from framework import Table

ROOT = Path(__file__).resolve().parents[3]
data = json.loads((ROOT / "tools" / "data" / "derived" / "openfda_label_safety.json").read_text(encoding="utf-8"))

ls = Table("drug_label_safety", 3800, ["medicine_id", "d_generic", "d_contraindications", "d_boxed_warning", "d_label"], "Drug label safety wording", "openFDA drug label API (CC0)", evidence="B", size=40)
M = ("Contraindication and boxed-warning wording copied from the openFDA label of the generic name; sentences with doses, rates or strengths are dropped. US label as a stand-in for the UK product of the same "
     "medicine group; the clinician confirms the local wording. Licence CC0 1.0.")
for med, v in data["medicines"].items():
    ls.add_derived(med, "openFDA drug label API", "https://open.fda.gov/apis/drug/label/", M, medicine_id=med, d_generic=v["generic_name"], d_contraindications=v["contraindications"],
                   d_boxed_warning=v["boxed_warning"], d_label=f"set id {v['label_set_id']}, effective {v['effective_time']}")

TABLES = [ls]
OFFICIAL_FILES = {}
