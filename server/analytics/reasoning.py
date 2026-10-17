"""Clinical reasoning features from a session (p2-ai-learning-analytics-s03, feature D42): how the trainee worked, not only what was scored.

    f = features(evidence)       # from a SessionEvidence record (its ordered actions)

Features, all from the ordered actions in the session evidence. LIMIT: the evidence holds the actions the scoring maps to a behaviour (the debrief timeline), not every verb the engine accepted, so an action that no behaviour names (examine, for one) can look absent; the features are read as "in the scored actions". DT-209 asks for the full action list in the evidence.

  gathering_order      the first position of each kind of action (identity, history, examination, observations, investigations, risk, decision), so the order of working is visible
  history_before_tests whether any history question came before the first investigation was ordered
  time_to_key_checks   seconds (sim) from the start to the first identity check, allergy question and observation, where they happened
  premature_closure    a decision (declare_outcome, decide_ready, postpone, redirect) made before the examination, any investigation result viewed, or the allergy question
  test_yield           investigations ordered, results viewed, and the share viewed (an ordered test whose result was never viewed is a wasted test)
  actions              the number of actions
Wording of the result is neutral and describes what happened ("the decision came before the allergy question"); it never names a trait of the person. Clustering of fingerprints into styles
is left until there are enough real sessions (DT-209); with a handful it would invent groups.
"""
from __future__ import annotations

KINDS = {
    "identity": {"ask_identity", "read_wristband", "open_notes", "confirm_identity", "search_patient"},
    "history": {"ask", "ask_allergies", "ask_allergy_reaction", "ask_reason_for_admission", "ask_responsible_adult", "ask_pain_score"},
    "examination": {"examine", "ask_exam_permission", "murphy_test", "palpate_pulse", "offer_chaperone"},
    "observations": {"attach_sensor", "measure_nibp", "measure_temperature", "count_rr", "assess_acvpu", "assess_news2"},
    "investigations": {"order_investigation", "take_sample", "send_samples", "perform_bedside_test"},
    "results": {"view_result", "view_image", "interpret_result", "interpret_imaging", "acknowledge_result"},
    "risk": {"assess_risk", "assess_vte", "compute_score", "request_anaesthetic_review", "check_capacity"},
    "decision": {"declare_outcome", "decide_ready", "postpone", "redirect", "decide", "declare_or_ready"},
}
KIND_OF = {v: k for k, vs in KINDS.items() for v in vs}


def seconds(clock: str) -> int:
    h, m, s = (int(x) for x in clock.split(":"))
    return h * 3600 + m * 60 + s


def features(ev: dict) -> dict:
    acts = ev["actions"]
    first: dict[str, int] = {}
    for i, a in enumerate(acts):
        k = KIND_OF.get(a["verb"])
        if k and k not in first:
            first[k] = i
    t0 = seconds(acts[0]["at"]) if acts else 0

    def at(verb_set):
        for a in acts:
            if a["verb"] in verb_set:
                return seconds(a["at"]) - t0
        return None
    ordered = sum(a["verb"] == "order_investigation" for a in acts)
    viewed = sum(a["verb"] in ("view_result", "view_image") for a in acts)
    decision_at = first.get("decision")
    before = lambda kind: decision_at is not None and kind in first and first[kind] > decision_at     # noqa: E731
    premature = []
    if decision_at is not None:
        if "examination" not in first or before("examination"):
            premature.append("examination")
        if "results" not in first or before("results"):
            premature.append("results")
        if not any(a["verb"] == "ask_allergies" for a in acts[:decision_at]):
            premature.append("allergy_question")
    return {"gathering_order": [k for k, _ in sorted(first.items(), key=lambda kv: kv[1])], "history_before_tests": ("history" in first and ("investigations" not in first or first["history"] < first["investigations"])),
            "time_to_key_checks": {"identity": at(KINDS["identity"]), "allergy_question": at({"ask_allergies"}), "observation": at(KINDS["observations"])},
            "premature_closure": premature, "test_yield": {"ordered": ordered, "viewed": viewed, "share_viewed": round(viewed / ordered, 2) if ordered else None}, "actions": len(acts)}


def describe(f: dict) -> list[str]:
    out = []
    if f["premature_closure"]:
        out.append("The decision came before: " + ", ".join(p.replace("_", " ") for p in f["premature_closure"]) + ".")
    ty = f["test_yield"]
    if ty["ordered"] and ty["share_viewed"] is not None and ty["share_viewed"] < 1:
        out.append(f"{ty['ordered']} tests were ordered and {ty['viewed']} results were viewed.")
    if f["history_before_tests"] is False and ty["ordered"]:
        out.append("Tests were ordered before any history question.")
    return out
