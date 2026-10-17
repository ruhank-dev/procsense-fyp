"""Picture, ECG and report selection for a generated case (p1-live-scenario-generation-s06, stage LG-08).

    from generation.media import select_media, check_media
    media = select_media(card, spine_id, seed)

A composed case keeps its spine's findings, so each imaging slot (an ultrasound, an ECG, a chest film, a CT...) shows a picture of the same finding. For every slot of the spine (content/case_imaging.csv):
  * the pictures that carry the slot's finding code are the candidates (the tags are in content/imaging/picture_tags.csv, s04);
  * a picture with a licence that forbids this use (non-commercial, no derivatives, research only) or a technical grade C is dropped, and a status below APPROVED_FOR_TRAINING never appears;
  * real pictures come before labelled synthetic ones, grade A before B, and the seed breaks ties, so the same request shows the same picture;
  * for an ECG the strip nearest the patient's pulse is chosen, and if the starting pulse can move without leaving the range that scores 0 on NEWS2 and is not named in the story it moves to within 3 per
    minute of the strip's measured rate, so the pulse and the strip agree;
  * the written report is the picture's own approved report; its numbers are the picture's measurements, never typed again.
When no picture fits, the slot says so (`picture_id` null) and the caller either uses a written-only result (the test allows one) or changes the story (stage LG-12). No image is generated at run time.
"""
from __future__ import annotations

import csv
import json
import random
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
STATUS_OK = {"APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"}
LICENCE_DENY = re.compile(r"non-?commercial|\bNC\b|no-?derivatives|\bND\b|research only|research use only", re.IGNORECASE)
ECG_TOLERANCE = 5            # beats a minute: the strip's rate and the patient's pulse
ALIGN_TO = 3
PULSE_RANGE = (51, 90)       # scores 0 on NEWS2


def read_csv(path: Path) -> list[dict]:
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def pictures() -> list[dict]:
    return read_csv(CONTENT / "imaging" / "picture_tags.csv")


def reports() -> dict[str, dict]:
    doc = json.loads((CONTENT / "imaging" / "imaging.json").read_text(encoding="utf-8"))
    return {r["image_id"]: r for r in doc["reports"]}


def slots_of(spine_id: str) -> list[dict]:
    return [{"test": r["test"], "finding_code": r["d_finding_code"], "basis": r["d_basis"], "ref": r["ref"]} for r in read_csv(CONTENT / "case_imaging.csv") if r["case_id"] == spine_id]


def licence_ok(licence: str) -> bool:
    return bool(licence.strip()) and not LICENCE_DENY.search(licence)


def rate_of(report: dict) -> float | None:
    v = (report.get("measurements") or {}).get("rate_bpm")
    return float(v) if v not in (None, "") else None


def _pulse_free(card: dict) -> bool:
    o = card["observations"][0]
    if o["condition"] != "initial" or o["news2"] != 0 or o.get("variant"):
        return False
    text = json.dumps({k: v for k, v in card.items() if k not in ("observations", "composition")}) + " ".join(x.get("note", "") for x in card["observations"])
    return re.search(r"(?<![0-9A-Za-z.])" + str(o["heart_rate"]) + r"(?![0-9])", text) is None


def select_media(card: dict, spine_id: str, seed: int, pics: list[dict] | None = None, reps: dict[str, dict] | None = None, slots: list[dict] | None = None) -> dict:
    """The picture for each imaging slot, plus a log. May move the card's starting pulse (to agree with an ECG strip); that is in the log."""
    pics = pics if pics is not None else pictures()
    reps = reps if reps is not None else reports()
    out, log = [], []
    for slot in (slots if slots is not None else slots_of(spine_id)):
        code = slot["finding_code"]
        rng = random.Random(f"{seed}:media:{slot['test']}")
        cands = [p for p in pics if p["finding_code"] == code and p["status"] in STATUS_OK and licence_ok(p["licence"]) and p["quality_grade"] != "C"]
        entry = {"test": slot["test"], "finding_code": code, "basis": slot["basis"], "picture_id": None}
        if not cands:
            entry["note"] = "no picture of this finding with a usable licence and grade: use a written-only result or change the story"
            log.append({"slot": slot["test"], "action": "no_picture", "finding_code": code})
            out.append(entry)
            continue
        rng.shuffle(cands)
        cands.sort(key=lambda p: (p["real"] != "true", p["quality_grade"]))
        pool = [p for p in cands if (p["real"], p["quality_grade"]) == (cands[0]["real"], cands[0]["quality_grade"])]
        pick = pool[0]
        if slot["test"] == "ecg":
            hr = card["observations"][0]["heart_rate"]
            rated = [(abs((rate_of(reps[p["id"]]) or 1e9) - hr), p) for p in cands if p["id"] in reps and rate_of(reps[p["id"]]) is not None and p["real"] == cands[0]["real"]]
            if rated:
                rated.sort(key=lambda t: (t[0], t[1]["id"]))
                pick = rated[0][1]
                strip = rate_of(reps[pick["id"]])
                if abs(strip - hr) > ECG_TOLERANCE and _pulse_free(card) and PULSE_RANGE[0] <= strip + 0 <= PULSE_RANGE[1]:
                    new = int(round(strip + rng.uniform(-ALIGN_TO, ALIGN_TO)))
                    new = min(PULSE_RANGE[1], max(PULSE_RANGE[0], new))
                    log.append({"slot": "ecg", "action": "pulse_moved_to_the_strip", "from": hr, "to": new, "strip_rate": strip})
                    card["observations"][0]["heart_rate"] = new
                elif abs(strip - hr) > ECG_TOLERANCE:
                    log.append({"slot": "ecg", "action": "strip_and_pulse_differ", "pulse": hr, "strip_rate": strip, "why": "the pulse is named in the story or the case is not all normal, so it stays"})
        rep = reps.get(pick["id"], {})
        entry.update(picture_id=pick["id"], dataset=pick["dataset"], licence=pick["licence"], real=pick["real"] == "true", quality_grade=pick["quality_grade"], tags=pick["tags"].split(),
                     report_id=rep.get("id"), report=rep.get("text"), measurements=rep.get("measurements"))
        log.append({"slot": slot["test"], "action": "picked", "picture_id": pick["id"], "candidates": len(cands)})
        out.append(entry)
    return {"slots": out, "log": log, "imaging_rows": [{"case_id": card["id"], "test": s["test"], "finding_code": s["finding_code"]} for s in out]}


def check_media(card: dict, media: dict, pics: list[dict] | None = None, reps: dict[str, dict] | None = None) -> list[str]:
    """Defects: a picture whose finding is not the slot's, a licence that forbids the use, a status below training, a pulse and an ECG rate more than 5 apart."""
    pics = {p["id"]: p for p in (pics if pics is not None else pictures())}
    reps = reps if reps is not None else reports()
    out = []
    for s in media["slots"]:
        if s["picture_id"] is None:
            continue
        p = pics.get(s["picture_id"])
        if p is None:
            out.append(f"{s['test']}: picture {s['picture_id']} is not in the library")
            continue
        if p["finding_code"] != s["finding_code"]:
            out.append(f"{s['test']}: the picture shows {p['finding_code']} but the slot needs {s['finding_code']}")
        if not licence_ok(p["licence"]):
            out.append(f"{s['test']}: the licence of {p['id']} ('{p['licence']}') does not allow this use")
        if p["status"] not in STATUS_OK:
            out.append(f"{s['test']}: {p['id']} is {p['status']}")
        if s["test"] == "ecg":
            rate = rate_of(reps.get(p["id"], {}))
            hr = card["observations"][0]["heart_rate"]
            if rate is not None and abs(rate - hr) > ECG_TOLERANCE and card["observations"][0]["news2"] == 0 and PULSE_RANGE[0] <= rate <= PULSE_RANGE[1] and _pulse_free(card):       # a pulse the story names is the author's and is logged, not a defect
                out.append(f"ecg: the strip's rate {rate:g} and the pulse {hr} are more than {ECG_TOLERANCE} apart")
        if s.get("report") and rep_numbers_changed(s, reps.get(p["id"], {})):
            out.append(f"{s['test']}: the report no longer carries the picture's measurements")
    return out


def rep_numbers_changed(slot: dict, report: dict) -> bool:
    return bool(report) and slot.get("measurements") != report.get("measurements")
