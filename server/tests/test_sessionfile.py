"""Reading a session file (no database). The real file the engine writes is the test: if the engine's format drifts, these fail."""
import hashlib
import json

import pytest

from server.sessionfile import MAX_ENTRIES, SessionFileError, parse_session_file
from server.tests.files import changed_log, golden_log_lines, golden_text, make_file


def test_the_file_the_engine_writes_today_is_read_as_version_1():
    parsed = parse_session_file(golden_text(), require_v2=False)
    assert parsed.is_v2 is False
    assert parsed.header.case == "VAR-S1-A" and parsed.header.mode == "practice" and parsed.header.seed == "42"
    assert len(parsed.entries) == 78 and parsed.entries[0].verb == "introduce_self"
    assert hashlib.sha256(parsed.log_text.encode()).hexdigest() == parsed.header.log_hash


def test_a_version_1_file_is_refused_when_version_2_is_required():
    with pytest.raises(SessionFileError) as e:
        parse_session_file(golden_text())
    assert e.value.code == "HEADER_NEEDS_VERSION_2" and e.value.line == 1


def test_a_version_2_file_is_read():
    parsed = parse_session_file(make_file())
    assert parsed.is_v2 and parsed.header.session_id is not None and parsed.header.scenario_package_hash == ""


def test_a_header_with_only_some_of_the_version_2_fields_is_refused():
    text = make_file(v2=False, header_extra={"session_id": "5b1d4a52-8a3e-4c2f-9c53-1c0f1f6e9d11"})
    with pytest.raises(SessionFileError) as e:
        parse_session_file(text, require_v2=False)
    assert e.value.code == "HEADER_PARTLY_VERSION_2"


def test_windows_line_endings_are_tolerated():
    parsed = parse_session_file(make_file().replace("\n", "\r\n"))
    assert len(parsed.entries) == 78


def test_the_log_must_match_the_hash_in_the_header():
    text = make_file()
    cut = text.rstrip("\n").rsplit("\n", 1)[0] + "\n"   # the last action is missing: a partial upload
    with pytest.raises(SessionFileError) as e:
        parse_session_file(cut)
    assert e.value.code == "LOG_HASH_MISMATCH"


def test_a_bad_line_is_named_by_its_line_number_and_its_content_is_not_repeated():
    lines = golden_log_lines()
    lines[9] = '{"seq":8,"step":99,"verb":"ask","target":"","channel":"cli","params":{},"accepted":true,"reject":"","effects":[],"secret_note":"do-not-echo-this"}'
    with pytest.raises(SessionFileError) as e:
        parse_session_file(make_file(lines=lines))
    assert e.value.code == "LINE_INVALID" and e.value.line == 11   # line 1 is the header, so the 10th action is line 11
    assert "do-not-echo-this" not in str(e.value) + e.value.detail
    with pytest.raises(SessionFileError) as e:
        parse_session_file(make_file(lines=golden_log_lines()[:3] + ["this is not json"]))
    assert e.value.code == "LINE_NOT_JSON" and e.value.line == 5


@pytest.mark.parametrize("edit,code", [
    (lambda es: es[5].update(seq=7), "LOG_NOT_SEQUENTIAL"),
    (lambda es: es[5].update(step=0), "LOG_TIME_GOES_BACKWARDS"),
    (lambda es: es[0].update(channel="telepathy"), "LINE_INVALID"),
    (lambda es: es[0].update(verb="Ask-Now"), "LINE_INVALID"),
    (lambda es: es[0].update(params={"k": 5}), "LINE_INVALID"),
    (lambda es: es[0].pop("effects"), "LINE_INVALID"),
])
def test_a_log_that_is_not_what_the_engine_writes_is_refused(edit, code):
    with pytest.raises(SessionFileError) as e:
        parse_session_file(make_file(lines=changed_log(edit)))
    assert e.value.code == code


@pytest.mark.parametrize("extra", [{"mode": "exam"}, {"case": "bad case!"}, {"seed": "forty-two"}, {"log_hash": "abc"}, {"scenario_package_hash": "xyz"}, {"unexpected": 1}])
def test_a_bad_header_is_refused(extra):
    with pytest.raises(SessionFileError) as e:
        parse_session_file(make_file(header_extra=extra))
    assert e.value.code == "HEADER_INVALID" and e.value.line == 1


def test_a_file_with_no_actions_or_no_header_is_refused():
    for text, code in (("", "EMPTY_FILE"), ("not json\n", "HEADER_NOT_JSON"), ('{"nothing":{}}\n', "HEADER_SHAPE"), (make_file(lines=[]), "NO_ENTRIES")):
        with pytest.raises(SessionFileError) as e:
            parse_session_file(text)
        assert e.value.code == code, text[:30]


def test_too_many_actions_are_refused():
    line = json.dumps({"seq": 0, "step": 0, "verb": "wait", "target": "", "channel": "cli", "params": {}, "accepted": True, "reject": "", "effects": []}, separators=(",", ":"))
    lines = [json.dumps({**json.loads(line), "seq": i}, separators=(",", ":")) for i in range(MAX_ENTRIES + 1)]
    with pytest.raises(SessionFileError) as e:
        parse_session_file(make_file(lines=lines))
    assert e.value.code == "TOO_MANY_ENTRIES"
