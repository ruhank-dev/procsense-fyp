"""End-to-end check of the voice path on synthetic data only (ADR-0026, ADR-0027): `uv run --project server --extra asr-gpu python tools/ai/e2e_synthetic.py [--questions 8] [--asr-device cuda] [--out reports/ai/e2e-synthetic.json]`.

For each synthetic trainee question: the question is spoken by the local voice (Piper, a synthetic voice standing in for a recording; no real person's voice), recognised by the local recogniser (faster-whisper),
read by the spoken-action interpreter (which tablet action was asked for), answered by the patient and the staff through the production dialogue code (the facts the mapped action releases, the gates of ADR-0026), and
the replies are spoken again by the local voice. The report holds, per stage, the model that ran and whether it was a cloud model, the time, and the totals, for four configurations: cloud routing off; the institutional
approval recorded but the person has not consented; approval and the person's consent; and a deployment declared synthetic. Nothing real is used: the case is invented, the voice is synthetic, the text sent to a
cloud model is invented text. The engine's state change for a spoken action is checked in Unity (PlayMode SpokenActionTests: the same state and log as the tablet button), not here. Needs Ollama (signed in for the
cloud runs), faster-whisper, Piper and ffmpeg.
"""
from __future__ import annotations

import argparse
import json
import os
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "tools" / "ai"))

QUESTIONS = [
    ("Do you have any red flag symptoms like fever or yellow eyes?", "ask:red_flags"),
    ("Can you tell me what medicines you take?", "ask:medicines"),
    ("When did you last have something to eat?", "ask:last_food"),
    ("Have you had an anaesthetic before?", "ask:previous_anaesthetic"),
    ("Do you smoke?", "ask:smoking"),
    ("Nurse, please check the pulse and tell me what it is.", None),
    ("Hello, I am the doctor and I am going to look after you today.", "introduce_self"),
    ("I am worried, the saturation is dropping, I need the anaesthetist here now.", None),
]


def pct(values, q):
    v = sorted(values)
    return v[min(len(v) - 1, int(q * len(v)))]


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--questions", type=int, default=8)
    ap.add_argument("--case", default="VAR-S1-A")
    ap.add_argument("--asr-device", default="auto", help="auto, cuda (needs the asr-gpu extra on Windows) or cpu (about 8 s a clip instead of 0.7 s; reports/ai/asr-device-bench.json)")
    ap.add_argument("--out", default="")
    a = ap.parse_args(argv)
    from asr.service import AsrService
    from composer import blocks as B
    from dialogue import dialogue as D
    from llm import client as L, routing
    from speech_action import service as S
    from tts.engine import TtsEngine
    asr, tts = AsrService("small.en", a.asr_device, "int8_float16" if a.asr_device == "cuda" else "int8"), TtsEngine()
    if not tts.available():
        raise SystemExit("the local voice (Piper) or ffmpeg is not available")
    intents = json.loads((ROOT / "content" / "ai" / "intents.json").read_text(encoding="utf-8"))["roles"]
    lines = json.loads((ROOT / "content" / "ai" / "staff_intent_lines.json").read_text(encoding="utf-8"))["roles"]
    case = next(c for c in B.load_cases() if c["id"] == a.case)
    authored = {"h." + h["topic"]: h["answer"] for h in case["history"] if h.get("variant") is None}
    catalogue = S.catalogue()
    client = L.OllamaClient()
    off = {"CLOUD_ROUTING_ENABLED": "0", "CLOUD_SYNTHETIC_DEPLOYMENT": "0", "CLOUD_TRAINEE_DATA_APPROVED": "0"}
    configs = [("cloud routing off", {**off}, False),
               ("institutional approval recorded, this person has NOT consented", {**off, "CLOUD_ROUTING_ENABLED": "1", "CLOUD_TRAINEE_DATA_APPROVED": "1"}, False),
               ("institutional approval recorded, this person has consented", {**off, "CLOUD_ROUTING_ENABLED": "1", "CLOUD_TRAINEE_DATA_APPROVED": "1"}, True),
               ("deployment declared synthetic (development, demonstrations)", {**off, "CLOUD_ROUTING_ENABLED": "1", "CLOUD_SYNTHETIC_DEPLOYMENT": "1"}, False)]
    report = {"adr": "ADR-0027", "asr_device": a.asr_device, "data": "synthetic only: an invented case, a synthetic voice, invented text", "case": a.case, "configurations": []}
    for label, env, consenting in configs:
        os.environ.update(env)
        routing.reload()
        d, sa = D.Dialogue(client), S.SpeechActions(client)
        runs = []
        for question, expected in QUESTIONS[: a.questions]:
            with routing.consent(consenting):
                t0 = time.monotonic()
                spoken = b"".join(audio for _s, audio in tts.speak(question, "clinician" if "clinician" in tts.roles() else tts.roles()[0]))
                t_voice_in = time.monotonic()
                heard = asr.transcribe(spoken)
                t_asr = time.monotonic()
                act = sa.interpret(list(catalogue), [], heard.text or question)
                t_act = time.monotonic()
                mapped = [catalogue[i] for i in act.actions]
                topic = next((m["value"] for m in mapped if m.get("key") == "topic"), None)
                facts = []
                for h in case["history"]:
                    if h.get("variant") is not None:
                        continue
                    facts.append({"id": "h." + h["topic"], "value": h["answer"], "disclosed": False, "disclosable_now": bool(h["volunteered"]) or h["topic"] == topic})
                ctx = {"role": "patient", "case": case["id"], "allowed_intents": intents["patient"], "facts": facts, "last_turns": [], "results": []}
                staff = {r: {"role": r, "case": case["id"], "allowed_intents": intents[r], "facts": [], "last_turns": [], "results": []} for r in ("nurse", "anaesthesia")}
                replies = d.team(ctx, staff, heard.text or question, age=case["identity"]["age"], sex=case["identity"]["sex"], authored=authored, staff_lines={r: lines[r] for r in staff}, asked_fact="h." + topic if topic else None)
                t_dialogue = time.monotonic()
                first_audio, audio_bytes = None, 0
                voices = {"patient": "patient_female" if case["identity"]["sex"] == "F" else "patient_male", "nurse": "nurse", "anaesthesia": "anaesthetist"}
                for role in [r for r in ("patient", "nurse", "anaesthesia") if r in replies]:
                    for _s, audio in tts.speak(replies[role].text, voices[role]):
                        audio_bytes += len(audio)
                        if first_audio is None:
                            first_audio = time.monotonic()
                t_end = time.monotonic()
            audit, act_audit = replies["patient"].audit or {}, act.audit or {}
            runs.append({"question": question, "heard": heard.text, "heard_ok": bool(heard.ok and not heard.low_confidence),
                         "expected_action": expected, "actions": act.actions, "action_ok": (act.actions == [expected]) if expected else (not act.actions or act.source == "fallback"),
                         "action_model": act_audit.get("model", act.source), "action_cloud": bool(act_audit.get("cloud")), "addressee": act.addressee,
                         "patient": replies["patient"].text, "patient_source": replies["patient"].source, "patient_intent": replies["patient"].intent,
                         "staff": {r: {"intent": replies[r].intent, "approved_line": replies[r].text in lines[r].values()} for r in staff if r in replies}, "silent_staff": [r for r in staff if r not in replies],
                         "model": audit.get("model", replies["patient"].source), "cloud": bool(audit.get("cloud")), "route": audit.get("route", ""), "fell_back_from": audit.get("fell_back_from", ""),
                         "ms": {"recognition": round((t_asr - t_voice_in) * 1000), "action": round((t_act - t_asr) * 1000), "dialogue": round((t_dialogue - t_act) * 1000),
                                "heard_to_first_voice": round((first_audio - t_asr) * 1000) if first_audio else None, "voices_ready": round((t_end - t_dialogue) * 1000)},
                         "audio_bytes": audio_bytes})
        a_ms = [r["ms"]["action"] + r["ms"]["dialogue"] for r in runs]
        e_ms = [r["ms"]["heard_to_first_voice"] for r in runs if r["ms"]["heard_to_first_voice"]]
        r_ms = [r["ms"]["recognition"] for r in runs]
        report["configurations"].append({"label": label, "env": env, "person_consented": consenting, "questions": len(runs), "cloud_calls": sum(r["cloud"] for r in runs) + sum(r["action_cloud"] for r in runs),
                                         "models": sorted({r["model"] for r in runs} | {r["action_model"] for r in runs}), "recognised": sum(r["heard_ok"] for r in runs), "action_correct": sum(r["action_ok"] for r in runs),
                                         "recognition_ms": {"p50": round(statistics.median(r_ms)), "p95": round(pct(r_ms, 0.95))},
                                         "action_plus_dialogue_ms": {"p50": round(statistics.median(a_ms)), "p95": round(pct(a_ms, 0.95))},
                                         "heard_to_first_voice_ms": {"p50": round(statistics.median(e_ms)), "p95": round(pct(e_ms, 0.95))} if e_ms else None,
                                         "patient_from_model": sum(r["patient_source"] == "model" for r in runs), "staff_lines_all_approved": all(s["approved_line"] for r in runs for s in r["staff"].values()), "runs": runs})
        c = report["configurations"][-1]
        print(f"{label} | models {c['models']} | cloud calls {c['cloud_calls']} | recognised {c['recognised']}/{len(runs)} | action right {c['action_correct']}/{len(runs)} | "
              f"recognition p50 {c['recognition_ms']['p50']} ms | action+dialogue p50/p95 {c['action_plus_dialogue_ms']['p50']}/{c['action_plus_dialogue_ms']['p95']} ms | heard->first voice p50 {c['heard_to_first_voice_ms'] and c['heard_to_first_voice_ms']['p50']} ms")
    if a.out:
        Path(a.out).write_text(json.dumps(report, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", a.out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
