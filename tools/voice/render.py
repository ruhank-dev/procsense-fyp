"""Renders the voice-line catalogue to audio with an offline voice (p0-voice-audio-s02, EXP-VOX-1 pipeline part).

    uv run --project tools tools/voice/render.py              # render what is new or changed (task voice-render)
    uv run --project tools tools/voice/render.py --check      # every plain line has a file, a sane length and the target loudness (plan test T-VP2)
    uv run --project tools tools/voice/render.py --only PAT-VAR-S1-A-IDENTITY [...]    # render just these lines

Piper (a separate program, never linked into the app) speaks each line; ffmpeg shifts the pitch and pace of the one allowed voice per role (voices.json), normalises the loudness to
-16 LUFS with loudnorm and writes Ogg Vorbis. A line is only rendered again when its text, its role settings or the voice change (a hash is kept in the index).
Output: build/voice/<line id>.ogg and build/voice/index.json (git-ignored; the copy into the Unity project is the playback step, p0-voice-audio-s03).
Where the programs are: PIPER_EXE, PIPER_MODEL and FFMPEG_EXE if set, else build/voice-tools/ (see docs/plans/p0/p0-voice-audio/README.md), else PATH.
A line with a {placeholder} is a template: its value is only known in the session, so it is left for live speech (p1-voice-input-s04) and listed.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CATALOGUE = ROOT / "content" / "voice" / "catalogue.json"
VOICES = Path(__file__).resolve().parent / "voices.json"
PRONUNCIATIONS = Path(__file__).resolve().parent / "pronunciations.json"
OUT = ROOT / "build" / "voice"
INDEX = OUT / "index.json"
TOOLS = ROOT / "build" / "voice-tools"
PLACEHOLDER = re.compile(r"\{[a-z_]+\}")
MIN_SECONDS, MAX_SECONDS = 0.3, 30.0
LOUDNESS_TOLERANCE = 1.5          # LU either side of the target: one-pass loudnorm is not exact on very short lines


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def is_gap(line: dict) -> bool:
    """A line with no words yet: the content does not say what is spoken (DT-178 to DT-180). Nothing is invented to fill it."""
    return not line["text"].strip()


def is_template(line: dict) -> bool:
    return bool(line.get("template")) or bool(PLACEHOLDER.search(line["text"]))


def sex_of(case: str, cache: dict[str, str] = {}) -> str:
    if case not in cache:
        cache[case] = load(ROOT / "content" / "case_files" / f"{case}.json")["identity"].get("sex", "F")
    return cache[case]


def role_of(line: dict) -> str:
    """The voice role of a line: patients by the case's sex, staff by their job."""
    if line["speaker"] == "patient":
        return "patient_male" if sex_of(line["case"]) == "M" else "patient_female"
    return line["speaker"]


def respell(text: str, words: dict[str, str]) -> str:
    """What is sent to the voice: whole words replaced by their respelling (the subtitle keeps the original text)."""
    for word, spoken in words.items():
        text = re.sub(r"(?<![A-Za-z0-9])" + re.escape(word) + r"(?![A-Za-z0-9])", spoken, text)
    return text


def line_hash(text: str, role: dict, voice: str) -> str:
    return hashlib.sha256(json.dumps([text, role, voice, "v3-gain-limiter"], sort_keys=True).encode("utf-8")).hexdigest()[:16]


def audio_filter(role: dict, rate: int) -> str:
    """Pitch by resampling (asetrate), the speed that causes undone with atempo, then the pace asked for. Loudness is added after measuring."""
    pitch, pace = role["pitch"], role["pace"]
    tempo = pace / pitch
    parts = []
    if pitch != 1.0:
        parts += [f"asetrate={rate * pitch:.0f}", f"aresample={rate}"]
    if abs(tempo - 1.0) > 1e-6:
        parts.append(f"atempo={tempo:.4f}")
    return ",".join(parts) if parts else "anull"


def find(env: str, local: Path, name: str) -> str:
    path = os.environ.get(env) or (str(local) if local.exists() else shutil.which(name))
    if not path:
        raise SystemExit(f"{name} not found: set {env}, or put it in {local.relative_to(ROOT)}, or on PATH")
    return path


def tools() -> tuple[str, str, str]:
    piper = find("PIPER_EXE", TOOLS / "piper" / "piper" / "piper.exe", "piper")
    model = os.environ.get("PIPER_MODEL") or str(TOOLS / f"{load(VOICES)['model']}.onnx")
    ffmpeg = os.environ.get("FFMPEG_EXE") or shutil.which("ffmpeg")
    if not ffmpeg:
        for found in sorted((Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "WinGet" / "Packages").glob("Gyan.FFmpeg*/**/bin/ffmpeg.exe")):
            ffmpeg = str(found)
    if not ffmpeg:
        raise SystemExit("ffmpeg not found: set FFMPEG_EXE or put it on PATH (winget install Gyan.FFmpeg)")
    if not Path(model).exists():
        raise SystemExit(f"voice model not found: {model}")
    return piper, model, ffmpeg


def render_one(line: dict, spoken: str, role: dict, cfg: dict, piper: str, model: str, ffmpeg: str, out: Path) -> float:
    """Piper speaks the words once (kept in build/voice/raw, named by the words), then ffmpeg shapes and normalises it; changing a role's pitch or the loudness never needs Piper again."""
    rate = int(load(Path(model + ".json"))["audio"]["sample_rate"])
    raw = OUT / "raw" / (hashlib.sha256((cfg["model"] + "\n" + spoken).encode("utf-8")).hexdigest()[:20] + ".wav")
    if not raw.exists():
        raw.parent.mkdir(parents=True, exist_ok=True)
        tmp = raw.with_suffix(".part")
        subprocess.run([piper, "--model", model, "--output_file", str(tmp)], input=spoken.encode("utf-8"), capture_output=True, check=True)
        tmp.replace(raw)
    shape = audio_filter(role, rate)
    # two passes: measure the integrated loudness of the shaped line, then apply exactly the gain that reaches the target and a limiter below the true-peak ceiling
    # (ffmpeg's one-step loudnorm leaves short lines up to 3 LU quiet because its gating cannot judge them)
    first = subprocess.run([ffmpeg, "-nostats", "-i", str(raw), "-af", shape + ",loudnorm=print_format=json", "-f", "null", "-"], capture_output=True, text=True, check=True)
    m, _ = json.JSONDecoder().raw_decode(first.stderr[first.stderr.rindex("{"):])
    gain = cfg["target_lufs"] - float(m["input_i"])
    ceiling = 10 ** (cfg["true_peak_db"] / 20)
    subprocess.run([ffmpeg, "-y", "-v", "error", "-i", str(raw), "-af", f"{shape},volume={gain:.2f}dB,alimiter=limit={ceiling:.3f}:level=disabled",
                    "-ar", "44100", "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", str(out)], capture_output=True, check=True)
    return duration(out, ffmpeg)


def duration(path: Path, ffmpeg: str) -> float:
    probe = subprocess.run([ffmpeg, "-i", str(path), "-f", "null", "-"], capture_output=True, text=True)
    m = re.findall(r"time=(\d+):(\d+):([\d.]+)", probe.stderr)
    if not m:
        raise RuntimeError(f"cannot read the length of {path}")
    h, mi, s = m[-1]
    return int(h) * 3600 + int(mi) * 60 + float(s)


def loudness(path: Path, ffmpeg: str) -> float:
    probe = subprocess.run([ffmpeg, "-nostats", "-i", str(path), "-af", "ebur128=framelog=quiet", "-f", "null", "-"], capture_output=True, text=True)
    m = re.findall(r"I:\s+(-?[\d.]+) LUFS", probe.stderr)
    return float(m[-1]) if m else float("nan")


def plan(catalogue: dict, cfg: dict, words: dict[str, str], index: dict) -> tuple[list[tuple[dict, str, dict, str]], list[dict]]:
    """The lines to render (new or changed) and the templates left for live speech."""
    todo, templates = [], []
    for line in catalogue["lines"]:
        if is_gap(line):
            continue
        if is_template(line):
            templates.append(line)
            continue
        role = cfg["roles"][role_of(line)]
        spoken = respell(line["text"], words)
        h = line_hash(spoken, role, cfg["model"])
        if index.get("lines", {}).get(line["id"], {}).get("hash") != h or not (OUT / f"{line['id']}.ogg").exists():
            todo.append((line, spoken, role, h))
    return todo, templates


def render(only: list[str]) -> int:
    cfg, cat, words = load(VOICES), load(CATALOGUE), load(PRONUNCIATIONS)["words"]
    OUT.mkdir(parents=True, exist_ok=True)
    index = load(INDEX) if INDEX.exists() else {"lines": {}}
    todo, templates = plan(cat, cfg, words, index)
    if only:
        todo = [t for t in todo if t[0]["id"] in only]
    piper, model, ffmpeg = tools()
    done = 0

    def work(item):
        line, spoken, role, h = item
        secs = render_one(line, spoken, role, cfg, piper, model, ffmpeg, OUT / f"{line['id']}.ogg")
        return line["id"], {"hash": h, "role": role_of(line), "seconds": round(secs, 2), "text": line["text"]}

    with ThreadPoolExecutor(max_workers=max(1, (os.cpu_count() or 2) // 2)) as pool:
        for lid, entry in pool.map(work, todo):
            index["lines"][lid] = entry
            done += 1
    gaps = sorted(l["id"] for l in cat["lines"] if is_gap(l))
    index.update(voice=cfg["model"], target_lufs=cfg["target_lufs"], templates_left=sorted(t["id"] for t in templates), lines_without_words=gaps)
    INDEX.write_text(json.dumps(index, indent=1, ensure_ascii=False, sort_keys=True) + "\n", encoding="utf-8")
    print(f"rendered {done} line(s); {len(index['lines'])} in the index; {len(templates)} template(s) left for live speech; {len(gaps)} line(s) have no words yet (DT-178 to DT-180)")
    return 0


def check() -> int:
    cfg, cat = load(VOICES), load(CATALOGUE)
    if not INDEX.exists():
        print("FAIL  nothing rendered yet: run `task voice-render`")
        return 1
    index = load(INDEX)
    ffmpeg = tools()[2]
    problems = []
    plain = [l for l in cat["lines"] if not is_template(l) and not is_gap(l)]
    for l in plain:
        f = OUT / f"{l['id']}.ogg"
        e = index["lines"].get(l["id"])
        if not f.exists() or e is None:
            problems.append(f"{l['id']}: no audio file")
            continue
        if not MIN_SECONDS <= e["seconds"] <= MAX_SECONDS:
            problems.append(f"{l['id']}: {e['seconds']} s is outside {MIN_SECONDS} to {MAX_SECONDS} s")
        lufs = loudness(f, ffmpeg)
        if not abs(lufs - cfg["target_lufs"]) <= LOUDNESS_TOLERANCE:
            problems.append(f"{l['id']}: {lufs} LUFS, target {cfg['target_lufs']} (+/- {LOUDNESS_TOLERANCE})")
    for p in problems[:30]:
        print("FAIL  " + p)
    if not problems:
        print(f"PASS  {len(plain)} lines rendered, {sum(e['seconds'] for e in index['lines'].values()):.0f} s of speech, all within {LOUDNESS_TOLERANCE} LU of {cfg['target_lufs']} LUFS; {len(index['templates_left'])} templates left for live speech; {len(index['lines_without_words'])} lines have no words yet")
    return 1 if problems else 0


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--check" in args:
        sys.exit(check())
    only = args[args.index("--only") + 1:] if "--only" in args else []
    sys.exit(render(only))
