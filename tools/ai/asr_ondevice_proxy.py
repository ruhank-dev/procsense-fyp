"""PC proxy for the on-device recogniser spike (p2-ai-extensions-s01, EXP-P2AI-1): `uv run --project tools --with sherpa-onnx tools/ai/asr_ondevice_proxy.py --model-dir DIR --kind whisper|zipformer`.

Runs a model that is small enough to be a Quest candidate (sherpa-onnx: Whisper tiny.en int8, or the 20M streaming Zipformer) over the SAME rendered clips as tools/ai/asr_wer.py (the server recogniser,
faster-whisper small.en) and scores them with the same normalisation and word error rate, so the two figures are comparable. What this does NOT measure, and why nobody should read it as the spike's answer:
latency and thermal cost on the headset (an ARM build on the Quest 3 and Quest 2 is Sannan's, DT-205), the frame-time cost beside the simulation, and accented or noisy speech (the clips are one clean
synthetic voice). The seconds per clip printed here are this PC's processor, shared with whatever else was running. Model files are downloaded by hand from the sherpa-onnx release `asr-models`
(Apache-2.0 code; check each model's own licence before it ships). Results: reports/ai/asr-ondevice-proxy.json (merged by model name).
"""
from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import asr_wer as W  # noqa: E402

REPORT = ROOT / "reports" / "ai" / "asr-ondevice-proxy.json"


def pcm16k(path: Path):
    """The clip as mono 16 kHz float samples (ffmpeg decodes the .ogg)."""
    import numpy as np
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "1", "-ar", "16000", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def recogniser(kind: str, d: Path, threads: int):
    import sherpa_onnx as S
    if kind == "whisper":
        return S.OfflineRecognizer.from_whisper(encoder=str(d / "tiny.en-encoder.int8.onnx"), decoder=str(d / "tiny.en-decoder.int8.onnx"), tokens=str(d / "tiny.en-tokens.txt"), language="en", num_threads=threads)
    return S.OnlineRecognizer.from_transducer(tokens=str(d / "tokens.txt"), encoder=str(d / "encoder-epoch-99-avg-1.int8.onnx"), decoder=str(d / "decoder-epoch-99-avg-1.onnx"),
                                              joiner=str(d / "joiner-epoch-99-avg-1.int8.onnx"), num_threads=threads, sample_rate=16000, feature_dim=80)


def transcribe(rec, kind: str, samples) -> str:
    s = rec.create_stream()
    s.accept_waveform(16000, samples)
    if kind == "whisper":
        rec.decode_stream(s)
        return s.result.text
    s.accept_waveform(16000, [0.0] * 8000)                                      # tail padding so the last word is flushed
    s.input_finished()
    while rec.is_ready(s):
        rec.decode_stream(s)
    return rec.get_result(s)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--model-dir", required=True)
    ap.add_argument("--kind", choices=["whisper", "zipformer"], required=True)
    ap.add_argument("--limit", type=int, default=40)
    ap.add_argument("--threads", type=int, default=2)
    a = ap.parse_args(argv)
    d = Path(a.model_dir)
    cat = {l["id"]: l for l in json.loads((ROOT / "content" / "voice" / "catalogue.json").read_text(encoding="utf-8"))["lines"]}
    files = [f for f in sorted((ROOT / "build" / "voice").glob("*.ogg")) if f.stem in cat and "{" not in cat[f.stem]["text"]][:: max(1, 282 // a.limit)][: a.limit]
    rec = recogniser(a.kind, d, a.threads)
    errs = words = 0
    secs, worst = [], []
    for f in files:
        x = pcm16k(f)
        t = time.monotonic()
        text = transcribe(rec, a.kind, x)
        secs.append((time.monotonic() - t) / (len(x) / 16000))                  # real-time factor: processing seconds per second of speech
        e, n = W.wer(W.norm(cat[f.stem]["text"]), W.norm(text))
        errs, words = errs + e, words + n
        worst.append((e / max(n, 1), f.stem, cat[f.stem]["text"], text))
    worst.sort(reverse=True)
    out = {"model": d.name, "kind": a.kind, "threads": a.threads, "clips": len(files), "reference_words": words, "wer": round(errs / words, 4), "real_time_factor_p50": round(statistics.median(secs), 3),
           "real_time_factor_max": round(max(secs), 3), "worst": worst[:6], "platform": "desktop processor (x86-64), NOT the Quest",
           "limit": "same synthetic clean clips as reports/ai/asr-wer.json (server small.en: WER 0.0932); latency on the headset, thermal and frame cost are not measured (Sannan, DT-205)"}
    merged = json.loads(REPORT.read_text(encoding="utf-8")) if REPORT.exists() else {}
    merged[d.name] = out
    REPORT.write_text(json.dumps(merged, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({k: v for k, v in out.items() if k != "worst"}))
    for w, i, ref, hyp in worst[:3]:
        print(f"  {w:.2f} {i}\n    ref: {ref}\n    hyp: {hyp}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
