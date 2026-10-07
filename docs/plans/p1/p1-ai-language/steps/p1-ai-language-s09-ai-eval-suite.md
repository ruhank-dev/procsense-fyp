# p1-ai-language-s09 — AI eval suite and feature gate

Milestone: **M12 P1 evaluation**

## What
One harness running every AI suite (grounding, disclosure, schema, latency, classifier, debrief mutation, ASR WER) against thresholds in `thresholds.yaml`, on a weekly schedule with trend charts; any feature below threshold is switched off by default in `ai_features.yaml`.

## Why
Models, prompts and content change every week; only a scheduled, automatic evaluation catches a regression (e.g. a new prompt that leaks hidden facts) before a trainee sees it — and the gate keeps weak features switched off.

## How to implement
1. Suite registry; endpoint down → SKIPPED, never PASS (T-EV3).
2. Warm-up before timing (flaky latency).
3. Schedule: weekly run on the desktop (Windows Task Scheduler); results committed to `reports/`.
4. Gate updates `ai_features.yaml` automatically; you approve the PR.

## Verification
Weekly report present; a deliberately bad output makes the suite fail (T-EV1).

## Files changed
`tools/ai_eval/run.py`, `tools/ai_eval/suites/*.py`, `tools/ai_eval/thresholds.yaml`, `ai_features.yaml`, `reports/eval_<date>.{json,html}`.

## Dependencies
p1-ai-language-s08.

## Common issues
Latency measured on a cold model is unfair → warm-up requests before timing; endpoint down must report SKIPPED, never PASS.

## Sources
Implementation Guide B-M5 Steps 1–4, T-EV1–T-EV4; Master Plan §37 (requirements matrix), §41.
