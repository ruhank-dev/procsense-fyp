"""The pre-generated pool: packages made ahead of time so a session starts at once and works offline (p1-live-scenario-generation-s10, stage LG-15).

    build(ctx, directory, per_family=6, levels=("beginner", "advanced"), seed0=0, headless=False)   # writes <id>.package.json files and index.json
    package = take(directory, scenario, level, exclusions, verify=True)                           # the next unused package that suits the request, or None

A pooled package is an ordinary sealed ScenarioPackage, made for no one in particular, so it is made from empty history. It carries its own SHA-256 and, when `headless` is on, has also been
played through the engine (idle run and the spine's correct path) before it was kept. The index lists what the pool holds (spine, fingerprint, hash of the file); `take` checks the file against
the index before it serves it, and leaves out the spines and fingerprints the trainee has met. Nothing leaves the pool without being marked used; the used list is the caller's (the backend's
per-trainee history), not the pool's, so the same pool serves many trainees.
"""
from __future__ import annotations

import json
from pathlib import Path

from . import package as pkg_mod, pipeline

INDEX = "index.json"


def build(ctx: pipeline.Context, directory: Path, per_family: int = 6, levels: tuple[str, ...] = ("beginner", "advanced"), seed0: int = 0, headless: bool = False,
          scenarios: tuple[str, ...] = ("S1", "S2")) -> dict:
    """Make `per_family` packages for each family and level. A seed that gives a fallback, a story already in the pool or (headless) a package the engine does not accept is skipped and the next seed is tried."""
    directory = Path(directory)
    entries: list[dict] = []
    seen: set[str] = set()
    skipped: list[dict] = []
    for scenario in scenarios:
        for level in levels:
            made, seed = 0, seed0
            spines: set[str] = set()                   # a family's packages in the pool are different stories, not one story with different names
            while made < per_family and seed < seed0 + per_family * 40:
                request = {"version": 1, "trainee": "trainee-000", "mode": "practice", "scenario": scenario, "level": level, "seed": seed}
                seed += 1
                r = pipeline.generate(request, [], [], ctx, serial=len(entries) + 1)
                if r.package is None:
                    skipped.append({"seed": seed - 1, "scenario": scenario, "level": level, "reason": r.fallback})
                    continue
                p = r.package
                if p["provenance"]["spine"] in spines:
                    skipped.append({"seed": seed - 1, "scenario": scenario, "level": level, "reason": "the story's spine is already in the pool for this family"})
                    continue
                if p["fingerprint"] in seen:
                    skipped.append({"seed": seed - 1, "scenario": scenario, "level": level, "reason": "the story is already in the pool"})
                    continue
                if headless:
                    report = pkg_mod.validate(p, ctx.cases, headless=True)
                    if not report["passed"]:
                        skipped.append({"seed": seed - 1, "scenario": scenario, "level": level, "reason": "engine: " + report["defects"][0]["detail"][:120]})
                        continue
                path = pkg_mod.write(p, directory)
                seen.add(p["fingerprint"])
                spines.add(p["provenance"]["spine"])
                entries.append({"id": p["id"], "scenario": scenario, "level": level, "spine": p["provenance"]["spine"], "fingerprint": p["fingerprint"], "tier": p["case"]["tier"],
                                "file": path.name, "sha256": pkg_mod.file_sha256(path), "headless": headless})
                made += 1
    index = {"version": 1, "content_hash": ctx.index.content_hash, "packages": sorted(entries, key=lambda e: e["id"]), "skipped": skipped}
    (directory / INDEX).write_text(json.dumps(index, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    return index


def load_index(directory: Path) -> dict:
    return json.loads((Path(directory) / INDEX).read_text(encoding="utf-8"))


def take(directory: Path, scenario: str, level: str, exclusions: dict | None = None, content_hash: str | None = None, verify: bool = True) -> dict | None:
    """The first pooled package for this family and level whose spine and story the trainee has not met, checked against the index (file hash) and its own seal. None if the pool has none."""
    directory = Path(directory)
    index = load_index(directory)
    if content_hash and index["content_hash"] != content_hash:
        return None                                                                  # the pool was made from other content: it is stale and not served
    ex = exclusions or {"spines": [], "fingerprints": []}
    for e in index["packages"]:
        if e["scenario"] != scenario or e["level"] != level or e["spine"] in ex["spines"] or e["fingerprint"] in ex["fingerprints"]:
            continue
        path = directory / e["file"]
        if verify and (not path.exists() or pkg_mod.file_sha256(path) != e["sha256"]):
            continue                                                                  # a damaged or changed file is never served
        pkg = json.loads(path.read_text(encoding="utf-8"))
        if verify and not pkg_mod.verify_seal(pkg):
            continue
        return pkg
    return None
