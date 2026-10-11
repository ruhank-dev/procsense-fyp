"""Proof-of-truth checks for content files: schema validity, register references, and ref coverage."""
import csv
import json
import re
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

import jsonlogic

# Keys that describe structure, not clinical facts — they don't need a source reference.
NON_CLINICAL_KEYS = {"id", "scenario", "refs", "topic", "wording", "volunteered"}


def load_register(path: Path) -> dict[str, dict]:
    with open(path, encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    ids = [r["id"] for r in rows]
    dupes = {i for i in ids if ids.count(i) > 1}
    if dupes:
        raise ValueError(f"duplicate register IDs: {sorted(dupes)}")
    return {r["id"]: r for r in rows}


def schema_errors(doc: dict, schema: dict) -> list[str]:
    validator = Draft202012Validator(schema, format_checker=FormatChecker())
    return [f"{'/'.join(map(str, e.absolute_path)) or '<root>'}: {e.message}" for e in validator.iter_errors(doc)]


def leaf_paths(node, prefix: str = "") -> list[str]:
    """JSON leaf paths like 'baseline_vitals.heart_rate' or 'history[3].answer'. Empty lists/objects count as leaves."""
    if isinstance(node, dict) and node:
        out = []
        for k, v in node.items():
            if k in NON_CLINICAL_KEYS:
                continue
            out += leaf_paths(v, f"{prefix}.{k}" if prefix else k)
        return out
    if isinstance(node, list) and node:
        out = []
        for i, v in enumerate(node):
            out += leaf_paths(v, f"{prefix}[{i}]")
        return out
    return [prefix]


def is_covered(path: str, refs: dict[str, str]) -> bool:
    """A ref on the path itself or on any ancestor covers it."""
    return any(path == r or path.startswith(r + ".") or path.startswith(r + "[") for r in refs)


def case_problems(doc: dict, register: dict[str, dict]) -> list[str]:
    refs = doc.get("refs", {})
    problems = [f"ref {p} -> {rid} not in register" for p, rid in refs.items() if rid not in register]
    problems += [f"no source reference for '{p}'" for p in leaf_paths(doc) if not is_covered(p, refs)]
    return problems


def rule_problems(doc: dict, register: dict[str, dict]) -> list[str]:
    """Every rule's refs must exist in the register, and rule ids must be unique (the C# loader checks the rest and runs the rule tests)."""
    problems = []
    ids = [r.get("id") for r in doc.get("rules", [])]
    problems += [f"duplicate rule id {i}" for i in sorted({i for i in ids if ids.count(i) > 1})]
    for rule in doc.get("rules", []):
        problems += [f"rule {rule.get('id')}: ref {rid} not in register" for rid in rule.get("refs", []) if rid not in register]
    return problems


def csv_problems(path: Path, register: dict[str, dict]) -> list[str]:
    """A content table (CSV) must have a 'ref' column and every row must cite register ids that exist (several ids separated by ';')."""
    with open(path, encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        if not reader.fieldnames or "ref" not in reader.fieldnames:
            return [f"{path.name}: needs a 'ref' column holding the source-register id of each row"]
        problems = []
        for n, row in enumerate(reader, start=2):   # line 1 is the header
            ids = [x.strip() for x in (row.get("ref") or "").split(";") if x.strip()]
            if not ids:
                problems.append(f"{path.name} line {n}: no source reference")
            problems += [f"{path.name} line {n}: ref {rid} not in register" for rid in ids if rid not in register]
    return problems


RULE_FIELDS = ("applies_if", "complete_if", "active_if", "blocked_if", "trigger")


def engine_rule_keys(source: Path) -> set[str]:
    """The data keys the engine gives every workflow rule: the string keys set in RuleData.Build in CaseGraphSubsystem.cs."""
    text = source.read_text(encoding="utf-8")
    build = text[text.index("Dictionary<string, object?> Build(Session s)"):]
    build = build[:build.index("ReadinessSubsystem")]
    return set(re.findall(r'\["(\w+)"\] =', build))


def workflow_rule_problems(doc: dict, known_keys: set[str]) -> list[str]:
    """Evaluated by the Python twin of the rule evaluator (p0-clinical-logic-s02): every rule must compile in the subset and may only read keys the engine provides."""
    problems = []
    for kind in ("goals", "decision_points", "blockers"):
        for e in doc.get(kind, []):
            for field in RULE_FIELDS:
                if field not in e or isinstance(e[field], bool):
                    continue
                where = f"{kind} {e.get('id')} {field}"
                try:
                    jsonlogic.compile_rule(e[field], where)
                except jsonlogic.RuleError as err:
                    problems.append(str(err))
                    continue
                keys: set[str] = set()
                if not jsonlogic.reads(e[field], keys):
                    problems.append(f"{where}: reads a variable the engine cannot know in advance")
                problems += [f"{where}: reads '{k}', which the engine does not provide (it provides {', '.join(sorted(known_keys))})" for k in sorted(keys - known_keys)]
    return problems


def handover_rule_problems(doc: dict, known_keys: set[str]) -> list[str]:
    """Every handover item's known_if rule compiles in the subset and reads only keys the engine provides."""
    problems = []
    for it in doc.get("items", []):
        rule = it.get("known_if")
        if rule is None or isinstance(rule, bool):
            continue
        where = f"handover item {it.get('id')} known_if"
        try:
            jsonlogic.compile_rule(rule, where)
        except jsonlogic.RuleError as err:
            problems.append(str(err))
            continue
        keys: set[str] = set()
        if not jsonlogic.reads(rule, keys):
            problems.append(f"{where}: reads a variable the engine cannot know in advance")
        problems += [f"{where}: reads '{k}', which the engine does not provide" for k in sorted(keys - known_keys)]
    return problems


def ward_rule_problems(doc: dict, known_keys: set[str]) -> list[str]:
    """Every ward checklist rule compiles in the subset (Python twin of the evaluator) and reads only keys the engine provides."""
    problems = []
    for it in doc.get("items", []):
        for field in ("verify_if", "required_if"):
            rule = it.get(field)
            if rule is None:
                continue
            where = f"ward item {it.get('id')} {field}"
            try:
                jsonlogic.compile_rule(rule, where)
            except jsonlogic.RuleError as err:
                problems.append(str(err))
                continue
            keys: set[str] = set()
            if not jsonlogic.reads(rule, keys):
                problems.append(f"{where}: reads a variable the engine cannot know in advance")
            problems += [f"{where}: reads '{k}', which the engine does not provide" for k in sorted(keys - known_keys)]
    return problems


def workflow_problems(doc: dict, register: dict[str, dict]) -> list[str]:
    """Every goal, decision point and blocker must cite register ids that exist (the C# loader checks cycles, options and the rest)."""
    problems = []
    for kind in ("goals", "decision_points", "blockers"):
        entries = doc.get(kind, [])
        ids = [e.get("id") for e in entries]
        problems += [f"{kind}: duplicate id {i}" for i in sorted({i for i in ids if ids.count(i) > 1})]
        for e in entries:
            problems += [f"{kind} {e.get('id')}: ref {rid} not in register" for rid in e.get("refs", []) if rid not in register]
    return problems


def flow_map_problems(doc: dict, register: dict[str, dict], stage_ids: list[str]) -> list[str]:
    """content/flow/flow_maps.json: every stage cites a register id that exists, and the stages are exactly the 40 rows of flow_stages.csv (the C# loader checks order, steps and tracks)."""
    problems = []
    found = []
    for m in doc.get("maps", []):
        for s in m.get("stages", []):
            found.append(s.get("id"))
            problems += [f"flow stage {s.get('id')}: ref {rid} not in register" for rid in s.get("refs", []) if rid not in register]
    problems += [f"flow stage {i}: in flow_stages.csv but not in flow_maps.json" for i in stage_ids if i not in found]
    problems += [f"flow stage {i}: in flow_maps.json but not in flow_stages.csv" for i in found if i not in stage_ids]
    return problems


def ref_problems(doc: dict, register: dict[str, dict], name: str) -> list[str]:
    """For simple content files (e.g. devices.json): every top-level entry needs a 'ref' that exists."""
    problems = []
    for key, entry in doc.items():
        rid = entry.get("ref") if isinstance(entry, dict) else None
        if not rid:
            problems.append(f"{name}:{key} has no 'ref'")
        elif rid not in register:
            problems.append(f"{name}:{key} ref {rid} not in register")
    return problems


def treatment_problems(doc: dict, register: dict[str, dict], medicine_ids: set[str]) -> list[str]:
    """content/treatments.json: every register id exists, every medicine treatment names a medicines.csv row, a dose text always has its source and reviewer
    (rule DOSE_REQUIRES_REVIEWER) and only an EDUCATIONAL_EXAMPLE row may have one, every treatment names the checks it needs and no check is implied."""
    problems: list[str] = []
    check_ids = [c["id"] for c in doc["checks"]]
    effect_ids = {e["id"] for e in doc["effects"]}
    prerequisite_ids = {p["id"] for p in doc["prerequisites"]}
    section_refs = list(doc.get("refs", [])) + [doc["workflow"]["ref"]] + [v["ref"] for v in doc["dose_labels"].values()] + list(doc["allergy"]["refs"])
    for rid in section_refs:
        if rid not in register:
            problems.append(f"treatments: ref {rid} not in register")
    if doc["workflow"]["confirm_seconds"] <= 0 or doc["workflow"]["administer_seconds"] <= 0:
        problems.append("treatments: the workflow delays must be more than 0 seconds")
    groups = {g["id"] for g in doc["allergy"]["groups"]}
    seen: set[str] = set()
    for t in doc["treatments"]:
        where = f"treatments:{t['id']}"
        if t["id"] in seen:
            problems.append(f"{where}: duplicate id")
        seen.add(t["id"])
        refs = list(t["refs"]) + [r["ref"] for r in t["restricted_by"]] + ([t["medicine"]["ref"]] if t.get("medicine") else []) + ([t["label_row"]["ref"]] if t.get("label_row") else [])
        problems += [f"{where}: ref {r} not in register" for r in sorted(set(refs)) if r not in register]
        if t["kind"] in ("medicine", "fluid") and (not t.get("medicine") or t["medicine"]["id"] not in medicine_ids):
            problems.append(f"{where}: names no row of medicines.csv")
        dose = t.get("dose_display")
        if dose is not None:
            if not str(dose.get("reviewer", "")).strip():
                problems.append(f"{where}: DOSE_REQUIRES_REVIEWER: a dose text needs a reviewer")
            if not str(dose.get("source", "")).strip():
                problems.append(f"{where}: DOSE_REQUIRES_REVIEWER: a dose text needs a source")
            if t["dose_policy"] != "EDUCATIONAL_EXAMPLE":
                problems.append(f"{where}: only an EDUCATIONAL_EXAMPLE treatment may show a dose")
        elif t["dose_policy"] == "EDUCATIONAL_EXAMPLE":
            problems.append(f"{where}: an EDUCATIONAL_EXAMPLE treatment needs its dose text")
        if not t["required_checks"]:
            problems.append(f"{where}: lists no checks (none are implied)")
        problems += [f"{where}: unknown check {c}" for c in t["required_checks"] if c not in check_ids]
        problems += [f"{where}: unknown effect {e}" for e in t["effects"] if e not in effect_ids]
        problems += [f"{where}: unknown prerequisite {r}" for r in t["requires"] if r not in prerequisite_ids]
        if t.get("allergen_group") and t["allergen_group"] not in groups:
            problems.append(f"{where}: allergy group {t['allergen_group']} is not defined")
        if ("restriction" in t["required_checks"]) != bool(t["restricted_by"]):
            problems.append(f"{where}: the restriction check and the restriction list must go together")
    return problems


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))
