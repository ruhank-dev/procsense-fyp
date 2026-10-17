# p1-ai-infrastructure-s03 — Deployment topology, remote option and caching

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**


## What
Decide from the benchmark: split mode (inference on the desktop, Unity on the laptop, LAN), single-machine mode (small models or CPU plus the pool), and an optional remote inference mode for offline batch jobs only, never with personal data; caching of generated packages and lines.

## Why
Hardware limits are handled by design, not by dropping features.

## How to implement
1. Topology document and config switches.
2. Fallback matrix per capability.
3. Network failure tests.

## Status (17 October 2026)

- **Built:** `docs/architecture/deployment.md`: the four modes (split, single machine, no model host, remote batch for offline jobs without personal data), the fallback matrix per capability with the test that covers each, the tested failure behaviour, and caching. Config switch: `OLLAMA_URL` (empty means every model call answers with the fallback signal). Network failure tests are in `tools/ci/test_llm.py` and `server/tests/test_ai_gateway.py`.

## Verification
Each AI capability has a deployment and a tested fallback.

## Files changed
`docs/architecture/deployment.md`, service config

## Dependencies
p1-ai-infrastructure-s02

## Common issues
LAN latency spikes: pool and timeouts.

## Sources
Feature E69.
