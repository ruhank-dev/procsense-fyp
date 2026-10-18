"""The task-by-task reconciliation of ALL steps of P0, P1 and P2 (`docs/plans/task-reconciliation.md`): `uv run --project tools tools/plans/reconcile.py [--check]`.

A checkmark in a step file (the leading tick of its file name) used to record the owner's APPROVAL, not that the work behind it exists, and a step with no tick may be fully built; since 18 October 2026 it is set by tools/plans/tick.py for DONE steps. This tool therefore
reads two things and puts them side by side: what the step files say (found on disk: every step of every P0, P1 and P2 phase) and `docs/plans/reconciliation.json`, a hand-written statement for each step of who
owns it, what was recorded before the 18 October 2026 work, what is true now, the files that prove it, the measured acceptance result and what is still open. The tool refuses to run if a step has no entry, if
an entry names a step that does not exist, if an evidence path does not exist, or if a status is outside the vocabulary, so the table cannot rot silently; --check also fails when the committed page differs
from what the files give. It changes no step file; ticking is done by tools/plans/tick.py and only for a step this data calls DONE.

Status vocabulary (`now`):
  DONE                   built, integrated, tested; the acceptance result is stated
  DONE_GATE_OFF          built and measured, but a measured acceptance criterion is not met, so the feature stays off (the gate says which)
  ENG_DONE               every part that can be built without a person, a device, an account or a licence is built and tested; only the external acceptance remains, named in `open`, with who it needs in `external`
  PARTLY                 the part that needs no one else is NOT yet all done; the rest is named in `open`
  BLOCKED                nothing can be built before the named input (a person, a device, a licence, disk space) exists
  RESEARCH_NEGATIVE      a research step whose trigger was analysed and is not met; the analysis is the deliverable
  APPROVED               a P0 step the owner ticked under the earlier rule (generated, no entry needed); it was not re-audited against its acceptance criteria here
  NOT_RUHAN              owned by someone else; listed so the table is complete, not reconciled here; its acceptance is listed for them in docs/testing/SANNAN_EXTERNAL_TESTING.md
Standard library only.
"""
from __future__ import annotations

import glob
import json
import os
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "docs" / "plans" / "reconciliation.json"
OUT = ROOT / "docs" / "plans" / "task-reconciliation.md"
IMPL_OK = ("DONE", "ENG_DONE")                      # a status that can carry `"implementation": "complete"`
STATUSES = {"APPROVED", "DONE", "DONE_GATE_OFF", "ENG_DONE", "PARTLY", "BLOCKED", "RESEARCH_NEGATIVE", "NOT_RUHAN"}
LABEL = {"APPROVED": "Approved by the owner (earlier rule)", "DONE": "Done", "DONE_GATE_OFF": "Done, feature off", "ENG_DONE": "Engineering done, external acceptance pending", "PARTLY": "Partly done", "BLOCKED": "Blocked", "RESEARCH_NEGATIVE": "Analysed, trigger not met", "NOT_RUHAN": "Not Ruhan's"}


def steps() -> dict[str, dict]:
    out = {}
    for tier in ("p0", "p1", "p2"):
        for phase in sorted(glob.glob(str(ROOT / "docs" / "plans" / tier / "*" / "steps" / "*.md"))):
            name = os.path.basename(phase)
            approved = name.startswith("✅")
            sid = re.match(r"(.+?-s\d{2})", name.replace("✅ ", "")).group(1)
            title = Path(phase).read_text(encoding="utf-8").splitlines()[0].lstrip("# ").split(" — ", 1)[-1]
            out[sid] = {"approved": approved, "title": title, "tier": tier, "file": Path(phase).relative_to(ROOT / "docs" / "plans").as_posix()}
    return out


def entries_of(found: dict[str, dict], data: dict) -> dict[str, dict]:
    """The hand-written entries plus a generated APPROVED entry for every ticked P0 step that has none (the owner's tick is the only record it has)."""
    entries = dict(data["steps"])
    for sid, f in found.items():
        if sid not in entries and f["tier"] == "p0" and f["approved"]:
            entries[sid] = {"owner": "-", "before": "Ticked by the owner", "now": "APPROVED", "evidence": [f"docs/plans/{f['file']}"], "measured": "approved by the owner under the earlier rule; not re-audited here"}
    return entries


def problems(found: dict[str, dict], data: dict) -> list[str]:
    out = []
    entries = entries_of(found, data)
    out += [f"no entry for step {s}" for s in sorted(set(found) - set(entries))]
    out += [f"entry for {s}, which is not a step" for s in sorted(set(entries) - set(found))]
    for s, e in entries.items():
        if e["now"] not in STATUSES:
            out.append(f"{s}: status {e['now']!r} is not in the vocabulary")
        for p in e.get("evidence", []):
            if not (ROOT / p).exists():
                out.append(f"{s}: evidence {p} does not exist")
        if e["now"] in ("PARTLY", "BLOCKED", "DONE_GATE_OFF", "ENG_DONE") and not e.get("open"):
            out.append(f"{s}: {e['now']} must say what is open")
        if e["now"] == "ENG_DONE" and not e.get("external"):
            out.append(f"{s}: ENG_DONE must say who or what the external acceptance needs (`external`)")
        if s in found and found[s]["approved"] and e["now"] not in ("APPROVED", "NOT_RUHAN") and not implementation_complete(e):
            out.append(f"{s}: the step file is ticked but the reconciliation says {e['now']} without `implementation: complete`; a tick means the engineering is complete")
        if e.get("implementation") == "complete" and e["now"] not in IMPL_OK:
            out.append(f"{s}: `implementation: complete` is only for DONE or ENG_DONE, not {e['now']}")
        if e["now"] == "ENG_DONE" and e.get("implementation") == "complete" and not e.get("external"):
            out.append(f"{s}: an implemented step awaiting external acceptance must say what it needs")
        if e["now"] != "NOT_RUHAN" and not e.get("evidence"):
            out.append(f"{s}: no evidence listed")
    return out


def implementation_complete(e: dict) -> bool:
    """The engineering is complete and tested (the tick of a step file). DONE is complete in full; ENG_DONE counts only when the entry says `implementation: complete`: its remaining part is a device, a person, an account or a licence."""
    return e["now"] == "DONE" or (e["now"] == "ENG_DONE" and e.get("implementation") == "complete")


def render(found: dict[str, dict], data: dict) -> str:
    entries = entries_of(found, data)
    approved = [s for s in sorted(found) if entries[s]["now"] == "APPROVED"]
    ours = [s for s in sorted(found) if entries[s]["now"] not in ("NOT_RUHAN", "APPROVED")]
    count = {k: sum(1 for s in ours if entries[s]["now"] == k) for k in LABEL if k not in ("NOT_RUHAN", "APPROVED")}
    n_full = sum(1 for s in ours if entries[s]["now"] == "DONE")
    n_wait = sum(1 for s in ours if entries[s]["now"] == "ENG_DONE" and entries[s].get("implementation") == "complete")
    n_open = len(ours) - n_full - n_wait
    per_tier = ", ".join(f"{t.upper()} {sum(1 for f in found.values() if f['tier'] == t)} ({sum(1 for f in found.values() if f['tier'] == t and f['approved'])} ticked)" for t in ("p0", "p1", "p2"))
    lines = ["# Reconciliation of all P0, P1 and P2 steps", "",
             f"_Generated by `tools/plans/reconcile.py` from the step files and `docs/plans/reconciliation.json`; do not edit by hand. Written {data['written']}._", "",
             "A tick on a step file is a status marker: until 18 October 2026 it recorded the owner's approval and could be set without the work behind it; since the owner's instruction of that day the engineer sets it with `tools/plans/tick.py` only for a step this page calls Done, with evidence that exists. This page states, step by step, what exists and how it was checked. Owners follow `docs/plans/working-model.md` and the Team Board (Ruhan: Core, physiology, events, checklist engine, main AI; Sannan: Unity, XR, 3D, UI, builds; Moiz: the service, database and dashboard).", "",
             "**Four states are kept apart and never conflated:** (1) engineering implementation complete (the tick on the step file); (2) automated tests passed (the CI); (3) physical device or frontend acceptance passed (`docs/testing/SANNAN_EXTERNAL_TESTING.md`, result files); (4) independent safety or research acceptance passed (the same page, EX items); and a fifth, whether the feature is enabled in the application (`content/ai/features.json`). A tick means (1) only. The reconciliation status below says what is left: *Done* has nothing left; *Engineering done, external acceptance pending* is ticked and waits for the named actor.", "",
             f"Of the {len(ours)} steps reconciled here, **{n_full} are complete in full**, **{n_wait} are implemented and ticked but wait for an outside actor** and **{n_open} have engineering still open, are gated off or are blocked**.", "",
             f"**{len(found)} steps in all** ({per_tier}). **{len(ours)} are Ruhan's or shared and reconciled below** ({', '.join(f'{LABEL[k]} {v}' for k, v in count.items() if v)}); {len(approved)} P0 steps were ticked by the owner under the earlier rule and are listed by phase after the table; the rest belong to others and are listed at the end.", "",
             "| Step | Approved (tick) | Before 18 Oct | Now | Evidence | Measured or tested | Still open and who it needs |", "|---|---|---|---|---|---|---|"]
    for s in ours:
        e = entries[s]
        ev = "<br>".join(f"`{p}`" for p in e["evidence"][:4])
        lines.append(f"| [{s}]({found[s]['file'].replace(' ', '%20')}) {found[s]['title'][:60]} | {'yes' if found[s]['approved'] else 'no'} | {e['before']} | **{LABEL[e['now']]}** | {ev} | {e['measured']} | {e.get('open', '')}{(' **Needs:** ' + e['external']) if e.get('external') else ''} |")
    by_phase: dict[str, list[str]] = {}
    for s in approved:
        by_phase.setdefault(re.sub(r"-s\d+$", "", s), []).append(s.rsplit("-", 1)[1])
    lines += ["", f"## P0 steps ticked by the owner under the earlier rule ({len(approved)})", "", "Each carries the owner's approval; none was re-audited against its acceptance criteria in this reconciliation. A future audit that finds a gap reopens the step.", ""]
    lines += [f"* {ph}: {', '.join(ids)}" for ph, ids in sorted(by_phase.items())]
    others = [s for s in sorted(found) if entries[s]["now"] == "NOT_RUHAN"]
    lines += ["", "## Steps owned by others (not reconciled here)", "", "| Step | Owner | Approved (tick) | Note |", "|---|---|---|---|"]
    for s in others:
        e = entries[s]
        lines.append(f"| {s} {found[s]['title'][:60]} | {e['owner']} | {'yes' if found[s]['approved'] else 'no'} | {e['before']} |")
    return "\n".join(lines) + "\n"


def main(argv: list[str]) -> int:
    found, data = steps(), json.loads(DATA.read_text(encoding="utf-8"))
    bad = problems(found, data)
    if bad:
        print("\n".join(bad))
        return 1
    text = render(found, data)
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("docs/plans/task-reconciliation.md is out of date: run tools/plans/reconcile.py")
            return 1
        print(f"reconciliation current ({len(found)} steps)")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT).as_posix()} ({len(found)} steps)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
