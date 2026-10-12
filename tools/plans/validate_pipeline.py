"""Validate docs/architecture/procsense_full_pipeline.json (the canonical planning graph).

Usage: uv run --project tools tools/plans/validate_pipeline.py           (report; exit 1 on any error)
       uv run --project tools tools/plans/validate_pipeline.py --quiet   (errors and the summary line only)
       ... --file other.json                                             (validate another copy; used by the tests)

Checks (validation rules VR-G-* in the JSON):
  parse and top-level fields; unique ids per list; edge endpoints exist; every referenced id exists
  (groups, models, datasets, phases, tasks as plan step files, features, nodes); every feature is realised by a node
  and mapped to a task in a phase; every live stage is reachable from an entry and reaches an exit; every cycle in
  the flow graph passes through an edge marked as a documented cycle; nothing planned is labelled implemented
  (implemented items must cite evidence paths that exist); every feature has a complete implementation plan with
  separate capability plans, tasks that exist and sit in a phase, and acceptance criteria (VR-G-PLANS); every node and edge traces back to feature plans or plan
  step files that exist (VR-G-TRACE). Standard library only.
"""
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
PIPELINE = ROOT / "docs" / "architecture" / "procsense_full_pipeline.json"
TOP = ["schema_version", "project", "metadata", "legend", "groups", "nodes", "edges", "external_dependencies", "datasets",
       "model_catalog", "phases", "validation_rules", "fallback_paths", "open_questions"]
FLOW = {"data_flow", "control", "repair", "fallback", "feedback", "produces", "uses_data", "component_of"}
IMPLEMENTED = "Implemented and tested"
NOT_BUILT = {"Planned", "Proposed new feature", "Blocked", "Deferred", "Superseded or rejected"}
PATHLIKE = re.compile(r"^[A-Za-z0-9_.\-]+(/[A-Za-z0-9_.\-*<>]+)+/?")


def step_ids() -> set[str]:
    out = set()
    for p in (ROOT / "docs" / "plans").glob("p*/p*-*/steps/*.md"):
        m = re.match(r"(p[0-2]-[a-z0-9-]+?-s\d{2})-", p.name.removeprefix("✅ "))
        if m:
            out.add(m.group(1))
    return out


def evidence_paths(items: list[str]) -> list[str]:
    """Leading path-like token of each evidence string (text after it is a note)."""
    paths = []
    for s in items:
        m = PATHLIKE.match(s.strip())
        if m and "*" not in m.group(0) and "<" not in m.group(0):
            paths.append(m.group(0).rstrip("/"))
    return paths


PLAN_FIELDS = ["user_experience", "exists", "missing", "deterministic_foundation", "ai_questions", "capabilities", "integration", "prerequisites",
               "status_rationale", "reassessment", "staged_versions", "deliverables", "definition_of_done"]
MAY_BE_EMPTY = {"prerequisites", "missing", "exists"}
Q_FIELDS = ["what_ai_does", "why_ai_is_useful", "method_split", "deterministic_still_needed", "ai_implemented", "ai_missing", "must_build", "better_than_baseline"]
CAP_FIELDS = ["id", "name", "kind", "status", "purpose", "method", "input_schema", "output_schema", "flow", "validation", "failure_handling", "tests",
              "metrics", "baseline_comparison", "acceptance", "hardware", "tasks"]
FAIL_FIELDS = ["errors", "hallucinations", "inconsistency", "model_unavailable", "invalid_input"]
INTEG_FIELDS = ["unity", "backend", "simulation_state", "other_features"]


def plan_problems(f: dict, steps: set[str], phased: set[str], statuses: set[str]) -> list[str]:
    """Why a feature's implementation plan is not complete (empty list = complete)."""
    out = []
    pl = f.get("plan")
    if not isinstance(pl, dict):
        return ["no implementation plan"]
    for k in PLAN_FIELDS:
        if k not in pl or (not pl[k] and k not in MAY_BE_EMPTY):
            out.append(f"plan field '{k}' missing or empty")
    for k in Q_FIELDS:
        if not pl.get("ai_questions", {}).get(k):
            out.append(f"AI question '{k}' unanswered")
    for k in INTEG_FIELDS:
        if not pl.get("integration", {}).get(k):
            out.append(f"integration '{k}' missing")
    if not pl.get("staged_versions", {}).get("initial") or not pl.get("staged_versions", {}).get("future"):
        out.append("staged versions missing")
    caps = pl.get("capabilities", [])
    for c in caps:
        for k in CAP_FIELDS:
            if not c.get(k):
                out.append(f"capability {c.get('id')} field '{k}' empty")
        for k in FAIL_FIELDS:
            if not c.get("failure_handling", {}).get(k):
                out.append(f"capability {c.get('id')} failure handling '{k}' missing")
        for k in ("gtx1660", "rtx4050", "ram", "latency", "deployment"):
            if not c.get("hardware", {}).get(k):
                out.append(f"capability {c.get('id')} hardware '{k}' missing")
        if c.get("status") not in statuses:
            out.append(f"capability {c.get('id')} status '{c.get('status')}' not in legend")
        for t in c.get("tasks", []):
            if t not in steps:
                out.append(f"capability {c.get('id')} task {t} has no step file")
            elif t not in phased:
                out.append(f"capability {c.get('id')} task {t} is in no phase")
    ai_caps = [c for c in caps if str(c.get("kind", "")).startswith("AI") or "learned" in str(c.get("kind", "")) or "learning" in str(c.get("kind", ""))]
    if f["kind"] in ("ai", "hybrid") and not ai_caps:
        out.append("feature needs AI but has no AI capability plan")
    if f["status"] == "Partially implemented" and f["kind"] in ("ai", "hybrid") and not [c for c in ai_caps if c["status"] != IMPLEMENTED]:
        out.append("partially implemented feature has no separate plan for its missing AI part")
    if f["status"] in ("Blocked", "Deferred") and not pl.get("prerequisites"):
        out.append("blocked or deferred feature lists no prerequisites")
    if f["status"] == "Superseded or rejected" and not pl.get("reassessment"):
        out.append("rejected feature has no reassessment")
    return out


def coverage(doc: dict) -> dict:
    """Feature coverage audit: computed, never asserted."""
    steps = step_ids()
    phased = {t for p in doc["phases"] for t in p["tasks"]}
    statuses = set(doc["legend"]["statuses"])
    rows = []
    for f in (n for n in doc["nodes"] if n["type"] == "feature"):
        probs = plan_problems(f, steps, phased, statuses)
        caps = f.get("plan", {}).get("capabilities", []) if isinstance(f.get("plan"), dict) else []
        tasks = sorted({t for c in caps for t in c.get("tasks", [])} | set(f["tasks"]))
        mapped = bool(tasks) and all(t in steps and t in phased for t in tasks)
        with_acc = bool(caps) and all(c.get("acceptance") for c in caps)
        ai_caps = [c for c in caps if str(c.get("kind", "")).startswith("AI") or "learned" in str(c.get("kind", "")) or "learning" in str(c.get("kind", ""))]
        rows.append(dict(id=f["id"], label=f["label"], status=f["status"], kind=f["kind"], complete=not probs, problems=probs, capabilities=len(caps), ai_capabilities=len(ai_caps),
                         tasks=tasks, mapped=mapped, acceptance=with_acc, phase=f["phase"]))
    total = len(rows)
    return dict(total=total, complete=sum(r["complete"] for r in rows), mapped=sum(r["mapped"] for r in rows), acceptance=sum(r["acceptance"] for r in rows),
                capabilities=sum(r["capabilities"] for r in rows), ai_capabilities=sum(r["ai_capabilities"] for r in rows), rows=rows)


def main() -> int:
    quiet = "--quiet" in sys.argv
    path = Path(sys.argv[sys.argv.index("--file") + 1]) if "--file" in sys.argv else PIPELINE
    errors, notes = [], []
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as e:
        print(f"FAIL  parse: {e}")
        return 1
    errors += [f"missing top-level field '{k}'" for k in TOP if k not in doc]
    if errors:
        print("\n".join("FAIL  " + e for e in errors))
        return 1

    statuses = set(doc["legend"]["statuses"])
    lists = {k: doc[k] for k in ["groups", "nodes", "edges", "external_dependencies", "datasets", "model_catalog", "phases", "validation_rules", "fallback_paths", "open_questions"]}
    for name, items in lists.items():
        seen = set()
        for it in items:
            if it["id"] in seen:
                errors.append(f"VR-G-UNIQUE: duplicate id {it['id']} in {name}")
            seen.add(it["id"])

    nodes = {n["id"]: n for n in doc["nodes"]}
    groups = {g["id"] for g in doc["groups"]}
    models = {m["id"] for m in doc["model_catalog"]}
    datasets = {d["id"] for d in doc["datasets"]}
    phases = {p["id"]: p for p in doc["phases"]}
    steps = step_ids()
    features = {i: n for i, n in nodes.items() if n["type"] == "feature"}

    for g in doc["groups"]:
        if g.get("parent") and g["parent"] not in groups:
            errors.append(f"VR-G-REFS: group {g['id']} parent {g['parent']} missing")
    for e in doc["edges"]:
        for end in ("source", "target"):
            if e[end] not in nodes:
                errors.append(f"VR-G-ENDPOINTS: edge {e['id']} {end} {e[end]} is not a node")
        if e["type"] not in doc["legend"]["edge_types"]:
            errors.append(f"VR-G-REFS: edge {e['id']} type {e['type']} not in legend")

    task_owner = defaultdict(list)
    for p in doc["phases"]:
        for d in p["depends_on"]:
            if d not in phases:
                errors.append(f"VR-G-REFS: phase {p['id']} depends on unknown {d}")
        if p["status"] not in statuses:
            errors.append(f"VR-G-REFS: phase {p['id']} status '{p['status']}' not in legend")
        for t in p["tasks"]:
            task_owner[t].append(p["id"])
            if t not in steps:
                errors.append(f"VR-G-REFS: phase {p['id']} task {t} has no plan step file")
        for f in p["features"]:
            if f not in features:
                errors.append(f"VR-G-REFS: phase {p['id']} lists unknown feature {f}")
    for t, owners in task_owner.items():
        if len(owners) > 1:
            errors.append(f"VR-G-UNIQUE: task {t} is in more than one phase {owners}")

    for nid, n in nodes.items():
        if n.get("group") and n["group"] not in groups:
            errors.append(f"VR-G-REFS: node {nid} group {n['group']} missing")
        if n["type"] not in doc["legend"]["node_types"]:
            errors.append(f"VR-G-REFS: node {nid} type {n['type']} not in legend")
        if n["type"] not in ("dataset", "model") and n.get("status") not in statuses:
            errors.append(f"VR-G-REFS: node {nid} status '{n.get('status')}' not in legend")
        if n["type"] == "dataset" and nid not in datasets:
            errors.append(f"VR-G-REFS: dataset node {nid} has no datasets entry")
        if n["type"] == "model" and nid not in models:
            errors.append(f"VR-G-REFS: model node {nid} has no model_catalog entry")
        for t in n.get("tasks", []):
            if t not in steps:
                errors.append(f"VR-G-REFS: {nid} task {t} has no plan step file")
        # honesty: implemented things cite evidence that exists; nothing implemented points at planned code
        if n.get("status") == IMPLEMENTED or n.get("ai_status") == IMPLEMENTED:
            paths = evidence_paths(n.get("evidence", []))
            if not paths:
                errors.append(f"VR-G-HONEST: {nid} is '{IMPLEMENTED}' but cites no evidence path")
            for p in paths:
                if not (ROOT / p).exists():
                    errors.append(f"VR-G-HONEST: {nid} evidence path {p} does not exist")
            if "(planned)" in str(n.get("component", "")) and "exists" not in str(n.get("component", "")):
                errors.append(f"VR-G-HONEST: {nid} is implemented but its component is only planned")
        if n.get("status") in NOT_BUILT and n.get("ai_status") == IMPLEMENTED:
            errors.append(f"VR-G-HONEST: {nid} overall '{n['status']}' but AI part '{IMPLEMENTED}'")

    for fid, f in features.items():
        if f["ai_status"] not in statuses and not f["ai_status"].startswith("Not applicable"):
            errors.append(f"VR-G-REFS: feature {fid} ai_status '{f['ai_status']}' not in legend")
        if not f["realised_by"]:
            errors.append(f"VR-G-COVERAGE: feature {fid} is not realised by any node")
        if not f["tasks"]:
            errors.append(f"VR-G-COVERAGE: feature {fid} has no task")
        if not any(t in task_owner for t in f["tasks"]):
            errors.append(f"VR-G-COVERAGE: feature {fid} tasks {f['tasks']} are in no phase")
        if f["phase"] not in phases:
            errors.append(f"VR-G-REFS: feature {fid} phase {f['phase']} missing")
        for r in f["realised_by"] + f["depends_on"]:
            if r not in nodes:
                errors.append(f"VR-G-REFS: feature {fid} references unknown node {r}")
        for m in f["model_refs"]:
            if m not in models:
                errors.append(f"VR-G-REFS: feature {fid} model {m} missing")
        for d in f["dataset_refs"]:
            if d not in datasets and d not in nodes:
                errors.append(f"VR-G-REFS: feature {fid} dataset {d} missing")
        for o in f["overlaps"]:
            ref = o.split(" ", 1)[0]
            if ref not in features:
                errors.append(f"VR-G-REFS: feature {fid} overlap {ref} is not a feature")
    for d in doc["datasets"]:
        for t in d["feeds"]:
            if t not in nodes:
                errors.append(f"VR-G-REFS: dataset {d['id']} feeds unknown {t}")
        for f in d["features"]:
            if f not in features:
                errors.append(f"VR-G-REFS: dataset {d['id']} lists unknown feature {f}")
    for fb in doc["fallback_paths"]:
        for k in ("at", "to"):
            ref = fb[k].split(" ", 1)[0]
            if k == "at" and ref not in nodes:
                errors.append(f"VR-G-REFS: fallback {fb['id']} at unknown node {ref}")
    for r in doc["validation_rules"]:
        ref = r["enforced_at"]
        if not ref.startswith("tools/") and ref not in nodes:
            errors.append(f"VR-G-REFS: rule {r['id']} enforced at unknown {ref}")
    for i in doc["metadata"]["critical_path"]:
        if i not in phases:
            errors.append(f"VR-G-REFS: critical path phase {i} missing")

    # reachability and cycles on the flow graph
    adj, adj_acyclic = defaultdict(list), defaultdict(list)
    for e in doc["edges"]:
        if e["type"] in FLOW and e["source"] in nodes and e["target"] in nodes:
            adj[e["source"]].append(e["target"])
            if "cycle" not in e:
                adj_acyclic[e["source"]].append(e["target"])

    def reach(starts, graph):
        seen, todo = set(starts), list(starts)
        while todo:
            for nxt in graph[todo.pop()]:
                if nxt not in seen:
                    seen.add(nxt)
                    todo.append(nxt)
        return seen

    entries = [i for i, n in nodes.items() if n["type"] == "entry"]
    exits = {i for i, n in nodes.items() if n["type"] == "exit"}
    from_entry = reach(entries, adj)
    rev = defaultdict(list)
    for a, bs in adj.items():
        for b in bs:
            rev[b].append(a)
    to_exit = reach(list(exits), rev)
    stages = [i for i, n in nodes.items() if n["type"] in ("stage", "offline_step", "runtime_step")]
    for s in stages:
        if s not in from_entry:
            errors.append(f"VR-G-ENTRY-EXIT: {s} is not reachable from an entry")
        if s not in to_exit:
            errors.append(f"VR-G-ENTRY-EXIT: {s} cannot reach an exit")
    for x in exits:
        if adj[x]:
            errors.append(f"VR-G-ENTRY-EXIT: exit {x} has outgoing flow edges")

    state = {}

    def dfs(u, stack):
        state[u] = 1
        for v in adj_acyclic[u]:
            if state.get(v) == 1:
                errors.append(f"VR-G-CYCLES: undocumented cycle through {' -> '.join(stack + [u, v])}")
            elif v not in state:
                dfs(v, stack + [u])
        state[u] = 2

    sys.setrecursionlimit(10000)
    for n in nodes:
        if n not in state:
            dfs(n, [])
    cycles = [e for e in doc["edges"] if "cycle" in e]
    for e in cycles:
        if e["type"] not in FLOW:
            errors.append(f"VR-G-CYCLES: cycle edge {e['id']} is not a flow edge")
        if not e["cycle"].get("bound"):
            errors.append(f"VR-G-CYCLES: cycle edge {e['id']} has no bound")

    # VR-G-TRACE: every node and edge is derived from documented plans
    indirect = []
    for nid, n in nodes.items():
        tr = n.get("traced_to")
        if not isinstance(tr, dict) or not (tr.get("features") or tr.get("steps")):
            errors.append(f"VR-G-TRACE: node {nid} traces to no feature plan or step file")
            continue
        for fid in tr["features"]:
            if fid not in features:
                errors.append(f"VR-G-TRACE: node {nid} traces to unknown feature {fid}")
        for t in tr["steps"]:
            if t not in steps:
                errors.append(f"VR-G-TRACE: node {nid} traces to missing step file {t}")
        if n["type"] == "feature" and tr["features"] != [nid]:
            errors.append(f"VR-G-TRACE: feature {nid} must trace to its own plan")
        if tr.get("kind") == "indirect":
            indirect.append(nid)
    for e in doc["edges"]:
        tr = e.get("traced_to")
        if not isinstance(tr, dict) or not (tr.get("features") or tr.get("steps")):
            errors.append(f"VR-G-TRACE: edge {e['id']} ({e['source']} -> {e['target']}) traces to no plan")
        if e["type"] in ("realised_by", "depends_on") and e["source"] not in features:
            errors.append(f"VR-G-TRACE: {e['type']} edge {e['id']} does not start at a feature")
    derived = {(f["id"], r, "realised_by") for f in features.values() for r in f["realised_by"]} | {(f["id"], r, "depends_on") for f in features.values() for r in f["depends_on"]}
    present = {(e["source"], e["target"], e["type"]) for e in doc["edges"] if e["type"] in ("realised_by", "depends_on")}
    for x in sorted(derived - present):
        errors.append(f"VR-G-TRACE: feature field {x[2]} {x[0]} -> {x[1]} has no edge")
    for x in sorted(present - derived):
        errors.append(f"VR-G-TRACE: edge {x[2]} {x[0]} -> {x[1]} is not in any feature plan")
    for p in doc["phases"]:
        want = sorted((fid for fid, f in features.items() if f["phase"] == p["id"]), key=lambda x: (x[0], int(x[1:])))
        if p["features"] != want:
            errors.append(f"VR-G-TRACE: phase {p['id']} feature list differs from the features that name it")
    for x in doc["external_dependencies"]:
        for u in x["used_by"]:
            if u not in nodes and not u == "visualisation":
                errors.append(f"VR-G-TRACE: external dependency {x['id']} used_by unknown {u}")
    for g in doc["groups"]:
        if not any(n.get("group") == g["id"] for n in doc["nodes"]) and not any(h.get("parent") == g["id"] for h in doc["groups"]):
            errors.append(f"VR-G-TRACE: group {g['id']} is empty")

    cov = coverage(doc)
    for r in cov["rows"]:
        for prob in r["problems"]:
            errors.append(f"VR-G-PLANS: feature {r['id']}: {prob}")
        if not r["mapped"]:
            errors.append(f"VR-G-PLANS: feature {r['id']} has tasks without a step file or phase")

    by_status = defaultdict(int)
    for f in features.values():
        by_status[f["status"]] += 1
    if not quiet:
        print(f"pipeline: {path.name} (schema {doc['schema_version']})")
        print(f"  nodes {len(nodes)} · edges {len(doc['edges'])} · groups {len(groups)} · features {len(features)} · phases {len(phases)} · "
              f"datasets {len(datasets)} · models {len(models)} · rules {len(doc['validation_rules'])} · documented cycles {len(cycles)}")
        print(f"  entries {sorted(entries)} · exits {sorted(exits)} · live and offline stages {len(stages)}")
        print("  features by status: " + ", ".join(f"{k} {v}" for k, v in sorted(by_status.items())))
        print(f"  traceability: {len(nodes) - len(indirect)}/{len(nodes)} nodes trace directly to feature plans or step files; indirect (via adjacent nodes): {', '.join(indirect) or 'none'}; {len(doc['edges'])}/{len(doc['edges'])} edges traced")
        print(f"  coverage: {cov['complete']}/{cov['total']} complete plans · {cov['mapped']}/{cov['total']} mapped to phased tasks · "
              f"{cov['acceptance']}/{cov['total']} with acceptance on every capability · {cov['capabilities']} capabilities ({cov['ai_capabilities']} AI)")
        for n in notes:
            print("  note: " + n)
    for e in errors:
        print("FAIL  " + e)
    print(f"{'PASS' if not errors else 'FAIL'}  pipeline JSON ({len(errors)} errors)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
