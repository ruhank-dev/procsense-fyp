"""Builds the ProcSense act benchmark v1 (SYNTHETIC; ADR-0020): `uv run --project tools tools/bench/build_act_dataset.py [--check]`.

Output: tests/action_bench_v1/dev.jsonl, test.jsonl (the TEST split is frozen: scored once per approach, by tools/bench/act_benchmark.py --final), manifest.json (sha256 of both files and of the seeds) and README.md.

How the labels are made: nobody labels a sentence. The seeds (tools/bench/act_dataset_seeds.py) hold, per act, FAMILIES (one meaning in three wordings and three verb forms). Each family is put under an assertion status by a WRAPPER template (denied, intended, hypothetical, reported, said by someone else), or broken off, or corrupted like speech recognition output, or left as it
is (actual). The label is a function of the construction: `act` is the family's act, `status` the wrapper, and `expect_credit` is true only for status actual, an ordinary act and the trainee as the actor; a critical-act sentence or a NONE sentence is never to be credited. The splits are by FAMILY and by WRAPPER TEMPLATE: a test sentence shares no
family, no wording and no wrapper with a development sentence (checked below), and the development wrappers are not the test wrappers, so a status detector trained on development cannot memorise the test construction. The corruptions are seeded, so a rebuild gives identical files.

What this is not: it is not human annotation and it is not independent; the author of seeds and wrappers is the builder's language model, the sentences are far more regular than a trainee's speech, and "intended", "reported" and "other actor" wrappers are cleaner than real ones. A programmatic validation runs at build time (below) and the sample audit
is in the README; neither replaces EX-01/EX-18.
"""
from __future__ import annotations

import hashlib
import json
import random
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "server"))
import act_dataset_seeds as S  # noqa: E402
import act_dataset_seeds2 as S2  # noqa: E402

OUT = ROOT / "tests" / "action_bench_v1"
# wrappers: (status, split, template); {inf} {pp} {past} come from the family's verb forms
WRAPPERS = [
    ("denied", "dev", "I haven't {pp} yet."), ("denied", "dev", "I didn't {inf}."), ("denied", "test", "I'm not going to {inf} now."), ("denied", "test", "I have not {pp}, sorry."),
    ("intended", "dev", "I will {inf} in a moment."), ("intended", "dev", "Later I'll {inf}."), ("intended", "test", "I'm planning to {inf} after this."), ("intended", "test", "Shortly I shall {inf}."),
    ("hypothetical", "dev", "If I were to {inf}, I would start slowly."), ("hypothetical", "dev", "Suppose I {past}, what would you think?"), ("hypothetical", "test", "Imagine I {past} just now."), ("hypothetical", "test", "What if I were to {inf}?"),
    ("reported", "dev", "The nurse said she {past}."), ("reported", "dev", "Someone told me he {past}."), ("reported", "test", "My colleague says she {past}."), ("reported", "test", "I heard that he {past} earlier."),
    ("other_actor", "dev", "The registrar will {inf} for you."), ("other_actor", "dev", "The nurse is going to {inf}."), ("other_actor", "test", "The consultant is about to {inf}."), ("other_actor", "test", "Dr Khan will {inf} later."),
]
# wrappers of the second held-out set (test2): different wording from every dev and test wrapper
WRAPPERS2 = [
    ("denied", "Sorry, I never got to {inf}."), ("denied", "No, I did not {inf}."),
    ("intended", "Give me a minute and I'll {inf}."), ("intended", "I intend to {inf} once we start."),
    ("hypothetical", "Say I were to {inf}, how would that sound?"), ("hypothetical", "Picture me trying to {inf}."),
    ("reported", "Apparently the porter {past}."), ("reported", "According to the chart, the nurse {past}."),
    ("other_actor", "Let the senior nurse {inf}."), ("other_actor", "The anaesthetist can {inf} instead."),
]
CATEGORY = {"denied": "negation", "intended": "intention", "hypothetical": "hypothetical", "reported": "reported", "other_actor": "actor_change"}
NO_INTENT = {"EXPLAIN_ACTION", "CLOSED_LOOP"}                 # "I will put the cuff on" or "I will call you when..." IS the act when said by the speaker, so an "intended" wrapper would not be a different status
PHONETIC = [("their", "there"), ("two", "to"), ("write", "right"), ("here", "hear"), ("for", "four"), ("no", "know"), ("x ray", "ex ray"), ("ecg", "e c g"), ("crp", "c r p"), ("scan", "can"), ("arm", "on"), ("tube", "tub"), ("clip", "click"), ("pain", "pane"), ("blood", "blud")]


def corrupt(text: str, rng: random.Random) -> str:
    """Seeded speech-recognition-like damage: lower case without punctuation, then one or two of: a homophone, a dropped article, a dropped letter, two words run together."""
    t = re.sub(r"[^\w' ]+", "", text.lower())
    for _ in range(rng.choice([1, 2])):
        k = rng.choice(["phone", "article", "letter", "merge"])
        w = t.split()
        if k == "phone":
            for a, b in rng.sample(PHONETIC, len(PHONETIC)):
                if re.search(rf"\b{re.escape(a)}\b", t):
                    t = re.sub(rf"\b{re.escape(a)}\b", b, t, count=1)
                    break
        elif k == "article" and any(x in ("a", "an", "the", "your") for x in w):
            i = rng.choice([j for j, x in enumerate(w) if x in ("a", "an", "the", "your")])
            t = " ".join(w[:i] + w[i + 1:])
        elif k == "letter":
            longs = [j for j, x in enumerate(w) if len(x) > 5]
            if longs:
                j = rng.choice(longs)
                p = rng.randrange(1, len(w[j]) - 1)
                w[j] = w[j][:p] + w[j][p + 1:]
                t = " ".join(w)
        elif k == "merge" and len(w) > 4:
            j = rng.randrange(1, len(w) - 2)
            w[j:j + 2] = ["".join(w[j:j + 2])]
            t = " ".join(w)
    return t


def build() -> dict[str, list[dict]]:
    rng = random.Random(20261019)
    rows: dict[str, list[dict]] = {"dev": [], "test": [], "test2": []}

    def add(split, text, act, status, family, category, expect):
        rows[split].append({"id": f"AB-{'u' if split == 'test2' else split[0]}{len(rows[split]) + 1:04d}", "text": text, "act": act, "status": status, "family": family, "category": category, "expect_credit": expect, "critical": act in S.CRITICAL, "synthetic": True, "independent": False})

    for act, fams in S.ORDINARY.items():
        for n, (split, surfaces, (inf, pp, past)) in enumerate(fams):
            fam = f"{act}.{n}"
            for s in surfaces:
                add(split, s, act, "actual", fam, "paraphrase", True)
            for status, wsplit, tpl in WRAPPERS:
                if wsplit != split or (status == "intended" and act in NO_INTENT):
                    continue
                add(split, tpl.format(inf=inf, pp=pp, past=past), act, status, fam, {"denied": "negation", "intended": "intention", "hypothetical": "hypothetical", "reported": "reported", "other_actor": "actor_change"}[status], False)
            cut = surfaces[0]
            words = cut.rstrip(".?!").split()
            add(split, " ".join(words[: max(2, int(len(words) * 0.6))]) + " and then I should -", act, "incomplete", fam, "incomplete", False)
            for s in rng.sample(surfaces, 2):
                add(split, corrupt(s, rng), act, "actual", fam, "asr_error", True)
    for act, fams in S.CRITICAL.items():
        for n, (split, surfaces) in enumerate(fams):
            fam = f"{act}.{n}"
            for s in surfaces:
                add(split, s, act, "actual", fam, "critical", False)
            add(split, corrupt(surfaces[0], rng), act, "actual", fam, "critical_asr", False)
    for n, (split, sents) in enumerate(S.NONE_SENTENCES):
        for s in sents:
            add(split, s, "NONE", "none", f"NONE.{n}", "none", False)
    for act, fams in S2.ORDINARY2.items():
        for n, (split, surfaces, (inf, pp, past)) in enumerate(fams):
            fam = f"{act}.t2.{n}"
            for sf in surfaces:
                add("test2", sf, act, "actual", fam, "paraphrase", True)
            for status, tpl in WRAPPERS2:
                if status == "intended" and act in NO_INTENT:
                    continue
                add("test2", tpl.format(inf=inf, pp=pp, past=past), act, status, fam, CATEGORY[status], False)
            words = surfaces[0].rstrip(".?!").split()
            add("test2", " ".join(words[: max(2, int(len(words) * 0.6))]) + " and then I should -", act, "incomplete", fam, "incomplete", False)
            for sf in rng.sample(surfaces, 2):
                add("test2", corrupt(sf, rng), act, "actual", fam, "asr_error", True)
    for act, fams in S2.CRITICAL2.items():
        for n, (split, surfaces) in enumerate(fams):
            fam = f"{act}.t2.{n}"
            for sf in surfaces:
                add("test2", sf, act, "actual", fam, "critical", False)
            add("test2", corrupt(surfaces[0], rng), act, "actual", fam, "critical_asr", False)
    for n, (split, sents) in enumerate(S2.NONE2):
        for sf in sents:
            add("test2", sf, "NONE", "none", f"NONE.t2.{n}", "none", False)
    return rows


def validate(rows: dict[str, list[dict]]) -> list[str]:
    bad = []
    taxonomy = {a["id"]: a["critical"] for a in json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))["acts"]}
    fams = {sp: {r["family"] for r in rows[sp]} for sp in rows}
    for a_, b_ in (("dev", "test"), ("dev", "test2"), ("test", "test2")):
        if fams[a_] & fams[b_]:
            bad.append(f"families in both {a_} and {b_}: {sorted(fams[a_] & fams[b_])[:3]}")
    texts: dict[str, str] = {}
    for sp in rows:
        for r in rows[sp]:
            if r["act"] not in taxonomy:
                bad.append(f"{r['id']}: act {r['act']} is not in the taxonomy")
            if r["expect_credit"] and (taxonomy[r["act"]] or r["status"] != "actual"):
                bad.append(f"{r['id']}: credit expected for a critical act or a non-actual status")
            if r["text"] in texts and texts[r["text"]] != sp:
                bad.append(f"{r['id']}: the same sentence is in both splits")
            texts[r["text"]] = sp
    stems = {sp: {re.sub(r"[^a-z ]+", "", r["text"].lower()) for r in rows[sp]} for sp in rows}
    for a_, b_ in (("dev", "test"), ("dev", "test2"), ("test", "test2")):
        if stems[a_] & stems[b_]:
            bad.append(f"a normalised sentence is in both {a_} and {b_}")
    return bad


def main(argv: list[str]) -> int:
    rows = build()
    bad = validate(rows)
    if bad:
        print("\n".join(bad))
        return 1
    files = {}
    for sp, rs in rows.items():
        text = "\n".join(json.dumps(r, ensure_ascii=False) for r in rs) + "\n"
        files[f"{sp}.jsonl"] = text
    seeds = (ROOT / "tools" / "bench" / "act_dataset_seeds.py").read_bytes()
    manifest = {"synthetic": True, "independent": False, "rows": {sp: len(rs) for sp, rs in rows.items()}, "files": {k: hashlib.sha256(v.encode("utf-8")).hexdigest() for k, v in files.items()},
                "seeds_sha256": hashlib.sha256(seeds.replace(b"\r\n", b"\n")).hexdigest(), "seeds2_sha256": hashlib.sha256((ROOT / "tools" / "bench" / "act_dataset_seeds2.py").read_bytes().replace(b"\r\n", b"\n")).hexdigest(),
                "note": "Frozen on 19 October 2026 before any approach was run on the test split; the test split is scored once per approach (tools/bench/act_benchmark.py --final)."}
    if "--check" in argv:
        ok = all((OUT / k).exists() and (OUT / k).read_text(encoding="utf-8") == v for k, v in files.items()) and json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))["files"] == manifest["files"]
        print("benchmark files " + ("current" if ok else "OUT OF DATE: rebuild only before the test split is first scored"))
        return 0 if ok else 1
    OUT.mkdir(exist_ok=True)
    for k, v in files.items():
        (OUT / k).write_text(v, encoding="utf-8", newline="\n")
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8", newline="\n")
    import collections
    for sp, rs in rows.items():
        print(sp, len(rs), dict(collections.Counter(r["category"] for r in rs)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
