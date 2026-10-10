"""Works out, for a case file, what the patient last ate and drank and how long before 'now' (p0-clinical-logic-s15).

The case files state the answer in words ("Toast and tea with milk at 07:00 this morning."). The fasting rule needs three facts from those words: the kind (clear liquids, milk, a light meal, a heavy meal,
nothing, or unknown), and how many hours before the case's 'now' it was. This module reads them once, when the case files are built, and the result is stored in the file next to the words
(history item "intake": {"kind": ..., "hours_before_now": ...}) so the engine never has to read English.

Rules (the classes are the ones in content/fasting_rules.csv, walkthrough Part 9):
  * a clock time later than the case's 'now' means the day before (a patient is asked in the morning about last night's dinner); "yesterday", "last night" and "this morning" say it outright
  * "N hours ago", "two days ago" and "noon" are read as they say
  * toast, biscuit, a light meal: light meal; dinner, lunch, rice, chicken, biryani, kebab, a heavy meal: heavy meal. Breakfast and anything else is NOT guessed: kind "unknown" (the rule then asks for the longest fast)
  * drinks: with milk: milk; water, tea or coffee without milk, fizzy drinks, juice: clear liquids; "nothing" for food or drink: none
"""
from __future__ import annotations

import re

LIGHT = re.compile(r"\b(toast|biscuit|light meal)\b", re.IGNORECASE)
HEAVY = re.compile(r"\b(dinner|lunch|supper|rice|chicken|biryani|kebab|heavy meal|paratha|nihari)\b", re.IGNORECASE)
CLEAR = re.compile(r"\b(water|tea|coffee|fizzy|juice|squash|drink|sips?)\b", re.IGNORECASE)
NOTHING = re.compile(r"^\s*(nothing|none)\b", re.IGNORECASE)
CLOCK = re.compile(r"\b([01]?\d|2[0-3]):([0-5]\d)\b")
AGO = re.compile(r"\b(?:about |around )?(\d+(?:\.\d+)?) hours? ago\b", re.IGNORECASE)
DAYS_AGO = re.compile(r"\b(?:about )?(one|two|three|\d+) days? ago\b", re.IGNORECASE)
NUMBER_WORDS = {"one": 1, "two": 2, "three": 3}


def minutes(text: str) -> int:
    h, m = text.split(":")
    return int(h) * 60 + int(m)


def first_clause(text: str) -> str:
    """The part of the sentence about the food or drink itself ('Dinner at 20:00 yesterday; water at 06:30.' is about dinner)."""
    return re.split(r"[;]", text)[0]


def kind_of(topic: str, text: str) -> str:
    clause = first_clause(text)
    if NOTHING.search(clause) and "apart from" not in clause.lower():
        return "none"
    if topic == "last_drink":
        if re.search(r"\bmilk\b", clause, re.IGNORECASE):
            return "milk"
        return "clear_liquids" if CLEAR.search(clause) else "unknown"
    if LIGHT.search(clause):
        return "light_meal"
    if HEAVY.search(clause):
        return "heavy_meal"
    return "unknown"


def hours_before_now(text: str, time_now: str) -> float | None:
    clause = first_clause(text)
    now = minutes(time_now)
    m = AGO.search(clause)
    if m:
        return round(float(m.group(1)), 4)
    m = DAYS_AGO.search(clause)
    if m:
        n = m.group(1).lower()
        return float((NUMBER_WORDS.get(n) or int(n)) * 24)
    clock = CLOCK.search(clause)
    if clock:
        t = minutes(f"{int(clock.group(1))}:{clock.group(2)}")
    elif re.search(r"\bnoon\b", clause, re.IGNORECASE):
        t = 12 * 60
    else:
        return None
    yesterday = re.search(r"\b(yesterday|last night|last evening)\b", clause, re.IGNORECASE) is not None
    elapsed = now - t
    if yesterday or elapsed < 0:      # said to be yesterday, or a time later than 'now' (which can only be the day before)
        elapsed += 24 * 60
    return round(elapsed / 60, 4)


def intake(topic: str, text: str, time_now: str) -> dict:
    kind = kind_of(topic, text)
    out: dict = {"kind": kind}
    if kind != "none":
        h = hours_before_now(text, time_now)
        if h is not None:
            out["hours_before_now"] = h
    return out
