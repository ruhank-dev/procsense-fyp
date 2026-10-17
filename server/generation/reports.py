"""Investigation reports with the numbers locked (p1-live-scenario-generation-s15, features B18 and B22).

    texts = render(case, media)             # {"lab.<id>": "...", "img.<test>": "..."}: one line per result and one report per picture
    problems = check(texts, case, media)    # [] when every number in every text is a number the case or the picture gives

A laboratory line is built from the result row (test, value, unit, reference range, flag, comment) with the value copied as text, never re-typed or re-computed. An imaging report is the picture's own approved
report (media.py), whose measurements are the picture's. Paraphrase by a language model is optional and goes through `check`; the template is used if the paraphrase fails. The set of numbers a text may
contain is closed: the case's result values and ranges, and the picture's measurements and the numbers of its report.
"""
from __future__ import annotations

import re

NUMBER = re.compile(r"(?<![\w.])\d+(?:\.\d+)?(?![\w])")


def lab_line(r: dict) -> str:
    parts = [f"{r['test']}: {r['value']}"]
    if r.get("unit"):
        parts[0] += f" {r['unit']}"
    if r.get("normal_range"):
        parts.append(f"reference {r['normal_range']}")
    if r.get("flag") and r["flag"] not in ("normal", "negative"):
        parts.append(r["flag"].replace("_", " "))
    if r.get("quality"):
        parts.append("sample: " + r["quality"].replace("_", " "))
    if r.get("comment"):
        parts.append(r["comment"].rstrip("."))
    return "; ".join(parts) + "."


def render(case: dict, media: dict | None = None) -> dict[str, str]:
    out = {f"lab.{r['id']}": lab_line(r) for r in case["results"] if not r.get("variant")}
    for s in (media or {}).get("slots", []):
        if s.get("report"):
            out[f"img.{s['test']}"] = s["report"]
    return out


def allowed_numbers(case: dict, media: dict | None = None) -> set[str]:
    nums: set[str] = set()
    for r in case["results"]:
        for field in ("test", "unit", "value", "normal_range", "comment"):
            nums.update(NUMBER.findall(str(r.get(field) or "")))
        if r.get("previous"):
            nums.update(NUMBER.findall(str(r["previous"])))
    for s in (media or {}).get("slots", []):
        nums.update(NUMBER.findall(s.get("report", "")))
        for v in s.get("measurements", {}).values():
            nums.update(NUMBER.findall(str(v)))
    return nums


def check(texts: dict[str, str], case: dict, media: dict | None = None) -> list[str]:
    allowed = allowed_numbers(case, media)
    out = []
    for key, text in texts.items():
        for n in NUMBER.findall(text):
            if n not in allowed:
                out.append(f"UNLOCKED_NUMBER: {key} contains {n}, which is not a number of the case or the picture")
    return out
