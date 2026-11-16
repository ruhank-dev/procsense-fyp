"""Renders the voice-feedback phrases and the listening cues into the Unity project (P3-SND-08).

    uv run --project tools tools/voice/render_feedback.py            # render what is new or changed, write the clip paths into content/ui/voice_feedback.json
    uv run --project tools tools/voice/render_feedback.py --check    # every fixed phrase has its clips and none is stale (no Piper or ffmpeg needed)

The phrase table is content/ui/voice_feedback.json (P3-VOX-07). Each phrase without a {placeholder} is spoken by the offline voice in the team role
(tools/voice/render.py: Piper, pitch and pace from voices.json, -16 LUFS, Ogg Vorbis): the phrase itself (`clip`) and, when the mode adds a sentence, the
phrase with that sentence (`practice_clip`, `assessment_clip`), so what is heard always equals the subtitle. A phrase with a placeholder ({reason_text},
{action_text}) is only known in the session and stays live speech. The two listening cues (start and stop of the push-to-talk recording) are short tones
made with ffmpeg, no recording or download.

Unlike the catalogue lines (build/voice, git-ignored), these clips are COMMITTED: they are a few seconds each, every build needs them, and the validator
(tools/content/validate_ui_guides.py) checks their paths. They sit in a Resources folder named Voice, so TeamConversation.SayFeedback loads them by file
name like the catalogue lines. clips.json beside them records the words of each clip; --check (run by the validator) fails when a phrase changed and its
clip was not rendered again.
"""
from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import render  # noqa: E402  (same folder: the offline voice pipeline)

ROOT = render.ROOT
TABLE = ROOT / "content" / "ui" / "voice_feedback.json"
CLIPS = ROOT / "unity" / "Assets" / "Audio" / "VoiceFeedback" / "Resources" / "Voice"
CUES = ROOT / "unity" / "Assets" / "Audio" / "VoiceFeedback" / "Resources" / "VoiceCues"
INDEX = CLIPS.parent.parent / "clips.json"
ROLE = "team"
MODES = (("clip", None), ("practice_clip", "practice_extra"), ("assessment_clip", "assessment_extra"))
# the cues: two short rising tones when listening starts, two falling when the recording is sent (frequencies in Hz, 70 ms each, 20 ms gap)
CUE_TONES = {"listen_start": (660, 880), "listen_stop": (880, 660)}
CUE_DB = -20


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def wanted(table: dict) -> dict[str, tuple[dict, str, str]]:
    """Clip file name -> (outcome, the field it fills, the words spoken)."""
    out = {}
    for o in table["outcomes"]:
        if render.PLACEHOLDER.search(o["spoken"]):
            continue
        for field, extra in MODES:
            words = o["spoken"].strip()
            if extra:
                if not o.get(extra, "").strip():
                    continue
                words = words + " " + o[extra].strip()
            suffix = "" if extra is None else "-" + extra.split("_")[0]
            out[f"VF-{o['code']}{suffix}"] = (o, field, words)
    return out


def words_hash(words: str) -> str:
    return hashlib.sha256(words.encode("utf-8")).hexdigest()[:16]


def problems() -> list[str]:
    """What --check reports: a fixed phrase without its clip, a clip whose words changed, a path in the table that does not match."""
    table = render.load(TABLE)
    index = render.load(INDEX) if INDEX.exists() else {"clips": {}}
    p = []
    want = wanted(table)
    for name, (o, field, words) in want.items():
        path = CLIPS / f"{name}.ogg"
        if not path.exists():
            p.append(f"{o['code']}: {rel(path)} is missing (run task voice-feedback)")
        elif index["clips"].get(name) != words_hash(words):
            p.append(f"{o['code']}: the words of {name} changed since it was rendered (run task voice-feedback)")
        if o.get(field) != rel(path):
            p.append(f"{o['code']}: {field} should be {rel(path)}")
    for o in table["outcomes"]:
        for field, _ in MODES:
            if o.get(field) and Path(o[field]).stem not in want:
                p.append(f"{o['code']}: {field} {o[field]} belongs to no fixed phrase")
    for cue in CUE_TONES:
        if not (CUES / f"{cue}.ogg").exists():
            p.append(f"cue {cue} is missing (run task voice-feedback)")
    return p


def make_cue(ffmpeg: str, name: str, tones: tuple[int, int]) -> None:
    a, b = tones
    graph = (f"sine=f={a}:d=0.07[x];anullsrc=r=44100:cl=mono,atrim=0:0.02[g];sine=f={b}:d=0.07[y];"
             f"[x][g][y]concat=n=3:v=0:a=1,afade=t=in:d=0.005,afade=t=out:st=0.155:d=0.005,volume={CUE_DB}dB")
    subprocess.run([ffmpeg, "-y", "-v", "error", "-filter_complex", graph, "-ar", "44100", "-ac", "1", "-c:a", "libvorbis", "-q:a", "4",
                    str(CUES / f"{name}.ogg")], capture_output=True, check=True)


def main(argv: list[str]) -> int:
    if "--check" in argv:
        p = problems()
        for x in p:
            print("FAIL  " + x)
        if not p:
            print(f"PASS  {len(wanted(render.load(TABLE)))} voice-feedback clips and {len(CUE_TONES)} cues present and current")
        return 1 if p else 0
    cfg, words_map = render.load(render.VOICES), render.load(render.PRONUNCIATIONS)["words"]
    table = render.load(TABLE)
    index = render.load(INDEX) if INDEX.exists() else {"clips": {}}
    piper, model, ffmpeg = render.tools()
    CLIPS.mkdir(parents=True, exist_ok=True)
    CUES.mkdir(parents=True, exist_ok=True)
    want = wanted(table)
    done = 0
    for name, (o, field, words) in want.items():
        path = CLIPS / f"{name}.ogg"
        if index["clips"].get(name) != words_hash(words) or not path.exists():
            render.render_one({"id": name}, render.respell(words, words_map), cfg["roles"][ROLE], cfg, piper, model, ffmpeg, path)
            index["clips"][name] = words_hash(words)
            done += 1
        o[field] = rel(path)
    for name in [n for n in index["clips"] if n not in want]:          # a phrase that became a template or lost its extra sentence
        (CLIPS / f"{name}.ogg").unlink(missing_ok=True)
        del index["clips"][name]
    for o in table["outcomes"]:
        for field, _ in MODES:
            if o.get(field) and Path(o[field]).stem not in want:
                o[field] = None
    for cue, tones in CUE_TONES.items():
        make_cue(ffmpeg, cue, tones)
    index.update(voice=cfg["model"], role=ROLE, target_lufs=cfg["target_lufs"])
    INDEX.write_text(json.dumps(index, indent=1, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    TABLE.write_text(json.dumps(table, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"rendered {done} voice-feedback clip(s), {len(want)} in all; {len(CUE_TONES)} cues")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
