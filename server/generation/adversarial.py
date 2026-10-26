"""Adversarial decision blocks in generated cases (DT-197, p1-live-scenario-generation): a decision whose WRONG option looks like the right thing to do, added to a composed case when - and only when - the case holds the fact that shows why it is wrong.

    added = apply(case, seed)            # appends up to LIMIT decisions to case["decisions"]; returns [{"block": "ADV-01", "decision": "adv.adv-01", "anchor": ...}]

The blocks are content/adversarial/blocks.json, checked against every case by tools/content/check_adversarial.py. A block is added only to a case whose own content holds its anchor (an allergy, a history answer, a result),
so the trap is always fair: the trainee could have found the reason by looking. Which of the fitting blocks is chosen, and in which position the right option stands, follow the seed, so the same request gives the same case.
The text of the blocks was written by an AI assistant and is NOT reviewed by a clinician; the package says so (`adversarial.status`). The decisions are teaching content of the case file (the debrief and the trap-fairness
metric read them); they add no action to the engine's decision graph.
"""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BLOCKS = ROOT / "content" / "adversarial" / "blocks.json"
LIMIT = 1                                          # one adversarial decision per case: the case keeps its own decisions and its length


def blocks() -> dict:
    return json.loads(BLOCKS.read_text(encoding="utf-8"))


def _text(case: dict, where: str) -> str:
    if where == "allergies":
        return " ".join((a.get("allergen", "") + " " + str(a.get("reaction", ""))) if isinstance(a, dict) else str(a) for a in case["allergies"]) + " " + case.get("allergy_answer", "")
    if where == "history":
        return " ".join(h["answer"] for h in case["history"]) + (" " + case["presentation"].get("summary", "") if isinstance(case.get("presentation"), dict) else "")
    if where == "problems":
        return " ".join(p["label"] + " " + p.get("why", "") for p in case["problems"])
    if where == "last_food":
        return " ".join(h["answer"] for h in case["history"] if h["topic"] == "last_food")
    if where == "results":
        return " ".join(r["test"] + " " + str(r.get("value", "")) for r in case["results"])
    raise ValueError(where)


def holds(block: dict, case: dict) -> bool:
    """True when the case's own content holds the block's anchor."""
    return re.search(block["anchor"]["match"], _text(case, block["anchor"]["in"]), re.I) is not None


def _rank(seed: int, block_id: str) -> str:
    return hashlib.sha256(f"{seed}|{block_id}".encode("utf-8")).hexdigest()


def apply(case: dict, seed: int, limit: int = LIMIT) -> list[dict]:
    """Adds up to `limit` adversarial decisions to the case (in place) and says which. A case with no fitting block is left as it is."""
    data = blocks()
    have = {d["id"] for d in case["decisions"]}
    fitting = [b for b in data["blocks"] if holds(b, case) and f"adv.{b['id'].lower()}" not in have]
    added = []
    for b in sorted(fitting, key=lambda b: _rank(seed, b["id"]))[:limit]:
        did = f"adv.{b['id'].lower()}"
        right = {"id": f"{did}.1", "text": b["right"], "correct": True, "consequence": "Matches the guidance: " + b["source"] + "."}
        wrong = {"id": f"{did}.2", "text": b["wrong"], "correct": False, "consequence": b["consequence"]}
        options = [right, wrong] if int(_rank(seed, did)[:2], 16) % 2 == 0 else [wrong, right]          # the right option is not always first
        for i, o in enumerate(options, 1):
            o["id"] = f"{did}.{i}"
        case["decisions"].append({"id": did, "prompt": b["prompt"], "rule": None, "options": options})
        added.append({"block": b["id"], "decision": did, "anchor": b["anchor"]["in"]})
    return added
