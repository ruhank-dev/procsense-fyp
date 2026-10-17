"""Progression and investigation use across sessions (p2-ai-learning-analytics-s04): the trainer's overview, reproduced from the session evidence history.

    p = progression(history)          # per behaviour: sessions, first-half and second-half share right, trend
    u = investigations(history)       # tests asked for appropriately, not indicated, and not ordered when they were needed, per session and in total

History is a list of SessionEvidence records (server/personalisation/evidence.py), oldest first. Everything is a count or a share of the records; nothing is estimated, and the same history always gives the
same answer. A behaviour with fewer than MIN_ITEMS items in a half is shown without a trend. The investigation counts come from the scored items `credit.test_appropriate` (a test the case needed, ordered),
`*.test_not_indicated` (a test ordered that the case did not need) and `missed.test_not_ordered` (a needed test not ordered).
"""
from __future__ import annotations

MIN_ITEMS = 4
TREND = 0.1


def progression(history: list[dict]) -> list[dict]:
    out = []
    behaviours = sorted({c["behaviour"] for h in history for c in h["competencies"]})
    half = len(history) // 2
    for b in behaviours:
        def share(part):
            good = sum(c["credits"] for h in part for c in h["competencies"] if c["behaviour"] == b)
            tot = sum(c["credits"] + c["penalties"] for h in part for c in h["competencies"] if c["behaviour"] == b)
            return (good / tot if tot else None), tot
        sessions = sum(any(c["behaviour"] == b for c in h["competencies"]) for h in history)
        (a, na), (z, nz) = share(history[:half]) if half else (None, 0), share(history[half:])
        trend = "not enough sessions"
        if a is not None and z is not None and na >= MIN_ITEMS and nz >= MIN_ITEMS:
            trend = "improving" if z - a >= TREND else "slipping" if a - z >= TREND else "steady"
        out.append({"behaviour": b, "sessions": sessions, "first_half": None if a is None else round(a, 3), "second_half": None if z is None else round(z, 3), "trend": trend})
    return out


def investigations(history: list[dict]) -> dict:
    per, total = [], {"appropriate": 0, "not_indicated": 0, "not_ordered": 0}
    for h in history:
        row = {"session": h["session_id"], "appropriate": 0, "not_indicated": 0, "not_ordered": 0}
        for s in h["steps"]:
            if s["item"].endswith("test_appropriate") and s["state"] == "completed":
                row["appropriate"] += 1
            if s["item"].endswith("test_not_ordered") and s["state"] == "missed":
                row["not_ordered"] += 1
        row["not_indicated"] = sum(e["item"].endswith("test_not_indicated") for e in h["errors"])
        per.append(row)
        for k in total:
            total[k] += row[k]
    asked = total["appropriate"] + total["not_indicated"]
    return {"sessions": per, "total": total, "unnecessary_share": round(total["not_indicated"] / asked, 3) if asked else None}
