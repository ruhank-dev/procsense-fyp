"""Builds content/events/events.json (the approved events the Event Director fires) and docs/research/whatif_coverage.md (how each of the 47 walkthrough what-ifs is covered).

    uv run --project tools tools/content/build_events.py            # write
    uv run --project tools tools/content/build_events.py --check    # fail if the files are out of date

Sources: walkthrough Part 14 (the what-if table in content/whatif_library.csv, 47 rows), the Implementation Guide B-R14 (the event shape, concurrency of one clinical and one other event, a 90 s global cooldown,
a 3 minute warm-up) and the thresholds of the verified vital-sign table (SR-0100 onward). The response windows (300 s to call for help, 600 s to ask for adrenaline), the cooldowns and the warm-up are design values (SR-2863).
Only what-ifs that can be judged from facts the engine already tracks are events; every other what-if is either already handled by a named subsystem and test, or is listed as data only. Nothing is faked.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "events" / "events.json"
COVERAGE = ROOT / "docs" / "research" / "whatif_coverage.md"
REF = "SR-2863"
APPROVAL = "APPROVED_FOR_TRAINING"       # the owner's attestation covers training use; assessment use awaits the clinician (docs/research/clinician_signoff.md Q7)


def consequence(items=(), flag="", effects=()):
    return {"items": list(items), "flag": flag, "physio_effects": list(effects)}


def var_rule(op, name, value):
    return {op: [{"var": "vitals." + name}, value]}


def recognition(id, whatif, label, watch, ref, escalate_within=300):
    """A patient whose own numbers cross a book threshold: the event watches that the trainee calls for help in time (no new cause is invented)."""
    return {
        "id": id, "whatif": whatif, "category": "clinical", "kind": "caused", "status": APPROVAL, "trigger": var_rule(watch[1], watch[0], watch[2]), "eligibility": True, "needs_flag": "", "watch": {"vital": watch[0], "op": watch[1], "value": watch[2]}, "base_probability": 1.0,
        "severity": "high", "difficulty_tier": "standard", "cooldown_seconds": 0, "cues": [], "physio_effects": [], "set_flags": [],
        "expected_responses": [{"id": "escalate", "verb": "escalate", "params": {}, "within_seconds": escalate_within, "label": label}],
        "unacceptable": [],
        "consequences": {"handled": consequence(["credit." + id.lower().replace("-", "_") + "_escalated"], "event." + id.lower() + ".handled"),
                         "missed": consequence(["major." + id.lower().replace("-", "_") + "_not_escalated"], "event." + id.lower() + ".missed"), "wrong": consequence()},
        "communication_intents": [], "sources": [ref, REF],
    }


EVENTS = [
    {
        "id": "E-TEST-DECLINED", "whatif": "w27", "category": "clinical", "kind": "caused", "status": APPROVAL,
        "trigger": {"in": ["patient.declined_test", {"var": "flags"}]}, "eligibility": True, "needs_flag": "patient.declined_test", "watch": {"vital": "", "op": "", "value": 0}, "base_probability": 1.0,
        "severity": "high", "difficulty_tier": "standard", "cooldown_seconds": 0,
        "cues": ["I don't want a test"], "physio_effects": [], "set_flags": ["event.test_declined"],
        "expected_responses": [{"id": "escalate", "verb": "escalate", "params": {}, "within_seconds": 600, "label": "Respect the decision, document it, tell the senior and the anaesthetist, and treat her as possibly pregnant"}],
        "unacceptable": [],
        "consequences": {"handled": consequence(["credit.e_test_declined_escalated"], "event.e-test-declined.handled"),
                         "missed": consequence(["major.e_test_declined_not_escalated"], "event.e-test-declined.missed"), "wrong": consequence()},
        "communication_intents": ["patient_distress"], "sources": ["SR-2226", REF],
    },
    {
        "id": "E-HYPERSENSITIVITY", "whatif": "w29", "category": "clinical", "kind": "caused", "status": APPROVAL,
        "trigger": {"in": ["allergy.reaction_eligible", {"var": "flags"}]}, "eligibility": True, "needs_flag": "allergy.reaction_eligible", "watch": {"vital": "", "op": "", "value": 0}, "base_probability": 1.0,
        "severity": "critical", "difficulty_tier": "advanced", "cooldown_seconds": 0,
        "cues": ["rash and flushing", "swollen lips", "wheeze"], "physio_effects": ["EFF-ANAPHYLAXIS"], "set_flags": ["event.hypersensitivity"],
        "expected_responses": [
            {"id": "escalate", "verb": "escalate", "params": {}, "within_seconds": 300, "label": "Call for help and the emergency team"},
            {"id": "adrenaline", "verb": "request_medication", "params": {"treatment": "TR-ADRENALINE"}, "within_seconds": 600, "label": "Ask the clinician for intramuscular adrenaline"}],
        "unacceptable": [],
        "consequences": {"handled": consequence(["credit.reaction_handled"], "event.hypersensitivity.handled"),
                         "missed": consequence(["critical.reaction_not_treated"], "event.hypersensitivity.missed"), "wrong": consequence()},
        "communication_intents": ["patient_distress"], "sources": ["SR-0973", "SR-4108", "SR-2860", REF],
    },
    recognition("E-LOW-BP", "w01", "Call the senior surgeon and the anaesthetist", ("sbp", "<=", 90), "SR-0109"),
    recognition("E-HIGH-HR", "w03", "Call the senior and the anaesthetist", ("hr", ">", 130), "SR-0104"),
    recognition("E-LOW-HR", "w04", "Call the senior, the anaesthetist and the medical team", ("hr", "<=", 40), "SR-0105"),
    recognition("E-LOW-OXYGEN", "w07", "Call the anaesthetist or senior urgently", ("spo2", "<=", 91), "SR-0124"),
    recognition("E-FAST-BREATHING", "w08", "Call the senior urgently", ("rr", ">=", 25), "SR-0119"),
    recognition("E-FEVER", "w05", "Tell the senior: a fever may change the pathway (DT-087)", ("temp", ">=", 38.0), "SR-2204"),
    recognition("E-COLD", "w06", "Tell the anaesthetist: warm the patient before theatre (DT-087)", ("temp", "<=", 36.0), "SR-2205"),
    recognition("E-PAIN-WORSE", "w11", "Call the senior immediately: pain this bad despite analgesia may be a perforation", ("pain", ">=", 9), "SR-2210"),
    recognition("E-SLOW-BREATHING", "w09", "Call the emergency team and the anaesthetist immediately", ("rr", "<=", 8), "SR-0120"),
]

# how every what-if is covered: ("event", event id) | ("engine", test file, what) | ("data", why there is no runtime)
COVER = {
    "w01": ("event", "E-LOW-BP"), "w03": ("event", "E-HIGH-HR"), "w04": ("event", "E-LOW-HR"), "w07": ("event", "E-LOW-OXYGEN"), "w08": ("event", "E-FAST-BREATHING"), "w09": ("event", "E-SLOW-BREATHING"), "w05": ("event", "E-FEVER"), "w06": ("event", "E-COLD"), "w11": ("event", "E-PAIN-WORSE"), "w27": ("event", "E-TEST-DECLINED"),
    "w35": ("engine", "ImagingTests.cs", "calling 'appendix not visualised' normal is the classic error and a major item (not_visualised_taken_as_negative)"),
    "w44": ("engine", "OrReadinessTests.cs", "an equipment fault is found when the station is checked; ignored, it is a major item and the theatre is not ready; reported, a replacement comes after the delay"),
    "w45": ("engine", "WardChecklistTests.cs", "the ward checklist, with its consent and documents lines, cannot be signed, so the transfer does not go, until every line is ticked and true"),
    "w29": ("event", "E-HYPERSENSITIVITY"),
    "w02": ("engine", "DecisionEngineTests.cs", "a clinic pressure of 180/110 or more on repeat postpones a planned case (decision engine)"),
    "w10": ("engine", "BehaviourTests.cs", "low oxygen or low sugar sets the confusion flag the behaviour model reads"),
    "w12": ("engine", "CriticalCallTests.cs", "critical potassium is phoned through"),
    "w13": ("engine", "CriticalCallTests.cs", "critical potassium is phoned through"),
    "w14": ("engine", "ResultGeneratorTests.cs", "abnormal sodium is flagged by the result bands"),
    "w15": ("engine", "ResultGeneratorTests.cs", "low haemoglobin is flagged by the result bands"),
    "w16": ("engine", "ResultGeneratorTests.cs", "white cell count flagged by the result bands"),
    "w17": ("engine", "ResultGeneratorTests.cs", "white cell count flagged by the result bands"),
    "w18": ("engine", "ResultGeneratorTests.cs", "creatinine flagged by the result bands"),
    "w19": ("engine", "ResultGeneratorTests.cs", "liver tests flagged by the result bands"),
    "w20": ("engine", "ResultGeneratorTests.cs", "bilirubin flagged by the result bands"),
    "w21": ("engine", "CriticalCallTests.cs", "critical glucose is phoned through"),
    "w22": ("engine", "ResultGeneratorTests.cs", "high glucose flagged by the result bands"),
    "w23": ("engine", "ResultGeneratorTests.cs", "high INR flagged by the result bands"),
    "w24": ("engine", "CriticalCallTests.cs", "critical lactate is phoned through"),
    "w25": ("engine", "InvestigationsTests.cs", "ECG tests give an approved report"),
    "w26": ("engine", "DecisionEngineTests.cs", "a positive pregnancy test changes the pathway"),
    "w28": ("engine", "MedicineWorkflowTests.cs", "an antibiotic against a known allergy is challenged or reacts"),
    "w30": ("engine", "ClashTests.cs", "a blood thinner raises a treatment clash"),
    "w31": ("engine", "ClashTests.cs", "diabetes raises the medicine plan clash"),
    "w32": ("engine", "ClashTests.cs", "steroids, GLP-1 and SGLT2 medicines raise plan clashes"),
    "w33": ("engine", "FastingTests.cs", "the fasting rule judges a breach"),
    "w34": ("engine", "FastingTests.cs", "unknown fasting stays unknown until asked"),
    "w36": ("engine", "ResultGeneratorTests.cs", "implausible and poor-quality results are flagged"),
    "w37": ("engine", "InvestigationsTests.cs", "results come after their turnaround time"),
    "w38": ("engine", "CriticalCallTests.cs", "the lab phones a critical result"),
    "w39": ("engine", "DeteriorationTests.cs", "the deterioration clock and the Usman timeline"),
    "w40": ("engine", "SepsisTests.cs", "the sepsis tiers and timers"),
    "w41": ("engine", "ConsentTests.cs", "refusal stops the case route"),
    "w42": ("engine", "ConsentTests.cs", "a patient who does not understand cannot sign"),
    "w43": ("engine", "ConsentTests.cs", "a minor needs a senior"),
    "w46": ("engine", "TransportTests.cs", "the corridor oxygen drop"),
    "w47": ("engine", "WristbandTests.cs", "a wristband mismatch is found and resolved"),
}


def whatifs() -> list[dict]:
    with open(ROOT / "content" / "whatif_library.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def build() -> tuple[str, str]:
    rows = whatifs()
    ids = [r["id"] for r in rows]
    if sorted(COVER) != sorted(ids) or len(ids) != 47:
        raise SystemExit(f"coverage must name exactly the 47 what-ifs: missing {sorted(set(ids) - set(COVER))}, extra {sorted(set(COVER) - set(ids))}")
    event_ids = {e["id"] for e in EVENTS}
    for w, c in COVER.items():
        if c[0] == "event" and c[1] not in event_ids:
            raise SystemExit(f"{w} names the event {c[1]}, which is not defined")
        if c[0] == "engine" and not (ROOT / "tests" / "ProcSense.Core.Tests" / c[1]).exists():
            raise SystemExit(f"{w} names the test file {c[1]}, which does not exist")
    doc = {"evaluate_every_steps": 20, "warmup_seconds": 180, "global_cooldown_seconds": 90, "max_clinical": 1, "max_other": 1, "events": EVENTS, "modifiers": [], "refs": [REF]}
    events_text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"

    title = {r["id"]: r["title"].replace("�", "°") for r in rows}
    lines = ["# What-if coverage: how each of the 47 walkthrough what-ifs is handled", "",
             "Generated by `tools/content/build_events.py` (do not edit by hand; `--check` fails if it is stale). Source: walkthrough Part 14 (`content/whatif_library.csv`).", "",
             "- **event**: the Event Director watches it (`content/events/events.json`).",
             "- **engine**: an existing subsystem already handles it and a named unit-test file covers it.",
             "- **data only**: the walkthrough row exists as data but the engine has no state or verb for it yet; it is not faked (see DT-087).", "",
             "| What-if | Title | How | Detail |", "|---|---|---|---|"]
    counts = {"event": 0, "engine": 0, "data": 0}
    for w in ids:
        c = COVER[w]
        counts[c[0]] += 1
        if c[0] == "event":
            lines.append(f"| {w} | {title[w]} | event | {c[1]} |")
        elif c[0] == "engine":
            lines.append(f"| {w} | {title[w]} | engine | {c[2]} (`{c[1]}`) |")
        else:
            lines.append(f"| {w} | {title[w]} | data only | {c[1]} |")
    lines += ["", f"Totals: {counts['event']} events, {counts['engine']} handled by existing subsystems, {counts['data']} data only; {sum(counts.values())} in all.", ""]
    return events_text, "\n".join(lines)


def main(argv: list[str]) -> int:
    events_text, cov = build()
    files = {OUT: events_text, COVERAGE: cov}
    if "--check" in argv:
        stale = [str(p.relative_to(ROOT)) for p, t in files.items() if not p.exists() or p.read_text(encoding="utf-8").replace("\r\n", "\n") != t]
        if stale:
            print(f"FAIL  {', '.join(stale)} out of date; run tools/content/build_events.py")
            return 1
        print("content/events and the what-if coverage are up to date")
        return 0
    for p, t in files.items():
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
