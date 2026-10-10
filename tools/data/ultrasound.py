"""Builds the ultrasound images, their matching reports and the index (ultrasound feature plan, ADR-0003).

    uv run --project tools --extra images tools/data/ultrasound.py candidates   # contact sheets of candidate real images to review by eye (written outside the repo)
    uv run --project tools --extra images tools/data/ultrasound.py build        # writes content/images/**, content/images/index.json, content/imaging_reports.json

Order of preference per finding code: a real licensed image (UIdataGB, CC BY 4.0, picked by the reviewed selection in ultrasound_real_selection.json), otherwise a labelled
synthetic image drawn in code (ultrasound_synth.py). Each image gets a report written from the SAME finding code (wording from the walkthrough, Part 6.3 and 6.4).
For a real image the report states only what its dataset class supports and says that measurements are not stated; for a synthetic image the numbers in the report are
the numbers drawn in the picture. Every image starts as DRAFT: it is shown in a session only after the clinician approves it (ADR-0003).
Real archives are expected in tools/data/cache/uidatagb/ (download links in the dataset card); the build is deterministic given the same inputs.
"""
from __future__ import annotations

import hashlib
import io
import json
import sys
import zipfile
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import ultrasound_synth as synth  # noqa: E402
import approvals  # noqa: E402

IMAGES = ROOT / "content" / "images"
CACHE = HERE / "cache" / "uidatagb"
SELECTION = HERE / "ultrasound_real_selection.json"
LICENCE = "CC BY 4.0"
ATTRIBUTION = ("Gallbladder Diseases Dataset (UIdataGB), Turki A., Obaid A.M., Bellaaj H. et al., Mendeley Data, V2, doi:10.17632/r6h24d2d3y.2, CC BY 4.0. "
               "Images downscaled and re-encoded without metadata.")
SYNTH_CODES = ["US-GB-00", "US-GB-08", "US-PV-00", "US-PV-01", "US-PV-02", "US-RN-00", "US-RN-01", "US-GB-03", "US-GB-04", "US-GB-07", "US-AP-01", "US-AP-02", "US-AP-03", "US-AP-04", "US-AP-05"]
# synthetic variants drawn to match a case's printed numbers (the others use random values inside the book's band)
FIXED = {("US-GB-03", 0): {"duct_mm": 9.0}, ("US-AP-01", 0): {"appendix_mm": 9.0, "appendicolith": False}}
CASES = {  # case -> (finding code, image variant note)
    "VAR-S1-A": "US-GB-01", "VAR-S1-E": "US-GB-02", "VAR-S1-PB": "US-GB-03", "VAR-S2-PC": "US-AP-01", "VAR-S2-D": "US-AP-04",
}
CASE_IMAGE = {"VAR-S1-PB": "us-gb-03-s0", "VAR-S2-PC": "us-ap-01-s0"}   # cases whose printed measurements the synthetic image reproduces exactly

REAL_TEXT = {
    "US-GB-01": ("Echogenic stones with posterior acoustic shadowing within the gallbladder. Wall thickness and bile duct diameter are not measured on this image.", "Gallstones."),
    "US-GB-02": ("Thickened gallbladder wall with surrounding fluid in keeping with acute inflammation. Measurements are not stated for this image.",
                 "Appearances of acute cholecystitis (inflamed gallbladder)."),
    "US-GB-05": ("Appearances of severe complicated inflammation of the gallbladder ({klass}). Measurements are not stated for this image.",
                 "Severe complicated inflammation of the gallbladder."),
    "US-GB-06": ("Gallbladder wall thickening or mass with appearances suspicious for malignancy. Measurements are not stated for this image.",
                 "Possible gallbladder cancer."),
}
SYNTH_TEXT = {
    "US-GB-08": ("Gallbladder wall mildly thickened at {wall_mm} mm. No gallstones and no pericholecystic fluid are seen. The common bile duct is normal at {duct_mm} mm (normal up to 6 mm). Appearances are equivocal.",
                 "Possible early cholecystitis: inconclusive."),
    "US-PV-00": ("Transabdominal pelvic scan through a filled bladder. The uterus is normal in size and echotexture ({uterus_mm} mm long axis). Both ovaries are normal. No free fluid and no adnexal mass.",
                 "Normal pelvic scan."),
    "US-PV-01": ("The uterus is empty: no intrauterine gestational sac. A {mass_mm} mm adnexal mass with a thick echogenic ring is seen, and there is free fluid in the pouch of Douglas.",
                 "Appearances suggest an ectopic pregnancy."),
    "US-PV-02": ("The ovary is enlarged ({ovary_mm} mm) with a swollen echogenic stroma and peripheral follicles. Doppler flow is reduced. No free fluid.",
                 "Enlarged ovary with reduced flow: suspected ovarian torsion."),
    "US-RN-00": ("Kidney, long axis ({kidney_mm} mm). Normal cortical thickness and a normal central echo complex. No hydronephrosis and no stone.", "Normal kidney."),
    "US-RN-01": ("Kidney, long axis ({kidney_mm} mm). Dilated renal pelvis and calyces (pelvis {pelvis_mm} mm) with an echogenic stone and posterior acoustic shadow at the pelvi-ureteric junction.",
                 "Hydronephrosis with an obstructing stone."),
    "US-GB-00": ("Gallbladder is normally distended with a thin wall ({wall_mm} mm), no stones, no sludge and no pericholecystic fluid. The common bile duct is normal at {duct_mm} mm (normal up to 6 mm).",
                 "Normal gallbladder and bile duct."),
    "US-GB-03": ("Gallbladder contains a stone with posterior acoustic shadowing; wall {wall_mm} mm. The common bile duct is dilated at {duct_mm} mm (normal up to 6 mm).",
                 "Bile duct wider than 6 mm: possible blockage of the duct."),
    "US-GB-04": ("Echogenic stone with posterior acoustic shadowing within the common bile duct (duct {duct_mm} mm). The gallbladder also contains stones.", "Stone seen in the bile duct."),
    "US-GB-07": ("Gallbladder contains low-level layering echoes (sludge) without stones or acoustic shadowing; wall {wall_mm} mm.", "No stones, only sludge."),
    "US-AP-01": ("Appendix seen in cross-section, {appendix_mm} mm, non-compressible, with surrounding inflamed echogenic fat{lith}. No perforation or collection.",
                 "Positive, simple (uncomplicated) appendicitis."),
    "US-AP-02": ("Swollen appendix, {appendix_mm} mm, with an adjacent complex collection of {abscess_mm} mm containing gas and debris, and surrounding inflammatory change.",
                 "Positive, complicated appendicitis (abscess)."),
    "US-AP-03": ("Appendix seen, {appendix_mm} mm, compressible, without surrounding inflammation. Separate anechoic ovarian cyst, {cyst_mm} mm.",
                 "Negative for appendicitis; other finding (ovarian cyst)."),
    "US-AP-04": ("Appendix not seen: bowel gas obscures the right iliac fossa. No other signs.", "Non-diagnostic (equivocal): this is not the same as a negative scan."),
    "US-AP-05": ("Appendix seen, {appendix_mm} mm, compressible, thin wall, without surrounding inflammation. No other abnormality.", "Normal appendix; appendicitis unlikely."),
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def candidates() -> int:
    """Contact sheets of the first evenly spaced images of each real class, so they can be looked at (for burned-in text) before choosing."""
    out = Path.home() / "ps-scrub" / "data" / "uidatagb" / "sheets"
    out.mkdir(parents=True, exist_ok=True)
    for zp in sorted(CACHE.glob("*.zip")):
        z = zipfile.ZipFile(zp)
        names = sorted(n for n in z.namelist() if n.lower().endswith((".jpg", ".jpeg", ".png")))
        picks = names[:: max(1, len(names) // 8)][:8]
        sheet = Image.new("L", (4 * 320, 2 * 240))
        for i, n in enumerate(picks):
            im = Image.open(io.BytesIO(z.read(n))).convert("L").resize((320, 240))
            sheet.paste(im, ((i % 4) * 320, (i // 4) * 240))
        sheet.save(out / f"{zp.stem}.png")
        print(zp.name, len(names), "images;", ", ".join(Path(n).name for n in picks))
    print("sheets in", out)
    return 0


def downscale_jpeg(raw: bytes) -> bytes:
    im = Image.open(io.BytesIO(raw)).convert("L")
    im = im.resize((640, 480), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=85, optimize=True)   # re-encoded from pixels: no EXIF, no ICC profile
    return buf.getvalue()


def build() -> int:
    for sub in IMAGES.glob("US-*"):
        for f in sub.glob("*"):
            f.unlink()
    entries, reports = [], []

    # ---- synthetic images
    for code in SYNTH_CODES:
        folder = IMAGES / code
        folder.mkdir(parents=True, exist_ok=True)
        for variant in range(4):
            img, info = synth.render(code, variant, FIXED.get((code, variant)))
            iid = f"{code.lower()}-s{variant}"
            path = folder / f"{iid}.png"
            img.save(path, "PNG")
            raw = path.read_bytes()
            strip = sha(synth.mark_strip(Image.open(path)))
            entries.append({"id": iid, "file": f"{code}/{iid}.png", "sha256": sha(raw), "bytes": len(raw), "width": img.width, "height": img.height, "finding_code": code,
                            "synthetic": True, "source": None, "licence": "ProcSense generated (CC0 for our own use)", "attribution": "",
                            "generator": {"name": "ultrasound_synth.py", "seed": info["seed"], "variant": variant, "kind": "stylised procedural illustration, not a trained generative model"},
                            "mark": {"text": synth.MARK, "strip_sha256": strip, "strip_height": synth.MARK_HEIGHT}, "status": "DRAFT", "cases": [c for c, i in CASE_IMAGE.items() if i == iid],
                            "review_required": True})
            m = info["measurements"]
            body, impression = SYNTH_TEXT[code]
            text = body.format(lith=", containing an echogenic appendicolith" if m.get("appendicolith") == "yes" else "", **{k: (f"{v:g}" if isinstance(v, float) else v) for k, v in m.items()}) + " Impression: " + impression
            reports.append({"id": f"rep-{iid}", "finding_code": code, "image_id": iid, "text": text, "measurements": m, "synthetic": True, "status": "DRAFT",
                            "wording": "Walkthrough Part 6.3/6.4 category wording; numbers are those drawn in the image"})

    # ---- real images (reviewed selection)
    sel = json.loads(SELECTION.read_text(encoding="utf-8")) if SELECTION.exists() else {"images": []}
    opened: dict[str, zipfile.ZipFile] = {}
    for item in sel["images"]:
        zp = CACHE / item["archive"]
        if not zp.exists():
            print(f"missing archive {zp.name}: skipped {item['id']}", file=sys.stderr)
            continue
        z = opened.setdefault(item["archive"], zipfile.ZipFile(zp))
        data = downscale_jpeg(z.read(item["member"]))
        folder = IMAGES / item["finding_code"]
        folder.mkdir(parents=True, exist_ok=True)
        path = folder / f"{item['id']}.jpg"
        path.write_bytes(data)
        entries.append({"id": item["id"], "file": f"{item['finding_code']}/{item['id']}.jpg", "sha256": sha(data), "bytes": len(data), "width": 640, "height": 480,
                        "finding_code": item["finding_code"], "synthetic": False,
                        "source": {"dataset": "UIdataGB", "class": item["class"], "archive": item["archive"], "member": item["member"], "doi": "10.17632/r6h24d2d3y.2",
                                   "url": "https://data.mendeley.com/datasets/r6h24d2d3y/2"},
                        "licence": LICENCE, "attribution": ATTRIBUTION, "generator": None, "mark": None, "status": "DRAFT",
                        "cases": [c for c, code in CASES.items() if code == item["finding_code"] and c not in CASE_IMAGE], "review_required": True})
        body, impression = REAL_TEXT[item["finding_code"]]
        reports.append({"id": f"rep-{item['id']}", "finding_code": item["finding_code"], "image_id": item["id"],
                        "text": body.format(klass=item["class"]) + " Impression: " + impression, "measurements": {}, "synthetic": False, "status": "DRAFT",
                        "wording": "Walkthrough Part 6.3 category wording; limited to what the dataset class supports; the clinician confirms the match"})

    approvals.apply(entries, reports)
    entries.sort(key=lambda e: e["id"])
    reports.sort(key=lambda r: r["id"])
    (IMAGES / "index.json").write_text(json.dumps({"schema": 1, "images": entries}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    (ROOT / "content" / "imaging_reports.json").write_text(json.dumps({"schema": 1, "reports": reports}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    by = {"real": sum(1 for e in entries if not e["synthetic"]), "synthetic": sum(1 for e in entries if e["synthetic"])}
    print(f"{len(entries)} images ({by['real']} real, {by['synthetic']} synthetic), {len(reports)} reports")
    return 0


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    sys.exit({"candidates": candidates, "build": build}.get(cmd, lambda: print(__doc__) or 1)())
