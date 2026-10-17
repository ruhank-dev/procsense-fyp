"""Mutation tests for tools/plans/validate_pipeline.py: the real JSON passes, and each deliberately broken copy fails
with the rule it breaks. Usage: uv run --project tools tools/plans/test_validate_pipeline.py
"""
import copy
import json
import subprocess
import sys
import tempfile
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
VALIDATOR = Path(__file__).with_name("validate_pipeline.py")
REAL = ROOT / "docs" / "architecture" / "procsense_full_pipeline.json"


def node(doc, nid):
    return next(n for n in doc["nodes"] if n["id"] == nid)


def first_cycle_edge(doc):
    return next(e for e in doc["edges"] if "cycle" in e)


MUTATIONS = [
    ("missing top-level field", "missing top-level field", lambda d: d.pop("open_questions")),
    ("duplicate node id", "VR-G-UNIQUE", lambda d: d["nodes"].append(copy.deepcopy(d["nodes"][0]))),
    ("dangling edge", "VR-G-ENDPOINTS", lambda d: d["edges"].append({"id": "EX1", "source": "LG-01", "target": "NOPE", "type": "data_flow", "label": ""})),
    ("unknown model ref", "VR-G-REFS", lambda d: node(d, "A1")["model_refs"].append("MC-99")),
    ("task without a step file", "VR-G-REFS", lambda d: d["phases"][1]["tasks"].append("p1-nothing-s01")),
    ("feature without task", "VR-G-COVERAGE", lambda d: node(d, "A6").update(tasks=[])),
    ("planned stage labelled implemented without evidence", "VR-G-HONEST", lambda d: node(d, "LG-21").update(status="Implemented and tested")),
    ("evidence path that does not exist", "VR-G-HONEST", lambda d: node(d, "LG-16").update(evidence=["src/ProcSense.Core/NoSuchFile.cs"])),
    ("undocumented cycle", "VR-G-CYCLES", lambda d: first_cycle_edge(d).pop("cycle")),
    ("stage cut off from exits", "VR-G-ENTRY-EXIT", lambda d: d.update(edges=[e for e in d["edges"] if e["source"] != "LG-21"])),
    ("feature plan missing a field", "VR-G-PLANS", lambda d: node(d, "C24")["plan"].pop("integration")),
    ("hybrid feature without an AI capability plan", "VR-G-PLANS", lambda d: node(d, "A2")["plan"].update(capabilities=node(d, "A2")["plan"]["capabilities"][:1])),
    ("node that traces to no plan", "VR-G-TRACE", lambda d: node(d, "LG-13").update(traced_to={"kind": "direct", "features": [], "steps": []})),
    ("edge not derived from any feature plan", "VR-G-TRACE", lambda d: d["edges"].append({"id": "EX2", "source": "A1", "target": "LG-19", "type": "realised_by", "label": "", "traced_to": {"features": ["A1"], "steps": []}})),
    ("capability without acceptance", "VR-G-PLANS", lambda d: node(d, "D67")["plan"]["capabilities"][1].update(acceptance="")),
]


def run(doc) -> tuple[int, str]:
    with tempfile.TemporaryDirectory() as tmp:
        p = Path(tmp) / "pipeline.json"
        p.write_text(json.dumps(doc), encoding="utf-8")
        r = subprocess.run([sys.executable, str(VALIDATOR), "--quiet", "--file", str(p)], capture_output=True, text=True, encoding="utf-8")
        return r.returncode, r.stdout + r.stderr


def main() -> int:
    base = json.loads(REAL.read_text(encoding="utf-8"))
    failures = []
    code, out = run(base)
    if code != 0:
        failures.append("the real pipeline JSON should pass:\n" + out)
    for name, rule, mutate in MUTATIONS:
        d = copy.deepcopy(base)
        mutate(d)
        code, out = run(d)
        if code == 0 or rule not in out:
            failures.append(f"{name}: expected failure with {rule}, got exit {code}:\n{out[-400:]}")
    for f in failures:
        print("FAIL  " + f)
    print(f"{len(MUTATIONS) + 1 - len(failures)}/{len(MUTATIONS) + 1} validator tests passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
