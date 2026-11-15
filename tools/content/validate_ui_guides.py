"""Validator for content/ui/training_guide.json (P3-TRN-01, P3-TRN-02 content half) and content/ui/voice_feedback.json (P3-VOX-07).

    uv run --project tools tools/content/validate_ui_guides.py

Training guide: every engine goal, every flow stage of S1 and S2 and every ward checklist item has exactly one entry; sections, orders, sources and
both input modes are present; no dose or measurement appears in the text; every goal whose `applies_if` is conditional has a not-needed reason.
Voice feedback: every outcome code of the error taxonomy (p3-15) has a phrase with a subtitle, placeholders are the known ones, practice and assessment
texts exist, and a pre-rendered clip, when given, is a path that exists. Exits 1 with the list of problems.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "content" / "ui"
CODES = {"NOT_UNDERSTOOD", "AMBIGUOUS", "WRONG_ADDRESSEE", "UNAVAILABLE_NOW", "ENGINE_REJECTED", "ASR_FAILED", "TIMEOUT", "NEEDS_CONFIRMATION", "SERVICE_OFFLINE", "SCENE_LOAD_FAILED", "CONTENT_INTEGRITY"}
PLACEHOLDERS = {"{reason_text}", "{action_text}"}
MEASURE = re.compile(r"\d+(\.\d+)?\s*(mg|mcg|ml|mmol|mmhg|kg|%|c\b|bpm|units?)|\b(mg|mcg|mmol)\b", re.I)


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def training_guide_problems() -> list[str]:
    p: list[str] = []
    g = load(UI / "training_guide.json")
    wf, fm, wd = load(ROOT / "content/workflow/workflow.json"), load(ROOT / "content/flow/flow_maps.json"), load(ROOT / "content/ward/ward_checklist.json")
    sections = {s["id"] for s in g["sections"]}
    if g.get("input_modes") != ["menu", "voice"]:
        p.append("training_guide: input_modes must be ['menu', 'voice']")
    if [s["order"] for s in g["sections"]] != list(range(1, len(g["sections"]) + 1)):
        p.append("training_guide: section orders must be 1..n")
    ids = [e["id"] for e in g["entries"]]
    for i in sorted({x for x in ids if ids.count(x) > 1}):
        p.append(f"training_guide: entry id used twice: {i}")
    need = {x["id"]: "goal" for x in wf["goals"]}
    need |= {s["id"]: "stage" for m in fm["maps"] for s in m["stages"]}
    need |= {"ward." + i["id"]: "ward_item" for i in wd["items"]}
    for i, kind in sorted(need.items()):
        e = next((x for x in g["entries"] if x["id"] == i), None)
        if e is None:
            p.append(f"training_guide: no entry for {kind} {i}")
        elif e["kind"] != kind:
            p.append(f"training_guide: {i} is a {kind}, entry says {e['kind']}")
    for i in sorted(set(ids) - set(need)):
        p.append(f"training_guide: entry {i} matches no goal, stage or ward item")
    if [e["order"] for e in g["entries"]] != list(range(1, len(g["entries"]) + 1)):
        p.append("training_guide: entry orders must be 1..n in file order")
    for e in g["entries"]:
        where = "training_guide." + e["id"]
        if e["section"] not in sections:
            p.append(f"{where}: unknown section {e['section']}")
        if not re.fullmatch(r"SR-\d{4}", e.get("source", "")):
            p.append(f"{where}: source must be a register id like SR-0038")
        for key in ("label", "explanation"):
            if not e.get(key, "").strip():
                p.append(f"{where}: {key} is empty")
        h = e.get("how_to", {})
        if not str(h.get("menu", "")).strip() or not str(h.get("voice", {}).get("text", "")).strip() or not str(h.get("voice", {}).get("example", "")).strip():
            p.append(f"{where}: how_to needs menu text and voice text with an example sentence")
        if MEASURE.search(json.dumps({k: e[k] for k in ("how_to", "explanation")}, ensure_ascii=False)):
            p.append(f"{where}: contains a dose or measurement; the guide only explains")
    reasons = {r["id"]: r for r in g["inactive_reasons"]}
    for goal in wf["goals"]:
        if goal["applies_if"] is not True and goal["id"] not in reasons:
            p.append(f"training_guide: conditional goal {goal['id']} has no not-needed reason")
    for r in g["inactive_reasons"]:
        if r["id"] not in need:
            p.append(f"training_guide.inactive_reasons: {r['id']} matches no goal or ward item")
        if not re.fullmatch(r"[A-Z0-9_]+", r["reason_id"]) or not r["text"].strip() or r["section"] not in sections:
            p.append(f"training_guide.inactive_reasons.{r['id']}: needs an upper-case reason_id, text and a known section")
        if MEASURE.search(r["text"]):
            p.append(f"training_guide.inactive_reasons.{r['id']}: contains a measurement")
    return p


def voice_feedback_problems() -> list[str]:
    p: list[str] = []
    v = load(UI / "voice_feedback.json")
    codes = [o["code"] for o in v["outcomes"]]
    for c in sorted(CODES - set(codes)):
        p.append(f"voice_feedback: no phrase for outcome code {c}")
    for c in sorted(set(codes) - CODES):
        p.append(f"voice_feedback: {c} is not in the error taxonomy")
    for c in sorted({x for x in codes if codes.count(x) > 1}):
        p.append(f"voice_feedback: {c} appears twice")
    for o in v["outcomes"]:
        where = "voice_feedback." + o["code"]
        for key in ("subtitle", "spoken", "meaning"):
            if not o.get(key, "").strip():
                p.append(f"{where}: {key} is empty (a subtitle is always required)")
        for key in ("practice_extra", "assessment_extra"):
            if key not in o:
                p.append(f"{where}: {key} is missing (empty text is allowed)")
        for ph in re.findall(r"\{[a-z_]+\}", o.get("subtitle", "") + o.get("spoken", "")):
            if ph not in PLACEHOLDERS:
                p.append(f"{where}: unknown placeholder {ph}")
        if o.get("clip") is not None and not (ROOT / o["clip"]).exists():
            p.append(f"{where}: clip {o['clip']} does not exist")
        if MEASURE.search(json.dumps(o, ensure_ascii=False)):
            p.append(f"{where}: contains a dose or measurement")
    e = next((o for o in v["outcomes"] if o["code"] == "ENGINE_REJECTED"), None)
    if e and (e.get("text_source") != "HintTexts" or "{reason_text}" not in e["subtitle"] or not e.get("fallback")):
        p.append("voice_feedback.ENGINE_REJECTED: must use {reason_text} from HintTexts and have a fallback")
    return p


def main() -> int:
    probs = training_guide_problems() + voice_feedback_problems()
    print("ui guides: " + ("all checks pass" if not probs else f"{len(probs)} problem(s)"))
    for x in probs:
        print("  - " + x)
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main())
