"""Makes a session file written by the real engine, with the header the hosted service verifies (p1-backend-s13).

    uv run --project tools tools/demo/make_demo_session.py [--out demo_session.jsonl] [--case VAR-S1-E] [--seed 42] [--actions tests/golden/<case>_correct.actions.json]

It packages the content for practice mode (the same package the service holds for this commit), runs the engine on a scripted set of actions with a fresh session id, and writes the session file.
The cases whose scripted run finishes with a verdict (VAR-S1-E, VAR-S1-PB, VAR-S2-B, STK-S1-D) make the best demonstration; VAR-S1-A stops at the first workflow steps and ends "in progress".
The file only verifies on a service built from the same commit: the content version in its header has to be one the service holds.
"""
from __future__ import annotations

import argparse
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "content"))
import package  # noqa: E402


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, default=Path("demo_session.jsonl"))
    parser.add_argument("--case", default="VAR-S1-E")
    parser.add_argument("--seed", default="42")
    parser.add_argument("--actions", type=Path, default=None, help="default: tests/golden/<case>_correct.actions.json")
    args = parser.parse_args(argv)
    out = args.out.resolve()
    actions = args.actions or ROOT / "tests" / "golden" / f"{args.case}_correct.actions.json"
    if not actions.exists():
        print(f"no scripted actions for {args.case} at {actions}: pass --actions")
        return 1
    with tempfile.TemporaryDirectory() as tmp:
        content = Path(tmp) / "content"
        version = package.write("practice", content)["content_version"]
        cmd = ["dotnet", "run", "-c", "Release", "--project", str(ROOT / "src" / "ProcSense.Cli"), "--", "run", "--case", args.case, "--seed", args.seed, "--actions", str(actions),
               "--out", str(Path(tmp) / "result.json"), "--log", str(out), "--mode", "practice", "--content", str(content), "--session-id", str(uuid.uuid4())]
        done = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if done.returncode != 0 or not out.exists():
        print((done.stdout or "") + (done.stderr or ""))
        print("the engine could not write the session")
        return 1
    print(f"wrote {out}\ncontent version in its header: {version}\nupload it with:  task headset-sim -- --url <the service address> --file {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
