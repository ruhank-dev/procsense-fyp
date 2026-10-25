"""Check that the planning documents, the plan step files and the pipeline JSON agree with each other (not only that the JSON is internally valid).

Usage: uv run --project tools tools/plans/check_planning_sync.py     (exit 1 on any inconsistency)

Checks:
  files     every feature has a plan file; derived exports (Cytoscape, Mermaid, registry, audit) match the JSON counts
  steps     the step files on disk, milestones.md and the JSON phase tasks agree; no new-module step file is outside a phase
  trace     plan documents and step files cite only ids that exist in the JSON (reverse of VR-G-TRACE)
  registers every DT id cited anywhere exists in docs/deferred-tasks.md; the register is in order
  index     docs/index.md lists every top-level entry of docs/ and every top-level code folder, and its links resolve
  stale     counts or wording from earlier planning passes do not survive in the living documents
Standard library only.
"""
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
ARCH = ROOT / "docs" / "architecture"
NEW_MODULES = ("p1-dataset-analysis", "p1-live-scenario-generation", "p1-ai-infrastructure", "p1-personalisation", "p2-ai-learning-analytics", "p2-ai-advanced-simulation")
# living documents that must not quote counts from an earlier pass
STALE_PATTERNS = ["65 features", "19 phases", "158 nodes", "10 ways", "13 ways", "11 of 11", "14 of 14", "DT-101 to DT-112", "247 steps"]
STALE_ARCH = ["one 6 GB GPU", "narrative only", "rejected for now", "Rejected for now", "(rejected, E64)", "E64 stays rejected", "E64 rejected"]
ID_PATTERNS = [r"\bLG-\d\d\b", r"\bOFF-\d\d\b", r"\bLP-\d\d\b", r"\bCMP-[A-Z]+\b", r"\bMC-\d\d\b", r"\bDS-\d{4}\b", r"\bPH-\d\d\b", r"\bFB-\d\d\b", r"\bOQ-\d\d\b",
               r"\bEXT-[A-Z0-9]+\b", r"\bVR-(?:D|CM|G)-[A-Z0-9-]*[A-Z0-9]\b"]


def git_ignored(path) -> bool:
    """True when git ignores this path (so a local, untracked toolkit folder inside docs/ does not count as a project document)."""
    import subprocess
    try:
        return subprocess.run(["git", "check-ignore", "-q", str(path)], cwd=ROOT, capture_output=True).returncode == 0
    except OSError:
        return False


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8", errors="replace")


def main() -> int:
    problems: list[str] = []
    doc = json.loads(read(ARCH / "procsense_full_pipeline.json"))
    nodes = {n["id"]: n for n in doc["nodes"]}
    feats = {i: n for i, n in nodes.items() if n["type"] == "feature"}
    ids = set(nodes) | {p["id"] for p in doc["phases"]} | {r["id"] for r in doc["validation_rules"]} | {f["id"] for f in doc["fallback_paths"]} | {q["id"] for q in doc["open_questions"]} \
        | {x["id"] for x in doc["external_dependencies"]}

    # files
    PLANS = ARCH.parent / "plans" / "features"
    plan_files = {(p.stem[4:] if p.stem.startswith("[x] ") else p.stem) for p in PLANS.glob("*.md")} if PLANS.exists() else set()
    stray = sorted(p.name for p in (ARCH / "feature_plans").glob("*.md") if p.name != "README.md")
    if stray:
        problems.append(f"files: plan files must live only in docs/plans/features, found in docs/architecture/feature_plans: {stray}")
    if plan_files != set(feats):
        problems.append(f"files: feature plan files differ from features: {sorted(plan_files ^ set(feats))}")
    reg = read(ARCH / "feature_registry.md")
    if len(re.findall(r"^### [A-E]\d+ — ", reg, re.M)) != len(feats):
        problems.append("files: feature_registry.md entry count differs from the JSON")
    cy = json.loads(read(ARCH / "procsense_pipeline.cytoscape.json"))["elements"]
    if len(cy) != len(doc["groups"]) + len(nodes) + len(doc["edges"]):
        problems.append("files: Cytoscape export element count differs from the JSON")
    audit = read(ARCH / "feature_coverage_audit.md")
    if f"| Features | {len(feats)} |" not in audit:
        problems.append("files: coverage audit feature total differs from the JSON")
    mm = read(ARCH / "pipeline_overview.mmd")
    for n in nodes.values():
        if n["type"] in ("entry", "exit", "fallback", "stage", "offline_step") and n["id"].replace("-", "_") not in mm:
            problems.append(f"files: Mermaid overview lacks node {n['id']}")

    # Synverk flow exports
    flow_dir = ARCH / "flows"
    flow_files = sorted(flow_dir.glob("*.json")) if flow_dir.exists() else []
    if len(flow_files) < 10:
        problems.append(f"files: expected the 10 Synverk flow files in docs/architecture/flows, found {len(flow_files)}")
    for ff in flow_files:
        if ff.stat().st_size >= 64 * 1024:
            problems.append(f"files: {ff.name} is {ff.stat().st_size} bytes (limit 65536)")
        fl = json.loads(read(ff))
        if len(fl["nodes"]) > 30 or len(fl.get("edges", [])) > 60 or len(fl.get("notes", [])) > 6:
            problems.append(f"files: {ff.name} exceeds the flowchart limits (30 nodes, 60 edges, 6 notes)")
        for fn in fl["nodes"]:
            if fn["id"] != "OFFLINE" and fn["id"] not in nodes and fn["id"] not in {p["id"] for p in doc["phases"]}:
                problems.append(f"trace: {ff.name} node {fn['id']} is not in the pipeline JSON")

    # steps
    steps = {}
    for p in (ROOT / "docs" / "plans").glob("p*/p*-*/steps/*.md"):
        m = re.match(r"(p[0-2]-[a-z0-9-]+?-s\d{2})-", p.name.removeprefix("✅ "))
        if m:
            steps[m.group(1)] = p
    msx = re.search(r"\*\*(\d+) steps\*\*", read(ROOT / "docs" / "plans" / "milestones.md"))
    if not msx or int(msx.group(1)) != len(steps):
        problems.append(f"steps: milestones.md step count {msx.group(1) if msx else '?'} differs from {len(steps)} step files")
    phased = {t for p in doc["phases"] for t in p["tasks"]}
    for s in sorted(steps):
        if s.startswith(NEW_MODULES) and s not in phased:
            problems.append(f"steps: {s} is in no roadmap phase")
    for t in phased:
        if t not in steps:
            problems.append(f"steps: phase task {t} has no step file")

    # trace (documents -> JSON)
    docs = list(ARCH.glob("*.md")) + list((ARCH.parent / "plans" / "features").glob("*.md")) + [steps[s] for s in steps if s.startswith(NEW_MODULES)]
    for d in docs:
        text = re.sub(r"`[^`\n]*<[A-Za-z]+>[^`\n]*`", "", read(d))
        found = set()
        for pat in ID_PATTERNS:
            found |= set(re.findall(pat, text))
        found = {i for i in found if i not in ("VR-G", "VR-CM", "VR-D")}
        for i in sorted(found - ids):
            problems.append(f"trace: {d.relative_to(ROOT).as_posix()} cites {i}, which is not in the pipeline JSON")
        for fid in set(re.findall(r"\b([A-E]\d{1,2})\b(?![.\w-])", text)):
            if re.fullmatch(r"[A-E]\d{1,2}", fid) and fid not in feats and int(fid[1:]) >= 1 and d.name != "feature_registry.md":
                # only flag ids that look like feature ids used as such (A1..E69); ignore other short codes
                if 1 <= int(fid[1:]) <= 69 and fid[0] in "ABCDE":
                    problems.append(f"trace: {d.relative_to(ROOT).as_posix()} cites feature {fid}, which is not in the pipeline JSON")
    for s, p in steps.items():
        if s.startswith(NEW_MODULES):
            names = re.findall(r"\bp[0-2]-[a-z0-9-]+?-s\d{2}\b", read(p))
            for ref in names:
                if ref not in steps:
                    problems.append(f"trace: step {s} cites missing step {ref}")

    # registers
    dt = read(ROOT / "docs" / "deferred-tasks.md")
    order = re.findall(r"^\| (DT-\d+) \|", dt, re.M)
    have = set(order)
    tail = [d for d in order if int(d[3:]) >= 96]
    if tail != sorted(tail):
        problems.append(f"registers: the DT register is out of order near {tail[-8:]}")
    cited = set()
    skip = ("previous_prompt", "ProcSense_Implementation", "ProcSense_Master", "ProcSense_Medical", "ProcSense_Data_Source", "CONVERSION_REPORT")
    for p in (ROOT / "docs").rglob("*.md"):
        if not any(k in p.name for k in skip) and not git_ignored(p):
            cited |= set(re.findall(r"DT-\d{3}", read(p)))
    for d in sorted(cited - have):
        problems.append(f"registers: {d} is cited but missing from docs/deferred-tasks.md")

    # index: docs/index.md must list every top-level entry of docs/ and every top-level code folder
    idx_path = ROOT / "docs" / "index.md"
    if not idx_path.exists():
        problems.append("index: docs/index.md is missing")
    else:
        idx = read(idx_path)
        for e in sorted((ROOT / "docs").iterdir()):
            if git_ignored(e):
                continue                          # a local, git-ignored folder (a downloaded toolkit) is not part of the project's documents
            if not re.search(r"^\| \[" + re.escape(e.name) + r"/?\]", idx, re.M):
                problems.append(f"index: docs/{e.name} has no row in section 3 of docs/index.md")
        for name in ("src", "tests", "content", "tools", "server", "unity", ".github", "Taskfile.yml"):
            if name not in idx:
                problems.append(f"index: top-level {name} is not described in docs/index.md")
        for link in re.findall(r"(?<!\!)\[[^\]\n]*\]\(([^)\s]+)\)", idx):
            if not re.match(r"^(https?:|mailto:|#)", link) and not (ROOT / "docs" / link.split("#", 1)[0]).exists():
                problems.append(f"index: docs/index.md links to missing {link}")
        plan_readme = read(ROOT / "docs" / "plans" / "README.md")
        for mod in NEW_MODULES:
            if mod not in plan_readme:
                problems.append(f"index: docs/plans/README.md phase map lacks {mod}")
        if re.search(r"\b\d+ steps, M0", plan_readme):
            problems.append("stale: docs/plans/README.md quotes a fixed step count")

    # stale
    living = [ROOT / "docs/ai-roadmap-status.md", ROOT / "docs/context/ProcSense_Team_Board.md", ARCH / "README.md", ARCH / "pipeline_json_guide.md", ROOT / "docs/CI.md",
              ARCH / "testing_evaluation_plan.md", ARCH / "architecture_integration_plan.md", ARCH / "ai_scenario_generation_architecture.md"]
    for p in living:
        t = read(p)
        for pat in STALE_PATTERNS:
            if pat in t:
                problems.append(f"stale: {p.relative_to(ROOT).as_posix()} contains '{pat}'")
    for p in ARCH.glob("*.md"):
        t = read(p)
        for pat in STALE_ARCH:
            if pat in t:
                problems.append(f"stale: docs/architecture/{p.name} contains '{pat}'")
    js = read(ARCH / "procsense_full_pipeline.json")
    for pat in STALE_ARCH + ['"method": "n/a"']:
        if pat in js:
            problems.append(f"stale: pipeline JSON contains '{pat}'")
    for f in feats.values():
        if f["status"] == "Superseded or rejected" and not f["plan"]["reassessment"]:
            problems.append(f"stale: rejected feature {f['id']} has no reassessment")

    for p in problems:
        print("FAIL  " + p)
    print(f"{'PASS' if not problems else 'FAIL'}  planning documents agree with the pipeline JSON ({len(feats)} features, {len(nodes)} nodes, {len(steps)} step files, {len(docs)} documents scanned; {len(problems)} problems)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
