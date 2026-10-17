"""A seeded set of text inconsistencies and the rule-based baseline that must be beaten (p1-live-scenario-generation-s15): `uv run --project tools tools/generation/inconsistency_set.py [--write]`.

Each member is an authored case in which ONE line of the patient's own text was changed so that it contradicts the structured case: an allergy denied that the case lists, an allergy invented that it does not,
the wrong operation, the wrong side of the body, the wrong age, the wrong sex. The structured case is untouched. The set is synthetic and deterministic (up to PER_TYPE of each kind, in case-id order); the clean
cases are the same authored cases unchanged and measure false alarms. `baseline_flags(case)` is the checker that exists without a model: the cross-modal rules, Stage B, the lexicon (an allergen the case does not
list, an operation word of the other operation), the age written in the text, and the pronoun-against-sex test. The critic (server/generation/critic.py) must find more of the inserted contradictions than this.
"""
from __future__ import annotations

import copy
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
OUT = ROOT / "tests" / "generation" / "text_inconsistencies.json"
PER_TYPE = 36
OTHER_OP = {"S1": "I am having my appendix taken out.", "S2": "I am having my gallbladder taken out."}


def _swap_side(text: str) -> str | None:
    if re.search(r"\bright\b", text, re.I):
        return re.sub(r"\bright\b", "left", text, flags=re.I)
    if re.search(r"\bleft\b", text, re.I):
        return re.sub(r"\bleft\b", "right", text, flags=re.I)
    return None


def mutations(case: dict) -> list[tuple[str, str, str]]:
    """[(kind, line id, new text)] that apply to this case."""
    out = []
    if case["allergies"]:
        out.append(("allergy_denied", "allergy", "None. I have never had a reaction to anything."))
    else:
        out.append(("allergy_invented", "allergy", "Penicillin: my throat swelled up and I collapsed."))
    out.append(("operation_wrong", "reason", OTHER_OP[case["scenario"]]))
    for h in case["history"]:
        if h["topic"] == "complaint" and h.get("variant") is None and _swap_side(h["answer"]):
            out.append(("side_flip", "h.complaint", _swap_side(h["answer"])))
    complaint = next((h["answer"] for h in case["history"] if h["topic"] == "complaint" and h.get("variant") is None), "")
    flags_h = next((h for h in case["history"] if h["topic"] == "red_flags" and h.get("variant") is None), None)
    if flags_h and re.search(r"right|left", complaint, re.I):
        side = "left" if re.search(r"right", complaint, re.I) else "right"
        out.append(("side_vs_other_line", "h.red_flags", f"The pain is on the {side} side only."))
    if flags_h and re.search(r"pain|ache|hurt", complaint, re.I):
        out.append(("pain_denied_elsewhere", "h.red_flags", "No pain at all, I only feel tired."))
    summary = case["presentation"].get("summary", "")
    age = case["identity"]["age"]
    if re.search(rf"\b{age}\b", summary):
        out.append(("age_wrong", "summary", re.sub(rf"\b{age}\b", str(age + 17), summary, count=1)))
    wrong = "She says" if case["identity"]["sex"] == "M" else "He says"
    pron = "her" if case["identity"]["sex"] == "M" else "his"
    out.append(("sex_wrong", "h.complaint", f"{wrong} {pron} pain started yesterday."))
    return out


def apply(case: dict, line: str, text: str) -> dict:
    c = copy.deepcopy(case)
    if line == "allergy":
        c["allergy_answer"] = text
    elif line == "reason":
        c["reason_answer"] = text
    elif line == "summary":
        c["presentation"]["summary"] = text
    else:
        topic = line.split(".", 1)[1]
        for h in c["history"]:
            if h["topic"] == topic and h.get("variant") is None:
                h["answer"] = text
    return c


def build(cases: list[dict]) -> dict:
    counts: dict[str, int] = {}
    members = []
    for case in sorted(cases, key=lambda c: c["id"]):
        if not case["status"].startswith("APPROVED"):
            continue
        for kind, line, text in mutations(case):
            if counts.get(kind, 0) >= PER_TYPE:
                continue
            counts[kind] = counts.get(kind, 0) + 1
            members.append({"id": f"{case['id']}:{kind}", "case": case["id"], "kind": kind, "line": line, "text": text})
    clean = [c["id"] for c in sorted(cases, key=lambda c: c["id"]) if c["status"].startswith("APPROVED")][::2]
    return {"note": "Synthetic. Each member changes one line of the patient's text so that it contradicts the structured case; clean cases are authored cases unchanged (false-alarm control).", "members": members, "clean": clean, "counts": counts}


def baseline_flags(case: dict) -> list[str]:
    """What the checks that exist without a model find in a case: a list of line ids (and 'case' for structured checks)."""
    from generation import crossmodal, quality
    from validation.validator import CATEGORIES, has_term, norm  # noqa: F401
    flags = []
    lex = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))
    substances = " ".join(a["substance"].lower() for a in case["allergies"])
    ans = case["allergy_answer"] or ""
    if case["allergies"] and re.match(r"^\s*(none|no known|no\b)", ans, re.I):
        flags.append("allergy")
    for term in lex["categories"]["allergen"]:
        if has_term(norm(ans), term) and norm(term) not in norm(substances) and not case["allergies"]:
            flags.append("allergy")
        elif has_term(norm(ans), term) and case["allergies"] and norm(term) not in norm(substances):
            flags.append("allergy")
    reason = case["reason_answer"].lower()
    other = {"S1": r"appendi", "S2": r"gall ?bladder|cholecyst"}[case["scenario"]]
    if re.search(other, reason):
        flags.append("reason")
    age = case["identity"]["age"]
    summary = case["presentation"].get("summary", "")
    found = [int(n) for n in re.findall(r"\b(\d{1,3})\b(?:[- ]year|, | years)", summary)]
    if found and age not in found:
        flags.append("summary")
    if any(f["code"] == "Q_PERSONA" for f in quality.persona(case)[1]):
        flags.append("h.complaint")
    media = {"slots": []}
    flags += ["case" for _ in crossmodal.check(case, media)]
    try:
        from composer import stage_b
        ctx = stage_b.load_ctx(False)
        flags += ["case" for d in stage_b.validate(case, ctx).defects if d.code not in ("PROBLEM_UNDISCOVERABLE",)]
    except Exception:
        pass
    return sorted(set(flags))


def main(argv: list[str]) -> int:
    from composer import blocks as B
    data = build(B.load_cases())
    print(f"{len(data['members'])} inconsistencies {data['counts']}, {len(data['clean'])} clean cases")
    if "--write" in argv:
        OUT.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
