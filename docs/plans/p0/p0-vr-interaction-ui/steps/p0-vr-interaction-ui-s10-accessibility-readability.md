# p0-vr-interaction-ui-s10 — Accessibility and readability (EXP-UI-2)

Milestone: **M7 S1 complete in VR**

## What
Minimum text size (≥ 24 px equivalent at 1 m), contrast AA, subtitles size/position, left-hand mode, seated mode, colour-blind-safe status colours (shape/text, not colour alone), UI scale setting persisted.

## Why
Readable and usable for every trainee in a headset.

## How to implement
1. Run EXP-UI-2 (sizes at arm's length and desk distance); write `ui-guidelines.md`; apply to all panels.
2. Settings panel: UI scale, subtitles, handedness.
3. Test sizes 18/24/32 px equivalent at 1 m and at desk distance (0.6 m) with 3 users; choose minimums.
4. Contrast check of every panel colour pair against WCAG AA.
5. Status never by colour alone: icons + text for normal/abnormal/critical.
6. Settings persisted with PlayerPrefs (per-viewer convenience only).

## Verification
`ui-guidelines.md` written; T-UI2 legible at 1 m; a settings change persists across restarts.

## Files changed
`docs/plans/p0/p0-vr-interaction-ui/ui-guidelines.md`, settings panel.

## Dependencies
p0-vr-interaction-ui-s03.

## Common issues
World-space text blurs at a distance → use TextMeshPro SDF and adequate canvas scale; dynamic resolution can soften text — test on device.

## Sources
Implementation Guide B-S10 Step 5, config (text ≥ 24 px at 1 m, contrast AA), T-UI2.
