"""Fits the joint model of laboratory results (p1-live-scenario-generation-s14, LG-07): `uv run --project tools tools/generation/fit_joint.py [--check]`.

A Gaussian copula over every pair of analytes that share at least MIN_PAIRS observations in the MIMIC-IV demo (DS-0003): the Spearman correlation of each such pair, kept only if its 95 percent interval excludes
zero, shrunk toward zero by n/(n+SHRINK) (a small sample earns less trust), turned into the correlation of the underlying normals (2 sin(pi rho / 6)), then made positive definite by mixing in the identity
until a Cholesky factorisation works. The result is aggregates only (no patient row): tools/data/derived/profiles/joint.json, copied into the generator packs by build_packs.py. The sampler draws a whole panel of
free results together through this matrix (server/generation/packs.py `draw_panel`), so results that move together in real patients (haemoglobin and haematocrit, urea and creatinine, white cells and CRP) still do.
Small data: 100 demo patients; the model is a copula of marginals plus correlations, not a per-condition model, and says so in its card. `--check` re-reads the file and verifies it is a valid correlation matrix.
"""
from __future__ import annotations

import itertools
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "tools" / "data"))
OUT = ROOT / "tools" / "data" / "derived" / "profiles" / "joint.json"
MIN_PAIRS = 30
SHRINK = 20.0


def cholesky(m: list[list[float]]) -> list[list[float]] | None:
    n = len(m)
    L = [[0.0] * n for _ in range(n)]
    for i in range(n):
        for j in range(i + 1):
            s = sum(L[i][k] * L[j][k] for k in range(j))
            if i == j:
                d = m[i][i] - s
                if d <= 1e-9:
                    return None
                L[i][i] = math.sqrt(d)
            else:
                L[i][j] = (m[i][j] - s) / L[j][j]
    return L


def positive_definite(m: list[list[float]]) -> tuple[list[list[float]], float]:
    """Mixes the identity in (1 - lam) m + lam I until the matrix factorises; returns the matrix and lam (0.0 if it already was)."""
    lam = 0.0
    while True:
        cur = [[(1 - lam) * m[i][j] + (lam if i == j else 0.0) for j in range(len(m))] for i in range(len(m))]
        if cholesky(cur) is not None:
            return cur, lam
        lam = 0.01 if lam == 0 else lam * 1.5
        if lam > 1:
            raise ValueError("cannot make the matrix positive definite")


def fit() -> dict:
    import profile_numeric as pn
    raw = pn.read_labs()
    by_key = {k: {e[2]: e[0] for e in v} for k, v in raw["rows"].items()}
    names = sorted(by_key)
    n = len(names)
    m = [[1.0 if i == j else 0.0 for j in range(n)] for i in range(n)]
    pairs = []
    for i, j in itertools.combinations(range(n), 2):
        a, b = names[i], names[j]
        common = sorted(set(by_key[a]) & set(by_key[b]))
        if len(common) < MIN_PAIRS:
            continue
        c = pn.correlation([by_key[a][k] for k in common], [by_key[b][k] for k in common])
        rho = c.get("spearman")
        ci = c.get("spearman_ci95")
        used = rho is not None and ci is not None and (ci[0] > 0 or ci[1] < 0)
        shrunk = rho * len(common) / (len(common) + SHRINK) if used else 0.0
        z = 2 * math.sin(math.pi * shrunk / 6)
        m[i][j] = m[j][i] = z
        pairs.append({"a": a, "b": b, "pairs": len(common), "spearman": rho, "used": bool(used), "normal_correlation": round(z, 4)})
    pd, lam = positive_definite(m)
    return {"kind": "joint", "version": 1, "method": "gaussian copula on the Spearman correlations of pairs with at least 30 observations, kept if the 95 percent interval excludes zero, shrunk by n/(n+20), "
            "mixed with the identity until positive definite", "source": "DS-0003 (MIMIC-IV demo 2.2, ODbL 1.0)", "analytes": names, "matrix": [[round(x, 5) for x in row] for row in pd],
            "identity_mix": round(lam, 4), "pairs": pairs, "card": "Small data: the demo has 100 patients, so the model is marginals plus correlations of whole-dataset pairs, not a model per condition; a pair with fewer than 30 observations is independent."}


def check_doc(doc: dict) -> list[str]:
    problems = []
    n = len(doc["analytes"])
    m = doc["matrix"]
    if len(m) != n or any(len(r) != n for r in m):
        return ["the matrix is not square on the analytes"]
    for i in range(n):
        if abs(m[i][i] - 1.0) > 1e-6:
            problems.append(f"diagonal {doc['analytes'][i]} is not 1")
        for j in range(i):
            if abs(m[i][j] - m[j][i]) > 1e-6 or abs(m[i][j]) >= 1:
                problems.append(f"entry {doc['analytes'][i]},{doc['analytes'][j]} is not symmetric or not inside (-1,1)")
    if cholesky(m) is None:
        problems.append("the matrix is not positive definite")
    return problems


def main(argv: list[str]) -> int:
    if "--check" in argv:
        problems = check_doc(json.loads(OUT.read_text(encoding="utf-8")))
        for p in problems:
            print("FAIL", p)
        print("joint model: " + ("OK" if not problems else f"{len(problems)} problem(s)"))
        return 1 if problems else 0
    doc = fit()
    OUT.write_text(json.dumps(doc, indent=1, sort_keys=True, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT).as_posix()}: {len(doc['analytes'])} analytes, {sum(1 for p in doc['pairs'] if p['used'])} correlated pairs of {len(doc['pairs'])}, identity mix {doc['identity_mix']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
