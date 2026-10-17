# p1-ai-infrastructure-s05 — Unity-side AI hooks (specification for when Unity work is approved)

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**


## What
Specify the Unity work the AI features need: AI client (async calls, timeouts, fallback switch), push-to-talk voice capture and playback with lip sync, subtitles, confidence prompts at decision points, 'explain this result' and 'why this case' panels, package download and pool loading, frame-time budget while models run.

## Why
Features must not be blocked by missing Unity hooks discovered late.

## How to implement
1. One spec section per hook with inputs, outputs and fallbacks.
2. EditMode test list for each hook.

## Status (17 October 2026)

- **Built:** `docs/architecture/unity_ai_hooks.md`: twelve hooks with inputs, outputs, timeouts, fallbacks, the server or engine half that exists and the EditMode tests to write. A specification only; nothing was run on the Quest (Sannan's side).

## Verification
Spec reviewed; each hook mapped to the features that need it.

## Files changed
`docs/architecture/unity_ai_hooks.md`

## Dependencies
p1-ai-infrastructure-s03

## Common issues
Main-thread blocking: all AI calls async.

## Sources
Features C31, C35, D41, B22, D68, A2.
