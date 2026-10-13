"""Packages content/ for one mode: the files a build of that mode may ship, and a manifest that proves them (p0-case-library-s01, Implementation Guide B-M11 step 3).

    uv run --project tools tools/content/package.py --mode practice --out build/content-practice
    uv run --project tools tools/content/package.py --mode assessment --out build/content-assessment
    uv run --project tools tools/content/package.py --mode assessment --check       # build into a temporary folder and verify it (CI)

Modes: dev ships everything. practice leaves out every case file (DRAFT, CLINICAL_REVIEW) that is not APPROVED_FOR_TRAINING or APPROVED_FOR_ASSESSMENT. assessment leaves out every case file that is not APPROVED_FOR_ASSESSMENT.
A DRAFT or CLINICAL_REVIEW case file is never shipped in practice or assessment. The manifest has the same `entries` as content/manifest.json (path, bytes, sha256), so the engine's integrity check reads it unchanged,
plus `mode`, a `content_version` (the first 12 characters of the content hash and the mode, so the same files always give the same version) and the list of case files left out with the reason.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import content_manifest  # noqa: E402

CONTENT = ROOT / "content"
MODES = {
    "dev": {"DRAFT", "CLINICAL_REVIEW", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"},
    "practice": {"APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"},
    "assessment": {"APPROVED_FOR_ASSESSMENT"},
}
NEEDS = {"dev": "any status", "practice": "APPROVED_FOR_TRAINING", "assessment": "APPROVED_FOR_ASSESSMENT"}


def plan(mode: str) -> tuple[list[Path], list[dict]]:
    """The files to ship and the case files left out (with why), in path order."""
    shipped, left_out = [], []
    for path in content_manifest.files():
        rel = path.relative_to(CONTENT).as_posix()
        if rel.startswith("case_files/") and path.suffix == ".json":
            case = json.loads(path.read_text(encoding="utf-8"))
            if case["status"] not in MODES[mode]:
                left_out.append({"path": rel, "id": case["id"], "status": case["status"], "reason": f"{mode} needs {NEEDS[mode]}"})
                continue
        shipped.append(path)
    return shipped, left_out


def manifest(mode: str, shipped: list[Path], left_out: list[dict]) -> dict:
    entries = {}
    for p in shipped:
        data = p.read_bytes()
        if p.suffix in (".json", ".csv", ".md"):
            data = data.replace(b"\r\n", b"\n")
        entries[p.relative_to(CONTENT).as_posix()] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    content_hash = hashlib.sha256("\n".join(f"{k}:{v['sha256']}" for k, v in entries.items()).encode("utf-8")).hexdigest()
    return {
        "schema": 1,
        "mode": mode,
        "content_version": f"{content_hash[:12]}-{mode}",
        "content_hash": content_hash,
        "files": len(entries),
        "bytes": sum(v["bytes"] for v in entries.values()),
        "left_out": left_out,
        "entries": entries,
    }


def write(mode: str, out: Path) -> dict:
    shipped, left_out = plan(mode)
    if out.exists():
        shutil.rmtree(out)
    for p in shipped:
        target = out / p.relative_to(CONTENT)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(p, target)
    m = manifest(mode, shipped, left_out)
    (out / "manifest.json").write_text(json.dumps(m, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    return m


def verify(out: Path, mode: str) -> list[str]:
    """What is wrong with a packaged folder: a file that does not hash to its entry, a file not listed, a listed file missing, a case file the mode does not allow."""
    problems = []
    m = json.loads((out / "manifest.json").read_text(encoding="utf-8"))
    if m["mode"] != mode:
        problems.append(f"the manifest says mode {m['mode']}, not {mode}")
    for rel, entry in m["entries"].items():
        path = out / rel
        if not path.exists():
            problems.append(f"{rel}: listed but missing")
            continue
        data = path.read_bytes()
        if path.suffix in (".json", ".csv", ".md"):
            data = data.replace(b"\r\n", b"\n")
        if hashlib.sha256(data).hexdigest() != entry["sha256"]:
            problems.append(f"{rel}: does not match its hash")
    listed = set(m["entries"]) | {"manifest.json"}
    for p in sorted(out.rglob("*")):
        rel = p.relative_to(out).as_posix()
        if p.is_file() and rel not in listed:
            problems.append(f"{rel}: in the folder but not in the manifest")
        if rel.startswith("case_files/") and p.suffix == ".json" and json.loads(p.read_text(encoding="utf-8"))["status"] not in MODES[mode]:
            problems.append(f"{rel}: a case file this mode may not ship")
    return problems


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if "--mode" not in argv or argv[argv.index("--mode") + 1] not in MODES:
        print("usage: package.py --mode dev|practice|assessment (--out <folder> | --check)")
        return 2
    mode = argv[argv.index("--mode") + 1]
    if "--check" in argv:
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "content"
            m = write(mode, out)
            problems = verify(out, mode)
            if problems:
                print(f"FAIL  the {mode} package is not what its manifest says:")
                for p in problems[:10]:
                    print("        " + p)
                return 1
            print(f"{mode} package ok: {m['files']} files, version {m['content_version']}, {len(m['left_out'])} case files left out")
            return 0
    if "--out" not in argv:
        print("usage: package.py --mode dev|practice|assessment (--out <folder> | --check)")
        return 2
    m = write(mode, Path(argv[argv.index("--out") + 1]))
    print(f"wrote {m['files']} files ({m['bytes'] / 1e6:.1f} MB), version {m['content_version']}, {len(m['left_out'])} case files left out of the {mode} package")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
