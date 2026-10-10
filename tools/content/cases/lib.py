"""Small helpers for writing the 17 P0 case files (tools/content/cases/s1.py, s2.py, stk.py) compactly and consistently.

A case is a plain dict; `build()` in tools/content/build_case_files.py fills in what can be computed (NEWS2, dates of birth, wristband text, source rows, approvals) and validates it.
"""
from __future__ import annotations

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


def E(id, system, finding, abnormal=False, origin="authored", ask=None):
    return {"id": id, "system": system, "finding": finding, "abnormal": abnormal, "disclose_on": ask or [f"examine:{system}"], "origin": origin}


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


def P(id, type_, label, rank, why, actions):
    return {"id": id, "type": type_, "label": label, "rank": rank, "why": why, "expected_actions": list(actions)}


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
         spoken=None, allergies=(), allergy_answer="None.", reason="", wristband="auto", base_card=None, variant_end=None):
    s = identity
    return {
        "schema": 1, "id": id, "walkthrough_label": label, "scenario": scenario, "tier": "P0", "synthetic": True, "status": "DRAFT", "reference_date": REF.isoformat(), "base_card": base_card,
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
