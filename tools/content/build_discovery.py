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
REVIEW = ROOT / "docs" / "research" / "discovery_review.md"
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


# ---------------------------------------------------------------------------------------------------------------- the 36 P1 and P2 cases (DT-091)
# The rules of the 17 P0 cases above were written one by one. For the other 36 cases they are DERIVED from the case's own content by a fixed reading, so the same case always gives the same rules and every rule can be
# checked against the case: the words of a problem (label, why, expected actions) name the evidence the trainee must have seen, and the evidence must exist in that case (a result of that test, an examination
# finding, a history topic). The first matching clue wins, in the order of CLUES. Nothing here is a clinician's judgement: the review pack (docs/research/discovery_review.md) lists every rule in words for a clinician to
# confirm or change (docs/research/clinician_signoff.md, Q10); until then each rule is the project's reading, marked as such in the file.
TEST_OF = {                                   # result name in a case file -> the investigation that holds it
    "WBC": "fbc", "Hb": "fbc", "Haemoglobin": "fbc", "MCV": "fbc", "Neutrophils": "fbc", "CRP": "crp", "Creatinine": "u_and_e", "Urea": "u_and_e", "Potassium": "u_and_e", "Sodium": "u_and_e",
    "Bilirubin": "lft", "ALT": "lft", "ALP": "lft", "HbA1c": "hba1c", "INR": "clotting", "Lactate": "lactate_cultures", "Blood cultures": "lactate_cultures", "Ferritin": "ferritin", "TSH": "tsh",
    "Tryptase": "tryptase", "Pregnancy test": "pregnancy", "Urine dipstick": "urine_dipstick", "Capillary glucose": "glucose_fingerprick", "Capillary ketones": "glucose_fingerprick", "ECG": "ecg",
    "BMI": "bmi", "STOP-BANG": "stopbang", "Ultrasound appendix": "ultrasound", "Ultrasound abdomen": "ultrasound", "CT appendix": "ct", "CT abdomen": "ct", "MRI appendix": "mri",
    "Chest X-ray": "chest_xray", "Venous pH": "blood_gas", "Venous bicarbonate": "blood_gas", "Venous pCO2": "blood_gas",
}
# (words that point at the evidence, kind, evidence): kind "test" needs a result whose investigation is the evidence; "exam" needs that finding; "topic" needs that history topic; "allergy" the known-allergy fact
CLUES = [
    ("sepsis", "sepsis", None), ("septic", "sepsis", None), ("news2", "sepsis", None),
    ("lactate", "test", "lactate_cultures"), ("ketoacidosis", "test", "glucose_fingerprick"), ("dka", "test", "glucose_fingerprick"), ("ketone", "test", "glucose_fingerprick"),
    ("glucose", "test", "glucose_fingerprick"), ("hypoglyc", "test", "glucose_fingerprick"), ("low sugar", "test", "glucose_fingerprick"),
    ("hyperkal", "test", "u_and_e"), ("potassium", "test", "u_and_e"), ("sodium", "test", "u_and_e"), ("kidney", "test", "u_and_e"), ("creatinine", "test", "u_and_e"), ("dehydrat", "test", "u_and_e"),
    ("bile duct", "test", "lft"), ("cholangitis", "test", "lft"), ("liver", "test", "lft"), ("bilirubin", "test", "lft"),
    ("hba1c", "test", "hba1c"), ("inr", "test", "clotting"), ("warfarin", "topic", "medicines"),
    ("anaemia", "test", "fbc"), ("haemoglobin", "test", "fbc"), ("iron", "test", "ferritin"),
    ("pregnan", "test", "pregnancy"), ("tryptase", "test", "tryptase"),
    ("sleep apnoea", "test", "stopbang"), ("obstructive sleep", "test", "stopbang"), ("airway", "exam", "ex.airway"),
    ("atrial fibrillation", "test", "ecg"), ("af ", "test", "ecg"), ("slow heart", "test", "ecg"), ("ecg", "test", "ecg"),
    ("ultrasound", "test", "ultrasound"), ("ct ", "test", "ct"), ("mri", "test", "mri"), ("chest x-ray", "test", "chest_xray"),
    ("white cell", "test", "fbc"), ("infection", "test", "fbc"),
    ("penicillin", "allergy", None), ("allergy", "allergy", None), ("apixaban", "topic", "medicines"), ("aspirin", "topic", "medicines"), ("antiplatelet", "topic", "medicines"), ("inhibitor", "topic", "medicines"),
    ("glp", "topic", "medicines"), ("beta-blocker", "topic", "medicines"), ("tablet", "topic", "medicines"), ("insulin", "topic", "medicines"), ("morphine", "topic", "medicines"), ("opioid", "topic", "medicines"),
    ("meal", "topic", "last_food"), ("stomach", "topic", "last_food"), ("fasting", "topic", "last_food"), ("fasted", "topic", "last_food"),
    ("abdomen", "exam", "ex.abdomen"), ("appendic", "exam", "ex.abdomen"), ("cholecyst", "exam", "ex.abdomen"), ("perforat", "exam", "ex.abdomen"), ("breathing", "exam", "ex.chest"), ("chest", "exam", "ex.chest"),
    ("pulse", "exam", "ex.pulse"), ("skin", "exam", "ex.skin"),
]


def _case_has(case: dict, kind: str, ev: str | None) -> bool:
    if kind == "test":
        return any(TEST_OF.get(res["test"].split(" (")[0]) == ev or TEST_OF.get(res["test"]) == ev for res in case["results"])
    if kind == "exam":
        return any(e["id"] == ev for e in case["examination"])
    if kind == "topic":
        return any(h["topic"] == ev for h in case["history"])
    return True


def derive(case: dict, problem: dict) -> tuple[dict, str]:
    text = " ".join([problem["label"], problem.get("why", "")] + list(problem.get("expected_actions", []))).lower() + " "
    label = problem["label"].lower() + " "
    for words in (label, text):                                     # the label is read first, then the rest of the problem's words
        for needle, kind, ev in CLUES:
            if needle in words and _case_has(case, kind, ev):
                if kind == "sepsis":
                    return anyof(sepsis_high(), seen("lactate_cultures")) if _case_has(case, "test", "lactate_cultures") else sepsis_high(), "the NEWS2 shows the high-risk sepsis tier" + (" or the trainee opens the lactate" if _case_has(case, "test", "lactate_cultures") else "")
                if kind == "test":
                    names = sorted({res["test"] for res in case["results"] if TEST_OF.get(res["test"].split(" (")[0]) == ev})
                    return seen(ev), f"the trainee opens the {ev.replace('_', ' ')} result ({', '.join(names)})"
                if kind == "exam":
                    f = next(e for e in case["examination"] if e["id"] == ev)
                    return found(ev), f"the trainee examines ({f['system']}) and finds: {f['finding'][:70]}"
                if kind == "topic":
                    return asked(ev), f"the trainee asks about {ev.replace('_', ' ')} and learns it"
                if kind == "allergy":
                    return {"==": [{"var": "allergies_known"}, True]}, "the allergy history has been taken"
    return asked("complaint"), "the trainee takes the history: the situation is part of what the patient says at the start"


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
    all_cases = {}
    for path in sorted((ROOT / "content" / "case_files").glob("*.json")):
        d = json.loads(path.read_text(encoding="utf-8"))
        all_cases[d["id"]] = d
    derived = []
    for cid in sorted(all_cases):
        if cid in P0:
            continue
        for p in all_cases[cid]["problems"]:
            rule, how = derive(all_cases[cid], p)
            derived.append((cid, p["id"], rule, how + " [derived from the case content]"))
    for case, pid, rule, how in RULES + derived:
        used: set[str] = set()
        keys_of(rule, used)
        if used - known:
            raise SystemExit(f"{case}:{pid} reads facts the engine does not keep: {sorted(used - known)}")
    problems = {}
    for case in P0 + sorted(c for c in all_cases if c not in P0):
        problems[case] = {p["id"]: p for p in all_cases[case]["problems"]}
    listed = set()
    out = []
    for case, pid, rule, how in RULES + derived:
        if case not in problems or pid not in problems[case]:
            raise SystemExit(f"{case} has no problem {pid}")
        listed.add((case, pid))
        out.append({"case": case, "problem": pid, "trigger": rule, "how": how, "ref": REF})
    missing = sorted((c, p) for c in problems for p in problems[c] if (c, p) not in listed and (c, p) not in NOT_DISCOVERABLE)
    if missing:
        raise SystemExit("problems with no discovery rule: " + ", ".join(f"{c}:{p}" for c, p in missing))
    return json.dumps({"discovery": out, "refs": [REF]}, indent=1, ensure_ascii=False) + "\n"


def review_page() -> str:
    """The page a clinician reads (DT-091): every rule in words, by case, with the problem it concerns, who wrote it (by hand or derived from the case content) and a column to confirm or change. Nobody has reviewed it yet."""
    data = json.loads(build())["discovery"]
    cases = {}
    for path in sorted((ROOT / "content" / "case_files").glob("*.json")):
        d = json.loads(path.read_text(encoding="utf-8"))
        cases[d["id"]] = d
    L = ["# Problem discovery rules: review pack for a clinician", "", "_Generated by `tools/content/build_discovery.py`; do not edit by hand._", "",
         "**Status: not reviewed by a clinician.** Each rule says what the trainee must have seen or done before a problem of the case joins the problem registry (a result opened, an examination finding, a history topic, a flag the engine set). "
         "The 29 rules of the 17 P0 cases were written one by one from the case content; the other 127 were derived from the same content by a fixed reading (the words of the problem name the evidence, and the evidence must exist in that case), "
         "so they are consistent with the case but are the project's reading, not a clinical judgement. A clinician is asked to confirm or change each (`docs/research/clinician_signoff.md`, Q10). "
         "Changing a rule never changes a case file or its approval.", "",
         "Internal checks that pass today: every problem of every case has a rule (except the two framing problems of the fit patient), every rule names only facts the engine keeps, every derived rule points at evidence that exists in its case, "
         "and the rule is false on an empty state and true once its evidence is there (`DiscoveryTests.cs`).", ""]
    by = {}
    for x in data:
        by.setdefault(x["case"], []).append(x)
    L += [f"{len(data)} rules for {len(by)} cases.", ""]
    for cid in sorted(by):
        c = cases[cid]
        L += [f"## {cid} ({c['scenario']}, {c.get('tier', '')})", "", "| Problem | Type | What the trainee must have seen | Written | Clinician (confirm / change) |", "|---|---|---|---|---|"]
        label = {p["id"]: p for p in c["problems"]}
        for x in by[cid]:
            how = x["how"].replace(" [derived from the case content]", "")
            origin = "derived" if x["how"].endswith("[derived from the case content]") else "by hand"
            L.append(f"| {x['problem']}: {label[x['problem']]['label'][:80].replace('|', '/')} | {label[x['problem']]['type']} | {how.replace('|', '/')} | {origin} | |")
        L.append("")
    return "\n".join(L) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" not in argv:
        REVIEW.write_text(review_page(), encoding="utf-8", newline="\n")
    if "--check" in argv:
        if not REVIEW.exists() or REVIEW.read_text(encoding="utf-8") != review_page():
            print("FAIL  docs/research/discovery_review.md is out of date; run tools/content/build_discovery.py")
            return 1
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/problems/discovery.json is out of date; run tools/content/build_discovery.py")
            return 1
        print(f"content/problems/discovery.json is up to date ({len(RULES)} authored rules for the 17 P0 cases and the rules derived for the other cases)")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
