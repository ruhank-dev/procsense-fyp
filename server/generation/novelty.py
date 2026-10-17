"""Novelty: is this case a combination the trainee has not met? (p1-live-scenario-generation-s09, stage LG-13)

    levels = levels_of(case, media)                         # {"structural": [...], "clinical": [...], "surface": [...]}
    report = check(levels, history, library, deliberate)    # NoveltyReport: per-level distances, verdict
    skeleton = pick_novel(build, history, library)          # up to 5 attempts, else the most novel one, logged LOW_NOVELTY

Three sets per case, taken from the finished case file (spec section 6): structural (scenario, spine, primary problem, the optional history blocks), clinical (abnormal results as analyte and flag,
imaging finding codes, rhythm, NEWS2 band, problem types) and surface (demographic and social blocks, age band, sex, name). D = sum of weight x (1 - Jaccard) over the levels, weights 0.5, 0.35, 0.15.
Accept when D >= 0.35 against each of the last 10 sessions and the structural set differs from each of the last 3 (a deliberate-practice request may repeat the structure when the clinical
distance is 0.3 or more). The numbers are deterministic; nothing here is a model. The thresholds are in THRESHOLDS and are tunable (OQ-04): too much novelty hurts learning as much as too little.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Callable

WEIGHTS = {"structural": 0.5, "clinical": 0.35, "surface": 0.15}
THRESHOLDS = {"min_distance": 0.35, "history": 10, "structural_history": 3, "deliberate_clinical": 0.3, "attempts": 5}
HISTORY_BLOCK = ("PSH", "ALG")                    # the blocks that belong to the story's history; the rest of the variable blocks are surface
NEWS2_BANDS = ((0, "low"), (4, "low_medium"), (5, "medium"), (7, "high"))


def _band(news2: int) -> str:
    name = "low"
    for floor, label in NEWS2_BANDS:
        if news2 >= floor:
            name = label
    return name


def _analyte(test: str) -> str:
    return re.sub(r"\s*\(.*\)\s*$", "", test).strip().lower()


def _age_band(age: int) -> str:
    return f"{(age // 10) * 10}s"


def levels_of(case: dict, media: dict | None = None) -> dict[str, list[str]]:
    """The three fingerprint sets of a finished case, as sorted lists (so they serialise the same way every time)."""
    comp = case.get("composition", {})
    blocks = comp.get("blocks", [])
    problems = sorted(case["problems"], key=lambda p: p.get("rank", 99))
    structural = {f"scenario:{case['scenario']}", f"spine:{comp.get('spine', case['id'])}", *(f"block:{b}" for b in blocks if b.split("-")[0] in HISTORY_BLOCK)}
    if problems:
        structural.add(f"primary:{problems[0]['id']}")
    clinical = {f"result:{_analyte(r['test'])}:{r['flag']}" for r in case["results"] if r["flag"] != "normal" and not r.get("variant")}
    clinical |= {f"problem_type:{p['type']}" for p in problems}
    first = case["observations"][0]
    clinical |= {f"rhythm:{first['heart_rhythm']}", f"news2:{_band(first['news2'])}"}
    for s in (media or {}).get("slots", []):
        if s.get("finding_code"):
            clinical.add(f"finding:{s['finding_code']}")
    ident = case["identity"]
    surface = {f"block:{b}" for b in blocks if b.split("-")[0] not in HISTORY_BLOCK}
    surface |= {f"sex:{ident['sex']}", f"age:{_age_band(ident['age'])}", f"name:{ident['full_name']}"}
    return {"structural": sorted(structural), "clinical": sorted(clinical), "surface": sorted(surface)}


def jaccard_distance(a, b) -> float:
    a, b = set(a), set(b)
    if not a and not b:
        return 0.0
    return 1.0 - len(a & b) / len(a | b)


def distance(x: dict[str, list[str]], y: dict[str, list[str]]) -> dict[str, float]:
    """Per-level distances and the weighted total D."""
    per = {lv: round(jaccard_distance(x[lv], y[lv]), 4) for lv in WEIGHTS}
    per["total"] = round(sum(WEIGHTS[lv] * per[lv] for lv in WEIGHTS), 4)
    return per


@dataclass
class NoveltyReport:
    verdict: str                                   # "novel", "not_novel", "low_novelty" (served after the attempts ran out)
    nearest: float                                 # smallest D over the trainee's last sessions (1.0 with no history)
    nearest_library: float | None
    reasons: list[str] = field(default_factory=list)
    per_level: dict[str, float] = field(default_factory=dict)

    def as_dict(self) -> dict:
        return {"verdict": self.verdict, "nearest": self.nearest, "nearest_library": self.nearest_library, "reasons": self.reasons, "per_level": self.per_level}


def check(levels: dict, history: list[dict], library: list[dict] | None = None, deliberate: bool = False, thresholds: dict | None = None) -> NoveltyReport:
    """`history` is the trainee's earlier fingerprints' levels, oldest first; `library` is the levels of the authored cases."""
    t = {**THRESHOLDS, **(thresholds or {})}
    reasons: list[str] = []
    recent = history[-t["history"]:]
    nearest, nearest_lv = 1.0, {}
    for i, h in enumerate(reversed(recent)):                       # i = 0 is the latest session
        d = distance(levels, h)
        if d["total"] < nearest or not nearest_lv:
            nearest, nearest_lv = d["total"], d
        if d["total"] < t["min_distance"]:
            reasons.append(f"too close to the session {i + 1} back (D={d['total']})")
        if i < t["structural_history"] and d["structural"] == 0.0 and not (deliberate and d["clinical"] >= t["deliberate_clinical"]):
            reasons.append(f"the same structure as the session {i + 1} back")
    lib = None
    if library:
        lib = min(distance(levels, x)["total"] for x in library)
    return NoveltyReport("not_novel" if reasons else "novel", nearest if recent else 1.0, lib, reasons, nearest_lv if recent else {})


def pick_novel(build: Callable[[int, list[str]], tuple[dict, dict]], history: list[dict], library: list[dict] | None = None, deliberate: bool = False,
               thresholds: dict | None = None) -> tuple[dict, NoveltyReport, list[dict]]:
    """`build(attempt, avoid_spines)` returns (skeleton, levels). Tries up to `attempts` times, each time leaving out the spines already tried; if none is novel, serves the most novel and says so.

    Returns (skeleton, report, attempts_log). The log goes into the package so a low-novelty case is visible to the evaluation loop (FB-05).
    """
    t = {**THRESHOLDS, **(thresholds or {})}
    tried: list[str] = []
    log: list[dict] = []
    best = None
    for attempt in range(t["attempts"]):
        try:
            skeleton, levels = build(attempt, list(tried))
        except Exception as e:                                       # Unsatisfiable: no spine is left to try
            log.append({"attempt": attempt, "error": str(e)})
            break
        report = check(levels, history, library, deliberate, thresholds)
        spine = next(x for x in levels["structural"] if x.startswith("spine:")).removeprefix("spine:")
        tried.append(spine)
        log.append({"attempt": attempt, "spine": spine, "verdict": report.verdict, "nearest": report.nearest})
        if report.verdict == "novel":
            return skeleton, report, log
        if best is None or report.nearest > best[1].nearest:
            best = (skeleton, report)
    if best is None:
        raise RuntimeError("no candidate could be built")
    best[1].verdict = "low_novelty"
    best[1].reasons.append("LOW_NOVELTY: no novel candidate in the attempts; the most novel one is served")
    return best[0], best[1], log
