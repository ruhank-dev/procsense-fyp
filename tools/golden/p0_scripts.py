"""Writes and checks the correct-path scripts for the P0 cases (p0-case-library s02, plan tests T-CASE1 and T-CASE2) and the 16 P1 cases (p1-case-library-extended-s03, EXP-CASE-3).

    uv run --project tools tools/golden/p0_scripts.py --write            # write tests/golden/<case>_correct.actions.json
    uv run --project tools tools/golden/p0_scripts.py --check            # run each script through the CLI, list refusals and compare the end state with the case file

A script is the walkthrough's correct path for one case, built from small segments (identity, history, readings, examination, risk, tests, consent, ward checklist, theatre) and the case's own data
(its tests, its expected ASA grade). `--check` plays it with the real engine and requires: no action refused, and the end state equal to the case's expected end state (or its variant's).
"""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "content"))
import build_discovery as bd  # noqa: E402

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]
GOLDEN = ROOT / "tests" / "golden"
P1_CASES = {"VAR-S1-I", "VAR-S1-K", "VAR-S1-L", "VAR-S1-M", "VAR-S2-E", "VAR-S2-H", "VAR-S2-I", "VAR-S2-J", "VAR-S2-K", "VAR-S2-L", "STK-S1-A", "STK-S1-B", "STK-S1-C", "STK-S2-A", "STK-S2-C", "STK-S2-D"}


def stem(case_id: str) -> str:
    """The file stem of a case's correct-path golden. The 17 P0 cases keep `<case>_correct`; the 16 P1 cases use `<case>_correct_p1`, so every tool that lists the P0 cases by their `_correct` files (voice catalogue, EMR sections, golden paths, traceability) is unchanged."""
    return f"{case_id}_correct_p1" if case_id in P1_CASES else f"{case_id}_correct"
END_CODE = {"ready": "READY_SAFE", "postponed": "POSTPONED_APPROPRIATELY", "redirected": "REDIRECTED_APPROPRIATELY", "delayed": "SAFE_BUT_DELAYED", "failure": "SAFETY_FAILURE"}
TOPICS = ["complaint", "red_flags", "past_history", "medicines", "herbal", "previous_anaesthetic", "family_anaesthetic", "smoking", "alcohol", "last_food", "last_drink", "pregnancy"]
VTE_NO = "age_over_60:no,bmi_over_30:no,cancer:no,previous_clot:no,family_history_clots:no,oestrogen_or_hrt:no,pregnancy:no,dehydration:no,reduced_mobility:no,surgery_over_90_minutes:no,acute_tummy_inflammation:no,active_bleeding:no,blood_thinners:no,platelets_below_75:no,very_high_bp:no"


class Script:
    def __init__(self) -> None:
        self.t = 0.0
        self.rows: list[dict] = []

    def add(self, verb: str, gap: float = 4, **params: str) -> "Script":
        row = {"t": round(self.t, 1), "verb": verb}
        if params:
            row["params"] = {k: str(v) for k, v in params.items()}
        self.rows.append(row)
        self.t += gap
        return self

    def skip(self, minutes: int) -> "Script":
        self.add("time_skip", 1, minutes=str(minutes))
        self.t += minutes * 60
        return self

    def wait(self, seconds: float) -> "Script":
        self.t += seconds
        return self

    def json(self) -> str:
        return "[\n" + ",\n".join("  " + json.dumps(r, ensure_ascii=False) for r in self.rows) + "\n]\n"


def case(case_id: str) -> dict:
    return json.loads((ROOT / "content" / "case_files" / f"{case_id}.json").read_text(encoding="utf-8"))


def investigations() -> list[dict]:
    return json.loads((ROOT / "content" / "investigations" / "investigations.json").read_text(encoding="utf-8"))["tests"]


# ---------------------------------------------------------------- segments

def identity(s: Script) -> None:
    for v in ("introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity", "ask_allergies", "ask_allergy_reaction", "ask_reason_for_admission", "ask_responsible_adult"):
        s.add(v)


def history(s: Script) -> None:
    for t in TOPICS:
        s.add("ask", topic=t)
    s.add("summarise")


def readings(s: Script) -> None:
    s.add("attach_sensor", sensor="spo2").add("attach_sensor", sensor="nibp_cuff").add("measure_nibp", 12).add("measure_temperature").add("count_rr").add("assess_acvpu").add("ask_pain_score")


def examination(s: Script, c: dict) -> None:
    regions = []
    for e in c["examination"]:
        for d in e["disclose_on"]:
            if d.startswith("examine:") and d.split(":", 1)[1] not in regions and d.split(":", 1)[1] != "murphy":
                regions.append(d.split(":", 1)[1])
    s.add("ask_exam_permission")
    for r in regions:
        if r not in ("allergy_band",):
            s.add("examine", region=r)
    if any("examine:murphy" in e["disclose_on"] for e in c["examination"]):
        s.add("murphy_test")
    for item in dict.fromkeys(d.split(":", 1)[1] for e in c["examination"] for d in e["disclose_on"] if d.startswith("airway_check:")):
        s.add("airway_check", item=item)


def tests_for(c: dict) -> list[dict]:
    """The orderable tests whose analytes the case gives results for (in the order of the investigations list)."""
    names = {r["test"].split(" (")[0].strip().lower() for r in c["results"] if r.get("variant") is None}
    return [t for t in investigations() if any(a.lower() in names for a in t.get("analytes", []))]


def order_tests(s: Script, tests: list[dict]) -> None:
    for t in tests:
        if t["kind"] == "imaging":
            continue                                  # a scan needs a transfer; scripts that want one add it themselves
        s.add("order_investigation", 2, test=t["id"])
        if t["kind"] == "blood":
            s.add("take_sample", 2, test=t["id"], tube=t["tube"])
        else:
            s.add("perform_bedside_test", 2, test=t["id"])


def view_tests(s: Script, tests: list[dict]) -> None:
    for t in tests:
        if t["kind"] != "imaging":
            s.add("view_result", 1, test=t["id"])


def risk(s: Script, c: dict) -> None:
    asa = c["risk"]["asa"]["min"]
    emergency = c["risk"]["asa"]["emergency"]
    params = {"asa": str(asa)}
    if emergency:
        params["emergency"] = "true"
    s.add("assess_risk", 6, **params)
    s.add("assess_vte", 6, items=VTE_NO)
    s.add("compute_score", 8, score="stopbang", snoring="false", tired="false", observed="false", pressure="false", bmi_over_35="false", age_over_50="false", neck_over_40="false", male="false")
    s.add("request_anaesthetic_review")


def consent(s: Script, c: dict) -> None:
    sc = c["scenario"]
    s.add("check_capacity", 1)
    for topic, q in (("diagnosis", None), ("operation", None), ("benefits", ("s1.home" if sc == "S1" else "s2.children")), ("risks", None), ("alternatives", ("s1.scars" if sc == "S1" else "s2.tablets")),
                     ("no_treatment", None), ("matters", None)):
        s.add("consent_discussion", 1, topic=topic)
        if q:
            s.add("answer_question", 1, q=q, a="approved")
    s.add("teach_back", 1).add("sign_consent", 1)


def ward(s: Script, c: dict) -> None:
    diabetic = any("diabetes" in h["answer"].lower() for h in c["history"] if h["topic"] == "past_history" and h.get("variant") is None)
    for item, detail in (("gown", "worn"), ("jewellery", "removed"), ("aids", "noted"), ("site_marking", "not_applicable"), ("documents", "together")) + ((("cbg", "recorded"),) if diabetic else ()):
        s.add("prepare", 1, item=item, detail=detail)
    for item in ("fasting", "gown", "jewellery", "aids", "site_marking", "pregnancy", "consent", "bands") + (("cbg",) if diabetic else ()) + ("temperature", "documents"):
        s.add("tick_ward_item", 1, item=item)
    s.add("sign_ward_checklist", 11)


def theatre(s: Script, c: dict, antibiotic: bool = False) -> None:
    for st in ("anaesthetic_machine", "monitors", "suction", "oxygen_airway", "lap_tower", "co2_cylinder", "diathermy", "warming"):
        s.add("check_station", 1, station=st)

    def say(item: str, response: str) -> None:
        s.add("checklist_item", 1, phase=item.split(".")[0], item=item, response=response)

    allergy = "yes" if c["allergies"] else "no"
    aspiration = "yes" if c["scenario"] == "S2" else "no"
    say("sign_in.01", "yes"); say("sign_in.02", "na"); s.wait(10)
    say("sign_in.04", "yes"); say("sign_in.05", allergy); say("sign_in.06", aspiration); say("sign_in.07", "no"); s.wait(10)
    say("time_out.02", "yes"); say("time_out.03", "yes" if antibiotic else ("no" if c["scenario"] == "S2" else "na")); s.wait(30); say("time_out.07", "na")
    s.add("declare_or_ready", 1).add("complete_time_out", 1)


def emergency_consent(s: Script, c: dict) -> None:
    """The patient cannot follow a consent talk: call the senior with a full SBAR, check capacity, then the emergency route."""
    s.add("escalate", 2, role="senior", s=f"{c['identity']['title']} {c['identity']['full_name']}, emergency, cannot consent", b="acute abdominal problem needing urgent surgery", a="patient lacks capacity to consent now", r="please document the emergency route")
    s.add("check_capacity", 1).add("use_emergency_route", 1)


def antibiotics(s: Script) -> None:
    s.add("check_treatment_timing", 2, treatment="TR-ABX-S2")
    s.add("request_medication", 2, **{"class": "TR-ABX-S2", "indication": "appendicitis"})
    s.wait(150)


# ---------------------------------------------------------------- the cases

def planned_ready(c: dict, emergency: bool = False, antibiotic: bool = False, repeat: list[str] | None = None) -> Script:
    s = Script()
    identity(s); history(s)
    tests = tests_for(c)
    order_tests(s, tests)
    readings(s); examination(s, c); risk(s, c)
    s.skip(60)
    view_tests(s, tests)
    for t in repeat or []:                                # a repeat after the first result (for example a haemolysed potassium)
        test = next(x for x in tests if x["id"] == t)
        s.add("order_investigation", 2, test=t)
        s.add("take_sample", 2, test=t, tube=test["tube"])
        s.skip(95)
        s.add("view_result", 1, test=t)
    if emergency:
        emergency_consent(s, c)
    else:
        consent(s, c)
    if c["scenario"] == "S1":
        ward(s, c)
    if antibiotic:
        antibiotics(s)
    s.add("decide_ready", 10, option="proceed")
    theatre(s, c, antibiotic)
    s.add("wait", 1)
    return s


def with_options(**kw):
    return lambda c: planned_ready(c, **kw)


def needs(case_id: str) -> dict:
    """What the trainee has to see to find the case's problems, read from its discovery rules: tests viewed, topics asked, readings taken, findings made."""
    out = {"tests": [], "topics": [], "measured": [], "findings": [], "flags": [], "sepsis": False, "allergies": False}

    def walk(rule) -> None:
        if isinstance(rule, dict):
            for k, v in rule.items():
                if k == "in" and isinstance(v, list) and isinstance(v[1], dict) and "var" in v[1]:
                    key = {"viewed_tests": "tests", "topics": "topics", "measured": "measured", "findings": "findings", "flags": "flags"}.get(v[1]["var"])
                    if key and v[0] not in out[key]:
                        out[key].append(v[0])
                elif k == "==" and isinstance(v, list) and isinstance(v[0], dict) and v[0].get("var") == "sepsis_tier":
                    out["sepsis"] = True
                elif k == "==" and isinstance(v, list) and isinstance(v[0], dict) and v[0].get("var") == "allergies_known":
                    out["allergies"] = True
                walk(v)
        elif isinstance(rule, list):
            for x in rule:
                walk(x)
    for cs, _, rule, _ in bd.RULES:
        if cs == case_id:
            walk(rule)
    return out


def evidence(s: Script, c: dict) -> None:
    """Plays what finds the case's problems, in an order a clinician would."""
    n = needs(c["id"])
    if n["allergies"]:
        s.add("ask_allergies")
    for t in n["topics"]:
        s.add("ask", topic=t)
    if "fasting.breach" in n["flags"]:
        s.add("ask", topic="last_food").add("ask", topic="last_drink")
    if n["sepsis"] or n["measured"]:
        readings(s)
        s.wait(40)
    tests = [t for t in investigations() if t["id"] in n["tests"]]
    if "pregnancy" in [t["id"] for t in tests] and "pregnancy" not in n["topics"]:
        s.add("ask", topic="pregnancy")
    order_tests(s, tests)
    for t in tests:
        if t["kind"] == "imaging":
            scan(s, t["id"])
    if n["findings"]:
        examination(s, c)
    s.skip(60)
    view_tests(s, tests)


def scan(s: Script, test: str) -> None:
    s.add("order_investigation", 2, test=test)
    s.add("request_transport", 4, to=test, mode="trolley", escort="porter,nurse", monitoring="none", accompany="false", oxygen="false")


def probe(case_id: str, s: Script) -> str:
    """The outcome the decision engine expects now, read from its answer to a wrong declaration in practice mode."""
    p = Script()
    p.rows = list(s.rows)
    p.t = s.t
    p.add("declare_outcome", outcome="SAFE")
    with tempfile.TemporaryDirectory() as tmp:
        script = Path(tmp) / "probe.actions.json"
        script.write_text(p.json(), encoding="utf-8")
        subprocess.run(CLI + ["run", "--case", case_id, "--seed", "42", "--actions", str(script), "--out", str(Path(tmp) / "o.json"), "--allow-draft"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
        for line in (Path(tmp) / "o.session.jsonl").read_text(encoding="utf-8").splitlines()[1:]:
            e = json.loads(line)
            for fx in e["effects"]:
                if fx.startswith("expected:"):
                    return fx.split(":", 1)[1]
    raise SystemExit(f"{case_id}: the engine did not say what it expects")


SERVICE = {"STK-S1-C": "acute medical team", "VAR-S1-E": "acute surgical team", "STK-S1-D": "acute surgical and medical team", "VAR-S2-B": "gynaecology", "VAR-S2-D": "senior surgical review and further imaging"}


def decided(c: dict) -> Script:
    """Finds the problems, declares the outcome the engine expects, then postpones or redirects."""
    s = Script()
    identity(s)
    evidence(s, c)
    s.add("declare_outcome", 4, outcome=probe(c["id"], s), reason=c["problems"][0]["label"][:80])
    if c["expected_end_state"] in ("postponed", "delayed"):
        s.add("postpone", 4, reason=c["problems"][0]["label"][:80] if c["expected_end_state"] == "postponed" else "move to the end of the list until the fasting time is met")
    else:
        s.add("redirect", 4, service=SERVICE[c["id"]])
    s.add("wait", 1)
    return s


BUILDERS = {
    "VAR-S1-G": decided, "VAR-S1-H": decided, "VAR-S1-PB": decided, "VAR-S1-E": decided, "STK-S1-D": decided, "VAR-S2-B": decided, "VAR-S2-D": decided,
    # the P1 cases (p1-case-library-extended-s03, EXP-CASE-3): the same segments play all sixteen to their expected end state with nothing refused, so no engine addition is needed for them
    "VAR-S1-I": planned_ready, "VAR-S1-M": planned_ready, "VAR-S1-K": decided, "VAR-S1-L": decided, "VAR-S2-E": decided,
    "VAR-S2-H": with_options(antibiotic=True), "VAR-S2-I": with_options(antibiotic=True), "VAR-S2-J": with_options(antibiotic=True), "VAR-S2-K": with_options(antibiotic=True), "VAR-S2-L": with_options(antibiotic=True),
    "STK-S1-A": decided, "STK-S1-B": decided, "STK-S1-C": decided, "STK-S2-A": with_options(antibiotic=True), "STK-S2-C": with_options(emergency=True, antibiotic=True), "STK-S2-D": with_options(antibiotic=True),
    "VAR-S1-A": planned_ready,
    "VAR-S1-B": planned_ready,
    "VAR-S1-C": planned_ready,
    "VAR-S1-D": planned_ready,
    "VAR-S1-J": with_options(repeat=["u_and_e"]),
    "VAR-S2-F": with_options(antibiotic=True), "VAR-S2-G": with_options(antibiotic=True), "VAR-S2-PC": with_options(emergency=True, antibiotic=True), "VAR-S2-PD": with_options(antibiotic=True), "STK-S2-B": with_options(emergency=True),
}


def write() -> None:
    for case_id, build in BUILDERS.items():
        (GOLDEN / f"{stem(case_id)}.actions.json").write_text(build(case(case_id)).json(), encoding="utf-8", newline="\n")
        stub = GOLDEN / f"{stem(case_id)}.expected.json"
        if not stub.exists():
            stub.write_text(json.dumps({"case": case_id, "seed": "42", "mode": "practice"}, indent=2) + "\n", encoding="utf-8", newline="\n")      # `procsense-sim golden --update` fills it in
        print(f"wrote tests/golden/{stem(case_id)}.actions.json")


def stale() -> int:
    """The scripts in tests/golden must be exactly what the generator writes today (so a change to the generator or to a case file is noticed)."""
    old = [c for c in BUILDERS if (GOLDEN / f"{stem(c)}.actions.json").read_text(encoding="utf-8") != BUILDERS[c](case(c)).json()]
    missing = [c for c in BUILDERS if not (GOLDEN / f"{stem(c)}.expected.json").exists()]
    if old or missing:
        print("FAIL  out of date: " + ", ".join(old + missing) + "; run tools/golden/p0_scripts.py --write and then procsense-sim golden --update")
        return 1
    print(f"the {len(BUILDERS)} correct-path scripts (P0 and P1 cases) are what the generator writes today")
    return 0


def check() -> int:
    bad = 0
    for case_id in BUILDERS:
        c = case(case_id)
        want = END_CODE[c["expected_end_state"]]
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "run.json"
            p = subprocess.run(CLI + ["run", "--case", case_id, "--seed", "42", "--actions", str(GOLDEN / f"{stem(case_id)}.actions.json"), "--out", str(out), "--allow-draft"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
            if p.returncode != 0:
                print(f"FAIL  {case_id}: {(p.stdout + p.stderr).strip()[-300:]}")
                bad += 1
                continue
            log = [json.loads(l) for l in (Path(tmp) / "run.session.jsonl").read_text(encoding="utf-8").splitlines()[1:]]
            refused = [(e["verb"], e["reject"], e["params"], e["effects"][:3]) for e in log if not e["accepted"]]
            rep = subprocess.run(CLI + ["report", "--log", str(Path(tmp) / "run.session.jsonl"), "--out", str(Path(tmp) / "r.html"), "--json", str(Path(tmp) / "r.json"), "--skip-integrity"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
            facts = json.loads((Path(tmp) / "r.json").read_text(encoding="utf-8")) if rep.returncode == 0 else {}
        got = facts.get("end_state", "?")
        ok = not refused and got == want
        print(("ok    " if ok else "FAIL  ") + f"{case_id}: end state {got} (want {want}); {len(refused)} refused" + ("" if not refused else " " + str(refused[:4])))
        bad += 0 if ok else 1
    return 1 if bad else 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if "--write" in sys.argv:
        write()
    if "--stale" in sys.argv:
        sys.exit(stale())
    sys.exit(check() if "--check" in sys.argv else 0)
