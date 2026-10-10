"""Builds ECG rhythm-strip pictures with matching reports into content/ecg/ (ADR-0003, ADR-0004).

    uv run --project tools --extra images tools/data/ecg_images.py

REAL strips come from PTB-XL (PhysioNet, CC BY 4.0): lead II, 10 s, 100 Hz, drawn as a paper strip (25 mm/s, 10 mm/mV, 1 mm grid). Records are cardiologist-validated and picked
evenly by record id from those that match the code; the heart rate (and for ST elevation the ST level) is MEASURED from the recording and is what the report states.
SYNTHETIC strips (hyperkalaemia, hypokalaemia, fast atrial fibrillation) have no labelled open recordings, so they are made from a REAL strip by editing the waveform in code
(peaked T waves; flat T waves with a U wave; the real atrial fibrillation record sped up), flagged synthetic, and carry the visible mark "SIMULATION: synthetic image, not a real scan".
A report states only the rhythm or pattern and the measured numbers, because a rhythm strip supports nothing more. Approvals come from content/image_approvals.json (approvals.py).
Writes content/ecg/index.json, content/ecg/ECG-xx/*.png and content/ecg_reports.json.
"""
from __future__ import annotations

import ast
import csv
import hashlib
import io
import json
import statistics
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import approvals  # noqa: E402
import extract_ptbxl as ptb  # noqa: E402

OUT = ROOT / "content" / "ecg"
MARK = "SIMULATION: synthetic image, not a real scan"
MARK_HEIGHT = 30
FONTS = ["C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]
ATTRIBUTION = ("PTB-XL, a large publicly available electrocardiography dataset, Wagner P. et al., PhysioNet, v1.0.3, doi:10.13026/kfzx-aw45, CC BY 4.0. "
               "Lead II drawn as a strip by ProcSense; the recording is not otherwise altered.")
ATTRIBUTION_SYNTH = "Derived from a PTB-XL recording (Wagner P. et al., doi:10.13026/kfzx-aw45, CC BY 4.0), edited in code and marked as synthetic."
PX_MM, HEIGHT_MM = 4, 60
PER = 4
RHYTHM_CODES = ("SR", "STACH", "SBRAD", "AFIB", "AFLT", "SVTAC", "PSVT", "SARRH", "BIGU", "TRIGU", "PACE")
MI_CODES = ("IMI", "AMI", "ILMI", "ALMI", "ASMI")

REAL = {
    "ECG-01": ("Sinus rhythm", "SR"), "ECG-02": ("Sinus tachycardia", "STACH"), "ECG-03": ("Atrial fibrillation", "AFIB"),
    "ECG-04": ("Sinus bradycardia", "SBRAD"), "ECG-05": ("Complete heart block", "3AVB"), "ECG-06": ("Supraventricular tachycardia", "SVT"),
    "ECG-07": ("ST elevation (inferior myocardial infarction pattern)", "STE"),
}


def font(size: int):
    for f in FONTS:
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            continue
    return ImageFont.load_default()


def render(x) -> Image.Image:
    x = list(x)
    w, h = int(len(x) / ptb.FS * 25 * PX_MM), HEIGHT_MM * PX_MM
    im = Image.new("RGB", (w, h), (255, 244, 244))
    d = ImageDraw.Draw(im)
    for mm in range(0, max(w, h) // PX_MM + 1):
        c = (240, 160, 160) if mm % 5 == 0 else (250, 210, 210)
        d.line([(mm * PX_MM, 0), (mm * PX_MM, h)], fill=c)
        d.line([(0, mm * PX_MM), (w, mm * PX_MM)], fill=c)
    base = statistics.median(x)
    d.line([(i / ptb.FS * 25 * PX_MM, h / 2 - (v - base) * 10 * PX_MM) for i, v in enumerate(x)], fill=(20, 20, 20), width=2)
    return im.convert("L")


def add_mark(im: Image.Image) -> Image.Image:
    out = Image.new("L", (im.width, im.height + MARK_HEIGHT), 0)
    out.paste(im, (0, 0))
    d = ImageDraw.Draw(out)
    f = font(17)
    d.text(((out.width - d.textlength(MARK, font=f)) / 2, im.height + 4), MARK, fill=235, font=f)
    return out


def strip_hash(im: Image.Image) -> str:
    return hashlib.sha256(im.convert("L").crop((0, im.height - MARK_HEIGHT, im.width, im.height)).tobytes()).hexdigest()


def measure(x):
    """(rate per minute, coefficient of variation of the beat interval, ST level in mV relative to the PR segment, R peaks) or None."""
    peaks = [p for p in ptb.r_peaks(x) if 14 <= p < len(x) - 16]
    if len(peaks) < 6:
        return None
    rr = [(peaks[i + 1] - peaks[i]) / ptb.FS for i in range(len(peaks) - 1)]
    st = [statistics.fmean(x[p + 9:p + 14]) - statistics.median(x[p - 14:p - 8]) for p in peaks]
    return 60 / statistics.fmean(rr), statistics.pstdev(rr) / statistics.fmean(rr), statistics.median(st), peaks


def pool_for(rows, kind: str):
    out = []
    for r in rows:
        codes = ast.literal_eval(r["scp_codes"])
        if r["validated_by_human"] != "True" or "PACE" in codes:
            continue
        others = {c for c in codes if c in RHYTHM_CODES}
        ok = (kind in ("SR", "STACH", "SBRAD", "AFIB") and others == {kind} and (kind != "SR" or codes.get("NORM", 0) >= 100)
              or kind == "3AVB" and "3AVB" in codes
              or kind == "SVT" and bool(others) and others <= {"SVTAC", "PSVT"}
              or kind == "STE" and any(codes.get(m, 0) >= 100 for m in MI_CODES) and others <= {"SR"})
        if ok:
            out.append(r)
    return sorted(out, key=lambda r: int(r["ecg_id"]))


def accepted(kind: str, m) -> bool:
    rate, cv, st, _ = m
    regular = cv < 0.12
    return {"SR": regular and 60 <= rate < 100, "STACH": regular and rate >= 100, "AFIB": (not regular) and rate >= 40, "SBRAD": regular and 35 <= rate < 60,
            "3AVB": 25 <= rate < 65, "SVT": regular and rate >= 140, "STE": regular and 50 <= rate < 110 and st >= 0.10}[kind]


def tw_edit(x, peaks, gain: float, u_wave: float = 0.0, st_drop: float = 0.0):
    """Scale the T wave of every beat about its baseline (peaked: gain > 1; flat: gain < 1), optionally add a U wave after it and a slight ST depression."""
    y = list(x)
    for p in peaks:
        if p + 66 >= len(y) or p < 14:
            continue
        base = statistics.median(x[p - 14:p - 8])
        for k in range(12, 48):                          # T wave window, 120-480 ms after the R peak
            y[p + k] = base + (x[p + k] - base) * gain
        for k in range(8, 20):
            y[p + k] -= st_drop * np.sin(np.pi * (k - 8) / 12)
        if u_wave:
            for k in range(48, 64):
                y[p + k] += u_wave * np.exp(-((k - 56) ** 2) / 18.0)
    return y


def speed_up(x, factor: float, seconds: float = 10.0):
    n = int(len(x) / factor)
    y = np.interp(np.linspace(0, len(x) - 1, n), np.arange(len(x)), x)
    reps = int(np.ceil(seconds * ptb.FS / len(y)))
    return list(np.tile(y, reps)[: int(seconds * ptb.FS)])


def prefetch(records, workers: int = 12) -> None:
    """Download the header and data files of many records at once (the network is the slow part)."""
    from concurrent.futures import ThreadPoolExecutor

    def one(r):
        try:
            ptb.get(r["filename_lr"] + ".hea")
            ptb.get(r["filename_lr"] + ".dat", binary=True)
        except Exception:   # noqa: BLE001
            pass
    with ThreadPoolExecutor(workers) as ex:
        list(ex.map(one, records))


def main() -> int:
    rows = list(csv.DictReader(io.StringIO(ptb.get("ptbxl_database.csv"))))
    entries, reports = [], []
    chosen: dict[str, list] = {}

    def save(iid, code, im, synthetic, label, rep, record=None, seed=None, kind=None):
        folder = OUT / code
        folder.mkdir(parents=True, exist_ok=True)
        if synthetic:
            im = add_mark(im)
        path = folder / f"{iid}.png"
        im.save(path, "PNG")
        raw = path.read_bytes()
        e = {"id": iid, "file": f"{code}/{iid}.png", "sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw), "width": im.width, "height": im.height, "finding_code": code,
             "synthetic": synthetic, "licence": "CC BY 4.0" if not synthetic else "ProcSense generated from a CC BY 4.0 recording", "status": "DRAFT", "cases": [], "review_required": True}
        if synthetic:
            e.update(source=None, attribution=ATTRIBUTION_SYNTH, generator={"name": "ecg_images.py", "seed": seed, "kind": kind, "base": record},
                     mark={"text": MARK, "strip_sha256": strip_hash(im), "strip_height": MARK_HEIGHT})
        else:
            e.update(source={"dataset": "PTB-XL", "class": label, "ecg_id": record["ecg_id"], "record": record["filename_lr"], "doi": "10.13026/kfzx-aw45",
                             "url": "https://physionet.org/content/ptb-xl/1.0.3/"}, attribution=ATTRIBUTION, generator=None, mark=None)
        entries.append(e)
        reports.append({"id": f"rep-{iid}", "finding_code": code, "image_id": iid, **rep, "synthetic": synthetic, "status": "DRAFT",
                        "wording": "Pattern label from PTB-XL (human-validated) or the pattern edited in; numbers measured from the signal"})

    for code, (label, kind) in REAL.items():
        usable = []
        candidates = pool_for(rows, kind)
        if kind != "STE":                                    # ST elevation is rare among the labelled records, so all of them are measured
            step_pool = max(1, len(candidates) // 160)       # the others are spread over the whole id range, at most 160 records per code
            candidates = candidates[::step_pool][:160]
        prefetch(candidates)
        for r in candidates:
            try:
                x = ptb.read_lead_ii(r["filename_lr"])
            except Exception:   # noqa: BLE001
                continue
            m = measure(x)
            if m and accepted(kind, m):
                usable.append((r, x, m))
            if len(usable) >= PER * 3 and kind != "STE":
                break
        if len(usable) < PER:
            raise SystemExit(f"{code}: only {len(usable)} usable real records")
        picks = usable[:: max(1, len(usable) // PER)][:PER]
        chosen[code] = picks
        for k, (r, x, (rate, cv, st, _)) in enumerate(picks):
            rate_i = round(rate)
            meas = {"rate_bpm": rate_i, "regular": "yes" if cv < 0.12 else "no"}
            body = f"Rhythm strip, lead II, 25 mm/s. {label}. Heart rate {rate_i} per minute (measured from the recording). "
            if code in ("ECG-01", "ECG-02", "ECG-04"):
                meas["rhythm"] = "sinus"
                body += "Regular rhythm with a P wave before each QRS."
            elif code == "ECG-03":
                meas["rhythm"] = "af"
                body += "Beats irregularly irregular; no clear P waves."
            elif code == "ECG-05":
                meas["rhythm"] = "chb"
                body += "Slow ventricular rate with no consistent relationship between P waves and QRS complexes."
            elif code == "ECG-06":
                meas["rhythm"] = "svt"
                body += "Fast regular narrow-complex rhythm."
            else:
                meas.update(rhythm="sinus", st="elevation", st_elevation_mm=round(st * 10, 1))
                body += f"ST segment elevated by {st * 10:.1f} mm in lead II (measured)."
            save(f"{code.lower()}-r{k}", code, render(x), False, label, {"text": f"{body} Impression: {label}.", "measurements": meas},
                 record={"ecg_id": int(r["ecg_id"]), "filename_lr": r["filename_lr"]})

    sr, af = chosen["ECG-01"], chosen["ECG-03"]
    for k in range(PER):
        r, x, (rate, cv, st, peaks) = sr[k % len(sr)]
        rate_i = round(rate)
        base = f"PTB-XL ecg_id {r['ecg_id']}"
        save(f"ecg-08-s{k}", "ECG-08", render(tw_edit(x, peaks, 2.3)), True, None,
             {"text": f"Rhythm strip, lead II, 25 mm/s. Regular rhythm, heart rate {rate_i} per minute. Tall, narrow, peaked T waves. Impression: peaked T waves, in keeping with a high potassium.",
              "measurements": {"rhythm": "sinus", "regular": "yes", "rate_bpm": rate_i, "t_wave": "peaked"}}, record=base, seed=800 + k, kind="T waves edited in a real sinus rhythm strip")
        save(f"ecg-09-s{k}", "ECG-09", render(tw_edit(x, peaks, 0.25, u_wave=0.14, st_drop=0.06)), True, None,
             {"text": f"Rhythm strip, lead II, 25 mm/s. Regular rhythm, heart rate {rate_i} per minute. Flattened T waves with prominent U waves and slight ST depression. Impression: changes in keeping with a low potassium.",
              "measurements": {"rhythm": "sinus", "regular": "yes", "rate_bpm": rate_i, "t_wave": "flat_u"}}, record=base, seed=900 + k, kind="T waves edited in a real sinus rhythm strip")
        r, x, (rate, cv, st, peaks) = af[k % len(af)]
        factor = 132.0 / rate
        y = speed_up(x, factor)
        m2 = measure(y)
        for _ in range(4):                                  # the measured rate of the sped-up strip must land near 132
            if m2 and abs(m2[0] - 132.0) > 6:
                factor *= 132.0 / m2[0]
                y = speed_up(x, factor)
                m2 = measure(y)
        rate_i = round(m2[0]) if m2 else 132
        save(f"ecg-10-s{k}", "ECG-10", render(y), True, None,
             {"text": f"Rhythm strip, lead II, 25 mm/s. Atrial fibrillation. Heart rate {rate_i} per minute (measured). Beats irregularly irregular; no clear P waves. Impression: atrial fibrillation with a fast ventricular rate.",
              "measurements": {"rhythm": "af", "regular": "no", "rate_bpm": rate_i}}, record=f"PTB-XL ecg_id {r['ecg_id']}", seed=1000 + k, kind="a real atrial fibrillation strip sped up")

    approvals.apply(entries, reports)
    entries.sort(key=lambda e: e["id"])
    reports.sort(key=lambda r: r["id"])
    (OUT / "index.json").write_text(json.dumps({"schema": 1, "images": entries}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    (ROOT / "content" / "ecg_reports.json").write_text(json.dumps({"schema": 1, "reports": reports}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{len(entries)} ECG strips ({sum(not e['synthetic'] for e in entries)} real, {sum(e['synthetic'] for e in entries)} synthetic), {len(reports)} reports")
    for r in reports:
        print(" ", r["id"], r["measurements"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
