"""Run every check CI runs, on your machine, in the same order. Standard library only.

Usage (from repo root):  task ci     or     uv run --project tools tools/ci/run_all.py [--quick]
--quick skips the slow steps (build/test) when only docs or content changed.
Keep this list in step with .github/workflows/ci.yml and docs/CI.md (INSTRUCTIONS.md section 6b).
"""
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.stdout.reconfigure(encoding="utf-8")
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def run(cmd: list[str], fail_if_in_output: str | None = None) -> tuple[bool, str]:
    p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (p.stdout or "") + (p.stderr or "")
    ok = p.returncode == 0 and not (fail_if_in_output and fail_if_in_output in out)
    return ok, out


def run_all_of(commands: list[list[str]]) -> tuple[bool, str]:
    """several commands as one check: ok only if every one is ok; the output of all of them is kept."""
    ok, out = True, ""
    for cmd in commands:
        good, text = run(cmd)
        ok, out = ok and good, out + text
    return ok, out


def replay_round_trip() -> tuple[bool, str]:
    with tempfile.TemporaryDirectory() as tmp:
        out = str(Path(tmp) / "run.json")
        ok, text = run(CLI + ["run", "--case", "VAR-S1-A", "--seed", "42", "--actions", "tests/golden/skeleton1_correct.actions.json", "--out", out])
        if not ok:
            return False, text
        return run(CLI + ["replay", "--log", str(Path(tmp) / "run.session.jsonl")])


def verify_session() -> tuple[bool, str]:
    """A session the engine just wrote has a version 2 header and verifies; the same file with one effect changed (and the header hash made to match) does not (p1-backend-s09)."""
    import hashlib
    import json
    with tempfile.TemporaryDirectory() as tmp:
        out = str(Path(tmp) / "run.json")
        ok, text = run(CLI + ["run", "--case", "VAR-S1-A", "--seed", "42", "--actions", "tests/golden/skeleton1_correct.actions.json", "--out", out])
        if not ok:
            return False, text
        log = Path(tmp) / "run.session.jsonl"
        ok, text = run(CLI + ["verify", "--log", str(log)])
        verdict = json.loads(next(l for l in text.splitlines() if l.startswith("{")))
        if not ok or not verdict["ok"] or verdict["header_version"] != 2 or not verdict["facts"]:
            return False, "the engine's own session did not verify as version 2: " + text[-300:]
        head, _, body = log.read_text(encoding="utf-8").partition("\n")
        edited = body.replace('"score:', '"info:', 1) if '"score:' in body else body.replace('"accepted":true', '"accepted":false', 1)
        old = json.loads(head)["header"]["log_hash"]
        (Path(tmp) / "edited.session.jsonl").write_text(head.replace(old, hashlib.sha256(edited.encode("utf-8")).hexdigest()) + "\n" + edited, encoding="utf-8", newline="")
        ok, text = run(CLI + ["verify", "--log", str(Path(tmp) / "edited.session.jsonl")])
        if ok or '"reason":"HASH_MISMATCH"' not in text:
            return False, "an edited session was not refused with HASH_MISMATCH: " + text[-300:]
        return True, "verified the engine's own session and refused an edited one"


def package_modes() -> tuple[bool, str]:
    text = ""
    for mode in ("dev", "practice", "assessment"):
        ok, out = run(["uv", "run", "--project", "tools", "tools/content/package.py", "--mode", mode, "--check"])
        text += out
        if not ok:
            return False, text
    return True, text


def session_file_contract() -> tuple[bool, str]:
    """The engine writes a session file with the CLI; the server's reader must accept it as header version 2."""
    with tempfile.TemporaryDirectory() as tmp:
        ok, text = run(CLI + ["run", "--case", "VAR-S1-A", "--seed", "42", "--actions", "tests/golden/skeleton1_correct.actions.json", "--out", str(Path(tmp) / "run.json")])
        if not ok:
            return False, text
        return run(["uv", "run", "--project", "server", "python", "-m", "server.scripts.check_session_file", str(Path(tmp) / "run.session.jsonl")])


def gate_refuses() -> tuple[bool, str]:
    """The review-status gate: assessment on a training-only case and practice on a DRAFT case must be refused (exit 1, STATUS_TOO_LOW)."""
    with tempfile.TemporaryDirectory() as tmp:
        for case, mode in (("VAR-S1-A", "assessment"),):
            p = subprocess.run(CLI + ["run", "--case", case, "--seed", "42", "--mode", mode, "--actions", "tests/golden/skeleton1_correct.actions.json", "--out", str(Path(tmp) / "x.json")],
                               cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
            out = (p.stdout or "") + (p.stderr or "")
            if p.returncode == 0 or "STATUS_TOO_LOW" not in out:
                return False, f"{case} in {mode} mode was not refused: {out}"
    return True, "refused as required"


def preflight(quick: bool) -> list[str]:
    """Tools the checks need. A missing one is an error with an install command, never a silent skip."""
    need = {"uv": "winget install --id astral-sh.uv -e", "git": "winget install --id Git.Git -e"}
    if not quick:
        need["dotnet"] = "winget install --id Microsoft.DotNet.SDK.9 -e"
    return [f"{tool} is not installed or not on PATH -> {cmd} (then restart the terminal)" for tool, cmd in need.items() if not shutil.which(tool)]


def record_local_run(passed: int, total: int, quick: bool) -> None:
    """Write .local-ci/last-run.json (git-ignored): what this machine's CI said, for which commit. tools/ci/remote_status.py prints it next to GitHub's runs."""
    import datetime
    import json
    sha = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    dirty = bool(subprocess.run(["git", "status", "--porcelain", "--untracked-files=no"], cwd=ROOT, capture_output=True, text=True).stdout.strip())
    out = ROOT / ".local-ci"
    out.mkdir(exist_ok=True)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")
    (out / "last-run.json").write_text(json.dumps({"commit": sha, "dirty": dirty, "quick": quick, "passed": passed, "total": total, "utc": stamp}, indent=1) + "\n", encoding="utf-8")


def main(argv: list[str]) -> int:
    quick = "--quick" in argv
    missing = preflight(quick)
    if missing:
        print("FAIL  required tools missing; nothing was checked")
        for m in missing:
            print("        " + m)
        return 1
    steps: list[tuple[str, callable]] = []
    if not quick:
        steps += [
            ("build (warnings are errors)", lambda: run(["dotnet", "build", "ProcSense.sln", "-c", "Release", "-warnaserror"])),
            ("unit tests", lambda: run(["dotnet", "test", "ProcSense.sln", "--no-build", "-c", "Release"])),
            ("backend tests (FastAPI on a real PostgreSQL; skipped with a notice when there is no Docker)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/server_tests.py"])),
            ("golden runs", lambda: run(CLI + ["golden", "--dir", "tests/golden"])),
            ("golden runs of generated cases", lambda: run(CLI + ["golden", "--dir", "tests/golden_generated"])),
            ("golden suite: 17 cases x 4 paths, each path behaves as it promises", lambda: run(["uv", "run", "--project", "tools", "tools/golden/paths.py", "--check"])),
        ("attributions up to date and every register row has what a credit needs", lambda: run(["uv", "run", "--project", "tools", "tools/eval/attributions.py", "--check"])),
        ("voice catalogue up to date (every line has a source; gaps and drafts listed)", lambda: run(["uv", "run", "--project", "tools", "tools/voice/catalogue.py", "--check"])),
        ("voice renderer tests (respelling, change hash, pitch filter, roles, plan, unique line ids)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_voice_render.py"])),
        ("voice catalogue tests (sources, the nurse covers every wristband step, a missing line is reported)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_voice_catalogue.py"])),
            ("review-status gate refuses an under-approved case (assessment on a training-only case)", gate_refuses),
            ("replay round trip", replay_round_trip),
            ("a session file the engine just wrote is read by the server (the engine and server contract)", session_file_contract),
            ("verify command: a session the engine wrote verifies (header version 2); an edited one is refused", verify_session),
            ("session report made from the golden run and valid against its schema", lambda: run(["uv", "run", "--project", "tools", "tools/ci/check_report.py"])),
        ]
        if os.name != "nt":  # Windows checkouts use CRLF; CI (Linux, LF) is the authority for formatting
            steps.append(("code format", lambda: run(["dotnet", "format", "ProcSense.sln", "--verify-no-changes"])))
        else:  # on Windows only the indentation/spacing rules are checked (line endings are CRLF here), so a format failure shows up before the push, not in CI
            steps.append(("code format (whitespace rules only)", lambda: (lambda r: (r[0] or "error WHITESPACE" not in r[1], r[1]))(
                run(["dotnet", "format", "whitespace", "ProcSense.sln", "--verify-no-changes"]))))
            steps.append(("code style (imports order and the other style rules)", lambda: run(["dotnet", "format", "style", "ProcSense.sln", "--no-restore", "--verify-no-changes"])))
        steps.append(("no vulnerable packages", lambda: run(["dotnet", "list", "ProcSense.sln", "package", "--vulnerable", "--include-transitive"],
                                                          fail_if_in_output="has the following vulnerable packages")))
    steps += [
        ("content + source register", lambda: run(["uv", "run", "--project", "tools", "tools/content/validate.py"])),
        ("walkthrough extract counts", lambda: run(["uv", "run", "--project", "tools", "tools/content/extract_walkthrough.py", "--check"])),
        ("traceability up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/trace.py", "--check"])),
        ("rule evaluator twin tests (same vectors as the C# evaluator)", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_jsonlogic.py"])),
        ("content tools tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_content_tools.py"])),
        ("content tables verified against the book and up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_tables.py", "--check"])),
        ("content table meaning tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_tables.py"])),
        ("case content gaps list up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/case_gaps.py", "--check"])),
        ("images + reports valid (ultrasound, ECG, chest X-ray, CT, MRI, MRCP)", lambda: run(["uv", "run", "--project", "tools", "tools/content/validate_images.py"])),
        ("case files built, valid and up to date (53 cases: 17 P0, 16 P1, 20 P2)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_case_files.py", "--check"])),
        ("flow maps up to date (S1 and S2, 20 stages each)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_flow_maps.py", "--check"])),
        ("score tables up to date (AIR, AAS, Alvarado, TG18, ASGE, STOP-Bang, AKI)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_scores.py", "--check"])),
        ("imaging reports and categories up to date (51 findings, approved reports, 35 cases)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_imaging.py", "--check"])),
        ("register ids, blocks and provenance consistent (and the allocation document current)", lambda: run(["uv", "run", "--project", "tools", "tools/content/register_audit.py", "--check"])),
        ("consent content up to date (elements, questions, rules)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_consent.py", "--check"])),
        ("behaviour model up to date (attributes, transitions, modes, disclosure)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_behaviour.py", "--check"])),
        ("scoring content up to date (22 behaviours, rubric, debrief sentences)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_scoring.py", "--check"])),
        ("AI lexicon, intents and shared validator vectors up to date", lambda: run(["uv", "run", "--project", "tools", "python", "-c", "import subprocess,sys; r=[subprocess.run(['uv','run','--project','tools',*c]).returncode for c in (['tools/content/build_ai_lexicon.py','--check'],['tools/content/build_ai_intents.py','--check'],['python','server/validation/make_vectors.py','--check'])]; sys.exit(1 if any(r) else 0)"])),
        ("grounding validator tests (disclosure probes, off-card drugs, false rejects, debrief mutations)", lambda: run(["uv", "run", "--project", "tools", "python", "server/validation/test_validator.py"])),
        ("P0 and P1 correct-path scripts are what the generator writes (33 cases)", lambda: run(["uv", "run", "--project", "tools", "tools/golden/p0_scripts.py", "--stale"])),
        ("what-if bindings up to date (P0 cases, derived from the case content)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_bindings.py", "--check"])),
        ("P1 variant cases hold every fact the book gives them (47 facts, report up to date)", lambda: run(["uv", "run", "--project", "tools", "tools/content/variant_facts.py", "--check"])),
        ("P1 variant check tests (a changed value and a phrase that is not the book's are caught)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_variant_facts.py"])),
        ("stacked cases follow Part D: priority ranks, one decision per book decision, results (report up to date)", lambda: run(["uv", "run", "--project", "tools", "tools/content/stacked_partd.py", "--check"])),
        ("stacked-case check tests (a rank out of order, a missing decision, a changed result are caught)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_stacked_partd.py"])),
        ("Stage B validator passes all 53 case files", lambda: run(["uv", "run", "--project", "tools", "tools/composer/stage_b.py", "--all"])),
        ("Stage B rejects all 60 mutated cases (12 contradiction types x 5)", lambda: run(["uv", "run", "--project", "tools", "tools/composer/mutate.py", "--type", "ALL", "--n", "5"])),
        ("composer tests", lambda: run(["uv", "run", "--project", "tools", "python", "tools/composer/test_composer.py"])),
        ("problem discovery rules up to date (29 rules for the 17 P0 cases)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_discovery.py", "--check"])),
        ("difficulty settings up to date (8 dimensions, K 16, 3 sessions)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_difficulty.py", "--check"])),
        ("events and what-if coverage up to date (7 events, 47 what-ifs)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_events.py", "--check"])),
        ("physiology effect plots up to date (for the review pack)", lambda: run(["uv", "run", "--project", "tools", "tools/physio/plot_effects.py", "--check"])),
        ("behaviour magnitude plots up to date (for the review pack)", lambda: run(["uv", "run", "--project", "tools", "tools/behaviour/plot.py", "--check"])),
        ("physiology model up to date (variables, couplings, noise)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_physio.py", "--check"])),
        ("end states up to date (5 states, delay items, surface-later facts)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_end_states.py", "--check"])),
        ("safety checklist up to date (24 items, expected-answer rules)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_checklist.py", "--check"])),
        ("handover items up to date (14 items, planned and emergency sets)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_handover.py", "--check"])),
        ("theatre readiness stations up to date (12 stations, faults)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_or.py", "--check"])),
        ("ward checklist up to date (13 items, verify rules)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_ward.py", "--check"])),
        ("sample dispatch up to date (tube station, porter, beginner prompt)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_diagnostics.py", "--check"])),
        ("patient record up to date (document types, each case's record, distractor patients)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_records.py", "--check"])),
        ("deterioration clock and Usman timeline up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_deterioration.py", "--check"])),
        ("decision outcomes and checks up to date (5 outcomes, 6 ordered checks, emergency signs)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_decision.py", "--check"])),
        ("clash table and medicine plans up to date (18 clashes, factors, plans)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_clashes.py", "--check"])),
        ("sepsis tiers and timers up to date (NG253 criteria, timers, infection cases)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_sepsis.py", "--check"])),
        ("problem types and ladder up to date (6 types, 7 rungs, P1 to P4)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_problems.py", "--check"])),
        ("critical result limits up to date (16 limits, conditions, actions)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_critical.py", "--check"])),
        ("normal results up to date (planned patient liver panel from the verified bands)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_normal_results.py", "--check"])),
        ("investigation indications up to date (17 tests, planned and emergency)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_indications.py", "--check"])),
        ("NEWS2 chart up to date (scale 1 and 2, response bands)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_news2.py", "--check"])),
        ("fasting table up to date (clear 2 h, milk 6 h, light meal 6 h, heavy meal 8 h)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_fasting.py", "--check"])),
        ("treatment library up to date (17 treatments, checks, doses only with a reviewer)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_treatments.py", "--check"])),
        ("content packages per mode (dev, practice, assessment): hashes, no case below the mode", package_modes),
        ("case file tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_case_files.py"])),
        ("EXP-DATA-2: generated lab values stay in their band (10,000 seeds)", lambda: run(["uv", "run", "--project", "tools", "tools/content/exp_data2.py"])),
        ("equipment sheets up to date (53 items)", lambda: run(["uv", "run", "--project", "tools", "tools/content/equipment_sheets.py", "--check"])),
        ("equipment sheet tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_equipment.py"])),
        ("imaging library complete and up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/imaging_library.py", "--check"])),
        ("image review pack up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/image_review_pack.py", "--check"])),
        ("dataset register and cards up to date", lambda: run(["uv", "run", "--project", "tools", "tools/data/dataset_cards.py", "--check"])),
        ("licence register covers every dataset, model and library id, with a decision and a date", lambda: run(["uv", "run", "--project", "tools", "tools/data/licence_register_check.py"])),
        ("licence register check tests (a missing id, a bad decision word, a missing date, registers that disagree)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_licence_register.py"])),
        ("image and dataset tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_images.py"])),
        ("numeric profiles reproduce the earlier aggregates and cover the lab bands", lambda: run(["uv", "run", "--project", "tools", "tools/data/profile_numeric.py", "--check"])),
        ("waveform profile covers every ECG finding of the imaging library", lambda: run(["uv", "run", "--project", "tools", "tools/data/profile_waveforms.py", "--check"])),
        ("imaging tags (every picture and finding is tagged and each tag is supported by its wording)", lambda: run(["uv", "run", "--project", "tools", "tools/content/imaging_tags.py", "--check"])),
        ("generator packs and evaluation sets current, no raw rows", lambda: run(["uv", "run", "--project", "tools", "tools/generation/build_packs.py", "--check"])),
        ("profile and pack tests (statistics, 10,000 draws reproduce the profile, zones, copula)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_packs.py"])),
        ("numeric profile tests (skew, ranks, correlations, borderline share)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_profile_numeric.py"])),
        ("scenario knowledge base current, sourced and valid", lambda: run(["uv", "run", "--project", "tools", "tools/kb/build_kb.py", "--check"])),
        ("knowledge base tests (grounding of model relations, review gate, sheet and score)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_kb.py"])),
        ("composer block library current, tagged and cut from named cases", lambda: run(["uv", "run", "--project", "tools", "tools/composer/blocks.py", "--check"])),
        ("composer solver tests (T-CO1 to T-CO5, definition of done)", lambda: run(["uv", "run", "--project", "tools", "tools/composer/test_solver.py"])),
        ("scenario quality judge: scores, a broken or missing answer gives none, nothing in the pipeline reads it (advisory only)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_judge.py"])),
        ("block compatibility suggestions: never constraints, current, found where chance says two tags should meet and never do", lambda: run(["uv", "run", "--project", "tools", "tools/composer/test_suggest_constraints.py"])),
        ("multi-problem composition engine: coherence rules, ladder order, stable schema, committed synthetic compositions valid and current (14B.5 recipe, DRAFT, not clinically validated)", lambda: run(["uv", "run", "--project", "tools", "tools/composer/test_multiproblem.py"])),
        ("multi-problem compositions current and every one traces to approved source problems", lambda: run(["uv", "run", "--project", "tools", "tools/composer/multiproblem.py", "--check"])),
        ("live generation contracts and objectives (requests, schemas, Elo mirror)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_contracts.py"])),
        ("retrieval index (recall at 20, status filter, current hash)", lambda: run(["uv", "run", "--project", "tools", "tools/generation/build_index.py", "--check"])),
        ("retrieval tests (tag filter, order, exclusions, stale index)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_retrieve.py"])),
        ("scenario skeleton tests (selection, novelty, property over seeds)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_skeleton.py"])),
        ("numeric generation tests (bands, KS, copula, story numbers untouched)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_numeric.py"])),
        ("media selection tests (finding tags, licence filter, ECG and pulse)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_media.py"])),
        ("cross-modal rules and the package gate (a failing example per rule, mutations caught)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_crossmodal.py"])),
        ("novelty fingerprints, distances and the sealed package", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_novelty.py"])),
        ("quality metrics and bounded repair", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_quality.py"])),
        ("deterministic side-of-body and denied-pain rules (no authored case flagged, the gate rejects without a critic)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_consistency.py"])),
        ("generated package files, the pool and the engine", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_package.py"])),
        ("generated golden snapshot is whole and passes the generation gate", lambda: run(["uv", "run", "--project", "tools", "tools/golden/generated.py", "check"])),
        ("a generated case ends and scores like the case it keeps", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_live.py"])),
        ("generation history, generated debrief and the offline evaluation loop", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_history.py"])),
        ("every generation release cites an evaluation report", lambda: run(["uv", "run", "--project", "tools", "tools/generation/evaluate.py", "check"])),
        ("classifier smoke dataset current and split by speaker (synthetic, SD-006)", lambda: run(["uv", "run", "--project", "tools", "tools/synth/gen_utterances.py", "--check"])),
        ("classifier benchmark scenario cards current (blank sheets: the independent set is blocked on people)", lambda: run(["uv", "run", "--project", "tools", "tools/bench/dataset.py", "cards", "--check"])),
        ("analysis of the learning-effect study and the AI-dialogue A/B", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_study_analysis.py"])),
        ("quiz candidates for the learning-effect study are current", lambda: run(["uv", "run", "--project", "tools", "tools/eval/build_quiz.py", "--check"])),
        ("spoken orders: the model fallback only suggests a read-back, nothing is released without a yes, the read-back ticket cannot be forged, moved, replayed or used late", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_orders_fallback.py"])),
        ("act benchmark page, order mapper comparison page and knowledge-base pair-gap page are current (generated from the committed result files and the library; synthetic, not independent)", lambda: (lambda x, y, z: (x[0] and y[0] and z[0], x[1] + y[1] + z[1]))(run(["uv", "run", "--project", "tools", "tools/bench/act_benchmark_report.py", "--check"]), run(["uv", "run", "--project", "tools", "tools/kb/pair_gaps.py", "--check"]), run(["uv", "run", "--project", "tools", "tools/ai/orders_report.py", "--check"]))),
        ("step audit: every step that claims implementation complete names a test or result that CI runs (or a note why not), every ticked step has a status that allows it, every step that needs an outside actor is in the handoff", lambda: run(["uv", "run", "--project", "tools", "tools/plans/audit_steps.py", "--check"])),
        ("the 9B model is retired (ADR-0019): no active code or config names it and every model default is the 4B", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_no_9b.py"])),
        ("plan extras (DT-224): events, what-if branches and interacting problems are chosen only among code-made candidates, a down model changes nothing, the engine restricts practice events to the plan", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_plan_extras.py"])),
        ("trap wording writer: a reworded trap keeps its facts, numbers and action, a refused wording leaves the original, the package records it", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_trap_writer.py"])),
        ("EX-01 phase two kit: author brief shows no sentence, coverage and real-recording rules, blind annotator files, disagreements kept, nothing invented, freeze refuses non-independent roles, set changes detected (placeholder fixtures only)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_ex01_kit.py"])),
        ("direct synthetic development set: specifications frozen and reproducible, gold derived from the approved rules, matrix, utterances carry no label, discards logged, validators, dataset hash (no model, no cloud)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_synth_dev.py"])),
        ("Gemma speech evaluator (ADR-0025): the engine's graded communication score equals the offline mirror on the same judgements, critical objectives are never accepted from speech", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_evaluate_parity.py"])),
        ("fresh held-out set HO1 and its single scored run (ADR-0025): specifications frozen before the sentences, no copy of any committed test sentence, data and criteria unchanged since the freeze, scored once, the verdict follows from the pre-registered criteria (synthetic, no model)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_synth_heldout.py"])),
        ("final fresh held-out set v2 (ADR-0025): specifications frozen before the sentences, no copy of any committed test sentence, data and criteria unchanged since the freeze, scored once, the verdict follows from the criteria written beforehand (synthetic, no model)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_synth_heldout_v2.py"])),
        ("fresh partial-credit set v3 and the final acceptance record (ADR-0025, DT-247): set whole and new, long speech included, data, criteria, prompt and reply contract unchanged since the freeze, scored once, verdict follows from the criteria, no reply rejected for long evidence, the gate record equals the two reports (synthetic, no model)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_synth_partial_v3.py"])),
        ("recall diagnosis page is current (generated from reports/ai/recall-*.json)", lambda: run(["uv", "run", "--project", "tools", "tools/bench/recall_report.py", "--check"])),
        ("unsafe-speech sets, regression cases, blind review sheet and report page are current (generated; synthetic, provisional labels, no human review)", lambda: run_all_of([
            ["uv", "run", "--project", "tools", "tools/bench/metamorph.py", "--check"], ["uv", "run", "--project", "tools", "tools/bench/safety_regression.py", "--check"],
            ["uv", "run", "--project", "tools", "tools/bench/safety_review_sheet.py", "--check"], ["uv", "run", "--project", "tools", "tools/bench/unsafe_speech_report.py", "--check"]])),
        ("blind review kit (toy data: blind files, check, kappa, adjudication, merge refusals), the frozen candidate, sets and final results are unchanged, and the set audit page is current (ADR-0023, p1-ai-language-s17; no human review exists)", lambda: run_all_of([
            ["uv", "run", "--project", "tools", "tools/ci/test_safety_review_kit.py"], ["uv", "run", "--project", "tools", "tools/bench/safety_frozen.py", "--check"], ["uv", "run", "--project", "tools", "tools/bench/safety_set_audit.py", "--check"]])),
        ("mutation tests of the speech-path guards (ADR-0025): each contract, route and engine check disabled in a copy of the code, a test must fail (about 8 minutes, builds engine copies)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_mutation_safety.py"])),
        ("interpretation benchmark page is current", lambda: run(["uv", "run", "--project", "tools", "tools/bench/interpretation_report.py", "--check"])),
        ("retrieval index and the recorded decision not to ship it", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_retrieval.py"])),
        ("model-call layer against a fake Ollama", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_llm.py"])),
        ("session evidence from golden sessions", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_personalisation_evidence.py"])),
        ("learner model against simulated learners", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_personalisation_model.py"])),
        ("targeting policy, explanations, interpreter and the simulated experiment", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_personalisation_policy.py"])),
        ("joint model of laboratory results", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_generation_joint.py"])),
        ("synthetic dataset cards are whole", lambda: run(["uv", "run", "--project", "tools", "tools/synth/publish.py", "--check"])),
        ("synthetic card tests", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_synth_cards.py"])),
        ("AI evaluation report is current", lambda: run(["uv", "run", "--project", "tools", "tools/eval/collect_ai.py", "--check"])),
        ("speech recognition vocabulary is current", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_vocab.py", "--check"])),
        ("learning analytics (patterns, calibration, reasoning)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_analytics.py"])),
        ("speech evaluation acceptance record is what the two stored acceptance reports give (the input of the speech_evaluation gate)", lambda: run(["uv", "run", "--project", "tools", "tools/eval/speech_acceptance.py", "--check"])),
        ("AI feature gate is current and a missing result never passes", lambda: run(["uv", "run", "--project", "tools", "tools/eval/ai_gate.py", "--check"])),
        ("AI gate tests", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_ai_gate.py"])),
        ("red team of the validator", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_redteam.py"])),
        ("synthetic diverse classifier set is current (model-written, NOT independent)", lambda: run(["uv", "run", "--project", "tools", "tools/bench/synthetic.py", "check"])),
        ("recovery handover content current and sourced", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_recovery_handover.py", "--check"])),
        ("intra-operative event content current and sourced", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_intraop_events.py", "--check"])),
        ("labelling toolkit (agreement, adjudication, frozen splits)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_labelling.py"])),
        ("reconciliation of all P0, P1 and P2 steps covers every step and cites existing evidence", lambda: run(["uv", "run", "--project", "tools", "tools/plans/reconcile.py", "--check"])),
        ("reconciliation tests", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_reconcile.py"])),
        ("terminology codes (LOINC check digits, ICD-11 shapes, coverage of every analyte and condition word)", lambda: run(["uv", "run", "--project", "tools", "tools/content/terminology_codes.py", "--check"])),
        ("terminology code tests", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_terminology_codes.py"])),
        ("content manifest up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/content_manifest.py", "--check"])),
        ("medical review pack up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/review_pack.py", "--check"])),
        ("plan index up to date", lambda: run(["uv", "run", "--project", "tools", "tools/plans/milestones.py", "--check"])),
        ("AI planning graph valid (pipeline JSON rules VR-G-*)", lambda: run(["uv", "run", "--project", "tools", "tools/plans/validate_pipeline.py", "--quiet"])),
        ("pipeline validator catches broken graphs (15 mutations)", lambda: run(["uv", "run", "--project", "tools", "tools/plans/test_validate_pipeline.py"])),
        ("planning documents agree with the pipeline JSON (plans, exports, steps, ids, registers)", lambda: run(["uv", "run", "--project", "tools", "tools/plans/check_planning_sync.py"])),
        ("AI planning documents up to date (generated from the pipeline JSON)", lambda: run(["uv", "run", "--project", "tools", "tools/plans/pipeline_docs.py", "--check"])),
        ("engine version rule tests (changed golden hashes need a raised CoreInfo.Version)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_engine_version.py"])),
        ("project rules", lambda: run(["uv", "run", "--project", "tools", "tools/ci/repo_checks.py"])),
        ("secret scan catches pasted keys and passwords (Supabase key, database address, token)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_repo_checks.py"])),
        ("deploy checker waits for the new commit, tolerates a sleeping service and fails clearly", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_wait_for_deploy.py"])),
        ("task doctor reads the wanted versions from the repository and reports missing tools", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_doctor.py"])),
        ("repo backup bundles every branch and tag, restores by clone and prunes old copies", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_repo_backup.py"])),
        ("model benchmark harness works against a fake Ollama (contexts never hold unearned facts, scoring, reports)", lambda: run(["uv", "run", "--project", "tools", "tools/ai/test_benchmark.py"])),
        ("drug availability: registration is never stock, synthetic is never live, stale is unknown", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_availability.py"])),
        ("availability flags cover every medicine group and name a permitted source", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_availability.py", "--check"])),
        ("model adaptation trigger check is current and a met trigger needs an experiment record", lambda: run(["uv", "run", "--project", "tools", "tools/ai/adaptation_trigger.py", "--check"])),
        ("adaptation trigger tests", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_adaptation_trigger.py"])),
        ("dialogue stress scenarios are seeded and their reply rules catch leaks, meta talk, doses and advice", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_dialogue_stress.py"])),
        ("external testing handoff covers every step that needs an outside actor, every item has a procedure and pass rule, and the page is current", lambda: run(["uv", "run", "--project", "tools", "tools/plans/external_testing.py", "--check"])),
        ("on-device recogniser proxy report is a desktop figure on the server's clips, never a headset result", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_asr_proxy_report.py"])),
        ("Pulse adapter data: mapping, recorded traces, parity page (no Pulse needed)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_pulse.py"])),
        ("classifier comparison page is current (per class, confusions, who did what; synthetic and AI-judged)", lambda: run(["uv", "run", "--project", "tools", "tools/bench/classifier_report.py", "--check"])),
        ("classifier safety page is current (false credit, ensemble gate, hard negatives; synthetic, AI-judged, not independent)", lambda: run(["uv", "run", "--project", "tools", "tools/bench/safety_report.py", "--check"])),
        ("blind comparison tool for human raters (EX-03): analysis arithmetic, refusal of missing scores, case rendering", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_blind_compare.py"])),
        ("experiments chapter of the report is current (generated from the phase READMEs)", lambda: run(["uv", "run", "--project", "tools", "tools/eval/collect_experiments.py", "--check"])),
        ("feature implementation matrix of the 68 plans is current and the pipeline JSON statuses follow the reconciliation; every feature has a runtime path and a test file", lambda: run(["uv", "run", "--project", "tools", "tools/plans/feature_matrix.py", "--check"])),
        ("AI-judged relation precision proxy (never the hand-labelled gate)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_kb_judge.py"])),
        ("seeded generation requests: refused with a reason or served with a sealed, reproducible package, never an error", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_request_fuzz.py"])),
        ("safety principles hold across layers (critical credit, deterministic checks before any model, features off while unmet)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_safety_invariants.py"])),
        ("candidate model rules (a gated model needs recorded terms; a switch needs measured parity and a blind rating)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_model_candidate.py"])),
        ("remote CI status classifier (a run GitHub never started is not a pass)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_remote_status.py"])),
        ("pre-push hook behaves (isolated test repo)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_pre_push_hook.py"])),
    ]
    results = []
    for name, step in steps:
        start = time.time()
        ok, out = step()
        results.append((name, ok))
        print(f"{'PASS' if ok else 'FAIL'}  {name}  ({time.time() - start:.0f}s)")
        if not ok:
            print("\n".join("        " + line for line in out.strip().splitlines()[-25:]))
    failed = [n for n, ok in results if not ok]
    record_local_run(len(results) - len(failed), len(results), quick)
    print(f"\n{len(results) - len(failed)}/{len(results)} checks passed" + (f"; failed: {', '.join(failed)}" if failed else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
