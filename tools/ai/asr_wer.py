"""Word error rate of the speech recogniser on the rendered voice lines (p1-voice-input-s01, EXP-VOX-3): `uv run --project tools --with faster-whisper tools/ai/asr_wer.py --model small.en [--limit 60] [--device cpu]`.

The offline voice pipeline (tools/voice) renders the lines of the catalogue with Piper. This feeds those files to the recogniser exactly as a clip would arrive and compares the text with the catalogue's text:
word error rate (substitutions, insertions, deletions over the reference words, after lower-casing and removing punctuation), the share of lines flagged low-confidence, and the seconds per clip. IMPORTANT LIMIT:
the speech is synthetic and clean (one British voice, shifted a little per role), so this is an upper bound on accuracy, not a measure of accented trainees in a noisy room; the plan's real test is
PriMock57 and consented recordings (DT-205). Results go to artifacts/ai-bench/ (git-ignored) and, when committed, reports/ai/.
"""
import argparse
import json
import re
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))


ORDINALS = {"first": "1st", "second": "2nd", "third": "3rd", "fourth": "4th", "fifth": "5th", "sixth": "6th", "seventh": "7th", "eighth": "8th", "ninth": "9th", "tenth": "10th", "eleventh": "11th", "twelfth": "12th",
            "thirteenth": "13th", "fourteenth": "14th", "fifteenth": "15th", "sixteenth": "16th", "seventeenth": "17th", "eighteenth": "18th", "nineteenth": "19th", "twentieth": "20th", "thirtieth": "30th"}
TENS = {"twenty": 20, "thirty": 30}
DIGITS = {"zero": "0", "oh": "0", "o": "0"}


def norm(text: str) -> list[str]:
    """Words compared by the error rate. Spoken and written forms of the same thing count as equal: '08:00', '8 0 0' and '800'; 'seventeenth of April' and '17th April'. Nothing else is forgiven."""
    t = re.sub(r"(\d\d?):(\d\d)", r"\1\2", text.lower().replace("-", " "))
    words = re.sub(r"[^a-z0-9' ]+", " ", t).split()
    out: list[str] = []
    for w in words:
        w = DIGITS.get(w, w) if out and out[-1].isdigit() else w
        if w in TENS:
            w = str(TENS[w])
        elif w in ORDINALS:
            w = ORDINALS[w]
        if w.isdigit() and out and out[-1].isdigit() and len(out[-1]) + len(w) <= 4 and (len(w) == 1 or out[-1] in ("20", "30")):      # '20 zero zero' or '8 0 0' is one number
            out[-1] += w
        elif w != "of":
            out.append(w)
    return [x.lstrip("0") or "0" if x.isdigit() else x for x in out]


def add_cuda_dlls() -> None:
    """On Windows the pip packages nvidia-cublas-cu12 and nvidia-cudnn-cu12 (`uv run --with nvidia-cublas-cu12 --with nvidia-cudnn-cu12`) put the CUDA libraries CTranslate2 needs under site-packages/nvidia; make them findable."""
    import os
    try:
        import nvidia
    except ImportError:
        return
    for base in nvidia.__path__:
        for sub in Path(base).glob("*/bin"):
            os.add_dll_directory(str(sub))
            os.environ["PATH"] = str(sub) + os.pathsep + os.environ.get("PATH", "")


def clip_seconds(path: Path) -> float:
    """Length of an audio file in seconds (ffprobe)."""
    import subprocess
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(path)], capture_output=True, text=True).stdout.strip()
    return float(out or 0)


def wer(ref: list[str], hyp: list[str]) -> tuple[int, int]:
    d = list(range(len(hyp) + 1))
    for i, r in enumerate(ref, 1):
        prev, d[0] = d[0], i
        for j, h in enumerate(hyp, 1):
            cur = min(d[j] + 1, d[j - 1] + 1, prev + (r != h))
            prev, d[j] = d[j], cur
    return d[len(hyp)], len(ref)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="small.en")
    ap.add_argument("--device", default="auto")
    ap.add_argument("--compute", default="int8")
    ap.add_argument("--limit", type=int, default=60)
    a = ap.parse_args()
    add_cuda_dlls()
    from asr.service import AsrService
    cat = {l["id"]: l for l in json.loads((ROOT / "content" / "voice" / "catalogue.json").read_text(encoding="utf-8"))["lines"]}
    files = [f for f in sorted((ROOT / "build" / "voice").glob("*.ogg")) if f.stem in cat and "{" not in cat[f.stem]["text"]][:: max(1, 282 // a.limit)][: a.limit]
    svc = AsrService(a.model, a.device, a.compute)
    errs = words = low = 0
    secs, worst, lengths = [], [], []
    svc.transcribe(files[0].read_bytes())                                    # warm-up: the first call loads kernels and is not what a trainee waits for (the model itself is loaded once at start)
    for f in files:
        lengths.append(clip_seconds(f))
        r = svc.transcribe(f.read_bytes())
        ref = norm(cat[f.stem]["text"])
        e, n = wer(ref, norm(r.text))
        errs, words, low = errs + e, words + n, low + r.low_confidence
        secs.append(r.seconds)
        worst.append((e / max(n, 1), f.stem, cat[f.stem]["text"], r.text))
    worst.sort(reverse=True)
    out = {"model": a.model, "device": a.device, "compute": a.compute, "clips": len(files), "reference_words": words, "wer": round(errs / words, 4), "low_confidence_share": round(low / len(files), 3),
           "seconds_per_clip_p50": round(statistics.median(secs), 2), "seconds_per_clip_max": round(max(secs), 2), "clip_seconds_p50": round(statistics.median(lengths), 2),
           "seconds_per_clip_3_to_7s_p50": round(statistics.median([x for x, n in zip(secs, lengths) if 3 <= n <= 7] or [0]), 2), "clips_3_to_7s": sum(3 <= n <= 7 for n in lengths), "warmed_up": True, "worst": worst[:8],
           "limit": "synthetic clean speech from one rendered voice: an upper bound, not a test on accented trainees in a noisy room"}
    path = ROOT / "artifacts" / "ai-bench" / f"asr-wer-{a.model.replace('/', '_')}-{a.device}-{a.compute}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k != "worst"}))
    for w, i, ref, hyp in worst[:4]:
        print(f"  {w:.2f} {i}\n    ref: {ref}\n    hyp: {hyp}")
