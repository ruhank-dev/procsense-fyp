"""The deterministic generation pipeline end to end (p1-live-scenario-generation s03 to s09).

    result = generate(request, history, novelty_history, context)
    result.package        # a sealed ScenarioPackage, or None when the caller must serve a pooled package or a library case
    result.fallback       # None | "FALLBACK-POOL-LIBRARY" (FB-02, FB-03)

Order: accept the request, derive the objectives, build a skeleton that passes the novelty check (up to 5 attempts, else the most novel, logged), assemble the package, run the gate (schema, Stage B, media,
cross-modal) and the quality metrics, repair at most 3 rounds (each repeats every check from the first), then seal with the fingerprint levels, novelty report, reproducibility key and SHA-256. No model is
called here; the model steps (s04, s13, s15) wrap this baseline and fall back to it.
"""
from __future__ import annotations

from dataclasses import dataclass, field

from . import contracts, novelty, objectives as ob, package, quality, repair, retrieve, skeleton


@dataclass
class Context:
    """What the pipeline reads and never changes: the index, the case library, the tables, the rule file and the levels of the library's cases."""
    index: retrieve.Index
    cases: list[dict]
    tables: ob.Tables
    behaviour_labels: dict[str, str] = field(default_factory=dict)
    library: list[dict] | None = None
    library_levels: list[dict] | None = None
    discovery_all: list[dict] | None = None

    def discovery(self, spine: str) -> list[dict]:
        return skeleton.discovery_rules(spine) if self.discovery_all is None else [r for r in self.discovery_all if r["case"] == spine]


@dataclass
class Result:
    package: dict | None
    fallback: str | None
    request_id: str
    log: list[dict] = field(default_factory=list)
    defects: list[dict] = field(default_factory=list)


def _candidate(objectives: dict, ctx: Context, serial: int, **kw) -> tuple[dict, dict]:
    sk = skeleton.build_skeleton(objectives, ctx.index, ctx.cases, ctx.tables, ctx.behaviour_labels, ctx.library, serial, **kw)
    return sk, novelty.levels_of(sk["case"], sk["media"])


def _gate(pkg: dict, objectives: dict, ctx: Context) -> list[dict]:
    """Every check, from the first: the package gate then the quality metrics. A defect list, empty when the package is good."""
    report = package.validate(pkg, ctx.cases)
    defects = list(report["defects"])
    q = quality.evaluate(pkg["case"], objectives, ctx.tables, ctx.discovery(pkg["case"]["composition"]["spine"]))
    pkg["quality"]["metrics"] = q["metrics"]
    pkg["quality"]["not_measured"] = q["not_measured"]
    pkg["quality"]["advisory"] = q["advisory"]
    pkg["quality"]["unmet_dimensions"] = q["unmet_dimensions"]
    defects += [{"stage": "quality", "code": f["code"], "detail": f["detail"]} for f in q["failures"]]
    return defects


def generate(request: dict, history: list[ob.SessionRecord], novelty_history: list[dict], ctx: Context, draw_seed=None, recent_fingerprints: list[str] | None = None,
             deliberate: bool = False, budget: repair.Budget | None = None, serial: int = 1) -> Result:
    req = contracts.accept_request(request, draw_seed or (lambda n: 0))
    objectives = ob.derive(req, history, ctx.tables, recent_fingerprints)
    budget = budget or repair.Budget()
    log: list[dict] = []

    def build(attempt: int, avoid: list[str]):
        o = dict(objectives)
        o["seed"] = objectives["seed"] + attempt * 15485863
        o["exclusions"] = {**objectives["exclusions"], "spines": list(dict.fromkeys([*objectives["exclusions"]["spines"], *avoid]))}
        sk, lv = _candidate(o, ctx, serial)
        sk["_objectives"] = o
        return sk, lv

    try:
        sk, nov, attempts = novelty.pick_novel(build, novelty_history, ctx.library_levels, deliberate)
    except Exception as e:                                  # nothing could be built: the family has no usable spine
        return Result(None, "FALLBACK-POOL-LIBRARY", objectives["request_id"], [{"stage": "skeleton", "error": str(e)}])
    o = sk.pop("_objectives")
    pkg = package.assemble(sk, o, ctx.index)
    pkg["provenance"]["novelty_attempts"] = attempts
    if o["seed"] != objectives["seed"]:
        pkg["provenance"]["base_seed"] = objectives["seed"]
    defects = _gate(pkg, o, ctx)
    tried: list[str] = []
    repairs: list[dict] = []
    while defects and not budget.spent():
        strategy = repair.choose(defects, tried)
        if strategy is None:
            break
        budget.take()
        tried.append(strategy)
        change, kw = repair.kwargs_for(strategy, o, pkg["provenance"]["spine"], budget.used)
        o2 = {**o, **change}
        try:
            sk2, lv2 = _candidate(o2, ctx, serial, **kw)
        except Exception as e:                                               # the repair itself found nothing to build
            repairs.append({"round": budget.used, "strategy": strategy, "codes": sorted({d["code"] for d in defects}), "result": f"no candidate: {e}"})
            continue
        nov2 = novelty.check(lv2, novelty_history, ctx.library_levels, deliberate)
        pkg2 = package.assemble(sk2, o2, ctx.index)
        pkg2["provenance"]["novelty_attempts"] = attempts
        pkg2["provenance"]["base_seed"] = objectives["seed"]
        d2 = _gate(pkg2, o2, ctx)
        repairs.append({"round": budget.used, "strategy": strategy, "codes": sorted({d["code"] for d in defects}), "result": "passed" if not d2 else f"{len(d2)} defect(s) remain"})
        pkg, o, defects, nov, sk = pkg2, o2, d2, nov2, sk2
        if nov.verdict == "not_novel":
            nov.verdict = "low_novelty"
    if repairs:
        pkg["provenance"]["repairs"] = repairs
    if defects:
        return Result(None, "FALLBACK-POOL-LIBRARY", objectives["request_id"], repairs, defects)
    levels = novelty.levels_of(pkg["case"], pkg["media"])
    package.seal(pkg, o, levels, nov.as_dict())
    return Result(pkg, None, objectives["request_id"], log + repairs)
