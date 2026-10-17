"""Builds the speech-recognition vocabulary prompt from the content (p1-voice-input-s01): `uv run --project tools tools/content/build_vocab.py [--check]`.

The recogniser is given the words a trainee in these scenes is likely to say (investigations and tubes, theatre equipment stations, roles, drug classes of the treatments and the scoring behaviours' actions) as an initial
prompt, so "U and E" or "lap tower" are heard as such. Nothing is typed twice: the words come from content/ only. The prompt is kept under 150 words (a Whisper prompt holds 224 tokens). Writes content/voice/asr_vocab.json.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "voice" / "asr_vocab.json"
MAX_WORDS = 150
ROLES = ["nurse", "anaesthetist", "surgeon", "porter", "radiographer", "pharmacist", "consultant"]


def words() -> list[str]:
    inv = json.loads((CONTENT / "investigations" / "investigations.json").read_text(encoding="utf-8"))
    out = []
    for t in inv["tests"]:
        out.append(t.get("label") or t.get("name") or t["id"].replace("_", " "))
    for t in inv["tubes"] if isinstance(inv["tubes"], list) else inv["tubes"].values():
        out.append(t.get("label") or t.get("name") or t.get("id", "") if isinstance(t, dict) else str(t))
    orr = json.loads((CONTENT / "or" / "or_requirements.json").read_text(encoding="utf-8"))
    for s in orr["stations"]:
        out.append(s["id"].replace("_", " "))
    tre = json.loads((CONTENT / "treatments.json").read_text(encoding="utf-8"))
    for t in tre.get("treatments", []):
        out.append(t.get("label") or t["id"])
    out += ROLES + ["NEWS2", "ASA", "SBAR", "wristband", "allergy", "fasting", "consent", "sign in", "time out", "sign out", "theatre"]
    clean, seen = [], set()
    for w in out:
        w = re.sub(r"\s+", " ", str(w)).strip()
        if w and w.lower() not in seen and len(w.split()) <= 4:
            seen.add(w.lower())
            clean.append(w)
    return clean


def build() -> dict:
    ws, prompt = words(), ""
    kept = []
    for w in ws:
        if len((prompt + ", " + w).split()) > MAX_WORDS:
            break
        prompt = prompt + (", " if prompt else "") + w
        kept.append(w)
    return {"note": "Initial prompt for the speech recogniser, built from the content by tools/content/build_vocab.py; do not edit.", "prompt": "Pre-operative checks in a hospital: " + prompt + ".", "terms": kept, "dropped": len(ws) - len(kept)}


def main(argv: list[str]) -> int:
    text = json.dumps(build(), indent=1, ensure_ascii=False) + "\n"
    if "--check" in argv:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("ASR vocabulary: " + ("current" if ok else "out of date: run tools/content/build_vocab.py"))
        return 0 if ok else 1
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
