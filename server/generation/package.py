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
    case["status"] = "GENERATED_PRACTICE_ONLY"                  # the only status the generator can give (ADR-0006); a person's approval is a different act
    comp = case["composition"]
    pkg = {"version": 1, "id": case["id"], "status": "GENERATED_PRACTICE_ONLY", "request_id": objectives["request_id"], "case": case,
           "provenance": {"generator": GENERATOR, "seed": objectives["seed"], "spine": comp["spine"], "blocks": comp["blocks"], "content_hash": index.content_hash, "packs": pack_versions(),
                          "stages": stages or ["LG-02", "LG-03", "LG-04", "LG-05", "LG-07", "LG-08"]},
           "fingerprint": skeleton["fingerprint"], "quality": {"numeric_log": skeleton.get("numeric_log", []), "media": skeleton.get("media", {}).get("log", [])}}
    plan = skeleton.get("plan") or {}
    if plan.get("method") == "model":
        models = list(models or [])
        seen = {(a["model"], a["model_digest"]) for a in plan["audit"] if a["outcome"] == "ok"}
        models += [{"role": "planner", "name": m, "digest": d} for m, d in sorted(seen)]
        pkg["provenance"].setdefault("prompts", {}).update({f"{a['prompt_id']}@{a['prompt_version']}": a["prompt_sha256"] for a in plan["audit"]})
        pkg["provenance"]["stages"].append("LG-04")
        pkg["provenance"]["plan"] = {"method": "model", "rounds": plan["rounds"], "reasons": plan["reasons"]}
    elif plan:
        pkg["provenance"]["plan"] = {"method": "solver", "why": plan.get("why", "")}
    nar = skeleton.get("narrative")
    if nar and any(x["method"] == "model" for x in nar["lines"]):
        pkg["narrative"] = {"persona": nar["persona"], "lines": nar["lines"]}
        seen = {(a["model"], a["model_digest"]) for a in nar["audit"] if a["outcome"] == "ok"}
        models = (models or []) + [{"role": "narrative", "name": m, "digest": d} for m, d in sorted(seen)]
        pkg["provenance"]["prompts"] = {f"{a['prompt_id']}@{a['prompt_version']}": a["prompt_sha256"] for a in nar["audit"]}
        pkg["provenance"]["stages"].append("LG-06")
        pkg["quality"]["narrative_audit"] = [{k: a[k] for k in ("task", "latency_ms", "attempts", "outcome", "input_hash", "output_hash")} for a in nar["audit"]]
    elif nar:
        pkg["narrative"] = {"persona": nar["persona"], "lines": nar["lines"]}
    if models:
        pkg["provenance"]["models"] = models
    pkg["media"] = skeleton.get("media", {"slots": []})
    pkg["discovery"] = skeleton.get("discovery", [])
    return pkg


def canonical(doc: dict) -> bytes:
    return json.dumps(doc, sort_keys=True, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def file_text(pkg: dict) -> str:
    """The text of a package file: the same package always gives the same bytes (two-space indent, keys in the order the generator wrote them, one newline at the end). The engine hashes this text."""
    return json.dumps(pkg, indent=2, ensure_ascii=False) + "\n"


def write(pkg: dict, directory: Path) -> Path:
    path = Path(directory) / f"{pkg['id']}.package.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(file_text(pkg), encoding="utf-8", newline="\n")
    return path


def file_sha256(path: Path) -> str:
    return hashlib.sha256(Path(path).read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def reproducibility_key(objectives: dict, pkg: dict) -> str:
    """The same request, seed, packs, block library, prompts and models give the same key (spec section 6). Named seed streams per stage and slot are the generators' own; this key names the inputs."""
    prov = pkg["provenance"]
    doc = {"request": {k: objectives[k] for k in ("scenario", "level", "seed")}, "exclusions": objectives["exclusions"], "packs": prov["packs"], "content_hash": prov["content_hash"],
           "generator": prov["generator"], "models": prov.get("models", []), "prompts": prov.get("prompts", {})}
    return hashlib.sha256(canonical(doc)).hexdigest()


def seal(pkg: dict, objectives: dict, levels: dict | None = None, novelty: dict | None = None) -> dict:
    """Add the fingerprint levels, the novelty report, the reproducibility key and the package's own SHA-256 (over everything else). A changed package no longer matches its hash."""
    pkg.pop("package_sha256", None)
    if levels:
        pkg["fingerprint_levels"] = levels
    if novelty:
        pkg["novelty"] = novelty
    pkg["reproducibility_key"] = reproducibility_key(objectives, pkg)
    pkg["package_sha256"] = hashlib.sha256(canonical(pkg)).hexdigest()
    return pkg


def verify_seal(pkg: dict) -> bool:
    body = {k: v for k, v in pkg.items() if k != "package_sha256"}
    return pkg.get("package_sha256") == hashlib.sha256(canonical(body)).hexdigest()


def validate(pkg: dict, cases: list[dict] | None = None, headless: bool = False) -> dict:
    """A report: {"passed": bool, "defects": [{"stage", "code", "detail"}]}."""
    defects: list[dict] = []
    schema_doc = {k: v for k, v in pkg.items() if k not in ("media", "discovery")}
    if "package_sha256" in pkg and not verify_seal(pkg):
        defects.append({"stage": "package", "code": "HASH", "detail": "the package no longer matches its SHA-256"})
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
