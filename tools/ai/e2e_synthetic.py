"""End-to-end check of the voice path on synthetic data only (ADR-0026): `uv run --project server tools/ai/e2e_synthetic.py [--questions 6] [--out reports/ai/e2e-synthetic.json]`.

For each synthetic trainee question: the question is spoken by the local voice (Piper, a synthetic voice standing in for a recording; no real person's voice), turned into a clip, recognised by the local recogniser
(faster-whisper), answered by the patient and the staff through the production dialogue code and the routes (the engine's own context for an invented case, the gates of ADR-0026), and the replies are spoken again by
the local voice. The report holds, per stage, the model that ran, whether it was a cloud model, the time, and the totals (median and 95th percentile), for three configurations: cloud routing off, on but the privacy
gate closed (a real deployment), and on with the deployment declared synthetic. Nothing real is used: the case is an invented one, the voice is synthetic, and the text sent to a cloud model is invented text.
Needs Ollama (signed in for the cloud run), faster-whisper (`uv sync --project server --extra asr`), Piper and ffmpeg. This tool changes nothing in the repository except the report it is asked to write.
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
    ("Do you have any red flag symptoms like fever or yellow eyes?", "red_flags"),
    ("Can you tell me what medicines you take?", "medicines"),
    ("When did you last have something to eat?", "last_food"),
    ("Have you had an anaesthetic before?", "previous_anaesthetic"),
    ("Do you smoke?", "smoking"),
    ("Nurse, please check the pulse and tell me what it is.", None),
    ("Hello, I am the doctor and I am going to look after you today.", None),
    ("I am worried, the saturation is dropping, I need the anaesthetist here now.", None),
]


def pct(values, q):
    v = sorted(values)
    return v[min(len(v) - 1, int(q * len(v)))]


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--questions", type=int, default=6)
    ap.add_argument("--case", default="VAR-S1-A")
    ap.add_argument("--asr-device", default="auto", help="auto, cuda (needs the asr-gpu extra on Windows) or cpu (about 5 s a clip instead of 0.6 s; reports/ai/asr-wer.json)")
    ap.add_argument("--out", default="")
    a = ap.parse_args(argv)
    from asr.service import AsrService
    from composer import blocks as B
    from dialogue import dialogue as D
    from llm import client as L, routing
    from tts.engine import TtsEngine
    import eval_dialogue as E
    asr, tts = AsrService("small.en", a.asr_device, "int8_float16" if a.asr_device == "cuda" else "int8"), TtsEngine()
    if not tts.available():
        raise SystemExit("the local voice (Piper) or ffmpeg is not available")
    intents = json.loads((ROOT / "content" / "ai" / "intents.json").read_text(encoding="utf-8"))["roles"]
    lines = json.loads((ROOT / "content" / "ai" / "staff_intent_lines.json").read_text(encoding="utf-8"))["roles"]
    case = next(c for c in B.load_cases() if c["id"] == a.case)
    authored = {"h." + h["topic"]: h["answer"] for h in case["history"] if h.get("variant") is None}
    client = L.OllamaClient()
    configs = [("cloud routing off", {"CLOUD_ROUTING_ENABLED": "0", "CLOUD_SYNTHETIC_DEPLOYMENT": "0", "CLOUD_TRAINEE_DATA_APPROVED": "0"}),
               ("cloud routing on, privacy gate closed (a real deployment)", {"CLOUD_ROUTING_ENABLED": "1", "CLOUD_SYNTHETIC_DEPLOYMENT": "0", "CLOUD_TRAINEE_DATA_APPROVED": "0"}),
               ("cloud routing on, deployment declared synthetic", {"CLOUD_ROUTING_ENABLED": "1", "CLOUD_SYNTHETIC_DEPLOYMENT": "1", "CLOUD_TRAINEE_DATA_APPROVED": "0"})]
    report = {"adr": "ADR-0026", "asr_device": a.asr_device, "data": "synthetic only: an invented case, a synthetic voice, invented text", "case": a.case, "configurations": []}
    for label, env in configs:
        os.environ.update(env)
        routing.reload()
        d = D.Dialogue(client)
        runs = []
        for question, topic in QUESTIONS[: a.questions]:
            t0 = time.monotonic()
            spoken = b"".join(audio for _s, audio in tts.speak(question, "clinician")) if "clinician" in tts.roles() else b"".join(audio for _s, audio in tts.speak(question, tts.roles()[0]))
            t_tts_in = time.monotonic()
            heard = asr.transcribe(spoken)
            t_asr = time.monotonic()
            facts = []
            for h in case["history"]:
                if h.get("variant") is not None:
                    continue
                facts.append({"id": "h." + h["topic"], "value": h["answer"], "disclosed": False, "disclosable_now": bool(h["volunteered"]) or h["topic"] == topic})
            ctx = {"role": "patient", "case": case["id"], "allowed_intents": intents["patient"], "facts": facts, "last_turns": [], "results": []}
            staff = {r: {"role": r, "case": case["id"], "allowed_intents": intents[r], "facts": [], "last_turns": [], "results": []} for r in ("nurse", "anaesthesia")}
            replies = d.team(ctx, staff, heard.text or question, age=case["identity"]["age"], sex=case["identity"]["sex"], authored=authored, staff_lines={r: lines[r] for r in staff}, asked_fact="h." + topic if topic else None)
            t_dialogue = time.monotonic()
            first_audio = None
            audio_bytes = 0
            voices = {"patient": "patient_female" if case["identity"]["sex"] == "F" else "patient_male", "nurse": "nurse", "anaesthesia": "anaesthetist"}
            for role in [r for r in ("patient", "nurse", "anaesthesia") if r in replies]:
                for _s, audio in tts.speak(replies[role].text, voices[role]):
                    audio_bytes += len(audio)
                    if first_audio is None:
                        first_audio = time.monotonic()
            t_end = time.monotonic()
            audit = replies["patient"].audit or {}
            runs.append({"question": question, "heard": heard.text, "heard_ok": bool(heard.ok and not heard.low_confidence), "topic": topic,
                         "patient": replies["patient"].text, "patient_source": replies["patient"].source, "patient_intent": replies["patient"].intent,
                         "staff": {r: {"intent": replies[r].intent, "source": replies[r].source, "approved_line": replies[r].text in lines[r].values()} for r in staff if r in replies}, "silent_staff": [r for r in staff if r not in replies],
                         "model": audit.get("model", replies["patient"].source), "cloud": bool(audit.get("cloud")), "route": audit.get("route", ""), "fell_back_from": audit.get("fell_back_from", ""),
                         "ms": {"synthetic_question_audio": round((t_tts_in - t0) * 1000), "recognition": round((t_asr - t_tts_in) * 1000), "dialogue": round((t_dialogue - t_asr) * 1000),
                                "first_voice_ready": round((first_audio - t_dialogue) * 1000) if first_audio else None, "voices_ready": round((t_end - t_dialogue) * 1000),
                                "question_heard_to_first_voice": round((first_audio - t_asr) * 1000) if first_audio else None},
                         "audio_bytes": audio_bytes})
        d_ms = [r["ms"]["dialogue"] for r in runs]
        e_ms = [r["ms"]["question_heard_to_first_voice"] for r in runs if r["ms"]["question_heard_to_first_voice"]]
        report["configurations"].append({"label": label, "env": env, "questions": len(runs), "cloud_calls": sum(r["cloud"] for r in runs), "models": sorted({r["model"] for r in runs}),
                                         "dialogue_ms": {"p50": round(statistics.median(d_ms)), "p95": round(pct(d_ms, 0.95))},
                                         "heard_to_first_voice_ms": {"p50": round(statistics.median(e_ms)), "p95": round(pct(e_ms, 0.95))} if e_ms else None,
                                         "recognised": sum(r["heard_ok"] for r in runs), "patient_from_model": sum(r["patient_source"] == "model" for r in runs),
                                         "staff_lines_all_approved": all(s["approved_line"] for r in runs for s in r["staff"].values()), "runs": runs})
        print(label, "| models", sorted({r["model"] for r in runs}), "| dialogue p50/p95", round(statistics.median(d_ms)), round(pct(d_ms, 0.95)), "ms | recognised", sum(r["heard_ok"] for r in runs), "of", len(runs))
    if a.out:
        Path(a.out).write_text(json.dumps(report, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", a.out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
