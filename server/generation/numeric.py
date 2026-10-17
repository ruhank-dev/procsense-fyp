"""Numeric generation: laboratory values and starting vital signs from the packs (p1-live-scenario-generation-s05, stage LG-07).

    from generation.numeric import vary_numbers
    log = vary_numbers(card, seed, lab_pack, vitals_pack, news2_score)

A composed case keeps its story, so its numbers are of two kinds. The values the story is built on (an abnormal result, a value named in the text, a result from a walkthrough row, a sample with a fault, a
result with a previous value, an observation that is not the starting one) are never touched. The rest are drawn again from the packs, seeded per slot, so two patients with the same story do not have
identical normal labs and starting vital signs:
  * a normal laboratory result is drawn from the normal zone of the walkthrough's band, shaped by the dataset's own curve (the pack), and kept only if it is also inside the case's own normal range;
    linked analytes (white cell count and CRP, ALT and bilirubin) are drawn together through the pack's correlation, independently if the pair is weak;
  * the starting vital signs move a little around the patient's own reading, by a share of the spread of real triage values (the pack), inside the range that scores 0 on NEWS2, and are kept only if the
    case's NEWS2 total stays what it was (the check is the caller's `news2_score`).
Every slot has its own random stream (seed and slot name). A draw that fails its check is repeated up to 20 times and then the original value stays; every decision is returned in a log, which the package keeps.
No language model is involved, and nothing is learned at run time.
"""
from __future__ import annotations

import random
import re
from typing import Callable

from . import packs

TRIES = 20
JITTER = 0.35          # the starting vital signs move by this share of the real spread (standard deviation of the triage values)
LAB_NAMES = {"hb": "haemoglobin", "haemoglobin": "haemoglobin", "wbc": "wbc", "platelets": "platelets", "sodium": "sodium", "potassium": "potassium", "creatinine": "creatinine", "crp": "crp",
             "alt": "alt", "alp": "alp", "bilirubin": "bilirubin", "albumin": "albumin", "inr": "inr", "lactate": "lactate", "urea": "urea", "ggt": "ggt"}
LINKED = (("wbc", "crp"), ("alt", "bilirubin"), ("alp", "bilirubin"))
# NEWS2 scores 0 inside these ranges (RCP 2017); the starting vitals are drawn inside them
NEWS2_ZERO = {"heart_rate": (51, 90), "systolic_bp": (111, 219), "respiratory_rate": (12, 20), "spo2": (96, 100), "temperature_c": (36.1, 38.0)}
TRIAGE_KEYS = {"heart_rate": "heart_rate", "systolic_bp": "systolic_bp", "respiratory_rate": "respiratory_rate", "spo2": "spo2", "temperature_c": "temperature_c"}


def stream(seed: int, slot: str) -> random.Random:
    return random.Random(f"{seed}:{slot}")


def in_normal(value: float, normal: str) -> bool | None:
    """True or False if the normal range text can be read ('3-21', 'below 5', '4.0-11.0 (note)'), None if it cannot."""
    t = normal.lower().strip()
    m = re.match(r"^below (\d+(?:\.\d+)?)", t)
    if m:
        return value < float(m.group(1))
    m = re.match(r"^(\d+(?:\.\d+)?)\s*(?:-|to)\s*(\d+(?:\.\d+)?)", t)
    if m:
        return float(m.group(1)) <= value <= float(m.group(2))
    m = re.match(r"^(\d+(?:\.\d+)?) or above", t)
    if m:
        return value >= float(m.group(1))
    return None


def decimals(text: str) -> int:
    return len(text.split(".")[1]) if "." in text else 0


def card_text(card: dict, skip: str = "") -> str:
    """Every string of the card except the one under test, so a number named in the story is found."""
    parts = []

    def walk(x, key=""):
        if isinstance(x, str):
            parts.append(x)
        elif isinstance(x, dict):
            for k, v in x.items():
                if k != "composition":
                    walk(v, k)
        elif isinstance(x, list):
            for v in x:
                walk(v)
    walk({k: v for k, v in card.items() if k not in ("results", "observations")})
    for r in card["results"]:
        if r["id"] != skip:
            parts.append(r.get("comment") or "")
    for o in card["observations"]:
        parts.append(o.get("note") or "")
    return " ".join(parts)


def mentions(text: str, value: str) -> bool:
    return re.search(r"(?<![0-9A-Za-z.])" + re.escape(value) + r"(?![0-9])(?!\.[0-9])", text) is not None


def variable_result(card: dict, r: dict) -> str | None:
    """None if the result may be drawn again, otherwise the reason it may not."""
    if r.get("variant"):
        return "belongs to a variant"
    if r["flag"] != "normal":
        return "not normal"
    if r.get("quality") or r.get("previous"):
        return "sample fault or a previous value"
    base = r["test"].split(" (")[0].strip().lower()
    if base not in LAB_NAMES:
        return "no pack row for this test"
    if re.fullmatch(r"-?\d+(?:\.\d+)?", r["value"].strip()) is None:
        return "not a plain number"
    if mentions(card_text(card, r["id"]), r["value"].strip()):
        return "the value is named in the story"
    return None


def draw_result(pack: packs.LabPack, analyte: str, sex: str, r: dict, rng: random.Random, u: float | None = None) -> tuple[float | None, str]:
    for _ in range(TRIES):
        d = pack.draw(analyte, "normal", rng, sex, u)
        v = round(d.value, decimals(r["value"]))
        if in_normal(v, r["normal_range"]) is not False:          # inside the case's own range, or its range cannot be read (then the pack's band stands)
            return v, d.source
        u = None
    return None, ""


def vary_numbers(card: dict, seed: int, lab_pack: packs.LabPack, vitals_pack: dict, news2_score: Callable[..., int]) -> list[dict]:
    """Draws the free numbers of a composed card again, in place. Returns the log: one entry per slot looked at (changed or kept, and why)."""
    log: list[dict] = []
    sex = card["identity"]["sex"]
    by_analyte: dict[str, list[dict]] = {}
    for r in card["results"]:
        why = variable_result(card, r)
        if why:
            log.append({"slot": r["id"], "action": "kept", "why": why})
        else:
            by_analyte.setdefault(LAB_NAMES[r["test"].split(" (")[0].strip().lower()], []).append(r)
    done: set[str] = set()
    if lab_pack.joint() is not None:
        _panel(by_analyte, lab_pack, sex, seed, log, done)
    else:
        _pairs(by_analyte, lab_pack, sex, seed, log, done)
    for analyte, rows in by_analyte.items():
        for r in rows:
            if r["id"] in done:
                continue
            rng = stream(seed, r["id"])
            v, source = draw_result(lab_pack, analyte, sex, r, rng)
            if v is None:
                log.append({"slot": r["id"], "action": "kept", "why": f"no draw of {TRIES} fitted the case's own normal range"})
                continue
            new = f"{v:.{decimals(r['value'])}f}"
            log.append({"slot": r["id"], "action": "drawn", "from": r["value"], "to": new, "source": source})
            r["value"] = new
            r["origin"] = "authored"          # no longer the walkthrough's number
            r.pop("source_row", None)
    _vitals(card, seed, vitals_pack, news2_score, log)
    return log


def _store(r: dict, v: float, d: packs.Draw, log: list[dict], done: set[str], together: list[str]) -> None:
    to = f"{v:g}" if decimals(r["value"]) == 0 else f"{v:.{decimals(r['value'])}f}"
    log.append({"slot": r["id"], "action": "drawn", "from": r["value"], "to": to, "source": d.source, "together_with": together})
    r["value"] = to
    r["origin"] = "authored"
    r.pop("source_row", None)
    done.add(r["id"])


def _panel(by_analyte: dict[str, list[dict]], lab_pack: packs.LabPack, sex: str, seed: int, log: list[dict], done: set[str]) -> None:
    """All the free results of the case drawn together through the joint model (fit_joint.py): results that move together in real patients still do. A draw whose value does not fit the case's own normal
    range for that row is redrawn with the rest of the panel (up to TRIES panels); a row that never fits keeps its authored value."""
    pending = {a: rows[0] for a, rows in sorted(by_analyte.items())}           # one row per analyte; further rows of the same analyte are drawn alone below
    for attempt in range(TRIES):
        if not pending:
            break
        order = sorted(pending)
        draws = lab_pack.draw_panel([(a, "normal") for a in order], stream(seed, f"panel:{attempt}"), sex)
        fits = {}
        for a, d in zip(order, draws):
            r = pending[a]
            v = round(d.value, decimals(r["value"]))
            if in_normal(v, r["normal_range"]) is not False:
                fits[a] = (r, v, d)
        ids = {a: fits[a][0]["id"] for a in fits}
        for a, (r, v, d) in fits.items():
            _store(r, v, d, log, done, [ids[b] for b in sorted(ids) if b != a])
        for a in fits:
            del pending[a]


def _pairs(by_analyte: dict[str, list[dict]], lab_pack: packs.LabPack, sex: str, seed: int, log: list[dict], done: set[str]) -> None:
    """The earlier method, used when the pack folder has no joint model: the linked pairs through their own copula."""
    corr = {frozenset((c["a"], c["b"])): c for c in lab_pack.corr.values()}
    for a, b in LINKED:
        if a in by_analyte and b in by_analyte and frozenset((a, b)) in corr and not corr[frozenset((a, b))]["weak"]:
            ra, rb = by_analyte[a][0], by_analyte[b][0]
            rng = stream(seed, f"{ra['id']}+{rb['id']}")
            for _ in range(TRIES):
                da, db = lab_pack.draw_pair(a, b, "normal", "normal", rng, sex)
                va, vb = round(da.value, decimals(ra["value"])), round(db.value, decimals(rb["value"]))
                if in_normal(va, ra["normal_range"]) is not False and in_normal(vb, rb["normal_range"]) is not False:
                    _store(ra, va, da, log, done, [rb["id"]])
                    _store(rb, vb, db, log, done, [ra["id"]])
                    break


def _vitals(card: dict, seed: int, vitals_pack: dict, news2_score: Callable[..., int], log: list[dict]) -> None:
    o = card["observations"][0]
    if o["condition"] != "initial" or o["news2"] != 0 or o.get("variant"):
        log.append({"slot": "obs.initial", "action": "kept", "why": "the starting observation is not an all-normal one"})
        return
    text = card_text(card)
    group = vitals_pack["triage"]["groups"]["all_ed_triage"]["variables"]
    new = {}
    for key, (lo, hi) in NEWS2_ZERO.items():
        if mentions(text, f"{o[key]:g}"):
            log.append({"slot": f"obs.initial.{key}", "action": "kept", "why": "the value is named in the story"})
            continue
        q = group[TRIAGE_KEYS[key]]
        sd = max((q["p75"] - q["p25"]) / 1.35 * JITTER, 0.1)       # the spread of real triage values, scaled down: a patient stays near his or her own starting reading
        rng = stream(seed, f"obs.initial.{key}")
        for _ in range(TRIES):
            value = o[key] + rng.gauss(0, sd)
            value = round(value, 1) if key == "temperature_c" else round(value)
            if lo <= value <= hi:
                new[key] = value
                break
        else:
            log.append({"slot": f"obs.initial.{key}", "action": "kept", "why": "no draw fitted the range that scores 0"})
    trial = {**o, **new}
    if new and news2_score(trial["heart_rate"], trial["systolic_bp"], trial["respiratory_rate"], trial["spo2"], trial["temperature_c"], trial["acvpu"], trial["oxygen"]) == 0:
        for k, v in new.items():
            log.append({"slot": f"obs.initial.{k}", "action": "drawn", "from": f"{o[k]:g}", "to": f"{v:g}"})
            o[k] = v
    elif new:
        log.append({"slot": "obs.initial", "action": "kept", "why": "the new set would not score 0 on NEWS2"})
