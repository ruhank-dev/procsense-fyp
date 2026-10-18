"""Word error rate of the speech recogniser on real mock consultations (PriMock57; p1-voice-input-s01, EXP-VOX-3): `uv run --project tools --with faster-whisper --with av==16.0.1 [--with nvidia-cublas-cu12 --with nvidia-cudnn-cu12] tools/ai/asr_primock.py --dir DIR --model small.en --device cuda --compute int8_float16`.

PriMock57 (Babylon Health, CC BY 4.0; https://github.com/babylonhealth/primock57) holds 57 mock primary-care consultations in British English between trained clinicians and actors, with the doctor and the patient
on separate audio channels (16 kHz) and human utterance-level transcripts (Praat TextGrid). This takes the utterances of the files in --dir that are 2 to 15 s long (the service refuses longer clips), cuts each from
its channel with ffmpeg, runs the same service the product uses (server/asr/service.py), and scores it with the same normalisation and error-rate code as the synthetic test (asr_wer.py), after removing from the
reference the fillers a recogniser leaves out (um, uh, er, mm, hmm) and the transcription tags. Utterances the transcribers marked unintelligible or inaudible are skipped; text inside <UNSURE> is kept.
It reports the error rate overall and for doctors and patients separately, and the time per clip against the clip length. Limits: actors, not patients; one corpus, British accents, recorded over video calls; it
says nothing about a noisy ward or the accents of Pakistani trainees (DT-205), and the recogniser is not tuned on it. The audio is not stored in the repository; only the figures are (reports/ai/asr-primock57.json).
"""
from __future__ import annotations

import argparse
import json
import re
import statistics
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import asr_wer as W  # noqa: E402

REPORT = ROOT / "reports" / "ai" / "asr-primock57.json"
FILLERS = {"um", "uh", "er", "erm", "mm", "hmm", "mhm", "ah", "oh"}
INTERVAL = re.compile(r"xmin = ([\d.]+)\s*\n\s*xmax = ([\d.]+)\s*\n\s*text = \"(.*?)\"\s*\n", re.S)
SKIP = re.compile(r"<(UNIN|INAUDIBLE|CROSSTALK|OVERLAP)[^>]*>", re.I)


def utterances(textgrid: Path) -> list[tuple[float, float, str]]:
    out = []
    for a, b, t in INTERVAL.findall(textgrid.read_text(encoding="utf-8")):
        t = t.strip()
        if not t or SKIP.search(t):
            continue
        t = re.sub(r"</?UNSURE>", "", t)
        t = re.sub(r"<[^>]+>", " ", t)
        out.append((float(a), float(b), t))
    return out


def reference(text: str) -> list[str]:
    return [w for w in W.norm(text) if w not in FILLERS]


def cut(wav: Path, start: float, end: float) -> bytes:
    return subprocess.run(["ffmpeg", "-v", "error", "-ss", str(start), "-to", str(end), "-i", str(wav), "-f", "wav", "-ar", "16000", "-ac", "1", "-"], capture_output=True, check=True).stdout


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--model", default="small.en")
    ap.add_argument("--device", default="auto")
    ap.add_argument("--compute", default="int8")
    ap.add_argument("--max-clips", type=int, default=0)
    a = ap.parse_args(argv)
    W.add_cuda_dlls()
    from asr.service import AsrService
    svc = AsrService(a.model, a.device, a.compute)
    errs = {"doctor": [0, 0], "patient": [0, 0]}
    secs, lens, worst = [], [], []
    files = sorted(Path(a.dir).glob("*.wav"))
    clips = 0
    for wav in files:
        role = "doctor" if wav.stem.endswith("_doctor") else "patient"
        for start, end, text in utterances(wav.with_suffix(".TextGrid")):
            if not 2 <= end - start <= 15:
                continue
            ref = reference(text)
            if len(ref) < 3:
                continue
            if clips == 0:
                svc.transcribe(cut(wav, start, end))                      # warm-up
            audio = cut(wav, start, end)
            t = time.monotonic()
            r = svc.transcribe(audio)
            secs.append(time.monotonic() - t)
            lens.append(end - start)
            e, n = W.wer(ref, [w for w in W.norm(r.text) if w not in FILLERS])
            errs[role][0] += e
            errs[role][1] += n
            worst.append((e / n, wav.stem, text, r.text))
            clips += 1
            if a.max_clips and clips >= a.max_clips:
                break
        if a.max_clips and clips >= a.max_clips:
            break
    worst.sort(reverse=True)
    tot_e, tot_n = errs["doctor"][0] + errs["patient"][0], errs["doctor"][1] + errs["patient"][1]
    five = [s for s, n in zip(secs, lens) if 4 <= n <= 6]
    out = {"corpus": "PriMock57 (Babylon Health), CC BY 4.0, https://github.com/babylonhealth/primock57", "files": [f.name for f in files], "model": a.model, "device": a.device, "compute": a.compute, "clips": clips,
           "reference_words": tot_n, "wer": round(tot_e / tot_n, 4), "wer_doctor": round(errs["doctor"][0] / max(errs["doctor"][1], 1), 4), "wer_patient": round(errs["patient"][0] / max(errs["patient"][1], 1), 4),
           "clip_seconds_p50": round(statistics.median(lens), 2), "seconds_per_clip_p50": round(statistics.median(secs), 3), "seconds_per_clip_p95": round(sorted(secs)[max(0, int(0.95 * len(secs)) - 1)], 3),
           "seconds_for_4_to_6s_clips_p50": round(statistics.median(five), 3) if five else None, "clips_4_to_6s": len(five), "warmed_up": True,
           "worst": [(round(w, 2), i, r, h) for w, i, r, h in worst[:6]],
           "method": "utterances of 2 to 15 s with at least 3 reference words, fillers removed from the reference and the hypothesis, unintelligible and cross-talk utterances skipped",
           "limit": "actors in mock consultations over video calls, British accents; not a noisy ward and not Pakistani trainees (DT-205)"}
    merged = json.loads(REPORT.read_text(encoding="utf-8")) if REPORT.exists() else {}
    merged[f"{a.model.replace('.', '_')}-{a.device}-{a.compute}"] = out
    REPORT.write_text(json.dumps(merged, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({k: v for k, v in out.items() if k not in ("worst", "files")}))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
