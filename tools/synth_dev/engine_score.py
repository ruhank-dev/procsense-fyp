"""Runs judged speech through the ENGINE's own evidence store and communication scorer (the headless CLI command `speech-score`, src/ProcSense.Core/Scoring/SpeechScoreCheck.cs): the authoritative numbers of the evaluation. Needs the CLI built (`dotnet build src/ProcSense.Cli -c Release`).
    out = score([{"id": "a", "steps": [2], "patient_question_pending": False, "readback_pending": False, "verified": [], "acts": [{"act": "ASK_TOPIC", "assertion": "performed", "actor": "trainee", "form": "question", "relevance": 3, "correctness": 3, "completeness": 3, "span": "x"}]}])
Each result: {"id", "acts": [{"act", "accepted", "reason", "behaviour", "grade"}], "communication": [{"behaviour", "act", "critical", "grade", "weight", "credit", "claims_physical", "physical_verified"}], "communication_total"}."""
from __future__ import annotations

import json
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", str(ROOT / "src" / "ProcSense.Cli"), "--"]


def score(items: list[dict]) -> list[dict]:
    if not items:
        return []
    with tempfile.TemporaryDirectory() as t:
        f, out = Path(t) / "items.json", Path(t) / "scores.json"
        f.write_text(json.dumps(items), encoding="utf-8")
        p = subprocess.run(CLI + ["speech-score", "--items", str(f), "--out", str(out), "--content", str(ROOT / "content")], capture_output=True, text=True, encoding="utf-8", cwd=str(ROOT))
        if p.returncode != 0:
            raise RuntimeError("speech-score failed: " + (p.stderr or p.stdout)[:400])
        return json.loads(out.read_text(encoding="utf-8"))


def items_of(run_items: list[dict], evaluations: dict) -> list[dict]:
    """engine input from the frozen scenario state of each item and the model's judged acts (run_items: dicts with id, context, spec)."""
    out = []
    for it in run_items:
        ev = evaluations.get(it["id"])
        acts = [] if not ev else [{"act": a["objective"], "assertion": a["assertion"], "actor": a["actor"], "form": a["form"], "relevance": a["relevance"], "correctness": a["correctness"], "completeness": a["completeness"], "span": a["evidence"] or a["objective"],
                                   "claims_physical": bool(ev["claims_physical_action"])} for a in ev["acts"]]
        c = it["context"]
        out.append({"id": it["id"], "steps": c["steps"], "patient_question_pending": c["patient_question_pending"], "readback_pending": c["readback_pending"], "verified": list(c.get("verified_actions") or []), "acts": acts})
    return out
