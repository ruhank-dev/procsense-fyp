"""Scenario cards and the freezing step for the independent communication-act benchmark (docs/research/classifier_benchmark/README.md).

    uv run --project tools tools/bench/dataset.py cards [--check]     # write (or check) the authoring sheets cards_dev.csv and cards_test.csv
    uv run --project tools tools/bench/dataset.py label-sheet dev     # the blind sheet for the second labeller: card id and text only, no act
    uv run --project tools tools/bench/dataset.py freeze              # validate the filled sheets and write tests/classifier_independent/{dev,test}.jsonl and manifest.json

The cards say WHAT an author must do (an act, an addressee, a situation, a subject, a hard-case instruction), never how to word it, and the generator's templates are not used, so the wording is the author's own.
Dev and test are written by different authors. Nothing here writes text or labels: the sheets are blank until real people fill them, and `freeze` refuses to produce a dataset unless every row has an author, every critical
act has a second label from a different person, every disagreement has an adjudicated label from a third person, no author wrote for both splits and no sentence repeats. A frozen dataset records its hashes in
the manifest; the benchmark (tools/bench/bench_classifier.py) refuses an independent run whose files do not match them. Until then the only data is the old synthetic set, which is smoke-test data only.
"""
from __future__ import annotations

import csv
import hashlib
import json
import random
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TAXONOMY = ROOT / "content" / "assessment" / "taxonomy.json"
SHEETS = ROOT / "docs" / "research" / "classifier_benchmark"
OUT = ROOT / "tests" / "classifier_independent"

# rows per split: (per critical act, per other act, NONE, rows in each special bucket, author slots). Dev 5*19+5 = 100; test 15*5 + 8*14 + 13 = 200.
SPLITS = {"dev": (5, 5, 5, 8, ("A", "B")), "test": (15, 8, 13, 16, ("C", "D"))}
SPECIAL = ("negation", "correction", "incomplete", "ambiguous")
CARD_COLUMNS = ["card_id", "split", "author_slot", "act", "critical", "bucket", "addressee", "definition", "context", "subject", "instruction", "confusable"]
FILL_COLUMNS = ["text", "author", "labeller", "label_2", "adjudicated_label", "adjudicator", "notes"]
NEAR_DUPLICATE = 0.8                                  # token Jaccard at or above this between two sentences counts as the same wording (fixed before any model sees the data)

INSTRUCTIONS = {
    "plain": "Say it the way you naturally would in this situation.",
    "paraphrase": "Say it informally, as you would to a friend or a busy colleague; avoid textbook phrasing.",
    "negation": "Include a negation (not, no, never, haven't...) and still perform this act.",
    "correction": "Start to say it, then correct yourself part way through (sorry, I mean...); the sentence ends up performing this act.",
    "incomplete": "Trail off or cut yourself off before you finish, but leave enough that the meaning can still be recovered.",
    "ambiguous": "Write something a reasonable reader could take either as this act or as {confusable}. You still label it with this act.",
}
CONFUSABLE = {"ASK_TOPIC": "STATE_ALLERGY", "CLARIFY": "CHECK_UNDERSTANDING", "ANSWER_QUESTION": "EXPLAIN_PROCEDURE", "EXPLAIN_PROCEDURE": "EXPLAIN_ACTION", "EXPLAIN_TEST": "EXPLAIN_PROCEDURE",
              "EXPLAIN_ACTION": "EXPLAIN_PROCEDURE", "CHECK_UNDERSTANDING": "CLARIFY", "ACKNOWLEDGE_EMOTION": "REASSURE", "REASSURE": "ACKNOWLEDGE_EMOTION", "ACKNOWLEDGE_FACT": "CLOSED_LOOP",
              "CLOSED_LOOP": "ACKNOWLEDGE_FACT", "ESCALATE_SPEECH": "VOICE_ORDER", "REPORT_FAULT": "ESCALATE_SPEECH", "SBAR_ELEMENT": "HANDOVER_CRITICAL_ELEMENT", "VOICE_ORDER": "ESCALATE_SPEECH",
              "CONFIRM_IDENTITY": "ASK_TOPIC", "STATE_ALLERGY": "ASK_TOPIC", "CONSENT_SIGN": "EXPLAIN_PROCEDURE", "HANDOVER_CRITICAL_ELEMENT": "STATE_ALLERGY"}
COLLEAGUE = {"CLOSED_LOOP", "ESCALATE_SPEECH", "REPORT_FAULT", "SBAR_ELEMENT", "VOICE_ORDER", "HANDOVER_CRITICAL_ELEMENT"}
CONTEXTS = ["Pre-operative assessment clinic; the patient is booked for a laparoscopic gallbladder removal tomorrow.", "Ward bay at 7 a.m.; the patient is nil by mouth for an appendix operation today.",
            "Day-surgery unit; a hernia repair is planned this morning and the patient has come in alone.", "Anaesthetic room; the patient has just been brought through from the ward.",
            "Theatre reception; the list is running late and the team is waiting.", "Recovery bay; the operation is over and the patient is waking up.",
            "Pre-operative ward round; a nurse and a junior doctor are with you.", "Busy morning on the surgical ward; one porter and two nurses are on duty."]
SUBJECTS = {
    "ASK_TOPIC": ["the last meal and drink", "medicines at home", "earlier operations and anaesthetics", "the main complaint", "smoking or alcohol"],
    "CLARIFY": ["something the patient said that was vague", "a word you did not catch", "an instruction from a colleague that was incomplete"],
    "ANSWER_QUESTION": ["whether the patient will be asleep", "how long the operation lasts", "who will be with the patient", "when the patient can eat afterwards"],
    "EXPLAIN_PROCEDURE": ["the operation", "the anaesthetic", "the recovery room"],
    "EXPLAIN_TEST": ["a blood test", "a heart tracing", "a chest X-ray", "a urine test"],
    "EXPLAIN_ACTION": ["putting a cuff on the arm", "listening to the chest", "taking blood", "checking the wristband"],
    "CHECK_UNDERSTANDING": ["the fasting instructions", "what will happen in theatre", "when to ring the bell"],
    "ACKNOWLEDGE_EMOTION": ["nervousness before the operation", "frustration at the long wait", "fear of not waking up"],
    "REASSURE": ["the team being present throughout", "pain being treated", "no need to rush"],
    "ACKNOWLEDGE_FACT": ["a medicine the patient stopped", "when the patient last ate", "a symptom the patient reported"],
    "CLOSED_LOOP": ["an instruction to give a medicine", "a result read out to you", "a request to order a test"],
    "ESCALATE_SPEECH": ["a patient who is getting worse", "a decision outside your role", "an unexpected finding"],
    "REPORT_FAULT": ["a monitor", "a piece of suction or diathermy equipment", "a bed or trolley"],
    "SBAR_ELEMENT": ["the situation of the patient", "relevant background", "your assessment", "what you recommend"],
    "VOICE_ORDER": ["a blood test", "a medicine", "transport to theatre", "a call to a specialist"],
    "CONFIRM_IDENTITY": ["name and date of birth", "the wristband against the notes"],
    "STATE_ALLERGY": ["asking about allergies", "the reaction to a named medicine"],
    "CONSENT_SIGN": ["agreement to the operation", "the consent form", "agreement to the anaesthetic"],
    "HANDOVER_CRITICAL_ELEMENT": ["an allergy", "the fasting state", "an identity mismatch", "a worsening observation"],
    "NONE": ["small talk", "thinking aloud", "a filler or a pause", "something about the shift"],
}


def taxonomy() -> list[dict]:
    return json.loads(TAXONOMY.read_text(encoding="utf-8"))["acts"]


def build_cards(split: str) -> list[dict]:
    per_crit, per_other, per_none, special, slots = SPLITS[split]
    acts = taxonomy()
    rng = random.Random(f"bench-cards-v1:{split}")
    plan: list[tuple[dict, str]] = []
    for a in acts:
        n = per_none if a["id"] == "NONE" else per_crit if a["critical"] else per_other
        plan += [(a, "plain")] * n
    index = {a["id"]: [i for i, (x, _) in enumerate(plan) if x["id"] == a["id"]] for a in acts}
    order = [a["id"] for a in acts]
    rng.shuffle(order)
    for k, bucket in enumerate(SPECIAL):
        eligible = [x for x in order if bucket != "ambiguous" or x in CONFUSABLE]
        for j in range(special):
            act_id = eligible[(j + k * 5) % len(eligible)]
            free = [i for i in index[act_id] if plan[i][1] == "plain"]
            if len(free) > 1:                                              # always leave an act at least one plain row
                plan[free[0]] = (plan[free[0]][0], bucket)
    for i, (a, bucket) in enumerate(plan):                                 # the rest alternate between plain and informal wording
        if bucket == "plain" and i % 2:
            plan[i] = (a, "paraphrase")
    rng.shuffle(plan)
    cards = []
    for n, (a, bucket) in enumerate(plan, 1):
        subject = SUBJECTS[a["id"]][rng.randrange(len(SUBJECTS[a["id"]]))]
        conf = CONFUSABLE.get(a["id"], "") if bucket == "ambiguous" else ""
        cards.append({"card_id": f"{split[0].upper()}{n:03d}", "split": split, "author_slot": slots[n % 2], "act": a["id"], "critical": str(a["critical"]).lower(), "bucket": bucket,
                      "addressee": "colleague" if a["id"] in COLLEAGUE else "anyone" if a["id"] == "NONE" else "patient", "definition": a["definition"],
                      "context": CONTEXTS[rng.randrange(len(CONTEXTS))], "subject": subject, "instruction": INSTRUCTIONS[bucket].format(confusable=conf), "confusable": conf})
    return cards


def write_sheet(path: Path, rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=CARD_COLUMNS + FILL_COLUMNS, lineterminator="\n")
        w.writeheader()
        for r in rows:
            w.writerow({**{c: "" for c in FILL_COLUMNS}, **r})


def read_sheet(path: Path) -> list[dict]:
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def tokens(text: str) -> set[str]:
    return set(re.sub(r"[^a-z0-9' ]", " ", text.lower()).split())


def jaccard(a: set[str], b: set[str]) -> float:
    return len(a & b) / len(a | b) if a | b else 0.0


def near_duplicates(left: list[str], right: list[str] | None = None, cut: float = NEAR_DUPLICATE) -> list[tuple[int, int, float]]:
    """Pairs (i, j, jaccard) of sentences with the same normalised wording or token overlap at or above `cut`; within one list when `right` is None."""
    toks_l = [tokens(t) for t in left]
    toks_r = toks_l if right is None else [tokens(t) for t in right]
    out = []
    for i, a in enumerate(toks_l):
        for j, b in enumerate(toks_r):
            if right is None and j <= i:
                continue
            s = jaccard(a, b)
            if s >= cut:
                out.append((i, j, round(s, 3)))
    return out


def kappa(a: list[str], b: list[str]) -> float | None:
    """Cohen's kappa between two label lists of equal length."""
    n = len(a)
    if n == 0:
        return None
    po = sum(x == y for x, y in zip(a, b)) / n
    pe = sum((a.count(c) / n) * (b.count(c) / n) for c in set(a) | set(b))
    return None if pe == 1 else round((po - pe) / (1 - pe), 3)


def problems(rows_by_split: dict[str, list[dict]], acts: set[str], critical: set[str]) -> list[str]:
    """Everything that stops a filled sheet from being frozen as an independent dataset. Empty list = freezable."""
    out = []
    authors = {s: {r["author"].strip().lower() for r in rows if r["author"].strip()} for s, rows in rows_by_split.items()}
    if authors.get("dev", set()) & authors.get("test", set()):
        out.append(f"the same author wrote for dev and test: {sorted(authors['dev'] & authors['test'])}")
    for s, rows in rows_by_split.items():
        if len(authors[s]) < 2:
            out.append(f"{s}: needs at least two authors, has {len(authors[s])}")
        for r in rows:
            cid = r["card_id"]
            if not r["text"].strip() or not r["author"].strip():
                out.append(f"{cid}: text or author is empty")
                continue
            lab2, adj = r["label_2"].strip(), r["adjudicated_label"].strip()
            if r["act"] in critical and not lab2:
                out.append(f"{cid}: critical act without a second label")
            for field in (lab2, adj):
                if field and field not in acts:
                    out.append(f"{cid}: unknown act '{field}'")
            if lab2 and lab2 != r["act"]:
                if not adj or not r["adjudicator"].strip():
                    out.append(f"{cid}: the labellers disagree and nobody adjudicated")
                elif r["adjudicator"].strip().lower() in (r["author"].strip().lower(), r["labeller"].strip().lower()):
                    out.append(f"{cid}: the adjudicator must be a third person")
            if lab2 and r["labeller"].strip().lower() == r["author"].strip().lower():
                out.append(f"{cid}: the second labeller must not be the author")
    texts = [(s, r["card_id"], r["text"]) for s, rows in rows_by_split.items() for r in rows if r["text"].strip()]
    for i, j, sim in near_duplicates([t for _, _, t in texts]):
        out.append(f"{texts[i][1]} and {texts[j][1]} are the same wording (overlap {sim})")
    return out


def final_label(r: dict) -> str:
    return r["adjudicated_label"].strip() or r["act"]


def freeze() -> int:
    tax = taxonomy()
    acts, critical = {a["id"] for a in tax}, {a["id"] for a in tax if a["critical"]}
    rows = {s: read_sheet(SHEETS / f"cards_{s}.csv") for s in SPLITS}
    for s, rs in rows.items():                                              # merge the second labeller's blind sheet, if there is one
        sheet = SHEETS / f"label_sheet_{s}.csv"
        second = {r["card_id"]: r for r in read_sheet(sheet)} if sheet.exists() else {}
        for r in rs:
            if r["card_id"] in second and second[r["card_id"]]["label_2"].strip():
                r["label_2"], r["labeller"] = second[r["card_id"]]["label_2"], second[r["card_id"]]["labeller"]
    bad = problems(rows, acts, critical)
    if bad:
        print("NOT FROZEN:\n  " + "\n  ".join(bad[:40]) + (f"\n  ... and {len(bad) - 40} more" if len(bad) > 40 else ""))
        return 1
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = {"note": "Independent communication-act benchmark. Frozen: the benchmark refuses files that do not match these hashes. Written by the authors named in the rows, second-labelled on every critical act, disagreements adjudicated.", "files": {}, "rows": {}, "authors": {}, "kappa_critical": {}}
    for s, rs in rows.items():
        lines = [json.dumps({"id": r["card_id"], "split": s, "text": r["text"].strip(), "act": final_label(r), "intended_act": r["act"], "critical": r["critical"] == "true", "bucket": r["bucket"],
                             "author": r["author"].strip(), "second_label": r["label_2"].strip(), "adjudicated": bool(r["adjudicated_label"].strip()), "derived_from": "", "independent": True}, ensure_ascii=False, sort_keys=True) for r in rs]
        text = "".join(l + "\n" for l in lines)
        (OUT / f"{s}.jsonl").write_text(text, encoding="utf-8", newline="\n")
        manifest["files"][f"{s}.jsonl"] = hashlib.sha256(text.encode("utf-8")).hexdigest()
        manifest["rows"][s] = len(rs)
        manifest["authors"][s] = sorted({r["author"].strip() for r in rs})
        pairs = [(r["act"], r["label_2"].strip()) for r in rs if r["label_2"].strip()]
        manifest["kappa_critical"][s] = kappa([p[0] for p in pairs], [p[1] for p in pairs])
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("frozen:", manifest["rows"], "kappa", manifest["kappa_critical"])
    return 0


def sheet_texts() -> dict[str, list[dict]]:
    return {s: build_cards(s) for s in SPLITS}


def main(argv: list[str]) -> int:
    cmd = argv[0] if argv else "cards"
    if cmd == "cards":
        built = sheet_texts()
        if "--check" in argv:
            stale = []
            for s, cards in built.items():
                p = SHEETS / f"cards_{s}.csv"
                have = [{c: r[c] for c in CARD_COLUMNS} for r in read_sheet(p)] if p.exists() else None
                if have != cards:
                    stale.append(p.name)
            print("FAIL out of date: " + ", ".join(stale) + "; run tools/bench/dataset.py cards" if stale else "scenario cards are current (blank authoring sheets; dataset creation is blocked until people fill them)")
            return 1 if stale else 0
        SHEETS.mkdir(parents=True, exist_ok=True)
        for s, cards in built.items():
            write_sheet(SHEETS / f"cards_{s}.csv", cards)
        print("wrote", {s: len(c) for s, c in built.items()})
        return 0
    if cmd == "label-sheet":
        split = argv[1]
        rows = read_sheet(SHEETS / f"cards_{split}.csv")
        path = SHEETS / f"label_sheet_{split}.csv"
        with path.open("w", encoding="utf-8", newline="") as f:
            w = csv.writer(f, lineterminator="\n")
            w.writerow(["card_id", "text", "label_2", "labeller"])
            others = [r for r in rows if r["critical"] != "true" and r["text"].strip()]
            sample = {r["card_id"] for r in random.Random(f"bench-label-sheet-v1:{split}").sample(others, len(others) // 4)}      # a quarter of the other rows, so the labeller cannot tell which rows are critical
            for r in rows:
                if r["text"].strip() and (r["critical"] == "true" or r["card_id"] in sample):
                    w.writerow([r["card_id"], r["text"], "", ""])
        print("wrote", path)
        return 0
    if cmd == "freeze":
        return freeze()
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
