"""Reading the generator parameter packs and drawing values from them (p1-dataset-analysis-s05; used live by numeric.py). A live generator only READS packs; it never refits.

A pack row carries a distribution's quantiles (from the profiles of p1-dataset-analysis-s02 and s03, aggregates only) and the walkthrough's zones for the analyte. Drawing a value for a requested zone:
  1. the zone is one or more intervals of lab_bands.csv; open ends are closed at max(p99, 1.25 x the interval's start), a rule recorded in the pack;
  2. where the interval overlaps the dataset's support (p1 to p99) by at least 2 percent of the data the value is drawn from the dataset's own quantile curve, restricted to the overlap (so a borderline value is as likely as the data says);
  3. where it does not (a critical value the demo never saw) the value is drawn uniformly (log-uniform when the interval spans more than a factor of 10) inside the band, and the draw says so (`source: band_only`).
Linked analytes are drawn together through a Gaussian copula with the pack's Spearman correlation, so WCC and CRP rise together instead of independently.
Every function takes a random.Random: the same seed gives the same value on any machine.
"""
from __future__ import annotations

import bisect
import json
import math
import random
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKS = ROOT / "content" / "generation" / "packs"
QUANTILE_POINTS = (1, 5, 25, 50, 75, 95, 99)
MIN_DATA_MASS = 0.02          # the share of the dataset a zone must hold for the dataset curve to be used inside it


def load(name: str, folder: Path = PACKS) -> dict:
    return json.loads((folder / f"{name}.json").read_text(encoding="utf-8"))


@dataclass(frozen=True)
class Draw:
    value: float
    source: str          # "dataset" (drawn from the quantile curve) or "band_only" (uniform inside the walkthrough's band)
    zone: str
    analyte: str


def _cholesky(m: list[list[float]]) -> list[list[float]] | None:
    n = len(m)
    L = [[0.0] * n for _ in range(n)]
    for i in range(n):
        for k in range(i + 1):
            s = sum(L[i][t] * L[k][t] for t in range(k))
            if i == k:
                d = m[i][i] - s
                if d <= 1e-9:
                    return None
                L[i][i] = math.sqrt(d)
            else:
                L[i][k] = (m[i][k] - s) / L[k][k]
    return L


def _phi(z: float) -> float:
    return 0.5 * (1 + math.erf(z / math.sqrt(2)))


def quantile_curve(q: dict) -> tuple[list[float], list[float]]:
    """Cumulative probabilities (0.01 to 0.99) and values from a quantile table; strictly increasing values only."""
    ps, vs = [], []
    for p in QUANTILE_POINTS:
        v = q[f"p{p}"]
        if vs and v <= vs[-1]:
            continue
        ps.append(p / 100)
        vs.append(v)
    return ps, vs


def inverse_cdf(ps: list[float], vs: list[float], u: float) -> float:
    """Value at cumulative probability u, linear between the points; clamped to the dataset's p1 to p99."""
    if u <= ps[0]:
        return vs[0]
    if u >= ps[-1]:
        return vs[-1]
    i = bisect.bisect_right(ps, u)
    t = (u - ps[i - 1]) / (ps[i] - ps[i - 1])
    return vs[i - 1] + t * (vs[i] - vs[i - 1])


def cdf(ps: list[float], vs: list[float], x: float) -> float:
    if x <= vs[0]:
        return 0.0 if x < vs[0] else ps[0]
    if x >= vs[-1]:
        return 1.0 if x > vs[-1] else ps[-1]
    i = bisect.bisect_right(vs, x)
    t = (x - vs[i - 1]) / (vs[i] - vs[i - 1])
    return ps[i - 1] + t * (ps[i] - ps[i - 1])


class LabPack:
    def __init__(self, labs: dict | None = None, correlations: dict | None = None):
        self.doc = labs if labs is not None else load("labs")
        self.corr = {frozenset((c["a"], c["b"])): c for c in (correlations if correlations is not None else load("correlations"))["pairs"]}

    def analytes(self) -> list[str]:
        return sorted(self.doc["analytes"])

    def joint(self) -> dict | None:
        """The joint model (a Gaussian copula over many analytes, tools/generation/fit_joint.py), or None if the pack folder has none."""
        if not hasattr(self, "_joint"):
            try:
                self._joint = load("joint")
                self._index = {a: i for i, a in enumerate(self._joint["analytes"])}
                self._chol: dict[tuple[str, ...], list[list[float]]] = {}
            except (OSError, ValueError):
                self._joint = None
        return self._joint

    def draw_panel(self, requests: list[tuple[str, str | None]], rng: random.Random, sex: str = "") -> list[Draw]:
        """Several analytes drawn together: `requests` is [(analyte, zone)]. One correlated normal per analyte from the joint model (an analyte it does not know is independent), each turned into a probability
        and then into a value of its zone. The order of the requests does not change a value: the normals are drawn in the order of the sorted analyte names."""
        j = self.joint()
        known = sorted({a for a, _ in requests if j and a in self._index})
        z: dict[str, float] = {a: rng.gauss(0, 1) for a in sorted({a for a, _ in requests})}          # independent normals first, in a fixed order
        if known:
            key = tuple(known)
            if key not in self._chol:
                sub = [[j["matrix"][self._index[a]][self._index[b]] for b in known] for a in known]
                L = _cholesky(sub)
                self._chol[key] = L if L is not None else [[1.0 if r == c else 0.0 for c in range(len(known))] for r in range(len(known))]
            L = self._chol[key]
            e = [z[a] for a in known]
            for r, a in enumerate(known):
                z[a] = sum(L[r][c] * e[c] for c in range(r + 1))
        return [self.draw(a, zone, rng, sex, _phi(z[a])) for a, zone in requests]

    def zones(self, analyte: str, sex: str = "") -> dict[str, list[tuple[float | None, float | None]]]:
        row = self.doc["analytes"][analyte]
        out: dict[str, list[tuple[float | None, float | None]]] = {}
        for z in row["zones"]:
            if z["sex"] and sex and z["sex"] != sex:
                continue
            out.setdefault(z["zone"], []).append((z["low"], z["high"]))
        return out

    def _intervals(self, analyte: str, zone: str, sex: str) -> list[tuple[float, float]]:
        row = self.doc["analytes"][analyte]
        ps, vs = quantile_curve(row["quantiles"]) if row["quantiles"] else ([], [])
        cap_rule = self.doc["open_end_rule"]
        out = []
        for lo, hi in self.zones(analyte, sex).get(zone, []):
            a = lo if lo is not None else min(0.0, vs[0] if vs else 0.0)
            b = hi if hi is not None else max(vs[-1] if vs else 0.0, (lo if lo is not None else 0.0) * cap_rule["factor_of_start"])
            if b > a:
                out.append((float(a), float(b)))
        return out

    def draw(self, analyte: str, zone: str | None, rng: random.Random, sex: str = "", u: float | None = None) -> Draw:
        """A value for the zone ("normal", "mild", ...); zone None draws from the whole dataset curve. `u` is a probability in [0,1] used instead of a fresh random one (the copula passes it)."""
        row = self.doc["analytes"][analyte]
        ps, vs = quantile_curve(row["quantiles"]) if row["quantiles"] else ([], [])
        u = rng.random() if u is None else u
        if zone is None:
            if not vs:
                raise ValueError(f"{analyte} has no dataset curve: ask for a zone")
            return Draw(inverse_cdf(ps, vs, 0.01 + 0.98 * u), "dataset", "", analyte)
        intervals = self._intervals(analyte, zone, sex)
        if not intervals:
            raise ValueError(f"{analyte} has no '{zone}' zone")
        mass = []
        for a, b in intervals:
            lo, hi = (max(a, vs[0]), min(b, vs[-1])) if vs else (1.0, 0.0)
            mass.append(max(0.0, cdf(ps, vs, hi) - cdf(ps, vs, lo)) if hi > lo else 0.0)
        total = sum(mass)
        if total >= MIN_DATA_MASS:      # a sliver of data (a few values at the edge of p99) is no basis for a shape: the band is used instead
            # pick an interval by its share of the data, then a point inside it by the data's own curve
            t, acc = u * total, 0.0
            for (a, b), m in zip(intervals, mass):
                if t <= acc + m or (a, b) == intervals[-1]:
                    lo, hi = max(a, vs[0]), min(b, vs[-1])
                    c0, c1 = cdf(ps, vs, lo), cdf(ps, vs, hi)
                    frac = min(1.0, max(0.0, (t - acc) / m)) if m > 0 else 0.5
                    return Draw(inverse_cdf(ps, vs, c0 + frac * (c1 - c0)), "dataset", zone, analyte)
                acc += m
        a, b = intervals[min(int(u * len(intervals)), len(intervals) - 1)]
        v = (a * (b / a) ** rng.random()) if a > 0 and b / a > 10 else a + (b - a) * rng.random()
        return Draw(v, "band_only", zone, analyte)

    def draw_pair(self, a: str, b: str, zone_a: str | None, zone_b: str | None, rng: random.Random, sex: str = "") -> tuple[Draw, Draw]:
        """Two linked analytes drawn through a Gaussian copula with the pack's Spearman correlation (independent when the pack has no pair or the pair is weak)."""
        pair = self.corr.get(frozenset((a, b)))
        rho_s = 0.0
        if pair and not pair["weak"] and pair["spearman"] is not None:
            rho_s = pair["spearman"]
        rho = 2 * math.sin(math.pi * rho_s / 6)          # Spearman to the correlation of the underlying normals
        z1 = rng.gauss(0, 1)
        z2 = rho * z1 + math.sqrt(max(0.0, 1 - rho * rho)) * rng.gauss(0, 1)
        return self.draw(a, zone_a, rng, sex, _phi(z1)), self.draw(b, zone_b, rng, sex, _phi(z2))
