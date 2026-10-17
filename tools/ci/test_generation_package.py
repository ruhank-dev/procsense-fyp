"""Packaging, the pre-generated pool and delivery to the engine (server/generation/package.py, pool.py; p1-live-scenario-generation-s10, stages LG-14 and LG-15).

The engine side (loader, status gate, TableKey) is tested in C# (ScenarioPackageTests); this runs freshly generated packages through the real command-line engine.
"""
import json
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
from generation import objectives as ob, package, pipeline, pool, retrieve  # noqa: E402
from composer import blocks as B  # noqa: E402

CASES = B.load_cases()
CTX = pipeline.Context(retrieve.build_index(), CASES, ob.Tables.load())
REQ = {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced"}
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def cli(*args):
    return subprocess.run(CLI + list(args), cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")


class Files(unittest.TestCase):
    def test_the_same_package_is_the_same_bytes(self):
        a = pipeline.generate({**REQ, "seed": 31}, [], [], CTX).package
        b = pipeline.generate({**REQ, "seed": 31}, [], [], CTX).package
        self.assertEqual(package.file_text(a), package.file_text(b))
        self.assertTrue(package.file_text(a).endswith("}\n"))
        with tempfile.TemporaryDirectory() as t:
            p1, p2 = package.write(a, Path(t) / "x"), package.write(b, Path(t) / "y")
            self.assertEqual(package.file_sha256(p1), package.file_sha256(p2))

    def test_the_committed_fixture_is_a_valid_sealed_package(self):
        fx = json.loads((ROOT / "tests" / "fixtures" / "generated" / "GEN-S1-0003-001.package.json").read_text(encoding="utf-8"))
        self.assertTrue(package.verify_seal(fx))
        self.assertEqual(fx["status"], "GENERATED_PRACTICE_ONLY")
        self.assertEqual(fx["case"]["status"], "GENERATED_PRACTICE_ONLY")


class Pool(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dir = Path(tempfile.mkdtemp())
        cls.index = pool.build(CTX, cls.dir / "a", per_family=2, seed0=100)

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.dir, ignore_errors=True)

    def test_it_holds_what_was_asked_and_every_file_is_a_sealed_package(self):
        self.assertEqual(len(self.index["packages"]), 2 * 2 * 2)
        for e in self.index["packages"]:
            p = json.loads((self.dir / "a" / e["file"]).read_text(encoding="utf-8"))
            self.assertTrue(package.verify_seal(p))
            self.assertEqual(package.file_sha256(self.dir / "a" / e["file"]), e["sha256"])
        self.assertEqual(len({e["fingerprint"] for e in self.index["packages"]}), len(self.index["packages"]))

    def test_the_same_seed_makes_the_same_pool_byte_for_byte(self):
        again = pool.build(CTX, self.dir / "b", per_family=2, seed0=100)
        self.assertEqual([(e["id"], e["sha256"]) for e in again["packages"]], [(e["id"], e["sha256"]) for e in self.index["packages"]])

    def test_take_serves_a_suitable_package_and_leaves_out_what_was_met(self):
        first = pool.take(self.dir / "a", "S1", "advanced")
        self.assertEqual(first["case"]["scenario"], "S1")
        second = pool.take(self.dir / "a", "S1", "advanced", {"spines": [first["provenance"]["spine"]], "fingerprints": []})
        self.assertNotEqual(second["provenance"]["spine"], first["provenance"]["spine"])
        third = pool.take(self.dir / "a", "S1", "advanced", {"spines": [], "fingerprints": [first["fingerprint"]]})
        self.assertEqual(third["fingerprint"], second["fingerprint"])
        self.assertIsNone(pool.take(self.dir / "a", "S1", "advanced", {"spines": [], "fingerprints": [first["fingerprint"], second["fingerprint"]]}))

    def test_a_changed_file_or_a_stale_pool_is_never_served(self):
        d = self.dir / "c"
        pool.build(CTX, d, per_family=1, levels=("advanced",), scenarios=("S1",), seed0=200)
        e = pool.load_index(d)["packages"][0]
        self.assertIsNotNone(pool.take(d, "S1", "advanced"))
        (d / e["file"]).write_text((d / e["file"]).read_text(encoding="utf-8").replace('"age"', '"age "', 1), encoding="utf-8", newline="\n")
        self.assertIsNone(pool.take(d, "S1", "advanced"))
        self.assertIsNone(pool.take(self.dir / "a", "S1", "advanced", content_hash="0" * 16))

    def test_nothing_suitable_gives_none(self):
        self.assertIsNone(pool.take(self.dir / "a", "S3", "advanced"))

    def test_serving_from_the_pool_takes_a_fraction_of_a_second(self):
        t = time.monotonic()
        pool.take(self.dir / "a", "S2", "beginner")
        self.assertLess(time.monotonic() - t, 2.0)


class Engine(unittest.TestCase):
    def test_generated_packages_play_through_the_real_engine_and_the_gate_refuses_assessment(self):
        with tempfile.TemporaryDirectory() as t:
            seen = set()
            for seed in range(40, 46):
                r = pipeline.generate({**REQ, "scenario": "S1" if seed % 2 else "S2", "seed": seed}, [], [], CTX)
                p = r.package
                path = package.write(p, Path(t))
                report = package.validate(p, CASES, headless=True)
                self.assertTrue(report["passed"], report["defects"][:2])
                seen.add(p["provenance"]["spine"])
            self.assertGreater(len(seen), 3)
            fixture = ROOT / "tests" / "fixtures" / "generated" / "GEN-S1-0003-001.package.json"
            out = Path(t) / "o.json"
            refused = cli("run", "--scenario", str(fixture), "--seed", "42", "--mode", "assessment", "--actions", str(ROOT / "tests" / "golden" / "VAR-S1-G_correct.actions.json"), "--out", str(out))
            self.assertNotEqual(refused.returncode, 0)
            self.assertIn("STATUS_TOO_LOW", refused.stdout + refused.stderr)
            played = cli("run", "--scenario", str(fixture), "--seed", "42", "--actions", str(ROOT / "tests" / "golden" / "VAR-S1-G_correct.actions.json"), "--out", str(out))
            self.assertEqual(played.returncode, 0, played.stdout + played.stderr)
            log = out.with_suffix(".session.jsonl")
            self.assertIn(package.file_sha256(fixture), log.read_text(encoding="utf-8").split("\n")[0])      # the session header names the package it was played from
            replay = cli("replay", "--scenario", str(fixture), "--log", str(log))
            self.assertEqual(replay.returncode, 0, replay.stdout + replay.stderr)


if __name__ == "__main__":
    unittest.main()
