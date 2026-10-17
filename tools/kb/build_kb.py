"""Builds the scenario knowledge base from the verified tables, the case files, the image tags and the dataset profiles (p1-dataset-analysis-s06, feature A66).

    uv run --project tools tools/kb/build_kb.py            # write content/kb/relations.json
    uv run --project tools tools/kb/build_kb.py --check    # fail if it is out of date, invalid against content/kb/schema.json, or a relation lacks a source (CI)
    uv run --project tools tools/kb/build_kb.py --neighbours analyte:potassium    # what the graph says about one node (a quick look)

What is imported, and how sure each kind is:
  verified       the walkthrough's bands (lab_bands.csv), critical limits, test indications, treatment clashes and what-ifs: every row has its SR id.
  authored       what the 53 authored cases show: a problem and the abnormal results it comes with (n = the cases that show it; one case is one example, not epidemiology), and the imaging findings.
  statistical    pairs of analytes that move together, from the dataset profiles, with n and a 95 percent interval; under 50 pairs is weak.
  llm_unreviewed relations an offline language model proposed from case or walkthrough text with a quote that appears word for word (tools/kb/extract_relations.py); they sit in the review queue and are never
                 imported here until a person approves them.
A relation says "this goes with that"; it never says why, and an outlier is never recorded as an abnormality without a band or a source. Nothing here decides anything at run time.
"""
from __future__ import annotations

import csv
import json
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "kb" / "relations.json"
SCHEMA = CONTENT / "kb" / "schema.json"
APPROVED = CONTENT / "kb" / "approved_llm_relations.json"

TYPES = {
    "analyte_zone": "an analyte value range and what the walkthrough calls it (normal, mild, significant, severe, critical)",
    "critical_limit": "a value at which the laboratory must phone",
    "test_indicated": "a test the walkthrough asks for in a scenario family, and why",
    "treatment_clash": "a need that clashes with another condition, and the safer way",
    "whatif": "a trigger the walkthrough teaches, what it means, what to check and what is correct",
    "problem_result": "a problem of an authored case and an abnormal result that comes with it (a single authored example per case)",
    "problem_action": "a problem of an authored case and the action expected for it",
    "finding_tag": "an imaging finding and the tags that describe it, with the test that shows it",
    "analyte_correlation": "two analytes that move together in real data (n and interval; weak under 50 pairs)",
    "llm_proposed": "a relation proposed by an offline model with a verbatim quote and approved by a person",
}


def read_csv(path: Path) -> list[dict]:
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def slug(s: str) -> str:
    out = "".join(c.lower() if c.isalnum() else "_" for c in s).strip("_")
    while "__" in out:
        out = out.replace("__", "_")
    return out


def rel(rid: str, rtype: str, subject: dict, predicate: str, obj: dict, sources: list[str], evidence: dict, confidence: str, status: str = "approved", strength: dict | None = None, note: str = "") -> dict:
    r = {"id": rid, "type": rtype, "subject": subject, "predicate": predicate, "object": obj, "sources": sources, "evidence": evidence, "confidence": confidence, "status": status}
    if strength is not None:
        r["strength"] = strength
    if note:
        r["note"] = note
    return r


def interval_text(lo: str, hi: str) -> str:
    return f"{lo or '..'} to {hi or '..'}"


def from_tables() -> list[dict]:
    out = []
    for r in read_csv(CONTENT / "lab_bands.csv"):
        label = f"{r['zone']} {interval_text(r['v_low'], r['v_high'])} {r['unit']}" + (f" ({r['sex']})" if r["sex"] else "") + (f" [{r['basis']}]" if r["basis"] else "")
        out.append(rel(f"zone.{r['id']}", "analyte_zone", {"kind": "analyte", "id": r["analyte"]}, "is_in_zone", {"kind": "zone", "id": r["zone"], "label": label}, [r["ref"]],
                       {"kind": "table", "file": "content/lab_bands.csv", "row": r["id"]}, "verified"))
    for r in read_csv(CONTENT / "critical_results.csv"):
        out.append(rel(f"limit.{r['id']}", "critical_limit", {"kind": "analyte", "id": r["analyte"]}, f"critical_when_{r['direction']}", {"kind": "limit", "id": r["id"], "label": f"{r['v_value']} {r['d_unit']}".strip()},
                       [r["ref"]], {"kind": "table", "file": "content/critical_results.csv", "row": r["id"]}, "verified"))
    for r in read_csv(CONTENT / "test_indications.csv"):
        for fam, col in (("S1", "v_s1"), ("S2", "v_s2")):
            out.append(rel(f"indication.{r['id']}.{fam.lower()}", "test_indicated", {"kind": "test", "id": r["test"]}, "indicated_in", {"kind": "scenario", "id": fam, "label": r[col]}, [r["ref"]],
                           {"kind": "table", "file": "content/test_indications.csv", "row": r["id"]}, "verified", note=r["v_why"]))
    for r in read_csv(CONTENT / "treatment_clashes.csv"):
        out.append(rel(f"clash.{r['id']}", "treatment_clash", {"kind": "situation", "id": r["clash_key"], "label": r["v_clash"]}, "is_safer_with", {"kind": "plan", "id": r["clash_key"] + ".safer", "label": r["v_safer"]}, [r["ref"]],
                       {"kind": "table", "file": "content/treatment_clashes.csv", "row": r["id"]}, "verified", note=r["v_why"]))
    for r in read_csv(CONTENT / "whatif_library.csv"):
        out.append(rel(f"whatif.{r['id']}", "whatif", {"kind": "trigger", "id": r["id"], "label": r["v_trigger"]}, "means", {"kind": "meaning", "id": r["id"] + ".meaning", "label": r["v_meaning"]}, [r["ref"]],
                       {"kind": "table", "file": "content/whatif_library.csv", "row": r["id"]}, "verified", note=r["title"]))
    return out


def from_cases() -> list[dict]:
    out = []
    for path in sorted((CONTENT / "case_files").glob("*.json")):
        c = json.loads(path.read_text(encoding="utf-8"))
        abnormal = [x for x in c["results"] if x["flag"] not in ("normal", "") and x.get("quality") in (None, "")]
        for p in c["problems"]:
            node = {"kind": "problem", "id": f"{c['id']}.{p['id']}", "label": p["label"]}
            src = [f"CASE:{c['id']}"]
            ev = {"kind": "case", "file": f"content/case_files/{c['id']}.json", "row": p["id"]}
            for a in p.get("expected_actions", []):
                out.append(rel(f"action.{c['id']}.{p['id']}.{slug(a)[:40]}", "problem_action", node, "expects_action", {"kind": "action", "id": slug(a)[:60], "label": a}, src, ev, "authored"))
            if p["type"] in ("pathway_changing", "optimisable", "life_threat", "information_only"):
                for r in abnormal:
                    out.append(rel(f"result.{c['id']}.{p['id']}.{slug(r['id'])}", "problem_result", node, "comes_with", {"kind": "result", "id": f"{slug(r['test'])}:{r['flag']}", "label": f"{r['test']} {r['value']} {r['unit']} ({r['flag']})"},
                                   src, ev, "authored", strength={"n": 1, "estimate": None, "ci95": None, "weak": True, "measure": "one authored case"},
                                   note="a single authored example, not a frequency" + (f"; the result belongs to variant {r['variant']}" if r.get("variant") else "")))
    for code, f in json.loads((CONTENT / "imaging" / "finding_tags.json").read_text(encoding="utf-8"))["findings"].items():
        out.append(rel(f"finding.{code}", "finding_tag", {"kind": "finding", "id": code}, "shown_by", {"kind": "test", "id": f["test"], "label": " ".join(f["tags"])}, [_ref(code)],
                       {"kind": "table", "file": "content/imaging/finding_tags.json", "row": code}, "authored"))
    return out


def _ref(code: str) -> str:
    for r in read_csv(CONTENT / "imaging_library.csv"):
        if r["finding_code"] == code:
            return r["ref"]
    raise KeyError(code)


def from_profiles() -> list[dict]:
    out = []
    labs = json.loads((ROOT / "tools" / "data" / "derived" / "profiles" / "labs.json").read_text(encoding="utf-8"))
    for c in labs["correlations"]:
        if not c.get("pairs") or c.get("spearman") is None:
            continue
        out.append(rel(f"corr.{c['a']}.{c['b']}", "analyte_correlation", {"kind": "analyte", "id": c["a"]}, "moves_with", {"kind": "analyte", "id": c["b"]}, ["DS-0003"],
                       {"kind": "dataset", "file": "tools/data/derived/profiles/labs.json", "row": f"{c['a']}|{c['b']}"}, "statistical",
                       strength={"n": c["pairs"], "estimate": c["spearman"], "ci95": c.get("spearman_ci95"), "weak": bool(c["weak"]), "measure": "Spearman rho, same patient and time"},
                       note="hospital patients in a 100-patient demo; association, not cause" + ("; fewer than 50 pairs: weak" if c["weak"] else "")))
    return out


def from_approved_llm() -> list[dict]:
    if not APPROVED.exists():
        return []
    return list(json.loads(APPROVED.read_text(encoding="utf-8"))["relations"])


def build() -> dict:
    relations = from_tables() + from_cases() + from_profiles() + from_approved_llm()
    ids = [r["id"] for r in relations]
    dupes = sorted({i for i in ids if ids.count(i) > 1})
    if dupes:
        raise SystemExit("duplicate relation ids: " + ", ".join(dupes[:5]))
    relations.sort(key=lambda r: r["id"])
    counts = defaultdict(int)
    for r in relations:
        counts[r["type"]] += 1
    return {"schema": 1, "note": "Generated by tools/kb/build_kb.py (p1-dataset-analysis-s06); do not edit. Evidence for the composer, not rules.", "relation_types": TYPES, "counts": dict(sorted(counts.items())), "relations": relations}


def dump(doc: dict) -> str:
    return json.dumps(doc, indent=1, sort_keys=True, ensure_ascii=False) + "\n"


def problems() -> list[str]:
    import jsonschema
    out = []
    doc = build()
    try:
        jsonschema.validate(doc, json.loads(SCHEMA.read_text(encoding="utf-8")))
    except jsonschema.ValidationError as e:
        out.append("relations do not match the schema: " + e.message[:200])
    register = {r["id"] for r in read_csv(ROOT / "docs" / "research" / "source_register.csv")}
    for r in doc["relations"]:
        for s in r["sources"]:
            if s.startswith("SR-") and s not in register:
                out.append(f"{r['id']}: source {s} is not in the source register")
        if r["type"] not in TYPES:
            out.append(f"{r['id']}: unknown type {r['type']}")
        st = r.get("strength")
        if st and st.get("n", 0) < 50 and not st.get("weak"):
            out.append(f"{r['id']}: n is {st['n']} (under 50) but the relation is not marked weak")
        if r["confidence"] == "llm_unreviewed" and r["status"] == "approved":
            out.append(f"{r['id']}: an unreviewed model relation is marked approved")
        if r["type"] == "llm_proposed" and not r["evidence"].get("quote"):
            out.append(f"{r['id']}: a model relation without its quote")
    if not OUT.exists() or OUT.read_text(encoding="utf-8").replace("\r\n", "\n") != dump(doc):
        out.append("content/kb/relations.json is out of date: run `uv run --project tools tools/kb/build_kb.py`")
    return out


def neighbours(node: str) -> list[dict]:
    kind, _, ident = node.partition(":")
    doc = json.loads(OUT.read_text(encoding="utf-8"))
    return [r for r in doc["relations"] if (r["subject"]["kind"], r["subject"]["id"]) == (kind, ident) or (r["object"]["kind"], r["object"]["id"]) == (kind, ident)]


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if "--check" in argv:
        p = problems()
        for x in p:
            print("FAIL  " + x)
        if not p:
            doc = json.loads(OUT.read_text(encoding="utf-8"))
            print(f"PASS  {len(doc['relations'])} relations, all with a source, valid against the schema, weak ones marked, nothing unreviewed approved")
        return 1 if p else 0
    if "--neighbours" in argv:
        for r in neighbours(argv[argv.index("--neighbours") + 1]):
            print(f"{r['id']}: {r['subject']['id']} --{r['predicate']}--> {r['object'].get('label') or r['object']['id']}  [{r['confidence']}, {', '.join(r['sources'])}]")
        return 0
    doc = build()
    OUT.write_text(dump(doc), encoding="utf-8", newline="\n")
    print(f"wrote {len(doc['relations'])} relations to {OUT.relative_to(ROOT).as_posix()}: " + ", ".join(f"{k} {v}" for k, v in doc["counts"].items()))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
