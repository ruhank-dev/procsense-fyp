"""The block library of the scenario composer (p1-scenario-composer-difficulty-s01, guide B-M1 steps 1 and 2).

    uv run --project tools tools/composer/blocks.py            # write content/blocks/<TYPE>/*.json, content/blocks/tags.yaml and content/templates/SCN-S1.json, SCN-S2.json
    uv run --project tools tools/composer/blocks.py --check    # fail if they are out of date, a tag is undefined, a block lacks its source case, or a block is drawn from a case below its status (CI)

How the composer works in P1 (decision of 17 October 2026): a composed case is an approved **spine** (an authored case file that fixes the clinical story: its problems, decisions, paths, debrief, risk and end state)
varied by **blocks** that do not change that story: who the patient is (name, title, age inside the same age class, occupation), the social and anaesthetic history answers, and the allergy when the story never touches
allergies. Anything that would change the pathway (a comorbidity, a medicine, a different result) is NOT varied here: it travels with its whole source case, so a stacked case is a case already authored (P1 stacked
cases) or built with the 14B.5 recipe (p2-content-extensions-s02). This keeps every composed case inside a story that was verified, and Stage B plus the headless run still check each one.
Of the 21 block types the plan names, five carry variation now (DEM, NAME, SOC, PSH, ALG); the others are owned by the spine and listed so the vocabulary is complete (they open up as the generators of
p1-live-scenario-generation arrive: OBS and LAB by the packs, IMG by the tags, EVT by the director).
Every block is cut from a named source case, carries its tags, what it requires and excludes, its sources and its status (the lowest of its source cases). Nothing is invented to fill a gap.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
BLOCKS = ROOT / "content" / "blocks"
TEMPLATES = ROOT / "content" / "templates"
CASES = ROOT / "content" / "case_files"
STATUS_ORDER = ["DRAFT", "CLINICAL_REVIEW", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"]
AGE_EDGES = [12, 16, 18, 50, 55, 60, 65, 70, 75]       # every age threshold a rule of the engine or the tables uses: a block never moves a patient across one

TYPES = {
    "DEM": ("Who the patient is: title, sex, age, occupation", "variable"), "NAME": ("A first name or a family name", "variable"), "SOC": ("Smoking and alcohol answers", "variable"),
    "PSH": ("Previous and family anaesthetic answers", "variable"), "ALG": ("Allergies and the allergy answer", "variable"),
    "PRES": ("The presentation and the booked procedure", "spine"), "CMB": ("A comorbidity", "spine"), "MED": ("A medicine and its reason", "spine"), "OBS": ("Observations over time", "spine"),
    "FND": ("Examination findings", "spine"), "RSK": ("ASA grade and the anaesthetic plan", "spine"), "LAB": ("Laboratory results", "spine"), "IMG": ("Imaging and ECG results", "spine"),
    "CON": ("The consent situation", "spine"), "PER": ("The patient's persona and how they behave", "spine"), "PAIN": ("Pain history and score", "spine"), "DIFF": ("The decisions and the difficulty", "spine"),
    "EVT": ("Events and what-if branches", "spine"), "ENV": ("The setting and the room", "spine"), "STAFF": ("Who is present and who can be called", "spine"), "TIME": ("Fasting times and the clock", "spine"),
}

TAGS: dict[str, str] = {
    "sex:F": "female patient", "sex:M": "male patient",
    **{f"age_class:{lo}-{hi}": f"age {lo} to {hi - 1}" for lo, hi in zip([0] + AGE_EDGES, AGE_EDGES + [200])},
    "smoking:never": "has never smoked", "smoking:current": "smokes", "smoking:former": "used to smoke", "smoking:other": "smoking answer not classified",
    "alcohol:none": "no alcohol", "alcohol:some": "drinks alcohol", "alcohol:other": "alcohol answer not classified",
    "anaesthetic:uneventful": "previous and family anaesthetics without problems", "anaesthetic:noteworthy": "a previous or family anaesthetic with something to note",
    "allergy:none": "no known allergy", "allergy:drug": "a drug allergy", "allergy:other": "an allergy that is not a drug",
    "procedure:cholecystectomy": "the booked operation removes the gallbladder", "procedure:appendicectomy": "the booked operation removes the appendix",
    "pathway:antibiotic": "the story involves an antibiotic choice", "pathway:allergy": "the story touches an allergy", "pathway:smoking": "the story depends on smoking", "pathway:alcohol": "the story depends on alcohol",
    "pathway:anaesthetic_history": "the story depends on an earlier anaesthetic", "pathway:identity": "the story is about identity, wristbands or names",
    "female_surgery_history": "mentions a surgery or event only a woman has had",
}
SPINE_PATHWAY_WORDS = {
    "pathway:antibiotic": r"antibiotic|antimicrobial|beta-lactam|cephalosporin|penicillin", "pathway:allergy": r"allerg|anaphyla|rash|hypersensitiv", "pathway:smoking": r"smok|cigarette|copd|inhaler",
    "pathway:alcohol": r"alcohol|drinks|withdrawal", "pathway:anaesthetic_history": r"previous anaesthetic|family anaesthetic|malignant hyperthermia|suxamethonium|difficult airway|post-?operative nausea",
    "pathway:identity": r"wristband|mismatch|identity|misidentif|wrong name|wrong patient",
}
# defined for the vocabulary but no authored case has them yet (every case answers: never smoked, anaesthetics uneventful); a case that does will use them without a change here
RESERVED = {"smoking:other", "alcohol:other", "allergy:other", "pathway:identity", "pathway:smoking", "smoking:current", "smoking:former", "anaesthetic:noteworthy"}
FEMALE_ONLY = re.compile(r"caesarean|hysterectomy|childbirth|delivery|pregnan|uterus|ovar|gynae|hysteroscopy|termination", re.IGNORECASE)
GALLBLADDER = re.compile(r"gallbladder|cholecyst", re.IGNORECASE)
APPENDIX = re.compile(r"appendic|appendect|appendix", re.IGNORECASE)


def age_class(age: int) -> str:
    edges = [0] + AGE_EDGES + [200]
    for lo, hi in zip(edges, edges[1:]):
        if lo <= age < hi:
            return f"age_class:{lo}-{hi}"
    raise ValueError(age)


def load_cases() -> list[dict]:
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted(CASES.glob("*.json"))]


def worst(statuses: list[str]) -> str:
    return min(statuses, key=STATUS_ORDER.index)


def case_text(c: dict) -> str:
    """The words that tell what a story depends on (not the patient's own answers): problems, decisions, paths, debrief and the presentation."""
    parts = [c["presentation"]["summary"]]
    for p in c["problems"]:
        parts += [p["label"], p["why"], *p.get("expected_actions", [])]
    for d in c["decisions"]:
        parts += [d["prompt"], *(o["text"] + " " + o["consequence"] for o in d["options"])]
    parts += c["paths"]["correct"] + [x for m in c["paths"]["incorrect"] for x in m.values()] + c["paths"]["missed"] + c["paths"]["recovery"] + list(c["debrief"].values())[:0]
    for v in c["debrief"].values():
        parts += v if isinstance(v, list) else [v]
    return " ".join(str(x) for x in parts)


def spine_tags(c: dict) -> set[str]:
    """What the composer must keep still in a spine: its sex, age class, procedure and the things its story depends on."""
    tags = {f"sex:{c['identity']['sex']}", age_class(c["identity"]["age"]), "procedure:cholecystectomy" if c["scenario"] == "S1" else "procedure:appendicectomy"}
    text = case_text(c)
    for tag, pattern in SPINE_PATHWAY_WORDS.items():
        if re.search(pattern, text, re.IGNORECASE):
            tags.add(tag)
    if c["allergies"]:
        tags.add("pathway:allergy")
    return tags


def history(c: dict, topic: str) -> str | None:
    for h in c["history"]:
        if h["topic"] == topic and h.get("variant") is None:
            return h["answer"]
    return None


def smoking_tag(text: str) -> str:
    t = text.lower()
    if re.match(r"(never|no\b|non-?smoker)", t):
        return "smoking:never"
    if re.search(r"stopped|quit|ex-?smoker|gave up|former", t):
        return "smoking:former"
    if re.search(r"cigarette|smoke[sd]?\b|a day|per day|pack", t):
        return "smoking:current"
    return "smoking:other"


def alcohol_tag(text: str) -> str:
    t = text.lower()
    if re.match(r"(none|never|no\b|does not drink|doesn't drink|teetotal)", t):
        return "alcohol:none"
    if re.search(r"unit|drink|beer|wine|glass|week|occasional|social", t):
        return "alcohol:some"
    return "alcohol:other"


def block(btype: str, bid: str, attrs: dict, tags: list[str], derived: list[str], statuses: list[str], requires: list[str] | None = None, excludes: list[str] | None = None) -> dict:
    return {"id": bid, "type": btype, "attributes": attrs, "tags": sorted(set(tags)), "requires": sorted(requires or []), "excludes": sorted(excludes or []), "enables_rules": [], "enables_events": [],
            "derived_from": sorted(set(derived)), "sources": sorted({s for c in derived for s in _sources.get(c, [])}), "status": worst(statuses)}


_sources: dict[str, list[str]] = {}


def slug(s: str) -> str:
    return re.sub(r"[^A-Z0-9]+", "-", s.upper()).strip("-")


def build_blocks(cases: list[dict]) -> list[dict]:
    _sources.clear()
    for c in cases:
        _sources[c["id"]] = [c["sources"]["story"], c["sources"]["authored"]]
    out: list[dict] = []
    firsts: dict[tuple[str, str], list[dict]] = {}
    lasts: dict[str, list[dict]] = {}
    for c in cases:
        i = c["identity"]
        parts = i["full_name"].split()
        firsts.setdefault((i["sex"], parts[0]), []).append(c)
        lasts.setdefault(" ".join(parts[1:]), []).append(c)
        out.append(block("DEM", f"DEM-{c['id']}", {"title": i["title"], "sex": i["sex"], "age": i["age"], "occupation": i["occupation"]}, [f"sex:{i['sex']}", age_class(i["age"])], [c["id"]], [c["status"]]))
        s, a = history(c, "smoking"), history(c, "alcohol")
        if s and a:
            out.append(block("SOC", f"SOC-{c['id']}", {"smoking": s, "alcohol": a}, [smoking_tag(s), alcohol_tag(a)], [c["id"]], [c["status"]]))
        pa, fa = history(c, "previous_anaesthetic"), history(c, "family_anaesthetic")
        if pa and fa:
            text = pa + " " + fa
            bad = re.search(r"problem|reaction|sick|nausea|difficult|malignant|slow to wake|complication|delayed", text, re.IGNORECASE) and not re.search(r"no (anaesthetic )?problems", text, re.IGNORECASE)
            tags = ["anaesthetic:noteworthy" if bad else "anaesthetic:uneventful"]
            excl = []
            if FEMALE_ONLY.search(text):
                tags.append("female_surgery_history")
            if GALLBLADDER.search(text):
                excl.append("procedure:cholecystectomy")
            if APPENDIX.search(text):
                excl.append("procedure:appendicectomy")
            req = ["sex:F"] if "female_surgery_history" in tags else []
            out.append(block("PSH", f"PSH-{c['id']}", {"previous_anaesthetic": pa, "family_anaesthetic": fa}, tags, [c["id"]], [c["status"]], req, excl))
    for (sex, first), cs in sorted(firsts.items()):
        out.append(block("NAME", f"NAME-FIRST-{slug(first)}-{sex}", {"part": "first", "text": first}, [f"sex:{sex}"], [c["id"] for c in cs], [c["status"] for c in cs], [f"sex:{sex}"]))
    for last, cs in sorted(lasts.items()):
        out.append(block("NAME", f"NAME-LAST-{slug(last)}", {"part": "last", "text": last}, [], [c["id"] for c in cs], [c["status"] for c in cs]))
    # allergies: one block per distinct allergy set, and one for "none"
    by_allergy: dict[str, list[dict]] = {}
    for c in cases:
        key = json.dumps([(a["substance"], a["reaction"]) for a in c["allergies"]]) + "|" + c["allergy_answer"]
        by_allergy.setdefault(key, []).append(c)
    for key, cs in sorted(by_allergy.items()):
        c0 = cs[0]
        if re.search(r"today|just now|a minute ago", c0["allergy_answer"], re.IGNORECASE):
            continue            # a reaction that happens in the session belongs to its story (the event), not to a patient who could be anyone
        if not c0["allergies"]:
            bid, tags = f"ALG-NONE-{slug(c0['allergy_answer'])[:20]}", ["allergy:none"]
        else:
            bid, tags = "ALG-" + slug("-".join(a["substance"] for a in c0["allergies"])), ["allergy:drug"]
        attrs = {"allergies": c0["allergies"], "allergy_answer": c0["allergy_answer"]}
        out.append(block("ALG", bid, attrs, tags, [c["id"] for c in cs], [c["status"] for c in cs], [], ["pathway:antibiotic", "pathway:allergy"] if c0["allergies"] else []))
    ids = [b["id"] for b in out]
    for i in sorted({x for x in ids if ids.count(x) > 1}):                 # same-named allergy sets from different wording: keep distinct ids
        n = 0
        for b in out:
            if b["id"] == i:
                n += 1
                b["id"] = f"{i}-{n}"
    return sorted(out, key=lambda b: (b["type"], b["id"]))


def templates() -> dict[str, dict]:
    sl = lambda t, lo, hi, req=False: {"type": t, "min": lo, "max": hi, "required": req, "source": "block" if TYPES[t][1] == "variable" else "spine"}      # noqa: E731
    slots = [sl("DEM", 1, 1, True), sl("NAME", 2, 2, True), sl("SOC", 0, 1), sl("PSH", 0, 1), sl("ALG", 0, 1)] + [sl(t, 1, 1, True) for t in TYPES if TYPES[t][1] == "spine"]
    out = {}
    for scenario, proc in (("S1", "procedure:cholecystectomy"), ("S2", "procedure:appendicectomy")):
        out[scenario] = {"scenario": scenario, "procedure": proc, "slots": slots, "spine_status_at_least": "APPROVED_FOR_TRAINING",
                         "difficulty_limits": {"1": {"max_problems": 1, "max_variable_blocks": 3, "tiers": ["P0"]}, "2": {"max_problems": 2, "max_variable_blocks": 4, "tiers": ["P0", "P1"]},
                                               "3": {"max_problems": 99, "max_variable_blocks": 5, "tiers": ["P0", "P1", "P2"]}},
                         "note": "Slots with source 'spine' are fixed by the spine case; slots with source 'block' are filled by the solver. min and max count the blocks of that type."}
    return out


def render() -> dict[str, str]:
    cases = load_cases()
    files: dict[str, str] = {}
    for b in build_blocks(cases):
        files[f"content/blocks/{b['type']}/{b['id']}.json"] = json.dumps(b, indent=1, sort_keys=True, ensure_ascii=False) + "\n"
    files["content/blocks/tags.yaml"] = "# Tag vocabulary of the composer's blocks (generated by tools/composer/blocks.py; do not edit).\n" + yaml.safe_dump(dict(sorted(TAGS.items())), sort_keys=True, allow_unicode=True, width=200)
    files["content/blocks/types.json"] = json.dumps({k: {"meaning": v[0], "owner": v[1]} for k, v in TYPES.items()}, indent=1, sort_keys=True) + "\n"
    for s, t in templates().items():
        files[f"content/templates/SCN-{s}.json"] = json.dumps(t, indent=1, sort_keys=True) + "\n"
    return files


def problems() -> list[str]:
    out = []
    want = render()
    cases = {c["id"]: c for c in load_cases()}
    have = {p.relative_to(ROOT).as_posix() for p in list((ROOT / "content" / "blocks").rglob("*.json")) + list((ROOT / "content" / "blocks").glob("*.yaml")) + list(TEMPLATES.glob("*.json"))}
    for rel, text in want.items():
        p = ROOT / rel
        if not p.exists() or p.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            out.append(f"{rel} is out of date: run `uv run --project tools tools/composer/blocks.py`")
    for rel in sorted(have - set(want)):
        out.append(f"{rel} is not made by the builder any more: delete it")
    for rel, text in want.items():
        if not rel.startswith("content/blocks/") or not rel.endswith(".json") or rel.endswith("types.json"):
            continue
        b = json.loads(text)
        if b["type"] not in TYPES:
            out.append(f"{b['id']}: unknown type {b['type']}")
        for t in b["tags"] + b["requires"] + b["excludes"]:
            if t not in TAGS:
                out.append(f"{b['id']}: tag '{t}' is not in tags.yaml")
        if not b["derived_from"] or not b["sources"]:
            out.append(f"{b['id']}: no source case or no source row")
        for c in b["derived_from"]:
            if c not in cases:
                out.append(f"{b['id']}: derived from {c}, which is not a case file")
    used = set()
    for rel, text in want.items():
        if rel.startswith("content/blocks/") and rel.endswith(".json") and not rel.endswith("types.json"):
            b = json.loads(text)
            used |= set(b["tags"] + b["requires"] + b["excludes"])
    for c in cases.values():
        used |= spine_tags(c)
    for t in sorted(set(TAGS) - used):
        if not t.startswith("age_class:") and t not in RESERVED:
            out.append(f"tag '{t}' is defined but nothing uses it")
    return out


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if "--check" in argv:
        p = problems()
        for x in p:
            print("FAIL  " + x)
        if not p:
            files = render()
            n = sum(1 for k in files if k.startswith("content/blocks/") and k.endswith(".json") and not k.endswith("types.json"))
            print(f"PASS  {n} blocks of {len({json.loads(v)['type'] for k, v in files.items() if k.startswith('content/blocks/') and k.endswith('.json') and not k.endswith('types.json')})} types and 2 templates are current, tagged from the vocabulary and cut from named cases")
        return 1 if p else 0
    files = render()
    for old in list((BLOCKS).rglob("*.json")):
        old.unlink()
    for rel, text in files.items():
        p = ROOT / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files: " + ", ".join(f"{t} {sum(1 for k in files if k.startswith(f'content/blocks/{t}/'))}" for t in TYPES if any(k.startswith(f'content/blocks/{t}/') for k in files)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
