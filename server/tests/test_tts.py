"""Live speech (p1-voice-input-s04): sentence splitting, the cache, the same roles as the offline voices, the streaming route and its fallback; and, where Piper is installed, the real first-sentence latency."""
from __future__ import annotations

import base64
import json
import time

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server.middleware import audit
from server.models.ai_call import AiCall
from server.repositories import accounts
from server.routers import tts as tts_router
from server.tts.engine import TtsEngine, split_sentences


def fake_engine(log=None):
    def synth(text, settings):
        if log is not None:
            log.append(text)
        return f"OGG[{text}|{settings['pitch']}]".encode()
    return TtsEngine(synth=synth)


def test_text_is_split_into_sentences_for_streaming():
    assert split_sentences("I take one tablet. It is for my blood pressure!  Is that all?") == ["I take one tablet.", "It is for my blood pressure!", "Is that all?"]
    assert split_sentences("No full stop") == ["No full stop"]
    assert split_sentences("   ") == []


def test_the_roles_are_the_offline_voices_and_the_first_sentence_comes_first():
    log = []
    e = fake_engine(log)
    assert e.roles() == sorted(json.loads((tts_router.__file__ and __import__("pathlib").Path(__file__).resolve().parents[2] / "tools" / "voice" / "voices.json").read_text(encoding="utf-8"))["roles"])
    it = e.speak("First one. Second one.", "patient_male")
    s, a = next(it)
    assert (s, log) == ("First one.", ["First one."])                    # the second sentence has not been made yet
    assert b"0.84" in a                                                  # the pitch of the male patient role
    assert [x[0] for x in it] == ["Second one."]


def test_repeated_lines_come_from_the_cache_and_a_different_role_does_not():
    log = []
    e = fake_engine(log)
    list(e.speak("Same line.", "nurse"))
    list(e.speak("Same line.", "nurse"))
    list(e.speak("Same line.", "surgeon"))
    assert log == ["Same line.", "Same line."] and (e.hits, e.misses) == (1, 2)


def test_an_unknown_role_and_a_too_long_line_are_refused():
    e = fake_engine()
    with pytest.raises(KeyError):
        list(e.speak("x.", "dragon"))
    with pytest.raises(ValueError):
        list(e.speak("x" * 401, "nurse"))


def test_without_piper_the_engine_says_so(monkeypatch):
    monkeypatch.setenv("PIPER_EXE", "")
    e = TtsEngine()
    e.piper = None
    assert e.available() is False


pytestmark_db = pytest.mark.db


@pytest.fixture
def world(api):
    def build(sess: DbSession):
        org = accounts.create_organisation(sess, "Alpha", "alpha-org")
        me = api.person(sess)
        accounts.add_membership(sess, me, org.id, "trainee")
        return org.id, me
    org, me = api.seed(build)
    api.client.app.dependency_overrides[tts_router.get_tts] = lambda: fake_engine()

    class W:
        pass
    w = W()
    w.api, w.org, w.me = api, org, me
    w.post = lambda body: api.client.post(f"/tts/organisations/{org}/speak", json=body, headers=api.headers(me))
    yield w
    api.client.app.dependency_overrides.clear()


@pytest.mark.db
def test_the_route_streams_one_json_line_per_sentence_and_audits_hashes(world):
    r = world.post({"text": "First one. Second one.", "role": "nurse"})
    assert r.status_code == 200 and r.headers["content-type"].startswith("application/x-ndjson")
    lines = [json.loads(x) for x in r.text.strip().splitlines()]
    assert [x["text"] for x in lines] == ["First one.", "Second one."]
    assert base64.b64decode(lines[0]["audio_b64"]).startswith(b"OGG[First one.")
    assert audit.get_writer().flush(10)
    with DbSession(world.api.engine) as s:
        (row,) = list(s.scalars(select(AiCall)))
    assert (row.task, row.model, row.context_id, row.validation) == ("tts", "piper", "nurse", "skipped") and "First" not in row.output_ref


@pytest.mark.db
def test_the_route_refuses_unknown_roles_and_empty_text_and_gives_the_fallback_without_a_voice(world):
    assert world.post({"text": "Hello.", "role": "dragon"}).status_code == 422
    assert world.post({"text": "", "role": "nurse"}).status_code == 422
    e = fake_engine()
    e._synth = None
    e.piper = None
    world.api.client.app.dependency_overrides[tts_router.get_tts] = lambda: e
    r = world.post({"text": "Hello.", "role": "nurse"})
    assert (r.status_code, r.json()["fallback"], r.json()["error"]) == (200, True, "TTS_UNAVAILABLE")


def test_with_the_real_voice_the_first_sentence_is_ready_within_the_budget():
    e = TtsEngine()
    if not e.available():
        pytest.skip("Piper, the voice or ffmpeg is not installed on this machine")
    t = time.monotonic()
    sentence, audio = next(e.speak("I take one tablet every morning. It is for my blood pressure.", "patient_male"))
    first = time.monotonic() - t
    assert sentence == "I take one tablet every morning." and audio[:4] == b"OggS"
    print(f"first sentence ready in {first:.2f} s")
    import os
    if os.environ.get("TTS_STRICT"):                                      # on a quiet machine: TTS_STRICT=1 holds it to the 2.5 s after which the client shows subtitles
        assert first < 2.5, first


def test_piper_writes_to_a_file_and_never_to_standard_output(tmp_path, monkeypatch):
    """On Windows Piper's WAV through the text-mode standard output has every 0x0A byte turned into 0x0D 0x0A and the speech is garbled (the recogniser heard 'I love you'): the engine must give Piper a file."""
    import subprocess
    from pathlib import Path

    (tmp_path / "voice.onnx.json").write_text(json.dumps({"audio": {"sample_rate": 22050}}), encoding="utf-8")
    monkeypatch.setenv("PIPER_PERSISTENT", "0")                                  # the one-shot path; the kept-alive process has its own test
    e = TtsEngine()
    e.piper, e.model, e.ffmpeg = "piper", str(tmp_path / "voice.onnx"), "ffmpeg"
    calls = []

    def fake_run(cmd, **kw):
        calls.append(cmd)
        if cmd[0] == "piper":
            out = cmd[cmd.index("--output_file") + 1]
            assert out != "-", "Piper must not write the WAV to standard output"
            Path(out).write_bytes(b"RIFF" + bytes([10, 13, 10]) * 20)             # bytes a text-mode pipe would have corrupted
            return subprocess.CompletedProcess(cmd, 0, stdout=b"", stderr=b"")
        assert kw["input"] == b"RIFF" + bytes([10, 13, 10]) * 20                  # ffmpeg received the file's bytes unchanged
        return subprocess.CompletedProcess(cmd, 0, stdout=b"OGG", stderr=b"")

    monkeypatch.setattr("server.tts.engine.subprocess.run", fake_run)
    audio, cached = e.one("Hello there.", "patient_male")
    assert (audio, cached) == (b"OGG", False)
    assert [c[0] for c in calls] == ["piper", "ffmpeg"]


FAKE_PIPER = '''
import os, sys
out = sys.argv[sys.argv.index("--output_dir") + 1]
die_after = int(os.environ.get("FAKE_PIPER_DIE_AFTER", "0"))
n = 0
for line in sys.stdin:
    n += 1
    path = os.path.join(out, f"{os.getpid()}-{n}.wav")
    with open(path, "wb") as f:
        f.write(("RIFF" + line.strip() + "|pid" + str(os.getpid())).encode())
    print(path, flush=True)
    if die_after and n >= die_after:
        sys.exit(0)
'''


def test_one_piper_process_serves_many_sentences_and_is_restarted_when_it_dies(tmp_path, monkeypatch):
    import sys
    from server.tts.engine import PersistentPiper

    script = tmp_path / "fake_piper.py"
    script.write_text(FAKE_PIPER, encoding="utf-8")
    p = PersistentPiper([sys.executable, str(script)], "voice.onnx", timeout=20)
    try:
        a, b = p.synth("First sentence."), p.synth("Second   sentence.")
        pid = lambda audio: audio.decode().split("|pid")[1]            # noqa: E731
        assert a.startswith(b"RIFFFirst sentence.") and b.startswith(b"RIFFSecond sentence.")            # one line per sentence, spaces collapsed
        assert pid(a) == pid(b)                                         # the same process answered both: the voice is loaded once
        assert list(__import__("pathlib").Path(p._dir.name).iterdir()) == []      # the files are removed after they are read
        p._proc.kill()                                                  # the process dies: the next sentence starts it again
        p._proc.wait()
        c = p.synth("Third.")
        assert c.startswith(b"RIFFThird.") and pid(c) != pid(a)
    finally:
        p.close()
