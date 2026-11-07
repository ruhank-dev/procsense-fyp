"""P3 final integration plans (docs/plans/p3/): checks every plan and writes the task index.

Every plan file `docs/plans/p3/p3-NN-<slug>.md` must have the sixteen numbered sections in order, a task table whose ids are
`P3-<CODE>-NN` with the plan's own code, a known owner and status, and dependencies that name existing P3 task ids (or existing
P0-P2 step ids, which are external and not ordered here). The dependency graph must have no cycle. The generated page
`docs/plans/p3/p3-task-index.md` lists every task in a valid implementation order (wave = longest dependency chain).

Usage: uv run --project tools tools/plans/p3_plans.py [--check]
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
P3 = ROOT / "docs" / "plans" / "p3"
INDEX = P3 / "p3-task-index.md"
SECTIONS = [
    "Objective and final user-facing behaviour", "Current implementation status", "Existing code, assets, scenes and systems to reuse",
    "Missing functionality", "Architecture and integrations", "Implementation tasks", "Dependencies and execution order",
    "Frontend components", "Backend and API changes", "AI and voice integration", "Unity objects, prefabs, animations and assets",
    "Performance", "Error handling and edge cases", "Testing", "Acceptance criteria", "Definition of Done",
]
OWNERS = {"Ruhan", "Moiz", "Sannan", "Team", "Owner"}
STATUSES = {"Planned", "In progress", "Done", "Blocked"}
TASK_ID = re.compile(r"^P3-([A-Z]{2,5})-(\d{2})$")
STEP_ID = re.compile(r"^p[0-2]-[a-z0-9-]+-s\d{2}$")
ROW = re.compile(r"^\| (P3-[A-Z]{2,5}-\d{2}) \|")


def plan_files() -> list[Path]:
    return sorted(P3.glob("p3-[0-9][0-9]-*.md"))


def existing_steps() -> set[str]:
    out = set()
    for p in (ROOT / "docs" / "plans").glob("p[012]/p[012]-*/steps/*.md"):
        m = re.match(r"(p[0-2]-[a-z0-9-]+?-s\d{2})-", p.name.removeprefix("✅ "))
        if m:
            out.add(m.group(1))
    return out


def parse(path: Path) -> tuple[str, list[dict], list[str]]:
    """The plan's code, its task rows and the problems found in its shape."""
    text = path.read_text(encoding="utf-8")
    problems = []
    first = text.splitlines()[0] if text else ""
    m = re.match(r"^# (p3-\d{2}-[a-z0-9-]+) — .+ \(([A-Z]{2,5})\)$", first)
    if not m or m.group(1) != path.stem:
        problems.append(f"{path.name}: first line must be '# {path.stem} — <title> (<CODE>)'")
        return "", [], problems
    code = m.group(2)
    heads = re.findall(r"^## (\d+)\. (.+)$", text, flags=re.M)
    want = [(str(i + 1), s) for i, s in enumerate(SECTIONS)]
    if heads != want:
        missing = [f"{n}. {s}" for n, s in want if (n, s) not in heads]
        problems.append(f"{path.name}: the sixteen sections must appear once each, in order" + (f" (missing: {'; '.join(missing[:3])})" if missing else " (order or extra headings)"))
    tasks = []
    for n, line in enumerate(text.splitlines(), 1):
        if not ROW.match(line):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) != 6:
            problems.append(f"{path.name}:{n}: a task row needs 6 cells (ID, Task, Owner, Depends on, Done when, Status), found {len(cells)}")
            continue
        tid, title, owner, deps, done, status = cells
        tm = TASK_ID.match(tid)
        if tm.group(1) != code:
            problems.append(f"{path.name}:{n}: {tid} does not use the plan code {code}")
        if owner not in OWNERS:
            problems.append(f"{path.name}:{n}: {tid} owner '{owner}' is not one of {sorted(OWNERS)}")
        if status not in STATUSES:
            problems.append(f"{path.name}:{n}: {tid} status '{status}' is not one of {sorted(STATUSES)}")
        if not title or not done:
            problems.append(f"{path.name}:{n}: {tid} needs a task and a 'done when'")
        dep_ids = [] if deps in ("", "-", "—") else [d.strip().strip("`") for d in deps.split(",")]
        tasks.append({"id": tid, "title": title, "owner": owner, "deps": dep_ids, "status": status, "plan": path.stem, "line": n})
    if not tasks:
        problems.append(f"{path.name}: no task rows found")
    return code, tasks, problems


def check_graph(tasks: dict[str, dict], steps: set[str]) -> list[str]:
    problems = []
    for t in tasks.values():
        for d in t["deps"]:
            if d in tasks:
                continue
            if STEP_ID.match(d):
                if d not in steps:
                    problems.append(f"{t['plan']}.md:{t['line']}: {t['id']} depends on the unknown step {d}")
            else:
                problems.append(f"{t['plan']}.md:{t['line']}: {t['id']} depends on the unknown task {d}")
    return problems


def waves(tasks: dict[str, dict]) -> tuple[dict[str, int], list[str]]:
    """Longest-path wave per task (1 = no P3 dependency); the ids in a cycle when there is one."""
    wave: dict[str, int] = {}
    state: dict[str, int] = {}
    cycle: list[str] = []

    def visit(tid: str, path: list[str]) -> int:
        if state.get(tid) == 2:
            return wave[tid]
        if state.get(tid) == 1:
            cycle.extend(path[path.index(tid):] + [tid])
            return 0
        state[tid] = 1
        w = 1
        for d in tasks[tid]["deps"]:
            if d in tasks:
                w = max(w, visit(d, path + [tid]) + 1)
                if cycle:
                    break
        state[tid] = 2
        wave[tid] = w
        return w

    for tid in sorted(tasks):
        visit(tid, [])
        if cycle:
            break
    return wave, cycle


def render(tasks: dict[str, dict], wave: dict[str, int], plans: list[Path]) -> str:
    lines = ["# P3 task index (generated)", "",
             "_Generated by `tools/plans/p3_plans.py` from the task tables of the P3 plans; do not edit by hand. Wave = the longest chain of P3 dependencies before the task (wave 1 needs no other P3 task). Tasks in one wave can run in parallel when their owners differ._", ""]
    total = len(tasks)
    by_status = {s: sum(1 for t in tasks.values() if t["status"] == s) for s in sorted(STATUSES)}
    by_owner = {o: sum(1 for t in tasks.values() if t["owner"] == o) for o in sorted(OWNERS)}
    lines += [f"**{total} tasks** in **{len(plans)} plans** · " + " · ".join(f"{s} {n}" for s, n in by_status.items()) + f" · {max(wave.values(), default=0)} waves", "",
              "Owners: " + ", ".join(f"{o} {n}" for o, n in by_owner.items() if n), "", "## Plans", "", "| Plan | Tasks |", "|---|---|"]
    for p in plans:
        n = sum(1 for t in tasks.values() if t["plan"] == p.stem)
        lines.append(f"| [{p.stem}]({p.name}) | {n} |")
    for w in range(1, max(wave.values(), default=0) + 1):
        ids = sorted((t for t in tasks if wave[t] == w), key=lambda t: (tasks[t]["plan"], t))
        lines += ["", f"## Wave {w} ({len(ids)})", "", "| Task | Plan | Owner | Depends on | Status |", "|---|---|---|---|---|"]
        for t in ids:
            d = tasks[t]
            deps = ", ".join(d["deps"]) or "—"
            lines.append(f"| {t} {d['title']} | [{d['plan']}]({d['plan']}.md) | {d['owner']} | {deps} | {d['status']} |")
    return "\n".join(lines) + "\n"


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    plans = plan_files()
    if not plans:
        print("p3_plans: no plan files in docs/plans/p3")
        return 1
    problems, tasks, codes = [], {}, {}
    for p in plans:
        code, rows, probs = parse(p)
        problems += probs
        if code and code in codes:
            problems.append(f"{p.name}: plan code {code} is already used by {codes[code]}")
        codes[code] = p.name
        for r in rows:
            if r["id"] in tasks:
                problems.append(f"{p.name}:{r['line']}: duplicate task id {r['id']}")
            tasks[r["id"]] = r
    problems += check_graph(tasks, existing_steps())
    wave, cycle = waves(tasks) if not problems else ({}, [])
    if cycle:
        problems.append("dependency cycle: " + " -> ".join(cycle))
    if problems:
        print("\n".join(problems))
        return 1
    page = render(tasks, wave, plans)
    if "--check" in argv:
        if not INDEX.exists() or INDEX.read_text(encoding="utf-8") != page:
            print("docs/plans/p3/p3-task-index.md is out of date: run uv run --project tools tools/plans/p3_plans.py")
            return 1
        print(f"p3 plans: {len(plans)} plans, {len(tasks)} tasks, {max(wave.values())} waves, index current")
        return 0
    INDEX.write_text(page, encoding="utf-8", newline="\n")
    print(f"wrote {INDEX.name}:{len(plans)} plans, {len(tasks)} tasks, {max(wave.values())} waves")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
