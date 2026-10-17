"""Machine-readable tags for the imaging library (p1-dataset-analysis-s04): finding tags per finding code, and per picture its view, technical quality grade, real or synthetic flag, dataset and licence.

    uv run --project tools tools/content/imaging_tags.py            # write content/imaging/finding_tags.json, content/imaging/picture_tags.csv and docs/research/imaging_gaps.md
    uv run --project tools tools/content/imaging_tags.py --check    # fail if they are out of date, a picture or finding has no tags, or a tag is not in the vocabulary (CI)

Why: live generation picks a picture by the tags of the finding it needs; a picture without tags cannot be matched safely.
The tags are authored here, one list per finding code, from a closed vocabulary; every tag carries support words and a finding's tags must be supported by the words of its own finding text, its plain
meaning or its test (the check fails otherwise), so a tag is never invented. The quality grade is TECHNICAL (size, exposure, contrast, sharpness measured on the file), not diagnostic quality; clinical approval
of a picture stays where it was (content/image_approvals.json, ADR-0004). "view" is only given where the dataset or the generator states it.
"""
from __future__ import annotations

import csv
import json
import re
import statistics
import sys
from pathlib import Path

from PIL import Image, ImageFilter, ImageStat

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
TAGS_OUT = CONTENT / "imaging" / "finding_tags.json"
PICTURES_OUT = CONTENT / "imaging" / "picture_tags.csv"
GAPS_OUT = ROOT / "docs" / "research" / "imaging_gaps.md"
SETS = {"ultrasound": "images", "ecg": "ecg", "cxr": "cxr", "xsec": "xsec"}
MODALITY = {"ultrasound": "ultrasound", "ecg": "ecg", "cxr": "xray"}

# tag -> (what it means, words in the finding text, plain text or test name that support it)
VOCABULARY: dict[str, tuple[str, str]] = {
    "ultrasound": ("ultrasound picture", r"ultrasound"), "ecg": ("12-lead-derived rhythm strip", r"ecg"), "xray": ("chest film", r"chest_xray"),
    "ct": ("computed tomography slice", r"^ct_|CT:"), "mri": ("magnetic resonance slice", r"^mri_|MRI:"), "mrcp": ("MR cholangiopancreatography", r"^mrcp|MRCP"),
    "gallbladder": ("gallbladder and bile ducts", r"mrcp|gallbladder|bile|duct|gallstone|cholecyst"), "appendix": ("appendix and right iliac fossa", r"appendi|iliac|caecal|caecum|diverticul"),
    "pelvis": ("pelvis", r"pelvis"), "kidney": ("kidney", r"kidney"), "chest": ("chest", r"chest|lung|pleural|pneumo|diaphragm|atelectasis|cardiomegaly|consolidation|oedema"),
    "heart": ("heart rhythm or shape", r"ecg|rhythm|tachycardia|bradycardia|fibrillation|block|ST elevation|T waves|heart|potassium|supraventricular"),
    "abdomen": ("mid abdomen", r"abdom|bowel|ct_abdomen"),
    "normal": ("no abnormality", r"no abnormality|acceptable|normal|no finding|unlikely|clean|no appendicitis|no acute|not appendicitis|no obstruction|no problem"),
    "non_diagnostic": ("the picture does not settle the question", r"not seen|less certain|sludge|still don't know|not the same as negative|no abnormality at this level"),
    "no_wording": ("the book gives no wording for this finding", r"finding wording not in the book"),
    "gallstones": ("stones in the gallbladder", r"stone"), "duct_stone": ("stone in the bile duct", r"stone seen in the bile duct|common bile duct|filling defect"),
    "duct_dilated": ("wide bile duct", r"bile duct wider|dilated duct|blockage of the duct"), "cholecystitis": ("inflamed gallbladder", r"inflam|cholecystitis|thicker than 4 mm"),
    "wall_thickening": ("thick gallbladder wall", r"thicker|thick"), "gas_in_wall": ("gas in a wall or collection", r"gas"), "abscess": ("pus collection", r"abscess|pus"),
    "perforation": ("a hole or gas outside the bowel or appendix", r"hole|burst|perforation|free air|outside the bowel|pneumoperitoneum"), "mass": ("mass, polyp or tumour", r"polyp|mass|tumour|cancer"),
    "sludge": ("sludge", r"sludge"), "appendicitis": ("inflamed appendix", r"appendicitis|inflamed appendix|swollen"), "appendix_not_seen": ("appendix not visualised", r"not seen"),
    "mimic": ("another condition that can look like the one asked about", r"mimic|not appendicitis|diverticulitis|caecal mass|other diagnosis"), "complicated": ("complicated disease", r"complicated|burst|abscess|gas-containing|perforation"),
    "bowel_obstruction": ("blocked bowel", r"obstruction|dilated fluid"), "free_air": ("gas under the diaphragm or in the peritoneum", r"free air|free gas|pneumoperitoneum|gas outside"),
    "written_result": ("a test that returns words and no picture", r"pregnancy|dipstick|sample_appearance"), "negative_result": ("a negative result", r"negative"), "positive_result": ("a positive result", r"positive"),
    "urine_infection": ("urine infection pattern", r"infection"), "glucose_ketones": ("glucose and ketones in the urine", r"glucose and ketones"), "blood_in_urine": ("blood in the urine", r"urine dipstick: blood"),
    "haemolysed": ("haemolysed sample", r"haemolysed"), "lipaemic": ("lipaemic sample", r"lipaemic"),
    "tachycardia": ("fast heart rate", r"tachycardia|fast"), "bradycardia": ("slow heart rate", r"bradycardia|slow"), "irregular_rhythm": ("irregular rhythm", r"fibrillation|irregular"),
    "atrial_fibrillation": ("atrial fibrillation", r"fibrillation|AF"), "heart_block": ("heart block", r"heart block|out of step"), "supraventricular": ("supraventricular rhythm", r"supraventricular"),
    "st_elevation": ("ST elevation", r"ST elevation|heart attack"), "hyperkalaemia": ("high potassium pattern", r"hyperkalaemia|high potassium"), "hypokalaemia": ("low potassium pattern", r"hypokalaemia|low potassium"),
    "consolidation": ("consolidation", r"consolidation|airspace"), "effusion": ("pleural effusion", r"effusion|fluid around the lung"), "pneumothorax": ("air around the lung", r"pneumothorax|air around"),
    "cardiomegaly": ("enlarged heart", r"cardiomegaly|enlarged heart"), "pulmonary_oedema": ("fluid in the lungs", r"oedema|fluid in the lungs"), "atelectasis": ("collapsed lung", r"atelectasis|collapsed"),
}

# finding code -> tags. Region and modality come from the test; the rest names what the finding says.
FINDING_TAGS: dict[str, list[str]] = {
    "US-GB-00": ["normal"], "US-GB-01": ["gallstones"], "US-GB-02": ["gallstones", "cholecystitis", "wall_thickening"], "US-GB-03": ["duct_dilated"], "US-GB-04": ["duct_stone", "gallstones"],
    "US-GB-05": ["complicated", "gas_in_wall", "abscess", "perforation"], "US-GB-06": ["mass"], "US-GB-07": ["sludge", "non_diagnostic"], "US-GB-08": ["non_diagnostic", "no_wording"],
    "US-AP-01": ["appendicitis"], "US-AP-02": ["complicated", "perforation", "abscess"], "US-AP-03": ["mimic", "normal"], "US-AP-04": ["appendix_not_seen", "non_diagnostic"], "US-AP-05": ["normal"],
    "US-PV-00": ["normal", "non_diagnostic"], "US-PV-01": ["no_wording"], "US-PV-02": ["no_wording"], "US-RN-00": ["normal", "non_diagnostic"], "US-RN-01": ["no_wording"],
    "ECG-01": ["normal"], "ECG-02": ["tachycardia"], "ECG-03": ["atrial_fibrillation", "irregular_rhythm"], "ECG-04": ["bradycardia"], "ECG-05": ["heart_block", "bradycardia"], "ECG-06": ["supraventricular", "tachycardia"],
    "ECG-07": ["st_elevation"], "ECG-08": ["hyperkalaemia"], "ECG-09": ["hypokalaemia"], "ECG-10": ["atrial_fibrillation", "irregular_rhythm", "tachycardia"],
    "CXR-01": ["normal"], "CXR-02": ["consolidation"], "CXR-03": ["effusion"], "CXR-04": ["pneumothorax"], "CXR-05": ["cardiomegaly"], "CXR-06": ["pulmonary_oedema"], "CXR-07": ["atelectasis"], "CXR-08": ["free_air", "perforation"],
    "CT-AB-00": ["normal"], "CT-AB-01": ["bowel_obstruction"], "CT-AB-02": ["free_air", "perforation"], "CT-AP-00": ["normal"], "CT-AP-01": ["appendicitis"], "CT-AP-02": ["appendicitis", "abscess", "complicated"],
    "CT-AP-03": ["mass", "mimic"], "CT-AP-04": ["mimic"], "CT-GB-00": ["normal"], "CT-GB-01": ["complicated", "gas_in_wall", "wall_thickening"], "MR-AP-00": ["normal"], "MR-AP-01": ["appendicitis"],
    "MRCP-00": ["normal"], "MRCP-01": ["duct_stone"],
    "PREG-00": ["negative_result"], "PREG-01": ["positive_result"], "DIP-00": ["normal"], "DIP-01": ["urine_infection"], "DIP-02": ["glucose_ketones"], "DIP-03": ["blood_in_urine"],
    "TUBE-00": ["normal"], "TUBE-01": ["haemolysed"], "TUBE-02": ["lipaemic"],
}
TEST_TAGS = {
    "ultrasound_gallbladder": ["ultrasound", "gallbladder"], "ultrasound_appendix": ["ultrasound", "appendix"], "ultrasound_pelvis": ["ultrasound", "pelvis"], "ultrasound_kidney": ["ultrasound", "kidney"],
    "ecg": ["ecg", "heart"], "chest_xray": ["xray", "chest"], "ct_abdomen_midlevel": ["ct", "abdomen"], "ct_appendix_level": ["ct", "appendix"], "ct_gallbladder_level": ["ct", "gallbladder"],
    "mri_appendix_level": ["mri", "appendix"], "mrcp": ["mrcp", "gallbladder"],
    "pregnancy_test": ["written_result"], "urine_dipstick": ["written_result"], "blood_sample_appearance": ["written_result"],
}
# the view is only stated where the dataset or the generator states it
VIEWS = {"ecg": "rhythm strip, lead II, 10 s", "cxr": "frontal (ChestX-ray14 holds frontal films only)", "CT": "axial slice", "MRI": "axial slice", "MRCP": "coronal thick slab (stated by the generator)"}

# technical quality limits (documented in the report): hard limits fail a picture, the sharpness share grades the rest within its modality
MIN_SIDE = {"ultrasound": 400, "ecg": 200, "cxr": 400, "xsec": 300}      # cropped CT and MRI slices are about 350 pixels high
EXPOSURE = {"ultrasound": (0.04, 0.90), "ecg": (0.50, 0.995), "cxr": (0.04, 0.90), "xsec": (0.04, 0.90)}       # mean brightness as a share of white: an almost black picture is poor (an ECG strip is dark ink on white paper, so it is bright by design)
MIN_CONTRAST = 0.03           # standard deviation as a share of white


def read_csv(path: Path) -> list[dict]:
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def measure(path: Path) -> dict:
    im = Image.open(path).convert("L")
    stat = ImageStat.Stat(im)
    edges = ImageStat.Stat(im.filter(ImageFilter.FIND_EDGES))
    return {"width": im.width, "height": im.height, "exposure": stat.mean[0] / 255, "contrast": stat.stddev[0] / 255, "sharpness": edges.var[0]}


def grade(m: dict, kind: str, sharp_floor: float) -> str:
    if min(m["width"], m["height"]) < MIN_SIDE[kind] and kind != "ecg":
        return "C"
    if kind == "ecg" and m["width"] < MIN_SIDE[kind]:
        return "C"
    if not EXPOSURE[kind][0] <= m["exposure"] <= EXPOSURE[kind][1] or m["contrast"] < MIN_CONTRAST:
        return "C"
    return "A" if m["sharpness"] >= sharp_floor else "B"


def finding_texts() -> dict[str, dict]:
    lib = {r["finding_code"]: r for r in read_csv(CONTENT / "imaging_library.csv")}
    meta = {f["code"]: f for f in json.loads((CONTENT / "imaging" / "imaging.json").read_text(encoding="utf-8"))["findings"]}
    out = {}
    for code, r in lib.items():
        out[code] = {"test": r["test"], "role": r["role"], "finding": r["finding"], "plain": meta.get(code, {}).get("plain", ""), "category": meta.get(code, {}).get("category", ""), "pictures": int(r["pictures"])}
    return out


def build() -> tuple[dict, list[dict], str]:
    texts = finding_texts()
    findings = {}
    for code, t in sorted(texts.items()):
        tags = list(dict.fromkeys(TEST_TAGS.get(t["test"], []) + FINDING_TAGS.get(code, [])))
        findings[code] = {"test": t["test"], "role": t["role"], "category": t["category"], "tags": tags, "has_pictures": t["pictures"] > 0}
    vocab = {k: v[0] for k, v in sorted(VOCABULARY.items())}
    doc = {"note": "Generated by tools/content/imaging_tags.py (p1-dataset-analysis-s04). Tags are a closed vocabulary; each finding's tags are supported by its own wording.", "vocabulary": vocab, "findings": findings}

    rows = []
    for kind, folder in SETS.items():
        path = CONTENT / folder / "index.json"
        pics = json.loads(path.read_text(encoding="utf-8"))["images"]
        measured = {p["id"]: measure(CONTENT / folder / p["file"]) for p in pics}
        floor = statistics.median(m["sharpness"] for m in measured.values())     # a picture is grade A when at least as sharp as the median of its own set
        for p in pics:
            m = measured[p["id"]]
            src = p.get("source") or {}
            view = ""
            if kind in ("ecg", "cxr"):
                view = VIEWS[kind]
            elif kind == "xsec":
                view = VIEWS.get(p.get("modality", ""), "")
            rows.append({"id": p["id"], "finding_code": p["finding_code"], "set": kind, "modality": p.get("modality") or MODALITY.get(kind, kind), "view": view,
                         "tags": " ".join(findings[p["finding_code"]]["tags"]), "quality_grade": grade(m, kind, floor), "width": m["width"], "height": m["height"],
                         "exposure": f"{m['exposure']:.3f}", "contrast": f"{m['contrast']:.3f}", "sharpness": f"{m['sharpness']:.1f}", "real": "false" if p["synthetic"] else "true",
                         "dataset": (src.get("dataset") or ("ProcSense generated" if p["synthetic"] else "")), "licence": p.get("licence", ""), "status": p.get("status", "")})
    rows.sort(key=lambda r: r["id"])
    return doc, rows, gaps_report(texts, rows, findings)


def gaps_report(texts: dict, rows: list[dict], findings: dict) -> str:
    by_code: dict[str, list[dict]] = {}
    for r in rows:
        by_code.setdefault(r["finding_code"], []).append(r)
    L = ["# Imaging library: what it cannot show", "", "Generated by `tools/content/imaging_tags.py` (p1-dataset-analysis-s04); do not edit. The quality grade is technical (size, exposure, contrast, sharpness of the file), not diagnostic.", ""]
    grades = {g: sum(1 for r in rows if r["quality_grade"] == g) for g in "ABC"}
    L += [f"{len(rows)} pictures: grade A {grades['A']}, B {grades['B']}, C {grades['C']}; {sum(1 for r in rows if r['real'] == 'true')} real, {sum(1 for r in rows if r['real'] == 'false')} synthetic.", ""]
    L += ["## Findings with synthetic pictures only", "", "| Finding | Wording | Pictures |", "|---|---|---:|"]
    for code in sorted(by_code):
        if all(r["real"] == "false" for r in by_code[code]):
            L.append(f"| {code} | {texts[code]['plain'] or texts[code]['finding']} | {len(by_code[code])} |")
    L += ["", "These are labelled synthetic on the picture; a generated case may use them in practice mode only. A real picture would need a dataset that holds the finding (E64, the offline research track, is not used at runtime).", "",
          "## Findings whose wording the book does not give", ""]
    for code, t in sorted(texts.items()):
        if t["finding"].startswith("(finding wording not in the book)"):
            L.append(f"- `{code}` ({t['test']}, {t['role']}): plain meaning \"{t['plain']}\" (category {t['category']}); the book states no finding, so a case that uses it needs its finding authored first.")
    L += ["", "## Tests that return a written result only (no picture)", ""]
    for t in sorted({t["test"] for t in texts.values() if t["pictures"] == 0}):
        L.append(f"- `{t}`: " + ", ".join(sorted(c for c, v in texts.items() if v["test"] == t)))
    L += ["", "## Pictures graded C (technical)", ""]
    bad = [r for r in rows if r["quality_grade"] == "C"]
    L += [f"- `{r['id']}` ({r['finding_code']}): {r['width']}x{r['height']}, exposure {r['exposure']}, contrast {r['contrast']}" for r in bad] or ["None."]
    L += ["", "## Findings with fewer than 4 pictures", ""]
    few = [c for c in sorted(by_code) if len(by_code[c]) < 4]
    L += [f"- `{c}`: {len(by_code[c])}" for c in few] or ["None."]
    return "\n".join(L) + "\n"


def render_csv(rows: list[dict]) -> str:
    import io
    out = io.StringIO()
    w = csv.DictWriter(out, fieldnames=list(rows[0].keys()), lineterminator="\n")
    w.writeheader()
    w.writerows(rows)
    return out.getvalue()


def problems() -> list[str]:
    out = []
    texts = finding_texts()
    doc, rows, gaps = build()
    for code, t in texts.items():
        if code not in FINDING_TAGS:
            out.append(f"{code}: no tags")
            continue
        text = " ".join([t["finding"], t["plain"], t["test"]])
        for tag in FINDING_TAGS[code]:
            if tag not in VOCABULARY:
                out.append(f"{code}: tag '{tag}' is not in the vocabulary")
            elif not re.search(VOCABULARY[tag][1], text, re.IGNORECASE):
                out.append(f"{code}: tag '{tag}' is not supported by its wording ('{t['finding']}' / '{t['plain']}')")
    for t, tags in TEST_TAGS.items():
        for tag in tags:
            if tag not in VOCABULARY or not re.search(VOCABULARY[tag][1], t, re.IGNORECASE):
                out.append(f"test {t}: tag '{tag}' is not in the vocabulary or not supported by the test name")
    for code in FINDING_TAGS:
        if code not in texts:
            out.append(f"{code}: tags for a finding that is not in the library")
    n_pictures = sum(len(json.loads((CONTENT / f / "index.json").read_text(encoding="utf-8"))["images"]) for f in SETS.values())
    if len(rows) != n_pictures:
        out.append(f"{len(rows)} picture rows for {n_pictures} pictures")
    for r in rows:
        if not r["tags"] or not r["quality_grade"] or not r["licence"]:
            out.append(f"{r['id']}: missing tags, quality grade or licence")
    for path, want in ((TAGS_OUT, json.dumps(doc, indent=1, sort_keys=True) + "\n"), (PICTURES_OUT, render_csv(rows)), (GAPS_OUT, gaps)):
        if not path.exists() or path.read_text(encoding="utf-8").replace("\r\n", "\n") != want:
            out.append(f"{path.relative_to(ROOT).as_posix()} is out of date: run `uv run --project tools tools/content/imaging_tags.py`")
    return out


def main(argv: list[str]) -> int:
    if "--check" in argv:
        p = problems()
        for x in p:
            print("FAIL  " + x)
        if not p:
            print("PASS  every finding and picture is tagged, every tag is supported by its wording, and the tag files are current")
        return 1 if p else 0
    doc, rows, gaps = build()
    TAGS_OUT.write_text(json.dumps(doc, indent=1, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    PICTURES_OUT.write_text(render_csv(rows), encoding="utf-8", newline="\n")
    GAPS_OUT.write_text(gaps, encoding="utf-8", newline="\n")
    print(f"wrote {len(doc['findings'])} finding tag lists, {len(rows)} picture rows and {GAPS_OUT.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
