"""Feature-by-feature implementation matrix of the 68 AI feature plans (`docs/architecture/feature_implementation_matrix.md`): `uv run --project tools tools/plans/feature_matrix.py [--sync-json] [--check]`.

For every feature of `docs/architecture/procsense_full_pipeline.json` the matrix states: the plan, the required behaviour (its acceptance), where it runs (`docs/architecture/feature_overlay.json`: runtime path, test files, feature gates),
the roadmap steps that realise it with the status the reconciliation gives each (`docs/plans/reconciliation.json`, evidence paths verified to exist), the state of its feature gate (`content/ai/features.json`), the external
items that still stand between it and final acceptance (`docs/testing/external_testing.json`) and a RESULT derived by fixed rules, never typed:

  Complete                        every realising step is done (or approved by the owner under the earlier rule), a runtime path and at least one test file exist
  Engineering complete            every part that needs no outside actor is built and tested; external acceptance (named) is pending
  Built, gated off                built and measured, but a measured acceptance criterion is not met, so the feature stays off
  Partly built                    a realising step is only partly done
  Owned by others                 a realising step is Sannan's and not yet approved (the engine half may be done)
  Research: trigger not met       analysed on purpose; nothing is built until the trigger holds

`--sync-json` writes the same result into the canonical pipeline JSON (feature `status`, capability `status`, and the evidence paths of the reconciliation) so the generated plans and registries stop saying "planned" for what is built;
`--check` fails when the committed matrix or the JSON differs from what the rules give, or a path in the overlay does not exist, or a feature has no test file. Nothing is ticked here.
"""
from __future__ import annotations

import json
import os
import re
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, str(Path(__file__).resolve().parent))
import external_testing as XT  # noqa: E402
import reconcile as RC  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
PIPELINE = ROOT / "docs" / "architecture" / "procsense_full_pipeline.json"
OVERLAY = ROOT / "docs" / "architecture" / "feature_overlay.json"
FEATURES = ROOT / "content" / "ai" / "features.json"
OUT = ROOT / "docs" / "architecture" / "feature_implementation_matrix.md"
RESULT_TO_STATUS = {"Complete": "Implemented and tested", "Engineering complete": "Implemented but incompletely tested", "Built, gated off": "Implemented but incompletely tested", "Partly built": "Partially implemented",
                    "Owned by others": "Planned", "Research: trigger not met": "Deferred"}
TEST = re.compile(r"(^|/)(test_[^/]*|[^/]*Tests?\.[a-z]+)$|^tests/|/tests/")


def result_of(states: list[str], ticked_others: list[bool]) -> str:
    s = set(states)
    if "RESEARCH_NEGATIVE" in s:
        return "Research: trigger not met"
    if "DONE_GATE_OFF" in s:
        return "Built, gated off"
    if "NOT_RUHAN" in s and not all(ticked_others) and s == {"NOT_RUHAN"}:
        return "Owned by others"
    if s & {"PARTLY", "BLOCKED"} or ("NOT_RUHAN" in s and not all(ticked_others)):
        return "Partly built"
    if "ENG_DONE" in s:
        return "Engineering complete"
    return "Complete"


def load() -> dict:
    doc = json.loads(PIPELINE.read_text(encoding="utf-8"))
    found, data = RC.steps(), json.loads(RC.DATA.read_text(encoding="utf-8"))
    entries = RC.entries_of(found, data)
    ov_doc = json.loads(OVERLAY.read_text(encoding="utf-8"))
    overlay, notbuilt = ov_doc["features"], ov_doc.get("not_built", {})
    gates = json.loads(FEATURES.read_text(encoding="utf-8"))["features"]
    ext = XT.all_items(json.loads(XT.DATA.read_text(encoding="utf-8")))
    return {"doc": doc, "found": found, "entries": entries, "overlay": overlay, "notbuilt": notbuilt, "gates": gates, "ext": ext}


def derive(f: dict, ctx: dict) -> dict:
    tasks = sorted({t for t in f.get("tasks", [])} | {t for c in f["plan"]["capabilities"] for t in c["tasks"]})
    deferred = [t for t in tasks if ctx["entries"].get(t, {}).get("now") == "RESEARCH_NEGATIVE"]          # a capability analysed on purpose and not built (its trigger is not met): reported beside the result, not as a defect of the rest
    core = [t for t in tasks if t not in deferred] or tasks
    st = [ctx["entries"][t]["now"] for t in core if t in ctx["entries"]]
    unticked_others = [ctx["found"][t]["approved"] for t in core if t in ctx["entries"] and ctx["entries"][t]["now"] == "NOT_RUHAN"]
    ev = list(dict.fromkeys(p for t in tasks for p in ctx["entries"].get(t, {}).get("evidence", []) if (ROOT / p).exists()))
    ov = ctx["overlay"].get(f["id"], {})
    problems = []
    if not ov:
        problems.append("no overlay entry")
    problems += [f"overlay path missing: {p}" for p in re.findall(r"`([^`]+)`", ov.get("runtime", "")) if "/" in p and not (ROOT / p.split(" ")[0]).exists()]
    problems += [f"test file missing: {t}" for t in ov.get("tests", []) if not (ROOT / t).exists()]
    if not ov.get("tests"):
        problems.append("no test file")
    problems += [f"task {t} has no reconciliation entry" for t in tasks if t not in ctx["entries"]]
    result = result_of(st, unticked_others) if st else "Partly built"
    unbuilt = {k: v for k, v in ctx["notbuilt"].items() if k.split(".")[0] == f["id"]}
    if unbuilt and result in ("Complete", "Engineering complete"):
        result = "Partly built"                                    # a capability of the plan that no step built, however done its other steps are
    ext = [i["id"] for i in ctx["ext"] if set(i["steps"]) & set(tasks)]
    return {"id": f["id"], "tasks": tasks, "states": dict(Counter(st)), "result": result, "evidence": ev, "overlay": ov, "ext": ext, "problems": problems, "unbuilt": unbuilt, "deferred": deferred if core != tasks else [],
            "gates": [(g, ctx["gates"].get(g, {})) for g in ov.get("gates", [])]}


def render(ctx: dict) -> str:
    feats = sorted((n for n in ctx["doc"]["nodes"] if n["type"] == "feature"), key=lambda n: (n["id"][0], int(re.sub(r"\D", "", n["id"]))))
    rows = [derive(f, ctx) for f in feats]
    count = Counter(r["result"] for r in rows)
    L = ["# Feature implementation matrix (68 feature plans)", "",
         "_Generated by `tools/plans/feature_matrix.py` from the pipeline JSON, the reconciliation, `feature_overlay.json`, the feature gate and the external testing items; do not edit by hand. The result of each feature is derived by fixed rules from the status of the steps that realise it, each reconciled against its own acceptance with evidence paths that exist._", "",
         "The plan set has 68 features: A1 to E65 except one number (a language feature the owner retired on 18 October 2026, Change Report decision 117, so there are 68 plans where 69 numbers were issued), plus A66, D67, D68 and E69. Every feature is realised by steps of P0, P1 or P2, so no additional phase is needed: the feature plans stay in `docs/plans/features/` and their steps in the existing phases.", "",
         "## Summary", "", "| Result | Features |", "|---|---|"] + [f"| {k} | {count.get(k, 0)} |" for k in RESULT_TO_STATUS] + ["",
         "**Complete** means every realising step is done with evidence, and a runtime path and a test file exist. It does not mean validated by people: synthetic and AI-judged measures are labelled as such in the step evidence, and the gates in `content/ai/features.json` decide what is on. "
         "**Engineering complete** means the only thing missing is the external acceptance named in the last column (see [../testing/SANNAN_EXTERNAL_TESTING.md](../testing/SANNAN_EXTERNAL_TESTING.md)).", "",
         "## Matrix", "", "| Feature | Required behaviour (acceptance) | Where it runs | Tests | Steps (status) | Gate | Result | External |", "|---|---|---|---|---|---|---|---|"]
    for f, r in zip(feats, rows):
        ov = r["overlay"]
        gate = ", ".join(f"{g}: {'ON' if v.get('enabled') else 'off'}{' (info)' if v.get('informational') else ''}" for g, v in r["gates"]) or "-"
        st = ", ".join(f"{k} {v}" for k, v in sorted(r["states"].items()))
        tests = "<br>".join(f"`{t}`" for t in ov.get("tests", [])[:4])
        L.append(f"| [{f['id']}](../plans/features/{f['id']}.md) {f['label'][:60]} | {f['acceptance'].replace('|', '/')[:220]} | {ov.get('runtime', '-').replace('|', '/')} | {tests} | {st} | {gate} | **{r['result']}**{(' (not built: ' + ', '.join(r['unbuilt']) + ')') if r['unbuilt'] else ''}{(' (deferred by design: ' + ', '.join(r['deferred']) + ')') if r['deferred'] else ''} | {', '.join(f'[{e}](../testing/SANNAN_EXTERNAL_TESTING.md#{e.lower()})' for e in r['ext']) or '-'} |")
    L += ["", "## Realising steps per feature", "", "| Feature | Steps | Evidence paths from the reconciliation |", "|---|---|---|"]
    for f, r in zip(feats, rows):
        L.append(f"| {f['id']} | {', '.join(r['tasks'])} | {'<br>'.join(f'`{p}`' for p in r['evidence'][:6]) or '-'} |")
    bad = [(r["id"], p) for r in rows for p in r["problems"]]
    L += ["", "## Problems found by the check", "", "None." if not bad else "\n".join(f"* {i}: {p}" for i, p in bad), ""]
    return "\n".join(L) + "\n"


def sync_json(ctx: dict) -> dict:
    """The pipeline JSON with each feature's status and capability statuses set from the derived result (the canonical status field; everything generated from it follows)."""
    doc = json.loads(PIPELINE.read_text(encoding="utf-8"))
    for n in doc["nodes"]:
        if n["type"] != "feature":
            continue
        r = derive(n, ctx)
        n["status"] = RESULT_TO_STATUS[r["result"]]
        order = ["Planned", "Partially implemented", "Implemented but incompletely tested", "Implemented and tested"]
        for c in n["plan"]["capabilities"]:
            st = [ctx["entries"][t]["now"] for t in c["tasks"] if t in ctx["entries"]]
            unticked = [ctx["found"][t]["approved"] for t in c["tasks"] if t in ctx["entries"] and ctx["entries"][t]["now"] == "NOT_RUHAN"]
            c["status"] = "Planned" if c["id"] in ctx["notbuilt"] else RESULT_TO_STATUS[result_of(st, unticked) if st else "Partly built"]
        ai = [c["status"] for c in n["plan"]["capabilities"] if c["kind"].startswith("AI") and c["status"] in order]
        if n.get("ai_status") and ai:
            n["ai_status"] = min(ai, key=order.index)
        have = {e.split(" (")[0] for e in n.get("evidence", [])}
        n["evidence"] = list(n.get("evidence", [])) + [p for p in r["evidence"] if p not in have and not p.startswith("docs/plans/")][:8]
        n["blockers"] = [f"external acceptance: {', '.join(r['ext'])} (docs/testing/SANNAN_EXTERNAL_TESTING.md)"] if r["ext"] and r["result"] != "Complete" else []
    return doc


def main(argv: list[str]) -> int:
    ctx = load()
    if "--sync-json" in argv:
        doc = sync_json(ctx)
        PIPELINE.write_text(json.dumps(doc, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("synced statuses into", PIPELINE.relative_to(ROOT).as_posix())
        ctx = load()
    text = render(ctx)
    rows = [derive(n, ctx) for n in ctx["doc"]["nodes"] if n["type"] == "feature"]
    bad = [(r["id"], p) for r in rows for p in r["problems"]]
    if bad:
        print("\n".join(f"FAIL {i}: {p}" for i, p in bad))
        return 1
    if "--check" in argv:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        again = sync_json(ctx) == ctx["doc"]
        print("feature matrix " + ("current" if ok else "OUT OF DATE: run tools/plans/feature_matrix.py") + "; pipeline JSON statuses " + ("current" if again else "OUT OF DATE: run tools/plans/feature_matrix.py --sync-json"))
        return 0 if ok and again else 1
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT).as_posix(), dict(Counter(r["result"] for r in rows)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
