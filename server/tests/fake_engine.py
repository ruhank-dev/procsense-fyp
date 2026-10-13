"""A stand-in for the engine's `verify` command, so the verification worker can be tested without .NET. Its behaviour is chosen by the FAKE_ENGINE_MODE environment variable.

It takes the arguments the real command takes (`verify --log f --html h --content dir [--package p]`) and prints one line of JSON, like the real one.
"""
import json
import os
import sys
import time

mode = os.environ.get("FAKE_ENGINE_MODE", "ok")
args = sys.argv[1:]
log_path = args[args.index("--log") + 1]
html_path = args[args.index("--html") + 1]
header = json.loads(open(log_path, encoding="utf-8").readline())["header"]

FACTS = {
    "schema": "debrief_facts.v1", "case": header["case"], "mode": header["mode"], "end_state": "READY_SAFE", "result": "SUCCESS",
    "items": [
        {"id": "credit.test_appropriate", "behaviour": "b16", "severity": "credit", "status": "credit", "points": 1, "evidence": [2, 5], "rule_id": "RUBRIC.test_appropriate", "source": "SR-2864", "sentence": "x"},
        {"id": "minor.no_introduction", "behaviour": "b13", "severity": "minor", "status": "penalty", "points": -1, "evidence": [], "rule_id": "RUBRIC.no_introduction", "source": "SR-2864", "sentence": "y"},
    ],
    "competencies": [
        {"behaviour": "b16", "label": "Recognises need", "credits": 1, "penalties": 0, "net": 1, "weight": 1.0, "items": 1},
        {"behaviour": "b13", "label": "Checks", "credits": 0, "penalties": 1, "net": -1, "weight": 1.0, "items": 1},
        {"behaviour": "b01", "label": "Asks", "credits": 0, "penalties": 0, "net": 0, "weight": 1.0, "items": 0},
    ],
}


def verdict(ok, reason="", detail=""):
    return {"ok": ok, "reason": reason, "detail": detail, "case": header["case"], "mode": header["mode"], "content_version": header.get("content_version", ""),
            "engine_version": header.get("engine_version", ""), "header_version": 2, "expected_hash": header["log_hash"], "log_hash": header["log_hash"],
            "end_state": FACTS["end_state"] if ok else "", "result": FACTS["result"] if ok else "", "process_total": 0.0 if ok else 0, "facts": FACTS if ok else None}


if mode == "ok":
    open(html_path, "w", encoding="utf-8").write("<!doctype html><html><body>report</body></html>")
    print(json.dumps(verdict(True)))
elif mode == "hash_mismatch":
    print(json.dumps(verdict(False, "HASH_MISMATCH", "the log does not replay to the hash in its header")))
    sys.exit(1)
elif mode == "not_allowed":
    print(json.dumps(verdict(False, "CASE_NOT_ALLOWED_FOR_MODE", "needs APPROVED_FOR_ASSESSMENT")))
    sys.exit(1)
elif mode == "package_loader_missing":
    print(json.dumps(verdict(False, "PACKAGE_LOADER_MISSING", "no loader")))
    sys.exit(1)
elif mode == "crash":
    print("Unhandled exception: boom", file=sys.stderr)
    sys.exit(3)
elif mode == "garbage":
    print("this is not a verdict")
elif mode == "slow":
    time.sleep(30)
elif mode == "huge":
    sys.stdout.write("x" * (9 * 1024 * 1024))
elif mode == "contradiction":
    print(json.dumps(verdict(True)))   # says ok but exits with the refusal code
    sys.exit(1)
elif mode == "environment":
    leaked = [k for k in os.environ if any(w in k.upper() for w in ("KEY", "SECRET", "PASSWORD", "DATABASE", "TOKEN"))]
    print(json.dumps(verdict(not leaked, "ENVIRONMENT_LEAK" if leaked else "", ",".join(leaked))))
    sys.exit(1 if leaked else 0)
elif mode == "records_package":
    pkg = args[args.index("--package") + 1] if "--package" in args else ""
    print(json.dumps(verdict(False, "PACKAGE_LOADER_MISSING" if pkg else "PACKAGE_MISSING", pkg and open(pkg, encoding="utf-8").read())))
    sys.exit(1)
