"""Cross-modal consistency: every modality tells the same story (p1-live-scenario-generation-s07, stage LG-09).

    from generation.crossmodal import check
    defects = check(case, media)          # [] or [Defect(rule, detail)]

A deterministic rule table (content/generation/crossmodal_rules.json lists the rules in words; this module is the code). Each rule reads the case file and the picture slots and names what is inconsistent.
The rules are the ones the walkthrough's teaching depends on: the ECG against the pulse and against potassium, the scan against the inflammatory markers, free air against the abdomen, a faulty
sample against its repeat, a high lactate against the patient's state, a pregnancy test against sex and age, and the order of times. All 53 authored cases pass every rule; each rule has a failing
example in the tests. A language model may later flag a suspected inconsistency as advice (LG-11) but never decides; nothing here is a model.
"""
from __future__ import annotations

import re
from dataclasses import dataclass


@dataclass(frozen=True)
class Defect:
    rule: str
    detail: str


def _num(text: str) -> float | None:
    m = re.fullmatch(r"\s*(-?\d+(?:\.\d+)?)\s*", text or "")
    return float(m.group(1)) if m else None


def _results(case: dict, base: str) -> list[dict]:
    return [r for r in case["results"] if r["test"].split(" (")[0].strip().lower() == base.lower() and not r.get("variant")]


def _codes(media: dict) -> set[str]:
    return {s["finding_code"] for s in media.get("slots", []) if s.get("finding_code")}


def _ecg(media: dict) -> dict | None:
    return next((s for s in media.get("slots", []) if s["test"] == "ecg" and s.get("picture_id")), None)


def cm01_ecg_rhythm_vs_pulse(case: dict, media: dict) -> list[Defect]:
    """The rhythm on the strip is the rhythm of the pulse: an irregular strip (atrial fibrillation) goes with an irregular pulse, a regular one with a regular pulse."""
    s = _ecg(media)
    if not s or not s.get("measurements"):
        return []
    regular = s["measurements"].get("regular")
    pulse = case["observations"][0]["heart_rhythm"]
    if regular == "no" and pulse == "regular":
        return [Defect("VR-CM-01", f"the ECG strip is irregular but the pulse is recorded as regular ({case['id']})")]
    if regular == "yes" and pulse == "irregular":
        return [Defect("VR-CM-01", f"the ECG strip is regular but the pulse is recorded as irregular ({case['id']})")]
    return []


def cm02_potassium_vs_ecg(case: dict, media: dict) -> list[Defect]:
    """A hyperkalaemia pattern needs a high potassium in the case; a low-potassium pattern a low one; a high potassium that is not a faulty sample does not sit with a normal strip."""
    out = []
    codes = _codes(media)
    ks = [(_num(r["value"]), r) for r in _results(case, "Potassium") if _num(r["value"]) is not None]
    real = [(v, r) for v, r in ks if not r.get("quality")]
    if "ECG-08" in codes and ks and not any(v >= 5.5 for v, _ in ks):
        out.append(Defect("VR-CM-02", "the ECG shows peaked T waves but no potassium result is high"))
    if "ECG-09" in codes and ks and not any(v <= 3.4 for v, _ in ks):
        out.append(Defect("VR-CM-02", "the ECG shows flat T waves with U waves but no potassium result is low"))
    ecg = _ecg(media)
    if ecg and ecg["finding_code"] == "ECG-01" and any(v >= 6.5 for v, _ in real):
        out.append(Defect("VR-CM-02", "a potassium of 6.5 or more that is not a faulty sample sits with a normal ECG"))
    return out


def cm03_scan_vs_markers(case: dict, media: dict) -> list[Defect]:
    """An ultrasound or scan that shows appendicitis goes with a raised white cell count or CRP when either is reported."""
    if not _codes(media) & {"US-AP-01", "US-AP-02", "CT-AP-01", "CT-AP-02", "MR-AP-01"}:
        return []
    markers = _results(case, "WBC") + _results(case, "CRP")
    if markers and all(r["flag"] == "normal" for r in markers):
        return [Defect("VR-CM-03", "the scan shows appendicitis but the white cell count and CRP are both normal")]
    return []


def cm04_free_air_vs_abdomen(case: dict, media: dict) -> list[Defect]:
    """Gas outside the bowel goes with a rigid or guarded abdomen on examination."""
    if not _codes(media) & {"CXR-08", "CT-AB-02"}:
        return []
    ab = [e["finding"] for e in case["examination"] if e["system"] == "abdomen" and e["abnormal"]]
    if not any(re.search(r"rigid|guard|board|peritonism|rebound", f, re.IGNORECASE) and not re.search(r"no peritonism|no rigidity|no guarding", f, re.IGNORECASE) for f in ab):
        return [Defect("VR-CM-04", "the picture shows free air but the abdomen is not described as rigid or guarded")]
    return []


def cm05_faulty_sample_vs_repeat(case: dict, media: dict) -> list[Defect]:
    """A result from a faulty sample is followed, in the case, by its repeat."""
    out = []
    for r in case["results"]:
        if r.get("quality") in ("haemolysed", "drip_arm", "wrong_tube_edta", "clotted", "lipaemic") and not r.get("variant"):
            base = r["test"].split(" (")[0].strip().lower()
            if not any(x["test"].split(" (")[0].strip().lower() == base and "(repeat" in x["test"].lower() for x in case["results"]):
                out.append(Defect("VR-CM-05", f"the {r['test']} result comes from a faulty sample ({r['quality']}) and the case has no repeat"))
    return out


def cm06_lactate_vs_state(case: dict, media: dict) -> list[Defect]:
    """A lactate of 4 or more does not sit with a patient whose starting observations are all normal."""
    high = [r for r in _results(case, "Lactate") if (_num(r["value"]) or 0) >= 4.0 and not r.get("quality")]
    if high and case["observations"][0]["news2"] <= 2:
        return [Defect("VR-CM-06", f"a lactate of {high[0]['value']} with a starting NEWS2 of {case['observations'][0]['news2']}")]
    return []


def cm07_pregnancy_vs_patient(case: dict, media: dict) -> list[Defect]:
    """A pregnancy test result belongs to a female patient between 12 and 55 years."""
    if not _codes(media) & {"PREG-00", "PREG-01"}:
        return []
    i = case["identity"]
    if i["sex"] != "F" or not 12 <= i["age"] <= 55:
        return [Defect("VR-CM-07", f"a pregnancy test for a {i['sex']} patient aged {i['age']}")]
    return []


def cm08_timeline_order(case: dict, media: dict) -> list[Defect]:
    """Observation times do not go backwards, the first is the starting one, and no result arrives before it is ordered."""
    out = []
    by_condition: dict[str, list[str]] = {}
    for o in case["observations"]:
        if not o.get("variant"):
            by_condition.setdefault(o["condition"], []).append(o["time"])        # a case may hold a second set of readings (a variant); each set runs forward on its own
    for cond, times in by_condition.items():
        if times != sorted(times):
            out.append(Defect("VR-CM-08", f"observation times are out of order ({cond}): {times}"))
    if any(r["minutes_to_result"] < 0 for r in case["results"]):
        out.append(Defect("VR-CM-08", "a result has a negative time to result"))
    return out


RULES = {
    "VR-CM-01": ("ECG rhythm against the pulse", cm01_ecg_rhythm_vs_pulse), "VR-CM-02": ("potassium against the ECG", cm02_potassium_vs_ecg), "VR-CM-03": ("scan against inflammatory markers", cm03_scan_vs_markers),
    "VR-CM-04": ("free air against the abdomen", cm04_free_air_vs_abdomen), "VR-CM-05": ("faulty sample against its repeat", cm05_faulty_sample_vs_repeat), "VR-CM-06": ("lactate against the patient's state", cm06_lactate_vs_state),
    "VR-CM-07": ("pregnancy test against sex and age", cm07_pregnancy_vs_patient), "VR-CM-08": ("order of times", cm08_timeline_order),
}


def check(case: dict, media: dict | None = None) -> list[Defect]:
    media = media or {"slots": []}
    out: list[Defect] = []
    for _, fn in RULES.values():
        out += fn(case, media)
    return out
