"""Checks an inference host (p1-ai-infrastructure-s01): `uv run --project tools tools/ai/check_hosts.py [--url http://127.0.0.1:11434] [--json] [--ask]`.

Reports, for the machine it runs on: the Ollama version and which wanted models are installed (with their digests, compared with the licence register), whether a schema-constrained prompt is answered (`--ask`),
whether faster-whisper is importable, whether Piper and ffmpeg are where the voice tools put them, and the GPU (nvidia-smi) with free memory. Exit code 0 when everything the machine is meant to do is ready,
1 otherwise; each missing piece is named with the fix. The same function backs the health endpoint of the AI gateway. Reads the machine; changes nothing; sends nothing outside it.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
WANTED = {"qwen3.5:4b-q4_K_M": "live", "qwen3.5:9b-q4_K_M": "batch", "all-minilm:22m": "embeddings (the communication classifier)"}
REGISTER = ROOT / "docs" / "research" / "licence_provenance_register.md"
PIPER = ROOT / "build" / "voice-tools"
PROBE_SCHEMA = {"type": "object", "required": ["ok"], "additionalProperties": False, "properties": {"ok": {"type": "boolean"}}}


def registered_digests() -> dict[str, str]:
    """Model name -> the first 12 hex characters of the digest the register records (empty when the register has none)."""
    if not REGISTER.exists():
        return {}
    text = REGISTER.read_text(encoding="utf-8")
    out = {}
    for name in WANTED:
        m = re.search(re.escape(name) + r"[^\n]*?\b([0-9a-f]{12})", text)
        if m:
            out[name] = m.group(1)
    return out


def gpu() -> dict:
    exe = shutil.which("nvidia-smi")
    if not exe:
        return {"present": False, "fix": "no NVIDIA driver found: install the driver (Ollama then uses the CPU, which is too slow for live work)"}
    try:
        line = subprocess.run([exe, "--query-gpu=name,memory.total,memory.free", "--format=csv,noheader,nounits"], capture_output=True, text=True, timeout=10).stdout.strip().splitlines()[0]
        name, total, free = [x.strip() for x in line.split(",")]
        return {"present": True, "name": name, "vram_mb": int(total), "free_mb": int(free)}
    except (OSError, subprocess.SubprocessError, IndexError, ValueError):
        return {"present": False, "fix": "nvidia-smi did not answer"}


def check(url: str = "http://127.0.0.1:11434", ask: bool = False, client=None, which=shutil.which, find_spec=importlib.util.find_spec) -> dict:
    from llm import client as C
    own = client is None
    client = client or C.OllamaClient(url)
    problems: list[str] = []
    try:
        models = client.models()
        report: dict = {"ollama": {"up": bool(models), "url": url, "models": {}}}
        if not models:
            problems.append(f"Ollama does not answer at {url}: start it (ollama serve) or fix the address (use 127.0.0.1, not localhost)")
        known = registered_digests()
        for name, role in WANTED.items():
            have = models.get(name, "")
            entry = {"role": role, "installed": bool(have), "digest": have[:12]}
            if not have:
                problems.append(f"{name} is not installed: ollama pull {name}")
            elif name in known and have[:12] != known[name]:
                entry["differs_from_register"] = known[name]
                problems.append(f"{name} has digest {have[:12]} but the licence register records {known[name]}: record the new digest or reinstall")
            report["ollama"]["models"][name] = entry
        if ask and models:
            r = client.chat(C.LIVE_MODEL, "Answer as JSON.", 'Reply with {"ok": true}.', schema=PROBE_SCHEMA, timeout=60, num_predict=20)
            report["ollama"]["schema_answer"] = {"ok": r.ok and r.json == {"ok": True}, "ms": r.latency_ms, "error": r.error}
            if not report["ollama"]["schema_answer"]["ok"]:
                problems.append("the live model did not answer a schema-constrained prompt: " + (r.error or "wrong answer"))
    finally:
        if own:
            client.close()
    report["faster_whisper"] = {"installed": find_spec("faster_whisper") is not None}
    if not report["faster_whisper"]["installed"]:
        problems.append("faster-whisper is not installed: pip install faster-whisper (speech recognition, p1-voice-input)")
    report["voice"] = {"piper": bool(list(PIPER.glob("**/piper*"))) if PIPER.exists() else False, "ffmpeg": which("ffmpeg") is not None}
    if not report["voice"]["piper"]:
        problems.append("Piper is not in build/voice-tools: see tools/voice/README.md")
    if not report["voice"]["ffmpeg"]:
        problems.append("ffmpeg is not on the path: winget install Gyan.FFmpeg")
    report["gpu"] = gpu()
    if not report["gpu"]["present"]:
        problems.append(report["gpu"]["fix"])
    report["ready"] = not problems
    report["problems"] = problems
    return report


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:11434")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--ask", action="store_true", help="also send a schema-constrained prompt to the live model")
    a = ap.parse_args(argv)
    report = check(a.url, a.ask)
    if a.json:
        print(json.dumps(report, indent=2))
    else:
        o = report["ollama"]
        print(f"Ollama: {'up' if o['up'] else 'DOWN'} at {o['url']}")
        for n, m in o["models"].items():
            print(f"  {n} ({m['role']}): {'installed ' + m['digest'] if m['installed'] else 'MISSING'}")
        print(f"faster-whisper: {'yes' if report['faster_whisper']['installed'] else 'NO'}; Piper: {'yes' if report['voice']['piper'] else 'NO'}; ffmpeg: {'yes' if report['voice']['ffmpeg'] else 'NO'}")
        g = report["gpu"]
        print("GPU: " + (f"{g['name']}, {g['free_mb']} of {g['vram_mb']} MB free" if g["present"] else "none found"))
        for p in report["problems"]:
            print("  FIX:", p)
        print("host ready" if report["ready"] else f"{len(report['problems'])} thing(s) to fix")
    return 0 if report["ready"] else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
