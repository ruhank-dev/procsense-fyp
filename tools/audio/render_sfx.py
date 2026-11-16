"""P3-SND-02: the ProcSense sound list, synthesised here (own work, no third-party audio, nothing downloaded).

    uv run --project tools tools/audio/render_sfx.py           # writes the clips and content/audio/sound_list.json
    uv run --project tools tools/audio/render_sfx.py --check   # fails if a clip or the list is missing or stale

Every sound of the plan's list (docs/plans/p3/p3-14-sound-design.md, section 5) has one row: id, group, trigger, the file (or the existing runtime synthesis that already makes
it: ToneBank, the voice cues of P3-SND-08), its source and licence. Clips are mono 16-bit WAV, made from seeded noise and sine partials in plain Python, so the same script always gives the
same bytes. They are serviceable placeholders: a recorded or licensed sound can replace any row later by changing its source and licence (downloads need the owner's approval).
"""
from __future__ import annotations

import hashlib
import json
import math
import random
import struct
import sys
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "unity" / "Assets" / "Audio" / "Sfx" / "Resources" / "Sfx"
LIST = ROOT / "content" / "audio" / "sound_list.json"
SOURCE = "synthesised by tools/audio/render_sfx.py"
LICENCE = "own work (ProcSense project licence)"
SR = 22050

# ------------------------------------------------------------------ small DSP in plain Python


def n(seconds: float) -> int:
    return int(seconds * SR)


def silence(seconds: float) -> list[float]:
    return [0.0] * n(seconds)


def env(x: list[float], attack: float, release: float, hold_curve: float = 1.0) -> list[float]:
    a, r, total = max(1, n(attack)), max(1, n(release)), len(x)
    out = []
    for i, v in enumerate(x):
        g = min(1.0, i / a) * min(1.0, (total - i) / r)
        out.append(v * g ** hold_curve)
    return out


def decay(x: list[float], seconds: float) -> list[float]:
    k = math.exp(-1.0 / max(1, n(seconds)))
    g, out = 1.0, []
    for v in x:
        out.append(v * g)
        g *= k
    return out


def tone(freq: float, seconds: float, partials: tuple[float, ...] = (1.0,), glide: float = 0.0) -> list[float]:
    out, phase = [], [0.0] * len(partials)
    total = n(seconds)
    for i in range(total):
        f = freq * (1 + glide * i / max(1, total))
        s = 0.0
        for k, amp in enumerate(partials):
            phase[k] += 2 * math.pi * f * (k + 1) / SR
            s += amp * math.sin(phase[k])
        out.append(s)
    return out


def noise(seconds: float, rng: random.Random) -> list[float]:
    return [rng.uniform(-1, 1) for _ in range(n(seconds))]


def lowpass(x: list[float], cutoff: float) -> list[float]:
    a = 1 - math.exp(-2 * math.pi * cutoff / SR)
    y, out = 0.0, []
    for v in x:
        y += a * (v - y)
        out.append(y)
    return out


def highpass(x: list[float], cutoff: float) -> list[float]:
    low = lowpass(x, cutoff)
    return [v - l for v, l in zip(x, low)]


def band(x: list[float], lo: float, hi: float) -> list[float]:
    return highpass(lowpass(x, hi), lo)


def brown(seconds: float, rng: random.Random) -> list[float]:
    y, out = 0.0, []
    for _ in range(n(seconds)):
        y = (y + rng.uniform(-1, 1) * 0.02) * 0.998
        out.append(y)
    return out


def mix(*parts: tuple[list[float], float, float]) -> list[float]:
    """(signal, gain, start seconds) ..."""
    length = max(n(start) + len(s) for s, _, start in parts)
    out = [0.0] * length
    for s, gain, start in parts:
        o = n(start)
        for i, v in enumerate(s):
            out[o + i] += v * gain
    return out


def cat(*parts: list[float]) -> list[float]:
    out: list[float] = []
    for p in parts:
        out.extend(p)
    return out


def loop(x: list[float], fade: float = 1.0) -> list[float]:
    """Seamless loop: the tail is crossfaded into the head."""
    f = n(fade)
    head, body, tail = x[:f], x[f:-f], x[-f:]
    return [*[(t * (1 - i / f) + h * (i / f)) for i, (h, t) in enumerate(zip(head, tail))], *body]


def normal(x: list[float], peak: float) -> list[float]:
    m = max(1e-9, max(abs(v) for v in x))
    return [v * peak / m for v in x]


def note(name: str) -> float:
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    semis = names[name[0]] + (1 if "#" in name else 0) + 12 * (int(name[-1]) - 4)
    return 440.0 * 2 ** (semis / 12)


def chime(freqs: list[float], step: float, ring: float, bright: tuple[float, ...] = (1, .3, .12)) -> list[float]:
    return mix(*[(decay(env(tone(f, ring, bright), .005, .05), ring / 4), 1.0, i * step) for i, f in enumerate(freqs)])


def blip(freq: float, seconds: float, glide: float = 0.0, partials: tuple[float, ...] = (1, .2)) -> list[float]:
    return env(tone(freq, seconds, partials, glide), .004, seconds * .6)


def click(rng: random.Random, seconds: float = .012, lo: float = 1500, hi: float = 6000) -> list[float]:
    return decay(band(noise(seconds, rng), lo, hi), seconds / 3)


# ------------------------------------------------------------------ the sounds


def pad_chord(freqs: list[float], seconds: float) -> list[float]:
    return env(mix(*[(lowpass(tone(f, seconds, (1, .25, .08)), 1800), 1.0, 0) for f in freqs]), seconds * .35, seconds * .35)


def menu_theme(rng: random.Random) -> list[float]:
    chords = [["C3", "E4", "G4", "B4"], ["A2", "C4", "E4", "G4"], ["F2", "A3", "C4", "E4"], ["G2", "B3", "D4", "E4"]]
    bars = [pad_chord([note(x) for x in c], 4.6) for c in chords]
    melody = ["E5", "G5", "D5", "C5", "E5", "A4", "C5", "B4"]
    bells = mix(*[(decay(env(tone(note(m), 1.6, (1, .2)), .01, .3), .6), .35, 1.0 + i * 2.0) for i, m in enumerate(melody)])
    body = mix(*[(b, .5, i * 4.0) for i, b in enumerate(bars)], (bells, 1.0, 0))
    return loop(body[: n(16.6)], .6)


def room(rng: random.Random, seconds: float, rumble: float, air: float, hum: float = 0.0, events: list[tuple[list[float], float, float]] | None = None) -> list[float]:
    bed = mix((lowpass(brown(seconds + 1.5, rng), 400), rumble, 0), (band(noise(seconds + 1.5, rng), 300, 2500), air, 0))
    if hum:
        bed = mix((bed, 1.0, 0), (tone(50, seconds + 1.5, (0, .6, .3, .2)), hum, 0))
    if events:
        bed = mix((bed, 1.0, 0), *events)
    return loop(bed[: n(seconds + 1.5)], 1.5)


def distant(rng: random.Random, seconds: float, count: int, maker) -> list[tuple[list[float], float, float]]:
    return [(maker(rng), rng.uniform(.08, .2), rng.uniform(0, seconds - 1)) for _ in range(count)]


def murmur(rng: random.Random) -> list[float]:
    """A distant voice: formant-ish band noise shaped into syllables."""
    syll = []
    for _ in range(rng.randint(3, 7)):
        s = band(noise(rng.uniform(.08, .2), rng), rng.uniform(250, 400), rng.uniform(900, 1400))
        syll.append(env(s, .02, .04))
        syll.append(silence(rng.uniform(.03, .12)))
    return lowpass(cat(*syll), 900)


def wheels(rng: random.Random, seconds: float = 1.6) -> list[float]:
    rattle = [v * (0.6 + 0.4 * math.sin(2 * math.pi * 11 * i / SR)) for i, v in enumerate(band(noise(seconds, rng), 120, 900))]
    return env(rattle, seconds * .3, seconds * .4)


def step(rng: random.Random, bright: float) -> list[float]:
    thud = decay(lowpass(noise(.09, rng), 300), .02)
    scuff = decay(band(noise(.06, rng), 1200, 1200 + 4000 * bright), .012)
    return mix((thud, 1.0, 0), (scuff, .35 + .4 * bright, .005))


def footsteps(rng: random.Random, bright: float) -> list[float]:
    return mix(*[(step(rng, bright), rng.uniform(.8, 1.0), i * .52 + rng.uniform(-.02, .02)) for i in range(4)])


def rustle(rng: random.Random, seconds: float, lo: float, hi: float) -> list[float]:
    x = band(noise(seconds, rng), lo, hi)
    shape = lowpass([abs(v) for v in noise(seconds, rng)], 8)
    return env([a * b * 3 for a, b in zip(x, shape)], .05, .1)


def door(rng: random.Random, opening: bool) -> list[float]:
    latch = click(rng, .02, 800, 4000)
    swing = env(band(noise(.6, rng), 80, 500), .1, .3)
    thump = decay(lowpass(noise(.12, rng), 200), .04)
    return mix((latch, .8, 0), (swing, .5, .03)) if opening else mix((swing, .4, 0), (thump, 1.0, .45), (latch, .7, .47))


def motor(rng: random.Random, seconds: float, freq: float) -> list[float]:
    whine = tone(freq, seconds, (1, .5, .25, .12))
    return env(mix((whine, .3, 0), (band(noise(seconds, rng), 200, 1500), .3, 0)), .15, .2)


RECIPES: dict[str, tuple[str, str, object]] = {
    # id: (group, trigger, maker)
    "music_menu_loop": ("Music", "MainMenu state (loops)", menu_theme),
    "sting_pass": ("Music", "Results: passed", lambda r: chime([note(x) for x in ("C5", "E5", "G5", "C6")], .12, 1.4)),
    "sting_not_pass": ("Music", "Results: not passed", lambda r: chime([note("E4"), note("C4")], .3, 1.6, (1, .2))),
    "sting_not_completed": ("Music", "Results: not completed", lambda r: pad_chord([note(x) for x in ("D4", "F4", "A4")], 1.8)),
    "amb_ward": ("Ambience", "Patient bay entry (crossfade 1.5 s)", lambda r: room(r, 12, .5, .05, 0, distant(r, 12, 4, murmur) + distant(r, 12, 1, wheels))),
    "amb_ed": ("Ambience", "Emergency department entry", lambda r: room(r, 12, .5, .08, 0, distant(r, 12, 9, murmur) + distant(r, 12, 3, wheels))),
    "amb_corridor": ("Ambience", "Corridor entry", lambda r: room(r, 12, .45, .06, 0, distant(r, 12, 3, murmur) + distant(r, 12, 2, lambda q: footsteps(q, .5)))),
    "amb_ot": ("Ambience", "Theatre entry", lambda r: room(r, 12, .7, .12, .05)),
    "amb_menu": ("Ambience", "Menu room tone", lambda r: room(r, 12, .35, .02)),
    "ui_hover": ("UI", "Kit hover", lambda r: blip(2400, .03, 0, (1,))),
    "ui_select": ("UI", "Kit press", lambda r: mix((blip(1200, .05), 1, 0), (blip(1800, .05), .6, .03))),
    "ui_back": ("UI", "Back / cancel", lambda r: mix((blip(1500, .05), 1, 0), (blip(1000, .06), .7, .04))),
    "ui_open": ("UI", "Glass panel opens", lambda r: env(band(noise(.18, r), 1500, 6000), .12, .05)),
    "ui_close": ("UI", "Glass panel closes", lambda r: env(band(noise(.14, r), 1000, 4000), .02, .1)),
    "ui_toggle": ("UI", "Toggle", lambda r: mix((click(r), 1, 0), (blip(1600, .03), .4, .005))),
    "ui_slider_tick": ("UI", "Slider step", lambda r: click(r, .008, 3000, 8000)),
    "ui_toast": ("UI", "Toast", lambda r: chime([note("A5"), note("E6")], .06, .5)),
    "ui_error": ("UI", "Refused action (shake)", lambda r: mix((blip(330, .09, 0, (1, .4, .2)), 1, 0), (blip(260, .12, 0, (1, .4, .2)), 1, .1))),
    "ui_confirm": ("UI", "Dialog confirm", lambda r: chime([note("E5"), note("A5")], .07, .6)),
    "ui_submit": ("UI", "Submit pressed", lambda r: chime([note("C5"), note("G5"), note("C6")], .08, .9)),
    "trn_step_chime": ("Training", "Step complete (SimFeedback goal)", lambda r: chime([note("G5"), note("D6")], .08, .8)),
    "trn_stage_chime": ("Training", "All steps of a stage", lambda r: chime([note(x) for x in ("C5", "E5", "G5", "B5", "D6")], .07, 1.2)),
    "trn_board_open": ("Training", "Training board opens", lambda r: mix((env(band(noise(.25, r), 800, 5000), .15, .08), .7, 0), (chime([note("E5")], 0, .5), .4, .1))),
    "trn_board_close": ("Training", "Training board closes", lambda r: env(band(noise(.2, r), 600, 3000), .02, .15)),
    "exm_hint_offer": ("Examination", "Hint offered", lambda r: chime([note("A4"), note("C5")], .1, .7, (1, .15))),
    "exm_hint_reveal": ("Examination", "Hint revealed (costs points)", lambda r: chime([note("C5"), note("A4")], .1, .7, (1, .15))),
    "exm_submit_confirm": ("Examination", "Submission confirmed", lambda r: mix((chime([note("D5"), note("A5")], .1, 1.0), 1, 0), (decay(lowpass(noise(.2, r), 200), .05), .5, 0))),
    "exm_end": ("Examination", "Examination ends", lambda r: pad_chord([note(x) for x in ("C4", "G4", "C5")], 2.2)),
    "vox_processing_loop": ("Voice", "Waiting for the interpreter (loops, soft)", lambda r: loop(mix(*[(blip(880, .05, 0, (1,)), .3, i * .4) for i in range(5)], (silence(2.0), 0, 0))[: n(2.0)], .05)),
    "vox_misunderstood": ("Voice", "Speech not understood", lambda r: mix((blip(700, .08, -.15), 1, 0), (blip(560, .1, -.1), .8, .1))),
    "eq_thermometer_beep": ("Equipment", "Tympanic thermometer reading", lambda r: cat(blip(3200, .08, 0, (1,)), silence(.07), blip(3200, .08, 0, (1,)))),
    "eq_glucometer_beep": ("Equipment", "Glucometer reading", lambda r: blip(2600, .25, 0, (1,))),
    "eq_ecg_print": ("Equipment", "ECG printout", lambda r: motor(r, 1.8, 140)),
    "eq_iv_pump_alarm": ("Equipment", "IV pump alarm", lambda r: cat(*[cat(blip(1046, .15, 0, (1, .3)), silence(.1)) for _ in range(3)], silence(.6))),
    "eq_trolley_wheels": ("Equipment", "Trolley moving", lambda r: wheels(r, 2.0)),
    "eq_bed_motor": ("Equipment", "Bed height motor", lambda r: motor(r, 2.0, 95)),
    "eq_curtain_pull": ("Equipment", "Privacy curtain", lambda r: mix((rustle(r, .9, 1500, 6000), .8, 0), *[(click(r, .006, 3000, 7000), .5, .05 + i * .07) for i in range(10)])),
    "eq_door_open": ("Equipment", "Door opens", lambda r: door(r, True)),
    "eq_door_close": ("Equipment", "Door closes", lambda r: door(r, False)),
    "foley_steps_vinyl": ("Foley", "Staff and porter walking (ward vinyl)", lambda r: footsteps(r, .3)),
    "foley_steps_tile": ("Foley", "Walking on corridor and theatre tile", lambda r: footsteps(r, .9)),
    "foley_rustle_coat": ("Foley", "Nurse coat movement", lambda r: rustle(r, .7, 600, 4000)),
    "foley_rustle_scrubs": ("Foley", "Scrubs movement", lambda r: rustle(r, .6, 900, 6000)),
    "foley_clipboard": ("Foley", "Clipboard set down", lambda r: mix((decay(band(noise(.1, r), 300, 2500), .025), 1, 0), (click(r, .01, 2000, 6000), .5, .002))),
    "foley_paper": ("Foley", "Paper handled", lambda r: rustle(r, .5, 2000, 9000)),
    "foley_pen_sign": ("Foley", "Pen signing", lambda r: rustle(r, 1.1, 3000, 9000)),
    "foley_wristband_snap": ("Foley", "Wristband fastened", lambda r: mix((click(r, .015, 1500, 7000), 1, 0), (click(r, .01, 2500, 8000), .6, .05))),
    "tr_whoosh": ("Transitions", "Fade to a new case (very soft)", lambda r: env(band(noise(.7, r), 300, 2500), .35, .35)),
    "tr_loading_loop": ("Transitions", "Loading (loops)", lambda r: loop(mix(*[(chime([note("E5")], 0, .6, (1,)), .25, i * .75) for i in range(4)], (silence(3.0), 0, 0))[: n(3.0)], .1)),
    "tr_arrive": ("Transitions", "Arrived in a room", lambda r: chime([note("G4"), note("D5")], .09, .8, (1, .1))),
}

# rows made at run time by code that already exists (no file here)
EXISTING = {
    "vox_talk_start": ("Voice", "Push-to-talk opens", "VoiceCues (P3-SND-08): Resources/VoiceCues/talk_start"),
    "vox_talk_end": ("Voice", "Push-to-talk closes", "VoiceCues (P3-SND-08): Resources/VoiceCues/talk_end"),
    "eq_monitor_beats_alarms": ("Equipment", "Monitor beats and alarms", "ToneBank (runtime synthesis)"),
    "eq_cuff_inflate_deflate": ("Equipment", "Cuff pump", "ToneBank (runtime synthesis)"),
    "eq_phone_ring": ("Equipment", "Laboratory phone", "ToneBank (runtime synthesis)"),
}

PEAK = {"Music": .5, "Ambience": .35, "UI": .45, "Transitions": .3}


def wav_bytes(x: list[float]) -> bytes:
    import io
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", max(-32767, min(32767, int(v * 32767)))) for v in x))
    return buf.getvalue()


def render(sound_id: str) -> bytes:
    group, _, maker = RECIPES[sound_id]
    rng = random.Random(int(hashlib.sha256(sound_id.encode()).hexdigest()[:8], 16))
    return wav_bytes(normal(maker(rng), PEAK.get(group, .7)))


def rows(hashes: dict[str, str]) -> list[dict]:
    out = []
    for sid, (group, trigger, _) in RECIPES.items():
        out.append({"id": sid, "group": group, "trigger": trigger, "file": f"unity/Assets/Audio/Sfx/Resources/Sfx/{sid}.wav", "source": SOURCE, "licence": LICENCE, "sha256": hashes[sid]})
    for sid, (group, trigger, made) in EXISTING.items():
        out.append({"id": sid, "group": group, "trigger": trigger, "file": "", "source": made, "licence": LICENCE, "sha256": ""})
    return sorted(out, key=lambda r: (r["group"], r["id"]))


def main(argv: list[str]) -> int:
    check = "--check" in argv
    hashes, problems = {}, []
    for sid in RECIPES:
        data = render(sid)
        hashes[sid] = hashlib.sha256(data).hexdigest()
        path = OUT / f"{sid}.wav"
        if check:
            if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != hashes[sid]:
                problems.append(f"{path.relative_to(ROOT)} missing or stale")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    doc = {"schema": "procsense.sound_list/1", "note": "Generated by tools/audio/render_sfx.py (P3-SND-02); do not edit by hand.", "sounds": rows(hashes)}
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    if check:
        if not LIST.exists() or LIST.read_text(encoding="utf-8") != text:
            problems.append(f"{LIST.relative_to(ROOT)} missing or stale")
        for p in problems:
            print("FAIL ", p)
        print("ok    sound list current" if not problems else f"{len(problems)} problem(s)")
        return 1 if problems else 0
    LIST.parent.mkdir(parents=True, exist_ok=True)
    LIST.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {len(RECIPES)} clips and {LIST.relative_to(ROOT)} ({len(doc['sounds'])} sounds)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
