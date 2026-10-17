"""Time to the first spoken sentence of the live voice (p1-voice-input-s04): `uv run --project tools tools/ai/tts_latency.py [--n 12]`.

Speaks the first sentence of `n` catalogue lines (content/voice/catalogue.json, lines without placeholders) through server/tts/engine.py exactly as the /tts route does (Piper, then ffmpeg shaping, nothing cached), voices
cycling through the roles, and reports the median, 95th percentile and worst time in seconds. `first_sentence_seconds` is the 95th percentile: the gate (content/ai/thresholds.json) asks for 2.5 s or less. Run it on
a quiet machine: the language-model evaluations and the speech recogniser share the processor and inflate it. Results go to artifacts/ai-bench/ (git-ignored) and, when committed, reports/ai/.
"""
import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--n", type=int, default=12)
    ap.add_argument("--runs", type=int, default=3, help="repeat the whole measurement; the reported figure is the WORST run's 95th percentile")
    a = ap.parse_args()
    from tts.engine import TtsEngine, split_sentences
    engine = TtsEngine()
    if not engine.available():
        raise SystemExit("BLOCKED: Piper, its voice or ffmpeg is not available (build/voice-tools)")
    lines = [l["text"] for l in json.loads((ROOT / "content" / "voice" / "catalogue.json").read_text(encoding="utf-8"))["lines"] if "{" not in l["text"]]
    chosen = lines[:: max(1, len(lines) // a.n)][: a.n]
    roles = engine.roles()
    engine.one("Warm up.", roles[0])                                            # loads the programs once so the first measured line is not a cold start of the disk cache
    try:
        import psutil
        load = psutil.cpu_percent(interval=1.0)
    except ImportError:
        load = None
    runs = []
    for r in range(a.runs):
        secs = []
        for i, text in enumerate(chosen):
            first = (split_sentences(text) or [text])[0]
            t = time.monotonic()
            audio, _ = engine.one(first + " " * r, roles[(i + r) % len(roles)])          # a different role each run so the cache cannot answer
            secs.append(time.monotonic() - t)
            assert len(audio) > 1000
        secs.sort()
        runs.append({"p95": round(secs[max(0, int(0.95 * len(secs)) - 1)], 2), "p50": round(statistics.median(secs), 2), "max": round(secs[-1], 2)})
    out = {"sentences": len(chosen), "runs": runs, "first_sentence_seconds": max(r["p95"] for r in runs), "p50_seconds": max(r["p50"] for r in runs), "max_seconds": max(r["max"] for r in runs),
           "cpu_percent_before": load, "baseline_before_persistent_piper_seconds": 6.7, "baseline_loaded_machine_seconds": 4.95,
           "note": "First sentence of each line, Piper kept alive, then ffmpeg shaping; the reported figure is the worst of the runs' 95th percentiles. The 'high' voice costs about 0.2 s per word on this processor and the figure is meant for a quiet processor (4.95 s was measured while language-model evaluations were running; 6.7 s was before Piper was kept alive)."}
    path = ROOT / "artifacts" / "ai-bench" / "tts-latency.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out))
