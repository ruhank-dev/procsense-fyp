"""The offline evaluation loop of live generation (p1-live-scenario-generation-s12, stage LG-21).

    uv run --project tools tools/generation/evaluate.py run [--requests 120] [--seed 0] [--log file.jsonl] [--write]    # make a report (from a simulated batch, or from a service's request log)
    uv run --project tools tools/generation/evaluate.py check                                                          # every release of packs, prompts and thresholds cites a report that exists

The loop learns from evidence and never inside a session: it reads what generation did (outcome, defect codes, repairs, novelty, quality, the story used), counts, and proposes changes to the parameter packs, the
block weights, the repair table or the thresholds. A proposal is only a proposal: it takes effect in a reviewed release (`content/generation/releases.json`), and a release must cite the evaluation report that
asked for it. Nothing in server/ imports this file (checked by the test), so nothing here can run while a trainee plays.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
REPORTS = ROOT / "docs" / "research" / "generation_evaluations"
RELEASES = ROOT / "content" / "generation" / "releases.json"
PACKS_MANIFEST = ROOT / "content" / "generation" / "packs" / "manifest.json"

# when a proposal is raised
FALLBACK_RATE = 0.02
LOW_NOVELTY_RATE = 0.02
REPAIR_MIN_TRIES = 5
REPAIR_USELESS = 0.0
REPAIR_WEAK = 0.2
SPINE_SHARE = 2.0               # a story used more than this many times its fair share
NOVELTY_COMFORT = 0.99          # novelty so high that the threshold could be raised
NOT_MEASURED_SHARE = 0.25
UNMET_SHARE = 0.05             # share of served cases that missed a requested dimension before a proposal is raised
STORY_USE = 0.5                 # share of the approved stories that should be used over a large batch


def canonical(doc) -> bytes:
    return json.dumps(doc, sort_keys=True, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


# ---- records -------------------------------------------------------------------------------------------------

def record_of(request: dict, result) -> dict:
    """One line of the request log: the outcome and what led to it. No trainee text, no names; the trainee is the pseudonym of the request."""
    rec = {"trainee": request["trainee"], "scenario": request["scenario"], "level": request["level"], "seed": request.get("seed"),
           "outcome": "served" if result.package else "fallback", "fallback": result.fallback}
    if result.package:
        p = result.package
        rec.update(spine=p["provenance"]["spine"], fingerprint=p["fingerprint"], novelty=p["novelty"]["verdict"], nearest=p["novelty"]["nearest"],
                   quality=p["quality"].get("metrics", {}), unmet=p["quality"].get("unmet_dimensions", []), not_measured=p["quality"].get("not_measured", []), repairs=p["provenance"].get("repairs", []),
                   attempts=len(p["provenance"].get("novelty_attempts", [])))
    else:
        rec.update(defects=sorted({d["code"] for d in result.defects}), repairs=result.log if isinstance(result.log, list) else [])
    return rec


def simulate(requests: int, seed0: int) -> list[dict]:
    """Six sessions per simulated trainee. Each has a fixed skill and earns credits and penalties from it, so after three sessions the objectives adapt as they would for a real trainee."""
    import random
    from composer import blocks as B
    from generation import history as H, objectives as ob, pipeline, retrieve
    cases = B.load_cases()
    tables = ob.Tables.load()
    behaviours = sorted(b for d in tables.dimensions for b in d.weak_behaviours)
    ctx = pipeline.Context(retrieve.build_index(), cases, tables)
    out = []
    store: dict[str, list] = collections.defaultdict(list)
    for i in range(requests):
        t = i // 6
        trainee = f"trainee-{t:03d}"
        req = {"version": 1, "trainee": trainee, "mode": "practice", "scenario": "S1" if i % 2 == 0 else "S2", "level": "advanced" if (i // 2) % 2 else "beginner", "seed": seed0 + i}
        hist = store[trainee]
        r = pipeline.generate(req, H.session_records(hist), H.novelty_history(hist), ctx, recent_fingerprints=H.recent_fingerprints(hist))
        out.append(record_of(req, r))
        if r.package:
            rng = random.Random(seed0 * 1000 + i)
            skill = ((t * 37) % 10) / 10.0
            by = {}
            for b in behaviours:
                if rng.random() < 0.6:
                    good = sum(rng.random() < 0.2 + 0.7 * skill for _ in range(6))
                    by[b] = [good, 6 - good]
            hist.append(H.HistoryEntry(trainee, f"s{i}", r.package["case"]["id"], r.package["provenance"]["spine"], r.package["case"]["tier"], by, "2026-10-17T00:00:00Z",
                                       r.package["id"], "", r.package["fingerprint"], r.package["fingerprint_levels"]))
    return out


# ---- aggregation and proposals ---------------------------------------------------------------------------------

def aggregate(records: list[dict]) -> dict:
    n = len(records)
    served = [r for r in records if r["outcome"] == "served"]
    fallbacks = [r for r in records if r["outcome"] == "fallback"]
    repairs = collections.defaultdict(lambda: [0, 0])                      # strategy -> [tries, passed]
    for r in records:
        for x in r.get("repairs", []):
            if isinstance(x, dict) and "strategy" in x:
                repairs[x["strategy"]][0] += 1
                repairs[x["strategy"]][1] += x.get("result") == "passed"
    spines = collections.Counter(r["spine"] for r in served)
    defect_codes = collections.Counter(c for r in fallbacks for c in r.get("defects", []))
    repaired_codes = collections.Counter(c for r in records for x in r.get("repairs", []) if isinstance(x, dict) for c in x.get("codes", []))
    metrics: dict[str, list[float]] = collections.defaultdict(list)
    for r in served:
        for k, v in r.get("quality", {}).items():
            if v is not None:
                metrics[k].append(v)
    not_measured = collections.Counter(k for r in served for k in r.get("not_measured", []))
    unmet = collections.Counter(d for r in served for d in r.get("unmet", []))
    return {
        "requests": n, "served": len(served), "fallbacks": len(fallbacks), "fallback_rate": round(len(fallbacks) / max(n, 1), 4),
        "repaired": sum(1 for r in served if r.get("repairs")), "repair_by_strategy": {k: {"tries": v[0], "passed": v[1]} for k, v in sorted(repairs.items())},
        "defect_codes_in_fallbacks": dict(defect_codes.most_common()), "defect_codes_repaired": dict(repaired_codes.most_common()),
        "novel": sum(1 for r in served if r["novelty"] == "novel"), "low_novelty": sum(1 for r in served if r["novelty"] == "low_novelty"),
        "novelty_rate": round(sum(1 for r in served if r["novelty"] == "novel") / max(len(served), 1), 4),
        "mean_nearest": round(sum(r["nearest"] for r in served) / max(len(served), 1), 4),
        "spine_use": dict(sorted(spines.items())), "metric_mean": {k: round(sum(v) / len(v), 4) for k, v in sorted(metrics.items())},
        "metric_min": {k: round(min(v), 4) for k, v in sorted(metrics.items())}, "not_measured": dict(not_measured), "unmet_dimensions": dict(unmet.most_common()),
    }


def propose(stats: dict) -> list[dict]:
    """Rule-based proposals; each carries the numbers behind it. They are for a person to review."""
    out = []

    def add(kind, target, change, why, evidence):
        out.append({"id": f"P{len(out) + 1:02d}", "kind": kind, "target": target, "change": change, "why": why, "evidence": evidence, "status": "proposed"})

    if stats["fallback_rate"] > FALLBACK_RATE:
        add("block_or_spine", "the defects that end in a fallback", "find the spines or blocks behind the most common defect code and fix or retire them",
            f"{stats['fallbacks']} of {stats['requests']} requests ended in a fallback ({stats['fallback_rate']:.1%}, limit {FALLBACK_RATE:.0%})", {"defect_codes_in_fallbacks": stats["defect_codes_in_fallbacks"]})
    for strategy, v in stats["repair_by_strategy"].items():
        if v["tries"] >= REPAIR_MIN_TRIES:
            rate = v["passed"] / v["tries"]
            if rate <= REPAIR_USELESS:
                add("repair_table", strategy, "remove this strategy from the repair table or fix what it does", f"it passed 0 of {v['tries']} tries", {"strategy": strategy, **v})
            elif rate < REPAIR_WEAK:
                add("repair_table", strategy, "move this strategy later in the repair table", f"it passed {v['passed']} of {v['tries']} tries ({rate:.0%})", {"strategy": strategy, **v})
    served = max(stats["served"], 1)
    if stats["low_novelty"] / served > LOW_NOVELTY_RATE:
        add("threshold", "novelty.min_distance or the number of approved cases", "approve more cases for the family, or lower the distance threshold after review",
            f"{stats['low_novelty']} of {stats['served']} served cases were low novelty", {"novelty_rate": stats["novelty_rate"], "low_novelty": stats["low_novelty"]})
    elif stats["novelty_rate"] >= NOVELTY_COMFORT and stats["mean_nearest"] > 0.7:
        add("threshold", "novelty.min_distance", "the distance threshold could be raised; novelty has room", f"novelty rate {stats['novelty_rate']:.2%} with a mean nearest distance of {stats['mean_nearest']}",
            {"novelty_rate": stats["novelty_rate"], "mean_nearest": stats["mean_nearest"]})
    if stats["spine_use"]:
        fair = stats["served"] / len(stats["spine_use"])
        for spine, uses in stats["spine_use"].items():
            if uses > SPINE_SHARE * fair and stats["served"] >= 40:
                add("block_weight", spine, "lower this story's weight in the ranking or approve more stories like the rest",
                    f"{spine} was used {uses} times, {uses / fair:.1f} times its fair share", {"spine": spine, "uses": uses, "fair": round(fair, 2)})
    for metric, mean in stats["metric_mean"].items():
        if mean < 0.9 and metric not in ("coverage", "difficulty", "trap_fairness"):         # coverage needs ratings; trap fairness is advisory (DT-197)
            add("quality_threshold", metric, "look at the cases that score low on this metric; the metric or the blocks may need a change", f"mean {mean} over served cases", {"metric": metric, "mean": mean, "min": stats["metric_min"][metric]})
    avail = stats.get("stories_available")
    if avail and stats["served"] >= 40 and len(stats["spine_use"]) / avail < STORY_USE:
        add("ranking", "rank_spines and the tier limits", "widen the choice among the best stories or review the tier limits so more approved stories are used",
            f"only {len(stats['spine_use'])} of {avail} approved stories were used in {stats['served']} served cases", {"used": len(stats["spine_use"]), "available": avail})
    for dim, count in stats.get("unmet_dimensions", {}).items():
        if count / served > UNMET_SHARE:
            add("content", dim, "author or approve stories that exercise this dimension; the ranking prefers them once they exist", f"{count} of {stats['served']} served cases were asked to exercise it and did not", {"dimension": dim, "count": count})
    for metric, count in stats["not_measured"].items():
        if metric == "coverage":
            continue                                                         # a new trainee has no objectives that name a dimension, so coverage is not measured by design
        if count / served > NOT_MEASURED_SHARE:
            add("content", metric, "author discovery rules for the stories that cannot be measured on this metric", f"{count} of {stats['served']} served cases could not be measured on it", {"metric": metric, "count": count})
    return out


def make_report(stats: dict, source: str, stories_available: int | None = None) -> dict:
    from generation import novelty, quality, repair
    if stories_available:
        stats = {**stats, "stories_available": stories_available}
    body = {"source": source, "stats": stats, "proposals": propose(stats), "thresholds": {"novelty": novelty.THRESHOLDS, "quality": quality.THRESHOLDS, "repair_rounds": repair.Budget().rounds}}
    return {"id": "EVAL-" + hashlib.sha256(canonical(body)).hexdigest()[:12], **body}


def to_markdown(report: dict) -> str:
    s = report["stats"]
    lines = [f"# Generation evaluation {report['id']}", "", f"Source: {report['source']}. This is a proposal for a reviewed release; nothing here changes a session.", "",
             "| Measure | Value |", "|---|---|", f"| Requests | {s['requests']} |", f"| Served | {s['served']} |", f"| Fallbacks | {s['fallbacks']} ({s['fallback_rate']:.1%}) |",
             f"| Served after a repair | {s['repaired']} |", f"| Novelty rate | {s['novelty_rate']:.2%} |", f"| Low-novelty cases served | {s['low_novelty']} |", f"| Mean distance to the nearest earlier case | {s['mean_nearest']} |", f"| Stories used | {len(s['spine_use'])} of {s.get('stories_available', '?')} approved |", ""]
    if s["repair_by_strategy"]:
        lines += ["## Repairs", "", "| Strategy | Tries | Passed |", "|---|---|---|"] + [f"| {k} | {v['tries']} | {v['passed']} |" for k, v in s["repair_by_strategy"].items()] + [""]
    if s["defect_codes_repaired"] or s["defect_codes_in_fallbacks"]:
        lines += ["## Defects", "", f"Repaired: {s['defect_codes_repaired']}. Ending in a fallback: {s['defect_codes_in_fallbacks']}.", ""]
    lines += ["## Quality (mean over served cases)", "", "| Metric | Mean | Lowest |", "|---|---|---|"] + [f"| {k} | {v} | {s['metric_min'][k]} |" for k, v in s["metric_mean"].items()] + [""]
    lines += ["## Stories used", "", ", ".join(f"{k} {v}" for k, v in s["spine_use"].items()), "", "## Proposals", ""]
    lines += [f"- **{p['id']}** ({p['kind']}, {p['target']}): {p['change']}. Because {p['why']}." for p in report["proposals"]] or ["None: every measure is inside its limit."]
    return "\n".join(lines) + "\n"


# ---- releases -------------------------------------------------------------------------------------------------

def packs_hash() -> str:
    return hashlib.sha256(PACKS_MANIFEST.read_bytes().replace(b"\r\n", b"\n")).hexdigest()[:16]


def check_releases() -> list[str]:
    """The packs now in the repository are those of the latest release, and every release cites reports that exist."""
    problems = []
    if not RELEASES.exists():
        return [f"{RELEASES.relative_to(ROOT).as_posix()} is missing"]
    doc = json.loads(RELEASES.read_text(encoding="utf-8"))
    rel = doc["releases"]
    if not rel:
        return ["no release is recorded"]
    for r in rel:
        if not r.get("cites"):
            problems.append(f"release {r['release']} cites no evaluation report")
        for c in r.get("cites", []):
            if not (REPORTS / f"{c}.json").exists():
                problems.append(f"release {r['release']} cites {c}, which does not exist in docs/research/generation_evaluations")
    if rel[-1]["packs_hash"] != packs_hash():
        problems.append(f"the parameter packs changed since release {rel[-1]['release']} (hash {packs_hash()}, release says {rel[-1]['packs_hash']}): add a release that cites the evaluation report behind the change")
    return problems


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("command", choices=("run", "check"))
    ap.add_argument("--requests", type=int, default=120)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--log")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    if a.command == "check":
        problems = check_releases()
        for p in problems:
            print("FAIL", p)
        print("generation releases: " + ("OK" if not problems else f"{len(problems)} problem(s)"))
        return 1 if problems else 0
    if a.log:
        records = [json.loads(x) for x in Path(a.log).read_text(encoding="utf-8").splitlines() if x.strip()]
        source = f"request log {Path(a.log).name}, {len(records)} requests"
    else:
        records = simulate(a.requests, a.seed)
        source = f"simulated batch of {a.requests} requests from seed {a.seed}"
    from composer import blocks as B
    available = sum(1 for c in B.load_cases() if c["status"].startswith("APPROVED") and c.get("created_by") != "composer")
    report = make_report(aggregate(records), source, available)
    print(to_markdown(report))
    if a.write:
        REPORTS.mkdir(parents=True, exist_ok=True)
        (REPORTS / f"{report['id']}.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        (REPORTS / f"{report['id']}.md").write_text(to_markdown(report), encoding="utf-8", newline="\n")
        print(f"wrote {report['id']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
