"""Mutation tests of the speech-safety guards (p1-ai-language-s16, ADR-0023): each critical check is DISABLED in a copy of the code and the final outcome (the interpretation path's decision AND the engine EvidenceStore's verdict) must change.
Machinery and catalogue: tools/ci/mutation_safety.py; the scenarios: tools/ci/safety_scenarios.py. `python tools/ci/test_mutation_safety.py` (about 12 minutes: a copy and a run per mutant, an engine build per engine mutant, one incremental test build per subsystem mutant; the engine part needs `dotnet`).

What is pinned, and why it is not the same as the recall tests:
  * the unmutated outcomes (every refusal refused, the positive controls accepted) so a mutant is compared with a known good run;
  * every single-guard mutant is KILLED: the guard's own `layer` scenario changes (a guard behind another one is still tested on its own);
  * every double mutant (all the service copies of one barrier removed) is OFFERED by the service, and the engine's verdict is the one the catalogue says: refused by its own checks for a critical act and a wrong window (the backstop holds), ACCEPTED when only the service could know
    (the critical-mass cap: 8 of 9 sentences; negation and the other non-performed kinds: 34 of 84 counterexamples; a behaviour the session already credited). That is the evidence that the service guards matter and which of them are the last line;
  * the engine's speech-evidence subsystem checks (a waiting question for ANSWER_QUESTION, a waiting read-back for CLOSED_LOOP, the confidence range, the steps stamp) are each removed in a copy and an engine unit test must fail;
  * every engine mutant is killed and, with the service guards also gone, a critical act and an out-of-window act ARE credited, so no single layer is carrying a case on its own without a test saying so.
A mutation that no longer matches exactly once fails loudly: the guard moved and the catalogue must follow it."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "ci"))
import mutation_safety as M  # noqa: E402

RES: dict = {}


def results() -> dict:
    if not RES:
        RES["service"] = M.run_all(workers=4)
        RES["rows"] = M.report(RES["service"])
    return RES


class Baseline(unittest.TestCase):
    def test_unmutated_refusals_and_controls(self):
        b = results()["service"]["baseline"]
        for sid in ("S01a_critical_act_from_gate", "S01b_critical_sbar_act_from_gate", "S02_critical_cue_with_ordinary_act", "S11_classifier_unavailable", "S12b_second_clarification", "S16_classifier_raises"):
            self.assertEqual(b[sid]["decision"], "MENU", sid)
        for sid in ("S03_negated_sentence", "S04_statement_act_asked_as_question", "S05_outside_the_behaviour_window", "S06_answer_without_a_waiting_question", "S07_readback_without_a_waiting_result", "S08_behaviour_already_credited", "S09_gate_did_not_credit", "S10_critical_mass_over_the_cap"):
            self.assertEqual((b[sid]["decision"], b[sid]["offer"]), ("ABSTAIN", None), sid)
        self.assertEqual((b["S13_positive_control"]["decision"], b["S13_positive_control"]["engine"]), ("OFFER_EVIDENCE", "accepted"))
        self.assertEqual(b["S17_cancellation"]["decision"], "PROPAGATED")
        for sid in ("G01_cap_blocked_unsafe_real_gate", "G02_non_performed_real_gate", "G03_critical_sentences_real_gate"):
            self.assertEqual((b[sid]["offered"], b[sid]["engine_accepted"]), (0, 0), sid)
        # the engine on direct offers
        want = {"E01_critical_act_offered": "CRITICAL_NEEDS_DETERMINISTIC_EVIDENCE", "E02_unknown_act_offered": "UNKNOWN_ACT", "E03_below_the_act_threshold": "LOW_CONFIDENCE", "E04_no_running_step": "NO_RUNNING_STEP", "E05_outside_the_window": "CONTEXT_MISMATCH",
                "E06b_same_behaviour_again": "DUPLICATE_BEHAVIOUR", "E07b_same_utterance_id_again": "DUPLICATE_UTTERANCE", "E07d_same_utterance_id_for_another_behaviour": "DUPLICATE_UTTERANCE", "E08_positive_control": "accepted", "S14b_same_behaviour_again_stale_context": "DUPLICATE_BEHAVIOUR",
                "S15b_same_utterance_id_again_stale_context": "DUPLICATE_UTTERANCE"}
        for sid, verdict in want.items():
            self.assertEqual(b[sid]["engine"], verdict, sid)
        for sid in ("L17_gate_control_credits_a_plain_sentence",):
            self.assertTrue(b[sid]["value"], sid)           # the control: the stand-in embedding IS credited, so the TF-IDF and cue scenarios can show their guard
        self.assertFalse(b["L18_gate_needs_the_tfidf_head_to_agree"]["value"])
        self.assertEqual(b["L19_gate_cue_on_an_ordinary_looking_embedding"]["value"], [False, False, False])


class ServiceGuards(unittest.TestCase):
    def test_every_single_guard_mutant_is_killed_by_the_scenarios_named_for_it(self):
        bad = [(r["mutant"], r["missing"]) for r in results()["rows"] if r["mutant"] in {m[0] for m in M.SINGLE} and (not r["killed"] or r["missing"])]
        self.assertEqual(bad, [], "a disabled guard went unnoticed: the scenarios named for it did not change")

    def test_each_guard_is_tested_on_its_own_even_when_another_layer_masks_it(self):
        rows = {r["mutant"]: r for r in results()["rows"]}
        for mid, desc, edits, kills, masked in M.SINGLE:
            self.assertTrue(kills, f"{mid} names no scenario")
            self.assertFalse(set(kills) - set(rows[mid]["changed"]), mid)

    def test_double_mutants_are_offered_and_the_engine_verdict_is_the_documented_one(self):
        res = results()["service"]
        for mid, desc, parts, offered, engine in M.DOUBLE:
            out = res["double"][mid]["outcomes"]
            for sid in offered:
                self.assertIn(sid, out, f"{mid}: {sid} did not change")
                o = out[sid]
                if o["kind"] == "real_gate":
                    self.assertGreater(o["offered"], 0, f"{mid}: nothing was offered")
                    if engine == "accepted":
                        self.assertGreater(o["engine_accepted"], 0, f"{mid}: the engine credited nothing")
                else:
                    self.assertEqual(o["decision"], "OFFER_EVIDENCE", mid)
                    self.assertEqual(o["engine"], engine, mid)

    def test_the_cap_and_the_non_performed_kinds_are_the_last_line_and_the_numbers_are_pinned(self):
        out = results()["service"]["double"]
        cap = out["DBL_cap"]["outcomes"]["G01_cap_blocked_unsafe_real_gate"]
        non = out["DBL_assertion"]["outcomes"]["G02_non_performed_real_gate"]
        self.assertEqual((cap["rows"], cap["engine_accepted"]), (9, 8))                  # 8 of the 9 sentences only the cap holds back would be CREDITED by the engine without it
        self.assertEqual((non["rows"], non["engine_accepted"]), (84, 34))                # 34 of 84 non-performed counterexamples would be credited without the assertion layer (the head alone does not refuse them)


@unittest.skipUnless(__import__("shutil").which("dotnet"), "needs dotnet to build the engine mutants")
class EngineGuards(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.res = M.run_engine(results()["service"]["baseline"])
        cls.tests = M.run_engine_tests()

    def test_every_engine_check_is_needed(self):
        for mid, desc, edits, scen in M.ENGINE:
            v = self.res["single"][mid]["verdicts"]
            for sid, want in scen.items():
                self.assertEqual(v.get(sid), want, f"{mid}: {sid}")

    def test_the_speech_evidence_subsystem_checks_are_each_caught_by_an_engine_unit_test(self):
        self.assertEqual(self.tests["baseline"]["exit"], 0, self.tests["baseline"]["tail"])
        for mid, desc, edits in M.ENGINE_TESTS:
            self.assertTrue(self.tests[mid]["killed"], f"{mid}: {desc} - no engine test failed")

    def test_with_the_service_guards_gone_too_a_critical_act_and_an_out_of_window_act_are_credited(self):
        for bid, desc, service, engine_id, scenario in M.BOTH:
            self.assertEqual(self.res["both"][bid]["engine"], "accepted", bid)


def _db_url() -> str:
    import os
    return os.environ.get("TEST_DATABASE_URL", "postgresql://postgres:postgres@localhost:54329/procsense_test")


def _db_up() -> bool:
    import os
    import socket
    if os.environ.get("TEST_DATABASE_URL"):
        return True
    try:
        socket.create_connection(("localhost", 54329), timeout=1).close()       # the throwaway PostgreSQL of tools/ci/server_tests.py
        return True
    except OSError:
        return False


@unittest.skipUnless(_db_up(), "needs the PostgreSQL of tools/ci/server_tests.py (running on port 54329) or TEST_DATABASE_URL; the route tests are mutation-run where the database is available")
class RouteGuards(unittest.TestCase):
    def test_the_route_fallbacks_and_audit_rules_are_each_caught_by_a_route_test(self):
        r = M.run_route_tests()
        self.assertEqual(r["baseline"]["exit"], 0, r["baseline"]["tail"])
        for mid, desc, edits in M.ROUTE:
            self.assertTrue(r[mid]["killed"], f"{mid}: {desc} - no route test failed")


if __name__ == "__main__":
    unittest.main()
