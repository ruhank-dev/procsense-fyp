"""Speech-recogniser copies of benchmark sentences, so the ASR-error rows hold a recogniser's own errors and not errors a person invented.

    uv run --project tools --with faster-whisper --with "av<14" tools/bench/asr_roundtrip.py --smoke 24            # check the machinery on old synthetic sentences; writes artifacts/ai-bench/asr-smoke-copies.jsonl
    uv run --project tools --with faster-whisper --with "av<14" tools/bench/asr_roundtrip.py --split test            # copies of the frozen sentences; writes tests/classifier_independent/asr_test.jsonl

Each sentence is spoken by the offline voice (server/tts/engine.py: Piper then ffmpeg, voices cycling through the roles of tools/voice/voices.json) and transcribed by the service the app uses
(server/asr/service.py, faster-whisper), in two variants: `clean`, and `phone` (band-limited to 300-3000 Hz with white noise mixed in by ffmpeg). The recogniser's domain vocabulary prompt is switched OFF here, so
the copies show what an unprompted recogniser does to the wording. Copies keep the group and the label of their source, record the source id and the SHA-256 of the source text, are marked independent false (the
words are the recogniser's, the meaning is the author's), and are scored as a separate group that never counts towards the 100/200 rows. Speech from one synthetic voice is far cleaner than a trainee in a
theatre: the error rate here is a floor, not an estimate (DT-205). Needs the speech modules (server/tts, server/asr), Piper and ffmpeg (build/voice-tools); recognition runs on the CPU here.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
INDEP = ROOT / "tests" / "classifier_independent"
OUT = ROOT / "artifacts" / "ai-bench"
PHONE = "highpass=f=300,lowpass=f=3000,volume=0.7"


def degrade(audio: bytes, ffmpeg: str) -> bytes:
    """Telephone-band speech with steady white noise, returned as ogg."""
    graph = f"[0:a]{PHONE}[s];anoisesrc=color=white:amplitude=0.02:sample_rate=44100[n];[s][n]amix=inputs=2:duration=first:normalize=0"
    return subprocess.run([ffmpeg, "-v", "error", "-i", "pipe:0", "-filter_complex", graph, "-c:a", "libvorbis", "-f", "ogg", "pipe:1"], input=audio, capture_output=True, check=True, timeout=60).stdout


def copies(rows: list[dict], limit: int) -> list[dict]:
    from asr.service import AsrService
    from tts.engine import TtsEngine
    tts = TtsEngine()
    if not tts.available():
        raise SystemExit("BLOCKED: Piper, its voice or ffmpeg is not available (build/voice-tools); see docs/plans/p0/p0-voice-audio/README.md")
    asr = AsrService("small.en", "cpu", "int8")
    asr.prompt = ""                                                          # no domain vocabulary: see the module note
    if not asr.available():
        raise SystemExit("BLOCKED: faster-whisper is not installed; run with `uv run --project tools --with faster-whisper`")
    roles = tts.roles()
    out = []
    for i, r in enumerate(rows[:limit] if limit else rows):
        speech, _ = tts.one(r["text"], roles[i % len(roles)])
        for variant, audio in (("clean", speech), ("phone", degrade(speech, tts.ffmpeg))):
            res = asr.transcribe(audio)
            if res.ok and res.text.strip():
                out.append({**{k: r[k] for k in ("act", "critical", "group") if k in r}, "id": f"{r['id']}-{variant}", "derived_from": r["id"], "source_sha256": hashlib.sha256(r["text"].encode("utf-8")).hexdigest(),
                            "text": res.text.strip(), "source_text": r["text"], "variant": variant, "role": roles[i % len(roles)], "bucket": "asr", "independent": False})
    return out


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--split", choices=["dev", "test"])
    ap.add_argument("--smoke", type=int, default=0, help="N old synthetic test sentences, to check the machinery only")
    ap.add_argument("--limit", type=int, default=0)
    a = ap.parse_args(argv)
    OUT.mkdir(parents=True, exist_ok=True)
    if a.smoke:
        rows = [json.loads(l) for l in (ROOT / "tests" / "classifier" / "test.jsonl").read_text(encoding="utf-8").splitlines()][::3]
        rows = [{**r, "group": r["speaker"]} for r in rows]
        made, path = copies(rows, a.smoke), OUT / "asr-smoke-copies.jsonl"
    else:
        if not a.split or not (INDEP / "manifest.json").exists():
            raise SystemExit("BLOCKED: the independent set is not frozen (tests/classifier_independent/manifest.json is missing). Use --smoke N to check the machinery.")
        rows = [{**json.loads(l), "group": json.loads(l)["author"]} for l in (INDEP / f"{a.split}.jsonl").read_text(encoding="utf-8").splitlines()]
        rows = [r for r in rows if r["bucket"] in ("plain", "paraphrase")]
        made, path = copies(rows, a.limit), INDEP / f"asr_{a.split}.jsonl"
    path.write_text("".join(json.dumps(r, ensure_ascii=False, sort_keys=True) + "\n" for r in made), encoding="utf-8", newline="\n")
    changed = sum(r["text"].lower().strip(" .?!") != r["source_text"].lower().strip(" .?!") for r in made)
    print(f"wrote {len(made)} copies to {path}; {changed} differ from their source after lower-casing")
    for r in [r for r in made if r["text"].lower().strip(" .?!") != r["source_text"].lower().strip(" .?!")][:6]:
        print(f"  [{r['variant']}] {r['source_text']!r} -> {r['text']!r}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
