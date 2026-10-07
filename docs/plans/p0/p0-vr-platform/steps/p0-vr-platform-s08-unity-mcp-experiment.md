# p0-vr-platform-s08 — Experiment EXP-VR-4: Unity MCP bridge for Claude

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Evaluate a community Unity MCP server (e.g. CoplayDev `unity-mcp` — not yet verified) that would let Claude inspect and edit the open Unity Editor: list scene objects, read/set component values, create prefabs, run tests, capture the Game view.

## Why
Claude can't see or click the Unity Editor; if a reliable bridge exists, scene inspection and wiring could happen directly instead of through editor scripts and your screenshots — a big time saver if (and only if) it's dependable.

## How to implement
1. Check the tool first: repo, maintenance, licence, what it can modify; read its install instructions fully.
2. Commit before trying (any scene change must be revertible).
3. Run 5 real tasks from the skeleton work; record success/failure per task.
4. Adopt if ≥ 4/5 succeed; otherwise keep editor scripts + screenshots.

## Verification
Decision recorded in the vr-platform README (EXP-VR-4) with the 5 task results.

## Files changed
`docs/plans/p0/p0-vr-platform/README.md` (EXP-VR-4 results table), MCP server config (only if adopted), `docs/decisions/ADR-xxxx-unity-mcp.md`.

## Dependencies
p0-vr-platform-s01.

## Common issues
Never let a tool change scenes without a clean git state first.

## Sources
working-model.md.
