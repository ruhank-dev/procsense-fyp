"""What the ENGINE accepts of the evidence the interpretation path offers (ADR-0022 follow-up): the offers are run through the engine's own EvidenceStore by the headless CLI command `evidence-check` (src/ProcSense.Cli), never through a Python copy of its rules.

    verdicts = accept([{"id": "x", "act": "ASK_TOPIC", "confidence": 0.49, "steps": [2]}])      # [{"id", "accepted", "reason", "behaviour"}]

An offer may carry `group` (offers of one group share a store, in file order, as one session) and `utterance` (the same id twice is refused as a duplicate). `content` (optional) names another checkout's content directory. `per_act` (optional) judges the same offers with other per-act thresholds, a what-if that writes nothing.
Each offer goes to a FRESH store, so one offer never changes another's answer. Needs the CLI built (`dotnet build src/ProcSense.Cli -c Release`); the command is used by tools/bench/recall_diagnosis.py and recall_eval.py.
"""
from __future__ import annotations

import json
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", str(ROOT / "src" / "ProcSense.Cli"), "--"]


def accept(offers: list[dict], per_act: dict | None = None, content: Path | None = None) -> list[dict]:
    if not offers:
        return []
    with tempfile.TemporaryDirectory() as t:
        f, out = Path(t) / "offers.json", Path(t) / "verdicts.json"
        f.write_text(json.dumps(offers), encoding="utf-8")
        extra = []
        if per_act is not None:                                  # a what-if: the same offers judged with other per-act thresholds
            (Path(t) / "per_act.json").write_text(json.dumps(per_act), encoding="utf-8")
            extra = ["--per-act", str(Path(t) / "per_act.json")]
        p = subprocess.run(CLI + ["evidence-check", "--offers", str(f), "--out", str(out), "--content", str(content or ROOT / "content")] + extra, capture_output=True, text=True, encoding="utf-8", cwd=str(ROOT))
        if p.returncode != 0:
            raise RuntimeError("evidence-check failed: " + (p.stderr or p.stdout)[:400])
        return json.loads(out.read_text(encoding="utf-8"))
