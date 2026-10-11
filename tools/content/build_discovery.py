"""Builds content/problems/discovery.json: when each problem of a P0 case joins the problem registry (DT-073).

    uv run --project tools tools/content/build_discovery.py            # write
    uv run --project tools tools/content/build_discovery.py --check    # fail if the file is out of date

A problem is found when the trainee has seen the evidence for it: a result opened, an examination finding, a history topic asked, a reading taken, or a flag the engine set (a fasting breach, the sepsis tier).
The rules live here, next to the case files and not inside them, so no case file changes and no approval is reset. Every rule is a JsonLogic rule over the facts the engine keeps (the same subset the workflow uses).
Each rule says in words what the trainee has to have seen (`how`). They are the project's reading of the case content (SR-2867) and a clinician is asked to confirm them (docs/research/clinician_signoff.md, Q10).
The builder checks every problem and variant named here exists in its case file, and that every P0 problem is either listed here or listed as deliberately not discoverable.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "problems" / "discovery.json"
REF = "SR-2867"


def has(key, value):
    return {"in": [value, {"var": key}]}


def anyof(*rules):
    return {"or": list(rules)}


def allof(*rules):
    return {"and": list(rules)}


def seen(test):
    return has("viewed_tests", test)


def asked(topic):
    return has("topics", topic)


def measured(name):
    return has("measured", name)


def found(finding):
    return has("findings", finding)


def sepsis_high():
    return {"==": [{"var": "sepsis_tier"}, "high"]}


# (case, problem, trigger, how)
RULES = [
    ("VAR-S1-B", "b.bp", measured("systolic_bp"), "the trainee takes a blood pressure and sees it is raised but below 180/110"),
    ("VAR-S1-B", "b.bp2", measured("systolic_bp"), "the trainee takes the blood pressure and sees 188/114 (variant B2 only)"),
    ("VAR-S1-C", "c.diabetes", asked("medicines"), "the trainee asks about medicines and learns of the diabetes tablets"),
    ("VAR-S1-D", "d.anticoag", asked("medicines"), "the trainee asks about medicines and learns of the apixaban"),
    ("VAR-S1-E", "e.cholecystitis", seen("ultrasound"), "the trainee opens the ultrasound report that shows the inflamed gallbladder"),
    ("VAR-S1-G", "g.fasting", has("flags", "fasting.breach"), "the fasting rule is found broken (a light meal 2 hours before)"),
    ("VAR-S1-H", "h.preg", seen("pregnancy"), "the trainee opens the positive pregnancy test"),
    ("VAR-S1-J", "j.k", seen("u_and_e"), "the trainee opens the urea and electrolytes and sees the potassium"),
    ("VAR-S1-PB", "pb.stone", seen("lft"), "the trainee opens the liver tests that point to a stone in the duct"),
    ("VAR-S1-PB", "pb.cholangitis", seen("lft"), "the same liver tests: watch for infection of the duct"),
    ("STK-S1-D", "s1d.sepsis", sepsis_high(), "the NEWS2 shows the high-risk sepsis tier"),
    ("STK-S1-D", "s1d.duct", seen("lft"), "the trainee opens the liver tests"),
    ("STK-S1-D", "s1d.aki", seen("u_and_e"), "the trainee opens the kidney tests"),
    ("STK-S1-D", "s1d.chole", asked("complaint"), "the trainee takes the history: the planned cholecystectomy is known from the start"),
    ("VAR-S2-B", "sb.bleed", seen("pregnancy"), "the trainee opens the positive pregnancy test in a patient in pain and pale"),
    ("VAR-S2-B", "sb.blocker", seen("pregnancy"), "the pregnancy status is positive"),
    ("VAR-S2-D", "sd.unknown", seen("ultrasound"), "the trainee opens the scan report that did not answer the question"),
    ("VAR-S2-F", "sf.stomach", asked("last_food"), "the trainee asks about the last meal and learns of the biryani 2 hours ago"),
    ("VAR-S2-G", "sg.aki", seen("u_and_e"), "the trainee opens the kidney tests"),
    ("VAR-S2-G", "sg.surgery", seen("fbc"), "the trainee opens the blood count that shows the infection"),
    ("VAR-S2-PC", "pc.pain", measured("pain_score"), "the trainee asks for the pain score and hears 8 out of 10"),
    ("VAR-S2-PC", "pc.appendicitis", seen("crp"), "the trainee opens the inflammatory markers"),
    ("VAR-S2-PC", "pc.aspiration", asked("last_food"), "the trainee asks about the last meal"),
    ("VAR-S2-PD", "pd.sepsis", sepsis_high(), "the NEWS2 shows the high-risk sepsis tier"),
    ("VAR-S2-PD", "pd.clock", sepsis_high(), "the same tier starts the one-hour clock"),
    ("VAR-S2-PD", "pd.source", seen("lactate_cultures"), "the trainee opens the lactate that shows poor circulation from a source"),
    ("STK-S2-B", "s2b.sepsis", sepsis_high(), "the NEWS2 shows the high-risk sepsis tier"),
    ("STK-S2-B", "s2b.allergy", {"==": [{"var": "allergies_known"}, True]}, "the allergy history has been taken"),
    ("STK-S2-B", "s2b.source", found("ex.abdomen"), "the trainee examines the abdomen and finds it rigid"),
]
# P0 problems that are deliberately not discoverable: they are the case's own framing (a fit patient, the standing checkpoints)
NOT_DISCOVERABLE = {("VAR-S1-A", "a.nothing"), ("VAR-S1-A", "a.blockers")}
P0 = ["VAR-S1-A", "VAR-S1-B", "VAR-S1-C", "VAR-S1-D", "VAR-S1-E", "VAR-S1-G", "VAR-S1-H", "VAR-S1-J", "VAR-S1-PB", "STK-S1-D", "VAR-S2-B", "VAR-S2-D", "VAR-S2-F", "VAR-S2-G", "VAR-S2-PC", "VAR-S2-PD", "STK-S2-B"]


def engine_keys() -> set[str]:
    """The facts a rule may read: the keys RuleState.Build gives (read from the C# source, as validate.py does for the workflow rules)."""
    src = (ROOT / "src" / "ProcSense.Core" / "Workflow" / "CaseGraphSubsystem.cs").read_text(encoding="utf-8")
    block = src[src.index("internal static class RuleState"):src.index("public sealed class ReadinessSubsystem")]
    return set(re.findall(r'\["([a-z_0-9]+)"\] =', block))


def keys_of(rule, out: set[str]) -> None:
    if isinstance(rule, dict):
        for k, v in rule.items():
            if k == "var":
                out.add((v if isinstance(v, str) else v[0]).split(".")[0])
            else:
                keys_of(v, out)
    elif isinstance(rule, list):
        for x in rule:
            keys_of(x, out)


def build() -> str:
    known = engine_keys()
    for case, pid, rule, how in RULES:
        used: set[str] = set()
        keys_of(rule, used)
        if used - known:
            raise SystemExit(f"{case}:{pid} reads facts the engine does not keep: {sorted(used - known)}")
    problems = {}
    for case in P0:
        d = json.loads((ROOT / "content" / "case_files" / f"{case}.json").read_text(encoding="utf-8"))
        problems[case] = {p["id"]: p for p in d["problems"]}
    listed = set()
    out = []
    for case, pid, rule, how in RULES:
        if case not in problems or pid not in problems[case]:
            raise SystemExit(f"{case} has no problem {pid}")
        listed.add((case, pid))
        out.append({"case": case, "problem": pid, "trigger": rule, "how": how, "ref": REF})
    missing = sorted((c, p) for c in P0 for p in problems[c] if (c, p) not in listed and (c, p) not in NOT_DISCOVERABLE)
    if missing:
        raise SystemExit("P0 problems with no discovery rule: " + ", ".join(f"{c}:{p}" for c, p in missing))
    return json.dumps({"discovery": out, "refs": [REF]}, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/problems/discovery.json is out of date; run tools/content/build_discovery.py")
            return 1
        print(f"content/problems/discovery.json is up to date ({len(RULES)} rules for 17 P0 cases)")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
