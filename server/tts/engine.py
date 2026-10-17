"""Live text-to-speech for lines the language model made at run time (p1-voice-input-s04): the same voice settings as the offline pipeline, sentence by sentence, with a cache.

    engine = TtsEngine()                                    # finds Piper, the voice and ffmpeg like tools/voice/render.py (PIPER_EXE, PIPER_MODEL, FFMPEG_EXE or build/voice-tools)
    for sentence, audio in engine.speak("I take one tablet. It is for my blood pressure.", "patient_male"):    # the first sentence is ready (and sent) before the second is made

The roles and their pitch and pace are `tools/voice/voices.json`, the file the offline renderer uses, so a live line sounds like the pre-rendered ones; the loudness is set by one normalisation pass (the
offline renderer measures first; one pass is within a few LU and fast enough for live use). Audio is Ogg Vorbis. Repeated lines are served from a cache keyed by the words, the role and the voice. If
Piper, the voice or ffmpeg is missing the engine says so (`available()` is False) and the client shows subtitles only. Piper runs as a separate program, never linked in (because of its licence). The engine
does no checking of the words: the gateway has already validated whatever is spoken.
"""
from __future__ import annotations

import hashlib
import json
import os
import queue
import re
import shutil
import subprocess
import tempfile
import threading
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VOICES = ROOT / "tools" / "voice" / "voices.json"
LOCAL_TOOLS = ROOT / "build" / "voice-tools"
MAX_CHARS = 400
CACHE_SIZE = 256
SPLIT = re.compile(r"(?<=[.!?])\s+")


def split_sentences(text: str) -> list[str]:
    return [s.strip() for s in SPLIT.split(text.strip()) if s.strip()]


def _find(env: str, patterns: tuple[str, ...], name: str) -> str | None:
    if os.environ.get(env):
        return os.environ[env]
    for pattern in patterns:
        found = sorted(LOCAL_TOOLS.glob(pattern)) if LOCAL_TOOLS.exists() else []
        if found:
            return str(found[0])
    return shutil.which(name)


def _ffmpeg() -> str | None:
    exe = os.environ.get("FFMPEG_EXE") or shutil.which("ffmpeg")
    if exe:
        return exe
    base = Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "WinGet" / "Packages"
    found = sorted(base.glob("Gyan.FFmpeg*/**/bin/ffmpeg.exe")) if base.exists() else []
    return str(found[0]) if found else None


class PersistentPiper:
    """One Piper process kept alive so the voice (about five seconds to load) is loaded once, not for every sentence.

    Piper run with `--output_dir` reads one line of text at a time from its standard input, writes each utterance as a WAV file in that folder and prints the file's path on its standard output. A reader thread
    puts the printed paths on a queue so a stuck process times out instead of hanging the service. The log lines on its standard error are discarded (a full pipe would stop it). If the process has died or answers
    nothing it is started again once; after that the error goes to the caller, who falls back to subtitles."""

    def __init__(self, exe: str | list[str], model: str, timeout: float = 60.0):
        self.cmd = list(exe) if isinstance(exe, (list, tuple)) else [exe]            # a list lets a test start a stand-in program
        self.model, self.timeout = model, timeout
        self._dir = tempfile.TemporaryDirectory(prefix="procsense-piper-")
        self._proc: subprocess.Popen | None = None
        self._paths: queue.Queue = queue.Queue()
        self._lock = threading.Lock()

    def _start(self) -> None:
        self.close()
        self._paths = queue.Queue()
        self._proc = subprocess.Popen([*self.cmd, "--model", self.model, "--output_dir", self._dir.name], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)

        def read(proc: subprocess.Popen, paths: queue.Queue) -> None:
            for raw in iter(proc.stdout.readline, b""):
                line = raw.decode("utf-8", "replace").strip()
                if line.lower().endswith(".wav"):
                    paths.put(line)
            paths.put(None)                                            # the process ended
        threading.Thread(target=read, args=(self._proc, self._paths), daemon=True).start()

    def alive(self) -> bool:
        return self._proc is not None and self._proc.poll() is None

    def warm(self) -> None:
        """Load the voice now (one throw-away sentence) so the first real sentence does not pay for it."""
        try:
            self.synth("Ready.")
        except (OSError, RuntimeError):
            pass

    def synth(self, text: str) -> bytes:
        line = " ".join(text.split()).encode("utf-8") + b"\n"          # one line, one utterance
        with self._lock:
            for attempt in (1, 2):
                if not self.alive():
                    self._start()
                try:
                    self._proc.stdin.write(line)
                    self._proc.stdin.flush()
                    path = self._paths.get(timeout=self.timeout)
                except (OSError, queue.Empty):
                    path = None
                if path:
                    try:
                        data = Path(path).read_bytes()
                    finally:
                        Path(path).unlink(missing_ok=True)
                    return data
                self.close()                                            # dead or silent: start again once
            raise RuntimeError("PIPER_NO_ANSWER")

    def close(self) -> None:
        if self._proc is not None:
            try:
                self._proc.kill()
            except OSError:
                pass
            self._proc = None


class TtsEngine:
    def __init__(self, synth=None):
        self.cfg = json.loads(VOICES.read_text(encoding="utf-8"))
        self._synth = synth                                           # tests give a function (words, role settings) -> audio bytes
        self._cache: OrderedDict[str, bytes] = OrderedDict()
        self._lock = threading.Lock()
        self.hits = self.misses = 0
        self.piper = _find("PIPER_EXE", ("piper/**/piper.exe", "piper/**/piper"), "piper")
        model = LOCAL_TOOLS / f"{self.cfg['model']}.onnx"
        self.model = os.environ.get("PIPER_MODEL") or (str(model) if model.exists() else None)
        self.ffmpeg = _ffmpeg()
        self._persistent: PersistentPiper | None = None

    def persistent(self) -> PersistentPiper | None:
        """The kept-alive Piper process (set PIPER_PERSISTENT=0 to start Piper afresh for every sentence, as before)."""
        if self._persistent is None and self._synth is None and self.piper and self.model and os.environ.get("PIPER_PERSISTENT", "1") != "0":
            self._persistent = PersistentPiper(self.piper, self.model)
        return self._persistent

    def warm(self) -> None:
        p = self.persistent()
        if p is not None:
            threading.Thread(target=p.warm, daemon=True).start()

    def available(self) -> bool:
        return self._synth is not None or bool(self.piper and self.model and self.ffmpeg)

    def roles(self) -> list[str]:
        return sorted(self.cfg["roles"])

    def key(self, text: str, role: str) -> str:
        return hashlib.sha256(json.dumps([text, self.cfg["roles"][role], self.cfg["model"]], sort_keys=True).encode("utf-8")).hexdigest()

    def _make(self, text: str, role: str) -> bytes:
        settings = self.cfg["roles"][role]
        if self._synth is not None:
            return self._synth(text, settings)
        rate = int(json.loads(Path(self.model + ".json").read_text(encoding="utf-8"))["audio"]["sample_rate"])
        persistent = self.persistent()
        if persistent is not None:
            wav = persistent.synth(text)
        else:
            with tempfile.TemporaryDirectory() as tmp:      # a file, not stdout: on Windows the text-mode stdout turns every 0x0A byte of the WAV into 0x0D 0x0A and the speech is garbled
                out = Path(tmp) / "speech.wav"
                subprocess.run([self.piper, "--model", self.model, "--output_file", str(out)], input=text.encode("utf-8"), capture_output=True, check=True, timeout=30)
                wav = out.read_bytes()
        pitch, pace = settings["pitch"], settings["pace"]
        parts = []
        if pitch != 1.0:
            parts += [f"asetrate={rate * pitch:.0f}", f"aresample={rate}"]
        if abs(pace / pitch - 1.0) > 1e-6:
            parts.append(f"atempo={pace / pitch:.4f}")
        parts.append(f"loudnorm=I={self.cfg['target_lufs']}:TP={self.cfg['true_peak_db']}:LRA=7")
        return subprocess.run([self.ffmpeg, "-v", "error", "-f", "wav", "-i", "pipe:0", "-af", ",".join(parts), "-ar", "44100", "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", "-f", "ogg", "pipe:1"],
                              input=wav, capture_output=True, check=True, timeout=30).stdout

    def one(self, text: str, role: str) -> tuple[bytes, bool]:
        """Audio for one sentence and whether it came from the cache."""
        k = self.key(text, role)
        with self._lock:
            if k in self._cache:
                self._cache.move_to_end(k)
                self.hits += 1
                return self._cache[k], True
        audio = self._make(text, role)
        with self._lock:
            self._cache[k] = audio
            self.misses += 1
            while len(self._cache) > CACHE_SIZE:
                self._cache.popitem(last=False)
        return audio, False

    def speak(self, text: str, role: str):
        """Yields (sentence, audio) one sentence at a time, the first as soon as it is made."""
        if role not in self.cfg["roles"]:
            raise KeyError(role)
        if len(text) > MAX_CHARS:
            raise ValueError("line too long")
        for sentence in split_sentences(text):
            yield sentence, self.one(sentence, role)[0]
