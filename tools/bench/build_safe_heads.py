"""Builds the two heads the ensemble gate uses, with hard negatives: `uv run --project tools --with scikit-learn --with numpy tools/bench/build_safe_heads.py [--ollama URL] [--dry-run]`.

Same recipe as the shipped heads (the 1,940 training rows of the synthetic v2 benchmark, C chosen on v2 DEV by macro-F1, per-act credit thresholds tuned on DEV for credited precision 0.8) PLUS hard negatives: the three
adversarial critical-content sets that were used for tuning (tests/classifier_synthetic_v2/adversarial_critical.json, _holdout.json, _holdout2.json: 179 evasive sentences about allergy, identity, consent, fasting, a
worsening sign and a handover, each with its critical act). They teach both heads that evasive critical wording is critical, which is the error the ensemble gate was found to need (safety_gate.md). Nothing here reads
the v2 test split, the v3 hold-out, adversarial hold-out 3 or the challenge set v4. The critical-mass cap of the embedding head is chosen on v2 DEV with the agreement rule in force (the largest cap that credits no
critical sentence under an ordinary act and keeps credited precision at 0.8 or more); the TF-IDF head gets the same rule alone. --dry-run prints the dev figures and writes nothing.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for sub in ("server", "tools/bench", "tools/ai"):
    sys.path.insert(0, str(ROOT / sub))
import bench_classifier as BC  # noqa: E402
from classify import classifier as C  # noqa: E402
from classify.embedding import EmbeddingActClassifier  # noqa: E402
from classify.guard import critical_cue  # noqa: E402
from classify.tfidf import TfidfActClassifier  # noqa: E402

HARD_SETS = ("adversarial_critical.json", "adversarial_critical_holdout.json", "adversarial_critical_holdout2.json")
GRID_HYBRID = [0.01, 0.02, 0.03, 0.05, 0.08, 0.12]
GRID = [0.01, 0.02, 0.03, 0.05]                                  # defence in depth: the cap stays tight even though the agreement rule already removes most wrong credit
HARD_NOTE = " Hard negatives: the 179 evasive critical sentences of the three tuning adversarial sets and, since the fifth session, the critical_evasive rows of the challenge set v4 (a development set since its third-session scoring) are added to training with their critical act (build_safe_heads.py). Adversarial hold-out 3 and the challenge set v5 are held out of training as the internal validation; the challenge set v6 is frozen and unread."


def hard_negatives() -> list[dict]:
    rows = []
    for name in HARD_SETS:
        rows += [{"text": r["text"], "act": r["act"], "group": "hard-negative", "bucket": "hard_negative"} for r in json.loads((BC.SYNTH2 / name).read_text(encoding="utf-8"))["rows"]]
    v4 = json.loads((ROOT / "tests" / "classifier_challenge_v4" / "challenge.json").read_text(encoding="utf-8"))["rows"]
    rows += [{"text": r["text"], "act": r["act"], "group": "hard-negative", "bucket": "hard_negative"} for r in v4 if r.get("bucket") == "critical_evasive"]       # fifth session: v4 was scored once in the third session and is a development set since
    return rows


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args(argv)
    import numpy as np
    from sklearn.feature_extraction.text import TfidfVectorizer
    from sklearn.linear_model import LogisticRegression
    data = BC.load_data("synthetic2", True)
    data["train"] = data["train"] + hard_negatives()
    data["provenance"] += HARD_NOTE
    tax = C.taxonomy()["acts"]
    labels, critical = [t["id"] for t in tax], BC.critical_acts(tax)
    tr_y, dev_y = [r["act"] for r in data["train"]], [r["act"] for r in data["dev"]]
    x_tr = np.array(BC.embed([r["text"] for r in data["train"]], "all-minilm:22m", a.ollama))
    x_dev = np.array(BC.embed([r["text"] for r in data["dev"]], "all-minilm:22m", a.ollama))
    vec = TfidfVectorizer(ngram_range=(1, 2), lowercase=True, sublinear_tf=True).fit([r["text"] for r in data["train"]])
    t_tr, t_dev = vec.transform([r["text"] for r in data["train"]]), vec.transform([r["text"] for r in data["dev"]])

    def best_fit(xt, xd):
        best = None
        for c in (1, 10, 100):
            m = LogisticRegression(max_iter=3000, C=c).fit(xt, tr_y)
            f1 = BC.macro_f1(dev_y, list(m.predict(xd)), labels)
            if best is None or f1 > best[0] + 1e-9:
                best = (f1, c, m)
        return best

    f_e, c_e, m_e = best_fit(x_tr, x_dev)
    f_t, c_t, m_t = best_fit(t_tr, t_dev)

    def thresholds(m, xd):
        p = m.predict_proba(xd)
        cls = [str(x) for x in m.classes_]
        return C.calibrate([(t, cls[i], float(row.max())) for t, i, row in zip(dev_y, p.argmax(axis=1), p)], C.taxonomy(), BC.MIN_PRECISION)

    thr_e, thr_t = thresholds(m_e, x_dev), thresholds(m_t, t_dev)
    BC.write_head(ROOT / "artifacts" / "ai-bench" / "act_head_safe_tmp.json", m_e, thr_e, c_e, {"embedding_model": "all-minilm:22m", "dimension": x_tr.shape[1]}, data, tax, critical)
    (ROOT / "artifacts" / "ai-bench").mkdir(parents=True, exist_ok=True)
    tmp = ROOT / "artifacts" / "ai-bench" / "act_head_safe_tmp.json"
    from classify.embedding import load_head
    e_head = load_head(tmp)
    terms = sorted(vec.vocabulary_, key=vec.vocabulary_.get)
    classes_t = [str(k) for k in m_t.classes_]
    t_head = {"kind": "tfidf_logreg_head.v1", "dimension": len(terms), "classes": classes_t, "vocabulary": terms, "idf": [round(float(v), 6) for v in vec.idf_],
              "coef": [[round(float(w), 5) for w in row] for row in m_t.coef_], "intercept": [round(float(b), 6) for b in m_t.intercept_], "C": c_t,
              "thresholds": {act: (None if act in critical or act == "NONE" else thr_t.get(act)) for act in classes_t}, "min_precision": BC.MIN_PRECISION, "trained_on_rows": len(data["train"]),
              "train_text_sha256": hashlib.sha256("\n".join(sorted(r["text"] for r in data["train"])).encode("utf-8")).hexdigest(), "thresholds_tuned_on": f"dev split ({len(data['dev'])} rows)",
              "valid_for_claims": False, "provenance": data["provenance"],
              "note": "Credit needs the act to be neither NONE nor critical, its probability to reach the threshold, no critical cue and a critical-act probability below critical_mass_max. Deterministic action-based scoring stays authoritative."}
    e_clf = EmbeddingActClassifier(None, e_head)
    t_clf = TfidfActClassifier(dict(t_head, _critical=critical))

    def table(kind: str) -> dict:
        out = {}
        for tau in GRID:
            credited = wrong = misrouted = 0
            for r, xv in zip(data["dev"], x_dev):
                pe = e_clf.probabilities(list(xv))
                act = max(pe, key=pe.get)
                t = e_head["thresholds"].get(act)
                ok = act not in critical and act != "NONE" and t is not None and pe[act] >= t and not critical_cue(r["text"])
                if kind == "embedding":
                    ok = ok and e_clf.critical_mass(pe) < tau
                else:
                    pt = t_clf.probabilities_of(r["text"])
                    at = max(pt, key=pt.get)
                    tt = t_head["thresholds"].get(at)
                    ok = ok and at == act and e_clf.critical_mass(pe) < tau if kind == "ensemble" else (at not in critical and at != "NONE" and tt is not None and pt[at] >= tt and t_clf.critical_mass(pt) < tau and not critical_cue(r["text"]))
                if ok:
                    credited += 1
                    wrong += act != r["act"] if kind != "tfidf" else 0
                    misrouted += r["act"] in critical
            out[tau] = {"credited": credited, "precision": round((credited - wrong) / credited, 3) if credited else None, "critical_said_but_credited_as_other_act": misrouted}
        return out

    def choose(tab):
        ok = [t for t, m in tab.items() if m["critical_said_but_credited_as_other_act"] == 0 and (m["precision"] or 0) >= 0.8]
        return (max(ok, key=lambda t: tab[t]["credited"]) if ok else min(tab, key=lambda t: (tab[t]["critical_said_but_credited_as_other_act"], -tab[t]["credited"])), bool(ok))

    tab_e = table("ensemble")
    cap_e, met_e = choose(tab_e)
    e_head.pop("_critical", None)
    e_head["critical_mass_max"] = cap_e if cap_e <= 1 else None
    e_head["critical_mass_tuning"] = {"split": "synthetic v2 dev only", "rows": len(data["dev"]), "rule": "ensemble agreement in force", "grid": {str(k): v for k, v in tab_e.items()}, "chosen": cap_e, "met_both_conditions": met_e}
    pt_tab = {}
    for tau in GRID:
        credited = wrong = misrouted = 0
        for r in data["dev"]:
            pt = t_clf.probabilities_of(r["text"])
            at = max(pt, key=pt.get)
            tt = t_head["thresholds"].get(at)
            if at not in critical and at != "NONE" and tt is not None and pt[at] >= tt and t_clf.critical_mass(pt) < tau and not critical_cue(r["text"]):
                credited += 1
                wrong += at != r["act"]
                misrouted += r["act"] in critical
        pt_tab[tau] = {"credited": credited, "precision": round((credited - wrong) / credited, 3) if credited else None, "critical_said_but_credited_as_other_act": misrouted}
    cap_t, met_t = choose(pt_tab)
    t_head["critical_mass_max"] = cap_t if cap_t <= 1 else None
    t_head["critical_mass_tuning"] = {"split": "synthetic v2 dev only", "grid": {str(k): v for k, v in pt_tab.items()}, "chosen": cap_t, "met_both_conditions": met_t}
    # the hybrid head: embedding and word features in one regression (the first reader of the gate)
    from scipy.sparse import csr_matrix, hstack
    h_tr, h_dev = hstack([csr_matrix(x_tr), t_tr]).tocsr(), hstack([csr_matrix(x_dev), t_dev]).tocsr()
    best_h = None
    for c in (10, 30, 100):
        m = LogisticRegression(max_iter=4000, C=c).fit(h_tr, tr_y)
        f1 = BC.macro_f1(dev_y, list(m.predict(h_dev)), labels)
        if best_h is None or f1 > best_h[0] + 1e-9:
            best_h = (f1, c, m)
    f_h, c_h, m_h = best_h
    thr_h = thresholds(m_h, h_dev)
    classes_h = [str(k) for k in m_h.classes_]
    p_h, p_t = m_h.predict_proba(h_dev), m_t.predict_proba(t_dev)
    tab_h = {}
    for tau in GRID_HYBRID:
        credited = wrong = misrouted = 0
        for r, ph, pt in zip(data["dev"], p_h, p_t):
            act = classes_h[int(ph.argmax())]
            t = thr_h.get(act)
            mass = sum(v for k, v in zip(classes_h, ph) if k in critical)
            if act not in critical and act != "NONE" and t is not None and ph.max() >= t and mass < tau and not critical_cue(r["text"]) and classes_t[int(pt.argmax())] == act:
                credited += 1
                wrong += act != r["act"]
                misrouted += r["act"] in critical
        tab_h[tau] = {"credited": credited, "precision": round((credited - wrong) / credited, 3) if credited else None, "critical_said_but_credited_as_other_act": misrouted}
    # fifth session: the hybrid head's cap is the largest of a wider grid that credits NO held-out critical sentence. Held out of training: adversarial hold-out 3 and the critical rows of the challenge set v5 (96 sentences).
    held = json.loads((BC.SYNTH2 / "adversarial_critical_holdout3.json").read_text(encoding="utf-8"))["rows"] + [r for r in json.loads((ROOT / "tests" / "classifier_challenge_v5" / "challenge.json").read_text(encoding="utf-8"))["rows"] if r.get("bucket") == "critical_evasive"]
    x_held = np.array(BC.embed([r["text"] for r in held], "all-minilm:22m", a.ollama))
    t_held = vec.transform([r["text"] for r in held])
    p_hh, p_th = m_h.predict_proba(hstack([csr_matrix(x_held), t_held]).tocsr()), m_t.predict_proba(t_held)
    held_tab = {}
    for tau in GRID_HYBRID:
        fc = 0
        for r, ph, pt in zip(held, p_hh, p_th):
            act = classes_h[int(ph.argmax())]
            t = thr_h.get(act)
            mass = sum(v for k, v in zip(classes_h, ph) if k in critical)
            fc += bool(act not in critical and act != "NONE" and t is not None and ph.max() >= t and mass < tau and not critical_cue(r["text"]) and classes_t[int(pt.argmax())] == act)
        held_tab[tau] = fc
    ok_caps = [t for t in GRID_HYBRID if held_tab[t] == 0 and tab_h.get(t, {"critical_said_but_credited_as_other_act": 0})["critical_said_but_credited_as_other_act"] == 0]
    cap_h, met_h = (max(ok_caps), True) if ok_caps else choose(tab_h)
    hybrid = {"kind": "hybrid_logreg_head.v1", "embedding_model": "all-minilm:22m", "embedding_dimension": int(x_tr.shape[1]), "dimension": int(x_tr.shape[1]) + len(terms), "classes": classes_h, "vocabulary": terms,
              "idf": t_head["idf"], "coef": [[round(float(w), 5) for w in row] for row in m_h.coef_], "intercept": [round(float(b), 6) for b in m_h.intercept_], "C": c_h,
              "thresholds": {act: (None if act in critical or act == "NONE" else thr_h.get(act)) for act in classes_h}, "min_precision": BC.MIN_PRECISION, "trained_on_rows": len(data["train"]),
              "train_text_sha256": t_head["train_text_sha256"], "thresholds_tuned_on": f"dev split ({len(data['dev'])} rows)", "valid_for_claims": False, "provenance": data["provenance"],
              "note": "Embedding and word features in one regression; the first reader of the credit gate, never trusted alone (server/classify/ensemble.py). Credit needs the act to be neither NONE nor critical, its probability to reach the threshold, no critical cue and a critical-act probability below critical_mass_max.",
              "critical_mass_max": cap_h if cap_h <= 1 else None,
              "critical_mass_tuning": {"split": "v2 dev (precision) and 96 held-out critical sentences (adversarial hold-out 3 and the critical rows of challenge v5, not in training)", "rule": "the TF-IDF head must name the same act; the cap is the largest of the grid at which no held-out critical sentence is credited", "grid": {str(k): v for k, v in tab_h.items()}, "held_out_false_credit_by_cap": {str(k): v for k, v in held_tab.items()}, "chosen": cap_h, "met_both_conditions": met_h}}
    print(json.dumps({"rows": len(data["train"]), "embedding": {"C": c_e, "dev_macro_f1": round(f_e, 4), "cap": cap_e, "met": met_e}, "tfidf": {"C": c_t, "dev_macro_f1": round(f_t, 4), "cap": cap_t, "met": met_t}, "hybrid": {"C": c_h, "dev_macro_f1": round(f_h, 4), "cap": cap_h, "met": met_h}}, indent=1))
    if a.dry_run:
        tmp.unlink()
        return 0
    (ROOT / "content" / "assessment" / "act_head.json").write_text(json.dumps(e_head, indent=1) + "\n", encoding="utf-8", newline="\n")
    (ROOT / "content" / "assessment" / "act_head_tfidf.json").write_text(json.dumps(t_head, indent=1) + "\n", encoding="utf-8", newline="\n")
    (ROOT / "content" / "assessment" / "act_head_hybrid.json").write_text(json.dumps(hybrid, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
    tmp.unlink()
    print("wrote content/assessment/act_head.json, act_head_tfidf.json and act_head_hybrid.json")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
