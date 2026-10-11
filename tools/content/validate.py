"""Validate every file in content/ against its schema and the source register.

Usage (from repo root):  task content-validate
                    or:  uv run --project tools tools/content/validate.py
Exit code 0 = all valid, 1 = problems found.
"""
import csv
import sys
from pathlib import Path

from checks import treatment_problems, case_problems, csv_problems, load_json, load_register, ref_problems, flow_map_problems, rule_problems, schema_errors, workflow_problems, workflow_rule_problems, ward_rule_problems, handover_rule_problems, checklist_rule_problems, engine_rule_keys

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
        problems = schema_errors(doc, workflow_schema) + workflow_problems(doc, register) + workflow_rule_problems(doc, engine_rule_keys(ROOT / "src" / "ProcSense.Core" / "Workflow" / "CaseGraphSubsystem.cs"))
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

    cs_doc = load_json(CONTENT / "consent" / "consent.json")
    csrefs = set(cs_doc["refs"]) | {x for p in cs_doc["procedures"].values() for e in p["elements"] for x in e["ref"].split()} | {q["ref"] for q in cs_doc["questions"]}
    failed |= _report("consent", [f"consent.json: ref {r} not in register" for r in sorted(csrefs) if r not in register], f"{len(cs_doc['questions'])} questions, {len(csrefs)} refs ok")

    bh_t = load_json(CONTENT / "behaviour" / "transitions.json")
    bh_m = load_json(CONTENT / "behaviour" / "modes.json")
    bh_d = load_json(CONTENT / "behaviour" / "disclosure.json")
    bhrefs = set(bh_t["refs"]) | {r["ref"] for r in bh_t["rules"]} | set(bh_m["refs"]) | set(bh_d["refs"])
    failed |= _report("behaviour model", [f"behaviour: ref {r} not in register" for r in sorted(bhrefs) if r not in register], f"{len(bh_t['attributes'])} attributes, {len(bh_t['rules'])} transitions, {len(bh_m['modes'])} mode rules, {len(bhrefs)} refs ok")

    ph_model = load_json(CONTENT / "physio" / "model.json")
    ph_coup = load_json(CONTENT / "physio" / "couplings.json")
    ph_noise = load_json(CONTENT / "physio" / "noise.json")
    phrefs = set(ph_model["refs"]) | {v["ref"] for v in ph_model["variables"]} | set(ph_coup["refs"]) | {f["ref"] for f in ph_coup["flags"]} | set(ph_noise["refs"]) | {n["ref"] for n in ph_noise["noise"]} | {n["cap_ref"] for n in ph_noise["noise"]}
    failed |= _report("physiology model", [f"physio: ref {r} not in register" for r in sorted(phrefs) if r not in register], f"{len(ph_model['variables'])} variables, {len(ph_coup['couplings'])} couplings, {len(ph_noise['noise'])} noise rows, {len(phrefs)} refs ok")

    es_doc = load_json(CONTENT / "endstate" / "end_states.json")
    esrefs = set(es_doc["refs"]) | {s["ref"] for s in es_doc["states"]} | {e["ref"] for e in es_doc["surface_later"]}
    failed |= _report("end states", [f"end_states.json: ref {r} not in register" for r in sorted(esrefs) if r not in register], f"{len(es_doc['states'])} states, {len(es_doc['surface_later'])} surface-later facts, {len(esrefs)} refs ok")

    sc_doc = load_json(CONTENT / "checklist" / "safety_checklist.json")
    screfs = set(sc_doc["refs"]) | {i["ref"] for i in sc_doc["items"]}
    sc_problems = [f"safety_checklist.json: ref {r} not in register" for r in sorted(screfs) if r not in register] + checklist_rule_problems(sc_doc, engine_rule_keys(ROOT / "src" / "ProcSense.Core" / "Workflow" / "CaseGraphSubsystem.cs"))
    failed |= _report("safety checklist", sc_problems, f"{len(sc_doc['items'])} items, {len(screfs)} refs ok, every rule compiles")

    ho_doc = load_json(CONTENT / "handover" / "handover_required.json")
    horefs = set(ho_doc["refs"]) | {i["ref"] for i in ho_doc["items"]}
    ho_problems = [f"handover_required.json: ref {r} not in register" for r in sorted(horefs) if r not in register] + handover_rule_problems(ho_doc, engine_rule_keys(ROOT / "src" / "ProcSense.Core" / "Workflow" / "CaseGraphSubsystem.cs"))
    failed |= _report("handover items", ho_problems, f"{len(ho_doc['items'])} items, {len(horefs)} refs ok, every rule compiles")

    or_doc = load_json(CONTENT / "or" / "or_requirements.json")
    orrefs = set(or_doc["refs"]) | {s["ref"] for s in or_doc["stations"]}
    failed |= _report("theatre readiness", [f"or_requirements.json: ref {r} not in register" for r in sorted(orrefs) if r not in register], f"{len(or_doc['stations'])} stations, {len(orrefs)} refs ok")

    ward_doc = load_json(CONTENT / "ward" / "ward_checklist.json")
    wrefs = set(ward_doc["refs"]) | {i["ref"] for i in ward_doc["items"]}
    ward_problems = [f"ward_checklist.json: ref {r} not in register" for r in sorted(wrefs) if r not in register] + ward_rule_problems(ward_doc, engine_rule_keys(ROOT / "src" / "ProcSense.Core" / "Workflow" / "CaseGraphSubsystem.cs"))
    failed |= _report("ward checklist", ward_problems, f"{len(ward_doc['items'])} items, {len(wrefs)} refs ok, every rule compiles")

    det_doc = load_json(CONTENT / "physiology" / "deterioration.json")
    detrefs = set(det_doc["refs"]) | {det_doc["clock"]["ref"]} | {p["ref"] for t in det_doc["timelines"].values() for p in t["points"]} | {t["treated"]["ref"] for t in det_doc["timelines"].values()}
    failed |= _report("deterioration clock", [f"deterioration.json: ref {r} not in register" for r in sorted(detrefs) if r not in register], f"{len(detrefs)} refs ok")

    dec_doc = load_json(CONTENT / "decision" / "decision.json")
    drefs = set(dec_doc["refs"]) | {o["ref"] for o in dec_doc["outcomes"]} | {c["ref"] for c in dec_doc["checks"]} | {dec_doc["emergency_signs"]["ref"]}
    failed |= _report("decision outcomes and checks", [f"decision.json: ref {r} not in register" for r in sorted(drefs) if r not in register], f"{len(drefs)} refs ok")

    clash_doc = load_json(CONTENT / "clashes" / "clashes.json")
    clrefs = set(clash_doc["refs"]) | {c["ref"] for c in clash_doc["clashes"]} | {c["ref"] for c in clash_doc["checks"]} | {p["ref"] for p in clash_doc["plans"]}
    failed |= _report("clashes and medicine plans", [f"clashes.json: ref {r} not in register" for r in sorted(clrefs) if r not in register], f"{len(clrefs)} refs ok")

    sep_doc = load_json(CONTENT / "sepsis" / "sepsis.json")
    srefs = set(sep_doc["refs"]) | {c["ref"] for c in sep_doc["criteria"]} | {t["ref"] for t in sep_doc["timers"].values()}
    failed |= _report("sepsis tiers", [f"sepsis.json: ref {r} not in register" for r in sorted(srefs) if r not in register], f"{len(srefs)} refs ok")

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
