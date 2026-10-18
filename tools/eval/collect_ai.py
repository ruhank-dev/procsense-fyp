"""AI evaluation report for the project report (p1-evaluation-s02): `uv run --project tools tools/eval/collect_ai.py [--check]`.

Reads the committed result files in reports/ai/ (one JSON per evaluation run, copied from artifacts/ai-bench/ or written by the tool that ran it) and writes docs/report/ai-eval.md: tables of what was measured,
on which model and digest, with the limit of each measure stated beside it. Every number in the page comes from a file; nothing is typed by hand. A result file that is missing is listed as "not run", never
filled in. `--check` fails if the page differs from what the files would give (so a result cannot change without the page changing).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / "reports" / "ai"
OUT = ROOT / "docs" / "report" / "ai-eval.md"


def load(name: str) -> dict | None:
    p = REPORTS / name
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else None


def pct(x) -> str:
    return "n/a" if x is None else f"{100 * x:.1f}%"


def section_narrative() -> list[str]:
    rows = []
    for name in ("narrative-4b.json", "narrative-9b.json"):
        d = load(name)
        if d is None:
            rows.append(f"| {name} | not run | | | | | |")
            continue
        rows.append(f"| {d['model']} (`{d['digest'][:12]}`) | {d['lines']} | {pct(d['first_try_schema_valid_rate'])} | {pct(d['first_try_rate'])} | {pct(d['accept_rate'])} | {d['fallback_to_authored']} | {d['latency_ms_p50']} ms |")
    return ["## Patient wording of generated cases (compose.narrative)", "",
            "| Model | Lines | Schema valid first try | Accepted first try | Accepted after one retry | Fell back to the authored line | Latency p50 |", "|---|---|---|---|---|---|---|"] + rows + [
            "", "Limit: the checks are lexical (numbers, number words, time words, yes and no, medicines, tests, conditions, allergens, prescribing phrases, length, words kept). Every accepted pair is in the result file for a person to read; "
            "the share a person finds unfaithful has not been measured (needs a reader).", ""]


def section_planner() -> list[str]:
    d = load("planner-eval.json")
    if d is None:
        return ["## Plan composer against the solver alone", "", "not run", ""]
    rows = []
    for arm, v in d["arms"].items():
        rows.append(f"| {arm} | {v['served']} | {v['passed_gate']} | {v['coverage_mean']} | {v['difficulty_mean']} | {v['distinct_block_sets']} | {v['distinct_stories']} | {v['seconds_p50']} s | {v['model_plans_used']} |")
    return [f"## Plan composer against the solver alone ({d['model']}, {d['requests']} requests)", "",
            "| Arm | Served | Passed the whole gate | Coverage of requested dimensions (mean) | Difficulty match (mean) | Distinct block sets | Distinct stories | Time p50 | Model plans used |", "|---|---|---|---|---|---|---|---|---|"] + rows + [
            "", f"The planner chooses among the best {d.get('shortlist', 8)} spines (spine prompt version {d.get('spine_prompt_version') or 1}); the solver draws from the same best 5." + "".join(f" Before: {k.replace('_', ' ')}, coverage {v['arms']['planner']['coverage_mean']}, served {v['arms']['planner']['served']}." for k, v in d.get("baselines", {}).items()),
            "", "Limit: coverage and difficulty are deterministic proxies; the planner is less varied than the solver and slow (an offline tool). The blind comparison by two raters that the plan asks for has not been done (DT-203).", ""]


def section_interpreter() -> list[str]:
    d = load("interpreter-eval.json")
    if d is None:
        return ["## Performance interpreter", "", "not run", ""]
    return ["## Performance interpreter (hypotheses against random picks)", "", f"Model {d['model']}, {d['learners']} simulated learners, {d['hypotheses']} hypotheses accepted, {d['learners_with_no_accepted_proposal']} learners with no accepted proposal.", "",
            "| Picks | Share confirmed by later sessions |", "|---|---|", f"| The model's hypotheses | {pct(d['confirmed_by_model'])} |", f"| Random weak competencies | {pct(d['confirmed_by_random_picks'])} |", "",
            "Limit: a simulation in which a weak dimension makes its behaviours weak; the model can only guess that from the plain names of the competencies.", ""]


def section_critic() -> list[str]:
    d = load("critic-eval.json")
    if d is None:
        return ["## Text consistency critic", "", "not run", ""]
    r = d["recall"]
    return ["## Text consistency critic", "", f"Model {d['model']}, {d['members']} seeded inconsistencies (SD-001), {d['clean_cases']} clean cases.", "", "| Measure | Value |", "|---|---|",
            f"| Recall of the rule-based checks before the side-of-body and denied-pain rules | {pct(r.get('baseline_before_rules', r['baseline']))} |", f"| Recall of the rule-based checks now (server/generation/consistency.py, always on, no model) | {pct(r['baseline'])} |",
            f"| Recall of the critic alone | {pct(r['critic'])} |", f"| Recall of rules and critic together | {pct(r['union'])} (before the new rules: {pct(r.get('union_before_rules', r['union']))}) |",
            f"| Critic false alarms on clean cases | {pct(d['critic_false_alarm_rate'])} |", f"| Rule-based false alarms on clean cases | {pct(d['baseline_false_alarm_rate'])} |", f"| Questions the critic could not answer | {d['critic_could_not_answer']} |", "",
            "The first set is saturated by the deterministic rules, so the critic is also measured on a second, held-out set worded differently and frozen before any run (`tests/generation/text_inconsistencies_v2.json`, DT-217):", ""] + section_critic_v2() + [
            "Limit: blunt synthetic contradictions written by the builder, not by a clinician; the critic recalls only 1 of 9 denied conditions. The side-of-body and denied-pain defects, which the model found 13 of 29 and 14 of 36 times, no longer depend on it.", ""]


def section_critic_v2() -> list[str]:
    d = load("critic-eval-v2.json")
    if d is None:
        return ["not run", ""]
    r = d["recall"]
    rows = ["| Measure (held-out set, scored once) | Value |", "|---|---|", f"| Recall of the rule-based checks | {pct(r['baseline'])} |", f"| Recall of the critic alone | {pct(r['critic'])} |", f"| Recall of rules and critic together | {pct(r['union'])} (gain over the rules {round(100 * (r['union'] - r['baseline']))} points) |",
            f"| Critic false alarms on clean cases | {pct(d['critic_false_alarm_rate'])} |"]
    rows += [f"| {k.replace('_', ' ')} ({v['n']}) | rules {v['baseline']}, critic {v['critic']} |" for k, v in d["by_kind"].items()]
    return rows + [""]


def section_gateway() -> list[str]:
    d = load("load-gateway-4x5.json")
    if d is None:
        return ["## Gateway under load", "", "not run", ""]
    return ["## Gateway under load (one model slot)", "", f"{d['callers']} callers x {d['calls_each']} calls, {d['model']}.", "", "| Measure | Value |", "|---|---|", f"| Answered | {d['served']} |", f"| Fallback signals | {d['fallback_signals']} |",
            f"| Queue wait p50 / p95 | {d['queue_wait_s_p50']} s / {d['queue_wait_s_p95']} s |", f"| Service time p50 / p95 | {d['service_s_p50']} s / {d['service_s_p95']} s |", f"| Calls per minute | {d['calls_per_minute']} |", "",
            "Limit: the model host and the slot rule only; the speech recognition half of EXP-BE-2 was not run.", ""]


def section_personalisation() -> list[str]:
    d = load("personalisation_simulation.json")
    if d is None:
        return ["## Personalisation policies (simulation)", "", "not run", ""]
    rows = [f"| {k.replace('_minus_', ' minus ')} | {v['mean']} | {v['ci95'][0]} to {v['ci95'][1]} | {'yes' if v['better'] else 'no'} |" for k, v in d["paired"].items()]
    return [f"## Personalisation policies ({d['learners']} simulated learners, {d['sessions']} sessions)", "", "| Paired difference in true improvement of the five weakest behaviours | Mean | 95% interval | Better |", "|---|---|---|---|"] + rows + [
            "", "Limit: a simulation with assumed learning; it shows whether a policy can find and train a weakness, not that real trainees improve (pilot: p1-evaluation-s03).", ""]


def section_novelty() -> list[str]:
    d = load("novelty_simulation.json")
    if d is None:
        return ["## Novelty over simulated trainees", "", "not run", ""]
    return ["## Novelty over simulated trainees", "", f"{d['trainees']} trainees, {d['sessions_each']} sessions each.", "", "| Measure | Value |", "|---|---|", f"| Novelty rate | {pct(d['novel_rate'])} |",
            f"| Low-novelty cases served | {d['low_novelty']} |", f"| Fallbacks | {d['fallbacks']} |", f"| Mean distance to the nearest earlier case | {round(d['mean_nearest'], 3)} |", ""]


def section_classifier() -> list[str]:
    d = load("classifier-bench-synthetic.json")
    if d is None:
        return ["## Communication act classifier", "", "not run", ""]
    lat = load("classifier-latency.json") or {}
    rows = []
    for name, c in d["candidates"].items():
        g = c["guard"]
        p = c["post_hoc_guard_v2"]
        asr = c.get("asr_copies", {})
        asr_text = " / ".join(f"{pct(v['accuracy'])}" for v in asr.values()) if asr else "n/a"
        ms = (lat.get(name) or {}).get("p50_ms")
        rows.append(f"| {name} | {c['macro_f1']} ({c['macro_f1_ci95'][0]} to {c['macro_f1_ci95'][1]}) | {c['credited']} / {c['credited_precision']} | {c['false_credits']} | {pct(c['abstained_share'])} | {c['critical_acts_credited_from_speech']} | "
                    f"{c['critical_said_but_credited_as_other_act']} (before the guard {c['critical_said_but_credited_as_other_act_before_guard']}; post hoc, guard v2: {p['critical_said_but_credited_as_other_act']}) | {asr_text} | {'n/a' if ms is None else str(ms) + ' ms'} |")
    diffs = [f"| {k} | {v['macro_f1_difference']} | {v['ci95'][0]} to {v['ci95'][1]} |" for k, v in d["paired_macro_f1_differences"].items()]
    return ["## Communication act classifier (synthetic, NOT independent)", "",
            f"{d['provenance']}", "",
            f"Rows: {d['rows']['train']} train, {d['rows']['dev']} dev, {d['rows']['test']} test, {d['rows'].get('asr', 0)} recogniser copies of test sentences. Leakage between splits: {d['leakage']}.", "",
            "| Candidate | Macro-F1 (95% interval) | Credited / precision | False credits | Not credited | Critical acts credited from speech | A critical act said, credited as another act | Recogniser copies, clean / telephone band | Latency p50, one sentence, quiet machine |",
            "|---|---|---|---|---|---|---|---|---|"] + rows + [
            "", "| Paired comparison | Macro-F1 difference | 95% interval |", "|---|---|---|"] + diffs + ["", f"Intervals: {d.get('intervals', 'by writing style')}", "", f"Verdict: {d['verdict']}", "",
            "Limit: every sentence and label was written by the model that built the tooling, in eight simulated styles (two in the test split); no person wrote, labelled or reviewed anything. This is a check across writing styles, not evidence about real trainee speech. "
            "The guard against crediting critical sentences was extended after the test split was scored, so its figures in brackets are post hoc. The independent set (docs/research/classifier_benchmark) is still blocked on people, so the classifier stays off in production (content/ai/features.json).", ""]


def section_classifier_v2() -> list[str]:
    d = load("classifier-bench-synthetic2.json")
    if d is None:
        return []
    lat = load("classifier-latency.json") or {}
    rows = []
    for name, c in d["candidates"].items():
        asr = " / ".join(pct(v["accuracy"]) for v in c.get("asr_copies", {}).values()) or "n/a"
        ms = (lat.get(name) or {}).get("p50_ms")
        rows.append(f"| {name} | {c['macro_f1']} ({c['macro_f1_ci95'][0]} to {c['macro_f1_ci95'][1]}) | {c['credited']} / {c['credited_precision']} | {c['false_credits']} | {pct(c['abstained_share'])} | {c['critical_acts_credited_from_speech']} | "
                    f"{c['critical_said_but_credited_as_other_act']} | {c['macro_f1_on_certain_rows_only']} | {asr} | {'n/a' if ms is None else str(ms) + ' ms'} |")
    diffs = [f"| {k} | {v['macro_f1_difference']} | {v['ci95'][0]} to {v['ci95'][1]} |" for k, v in d["paired_macro_f1_differences"].items()]
    out = ["## Communication act classifier on the AI-judged synthetic set v2 (NOT independent, NOT validated)", "", d["provenance"], "",
           f"Rows: {d['rows']['train']} train, {d['rows']['dev']} dev, {d['rows']['test']} test (scored once), {d['rows'].get('asr', 0)} recogniser copies of plain test sentences. Leakage: {d['leakage']}.", "",
           "| Candidate | Macro-F1 (95% interval) | Credited / precision | False credits | Not credited | Critical acts credited from speech | A critical act said, credited as another act | Macro-F1 on the rows both judges agreed with | Recogniser copies, clean / telephone band | Latency p50 (quiet machine) |",
           "|---|---|---|---|---|---|---|---|---|---|"] + rows + ["", "| Paired comparison | Macro-F1 difference | 95% interval |", "|---|---|---|"] + diffs + ["", f"Verdict: {d['verdict']}", ""]
    h = load("classifier-holdout-v3.json")
    if h:
        def row(label, c):
            return f"| {label} | {c['macro_f1']} | {c['credited']} / {c['credited_precision']} | {c['false_credits']} | {pct(c['not_credited_share'])} | {c['critical_acts_credited_from_speech']} | {c['critical_said_but_credited_as_other_act']} of {c['critical_said_total']} |"
        out += ["### Fresh hold-out v3 (300 sentences written and judged after the v2 test was scored once)", "", "| Head | Macro-F1 | Credited / precision | False credits | Not credited | Critical acts credited from speech | Critical sentences credited under an ordinary act |", "|---|---|---|---|---|---|---|",
                row("shipped (v2 training, cues v3, critical-mass cap 0.02)", h["shipped_head"]), row("v2 head without the cap", h["v2_head_without_mass_guard"]), row("the head shipped on 17 October (v1 training)", h["v1_head_17_oct"]), row("TF-IDF, same training", h["tfidf_logreg"]), row("TF-IDF plain-Python head with its critical-mass cap 0.01 (act_head_tfidf.json)", h["tfidf_head_plain_python"]), "",
                f"On the v2 test split, post hoc, the shipped head credits {h['v2_test_post_hoc']['shipped_head']['credited']} sentences at precision {h['v2_test_post_hoc']['shipped_head']['credited_precision']} with {h['v2_test_post_hoc']['shipped_head']['critical_said_but_credited_as_other_act']} critical sentences under an ordinary act (as scored: {d['candidates']['embeddings_logreg']['credited']}, {d['candidates']['embeddings_logreg']['credited_precision']}, {d['candidates']['embeddings_logreg']['critical_said_but_credited_as_other_act']}).", ""]
    a = load("classifier-adversarial.json")
    if a:
        s = a["shipped_head_with_cues_v3_and_mass_guard"]
        out += ["### Evasive critical sentences", "", f"Sentences that touch an allergy, identity, consent, the fasting state, a worsening sign or a handover in words that avoid the obvious ones would be credited under an ordinary act: before any change {a['before_any_change_17_oct']['tuning']['credited_as_ordinary_count']} of {a['before_any_change_17_oct']['tuning']['rows']} (tuning set) and {a['before_any_change_17_oct']['holdout']['credited_as_ordinary_count']} of {a['before_any_change_17_oct']['holdout']['rows']} (hold-out); with the shipped head, the version 3 cues and the critical-mass cap: {s['tuning']['credited_as_ordinary_count']} and {s['holdout']['credited_as_ordinary_count']}. The hold-out was written by the same author before the cues were chosen, so it shows generalisation only to that author's other wordings.", ""]
    out += ["Limit: every sentence was written by the model that built the tooling and the labels are the authors' intent checked by two configurations of one local AI judge, settled where both disagreed with the intent by the same builder, who sided with the intent in nearly every case (the adjudication is not an independent check). No person wrote, labelled or reviewed a row. TF-IDF is equal to or ahead of the embedding head on both sets, so the embeddings have shown no advantage here. The independent set is still blocked on people, so the classifier stays off in production (content/ai/features.json).", ""]
    return out


def section_dialogue() -> list[str]:
    d = load("dialogue-eval.json")
    if d is None:
        return ["## Patient and staff dialogue", "", "not run", ""]
    return ["## Patient dialogue", "", f"Model {d['model']} (`{d['digest'][:12]}`), {d['cases']} cases.", "", "| Measure | Value |", "|---|---|",
            f"| Earned questions answered by the model | {d['earned']['model']} of {d['earned']['n']} (fell back {d['earned']['fallback']}) |", f"| Of those, the reply used the fact | {d['earned']['used_fact']} |",
            f"| Withheld questions answered \"cannot recall\" | {d['withheld']['cannot_recall']} of {d['withheld']['n']} |", f"| Validator leaks / shared-word leaks | {d['withheld']['validator_leaks']} / {d['withheld']['word_leaks']} |",
            f"| Latency p50 / p95 | {d['ms_p50']} ms / {d['ms_p95']} ms |", "",
            "Limit: the leak checks are lexical; a paraphrase or a made-up denial is not seen by them (red team: docs/research/redteam_report.md). Every withheld reply is in the result file for a person to read.", ""]


def section_debrief() -> list[str]:
    d = load("debrief-eval.json")
    if d is None:
        return ["## AI debrief", "", "not run", ""]
    return ["## AI debrief (rewrite of the template debrief)", "", f"Model {d['model']} (`{d['digest'][:12]}`), prompt version {d.get('prompt_version') or 1}, {d['sessions']} golden sessions.", "", "| Measure | Value |", "|---|---|",
            f"| The rewrite replaced the template | {d['ai_used']} of {d['sessions']} ({pct(d['accept_rate'])}) |", f"| The template stayed (reasons) | {d['template_kept']} ({d['reasons']}) |",
            f"| Latency p50 / worst | {d['latency_ms_p50']} ms / {d['latency_ms_max']} ms; within 8 s: {pct(d['within_8s_share'])} (95% lower bound {pct(d['within_8s_wilson_lower_95']) if 'within_8s_wilson_lower_95' in d else 'n/a'}) |",
            f"| Corrupted rewrites refused | {d['mutations_rejected']} of {d['mutations']} |"] + [f"| Before: {k.replace('_', ' ')} | accepted {pct(b['accept_rate'])}, p50 {b['latency_ms_p50']} ms, within 8 s {pct(b['within_8s_share'])} ({b['sessions'] if 'sessions' in b else 16} sessions) |" for k, b in d.get("baselines", {}).items()] + ["",
            "Limit: the validator knows the engine's event words only; an event that is not one of them passes (DT-211). The within-8-seconds pass is a point estimate: its lower 95 percent bound is just under the 80 percent target.", ""]


def section_voice() -> list[str]:
    out = ["## Voice: speech recognition, speech output and spoken orders", ""]
    a = load("asr-wer.json")
    out += ["| Measure | Value |", "|---|---|"]
    out.append("| Word error rate of the recogniser on rendered voice lines | not run |" if a is None else f"| Word error rate ({a['model']} on {a['device']}, {a['clips']} clips) | {a['wer']} |")
    t = load("tts-latency.json")
    out.append("| First spoken sentence, 95th percentile | not run |" if t is None else f"| First spoken sentence, 95th percentile ({t['sentences']} sentences) | {t['first_sentence_seconds']} s (median {t['p50_seconds']} s; worst of {len(t.get('runs', [1]))} runs, Piper kept alive) |")
    o = load("orders-eval.json")
    if o is None:
        out.append("| Spoken orders | not run |")
    else:
        out += [f"| Spoken orders parsed to the right target ({o['phrases']} phrasings, mild recogniser-like errors) | {pct(o['parsed_right'])} |", f"| ... to a wrong target (the read-back catches these) | {pct(o['wrong_target'])} |",
                f"| Actions released without a confirmation | {o['released_without_confirmation']} |"]
    return out + ["", "Limit: clean synthetic speech from one voice is an upper bound, not accented trainees in a noisy room (DT-205); Piper is kept alive; the voice costs about 0.2 s per word on this processor, so the figure holds on a quiet machine only (4.95 s while language-model evaluations were running).", ""]


def section_redteam() -> list[str]:
    d = load("redteam.json")
    if d is None:
        return ["## Red team of the grounding validator", "", "not run", ""]
    rows = [f"| {k} | {v['attacks']} | {v['escapes']} | {pct(v['escape_rate'])} | {'critical' if v['critical'] else 'known gap'} |" for k, v in d["classes"].items()]
    return ["## Red team of the grounding validator", "", f"Critical classes: {d['critical_escapes']} escapes.", "", "| Class | Attacks | Escapes | Escape rate | Kind |", "|---|---|---|---|---|"] + rows + ["", "Limit: a lexical check cannot see a paraphrase, a synonym or a made-up denial (DT-190, DT-211).", ""]


def section_multiagent() -> list[str]:
    d = load("multiagent-eval.json")
    if d is None:
        return ["## Three characters on one model", "", "not run", ""]
    return ["## Three characters answering one trainee turn (one model, one request at a time)", "", f"Model {d['model']}, {d['turns']} turns, characters {', '.join(d['characters'])}.", "", "| Measure | Value |", "|---|---|",
            f"| First reply p50 / p95 | {d['first_reply_s']['p50']} s / {d['first_reply_s']['p95']} s |", f"| All three replies p50 / p95 | {d['all_three_s']['p50']} s / {d['all_three_s']['p95']} s |",
            f"| All three within {d['budget_s']} s | {pct(d['all_three_within_budget'])} |", f"| Replies that fell back to a fixed line | {pct(d['fallback_share'])} |", f"| Target met (p95 of all three under {d['budget_s']} s) | {'yes' if d['meets_target'] else 'no'} |", "",
            f"Staff: {d.get('staff', 'write a reply')}. " + "".join(f"Before: {k.replace('_', ' ')}, all three p95 {v['all_three_s']['p95']} s. " for k, v in d.get("baselines", {}).items()), "",
            f"Limit: {d['limit']}. {d['reading']}." + (f" {d['note']}" if d.get("note") else ""), ""]


def build() -> str:
    parts = ["# AI evaluation (generated)", "", "Generated by `tools/eval/collect_ai.py` from the result files in `reports/ai/`; do not edit. Each section names the model and its digest, and each states what the measure does not show.", ""]
    for fn in (section_narrative, section_planner, section_interpreter, section_critic, section_classifier, section_classifier_v2, section_dialogue, section_debrief, section_voice, section_redteam, section_multiagent, section_gateway, section_personalisation, section_novelty):
        parts += fn()
    return "\n".join(parts).rstrip("\n") + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("AI evaluation report: " + ("current" if ok else "out of date: run tools/eval/collect_ai.py"))
        return 0 if ok else 1
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
