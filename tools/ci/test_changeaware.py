"""Tests for the change-aware selector and result cache (no Unity, no network).

    uv run --project tools tools/ci/test_changeaware.py        (also collected by pytest)
"""
from __future__ import annotations

import os
import sys
import tempfile
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import changeaware as ca  # noqa: E402
import run as runner  # noqa: E402

RULES = ca.load_rules()


def runs(files, **kw):
    v = ca.select(RULES, files, **kw)
    return {n for n, c in v["checks"].items() if c["run"]}, v


def test_glob_semantics():
    assert ca.glob_re("docs/**").match("docs/a/b.md")
    assert ca.glob_re("**/*.md").match("README.md") and ca.glob_re("**/*.md").match("a/b/c.md")
    assert not ca.glob_re("*.md").match("docs/a.md")           # * stays inside one folder
    assert ca.matches("unity/Assets/Shaders/SunShaft.shader.meta", ["unity/Assets/Shaders/**"])
    assert ca.norm("./a/b") == "a/b" and ca.norm(".github/x.yml") == ".github/x.yml" and ca.norm("a\\b") == "a/b"


def test_every_check_is_runnable_and_has_inputs():
    for n, c in RULES["checks"].items():
        assert c["inputs"], n
        assert runner.commands_for(c), n
        assert c["job"] in {"engine", "server", "repo_checks", "dependencies", "unity_compile", "unity_tests", "android_build"}, (n, c["job"])


def test_documentation_only_runs_only_documentation_checks():
    r, v = runs(["docs/qa/quest2_config_matrix.md"])
    assert r == {"docs_check"} and not v["full"], r
    assert not any(v["jobs"][j] for j in ("engine", "server", "unity_compile", "unity_tests", "android_build"))


def test_planning_docs_run_plans_check_and_python_but_no_unity():
    r, _ = runs(["docs/plans/p3/p3-17-quest-performance.md"])
    assert {"docs_check", "plans_check", "python_tests"} <= r and not any(n.startswith("unity") for n in r)


def test_sunlight_shader_runs_shader_compile_and_visual_not_backend():
    r, _ = runs(["unity/Assets/Shaders/SunShaft.shader"])
    assert {"unity_compile", "unity_shader_visual", "unity_editmode"} <= r
    assert not ({"backend_tests", "rules_engine_tests", "unity_playmode_character", "plans_check"} & r)


def test_character_animation_runs_character_tests_only_among_playmode():
    r, _ = runs(["unity/Assets/Scripts/Views/CharacterPerfCuller.cs"])
    assert "unity_playmode_character" in r and "unity_playmode_exterior" not in r and "unity_shader_visual" not in r


def test_city_culling_runs_visibility_tests_and_diff_but_not_character():
    r, _ = runs(["unity/Assets/Scripts/Views/ExteriorVisibility.cs"])
    assert {"unity_playmode_exterior", "unity_walkthrough_diff", "unity_sweep", "unity_compile"} <= r
    assert "unity_playmode_character" not in r and "backend_tests" not in r


def test_backend_change_runs_backend_not_unity():
    r, v = runs(["server/main.py"])
    assert "backend_tests" in r and v["jobs"]["server"] and not any(n.startswith("unity") for n in r)


def test_rules_engine_change_runs_dotnet_and_backend_not_unity():
    r, _ = runs(["src/ProcSense.Core/Engine.cs"])
    assert {"rules_engine_tests", "dotnet_format", "backend_tests"} <= r and not any(n.startswith("unity") for n in r)


def test_texture_only_change_skips_scripts_tests():
    r, _ = runs(["unity/Assets/Textures/Hospital/Monitor/monitor_ui.png"])
    assert "unity_shader_visual" in r and "unity_editmode" not in r and "unity_playmode_character" not in r


def test_project_settings_run_all_unity_checks():
    r, _ = runs(["unity/ProjectSettings/QualitySettings.asset"])
    assert {n for n in RULES["checks"] if n.startswith("unity") or n == "android_build"} - {"unity_playmode_runtime_settings"} <= r


def test_shared_infrastructure_selects_everything():
    for f in ("tools/ci/run.py", "Taskfile.yml", ".github/workflows/ci.yml", "global.json", ".githooks/pre-push"):
        r, v = runs([f])
        assert v["full"] and r == set(RULES["checks"]), f
        assert all(v["jobs"].values())


def test_unmapped_path_selects_everything_and_says_why():
    r, v = runs(["mystery/new_folder/file.xyz"])
    assert v["full"] and r == set(RULES["checks"]) and "unmapped path" in v["reason"]


def test_ignored_paths_select_nothing():
    r, v = runs(["artifacts/unity/ProcSense-demo.apk", "unity/Logs/x.log", "bad.json"])
    assert not r and not v["full"]


def test_all_flag_selects_everything():
    r, v = runs([], force_all=True)
    assert v["full"] and r == set(RULES["checks"])


def test_cache_reuses_only_identical_passes():
    with tempfile.TemporaryDirectory() as d:
        os.environ["PROCSENSE_CI_CACHE"] = d
        try:
            c = RULES["checks"]["docs_check"]
            h1 = {"docs/a.md": "1", "src/x.cs": "1"}
            k1 = ca.cache_key(c, h1)
            assert ca.cache_lookup(c, k1) is None
            ca.cache_store(c, k1, False, 1.0)
            assert ca.cache_lookup(c, k1) is None, "a failure must never be reused"
            ca.cache_store(c, k1, True, 1.0)
            assert ca.cache_lookup(c, k1)
            assert ca.cache_key(c, {**h1, "docs/a.md": "2"}) != k1, "a changed input invalidates"
            assert ca.cache_key(c, {**h1, "unity/Assets/x.cs": "9"}) == k1, "an unrelated file must not invalidate"
            assert ca.cache_key(c, {**h1, "docs/new.md": "1"}) != k1, "a new input file invalidates"
            changed = dict(c, cmds=[["echo", "different"]])
            assert ca.cache_key(changed, h1) != k1, "a changed check definition invalidates"
        finally:
            os.environ.pop("PROCSENSE_CI_CACHE", None)


def test_cache_expires_after_max_age():
    with tempfile.TemporaryDirectory() as d:
        os.environ["PROCSENSE_CI_CACHE"] = d
        try:
            c = RULES["checks"]["dependency_audit"]
            k = ca.cache_key(c, {"ProcSense.sln": "1"})
            ca.cache_store(c, k, True, 1.0)
            assert ca.cache_lookup(c, k)
            assert ca.cache_lookup(c, k, now=time.time() + 25 * 3600) is None
        finally:
            os.environ.pop("PROCSENSE_CI_CACHE", None)


def test_unrelated_change_keeps_every_other_key():
    h = {"docs/a.md": "1", "server/main.py": "1", "unity/Assets/Scripts/Views/ExteriorVisibility.cs": "1"}
    before = {n: ca.cache_key(c, h) for n, c in RULES["checks"].items()}
    h2 = dict(h, **{"server/main.py": "2"})
    after = {n: ca.cache_key(c, h2) for n, c in RULES["checks"].items()}
    assert after["unity_playmode_exterior"] == before["unity_playmode_exterior"]
    assert after["docs_check"] == before["docs_check"]
    assert after["backend_tests"] != before["backend_tests"]


def test_selector_json_shape_for_workflow():
    v = ca.select(RULES, ["server/main.py"])
    assert set(v) >= {"full", "reason", "jobs", "checks", "changed"}
    assert set(v["jobs"]) == {"engine", "server", "repo_checks", "dependencies", "unity_compile", "unity_tests", "android_build"}
    assert set(next(iter(v["checks"].values()))) >= {"run", "why", "cached"}


if __name__ == "__main__":
    failed = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn(); print("ok   ", name)
            except Exception as e:  # noqa: BLE001
                failed += 1; print("FAIL ", name, repr(e))
    sys.exit(1 if failed else 0)
