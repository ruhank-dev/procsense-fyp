"""Validator for content/ui/interactions.json (P3-CTX-01).

    uv run --project tools tools/content/validate_interactions.py

Rules: entry ids are unique and match the tablet entries one to one (same tab, verb, params); every tablet entry belongs to at least one entity or the global
group; every id an entity or the global group names exists; every entry has an icon and a label; spoken entries are only verbs of content/ai/spoken_actions.json and
have examples, and physical or safety-critical verbs are never spoken. Exits 1 with the list of problems.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "content" / "ui"
NEVER_SPOKEN = {"confirm_identity", "report_mismatch", "read_wristband", "open_notes", "attach_sensor", "detach_sensor", "measure_nibp", "measure_temperature"}


def load(p: Path):
    return json.loads(p.read_text(encoding="utf-8"))


def problems(data: dict | None = None, tablet: dict | None = None, spoken: set[str] | None = None) -> list[str]:
    data = data if data is not None else load(UI / "interactions.json")
    tablet = tablet if tablet is not None else load(UI / "tablet.json")
    if spoken is None:
        spoken = {a["verb"] for a in load(ROOT / "content" / "ai" / "spoken_actions.json")["actions"]}
    p: list[str] = []
    ids = [e["id"] for e in data["entries"]]
    for i in sorted({x for x in ids if ids.count(x) > 1}):
        p.append(f"duplicate entry id {i}")
    known = set(ids)
    tablet_keys = {(t["id"], e["verb"], tuple(sorted(e["params"].items()))) for t in tablet["tabs"] for e in t["entries"]}
    have = {(e["tab"], e["verb"], tuple(sorted(e["params"].items()))) for e in data["entries"]}
    for k in sorted(tablet_keys - have):
        p.append(f"tablet entry {k[0]}.{k[1]} {dict(k[2])} is missing from the catalogue")
    for k in sorted(have - tablet_keys):
        p.append(f"catalogue entry {k[0]}.{k[1]} {dict(k[2])} is not on the tablet")
    used: set[str] = set()
    for ent in data["entities"]:
        for g in ent["groups"]:
            for i in g["entries"]:
                used.add(i)
                if i not in known:
                    p.append(f"{ent['id']}/{g['id']} names unknown entry {i}")
    for i in data["global"]["entries"]:
        used.add(i)
        if i not in known:
            p.append(f"global group names unknown entry {i}")
    for i in sorted(known - used):
        p.append(f"entry {i} belongs to no entity and not to the global group")
    for e in data["entries"]:
        if not e.get("icon") or not e.get("label"):
            p.append(f"entry {e['id']} lacks an icon or a label")
        v = e["voice"]
        if v["spoken"] and e["verb"] not in spoken:
            p.append(f"entry {e['id']} is spoken but {e['verb']} is not a spoken action")
        if v["spoken"] and e["verb"] in NEVER_SPOKEN:
            p.append(f"entry {e['id']} is a physical or safety-critical action and must not be spoken")
        if v["spoken"] != bool(v["examples"]):
            p.append(f"entry {e['id']}: spoken flag and examples disagree")
    return p


def main() -> int:
    bad = problems()
    for b in bad:
        print("FAIL ", b)
    if not bad:
        print("ok    interaction catalogue valid")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
