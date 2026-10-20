"""Mutation testing of the speech-safety guards (p1-ai-language-s16, ADR-0023): `python tools/ci/mutation_safety.py [--engine] [--write]`.

Why this exists: on 20 October 2026 disabling `validate` was NOT caught by the recall tests, because every counterexample those tests use is refused earlier, by the gate's assertion layer. The guards overlap on purpose (defence in depth), so a test that only looks at the
final decision cannot tell which guard did the work. This module disables each guard in a COPY of the code (one textual edit; the edit must match exactly once, so a refactor that moves the code fails loudly instead of silently testing nothing) and runs
tools/ci/safety_scenarios.py against the copy. A mutant is KILLED when some scenario's outcome differs from the unmutated run; the catalogue below says which scenarios must change.
Three kinds of mutant:
  single      one guard disabled. Its `layer` scenario must change (the guard is tested on its own). Whether the SYSTEM outcome (decision + engine verdict) changes too is recorded in `system`: "killed" when this guard is the only barrier for that scenario,
              "masked" when another layer still refuses (the redundancy that hid the first mutant), and the test pins which it is, so a guard that silently becomes redundant or silently becomes the only barrier shows up.
  double      all the redundant copies of one barrier disabled in the service: the scenario must now be OFFERED by the service, and what the ENGINE then does is the final outcome (refused by its own checks, or ACCEPTED when only the service could know).
  engine      (--engine, slower: a build per mutant) one check of the engine's EvidenceStore disabled, the service guards left on, then both: the final outcome is a credit only when every barrier is gone.
Unity (SpeechInterpretation.cs) is not mutation-run here: it has no dotnet-free runner; its 8 EditMode tests are the check and the gap is recorded in docs/research/safety_layers_audit.md.
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PY = sys.executable
SCEN = ROOT / "tools" / "ci" / "safety_scenarios.py"
Edit = tuple[str, str, str]

# ---- the service guards: (id, description, [edits], scenarios whose OUTCOME must change, system-level scenarios expected to stay unchanged because another layer refuses)
V = "server/interpret/validate.py"
D = "server/interpret/decide.py"
EN = "server/classify/ensemble.py"
EM = "server/classify/embedding.py"
G = "server/classify/guard.py"
A = "server/classify/assertion.py"
SINGLE: list[tuple[str, str, list[Edit], list[str], list[str]]] = [
    ("V1_validate_critical_entry", "validate no longer refuses an eligible critical entry", [(V, "    if entry.critical:\n        return [\"CRITICAL_NEVER_FROM_SPEECH\"]", "    if False:\n        return [\"CRITICAL_NEVER_FROM_SPEECH\"]")], ["L01_validate_refuses_a_critical_act"], ["S01a_critical_act_from_gate", "S01b_critical_sbar_act_from_gate"]),
    ("V2_validate_status", "validate no longer refuses a question for a statement act or a non-performed status", [(V, "    if status == \"question\":", "    if False:"), (V, "    elif status != \"actual\":", "    elif False:")], ["L02_validate_refuses_a_denied_sentence", "L03_validate_refuses_a_question_for_a_statement_act", "S03_negated_sentence", "S04_statement_act_asked_as_question"], []),
    ("V3_validate_eligibility", "validate offers an act that no running behaviour window lists", [(V, "    return (fresh or entries or [None])[0]", "    return (fresh or entries or [type(\"E\", (), {\"critical\": False, \"behaviour\": \"none\", \"act\": act})()])[0]")], ["L04_validate_refuses_an_ineligible_act", "S05_outside_the_behaviour_window"], []),
    ("V4_validate_pending_question", "validate offers ANSWER_QUESTION with no patient question waiting", [(V, "    if act == \"ANSWER_QUESTION\" and not context.patient_question_pending:", "    if False:")], ["L05_validate_needs_a_waiting_question", "S06_answer_without_a_waiting_question"], []),
    ("V5_validate_readback", "validate offers CLOSED_LOOP with no read-back waiting", [(V, "    if act == \"CLOSED_LOOP\" and not context.readback_pending:", "    if False:")], ["L06_validate_needs_a_waiting_readback", "S07_readback_without_a_waiting_result"], []),
    ("V6_validate_already_accepted", "validate offers a behaviour the session already credited", [(V, "    if entry.behaviour in context.accepted:", "    if False:")], ["L07_validate_refuses_a_credited_behaviour", "S08_behaviour_already_credited"], []),
    ("V7_validate_gate_required", "validate no longer needs the classifier gate to have credited the act", [(V, "    if require_gate and not gate_credited:", "    if False:")], ["L08_validate_needs_the_gate", "S09_gate_did_not_credit"], []),
    ("D1_decide_critical_branch", "decide no longer sends a critical cue or critical act to the menu", [(D, "        if result.critical or cue:", "        if False:")], ["S02_critical_cue_with_ordinary_act"], ["S01a_critical_act_from_gate", "S01b_critical_sbar_act_from_gate"]),
    ("D2_decide_cap_recheck", "decide no longer re-checks the critical-mass cap", [(D, "        if cap is not None and mass >= cap:", "        if False:")], ["S10_critical_mass_over_the_cap"], ["G01_cap_blocked_unsafe_real_gate"]),
    ("D3_decide_unavailable", "decide offers although the classifier was unavailable", [(D, "        if result.source == \"unavailable\" or not probs:", "        if False:")], ["S11_classifier_unavailable"], []),
    ("D4_decide_clarify_limit", "decide asks more than one clarification", [(D, "        if clarifications_used >= CLARIFY_LIMIT:", "        if False:")], ["S12b_second_clarification"], []),
    ("D5_decide_status_ignored", "decide judges every utterance as actual (the status check)", [(D, "        status = status_of(utterance)", "        status = \"actual\"")], ["S03_negated_sentence", "S04_statement_act_asked_as_question"], ["G02_non_performed_real_gate"]),
    ("D6_safe_interpret_swallow", "a failing classifier is no longer turned into the menu", [(D, "    except Exception:\n        return {\"decision\": \"MENU\"", "    except ZeroDivisionError:\n        return {\"decision\": \"MENU\"")], ["S16_classifier_raises"], []),
    ("E1_gate_assertion", "the gate no longer applies the assertion layer", [(EN, "            elif (kind := non_assertive(sentence)):", "            elif False:")], ["L15_gate_assertion_blocks_non_performed"], ["G02_non_performed_real_gate"]),
    ("E2_gate_agreement", "the gate no longer needs the TF-IDF head to agree", [(EN, "            if other.act != act:", "            if False:")], ["L18_gate_needs_the_tfidf_head_to_agree"], []),
    ("H1_gate_cap", "the gate no longer applies the critical-mass cap", [(EM, "        if cap is not None and mass is not None and mass >= cap:", "        if False:")], ["L14_gate_cap_blocks_the_cap_sentences"], ["G01_cap_blocked_unsafe_real_gate"]),
    ("H2_gate_cue", "the gate no longer applies the critical-content cue", [(EM, "        return t is not None and probability >= t and not critical_cue(sentence)", "        return t is not None and probability >= t")], ["L19_gate_cue_on_an_ordinary_looking_embedding"], ["G03_critical_sentences_real_gate"]),
    ("A1_assertion_layer", "the assertion layer recognises nothing", [(A, "    for kind, pattern in _PATTERNS:", "    for kind, pattern in []:")], ["L09_status_of_names_a_denial", "L10_status_of_names_a_hypothetical", "L12_assertion_layer_names_other_kinds", "S03_negated_sentence"], ["G02_non_performed_real_gate"]),
    ("A2_assertion_other_actor", "the assertion layer no longer names another person who did it", [(A, "            if not _CAUSATIVE.search(text[:m.start()]):\n                return \"other_actor\"", "            if False:\n                return \"other_actor\"")], ["L12_assertion_layer_names_other_kinds"], []),
    ("K1_cue_regexes", "the critical-content cues match nothing", [(G, "        if pattern.search(text):\n            return kind", "        if False:\n            return kind")], ["L13_critical_cue", "S02_critical_cue_with_ordinary_act"], ["G03_critical_sentences_real_gate"]),
]
# ---- double mutants: every service copy of one barrier removed. scenarios that must now be OFFERED by the service, and the engine verdict expected for at least one of them
DOUBLE: list[tuple[str, str, list[str], list[str], str]] = [
    ("DBL_critical", "validate AND decide stop refusing a critical act", ["V1_validate_critical_entry", "D1_decide_critical_branch"], ["S01b_critical_sbar_act_from_gate"], "CRITICAL_NEEDS_DETERMINISTIC_EVIDENCE"),
    ("DBL_cap", "the gate AND decide stop applying the critical-mass cap", ["H1_gate_cap", "D2_decide_cap_recheck"], ["G01_cap_blocked_unsafe_real_gate"], "accepted"),
    ("DBL_assertion", "the gate's assertion layer AND the interpreter's status check are gone", ["E1_gate_assertion", "D5_decide_status_ignored"], ["G02_non_performed_real_gate"], "accepted"),
    ("DBL_assertion_patterns", "the assertion layer recognises nothing (it serves both)", ["A1_assertion_layer", "D5_decide_status_ignored"], ["G02_non_performed_real_gate"], "accepted"),
    ("DBL_window", "validate stops checking the behaviour window", ["V3_validate_eligibility"], ["S05_outside_the_behaviour_window"], "CONTEXT_MISMATCH"),
    ("DBL_duplicate", "validate stops refusing a behaviour already credited", ["V6_validate_already_accepted"], ["S08_behaviour_already_credited"], "accepted"),
]


# ---- the engine's checks (src/ProcSense.Core/Scoring/Evidence.cs): (id, description, [edits], scenarios whose engine verdict must change from refused to "accepted")
EV = "src/ProcSense.Core/Scoring/Evidence.cs"
ENGINE: list[tuple[str, str, list[Edit], dict[str, str]]] = [
    ("EN1_engine_critical", "the store no longer refuses a critical act", [(EV, "else if (_t.CriticalActs.Contains(act)) record", "else if (false) record")], {"E01_critical_act_offered": "accepted"}),
    ("EN2_engine_threshold", "the store no longer applies the per-act confidence threshold", [(EV, "else if (confidence < _t.ThresholdFor(act)) record", "else if (false) record")], {"E03_below_the_act_threshold": "accepted"}),
    ("EN3_engine_no_running_step", "the store accepts evidence with no running step", [(EV, "else if (steps.Count == 0) record", "else if (false) record")], {"E04_no_running_step": "CONTEXT_MISMATCH"}),   # the next check still refuses, with another reason: the change is the proof the check ran
    ("EN4_engine_window", "the store no longer checks the behaviour window", [(EV, "else if (!behaviour.Steps.Any(steps.Contains)) record", "else if (false) record")], {"E05_outside_the_window": "accepted"}),
    ("EN5_engine_duplicate_behaviour", "the store credits a behaviour twice from speech", [(EV, "else if (_records.Any(r => r.Source == \"ai\" && r.Accepted && r.Behaviour == behaviour.Id)) record", "else if (false) record")], {"E06b_same_behaviour_again": "accepted", "S14b_same_behaviour_again_stale_context": "accepted"}),
    ("EN6_engine_duplicate_utterance", "the store credits the same utterance twice", [(EV, "if (utterance.Length > 0 && !_utterances.Add(utterance))", "if (false)")], {"E07d_same_utterance_id_for_another_behaviour": "accepted"}),   # for the SAME behaviour the duplicate-behaviour check refuses it too (E07b stays refused): the id check is the barrier only for another behaviour
]
# service guards AND an engine check removed: the final outcome is a credit only when every barrier for that case is gone. (id, service mutants, engine mutant, scenario that must end "accepted")
BOTH: list[tuple[str, str, list[str], str, str]] = [
    ("BOTH_critical", "validate, decide AND the store stop refusing a critical act", ["V1_validate_critical_entry", "D1_decide_critical_branch"], "EN1_engine_critical", "S01b_critical_sbar_act_from_gate"),
    ("BOTH_window", "validate AND the store stop checking the behaviour window", ["V3_validate_eligibility"], "EN4_engine_window", "S05_outside_the_behaviour_window"),
]


# ---- the engine's speech-evidence subsystem (src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs), mutated in ONE copy of src + the engine unit tests (incremental builds) and judged by `dotnet test --filter InterpretationContextTests`: the mutant must make a test fail
SE = "src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs"
ENGINE_TESTS: list[tuple[str, str, list[Edit]]] = [
    ("ET1_answer_needs_a_waiting_question", "the engine no longer checks that a patient question is waiting before logging ANSWER_QUESTION evidence", [(SE, "if (a.Params[ActParam] == \"ANSWER_QUESTION\" && !InterpretationContextBuilder.PatientQuestionPending(_s))", "if (false)")]),
    ("ET2_readback_needs_a_waiting_result", "the engine no longer checks that a read-back is waiting before logging CLOSED_LOOP evidence", [(SE, "if (a.Params[ActParam] == \"CLOSED_LOOP\" && !InterpretationContextBuilder.ReadbackPending(_s))", "if (false)")]),
    ("ET3_confidence_range", "the engine accepts a confidence outside 0 to 1", [(SE, "double.IsNaN(conf) || conf < 0 || conf > 1", "false")]),
    ("ET4_steps_stamped_by_the_engine", "the engine no longer stamps the workflow steps on the logged action", [(SE, "o.Effects.Add(StepsEffect + string.Join(\",\", steps.Select(n => n.ToString(CultureInfo.InvariantCulture))));", "")]),
]


# the same store checks, judged END TO END by the engine unit tests (a speech_evidence action applied to a Session, the log, EvidenceStore.FromLog), not by the CLI `evidence-check` shortcut
for _mid, _desc, _edits, _scen in ENGINE:
    ENGINE_TESTS.append(("ET_" + _mid, _desc + " (end to end through the session log)", _edits))

# ---- the interpretation route (server/routers/interpret.py), mutated in a copy of server/ and judged by server/tests/test_interpret_api.py on the real PostgreSQL (needs TEST_DATABASE_URL, or the Docker container of tools/ci/server_tests.py)
RT = "server/routers/interpret.py"
ROUTE: list[tuple[str, str, list[Edit]]] = [
    ("RT1_feature_flag", "the route ignores the speech_interpretation switch", [(RT, "    if not ai_features.enabled(\"speech_interpretation\"):", "    if False:")]),
    ("RT2_busy_slot", "the route proceeds when the model slot is busy", [(RT, "    if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):", "    if not (ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds) or True):")]),
    ("RT3_failure_is_the_menu", "a failing classifier is no longer recorded and answered as the menu", [(RT, "    if failed:", "    if False:")]),
    ("RT4_reason_codes", "the audit row keeps no reason code", [(RT, "reason_codes(out[\"reasons\"], out[\"decision\"])", "[]")]),
    ("RT5_refusal_marked_passed", "a refusal is audited as passed", [(RT, "out[\"decision\"] == \"OFFER_EVIDENCE\"", "True")]),
]


def run_route_tests() -> dict:
    """each route mutant in its own copy of server/ (no .venv), judged by pytest in the server environment; the unmutated copy must pass first."""
    import os
    url = os.environ.get("TEST_DATABASE_URL", "postgresql://postgres:postgres@localhost:54329/procsense_test")
    py = ROOT / "server" / ".venv" / ("Scripts" if os.name == "nt" else "bin") / ("python.exe" if os.name == "nt" else "python")
    out = {}

    def one(edits):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts", ignore_cleanup_errors=True) as t:
            d = Path(t)
            shutil.copytree(ROOT / "server", d / "server", ignore=shutil.ignore_patterns(".venv", "__pycache__"))
            for item in ("content/assessment", "content/ai", "tests/classifier_regression"):
                shutil.copytree(ROOT / item, d / item)
            apply(d, edits)
            p = subprocess.run([str(py), "-m", "pytest", str(d / "server" / "tests" / "test_interpret_api.py"), "-q", "-x", "-p", "no:cacheprovider"], capture_output=True, text=True, encoding="utf-8", cwd=str(d),
                               env={**os.environ, "TEST_DATABASE_URL": url, "REQUIRE_DB": "1", "PYTHONPATH": str(d)})
            return p.returncode, (p.stdout + p.stderr)[-400:]
    code, tail = one([])
    out["baseline"] = {"exit": code, "tail": tail}
    for mid, desc, edits in ROUTE:
        code, tail = one(edits)
        out[mid] = {"exit": code, "killed": code != 0, "tail": tail}
    return out


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
            else:
                shutil.copy2(src, d / item)

        def test() -> tuple[int, str]:
            p = subprocess.run(["dotnet", "test", str(d / "tests" / "ProcSense.Core.Tests"), "-c", "Release", "--nologo", "-v", "q", "--disable-build-servers", "-p:UseSharedCompilation=false", "-p:TreatWarningsAsErrors=false", "--filter", "FullyQualifiedName~InterpretationContextTests"],
                               capture_output=True, text=True, encoding="utf-8", cwd=str(d))
            return p.returncode, (p.stdout + p.stderr)[-300:]
        code, tail = test()
        out["baseline"] = {"exit": code, "tail": tail}
        for mid, desc, edits in ENGINE_TESTS:
            originals = {rel: (d / rel).read_text(encoding="utf-8") for rel, _, _ in edits}
            apply(d, edits)
            code, tail = test()
            out[mid] = {"exit": code, "killed": code != 0, "tail": tail}
            for rel, text in originals.items():
                (d / rel).write_text(text, encoding="utf-8", newline="\n")
    return out


def build_engine(dest: Path, edits: list[Edit]) -> Path:
    """a copy of the engine with the edits applied and built; returns the CLI project."""
    for item in ("src", "Directory.Build.props", "global.json"):
        src = ROOT / item
        if src.is_dir():
            shutil.copytree(src, dest / item, ignore=shutil.ignore_patterns("bin", "obj"))
        elif src.exists():
            shutil.copy2(src, dest / item)
    apply(dest, edits)
    p = subprocess.run(["dotnet", "build", str(dest / "src" / "ProcSense.Cli"), "-c", "Release", "-nologo", "-v", "q", "--disable-build-servers", "-p:UseSharedCompilation=false", "-p:TreatWarningsAsErrors=false"], capture_output=True, text=True, encoding="utf-8", cwd=str(dest))
    if p.returncode != 0:
        raise RuntimeError("engine mutant did not build: " + (p.stdout + p.stderr)[-600:])
    return dest / "src" / "ProcSense.Cli"


def run_engine(base: dict) -> dict:
    by_id = {m[0]: m for m in SINGLE}
    eng = {e[0]: e for e in ENGINE}
    res = {"single": {}, "both": {}}
    for mid, desc, edits, scen in ENGINE:
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts", ignore_cleanup_errors=True) as t:
            proj = build_engine(Path(t), edits)
            out = scenarios(None, proj)
            res["single"][mid] = {"changed": changed(base, out), "verdicts": {s: out[s].get("engine") for s in scen if s in out}}
            for bid, bdesc, service, engine_id, scenario in BOTH:
                if engine_id != mid:
                    continue
                svc_edits = [e for sid in service for e in by_id[sid][2]]
                o = run_mutant(svc_edits, proj)
                res["both"][bid] = {"changed": changed(base, o), "scenario": scenario, "decision": o.get(scenario, {}).get("decision"), "offer": o.get(scenario, {}).get("offer"), "engine": o.get(scenario, {}).get("engine")}
    return res


def copy_tree(dest: Path) -> None:
    for src in ("server", "content/assessment", "content/ai"):
        shutil.copytree(ROOT / src, dest / src, ignore=shutil.ignore_patterns(".venv", "__pycache__", "tests", "static", "migrations", "*.pyc"))


def apply(dest: Path, edits: list[Edit]) -> None:
    for rel, old, new in edits:
        f = dest / rel
        text = f.read_text(encoding="utf-8")
        n = text.count(old)
        if n != 1:
            raise RuntimeError(f"mutation does not match exactly once in {rel} ({n} matches): {old[:70]!r}; the guard moved, update tools/ci/mutation_safety.py")
        f.write_text(text.replace(old, new), encoding="utf-8", newline="\n")


def scenarios(dest: Path | None, cli_project: Path | None = None) -> dict:
    cmd = [PY, str(SCEN), "--impl", str(dest or ROOT)] + (["--cli-project", str(cli_project)] if cli_project else [])
    p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", cwd=str(ROOT))
    if p.returncode != 0:
        return {"__error__": (p.stderr or p.stdout)[-600:]}
    return json.loads(p.stdout)


def outcome(v: dict):
    return json.dumps({k: v[k] for k in v if k not in ("kind", "tag")}, sort_keys=True)


def changed(base: dict, mut: dict) -> list[str]:
    if "__error__" in mut:
        return ["__crash__"]
    return sorted(k for k in base if k in mut and outcome(base[k]) != outcome(mut[k]))


def run_mutant(edits: list[Edit], cli_project: Path | None = None) -> dict:
    with tempfile.TemporaryDirectory(dir=ROOT / "artifacts", ignore_cleanup_errors=True) as t:
        d = Path(t)
        copy_tree(d)
        apply(d, edits)
        return scenarios(d, cli_project)


def run_all(workers: int = 4) -> dict:
    (ROOT / "artifacts").mkdir(exist_ok=True)
    base = scenarios(None)
    if "__error__" in base:
        raise RuntimeError(base["__error__"])
    by_id = {m[0]: m for m in SINGLE}
    with ThreadPoolExecutor(workers) as ex:
        singles = dict(zip(by_id, ex.map(lambda m: run_mutant(m[2]), SINGLE)))
        doubles = dict(zip([d[0] for d in DOUBLE], ex.map(lambda d: run_mutant([e for mid in d[2] for e in by_id[mid][2]]), DOUBLE)))
    return {"baseline": base, "single": {k: {"changed": changed(base, v), "outcomes": {s: v[s] for s in changed(base, v) if s in v}} for k, v in singles.items()},
            "double": {k: {"changed": changed(base, v), "outcomes": {s: v[s] for s in changed(base, v) if s in v}} for k, v in doubles.items()}}


def report(res: dict) -> list[dict]:
    rows = []
    for mid, desc, edits, kills, masked in SINGLE:
        ch = set(res["single"][mid]["changed"])
        rows.append({"mutant": mid, "guard": desc, "killed": bool(ch) and "__crash__" not in ch, "expected_to_change": kills, "changed": sorted(ch), "missing": sorted(set(kills) - ch), "masked_unchanged": sorted(set(masked) - ch), "masked_but_changed": sorted(set(masked) & ch)})
    for mid, desc, parts, offered, engine in DOUBLE:
        ch = res["double"][mid]["changed"]
        o = res["double"][mid]["outcomes"]
        rows.append({"mutant": mid, "guard": desc, "killed": bool(ch), "expected_to_change": offered, "changed": ch, "missing": sorted(set(offered) - set(ch)),
                     "engine_verdicts": {s: o[s].get("engine") if o[s].get("engine") is not None else o[s].get("engine_accepted") for s in offered if s in o}, "expected_engine_verdict": engine})
    return rows


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--engine", action="store_true", help="also the engine mutants (a build each, a few minutes)")
    a = ap.parse_args(argv)
    res = run_all()
    rows = report(res)
    if a.engine:
        tres = run_engine_tests()
        if tres["baseline"]["exit"] != 0:
            raise RuntimeError("the unmutated engine tests do not pass in the copy: " + tres["baseline"]["tail"])
        for mid, desc, edits in ENGINE_TESTS:
            rows.append({"mutant": mid, "guard": desc, "killed": tres[mid]["killed"], "expected_to_change": ["dotnet test InterpretationContextTests"], "changed": [], "missing": [] if tres[mid]["killed"] else ["a failing test"]})
        rres = run_route_tests()
        if rres["baseline"]["exit"] != 0:
            raise RuntimeError("the unmutated route tests do not pass in the copy (is the PostgreSQL container up?): " + rres["baseline"]["tail"])
        for mid, desc, edits in ROUTE:
            rows.append({"mutant": mid, "guard": desc, "killed": rres[mid]["killed"], "expected_to_change": ["pytest test_interpret_api"], "changed": [], "missing": [] if rres[mid]["killed"] else ["a failing test"]})
        eres = run_engine(res["baseline"])
        for mid, desc, edits, scen in ENGINE:
            v = eres["single"][mid]
            ok = all(v["verdicts"].get(s) == want for s, want in scen.items())
            rows.append({"mutant": mid, "guard": desc, "killed": ok, "expected_to_change": list(scen), "changed": v["changed"], "missing": [] if ok else list(scen), "engine_verdicts": v["verdicts"], "expected_engine_verdict": scen})
        for bid, bdesc, service, engine_id, scenario in BOTH:
            v = eres["both"][bid]
            ok = v["engine"] == "accepted"
            rows.append({"mutant": bid, "guard": bdesc, "killed": ok, "expected_to_change": [scenario], "changed": v["changed"], "missing": [] if ok else [scenario], "engine_verdicts": {scenario: v["engine"]}, "expected_engine_verdict": "accepted"})
    survivors = [r for r in rows if not r["killed"] or r["missing"]]
    for r in rows:
        print(("KILLED  " if r["killed"] and not r["missing"] else "SURVIVED"), r["mutant"], "|", r["guard"], "| changed:", ", ".join(r["changed"][:5]) or "-", ("| missing: " + ", ".join(r["missing"])) if r["missing"] else "")
    if a.write:
        out = ROOT / "reports" / "ai" / "safety" / "mutation-service.json"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps({"mutants": rows}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", out.relative_to(ROOT).as_posix())
    print(f"{len(rows) - len(survivors)}/{len(rows)} mutants killed with every expected scenario changed")
    return 1 if survivors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
