"""Mutation testing of the guards of the speech path (p1-ai-language-s22, ADR-0025): `python tools/ci/mutation_safety.py [--engine] [--write]`.

Each guard is disabled in a COPY of the code (one textual edit; it must match exactly once, so a refactor that moves the code fails loudly instead of silently testing nothing) and the tests that are meant to hold it must FAIL. A mutant that survives is a guard nobody tests.
Three groups, each judged by its own tests:
  contract   server/evaluate (the strict output contract, the reader)        judged by server/tests/test_evaluate.py (no database)
  route      server/routers/evaluate.py (feature gate, busy slot, audit)    judged by server/tests/test_evaluate_api.py (needs the PostgreSQL of tools/ci/server_tests.py or TEST_DATABASE_URL)
  engine     src/ProcSense.Core (Evidence, the state gate, the speech action)   judged by `dotnet test` of the engine tests (--engine, a build per mutant, slow)
The engine is the scoring authority: a critical objective is never accepted from speech, a non-performed or other-actor objective never counts, the behaviour window, the running step, the waiting question / read-back, the credit floor and the duplicate utterance are each checked
there, whatever the evaluator says. Unity (SpeechEvaluation.cs) is not mutation-run: its EditMode tests are the check.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
Edit = tuple[str, str, str]

CT = "server/evaluate/contract.py"
RD = "server/evaluate/reader.py"
CONTRACT: list[tuple[str, str, list[Edit]]] = [
    ("C1_performed_now_consistent", "performed_now may disagree with the assertion and the actor", [(CT, 'if a["performed_now"] != (a["assertion"] == "performed" and a["actor"] == "trainee"):', "if False:")]),
    ("C2_evidence_in_utterance", "the evidence quoted need not be in the utterance", [(CT, 'if a["evidence"].strip() and norm(a["evidence"]) not in spoken:', "if False:")]),
    ("C3_objective_closed_and_once", "an unknown or repeated objective is accepted", [(CT, 'if a["objective"] not in ids or a["objective"] in seen:', "if False:")]),
    ("C4_grade_range", "a grade outside 0..3 is accepted", [(CT, "if type(a[k]) is not int or not 0 <= a[k] <= rubric.GRADE_MAX:", "if False:")]),
    ("C5_performed_needs_evidence", "a performed act with no quoted evidence is accepted", [(CT, 'if a["performed_now"] and not a["evidence"].strip():', "if False:")]),
    ("C6_truncated_reply", "a reply that stopped for length is accepted", [(CT, 'if done_reason not in (None, "stop"):', "if False:")]),
    ("C7_fenced_reply", "a code-fenced reply is accepted", [(CT, 'if t.startswith("```"):', "if False:")]),
    ("C8_evidence_bounded", "evidence of any length is accepted", [(CT, 'or len(a["evidence"]) > MAX_EVIDENCE or', "or False or")]),
    ("R1_retry_limit", "the reader retries without a limit on a format problem", [(RD, "for attempt in range(1, self.max_attempts + 1):", "for attempt in range(1, self.max_attempts + 5):")]),
]
RT = "server/routers/evaluate.py"
ROUTE: list[tuple[str, str, list[Edit]]] = [
    ("RT1_feature_flag", "the route ignores the speech_evaluation switch", [(RT, 'if not ai_features.enabled("speech_evaluation"):', "if False:")]),
    ("RT2_busy_slot", "the route proceeds when the model slot is busy", [(RT, "if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):", "if False:")]),
    ("RT3_reason_recorded", "an unavailable answer is audited as passed", [(RT, 'call.validated(False, answer["reasons"])', "call.validated(True, [])")]),
]
EV = "src/ProcSense.Core/Scoring/Evidence.cs"
GATE = "src/ProcSense.Core/Speech/SpeechStateGate.cs"
SE = "src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs"
ENGINE: list[tuple[str, str, list[Edit]]] = [
    ("EN1_critical_never_accepted", "a critical objective spoken is accepted as evidence for its behaviour", [(EV, "logRefs, !critical, critical ?", "logRefs, true, critical ?")]),
    ("EN2_not_performed", "a negated, planned, hypothetical, reported or other-actor objective counts", [(EV, "else if (!judgement.PerformedNow) record", "else if (false) record")]),
    ("EN3_no_running_step", "evidence is accepted with no running step", [(EV, "else if (steps.Count == 0) record", "else if (false) record")]),
    ("EN4_window", "evidence is accepted outside the behaviour's window", [(EV, "else if (!behaviour.Steps.Any(steps.Contains)) record", "else if (false) record")]),
    ("EN5_credit_floor", "a grade of zero is recorded as credit", [(EV, "if (grade <= 0) record = Refuse(BelowCreditFloor);", "if (false) record = Refuse(BelowCreditFloor);")]),
    ("EN6_duplicate_utterance", "the same utterance and objective is credited twice", [(EV, 'if (utterance.Length > 0 && !_utterances.Add(utterance + "|" + act))', "if (false)")]),
    ("EN7_malformed_judgement", "a malformed judgement is accepted", [(EV, "else if (!judgement.IsValid) record", "else if (false) record")]),
    ("ET1_answer_needs_a_waiting_question", "ANSWER_QUESTION counts with no patient question waiting", [(GATE, 'if (act == "ANSWER_QUESTION" && !patientQuestionPending)', "if (false)")]),
    ("ET2_readback_needs_a_waiting_result", "CLOSED_LOOP counts with no read-back waiting", [(GATE, 'if (act == "CLOSED_LOOP" && !readbackPending)', "if (false)")]),
    ("ET3_steps_stamped_by_the_engine", "the engine no longer stamps the workflow steps on the logged action", [(SE, 'o.Effects.Add(StepsEffect + string.Join(",", _s.RunningSteps.Select(n => n.ToString(CultureInfo.InvariantCulture))));', "")]),
    ("ET4_bad_judgement_rejected", "the speech action accepts a missing or malformed judgement", [(SE, "if (Judgement(a.Params) == null) { o.Reject(BadJudgement); return; }", "")]),
]


def apply(dest: Path, edits: list[Edit]) -> None:
    for rel, old, new in edits:
        f = dest / rel
        text = f.read_text(encoding="utf-8")
        n = text.count(old)
        if n != 1:
            raise RuntimeError(f"mutation does not match exactly once in {rel} ({n} matches): {old[:70]!r}; the guard moved, update tools/ci/mutation_safety.py")
        f.write_text(text.replace(old, new), encoding="utf-8", newline="\n")


def python() -> str:
    return str(ROOT / "server" / ".venv" / ("Scripts" if os.name == "nt" else "bin") / ("python.exe" if os.name == "nt" else "python"))


def pytest_group(mutants, test_files: list[str], env_extra: dict) -> dict:
    out = {}

    def one(edits):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts", ignore_cleanup_errors=True) as t:
            d = Path(t)
            shutil.copytree(ROOT / "server", d / "server", ignore=shutil.ignore_patterns(".venv", "__pycache__"))
            for item in ("content", "tests/classifier_synthetic_v2", "tests/synthetic_dev_v1/dataset.jsonl", "tests/synthetic_heldout_v1/dataset.jsonl", "tests/synthetic_heldout_v2/dataset.jsonl", "src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs"):
                src = ROOT / item
                (d / item).parent.mkdir(parents=True, exist_ok=True)
                shutil.copytree(src, d / item) if src.is_dir() else shutil.copy2(src, d / item)
            apply(d, edits)
            p = subprocess.run([python(), "-m", "pytest", *[str(d / "server" / "tests" / f) for f in test_files], "-q", "-x", "-p", "no:cacheprovider"], capture_output=True, text=True, encoding="utf-8",
                               cwd=str(d), env={**os.environ, **env_extra, "PYTHONPATH": str(d)})
            return p.returncode, (p.stdout + p.stderr)[-400:]
    code, tail = one([])
    out["baseline"] = {"exit": code, "tail": tail}
    for mid, desc, edits in mutants:
        code, tail = one(edits)
        out[mid] = {"exit": code, "killed": code != 0, "tail": tail}
    return out


def run_contract_tests() -> dict:
    return pytest_group(CONTRACT, ["test_evaluate.py"], {})


def run_route_tests() -> dict:
    url = os.environ.get("TEST_DATABASE_URL", "postgresql://postgres:postgres@localhost:54329/procsense_test")
    return pytest_group(ROUTE, ["test_evaluate_api.py"], {"TEST_DATABASE_URL": url, "REQUIRE_DB": "1"})


def run_engine_tests() -> dict:
    """one copy of src, content and the engine tests; each mutant applied, built, tested and restored; the unmutated copy must pass first."""
    out = {}
    with tempfile.TemporaryDirectory(dir=ROOT / "artifacts", ignore_cleanup_errors=True) as t:
        d = Path(t)
        for item in ("src", "content", "tests/fixtures", "tests/ProcSense.Core.Tests", "Directory.Build.props", "global.json"):
            src = ROOT / item
            (d / item).parent.mkdir(parents=True, exist_ok=True)
            if src.is_dir():
                shutil.copytree(src, d / item, ignore=shutil.ignore_patterns("bin", "obj"))
            elif src.exists():
                shutil.copy2(src, d / item)

        def test() -> tuple[int, str]:
            p = subprocess.run(["dotnet", "test", str(d / "tests" / "ProcSense.Core.Tests"), "-c", "Release", "--nologo", "-v", "q", "--disable-build-servers", "-p:UseSharedCompilation=false", "-p:TreatWarningsAsErrors=false",
                                "--filter", "FullyQualifiedName~Scoring|FullyQualifiedName~InterpretationContext|FullyQualifiedName~CommunicationScoring"], capture_output=True, text=True, encoding="utf-8", cwd=str(d))
            return p.returncode, (p.stdout + p.stderr)[-300:]
        code, tail = test()
        out["baseline"] = {"exit": code, "tail": tail}
        for mid, desc, edits in ENGINE:
            originals = {rel: (d / rel).read_text(encoding="utf-8") for rel, _, _ in edits}
            apply(d, edits)
            code, tail = test()
            out[mid] = {"exit": code, "killed": code != 0, "tail": tail}
            for rel, text in originals.items():
                (d / rel).write_text(text, encoding="utf-8", newline="\n")
    return out


def rows_of(group: str, mutants, res: dict) -> list[dict]:
    if res["baseline"]["exit"] != 0:
        raise RuntimeError(f"the unmutated {group} tests do not pass in the copy: " + res["baseline"]["tail"])
    return [{"group": group, "mutant": mid, "guard": desc, "killed": res[mid]["killed"]} for mid, desc, edits in mutants]


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--engine", action="store_true", help="also the engine mutants (a build each, a few minutes)")
    ap.add_argument("--no-route", action="store_true", help="skip the route mutants (they need the PostgreSQL of tools/ci/server_tests.py)")
    a = ap.parse_args(argv)
    (ROOT / "artifacts").mkdir(exist_ok=True)
    rows = rows_of("contract", CONTRACT, run_contract_tests())
    if not a.no_route:
        rows += rows_of("route", ROUTE, run_route_tests())
    if a.engine:
        rows += rows_of("engine", ENGINE, run_engine_tests())
    for r in rows:
        print("KILLED  " if r["killed"] else "SURVIVED", r["group"], r["mutant"], "|", r["guard"])
    survivors = [r for r in rows if not r["killed"]]
    if a.write:
        out = ROOT / "reports" / "ai" / "safety" / "mutation-service.json"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps({"adr": "ADR-0025", "mutants": rows}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", out.relative_to(ROOT).as_posix())
    print(f"{len(rows) - len(survivors)}/{len(rows)} mutants killed")
    return 1 if survivors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
