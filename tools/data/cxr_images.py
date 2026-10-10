"""Builds chest X-ray pictures with matching reports from the NIH ChestX-ray14 collection into content/cxr/ (same rules as ultrasound and ECG, ADR-0003).

    uv run --project tools --extra images tools/data/cxr_images.py candidates   # contact sheets of single-label candidates, to look at before choosing
    uv run --project tools --extra images tools/data/cxr_images.py build        # writes content/cxr/**, content/cxr/index.json, content/cxr_reports.json

Source: NIH Clinical Center ChestX-ray14 (Wang et al., CVPR 2017; NIH download site https://nihcc.app.box.com/v/ChestXray-NIHCC). Only ONE shard of the Hugging Face
parquet copy (BahaaEldin0/NIH-Chest-Xray-14, test-00000-of-00010, 450 MB, about 1,100 images) is needed, not the 45 GB: it is cached in tools/data/cache/nih/ (git-ignored).
The copy has no licence of its own; the NIH terms (as quoted by the Google Cloud and RSNA documentation, the NIH page itself is script-driven and could not be read) are: no
restrictions on use, but link to the NIH site, cite the CVPR 2017 paper and acknowledge the NIH Clinical Center. The dataset has labels only (text-mined from reports, not
clinician-checked) and NO radiology reports, so each report here says only what the label supports. Patient id, age and sex in the source are NOT copied.
Only images with exactly one label are used. Pictures are re-encoded from pixels (no metadata) at 640 x 640. All DRAFT until a clinician approves them.
"""
from __future__ import annotations

import hashlib
import io
import json
import sys
from pathlib import Path

import numpy as np
import pyarrow.parquet as pq
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import approvals  # noqa: E402
SHARDS = sorted((HERE / "cache" / "nih").glob("test-0000*.parquet"))
SELECTION = HERE / "cxr_real_selection.json"
OUT = ROOT / "content" / "cxr"
CODES = {"No Finding": "CXR-01", "Consolidation": "CXR-02", "Effusion": "CXR-03", "Pneumothorax": "CXR-04", "Cardiomegaly": "CXR-05", "Edema": "CXR-06", "Atelectasis": "CXR-07"}
WORDS = {
    "CXR-01": ("No acute abnormality is labelled on this film.", "No abnormality labelled."),
    "CXR-02": ("Airspace opacity labelled as consolidation. The extent and side are not stated for this image.", "Consolidation (possible pneumonia or aspiration; clinical correlation needed)."),
    "CXR-03": ("Pleural effusion labelled on this film. The size and side are not stated for this image.", "Pleural effusion."),
    "CXR-04": ("Pneumothorax labelled on this film. The size and side are not stated for this image.", "Pneumothorax."),
    "CXR-05": ("Enlarged cardiac silhouette labelled (cardiomegaly). No measurement is stated for this image.", "Cardiomegaly."),
    "CXR-06": ("Features labelled as pulmonary oedema. The extent is not stated for this image.", "Pulmonary oedema (fluid in the lungs)."),
    "CXR-07": ("Atelectasis (partial lung collapse) labelled on this film. The side and extent are not stated for this image.", "Atelectasis."),
}
MARK = "SIMULATION: synthetic image, not a real scan"
MARK_HEIGHT = 30
FONTS = ["C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]
ATTRIBUTION = ("NIH Clinical Center chest X-ray database (ChestX-ray14), https://nihcc.app.box.com/v/ChestXray-NIHCC; Wang X., Peng Y., Lu L., Lu Z., Bagheri M., Summers R., "
               "ChestX-ray8, IEEE CVPR 2017, pp. 3462-3471. NIH Clinical Center is the data provider. Image resized and re-encoded without metadata.")


def rows(columns: list[str]):
    """Every row of every cached shard as ((shard name, row number), row)."""
    for shard in SHARDS:
        f = pq.ParquetFile(shard)
        n = 0
        for g in range(f.num_row_groups):
            for r in f.read_row_group(g, columns=columns).to_pylist():
                yield (shard.stem, n), r
                n += 1


def single_label() -> dict[str, list[tuple]]:
    out: dict[str, list[tuple]] = {}
    for key, r in rows(["label", "View Position"]):
        labels = r["label"]
        if r["View Position"] != "PA":
            continue
        if len(labels) == 1 and labels[0] in CODES:
            out.setdefault(labels[0], []).append(key)
        elif "Edema" in labels and set(labels) <= {"Edema", "Infiltration", "Effusion", "Cardiomegaly"}:   # oedema rarely stands alone: allow its usual companions
            out.setdefault("Edema", []).append(key)
    return out


def jpeg(raw: bytes) -> bytes:
    im = Image.open(io.BytesIO(raw)).convert("L").resize((640, 640), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=85, optimize=True)
    return buf.getvalue()


def candidates() -> int:
    pool = single_label()
    want = {label: ks[:: max(1, len(ks) // 12)][:12] for label, ks in pool.items()}
    needed = {k: label for label, ks in want.items() for k in ks}
    sheets = {label: Image.new("L", (4 * 320, 3 * 320)) for label in want}
    slot: dict[str, int] = {}
    ids: dict[str, list] = {label: [] for label in want}
    for key, r in rows(["image"]):
        if key in needed:
            label = needed[key]
            k = slot.get(label, 0)
            slot[label] = k + 1
            sheets[label].paste(Image.open(io.BytesIO(r["image"]["bytes"])).convert("L").resize((320, 320)), ((k % 4) * 320, (k // 4) * 320))
            ids[label].append(key)
    out = Path.home() / "ps-scrub" / "data" / "cxr"
    out.mkdir(parents=True, exist_ok=True)
    for label, sh in sheets.items():
        sh.save(out / f"{CODES[label]}.png")
        print(CODES[label], label, f"({len(pool[label])} single-label PA images)", "keys:", ids[label])
    print("sheets in", out)
    return 0


def free_air(gray: np.ndarray, rng) -> np.ndarray:
    """Paint a crescent of gas under the right hemidiaphragm (the left side of the picture) into a real chest film.
    The dome is found as the first strong dark-to-bright step going down each column, fitted with a smooth arc; the crescent takes its texture from the lung above it,
    is slightly darker than lung, and ends in a thin bright line (the top of the liver below the gas)."""
    h, w = gray.shape
    smooth = np.asarray(Image.fromarray(gray).filter(ImageFilter.GaussianBlur(3))).astype(np.float32)
    xs = np.arange(int(w * 0.17), int(w * 0.42))
    found = []
    for x in xs:
        col = smooth[int(h * 0.52):int(h * 0.84), x]
        g = np.diff(col)
        idx = int(np.argmax(g > 0.55 * g.max())) if g.max() > 0 else int(np.argmax(g))
        found.append(int(h * 0.52) + idx)
    found = np.asarray(found, np.float32)
    med = np.convolve(np.pad(found, 10, mode="edge"), np.ones(21) / 21, mode="valid")
    coef = np.polyfit(xs, med, 2)
    ys = np.polyval(coef, xs)
    out = gray.astype(np.float32).copy()
    thick = 20 + 8 * rng.random()
    lung_shift = int(thick + 40)
    n = len(xs)
    yy = np.arange(h)
    for i, x in enumerate(xs):
        t = thick * np.sin(np.pi * (i + 1) / (n + 1)) ** 0.6
        top, bottom = ys[i] + 1.0, ys[i] + 1.0 + t
        feather = np.clip(np.minimum(yy - top, bottom - yy) / 3.0, 0, 1)            # soft top and bottom edges
        src = np.clip(yy - lung_shift, 0, h - 1)
        lung = out[src, x] * 0.9 + rng.normal(0, 2, h)                              # lung-like texture, slightly darker than lung
        line = np.exp(-((yy - bottom) ** 2) / 2.0) * 28.0                           # the liver edge under the gas
        out[:, x] = out[:, x] * (1 - feather) + lung * feather + line
    return np.asarray(Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)))


def font(size: int):
    for f in FONTS:
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            continue
    return ImageFont.load_default()


def add_mark(im: Image.Image) -> Image.Image:
    out = Image.new("L", (im.width, im.height + MARK_HEIGHT), 0)
    out.paste(im, (0, 0))
    d = ImageDraw.Draw(out)
    f = font(17)
    d.text(((out.width - d.textlength(MARK, font=f)) / 2, im.height + 4), MARK, fill=235, font=f)
    return out


def build() -> int:
    sel = json.loads(SELECTION.read_text(encoding="utf-8"))
    by_key = {(s["shard"], s["row"]): s for s in sel["images"]}
    base_keys = {(s["shard"], s["row"]): s for s in sel["free_air_bases"]}
    for f in OUT.glob("CXR-*/*"):
        f.unlink()
    entries, reports = [], []
    for key, r in rows(["image", "label"]):
        s = by_key.get(key)
        b = base_keys.get(key)
        if not s and not b:
            continue
        raw_bytes = r["image"]["bytes"]
        if s:
            assert s["label"] in r["label"], (key, r["label"])
            code = CODES[s["label"]]
            data = jpeg(raw_bytes)
            folder = OUT / code
            folder.mkdir(parents=True, exist_ok=True)
            (folder / f"{s['id']}.jpg").write_bytes(data)
            entries.append({"id": s["id"], "file": f"{code}/{s['id']}.jpg", "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data), "width": 640, "height": 640,
                            "finding_code": code, "synthetic": False,
                            "source": {"dataset": "NIH ChestX-ray14", "class": s["label"], "shard": f"{key[0]}-of-00010", "row": key[1], "url": "https://nihcc.app.box.com/v/ChestXray-NIHCC"},
                            "licence": "NIH Clinical Center terms: no restrictions on use; attribution required", "attribution": ATTRIBUTION, "generator": None, "mark": None,
                            "status": "DRAFT", "cases": [], "review_required": True})
            body, impression = WORDS[code]
            reports.append({"id": f"rep-{s['id']}", "finding_code": code, "image_id": s["id"], "text": f"Chest X-ray, PA. {body} Impression: {impression}",
                            "measurements": {"label": s["label"].lower().replace(" ", "_")}, "synthetic": False, "status": "DRAFT",
                            "wording": "Label from the dataset (text-mined from the original report, not clinician-checked); wording limited to what the label supports"})
        if b:      # a real 'No Finding' film with free air painted under the right hemidiaphragm
            rng = np.random.default_rng(80 + b["variant"])
            base = np.asarray(Image.open(io.BytesIO(raw_bytes)).convert("L").resize((640, 640), Image.LANCZOS))
            im = add_mark(Image.fromarray(free_air(base, rng)))
            code, iid = "CXR-08", f"cxr-08-s{b['variant']}"
            folder = OUT / code
            folder.mkdir(parents=True, exist_ok=True)
            path = folder / f"{iid}.png"
            im.save(path, "PNG", optimize=True)
            raw = path.read_bytes()
            strip = hashlib.sha256(im.crop((0, im.height - MARK_HEIGHT, im.width, im.height)).tobytes()).hexdigest()
            entries.append({"id": iid, "file": f"{code}/{iid}.png", "sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw), "width": im.width, "height": im.height,
                            "finding_code": code, "synthetic": True, "source": None, "licence": "ProcSense generated from an NIH ChestX-ray14 image", "attribution": ATTRIBUTION,
                            "generator": {"name": "cxr_images.py", "seed": 80 + b["variant"], "kind": "free air painted into a real chest film", "base": f"{key[0]} row {key[1]}"},
                            "mark": {"text": MARK, "strip_sha256": strip, "strip_height": MARK_HEIGHT}, "status": "DRAFT", "cases": [], "review_required": True})
            reports.append({"id": f"rep-{iid}", "finding_code": code, "image_id": iid,
                            "text": "Chest X-ray, PA, erect. A crescent of free gas lies under the right hemidiaphragm. Impression: free air under the diaphragm (pneumoperitoneum), suggesting a perforated hollow organ.",
                            "measurements": {"free_air": "yes"}, "synthetic": True, "status": "DRAFT", "wording": "Category wording; the crescent is painted in at the right hemidiaphragm of a real film"})
    approvals.apply(entries, reports)
    entries.sort(key=lambda e: e["id"])
    reports.sort(key=lambda r: r["id"])
    (OUT / "index.json").write_text(json.dumps({"schema": 1, "images": entries}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    (ROOT / "content" / "cxr_reports.json").write_text(json.dumps({"schema": 1, "reports": reports}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{len(entries)} chest X-rays ({sum(not e['synthetic'] for e in entries)} real, {sum(e['synthetic'] for e in entries)} synthetic), {len(reports)} reports")
    return 0


if __name__ == "__main__":
    sys.exit({"candidates": candidates, "build": build}.get(sys.argv[1] if len(sys.argv) > 1 else "", lambda: print(__doc__) or 1)())
