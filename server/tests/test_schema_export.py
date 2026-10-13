"""The published session-file schema is generated from the code that enforces it."""
import json
from pathlib import Path

from server.scripts import check_session_file, export_schemas
from server.tests.files import GOLDEN


def test_the_committed_schema_is_what_the_models_produce():
    committed = export_schemas.TARGET.read_text(encoding="utf-8").replace("\r\n", "\n")
    assert committed == export_schemas.text(), "run `uv run --project server python -m server.scripts.export_schemas`"


def test_the_schema_has_both_shapes_and_forbids_unknown_fields():
    doc = json.loads(export_schemas.text())
    assert set(doc["$defs"]) == {"header", "entry"}
    assert doc["$defs"]["header"]["additionalProperties"] is False and doc["$defs"]["entry"]["additionalProperties"] is False


def test_the_check_tool_reads_what_the_engine_wrote_and_says_what_is_wrong_with_the_rest(tmp_path, capsys):
    assert check_session_file.main([str(GOLDEN), "--v1"]) == 0
    assert "78 actions" in capsys.readouterr().out
    assert check_session_file.main([str(GOLDEN)]) == 1   # version 2 is required without --v1
    assert "HEADER_NEEDS_VERSION_2" in capsys.readouterr().out
    broken = tmp_path / "broken.ndjson"
    broken.write_text(GOLDEN.read_text(encoding="utf-8")[:-40], encoding="utf-8")
    assert check_session_file.main([str(broken), "--v1"]) == 1
    assert check_session_file.main([]) == 2
