"""Check 4: the 12 contradiction types (content/contradictions.json says what each one means).

Six come from the existing case-file rules (identity, NEWS2, result flags, impossible times, decisions, pregnancy of a male patient); the other rules are here. Two of the names (MEDICATION_WITHOUT_INDICATION and
RESULTS_VS_STATE) are the guide's own examples; the rest are the project's. They are checked on the case's base content; a variant fact is judged inside its variant, not against the base.
"""
from __future__ import annotations

import sys
from pathlib import Path

from . import Defect
from .base import ROOT, defects

sys.path.insert(0, str(ROOT / "tools" / "content"))

# a chronic-disease medicine and the words that give it a reason somewhere in the case (history, complaint or the medicines answer itself); a design table, provisional (SR-2868)
INDICATIONS = {
    "metformin": ["diabetes"], "gliclazide": ["diabetes"], "insulin": ["diabetes"], "semaglutide": ["diabetes", "obes"], "dapagliflozin": ["diabetes"], "sglt2": ["diabetes"],
    "amlodipine": ["blood pressure", "hypertension"], "ramipril": ["blood pressure", "hypertension", "kidney", "heart"], "bisoprolol": ["blood pressure", "fibrillation", "heart"],
    "bendroflumethiazide": ["blood pressure", "hypertension"], "spironolactone": ["heart failure", "kidney", "blood pressure"], "furosemide": ["heart failure", "kidney"],
    "apixaban": ["fibrillation", "heartbeat", "clot"], "warfarin": ["fibrillation", "heartbeat", "clot", "valve"], "rivaroxaban": ["fibrillation", "heartbeat", "clot"],
    "blood pressure tablet": ["blood pressure", "hypertension"],
}
PENICILLINS = ["penicillin", "amoxicillin", "co-amoxiclav", "flucloxacillin", "ampicillin", "piperacillin"]
MEAL_HOURS = {"clear_liquids": 2.0, "milk": 6.0, "light_meal": 6.0, "heavy_meal": 8.0}


def _base_history(card: dict) -> dict[str, str]:
    return {h["topic"]: h["answer"] for h in card["history"] if h.get("variant") is None}


def _text(card: dict) -> str:
    h = _base_history(card)
    return " ".join(h.get(t, "") for t in ("complaint", "red_flags", "past_history", "medicines")).lower()


def results_vs_state(card: dict) -> list[Defect]:
    """A sepsis problem with inflammatory results (white cells, CRP, lactate) that are all normal contradicts itself (the guide's own example: a normal white count with a sepsis progression)."""
    out = []
    inflammatory = [r for r in card["results"] if r.get("variant") is None and r["test"].split(" (")[0] in ("WBC", "CRP", "Lactate")]
    for p in card["problems"]:
        if "sepsis" in (p["id"] + " " + p["label"]).lower() and p.get("variant") is None and inflammatory and not any(r["flag"] in ("high", "critical_high") for r in inflammatory):
            out.append(Defect(4, "RESULTS_VS_STATE", f"{card['id']}: the problem {p['id']} is sepsis but every white cell count, CRP and lactate in the case is normal"))
    return out


def medication_without_indication(card: dict) -> list[Defect]:
    """A chronic-disease medicine named in the medicines answer needs its reason somewhere in the case (the guide's own example)."""
    meds = _base_history(card).get("medicines", "").lower()
    text = _text(card)
    out = []
    for drug, reasons in INDICATIONS.items():
        if drug in meds and not any(r in text for r in reasons):
            out.append(Defect(4, "MEDICATION_WITHOUT_INDICATION", f"{card['id']}: takes {drug} but nothing in the case gives a reason ({', '.join(reasons)})"))
    return out


def allergy_vs_medicine(card: dict) -> list[Defect]:
    """A patient allergic to penicillin does not take a penicillin-group medicine."""
    meds = _base_history(card).get("medicines", "").lower()
    out = []
    for a in card["allergies"]:
        if "penicillin" in a["substance"].lower() and a["reaction"].lower().find("rash as a child") < 0:
            hit = [m for m in PENICILLINS if m in meds]
            if hit:
                out.append(Defect(4, "ALLERGY_VS_MEDICINE", f"{card['id']}: allergic to {a['substance']} but takes {hit[0]}"))
    return out


def fasting_vs_state(card: dict) -> list[Defect]:
    """A planned (S1) case that is expected to go ahead needs a last meal that meets the fasting rule at the time of surgery (clear fluids 2 h, milk or a light meal 6 h, a heavy meal 8 h)."""
    out = []
    if card["scenario"] != "S1" or card["expected_end_state"] != "ready":
        return out
    p = card["presentation"]
    h_to_surgery = 0.0
    if p.get("surgery_time"):
        hh = lambda t: int(t[:2]) + int(t[3:5]) / 60
        h_to_surgery = hh(p["surgery_time"]) - hh(p["time_now"])
    for h in card["history"]:
        if h["topic"] in ("last_food", "last_drink") and h.get("variant") is None and h.get("intake"):
            i = h["intake"]
            need = MEAL_HOURS.get(i["kind"])
            at_surgery = None if i.get("hours_before_now") is None else i["hours_before_now"] + h_to_surgery
            if need is not None and at_surgery is not None and at_surgery < need:
                out.append(Defect(4, "FASTING_VS_STATE", f"{card['id']}: expected to go ahead but the last {i['kind']} will be only {at_surgery:.1f} hours before surgery (the rule needs {need})"))
    return out


def problem_without_evidence(card: dict) -> list[Defect]:
    """A P0 case's problem must be one the trainee can find: it has a discovery rule (or is one of the two framing problems of the example patient)."""
    if card["tier"] != "P0":
        return []
    import build_discovery as bd
    listed = {(c, p) for c, p, *_ in bd.RULES} | set(bd.NOT_DISCOVERABLE)
    return [Defect(4, "PROBLEM_WITHOUT_EVIDENCE", f"{card['id']}: the problem {p['id']} has no discovery rule, so the trainee cannot find it")
            for p in card["problems"] if p["type"] != "information_only" and (card["id"], p["id"]) not in listed]


CONTRADICTIONS = [
    ("IDENTITY_MISMATCH", None), ("NEWS2_MISMATCH", None), ("RESULT_FLAG_MISMATCH", None), ("TIME_IMPOSSIBLE", None), ("DECISION_OPTIONS_INVALID", None), ("PREGNANT_MALE", None),
    ("RESULTS_VS_STATE", results_vs_state), ("MEDICATION_WITHOUT_INDICATION", medication_without_indication), ("ALLERGY_VS_MEDICINE", allergy_vs_medicine),
    ("FASTING_VS_STATE", fasting_vs_state), ("PROBLEM_WITHOUT_EVIDENCE", problem_without_evidence), ("RESULT_UNORDERABLE", None),
]


def c04_contradictions(card: dict, ctx: dict) -> list[Defect]:
    if defects(card, ctx, 1):
        return []
    out = list(defects(card, ctx, 4))
    for code, fn in CONTRADICTIONS:
        if fn is not None:
            out += fn(card)
    return out
