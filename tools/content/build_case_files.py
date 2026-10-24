"""Builds content/case_files/<id>.json for the 53 cases (17 P0, 16 P1, 20 P2) from tools/content/cases/ (s1, s1b, s2, stk, p1v, p1stk, p2), recomputes what can be computed, validates, and applies approvals.

    uv run --project tools tools/content/build_case_files.py            # write
    uv run --project tools tools/content/build_case_files.py --check    # fail if a file is missing, out of date or invalid (CI)
    uv run --project tools tools/content/build_case_files.py --grant    # record the owner-attested approval for the cases as they are now
    uv run --project tools tools/content/build_case_files.py --grant-cases STK-S1-A,STK-S1-B --by "Owner (Abdul Moiz)" --date 2026-10-17 --why "..."
                                                                         # approve only these cases as they are now, leave every other approval as it is,
                                                                         # and take them off content/case_signoff_pending.json

Filled in here, never typed by hand: NEWS2 of every observation (content/news2_scoring.csv), the wristband text, the source rows (story, measurements, authoring row) and the approval status.
An approval is tied to a hash of the case content, so editing a case drops its approval until it is granted again.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE / "cases"))
import case_file_checks as chk  # noqa: E402
import news2  # noqa: E402
import fasting_intake  # noqa: E402
import lib  # noqa: E402
import s1, s1b, s2, stk, p1v, p1stk, p2, p2comb  # noqa: E402,F401
from risk import RISK  # noqa: E402
RISK.update(p2comb.RISK)
from responsible_adults import RESPONSIBLE_ADULT  # noqa: E402

# The cases whose patient is pregnant (history and the pregnancy test say so): NEWS2 is not the tool for them once the trainee knows.
# Facts that change which critical limits apply (see content/investigations/critical.json): SC-13 is on warfarin; no case has known kidney failure.
FACTS = {"SC-13": ["on_warfarin"]}
PREGNANT = {"VAR-S1-H", "VAR-S2-B", "SC-2", "SC-3", "STK-S2-D", "VAR-S2-H"}

OUT = ROOT / "content" / "case_files"
APPROVALS = ROOT / "content" / "case_approvals.json"
PENDING = ROOT / "content" / "case_signoff_pending.json"
BY = "Owner (Ruhan Kamran), who states that the clinician approves everything; the clinician is not named"
NOTE = "Owner-attested clinician approval, 10 October 2026; verified against the guideline claims in docs/research/clinical_verification.md; reconfirm with the named clinician before assessment use"


def all_cases() -> dict[str, dict]:
    cases: dict[str, dict] = {}
    for mod in (s1, s1b, s2, stk, p1v, p1stk, p2, p2comb):
        for k, v in mod.CASES.items():
            assert k not in cases, k
            cases[k] = v
    return cases


REGISTRY = ROOT / "content" / "combination_cases.json"


def registry_text(cases: dict) -> str:
    """The authoritative list of the combination cases (DT-220): tests and tools derive the number of cases from the book's 53 plus this list, not from a literal."""
    fixtures = {c["id"]: c for c in json.loads((ROOT / "tests" / "composition" / "fixtures_authored.json").read_text(encoding="utf-8"))["compositions"]}
    rows = [{"id": cid, "fixture": p2comb.FIXTURE[cid], "scenario": cases[cid]["scenario"], "tier": cases[cid]["tier"], "expected_end_state": cases[cid]["expected_end_state"],
             "fixture_problems": [(p["source_case"], p["source_problem"], p["type"]) for p in fixtures[p2comb.FIXTURE[cid]]["problems"]]} for cid in sorted(p2comb.CASES)]
    return json.dumps({"schema": 1, "note": "Synthetic multi-problem practice cases authored from the fixtures of tests/composition/compositions.json (DT-220); not from the walkthrough, not clinician-reviewed.",
                       "book_cases": 53, "cases": rows}, indent=1, ensure_ascii=False) + chr(10)


def content_hash(c: dict) -> str:
    body = {k: v for k, v in c.items() if k not in ("status", "approval")}
    return hashlib.sha256(json.dumps(body, sort_keys=True, ensure_ascii=False).encode("utf-8")).hexdigest()


def fill(c: dict, ctx: dict) -> dict:
    ident = c["identity"]
    if c["wristband"] == "auto":
        d = ident["dob"].split("-")
        c["wristband"] = {"name_text": ident["full_name"].upper(), "dob_text": f"{d[2]}/{d[1]}/{d[0]}", "mrn_text": "MR " + ident["mrn"]}
    for o in c["observations"]:
        o["news2"] = news2.score(o["heart_rate"], o["systolic_bp"], o["respiratory_rate"], o["spo2"], o["temperature_c"], o["acvpu"], o["oxygen"])
    pres = c["presentation"]
    c["presentation"] = {"setting": pres["setting"], "time_now": pres["time_now"], **({"surgery_time": pres["surgery_time"]} if "surgery_time" in pres else {}), "summary": pres["summary"]}
    for h in c["history"]:
        if h["topic"] in ("last_food", "last_drink"):
            h["intake"] = fasting_intake.intake(h["topic"], h["answer"], c["presentation"]["time_now"])
    cid = c["id"]
    if cid in PREGNANT:
        c["pregnant"] = True
    if cid in FACTS:
        c["facts"] = list(FACTS[cid])
    if cid in RESPONSIBLE_ADULT:
        who, home, night, say = RESPONSIBLE_ADULT[cid]
        c["responsible_adult"] = {"who": who, "takes_home": home, "stays_overnight": night, "text": say, "wording": "quote"}
    if cid in RISK:
        c["risk"] = json.loads(json.dumps(RISK[cid]))
    meas = sorted({r["source_row"] for r in c["results"] if r.get("source_row")} | {m for m, r in ctx["measurements"].items() if r["case_id"] == cid})
    c["sources"] = {"story": ctx["case_seeds"][cid], "authored": ctx["authoring"][cid], **({"measurements": meas} if meas else {})}
    return c


def build() -> dict[str, dict]:
    ctx = chk.load_context()
    cases = {k: fill(json.loads(json.dumps(v)), ctx) for k, v in all_cases().items()}
    approvals = json.loads(APPROVALS.read_text(encoding="utf-8"))["approvals"] if APPROVALS.exists() else {}
    for k, c in cases.items():
        a = approvals.get(k)
        if a and a["sha256"] == content_hash(c):
            c["status"], c["approval"] = a["level"], {"by": a["by"], "date": a["date"], "note": a["note"]}
    return dict(sorted(cases.items()))


def _arg(argv: list[str], name: str) -> str:
    i = argv.index(name)
    if i + 1 >= len(argv):
        raise SystemExit(f"{name} needs a value")
    return argv[i + 1]


def grant_cases(cases: dict[str, dict], ids: list[str], by: str, date: str, why: str) -> None:
    """Approval of the named cases only, as they are now; the other approvals are kept, and the cases leave the sign-off list."""
    unknown = [i for i in ids if i not in cases]
    if unknown:
        raise SystemExit(f"no such case: {', '.join(unknown)}")
    data = json.loads(APPROVALS.read_text(encoding="utf-8"))
    for i in ids:
        data["approvals"][i] = {"sha256": content_hash(cases[i]), "level": "APPROVED_FOR_TRAINING", "by": by, "date": date, "note": why}
    data["approvals"] = dict(sorted(data["approvals"].items()))
    APPROVALS.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    pending = json.loads(PENDING.read_text(encoding="utf-8"))
    for i in ids:
        pending["pending"].pop(i, None)
    PENDING.write_text(json.dumps(pending, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"recorded approval for {len(ids)} cases: {', '.join(ids)}")


def text(c: dict) -> str:
    return json.dumps(c, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    cases = build()
    ctx = chk.load_context()
    if "--grant" in argv:
        data = {"schema": 1, "approvals": {k: {"sha256": content_hash(c), "level": "APPROVED_FOR_TRAINING", "by": BY, "date": "2026-10-10", "note": NOTE} for k, c in cases.items()}}
        APPROVALS.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"recorded approval for {len(cases)} cases")
        cases = build()
    if "--grant-cases" in argv:
        grant_cases(cases, [i.strip() for i in _arg(argv, "--grant-cases").split(",") if i.strip()], _arg(argv, "--by"), _arg(argv, "--date"), _arg(argv, "--why"))
        cases = build()
    probs = []
    for k, c in cases.items():
        probs += chk.problems(c, ctx, list(cases.values()))
    registry = registry_text(cases)
    if "--check" in argv:
        if not REGISTRY.exists() or REGISTRY.read_text(encoding="utf-8").replace(chr(13) + chr(10), chr(10)) != registry:
            probs.append("content/combination_cases.json is missing or out of date: run `uv run --project tools tools/content/build_case_files.py`")
        for k, c in cases.items():
            p = OUT / f"{k}.json"
            if not p.exists() or p.read_text(encoding="utf-8").replace("\r\n", "\n") != text(c):
                probs.append(f"content/case_files/{k}.json is missing or out of date: run `uv run --project tools tools/content/build_case_files.py`")
        extra = {p.stem for p in OUT.glob("*.json")} - set(cases) if OUT.exists() else set()
        probs += [f"content/case_files/{e}.json has no source in tools/content/cases" for e in sorted(extra)]
    else:
        OUT.mkdir(parents=True, exist_ok=True)
        REGISTRY.write_text(registry, encoding="utf-8", newline=chr(10))
        for k, c in cases.items():
            (OUT / f"{k}.json").write_text(text(c), encoding="utf-8", newline="\n")
    n_ap = sum(1 for c in cases.values() if c["status"] != "DRAFT")
    print(f"{len(cases)} case files, {n_ap} approved: " + ("all checks pass" if not probs else f"{len(probs)} problem(s)"))
    for p in probs:
        print("  - " + p)
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
