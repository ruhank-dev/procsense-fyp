"""Validate every file in content/ against its schema and the source register.

Usage (from repo root):  task content-validate
                    or:  uv run --project tools tools/content/validate.py
Exit code 0 = all valid, 1 = problems found.
"""
import csv
import sys
from pathlib import Path

from checks import treatment_problems, case_problems, csv_problems, load_json, load_register, ref_problems, flow_map_problems, rule_problems, schema_errors, workflow_problems

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
REGISTER = ROOT / "docs" / "research" / "source_register.csv"


def main() -> int:
    register = load_register(REGISTER)
    case_schema = load_json(CONTENT / "schema" / "case.schema.json")
    failed = False

    for path in sorted((CONTENT / "cases").glob("*.json")):
        doc = load_json(path)
        problems = schema_errors(doc, case_schema) + case_problems(doc, register)
        failed |= _report(path.stem, problems, f"{len(doc.get('refs', {}))} refs")

    for path in sorted((ROOT / "tests" / "fixtures" / "cases").glob("*.json")):
        doc = load_json(path)
        problems = schema_errors(doc, case_schema) + case_problems(doc, register)
        failed |= _report("fixture " + path.stem, problems, f"{len(doc.get('refs', {}))} refs")

    rule_schema = load_json(CONTENT / "schema" / "rule.schema.json")
    rule_files = [p for p in sorted((CONTENT / "rules").glob("*.json")) if p.stem != "vectors"]
    rule_files += sorted((ROOT / "tests" / "fixtures" / "rules").glob("*.json"))
    for path in rule_files:
        doc = load_json(path)
        problems = schema_errors(doc, rule_schema) + rule_problems(doc, register)
        failed |= _report("rules " + path.stem, problems, f"{len(doc.get('rules', []))} rules")

    workflow_schema = load_json(CONTENT / "schema" / "workflow.schema.json")
    workflow_files = sorted((CONTENT / "workflow").glob("*.json")) + sorted((ROOT / "tests" / "fixtures" / "workflow").glob("*.json"))
    for path in workflow_files:
        doc = load_json(path)
        problems = schema_errors(doc, workflow_schema) + workflow_problems(doc, register)
        failed |= _report("workflow " + path.stem, problems, f"{len(doc.get('goals', []))} goals, {len(doc.get('decision_points', []))} decisions, {len(doc.get('blockers', []))} blockers")

    flow = load_json(CONTENT / "flow" / "flow_maps.json")
    with open(CONTENT / "flow_stages.csv", encoding="utf-8", newline="") as f:
        stage_ids = [r["id"] for r in csv.DictReader(f)]
    failed |= _report("flow maps", flow_map_problems(flow, register, stage_ids), f"{len(stage_ids)} stages match flow_stages.csv, refs ok")

    scores = load_json(CONTENT / "scores" / "scores.json")
    refs = {i["ref"] for s in scores["point_scores"] for i in s["items"]} | {b["ref"] for s in scores["point_scores"] for b in s["bands"]}
    for key in ("tg18_cholecystitis", "tg18_cholangitis", "asge", "aki"):
        refs |= set(scores[key].get("refs", [])) | {v["ref"] for v in scores[key].values() if isinstance(v, dict) and "ref" in v}
    refs.add(scores["stopbang"]["ref"])
    failed |= _report("scores", [f"scores.json: ref {r} not in register" for r in sorted(refs) if r not in register], f"{len(refs)} refs ok")

    inv_doc = load_json(CONTENT / "investigations" / "investigations.json")
    ind_doc = load_json(CONTENT / "investigations" / "indications.json")
    irefs = set(inv_doc["refs"]) | {t["ref"] for t in inv_doc["tests"]} | {inv_doc["delay"]["ref"]} | {i["ref"] for i in ind_doc["indications"]}
    nr_doc = load_json(CONTENT / "investigations" / "normal_results.json")
    img_doc = load_json(CONTENT / "imaging" / "imaging.json")
    irefs |= {f["ref"] for f in img_doc["findings"]} | set(img_doc["refs"])
    crit_doc = load_json(CONTENT / "investigations" / "critical.json")
    irefs |= {r["ref"] for r in crit_doc["rules"]} | set(crit_doc["refs"])
    irefs |= {r["ref"] for t in nr_doc["tests"].values() for r in t["results"]} | set(inv_doc["artefacts"]["refs"])
    inv_problems = [f"investigations: ref {r} not in register" for r in sorted(irefs) if r not in register]
    known = {a for t in inv_doc["tests"] for a in t["analytes"]}
    for case_path in sorted((CONTENT / "case_files").glob("*.json")):
        for res in load_json(case_path)["results"]:
            base = res["test"].split(" (")[0].strip()
            if base not in known:
                inv_problems.append(f"{case_path.stem}: the result '{res['test']}' belongs to no test in investigations.json")
    failed |= _report("investigations", inv_problems, f"{len(irefs)} refs ok, every case result belongs to a test")

    loc_doc = load_json(CONTENT / "locations" / "locations.json")
    lrefs = set(loc_doc["refs"]) | {t["ref"] for t in loc_doc["transport"]["escort_tiers"]} | {loc_doc["corridor_event"]["ref"]}
    failed |= _report("locations and transport", [f"locations.json: ref {r} not in register" for r in sorted(lrefs) if r not in register], f"{len(lrefs)} refs ok")

    news2_doc = load_json(CONTENT / "news2" / "news2.json")
    nrefs = {c["ref"] for cs in news2_doc["parameters"].values() for c in cs} | {c["ref"] for c in news2_doc["spo2_scale2"]} | set(news2_doc["supplemental_oxygen"]["refs"]) | set(news2_doc["acvpu"]["refs"]) | {b["ref"] for b in news2_doc["response"]}
    failed |= _report("news2", [f"news2.json: ref {r} not in register" for r in sorted(nrefs) if r not in register], f"{len(nrefs)} refs ok")

    risk_doc = load_json(CONTENT / "risk" / "risk.json")
    intents_doc = load_json(CONTENT / "npc" / "anaesthetist_intents.json")
    rrefs = set(risk_doc["refs"]) | {risk_doc["asa_difference"]["ref"], risk_doc["delay_unexplained"]["ref"]} | set(risk_doc["vte"]["refs"]) | set(intents_doc["refs"]) | {i["ref"] for i in intents_doc["intents"]}
    failed |= _report("risk and anaesthetist lines", [f"risk content: ref {r} not in register" for r in sorted(rrefs) if r not in register], f"{len(rrefs)} refs ok")

    prob_doc = load_json(CONTENT / "problems" / "problems.json")
    prefs = set(prob_doc["refs"]) | {t["ref"] for t in prob_doc["types"]} | {x["ref"] for x in prob_doc["ladder"]} | {x["ref"] for x in prob_doc["priorities"]}
    failed |= _report("problem types and ladder", [f"problems.json: ref {r} not in register" for r in sorted(prefs) if r not in register], f"{len(prefs)} refs ok")

    fasting = load_json(CONTENT / "fasting" / "fasting.json")
    frefs = {c["ref"] for c in fasting["classes"].values()} | set(fasting["refs"])
    failed |= _report("fasting", [f"fasting.json: ref {r} not in register" for r in sorted(frefs) if r not in register], f"{len(frefs)} refs ok")

    treatments = load_json(CONTENT / "treatments.json")
    with open(CONTENT / "medicines.csv", encoding="utf-8", newline="") as f:
        medicine_ids = {r["id"] for r in csv.DictReader(f)}
    failed |= _report("treatments", treatment_problems(treatments, register, medicine_ids), f"{len(treatments['treatments'])} treatments, doses only with a reviewer")

    for path in sorted(CONTENT.glob("*.csv")):
        problems = csv_problems(path, register)
        failed |= _report("table " + path.stem, problems, "every row has a ref")

    for name in ("devices", "behaviour", "history_topics"):
        path = CONTENT / f"{name}.json"
        if path.exists():
            failed |= _report(name, ref_problems(load_json(path), register, name), "refs ok")

    print(f"register: {len(register)} rows")
    return 1 if failed else 0


def _report(name: str, problems: list[str], ok_detail: str) -> bool:
    if problems:
        print(f"{name}: INVALID ({len(problems)} problems)")
        for p in problems:
            print(f"  - {p}")
        return True
    print(f"{name}: valid, {ok_detail}, 0 missing refs")
    return False


if __name__ == "__main__":
    sys.exit(main())
