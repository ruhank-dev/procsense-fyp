"""Persistent visual QA register (docs/qa/visual_qa_register.json, rendered to docs/qa/visual_qa_register.md) and the two-round approval gate.

  python unity/Tools/QA/qa_register.py ingest <review.json>                 add the review's issues (new ids VQ-<SCENE>-<n>), apply its verdicts on previously registered issues, log the round
  python unity/Tools/QA/qa_register.py set <ID> <STATUS> [--note T] [--evidence PATH ...]      OPEN | FIXING | READY_FOR_REVIEW | VERIFIED | BLOCKED_WITH_EVIDENCE
  python unity/Tools/QA/qa_register.py list [--scene S] [--open]
  python unity/Tools/QA/qa_register.py gate [--scene S]                    pass only when the last two rounds are both VISUALLY_APPROVED, with no open critical or major issue
  python unity/Tools/QA/qa_register.py render

Rules: an issue is never deleted; VERIFIED needs evidence (a ChatGPT FIXED verdict with its image text, or screenshot paths); ChatGPT's FIXED verdict moves an issue to VERIFIED only for the scene round that saw it.
Times are UTC.
"""
import argparse
import datetime
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
REG = ROOT / "docs" / "qa" / "visual_qa_register.json"
MD = ROOT / "docs" / "qa" / "visual_qa_register.md"
STATUSES = ("OPEN", "FIXING", "READY_FOR_REVIEW", "VERIFIED", "BLOCKED_WITH_EVIDENCE")
SEVERITIES = ("CRITICAL", "MAJOR", "MINOR", "ENHANCEMENT")


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%MZ")


def load():
    if REG.exists():
        return json.loads(REG.read_text(encoding="utf-8"))
    return {"issues": [], "rounds": []}


def save(reg):
    REG.parent.mkdir(parents=True, exist_ok=True)
    REG.write_text(json.dumps(reg, indent=2), encoding="utf-8"); render(reg)


def next_id(reg, scene):
    code = "".join(ch for ch in scene.upper() if ch.isalnum())[:6]
    n = 1 + max([int(i["id"].rsplit("-", 1)[1]) for i in reg["issues"] if i["id"].startswith(f"VQ-{code}-")] or [0])
    return f"VQ-{code}-{n:03d}"


def ingest(path):
    r = json.loads(Path(path).read_text(encoding="utf-8")); reg = load(); scene = r["scene"]; rnd = r["round"]
    verdicts = {s["id"]: s for s in r.get("previous_issue_status", [])}
    for i in reg["issues"]:
        v = verdicts.get(i["id"])
        if not v or i["scene"] != scene:
            continue
        i.setdefault("history", []).append({"round": rnd, "when": now(), "verdict": v["status"], "evidence": v.get("evidence", "")})
        if v["status"] == "FIXED" and i["status"] in ("READY_FOR_REVIEW", "FIXING", "OPEN"):
            i["status"] = "VERIFIED"; i["verification"] = {"round": rnd, "by": "ChatGPT", "evidence": v.get("evidence", ""), "when": now()}
        elif v["status"] in ("STILL_PRESENT", "PARTIAL") and i["status"] in ("READY_FOR_REVIEW", "FIXING", "VERIFIED"):
            i["status"] = "OPEN"
    added = []
    for x in r.get("issues", []):
        sev = x.get("severity", "MINOR"); sev = sev if sev in SEVERITIES else "MINOR"
        rid = next_id(reg, scene)
        reg["issues"].append({"id": rid, "scene": scene, "object": x.get("object_or_region", ""), "severity": sev, "category": x.get("category", ""), "description": x.get("description", ""),
                              "why": x.get("why_it_looks_wrong", ""), "correct_result": x.get("correct_result", ""), "root_cause": x.get("root_cause_hypothesis", ""),
                              "proposed_fix": x.get("recommended_fix", ""), "expected": x.get("expected_visual_improvement", ""), "verification_needed": x.get("verification_evidence", ""),
                              "image": x.get("image", ""), "chatgpt_round": rnd, "status": "OPEN", "evidence": [], "notes": "", "opened": now(), "history": []})
        added.append(rid)
    reg["rounds"].append({"scene": scene, "round": rnd, "when": now(), "decision": r["approval_decision"], "issues_added": added,
                          "critical": sum(1 for x in r.get("issues", []) if x.get("severity") == "CRITICAL"), "major": sum(1 for x in r.get("issues", []) if x.get("severity") == "MAJOR"), "source": str(path)})
    save(reg)
    print("INGESTED", scene, "round", rnd, r["approval_decision"], "new issues", len(added))


def set_status(iid, status, note="", evidence=()):
    reg = load()
    if status not in STATUSES:
        sys.exit("status must be one of " + ", ".join(STATUSES))
    for i in reg["issues"]:
        if i["id"] == iid:
            if status == "VERIFIED" and not (evidence or i.get("verification")):
                sys.exit("VERIFIED needs evidence (--evidence screenshot paths) or a ChatGPT FIXED verdict")
            if status == "BLOCKED_WITH_EVIDENCE" and not (evidence or note):
                sys.exit("BLOCKED_WITH_EVIDENCE needs --note and/or --evidence")
            i["status"] = status; i["evidence"] = list(dict.fromkeys(i.get("evidence", []) + list(evidence)))
            if note: i["notes"] = (i["notes"] + " | " if i["notes"] else "") + f"{now()}: {note}"
            save(reg); print(iid, "->", status); return
    sys.exit("unknown issue " + iid)


def gate(scene=None):
    reg = load(); scenes = [scene] if scene else sorted({r["scene"] for r in reg["rounds"]} | {i["scene"] for i in reg["issues"]})
    ok_all = True
    for s in scenes:
        rounds = [r for r in reg["rounds"] if r["scene"] == s][-2:]
        blocking = [i for i in reg["issues"] if i["scene"] == s and i["severity"] in ("CRITICAL", "MAJOR") and i["status"] != "VERIFIED"]
        two = len(rounds) == 2 and all(r["decision"] == "VISUALLY_APPROVED" and r["critical"] == 0 and r["major"] == 0 for r in rounds)
        ok = two and not blocking; ok_all &= ok
        print(f"GATE {s}: {'PASS' if ok else 'NOT PASSED'} (last two rounds approved: {two}; open critical/major: {len(blocking)})")
    return ok_all


def render(reg=None):
    reg = reg or load(); L = ["# Visual QA register", "", "_Generated by `unity/Tools/QA/qa_register.py`; edit through its commands. UTC times. Reviews come from the project's authorised ChatGPT integration (`unity/Tools/QA/chatgpt_review.py`)._", "",
                              "## Rounds", "", "| Scene | Round | Decision | Critical | Major | When (UTC) |", "|---|---|---|---|---|---|"]
    for r in reg["rounds"]:
        L.append(f"| {r['scene']} | {r['round']} | {r['decision']} | {r['critical']} | {r['major']} | {r['when']} |")
    L += ["", "## Issues", "", "| ID | Scene | Object | Severity | Status | Description | Proposed fix | Evidence |", "|---|---|---|---|---|---|---|---|"]
    for i in sorted(reg["issues"], key=lambda x: (x["scene"], SEVERITIES.index(x["severity"]), x["id"])):
        ev = ", ".join(i.get("evidence", [])[:2]) or (i.get("verification", {}).get("evidence", "")[:80])
        L.append("| {id} | {scene} | {obj} | {sev} | {st} | {d} | {f} | {e} |".format(id=i["id"], scene=i["scene"], obj=i["object"].replace("|", "/")[:60], sev=i["severity"], st=i["status"],
                                                                                         d=i["description"].replace("|", "/")[:160], f=i["proposed_fix"].replace("|", "/")[:160], e=ev.replace("|", "/")))
    MD.write_text("\n".join(L) + "\n", encoding="utf-8")


def main():
    ap = argparse.ArgumentParser(); sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("ingest"); p.add_argument("review")
    p = sub.add_parser("set"); p.add_argument("id"); p.add_argument("status"); p.add_argument("--note", default=""); p.add_argument("--evidence", nargs="*", default=[])
    p = sub.add_parser("list"); p.add_argument("--scene"); p.add_argument("--open", action="store_true")
    p = sub.add_parser("gate"); p.add_argument("--scene")
    sub.add_parser("render")
    a = ap.parse_args()
    if a.cmd == "ingest": ingest(a.review)
    elif a.cmd == "set": set_status(a.id, a.status, a.note, a.evidence)
    elif a.cmd == "gate": sys.exit(0 if gate(a.scene) else 1)
    elif a.cmd == "render": render(); print("rendered", MD)
    else:
        for i in load()["issues"]:
            if (not a.scene or i["scene"] == a.scene) and (not a.open or i["status"] != "VERIFIED"):
                print(f"{i['id']} [{i['severity']}] {i['status']:20s} {i['scene']}: {i['object'][:50]} - {i['description'][:90]}")


if __name__ == "__main__":
    main()
