"""Applies recorded approvals to the pictures and reports the builders write (ADR-0003 status gate).

content/image_approvals.json holds, per picture id, the SHA-256 of the exact file approved and the SHA-256 of its report text, plus who approved and when. A builder calls
`apply(entries, reports)` as its last step: a picture or report whose bytes or text differ from what was approved goes back to DRAFT, so an edit can never inherit an old approval.
`python tools/data/approvals.py grant --by "..." --date YYYY-MM-DD --level APPROVED_FOR_TRAINING` records approval for every picture now in the library (what the owner asked for on
10 October 2026: the owner states that the clinician approves everything; the clinician is not named, so the record says so).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FILE = ROOT / "content" / "image_approvals.json"
SETS = [("content/images/index.json", "content/imaging_reports.json"), ("content/ecg/index.json", "content/ecg_reports.json"),
        ("content/cxr/index.json", "content/cxr_reports.json"), ("content/xsec/index.json", "content/xsec_reports.json")]


def text_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def load() -> dict:
    return json.loads(FILE.read_text(encoding="utf-8")) if FILE.exists() else {"schema": 1, "approvals": {}}


def apply(entries: list[dict], reports: list[dict]) -> None:
    ap = load()["approvals"]
    for e in entries:
        a = ap.get(e["id"])
        if a and a["sha256"] == e["sha256"]:
            e["status"], e["approval"] = a["level"], {"by": a["by"], "date": a["date"], "note": a["note"]}
    for r in reports:
        a = ap.get(r["image_id"])
        if a and a["text_sha256"] == text_hash(r["text"]) and a["sha256"] == next((e["sha256"] for e in entries if e["id"] == r["image_id"]), None):
            r["status"], r["approval"] = a["level"], {"by": a["by"], "date": a["date"], "note": a["note"]}


def grant(by: str, date: str, level: str, note: str) -> int:
    ap = {"schema": 1, "approvals": {}}
    for idx, rep in SETS:
        images = json.loads((ROOT / idx).read_text(encoding="utf-8"))["images"]
        reports = {r["image_id"]: r for r in json.loads((ROOT / rep).read_text(encoding="utf-8"))["reports"]}
        for e in images:
            ap["approvals"][e["id"]] = {"sha256": e["sha256"], "text_sha256": text_hash(reports[e["id"]]["text"]), "level": level, "by": by, "date": date, "note": note}
    FILE.write_text(json.dumps(ap, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"recorded approval for {len(ap['approvals'])} pictures and their reports")
    return 0


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("cmd", choices=["grant"])
    p.add_argument("--by", required=True)
    p.add_argument("--date", required=True)
    p.add_argument("--level", default="APPROVED_FOR_TRAINING")
    p.add_argument("--note", default="")
    a = p.parse_args()
    sys.exit(grant(a.by, a.date, a.level, a.note))
