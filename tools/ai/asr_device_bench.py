"""Speed of the speech recogniser on the card against the processor (ADR-0027): `uv run --project server --extra asr-gpu python tools/ai/asr_device_bench.py [--out reports/ai/asr-device-bench.json]`.

Synthetic clips only: invented trainee questions spoken by the local synthetic voice (Piper), so no real person's voice is used. Each clip is recognised on the processor (int8) and on the card (int8_float16)
by the production service (server/asr/service.py); the report holds the words heard, the time of each call after a warm-up, and the median and 95th percentile. The service already falls back to the processor when the
card libraries are missing or fail (`device auto`); this tool shows the gain and that the two agree on the words.
"""
from __future__ import annotations

import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))

QUESTIONS = ["Do you have any allergies?", "What medications are you taking?", "Have you had surgery before?", "Nurse, what are the latest observations?", "When did you last eat or drink anything?",
             "Can you tell me your full name and date of birth please?", "Do you smoke or drink alcohol?", "I am worried, the saturation is dropping, I need the anaesthetist here now."]


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="")
    a = ap.parse_args(argv)
    from asr.service import AsrService
    from tts.engine import TtsEngine
    tts = TtsEngine()
    if not tts.available():
        raise SystemExit("the local voice (Piper) or ffmpeg is not available")
    role = "clinician" if "clinician" in tts.roles() else tts.roles()[0]
    clips = [(q, b"".join(audio for _s, audio in tts.speak(q, role))) for q in QUESTIONS]
    report = {"data": "synthetic only: invented questions spoken by a synthetic voice", "clips": len(clips), "devices": {}}
    heard = {}
    for label, device, compute in (("processor (cpu, int8)", "cpu", "int8"), ("card (cuda, int8_float16)", "cuda", "int8_float16")):
        try:
            svc = AsrService("small.en", device, compute)
            svc.transcribe(clips[0][1])                                             # warm-up: the model loads on the first call
            runs = []
            texts = []
            for q, audio in clips:
                t = time.monotonic()
                r = svc.transcribe(audio)
                runs.append(round((time.monotonic() - t) * 1000))
                texts.append(r.text)
            heard[device] = texts
            report["devices"][label] = {"ok": True, "ms_each": runs, "ms_p50": round(statistics.median(runs)), "ms_p95": sorted(runs)[min(len(runs) - 1, int(0.95 * len(runs)))], "heard": texts}
        except Exception as e:                                                      # a missing library is a result, not a crash: the service then uses the processor
            report["devices"][label] = {"ok": False, "error": f"{type(e).__name__}: {str(e)[:160]}"}
        print(label, {k: v for k, v in report["devices"][label].items() if k != "heard"})
    if "cpu" in heard and "cuda" in heard:
        norm = lambda t: " ".join("".join(c for c in t.lower() if c.isalnum() or c == " ").split())      # noqa: E731
        report["same_words_on_both"] = sum(norm(x) == norm(y) for x, y in zip(heard["cpu"], heard["cuda"]))
        print("same words on both:", report["same_words_on_both"], "of", len(clips))
    if a.out:
        Path(a.out).write_text(json.dumps(report, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
