"""Assembling and validating a ScenarioPackage (p1-live-scenario-generation s07 and s10, stages LG-09, LG-10 and LG-14).

    pkg = assemble(skeleton, objectives, index)            # a ScenarioPackage v1: the case, how it was made, its fingerprint
    report = validate(pkg)                                  # the gate every package passes before anyone plays it

The gate, in order: the package schema; the case against the case schema and Stage B (tools/composer: schema, required fields, constraints, the 12 contradiction types, combinations, difficulty, events, results);
the media the skeleton chose (finding tags, licence, status); the cross-modal rules (crossmodal.py). A package with any defect is not delivered. The checks that need the engine (Stage B 9 and 10, the idle run
and the correct path through the Core) run when the engine can load a generated package (`headless=True`, s10). Nothing here approves anything: a package says GENERATED_PRACTICE_ONLY and nothing more.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

from . import contracts, crossmodal, media as media_mod, packs, retrieve

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / "tools"
GENERATOR = "skeleton-1"


def _stage_b():
    for p in (str(TOOLS), str(TOOLS / "content")):
        if p not in sys.path:
            sys.path.insert(0, p)
    from composer import stage_b  # type: ignore
    return stage_b


def pack_versions() -> dict[str, str]:
    manifest = json.loads((packs.PACKS / "manifest.json").read_text(encoding="utf-8"))
    return {name.removesuffix(".json"): digest[:16] for name, digest in manifest["files"].items()}


def assemble(skeleton: dict, objectives: dict, index: retrieve.Index, models: list[dict] | None = None, stages: list[str] | None = None) -> dict:
    case = skeleton["case"]
    comp = case["composition"]
    pkg = {"version": 1, "id": case["id"], "status": "GENERATED_PRACTICE_ONLY", "request_id": objectives["request_id"], "case": case,
           "provenance": {"generator": GENERATOR, "seed": objectives["seed"], "spine": comp["spine"], "blocks": comp["blocks"], "content_hash": index.content_hash, "packs": pack_versions(),
                          "stages": stages or ["LG-02", "LG-03", "LG-04", "LG-05", "LG-07", "LG-08"]},
           "fingerprint": skeleton["fingerprint"], "quality": {"numeric_log": skeleton.get("numeric_log", []), "media": skeleton.get("media", {}).get("log", [])}}
    if models:
        pkg["provenance"]["models"] = models
    pkg["media"] = skeleton.get("media", {"slots": []})
    pkg["discovery"] = skeleton.get("discovery", [])
    return pkg


def validate(pkg: dict, cases: list[dict] | None = None, headless: bool = False) -> dict:
    """A report: {"passed": bool, "defects": [{"stage", "code", "detail"}]}."""
    defects: list[dict] = []
    schema_doc = {k: v for k, v in pkg.items() if k not in ("media", "discovery")}
    for e in contracts.errors("scenario_package", schema_doc):
        defects.append({"stage": "package", "code": "SCHEMA", "detail": e})
    sb = _stage_b()
    from composer import blocks as B  # type: ignore
    cases = cases if cases is not None else B.load_cases()
    ctx = sb.load_ctx(headless, cases)
    ctx["others"] = cases
    report = sb.validate(pkg["case"], ctx)
    defects += [{"stage": "stage_b", "code": d.code, "detail": d.detail} for d in report.defects]
    for d in media_mod.check_media(pkg["case"], pkg.get("media", {"slots": []})):
        defects.append({"stage": "media", "code": "MEDIA", "detail": d})
    for d in crossmodal.check(pkg["case"], pkg.get("media")):
        defects.append({"stage": "crossmodal", "code": d.rule, "detail": d.detail})
    fp = hashlib.sha256(json.dumps(pkg["case"]["composition"], sort_keys=True).encode("utf-8")).hexdigest()[:16]
    return {"package": pkg["id"], "passed": not defects, "defects": defects, "composition_hash": fp}
