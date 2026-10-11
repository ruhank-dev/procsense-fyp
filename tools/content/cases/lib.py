"""Small helpers for writing the 17 P0 case files (tools/content/cases/s1.py, s2.py, stk.py) compactly and consistently.

A case is a plain dict; `build()` in tools/content/build_case_files.py fills in what can be computed (NEWS2, dates of birth, wristband text, source rows, approvals) and validates it.
"""
from __future__ import annotations

import re
from datetime import date

REF = date(2026, 10, 10)    # the game's 'today' (ISS-0018): every age below is computed from the date of birth against this date
TOPICS = ["complaint", "red_flags", "past_history", "medicines", "herbal", "previous_anaesthetic", "family_anaesthetic", "smoking", "alcohol", "last_food", "last_drink", "pregnancy"]
ASK = {  # words and topics that open each history topic (matched by the dialogue layer, not here)
    "complaint": ["ask:complaint", "ask:pain", "ask:why here"], "red_flags": ["ask:red flags", "ask:yellow", "ask:fever", "ask:urine colour"],
    "past_history": ["ask:past history", "ask:illnesses", "ask:operations"], "medicines": ["ask:medicines", "ask:tablets", "ask:drugs"], "herbal": ["ask:herbal", "ask:supplements"],
    "previous_anaesthetic": ["ask:previous anaesthetic", "ask:previous operations"], "family_anaesthetic": ["ask:family anaesthetic"], "smoking": ["ask:smoking"], "alcohol": ["ask:alcohol"],
    "last_food": ["ask:last food", "ask:when did you eat", "ask:fasting"], "last_drink": ["ask:last drink", "ask:fasting"], "pregnancy": ["ask:pregnancy", "ask:last period"],
}


def dob_for(age: int, month: int, day: int) -> str:
    year = REF.year - age - (1 if (month, day) > (REF.month, REF.day) else 0)
    return f"{year:04d}-{month:02d}-{day:02d}"


def person(title, name, sex, age, occupation, month, day, mrn, dob=None):
    return {"title": title, "full_name": name, "sex": sex, "age": age, "occupation": occupation, "dob": dob or dob_for(age, month, day), "mrn": mrn}


def H(topic, answer, volunteered=False, origin="authored", id=None, ask=None, variant=None):
    """A history fact. The patient volunteers only the main complaint (walkthrough Part 16 stage 2); everything else is disclosed when the trainee asks."""
    h = {"id": id or topic, "topic": topic, "answer": answer, "volunteered": volunteered, "disclose_on": [] if volunteered else (ask or ASK.get(topic, [f"ask:{topic}"])), "origin": origin}
    if variant:
        h["variant"] = variant
    return h


def history(complaint, red_flags=None, past="None.", medicines="None.", herbal="None.", prev_an="No previous anaesthetics.", fam_an="No anaesthetic problems in the family.", smoking="Never.", alcohol="None.",
            last_food=None, last_drink=None, pregnancy="Not applicable.", extra=(), origin=None):
    """The twelve standard topics; `origin` maps a topic to 'book' where the walkthrough states it, everything else is authored ('None.' style answers)."""
    origin = origin or {}
    rows = [H("complaint", complaint, True, origin.get("complaint", "book"))]
    for topic, text in (("red_flags", red_flags), ("past_history", past), ("medicines", medicines), ("herbal", herbal), ("previous_anaesthetic", prev_an), ("family_anaesthetic", fam_an), ("smoking", smoking),
                        ("alcohol", alcohol), ("last_food", last_food), ("last_drink", last_drink), ("pregnancy", pregnancy)):
        if text is not None:
            rows.append(H(topic, text, False, origin.get(topic, "authored")))
    rows.extend(extra)
    return rows


def O(time, hr, sbp, dbp, rr, spo2, temp, acvpu="A", oxygen="room_air", pain=0, rhythm="regular", note="", cond="initial", origin="book", variant=None):
    o = {"time": time, "condition": cond, "heart_rate": hr, "heart_rhythm": rhythm, "systolic_bp": sbp, "diastolic_bp": dbp, "respiratory_rate": rr, "spo2": spo2, "oxygen": oxygen,
            "temperature_c": temp, "acvpu": acvpu, "pain_score": pain, "news2": -1, "note": note, "origin": origin}
    if variant:
        o["variant"] = variant
    return o


_NEGATED_PAIN = re.compile(r"\b(?:no|non|not|without)[- ](?:[a-z]+ ){0,2}(?:tender(?:ness)?|guarding|rigidity|rebound|pain)", re.IGNORECASE)
_PAIN_WORDS = re.compile(r"tender|guard|rigid|rebound|pain", re.IGNORECASE)


def E(id, system, finding, abnormal=False, origin="authored", ask=None, painful=None, flags=None):
    """One examination finding. 'painful' (pressing there hurts, so a careful trainee examines it last) and 'flags' (facts the finding sets when revealed, for rules) are worked out
    from the wording unless given: negated wording ('no tenderness') is not painful, 'rigid' sets finding.rigid_abdomen and a positive Murphy's sign sets finding.murphy_positive."""
    if painful is None:
        painful = bool(_PAIN_WORDS.search(_NEGATED_PAIN.sub("", finding)))
    if flags is None:
        flags = []
        if re.search(r"\brigid\b", finding, re.IGNORECASE):
            flags.append("finding.rigid_abdomen")
        if re.search(r"murphy'?s sign positive", finding, re.IGNORECASE):
            flags.append("finding.murphy_positive")
    e = {"id": id, "system": system, "finding": finding, "abnormal": abnormal, "disclose_on": ask or [f"examine:{system}"], "origin": origin}
    if painful:
        e["painful"] = True
    if flags:
        e["flags"] = list(flags)
    return e


def R(id, test, value, unit, normal, flag="normal", comment="", prev=None, mins=45, quality=None, origin="authored", row=None, variant=None):
    r = {"id": id, "test": test, "value": str(value), "unit": unit, "normal_range": normal, "flag": flag, "comment": comment, "quality": quality,
         "previous": None if prev is None else {"value": str(prev[0]), "when": prev[1]}, "minutes_to_result": mins, "origin": origin}
    if row:
        r["source_row"] = row
    if variant:
        r["variant"] = variant
    return r


def S(name, value, note="", origin="book"):
    return {"name": name, "value": str(value), "note": note, "origin": origin}


def P(id, type_, label, rank, why, actions, variant=None):
    p = {"id": id, "type": type_, "label": label, "rank": rank, "why": why, "expected_actions": list(actions)}
    if variant:
        p["variant"] = variant     # the problem exists only in this variant of the case
    return p


def D(id, prompt, options, rule=None, variant=None):
    d = {"id": id, "prompt": prompt, "rule": rule, "options": [{"id": f"{id}.{i + 1}", "text": t, "correct": c, "consequence": q} for i, (t, c, q) in enumerate(options)]}
    if variant:
        d["variant"] = variant
    return d


def bad(mistake, shows, risk, recovery):
    return {"mistake": mistake, "shows": shows, "risk": risk, "recovery": recovery}


def debrief(recognised, missed, priority, why, related, independent, trigger, if_ignored, remember):
    return {"recognised": list(recognised), "missed": list(missed), "priority": priority, "why_priority": why, "related": related, "independent": independent,
            "trigger_escalation": trigger, "if_ignored": if_ignored, "remember": remember}


def case(id, label, scenario, identity, presentation, history, observations, examination, results, scores, problems, decisions, end_state, paths, debrief_, verification,
         spoken=None, allergies=(), allergy_answer="None.", reason="", wristband="auto", base_card=None, variant_end=None, tier="P0"):
    s = identity
    return {
        "schema": 1, "id": id, "walkthrough_label": label, "scenario": scenario, "tier": tier, "synthetic": True, "status": "DRAFT", "reference_date": REF.isoformat(), "base_card": base_card,
        "identity": s, "spoken_identity_answer": spoken or f"{s['full_name']}, {_spoken_date(s['dob'])}.", "wristband": wristband, "allergies": [dict(substance=a, reaction=r) for a, r in allergies],
        "allergy_answer": allergy_answer, "reason_answer": reason, "presentation": presentation, "history": history, "observations": observations, "examination": examination, "results": results,
        "scores": scores, "problems": problems, "decisions": decisions, "expected_end_state": end_state, "paths": paths, "debrief": debrief_, "verification": verification,
        "sources": {"story": "", "authored": ""},
        **({"variant_end_states": variant_end} if variant_end else {}),
    }


MONTHS = ["", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"]
ORD = {1: "first", 2: "second", 3: "third", 4: "fourth", 5: "fifth", 6: "sixth", 7: "seventh", 8: "eighth", 9: "ninth", 10: "tenth", 11: "eleventh", 12: "twelfth", 13: "thirteenth", 14: "fourteenth",
       15: "fifteenth", 16: "sixteenth", 17: "seventeenth", 18: "eighteenth", 19: "nineteenth", 20: "twentieth", 21: "twenty-first", 22: "twenty-second", 23: "twenty-third", 24: "twenty-fourth",
       25: "twenty-fifth", 26: "twenty-sixth", 27: "twenty-seventh", 28: "twenty-eighth", 29: "twenty-ninth", 30: "thirtieth", 31: "thirty-first"}


def _spoken_date(iso: str) -> str:
    y, m, d = (int(x) for x in iso.split("-"))
    return f"{ORD[d]} of {MONTHS[m]} {y}"


def mk(id, label, sc, who, summary, complaint, hx, vitals, exam, results, scores, problems, decisions, end, correct, wrong, missed, recovery, remember, ver, setting=None, time="08:00", surgery="10:00",
       allergies=(), allergy_answer=None, reason=None, variant_end=None, trigger=None, if_ignored=None, tier="P1", recognised=None, independent="None"):
    """A compact case: `hx` maps a topic to an answer (or (answer, 'book')); the rest of the twelve topics get plain 'none' answers; the debrief is built from the problems and the lesson."""
    setting = setting or ("pre-assessment" if sc == "S1" else "emergency department")
    origin, answers = {"complaint": "book"}, {}
    for k, v in hx.items():
        if isinstance(v, tuple):
            answers[k], origin[k] = v[0], v[1]
        else:
            answers[k] = v
    last_food = answers.pop("last_food", "Dinner at 20:00 yesterday." if sc == "S1" else "Lunch at 12:00 yesterday.")
    last_drink = answers.pop("last_drink", "Water at 06:30.")
    history_ = history(complaint, red_flags=answers.pop("red_flags", "No fever, no yellow eyes."), past=answers.pop("past_history", "None."), medicines=answers.pop("medicines", "None."),
                       herbal=answers.pop("herbal", "None."), prev_an=answers.pop("previous_anaesthetic", "No previous anaesthetics."), fam_an=answers.pop("family_anaesthetic", "No anaesthetic problems in the family."),
                       smoking=answers.pop("smoking", "Never."), alcohol=answers.pop("alcohol", "None."), last_food=last_food, last_drink=last_drink, pregnancy=answers.pop("pregnancy", "Not applicable."), origin=origin)
    for h in history_:
        if h["topic"] != "complaint":
            h["origin"] = origin.get(h["topic"], "authored")
    pres = {"setting": setting, "time_now": time, "summary": summary}
    if sc == "S1":
        pres["surgery_time"] = surgery
    top = [p for p in problems if p["rank"] <= 2]
    c = case(id, label, sc, who, pres, history_, vitals, exam, results, scores, problems, decisions, end,
             {"correct": correct, "incorrect": [bad(*w) for w in wrong], "missed": missed, "recovery": recovery},
             debrief(recognised or [p["label"] for p in top], [m for m in missed], problems[0]["label"], problems[0]["why"], "The findings in this case point to the same cause" if len(problems) > 1 else "A single issue",
                     independent, trigger or "Any value outside its safe band or any breach of a safety rule", if_ignored or "Harm from the missed problem or an unsafe operation", remember),
             ver, allergies=allergies, allergy_answer=allergy_answer or ("None." if not allergies else "; ".join(f"{a}: {r}" for a, r in allergies)), reason=reason or ("To have my gallbladder taken out." if sc == "S1" else "I have tummy pain."),
             variant_end=variant_end, tier=tier)
    return c
