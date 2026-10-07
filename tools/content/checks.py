"""Proof-of-truth checks for content files: schema validity, register references, and ref coverage."""
import csv
import json
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

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


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))
