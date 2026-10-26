"""Quality evaluation: is a valid case also a good one to learn from? (p1-live-scenario-generation-s08, stage LG-11)

    report = evaluate(case, objectives, tables, discovery)       # {"metrics": {...}, "failures": [...], "verdict": "pass" | "fail", "judge": None}

Deterministic metrics decide. Each returns a score from 0 to 1 and, below its threshold, a defect with a code the repair table (repair.py) knows. The metrics:

    teachable      every problem names what to do (expected actions) and why it matters
    discoverable   every problem of a spine that has discovery rules has a rule, so it can be found by examining or testing
    difficulty     the case's tier rating is near the target in the objectives (one tier = 100 points)
    coverage       ADVISORY: the dimensions the objectives name that the case exercises (the ranking prefers stories that exercise more; a library of 51 stories cannot exercise every dimension in every family, and a good case that misses one is better than a fallback that misses it too). The unmet dimensions are reported to the evaluation loop, which asks for stories that exercise them (dimensions only the headset delivers are listed, not counted)
    fairness       every decision has a correct option, at least one wrong one, a consequence for each wrong one, and no duplicate options
    trap_fairness  ADVISORY: a wrong option is a trap only if the case holds the fact that shows why it is wrong. A word-overlap test flags 20 of the 53 good authored cases, so it is reported and
                   never fails a case
    adversarial_anchor  every adversarial decision (id adv.<block>, DT-197) is a block of content/adversarial/blocks.json and the case holds that block's anchor fact, so the trap is always fair; this one FAILS a case
    reading        patient-facing sentences stay short enough to read under time pressure
    persona        pronouns in the patient's own text agree with the recorded sex; the spoken name is the identity's name
    coherence      no unfilled placeholder, no empty text, no repeated answer text across different questions

An optional language-model judge may add advisory scores (`judge`); they are logged and never change the verdict until they agree with human ratings at Spearman 0.6 or more (not yet measured, DT-196).
"""
from __future__ import annotations

import re

THRESHOLDS = {"teachable": 1.0, "discoverable": 1.0, "difficulty": 0.5,  "fairness": 1.0, "reading": 0.8, "persona": 1.0, "coherence": 1.0, "adversarial_anchor": 1.0}
MAX_SENTENCE_WORDS = 26
HARD_SENTENCE_WORDS = 45                  # one sentence this long fails the case outright
TIER_POINTS = 100.0

# dimensions a case file cannot show: the headset delivers them (interruptions, speech difficulty, handover) so the case cannot be judged on them
HEADSET_DIMENSIONS = {"communication_challenges", "environmental_interruptions", "complex_handover"}


def _patient_text(case: dict) -> list[str]:
    out = [case["presentation"].get("summary", "")] if isinstance(case.get("presentation"), dict) else []
    out += [h["answer"] for h in case["history"] if h.get("answer")]
    out += [case.get("reason_answer", ""), case.get("allergy_answer", ""), case.get("spoken_identity_answer", "")]
    return [t for t in out if t]


def _sentences(text: str) -> list[str]:
    return [s for s in re.split(r"(?<=[.!?])\s+", text.strip()) if s]


def teachable(case: dict) -> tuple[float, list[dict]]:
    bad = [p["id"] for p in case["problems"] if not p.get("expected_actions") or not p.get("why", "").strip()]
    return (1.0 - len(bad) / max(len(case["problems"]), 1)), ([{"code": "Q_TEACHABLE", "detail": f"problems with no actions or no reason: {bad}"}] if bad else [])


def discoverable(case: dict, discovery: list[dict]) -> tuple[float | None, list[dict]]:
    if not discovery:
        return None, []                                                  # a spine whose discovery is not modelled yet is not measured, and the report says so
    ruled = {r["problem"] for r in discovery}
    ids = [p["id"] for p in case["problems"]]
    missing = [i for i in ids if i not in ruled]
    return (1.0 - len(missing) / max(len(ids), 1)), ([{"code": "Q_DISCOVERABLE", "detail": f"problems nothing in the case would reveal: {missing}"}] if missing else [])


def difficulty(case: dict, objectives: dict, tables) -> tuple[float, list[dict]]:
    from .skeleton import target_difficulty
    gap = abs(tables.case_rating(case["tier"]) - target_difficulty(objectives))
    score = max(0.0, 1.0 - gap / (2 * TIER_POINTS))
    return round(score, 3), ([{"code": "Q_DIFFICULTY", "detail": f"the {case['tier']} case sits {gap:.0f} points from the target"}] if score < THRESHOLDS["difficulty"] else [])


def _covered(case: dict, tag: str) -> bool:
    comp = case.get("composition", {})
    if tag == "history_complex":
        return len([h for h in case["history"] if h.get("variant") is None]) >= 6 or bool(case["allergies"]) or any(b.split("-")[0] in ("PSH", "ALG") for b in comp.get("blocks", []))
    if tag == "multiple_problems":
        return len(case["problems"]) >= 3
    if tag == "hidden_facts":
        return any(r.get("quality") or r.get("variant") for r in case["results"]) or any(h.get("variant") for h in case["history"])
    if tag == "unexpected_events":
        return any(o.get("variant") for o in case["observations"]) or bool(case.get("variant_end_states"))
    if tag == "emergency_pace":
        return any(p["type"] == "life_threat" for p in case["problems"])
    return True


def coverage(case: dict, objectives: dict, tables) -> tuple[float | None, list[dict], list[str], list[str]]:
    wanted = [d for d in tables.dimensions if d.id in objectives["dimensions"]]
    judged = [d for d in wanted if d.id not in HEADSET_DIMENSIONS]
    delivery = [d.id for d in wanted if d.id in HEADSET_DIMENSIONS]
    if not judged:
        return None, [], delivery, []
    hit = [d for d in judged if _covered(case, d.tag)]
    unmet = [d.id for d in judged if d not in hit]
    return len(hit) / len(judged), ([{"code": "Q_COVERAGE", "detail": f"the case does not exercise {unmet}"}] if unmet else []), delivery, unmet


def fairness(case: dict) -> tuple[float, list[dict]]:
    defects = []
    for d in case["decisions"]:
        opts = d["options"]
        texts = [o["text"].strip().lower() for o in opts]
        if not any(o["correct"] for o in opts):
            defects.append(f"{d['id']}: no correct option")
        if all(o["correct"] for o in opts):
            defects.append(f"{d['id']}: every option is correct")
        if len(set(texts)) != len(texts):
            defects.append(f"{d['id']}: duplicate options")
        for o in opts:
            if not o["correct"] and not o.get("consequence", "").strip():
                defects.append(f"{o['id']}: a wrong option with no consequence")
            if re.search(r"\b(all|none) of the above\b", o["text"], re.IGNORECASE):
                defects.append(f"{o['id']}: 'all/none of the above'")
    n = max(len(case["decisions"]), 1)
    return round(1.0 - len({x.split(':')[0] for x in defects}) / n, 3), ([{"code": "Q_FAIRNESS", "detail": "; ".join(defects)}] if defects else [])


def adversarial_anchor(case: dict) -> tuple[float, list[dict]]:
    """An adversarial decision is a trap only if the case holds the fact that shows why its wrong option is wrong (the anchor of its block). Checked against the library, so a hand-edited or stale package cannot carry an unfair trap."""
    from . import adversarial
    by_id = {f"adv.{b['id'].lower()}": b for b in adversarial.blocks()["blocks"]}
    mine = [d["id"] for d in case["decisions"] if d["id"].startswith("adv.")]
    bad = [i for i in mine if i not in by_id or not adversarial.holds(by_id[i], case)]
    return round(1.0 - len(bad) / max(len(mine), 1), 3), ([{"code": "Q_ADVERSARIAL_ANCHOR", "detail": f"adversarial decisions whose block is unknown or whose anchor the case does not hold: {bad}"}] if bad else [])


def trap_fairness(case: dict) -> tuple[float, list[dict]]:
    """A wrong option the case teaches with a consequence must point at something the trainee could have found: the consequence names an allergy, a result, an examination or an observation the case holds."""
    facts = " ".join([a["allergen"] if isinstance(a, dict) and "allergen" in a else str(a) for a in case["allergies"]] + [r["test"] for r in case["results"]] + [e["finding"] for e in case["examination"]] + [p["label"] for p in case["problems"]] + [p.get("why", "") for p in case["problems"]]).lower()
    words = set(re.findall(r"[a-z]{5,}", facts))
    unfair = []
    for d in case["decisions"]:
        for o in d["options"]:
            if o["correct"]:
                continue
            cons = set(re.findall(r"[a-z]{5,}", (o.get("consequence", "") + " " + o["text"]).lower()))
            if cons and not (cons & words) and len(cons) > 3:
                unfair.append(o["id"])
    n = max(sum(1 for d in case["decisions"] for o in d["options"] if not o["correct"]), 1)
    return round(1.0 - len(unfair) / n, 3), ([{"code": "Q_TRAP", "detail": f"wrong options whose consequence names nothing the case holds: {unfair}"}] if unfair else [])


def reading(case: dict) -> tuple[float, list[dict]]:
    sents = [s for t in _patient_text(case) for s in _sentences(t)]
    if not sents:
        return 1.0, []
    long_ = [s for s in sents if len(s.split()) > MAX_SENTENCE_WORDS]
    score = round(1.0 - len(long_) / len(sents), 3)
    longest = max(len(s.split()) for s in sents)
    if longest > HARD_SENTENCE_WORDS:
        score = min(score, 0.0)
    return score, ([{"code": "Q_READING", "detail": f"{len(long_)} of {len(sents)} patient-facing sentences are over {MAX_SENTENCE_WORDS} words (longest {longest})"}] if score < THRESHOLDS["reading"] else [])


def persona(case: dict) -> tuple[float, list[dict]]:
    sex = case["identity"]["sex"]
    wrong = r"\b(she|her|hers|herself)\b" if sex == "M" else r"\b(he|him|his|himself)\b"
    own = [t for t in _patient_text(case) if not re.search(r"\b(husband|wife|son|daughter|brother|sister|mother|father|partner)\b", t, re.IGNORECASE)]
    hits = [t[:60] for t in own if re.search(wrong, t, re.IGNORECASE)]
    defects = []
    if hits:
        defects.append({"code": "Q_PERSONA", "detail": f"text uses {'she/her' if sex == 'M' else 'he/his'} for a {sex} patient: {hits[:2]}"})
    given = case["identity"]["full_name"].split()[-2:]
    spoken = case.get("spoken_identity_answer", "")
    if spoken and not any(g.lower() in spoken.lower() for g in given):
        defects.append({"code": "Q_PERSONA", "detail": "the spoken name is not the identity's name"})
    return (0.0 if defects else 1.0), defects


def coherence(case: dict) -> tuple[float, list[dict]]:
    texts = _patient_text(case) + [p["why"] for p in case["problems"]]
    bad = [t[:50] for t in texts if re.search(r"\{[^}]*\}|\bTODO\b|\bXXX\b|lorem ipsum|<[a-z_]+>", t, re.IGNORECASE) or not t.strip()]
    answers = [h["answer"].strip().lower() for h in case["history"] if h.get("variant") is None and len(h.get("answer", "")) > 40]
    dup = len(answers) - len(set(answers))
    defects = []
    if bad:
        defects.append({"code": "Q_COHERENCE", "detail": f"unfilled or empty text: {bad[:3]}"})
    if dup:
        defects.append({"code": "Q_COHERENCE", "detail": f"{dup} long answer(s) repeated under different questions"})
    return (0.0 if defects else 1.0), defects


def evaluate(case: dict, objectives: dict, tables, discovery: list[dict] | None = None, judge: dict | None = None) -> dict:
    metrics: dict[str, float | None] = {}
    failures: list[dict] = []
    advisory: list[dict] = []
    delivery: list[str] = []
    for name, fn in (("teachable", lambda: teachable(case)), ("discoverable", lambda: discoverable(case, discovery or [])), ("difficulty", lambda: difficulty(case, objectives, tables)),
                     ("fairness", lambda: fairness(case)), ("trap_fairness", lambda: trap_fairness(case)), ("reading", lambda: reading(case)),
                     ("persona", lambda: persona(case)), ("coherence", lambda: coherence(case)), ("adversarial_anchor", lambda: adversarial_anchor(case))):
        score, defects = fn()
        metrics[name] = score
        if name == "trap_fairness":
            advisory += defects
        else:
            failures += defects
    score, defects, delivery, unmet = coverage(case, objectives, tables)
    metrics["coverage"] = score
    advisory += defects
    for name, score in metrics.items():
        code = "Q_" + name.upper()
        if name in THRESHOLDS and score is not None and score < THRESHOLDS[name] and not any(f["code"] == code for f in failures):
            failures.append({"code": code, "detail": f"{name} {score} is under {THRESHOLDS[name]}"})
    return {"metrics": metrics, "not_measured": sorted(k for k, v in metrics.items() if v is None), "headset_dimensions": delivery, "failures": failures, "advisory": advisory, "unmet_dimensions": unmet,
            "verdict": "fail" if failures else "pass", "judge": judge}
