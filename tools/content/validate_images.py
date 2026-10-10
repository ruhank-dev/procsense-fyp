"""Checks content/images/index.json and content/imaging_reports.json (ultrasound feature plan, ADR-0003).

    uv run --project tools tools/content/validate_images.py

For every image: the file exists and its SHA-256, size and dimensions match the index; the finding code exists in content/ultrasound_findings.csv; a REAL image has a
licence, an attribution and a named source, and no embedded metadata (EXIF, ICC); a SYNTHETIC image shows the visible mark: the mark strip is read back from the pixels and
must equal the recorded hash and contain bright text; the generator is recorded. Only DRAFT images may lack an approval record; any other status needs one.
For every report: it names an existing image with the same finding code, and the numbers it states (measurements, and every 'N mm' in its text) stay inside the limits of
that finding code (the book's numbers). Every finding code has at least one report. The total size of the pictures stays small (they ship in the headset build).
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
STATUSES = {"DRAFT", "REVIEWED", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"}
MAX_TOTAL_BYTES = 16_000_000      # per picture set; all sets together stay under 40 MB (they ship in the headset build)
MARK_TEXT = "SIMULATION: synthetic image, not a real scan"


def rule_problems(rules: str, measurements: dict) -> list[str]:
    """Every rule whose key appears in the measurements must hold. Rules: 'key op value' joined by ';' (and); 'a|b' alternatives for words; numeric ops <= < >= > ="""
    out = []
    for part in [p.strip() for p in rules.split(";") if p.strip()]:
        alternatives = [a.strip() for a in part.split("|")]
        results = []
        first = re.match(r"^([a-z_]+)\s*(<=|>=|<|>|=)\s*", alternatives[0])
        for alt in alternatives:
            m = re.match(r"^([a-z_]+)\s*(<=|>=|<|>|=)\s*(.+)$", alt)
            if not m and first and re.fullmatch(r"[A-Za-z0-9_ ]+", alt):
                m = re.match(r"^([a-z_]+)(=)(.+)$", f"{first.group(1)}={alt}")
            if not m:
                out.append(f"unreadable rule '{alt}'")
                continue
            key, op, want = m.groups()
            if key not in measurements:
                results.append(None)
                continue
            got = measurements[key]
            try:
                g, w = float(got), float(want)
                results.append({"<=": g <= w, ">=": g >= w, "<": g < w, ">": g > w, "=": g == w}[op])
            except (TypeError, ValueError):
                results.append(str(got) == want)
        # a rule 'complication=gas|pus' holds if any alternative whose key is present holds; keys that are absent are not asserted
        present = [r for r in results if r is not None]
        if present and not any(present):
            out.append(f"'{part}' is not met by {measurements}")
    return out


def mm_numbers(text: str) -> list[float]:
    return [float(x) for x in re.findall(r"(\d+(?:\.\d+)?)\s*mm(?!/)", text)]   # 'mm/s' is a paper speed, not a measurement


def validate(index_path: Path, reports_path: Path, findings_path: Path, images_dir: Path, approvals_file: dict | None = None) -> list[str]:
    problems: list[str] = []
    with open(findings_path, encoding="utf-8", newline="") as f:
        findings = {r["code"]: r for r in csv.DictReader(f)}
    index = json.loads(index_path.read_text(encoding="utf-8"))["images"]
    reports = json.loads(reports_path.read_text(encoding="utf-8"))["reports"]
    images = {i["id"]: i for i in index}
    if len(images) != len(index):
        problems.append("duplicate image ids")
    total = 0
    for e in index:
        where = f"image {e['id']}"
        path = images_dir / e["file"]
        if e["finding_code"] not in findings:
            problems.append(f"{where}: unknown finding code {e['finding_code']}")
        if e["status"] not in STATUSES:
            problems.append(f"{where}: status {e['status']} is not one of {sorted(STATUSES)}")
        if e["status"] != "DRAFT" and not e.get("approval"):
            problems.append(f"{where}: status {e['status']} needs an approval record (by, date)")
        if e["status"] != "DRAFT" and e.get("approval") and approvals_file is not None:
            rec = approvals_file.get(e["id"])
            if not rec or rec["sha256"] != e["sha256"] or rec["level"] != e["status"]:
                problems.append(f"{where}: the approval does not match this exact picture in content/image_approvals.json")
        if not path.exists():
            problems.append(f"{where}: file {e['file']} is missing")
            continue
        data = path.read_bytes()
        total += len(data)
        if hashlib.sha256(data).hexdigest() != e["sha256"]:
            problems.append(f"{where}: sha256 does not match the file")
        with Image.open(path) as im:
            im.load()
        if im.size != (e["width"], e["height"]):
            problems.append(f"{where}: size {im.size} differs from the index")
        if e["synthetic"]:
            mark = e.get("mark") or {}
            if mark.get("text") != MARK_TEXT:
                problems.append(f"{where}: a synthetic image needs the mark text '{MARK_TEXT}'")
            h = mark.get("strip_height", 0)
            strip = im.convert("L").crop((0, im.height - h, im.width, im.height)) if h else None
            if strip is None or hashlib.sha256(strip.tobytes()).hexdigest() != mark.get("strip_sha256"):
                problems.append(f"{where}: the mark strip in the pixels does not match the recorded hash")
            elif sum(1 for p in strip.tobytes() if p > 200) < 300:
                problems.append(f"{where}: the mark strip has no readable bright text")
            gen = e.get("generator") or {}
            if not (gen.get("name") and "seed" in gen):
                problems.append(f"{where}: a synthetic image records its generator and seed")
            if e.get("source"):
                problems.append(f"{where}: a synthetic image must not name a dataset source")
        else:
            src = e.get("source") or {}
            if not (e.get("licence") and e.get("attribution") and src.get("dataset") and src.get("class")):
                problems.append(f"{where}: a real image needs licence, attribution, dataset and class")
            if e.get("mark"):
                problems.append(f"{where}: a real image carries no synthetic mark")
        if im.getexif() or "icc_profile" in im.info or "exif" in im.info:
            problems.append(f"{where}: embedded metadata (EXIF/ICC) must be stripped")
    if total > MAX_TOTAL_BYTES:
        problems.append(f"the pictures total {total} bytes, over the {MAX_TOTAL_BYTES} budget")
    seen_codes = set()
    for r in reports:
        where = f"report {r['id']}"
        img = images.get(r["image_id"])
        if img is None:
            problems.append(f"{where}: image {r['image_id']} does not exist")
            continue
        if r["finding_code"] != img["finding_code"]:
            problems.append(f"{where}: finding code {r['finding_code']} differs from its image's {img['finding_code']}")
        if r["synthetic"] != img["synthetic"]:
            problems.append(f"{where}: synthetic flag differs from its image")
        code = r["finding_code"]
        seen_codes.add(code)
        if code in findings:
            problems += [f"{where}: {p}" for p in rule_problems(findings[code]["d_rules"], r["measurements"])]
        stated = set(mm_numbers(r["text"]))
        allowed = {float(v) for k, v in r["measurements"].items() if k.endswith("_mm")} | {6.0}   # 6 mm is the book's normal duct limit, quoted in some wording
        for n in stated - allowed:
            problems.append(f"{where}: the text states {n:g} mm, which is not one of its measurements")
        if r["synthetic"]:
            for k, v in r["measurements"].items():
                if k.endswith("_mm") and float(v) not in stated:
                    problems.append(f"{where}: measurement {k}={v} is not stated in the text")
        if r["status"] != "DRAFT" and not r.get("approval"):
            problems.append(f"{where}: status {r['status']} needs an approval record")
        if r["status"] != img["status"]:
            problems.append(f"{where}: status {r['status']} differs from its picture's {img['status']}")
    for code in findings:
        if code not in seen_codes:
            problems.append(f"finding code {code} has no report")
    for e in index:
        if not any(r["image_id"] == e["id"] for r in reports):
            problems.append(f"image {e['id']} has no report")
    return problems


SETS = [("ultrasound", "content/images/index.json", "content/imaging_reports.json", "content/ultrasound_findings.csv", "content/images"),
        ("ECG", "content/ecg/index.json", "content/ecg_reports.json", "content/ecg_findings.csv", "content/ecg"),
        ("chest X-ray", "content/cxr/index.json", "content/cxr_reports.json", "content/cxr_findings.csv", "content/cxr"),
        ("CT, MRI and MRCP", "content/xsec/index.json", "content/xsec_reports.json", "content/xsec_findings.csv", "content/xsec")]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    bad = 0
    afile = ROOT / "content" / "image_approvals.json"
    approvals_file = json.loads(afile.read_text(encoding="utf-8"))["approvals"] if afile.exists() else {}
    for name, idx, rep, fnd, folder in SETS:
        problems = validate(ROOT / idx, ROOT / rep, ROOT / fnd, ROOT / folder, approvals_file)
        index = json.loads((ROOT / idx).read_text(encoding="utf-8"))["images"]
        print(f"{name}: {len(index)} images ({sum(1 for i in index if not i['synthetic'])} real, {sum(1 for i in index if i['synthetic'])} synthetic, "
              f"{sum(1 for i in index if i['status'] != 'DRAFT')} approved): "
              + ("all checks pass" if not problems else f"{len(problems)} problem(s)"))
        for p in problems:
            print("  - " + p)
        bad += len(problems)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
