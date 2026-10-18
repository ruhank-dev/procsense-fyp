# p2-ai-extensions-s03 — Distilled small classifier (EXP-P2AI-2)

Milestone: **M13 P2 extensions**

## What
Replace the LLM-based communication classifier with a small, fast model trained on the human-labelled set (plus flagged LLM-labelled extra data), keeping the frozen test split and the critical-act guard.

## Why
A small classifier is cheaper and faster (no GPU contention with dialogue) and works with lower latency.

## How to implement
1. Train; compare F1 on the frozen test split.
2. Training data: human-labelled split + LLM-labelled extra data (flagged), frozen test split unchanged.
3. Model: small text classifier (e.g. fine-tuned compact transformer or logistic regression on embeddings) — start with the simplest that meets F1.
4. Compare macro-F1 and latency vs the LLM classifier; keep the critical-act guard.

## Status (17 October 2026)

- **Built:** the small classifier of this step is the embeddings + logistic regression head (`server/classify/embedding.py`, `content/assessment/act_head.json`); see `p1-ai-language-s07` for the results of three candidates on two synthetic sets and the adversarial check. It is off in production until an independent set exists (DT-210).

## Verification
EXP-P2AI-2 table; ship only within the agreed F1 margin.

## Files changed
`tools/classifier/train.py`, model artefact (dev-only), comparison report, ADR.

## Dependencies
p1-ai-language-s07.

## Common issues
Training on LLM labels can copy the LLM's mistakes — evaluate only on the human-labelled test split.

## Sources
Master Plan §26 (multi-model table, component 4: LLM-based P1 → small classifier P2); Implementation Guide B-M6, B-R8.
