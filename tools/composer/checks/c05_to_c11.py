"""Checks 5 to 11: combinations and status, difficulty, event eligibility, results against orderable tests, the idle run, the golden run and the status of an AI draft."""
from __future__ import annotations

import csv
import json
import subprocess
import tempfile
from pathlib import Path

from . import Defect
from .base import ROOT, defects

STATUSES = ["DRAFT", "CLINICAL_REVIEW", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"]


def c05_status(card: dict, ctx: dict) -> list[Defect]:
    """Combinations and status: an approved case carries its approval; the end states exist."""
    if defects(card, ctx, 1):
        return []
    out = []
    if card["status"].startswith("APPROVED") and not card.get("approval"):
        out.append(Defect(5, "STATUS_WITHOUT_APPROVAL", f"{card['id']}: status {card['status']} but no approval block"))
    return out


def c06_difficulty(card: dict, ctx: dict) -> list[Defect]:
    """Difficulty: the tier is one the difficulty settings know."""
    if defects(card, ctx, 1):
        return []
    tiers = json.loads((ROOT / "content" / "difficulty" / "dimensions.json").read_text(encoding="utf-8"))["tier_offsets"]
    return [] if card["tier"] in tiers else [Defect(6, "DIFFICULTY_UNKNOWN_TIER", f"{card['id']}: tier {card['tier']} is not one of {sorted(tiers)}")]


def bound_events(case_id: str) -> list[tuple[str, str]]:
    """(what-if, event) pairs bound to a case in content/whatif_bindings.csv (empty until the bindings exist)."""
    path = ROOT / "content" / "whatif_bindings.csv"
    if not path.exists():
        return []
    with open(path, encoding="utf-8", newline="") as f:
        return [(r["whatif"], r["event"]) for r in csv.DictReader(f) if r["case"] == case_id and r["event"]]


def event_reachable(event: str, card: dict) -> bool:
    """Can this case ever make the event fire? The reaction needs an allergy the case lists; a vital-sign event needs an observation of the case that crosses its threshold."""
    if event == "E-HYPERSENSITIVITY":
        return bool(card["allergies"])
    events = {e["id"]: e for e in json.loads((ROOT / "content" / "events" / "events.json").read_text(encoding="utf-8"))["events"]}
    e = events.get(event)
    if e is None:
        return False
    w = e["watch"]
    if not w["vital"]:
        return True
    key = {"hr": "heart_rate", "sbp": "systolic_bp", "dbp": "diastolic_bp", "rr": "respiratory_rate", "spo2": "spo2", "temp": "temperature_c"}[w["vital"]]
    ops = {"<": lambda a, b: a < b, "<=": lambda a, b: a <= b, ">": lambda a, b: a > b, ">=": lambda a, b: a >= b}
    return any(ops[w["op"]](o[key], w["value"]) for o in card["observations"])


def c07_events(card: dict, ctx: dict) -> list[Defect]:
    """Event eligibility: every event bound to the case can really fire in it (T-VB5)."""
    if defects(card, ctx, 1):
        return []
    return [Defect(7, "EVENT_INELIGIBLE", f"{card['id']}: {ev} (what-if {w}) is bound to the case but can never fire in it")
            for w, ev in bound_events(card["id"]) if not event_reachable(ev, card)]


def c08_results(card: dict, ctx: dict) -> list[Defect]:
    """Every result is of a test the trainee can order (RESULT_UNORDERABLE)."""
    if defects(card, ctx, 1):
        return []
    analytes = {a.lower() for t in ctx["investigations"] for a in t.get("analytes", [])}
    out = []
    for r in card["results"]:
        name = r["test"].split(" (")[0].strip().lower()
        if name not in analytes:
            out.append(Defect(8, "RESULT_UNORDERABLE", f"{card['id']}: the result {r['id']} is of '{r['test']}', which no orderable test gives"))
    return out


def _cli(args: list[str]) -> subprocess.CompletedProcess:
    return subprocess.run(["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"] + args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")


def _scenario_args(card: dict, tmp: Path) -> list[str]:
    """A composed case is not in content/: the engine reads it from a package file put in front of the real content (--scenario)."""
    if card.get("created_by") != "composer":
        return ["--case", card["id"]]
    package = tmp / "check.package.json"
    package.write_text(json.dumps({"version": 1, "status": "GENERATED_PRACTICE_ONLY", "case": card}, ensure_ascii=False), encoding="utf-8")
    return ["--scenario", str(package)]


def _golden_script(card: dict, kind: str) -> Path:
    """The authored case's own script, or for a composed case its spine's: a composed case keeps the story, so the same actions are the right ones."""
    spine = card.get("composition", {}).get("spine", card["id"]) if card.get("created_by") == "composer" else card["id"]
    return ROOT / "tests" / "golden" / f"{spine}_{kind}.actions.json"


def _adapt_script(script: Path, card: dict, ctx: dict, tmp: Path) -> Path:
    """The spine's correct path is right for the spine's patient. What the variable blocks change, the correct answers follow: the safety checklist asks whether the patient has a known allergy
    (sign_in.05), and the answer is the case's own. Any other change of the correct path by a variable block is a defect for the golden run to find, not something to adapt away."""
    if card.get("created_by") != "composer":
        return script
    spine = next((c for c in ctx.get("others", []) if c["id"] == card["composition"]["spine"]), None)
    if spine is None or bool(spine["allergies"]) == bool(card["allergies"]):
        return script
    actions = json.loads(script.read_text(encoding="utf-8"))
    for a in actions:
        if a.get("verb") == "checklist_item" and a.get("params", {}).get("item") == "sign_in.05":
            a["params"]["response"] = "yes" if card["allergies"] else "no"
    adapted = tmp / script.name
    adapted.write_text(json.dumps(actions), encoding="utf-8")
    return adapted


def c09_idle(card: dict, ctx: dict) -> list[Defect]:
    """A five-minute idle run of the case stays inside the engine's bounds (T-VB4). Runs the real CLI; only when asked (--headless). A composed case is read from a package file."""
    if not ctx.get("headless"):
        return []
    with tempfile.TemporaryDirectory() as tmp:
        actions = Path(tmp) / "idle.actions.json"
        actions.write_text(json.dumps([{"t": 1, "verb": "attach_sensor", "params": {"sensor": "spo2"}}, {"t": 300, "verb": "wait", "target": "patient", "params": {}}]), encoding="utf-8")   # the sensor makes the vital signs readable
        out = Path(tmp) / "out.json"
        p = _cli(["run", *_scenario_args(card, Path(tmp)), "--seed", "42", "--actions", str(actions), "--out", str(out), "--allow-draft"])
        if p.returncode != 0:
            return [Defect(9, "IDLE_RUN_FAILED", f"{card['id']}: {(p.stdout + p.stderr).strip()[-200:]}")]
        vit = {v["name"]: float(v["value"]) for v in json.loads(out.read_text(encoding="utf-8"))["observable"].get("vitals", []) if v["name"] != "oxygen" and v["name"] != "acvpu"}
    if not vit:
        return [Defect(9, "IDLE_NO_VITALS", f"{card['id']}: the pulse oximeter shows no reading after the idle run")]
    bad = [f"{k}={v}" for k, v in vit.items() if (k == "heart_rate" and not 30 <= v <= 200) or (k == "spo2" and not 60 <= v <= 100) or (k == "respiratory_rate" and not 4 <= v <= 45)]
    return [Defect(9, "IDLE_OUT_OF_BOUNDS", f"{card['id']}: " + ", ".join(bad))] if bad else []


def c10_golden(card: dict, ctx: dict) -> list[Defect]:
    """The golden correct run reaches the end state and result the authored case's golden file declares (T-CASE1). For a composed case the spine's script and expectations apply. Only when asked (--headless)."""
    script = _golden_script(card, "correct")
    expected_file = script.with_name(script.name.replace(".actions.json", ".expected.json"))
    if not ctx.get("headless") or not script.exists() or not expected_file.exists():
        return []
    expected = json.loads(expected_file.read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory() as tmp:
        out, report = Path(tmp) / "out.json", Path(tmp) / "report.json"
        scenario = _scenario_args(card, Path(tmp))
        script = _adapt_script(script, card, ctx, Path(tmp))
        p = _cli(["run", *scenario, "--seed", "42", "--actions", str(script), "--out", str(out), "--allow-draft"])
        if p.returncode != 0:
            return [Defect(10, "GOLDEN_RUN_FAILED", f"{card['id']}: {(p.stdout + p.stderr).strip()[-200:]}")]
        p = _cli(["report", *scenario, "--log", str(out.with_suffix(".session.jsonl")), "--out", str(Path(tmp) / "r.html"), "--json", str(report)])
        if p.returncode != 0:
            return [Defect(10, "GOLDEN_REPORT_FAILED", f"{card['id']}: {(p.stdout + p.stderr).strip()[-200:]}")]
        got = json.loads(report.read_text(encoding="utf-8"))
    want = (expected["facts"]["end_state"], expected["score"]["result"])
    if (got["end_state"], got["result"]) != want:
        return [Defect(10, "GOLDEN_END_STATE", f"{card['id']}: the correct path ends {got['end_state']} / {got['result']}, the golden file says {want[0]} / {want[1]}")]
    return []


def c11_ai_status(card: dict, ctx: dict) -> list[Defect]:
    """A case drafted by AI never goes above CLINICAL_REVIEW by itself (T-VB3): a human reviewer approves it, the validator never does."""
    if defects(card, ctx, 1):
        return []
    if card.get("created_by") == "ai_draft" and card["status"] in ("APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"):
        return [Defect(11, "AI_CARD_AUTO_APPROVED", f"{card['id']}: drafted by AI but its status is {card['status']}; it stops at CLINICAL_REVIEW until a person approves it")]
    return []
