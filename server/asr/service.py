"""Speech recognition of push-to-talk clips (p1-voice-input-s01, EXP-VOX-3): faster-whisper, loaded once, one clip at a time.

    svc = AsrService()                                  # nothing is loaded until the first clip
    result = svc.transcribe(audio_bytes)                # AsrResult: ok, text, words [(word, start, end, probability)], avg_logprob, low_confidence, seconds, error

A clip is any audio file ffmpeg reads; it is converted to 16 kHz mono and refused if longer than MAX_SECONDS (15). The recogniser gets the vocabulary prompt built from the content (tools/content/build_vocab.py),
voice-activity detection on, beam 3, temperature 0. A clip whose average log-probability is under MIN_AVG_LOGPROB is flagged `low_confidence` (the client says "Say again"; nothing is credited from it).
Requests are served one at a time (a lock): concurrent requests queue and none crashes. The model is the optional dependency `faster-whisper` (`uv sync --extra asr`); without it the service answers
`ASR_UNAVAILABLE` and the client falls back to menus. The model name and device come from settings; the default is `small.en` (the plan names large-v3-turbo: see the measurements in docs/research/asr.md).
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import threading
import time
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAX_SECONDS = 15.0
MIN_AVG_LOGPROB = -1.0
SAMPLE_RATE = 16000
VOCAB = ROOT / "content" / "voice" / "asr_vocab.json"


@dataclass
class AsrResult:
    ok: bool
    text: str = ""
    words: list = field(default_factory=list)
    avg_logprob: float = 0.0
    low_confidence: bool = False
    seconds: float = 0.0
    duration: float = 0.0
    error: str = ""


def find_ffmpeg() -> str | None:
    exe = os.environ.get("FFMPEG_EXE") or shutil.which("ffmpeg")
    if exe:
        return exe
    base = Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "WinGet" / "Packages"
    found = sorted(base.glob("Gyan.FFmpeg*/**/bin/ffmpeg.exe")) if base.exists() else []
    return str(found[0]) if found else None


def decode(audio: bytes) -> tuple[tuple[bytes, float] | None, str]:
    """Any audio to a 16 kHz mono 16-bit WAV (bytes) and its length in seconds, or (None, error)."""
    ffmpeg = find_ffmpeg()
    if not ffmpeg:
        return None, "FFMPEG_MISSING"
    p = subprocess.run([ffmpeg, "-v", "error", "-i", "pipe:0", "-f", "wav", "-ac", "1", "-ar", str(SAMPLE_RATE), "-acodec", "pcm_s16le", "pipe:1"], input=audio, capture_output=True, timeout=30)
    if p.returncode != 0 or len(p.stdout) <= 44:
        return None, "BAD_AUDIO"
    return (p.stdout, (len(p.stdout) - 44) / (2 * SAMPLE_RATE)), ""


def add_cuda_dlls() -> None:
    """On Windows the CUDA libraries CTranslate2 needs come from the pip packages of the asr-gpu extra (nvidia-cublas-cu12, nvidia-cudnn-cu12) and sit under site-packages/nvidia; make them findable. Does nothing elsewhere or without them."""
    if not hasattr(os, "add_dll_directory"):
        return
    try:
        import nvidia
    except ImportError:
        return
    for base in nvidia.__path__:
        for sub in Path(base).glob("*/bin"):
            os.add_dll_directory(str(sub))


class AsrService:
    def __init__(self, model_name: str = "small.en", device: str = "auto", compute_type: str = "int8", model_factory=None):
        self.model_name, self.device, self.compute_type = model_name, device, compute_type
        self._factory = model_factory
        self._model = None
        self._lock = threading.Lock()
        self.prompt = json.loads(VOCAB.read_text(encoding="utf-8"))["prompt"] if VOCAB.exists() else ""

    def _load(self):
        if self._model is None:
            if self._factory is not None:
                self._model = self._factory()
            else:
                add_cuda_dlls()
                from faster_whisper import WhisperModel          # the optional dependency
                self._model = WhisperModel(self.model_name, device=self.device, compute_type=self.compute_type)
        return self._model

    def available(self) -> bool:
        if self._factory is not None:
            return True
        try:
            import faster_whisper  # noqa: F401
            return True
        except ImportError:
            return False

    def transcribe(self, audio: bytes) -> AsrResult:
        if not self.available():
            return AsrResult(False, error="ASR_UNAVAILABLE")
        decoded, err = decode(audio)
        if decoded is None:
            return AsrResult(False, error=err)
        wav, duration = decoded
        if duration > MAX_SECONDS:
            return AsrResult(False, duration=duration, error="CLIP_TOO_LONG")
        with self._lock:                                                    # one clip at a time: a second request waits its turn
            t = time.monotonic()
            model = self._load()
            import io
            segments, _info = model.transcribe(io.BytesIO(wav), beam_size=3, temperature=0, vad_filter=True, initial_prompt=self.prompt or None, word_timestamps=True, language="en")
            segs = list(segments)
        words = [(w.word.strip(), round(w.start, 2), round(w.end, 2), round(w.probability, 3)) for s in segs for w in (s.words or [])]
        avg = sum(s.avg_logprob for s in segs) / len(segs) if segs else -9.0
        text = " ".join(s.text.strip() for s in segs).strip()
        return AsrResult(bool(text), text, words, round(avg, 3), avg < MIN_AVG_LOGPROB or not text, round(time.monotonic() - t, 2), round(duration, 2), "" if text else "NO_SPEECH")
