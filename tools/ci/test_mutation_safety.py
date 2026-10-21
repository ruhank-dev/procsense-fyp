"""Mutation tests of the speech-path guards (ADR-0025): each guard is disabled in a copy of the code and a test must fail. Machinery and catalogue: tools/ci/mutation_safety.py. `python tools/ci/test_mutation_safety.py`
(the contract group needs no database; the route group needs the PostgreSQL of tools/ci/server_tests.py or TEST_DATABASE_URL; the engine group needs dotnet and a build per mutant, a few minutes).
A mutation that no longer matches exactly once fails loudly: the guard moved and the catalogue must follow it."""
import os
import shutil
import socket
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "ci"))
import mutation_safety as M  # noqa: E402


def db_up() -> bool:
    if os.environ.get("TEST_DATABASE_URL"):
        return True
    try:
        socket.create_connection(("localhost", 54329), timeout=1).close()
        return True
    except OSError:
        return False


def check(case, res, mutants):
    case.assertEqual(res["baseline"]["exit"], 0, res["baseline"]["tail"])
    for mid, desc, edits in mutants:
        case.assertTrue(res[mid]["killed"], f"{mid}: {desc} - no test failed")


class ContractGuards(unittest.TestCase):
    def test_every_contract_and_reader_guard_is_caught_by_an_evaluator_test(self):
        check(self, M.run_contract_tests(), M.CONTRACT)


@unittest.skipUnless(db_up(), "needs the PostgreSQL of tools/ci/server_tests.py (port 54329) or TEST_DATABASE_URL")
class RouteGuards(unittest.TestCase):
    def test_the_route_fallbacks_and_audit_rules_are_each_caught_by_a_route_test(self):
        check(self, M.run_route_tests(), M.ROUTE)


@unittest.skipUnless(shutil.which("dotnet"), "needs dotnet to build the engine mutants")
class EngineGuards(unittest.TestCase):
    def test_every_engine_check_is_caught_by_an_engine_test(self):
        check(self, M.run_engine_tests(), M.ENGINE)


if __name__ == "__main__":
    unittest.main()
