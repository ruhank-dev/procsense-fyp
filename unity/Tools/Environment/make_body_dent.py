"""Mattress depression under the resting patient and pillow, from the measured body underside (PatientPose.Report).

Reads Logs/LookUpgrade/body_field.json (1 cm grid of the lowest body/pillow height per cell, -9 = nothing) and writes
Logs/LookUpgrade/body_dent.json: a 1 cm grid of how far the soft mattress top must sink so the body rests ON the surface,
with a smooth shoulder around the footprint. Soft foam: depth is capped, and the cradle never rises above the flat top.
"""
import json
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Logs" / "LookUpgrade" / "body_field.json"
OUT = ROOT / "Logs" / "LookUpgrade" / "body_dent.json"
TOP = 0.700
SKIN = 0.0012           # the surface sits this far under the body so nothing intersects
MAX_DEPTH = 0.052


def gauss(a, sigma_cells):
    k = int(sigma_cells * 3)
    x = np.arange(-k, k + 1)
    g = np.exp(-(x ** 2) / (2 * sigma_cells ** 2)); g /= g.sum()
    a = np.apply_along_axis(lambda r: np.convolve(r, g, mode="same"), 0, a)
    return np.apply_along_axis(lambda r: np.convolve(r, g, mode="same"), 1, a)


def main():
    d = json.loads(SRC.read_text())
    nx, nz = d["nx"], d["nz"]
    f = np.array(d["minY"], dtype=float).reshape(nz, nx)
    have = f > -8
    raw = np.where(have, np.clip(TOP - (f - SKIN), 0, MAX_DEPTH), 0.0)
    # fill one-cell gaps inside the footprint (vertex sampling leaves holes) by a local max, then smooth
    for _ in range(2):
        pad = np.pad(raw, 1)
        nb = np.maximum.reduce([pad[:-2, 1:-1], pad[2:, 1:-1], pad[1:-1, :-2], pad[1:-1, 2:]])
        raw = np.where((raw == 0) & (nb > 0.004), nb * .8, raw)
    soft = gauss(raw, 2.2)                         # ~2.2 cm shoulder around the contact
    dent = np.maximum(gauss(raw, .8), soft * .92)  # keep full depth where the body is, a soft rim outside
    dent = np.minimum(dent, MAX_DEPTH)
    OUT.write_text(json.dumps({"x0": d["x0"], "z0": d["z0"], "step": d["step"], "nx": nx, "nz": nz,
                               "dent": [round(float(v), 5) for v in dent.ravel()]}))
    print(f"dent: max {dent.max() * 100:.1f} cm, covers {(dent > .002).mean() * 100:.1f}% of the grid")


if __name__ == "__main__":
    main()
