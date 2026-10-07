# p2-ai-extensions-s02 — Retrieval / embeddings for grounding

Milestone: **M13 P2 extensions**

## What
A small local embedding model and a flat index over approved content (case facts, explain texts, glossary), used to select context for dialogue and debrief instead of passing everything — measured against the grounding eval.

## Why
Less prompt text means lower latency on the 6 GB GPU and potentially fewer grounding errors.

## How to implement
1. Index build at content-package time; eval impact.
2. Embed approved content chunks (case facts, explain texts, glossary) with a small local embedding model on the desktop GPU; store a flat index (numpy) — no vector DB unless needed.
3. Use retrieval to select context for dialogue and debrief instead of passing everything.
4. Measure grounding failures and token count before/after on the eval suite.

## Verification
Grounding eval no worse and prompt tokens reduced — or decision recorded not to ship.

## Files changed
`server/retrieval/*`, index build script, eval report.

## Dependencies
p1-ai-language-s04.

## Common issues
Retrieval that misses a needed fact causes 'I don't know' answers — measure both leaks and unhelpful answers.

## Sources
Master Plan §26 (component 5: embeddings/retrieval, P2); Implementation Guide B-R2.
