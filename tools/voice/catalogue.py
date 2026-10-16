"""Builds content/voice/catalogue.json: every spoken line of the P0 cases with an id, the speaker, the words, the Core event that plays it and its content source (p0-voice-audio-s01).

    uv run --project tools tools/voice/catalogue.py            # write (task voice-catalogue)
    uv run --project tools tools/voice/catalogue.py --check    # fail if out of date or a line has no source; print the events with no line and the drafts awaiting review

Nothing is typed twice: the lines come from the content only.
- the patient: per case, the answers to the identity, allergy, allergy reaction, reason for admission and responsible-adult questions and to the 12 history topics (the case card when there
  is one, else the case file, as the engine does: ProcSense.Core.Workflow.PatientAnswers);
- the staff: the anaesthetist's approved intents (content/npc/anaesthetist_intents.json), the clinician's and nurse's medicine lines (content/treatments.json), the nurse's prompt about tubes
  at the bedside (content/diagnostics/diagnostics.json), the porter's delay message (content/locations/locations.json), and the team's answers to the WHO checklist items they answer;
- content/voice/nurse_wristband.json: the nurse's lines during a wristband mismatch or a missing band (p0-voice-audio-s06), authored, procedural only;
- content/voice/spoken_variants.json: first-person variants drafted by the developer where the content holds a narration ("Not applicable.") or no answer text at all; each is marked
  awaiting_review and keeps its source text.
A line with {placeholders} is a template: it is rendered per value (offline) or by live speech later (p1-voice-input). An event of a P0 case with no line is a warning (plan test T-VP1).
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "voice" / "catalogue.json"
VARIANTS = CONTENT / "voice" / "spoken_variants.json"
PLACEHOLDER = re.compile(r"\{([a-z_]+)\}")
NARRATION = re.compile(r"^(Not applicable|Not stated|Her |His |She |He )")

# who answers each WHO checklist item the team answers, from its own prompt; Step 15 (sign out) is optional and P1
CHECKLIST_SPEAKER = {"sign_in.03": "anaesthetist", "time_out.01": "team", "time_out.04": "surgeon", "time_out.05": "anaesthetist", "time_out.06": "nurse"}
MEDICINE_SPEAKER = {"confirm": "clinician", "confirm_allergy_caution": "clinician"}


def p0_cases() -> list[str]:
    return sorted(p.name[: -len("_correct.actions.json")] for p in (ROOT / "tests" / "golden").glob("*_correct.actions.json") if "-" in p.name and p.name[0].isupper())


def load(rel: str) -> dict:
    return json.loads((CONTENT / rel).read_text(encoding="utf-8"))


def patient_lines(case: str) -> list[dict]:
    card_path = CONTENT / "cases" / f"{case}.json"
    src = load(f"cases/{case}.json") if card_path.exists() else load(f"case_files/{case}.json")
    where = f"content/cases/{case}.json" if card_path.exists() else f"content/case_files/{case}.json"
    lines = []

    def add(topic: str, text: str | None, event: dict):
        lines.append({"id": f"PAT-{case}-{topic.upper()}", "speaker": "patient", "case": case, "text": text or "", "event": event, "source": where})

    add("identity", src.get("spoken_identity_answer"), {"verb": "ask_identity"})
    add("allergies", src.get("allergy_answer"), {"verb": "ask_allergies"})
    if src.get("allergies"):
        add("allergy_reaction", "; ".join(f"{a['substance']}: {a['reaction']}" for a in src["allergies"]), {"verb": "ask_allergy_reaction"})
    add("reason_for_admission", src.get("reason_answer"), {"verb": "ask_reason_for_admission"})
    ra = src.get("responsible_adult")
    add("responsible_adult", ra["text"] if ra else None, {"verb": "ask_responsible_adult"})
    for h in src.get("history", []):
        add(h["topic"], h["answer"], {"verb": "ask", "topic": h["topic"]})
    return lines


def staff_lines() -> list[dict]:
    lines = []
    for i in load("npc/anaesthetist_intents.json")["intents"]:
        lines.append({"id": f"ANA-{i['id'].upper()}", "speaker": "anaesthetist", "text": i["line"], "event": {"anaesthetist_intent": i["id"]}, "source": f"content/npc/anaesthetist_intents.json ({i['ref']})"})
    t = load("treatments.json")
    for l in t["lines"]:
        lines.append({"id": f"MED-{l['id'].upper()}", "speaker": MEDICINE_SPEAKER.get(l["id"], "nurse"), "text": l["text"], "event": {"medicine_line": l["id"]}, "source": "content/treatments.json (SR-4109)"})
    d = load("diagnostics/diagnostics.json")["waiting_prompt"]
    lines.append({"id": "NUR-SAMPLES_WAITING", "speaker": "nurse", "text": d["text"], "event": {"core_event": "SamplesWaiting"}, "source": f"content/diagnostics/diagnostics.json ({d['ref']})"})
    loc = load("locations/locations.json")
    msg = loc["transport"].get("messages", {}).get("porter_unavailable", "")
    lines.append({"id": "POR-UNAVAILABLE", "speaker": "porter", "text": msg, "event": {"transport_state": "DELAYED"}, "source": "content/locations/locations.json"})
    # the laboratory's critical-result call is put together in the engine's code from the case (ResultReviewSubsystem), not in the content: no line until it moves to content
    lines.append({"id": "LAB-CRITICAL_CALL", "speaker": "laboratory", "text": "", "event": {"core_event": "critical_call_rings"},
                  "source": "src/ProcSense.Core/Workflow/Steps/ResultReviewSubsystem.cs (the words are built in code, not content)"})
    for l in load("voice/nurse_wristband.json")["lines"]:
        lines.append({"id": l["id"], "speaker": "nurse", "text": l["text"], "event": l["trigger"], "source": "content/voice/nurse_wristband.json (authored, p0-voice-audio-s06)",
                      "spoken_variant": "authored", "review": l["status"]})
    items = {i["id"]: i for i in load("checklist/safety_checklist.json")["items"]}
    for item_id, speaker in CHECKLIST_SPEAKER.items():
        lines.append({"id": "NPC-" + item_id.upper().replace(".", "-"), "speaker": speaker, "text": "", "event": {"checklist_item": item_id},
                      "source": f"content/checklist/safety_checklist.json ({items[item_id]['ref']}): {items[item_id]['prompt']}"})
    return lines


def nurse_coverage(lines: list[dict]) -> list[str]:
    """T-VP1 for the wristband nurse (p0-voice-audio-s06): every nurse step the engine publishes, and every outcome the step lists, has a line. The steps are read from the engine's code, so a new one cannot be missed."""
    code = (ROOT / "src" / "ProcSense.Core" / "Workflow" / "Steps" / "WristbandResolution.cs").read_text(encoding="utf-8")
    states = re.findall(r'public const string \w+ = "([A-Z_]+)";', code)
    events = [l["event"] for l in lines if l["speaker"] == "nurse"]
    out = [f"nurse step {st}: no line" for st in states if not any(e.get("nurse_state") == st for e in events)]
    for need in ({"verb": "request_wristband", "accepted": True}, {"verb": "request_wristband", "rejection": "VERIFY_IDENTITY_FIRST"}, {"effect": "allergy_band_applied"}, {"effect": "nothing_to_resolve"},
                 {"nurse_state": "REPLACING", "effect": "wristband_applied"}):
        if not any(all(e.get(k) == v for k, v in need.items()) for e in events):
            out.append(f"nurse: no line for {json.dumps(need)}")
    return out


def build() -> tuple[str, list[str], list[str]]:
    variants = {v["id"]: v for v in json.loads(VARIANTS.read_text(encoding="utf-8"))["variants"]}
    lines = [l for c in p0_cases() for l in patient_lines(c)] + staff_lines()
    known = {l["id"] for l in lines}
    stray = sorted(set(variants) - known)
    missing, review = nurse_coverage(lines), []
    for l in lines:
        if l.get("spoken_variant") == "authored" and l["id"] not in variants and l.get("review") != "approved":
            review.append(f"{l['id']}: \"{l['text']}\"")
        v = variants.get(l["id"])
        if v:
            l["source_text"] = l["text"]
            l["text"] = v["text"]
            l["spoken_variant"] = "authored"
            l["review"] = v["status"]
            if v["status"] != "approved":
                review.append(f"{l['id']}: \"{v['text']}\"")
        elif NARRATION.match(l["text"]):
            missing.append(f"{l['id']}: the content holds a narration, not a spoken line: \"{l['text']}\"")
        if not l["text"]:
            missing.append(f"{l['id']}: no line ({l['source']})")
        names = PLACEHOLDER.findall(l["text"])
        if names:
            l["template"] = sorted(set(names))
    doc = {"note": "Generated by tools/voice/catalogue.py from the content; do not edit (task voice-catalogue).", "cases": p0_cases(), "lines": lines,
           "summary": {"lines": len(lines), "templates": sum(1 for l in lines if "template" in l), "authored_variants": len([l for l in lines if l.get("spoken_variant")]),
                       "events_without_a_line": len(missing)}}
    problems = [f"spoken_variants.json names {s}, which is not a line of the catalogue" for s in stray]
    problems += [f"{l['id']}: no source" for l in lines if not l.get("source")]
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n", problems, missing + ["awaiting review: " + r for r in review]


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    text, problems, notes = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            problems.append("content/voice/catalogue.json is out of date: run tools/voice/catalogue.py")
    else:
        if not problems:
            OUT.parent.mkdir(parents=True, exist_ok=True)
            OUT.write_text(text, encoding="utf-8", newline="\n")
    s = json.loads(text)["summary"]
    for n in notes:
        print("  note: " + n)
    for p in problems:
        print("  - " + p)
    print(f"voice catalogue: {s['lines']} lines for {len(p0_cases())} cases, {s['templates']} templates, {s['authored_variants']} authored variants, {s['events_without_a_line']} events without a line"
          + ("" if not problems else f"; {len(problems)} problems"))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
